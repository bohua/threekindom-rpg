# Milestone: Alpha 0.1「涿郡起兵」

> 范围由用户 2026-10-18 确认；规则细节以用户电脑上共享目录 `C:\Users\fanta\Documents\SharedProjects\ThreeKingdomsJRPG-Docs` 的最新 V1_SPEC.md 为准。本里程碑是首版的测试版：主线截到第三场黄巾战胜利后的简短过场。

## Overview

- **Target Date**: 待定
- **Type**: Alpha
- **Duration**: 待定
- **Number of Sprints**: 待定

## Milestone Goal

做出一条可从头玩到尾的短流程：入涿郡 → 结识关羽、张飞并入队 → 自动桃园结义演出 → 三场黄巾教学战 → 第三战胜利后的简短过场 →「测试版结束」画面。目标是验证「武将率部队」的对列回合制战斗与成长在三场教学战里能否教会玩家并让人愿意玩完。

> **测试版临时处理**：结尾的「测试版结束」画面只用于本测试版。正式首版在第三战后接完整的连续过场：剧情简介、徐州保卫战、赵云参与与辞别、陶谦身后推举刘备接掌徐州，然后章节结算并返回标题（V1_SPEC §2）。首版本身就严格只有 3 场战斗、刘关张 6 个技能，没有后备、四选三、可选战或徐州战。

## Success Criteria

- [ ] 新游戏能进入涿郡，结识并招募关羽、张飞；两人入队后自动播放桃园结义演出，结束后恢复控制，不重复招募／发装备，存读档不残留操作锁。
- [ ] 严格完成 3 场指定黄巾战：程远志＋2 名杂兵头领；张梁＋3 名黄巾将领；张角、张宝、张梁。无其他战斗。
- [ ] 刘关张覆盖步骑弓，6 项技能可用；能展示全队下令、速度顺序、技力、1.4 克制、防御先行、撤退与战后全恢复（`design/gdd/combat-system.md` 验收条目）。
- [ ] 三战经验 10／20／30 → 战后 2／3／4 级，第三战前解锁三级技能。
- [ ] 战前自动存档；战败还原并重试，不重复发奖。
- [ ] 第三战胜利后播放简短过场，随后显示「测试版结束」（测试版临时处理）并返回标题。
- [ ] Windows 构建可运行；逻辑测试（EditMode）通过。
- [ ] All S1 and S2 bugs resolved
- [ ] Performance within budget on target hardware（预算待定）

## Feature List

### Must Ship (Milestone Fails Without These)

| Feature | Design Doc | Owner | Sprint Target | Status |
|---------|-----------|-------|--------------|--------|
| 对列回合制战斗（规则层、敌方固定 AI、三场编组数据） | `design/gdd/combat-system.md` | 待定 | 待定 | 设计完成；规则层 WIP 在 `feature/battle-core` |
| 武将成长与技能解锁 | combat-system.md Formulas（独立 GDD 待写） | 待定 | 待定 | 未开始 |
| 涿郡城镇探索与关张招募 | ZHUOJUN_TOWN_DESIGN.md（共享目录） | 待定 | 待定 | 未开始 |
| 自动桃园结义演出 | V1_SPEC §2.1（GDD 待写） | 待定 | 待定 | 未开始 |
| 战前快照、自动存档与战败重试 | V1_SPEC §9（GDD 待写） | 待定 | 待定 | 未开始 |
| 第三战后简短过场＋「测试版结束」（临时） | V1_SPEC §2（GDD 待写） | 待定 | 待定 | 未开始 |
| 战斗界面（行动顺序、兵力／技力、状态剩余回合） | combat-system.md UI Requirements | 待定 | 待定 | 未开始 |

### Should Ship (Planned but Cuttable)

| Feature | Design Doc | Owner | Sprint Target | Cut Impact | Status |
|---------|-----------|-------|--------------|-----------|--------|
| 三槽装备、涿郡商店、前两战后回城整备、手动存档 | V1_SPEC §8–9 | 待定 | 待定 | 0.1 范围陈述未提及，是否纳入待用户确认；砍掉后战斗只用初始装备 | 待确认 |

### Stretch Goals (Only if Ahead of Schedule)

| Feature | Design Doc | Owner | Value Add |
|---------|-----------|-------|----------|
| — | — | — | 无；不为凑时长增加内容 |

### Explicitly Out of Scope（本测试版不做）

- 第三战后的完整连续过场（剧情简介、徐州保卫、赵云参与与辞别、陶谦推举刘备接掌徐州）：属于正式首版，本测试版用简短过场＋「测试版结束」临时代替。
- 非战斗支线（首版最多 1 条）：本测试版不做。
- 任何额外战斗、武将、地图或系统。首版本身也没有后备换人、四选三、可选战、徐州战或可操作赵云（V1_SPEC 已列为后续储备）。

## Quality Gates

| Gate | Threshold | Measurement Method |
|------|-----------|-------------------|
| Crash rate | 待定 | 人工完整流程验收 |
| Frame rate | 待定 | Performance profiling |
| Load time | 待定 | 人工计时 |
| Critical bugs | 0 open S1 | Bug tracker |
| High-severity bugs | 待定 | Bug tracker |
| Test coverage | 战斗规则逻辑测试覆盖 combat-system 验收条目 | Unity Test Framework |

## Risk Register

| Risk | Probability | Impact | Mitigation | Owner | Status |
|------|------------|--------|-----------|-------|--------|
| 三战平衡（初值未测） | 高 | 中 | 首场战斗贯通后实测调参（V1_SPEC §13 步骤 1） | 待定 | Open |
| 敌方数值／AI 仅为草案 | 高 | 中 | 用户确认 combat-system.md 中的草案 | Bohua | Open |
| 美术量（像素武将、城镇模块）超出预期 | 中 | 高 | 先用占位验证规则 | 待定 | Open |
| 时长不足 30 分钟 | 中 | 低 | 不为凑时长加战斗；实测记录 | 待定 | Open |

## Dependencies

### Internal Dependencies

| Feature | Depends On | Owner of Dependency | Status |
|---------|-----------|-------------------|--------|
| 三场教学战 | 战斗规则层、成长系统 | 待定 | WIP |
| 桃园结义演出 | 关张招募事件标记 | 待定 | 未开始 |
| 战败重试 | 战前快照／存档 | 待定 | 未开始 |

### External Dependencies

| Dependency | Provider | Status | Risk if Delayed |
|-----------|---------|--------|----------------|
| 刘关张立绘与像素九宫格 | 美术（D:\BuildArtifacts\Codex\hd2d-jrpg） | 已有；未精确切片、未补连续动画、未导入 Unity | 用占位继续 |
| 黄巾一方立绘与 Q 版（程远志、张角、张宝、张梁、通用黄巾将领；通用黄巾、张角、张宝、张梁 4 套九宫格） | 美术（D:\BuildArtifacts\Codex\hd2d-jrpg\initial-scenario-enemy-art-20261018-v1，INITIAL_SCENARIO_ART.md） | 已有；未精确切片、未导入 Unity | 用占位继续 |

## Review Schedule

| Date | Review Type | Attendees |
|------|-----------|-----------|
| 待定 | 首场战斗贯通检查 | Bohua |
| 待定 | 里程碑验收（完整流程、Windows 构建） | Bohua |
