# ADR-0006: 场景流程与剧情标志

## Status

Accepted（2026-10-20 用户确认“待确认决定”全部按推荐采用；起草说明：2026-10-20 起草；用户 2026-10-20 授权按 CCGS 流程串起首版主线。TD-ADR 评审见文末；用户确认“待确认决定”后改为 Accepted）

## Date

2026-10-20

## Last Verified

2026-10-20（Unity 6000.3.26f1）

## Decision Makers

- 用户 Bohua：2026-10-20 授权串起首版主线——城中与关羽、张飞对话入队，简化版桃园结义，南门出城依次进入三场战斗，打完回城
- 起草：Claude（`/architecture-decision` 流程）
- 评审：`technical-director` TD-ADR（项目 `review_mode` 为 solo；本 ADR 只依赖已 Accepted 的 ADR-0001／0002，引擎面很小，未另跑引擎专家）

## Summary

首版主线要把涿郡城镇、桃园演出与三场战斗串成一条流程，需要定下剧情标志放在哪里、场景之间如何传递进度、演出期间如何锁定操作。本 ADR 决定：剧情标志与主线阶段是 `ThreeKingdoms.Core.Story` 里的纯 C# 状态机（`StoryState`），与战斗外的进度一起放在一次游戏进程对象 `GameRun` 中，跨场景保留；城镇用 `TownStoryController` 驱动对话、入队、桃园演出与南门出城；战斗场景从 `GameRun` 取进度、胜利后写入完成标志并回到南门内侧。存档持久化仍由 ADR-0004 决定。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS（6000.3.26f1） |
| **Domain** | Core / Scripting（标志与阶段）；Gameplay（场景切换、输入锁） |
| **Layer** | Feature |
| **Knowledge Risk** | LOW：标志与阶段为纯 C#；场景切换用 `SceneManager.LoadScene`，UI 沿用 ADR-0002 的 UI Toolkit 做法 |
| **References Consulted** | ADR-0001、ADR-0002；`design/gdd/story-events.md`（含 2026-10-19 确认的 11 项答案）；V1_SPEC §2、§14；ZHUOJUN_TOWN_DESIGN.md §3–§5 |
| **Post-Cutoff APIs Used** | 无新增（UI Toolkit、Input System 用法同 ADR-0002） |
| **Verification Required** | PlayMode：从新游戏到第三战胜利的整条主线自动走通；读取任一阶段的状态都不残留操作锁 |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001（Accepted）、ADR-0002（Accepted：战斗只交出 `BattleResult`，完成标志归剧情系统） |
| **Enables** | ADR-0004 存档（持久化 `StoryState` 与进度）；教学提示、NPC 状态对白、商店整备 |
| **Blocks** | 首版主线、Alpha 0.1 完整流程验收 |
| **Ordering Note** | 存档未实现前，`GameRun` 只存在于一次运行的内存中 |

## Context

### Problem Statement

城镇、桃园演出与三场战斗目前是互不相连的场景。story-events GDD 已定义标志（关张入队、结义三态、三战完成、结尾三态）、阶段（寻找同伴 → 桃园演出 → 出城迎敌 → 第 N 战 → 结尾）与 11 项已确认答案，需要决定这些状态归谁、如何跨场景、演出如何锁定操作。

### Constraints

- ADR-0002：战斗不改写战斗外状态，只交出 `BattleResult`；完成标志归剧情系统。
- 规则可测：主线条件（何时触发结义、能否出城、下一战是哪一场）要能在 EditMode 测试。
- 尚无存档、标题画面；Alpha 0.1 结尾为「测试版结束」画面。

## Decision

### 1. `StoryState`（Core，纯 C#）

- 字段：`GuanYuJoined`、`ZhangFeiJoined`、`Oath`、`BattlesDone`（0–3）、`Ending`。`Oath` 与 `Ending` 用通用三态 `SequenceState`（未开始／播放中／已完成）。GDD 的 `battle_N_done` 由 `BattlesDone` 表示（`battle_N_done` ⇔ `BattlesDone ≥ N`）。
- 派生：`Phase`（寻找同伴／桃园演出／出城迎敌／结尾）、`NextBattleId`（`battle_1`…`battle_3`，与 ADR-0002 遭遇 ID 一致）、`Objective`（目标提示）。
- 规则方法：
  - `Join(id)` 返回“现在是否该自动开始结义”（GDD 规则 7：两人都入队且结义未开始）。
  - `BeginOath()` 占用触发，`CompleteOath()` 只改标志。
  - `ToSafeState()` 把“播放中”的结义／结尾退回“未开始”。城镇每次加载都会调用，ADR-0004 存档前也会调用（GDD 规则 11：中途退出后从头重播）。
  - `CanLeaveTown(out reason)`：结义前给出明确原因（GDD 规则 12）。
  - `RecordVictory(encounterId)`：只记录与 `NextBattleId` 相符的胜利。**这是唯一的“每场只发一次奖励”关口**：返回 true 才发放经验与铜钱（GDD 规则 17）。
- 队伍名单由入队标志推出（`HasJoined`）：战斗的出战名单只包含已入队的武将。入队时的等级（1 级 0 经验）与基础装备（GDD 规则 5）由成长、装备系统读取同一组标志，不另发事件。
- 不持有引擎对象，可直接序列化给 ADR-0004。

### 2. `GameRun`（Gameplay）：一次游戏进程

- 持有 `StoryState`、`DevPartyProgress` 与 `BattleData`。用静态引用跨场景保留；`GameRun.NewGame()` 重置，`SubsystemRegistration` 时清空。存档实现后由 ADR-0004 读写，`GameRun` 只是运行时容器。只有 Gameplay／App 层读取它，Core 不读。
- **扩展 ADR-0002 的临时例外**：`DevPartyProgress` 继续暂代成长与装备系统入账，但不再持有自己的完成标志与跨场景静态副本；它只按 `RecordVictory` 的结果发奖。ADR-0002 评审记录已注明此项。
- 直接打开某个战斗场景（开发用）时，按该战的预期状态建一个“开发进程”（`GameRun.ForBattle`，`StoryState.AtBattle`）。胜利后提供“下一战”而不是回城；正式进程永远从城镇开始。

### 3. 城镇：`TownStoryController`

- 交互：玩家靠近关羽、张飞或南门门洞时显示提示，按确认（Input Actions `Interact`）对话。对话与演出期间锁定移动（`PlayerMover.InputEnabled = false`），也不接受新的交互（`Busy`）；确认键逐句推进，锁在 `finally` 里释放。首版没有队伍菜单；系统菜单键仍可退出游戏（GDD 答案 9）。
- 入队：对话结束才写入入队标志（GDD 边界条件）；入队的同伴离开原站位。最后一人入队后立即自动开始桃园演出。
- 桃园演出（简化版）：淡出 → 刘关张站到西门外桃林 → 镜头跟随桃林 → 数句对白（确认键推进，不可跳过）→ 淡出 → 刘备回到西门内侧（GDD 答案 7），恢复控制，目标改为“出城迎敌”。
- 南门：玩家进入门洞区域时，结义前给出原因；结义后提示“按确认出城：第 N 战”，确认后切到对应战斗场景。
- 落点：新游戏在南门外；从战斗回城落在南门内侧（GDD 答案 3）。

### 4. 战斗衔接

- 战斗场景从 `GameRun` 取队伍进度；胜利后 `GameRun.Story.RecordVictory`，结算面板显示“回到涿郡”（前两战）或“继续”（第三战 → 结尾）。战败仍是重新挑战。
- 第三战胜利：播放一两段旁白（GDD 答案 5），显示「测试版结束」，可“重新开始”回到新游戏。GDD 规则 19 写的是“返回标题”，但标题画面要随 ADR-0004 一起做，这里暂以“重新开始”代替。

### 5. 文字

- 对白与提示集中在 UI 层的 `StoryText`，简体中文。GDD 写明对白【待编写】，本 ADR 的实现用标注为“暂定”的短句占位，正式对白另由剧情编写替换。

### Key Interfaces

```csharp
namespace ThreeKingdoms.Core.Story
{
    public enum SequenceState { NotStarted, Playing, Done }
    public enum StoryPhase { FindCompanions, Oath, LeaveTown, Ending }
    public sealed class StoryState
    {
        public bool GuanYuJoined { get; }
        public bool ZhangFeiJoined { get; }
        public SequenceState Oath { get; }
        public int BattlesDone { get; }
        public SequenceState Ending { get; }
        public StoryPhase Phase { get; }
        public string NextBattleId { get; }
        public bool Join(string generalId);          // true → start the oath now
        public bool BeginOath();
        public void CompleteOath();
        public void ToSafeState();
        public bool HasJoined(string generalId);
        public bool CanLeaveTown(out string reasonKey);
        public bool RecordVictory(string encounterId);
    }
}
```

### Implementation Guidelines

- 主线条件**必须**由 `StoryState` 判断，场景脚本**绝不能**自行拼条件。
- 入队标志**必须**在对话结束时写入；结义**必须**先 `BeginOath` 占用，结束只 `CompleteOath`。
- 演出与对话期间**必须**锁定移动与交互，结束时**必须**解锁（包括异常退出路径）。
- 战斗**绝不能**直接改 `StoryState` 以外的战斗外状态（ADR-0002）。

## Alternatives Considered

### Alternative 1：标志放在 ScriptableObject 运行时实例

- **Rejection Reason**：编辑器里 SO 改动会写回资产，跨测试残留；也无法放进不引用引擎的 Core。

### Alternative 2：常驻管理场景（Additive bootstrap scene）

- **Pros**：生命周期清楚。
- **Cons**：首版只有 4 个场景，需额外场景加载顺序；测试也要多一步。
- **Rejection Reason**：首版规模不需要；ADR-0004 引入标题与读档时再评估。

### Alternative 3：通用事件／条件脚本系统（如 Yarn 变量驱动）

- **Rejection Reason**：首版只有十来个标志，强类型状态机更易测；Yarn 对白接入在 ADR-0007 时再定。

## Consequences

### Positive

- 主线条件全部可在 EditMode 测试；场景只负责表现。
- 存档只需序列化 `StoryState` 与进度。

### Negative

- 静态 `GameRun` 是全局状态，测试需显式 `NewGame()`；Enter Play Mode 关闭域重载时要在启动时重置。
- 演出是脚本化的协程，正式镜头与动作需要时要另做时间线。

## Risks

- **静态状态残留**：缓解——`GameRun` 在 `RuntimeInitializeOnLoadMethod(SubsystemRegistration)` 时清空。
- **对白占位被当成正式文本**：缓解——`StoryText` 注明“暂定”，PR 列出待编写项。

## GDD Requirements Addressed

| GDD | 要求 | 处理 |
| --- | --- | --- |
| story-events.md 规则 4–6、答案 1–2 | 关张顺序自由、无前置，对话后入队 | §3 |
| story-events.md 规则 7–11、答案 7、9 | 自动结义三态、锁定操作、西门内侧恢复、不可跳过 | §1、§3 |
| story-events.md 规则 12–15、答案 3 | 南门给原因、按顺序三战、胜利回南门内侧 | §1、§3、§4 |
| story-events.md 规则 17 | 每场只发一次奖励 | ADR-0002 + `RecordVictory` |
| story-events.md 规则 19、答案 5–6 | 测试版简短旁白后「测试版结束」 | §4 |
| story-events.md 规则 5 | 入队即 1 级、带基础装备 | §1 名单由入队标志推出；装备随装备系统落地 |
| story-events.md 规则 1 | 开场门卫一句话、镜头扫过市集与郡署 | 延后：开场演出故事（门卫尚无像素图） |
| story-events.md 规则 3、答案 11 | NPC 按剧情阶段切换状态对白 | 延后：居民 NPC 故事 |
| story-events.md 答案 4 | 可选的商店提示 | 延后：商店故事 |
| story-events.md 答案 9 | 演出中可开系统菜单、不可跳过 | §3 |
| story-events.md 规则 18、答案 8 | 教学提示 | 延后：另做教学提示故事 |
| save-system.md | 标志持久化、安全落点 | 延后到 ADR-0004 |

## Validation Criteria

- [ ] EditMode：`StoryState` 覆盖顺序自由入队、最后一人触发结义、结义不重复、结义前不能出城、按顺序记录三战、第三战进入结尾
- [ ] PlayMode：新游戏 → 与关羽、张飞对话 → 结义演出 → 南门出城 → 三场战斗（自动下令）→ 回城 → 结尾，全程无残留操作锁
- [ ] IL2CPP 构建：默认从涿郡南门外开始

## 已确认决定（用户 2026-10-20：都按推荐）

| # | 问题 | 推荐 |
| --- | --- | --- |
| 1 | 标志与阶段位置 | Core 纯 C# `StoryState` |
| 2 | 跨场景保留 | 静态 `GameRun`（存档前的运行时容器） |
| 3 | 桃园演出 | 同一城镇场景内的脚本化简化演出 |
| 4 | 构建入口 | 默认从涿郡南门外开始新游戏 |

## Related

- ADR-0001、ADR-0002（依赖）；ADR-0004（存档）、ADR-0007（对白与本地化）
- `design/gdd/story-events.md`、`save-system.md`、`town-exploration.md`；V1_SPEC §2、§14

## 评审记录（2026-10-20）

`technical-director` TD-ADR 结论 **CONCERNS**（4 项阻断、5 项建议），没有 REJECT。全部已处理：

| 意见 | 处理 |
| --- | --- |
| 阻断 1：完成标志与“只发一次奖励”有两个所有者 | `RecordVictory` 是唯一关口；`DevPartyProgress` 删去自己的完成集合与静态 `Carried`；§2 写明扩展 ADR-0002 的临时例外 |
| 阻断 2：结义中途退出会卡死进度 | 新增 `ToSafeState()`，城镇加载时调用；EditMode 测试覆盖 |
| 阻断 3：队伍名单的所有者不明 | §1：名单由入队标志推出；`Lineup(HasJoined)` |
| 阻断 4：登记表未更新 | 新增 `story_state` 状态归属、修订 `battle_clear_flags` 接口、新增禁止模式 `scene_composes_story_conditions` 与 `game_run_static_in_core` |
| 建议 5：输入锁不完整 | §3：移动锁 + `Busy` 拒绝交互；首版没有队伍菜单；系统菜单按答案 9 保留 |
| 建议 6：GDD 覆盖表缺项 | 表中补规则 1、3、5，答案 4、9，并写明“重新开始”与标题画面的偏差 |
| 建议 7：`Ending` 复用 `OathState` | 改为通用 `SequenceState`；“第 N 战”目标由 `NextBattleId` 推出 |
| 建议 8：开发入口进入发行构建 | 保留：用户需要从 .bat 直接试玩单场战斗；开发进程胜利后只提供“下一战”，不会进入城镇主线 |
| 建议 9：测试 | EditMode：重复入队、乱序／重复胜利、出城原因、中断恢复、结尾；PlayMode 整条主线用固定自动下令 |
