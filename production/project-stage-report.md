# Project Stage Analysis

**Date**: 2026-10-18
**Stage**: Concept（已写入 `project.yaml` 的 `project.stage` 与 `production/stage.txt`）
**Stage Confidence**: CONCERNS — 配置阶段与观察到的信号不一致（见下）
**Workflow tier**: standard（`modes.rigor: standard`）· automation: guided · review_mode: lean

## 阶段判定

- **配置阶段**：Concept（`/start` 路径 D：已有工作；引擎已配置，但仓库内没有 GDD 或架构文档 → 按 `/start` 映射为 Concept）。
- **观察阶段**：启发式表在代码根 `Assets/` 下数到 10 个 C# 文件（达到 Production 行的 10+ 阈值），引擎已配置 → 机械读数为 **Production**。
- **两者不一致的原因**：这 10 个文件是「Moonlit Glade」技术原型（移动、相机、史莱姆单次攻击、数据定义与 2 个测试文件），PROJECT.md §7 明确说明它们不代表正式战斗规则或故事设定。另有约 800 行战斗规则层代码在未合并的 `feature/battle-core`（eaae2a2，WIP，未编译），不在本分支。设计工作实际存在于仓库外的共享目录 `ThreeKingdomsJRPG-Docs`（PROJECT.md、V1_SPEC.md、ZHUOJUN_TOWN_DESIGN.md），框架的 Concept 产物 `design/gdd/game-concept.md` 尚未生成。结论：保持 Concept，作为框架意义上的起点。

## Completeness Overview

- Design: 1 份 GDD（`design/gdd/combat-system.md`，本次新建）；缺 `game-concept.md`、`systems-index.md`、art bible。外部共享目录已有完整首版规格（V1_SPEC），但尚未转换为框架格式。
- Code: 10 个文件（原型）；正式系统 0 个已合并。战斗规则层 WIP 在 `feature/battle-core`。
- Architecture: 0 ADR，无 `docs/architecture/architecture.md`（`tr-registry.yaml` 仅为框架模板）。
- Production: 无 sprint、milestone、epic。
- Tests: 2 个原型测试文件（EditMode 3/3、PlayMode 2/2 曾通过，仅技术验证）。

## Gaps Identified（按 standard 档位）

1. **没有 `design/gdd/game-concept.md`**：Concept → Systems Design 关口的唯一必需产物。共享目录的 PROJECT.md 内容足够，应以 `/brainstorm`（或 `/reverse-document concept`）把它转换进仓库，还是保持外部文档为唯一来源、仓库内只放引用？
2. **没有 `design/gdd/systems-index.md`**：战斗 GDD 目前是「索引外」系统，Category / Layer / Priority 为推断。需要 `/map-systems`。
3. **原型战斗代码 vs 新 GDD**：`DemoEncounter.cs`（按速度排序、单次攻击）是否保留为原型，还是由 `feature/battle-core` 取代？
4. **无 milestone 文档**：Alpha 0.1「涿郡起兵」范围只存在于对话与 V1_SPEC，`/scope-check` 无法以里程碑为基线。
5. **无 ADR**：标准档在 Technical Setup 才要求，现阶段不阻塞。

## Recommended Next Steps

1. `/brainstorm`（以 PROJECT.md 为输入）→ 生成 `design/gdd/game-concept.md`（含 pillars 与 Visual Identity Anchor）。
2. `/map-systems` → `systems-index.md`，把 `combat-system` 登记进去。
3. 新会话中 `/design-review design/gdd/combat-system.md`。
4. 为 Alpha 0.1 写 milestone 文档（`production/milestones/alpha-0.1.md`），之后 `/scope-check alpha-0.1` 才有基线。
5. 再次 `/gate-check systems-design`。
