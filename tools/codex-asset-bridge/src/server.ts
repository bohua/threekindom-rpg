#!/usr/bin/env node
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import { resolvePaths } from "./config.js";
import { Bridge } from "./bridge.js";
import { TaskState } from "./task.js";

const bridge = new Bridge(resolvePaths());
const server = new McpServer({ name: "codex-assets", version: "0.1.0" });

const json = (value: unknown) => ({ content: [{ type: "text" as const, text: JSON.stringify(value, null, 2) }] });
const fail = (err: unknown) => ({ isError: true, content: [{ type: "text" as const, text: String((err as Error)?.message ?? err) }] });

/** The parts of a task Claude usually needs, without the full run history. */
function brief(t: TaskState) {
  return {
    id: t.id,
    title: t.spec.title,
    status: t.status,
    detail: t.status_detail,
    revision: t.revision,
    plan_only: !t.authorization.generation_authorized,
    thread_id: t.thread_id,
    totals: t.totals,
    budget: t.budget,
    staging_dir: bridge.paths.stagingDir + `\\${t.id}\\r${t.manifest?.revision ?? t.revision}`,
    target_dir: t.spec.target_dir,
    manifest: t.manifest
      ? { ok: t.manifest.ok, revision: t.manifest.revision, deliverables: t.manifest.deliverables, problems: t.manifest.problems, files: t.manifest.files.map((f) => ({ path: f.path, png: f.png, problems: f.problems })), codex_report: t.manifest.codex_report }
      : undefined,
    previewed_revision: t.previewed_revision,
    approval: t.approval,
    last_runs: t.runs.slice(-3),
  };
}

const expectedFile = z.object({
  name: z.string().describe("File name relative to the task folder, e.g. TAO_QIAN_MAIN_POSE.png"),
  width: z.number().int().positive().optional(),
  height: z.number().int().positive().optional(),
  binary_alpha: z.boolean().optional().describe("Alpha must be only 0 or 255 (pixel art)"),
  transparent: z.boolean().optional().describe("Must have a transparent background"),
});

const budget = z
  .object({
    max_retries: z.number().int().min(0).max(5).optional(),
    max_revisions: z.number().int().min(0).max(10).optional(),
    max_images: z.number().int().min(0).max(40).optional(),
    run_timeout_minutes: z.number().int().min(1).max(180).optional(),
    task_timeout_minutes: z.number().int().min(1).max(720).optional(),
    max_total_tokens: z.number().int().min(10_000).optional(),
  })
  .optional();

server.registerTool(
  "asset_generate",
  {
    description:
      "Dispatch a new art task to Codex. Returns at once with a task id; Codex works in a background process and writes only to a staging folder. " +
      "Without generation_authorized=true (and the user's quoted consent) Codex only writes prompts and a plan, no images. Poll with asset_status.",
    inputSchema: {
      task_name: z.string().min(3).describe("Short English slug, e.g. tao-qian-portrait"),
      title: z.string().describe("Human title, e.g. 陶谦主立绘"),
      asset_type: z.enum(["character_portrait", "sprite_sheet", "texture", "ui", "vfx", "background", "other"]),
      brief: z.string().min(20).describe("Full Codex instructions in Markdown (style, sizes, references, checks), like the old CODEX_PROMPT.md"),
      expected_files: z.array(expectedFile).describe("Deliverables the bridge will validate"),
      reference_paths: z.array(z.string()).default([]).describe("Absolute paths of read-only reference images or docs"),
      target_dir: z.string().describe("New folder under the art root that approval copies into, e.g. tao-qian-portrait. Must not exist or be empty."),
      generation_authorized: z.boolean().default(false).describe("Allow image generation (uses the user's Codex quota). Only true when the user has agreed."),
      user_consent: z.string().optional().describe("Required when generation_authorized: the user's words and time, e.g. 'Bohua 2026-10-23 12:10: 可以'"),
      budget,
    },
  },
  async (a) => {
    try {
      const t = await bridge.generate({
        spec: {
          task_name: a.task_name,
          title: a.title,
          asset_type: a.asset_type,
          brief: a.brief,
          expected_files: a.expected_files,
          reference_paths: a.reference_paths,
          target_dir: a.target_dir,
        },
        authorization: { generation_authorized: a.generation_authorized, user_consent: a.user_consent },
        budget: a.budget,
      });
      return json(brief(t));
    } catch (err) {
      return fail(err);
    }
  },
);

server.registerTool(
  "asset_status",
  {
    description: "Status of one task (with recent Codex activity), or the latest tasks when no id is given.",
    inputSchema: { task_id: z.string().optional(), events: z.number().int().min(0).max(100).default(15) },
  },
  async (a) => {
    try {
      if (!a.task_id) return json(await bridge.list());
      const s = await bridge.status(a.task_id, a.events);
      return json({ ...brief(s.task), heartbeat: s.heartbeat, recent_events: s.recent_events });
    } catch (err) {
      return fail(err);
    }
  },
);

server.registerTool(
  "asset_preview",
  {
    description:
      "Show the latest revision's validated manifest, images (downscaled with nearest-neighbour) and prompt files. Must be called on the current revision before asset_approve.",
    inputSchema: {
      task_id: z.string(),
      files: z.array(z.string()).optional().describe("Specific files (relative to the revision folder); default all PNG deliverables"),
      max_side: z.number().int().min(64).max(2048).default(768),
      max_images: z.number().int().min(0).max(8).default(4),
    },
  },
  async (a) => {
    try {
      const p = await bridge.preview(a.task_id, { files: a.files, maxSide: a.max_side, maxImages: a.max_images });
      return {
        content: [
          { type: "text" as const, text: JSON.stringify({ ...brief(p.task), folder: p.dir, prompts: p.prompts }, null, 2) },
          ...p.images.flatMap((img) => [
            { type: "text" as const, text: img.path },
            { type: "image" as const, data: img.base64, mimeType: "image/png" },
          ]),
        ],
      };
    } catch (err) {
      return fail(err);
    }
  },
);

server.registerTool(
  "asset_revise",
  {
    description:
      "Send review feedback to Codex on the same thread; it works in a new revision folder. Also used to continue a failed/blocked task, or to switch a plan-only task to real generation once the user agreed.",
    inputSchema: {
      task_id: z.string(),
      feedback: z.string().min(5),
      generation_authorized: z.boolean().optional(),
      user_consent: z.string().optional(),
    },
  },
  async (a) => {
    try {
      const auth = a.generation_authorized ? { generation_authorized: true, user_consent: a.user_consent } : undefined;
      return json(brief(await bridge.revise(a.task_id, a.feedback, auth)));
    } catch (err) {
      return fail(err);
    }
  },
);

server.registerTool(
  "asset_approve",
  {
    description:
      "Approve the previewed, validated revision: copies it into art/<target_dir> (never overwriting) and writes CODEX_STATUS.txt. Unity import is a separate step.",
    inputSchema: { task_id: z.string(), note: z.string().optional() },
  },
  async (a) => {
    try {
      return json(brief(await bridge.approve(a.task_id, a.note)));
    } catch (err) {
      return fail(err);
    }
  },
);

server.registerTool(
  "asset_cancel",
  { description: "Stop a queued or running task (cooperatively, then by force).", inputSchema: { task_id: z.string() } },
  async (a) => {
    try {
      return json(brief(await bridge.cancel(a.task_id)));
    } catch (err) {
      return fail(err);
    }
  },
);

await server.connect(new StdioServerTransport());
