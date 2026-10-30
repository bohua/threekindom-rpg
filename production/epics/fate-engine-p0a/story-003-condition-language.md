# Story 003: 条件语言

> **Epic**: 命运引擎 P0a：核心与工具
> **Status**: Done
> **Layer**: Core
> **Type**: Logic
> **Estimate**: S
> **Manifest Version**: N/A（尚无 control-manifest）
> **Last Updated**: 2026-10-29

## Context

**GDD**: `design/gdd/ai-character-fate.md`
**Requirement**: `TR-fate-008`
*(Requirement text lives in `docs/architecture/tr-registry.yaml`)*
**ADR Governing Implementation**: ADR-0013 命运引擎（Accepted）；ADR-0001（程序集）
**ADR Version**: v2（2026-10-27，2026-10-28 用户确认）
**Engine**: Unity 6.3 LTS · **Risk**: LOW（纯 C# 整数运算）

## Acceptance Criteria

- [x] §4.4 每个谓词正反各一测
- [x] alive 含 pendingDeath
- [x] battleObjective 只在 fulfillment 求值为真
- [x] V-02 拒绝未知谓词

## Implementation Notes

- 文件：Condition.cs

## Test Evidence

- FateConditionTests（18）；EditMode 全部通过（2026-10-29，见 PR）。
