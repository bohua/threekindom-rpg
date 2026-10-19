# Review Log — game-ui.md

## Review — 2026-10-18 — Verdict: NEEDS REVISION
Scope signal: M
Specialists: none (lean mode; ux-designer / ui-programmer / qa-lead analysis done in main review by independent reviewer; guided-mode prompts took recommended defaults; freshness check RECEIPT: NONE → full review)
Blocking items: 1 | Recommended: 9
Summary: All 5 standard-required sections are present (Formulas present; the UI owns no numeric rules, sell price/diff correctly attributed). Combat, shop, save and teaching rules match V1_SPEC §2.2/§4/§8/§9/§10, ZHUOJUN §4 and the user-confirmed combat rules (SP-greyed skills, 整队 self-target); 「测试版结束」 is sourced from production/milestones/alpha-0.1.md, not invented. Blocking: character-progression.md declares a 城镇／整备武将页 (level, XP, core + derived stats, skills) owned by this GDD, but game-ui.md defines no such screen and its town HUD → 「菜单」 transition leads to an undefined state.
Prior verdict resolved: First review
Findings:
- [BLOCKING] Detailed Design / States and Transitions: no 武将页／队伍菜单, though character-progression.md UI Requirements (row 4) and Dependencies require one; 城镇 HUD →「菜单」 has no state row. Fix: add a 武将页 rule (等级、经验／下一级、武力统率智力、派生属性、技能及说明) reachable from town and 战前准备; layout/entry 【待定】 (推荐：城镇菜单键＋战前准备内入口).
- [RECOMMENDED] Core Rules 3: 「按出战位顺序」 command-input order is not in V1_SPEC/combat-system — mark 【待定】 with that as the recommended answer.
- [RECOMMENDED] Core Rules 11/15/16: V1_SPEC §4.1 lists 兵种 in pre-battle confirmation and combat-system adds enemy-only 黄巾军／文官军师 types; require showing each unit's 兵种 in prep and battle (克制 teaching depends on it).
- [RECOMMENDED] Core Rules (战斗): no skill description requirement; V1_SPEC §4/§6 require durations to state 「含施放当回合」. Add: 技能说明 shows cost, target, effect, duration incl. cast turn.
- [RECOMMENDED] Core Rules 20/23: 「装备者」 ambiguous with 3 members when buying — 【待定】, 推荐 show diff for each member who can equip it (不可装备者标明).
- [RECOMMENDED] Core Rules 22: V1_SPEC §8 one-off story gifts use a configured sell price (no 50% of buy price, no buy-back arbitrage) — not covered; display configured price.
- [RECOMMENDED] Dependencies: missing 城镇探索与交互 (#7, Not Started) though rules 2, dialog box, town HUD rely on it; 装备道具与商店 GDD not written (hard dep). Also story-events expects 目标提示／过场字幕 from UI — add an explicit rule for objective display.
- [RECOMMENDED] Save UI (Core Rules 28–29): not reflecting save-system.md items — 存档成功／失败提示, 覆盖确认 (save OQ2), slot summary fields (save OQ4), save unavailable during 演出; reference them.
- [RECOMMENDED] Acceptance Criteria: 「任一界面，THEN 文字为中文」 not testable → 「逐屏检查清单（标题、城镇、对话、商店、存读档、战前、战斗、结算、测试版结束）无非中文 UI 字符串（按键图标除外）」; 破阵 AC should state expected value (施放当回合显示 2、下回合 1); add ACs for rule 21 (武器兵种不可用), 32 (演出期间菜单不响应), 2 (接近才出现提示), 铜钱不足.
- [RECOMMENDED] Advisory sections Player Fantasy / Tuning Knobs absent (advisory at standard).
- [NICE] Edge Cases: 「若所有饰品槽为空」→「若该武将饰品槽为空」; add edge for 最后一个敌人撤退时队列剩余动作不再执行 (combat rule 9).
- [NICE] Header 规则来源: cite production/milestones/alpha-0.1.md for 「测试版结束」. Sibling note: combat-system.md Dependencies still lists 「战斗界面 未编写」 — stale.
Reviewed-Content-Hash: design/gdd/game-ui.md 3a61a4b401b330744ca9b2bd5886abfafea77aaa
Reviewed-Content-Hash: design/registry/entities.yaml 216ffa67ed4d5d24548df37b2ce899fcba892285

## Review — 2026-10-18 — Verdict: APPROVED
Scope signal: M
Specialists: none (lean mode; ux-designer / ui-programmer / qa-lead analysis done in main review by independent reviewer). Automation guided: freshness check CHANGED → full re-review; Phase 5 prompts took defaults (append to review log only).
Blocking items: 0 | Recommended: 4
Summary: Re-review at commit 277c925. The prior blocker is resolved: rules 17a/17b define a 武将页 (等级、经验与下一级、三核心与派生属性、兵种、技能、装备) reachable from town and 战前准备, entry provisional in OQ10, with an AC. Nearly all prior recommendations applied (OQ9 command order, 兵种 display incl. 黄巾军/文官军师, 11a skill text with 「含施放当回合」, per-member diff, story-gift configured sell price, 城镇探索 dependency, objective display 29b, save UI 28–29a, testable 中文 checklist and 破阵 values, new ACs). Content is consistent with V1_SPEC §2.2/§4/§6/§8/§9/§10, ZHUOJUN §4 and combat-system.md.
Prior verdict resolved: Yes
Findings:
- [RECOMMENDED] States and Transitions: 城镇 HUD → 「菜单」 still has no target row; add 主菜单 / 武将页 rows (enter: 菜单键 or 战前准备 entry; leave: 关闭 → previous screen). OQ10's 「物品」「设置」 sub-screens are new and undefined — mark them as part of the provisional answer or drop.
- [RECOMMENDED] Rule 32 / AC 「桃园演出播放中按菜单键」: align with story-events.md OQ9 (system menu may open to quit to title); currently rule 32 says 菜单不响应 without distinguishing 队伍菜单 vs 系统菜单, and no screen offers 退出.
- [RECOMMENDED] Rule 26: 「提示的触发时机……【待编写】」 is stale — story-events.md Rule 18 / OQ8 now owns timing; reference it and keep only text 【待编写】.
- [RECOMMENDED] Edge Cases: 「若所有饰品槽为空」 → 「若该武将饰品槽为空」 (carried over, nice-to-have level).
- [NICE-TO-HAVE] Header 规则来源 could cite production/milestones/alpha-0.1.md for 「测试版结束」; Player Fantasy / Tuning Knobs absent (advisory at standard); 装备道具与商店 / 城镇探索 GDDs not written (known).
Reviewed-Content-Hash: design/gdd/game-ui.md bbfc3bdc643d6eadb92a2e90d9b3271ce2065d9b
Reviewed-Content-Hash: design/registry/entities.yaml 216ffa67ed4d5d24548df37b2ce899fcba892285
