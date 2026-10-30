# Story 006: 结算八步

> **Epic**: 命运引擎 P0a：核心与工具
> **Status**: Done
> **Layer**: Core
> **Type**: Logic
> **Estimate**: S
> **Manifest Version**: N/A（尚无 control-manifest）
> **Last Updated**: 2026-10-29

## Context

**GDD**: `design/gdd/ai-character-fate.md`
**Requirement**: `TR-fate-001`, `TR-fate-002`, `TR-fate-003`, `TR-fate-007`, `TR-fate-011`, `TR-fate-012`
*(Requirement text lives in `docs/architecture/tr-registry.yaml`)*
**ADR Governing Implementation**: ADR-0013 命运引擎（Accepted）；ADR-0001（程序集）
**ADR Version**: v2（2026-10-27，2026-10-28 用户确认）
**Engine**: Unity 6.3 LTS · **Risk**: LOW（纯 C# 整数运算）

## Acceptance Criteria

- [x] AC 2：同种子同日志逐字段一致（含 JSON 往返后的状态）
- [x] AC 3：无交往时 T1 只有默认；importance ≥3 交往后同向候选进入打分；importance 2 不算因
- [x] AC 5：首次胜出只宣告、提交阶段执行并退回赠物；拉回好感则撤销并冷却一阶段
- [x] RELATION 令牌按相对上升；HISTORY 分发按 scope/delay；RANDOM 走随机流
- [x] 偏离预算不超额；在队人物只有默认与两步离队候选

## Implementation Notes

- 文件：FateEngine.cs、SettleTypes.cs

## Test Evidence

- FateEngineTests（22）；EditMode 全部通过（2026-10-29，见 PR）。
