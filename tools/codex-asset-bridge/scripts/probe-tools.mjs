// Diagnostic: asks Codex which tools it has (no tool calls, no image generation).
import { Codex } from "@openai/codex-sdk";
import os from "node:os";
const codex = new Codex();
const thread = codex.startThread({ sandboxMode: "read-only", skipGitRepoCheck: true, workingDirectory: os.tmpdir(), modelReasoningEffort: "low" });
const t = await thread.run("Do not call any tool. List the exact names of every tool available to you in this session, one per line, then say whether one of them can generate raster images and, if so, where its output files are saved.");
console.log("thread:", thread.id);
console.log(t.finalResponse);
console.log(JSON.stringify(t.usage));
