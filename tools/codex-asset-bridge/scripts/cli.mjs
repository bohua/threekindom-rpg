// Command-line access to the same bridge the MCP server uses (debugging and scripted runs).
//   node scripts/cli.mjs generate <spec.json> [--authorize "<user consent>"]
//   node scripts/cli.mjs status <taskId> | list | wait <taskId> | cancel <taskId>
import fs from "node:fs";
import { resolvePaths } from "../dist/src/config.js";
import { Bridge } from "../dist/src/bridge.js";

const bridge = new Bridge(resolvePaths());
const [cmd, arg, flag, consent] = process.argv.slice(2);
const out = (v) => console.log(JSON.stringify(v, null, 2));

if (cmd === "generate") {
  const spec = JSON.parse(fs.readFileSync(arg, "utf8"));
  const authorized = flag === "--authorize";
  const { budget, ...rest } = spec;
  const t = await bridge.generate({ spec: rest, budget, authorization: { generation_authorized: authorized, user_consent: authorized ? consent : undefined } });
  out({ id: t.id, status: t.status });
} else if (cmd === "status") {
  const s = await bridge.status(arg);
  out({ status: s.task.status, detail: s.task.status_detail, revision: s.task.revision, thread: s.task.thread_id, totals: s.task.totals, manifest: s.task.manifest, events: s.recent_events });
} else if (cmd === "list") {
  out(await bridge.list());
} else if (cmd === "wait") {
  for (;;) {
    const s = await bridge.status(arg, 1);
    if (!["queued", "running"].includes(s.task.status)) { out({ status: s.task.status, detail: s.task.status_detail }); break; }
    await new Promise((r) => setTimeout(r, 10000));
  }
} else if (cmd === "cancel") {
  out((await bridge.cancel(arg)).status);
} else {
  console.error("usage: generate|status|list|wait|cancel");
  process.exit(2);
}
