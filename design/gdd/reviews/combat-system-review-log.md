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
