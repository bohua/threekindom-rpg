import fs from "node:fs";
import path from "node:path";
import { listFiles, sha256 } from "./fsutil.js";
import { inspectPng, isPng } from "./png.js";
import { ExpectedFile, FileCheck, Manifest } from "./task.js";

/** Folders inside a revision that are provenance, not deliverables. */
export const PROVENANCE_DIRS = ["_sources", "_prompts", "_checks"];

/** Working notes Codex's global instructions make it keep in any workspace; never deliverables. */
export const NOTE_FILES = ["AGENTS.md", "MEMORY.md"];

const isProvenance = (rel: string) => NOTE_FILES.includes(rel) || PROVENANCE_DIRS.some((d) => rel === d || rel.startsWith(`${d}/`));

/**
 * Build the manifest for one revision folder from what is actually on disk.
 * Codex's own report is kept for reference but never trusted for pass/fail.
 */
export function buildManifest(opts: {
  dir: string;
  revision: number;
  planOnly: boolean;
  expected: ExpectedFile[];
  codexReport: unknown;
  imagesGenerated: number;
}): Manifest {
  const { dir, revision, planOnly, expected, codexReport, imagesGenerated } = opts;
  const problems: string[] = [];
  const files: FileCheck[] = [];
  for (const rel of listFiles(dir)) {
    if (rel === "manifest.json" || rel === "CODEX_STATUS.txt") continue;
    const abs = path.join(dir, rel);
    const check: FileCheck = { path: rel, bytes: fs.statSync(abs).size, sha256: sha256(abs), problems: [] };
    if (rel.toLowerCase().endsWith(".png")) {
      if (!isPng(abs)) check.problems.push("not a valid PNG (bad signature)");
      else {
        try {
          check.png = inspectPng(abs);
        } catch (err: any) {
          check.problems.push(`PNG could not be decoded: ${err?.message ?? err}`);
        }
      }
    }
    files.push(check);
  }

  const byName = new Map(files.map((f) => [f.path, f]));
  const missing: string[] = [];
  if (planOnly) {
    if (!files.some((f) => f.path.startsWith("_prompts/"))) problems.push("plan-only run wrote nothing under _prompts/");
    if (imagesGenerated > 0) problems.push(`policy violation: ${imagesGenerated} image(s) generated in a plan-only run`);
  } else {
    for (const exp of expected) {
      const f = byName.get(exp.name);
      if (!f) {
        missing.push(exp.name);
        continue;
      }
      if (exp.width !== undefined || exp.height !== undefined || exp.binary_alpha || exp.transparent) {
        if (!f.png) {
          f.problems.push("expected a PNG");
          continue;
        }
        if (exp.width !== undefined && f.png.width !== exp.width) f.problems.push(`width ${f.png.width}, expected ${exp.width}`);
        if (exp.height !== undefined && f.png.height !== exp.height) f.problems.push(`height ${f.png.height}, expected ${exp.height}`);
        if (exp.transparent && (!f.png.hasAlpha || f.png.transparentRatio === 0)) f.problems.push("expected a transparent background");
        if (exp.binary_alpha && (!f.png.hasAlpha || !f.png.binaryAlpha)) f.problems.push("alpha must be only 0 or 255");
      }
    }
    if (missing.length) problems.push(`missing expected file(s): ${missing.join(", ")}`);
  }
  for (const f of files) for (const p of f.problems) problems.push(`${f.path}: ${p}`);

  const deliverables = files.map((f) => f.path).filter((p) => !isProvenance(p));
  return {
    revision,
    generated_at: new Date().toISOString(),
    plan_only: planOnly,
    files,
    deliverables,
    missing,
    problems,
    ok: problems.length === 0,
    codex_report: codexReport,
    images_generated_this_revision: imagesGenerated,
  };
}

/** Image files Codex's image_gen saved for a thread (the folder is keyed by thread id). */
export function listGeneratedImages(generatedImagesDir: string, threadId: string | undefined): string[] {
  if (!threadId) return [];
  const dir = path.join(generatedImagesDir, threadId);
  if (!fs.existsSync(dir)) return [];
  return fs
    .readdirSync(dir)
    .filter((n) => /\.(png|jpe?g|webp)$/i.test(n))
    .map((n) => path.join(dir, n));
}
