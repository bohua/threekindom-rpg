// Command-line access to the same bridge the MCP server uses (for sessions without the MCP tools).
//   node scripts/cli.mjs generate <spec.json> [--authorize "<user consent>"]
//   node scripts/cli.mjs status <id> | list | wait <id> | cancel <id>
//   node scripts/cli.mjs preview <id>                       (records the preview; then view the PNGs yourself)
//   node scripts/cli.mjs revise <id> "<feedback>" [--authorize "<user consent>"]
//   node scripts/cli.mjs approve <id> ["<note>"]
import fs from "node:fs";
import { resolvePaths } from "../dist/src/config.js";
import { Bridge } from "../dist/src/bridge.js";

const bridge = new Bridge(resolvePaths());
const args = process.argv.slice(2);
const cmd = args[0];
const authIndex = args.indexOf("--authorize");
const consent = authIndex >= 0 ? args[authIndex + 1] : undefined;
const positional = authIndex >= 0 ? args.slice(1, authIndex) : args.slice(1);
const authorization = consent ? { generation_authorized: true, user_consent: consent } : undefined;
const out = (v) => console.log(JSON.stringify(v, null, 2));

try {
  if (cmd === "generate") {
    const { budget, ...spec } = JSON.parse(fs.readFileSync(positional[0], "utf8"));
    const t = await bridge.generate({ spec, budget, authorization: authorization ?? { generation_authorized: false } });
    out({ id: t.id, status: t.status, plan_only: !t.authorization.generation_authorized });
  } else if (cmd === "status") {
    const s = await bridge.status(positional[0]);
    out({ status: s.task.status, detail: s.task.status_detail, revision: s.task.revision, thread: s.task.thread_id, totals: s.task.totals, manifest: s.task.manifest, events: s.recent_events });
  } else if (cmd === "list") {
    out(await bridge.list());
  } else if (cmd === "wait") {
    for (;;) {
      const s = await bridge.status(positional[0], 1);
      if (!["queued", "running"].includes(s.task.status)) {
        out({ status: s.task.status, detail: s.task.status_detail });
        break;
      }
      await new Promise((r) => setTimeout(r, 10000));
    }
  } else if (cmd === "preview") {
    const p = await bridge.preview(positional[0], { maxImages: 0 });
    out({ folder: p.dir, status: p.task.status, previewed_revision: p.task.previewed_revision, deliverables: p.task.manifest?.deliverables, problems: p.task.manifest?.problems, prompts: p.prompts.map((x) => x.path) });
  } else if (cmd === "revise") {
    const t = await bridge.revise(positional[0], positional[1], authorization);
    out({ id: t.id, status: t.status, revision: t.revision, plan_only: !t.authorization.generation_authorized });
  } else if (cmd === "approve") {
    const t = await bridge.approve(positional[0], positional[1]);
    out({ status: t.status, approval: t.approval });
  } else if (cmd === "cancel") {
    out((await bridge.cancel(positional[0])).status);
  } else {
    console.error("usage: generate|status|list|wait|preview|revise|approve|cancel");
    process.exit(2);
  }
} catch (err) {
  console.error(`error: ${err?.message ?? err}`);
  process.exit(1);
}
