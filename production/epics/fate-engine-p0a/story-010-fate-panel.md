# Story 010: 命运面板（最小）

> **Epic**: 命运引擎 P0a：核心与工具
> **Status**: Done
> **Layer**: Presentation
> **Type**: UI
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

- [x] EditorWindow 列出一次结算每人的候选、状态、分值、权重、因果令牌、仲裁
- [x] 结果可导出 JSON

## Implementation Notes

- 文件：EditorTools/Fate/FateTools.cs（FatePanel，菜单 Tools/Fate/Fate Panel）

## Test Evidence

- 编辑器工具，无自动测试；编译通过；EditMode 全部通过（2026-10-29，见 PR）。
