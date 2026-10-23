import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";

const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms));

/** Write to a temp file and rename over the target. Retries EPERM/EBUSY, which Windows raises while a reader or antivirus holds the file. */
export async function writeJsonAtomic(file: string, value: unknown): Promise<void> {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  const tmp = `${file}.${process.pid}.${crypto.randomBytes(4).toString("hex")}.tmp`;
  fs.writeFileSync(tmp, JSON.stringify(value, null, 2), "utf8");
  for (let attempt = 0; ; attempt++) {
    try {
      fs.renameSync(tmp, file);
      return;
    } catch (err: any) {
      if (attempt >= 8 || !["EPERM", "EBUSY", "EACCES"].includes(err?.code)) {
        fs.rmSync(tmp, { force: true });
        throw err;
      }
      await sleep(50 * 2 ** attempt);
    }
  }
}

/** Read JSON, tolerating a missing file or a momentarily partial one. */
export async function readJson<T>(file: string): Promise<T | null> {
  for (let attempt = 0; attempt < 5; attempt++) {
    try {
      return JSON.parse(fs.readFileSync(file, "utf8")) as T;
    } catch (err: any) {
      if (err?.code === "ENOENT") return null;
      await sleep(40 * (attempt + 1));
    }
  }
  return null;
}

/** Exclusive lock file. Stale locks (older than staleMs) are broken. */
export async function withLock<T>(lockFile: string, fn: () => Promise<T>, staleMs = 10 * 60_000): Promise<T> {
  fs.mkdirSync(path.dirname(lockFile), { recursive: true });
  for (let attempt = 0; ; attempt++) {
    try {
      const fd = fs.openSync(lockFile, "wx");
      fs.writeSync(fd, String(process.pid));
      fs.closeSync(fd);
      break;
    } catch (err: any) {
      if (err?.code !== "EEXIST") throw err;
      try {
        if (Date.now() - fs.statSync(lockFile).mtimeMs > staleMs) {
          fs.rmSync(lockFile, { force: true });
          continue;
        }
      } catch {
        continue;
      }
      if (attempt > 100) throw new Error(`Task is busy (lock held: ${lockFile})`);
      await sleep(100);
    }
  }
  try {
    return await fn();
  } finally {
    fs.rmSync(lockFile, { force: true });
  }
}

function realOrResolved(p: string): string {
  // Resolve the deepest existing ancestor so junctions/symlinks on the way are followed.
  let current = path.resolve(p);
  const rest: string[] = [];
  while (!fs.existsSync(current)) {
    const parent = path.dirname(current);
    if (parent === current) break;
    rest.unshift(path.basename(current));
    current = parent;
  }
  const real = fs.existsSync(current) ? fs.realpathSync.native(current) : current;
  return path.join(real, ...rest);
}

const norm = (p: string) => (process.platform === "win32" ? p.toLowerCase() : p);

/** True when `child` (after resolving links) is inside `parent` or equal to it. */
export function isInside(parent: string, child: string): boolean {
  const a = norm(realOrResolved(parent));
  const b = norm(realOrResolved(child));
  const rel = path.relative(a, b);
  return rel === "" || (!rel.startsWith("..") && !path.isAbsolute(rel));
}

/** Resolve `relative` under `base` and refuse anything that escapes it. */
export function safeJoin(base: string, relative: string): string {
  if (path.isAbsolute(relative)) throw new Error(`Absolute path not allowed here: ${relative}`);
  const joined = path.resolve(base, relative);
  if (!isInside(base, joined)) throw new Error(`Path escapes ${base}: ${relative}`);
  return joined;
}

/** All files under dir (relative, forward slashes), skipping symlinks and junctions. */
export function listFiles(dir: string): string[] {
  const out: string[] = [];
  const walk = (d: string, prefix: string) => {
    if (!fs.existsSync(d)) return;
    for (const entry of fs.readdirSync(d, { withFileTypes: true })) {
      if (entry.isSymbolicLink()) continue;
      const rel = prefix ? `${prefix}/${entry.name}` : entry.name;
      if (entry.isDirectory()) walk(path.join(d, entry.name), rel);
      else if (entry.isFile()) out.push(rel);
    }
  };
  walk(dir, "");
  return out.sort();
}

export function sha256(file: string): string {
  return crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex");
}

export function dirIsEmptyOrMissing(dir: string): boolean {
  return !fs.existsSync(dir) || fs.readdirSync(dir).length === 0;
}
