# ADR-0009: Codex 美术生产桥（本地 MCP）

## Status

Proposed（2026-10-23 起草）。用户 2026-10-23 要求：美术继续由 Codex 制作，但派发、查询、验收不再靠人工粘贴提示词和口头回报完成。

## Date

2026-10-23

## Last Verified

2026-10-23（Windows 11，Node 22.21，`@openai/codex-sdk` 0.164.0，`@modelcontextprotocol/sdk` 1.32）

## Decision Makers

- 用户 Bohua：提出自动化需求与边界（不覆盖现有资源、未经授权不触发付费生成、Claude 验收后才进 Unity）
- 起草：Claude；评审：technical-director、lead-programmer

## Summary

在仓库 `tools/codex-asset-bridge/` 新增一个本地 stdio MCP 服务器。Claude 通过它调用 `asset_generate / asset_status / asset_preview / asset_revise / asset_approve`（另有 `asset_list / asset_cancel`）。服务器用官方 Codex TypeScript SDK 在**独立的后台工作进程**里驱动 Codex 线程，Codex 用自带的 `image_gen` 工具出图，写入隔离的暂存目录；桥自己校验文件并生成结构化清单，Claude 预览、批准后才把文件复制到正式美术目录。Unity 导入仍由 Claude 在游戏仓库里另做，不属于桥的职责。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | 与引擎无关（Node 工具）；产物供 Unity 6.3 导入 |
| **Domain** | Tooling／美术管线 |
| **Layer** | 工具（不进游戏构建） |
| **Knowledge Risk** | MEDIUM：Codex SDK 0.x 仍在快速变动；`image_gen` 是 Codex 内建工具，由 CLI 配置决定是否可用 |
| **References Consulted** | `@openai/codex-sdk` 0.164 的 `index.d.ts`；`codex exec` 实测工具清单（含 `image_gen__imagegen`）；现有 `D:\BuildArtifacts\Codex\hd2d-jrpg\art\*\CODEX_PROMPT.md` 与 `CODEX_STATUS.txt` 约定 |
| **Post-Cutoff APIs Used** | Codex SDK `Codex.startThread / resumeThread / Thread.runStreamed`，`TurnOptions.outputSchema` |
| **Verification Required** | 单元测试（状态机、路径约束、PNG 校验、不覆盖规则）；一次真实人物立绘端到端（生成→预览→批准） |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001（仓库结构：工具放 `tools/`，不进 `Assets/`） |
| **Enables** | 美术任务自动派发与验收；后续可把 Unity 导入做成独立步骤 |
| **Blocks** | 无 |

## Context

现在的流程：Claude 写 `CODEX_PROMPT.md` → 用户手动粘贴给 Codex → Codex 出图并写 `CODEX_STATUS.txt` → 用户回来告诉 Claude。用户是瓶颈，且容易漏报。Codex 提供官方 TypeScript SDK（包装 `codex exec`），线程可持久化、可恢复；实测 `codex exec` 线程里有 `image_gen` 工具，生成的图保存在 `~/.codex/generated_images/<thread-id>/`。

## Decision

1. **形态**：stdio MCP 服务器（`dist/src/server.js`），`npm run deploy` 复制到 `%LOCALAPPDATA%\codex-asset-bridge\app`（与 worktree 无关），仓库根 `.mcp.json` 注册为 `codex-assets`。运行时依赖只有官方 `@openai/codex-sdk`、`@modelcontextprotocol/sdk`、`zod` 与 PNG 解码用的 `pngjs`，全部锁定精确版本。
2. **异步**：`asset_generate` 立即返回任务 ID。实际生成由**分离的工作进程**（`dist/worker.js <taskId>`，detached）执行，因此 Claude 会话或 MCP 服务器重启不会中断任务。工作进程退出前写回最终状态；`asset_status` 发现 `running` 但进程已不在时标为 `failed`（孤儿检测）。
3. **状态持久化**：每个任务一个 JSON（`<root>/tasks/<id>.json`），写临时文件后原子改名。记录：状态、规格、Codex 线程 ID、修订号、每次运行的 token 用量、错误、预算、清单、预览记录、批准记录、事件日志尾部。`root` 默认 `D:\BuildArtifacts\Codex\hd2d-jrpg\art\_bridge`，可用 `CODEX_BRIDGE_ROOT` 覆盖。
4. **状态机**：`planning_only`／`queued` → `running` → `review`（等 Claude 验收）→ `approved`；任一步可到 `failed`／`blocked`／`cancelled`；`review` 可经 `asset_revise` 回到 `queued`。
5. **生成授权**：`asset_generate` 的 `generation_authorized` 默认 `false`，此时 Codex 只写提示词与规格（`_prompts/`），**禁止调用生图工具**，状态停在 `review`（标记 plan-only）。Claude 只有在用户同意后才传 `true`。任务级标志会持久化，修订沿用。
6. **文件系统限制**：每个任务的修订写在 `<root>/staging/<id>/r<N>/`，Codex 线程以 `sandboxMode: "workspace-write"`、`workingDirectory` = 该修订目录运行（可读其他位置做参考，只能写这里）；`approvalPolicy: "never"`，`networkAccessEnabled: false`，`webSearchMode: "disabled"`。桥本身只写 `<root>` 下，以及批准时的目标目录。所有路径参数在规范化后检查是否落在允许的根下（防 `..`、绝对路径逃逸）。
7. **预算与重试**：每任务预算 `max_retries`（默认 2，单次运行失败后在同一线程恢复重试）、`max_revisions`（默认 3）、`max_images`（默认 6，按 `generated_images/<thread>` 新增文件计数，超出则失败且不再重试）、`timeout_minutes`（默认 45，AbortSignal 中止）、`max_total_tokens`（默认 3,000,000，超出不再重试或修订）。
8. **校验与清单**：Codex 以 `outputSchema` 返回 JSON（交付文件列表、自检、风险）。桥不信任它：自己遍历修订目录，逐个文件检查存在、位于目录内、PNG 签名、宽高、色彩类型、是否二值透明（按规格要求）、SHA-256；与规格里的 `expected_files`（文件名＋尺寸）比对。结果写 `manifest.json` 并入任务状态。校验失败视为本次运行失败，进入重试。生成源图从 `~/.codex/generated_images/<thread-id>/` 复制到 `_sources/` 留档。
9. **验收**：`asset_preview` 返回清单与图像内容（MCP image block，超大文件只给路径），并记录“该修订已预览”。`asset_approve` 只接受状态为 `review`、非 plan-only、校验通过、且当前修订已预览的任务；把交付文件（不含 `_sources/`、`_prompts/`）复制到 `art/<target_dir>/`。**目标目录已存在且非空则拒绝**，绝不覆盖现有资源。同时写 `CODEX_STATUS.txt`（`STATUS: DONE`）与 `manifest.json`，兼容现有交接约定。
10. **Unity 边界**：桥不碰游戏仓库与 `Assets/`。导入 Unity、改场景仍是 Claude 在 worktree 里的独立开发步骤。

## Alternatives Considered

- **继续用 `CODEX_STATUS.txt` 轮询，只把粘贴步骤自动化（直接 `codex exec`）**：没有线程 ID、预算、校验和验收门槛，失败时无法恢复同一线程。不采用，但保留 `CODEX_STATUS.txt` 兼容输出。
- **在 MCP 服务器进程内跑 Codex**：实现最简单，但 Claude 会话结束就丢任务。不采用，改为分离的工作进程。
- **桥直接调用 OpenAI 图像 API**：要单独的 API Key 与按次付费，且失去 Codex 现有的修图、切格、自检能力。不采用。
- **Codex 直接写正式美术目录**：可能覆盖已有资源，也跳过 Claude 验收。不采用。

## Consequences

- 用户不再需要搬运提示词和完成通知；Claude 可以连续派发、检查、要求修改。
- 新增一个 Node 工具目录（含 `package-lock.json`），需要 `npm ci && npm run build` 一次。
- Codex SDK 版本锁定在 0.164.0；升级需重跑测试和一次端到端。
- 生图消耗用户 Codex 账号额度；授权开关让 Claude 在用户同意前只能做 plan-only。

## Risks

- `image_gen` 的可用性由 Codex 配置／账号决定；不可用时任务应以 `blocked` 结束并说明原因。
- Windows 沙箱（`elevated`）下 `workspace-write` 的写入范围若与预期不同，可能写到修订目录外；桥的校验只认修订目录内文件，批准只复制这些文件。
- 分离进程在机器重启时会中断；孤儿检测会把任务标为失败，可用 `asset_revise` 在原线程上继续。

## GDD Requirements Addressed

无直接 GDD 条目；服务 V1_SPEC 第 10 节“最终需达到基本统一的美术目标”的生产效率。

## Validation Criteria

- `npm test`：状态机转换、路径逃逸被拒、不覆盖规则、PNG 校验（尺寸、二值透明）、预算判定。
- 端到端：一次人物立绘任务走完 generate → status → preview → approve，正式目录出现文件与 `CODEX_STATUS.txt`，未改动任何已有文件。

## Performance Implications

不进游戏运行时，无帧率或内存影响。代价只有磁盘（暂存与 `_sources/` 留档）和用户的 Codex 额度。暂存目录目前不自动清理：已批准任务的 `staging/<id>` 可手动删除，状态 JSON 保留作记录。

## 评审记录（2026-10-23）

technical-director：APPROVED WITH CHANGES；lead-programmer：ACCEPT WITH REQUIRED CHANGES。已纳入实现：

| 意见 | 处理 |
| --- | --- |
| 只出提示词仅靠提示词约束 | 只出提示词时以 `features.image_generation=false` 在 Codex 配置层关闭生图；运行中若生图目录出现新文件，立即中止并以“策略违规”失败 |
| 授权只是布尔值 | `generation_authorized=true` 必须附 `user_consent`（用户原话＋时间），持久化在任务中 |
| 生图预算只能事后计 | SDK 无生图事件，worker 每 3 秒轮询 `generated_images/<线程>`，超出 `max_images` 立即中止；图片与 token 预算跨重试、修订累计；token 只在每轮结束得知，可能超出一轮 |
| 两个进程写同一状态文件 | 单写者：worker 存活（queued/running）期间只有它写任务 JSON；服务器的取消请求写独立的 `.cancel` 标记；服务器只在非活动状态下、持有任务锁时写；每次写入带版本号乐观校验，Windows 上改名遇 EPERM/EBUSY 退避重试，读取容忍缺失或半写文件 |
| 只看 PID 判孤儿不可靠 | worker 每 15 秒写心跳（pid、随机 nonce、时间）并刷新 worker 锁；心跳超过 60 秒且 PID 不在（或超过 4 分钟无心跳）才判定孤儿；worker 锁（`wx` 创建）防止同一任务被两个 worker 同时执行 |
| 中止只杀 `codex.exe` | 超时／取消后由 worker 用 `taskkill /T /F` 结束自己的所有子进程树；取消先协作，40 秒无响应由服务器结束 worker 进程树；最终状态在 `finally` 写回 |
| 每次运行与整个任务的超时 | `run_timeout_minutes`（单次运行）与 `task_timeout_minutes`（累计运行时间）分开 |
| 路径与批准竞争 | 路径按 realpath（含联接点）解析、Windows 下不区分大小写比较；批准先复制到同级临时目录再改名，复制前后两次确认目标为空，持任务锁 |
| 校验失败不应反复重试 | 确定性的校验失败只把问题回馈 Codex 一次；运行出错才按 `max_retries` 重试 |
| 版本未锁定、缺测试 | 依赖全部锁定精确版本（SDK 0.164.0，自带 CLI 0.164.0），`node:test` 单元测试用假 Runner 覆盖状态机 |
| 子进程环境 | 只传 Codex 必需的环境变量（PATH、用户目录、TEMP 等），不继承其他变量；桥代码从不读取 `~/.codex/auth.json` |
| 预览体积 | 预览用最近邻缩放（像素图不糊），每次最多 4 张、总计约 3MB |
| `networkAccessEnabled:false` 是否影响生图 | 由人物立绘端到端测试确认（见 Validation Criteria） |

## Related

- 共享文档：`CODEX_ASSET_BRIDGE.md`（用法）
- `tools/codex-asset-bridge/README.md`
