# Story 002: FixedMath／Hash64／Pcg32／EXP 表

> **Epic**: 命运引擎 P0a：核心与工具
> **Status**: Done
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: S
> **Manifest Version**: N/A（尚无 control-manifest）
> **Last Updated**: 2026-10-29

## Context

**GDD**: `design/gdd/ai-character-fate.md`
**Requirement**: `TR-fate-001`
*(Requirement text lives in `docs/architecture/tr-registry.yaml`)*
**ADR Governing Implementation**: ADR-0013 命运引擎（Accepted）；ADR-0001（程序集）
**ADR Version**: v2（2026-10-27，2026-10-28 用户确认）
**Engine**: Unity 6.3 LTS · **Risk**: LOW（纯 C# 整数运算）

## Acceptance Criteria

- [x] PCG32 官方 demo 向量、FNV-1a 空串与 "a"、SplitMix64 已知值通过
- [x] EXP 表 4097 项，[0]=65536、[4096]=0，整数构建，哈希锁定
- [x] isqrt、四舍五入半值远离零、定点 softmax 极端分差不溢出

## Implementation Notes

- 文件：FixedMath.cs、Random.cs

## Test Evidence

- FateMathTests（12）；EditMode 全部通过（2026-10-29，见 PR）。
