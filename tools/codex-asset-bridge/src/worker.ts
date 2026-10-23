import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { fileURLToPath } from "node:url";
import { BridgePaths, HEARTBEAT_MS, POLL_MS, resolvePaths } from "./config.js";
import { writeJsonAtomic } from "./fsutil.js";
import { buildFixPrompt, buildRunPrompt } from "./prompt.js";
import { CodexRunner, SdkCodexRunner } from "./runner.js";
import {
  appendEvent, budgetExhausted, cancelFile, heartbeatFile, loadTask, revisionDir, RunRecord, saveTask,
  TaskState, TaskStatus, transition, workerLockFile,
} from "./task.js";
import { buildManifest, listGeneratedImages } from "./validate.js";

const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms));

function log(p: BridgePaths, id: string, msg: string) {
  fs.mkdirSync(p.tasksDir, { recursive: true });
  fs.appendFileSync(path.join(p.tasksDir, `${id}.log`), `${new Date().toISOString()} ${msg}\n`, "utf8");
}

/** Take the per-task worker lock so two workers can never run the same task. */
function acquireWorkerLock(p: BridgePaths, id: string): boolean {
  const file = workerLockFile(p, id);
  fs.mkdirSync(p.tasksDir, { recursive: true });
  try {
    fs.writeFileSync(file, String(process.pid), { flag: "wx" });
    return true;
  } catch (err: any) {
    if (err?.code !== "EEXIST") throw err;
    // A lock whose heartbeat stopped long ago belongs to a dead worker.
    const age = Date.now() - fs.statSync(file).mtimeMs;
    if (age > 4 * HEARTBEAT_MS) {
      fs.rmSync(file, { force: true });
      return acquireWorkerLock(p, id);
    }
    return false;
  }
}

export async function runTask(p: BridgePaths, id: string, runner: CodexRunner): Promise<TaskState> {
  if (!acquireWorkerLock(p, id)) throw new Error(`Another worker is already running task ${id}`);
  const nonce = crypto.randomBytes(8).toString("hex");
  const startedAt = new Date().toISOString();
  let controller: AbortController | null = null;
  let cancelRequested = false;
  const beat = async () => {
    await writeJsonAtomic(heartbeatFile(p, id), { pid: process.pid, nonce, started_at: startedAt, at: new Date().toISOString() });
    const now = new Date();
    try {
      fs.utimesSync(workerLockFile(p, id), now, now);
    } catch {
      /* lock removed by cleanup */
    }
    if (fs.existsSync(cancelFile(p, id)) && !cancelRequested) {
      cancelRequested = true;
      controller?.abort("cancelled");
    }
  };
  await beat();
  const timer = setInterval(() => void beat().catch(() => undefined), HEARTBEAT_MS);

  let t = await loadTask(p, id);
  const save = async (next: TaskState) => (t = await saveTask(p, next, t.version));
  const finish = async (status: TaskStatus, detail: string) => {
    await save(transition(t, status, detail));
    appendEvent(p, id, { type: "task", status, detail });
    log(p, id, `${status}: ${detail}`);
  };

  try {
    if (t.status !== "queued") throw new Error(`Task ${id} is ${t.status}, not queued`);
    await save(transition(t, "running", `revision ${t.revision}`));
    const planOnly = !t.authorization.generation_authorized;
    const workDir = revisionDir(p, id, t.revision);
    fs.mkdirSync(workDir, { recursive: true });
    const previousDir = t.revision > 1 ? revisionDir(p, id, t.revision - 1) : null;

    let prompt = buildRunPrompt(t, workDir, previousDir);
    let validationRetries = 0;
    let revisionImages = 0;
    for (let attempt = 0; ; attempt++) {
      const exhausted = budgetExhausted(t);
      if (exhausted) return await finish("failed", `budget: ${exhausted}`), t;
      if (cancelRequested) return await finish("cancelled", "cancel requested"), t;

      const before = new Set(listGeneratedImages(p.generatedImagesDir, t.thread_id));
      controller = new AbortController();
      const ctl = controller;
      let overBudget = false;
      const timeout = setTimeout(() => ctl.abort("timeout"), t.budget.run_timeout_minutes * 60_000);
      // Image budget is enforced while the turn runs: the SDK emits no image events, so poll the folder.
      let threadForPoll = t.thread_id;
      const poll = setInterval(() => {
        const fresh = listGeneratedImages(p.generatedImagesDir, threadForPoll).filter((f) => !before.has(f)).length;
        if ((planOnly && fresh > 0) || t.totals.images + fresh > t.budget.max_images) {
          overBudget = true;
          ctl.abort(planOnly ? "policy" : "over_budget");
        }
      }, POLL_MS);

      const run: RunRecord = { revision: t.revision, attempt, started_at: new Date().toISOString() };
      log(p, id, `run r${t.revision} attempt ${attempt} (plan_only=${planOnly})`);
      const result = await runner.runTurn({
        threadId: t.thread_id,
        workDir,
        prompt,
        planOnly,
        signal: ctl.signal,
        onThreadId: (tid) => {
          threadForPoll = tid;
          if (!t.thread_id) void save({ ...t, thread_id: tid }).catch((e) => log(p, id, `save thread id failed: ${e}`));
        },
        onEvent: (e) => appendEvent(p, id, e),
      });
      clearTimeout(timeout);
      clearInterval(poll);
      // Let a pending thread-id save land before the next write.
      await sleep(50);
      t = await loadTask(p, id);
      const threadId = result.threadId ?? t.thread_id;

      // Count and archive what image_gen produced during this turn.
      const fresh = listGeneratedImages(p.generatedImagesDir, threadId).filter((f) => !before.has(f));
      if (fresh.length) {
        const src = path.join(workDir, "_sources");
        fs.mkdirSync(src, { recursive: true });
        for (const f of fresh) fs.copyFileSync(f, path.join(src, `codex-${path.basename(f)}`));
      }
      revisionImages += fresh.length;
      run.images_generated = fresh.length;
      run.usage = result.usage ?? "unknown";
      run.ended_at = new Date().toISOString();
      const totals = {
        tokens: t.totals.tokens + (result.usage ? result.usage.input_tokens + result.usage.output_tokens : 0),
        images: t.totals.images + fresh.length,
        unknown_usage_runs: t.totals.unknown_usage_runs + (result.usage ? 0 : 1),
      };

      const aborted = ctl.signal.aborted ? String(ctl.signal.reason) : null;
      if (aborted === "cancelled" || cancelRequested) {
        run.outcome = "cancelled";
        await save({ ...t, thread_id: threadId, runs: [...t.runs, run], totals });
        return await finish("cancelled", "cancel requested"), t;
      }
      if (overBudget || aborted === "over_budget" || aborted === "policy") {
        run.outcome = planOnly ? "policy_violation" : "over_budget";
        await save({ ...t, thread_id: threadId, runs: [...t.runs, run], totals });
        const why = planOnly ? "policy violation: an image was generated in a plan-only run" : `image budget exceeded (${totals.images}/${t.budget.max_images})`;
        return await finish("failed", why), t;
      }
      if (result.error || !result.report) {
        run.outcome = aborted === "timeout" ? "timeout" : "error";
        run.error = result.error ?? `no structured answer: ${result.rawFinal.slice(0, 300)}`;
        await save({ ...t, thread_id: threadId, runs: [...t.runs, run], totals });
        if (attempt < t.budget.max_retries) {
          prompt = threadId
            ? `The previous attempt ended with an error (${run.error}). Continue the task in ${workDir} and finish with the JSON answer.`
            : buildRunPrompt(t, workDir, previousDir);
          continue;
        }
        return await finish("failed", `Codex run failed after ${attempt + 1} attempt(s): ${run.error}`), t;
      }
      if (result.report.status === "blocked") {
        run.outcome = "blocked";
        await save({ ...t, thread_id: threadId, runs: [...t.runs, run], totals });
        return await finish("blocked", result.report.blocked_reason || result.report.summary), t;
      }

      const manifest = buildManifest({
        dir: workDir,
        revision: t.revision,
        planOnly,
        expected: t.spec.expected_files,
        codexReport: result.report,
        imagesGenerated: revisionImages,
      });
      await writeJsonAtomic(path.join(workDir, "manifest.json"), manifest);
      run.outcome = manifest.ok ? "ok" : "validation_failed";
      await save({ ...t, thread_id: threadId, runs: [...t.runs, run], totals, manifest });
      if (manifest.ok) {
        return await finish("review", planOnly ? "plan-only: prompts written, no images generated" : "ready for review"), t;
      }
      // Validation failures are deterministic: feed them back once, then stop.
      if (validationRetries < 1 && attempt < t.budget.max_retries && !planOnly) {
        validationRetries++;
        prompt = buildFixPrompt(manifest.problems, workDir);
        continue;
      }
      return await finish("failed", `validation failed: ${manifest.problems.slice(0, 5).join("; ")}`), t;
    }
  } catch (err: any) {
    log(p, id, `worker error: ${err?.stack ?? err}`);
    try {
      t = await loadTask(p, id);
      if (t.status === "running" || t.status === "queued") await finish("failed", `worker error: ${err?.message ?? err}`);
    } catch {
      /* nothing more we can do */
    }
    return t;
  } finally {
    clearInterval(timer);
    fs.rmSync(cancelFile(p, id), { force: true });
    fs.rmSync(heartbeatFile(p, id), { force: true });
    fs.rmSync(workerLockFile(p, id), { force: true });
  }
}

// Entry point: node dist/src/worker.js <taskId>
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const id = process.argv[2];
  const p = resolvePaths();
  runTask(p, id, new SdkCodexRunner())
    .then(() => process.exit(0))
    .catch((err) => {
      log(p, id ?? "unknown", `fatal: ${err?.stack ?? err}`);
      process.exit(1);
    });
}
