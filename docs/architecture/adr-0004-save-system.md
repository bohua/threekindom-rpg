# ADR-0004: 存档、战前自动存档与读档

## Status

Proposed（2026-10-20 起草；用户 2026-10-20 要求完成一项后直接继续 Alpha 0.1 开发。用户确认“待确认决定”后改为 Accepted）

## Date

2026-10-20

## Last Verified

2026-10-20（Unity 6000.3.26f1）

## Decision Makers

- 用户 Bohua：save-system GDD 于 2026-10-18 复审通过，7 项待定问题于 2026-10-19 确认采用
- 起草：Claude

## Summary

Alpha 0.1 需要 3 个涿郡手动存档位、1 个滚动战前自动存档位、战败重试还原，以及从标题读档。本 ADR 决定：存档是 `ThreeKingdoms.Save` 里的 JSON 文件（`SaveData`，`JsonUtility`），放在 `Application.persistentDataPath/saves/`；`SaveCodec` 只在 Core 状态与 `SaveData` 之间复制，各状态的所有者（`StoryState.Restore`、`Inventory.Restore`、成长经验）自己校验重建；`SaveSlots` 先写临时文件再替换槽位，读取永不修改或删除文件；战前自动存档在战斗场景开始时写入；读档由标题界面发起，落在酒馆或该战的战前状态。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS（6000.3.26f1） |
| **Domain** | Persistence（文件 IO、JSON）；Gameplay（场景加载）；UI（存档、标题） |
| **Layer** | Feature |
| **Knowledge Risk** | LOW：`JsonUtility`、`File.Replace`、`Application.persistentDataPath` 均为长期稳定 API |
| **References Consulted** | ADR-0001、0002、0006、0008；`design/gdd/save-system.md`、`story-events.md`、`game-ui.md`；V1_SPEC §9、§11 |
| **Post-Cutoff APIs Used** | 无 |
| **Verification Required** | EditMode：字段往返、损坏／版本不支持不改文件、演出不在中途、奖励每进程一次；PlayMode：酒馆存档（覆盖确认）、标题读档回酒馆、战前存档读入对应战斗、主线每战写入自动存档 |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001（`ThreeKingdoms.Save` 只引用 Core）、ADR-0006（`GameRun`、`StoryState.ToSafeState`）、ADR-0008（`Inventory.Clone`／`Restore`） |
| **Enables** | 战前准备界面（确认整备后写自动存档）、标题与返回标题 |
| **Blocks** | 无 |

## Context

此前 `GameRun` 只在内存里，退出即丢失；`StoryState` 已有 `ToSafeState`，`Inventory` 已能 Clone。GDD 要求：存档不含账号数据；读档只回到“城镇安全状态”或“第 N 战战前准备”；损坏或版本不符时提示并保留原文件；防重标志属于当前存档进程；第三战后的测试版结束不写新存档。

## Decision

1. **格式**：`SaveData` 为纯字段类，`JsonUtility` 读写，UTF-8；`formatVersion` 只接受当前值 1（答案 7）。保存 V1_SPEC §9 的全部字段：章节、落点、关张入队／桃园／战斗进度／结尾标志、各将经验、铜钱、军粮、背包、装备归属、游戏版本、存档时间；另存刘备等级仅供存档位摘要（答案 4）。任务奖励标志在 Alpha 0.1 没有支线，暂无字段。
2. **槽位**：`manual_1..N`（N 来自 `BattleCatalog.ManualSaveSlots`，默认 3）与 `auto`。写入先写 `.tmp` 再 `File.Replace`／`Move`，中途崩溃不会留下半个存档。读取返回 Ok／Empty／Damaged／Unsupported，永不改动文件。
3. **捕获与还原**：`SaveCodec.Capture` 先对剧情状态 `ToSafeState()`，所以演出中途永远不会被保存；`TryRestore` 交给 `StoryState.Restore` 与 `Inventory.Restore`，未知物品、未知落点、与进度不符的战前落点（例如第一战未胜却指向第二战，或任何徐州落点）一律视为损坏，不加载。
4. **手动存档**：只在酒馆掌柜 N05 处（答案 1）。`TownStoryController` 的面板机制与商店共用：打开时暂停移动，演出期间无法打开；已有存档的槽位覆盖前确认（答案 2）。
5. **战前自动存档**：主线战斗先进入战前准备（选 1–3 名出战、配装、看敌情），“确认出战”时写入 `auto`，内容就是该战的出发状态。战败不写入任何结果；“重新挑战”回到战前准备，再次确认即形成新的战前状态与新的自动存档（规则 9、11、12）。开发用的直接打开战斗场景不经过准备、不写存档。
6. **读档**：标题界面列出自动存档与 3 个手动存档的摘要；读取成功后重建 `GameRun` 并加载场景：城镇存档落在酒馆掌柜前，战前存档直接进入该战。失败时显示原因并停在标题。
7. **标题与返回**：新增 Title 场景为构建首场景；战败画面增加“返回标题”（答案 5）；测试版结束画面的按钮改为“返回标题”，不写新存档（答案 3）。
8. **测试隔离**：`GameRun.SaveDirectoryOverride` 让测试写入临时目录，永不触碰玩家存档。

## Alternatives Considered

- **二进制或自定义格式**：调试困难，测试版没有防篡改需求。不采用。
- **PlayerPrefs**：Windows 写注册表，不便备份和检查，也不好表示“损坏文件保留原样”。不采用。
- **胜利后自动存档**：GDD 明确没有，本文不加（Edge Cases：胜利后不手动存档就退出，需要重打这一战）。

## Consequences

- 构建首场景变为标题；自动演示启动器改为 `-scene Zhuojun_Town -storyAuto`。
- 战前准备界面同期实现：配装复用商店的装备面板（只换装、不买卖）。

## Risks

- `JsonUtility` 对缺失字段给默认值：缺少 `formatVersion` 的文件会被当作当前版本读取，再由各所有者的校验兜底。
- 存档目录写入失败（权限、磁盘满）时只给出提示，不阻止进入战斗。

## GDD Requirements Addressed

| GDD | 条目 | 落实 |
|-----|------|------|
| save-system | 规则 1–14、验收条目 | SaveSlots／SaveCodec／GameRun；SaveTests、SaveFlowTests、MainLineTests |
| game-ui | 存读档界面、标题、返回标题 | SaveHud、TitleHud、BattleHud |
| story-events | 读档不落在演出中途、不残留操作锁 | Capture 前 ToSafeState；读档后城镇 Start 恢复操作 |

## Validation Criteria

- EditMode `SaveTests`：全部字段往返；损坏、版本不符不改文件；覆盖不留临时文件；中途演出读档后从头播放；早期存档可再领一次奖励且仅一次；错误落点与未知物品被拒；存档不含账号类字段。
- PlayMode `SaveFlowTests`：酒馆存档与覆盖确认、标题读档回酒馆且关羽仍在队伍跟随、损坏槽位提示且文件不变、战前存档直接进入第二战并重写自动存档。
- PlayMode `MainLineTests`：三场战斗各写入一次自动存档，落点与进度正确。

## 待确认决定

1. 存档放在 `%USERPROFILE%\AppData\LocalLow\<公司>\<产品>\saves\`（Unity 的 persistentDataPath），JSON 明文。
2. 战前自动存档在战前准备“确认出战”时写入（2026-10-20 已随准备界面实现）。
3. 标题界面暂用纯色背景与文字标题，正式主视觉待美术。

## Related

- ADR-0006 场景流程与剧情标志；ADR-0008 队伍库存与商店
- `docs/registry/architecture.yaml`：`save_files`
