// Start the MCP server over stdio, list its tools and call asset_status (read-only).
import path from "node:path";
import { fileURLToPath } from "node:url";
import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StdioClientTransport } from "@modelcontextprotocol/sdk/client/stdio.js";

const server = process.argv[2] ?? path.join(path.dirname(fileURLToPath(import.meta.url)), "..", "dist", "src", "server.js");
const client = new Client({ name: "smoke", version: "0.0.0" });
await client.connect(new StdioClientTransport({ command: process.execPath, args: [server] }));
const { tools } = await client.listTools();
console.log("tools:", tools.map((t) => t.name).join(", "));
const res = await client.callTool({ name: "asset_status", arguments: {} });
console.log(res.content[0].text.slice(0, 1500));
await client.close();
