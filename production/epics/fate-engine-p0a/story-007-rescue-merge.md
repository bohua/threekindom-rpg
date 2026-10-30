# Story 007: 救人并入

> **Epic**: 命运引擎 P0a：核心与工具
> **Status**: Done
> **Layer**: Core
> **Type**: Logic
> **Estimate**: S
> **Manifest Version**: N/A（尚无 control-manifest）
> **Last Updated**: 2026-10-29

## Context

**GDD**: `design/gdd/ai-character-fate.md`
**Requirement**: `TR-fate-004`
*(Requirement text lives in `docs/architecture/tr-registry.yaml`)*
**ADR Governing Implementation**: ADR-0013 命运引擎（Accepted）；ADR-0001（程序集）
**ADR Version**: v2（2026-10-27，2026-10-28 用户确认）
**Engine**: Unity 6.3 LTS · **Risk**: LOW（纯 C# 整数运算）

## Acceptance Criteria

- [x] AC 6：eligibility 不满足时无救人；满足并完成后 T1S、存活路线、unchangedDeaths 照常
- [x] 失败按 historical 继续并广播 importance 5 记忆
- [x] 成功／失败／未触发三条路径的 WorldSlots 一致；powerTransfer 在 atNode 应用
- [x] 存活者约束挡住禁止的官职

## Implementation Notes

- 文件：FateEngine.cs（MergeDeathsAndRescues、Arbitrate）

## Test Evidence

- FateRescueTests（7）；EditMode 全部通过（2026-10-29，见 PR）。
