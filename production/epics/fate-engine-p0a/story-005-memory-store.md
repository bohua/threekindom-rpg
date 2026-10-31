# Story 005: 记忆存储与压缩

> **Epic**: 命运引擎 P0a：核心与工具
> **Status**: Done
> **Layer**: Core
> **Type**: Logic
> **Estimate**: S
> **Manifest Version**: N/A（尚无 control-manifest）
> **Last Updated**: 2026-10-29

## Context

**GDD**: `design/gdd/ai-character-fate.md`
**Requirement**: `TR-fate-010`
*(Requirement text lives in `docs/architecture/tr-registry.yaml`)*
**ADR Governing Implementation**: ADR-0013 命运引擎（Accepted）；ADR-0001（程序集）
**ADR Version**: v2（2026-10-27，2026-10-28 用户确认）
**Engine**: Unity 6.3 LTS · **Risk**: LOW（纯 C# 整数运算）

## Acceptance Criteria

- [x] 同 key 覆盖
- [x] 41 条触发压缩、40 条不压
- [x] importance 5 永不压缩；4 写入 3 阶段后才压
- [x] 压缩后 memory() 仍返回 value，摘要键 sum.<tier>.<tag×3>

## Implementation Notes

- 文件：Attitude.cs（MemoryStore）

## Test Evidence

- FateMemoryTests（6）；EditMode 全部通过（2026-10-29，见 PR）。
