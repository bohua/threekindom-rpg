# Codex 美术生产桥（codex-assets MCP）

决策见 `docs/architecture/adr-0009-codex-asset-bridge.md`。Claude 通过本地 stdio MCP 服务器把美术任务派给 Codex，查询进度、预览、要求修改、批准入库；不再需要人工粘贴提示词或回报完成。

## 安装 / 更新

```powershell
cd tools/codex-asset-bridge
npm ci
npm test          # 编译 + 单元测试
npm run deploy    # 复制到 %LOCALAPPDATA%\codex-asset-bridge\app（与 worktree 无关的固定位置）
```

仓库根的 `.mcp.json` 已登记 `codex-assets`（指向上面的固定位置）。在本仓库打开 Claude Code 时批准一次即可。其他目录（如共享文档目录）要用，在终端运行一次：

```powershell
claude mcp add --scope user codex-assets -- node "$env:LOCALAPPDATA\codex-asset-bridge\app\dist\src\server.js"
```

前提：本机 Codex 已登录（桥不读取 `~/.codex/auth.json`，只让 Codex 自己使用）。

## 工具

| 工具 | 作用 |
| --- | --- |
| `asset_generate` | 新建任务，立即返回 ID；后台进程驱动 Codex。`generation_authorized` 默认 false＝只写提示词（Codex 的生图功能在配置层关闭）。设为 true 必须附 `user_consent`（用户原话＋时间）。 |
| `asset_status` | 无参数＝最近任务列表；带 ID＝状态、预算用量、清单、最近 Codex 动作。 |
| `asset_preview` | 返回校验后的清单、缩小的图像和提示词文件；**批准前必须预览当前修订**。 |
| `asset_revise` | 在同一 Codex 线程上给修改意见，产出新修订目录；也用于继续失败／受阻任务，或在用户同意后从只出提示词升级为生图。 |
| `asset_approve` | 把通过校验且已预览的修订复制到 `art/<target_dir>`（目录已存在且非空则拒绝，绝不覆盖），并写 `CODEX_STATUS.txt`。导入 Unity 是之后的独立步骤。 |
| `asset_cancel` | 先请求后台进程停止，40 秒无响应则结束进程树。 |

## 目录

- 状态：`D:\BuildArtifacts\Codex\hd2d-jrpg\art\_bridge\tasks\<id>.json`（另有 `.events.jsonl` Codex 动作、`.log` 后台日志、`.heartbeat.json` 心跳）
- 暂存：`...\art\_bridge\staging\<id>\r<N>\`，Codex 只能写这里；`_sources/` 生成原图（含从 `~/.codex/generated_images/<线程>` 复制的留档），`_prompts/` 提示词，`_checks/` 自检
- 批准后：`...\art\<target_dir>\`

环境变量可改位置：`CODEX_BRIDGE_ART_ROOT`、`CODEX_BRIDGE_ROOT`、`CODEX_BRIDGE_GENERATED_IMAGES`。

## 预算（每任务，可在 `asset_generate.budget` 覆盖）

`max_retries` 2（单次运行出错后在同一线程重试；校验失败只回馈一次）、`max_revisions` 3、`max_images` 6（运行中轮询生图目录，超出立即中止；跨重试和修订累计）、`run_timeout_minutes` 45、`task_timeout_minutes` 240（累计运行时间）、`max_total_tokens` 3,000,000（只在每轮结束时得知，可能超出一轮）。

## 命令行（调试）

```powershell
node scripts/cli.mjs generate examples/tao-qian-portrait.json            # 只出提示词
node scripts/cli.mjs generate <spec.json> --authorize "用户原话 时间"      # 允许生图
node scripts/cli.mjs status <id> | list | wait <id> | preview <id> | approve <id> | cancel <id>
node scripts/cli.mjs revise <id> "修改意见" [--authorize "用户原话 时间"]
node scripts/mcp-smoke.mjs        # 启动 MCP 服务器并列出工具
node scripts/probe-tools.mjs      # 让 Codex 列出它的工具（不生图）
```
