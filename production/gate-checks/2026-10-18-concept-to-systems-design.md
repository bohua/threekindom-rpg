## Gate Check: Concept → Systems Design

**Date**: 2026-10-18
**Checked by**: gate-check skill（workflow: standard · review_mode: lean · qa.level: standard）
**Scope context**: Alpha 0.1「涿郡起兵」（入城 → 结识关张入队 → 自动桃园结义 → 三场黄巾教学战 → 过场到徐州结束）

### Required Artifacts: 0/1 present
- [ ] `design/gdd/game-concept.md` — MISSING（`artifact-check.sh`: `game-concept ... status=ABSENT`）

### Recommended (standard 档降级为建议，缺失记为 CONCERNS)
- [ ] Game pillars（concept 文档内或 `design/gdd/game-pillars.md`）— MISSING
- [ ] Visual Identity Anchor（`game-concept.md` 内）— MISSING；外部 PROJECT.md §5 有美术方向，可作为来源
- [ ] Concept prototype（`prototypes/*/REPORT.md`，PROCEED）— MISSING；现有 Moonlit Glade 原型只验证技术，不验证三国战斗规则

### Quality Checks: 0/4 可评估
- [ ] Game concept 已评审 — 无法评估（文档不存在）
- [?] Core loop 已描述 — 外部 PROJECT.md §4 有循环描述，但不在仓库内；本关口只认 `game-concept.md`
- [ ] Target audience 已明确 — 未在任何文档中找到
- [ ] Visual Identity Anchor 有一句视觉规则 + ≥2 原则 — 无法评估

### Blockers
1. **没有 `design/gdd/game-concept.md`** — 运行 `/brainstorm`，以共享目录 PROJECT.md（愿景、叙事、核心循环、美术方向）和 V1_SPEC 首版范围为输入生成。本关口不会代为创建该文件。

### Recommendations
- 生成 concept 时一并写出 pillars、Visual Identity Anchor 和目标玩家（目标玩家目前在任何文档中都没有）。
- `/map-systems` 建立 systems index，把已写好的 `design/gdd/combat-system.md` 登记进去。
- 在新会话中运行 `/design-review design/gdd/combat-system.md`。
- 非阻塞：Alpha 0.1 还没有 milestone 文档，建议补 `production/milestones/alpha-0.1.md`，供 `/scope-check` 使用。

### Director Panel
未运行（standard 档应为 `technical-director` + `producer`）。原因：唯一必需产物缺失，按判定优先级 FAIL 已成立，评审团结果不会改变判定；在 concept 文档补齐后重跑本关口时再运行评审团。

### Verdict: FAIL
- 关键阻塞：Concept 阶段唯一必需产物 `game-concept.md` 缺失。阶段保持 **Concept**，不改写 `project.stage`。

Chain-of-Verification: 5 questions checked — verdict unchanged
- [TOOL ACTION] 重新运行 `artifact-check.sh`：`game-concept` ABSENT、`map-systems` ABSENT、`art-bible` ABSENT。
- [TOOL ACTION] 检查 `design/gdd/`：只有 `combat-system.md`，没有 concept 或 pillars 文件。
- 是否把 FAIL 软化成了 CONCERNS？没有 — standard 档仍要求 `game-concept.md`。
- 最小通过路径：(1) `/brainstorm` 生成 `game-concept.md`；(2) 写入 pillars + Visual Identity Anchor（建议项，可消除 CONCERNS）；(3) 重跑 `/gate-check systems-design`。
- 这个阻塞是缺一份可以写出来的文档，不是更深的设计问题：设计内容已存在于外部共享目录。
