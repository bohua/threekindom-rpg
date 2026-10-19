# ADR-0002: 战斗规则核心架构

## Status

Proposed（2026-10-19 起草并完成两项评审，评审意见已改进正文，见文末“评审记录”；待用户确认“待确认决定”后改为 Accepted）

## Date

2026-10-19

## Last Verified

2026-10-19（Unity 6000.3.26f1 本机编辑器与 IL2CPP 构建）

## Decision Makers

- 用户 Bohua：2026-10-19 授权按 CCGS 流程起草本 ADR 并跑评审，在 `feature/battle-core` 上继续开发并完成刘关张对程远志的 3v3 验收；同日确定界面用简体、场景招牌用繁体
- 起草：Claude（`/architecture-decision` 流程，在用户电脑上操作仓库的线程）
- 评审：`unity-specialist` 引擎校验与 `technical-director` TD-ADR 门（项目 `review_mode` 为 solo，本次按用户要求补跑）

## Summary

战斗系统需要一个确定、可测试的规则核心，供三场黄巾教学战、战斗界面和战前快照共用；现有 WIP 代码直接持有 ScriptableObject、放在 `ThreeKingdoms.Data`、数值写死，敌方 AI 也与已确认的 GDD 不符。本 ADR 决定：规则层是不引用 UnityEngine 的纯 C#，放进 `ThreeKingdoms.Core`；它只接收不可变输入（规格对象与 `CombatantInput`），整回合一次性结算并输出结构化事件列表，战斗结束只交出 `BattleResult`，从不改写战斗以外的状态；兵种、克制与倍率来自数据资产；敌方 AI 是按遭遇配置的策略，只读回合开始时的快照。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS（6000.3.26f1） |
| **Domain** | Core / Scripting（规则层）；表现层只定约定 |
| **Layer** | Core |
| **Knowledge Risk** | LOW（规则层是纯 C#，不调用引擎 API）；表现层的 UI Toolkit 运行时与系统字体为 HIGH，已按 ADR-0001 要求在本机编辑器与 IL2CPP 构建里实测 |
| **References Consulted** | ADR-0001（程序集依赖图、`noEngineReferences`、UI Toolkit、`ToRuntime()` 命名）；ADR-0005 §8（战斗分层背景与受光面片人物）；`design/gdd/combat-system.md`、`character-progression.md`、`equipment-items.md`、`save-system.md`、`story-events.md`、`game-ui.md`（PR #3 分支）；共享文档 V1_SPEC §4–§8、§14，BATTLE_SCREEN_DESIGN.md V2；`docs/engine-reference/unity/modules/ui.md`；本机 `UnityEngine.UIElementsModule.xml` 与 `TextRenderingModule.xml` 的 API 签名 |
| **Post-Cutoff APIs Used** | 规则层：无。表现层：UI Toolkit 运行时（`UIDocument`、`PanelSettings`、代码构建 `VisualElement`、`RuntimePanelUtils.CameraTransformWorldToPanel`）、`FontDefinition.FromFont`、`Font.CreateDynamicFontFromOSFont(string[], int)`、Input System `InputSystemUIInputModule` |
| **Verification Required** | ① `ThreeKingdoms.Core` 在 `noEngineReferences` 下编译；② Core 可测的 GDD 验收条目全部有 EditMode 测试；③ 可玩验收场景在 IL2CPP 构建中运行，中文无缺字（实机截图核对）；④ 键盘／手柄能经 `InputSystemUIInputModule` 操作 HUD |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001（Accepted：程序集划分、依赖方向、`Win64-Release` IL2CPP Build Profile） |
| **Enables** | ADR-0003 数据资产格式（本 ADR 先定“SO → 规格对象”边界）；ADR-0004 存档格式（快照与防重标记的持久化）；ADR-0007 文本与本地化（替换临时字体与 `BattleText`）；战斗界面 UX 规格 |
| **Blocks** | 三场教学战的实现故事；战斗界面；成长、装备与剧情系统接入战斗结果 |
| **Ordering Note** | 与 ADR-0005 互不依赖。ADR-0003／0004 必须沿用本 ADR 的规格对象、`CombatantInput` 与 `BattleResult` 边界，不得让规则层读 ScriptableObject、存档 DTO 或其他系统的状态 |

## Context

### Problem Statement

战斗是首版唯一的核心玩法，三场教学战全部依赖它。GDD 已确认全部规则（V1_SPEC §4–§7、§14 与 `combat-system.md`），要求“所有数值来自数据文件”“确定性、无随机”，并有 30 多条可测验收条目。现有 WIP（PR #8 的 `eaae2a2`）实现了大部分结算，但有五个结构问题，必须在铺内容前定下来：

1. 规则类直接引用 ScriptableObject，只能放在 `ThreeKingdoms.Data`，违背 ADR-0001 的分层；
2. 兵种只有步／骑／弓，数值写死在 `switch` 里，放不进敌方专用的黄巾军与文官／军师；
3. 敌方 AI 与用户 2026-10-18 确认的规则不符；
4. 战斗自己持有并改写经验、军粮、铜钱和完成标记，与 GDD 的状态归属冲突（成长、装备与道具、剧情、存档各自持有）；
5. 没有装备加值的输入，也没有规则层与表现层之间的约定。

### Current State

- WIP 代码：`Assets/_Project/Scripts/Data/Battle`（命名空间 `Jrpg.Battle`），没有测试、资产和界面。
- 战斗画面样板（PR #10）：`Battle_ZhuojunMarket` 场景，正交相机、640×360 分层背景、刘关张与黄巾受光面片，没有逻辑。

### Constraints

- ADR-0001：`ThreeKingdoms.Core` 不引用 UnityEngine；依赖方向 Core ← Data ← Gameplay ← UI ← App。
- 首版规模：我方最多 3 人、敌方最多 4 人、每场十几回合；可读性与可测性比性能重要。
- 结果必须确定：同一输入与同一指令序列，在任何平台、帧率、脚本后端下结果相同。

### Requirements

- 覆盖 GDD 的结算顺序、伤害公式、克制、增减益、失效目标、资源扣除与立即胜负判定。
- 敌方 AI 只读回合开始时的状态。
- 下令阶段能查询技能是否可选（技力不足置灰）。
- 战斗不改写战斗外状态；胜利交出结果，由各所属系统入账；战败什么都不入账。
- 界面逐条回放结算，回放速度不影响结果。

## Decision

### 1. 分层与程序集

```
ThreeKingdoms.Core      Battle/   纯 C# 规则：规格对象、CombatantInput、BattleState、BattleUnit、指令、事件、
        ↑                         敌方 AI、BattleSession、BattleResult
ThreeKingdoms.Data      Battle/   ScriptableObject：GeneralDefinition、BattleSkillDefinition、EncounterDefinition、
        ↑                         TroopTableDefinition、BattleConfigDefinition、ItemDefinition、BattleCatalog.ToRuntime()
ThreeKingdoms.Gameplay  Battle/   BattleController（阶段状态机、回放）、BattleUnitView、DevPartyProgress（临时）
        ↑
ThreeKingdoms.UI        Battle/   BattleHud（UI Toolkit）、BattleText（全部战斗文字）、BattleFont（临时字体）
```

- 命名空间与程序集一致（`ThreeKingdoms.Core.Battle` 等），旧的 `Jrpg.Battle` 移除。
- `[assembly: InternalsVisibleTo("ThreeKingdoms.Tests.EditMode")]` 放在 `Core/Battle/AssemblyInfo.cs`。

### 2. 输入：规格对象、`CombatantInput` 与数据边界

- 规则层只认不可变对象：`GeneralSpec`、`SkillSpec`、`TroopTable`、`EncounterSpec`、`BattleConfig`、`ItemSpec`。
- 我方每名出战武将用一个 `CombatantInput`（武将规格、等级、已解锁技能、装备加值合计 `StatBonus`）。它由 Gameplay 层从成长系统（等级、技能）与装备系统（加值）汇总后传入，战斗不读这两个系统。
- 敌方用 `EnemySlot`（武将规格 + 等级）。敌方三核心固定、成长为 0，跨战只换等级（GDD 敌方表），不带装备。
- 军粮的数值来自 `ItemSpec`（装备与道具系统的数据），数量作为开战参数传入。
- ScriptableObject 只做编辑器数据载体，`BattleCatalog.ToRuntime()`（沿用 ADR-0001 的命名）在开战时转换一次并校验：ID 唯一、引用非空、每个兵种都有数值、我方不含敌方专用兵种。EditMode 测试遍历全部 `BattleCatalog` 资产调用 `ToRuntime()`，构建前就能发现坏数据；更完整的校验与 JSON 分工留给 ADR-0003。
- GDD 标为“data file”的变量全部进入数据：兵种兵力修正、物防修正、基础速度、玩家可用标记与克制对；技能系数、技力、目标、恢复与增减益；克制 140%、防御 50%、下限 20%、经验表、属性上限；军粮恢复 30%。派生公式的结构常数（如 `80 + 20 × 等级`）是公式本身，写成代码。

### 3. 兵种

- `TroopType` 为封闭枚举：`Infantry`、`Cavalry`、`Archer`、`YellowTurban`、`Strategist`。新增兵种属于设计变更。
- 数值与克制对来自 `TroopTable` 数据：步 +30／+5／6、骑 +10／0／10、弓 0／0／8、黄巾军 +20／0／6、文官／军师 0／0／7；克制对 骑→步、步→弓、弓→骑、骑→黄巾军。文官／军师不在任何克制对里。黄巾军与文官／军师 `PlayerUsable = false`，`BattleSession` 拒绝把它们编入我方。

### 4. 回合结算：指令收集 → 一次性结算 → 事件回放

```
下令（可反复修改、可退回上一人） ──提交──▶ 敌方 AI 读取回合开始快照（BattleView）
        ▲                                              │
        │                                              ▼
   新回合 ◀── 回合结束（增减益 −1） ◀── BattleSession.PlayRound → 事件列表
                                                       │
                                                       ▼
                                     表现层逐条播放（不回调规则层）
```

- 唯一的推进入口是 `BattleSession.PlayRound(我方指令)`：先让敌方 AI 按回合开始快照选指令，再结算整回合并返回事件列表。`BattleState.ResolveRound` 为 `internal`，界面无法绕过 AI 或结果直接结算。
- 行动顺序在回合开始时按“速度降序 → 同速我方先 → 同侧出战位”排定，回合内增减益不改变顺序；防御在其他动作之前生效。
- 每个动作后检查胜负，一方无存活单位时立即结束，后续动作不执行、不扣技力或道具。
- 事件是结构化记录：种类、行动者、目标、技能、指令种类、数值、事件后目标剩余兵力、是否克制、是否防御、取消原因、增减益数值与剩余回合；另有 `ActionStarted` 标记每个动作开始，供表现层放动画。事件不含文字。
- 查询接口：`ActionOrder()`、`CanUseSkill`、`CanUseRation`、`LegalTargets`。

### 5. 敌方 AI

```csharp
public interface IEnemyAi { IReadOnlyList<BattleCommand> Decide(BattleView view); }
```

- `BattleView` 只暴露回合数与双方单位，不含我方本回合指令。
- 遭遇数据选择策略，首版两种：
  - `HighestTroops`（第一战）：每个敌人普攻当前兵力最高的存活我方单位，同值按出战位。
  - `FacingSlot`（第二、三战）：攻击对位我方单位（敌方第 N 位对我方第 N 位，多出的对我方最后一位）；对位在回合开始时已撤退，则打兵力最低者。有技能的敌人在 `SkillEveryRounds`（=3）倍数回合且技力足够时放技能，按武将数据中的技能顺序轮换（张角：第 3、9、15 回合惑众类，第 6、12 回合纵火类）；单体技能打与普攻相同的目标。
- 敌方不防御、不用道具；目标在结算中途撤退时，普攻改打第一个存活单位（与我方同一规则）。

### 6. 数值表示：整数定点

- 百分数一律存整数。伤害用 `long` 一次算完再整除取整：`(2·攻·攻倍率·系数 − 100·防·防倍率) × 克制 × 防御 ÷ (20000·100·100)`，结果至少为 1。没有 `float` 和 `decimal`，所有平台与脚本后端结果相同（验收示例 40.8 → 21 精确成立）。
- 无随机数。日后若需随机，必须由会话注入种子，规则层不得调用 `System.Random` 或 `UnityEngine.Random`。

### 7. 结果与状态归属

- 战斗不持有、不改写战斗外状态。战斗结束交出 `BattleResult`：遭遇 ID、胜负、回合数、等额经验、铜钱奖励、实际用掉的军粮。
- 胜利时由各所属系统入账：成长系统给出战与未出战的武将加同样的经验（不按出战人数稀释）；装备与道具系统加铜钱、扣军粮；剧情系统写 `battle_N_done` 完成标志，并以它判断是否重复发奖。战败时什么都不入账，所以“还原战前状态”在战斗这一侧只是丢弃 `BattleSession`。
- 战前快照（编队、装备、库存、铜钱、经验、事件状态）、自动存档、防重标记的持久化由 ADR-0004 决定，属于 `ThreeKingdoms.Save`。
- 在这些系统实现之前，Gameplay 里的 `DevPartyProgress` 临时代为入账，只服务验收场景。它被成长、装备、剧情系统与 ADR-0004 取代后删除，不得被其他系统引用。

### 8. 表现层约定

- `BattleController`（Gameplay）是唯一驱动规则层的组件，阶段：`Commanding` → `Playback` → 下一回合或 `Finished`。它持有单位视图（`BattleUnitView`，含卡片用的胸像与裁切），对外发出 `BattleStarted`、`Changed`、`EventPlayed` 事件。
- UI 只读状态与事件，经 `BattleController.Choose/Undo/StartBattle` 操作，不直接改规则状态；胸像与兵种图标由 `BattleController.ViewOf(unit)` 与 HUD 的序列化字段提供，UI 不需要引用 Data。
- 界面布局按 BATTLE_SCREEN_DESIGN.md V2：我方左、敌方右；底部人物卡片（主立绘胸像、等级、兵力／技力条、右上兵种图标），当前单位卡片与脚下光环同时高亮；左侧指令菜单 攻击／技能／防御／道具；右上回合数；敌方头顶名字与兵力条。
- 输入：战斗场景放 EventSystem + `InputSystemUIInputModule`；HUD 只用 UI Toolkit 的按钮与导航事件，打开菜单时聚焦第一个可用项；加速等快捷键将来走 Gameplay 的 Input Actions，UI 程序集不引用 Input System。
- 文字：界面用简体中文（用户 2026-10-19 确定）；场景贴图内的招牌、幌子用繁体（ADR-0005 第 6 节）。所有战斗文字集中在 `BattleText`，换成 ADR-0007 的本地化表时只替换这一处。
- 中文字体（临时，开发与验证构建用）：ADR-0007 之前，`BattleFont` 用 `Font.CreateDynamicFontFromOSFont` 依次尝试微软雅黑、黑体、宋体、等线，经 `FontDefinition.FromFont` 设在 HUD 根元素上一次；都找不到时记录错误。只适用于 Windows，不打包第三方字体。

### Key Interfaces

```csharp
namespace ThreeKingdoms.Core.Battle
{
    public sealed class CombatantInput
    {
        public CombatantInput(GeneralSpec general, int level, IEnumerable<SkillSpec> skills, StatBonus equipment = default);
        public static CombatantInput AtLevel(GeneralSpec general, int level, StatBonus equipment = default);
    }

    public sealed class BattleSession
    {
        public BattleSession(EncounterSpec encounter, IReadOnlyList<CombatantInput> lineup, ItemSpec rationItem, int rations,
                             BattleConfig config, TroopTable troops, IEnemyAi ai = null);
        public BattleState State { get; }
        public BattleResult Result { get; }                    // 胜负判定后非空
        public IReadOnlyList<BattleEvent> PlayRound(IEnumerable<BattleCommand> playerCommands);
    }

    public sealed class BattleState                            // 只读查询；ResolveRound 为 internal
    {
        public int Round { get; }
        public BattleOutcome Outcome { get; }
        public IReadOnlyList<BattleUnit> Players { get; }
        public IReadOnlyList<BattleUnit> Enemies { get; }
        public IReadOnlyList<BattleUnit> ActionOrder();
        public bool CanUseSkill(BattleUnit actor, SkillSpec skill);
        public bool CanUseRation(BattleUnit actor);
        public IReadOnlyList<BattleUnit> LegalTargets(BattleUnit actor, CommandKind kind, SkillSpec skill = null);
    }

    public sealed class BattleResult
    {
        public string EncounterId { get; }
        public BattleOutcome Outcome { get; }
        public int Rounds { get; }
        public int Exp { get; }            // 胜利时，每名武将等额
        public int Copper { get; }
        public int RationsUsed { get; }
    }
}
```

### Implementation Guidelines

- 规则层**必须**放在 `ThreeKingdoms.Core`，**绝不能**引用 UnityEngine、ScriptableObject、Save DTO 或其他系统的状态对象。
- 规则层**绝不能**调用随机数、时间、帧计数、日志或 I/O；诊断信息走事件或异常，由 Gameplay 层输出。
- Core **绝不能**有可变的静态状态（关闭域重载时会残留）。
- GDD 标为数据的数值**必须**来自 `BattleConfig`／`TroopTable`／`SkillSpec`／`ItemSpec`，**绝不能**在规则代码里写 140、50、20、30 或兵种数值。
- 敌方 AI **必须**只接收 `BattleView`；**绝不能**接收我方本回合指令。
- 战斗**绝不能**改写战斗外状态，只能交出 `BattleResult`。
- 表现层**绝不能**在播放事件时修改规则状态，也**绝不能**依赖动画回调推进规则。
- 界面文字**必须**经 `BattleText` 生成，**绝不能**在 HUD 里拼接字面量中文。

## Alternatives Considered

### Alternative 1: 保留 WIP 结构（规则直接用 ScriptableObject，留在 Data）

- **Pros**：不用重构。
- **Cons**：违背 ADR-0001 分层；测试要建 SO；数值写死。
- **Rejection Reason**：分层违规会扩散到 ADR-0003／0004；GDD 要求数值数据化。

### Alternative 2: 纯 C# 逐动作步进（`ResolveNextAction()`）

- **Description**：规则层仍确定、纯 C#，但每次只结算一个动作，表现层播完再取下一个。
- **Pros**：天然贴合“每个动作后更新行动队列与数值”的界面需求；中途可以插入教学提示。
- **Cons**：回合内状态要能被外部观察到“半回合”，立即胜负、取消与回合结束的状态机多出中间态，测试需逐步驱动；跳过与加速要处理未结算的剩余动作。
- **Rejection Reason**：整回合结算 + 每个事件带“事件后剩余兵力”已能满足逐条显示；教学提示可按事件种类在回放中触发。若之后的教学需要在动作之间暂停玩家输入，再评估改为步进，事件格式不变。

### Alternative 3: 逐动作协程结算，与动画交织

- **Pros**：实现直观。
- **Cons**：规则依赖帧与动画时长，无法在 EditMode 下秒级跑整场；跳过与回放难做。
- **Rejection Reason**：违背确定性与可测性。

### Alternative 4: `decimal` 运算

- **Pros**：直接对应公式里的小数，代码好读；`decimal` 在 Mono 与 IL2CPP 下同样确定。
- **Cons**：比整数慢；需要 `Math.Floor` 后显式转换，每个中间步骤都要留意精度。
- **Rejection Reason**：整数定点只有一次整除，没有中间舍入，也不需要额外验证脚本后端的一致性。

### Alternative 5: ECS／DOTS

- **Rejection Reason**：单场最多 7 个单位，没有性能需求，维护成本高。

## Consequences

### Positive

- 规则层在 EditMode 下毫秒级测完全部可测条目，整场 3v3 也能按固定指令跑完并逐事件比较。
- 表现层可自由调整动画、加速与跳过，不影响结果。
- 敌方兵种、AI 与数值调整只改数据。
- 成长、装备、剧情与存档系统的边界清楚：战斗只给结果，谁的状态谁入账。

### Negative

- 多一层 SO → 规格对象转换，字段要在两处一致（由转换测试兜底）。
- 规则层不能用 Unity 的调试工具，需要事件日志。
- `DevPartyProgress` 是临时代码，正式系统落地时必须删除。
- 临时字体只适用于 Windows；Steam Deck 等平台必须等 ADR-0007。

## Risks

- **GDD 仍在评审**：`combat-system.md` 标为 In Review，而 `equipment-items.md` 把它写成 Approved，两者不一致。缓解：测试按 GDD 条目编写，GDD 修订时用 `/propagate-design-change` 同步测试；状态不一致在 PR #3 中修正。
- **系统字体缺失**：精简版 Windows 可能没有微软雅黑。缓解：依次回退黑体、宋体、等线，全部缺失时记录错误；只用于开发与验证构建。
- **事件粒度不足**：表现层若需要更细的中间状态，会倾向自行推算。缓解：事件带事件后剩余兵力与增减益数值，新需求先加事件字段。
- **临时入账代码被误用**：缓解：`DevPartyProgress` 标注为临时，正式系统接入时删除，code review 检查无其他引用。

## GDD Requirements Addressed

| GDD System | Requirement | 处理方式 |
|------------|-------------|---------|
| combat-system.md | 结算顺序：防御先行、速度降序、同速我方先、同侧按出战位 | 本 ADR：第 4 节 |
| combat-system.md | 伤害公式、克制 ×1.4 一次、防御 ×0.5、至少 1 | 本 ADR：第 6 节 |
| combat-system.md | 敌方专用兵种与 `PlayerUsable` 校验 | 本 ADR：第 3 节 |
| combat-system.md | 敌方固定 AI | 本 ADR：第 5 节 |
| combat-system.md | 立即胜负判定，未执行动作不扣资源 | 本 ADR：第 4 节 |
| combat-system.md | 编队 1–3 人、空队不可开战、同一武将不重复 | 本 ADR：`BattleSession` 构造校验 |
| combat-system.md | 技力不足的技能置灰 | 本 ADR：`CanUseSkill`；界面置灰为 UX 规格 |
| combat-system.md | 所有数值来自数据文件 | 本 ADR：第 2 节 |
| combat-system.md | 张梁等“部队溃退、本人撤走”，不播死亡 | 表现层：`BattleUnitView.Retreat` 只做后撤与收起；正式演出归 UX 规格 |
| character-progression.md | 等额经验、经验表、等级解锁技能 | 本 ADR 交出经验；入账归成长系统（暂由 `DevPartyProgress`） |
| equipment-items.md | 装备加值进入派生公式 | 本 ADR：`CombatantInput.Equipment`；加值的数据与汇总归装备系统 |
| equipment-items.md | 铜钱奖励、军粮消耗与库存 | 本 ADR 交出 `Copper`／`RationsUsed`；入账归装备与道具系统 |
| story-events.md | 每场只发一次奖励、`battle_N_done` 标志 | 归剧情系统（暂由 `DevPartyProgress`）；本 ADR 用遭遇 ID 对齐标志名 |
| story-events.md | 教学提示触发 | 延后：剧情系统订阅 `BattleController.EventPlayed`，由 App 注入，具体在教学提示故事中定 |
| save-system.md | 战前快照、战败还原、读旧档可再领奖 | 延后到 ADR-0004；本 ADR 保证战斗不改写战斗外状态 |
| game-ui.md | 行动顺序、兵力／技力、剩余回合、防御说明 | 本 ADR：查询接口与事件；布局按 BATTLE_SCREEN_DESIGN V2，细节归 UX 规格 |

## Performance Implications

- **CPU**：每回合结算微秒级。
- **Memory**：每场战斗几 KB；HUD 胸像 3 张 256×256。
- **Load Time**：开战时 SO 转换；首次进入战斗时从系统字体建动态字体，需载入约 20 MB 的字体文件。
- **Network**：不适用。

## Migration Plan

1. 规则类移到 `Core/Battle`，改命名空间，改用规格对象与 `CombatantInput`，删除 `PartyState` 与快照代码。
2. `Data/Battle` 只留 SO，新增 `TroopTableDefinition`、`BattleConfigDefinition`、`ItemDefinition`、`BattleCatalog.ToRuntime()`。
3. `BattleText` 移到 `UI/Battle`，集中全部战斗文字。
4. 编辑器工具 `BattleDataBuilder` 生成首版数据资产（刘关张、三场遭遇的敌方、6 项我方技能与 4 项敌方技能、兵种表、战斗配置、军粮）。
5. EditMode 测试：GDD 可测条目、资产转换、第一战 3v3 整场确定性。
6. `BattleSceneBuilder` 在战斗样板上搭可玩验收场景 `Battle_Tutorial01`，PlayMode 测试整场跑通。

## Validation Criteria

**规则层（EditMode）**

- [ ] `ThreeKingdoms.Core` 在 `noEngineReferences` 下编译，规则层无 UnityEngine 引用
- [ ] 派生数值与 GDD 表一致（刘关张 1 级、程远志、杂兵头领）
- [ ] 伤害示例：突击 102／防御 51、火矢 54、威慑后 21（无威慑 32）、至少 1
- [ ] 克制方向、文官／军师不参与克制；改数据中的克制倍率，结果随之变化（证明无写死）
- [ ] 防御先行、同速我方先与出战位顺序
- [ ] 失效目标：普攻转攻、单体技能取消不扣技力、全体技能只打剩余、军粮对满兵力取消不扣
- [ ] 技力不足：`CanUseSkill` 为 false，强制下令时取消且技力不变；战中技力不回复
- [ ] 增减益：不叠加并刷新、正负相加、下限 0.2、剩余回合 2→1→移除、破阵从下一击起生效
- [ ] 立即胜负判定，未执行动作不扣资源；战败时结果不含奖励
- [ ] 敌方专用兵种不能编入我方；空队不能开战
- [ ] AI：第一战打兵力最高者；第二、三战打对位、对位撤退打最弱、第 3／6 回合轮换技能；同一快照下不同我方指令得到相同 AI 指令
- [ ] 第一战 3v3 按固定指令两次运行，事件序列相同并胜利
- [ ] 全部 `BattleCatalog` 资产 `ToRuntime()` 通过，三场遭遇编组、经验与铜钱与 GDD 一致

**表现层（PlayMode 与人工）**

- [ ] PlayMode：`Battle_Tutorial01` 中刘关张对程远志＋2 名杂兵头领能打到胜利，首胜发奖（经验 10、铜钱 +30，三人升到 2 级），再战一次不重复发奖
- [ ] IL2CPP 构建中场景可运行，截图核对中文无缺字、卡片与指令菜单布局符合 V2
- [ ] 人工：能下令（攻击／技能／防御／道具）、选目标、退回上一人，看行动顺序与回放；能胜利结算，也能战败后重新挑战

**移交其他文档**

- 战前快照与读旧档再领奖：ADR-0004
- 置灰样式、教学提示、溃退演出细节：战斗界面 UX 规格与剧情事件故事

## 待确认决定

以下为推荐方案，用户确认后改为 Accepted：

| # | 问题 | 推荐 |
| --- | --- | --- |
| 1 | 规则层位置 | 纯 C#，`ThreeKingdoms.Core`；SO 经 `ToRuntime()` 转成规格对象 |
| 2 | 结算模型 | 整回合一次性结算 + 事件列表回放 |
| 3 | 状态归属 | 战斗只接收 `CombatantInput` 并交出 `BattleResult`，由成长、装备与道具、剧情系统各自入账 |
| 4 | 兵种 | 封闭枚举 + `TroopTable` 数据（含两个敌方专用兵种） |
| 5 | 敌方 AI | `IEnemyAi` 策略接口，遭遇数据选 `HighestTroops`／`FacingSlot` |
| 6 | 数值表示 | 整数定点，无随机 |
| 7 | 中文字体 | ADR-0007 之前临时用 Windows 系统字体，只用于开发与验证构建 |

## Related

- ADR-0001：引擎、程序集与目录结构（依赖）
- ADR-0003：数据资产格式（本 ADR 启用）
- ADR-0004：存档格式与版本迁移（本 ADR 启用，承接快照与防重标记）
- ADR-0005：HD-2D 渲染与相机，§8 战斗分层背景（表现层共用）
- ADR-0007：文本与本地化（替换临时字体与 `BattleText`）
- `design/gdd/combat-system.md`、`character-progression.md`、`equipment-items.md`、`save-system.md`、`story-events.md`、`game-ui.md`
- 共享文档 V1_SPEC §4–§8、§14；BATTLE_SCREEN_DESIGN.md V2

## 评审记录（2026-10-19）

两项评审都在初稿完成后运行（只读），结论都是 **CONCERNS**，没有 REJECT。所有意见已经改进正文，处理如下：

| 来源 | 意见 | 处理 |
| --- | --- | --- |
| technical-director 1（阻断） | `PartyState` 持有经验、军粮、铜钱与完成标记，与成长、装备、剧情、存档的 GDD 归属冲突 | 删除 `PartyState`；战斗只交出 `BattleResult`，各系统入账（第 7 节）；临时入账代码 `DevPartyProgress` 放在 Gameplay 并标注删除条件 |
| technical-director 2（阻断） | 没有装备加值的输入 | 新增 `CombatantInput`（含 `StatBonus` 装备加值）；军粮数值改由 `ItemSpec` 提供（第 2 节） |
| technical-director 3（阻断） | 战败还原范围不全 | 第 7 节：战斗不改写战斗外状态，战败即丢弃会话；完整快照归 ADR-0004 |
| technical-director 4／unity-specialist 9 | `ResolveRound` 公开，可绕过会话 | 改为 `internal`，唯一入口 `BattleSession.PlayRound` |
| technical-director 5 | GDD 对照表遗漏 | 补编队、`PlayerUsable`、铜钱、装备、教学提示、溃退表现，并标明归属 |
| technical-director 6 | 验收不可全由 EditMode 覆盖；缺 AI 独立性与无写死的可测项 | 验收按规则层／表现层／移交拆分；新增两项测试 |
| technical-director 7 | 字体方案越界；`BattleText` 拼死中文 | 字体明确为临时、只用于开发与验证构建；全部文字集中在 `BattleText`，由 ADR-0007 替换 |
| technical-director 8／unity-specialist 3 | 缺“逐动作步进”与“整数定点”备选；`decimal` 跨后端的验收不清 | 补备选 2、4；改用整数定点，删去 Mono／IL2CPP 对比验收 |
| technical-director 9 | `ToSpec` 与 ADR-0001 的 `ToRuntime` 不一致；敌方固定属性；张角技能目标 | 统一为 `ToRuntime()`；第 2、5 节写明 |
| technical-director 10 | 登记表 | Accepted 时登记，见下方“登记表候选” |
| unity-specialist 1（阻断） | `FontAsset.CreateFontAsset` 用法不完整、签名未核实 | 本机 6000.3.26f1 的公开文档未列出该重载；改用已核实的 `Font.CreateDynamicFontFromOSFont(string[], int)` + `FontDefinition.FromFont`，英文家族名依次回退，在根元素设置一次；IL2CPP 实机截图核对 |
| unity-specialist 2 | 输入路由未写明 | 第 8 节：EventSystem + `InputSystemUIInputModule`，菜单聚焦第一项，UI 不引用 Input System |
| unity-specialist 4 | 迁移漏了 `InternalsVisibleTo` | 第 1 节：放到 `Core/Battle/AssemblyInfo.cs` |
| unity-specialist 5 | Core 不能用 Unity 工具、静态状态风险 | Implementation Guidelines 补充 |
| unity-specialist 6 | 数据校验太晚 | EditMode 测试遍历全部 `BattleCatalog` 资产 |
| unity-specialist 7 | UI 拿头像的路径未定 | `BattleUnitView` 持有胸像，HUD 经 `ViewOf` 读取，UI 不引用 Data |
| unity-specialist 8 | 依赖 IL2CPP Build Profile | Dependencies 写明依赖 ADR-0001 的 `Win64-Release` |

### 登记表候选（Accepted 时写入 `docs/registry/architecture.yaml`）

- forbidden_patterns：Core 引用 UnityEngine 或 ScriptableObject；规则层使用随机数、时间或 I/O；敌方 AI 读取我方本回合指令；战斗改写战斗外状态
- interfaces：`battle_event_playback`（规则层产出 `BattleEvent` 列表，Gameplay 回放、UI 显示）；`battle_result`（`BattleResult` 由成长、装备与道具、剧情系统消费）
- state_ownership：战斗只拥有单场内的单位状态；经验归成长系统，库存与铜钱归装备与道具系统，完成标志归剧情系统，快照归存档系统
