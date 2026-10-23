import { test } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { PNG } from "pngjs";
import { BridgePaths } from "../src/config.js";
import { Bridge } from "../src/bridge.js";
import { safeJoin } from "../src/fsutil.js";
import { inspectPng } from "../src/png.js";
import { CodexRunner, TurnRequest, TurnResult } from "../src/runner.js";
import { heartbeatFile, loadTask, saveTask, TaskSpec } from "../src/task.js";
import { runTask } from "../src/worker.js";

function tempPaths(): BridgePaths {
  const base = fs.mkdtempSync(path.join(os.tmpdir(), "cab-test-"));
  const artRoot = path.join(base, "art");
  fs.mkdirSync(artRoot, { recursive: true });
  return {
    root: path.join(artRoot, "_bridge"),
    tasksDir: path.join(artRoot, "_bridge", "tasks"),
    stagingDir: path.join(artRoot, "_bridge", "staging"),
    artRoot,
    generatedImagesDir: path.join(base, "generated_images"),
  };
}

function writePng(file: string, w: number, h: number, alpha: (x: number, y: number) => number) {
  const png = new PNG({ width: w, height: h });
  for (let y = 0; y < h; y++)
    for (let x = 0; x < w; x++) {
      const i = (y * w + x) * 4;
      png.data[i] = 200;
      png.data[i + 1] = 120;
      png.data[i + 2] = 40;
      png.data[i + 3] = alpha(x, y);
    }
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, PNG.sync.write(png));
}

const spec = (over: Partial<TaskSpec> = {}): TaskSpec => ({
  task_name: "test-portrait",
  title: "测试立绘",
  asset_type: "character_portrait",
  brief: "Draw a test portrait with a transparent background.",
  expected_files: [{ name: "PORTRAIT.png", width: 32, height: 48, transparent: true, binary_alpha: true }],
  reference_paths: [],
  target_dir: "test-portrait",
  ...over,
});

const report = { status: "done", summary: "ok", files: [], self_checks: [], risks: [], blocked_reason: "" };
const usage = { input_tokens: 1000, cached_input_tokens: 0, output_tokens: 100, reasoning_output_tokens: 0 };

/** Fake Codex: each turn runs the next scripted step. */
class FakeRunner implements CodexRunner {
  prompts: string[] = [];
  constructor(private readonly paths: BridgePaths, private readonly steps: ((req: TurnRequest, threadId: string) => Partial<TurnResult> | void)[]) {}
  async runTurn(req: TurnRequest): Promise<TurnResult> {
    this.prompts.push(req.prompt);
    const threadId = req.threadId ?? "thread-fake-1";
    if (!req.threadId) req.onThreadId(threadId);
    req.onEvent({ type: "message", text: "working" });
    const step = this.steps[this.prompts.length - 1] ?? (() => undefined);
    const extra = step(req, threadId) ?? {};
    return { threadId, report: report as any, rawFinal: JSON.stringify(report), usage, ...extra };
  }
  genImage(threadId: string, name: string) {
    writePng(path.join(this.paths.generatedImagesDir, threadId, name), 8, 8, () => 255);
  }
}

const goodPortrait = (req: TurnRequest) => {
  writePng(path.join(req.workDir, "PORTRAIT.png"), 32, 48, (x) => (x < 4 ? 0 : 255));
  fs.writeFileSync(path.join(req.workDir, "AGENTS.md"), "codex notes");
};

async function generateAndRun(p: BridgePaths, runner: CodexRunner, authorized = true, budget = {}) {
  const bridge = new Bridge(p, () => undefined);
  const t = await bridge.generate({
    spec: spec(),
    authorization: { generation_authorized: authorized, user_consent: authorized ? "tester: yes" : undefined },
    budget,
  });
  const done = await runTask(p, t.id, runner);
  return { bridge, id: t.id, done };
}

test("safeJoin refuses escapes and absolute paths", () => {
  const base = fs.mkdtempSync(path.join(os.tmpdir(), "cab-join-"));
  assert.throws(() => safeJoin(base, "../outside"));
  assert.throws(() => safeJoin(base, "a/../../outside"));
  assert.throws(() => safeJoin(base, path.join(os.tmpdir(), "x")));
  assert.equal(safeJoin(base, "ok/file.png"), path.join(base, "ok", "file.png"));
});

test("PNG inspection reads size and binary alpha", () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "cab-png-"));
  writePng(path.join(dir, "a.png"), 10, 6, (x) => (x < 2 ? 0 : 255));
  writePng(path.join(dir, "b.png"), 10, 6, () => 128);
  const a = inspectPng(path.join(dir, "a.png"));
  assert.equal(a.width, 10);
  assert.equal(a.height, 6);
  assert.equal(a.binaryAlpha, true);
  assert.ok(a.transparentRatio > 0);
  assert.equal(inspectPng(path.join(dir, "b.png")).binaryAlpha, false);
});

test("generation needs quoted consent and never targets a non-empty folder", async () => {
  const p = tempPaths();
  const bridge = new Bridge(p, () => undefined);
  await assert.rejects(bridge.generate({ spec: spec(), authorization: { generation_authorized: true } }), /user_consent/);
  fs.mkdirSync(path.join(p.artRoot, "taken"));
  fs.writeFileSync(path.join(p.artRoot, "taken", "old.png"), "keep me");
  await assert.rejects(bridge.generate({ spec: spec({ target_dir: "taken" }), authorization: { generation_authorized: false } }), /never be overwritten/);
  await assert.rejects(bridge.generate({ spec: spec({ target_dir: "../escape" }), authorization: { generation_authorized: false } }), /escapes/);
  assert.equal(fs.readFileSync(path.join(p.artRoot, "taken", "old.png"), "utf8"), "keep me");
});

test("happy path: generate, review, preview gate, approve without overwriting", async () => {
  const p = tempPaths();
  let runner!: FakeRunner;
  runner = new FakeRunner(p, [(req, tid) => (runner.genImage(tid, "exec-1.png"), goodPortrait(req))]);
  const { bridge, id, done } = await generateAndRun(p, runner);
  assert.equal(done.status, "review", done.status_detail ?? "");
  assert.equal(done.thread_id, "thread-fake-1");
  assert.equal(done.totals.images, 1);
  assert.equal(done.totals.tokens, 1100);
  assert.ok(fs.existsSync(path.join(p.stagingDir, id, "r1", "_sources", "codex-exec-1.png")));
  assert.deepEqual(done.manifest?.deliverables, ["PORTRAIT.png"]);

  await assert.rejects(bridge.approve(id), /asset_preview/);
  const pv = await bridge.preview(id);
  assert.equal(pv.images.length, 1);
  const approved = await bridge.approve(id, "looks right");
  assert.equal(approved.status, "approved");
  const target = path.join(p.artRoot, "test-portrait");
  assert.ok(fs.existsSync(path.join(target, "PORTRAIT.png")));
  assert.match(fs.readFileSync(path.join(target, "CODEX_STATUS.txt"), "utf8"), /^STATUS: DONE/);
  await assert.rejects(bridge.approve(id), /only tasks in review/);
});

test("validation failure is fed back once, then the task fails", async () => {
  const p = tempPaths();
  const wrong = (req: TurnRequest) => writePng(path.join(req.workDir, "PORTRAIT.png"), 30, 48, () => 255);
  const runner = new FakeRunner(p, [wrong, wrong, wrong]);
  const { done } = await generateAndRun(p, runner);
  assert.equal(done.status, "failed");
  assert.equal(runner.prompts.length, 2);
  assert.match(runner.prompts[1], /width 30, expected 32/);
});

test("a fix after validation feedback reaches review", async () => {
  const p = tempPaths();
  const wrong = (req: TurnRequest) => writePng(path.join(req.workDir, "PORTRAIT.png"), 32, 48, () => 255);
  const runner = new FakeRunner(p, [wrong, goodPortrait]);
  const { done } = await generateAndRun(p, runner);
  assert.equal(done.status, "review");
  assert.equal(done.runs.length, 2);
});

test("plan-only run that generates an image is a policy violation", async () => {
  const p = tempPaths();
  let runner!: FakeRunner;
  runner = new FakeRunner(p, [
    (req, tid) => {
      fs.mkdirSync(path.join(req.workDir, "_prompts"), { recursive: true });
      fs.writeFileSync(path.join(req.workDir, "_prompts", "main.txt"), "prompt");
      runner.genImage(tid, "exec-sneaky.png");
    },
  ]);
  const { done } = await generateAndRun(p, runner, false);
  assert.equal(done.status, "failed");
  assert.match(done.status_detail ?? "", /policy violation/);
});

test("plan-only run that writes prompts reaches review but cannot be approved", async () => {
  const p = tempPaths();
  const runner = new FakeRunner(p, [
    (req) => {
      fs.mkdirSync(path.join(req.workDir, "_prompts"), { recursive: true });
      fs.writeFileSync(path.join(req.workDir, "_prompts", "PLAN.md"), "plan");
    },
  ]);
  const { bridge, id, done } = await generateAndRun(p, runner, false);
  assert.equal(done.status, "review");
  await bridge.preview(id);
  await assert.rejects(bridge.approve(id), /plan-only/);
});

test("Codex errors are retried within max_retries", async () => {
  const p = tempPaths();
  const runner = new FakeRunner(p, [() => ({ error: "boom", report: null, usage: null }), goodPortrait]);
  const { done } = await generateAndRun(p, runner);
  assert.equal(done.status, "review");
  assert.equal(done.totals.unknown_usage_runs, 1);
});

test("blocked report ends in blocked, and revise continues on the same thread", async () => {
  const p = tempPaths();
  const blocked = { ...report, status: "blocked", blocked_reason: "reference missing" };
  const runner = new FakeRunner(p, [() => ({ report: blocked as any }), goodPortrait]);
  const { bridge, id, done } = await generateAndRun(p, runner);
  assert.equal(done.status, "blocked");
  assert.equal(done.status_detail, "reference missing");
  const queued = await bridge.revise(id, "The reference is now at X; please continue.");
  assert.equal(queued.revision, 2);
  const again = await runTask(p, id, runner);
  assert.equal(again.status, "review");
  assert.match(runner.prompts[1], /reference is now at X/);
});

test("token budget stops the task before another run", async () => {
  const p = tempPaths();
  const runner = new FakeRunner(p, [() => ({ error: "boom", report: null })]);
  const { done } = await generateAndRun(p, runner, true, { max_total_tokens: 10_000, max_retries: 5 });
  // 1100 tokens per run: the budget check stops it well before 5 retries would.
  assert.equal(done.status, "failed");
  assert.ok(done.runs.length <= 6);
});

test("a running task whose worker vanished is marked failed", async () => {
  const p = tempPaths();
  const bridge = new Bridge(p, () => undefined);
  const t = await bridge.generate({ spec: spec(), authorization: { generation_authorized: false } });
  const stale = { ...(await loadTask(p, t.id)), status: "running" as const, updated_at: new Date(Date.now() - 10 * 60_000).toISOString() };
  fs.writeFileSync(path.join(p.tasksDir, `${t.id}.json`), JSON.stringify({ ...stale, updated_at: stale.updated_at }));
  fs.writeFileSync(heartbeatFile(p, t.id), JSON.stringify({ pid: 999999, nonce: "x", started_at: stale.updated_at, at: stale.updated_at }));
  const s = await bridge.status(t.id);
  assert.equal(s.task.status, "failed");
});

test("optimistic version check rejects a lost update", async () => {
  const p = tempPaths();
  const bridge = new Bridge(p, () => undefined);
  const t = await bridge.generate({ spec: spec(), authorization: { generation_authorized: false } });
  await saveTask(p, { ...t, status_detail: "a" }, t.version);
  await assert.rejects(saveTask(p, { ...t, status_detail: "b" }, t.version), /changed concurrently/);
});
