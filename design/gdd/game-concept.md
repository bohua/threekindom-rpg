# Game Concept: 三国 HD-2D JRPG（正式名称待定）

*Created: 2026-10-18*
*Status: Draft（待 `/design-review`）*

> **来源与标签**：设定的唯一来源是用户电脑上的共享目录 `C:\Users\fanta\Documents\SharedProjects\ThreeKingdomsJRPG-Docs` 的最新版（PROJECT.md、V1_SPEC.md、MEMORY.md、INITIAL_SCENARIO_ART.md、ZHUOJUN_TOWN_DESIGN.md）。本文按 CCGS `/brainstorm` 的产出格式整理，不新增正典；两者冲突时以共享目录为准。标签含义：
> - **【已确认】** 用户明确描述或确认；
> - **【建议】** 由已确认内容提炼的设计表述，尚待用户确认；
> - **【待定】** 未决定，不得自行补成正式设定。
>
> `Moonlit Glade` 仅为技术原型名，不是正式游戏名称。

---

## Elevator Pitch

【已确认，表述为建议】这是一款三国题材的 HD-2D 日式 RPG：你扮演早年起兵的刘备，在涿郡结识关羽、张飞，率领各自的部队打传统对列回合制战斗，沿史实进程一路走到接掌徐州。

---

## Core Identity

| Aspect | Detail |
| ---- | ---- |
| **Genre** | 日式 RPG，传统对列回合制战斗【已确认】 |
| **Platform** | PC（Windows）【工程现状：`project.yaml` 与已有 Windows 构建；共享目录写的是 PC 单机】 |
| **Target Audience** | 喜欢《八方旅人》式 HD-2D、对三国故事感兴趣的 PC 单机 JRPG 玩家【用户 2026-10-18 确认】 |
| **Player Count** | 单人【已确认：无联网】 |
| **Session Length** | 首版通关 30–60 分钟（玩法验证版，待实测）【已确认目标】 |
| **Monetization** | 【待定】 |
| **Estimated Scope** | 首版：小（30–60 分钟验证版，开发工期【待定】）；长期：大（三势力多主角，工期【待定】） |
| **Comparable Titles** | 《八方旅人》（HD-2D 视觉）、史克威尔系 Q 版 JRPG【已确认为视觉参考】 |

---

## Core Fantasy

【建议】亲历三国开篇：从一个涿郡游侠起步，结识关张、结义、率军迎战黄巾，看着自己的小队在历史进程中成长。玩家感受到的不是统率千军的宏大战略，而是「人物驱动」的旅程——每名武将的能力直接决定他所率部队在战场上的表现。

---

## Unique Hook

【建议】像《八方旅人》那样的 HD-2D 回合制 JRPG，**而且**每个战斗单位都是「一名武将率一支部队」：兵力就是 HP，武力／统率／智力决定部队攻防，步骑弓互相克制——三国武将的个性被翻译成可读的部队战术。

---

## Player Experience Analysis (MDA Framework)

### Target Aesthetics (What the player FEELS)

| Aesthetic | Priority | How We Deliver It |
| ---- | ---- | ---- |
| **Narrative** | 1【建议】 | 史实主线＋明确标注的演义／原创情节；桃园结义演出、徐州结尾过场【已确认】 |
| **Fantasy** | 2【建议】 | 扮演刘备，结识并率领关张【已确认】 |
| **Sensation** | 3【建议】 | 中式 HD-2D：Q 版像素人物＋立体场景与光影【已确认】 |
| **Challenge** | 4【建议】 | 兵种克制、速度顺序、技力管理；三场教学战难度递进【已确认机制】 |
| **Discovery** | 5【建议】 | 涿郡城镇探索、居民对话、最多 1 条非战斗支线【已确认】（Alpha 0.1 测试版不含支线） |
| **Expression** | 6【建议】 | 三槽配装与编队（选 1–3 人出战）【已确认】 |
| **Fellowship** | N/A | 单人，无联网【已确认】 |
| **Submission** | N/A【建议】 | — |

### Key Dynamics (Emergent player behaviors)

【建议】
- 玩家会根据兵种克制为每名武将挑选目标（如关羽骑兵打步兵），并保护会被步兵克制的刘备弓兵。
- 玩家会在高伤害回合前让脆弱单位防御，利用「防御先行」规则。
- 玩家会规划技力：先用减益（破阵、威慑），再集火首领。
- 前两战后回涿郡买装备、存档，再出城下一战。

### Core Mechanics (Systems we build)

【已确认】详细规则见 V1_SPEC 与 `design/gdd/combat-system.md`。
1. 传统对列回合制：全队先下令，再按速度结算；防御先行；兵力即 HP。
2. 武将三核心属性（武力／统率／智力）派生部队攻防、兵力与速度；步骑弓克制，占优 ×1.4。
3. 固定成长、自动学技：每将 2 项技能，等额经验，三战后到 4 级。
4. 三槽装备（武器／防具／饰品）与城镇买卖，单一货币铜钱。
5. 剧情事件：城镇招募关张 → 自动桃园结义演出 → 三场教学战 → 结尾过场。

---

## Player Motivation Profile

### Primary Psychological Needs Served

| Need | How This Game Satisfies It | Strength |
| ---- | ---- | ---- |
| **Autonomy** | 每回合为全队选指令；编队、配装、买卖选择【已确认】 | Supporting【建议】 |
| **Competence** | 理解克制、速度与技力后能更快获胜；教学提示分步出现【已确认】 | Core【建议】 |
| **Relatedness** | 与刘关张的结义情谊、城镇居民【已确认】 | Core【建议】 |

### Player Type Appeal (Bartle Taxonomy)

- [x] **Achievers** — 武将升级、技能解锁、三战完成【建议】
- [x] **Explorers** — 涿郡城镇与居民对话、支线【建议】
- [ ] **Socializers** — 无联网
- [ ] **Killers/Competitors** — 无 PvP

### Flow State Design

- **Onboarding curve**：第一战只教全队下令、目标、顺序、普攻与战后恢复；第二战加技力、技能、防御、治疗、集火与克制；第三战综合三级技能、减益、道具与目标优先级【已确认】。
- **Difficulty scaling**：三战敌方编组递进（程远志＋头领 → 张梁＋3 将 → 张角三兄弟）【已确认】；数值平衡【待定，需实测】。
- **Feedback clarity**：显示行动顺序、剩余回合、防御说明【已确认】。
- **Recovery from failure**：战败恢复战前状态立即重试，无永久损失【已确认】。

---

## Core Loop

### Moment-to-Moment (30 seconds)
【已确认】为最多 3 名武将选指令（攻击／技能／防御／道具），提交后观看按速度结算的部队交锋。

### Short-Term (5-15 minutes)
【已确认】一场教学战：读敌方情报 → 整备 → 数回合战斗 → 胜利结算（经验、铜钱、全恢复）。

### Session-Level (30-120 minutes)
【已确认】正式首版一次通关（Alpha 0.1 测试版在第三战后改为简短过场＋「测试版结束」）：入涿郡 → 结识关张入队 → 自动桃园结义 → 三场黄巾教学战（前两战后可回城整备）→ 徐州结尾过场 → 章节结算／返回标题。

### Long-Term Progression
【已确认】首版内：1 → 4 级、6 项技能、最多 12 件装备。长期：曹操、孙权视角与后续版本【待定】。

### Retention Hooks
- **Curiosity**：结尾过场引出赵云与徐州，后续章节【建议】
- **Investment**：刘关张成长与装备【建议】
- **Social**：N/A
- **Mastery**：克制与技力规划【建议】

---

## Game Pillars

【建议 — 由 PROJECT.md 已确认内容提炼，待用户确认】

### Pillar 1: 史实为骨，演义为彩
历史进程是主线骨架；演义典故与原创情节必须明确标注来源，不伪称史实。

*Design test*：若在「更戏剧化但无出处的情节」和「史实／标注过的演义情节」之间选择，选后者；原创情节可以有，但必须标为游戏改编（如三兄弟同场合战）。

### Pillar 2: 武将即部队
每名武将率一支部队，武将的能力差异必须体现为部队在战斗中的可读差异。

*Design test*：若在「新增独立个人 HP／额外系统」和「用现有属性、兵种和兵力表达差异」之间选择，选后者。

### Pillar 3: 中式 HD-2D
Q 版像素人物置于有立体空间与光影层次的场景中，建筑、道路、载具与服饰都为三国时代重新设计。

*Design test*：若在「直接沿用参考游戏的欧式／通用奇幻素材」和「为中国历史背景重新设计」之间选择，选后者。

### Pillar 4: 小而完整
首版是 30–60 分钟的完整短篇验证版，靠收敛范围换取完整可玩。

*Design test*：若在「为凑时长或丰富度新增战斗、武将、地图或系统」和「保持 V1_SPEC 上限」之间选择，选后者。

### Anti-Pillars (What This Game Is NOT)

【已确认为「未确认、不作为默认追加需求」的事项，PROJECT.md §4.7；表述为建议】
- **首版 NOT 国家经营／大规模战争模拟**：会稀释「武将即部队」的人物驱动体验；长期是否加入【待定】。
- **NOT 战棋地形与格子移动**：首版是对列回合制，不是战棋。
- **NOT 神怪／妖术体系**：计策表现为部队战术，违背「史实为骨」。
- **首版 NOT 联网、开放世界、分支结局、穿越**：超出「小而完整」的首版。其中长期分支结局【待定】，见共享目录 STORY_BRANCH_STRUCTURE.md（建议稿，等待用户决定）；穿越与神怪体系 PROJECT.md 已明确不引入。
- **NOT 抽卡、付费招募、重复角色升星**：武将剧情必得【已确认】。

---

## Visual Identity Anchor

【已确认方向，表述为建议】

- **Direction name**：中式 HD-2D 样板
- **One-line visual rule**：Q 版像素人物 × 立体光影场景，所有可见之物都属于三国时代的中国。
- **Supporting principles**
  1. **重新设计，不照搬**：建筑、道路、载具、服饰围绕中国历史背景重新制作。*Design test*：参考游戏只提炼构图、比例与光影，不搬用其素材或欧式形制。
  2. **轮廓即身份**：武将靠轮廓、动作、视线和情绪区分，而不只是换色或换脸（PROJECT.md「四人主立绘」）。*Design test*：两个角色剪影无法区分时，改轮廓，不只改颜色。
  3. **地图精灵紧凑一致**：地图人物以涿郡 NPC 风格为基准，简化五官与衣纹、头身紧凑。*Design test*：新精灵与涿郡 NPC 并排时比例不突兀。
- **Color philosophy**：四人主色 刘备橙、关羽绿、张飞黑、赵云白，用于立绘与地图角色的一致识别【已确认】。
- **已有美术【已确认】**：刘关张赵四人的立绘与像素九宫格；黄巾一方的程远志、张角、张宝、张梁和通用黄巾将领立绘，以及通用黄巾、张角、张宝、张梁 4 套九宫格；另有城主府掌士通用对话立绘（灰须文官坐姿图，不绑定张角或陶谦）。陶谦的过场立绘【待定】。见 INITIAL_SCENARIO_ART.md 与 `D:\BuildArtifacts\Codex\hd2d-jrpg\`。这些图都还没精确切片、补连续动画或导入 Unity。
- 敌方 Q 版规则【已确认】：普通黄巾将领（含程远志、杂兵头领、第二战 3 名将领）共用同一套黄巾军 Q 版；张角（文官／军师）、张宝、张梁（步兵）各有独立 Q 版。
- 具体像素尺寸、帧数、渲染分辨率【待定】，在 `/art-bible` 中细化。

---

## Inspiration and References

| Reference | What We Take From It | What We Do Differently | Why It Matters |
| ---- | ---- | ---- | ---- |
| 《八方旅人》 | HD-2D：像素角色＋立体场景与光影 | 中国三国时代的建筑与服饰 | 验证 HD-2D 风格的受众 |
| 史克威尔系 JRPG | Q 版人物比例、传统回合制 | 武将率部队、兵力即 HP | 经典回合制玩家熟悉的操作 |
| 《三国志14》／《三国志曹操传》／《三国志英杰传》 | 「人物能力影响部队表现」概念（仅参考） | 自有公式；不采用战棋地形、装备成长或 if 结局 | 三国题材玩家熟悉的武将能力映射 |

**Non-game inspirations**：《三国志》（陈寿）作主干史料；《三国演义》第 1、2、11 回作明确标注的演义补充【已确认】。

---

## Target Player Profile

| Attribute | Detail |
| ---- | ---- |
| **Age range** | 【待定】 |
| **Gaming experience** | 中度玩家（熟悉传统 JRPG）【建议】 |
| **Time availability** | 能一次坐下 30–60 分钟【建议，依首版时长】 |
| **Platform preference** | PC 单机【用户确认】 |
| **Current games they play** | 《八方旅人》类 HD-2D JRPG【建议，由用户的目标玩家描述推出】 |
| **What they're looking for** | 三国故事 × HD-2D 单机 JRPG【用户确认】；「回合制」为【建议】补充 |
| **What would turn them away** | 【建议】伪史实、神怪化三国、照搬欧式奇幻美术、抽卡 |

---

## Technical Considerations

| Consideration | Assessment |
| ---- | ---- |
| **Recommended Engine** | Unity 6.3 LTS + URP + C#【工程现状：`project.yaml`、`technical-preferences.md` 与现有原型】 |
| **Key Technical Challenges** | HD-2D 渲染（正交相机、像素角色与 3D 光影）、数据驱动的战斗规则、战前快照与存档防重 |
| **Art Style** | 2.5D：像素角色＋3D 场景 |
| **Art Pipeline Complexity** | 中（自制像素精灵＋3D 场景模块） |
| **Audio Needs** | 【待定】；无配音【已确认】 |
| **Networking** | 无【已确认】 |
| **Content Volume** | 首版：1 座城镇、桃园演出区、复用城外区域、最多 2 套战斗背景、3 场战斗、3 名武将、6 项技能、≤12 件装备、1 种道具、≤1 条非战斗支线【已确认】 |
| **Procedural Systems** | 无 |

---

## Risks and Open Questions

### Design Risks
- 3 场战斗能否撑起 30–60 分钟并教会全部机制【待实测】
- 初值平衡未测试【已确认为未测试】

### Technical Risks
- HD-2D 视觉在 URP 下的效果与性能【待验证】
- 战前快照、存档防重与自动演出操作锁的状态一致性

### Market Risks
- 【待定】

### Scope Risks
- 美术量（像素武将动画、城镇模块）可能超过战斗与系统工作量

### Open Questions
- 正式游戏名称【待定】
- 起始年份、人物年龄、具体对白与镜头【待定】
- 长期多主角切换形式【待定】
- 长期多线剧情、分歧点与结局【待定】：共享目录 STORY_BRANCH_STRUCTURE.md 为建议稿，等待用户决定
- 张角（文官／军师）的克制映射、数值与技能【待定】：战斗 GDD 已起草，草案待用户确认
- 第一战杂兵头领数量：用户 2026-10-18 确认为 2 名（经项目线程转达），尚未回写共享目录 V1_SPEC

---

## MVP Definition

**Core hypothesis**：【建议】「武将率部队」的对列回合制，加上刘关张的故事，在 30–60 分钟的短篇里既好懂又有趣。

**Required for MVP**（= Alpha 0.1「涿郡起兵」，见 `production/milestones/alpha-0.1.md`）：
1. 涿郡城镇：结识关张入队
2. 自动桃园结义演出
3. 三场黄巾教学战（战斗、成长、技能）
4. 第三战胜利后的简短过场，接「测试版结束」画面（测试版临时处理）

**Explicitly NOT in MVP**：第三战后的完整连续过场（剧情简介、徐州保卫、赵云参与与辞别、陶谦推举刘备接掌徐州）属于正式首版，测试版暂不做；非战斗支线测试版暂不做。【已确认】首版本身严格只有 3 场黄巾战和刘关张 6 个技能，没有后备换人、四选三、可选战、徐州战或可操作赵云。

### Scope Tiers (if budget/time shrinks)

| Tier | Content | Features | Timeline |
| ---- | ---- | ---- | ---- |
| **Alpha 0.1（测试版）** | 涿郡＋桃园演出＋三战＋简短过场＋「测试版结束」 | 战斗、成长、招募、自动结义演出、存档（装备商店是否纳入【待定】） | 【待定】 |
| **首版（V1_SPEC）** | ＋第三战后的完整连续过场（徐州保卫、赵云参与与辞别、接掌徐州，全部为过场）＋最多 1 条非战斗支线 | V1_SPEC 首版全部内容；战斗仍严格 3 场 | 【待定】 |
| **后续储备** | 可操作赵云、后备换人、四选三、离队返装 | V1_SPEC 列为后续储备，不属于首版 | 【待定】 |
| **Full Vision** | 曹操、孙权视角 | 多主角、多势力 | 【待定】 |

---

## Next Steps

- [ ] `/design-review design/gdd/game-concept.md`
- [ ] 用户确认 4 项 pillars 与视觉锚点表述
- [ ] `/art-bible`
- [ ] `/map-systems`
- [ ] `/design-system`（其余系统）
