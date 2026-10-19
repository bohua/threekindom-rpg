# ADR-0001: 引擎、平台目标与工程结构基线

## Status

Accepted

> 只有用户，或在用户明确确认后由 `technical-director`，可以把本 ADR 改为 `Accepted`。

## Date

2026-10-18

## Last Verified

2026-10-18

## Decision Makers

- 用户 Bohua（引擎、工具与包选型已于 2026-10-18 批准，见 `docs/tool-selection.md`；本 ADR 的平台目标与工程结构待确认）
- 起草：Claude（`/architecture-decision` 流程，“选定引擎与技术方案”线程）
- 评审：`unity-specialist`（引擎校验，2026-10-18：仅有次要意见）、`technical-director`（TD-ADR 门，2026-10-18：CONCERNS，10 项意见已全部按建议修订进本稿；引擎与平台结论未被质疑）

## Summary

本 ADR 把已经在用的 Unity 6.3 LTS + C# + URP 定为全项目的地基决定，并补齐此前没定的三件事：发行平台目标、构建配置，以及 `Assets/` 下的工程目录与程序集（asmdef）划分。对比过 Godot 4、RPG Maker MZ 和 Unreal 5 后保留 Unity；首发平台为 Windows x64，Steam Deck 作为后续兼容验证目标，主机与移动端暂不纳入。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS（本机编辑器 6000.3.26f1） |
| **Domain** | Core / Scripting（附带 Rendering、Input 的基线约束） |
| **Layer** | Foundation |
| **Knowledge Risk** | HIGH — Unity 6 系列整体晚于模型训练数据；`docs/engine-reference/unity/VERSION.md` 的版本时间表自身标注未核实 |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`、`breaking-changes.md`、`deprecated-apis.md`、`current-best-practices.md`；`.claude/docs/directory-structure.md`；`docs/tool-selection.md` |
| **Post-Cutoff APIs Used** | 本 ADR 不写具体 API，但强制了三个 Unity 6 时代的高风险领域：Input System 作为唯一输入后端、UI Toolkit 运行时 UI、URP Render Graph（`RecordRenderGraph`）。另用到 Unity 6 的 Build Profiles。这些领域的代码必须对照 Unity 6.3 官方文档核实，不能只依赖 `docs/engine-reference/unity/`（其版本信息自身标注未核实） |
| **Verification Required** | ① Active Input Handling 切到 “Input System Package (New)” 后：原型移动与战斗输入正常，键盘与手柄都能操作，UI Toolkit 菜单与 Yarn 对话可用手柄导航，插件与包内无 `UnityEngine.Input` 调用；② IL2CPP Windows x64 发行构建成功并能启动；③ asmdef 拆分后 EditMode／PlayMode 测试仍被发现且通过（不是 “0 tests, Passed”） |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | None（地基决定，无上游 ADR） |
| **Enables** | 后续 ADR：战斗规则核心架构、数据资产格式、存档格式、HD-2D 渲染与相机管线、场景流程与剧情标志、文本与本地化（见文末“后续 ADR”） |
| **Blocks** | 所有需要新建代码目录或程序集的故事（首个是 V1 开发顺序第 1 步“一场战斗贯通”的战斗核心故事） |
| **Ordering Note** | 工程目录迁移必须在 CCGS 框架 PR #1 合并之后、战斗核心大规模铺代码之前完成，以免两次搬家 |

## Context

### Problem Statement

项目已经在 Unity 上有可运行原型，工具与第三方包也已批准，但这些决定散在 `project.yaml`、`tool-selection.md` 和对话记录里，没有一份 CCGS 认可的 ADR。CCGS 在 `standard` 流程下要求 Foundation 层决定先有 ADR，`/create-stories` 遇到缺失的关键 ADR 会停下。此外，发行平台只写了 “PC，其余待定”，代码放在哪、程序集怎么拆也没有约定。这些不先定，战斗核心一开写就会把结构固化下来。

### Current State

- Unity 6000.3.26f1、URP、C#；原型含正交相机、3D 场景、像素角色、移动碰撞、简单回合制与数据定义。
- 测试：EditMode 3/3、PlayMode 2/2；Windows x64 Mono 构建成功。
- 已装包：Input System 1.20.0、Cinemachine 3.1.7、Timeline 1.8.13、2D Sprite、2D Aseprite 3.0.2、Newtonsoft Json 3.2.2；IL2CPP 模块已装。
- 已知问题：Active Input Handling 仍为旧输入，代码使用 `Input.GetKeyDown`。
- 现有 `Assets/` 目录布局未在本线程核实（仓库由“第一版测试版评估”线程在用户电脑上操作），迁移步骤按“以实际为准”写。

### Constraints

- 一人开发 + AI 代理协作，Unity Personal 授权（年收入／融资 20 万美元以下）。
- 首版 V1 是 30–60 分钟验证版：3 名可操作武将、3 场黄巾战、1 座城镇；不联网、无内购、无配音。
- 必须兼容 CCGS 流程：代码根是 `Assets/`，测试根是 `Assets/Tests/EditMode|PlayMode/`。
- 中文为源语言，需要 CJK 字体与以后的多语言扩展。
- 输入已定为键盘 + 手柄全支持（`primary_input: Mixed`）。

### Requirements

- 在 3D 场景中渲染受光的像素精灵，并支持景深、Bloom、调色等后处理（HD-2D）。
- 回合制战斗规则可以在不启动场景的情况下单元测试（V1 规格大量公式与边界规则）。
- 数据驱动的角色、技能、装备、任务定义。
- 本机存档（3 手动 + 1 自动），版本号随存档保存。
- 1920×1080 下 60 fps 稳定（见“Performance Implications”的预算草案）。

## Decision

### 1. 引擎：保留 Unity 6.3 LTS

- 锁定 `6000.3.x` 补丁线，当前为 6000.3.26f1。开发期内只升级同一 LTS 线的补丁版本；跨小版本（如 6.4、7.x）升级须写新 ADR 取代本条。
- 语言 C#；渲染 URP，使用 **Universal Renderer（3D）**，不使用 2D Renderer；物理 PhysX（仅用于探索碰撞与触发，战斗不依赖物理）。
- Input System 为唯一输入后端（Player Settings → Active Input Handling = “Input System Package (New)”，修改后编辑器会重启）。旧 `Input` 类禁止出现在项目代码中；第三方插件若调用旧 `Input`，在只开新输入时会运行期抛 `InvalidOperationException`，引入前必须确认。UI 的 EventSystem 使用 `InputSystemUIInputModule`，不用 `StandaloneInputModule`。
- URP 自定义渲染 Pass 与第三方渲染特性都必须支持 Render Graph；URP 的旧兼容模式在 6.x 已弃用，不作为退路。
- Enter Play Mode Options：V1 保持默认的 Domain Reload 开启，换取静态状态的安全；如果以后为缩短进入播放时间而关闭，Core 的静态状态必须提供显式重置。

### 2. 平台目标

| 层级 | 平台 | 说明 |
| --- | --- | --- |
| 首发（V1 及之后的主目标） | Windows 10/11 x64 | 所有 Alpha／测试版只出 Windows 构建 |
| 兼容验证（V1 之后） | Steam Deck（通过 Proton 运行 Windows 构建） | 不做原生 Linux 构建；验证手柄操作、1280×800 显示、文字可读性 |
| 暂不纳入 | 主机（Switch／PlayStation／Xbox）、macOS、移动端、Web | 需要时写新 ADR；平台开发者授权与认证成本另行评估 |

为了不堵死以后上主机，代码层面遵守：不调用 Windows 专有 API；存档路径统一走 `Application.persistentDataPath`；所有输入经 Input Actions 抽象；UI 按分辨率无关布局并支持 16:9 与 16:10。发行渠道（是否上 Steam）尚未决定，本 ADR 不依赖它。

### 3. 构建配置

| 项 | 开发／测试构建 | 发行构建 |
| --- | --- | --- |
| Scripting Backend | Mono（迭代快） | IL2CPP |
| API Compatibility | .NET Standard 2.1 | 同左 |
| 架构 | x86_64 | x86_64 |
| 资源序列化 | Force Text + Visible Meta Files | 同左 |
| 加载方式 | 场景直接引用 | V1 同左；章节增多后再按单独 ADR 引入 Addressables |
| Build Profile | `Win64-Dev` | `Win64-Release`（托管代码剥离从 Minimal／Low 起步，`link.xml` 放在 `Assets/` 下） |

- 用 Unity 6 的 Build Profiles 区分两套配置，命令行构建用 `-activeBuildProfile` 指定；`-buildWindows64Player` 是否会忽略 Profile 覆盖需在首次配置时实测确认。
- IL2CPP 构建机前提：Visual Studio 2022 的“使用 C++ 的桌面开发”工作负载与 Windows SDK。
- 为避免 Mono 开发构建掩盖剥离／反射问题，每个里程碑至少出一次 IL2CPP 冒烟构建并启动，首个 Alpha 起执行。

### 4. 工程目录与程序集

整个仓库沿用 CCGS 结构（`design/`、`docs/`、`production/`、`tools/` 在仓库根目录），游戏代码与资源全部在 `Assets/` 下，项目自有内容集中在 `Assets/_Project/`，与第三方插件分开。

```
Assets/
├── _Project/
│   ├── Scripts/
│   │   ├── Core/          ThreeKingdoms.Core       纯 C# 规则：战斗结算、属性公式、成长、背包、事件标志
│   │   ├── Data/          ThreeKingdoms.Data       ScriptableObject 定义：武将、技能、装备、道具、敌方编组
│   │   ├── Save/          ThreeKingdoms.Save       存档读写、版本迁移、战前快照
│   │   ├── Gameplay/      ThreeKingdoms.Gameplay   探索、交互、战斗表现层、场景流程
│   │   ├── UI/            ThreeKingdoms.UI         UI Toolkit 界面与控制器（含通用对话框视图）
│   │   ├── Narrative/     ThreeKingdoms.Narrative  Yarn Spinner 接入、对话视图适配、过场（Timeline）驱动
│   │   ├── App/           ThreeKingdoms.App        组合根：Boot 场景、服务装配、连接 Gameplay／UI／Narrative
│   │   └── EditorTools/   ThreeKingdoms.EditorTools 仅编辑器：数据校验、导入工具（不用 “Editor” 作命名空间，避免与 UnityEditor.Editor 冲突）
│   ├── Data/              具体数据资产（.asset），按 Characters/Skills/Items/Encounters 分子目录
│   ├── Art/               Sprites、Models、Materials、Shaders、VFX
│   ├── Audio/             BGM、SFX、Mixers
│   ├── UI/                UXML、USS、字体、图标
│   ├── Dialogue/          .yarn 脚本
│   ├── Prefabs/
│   ├── Scenes/            Boot、Title、Town_Zhuojun、Taoyuan、Field_Outskirts、Battle、Cutscene_Xuzhou
│   └── Settings/          URP Asset、Volume Profile、Input Actions
├── Plugins/               只放无法走 UPM 的第三方（DOTween），且必须带 asmdef
└── Tests/
    ├── EditMode/          ThreeKingdoms.Tests.EditMode   引用 Core、Data、Save
    └── PlayMode/          ThreeKingdoms.Tests.PlayMode   引用 Gameplay、UI
```

程序集依赖方向（只能向下引用）：

```
App ───────► Narrative, UI, Gameplay, Save, Data, Core   （组合根）
Narrative ─► UI, Gameplay, Core
UI ────────► Gameplay, Core
Gameplay ──► Save, Data, Core
Save ──────► Core                                       （另加 Newtonsoft）
Data ──────► Core
Core ──────► （无）

EditorTools ──► 任意运行时程序集（仅 Editor 平台）
Tests.EditMode ──► Core, Data, Save
Tests.PlayMode ──► Gameplay, UI, Narrative, App
```

规则：App 在最上层；UI 和 Narrative 引用 Gameplay，Narrative 引用 UI（对话视图用 UI 的通用组件）；Gameplay 引用 Save 与 Data；Save 只引用 Core（另加 Newtonsoft），不引用 Data；Data 与 Save 都只向 Core 看齐。Gameplay 需要“开始对话”“打开菜单”时，通过在 Gameplay 中定义的接口或事件发出请求，由 App 把 Narrative／UI 的实现注入进去。

第三方依赖的接入方式与可见范围：

| 插件 | 安装方式 | 可引用的程序集 |
| --- | --- | --- |
| Yarn Spinner for Unity | UPM（自带 asmdef） | Narrative、App |
| UniTask | UPM（自带 asmdef） | Gameplay、UI、Narrative、App（Core 不得引用） |
| DOTween | Asset 导入到 `Assets/Plugins/`，用 DOTween Utility Panel → “Create ASMDEF” 生成程序集 | UI、Gameplay |
| Newtonsoft Json | UPM | Save |

UniTask 的 DOTween 支持（`UNITASK_DOTWEEN_SUPPORT`）只能在 DOTween 生成 asmdef 之后开启。

### Key Interfaces

本 ADR 只规定边界，不规定具体类。各边界的契约：

```csharp
// ThreeKingdoms.Core：不引用 UnityEngine（asmdef 勾选 noEngineReferences），不引用任何第三方库。
// 不能用 Debug、Mathf、Random、Vector*、[SerializeField]；
// 数学用 System.Math，日志与随机数通过注入的接口提供。
// 数据类型标 [System.Serializable] 并用公有字段，这样既能嵌进 ScriptableObject，也能被 Newtonsoft 序列化。
// 若使用 C# 9 record / init，需要手动声明 System.Runtime.CompilerServices.IsExternalInit。
namespace ThreeKingdoms.Core
{
    public interface ICoreLogger { void Info(string message); void Warn(string message); }
    public interface IRandomSource { int Range(int minInclusive, int maxExclusive); }
    /* 战斗结算、公式、状态机 */
}

// ThreeKingdoms.Data：ScriptableObject 把编辑器里配置的数据
// 转成 Core 使用的纯数据结构，Core 永远不直接拿 ScriptableObject。
namespace ThreeKingdoms.Data { /* XxxDefinition : ScriptableObject, ToRuntime() */ }

// ThreeKingdoms.Save：只序列化 Core 的纯数据状态 + 存档版本号。
namespace ThreeKingdoms.Save { /* SaveSlot, SaveService, 版本迁移 */ }
```

### Implementation Guidelines

- 新代码必须放进上面某个 asmdef；`Assets/` 根和 `Assembly-CSharp` 中不得新增游戏代码。
- `ThreeKingdoms.Core` 必须不引用 `UnityEngine`，战斗公式与 V1 规格第 4–8 节的规则必须在 EditMode 测试中可直接验证。
- 程序集只能按上图引用；出现反向依赖时用事件或接口反转，由 App 装配，而不是加引用。
- 每个第三方库都必须有自己的 asmdef。`Assets/Plugins/` 下无 asmdef 的代码会编进 `Assembly-CSharp-firstpass`，asmdef 程序集无法引用它。
- 两个测试 asmdef 必须：`overrideReferences: true`，`precompiledReferences` 含 `nunit.framework.dll`，引用 `UnityEngine.TestRunner` 与 `UnityEditor.TestRunner`，`defineConstraints: [UNITY_INCLUDE_TESTS]`，`autoReferenced: false`；EditMode 设 `includePlatforms: [Editor]`，PlayMode 不限平台。不使用已弃用的 `optionalUnityReferences: TestAssemblies`。用编辑器菜单的测试程序集模板创建即可得到这些设置。
- RenderGraph 与 UI Toolkit 运行时数据绑定的代码，必须对照 Unity 6.3 官方文档核实后再提交。
- 根命名空间 `ThreeKingdoms.<程序集名>`，与 `project.yaml` 的命名约定一致（类 PascalCase、变量 camelCase、事件 `On<Event>`）。
- 新代码不得使用旧 `Input` 类；不得使用 DOTS/Entities、Netcode、Unity Gaming Services。
- 自定义渲染 Pass 必须使用 RenderGraph API（`RecordRenderGraph`），不写旧的 `Execute` 签名。
- 存档文件只写 `Application.persistentDataPath`。

## Alternatives Considered

### Alternative 1: Godot 4.x（C# 或 GDScript）

- **Description**：用 Godot 4 的 3D 场景 + Sprite3D 做 HD-2D，C# 或 GDScript 编写。
- **Pros**：完全免费、MIT 授权、无收入门槛；编辑器轻量；Sprite3D 与 2D/3D 混合方便；CCGS 自带 Godot 专家代理。
- **Cons**：原型、测试、构建链和已批准的工具清单要全部重做；HD-2D 关键的后处理（移轴景深、Bloom、调色）需要更多自定义；中文社区的成熟 JRPG 插件和教程少于 Unity；主机移植依赖第三方移植商。
- **Estimated Effort**：比保留 Unity 多出“重建原型 + 工具链”的完整一轮。
- **Rejection Reason**：Unity 原型已经验证了移动、碰撞、相机、灯光和回合制，而且用户已批准整套 Unity 工具链。Godot 的授权优势不足以抵消重做成本。

### Alternative 2: RPG Maker MZ

- **Description**：使用 RPG Maker MZ 的内置地图、数据库和战斗系统。
- **Pros**：JRPG 功能（对话、事件、数据库、存档、菜单）开箱即用，最快产出可玩内容。
- **Cons**：本质是 2D 瓦片地图，无法实现 3D 场景 + 光影 + 景深的 HD-2D 目标；要实现“兵力即 HP、兵种克制、先下令后按速度结算”需大量 JS 插件改造；测试与版本控制工作流弱，不适配 CCGS。
- **Estimated Effort**：内容产出快，但 HD-2D 画面基本不可达。
- **Rejection Reason**：与已确认的 HD-2D 美术方向根本冲突。

### Alternative 3: Unreal Engine 5

- **Description**：使用 UE5 的 3D 渲染与 Paper2D／Paper ZD 精灵方案，《八方旅人》即基于 Unreal。
- **Pros**：HD-2D 原作同引擎，渲染与后处理能力最强。
- **Cons**：C++/蓝图学习与迭代成本高，对一人 + AI 代理团队偏重；2D 精灵工作流不如 Unity 成熟；工程体积大；同样需重做原型。
- **Estimated Effort**：明显高于 Unity。
- **Rejection Reason**：V1 规模不需要 Unreal 的渲染上限，迭代速度和现有积累更重要。

### Alternative 4（平台）：PC + 主机同时作为首发目标

- **Rejection Reason**：主机开发者授权、开发机与认证在 V1 验证版阶段成本过高；本 ADR 通过代码约束保留以后移植的可能。

## Consequences

### Positive

- 现有原型、测试和工具链全部继续有效，不重做。
- Core 规则与 Unity 解耦后，V1 规格中大量公式与边界规则能用快速的 EditMode 测试覆盖。
- asmdef 拆分缩短编译时间，并让依赖方向可被编译器强制。
- 平台范围收窄到 Windows，测试和构建矩阵最小。

### Negative

- Unity 6 晚于模型训练数据，代理写 Unity API 时必须查 `docs/engine-reference/unity/`，且该参考目录自身部分未核实。
- 需要一次工程目录迁移和输入后端切换，短期内会动到原型代码。
- Core 与 Data 分层多一层转换代码。
- Unity Personal 有收入门槛，发行前需复核授权条款。

### Neutral

- Steam Deck 仅作兼容验证，不产生额外原生构建。

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| 代理按旧版 Unity 知识写出不能编译的 API | 中 | 中 | 写 Unity 代码前查 engine-reference；每个故事都要求编译 + 测试通过 |
| 切换 Input System 后原型输入失效 | 中 | 低 | 迁移作为单独故事，先改代码再切 Active Input Handling，用 PlayMode 测试验证 |
| asmdef 拆分后测试未被发现（“0 tests, Passed”） | 中 | 中 | 迁移验收必须看到原有 5 个测试的名字和通过结果 |
| 目录迁移与其他线程正在进行的战斗核心分支冲突 | 中 | 中 | 由在用户电脑上操作仓库的线程统一执行，先合并框架 PR，再迁移，再继续战斗核心 |
| Unity 授权条款变化 | 低 | 高 | 发行前复核；Core 与引擎解耦降低最坏情况的迁移成本 |
| IL2CPP 发行构建出现反射／剥离问题（Newtonsoft、Yarn Spinner） | 中 | 中 | 首个 Alpha 起每个里程碑出 IL2CPP 冒烟构建；需要时加 `link.xml` |
| 第三方插件仍调用旧 `Input`，只开新输入后运行期报错 | 中 | 中 | 切换前全局搜索插件与包；对话推进与 UI 导航纳入验证 |
| URP 默认精灵材质（Sprite-Lit-Default）只响应 2D Renderer 灯光，在 Universal Renderer 下像素人物不受 3D 光照 | 高 | 中 | 人物需要 3D 受光、Alpha Clip 的材质（要投影还需 ShadowCaster Pass），具体方案与渲染路径（Forward+ 等）留给 ADR-0005 |

## Performance Implications

预算为暂定草案。原型尚无 HD-2D 后处理，最低配置下的帧率目标在 ADR-0005（渲染与相机）中验证并定稿；本 ADR 只要求在开发机上记录一次基线测量。

| Metric | Before | Expected After | Budget |
|--------|--------|---------------|--------|
| CPU (frame time) | 未测 | 无变化 | 16.6 ms（60 fps，1920×1080） |
| Memory | 未测 | 无变化 | ≤ 2 GB 进程内存 |
| Load Time | 未测 | 无变化 | 场景切换 ≤ 3 s（SSD） |
| Network (if applicable) | 不适用 | 不适用 | 不适用 |

最低配置草案：4 核 CPU、8 GB 内存、GTX 1050 Ti／同级集显（Iris Xe、Radeon 680M）。Steam Deck 兼容验证时以 800p 30–60 fps 为目标。

## Migration Plan

由在用户电脑上操作仓库的线程执行（本线程不直接改仓库）：

1. 合并 CCGS 框架 PR #1；把 6000.3.26f1 写入 `docs/engine-reference/unity/VERSION.md` 的 “Installed at pin time”。
2. 记录当前 `Assets/` 布局，以及场景、预制体对脚本与资源的引用，作为迁移前基线。
3. 新建 `Assets/_Project/` 与各 asmdef，**只在 Unity 编辑器里移动文件**（保留 .meta 的 GUID，不用 git mv 或文件管理器），把原型代码按职责搬入对应目录；战斗规则与数据定义优先移入 Core／Data。验证：编译通过，EditMode 3/3、PlayMode 2/2 名字与结果和迁移前一致，场景无丢失脚本。
4. 把 `Input.GetKeyDown` 等调用改为 Input Actions，EventSystem 换成 `InputSystemUIInputModule`，再把 Active Input Handling 切为 “Input System Package (New)”。验证：手动移动、攻击史莱姆、柱子阻挡依旧正常，键盘和手柄都可操作。
5. 配置 `Win64-Dev`／`Win64-Release` 两个 Build Profile，出一次 IL2CPP Windows x64 构建并启动；在开发机记录一次帧时间与内存基线。
6. 更新 `technical-preferences.md` 的 Architecture Decisions Log、Engine Specialists 与 Forbidden Patterns。

**Rollback plan**：步骤 2–4 各自独立提交；任一步失败直接回退该提交，原型保持可运行。

## Validation Criteria

- [ ] `Assets/` 根与 `Assembly-CSharp` 中没有游戏代码，所有代码都属于某个 `ThreeKingdoms.*` 程序集
- [ ] `ThreeKingdoms.Core` 勾选 `noEngineReferences` 且能编译
- [ ] 迁移后 EditMode 与 PlayMode 测试数量不少于迁移前，且全部通过
- [ ] 在 `Assets/` 下搜索 `\bInput\.(Get|any|mouse)` 无项目代码命中，且没有包要求 Active Input Handling 保持 “Both”
- [ ] IL2CPP Windows x64 构建成功并能启动
- [ ] 开发机上记录了原型场景的帧时间与内存基线（最低配置 60 fps 的验证移交 ADR-0005）

## GDD Requirements Addressed

来源说明：`design/gdd/combat-system.md` 目前只在未合并的 PR #2（分支 docs/ccgs-onboarding）中；V1_SPEC 指用户电脑上 `ThreeKingdomsJRPG-Docs/V1_SPEC.md` 2026-10-18 20:51 的版本（“三战版本”）。这些要求的 TR 编号尚未登记，待 PR #2 合并后由 `/architecture-review` 写入 `docs/architecture/tr-registry.yaml`。

Foundational — 无单一 GDD 要求。Enables：战斗系统（`design/gdd/combat-system.md`，位于 PR #2）的规则可单测要求；存档系统（V1 规格第 9 节）的本机存档要求；输入（键盘 + 手柄全支持）；HD-2D 画面（V1 规格第 10 节）。

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `design/gdd/combat-system.md`（PR #2）／V1_SPEC §4–5 | 战斗 | 先全队下令再按速度结算；同速、撤退、技力、克制等边界规则必须确定且可验证 | 规则放在不依赖 Unity 的 Core 程序集，可直接用 EditMode 测试覆盖 |
| V1_SPEC §9 | 存档 | 3 手动 + 1 自动本机存档，记录游戏版本，损坏时不清空 | 独立 Save 程序集，统一写入 `persistentDataPath` 并带版本号 |
| `docs/tool-selection.md` | 输入 | 键盘 + 手柄全支持、可改键 | Input System 为唯一输入后端 |
| PROJECT.md §5、V1_SPEC §10 | 画面 | HD-2D：像素人物 + 3D 场景光影与景深 | URP Universal Renderer（3D）+ Volume 后处理 |

## Related

- `docs/tool-selection.md`：已批准的工具、包与插件（UI Toolkit、Yarn Spinner、DOTween、UniTask），本 ADR 不重复选择
- `ThreeKingdomsJRPG-Docs/V1_SPEC.md`：首版规则基线
- 后续 ADR（建议顺序）：
  1. ADR-0002 战斗规则核心架构（指令收集、速度排序、结算管线、敌方 AI 接口）
  2. ADR-0003 数据资产格式（ScriptableObject 与 JSON 的分工、数据校验）
  3. ADR-0004 存档格式与版本迁移（含战前快照与防重复发奖标志）
  4. ADR-0005 HD-2D 渲染与相机（正交／低 FOV 透视、像素精灵受光着色器、像素密度）
  5. ADR-0006 场景流程与剧情标志（场景切换、事件状态、过场锁）
  6. ADR-0007 文本与本地化（Yarn Spinner 与 Unity Localization 衔接、CJK 字体）
  7. UI Toolkit 与 Yarn Spinner 的既有决定可用 `/architecture-decision retrofit` 补成 ADR
