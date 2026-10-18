## Gate Check: Concept → Systems Design（重跑）

**Date**: 2026-10-18
**Checked by**: gate-check skill（workflow: standard · review_mode: lean · qa.level: standard）
**Supersedes**: `2026-10-18-concept-to-systems-design.md`（FAIL：缺 game-concept.md）

### Required Artifacts: 1/1 present
- [x] `design/gdd/game-concept.md` — 存在，有内容（按 `/brainstorm` 格式，内容来自共享目录，保留 已确认／建议／待定 标签）

### Recommended（standard 档为建议项）
- [~] Game pillars — 有 4 条，但都标为【建议】，待用户确认
- [~] Visual Identity Anchor — 有一句视觉规则、3 条支撑原则和色彩方针，标为【建议】，待用户确认
- [ ] Concept prototype（`prototypes/*/REPORT.md`）— 没有；现有 Moonlit Glade 原型只做过技术验证

### Quality Checks: 4/4 通过（其中 1 项附带说明）
- [x] Game concept 已评审：`/design-review` 结论 NEEDS REVISION，不是 MAJOR REVISION NEEDED，满足本项（记录见 `design/gdd/reviews/game-concept-review-log.md`）。唯一的阻断项（陶谦立绘绑定已过时）和大部分建议项已修，修订后还没复审。
- [x] Core loop 已描述（game-concept.md 的 Core Loop 一节，30 秒／单场／整局三层）
- [x] Target audience 已明确：「喜欢八方旅人式 HD-2D、对三国故事感兴趣的 PC 单机 JRPG 玩家」（用户 2026-10-18 确认）
- [x] Visual Identity Anchor：有一句规则，另有 3 条原则，每条都有设计判断标准

### Director Panel（standard 档：technical-director + producer）
- **TD-PHASE-GATE：CONCERNS**（无阻断项）。在 Unity 6.3 LTS + URP 上可行，战斗规则可以写成纯 C# 并用 EditMode 测试。意见：
  - `docs/engine-reference/unity/VERSION.md` 还没记录实际安装版本 6000.3.26f1，需要跑 `/setup-engine`
  - HD-2D 在 URP 下的效果没有渲染原型验证
  - 里程碑的性能预算全是待定，Technical Setup 之前要定下来
  - 支柱和视觉锚点还是【建议】，待用户确认
- **PR-PHASE-GATE：CONCERNS**（无阻断项）。0.1 范围收得紧，各文档一致。意见：
  - 战斗 GDD 把装备列为硬依赖，但里程碑里装备／商店还是「待确认」，需要在 Systems Design 开头定下来
  - 成长、存档、剧情、界面四个系统还没有 GDD，也没有 systems index
  - 目标日期和人力都是待定
- 两位都提到的「战斗 GDD 文件头过时」已修正。

### Blockers
无。

### Concerns（进入 Systems Design 时一并处理）
1. 4 条支柱和视觉锚点需要用户确认（目前是【建议】）。
2. 没有概念原型。建议在 Systems Design 期间做一个 HD-2D 渲染小原型（正交相机、像素精灵、3D 光影）。
3. 装备／商店是否纳入 Alpha 0.1 还没定，它是战斗系统的硬依赖。
4. 概念文档修订后还没复审。
5. 引擎版本记录和性能预算都还空着（Technical Setup 之前要补）。

### Verdict: CONCERNS
没有阻断项，必需产物和质量检查都已满足；剩下的问题都可以在 Systems Design 阶段解决。按规则，只有用户明确接受上面列出的风险，阶段才会推进到 **Systems Design**。在那之前 `project.stage` 保持 **Concept**。

Chain-of-Verification: 5 questions checked — verdict unchanged
- [TOOL ACTION] 确认 `design/gdd/game-concept.md` 存在，且 Pillars、Visual Identity Anchor、Target Player Profile 三节都有实际内容（不是占位）。
- [TOOL ACTION] 读了概念评审日志，结论是 NEEDS REVISION，不是 MAJOR。
- 有没有哪条 CONCERN 应该升为阻断？没有：standard 档下支柱和视觉锚点只是建议项。
- 有没有把 FAIL 条件软化成 CONCERN？没有：唯一的必需产物确实存在。
- 这些 CONCERN 叠在一起会不会构成阻断？不会：它们都是 Systems Design 阶段本来就要做的工作。

### Accepted Risks
（待用户决定；接受后在这里记录，并把阶段写为 Systems Design）
