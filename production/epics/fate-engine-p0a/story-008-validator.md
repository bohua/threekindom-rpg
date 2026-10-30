# Story 008: 校验器

> **Epic**: 命运引擎 P0a：核心与工具
> **Status**: Done
> **Layer**: Core
> **Type**: Logic
> **Estimate**: S
> **Manifest Version**: N/A（尚无 control-manifest）
> **Last Updated**: 2026-10-29

## Context

**GDD**: `design/gdd/ai-character-fate.md`
**Requirement**: `TR-fate-013`
*(Requirement text lives in `docs/architecture/tr-registry.yaml`)*
**ADR Governing Implementation**: ADR-0013 命运引擎（Accepted）；ADR-0001（程序集）
**ADR Version**: v2（2026-10-27，2026-10-28 用户确认）
**Engine**: Unity 6.3 LTS · **Risk**: LOW（纯 C# 整数运算）

## Acceptance Criteria

- [x] 干净的合成数据零问题
- [x] 简报 §5 的 11 类故障样本各被对应校验拦截（另加 V-01 引用、V-06 权位与主角案例）
- [x] 菜单项 Tools/Fate/Validate Test Data

## Implementation Notes

- 文件：FateValidator.cs、EditorTools/Fate/FateTools.cs

## Test Evidence

- FateValidatorTests（21）；EditMode 全部通过（2026-10-29，见 PR）。
