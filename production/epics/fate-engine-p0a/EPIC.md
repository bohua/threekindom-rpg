# Epic: 命运引擎 P0a：核心与工具

> **Status**: Done（2026-10-29）
> **GDD**: `design/gdd/ai-character-fate.md`（规格 v5.1）
> **ADR**: `docs/architecture/adr-0013-fate-engine.md`（Accepted；原稿编号 0012，因仓库已占用顺延为 0013）
> **Brief**: 项目共享 `design/ai-character-fate/P0A_ENGINE_CORE_BRIEF.md`
> **Layer**: Core（纯 C#，不依赖场景、存档系统与节点状态机）

## Goal

交付 `ThreeKingdoms.Core.Fate` ＋ `ThreeKingdoms.Data.Fate` ＋ EditMode 测试 ＋ 校验器 ＋ 模拟器（含可达性报告）＋ 合成测试数据集。
退出标准（规格 §10）：AC 2、3、5、6 在合成数据上通过；模拟器 1,000 次 × 2 策略 HARD 锚点违反 0；记录单人单阶段候选撰写工时。

## Out of Scope

`ThreeKingdoms.Save`、年份节点状态机、`InteractionRecorder`／0.2 记忆写入、Gameplay／UI／Narrative（P0b，0.4 第一个故事）；文本库与 lint（P1）；ChronicleRecord（0.5）；物品流转／适性变化逻辑（只留字段）。

## Stories

| # | Story | Layer | Status |
| --- | --- | --- | --- |
| 001 | [程序集与 DTO](story-001-assemblies-dto.md) | Foundation | Done |
| 002 | [FixedMath／Hash64／Pcg32／EXP 表](story-002-fixed-math-random.md) | Foundation | Done |
| 003 | [条件语言](story-003-condition-language.md) | Core | Done |
| 004 | [态度规则](story-004-attitude-rules.md) | Core | Done |
| 005 | [记忆存储与压缩](story-005-memory-store.md) | Core | Done |
| 006 | [结算八步](story-006-settle-eight-steps.md) | Core | Done |
| 007 | [救人并入](story-007-rescue-merge.md) | Core | Done |
| 008 | [校验器](story-008-validator.md) | Core | Done |
| 009 | [模拟器与可达性报告](story-009-simulator-reachability.md) | Core | Done |
| 010 | [命运面板（最小）](story-010-fate-panel.md) | Presentation | Done |

## 对规格的实装偏离（P0a）

| 条款 | 偏离 | 理由 |
| --- | --- | --- |
| ADR §Key Interfaces `Settle(..., SaveDocument, ...)` | 入参为 `FateSaveSection`；`SaveDocument` 留在 `ThreeKingdoms.Save`，由 P0b 包装 | Core 不能引用 Save；P0a 不碰存档 |
| ADR `StageEvent.Json` | 改为带类型字段（Memory、CharacterId、RecruitState、ItemId、CaseId、Result），另加 `death` 事件 | Core 没有 JSON 解析器 |
| ADR `IConditionContext` | 增加 `FactionDestroyed`、`EventSeen`（谓词 factionDestroyed／eventSeen 需要） | §4.4 谓词表 |
| §4.3 `effect` | 增加 `presentAt`（本阶段出场场景）与 `post`；`FateOption.tags` 为八维性格向量；`FateRoute` 增加 `routeId`（存活路线）与 `genericOptions` | presentAt 锚点、唯一官职仲裁、fitP、afterRouteId 需要可求值的数据 |
| §4.2 档案 | 增加 `initialFaction/initialLocation/initialRecruitState/initialRole` | 无存档时初始化篇章 |
| §4.3 锚点 | `kind: "dies"` 表示 dies 锚点 | 规格未给出 dies 锚点的数据形态 |
| §5.2⑥ 偏离预算 | 只计 T1／T2／T1S；已宣告的两步提交在提交阶段先占预算、不被预算挤回 | 规格以在场 T1/T2 定额度；预警过的离队不应被预算吞掉 |
| §5.2① RUMOR | 未写 | 依赖文本库（P1） |
| §8.2 可达性 | 报告“与在队冲突且缺 departureSceneId”的锚点与从未到达 available 的人物、从未成立的救人 | 简报 S9 |
| 合成数据 | 节点 tv.N1–tv.N5（简报为 N1–N3），N4／N5 仅供两步提交冷却用例 | 冷却需要提交阶段之后再两个阶段 |
