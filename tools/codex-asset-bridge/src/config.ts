import os from "node:os";
import path from "node:path";

/** Where the bridge keeps task state and staging folders. */
export interface BridgePaths {
  root: string;
  tasksDir: string;
  stagingDir: string;
  /** Approved assets are copied under this folder (never overwriting). */
  artRoot: string;
  /** Codex saves image_gen output here, one folder per thread id. */
  generatedImagesDir: string;
}

export function resolvePaths(env: NodeJS.ProcessEnv = process.env): BridgePaths {
  const artRoot = env.CODEX_BRIDGE_ART_ROOT ?? "D:\\BuildArtifacts\\Codex\\hd2d-jrpg\\art";
  const root = env.CODEX_BRIDGE_ROOT ?? path.join(artRoot, "_bridge");
  const codexHome = env.CODEX_HOME ?? path.join(os.homedir(), ".codex");
  return {
    root,
    tasksDir: path.join(root, "tasks"),
    stagingDir: path.join(root, "staging"),
    artRoot,
    generatedImagesDir: env.CODEX_BRIDGE_GENERATED_IMAGES ?? path.join(codexHome, "generated_images"),
  };
}

export const DEFAULT_BUDGET = {
  max_retries: 2,
  max_revisions: 3,
  max_images: 6,
  run_timeout_minutes: 45,
  task_timeout_minutes: 240,
  max_total_tokens: 3_000_000,
};

export type Budget = typeof DEFAULT_BUDGET;

/** Heartbeat cadence and the age after which a silent worker counts as gone. */
export const HEARTBEAT_MS = 15_000;
export const HEARTBEAT_STALE_MS = 60_000;
/** How often the worker polls the image folder and the cancel marker during a run. */
export const POLL_MS = 3_000;
