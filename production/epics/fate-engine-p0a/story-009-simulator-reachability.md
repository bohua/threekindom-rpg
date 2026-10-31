# Story 009: 模拟器与可达性报告

> **Epic**: 命运引擎 P0a：核心与工具
> **Status**: Done
> **Layer**: Core
> **Type**: Logic
> **Estimate**: S
> **Manifest Version**: N/A（尚无 control-manifest）
> **Last Updated**: 2026-10-29

## Context

**GDD**: `design/gdd/ai-character-fate.md`
**Requirement**: `TR-fate-014`
*(Requirement text lives in `docs/architecture/tr-registry.yaml`)*
**ADR Governing Implementation**: ADR-0013 命运引擎（Accepted）；ADR-0001（程序集）
**ADR Version**: v2（2026-10-27，2026-10-28 用户确认）
**Engine**: Unity 6.3 LTS · **Risk**: LOW（纯 C# 整数运算）

## Acceptance Criteria

- [x] 1,000 次 × 2 策略 HARD 锚点违反 0、预算超额 0、historical 不一致 0
- [x] 全不交互时（人物，阶段）偏离 ≤5%
- [x] 1,000 次 × 2 策略 ≤3 分钟
- [x] 缺 departureSceneId 的数据列出缺口；不可达的招募与救人列为缺口；输出 EPS 影响

## Implementation Notes

- 文件：FateSimulator.cs、FateTools.BatchCheck

## Test Evidence

- FateSimulatorTests（6）；EditMode 全部通过（2026-10-29，见 PR）。
