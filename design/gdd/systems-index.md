# Systems Index: 三国 HD-2D JRPG（正式名称待定）— Alpha 0.1「涿郡起兵」

> **Status**: Draft
> **Created**: 2026-10-18
> **Last Updated**: 2026-10-18
> **Source Concept**: design/gdd/game-concept.md
> **Scope**: 只列 Alpha 0.1 测试版需要的系统（`production/milestones/alpha-0.1.md`）。规则来源以用户电脑上共享目录 `ThreeKingdomsJRPG-Docs` 的最新 V1_SPEC.md、ZHUOJUN_TOWN_DESIGN.md 为准。

---

## Overview

Alpha 0.1 是一条约 30–60 分钟（待实测）的单人短流程：在涿郡探索并招募关羽、张飞，自动播放桃园结义，然后打三场黄巾教学战（前两战之间可回城整备、买卖装备、存档），第三战后播简短过场并显示「测试版结束」。系统围绕 Pillar 2「武将即部队」组织：战斗是核心，成长、装备和存档都为三场教学战服务；剧情事件负责把城镇、演出和战斗串成一条不会卡死、不会重复发奖的流程；界面负责把行动顺序、技力和克制讲清楚。

---

## Systems Enumeration

| # | System Name | Category | Priority | Status | Design Doc | Depends On |
|---|-------------|----------|----------|--------|------------|------------|
| 1 | 战斗系统 | Gameplay | MVP | Approved | design/gdd/combat-system.md | 武将成长与技能、装备道具与商店、存档与快照、剧情事件 |
| 2 | 武将成长与技能 | Progression | MVP | Approved | design/gdd/character-progression.md | 无（读取武将数据） |
| 3 | 装备、道具与商店（含铜钱） | Economy | MVP | In Review | design/gdd/equipment-items.md | 武将成长与技能（兵种用于武器限制） |
| 4 | 存档与战前快照 | Persistence | MVP | Approved | design/gdd/save-system.md | 武将成长、装备道具、剧情事件（保存它们的状态） |
| 5 | 剧情事件与过场 | Narrative | MVP | Approved | design/gdd/story-events.md | 城镇探索与交互、存档与快照 |
| 6 | 游戏界面（战斗、整备、商店、教学、存读档） | UI | MVP | Approved | design/gdd/game-ui.md | 战斗、成长、装备商店、存档、剧情事件 |
| 7 | 城镇探索与交互 (inferred) | Core | MVP | In Review | design/gdd/town-exploration.md | 无（现有原型已有移动、碰撞、正交相机） |

「(inferred)」：V1_SPEC 与 ZHUOJUN_TOWN_DESIGN 都要求可走动的涿郡、NPC 交互和南门出城，但没有把它列为一个独立系统。

本测试版不做的系统：非战斗支线（首版最多 1 条）、徐州完整连续过场（正式首版才有）、可操作赵云、后备换人／四选三、音频系统（首版无配音，音乐和音效需求【待定】）。

---

## Categories

本作用到的类别：Core、Gameplay、Progression、Economy、Persistence、UI、Narrative。Audio 与 Meta 在 Alpha 0.1 不单独成系统（教学提示归入剧情事件与界面）。

---

## Priority Tiers

| Tier | Definition | Target Milestone | Design Urgency |
|------|------------|------------------|----------------|
| **MVP** | Alpha 0.1 测试版缺了就玩不通 | Alpha 0.1「涿郡起兵」 | Design FIRST |
| **Vertical Slice** | 本作暂不单列 | — | — |
| **Alpha** | 本作暂不单列 | — | — |
| **Full Vision** | 正式首版及以后：徐州连续过场、非战斗支线；后续储备（赵云可操作等） | 首版／后续 | Design as needed |

本索引中所有系统都是 MVP，即 Alpha 0.1 测试版必需。

---

## Dependency Map

### Foundation Layer (no dependencies)

1. 城镇探索与交互 — 移动、碰撞、相机、NPC 交互和场景切换，其他系统都在它之上运行。
2. 武将成长与技能 — 只读武将基础数据，决定等级、属性和可用技能。

### Core Layer (depends on foundation)

1. 装备、道具与商店 — depends on: 武将成长与技能（兵种决定可装备的武器类别）
2. 战斗系统 — depends on: 武将成长与技能、装备道具与商店

### Feature Layer (depends on core)

1. 剧情事件与过场 — depends on: 城镇探索与交互；触发战斗、接收胜负
2. 存档与战前快照 — depends on: 成长、装备道具、剧情事件（保存并还原它们的状态）

### Presentation Layer (depends on features)

1. 游戏界面 — depends on: 战斗、成长、装备商店、存档、剧情事件

### Polish Layer (depends on everything)

1. （Alpha 0.1 无）

---

## Recommended Design Order

| Order | System | Priority | Layer | Agent(s) | Est. Effort |
|-------|--------|----------|-------|----------|-------------|
| 1 | 战斗系统 | MVP | Core | game-designer, systems-designer | 已完成 |
| 2 | 武将成长与技能 | MVP | Foundation | game-designer, systems-designer | S |
| 3 | 存档与战前快照 | MVP | Feature | systems-designer | S |
| 4 | 剧情事件与过场 | MVP | Feature | game-designer, narrative-director | M |
| 5 | 游戏界面 | MVP | Presentation | game-designer, ux-designer | M |
| 6 | 装备、道具与商店 | MVP | Core | economy-designer | S |
| 7 | 城镇探索与交互 | MVP | Foundation | game-designer, level-designer | S |

第 2–5 项按项目线程的指示先写；第 6、7 项尚未写 GDD，建议接着写（装备是战斗的硬依赖）。

---

## Circular Dependencies

- 战斗 ↔ 剧情事件：剧情事件触发战斗，战斗胜利又推进剧情事件。解法：剧情事件只按战斗 ID 发起战斗、接收「胜利／失败」结果；战斗不读剧情标记。
- 存档 ↔ 战斗：战斗在确认整备时请求建立快照，存档系统持有快照格式。解法：存档系统提供「建立快照／还原快照」两个接口，战斗只调用、不持有格式。

---

## High-Risk Systems

| System | Risk Type | Risk Description | Mitigation |
|--------|-----------|-----------------|------------|
| 存档与战前快照 | Technical | 快照、读档与防重标志要在同一存档状态内保持一致，出错会重复发奖或卡死 | 先写快照与还原的逻辑测试（EditMode） |
| 剧情事件与过场 | Design / Technical | 自动结义演出的操作锁，以及读档后演出状态的恢复 | 演出状态分三态，并验证读档后不残留操作锁 |
| 游戏界面 | Scope | 战斗、整备、商店、教学、存档多个界面，美术与交互量大 | 先用占位界面验证规则 |
| 战斗系统 | Design | 三战平衡未测试 | 先打通第一场战斗（V1_SPEC §13 第 1 步） |

---

## Progress Tracker

| Metric | Count |
|--------|-------|
| Total systems identified | 7 |
| Design docs started | 5 |
| Design docs reviewed | 5（成长、战斗 Approved；存档、剧情、界面 NEEDS REVISION，已修订待复审） |
| Design docs approved | 2 |
| MVP systems designed | 5/7 |
| Vertical Slice systems designed | — |

---

## Next Steps

- [ ] 用户确认本系统清单
- [ ] 为「装备、道具与商店」「城镇探索与交互」写 GDD
- [ ] 各 GDD 跑 `/design-review`
- [ ] 全部 MVP GDD 评审后跑 `/review-all-gdds`，再 `/gate-check technical-setup`
