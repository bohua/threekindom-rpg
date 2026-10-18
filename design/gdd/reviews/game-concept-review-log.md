# Review Log — game-concept.md

## Review — 2026-10-18 — Verdict: NEEDS REVISION
Scope signal: L (concept-level; whole first release — producer should verify before sprint planning)
Specialists: none (lean mode; analysis done in main review by independent reviewer)
Blocking items: 1 | Recommended: 6
Summary: Concept faithfully transcribes the shared folder (PROJECT / V1_SPEC / MEMORY / INITIAL_SCENARIO_ART, latest on disk) for scope, combat, growth, equipment, enemy rosters and the target player; Alpha 0.1 framing (short cutscene + temporary 「测试版结束」 after battle 3; formal release strictly 3 battles with an all-cutscene Xuzhou ending) matches production/milestones/alpha-0.1.md. One 【已确认】 claim is contradicted by the latest source (陶谦 portrait), and several 【已确认】 labels rest on repo files rather than the shared folder or slightly overstate the source.
Prior verdict resolved: First review
Reviewer decisions (guided defaults): concept doc is not a system GDD — the 8-section GDD checklist (gdd-structure-check reports all ABSENT) was applied as advisory only and the brainstorm-template sections were checked instead; registry has no game-concept entries, so cross-checks used the shared folder, combat-system.md and alpha-0.1.md directly.
Findings:
- [BLOCKING] Visual Identity Anchor (line 176): 「另有陶谦立绘，仅用于结尾过场」【已确认】 is stale — INITIAL_SCENARIO_ART.md / PROJECT.md / MEMORY latest entry rebind that grey-bearded image to 城主府掌士 (「本图不再绑定给他」); current set is 程远志、张角、张宝、张梁 + 城主府掌士 + 通用黄巾将领. Replace with 城主府掌士 (通用对话立绘, 仅府内无特殊人物且无太守时启用) and mark 陶谦过场立绘 as 【待定】.
- [RECOMMENDED] Core Identity / Technical Considerations: Platform 「PC（Windows）【已确认：project.yaml】」 and Engine 「Unity 6.3 LTS + URP【已确认】」 come from repo files (project.yaml, technical-preferences.md), not the shared folder the header names as sole source; relabel as 【工程现状】 with file citation, or extend the header's source statement.
- [RECOMMENDED] MDA Discovery (line 58): 「1 条非战斗支线【已确认】」 — source says 最多 1 条; Alpha 0.1 excludes it. Write 「最多 1 条（测试版不含）」.
- [RECOMMENDED] Anti-Pillars (156–160): 「NOT 分支结局」 and 国家经营 are framed as permanent identity, but PROJECT §8 says 长期分支待定 and the new shared-folder proposal STORY_BRANCH_STRUCTURE.md (pending user decision) is about long-term endings; scope these anti-pillars to 「首版不做」 and add STORY_BRANCH_STRUCTURE.md to the Open Questions as an undecided proposal.
- [RECOMMENDED] Open Questions: missing 张角 文官／军师 兵种的克制映射、数值与技能 (V1_SPEC §2.2 / INITIAL_SCENARIO_ART: 待配置, 不自动当弓兵); Unique Hook's 「步骑弓互相克制」 should note a 4th enemy type exists.
- [RECOMMENDED] Flow State Design (101–102) vs milestone: battle-1 roster says 「程远志＋头领」 (matches V1_SPEC 「数量尚未细化」), but alpha-0.1.md and combat-system.md record 「2 名杂兵头领」 as user-confirmed 2026-10-18 outside the shared folder. Ask the user to write the count back to the shared V1_SPEC, then align concept and milestone; until then the milestone states a count the SSOT does not.
- [RECOMMENDED] Target Player Profile (202–203): 「Current games they play: 《八方旅人》类」 and 「回合制」 in 「What they're looking for」 are labelled 【用户确认】 but go beyond the user's sentence (「喜欢八方旅人式 HD-2D、对三国故事感兴趣的 PC 单机 JRPG 玩家」); relabel those two cells 【建议】.
- [NICE-TO-HAVE] Onboarding curve (101): V1_SPEC heads that column 「教学重点（执行设计）」 — label 【已确认基线·执行设计】 rather than plain 【已确认】; Non-game inspirations: 演义第2回 is cited in V1_SPEC only for checking the brothers' original fate, not as a used supplement; MVP item 4 could add 「并返回标题」 to mirror alpha-0.1.md.
Reviewed-Content-Hash: design/gdd/game-concept.md 3c30d260b7fcada25251c115077f061983ee5ab6
Reviewed-Content-Hash: design/registry/entities.yaml 216ffa67ed4d5d24548df37b2ce899fcba892285

## Review — 2026-10-18 — Verdict: NEEDS REVISION
Scope signal: L (concept-level; whole first release — producer should verify before sprint planning)
Specialists: none (lean mode; independent re-review, analysis done in main review)
Blocking items: 1 | Recommended: 5
Summary: Re-review against the latest shared folder. The prior blocker (陶谦 portrait) is fixed — line 176 now matches INITIAL_SCENARIO_ART / PROJECT / MEMORY (掌士通用立绘; 陶谦过场暂时复用图片、不套身份). Pillars and visual anchor are correctly marked as provisionally adopted, and equipment/shop sits in the Alpha 0.1 tier, consistent with alpha-0.1.md and systems-index.md. However the shared folder moved on after the concept was written: STORY_BRANCH_STRUCTURE.md is now user-confirmed (PROJECT §3/§4.7/§8, MEMORY「长期多线剧情结构确认」), the 东吴 line is 孙策→孙权, and V1_SPEC §2.2 now records 「程远志＋2名杂兵头领」 — the concept still describes all three as pending, so it misstates the SSOT in several places.
Prior verdict resolved: Partially — the blocker and 5 of 6 recommended items are resolved (platform/engine relabelled 【工程现状】; 支线「最多1条（测试版不含）」; anti-pillars scoped to 首版; 头领数量 raised as an open question; Target Player cells relabelled 【建议】). Not resolved: 张角 文官／军师 (and 黄巾军) 兵种克制映射 is still not an Open Question, and the Unique Hook still says only 「步骑弓互相克制」.
Reviewer decisions (guided defaults): concept doc is not a system GDD — gdd-structure-check reports all 8 sections ABSENT, applied as advisory only; brainstorm-template sections checked instead. Registry unchanged and has no game-concept entries; cross-checks used the shared folder, alpha-0.1.md and systems-index.md directly.
Findings:
- [BLOCKING] Anti-Pillars (line 160) / Open Questions (241–243) / Long-Term Progression (120) / Scope Tiers Full Vision (267): STORY_BRANCH_STRUCTURE.md is described as 「建议稿，等待用户决定」 and 长期分支结局／多主角切换形式 as 【待定】, but the user confirmed it on 2026-10-18 (分卷制作、每卷史实结局＋尾声变体、赤壁共同终章可切换主角; PROJECT §4.7 makes this the limited exception to 「分支结局」). The 东吴 line is 孙策→孙权, not 「孙权视角」. Rewrite these to 【已确认·长期，不排期】, scope 「NOT 分支结局」 to 首版, and remove the two stale Open Questions.
- [RECOMMENDED] Open Questions (244) / Flow State (102): 「尚未回写共享目录 V1_SPEC」 is stale — V1_SPEC §2.2 now reads 「程远志＋2名杂兵头领（数量用户2026-10-18确认）」. Delete the open question and write 「程远志＋2名杂兵头领」 in the difficulty-scaling line.
- [RECOMMENDED] Open Questions / Unique Hook (44): carried over — add 「黄巾军与张角文官／军师兵种的克制映射、数值与技能【待定】（不自动当弓兵）」, and note in the hook that the enemy side has types beyond 步骑弓.
- [RECOMMENDED] Anti-Pillars (160): 「穿越与神怪体系 PROJECT.md 已明确不引入」 overstates the source for 穿越 — PROJECT §4.7 says only 「未被确认，不作为默认追加需求」 (神怪 is explicitly excluded in §4.3). Reword 穿越 accordingly.
- [RECOMMENDED] MVP Definition (252–256): 「Required for MVP（= Alpha 0.1）」 lists 4 items without 三槽装备／涿郡商店／回城整备／存档与战败重试, which the Alpha 0.1 Scope Tier row and alpha-0.1.md Must Ship include (user-confirmed 2026-10-18). Add them, and add 「并返回标题」 to item 4 (prior nice-to-have).
- [RECOMMENDED] Next Steps (274) / header (6): 「用户确认 4 项 pillars 与视觉锚点表述」 is still open although line 132/167 record provisional adoption — tick it (暂定采用); add STORY_BRANCH_STRUCTURE.md to the header's source list since the concept cites it.
- [NICE-TO-HAVE] Onboarding curve (101) still plain 【已确认】 for V1_SPEC's 「教学重点（执行设计）」 column; 演义第2回 still listed as a used supplement rather than a check source.
Reviewed-Content-Hash: design/gdd/game-concept.md 2fab3a006d3b3467790a3c3a7fb1df7d07a24795
Reviewed-Content-Hash: design/registry/entities.yaml 216ffa67ed4d5d24548df37b2ce899fcba892285

## Review — 2026-10-18 — Verdict: APPROVED
Scope signal: L (concept-level; whole first release — producer should verify before sprint planning)
Specialists: none (lean mode; independent re-review, analysis done in main review)
Blocking items: 0 | Recommended: 3
Summary: Re-review against the latest shared folder (PROJECT / V1_SPEC / MEMORY / INITIAL_SCENARIO_ART / STORY_BRANCH_STRUCTURE, 23:27 versions). The prior blocker is resolved: Long-Term Progression (120), Anti-Pillars (160) and Full Vision (269) now state the user-confirmed multi-volume structure (分卷、每卷史实结局＋尾声变体、赤壁共同终章切换主角、孙策→孙权写到夷陵) as 【已确认·长期，不排期】, with 「分支结局」 scoped to 首版 and the §4.7 exception cited. Alpha 0.1 / 首版 tiers, MVP list and battle-1 roster now match alpha-0.1.md, systems-index.md and V1_SPEC §2.2. Remaining issues are label-level: one leftover 【待定】 that reads as the confirmed presentation form, and one enemy-type claim that rests on a repo GDD the SSOT still lists as undecided.
Prior verdict resolved: Yes (blocker) — 5 of 6 recommended also resolved (头领数量 open question removed and difficulty line fixed; Unique Hook notes enemy-only types; 穿越 reworded per §4.7; MVP list gains 装备／商店／整备／存档／战败重试 and 「并返回标题」; pillars ticked as 暂定采用 and STORY_BRANCH_STRUCTURE.md added to the header). Residual: Open Questions line 242 「长期多主角切换形式【待定】」 survived the cleanup (see below). Prior nice-to-haves unchanged.
Reviewer decisions (guided defaults): concept doc is not a system GDD — gdd-structure-check reports all 8 sections ABSENT, applied as advisory only; brainstorm-template sections checked instead. Registry unchanged and has no game-concept entries; cross-checks used the shared folder, alpha-0.1.md, systems-index.md and combat-system.md directly. Line 242 graded RECOMMENDED rather than BLOCKING because the body states the confirmed form in three places and STORY_BRANCH_STRUCTURE §2 does leave the 共同终章 switching rules to be designed.
Findings:
- [RECOMMENDED] Open Questions (242): 「长期多主角切换形式【待定】」 contradicts Q2=D (user-confirmed; PROJECT §3/§8). Reword to 「共同终章的主角切换规则【待定】（STORY_BRANCH_STRUCTURE §2 决定二 D）」.
- [RECOMMENDED] Open Questions (243): 敌方专用兵种 (黄巾军／文官／军师) rules are labelled user-confirmed via combat-system.md, but the SSOT still lists them as undecided (V1_SPEC §2.2 「军师的克制映射、数值…仍需配置」; MEMORY latest 「仍未定：黄巾军与文官／军师兵种规则、敌方数值与AI」), and alpha-0.1.md's risk register keeps 「敌方数值／AI 仅为草案」 Open. Per the header's conflict rule, label it 【仓库 combat-system.md 用户确认，尚未回写共享目录】 and ask the user to write the rule back to V1_SPEC §2.2 / §6.
- [RECOMMENDED] Pillar 1 / Flow State (102): V1_SPEC §2.2 now fixes 张梁（第二战）与张角三兄弟（第三战）只写兵败退走、不写死亡，最终结局按史实另交代 — the only first-release effect of the long-term structure. The concept does not mention it; add it to the third battle line or Pillar 1's design test so downstream story-events work inherits it.
- [NICE-TO-HAVE] Carried over: Onboarding curve (101) still plain 【已确认】 for V1_SPEC's 「教学重点（执行设计）」 column; Non-game inspirations (190) still lists 演义第2回 as a used supplement rather than a check source. Cross-doc: combat-system.md line 7 still calls the pillars 「【建议】，待用户确认」 while the concept records them as 暂定采用 — align the sibling GDD.
Reviewed-Content-Hash: design/gdd/game-concept.md b3c9cdf78005a5ef01953b3f839d4c8286242eed
Reviewed-Content-Hash: design/registry/entities.yaml 216ffa67ed4d5d24548df37b2ce899fcba892285
