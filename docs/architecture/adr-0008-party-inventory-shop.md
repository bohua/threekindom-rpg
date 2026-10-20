# ADR-0008: 队伍库存、装备与涿郡商店

## Status

Proposed（2026-10-20 起草；用户 2026-10-20 要求完成一项后直接继续 Alpha 0.1 开发。用户确认“待确认决定”后改为 Accepted）

## Date

2026-10-20

## Last Verified

2026-10-20（Unity 6000.3.26f1）

## Decision Makers

- 用户 Bohua：equipment-items GDD 已于 2026-10-18 复审通过，12 件装备目录与 7 项待定问题于 2026-10-19 确认采用
- 起草：Claude

## Summary

Alpha 0.1 要在前两战之间让玩家在涿郡铁匠 N04 处买卖装备、比较新旧数值并更换三槽装备，战斗要算上装备加值，战败重试要还原铜钱与库存。本 ADR 决定：铜钱、军粮、背包与每名武将的三槽由 `ThreeKingdoms.Core.Economy.Inventory`（纯 C#）唯一持有，所有交易规则都是它的方法；物品数值来自 `EconomyCatalog` 资产；`GameRun` 跨场景持有它；商店界面 `ShopHud` 只调用这些方法并显示结果。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS（6000.3.26f1） |
| **Domain** | Core（规则）；Data（ScriptableObject 目录）；UI（UI Toolkit 商店） |
| **Layer** | Core |
| **Knowledge Risk** | LOW：规则为纯 C#；界面沿用 ADR-0002／0006 的 UI Toolkit 与 EventSystem 做法 |
| **References Consulted** | ADR-0001、0002、0006；`design/gdd/equipment-items.md`、`game-ui.md`、`town-exploration.md`；V1_SPEC §8–9 |
| **Post-Cutoff APIs Used** | 无新增 |
| **Verification Required** | EditMode：GDD 验收条目；PlayMode：在城中与 N04 对话完成买、卖（确认）、更换并回到操作，新装备进入战斗编队 |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001（分层）、ADR-0002（`StatBonus`／`CombatantInput` 只读装备加值）、ADR-0006（`GameRun` 跨场景持有） |
| **Enables** | ADR-0004 存档：库存、装备归属与铜钱的序列化 |
| **Blocks** | 无 |

## Context

ADR-0002 只给战斗留了只读的 `StatBonus` 入口，此前铜钱和军粮由临时的 `DevPartyProgress` 持有，战斗没有算任何装备。GDD 要求：三槽、兵种限制武器、卖价为买价一半（初始装备按配置售价）、已装备不能直接卖、出售需确认、同种物品可重复购买、战败还原。

## Decision

1. **规则层**：`EquipmentSpec`、`EconomyConfig`、`EconomyData`（不可变）与 `Inventory`（可变）放在 `ThreeKingdoms.Core.Economy`，不引用引擎。每个操作返回 `TradeResult`：要么成功，要么什么都不改并说明原因（铜钱不足、仍在装备中、兵种不符、不卖此物、背包没有、没有军粮）。
2. **数据**：`EquipmentDefinition` 与 `EconomyCatalog`（初始铜钱 120、军粮 2、军粮 10 铜钱、卖价 50%、12 件目录、初始装备）由 `BattleCatalog.Economy` 引用，`ToRuntime()` 校验初始装备与兵种、同槽冲突。商店是否出售由买价是否大于 0 决定，初始装备与支线奖励的买价为 0，按配置售价收购。
3. **初始装备**：新游戏时三人的初始装备都已在各自槽中；关羽、张飞未入队时不出现在商店和编队里，入队即带着自己的装备（GDD 规则 4），不需要额外的发放事件，也就不可能重复发放。
4. **战斗**：`DevPartyProgress.Lineup` 把 `Inventory.Bonus(general)` 交给 `CombatantInput`；胜利时只调用 `SpendRations` 与 `AddCopper`；战败什么都不写，就是还原。`Inventory.Clone()` 留给 ADR-0004 的战前快照。
5. **商店**：`TownStoryController` 在 N04 附近给出提示，打开时暂停移动、交给 `ShopHud`，关闭的那一帧不响应交互和菜单键（避免同一次按键马上重新打开或退出游戏）。`ShopHud` 分购买／出售／更换装备三项，出售一律先确认，已装备物品列出但提示先卸下；选中物品时对每名已入队武将显示派生数值的新旧对比（用战斗同一套公式 `BattleRules.Compute`），不能装备的标明兵种限制。取消键逐级返回，最上层关闭商店。
6. **武器槽为空**：允许出战；在南门出城前提示一次，再次确认即出城（GDD 待定问题 2 的答案）。

## Alternatives Considered

- **把铜钱、库存放进 StoryState 或 GameRun 字段**：会让剧情状态承担经济规则，交易规则散落在界面里。不采用。
- **入队时由剧情事件发放初始装备**：需要一个“已发放”标志防重复，读档时还要对齐；直接把初始装备放在槽里更简单且无法重复。不采用。
- **商店按“件”限量**：用户 2026-10-19 确认 12 件指 12 种、商店不限量。

## Consequences

- 战斗首次计入装备：三人初始各有物攻 +4、物防 +3，三场教学战会比此前略容易，需在试玩中观察。
- `DevPartyProgress` 不再自己持有铜钱和军粮，只保留经验这一临时部分（成长系统落地前）。

## Risks

- N04 还没有像素立绘，提示只出现在铺子门前的站位上；需要美术补 NPC。
- 商店界面布局是功能样板，正式布局待 `/ux-design`。

## GDD Requirements Addressed

| GDD | 条目 | 落实 |
|-----|------|------|
| equipment-items | 规则 1–21、验收条目 | Inventory 方法与 EconomyTests |
| game-ui | 商店与配装：比较、出售确认、先卸下 | ShopHud |
| town-exploration | 与 N04 对话打开商店，商店期间移动暂停 | TownStoryController |

## Validation Criteria

- EditMode `EconomyTests` 覆盖 GDD 验收条目（初始状态、买卖价格、已装备不可卖、兵种限制、重复购买、加值相加、快照）。
- PlayMode `ShopTests`：在城中与 N04 对话，买入、拒卖已装备物品、确认出售、更换关羽武器、逐级取消关闭商店并恢复操作，编队装备加值正确。

## 待确认决定

1. 初始装备在新游戏时就放在三人槽中（关张入队即带着），不另设发放事件。
2. 商店界面先用功能样板布局（左列表、右详情、底部提示），正式样式待 UX 规格。
3. 武器槽为空时只在南门提示一次，再次确认即出城。

## Related

- ADR-0002 战斗核心；ADR-0004 存档（待写）；ADR-0006 场景流程与剧情标志
- `docs/registry/architecture.yaml`：`party_inventory`、`inventory_written_outside_owner`
