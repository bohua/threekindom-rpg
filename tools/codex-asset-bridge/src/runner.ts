import { execFileSync } from "node:child_process";
import { Codex, ThreadEvent, ThreadOptions } from "@openai/codex-sdk";
import { CODEX_OUTPUT_SCHEMA, CodexReport } from "./prompt.js";

export interface TurnRequest {
  threadId?: string;
  workDir: string;
  prompt: string;
  planOnly: boolean;
  signal: AbortSignal;
  onThreadId: (id: string) => void;
  onEvent: (summary: Record<string, unknown>) => void;
}

export interface TurnResult {
  threadId?: string;
  report: CodexReport | null;
  rawFinal: string;
  usage: { input_tokens: number; cached_input_tokens: number; output_tokens: number; reasoning_output_tokens: number } | null;
  error?: string;
}

/** Seam for tests: the worker only talks to Codex through this. */
export interface CodexRunner {
  runTurn(req: TurnRequest): Promise<TurnResult>;
}

/** Variables Codex needs to find its home, auth and tools. Nothing else is inherited. */
const ENV_KEYS = [
  "PATH", "Path", "PATHEXT", "SystemRoot", "SYSTEMROOT", "windir", "ComSpec", "COMSPEC",
  "USERPROFILE", "HOMEDRIVE", "HOMEPATH", "HOME", "APPDATA", "LOCALAPPDATA", "ProgramData",
  "ProgramFiles", "ProgramFiles(x86)", "TEMP", "TMP", "USERNAME", "CODEX_HOME", "NUMBER_OF_PROCESSORS", "PROCESSOR_ARCHITECTURE", "OS",
];

export function minimalEnv(src: NodeJS.ProcessEnv = process.env): Record<string, string> {
  const env: Record<string, string> = {};
  for (const k of ENV_KEYS) if (src[k] !== undefined) env[k] = src[k] as string;
  return env;
}

function summarize(e: ThreadEvent): Record<string, unknown> | null {
  switch (e.type) {
    case "thread.started":
      return { type: e.type, thread_id: e.thread_id };
    case "turn.completed":
      return { type: e.type, usage: e.usage };
    case "turn.failed":
      return { type: e.type, error: e.error.message };
    case "error":
      return { type: e.type, error: e.message };
    case "item.completed": {
      const it = e.item;
      if (it.type === "command_execution") return { type: "command", command: it.command.slice(0, 200), exit_code: it.exit_code, status: it.status };
      if (it.type === "file_change") return { type: "file_change", files: it.changes.map((c) => `${c.kind} ${c.path}`).slice(0, 20), status: it.status };
      if (it.type === "mcp_tool_call") return { type: "tool", server: it.server, tool: it.tool, status: it.status };
      if (it.type === "agent_message") return { type: "message", text: it.text.slice(0, 400) };
      if (it.type === "error") return { type: "item_error", message: it.message };
      if (it.type === "todo_list") return { type: "todo", items: it.items.map((i) => `${i.completed ? "[x]" : "[ ]"} ${i.text}`) };
      return null;
    }
    default:
      return null;
  }
}

export class SdkCodexRunner implements CodexRunner {
  async runTurn(req: TurnRequest): Promise<TurnResult> {
    // Plan-only turns switch the image tool off in Codex itself, not just in the prompt.
    const codex = new Codex({
      env: minimalEnv(),
      config: req.planOnly ? { features: { image_generation: false } } : {},
    });
    const options: ThreadOptions = {
      sandboxMode: "workspace-write",
      workingDirectory: req.workDir,
      skipGitRepoCheck: true,
      approvalPolicy: "never",
      networkAccessEnabled: false,
      webSearchMode: "disabled",
    };
    const thread = req.threadId ? codex.resumeThread(req.threadId, options) : codex.startThread(options);
    let threadId = req.threadId;
    let usage: TurnResult["usage"] = null;
    let error: string | undefined;
    let rawFinal = "";
    try {
      const { events } = await thread.runStreamed(req.prompt, { outputSchema: CODEX_OUTPUT_SCHEMA, signal: req.signal });
      for await (const e of events) {
        if (e.type === "thread.started") {
          threadId = e.thread_id;
          req.onThreadId(e.thread_id);
        }
        if (e.type === "turn.completed") usage = e.usage;
        if (e.type === "turn.failed") error = e.error.message;
        if (e.type === "error") error = e.message;
        if (e.type === "item.completed" && e.item.type === "agent_message") rawFinal = e.item.text;
        const s = summarize(e);
        if (s) req.onEvent(s);
      }
    } catch (err: any) {
      error = req.signal.aborted ? `aborted: ${String(req.signal.reason ?? "")}` : String(err?.message ?? err);
    } finally {
      if (req.signal.aborted) killChildProcessTree(process.pid);
    }
    let report: CodexReport | null = null;
    if (rawFinal) {
      try {
        report = JSON.parse(rawFinal) as CodexReport;
      } catch {
        report = null;
      }
    }
    return { threadId: threadId ?? thread.id ?? undefined, report, rawFinal, usage, error };
  }
}

/** The SDK's abort only kills codex.exe; its own children can survive on Windows. */
export function killChildProcessTree(parentPid: number) {
  if (process.platform !== "win32") return;
  try {
    const out = execFileSync(
      "powershell.exe",
      ["-NoProfile", "-Command", `(Get-CimInstance Win32_Process -Filter "ParentProcessId=${parentPid}").ProcessId`],
      { encoding: "utf8", windowsHide: true, timeout: 15_000 },
    );
    for (const line of out.split(/\r?\n/)) {
      const pid = Number(line.trim());
      if (pid > 0) {
        try {
          execFileSync("taskkill.exe", ["/PID", String(pid), "/T", "/F"], { windowsHide: true, stdio: "ignore", timeout: 15_000 });
        } catch {
          /* already gone */
        }
      }
    }
  } catch {
    /* best effort */
  }
}

export function killProcessTree(pid: number) {
  try {
    if (process.platform === "win32") execFileSync("taskkill.exe", ["/PID", String(pid), "/T", "/F"], { windowsHide: true, stdio: "ignore", timeout: 15_000 });
    else process.kill(pid, "SIGKILL");
  } catch {
    /* already gone */
  }
}
