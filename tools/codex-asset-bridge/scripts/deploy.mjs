// Copy the built bridge to a stable folder outside any git worktree, so the MCP registration
// keeps working after worktrees are removed. Usage: npm run build && node scripts/deploy.mjs
import fs from "node:fs";
import path from "node:path";
import os from "node:os";
import { fileURLToPath } from "node:url";

const here = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
const dest = process.env.CODEX_BRIDGE_APP_DIR ?? path.join(process.env.LOCALAPPDATA ?? path.join(os.homedir(), "AppData", "Local"), "codex-asset-bridge", "app");
fs.rmSync(dest, { recursive: true, force: true });
fs.mkdirSync(dest, { recursive: true });
for (const item of ["dist", "node_modules", "scripts", "package.json", "package-lock.json"]) {
  fs.cpSync(path.join(here, item), path.join(dest, item), { recursive: true });
}
console.log(`deployed to ${dest}`);
console.log(`MCP command: node "${path.join(dest, "dist", "src", "server.js")}"`);
