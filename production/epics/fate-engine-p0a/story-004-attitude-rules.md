# Story 004: 态度规则

> **Epic**: 命运引擎 P0a：核心与工具
> **Status**: Done
> **Layer**: Core
> **Type**: Logic
> **Estimate**: S
> **Manifest Version**: N/A（尚无 control-manifest）
> **Last Updated**: 2026-10-29

## Context

**GDD**: `design/gdd/ai-character-fate.md`
**Requirement**: `TR-fate-009`
*(Requirement text lives in `docs/architecture/tr-registry.yaml`)*
**ADR Governing Implementation**: ADR-0013 命运引擎（Accepted）；ADR-0001（程序集）
**ADR Version**: v2（2026-10-27，2026-10-28 用户确认）
**Engine**: Unity 6.3 LTS · **Risk**: LOW（纯 C# 整数运算）

## Acceptance Criteria

- [x] 赵云算例 −27／−12／诡诈 +16 逐项一致（Q16.16 精确 −27.0）
- [x] 荀彧型为负、贾诩型为正
- [x] 偏好书 ×1.5 作用于 Δfavor；赠宝傲加成需 affinity
- [x] tuning.json 与规格默认值一致

## Implementation Notes

- 文件：Attitude.cs、TuningTable.cs

## Test Evidence

- FateAttitudeTests（6）；EditMode 全部通过（2026-10-29，见 PR）。
