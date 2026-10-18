# Review Log — story-events.md

## Review — 2026-10-18 — Verdict: NEEDS REVISION
Scope signal: M
Specialists: none (lean mode; game-designer / narrative-director / qa-lead analysis done in main review by independent reviewer; guided-mode prompts took recommended defaults)
Blocking items: 2 | Recommended: 9
Summary: All 5 sections required at `standard` are present (Formulas not required — no numeric rules; Player Fantasy / Tuning Knobs advisory). Core flow matches V1_SPEC §2–2.3, §9, §11 and alpha-0.1.md: join-then-auto-oath with three-state trigger lock, strict 3 battles, 张梁/三兄弟 only retreat (no death), Alpha short cutscene + temporary「测试版结束」, formal release all cutscenes; no dialogue is written and 演义/改编 labels for 桃园、程远志、三兄弟合战、赵云 are correct. Blocking: tutorial-hint trigger timing is a rule but is labelled 【待编写】 and has no owner across combat/ui/story GDDs; and where control resumes after the 桃林 cutscene (a save-relevant safe drop point) is undefined.
Prior verdict resolved: First review
Findings:
- [BLOCKING] Core Rules 18 / Interactions: tutorial step trigger timing is a design rule, not dialogue, yet marked 【待编写】; combat-system.md (Interactions, "教学步骤" inflow) and game-ui.md rule 26 both say story-events owns it, while this GDD defers to combat-system.md — nobody defines when each step fires. Fix: take ownership here, mark timing 【待定】 with a recommended answer (e.g. each step fires on first turn/first time the relevant command becomes available), keep only hint text 【待编写】.
- [BLOCKING] Core Rules 10 / States: after the oath cutscene in the 西门外桃林 area, the location where normal control resumes (and the safe drop point saved per V1_SPEC §9 "安全场景及具体落点") is not stated. Fix: add 【待定】 + recommendation (e.g. 涿郡西门内侧, objective points to 南门).
- [RECOMMENDED] Core Rules 12: "南门 N10" mixes numbering — N10 is the 孩童 NPC; 南门 is 图号 10 and its guard is N01. State who gives the "cannot leave" reason (recommend N01).
- [RECOMMENDED] Edge Cases #1: "两人都已入队，但最后一人的入队对话被打断" contradicts the flag table (join flag written at dialogue end). Clarify: interrupted dialogue → not joined, re-talk; and note that the "两人已入队、结义未完成" save state is not reachable via normal tavern/pre-battle saves (auto-trigger fires immediately), so the matching AC needs a constructed/debug save as test setup.
- [RECOMMENDED] Core Rules 20 (formal release): source labelling incomplete — 陶谦身后推举刘备接掌徐州 / 援救陶谦 are 史实 (《先主传》, V1_SPEC §12) and should be labelled so; STORY_BRANCH_STRUCTURE §5 汇合3 suggests the ending avoid specific 曹军暴行 details (for future 曹操卷) — record as 【待定】 for the formal ending.
- [RECOMMENDED] Core Rules / Edge Cases: ZHUOJUN_TOWN_DESIGN §5 step 5 "结义后完成整备与告别" — the 告别 beat is not mentioned; add as 【待定】 (recommend: optional NPC state dialogue only, no gating).
- [RECOMMENDED] Edge Cases (演出中按菜单键): "菜单不响应" may also block a system/quit menu; and cutscene skip is unspecified. Add 【待定】 (recommend: system menu allowed to quit to title — reload replays from start per rule 11; no skip in Alpha).
- [RECOMMENDED] States / Interactions: ownership of encounter data is ambiguous — Interactions says this system outputs "敌方编组", Cross-References says combat owns 编组. Recommend: story passes battle ID only; composition stays in combat data. combat-system.md Dependencies row still says story-events "未编写" (stale, bidirectional fix there).
- [RECOMMENDED] States: `ending_state` vs save-system OQ3 (recommend no save after battle 3) — note that in Alpha the ending flag is not persisted, and reloading the pre-battle-3 autosave replays battle 3 then the ending.
- [RECOMMENDED] Acceptance Criteria: missing ACs for rule 3 (NPC state line switches on stage change), rule 15 (after battles 1/2 can shop and manual-save in 涿郡), battle 2 → south gate → battle 3, and rule 17 reward-once after defeat-retry; last AC lacks a WHEN.
- [RECOMMENDED] Header/minor: header Status "In Design" vs systems-index "In Review"; `objective` value「出城迎敌」vs rule 9/AC「出城」 — use one term. OQ5 recommendation "刘备一行声名渐起" is new narrative content — keep it marked as a recommendation only. Advisory sections Player Fantasy / Tuning Knobs absent (advisory at standard).
Reviewed-Content-Hash: design/gdd/story-events.md 0a6ff906350ef1c7c8a4c1ea5a97c2ae4b480fa0
Reviewed-Content-Hash: design/registry/entities.yaml 216ffa67ed4d5d24548df37b2ce899fcba892285
