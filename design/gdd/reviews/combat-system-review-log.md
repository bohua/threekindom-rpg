# Review Log — combat-system.md

## Review — 2026-10-18 — Verdict: NEEDS REVISION
Scope signal: L
Specialists: none (lean mode; game-designer / systems-designer / qa-lead analysis done in main review by independent reviewer)
Blocking items: 2 | Recommended: 6
Summary: Core rules, formulas and all player/enemy derived stats match V1_SPEC §4–7 and recompute correctly; worked examples (damage 101/50, heal 39/33/37, XP 10/30/60) are arithmetically correct. Blocking: the drafted enemy troop types contradict V1_SPEC §2.2 (张宝/张梁 infantry, 张角 文官/军师 with unmapped advantage, ordinary generals share one Yellow Turban type), which also invalidates the battle-3 "each brother counters one hero" intent and AI rule 2; and immediate-defeat timing is an unlabelled rule extension.
Prior verdict resolved: First review
Findings:
- [BLOCKING] Detailed Design / 敌方编组草案: 张梁 marked 骑 and 张角 弓 contradict V1_SPEC §2.2 (张宝、张梁 步兵; 张角 文官／军师, "不自动将军师映射为弓兵"); 将领 A/B/C split 步/弓/骑 contradicts "普通黄巾将领统一黄巾军兵种"; battle-3 counter design and AI rule 2 depend on these.
- [BLOCKING] Edge Cases / States: "最后一个我方单位撤退立即判定失败" and per-action loss check are not in V1_SPEC nor labelled 用户确认／草案.
- [RECOMMENDED] Header: claims no game-concept.md exists; it does — link Pillar 2「武将即部队」.
- [RECOMMENDED] Formulas/Dependencies: stale "敌方属性待定"/"AI 规则细节待定" notes now that a draft exists.
- [RECOMMENDED] Edge Cases vs AC: order-time SP validation makes the "技力 2 选 3 技力技能" AC unreachable; state whether the menu permits unaffordable skills.
- [RECOMMENDED] Enemy table: 张梁 stats change 75/65/50→78/66/50 between battles with no growth rule; add SP column (skill cadence depends on it).
- [RECOMMENDED] Acceptance Criteria: "单回合结算耗时上限待定" not testable; missing ACs for 威慑, 火矢 计策 calc, defeat condition.
- [RECOMMENDED] Advisory sections Player Fantasy / Tuning Knobs absent (advisory at standard).
Reviewed-Content-Hash: design/gdd/combat-system.md dbd91e6fd473c20985cde287b7959020eeab636b
Reviewed-Content-Hash: design/registry/entities.yaml 216ffa67ed4d5d24548df37b2ce899fcba892285

## Review — 2026-10-18 — Verdict: APPROVED
Scope signal: L
Specialists: none (lean mode; game-designer / systems-designer / qa-lead analysis done in main review by independent re-reviewer; guided-mode prompts took recommended defaults)
Blocking items: 0 | Recommended: 6
Summary: Both prior blockers are resolved: enemy troop types now match V1_SPEC §2.2 (张宝/张梁 步, 张角 文官／军师 not mapped to archer, all ordinary Yellow Turban units on one 黄巾军 type), the 黄巾军/军师 rules are labelled 草案待用户确认 and avoid sorcery, and immediate-defeat timing is labelled draft. The full enemy table (incl. 技力), player L1 table, output ranges and all worked examples/ACs (102/51, 54, 威慑 21/32, heal 39/33/37, XP 10/30/60) recompute correctly. Main remaining issue is a dead branch in 张角's draft AI that contradicts the battle-3 design intent.
Prior verdict resolved: Yes
Findings:
- [RECOMMENDED] Detailed Design / 固定 AI 规则 3: 威慑 lasts 2 turns but skill turns are every 3, so "我方没有物攻减益" is always true on 3/6/9 — 张角 never casts 火矢, contradicting the stated intent that his 计策伤害 is a priority threat.
- [RECOMMENDED] Formulas / 伤害 Output Range: "普攻约 10–55、技能约 20–95" understates draft values (e.g. L2 关羽 普攻 vs 黄巾将领 = 67; L2 突击 vs 黄巾将领 = 107).
- [RECOMMENDED] Header / Formulas footnote: "本文只整理、不新增规则" and "按 V1_SPEC 原样转录、未提出新数值" are stale now that drafted troop-type values and enemy stats exist (they are labelled, but the blanket statements contradict them).
- [RECOMMENDED] Acceptance Criteria: defend AC uses an enemy 骑兵 and the 弓→骑 / 骑→弓 AC uses unit pairings that do not exist in any V1 encounter — mark as harness-level tests or add an encounter-based 步克弓 AC (e.g. 张梁 vs 刘备).
- [RECOMMENDED] Edge Cases / AI rule 2: clarify that enemy execution-time retargeting (target retreated mid-round) follows Core Rule 7 (first surviving unit), distinct from AI rule 2's turn-start fallback to lowest troops.
- [RECOMMENDED] Advisory sections Player Fantasy / Tuning Knobs still absent (advisory at standard); 增减益倍率 example still says enemy buff skills "待定" though no drafted enemy skill grants a buff.
Reviewed-Content-Hash: design/gdd/combat-system.md e202c37e8c136487ff71a5f462478d709e46ba2a
Reviewed-Content-Hash: design/registry/entities.yaml 216ffa67ed4d5d24548df37b2ce899fcba892285
