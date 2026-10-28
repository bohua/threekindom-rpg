# Story 001: 程序集与 DTO

> **Epic**: 命运引擎 P0a：核心与工具
> **Status**: Done
> **Layer**: Foundation
> **Type**: Config/Data
> **Estimate**: S
> **Manifest Version**: N/A（尚无 control-manifest）
> **Last Updated**: 2026-10-29

## Context

**GDD**: `design/gdd/ai-character-fate.md`
**Requirement**: `TR-fate-001`, `TR-fate-008`
*(Requirement text lives in `docs/architecture/tr-registry.yaml`)*
**ADR Governing Implementation**: ADR-0013 命运引擎（Accepted）；ADR-0001（程序集）
**ADR Version**: v2（2026-10-27，2026-10-28 用户确认）
**Engine**: Unity 6.3 LTS · **Risk**: LOW（纯 C# 整数运算）

## Acceptance Criteria

- [x] Core.Fate（ThreeKingdoms.Core 程序集内，noEngineReferences）与 Data.Fate 目录建立；Data 程序集用 Newtonsoft 加载 JSON
- [x] §4 全部结构有 [Serializable] 公有字段 DTO；link.xml 保留 Core.Fate、Data.Fate
- [x] EditMode 能加载 Assets/_Project/Data/Fate/test 合成数据集；JSON 小数被拒绝

## Implementation Notes

- 文件：FateData.cs、FateState.cs、FateDataSet.cs、Data/Fate/FateJsonLoader.cs、Assets/_Project/link.xml

## Test Evidence

- FateConditionTests.LoaderParsesConditionJsonAndRejectsFloats、LoaderRejectsNonIntegerNumbersInDtos；所有用 TestData() 的测试；EditMode 全部通过（2026-10-29，见 PR）。
