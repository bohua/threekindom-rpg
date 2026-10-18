# Review Log — equipment-items.md

## Review — 2026-10-18 — Verdict: NEEDS REVISION
Scope signal: M (6 dependencies, 2 formulas — sell price and additive equip bonuses; inventory/stock data model may warrant an ADR together with the save format; producer should verify before sprint planning)
Specialists: none (lean mode; economy-designer / systems-designer / qa-lead analysis done in main review by independent reviewer). Automation guided: Phase 1 freshness check returned RECEIPT: NONE → full review; Phase 5 defaulted to "[B] Stop here — revise in a separate session" and "append to review log"; systems-index and other files not touched (caller restricted writes to this log).
Blocking items: 1 | Recommended: 6
Summary: Rules faithfully restate V1_SPEC §1/§6/§8/§9 and ZHUOJUN §3/§4 (N04 sole trader, buy/sell/compare/swap, no 强化/耐久/负重/词条); no rule exceeds the spec. The 12-item draft (草案待用户确认) matches §8 composition exactly (3+1+3+1+4), prices sit in range (improved weapons 60, armor 50, shop accessories 40/60/80), uses only 物攻/物防/兵力/技力/速度, and has no supernatural effects; worked examples check out against combat-system formulas (关羽 55+9=64, 刘备 42+8=50, speed 8+1=9). Copper budget is sane: 120 → 150 → 200 spendable before battle 3, ~3 purchases (≈4 if starter gear is sold for 15/10), no arbitrage loop (buy 60/sell 30; gifts not stocked). Blocking: item instance/stock model is undefined — "最多 12 件" is clearly item types (three 布甲 exist), but whether the shop stock is unlimited, whether 皮甲/accessories can be bought multiple times, and how inventory counts duplicates are not stated, so the inventory/save schema and the budget claim cannot be implemented or verified without guessing.
Prior verdict resolved: First review
Findings:
- [BLOCKING] Core Rules 6/13–16 + States: no rule on item instances vs. types, shop stock (unlimited vs. one each) or duplicate purchases (e.g. 3× 皮甲 for all three generals). Fix: add a rule — recommended "目录 12 种为物品种类；商店商品不限量，可重复购买；库存按种类计数，装备归属引用种类" marked 推荐暂用，待用户确认 in Open Questions, and re-state the budget line under that rule.
- [RECOMMENDED] Formulas 设计意图: "快马鞭速度 +1 不足以改变兵种速度排序" is false when stacked — 刘备 角弓 +1 + 快马鞭 +1 = 10 ties 关羽 (骑 10); 张飞 6+1=7 ties 张角 (7) and, by combat rule 5, then acts first. Fix: reword the intent (or state the tie outcome); low impact since 快马鞭 is unobtainable in Alpha 0.1.
- [RECOMMENDED] Formulas catalog #12: 快马鞭 买价/售价 "待定" — V1_SPEC §8 requires gifts without a buy record to have an explicitly configured sell price. Fix: give a draft gift_sell_price (e.g. 20) or mark it 【待定】 in an OQ.
- [RECOMMENDED] Cross-doc (combat-system.md): still lists equipment-items as "未编写", copper as owned by a separate "经济系统" (rules §成长与奖励, Interactions, Dependencies, Cross-References), and equip_* ranges as 待定. Fix: propagate — owner is this GDD; draft ranges equip_phys_atk 0–9, equip_phys_def 0–9 (皮甲 6 + 护腕 3), equip_troops 0–30 (皮甲 10 + 军旗 20), equip_sp 0–2, equip_speed 0–2, equip_tac_atk/def 0 (not used by catalog). Same "未编写" stale status in game-ui.md (Dependencies, Cross-References) and save-system.md (Dependencies).
- [RECOMMENDED] Dependencies vs systems-index.md: index row #3 lists only 武将成长与技能 as dependency; GDD adds 城镇探索 (hard upstream), 剧情事件, 存档, 界面. Fix: sync the index row.
- [RECOMMENDED] Edge Cases: empty armor slot (selling 布甲) is not covered alongside the empty-weapon case; 军粮 sale (OQ4) and who sells 军粮 (N04 — ZHUOJUN N11 粮贩 has no trade UI) should be stated as rules, not only inside OQ1/OQ4. Fix: add one edge case and one line under 商店 rules.
- [RECOMMENDED] Acceptance Criteria: missing ACs for stacked bonuses from multiple slots (e.g. 皮甲+军旗 → 兵力 +30), selling 军粮 for 5, duplicate purchase per the new stock rule, and save→load equality of inventory/equipment/copper. "物品数值…来自数据文件，无硬编码" is a code-review check, not a play test — keep but tag as review/automated-data test.
- [NICE-TO-HAVE] Economy note: the battle-3 reward (+60) has no sink in Alpha 0.1 (ending follows immediately) — V1_SPEC-mandated, no change; just note it so playtest copper totals are read as 200 spendable.
- [NICE-TO-HAVE] Player Fantasy and Tuning Knobs sections absent — advisory at `standard`; a short Tuning Knobs list (sell ratio, prices, 军粮 heal %, starting copper/军粮) would help balance-check.
Reviewed-Content-Hash: design/gdd/equipment-items.md a87c72bc4d6ede4baf67f83d7b0d39816ce8e7ba
Reviewed-Content-Hash: design/registry/entities.yaml 216ffa67ed4d5d24548df37b2ce899fcba892285

## Review — 2026-10-18 — Verdict: APPROVED
Scope signal: M (unchanged: 6 dependencies, 2 formulas; inventory/stock data model still a candidate for an ADR alongside the save format)
Specialists: none (lean mode; economy-designer / systems-designer / qa-lead analysis done in main review by independent reviewer). Automation guided: Phase 1 freshness check → doc CHANGED, registry UNCHANGED → full re-review; Phase 5 defaults taken; only this log appended (caller restricted writes; systems-index not touched).
Blocking items: 0 | Recommended: 3
Summary: Full re-read against latest V1_SPEC §8/§9 and ZHUOJUN §3/§4. The prior blocker is resolved: rule 8a + OQ7 define "12 件" as 12 item types, shop stock unlimited with repeat purchase, inventory counted per type (推荐暂用，待用户确认（2026-10-18）), the budget line is restated under it, and an AC covers buying a second 皮甲. All six prior recommendations were addressed (speed-tie wording now correct per combat rule; 快马鞭 gift_sell_price 30 draft; combat-system/game-ui/save-system/systems-index now reference this GDD with equip_* ranges; empty-armor edge case and N04 军粮 收购 rule added; stacked-bonus, 军粮 sale, duplicate-purchase and save→load ACs added). No rule exceeds the spec; no new blockers.
Prior verdict resolved: Yes
Findings:
- [RECOMMENDED] States and Transitions / rule 8a: per-type counting is not tied to equipped copies — does "库存皮甲数量为 2" include one worn by a general? Fix: state that inventory count = unequipped copies, equipment ownership references a type, and unequip returns +1 to that count; reword the duplicate-purchase AC to "未装备的皮甲数量为 2".
- [RECOMMENDED] Acceptance Criteria: "物品数值…来自数据文件，无硬编码" is still untagged — mark it as a code-review / automated data check, not a play test (carried from prior review).
- [RECOMMENDED] Edge Cases / OQ2: the "武器槽为空时出战前提示一次" behaviour lives only in OQ2; add it as an edge case and an AC so game-ui and battle-prep know who shows it.
- [NICE-TO-HAVE] Player Fantasy and Tuning Knobs sections still absent — advisory at `standard`.
Reviewed-Content-Hash: design/gdd/equipment-items.md 7a1699139be182a9fe88f0e874192b812bb56677
Reviewed-Content-Hash: design/registry/entities.yaml 216ffa67ed4d5d24548df37b2ce899fcba892285
