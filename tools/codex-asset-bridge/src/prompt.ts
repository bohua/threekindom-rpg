import { TaskState } from "./task.js";

/** Structured final answer Codex must return (OpenAI strict schema: every key required). */
export const CODEX_OUTPUT_SCHEMA = {
  type: "object",
  additionalProperties: false,
  required: ["status", "summary", "files", "self_checks", "risks", "blocked_reason"],
  properties: {
    status: { type: "string", enum: ["done", "blocked"] },
    summary: { type: "string" },
    files: {
      type: "array",
      items: {
        type: "object",
        additionalProperties: false,
        required: ["path", "description"],
        properties: { path: { type: "string" }, description: { type: "string" } },
      },
    },
    self_checks: {
      type: "array",
      items: {
        type: "object",
        additionalProperties: false,
        required: ["check", "passed", "detail"],
        properties: { check: { type: "string" }, passed: { type: "boolean" }, detail: { type: "string" } },
      },
    },
    risks: { type: "array", items: { type: "string" } },
    blocked_reason: { type: "string" },
  },
} as const;

export interface CodexReport {
  status: "done" | "blocked";
  summary: string;
  files: { path: string; description: string }[];
  self_checks: { check: string; passed: boolean; detail: string }[];
  risks: string[];
  blocked_reason: string;
}

function expectedTable(t: TaskState): string {
  if (!t.spec.expected_files.length) return "(no fixed file list; name deliverables clearly)";
  return t.spec.expected_files
    .map((f) => {
      const parts = [f.width && f.height ? `${f.width}x${f.height}` : null, f.transparent ? "transparent background" : null, f.binary_alpha ? "alpha only 0 or 255" : null].filter(Boolean);
      return `- ${f.name}${parts.length ? ` (${parts.join(", ")})` : ""}`;
    })
    .join("\n");
}

/** The first prompt of a revision. Plan-only runs must not generate images. */
export function buildRunPrompt(t: TaskState, workDir: string, previousDir: string | null): string {
  const planOnly = !t.authorization.generation_authorized;
  const feedback = t.revision_feedback[String(t.revision)];
  const lines = [
    `You are producing art for a Three Kingdoms HD-2D JRPG. Task: ${t.spec.title} (${t.spec.asset_type}).`,
    "",
    "## Working rules (enforced by the bridge)",
    `- Your writable folder is ONLY: ${workDir}`,
    "- Write every file there. Never modify, move or delete files anywhere else, including existing game art.",
    "- Reference files listed below are read-only.",
    "- Put generation prompts in _prompts/, raw generated images in _sources/, check scripts and results in _checks/.",
    "- Deliverables go at the top level of the folder with the exact names listed below.",
    "- Do not write CODEX_STATUS.txt or manifest.json; the bridge writes them. Keep any working notes in _checks/, not AGENTS.md or MEMORY.md.",
  ];
  if (planOnly) {
    lines.push(
      "- PLAN-ONLY MODE: image generation is NOT authorized. Do not call any image generation tool.",
      "  Instead write the full, final image-generation prompt(s) you would use to _prompts/*.txt and a short plan to _prompts/PLAN.md, then finish.",
    );
  } else {
    lines.push(
      `- Image generation is authorized. Generate at most ${t.budget.max_images} images in total for this task, including retries; the bridge aborts past that.`,
      "- After generating, copy the source image into _sources/, then produce the deliverables (crop, resize, clean alpha) with scripts, and verify them.",
    );
  }
  lines.push("", "## Deliverables", expectedTable(t));
  if (t.spec.reference_paths.length) lines.push("", "## Reference files (read-only)", ...t.spec.reference_paths.map((r) => `- ${r}`));
  if (previousDir) lines.push("", `## Previous revision (read-only, for comparison)`, `- ${previousDir}`);
  if (feedback) lines.push("", "## Revision feedback from the art reviewer (Claude)", feedback);
  lines.push("", "## Brief", t.spec.brief);
  lines.push(
    "",
    "## Final answer",
    "Reply with the JSON object required by the output schema: status done or blocked, a summary, the files you produced (paths relative to the folder), your self-checks, risks, and blocked_reason (empty string when done).",
  );
  return lines.join("\n");
}

/** Follow-up prompt when the bridge's own validation rejected the output. */
export function buildFixPrompt(problems: string[], workDir: string): string {
  return [
    "The bridge validated your output and found these problems:",
    ...problems.map((p) => `- ${p}`),
    "",
    `Fix them inside ${workDir} only, re-run your checks, and answer again with the JSON object required by the output schema.`,
  ].join("\n");
}
