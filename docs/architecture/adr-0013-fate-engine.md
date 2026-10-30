# ADR-0013: 命运引擎（人物记忆、态度与去向结算）

## Status

Accepted（v2，2026-10-27 按 TD-ADR 意见修订；用户 Bohua 2026-10-28 21:24 回复“继续”确认进入实装。Verification Required ④ 对 ADR-0004 的核对在 P0b 开工前完成，不阻塞 P0a）

> 只有用户，或在用户明确确认后由 `technical-director`，可以把本 ADR 改为 `Accepted`。
> 编号：原稿为 ADR-0012。2026-10-29 合入仓库时核对：ADR-0011 已为音乐管线（feature/music-pipeline），ADR-0012 已为 Blender 模型管线（feature/blender-pilot 等分支），故顺延为 **ADR-0013**，规格 v5.1 的引用同步改为 0013。

## Date

2026-10-27

## Last Verified

2026-10-28（ADR-0004 的分段／版本／原子替换语义在云端无副本，**待在仓库内核对**，见 Verification Required ④）

## Decision Makers

- 用户 Bohua（2026-10-28 确认）
- 起草：Claude（线程「细化 AI 武将命运系统规格」）
- 评审：`technical-director`（TD-ADR 门，2026-10-27：CONCERNS，A-01～A-07 已全部按意见修订进本稿）

## Summary

“AI 武将命运系统”需要一个在游戏运行时**完全离线、确定性**的结算模块，在进入每个主线节点时根据记忆与态度重算人物的去向，并保证史实锚点与事件结果不变。本 ADR 决定：命运引擎作为 `ThreeKingdoms.Core` 内不依赖 Unity 场景的纯 C# 定点数模块实现；命运数据与文本库采用 JSON 文本资产（TextAsset）并由构建期校验器把关；存档以整份文件为事务单位原子提交，新增带版本的 `FateSaveSection` 与跨篇章 `ChronicleRecord`；大模型只出现在开发期内容管线，运行时没有任何模型接口。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS（6000.3.26f1，沿用 ADR-0001） |
| **Domain** | Core / Scripting；附带 Persistence（存档）与 Narrative（Yarn 函数） |
| **Layer** | Feature（依赖 Foundation 层 ADR-0001、ADR-0004、ADR-0006） |
| **Knowledge Risk** | LOW — 引擎本体是纯 C# 整数运算，不用 Unity 运行时 API；MEDIUM — IL2CPP 托管代码剥离与 Newtonsoft 反序列化的交互需验证 |
| **References Consulted** | ADR-0001（程序集划分、IL2CPP 发行、`link.xml`、`IsExternalInit`）；`design/ai-character-fate/AI_CHARACTER_FATE_SPEC_v5.md`（v5.1）；`design/bingshu-system.md` §3.7、§3.11；`design/v0.2-huangjin/SYSTEMS_V02.md` §3；`design/game-blueprint.md` §5.6–5.7 |
| **Post-Cutoff APIs Used** | None |
| **Platform note** | Steam Deck 经 Proton 运行同一份 IL2CPP Windows 二进制，不构成额外的确定性平台；确定性只需在 Mono（开发）与 IL2CPP（发行）两种构建间验证 |
| **Verification Required** | ① IL2CPP Win64 发行构建下，JSON 加载的命运数据 DTO 未被剥离（`link.xml` 覆盖 `ThreeKingdoms.Data.Fate`），同种子 1,000 次结算与 Mono 构建哈希一致（随 0.5 冒烟构建跑一次）；② 无 I/O 结算 120 人合成档案 ≤5 ms，含整档写入 ≤500 ms（Steam Deck 实测）；③ 0.2 记忆最小版存档迁移后 `memory(who,key)` 查询结果不变；④ 对照仓库内 ADR-0004 核实存档分段、版本号与自动档替换语义，与本 ADR Decision 3 一致 |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001（程序集与构建配置；本 ADR 对其依赖表提出一条修订，见 Decision 2）；ADR-0004（存档格式）；ADR-0006（场景流程与剧情标志，条件谓词 `flag` 的来源） |
| **Enables** | 全员收集／人物去向页、历史剧情点在场判定、开放区域人物分布表、兵书“名士拜师／敌将缴获”途径、跨篇章共同终章 |
| **Blocks** | P0a 引擎核心的全部故事；0.4 河北的第一个故事（P0b 集成）；0.3 以后任何“人物命运重算”相关故事 |
| **Ordering Note** | 必须在 P0a 开工前 Accepted；P0a 与 0.3 并行且不碰 Save／节点状态机／InteractionRecorder；0.2 记忆迁移随 0.4 的存档版本升级一次完成（不在 0.3 期间单独升版） |

## Context

### Problem Statement

蓝图支柱“人物记得你、命运因你而变”需要一个系统在每个主线节点重算约 100 名人物的去向。用户已决定：史实大事不可改、只能通过救人条件让人不死；运行时离线；大模型只在开发期用。v4 设计稿仍混有运行时调模型的架构，且数据格式、确定性、存档事务、跨篇章读取都没有工程层面的定义。不先定这些，P0 原型会把临时结构固化，之后每个篇章的数据都要返工。

### Current State

- 0.2 已实装“记忆最小版”：`who/key/value/node/text`，同 who+key 覆盖，Yarn 条件 `memory(who,key)==value`，存在当前存档的剧情状态里。
- 存档：ADR-0004（3 手动档＋自动档，版本号随档保存）已 Accepted；剧情标志：ADR-0006 已 Accepted。两者正文在用户电脑仓库，云端无副本。
- 数据资产：V1 的人物、技能、装备用 ScriptableObject。
- 已装包：Newtonsoft Json 3.2.2；ADR-0001 目前只让 `Save` 引用它。
- 0.3 讨董正在修改年份节点状态机与天下图节点存档。

### Constraints

- 一人开发＋AI 代理；没有后端运维能力；游戏单机。
- 同输入同输出：读档不重抽、Mono／IL2CPP 一致。
- 文本库由开发期管线生成并全审；运行时只查表。
- 与 0.2 记忆最小版向后兼容（存档迁移，不改 Yarn 查询函数签名）。
- 兵书系统字段必须沿用。
- .NET Standard 2.1：`init` 访问器需手动声明 `IsExternalInit`（ADR-0001 已提）。

### Requirements

- 结算可在 EditMode 测试中无头跑一整篇章（P0a 1,000 次、P0.5 起 10,000 次）。
- 候选、锚点、救人条件、文本库在构建期校验；校验失败即构建失败。
- 存档内命运状态单独成段、带版本；提交以整份存档为事务单位；跨篇章通关记录可被下一篇章与共同终章读取。
- 文本库缺条目时退到模板，游戏完整可玩。

## Decision

### 1. 命运引擎是 `ThreeKingdoms.Core` 内的纯 C# 定点数模块

- 命名空间 `ThreeKingdoms.Core.Fate`；`noEngineReferences` 不变。
- 入口是纯函数 `FateEngine.Settle(...)`（签名见 Key Interfaces）。它不读全局状态、不做 I/O、不抛出到调用方之外的异常（数据错误以 `SettleResult.Errors` 返回并回退默认路线）。
- 引擎内部禁止 `float`/`double` 参与影响结果的计算、禁止 `UnityEngine.Random` 与 `System.Random`、禁止依赖 `Dictionary`/`HashSet` 枚举顺序。使用 Q16.16 定点数（`long` 中间值）、PCG32（标准参数）、SplitMix64 finalizer、FNV-1a 64（UTF-8）与随包发布的 `EXP_TABLE`；随机流按规格 §5.3 的流表派生。
- 态度即时更新（规格 §5.1）同样在 Core 内，`ApplyInteraction` 返回增量，不修改入参。

### 2. 命运数据与文本库是 JSON 文本资产（TextAsset），不是 ScriptableObject

- 路径：`Assets/_Project/Data/Fate/<volume>/{profiles,routes,rescues,history_events,random_events,world_slots,nodes}.json`、`Assets/_Project/Data/Fate/text_library/<volume>.json`、`Assets/_Project/Data/Fate/{tuning,exp_table,fate_migrations}.json`、`Assets/_Project/Data/Fate/chronicle_defaults/<volume>.json`。发行形态为 **TextAsset 场景引用**（不用 StreamingAssets，避免发行目录可被篡改且 IL2CPP 下路径差异），V1 已定“场景直接引用”，章节增多后随 Addressables ADR 一并迁移。
- 理由：这些数据由工具生成与校验（内容管线、可达性报告、模拟器），需要 diff 友好、可在 Unity 外处理；ScriptableObject 仍用于 V1 既有的人物／技能／装备资产，`CharacterProfile.id` 与既有人物资产 id 由校验器 V-01 对账。
- 加载：`ThreeKingdoms.Data.Fate` 用 Newtonsoft 反序列化为 **`[Serializable]` 公有字段 DTO**（遵守 ADR-0001 的 Core 数据类型约定，不用 `init` 记录），再包成只读视图 `FateDataSet` 交给 Core；发行构建用 `link.xml` 保留 DTO 类型；谓词分派用 `switch`。
- **对 ADR-0001 依赖表的修订条款**：`Data ──► Core（另加 Newtonsoft）`。原表只让 `Save` 引用 Newtonsoft；本 ADR 被 Accepted 时同步在 ADR-0001 的程序集依赖图加这一行并注明“由 ADR-0013 修订”。替代方案“把 JSON 加载放进 `Save`”因语义错位（`Save` 不应持有只读设计数据）被否决。
- 所有 JSON 带 `fateDataVersion`；参与运算的数值一律整数（Q16.16 或千分数）。

### 3. 存档：整档事务、`FateSaveSection` 与 `ChronicleRecord`

- `FateSaveSection` 作为 ADR-0004 存档文件的独立分段，自带 `schemaVersion`。
- **事务单位是整份 `SaveDocument`**（命运段、库存、队伍、剧情标志一起）：第⑦步的退回物品、`itemTransfer`、离队与命运状态在同一次写入里。写入：临时文件 → `FileStream.Flush(true)` → `File.Replace`（Windows `ReplaceFile`）替换自动档。
- 进入节点先原子写“日志冻结 ＋ `pendingTargetNode` ＋ `pendingRecompute=true`”，再结算；读档见该标记从第①步整体重跑（Settle 幂等）。
- `ChronicleRecord` 存在 `Application.persistentDataPath/chronicle/<line>/<volume>/<runId>.json`；每线导出所有触碰过的人物；组装**按字段分主**（去向类取 `ownerLine`，`relations[protagonistX]` 取 X 线）；无记录时用随包的默认终局记录；终章开档时把所选记录整份复制进终章存档；记录读取经 `fate_migrations.json`。
- 0.2 记忆最小版的迁移由 `ThreeKingdoms.Save` 在读档时完成，一次性、不改原文件，随 0.4 存档升版一起发布。

### 4. 运行时没有模型接口

- 不定义 `ILlmClient`、`IFateResolver` 等抽象；P2 若立项，用新 ADR 增加 `IOnlineAdviser` 覆盖第④步“拟选”，并复用第⑤–⑦步校验。
- 大模型只出现在 `tools/fate-pipeline`（仓库 `tools/` 下的独立命令行）与 `ThreeKingdoms.EditorTools.Fate` 的导出／导入，不进入运行时程序集。

### 5. 与叙事层的接口

- `ThreeKingdoms.Narrative` 通过 `IFateQuery` 只读取数；Yarn 函数：`memory(who,key)`（保留）、`favor(who)`、`trust(who)`、`view(who,dim)`、`fate_location(who)`、`fate_faction(who)`、`in_party(who)`、`rescued(caseId)`。P0b 先接 `memory/favor/trust/in_party/rescued`。
- 剧情写入人物状态只有三种途径，且都是**日志事件**：写记忆（`InteractionRecorder`）、脚本化加入／离队（`recruit` 事件，`scripted:true`）、救人结果（`RescueDirector`）。其余字段剧情不可写。

### Architecture Diagram

```
                 ┌──────────────── 开发期（不进包） ─────────────────┐
                 │ tools/fate-pipeline: 导出批次 → 模型生成 → lint   │
                 │ → 人工审校工具 → text_library/<volume>.json       │
                 │ EditorTools.Fate: 校验器 · 模拟器＋可达性报告      │
                 │ · 命运面板（EditorWindow 表格＋JSON 导出）          │
                 └───────────────────────┬──────────────────────────┘
                                         │ JSON TextAsset（CI 校验通过）
┌──────────────┐   FateDataSet   ┌───────▼────────┐   SettleResult   ┌──────────────────┐
│ Data.Fate    │────────────────►│ Core.Fate      │─────────────────►│ Gameplay         │
│ JSON→DTO→只读│                 │ FateEngine     │                  │ StageDirector    │
└──────────────┘                 │ Attitude/Cond  │◄─StageEventLog───│ RescueDirector   │
                                 │ FixedMath/PCG  │  + IConditionCtx │ InteractionRec.  │
                                 │ IFateQuery     │                  └────────┬─────────┘
                                 └───────┬────────┘                           │ 事件
                                         │ SaveDocument（整档）         ┌────────▼─────────┐
                                 ┌───────▼────────┐                  │ Narrative        │
                                 │ Save           │                  │ Yarn 只读函数    │
                                 │ 原子提交·迁移   │                  │ (IFateQuery)     │
                                 │ ChronicleStore │                  │ TextLibrary 查表 │
                                 └────────────────┘                  └──────────────────┘
```

### Key Interfaces

```csharp
namespace ThreeKingdoms.Core.Fate
{
    public static class FateEngine
    {
        // 纯函数：不修改入参；data 为只读视图；ctx 由 Gameplay 组装（剧情标志、库存、声望、战斗目标）
        public static SettleResult Settle(FateDataSet data, SaveDocument state, StageEventLog log,
                                          string nodeId, IConditionContext ctx);
        public static AttitudeDelta ApplyInteraction(CharacterProfileView p, RelationView current,
                                                     MemoryEntry m, TuningTable t);   // 返回增量，不改入参
    }
    public sealed class SettleResult
    {
        public FateSaveSection NewFateSection;
        public IReadOnlyList<FateChange> Changes;          // 退回物品、itemTransfer、recruitState、aptitudeChange…由 Gameplay 在同一事务内应用
        public IReadOnlyList<PendingTransition> Announced; // 需要触发预警对白的两步提交
        public IReadOnlyList<string> Errors;
    }
    public sealed class FateChange { public string Kind; public string CharacterId; public string ItemId; public string From; public string To; }
    public sealed class PendingTransition { public string CharacterId; public string OptionId; public string Cause; public string AnnouncedStage; public string CommitStage; public string WarningSceneId; }
    public interface IConditionContext
    {
        bool Flag(string name); string FlagValue(string name);
        bool BattleObjective(string id);   // 仅 fulfillment 段
        bool HasItem(string itemId); int Reputation { get; }
    }
    public static class ConditionEvaluator { public static bool Eval(Condition c, FateSaveSection s, IConditionContext ctx); }
    public interface IFateQuery   // Narrative 只读取数
    {
        string Memory(string who, string key); int Favor(string who); int Trust(string who); int View(string who, string dim);
        string Location(string who); string Faction(string who); bool InParty(string who); bool Rescued(string caseId);
    }
    public sealed class StageEventLog { public IReadOnlyList<StageEvent> Events; }
    public sealed class StageEvent { public long Seq; public string Type; /* memory|recruit|gift|battle|flag|rescue */ public bool Scripted; public string Json; }
}
namespace ThreeKingdoms.Narrative
{
    public struct TextQuery { public string Slot, CharacterId, OptionId, StageGroup, FavorBand, ViewDim, ProtagonistId; public ulong Seed; }
    public interface ITextLibrary { TextEntry Lookup(TextQuery q); }   // 回退链见规格 §5.7；缺键返回 null，调用方退模板
}
namespace ThreeKingdoms.Save
{
    public interface ISaveStore { SaveDocument Load(SaveSlot slot); void CommitAtomic(SaveSlot slot, SaveDocument doc); void MarkPendingRecompute(SaveSlot slot, string targetNode); }
    public interface IChronicleStore { void Write(ChronicleRecord r); IReadOnlyList<ChronicleRecord> List(string line, string volume); ChronicleRecord Default(string volume); }
}
```

### Implementation Guidelines

- `Core.Fate` 必须不引用 `UnityEngine`；必须不使用浮点参与结果；乘除中间值必须用 `long`；必须按 `string.CompareOrdinal` 遍历。
- `Data.Fate` 的 DTO 必须是 `[Serializable]` 公有字段类；必须在启动时做轻量校验（V-01、V-02），失败时拒绝进入新游戏并显示数据错误（开发构建）。
- 剧情脚本必须不直接改 `CharacterState`；只能发日志事件（Decision 5）。
- 救人判定必须在场景加载前求 `eligibility`、场景结束时求 `fulfillment`；`battleObjective` 必须不出现在其他条件里。
- `TextLibrary.Lookup` 缺键必须返回 null 而不是抛异常；调用方必须有模板路径。
- 存档提交必须以整份 `SaveDocument` 为单位原子完成；`pendingRecompute` 必须在提交成功后才清。
- 内容管线必须把 `approved` 作为进包的唯一状态；生成文本必须经过 lint L1–L10。

## Alternatives Considered

### Alternative 1: 每个人物一棵行为树／效用 AI，在阶段内持续运行
- **Description**: 让人物在开放世界里实时决策。
- **Pros**: 更“活”。
- **Cons**: 阶段内世界不稳定、不可测、支线会被打断；与“年份只在节点跳”冲突。
- **Rejection Reason**: 违反已定时间模型与可测试性要求。

### Alternative 2: 运行时本地小模型做裁决
- **Description**: 打包本地模型在结算时选候选、写心声。
- **Pros**: 每周目文本不同。
- **Cons**: 4–8 GB 显存、数 GB 安装包、中文与史实准确度不稳定、内容不能事前审核、不可复现。
- **Rejection Reason**: 用户已决定运行时离线且文本全审；不可复现与“读档不重抽”冲突。

### Alternative 3: 纯脚本分支（《曹操传》式逐人手写）
- **Description**: 每个人物每阶段用剧情标志手写去向。
- **Pros**: 完全可控。
- **Cons**: 组合随篇章爆炸；无法保证“偏离需因”“可达性”这类全局性质。
- **Rejection Reason**: 内容量不可控，且无法表达支柱“命运因你而变”。

### Alternative 4: 命运数据用 ScriptableObject；或 JSON 加载放进 `Save` 程序集
- **Description**: 与 V1 人物资产一样用 SO；或为了不改 ADR-0001 依赖表而让 `Save` 负责加载。
- **Pros**: Inspector 可编辑；不动依赖表。
- **Cons**: SO 不便于工具批量生成与校验、diff 不友好；`Save` 持有只读设计数据语义错位。
- **Rejection Reason**: 这部分数据的主要生产者是工具与管线；修订 ADR-0001 一行依赖比错位的职责更便宜。

## Consequences

### Positive
- 引擎可在 EditMode 与 CI 里跑整篇章模拟；确定性有硬规范（定点、`long` 中间值、随机流表）。
- 内容风险前移到开发期：校验器、可达性报告、lint 在构建期拦截。
- 0.2 记忆最小版平滑升级；Yarn 查询签名不变。
- 不引入后端、网络、合规负担。

### Negative
- 候选、锚点、救人数据的撰写量仍大（P0 实测工时后定配额）。
- 定点数与自定义随机数增加少量实现成本和单元测试量。
- 文本来自固定库，多周目新鲜感有限。
- JSON 与 SO 两套数据资产并存，需要 V-01 对账；ADR-0001 依赖表要改一行。

## Performance Implications

| 项 | Before | After | Budget |
| --- | --- | --- | --- |
| 进入主线节点 | 仅节点状态机切换与自动档写入 | 加一次结算（120 人、纯整数）＋整档写入 | 无 I/O 结算 ≤5 ms；含整档写入 ≤500 ms（上限 2 s） |
| CI 回归 | — | P0a 1,000 次 ×2 策略；P0.5 起 10,000 次 ×4 策略，不写存档 | 单次结算 ≤5 ms → 10,000×8 次 ≤ 7 分钟 |
| 存档体积 | 0.2 记忆最小版 ≈ 数十 KB | 120 人 ×40 条 ≈1.2 MB 原始 | 压缩后 ≤300 KB |
| 启动加载 | — | 一次性反序列化全部命运 JSON（每篇章约 1–3 MB 文本） | ≤200 ms，Boot 场景异步 |
| 内存 | — | `FateDataSet` 常驻 | ≤10 MB |

## Migration Plan

1. **数据**：P0a 以合成测试数据建立 `Assets/_Project/Data/Fate/test/`；P0.5 填第一篇章。
2. **存档**：0.4 存档版本 +1 时一次迁移——旧记忆按写入表转 `MemoryEntry`（`seen` 类 importance 2）、`relations` 从 `initialRelations` 初始化、`fateSeed` 由存档创建时间派生；迁移后另存新版本，不改原文件（沿用 0.2 §5 做法）。
3. **0.3 期间**：不升存档版本；0.3 新写入点在写入表里补 `kind/importance/tags` 三列（纯数据）。
4. **ADR-0001**：Accepted 时同步在其依赖图加 `Data ──► Core（另加 Newtonsoft）`，并注明由本 ADR 修订。
5. **回滚**：P0b 的所有运行时接入点在一个开关 `FateFeature.Enabled` 后；关闭时 `StageDirector` 跳过结算、`InteractionRecorder` 退回 0.2 行为、存档段保留但不读。回滚不丢 0.2 记忆（迁移保留原字段）。
6. **ChronicleRecord**：0.5 篇章结算故事上线时才开始写；之前的存档没有记录，用默认终局记录。

## Validation Criteria

- [ ] `Core.Fate` 程序集无 `UnityEngine` 引用；静态检查无 `float`/`double`/`System.Random`
- [ ] 同种子 1,000 次结算，Mono 与 IL2CPP 构建哈希一致（0.5 冒烟）
- [ ] 无 I/O 结算 120 人合成档案 ≤5 ms；含整档写入 ≤500 ms（Steam Deck）
- [ ] 0.2 存档迁移后，0.2 写入表 10 条记忆的 `memory(who,key)` 查询结果逐条相同
- [ ] 强退注入：在 ⓪ 之后、⑦ 之前任意点杀进程，重启后结算结果与不中断时逐字段相同，库存与队伍无重复或丢失
- [ ] 校验器 V-01～V-06、V-08 在故意损坏的数据上全部报错并使构建失败
- [ ] 模拟器可达性报告在缺一条离队剧情的数据上列出该缺口
- [ ] 删空文本库后 P0b 的两次结算可完整进行并显示模板文本
- [ ] ADR-0001 依赖表已加修订行

## Risks

| 风险 | 概率 | 影响 | 缓解 |
| --- | --- | --- | --- |
| IL2CPP 剥离导致 JSON DTO 反序列化失败 | 中 | 高 | `link.xml` 覆盖 `Data.Fate`；每里程碑 IL2CPP 冒烟构建 |
| 定点运算中间值溢出（如 softmax 索引） | 中 | 高（稳定的错数，测不出不一致） | 规格 §5.3 规定 `long` 中间值与索引公式；单元测试覆盖极端分差 |
| CI 模拟时间超预算 | 中 | 中 | 单次结算 ≤5 ms 预算；模拟不写存档；P0a 只跑 1,000 次 |
| 可达性报告把太多人物标为不可招募 | 中 | 中 | 报告列出每个缺口（缺候选／离队剧情／标志），策划补数据而非放宽规则 |
| “偏离需因”让世界过于安静 | 中 | 中 | `randomDeviationRate` 与带因 HISTORY 事件由数据调；P0b 试玩验证 |
| 整档事务让自动档写入变慢 | 低 | 低 | 压缩＋异步写临时文件，`File.Replace` 本身毫秒级 |
| ADR-0004 实际语义与本 ADR 假设不符 | 低 | 中 | Verification ④ 在仓库内核对后再 Accepted |
| 存档体积随人物增长 | 低 | 低 | 压缩规则与 `memoryCap` |

## GDD Requirements Addressed

- 规格 v5.1 §2（原则 1–6）、§3（时间模型）、§5（结算与确定性）、§7（存档与跨篇章）、§8（校验与测试）、§9（模块与接口）。
- 蓝图 §5.7 对 AI 系统的四条约束（阶段结算、结构化输出＋锚点校验、种子与存档分段、不做永久错过）。
- 兵书 GDD §3.11 的字段与退回规则。
- 技术需求编号：合入仓库后由 `/architecture-review` 在 `tr-registry.yaml` 登记 `TR-fate-001…`。

## Related

- ADR-0001 引擎、平台与工程结构（依赖表修订一行）
- ADR-0004 存档格式；ADR-0006 场景流程与剧情标志
- `design/ai-character-fate/AI_CHARACTER_FATE_SPEC_v5.md`（v5.1）、`AI_CHARACTER_FATE_DESIGN.md`（v4 背景）
- `design/bingshu-system.md`、`design/game-blueprint.md` §5.6–5.7、`design/v0.2-huangjin/SYSTEMS_V02.md` §3
- 后续 ADR：P2 在线增强（`IOnlineAdviser`）；共同终章记录选择界面（第二篇章立项时）
