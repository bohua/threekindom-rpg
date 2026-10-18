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
