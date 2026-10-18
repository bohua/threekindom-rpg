# Review Log — design/gdd/character-progression.md

## Review — 2026-10-18 — Verdict: APPROVED
Scope signal: M
Specialists: none (lean mode; game-designer / systems-designer / qa-lead analysis done in main review by independent reviewer; guided-mode prompts took recommended defaults; registry lists no entries for this system, so sibling GDDs named in Dependencies were read instead)
Blocking items: 0 | Recommended: 7
Summary: All 5 required sections plus Formulas are present; every rule traces to V1_SPEC §1/§5/§6/§7/§9 and PROJECT.md §4.4, and the stat table (L2–L5 for 刘关张, incl. 张飞武力 101/104/107→100) and XP path (0→10→30→60, 2/3/4 级) recompute exactly. Interfaces agree with combat-system.md (combat reads level/stats, reports equal XP) and save-system.md. Remaining issues are an unlabelled XP-eligibility edge (retreated units), ACs that silently assume all three deploy while Open Question 1 is open, and a layer/dependency mismatch with systems-index.md.
Prior verdict resolved: First review
Findings:
- [RECOMMENDED] Core Rules 5 / Edge Cases: XP for a unit that deployed but retreated (0 兵力) at victory is not addressed and not marked 【待定】; add to Open Question 1 with recommendation "获得（已出战，V1_SPEC §7 等额、不稀释）".
- [RECOMMENDED] Open Questions 1: recommendation "未出战者也获得" is well supported but should cite MEMORY.md 第4轮「后备共享经验」 and V1_SPEC §7「第三战开始时为3级」(only guaranteed if bench units earn XP, since §3 allows 1–3 出战).
- [RECOMMENDED] Acceptance Criteria: the "三人各得 10 / 3 级 / 累计 60、4 级" ACs depend on OQ1 when fewer than 3 deploy; add precondition "三人均出战" or an AC for the partial-deployment case once OQ1 is resolved; add AC for 刘备 new-game state (1 级, 0 经验, 整队).
- [RECOMMENDED] Header Quick reference / Dependencies vs systems-index.md: GDD says Layer Foundation and lists 剧情事件 as an upstream hard dependency, but systems-index row 2 says "Depends On: 无（读取武将数据）"; either reframe 入队 as a call from story-events (story-events downstream caller) or update the index.
- [RECOMMENDED] Edge Cases (跨两级逐级升级): not in V1_SPEC; harmless and unreachable in V1 data, but label as implementation default/【待定】 to honour "不新增规则".
- [RECOMMENDED] Formulas variable table: cumulative_xp range 0–100 presumes OQ2's recommended cap; note "待 OQ2 确定".
- [RECOMMENDED] Advisory sections Player Fantasy / Tuning Knobs absent (advisory at standard); combat-system.md Dependencies still lists this GDD as "未编写" (stale, combat side).
Reviewed-Content-Hash: design/gdd/character-progression.md 8beac7ecdda665284c811ccb906fac8c5ca8d2b4
Reviewed-Content-Hash: design/registry/entities.yaml 216ffa67ed4d5d24548df37b2ce899fcba892285
