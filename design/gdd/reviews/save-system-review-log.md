# Review Log — save-system.md

## Review — 2026-10-18 — Verdict: NEEDS REVISION
Scope signal: M (5 dependencies, no formulas, save format/serialization needs a new ADR; producer should verify before sprint planning)
Specialists: none (lean mode; game-designer / systems-designer / qa-lead analysis done in main review by independent reviewer). Automation guided: Phase 1 freshness check returned RECEIPT: NONE → full review; Phase 5 defaulted to "[B] Stop here — revise in a separate session" and "append to review log"; systems-index not touched (caller restricted writes to this log).
Blocking items: 2 | Recommended: 7
Summary: Core rules faithfully restate V1_SPEC §1/§2.1/§4.1,9/§9/§11 and ZHUOJUN §3 (tavern entry correctly labelled 设计建议 + OQ1); no spec-violating rule found, and cross-doc behaviour matches combat-system, story-events and character-progression (retry restore, per-save anti-duplicate flags, oath replay). Blocking: the saved "三场战斗 ID 与安全阶段" / "具体落点" are undefined and not mapped to story-events' flag table, so the save schema cannot be implemented without guessing; and the Acceptance Criteria omit the system's primary contract (save→load round-trip of every §9 field), unsupported-version handling, and V1_SPEC §11's "三个战前阶段均可自动存档".
Prior verdict resolved: First review
Findings:
- [BLOCKING] Core Rules 4 / States: "安全阶段" and "具体落点" undefined; not tied to story-events flags (guanyu_joined, zhangfei_joined, oath_state, objective, battle_N_done, ending_state). Fix: reference that table as the saved event state, and mark the safe-stage enum 【待定】 with recommendation {城镇安全状态(涿郡落点), 第N战战前准备(已确认整备)} in a new OQ.
- [BLOCKING] Acceptance Criteria: missing (a) save→load round-trip equality for all V1_SPEC §9 fields, (b) unsupported-version prompt + file untouched, (c) auto-save written before each of the three battles (V1_SPEC §11), (d) manual save unavailable outside 涿郡/in battle.
- [RECOMMENDED] Edge Cases: "暂停队伍操作和菜单" attributed to V1_SPEC §2.1, which says "自由移动、交互和队伍操作" (no 菜单); align wording, derive no-save from save entry being an interaction.
- [RECOMMENDED] Core Rules 7: "版本不支持" policy undefined; add OQ 【待定】, recommend: only the current save-format version loads, any other → prompt, keep file.
- [RECOMMENDED] Edge Cases: no auto-save after victory (spec defines none); state explicitly that quitting after a win without manual save reloads the pre-battle auto-save and replays that battle — do not add a post-victory auto-save (would exceed spec).
- [RECOMMENDED] OQ3: note that recommending "no save after 测试版结束" means 结尾完成标志 is never persisted in Alpha 0.1 (consistent with spec, but say so).
- [RECOMMENDED] OQ4: recommended summary field 游玩时间 is not among V1_SPEC §9 saved fields; mark it as an addition requiring user approval or drop it (save timestamp can come from file metadata).
- [RECOMMENDED] Dependencies: 装备道具与商店 GDD not found (broken reference); combat-system.md Dependencies row still lists save-system as "未编写" (stale, bidirectional mismatch); header Status "In Design" vs systems-index "In Review".
- [RECOMMENDED] Acceptance Criteria: "GIVEN 任何读档 THEN 不会落在演出中间…" is not independently testable; rewrite as: for each save point (3 manual-slot locations, 3 auto-saves, oath-pending save) load → state is 城镇安全状态 or 战前准备, no 播放中 state, no 徐州 scene. Last AC lacks GIVEN/WHEN (inspection test — label it).
- [NICE-TO-HAVE] Clarify snapshot vs auto-save relationship (same payload? retry after relaunch uses auto-save) for the ADR; "不可读档中断恢复" wording unclear; Player Fantasy / Tuning Knobs absent (advisory at standard; constants already noted in Formulas).
Reviewed-Content-Hash: design/gdd/save-system.md f2f90ab0844b0c73c215324941ab044df9a2b463
Reviewed-Content-Hash: design/registry/entities.yaml 216ffa67ed4d5d24548df37b2ce899fcba892285

## Review — 2026-10-18 — Verdict: APPROVED
Scope signal: M (5 dependencies, no formulas, save format/serialization still needs an ADR; producer should verify before sprint planning)
Specialists: none (lean mode; game-designer / systems-designer / qa-lead analysis done in main review by independent reviewer). Automation guided: freshness check CHANGED → full re-review; Phase 5 prompts took defaults (append to review log only; systems-index not touched per caller restriction).
Blocking items: 0 | Recommended: 3
Summary: Re-review at commit 277c925. Both prior blockers are resolved: Core Rule 4 now ties event state to the story-events.md flag table, defines 具体落点 (酒馆 / 该战战前准备) and puts the safe-stage enum in OQ6; ACs now cover §9 round-trip, unsupported-version handling, one auto-save per battle (V1_SPEC §11) and manual save unavailable outside 涿郡/in battle. Most prior recommendations were also applied (§2.1 wording, OQ7 version policy, no post-victory auto-save, OQ3 ending-flag consequence, OQ4 游玩时间 caveat, testable save-point AC). All 推荐暂用 items are labelled and consistent with V1_SPEC §1/§2.1/§4/§9/§11 and ZHUOJUN §3–§4.
Prior verdict resolved: Yes
Findings:
- [RECOMMENDED] States and Transitions / Interactions: 战败选择 row still says 「返回标题【待定】」 and UI row says 「显示内容待定」 — point them at OQ5 / OQ4 so the provisional answers are visible where implemented.
- [RECOMMENDED] Acceptance Criteria: 「酒馆手动存档和三场战斗各自的战前自动存档，逐个读档」 — only one rolling auto-save slot exists; state the test is run by capturing each battle's auto-save in sequence (or copying the file) before it is overwritten.
- [RECOMMENDED] Cross-doc: story-events.md OQ7 calls 涿郡西门内侧 「演出后的存档安全落点」, but this GDD's 具体落点 lists only 酒馆 and 战前准备 and nothing auto-saves after the oath; align wording (it is a control-resume point, not a save point).
- [NICE-TO-HAVE] Snapshot vs auto-save payload relationship for the ADR; Player Fantasy / Tuning Knobs absent (advisory at standard). Dependencies: 装备道具与商店 GDD still not written (known, not a doc defect).
Reviewed-Content-Hash: design/gdd/save-system.md 8c900481653a2972a0ee6f7320986c7042b067b7
Reviewed-Content-Hash: design/registry/entities.yaml 216ffa67ed4d5d24548df37b2ce899fcba892285
