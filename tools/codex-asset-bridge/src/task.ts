import fs from "node:fs";
import path from "node:path";
import { Budget, BridgePaths, HEARTBEAT_STALE_MS } from "./config.js";
import { readJson, writeJsonAtomic } from "./fsutil.js";

export type TaskStatus =
  | "queued"
  | "running"
  | "review" // waiting for Claude to inspect
  | "approved"
  | "failed"
  | "blocked" // Codex reported it cannot proceed
  | "cancelled";

export interface ExpectedFile {
  name: string;
  width?: number;
  height?: number;
  /** Require alpha that is only 0 or 255. */
  binary_alpha?: boolean;
  /** Require an alpha channel with at least some transparent pixels. */
  transparent?: boolean;
}

export interface TaskSpec {
  task_name: string;
  title: string;
  asset_type: string;
  brief: string;
  expected_files: ExpectedFile[];
  reference_paths: string[];
  /** Folder under the art root that approval copies into. */
  target_dir: string;
}

export interface Authorization {
  generation_authorized: boolean;
  /** The user's own words granting image generation, with when they said it. */
  user_consent?: string;
}

export interface FileCheck {
  path: string;
  bytes: number;
  sha256: string;
  png?: { width: number; height: number; hasAlpha: boolean; binaryAlpha: boolean; transparentRatio: number };
  problems: string[];
}

export interface Manifest {
  revision: number;
  generated_at: string;
  plan_only: boolean;
  files: FileCheck[];
  deliverables: string[];
  missing: string[];
  problems: string[];
  ok: boolean;
  codex_report: unknown;
  images_generated_this_revision: number;
}

export interface RunRecord {
  revision: number;
  attempt: number;
  started_at: string;
  ended_at?: string;
  outcome?: "ok" | "validation_failed" | "error" | "timeout" | "cancelled" | "over_budget" | "blocked" | "policy_violation";
  error?: string;
  usage?: { input_tokens: number; cached_input_tokens: number; output_tokens: number; reasoning_output_tokens: number } | "unknown";
  images_generated?: number;
}

export interface TaskState {
  id: string;
  version: number;
  created_at: string;
  updated_at: string;
  status: TaskStatus;
  status_detail?: string;
  spec: TaskSpec;
  authorization: Authorization;
  budget: Budget;
  thread_id?: string;
  revision: number;
  revision_feedback: Record<string, string>;
  runs: RunRecord[];
  totals: { tokens: number; images: number; unknown_usage_runs: number };
  manifest?: Manifest;
  previewed_revision?: number;
  approval?: { at: string; target: string; note?: string; files: string[] };
}

export interface Heartbeat {
  pid: number;
  nonce: string;
  started_at: string;
  at: string;
}

export const LIVE_STATUSES: TaskStatus[] = ["queued", "running"];

export function taskFile(p: BridgePaths, id: string) {
  return path.join(p.tasksDir, `${id}.json`);
}
export function heartbeatFile(p: BridgePaths, id: string) {
  return path.join(p.tasksDir, `${id}.heartbeat.json`);
}
export function cancelFile(p: BridgePaths, id: string) {
  return path.join(p.tasksDir, `${id}.cancel`);
}
export function lockFile(p: BridgePaths, id: string) {
  return path.join(p.tasksDir, `${id}.lock`);
}
export function workerLockFile(p: BridgePaths, id: string) {
  return path.join(p.tasksDir, `${id}.worker.lock`);
}
export function eventsFile(p: BridgePaths, id: string) {
  return path.join(p.tasksDir, `${id}.events.jsonl`);
}
export function logFile(p: BridgePaths, id: string) {
  return path.join(p.tasksDir, `${id}.log`);
}
export function revisionDir(p: BridgePaths, id: string, revision: number) {
  return path.join(p.stagingDir, id, `r${revision}`);
}

export async function loadTask(p: BridgePaths, id: string): Promise<TaskState> {
  if (!/^[a-z0-9][a-z0-9-]{2,80}$/.test(id)) throw new Error(`Invalid task id: ${id}`);
  const t = await readJson<TaskState>(taskFile(p, id));
  if (!t) throw new Error(`No such task: ${id}`);
  return t;
}

/** Write with an optimistic version check: fails if someone else wrote since `expectedVersion`. */
export async function saveTask(p: BridgePaths, t: TaskState, expectedVersion: number | null): Promise<TaskState> {
  const current = await readJson<TaskState>(taskFile(p, t.id));
  const currentVersion = current ? current.version : null;
  if (currentVersion !== expectedVersion) {
    throw new Error(`Task ${t.id} changed concurrently (expected v${expectedVersion}, found v${currentVersion})`);
  }
  const next = { ...t, version: (expectedVersion ?? 0) + 1, updated_at: new Date().toISOString() };
  await writeJsonAtomic(taskFile(p, t.id), next);
  return next;
}

export function appendEvent(p: BridgePaths, id: string, event: Record<string, unknown>) {
  fs.mkdirSync(p.tasksDir, { recursive: true });
  fs.appendFileSync(eventsFile(p, id), JSON.stringify({ at: new Date().toISOString(), ...event }) + "\n", "utf8");
}

export function readEventsTail(p: BridgePaths, id: string, n: number): unknown[] {
  try {
    const lines = fs.readFileSync(eventsFile(p, id), "utf8").trim().split("\n");
    return lines.slice(-n).map((l) => {
      try {
        return JSON.parse(l);
      } catch {
        return l;
      }
    });
  } catch {
    return [];
  }
}

function pidAlive(pid: number): boolean {
  try {
    process.kill(pid, 0);
    return true;
  } catch (err: any) {
    return err?.code === "EPERM";
  }
}

/**
 * A live task's worker is gone when its heartbeat is stale and its pid no longer runs.
 * A fresh heartbeat wins over a pid check, because Windows reuses pids.
 */
export async function workerGone(p: BridgePaths, t: TaskState, now = Date.now()): Promise<boolean> {
  if (!LIVE_STATUSES.includes(t.status)) return false;
  const hb = await readJson<Heartbeat>(heartbeatFile(p, t.id));
  const reference = hb ? Date.parse(hb.at) : Date.parse(t.updated_at);
  if (now - reference < HEARTBEAT_STALE_MS) return false;
  return hb ? !pidAlive(hb.pid) || now - reference > 4 * HEARTBEAT_STALE_MS : true;
}

/** Allowed transitions; anything else is a bug. */
const TRANSITIONS: Record<TaskStatus, TaskStatus[]> = {
  queued: ["running", "failed", "cancelled"],
  running: ["review", "failed", "blocked", "cancelled", "running"],
  review: ["queued", "approved", "cancelled"],
  failed: ["queued", "cancelled"],
  blocked: ["queued", "cancelled"],
  approved: [],
  cancelled: [],
};

export function transition(t: TaskState, to: TaskStatus, detail?: string): TaskState {
  if (!TRANSITIONS[t.status].includes(to)) throw new Error(`Cannot move task ${t.id} from ${t.status} to ${to}`);
  return { ...t, status: to, status_detail: detail };
}

export function budgetExhausted(t: TaskState): string | null {
  if (t.totals.tokens >= t.budget.max_total_tokens) return `token budget used (${t.totals.tokens}/${t.budget.max_total_tokens})`;
  if (t.totals.images > t.budget.max_images) return `image budget exceeded (${t.totals.images}/${t.budget.max_images})`;
  // Wall-clock budget counts time spent running, so a revision requested days later is still allowed.
  const elapsed = t.runs.reduce((sum, r) => sum + ((r.ended_at ? Date.parse(r.ended_at) : Date.now()) - Date.parse(r.started_at)), 0);
  if (elapsed > t.budget.task_timeout_minutes * 60_000) return `task run-time limit reached (${t.budget.task_timeout_minutes} min)`;
  return null;
}
