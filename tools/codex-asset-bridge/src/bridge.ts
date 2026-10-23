import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { spawn } from "node:child_process";
import { fileURLToPath } from "node:url";
import { Budget, BridgePaths, DEFAULT_BUDGET } from "./config.js";
import { dirIsEmptyOrMissing, isInside, readJson, safeJoin, withLock } from "./fsutil.js";
import { previewPng } from "./png.js";
import { killProcessTree } from "./runner.js";
import {
  Authorization, cancelFile, Heartbeat, heartbeatFile, LIVE_STATUSES, loadTask, lockFile, readEventsTail,
  revisionDir, saveTask, TaskSpec, TaskState, taskFile, transition, workerGone,
} from "./task.js";

export type SpawnWorker = (paths: BridgePaths, id: string) => void;

/** Start the worker as a detached, hidden process so it outlives the MCP server and the Claude session. */
export const spawnDetachedWorker: SpawnWorker = (paths, id) => {
  const workerJs = path.join(path.dirname(fileURLToPath(import.meta.url)), "worker.js");
  const child = spawn(process.execPath, [workerJs, id], {
    detached: true,
    stdio: "ignore",
    windowsHide: true,
    env: {
      ...process.env,
      CODEX_BRIDGE_ROOT: paths.root,
      CODEX_BRIDGE_ART_ROOT: paths.artRoot,
      CODEX_BRIDGE_GENERATED_IMAGES: paths.generatedImagesDir,
    },
  });
  child.unref();
};

export interface GenerateInput {
  spec: TaskSpec;
  authorization: Authorization;
  budget?: Partial<Budget>;
}

function newId(taskName: string): string {
  const slug = taskName.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-+|-+$/g, "").slice(0, 40) || "task";
  const stamp = new Date().toISOString().replace(/[-:T]/g, "").slice(0, 12);
  return `${slug}-${stamp}-${crypto.randomBytes(2).toString("hex")}`;
}

export class Bridge {
  constructor(readonly paths: BridgePaths, private readonly spawnWorker: SpawnWorker = spawnDetachedWorker) {}

  private checkSpec(spec: TaskSpec) {
    // target_dir must be a plain relative folder under the art root.
    safeJoin(this.paths.artRoot, spec.target_dir);
    if (spec.target_dir.startsWith("_")) throw new Error("target_dir may not start with '_' (reserved for the bridge)");
    for (const f of spec.expected_files) {
      if (path.isAbsolute(f.name) || f.name.includes("..")) throw new Error(`Bad expected file name: ${f.name}`);
    }
    for (const r of spec.reference_paths) {
      if (!path.isAbsolute(r)) throw new Error(`reference_paths must be absolute: ${r}`);
      if (!fs.existsSync(r)) throw new Error(`Reference not found: ${r}`);
    }
  }

  async generate(input: GenerateInput): Promise<TaskState> {
    this.checkSpec(input.spec);
    const auth = input.authorization;
    if (auth.generation_authorized && !auth.user_consent?.trim()) {
      throw new Error("generation_authorized=true needs user_consent: quote the user's approval and when it was given");
    }
    const target = safeJoin(this.paths.artRoot, input.spec.target_dir);
    if (!dirIsEmptyOrMissing(target)) {
      throw new Error(`target_dir already has files and will never be overwritten: ${target}. Pick a new folder name.`);
    }
    const id = newId(input.spec.task_name);
    const now = new Date().toISOString();
    const state: TaskState = {
      id,
      version: 0,
      created_at: now,
      updated_at: now,
      status: "queued",
      status_detail: "waiting for worker",
      spec: input.spec,
      authorization: auth,
      budget: { ...DEFAULT_BUDGET, ...(input.budget ?? {}) },
      revision: 1,
      revision_feedback: {},
      runs: [],
      totals: { tokens: 0, images: 0, unknown_usage_runs: 0 },
    };
    fs.mkdirSync(this.paths.tasksDir, { recursive: true });
    const saved = await saveTask(this.paths, state, null);
    this.spawnWorker(this.paths, id);
    return saved;
  }

  /** Current state; marks a live task failed if its worker died without saying so. */
  async status(id: string, eventsTail = 15) {
    let t = await loadTask(this.paths, id);
    if (await workerGone(this.paths, t)) {
      t = await withLock(lockFile(this.paths, id), async () => {
        const fresh = await loadTask(this.paths, id);
        if (!(await workerGone(this.paths, fresh))) return fresh;
        return saveTask(this.paths, transition(fresh, "failed", "worker process stopped without finishing (machine restart or crash); use asset_revise to continue on the same Codex thread"), fresh.version);
      });
    }
    const hb = await readJson<Heartbeat>(heartbeatFile(this.paths, id));
    return { task: t, heartbeat: hb, recent_events: readEventsTail(this.paths, id, eventsTail) };
  }

  async list(limit = 20) {
    if (!fs.existsSync(this.paths.tasksDir)) return [];
    const files = fs.readdirSync(this.paths.tasksDir).filter((f) => /^[a-z0-9-]+\.json$/.test(f) && !f.includes(".heartbeat"));
    const tasks: TaskState[] = [];
    for (const f of files) {
      const t = await readJson<TaskState>(path.join(this.paths.tasksDir, f));
      if (t?.id) tasks.push(t);
    }
    return tasks
      .sort((a, b) => b.updated_at.localeCompare(a.updated_at))
      .slice(0, limit)
      .map((t) => ({ id: t.id, title: t.spec.title, status: t.status, detail: t.status_detail, revision: t.revision, updated_at: t.updated_at }));
  }

  /** Files for Claude to inspect. Recording the preview is what unlocks approval. */
  async preview(id: string, opts: { files?: string[]; maxSide?: number; maxImages?: number } = {}) {
    return withLock(lockFile(this.paths, id), async () => {
      const t = await loadTask(this.paths, id);
      if (!t.manifest) throw new Error(`Task ${id} has no output yet (status ${t.status})`);
      const dir = revisionDir(this.paths, id, t.manifest.revision);
      const pngs = (opts.files ?? t.manifest.deliverables.filter((f) => f.toLowerCase().endsWith(".png")))
        .map((rel) => safeJoin(dir, rel))
        .filter((abs) => fs.existsSync(abs));
      const maxSide = opts.maxSide ?? 768;
      const images: { path: string; base64: string }[] = [];
      let budgetBytes = 3_000_000;
      for (const abs of pngs.slice(0, opts.maxImages ?? 4)) {
        const buf = previewPng(abs, maxSide);
        if (buf.length > budgetBytes) break;
        budgetBytes -= buf.length;
        images.push({ path: abs, base64: buf.toString("base64") });
      }
      const textFiles = t.manifest.files
        .filter((f) => f.path.startsWith("_prompts/") && /\.(txt|md)$/i.test(f.path))
        .slice(0, 6)
        .map((f) => ({ path: f.path, text: fs.readFileSync(path.join(dir, f.path), "utf8").slice(0, 6000) }));
      let task = t;
      if (t.status === "review" && t.previewed_revision !== t.manifest.revision) {
        task = await saveTask(this.paths, { ...t, previewed_revision: t.manifest.revision }, t.version);
      }
      return { task, dir, images, prompts: textFiles };
    });
  }

  async revise(id: string, feedback: string, authorization?: Authorization) {
    const t = await withLock(lockFile(this.paths, id), async () => {
      let t = await loadTask(this.paths, id);
      if (!["review", "failed", "blocked"].includes(t.status)) throw new Error(`Task ${id} is ${t.status}; only review/failed/blocked tasks can be revised`);
      if (t.revision >= t.budget.max_revisions + 1) throw new Error(`Revision budget used (${t.budget.max_revisions} revisions)`);
      let auth = t.authorization;
      if (authorization?.generation_authorized && !t.authorization.generation_authorized) {
        if (!authorization.user_consent?.trim()) throw new Error("Upgrading to generation needs user_consent");
        auth = authorization;
      }
      const revision = t.revision + 1;
      t = transition({ ...t, authorization: auth, revision, revision_feedback: { ...t.revision_feedback, [String(revision)]: feedback } }, "queued", `revision ${revision} queued`);
      return saveTask(this.paths, t, t.version);
    });
    this.spawnWorker(this.paths, id);
    return t;
  }

  async approve(id: string, note?: string) {
    return withLock(lockFile(this.paths, id), async () => {
      const t = await loadTask(this.paths, id);
      if (t.status !== "review") throw new Error(`Task ${id} is ${t.status}; only tasks in review can be approved`);
      const m = t.manifest;
      if (!m || !m.ok) throw new Error("The latest revision did not pass validation");
      if (m.plan_only) throw new Error("This revision is plan-only (no images). Revise with generation authorized first.");
      if (t.previewed_revision !== m.revision) throw new Error(`Call asset_preview on revision ${m.revision} before approving`);

      const target = safeJoin(this.paths.artRoot, t.spec.target_dir);
      if (!dirIsEmptyOrMissing(target)) throw new Error(`Refusing to overwrite existing folder: ${target}`);
      const src = revisionDir(this.paths, id, m.revision);
      // Copy to a sibling temp folder, then rename into place, so an existing target can never be half-written.
      const tmp = `${target}.approving-${crypto.randomBytes(3).toString("hex")}`;
      fs.cpSync(src, tmp, { recursive: true, errorOnExist: true, force: false, verbatimSymlinks: true });
      const doneAt = new Date().toISOString();
      fs.writeFileSync(
        path.join(tmp, "CODEX_STATUS.txt"),
        [
          "STATUS: DONE",
          `任务: ${t.spec.title}`,
          `完成时间: ${doneAt}`,
          `桥任务: ${t.id}（修订 r${m.revision}，Codex 线程 ${t.thread_id ?? "?"}）`,
          `交付文件:`,
          ...m.deliverables.map((d) => `- ${d}`),
          `检查: 桥校验通过（${m.files.length} 个文件）；Claude 已预览并批准${note ? `：${note}` : ""}`,
          "未完成项: 尚未导入 Unity",
          "",
        ].join("\n"),
        "utf8",
      );
      if (!dirIsEmptyOrMissing(target)) {
        fs.rmSync(tmp, { recursive: true, force: true });
        throw new Error(`Refusing to overwrite existing folder: ${target}`);
      }
      if (fs.existsSync(target)) fs.rmdirSync(target); // empty folder only
      fs.renameSync(tmp, target);
      if (!isInside(this.paths.artRoot, target)) throw new Error("approved folder resolved outside the art root");
      const approved = transition({ ...t, approval: { at: doneAt, target, note, files: m.deliverables } }, "approved", `copied to ${target}`);
      return saveTask(this.paths, approved, t.version);
    });
  }

  /** Cooperative cancel; the worker stops at its next heartbeat. A worker that never answers is killed. */
  async cancel(id: string) {
    const t = await loadTask(this.paths, id);
    if (["approved", "cancelled"].includes(t.status)) return t;
    if (LIVE_STATUSES.includes(t.status)) {
      fs.writeFileSync(cancelFile(this.paths, id), new Date().toISOString());
      for (let i = 0; i < 40; i++) {
        await new Promise((r) => setTimeout(r, 1000));
        const now = await loadTask(this.paths, id);
        if (!LIVE_STATUSES.includes(now.status)) return now;
      }
      const hb = await readJson<Heartbeat>(heartbeatFile(this.paths, id));
      if (hb) killProcessTree(hb.pid);
    }
    return withLock(lockFile(this.paths, id), async () => {
      const fresh = await loadTask(this.paths, id);
      if (["approved", "cancelled"].includes(fresh.status)) return fresh;
      return saveTask(this.paths, transition(fresh, "cancelled", "cancelled by Claude"), fresh.version);
    });
  }

  taskPath(id: string) {
    return taskFile(this.paths, id);
  }
}
