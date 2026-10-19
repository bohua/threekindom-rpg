# ADR-0001 迁移前基线（Migration Plan 第 2 步）

记录日期：2026-10-19。基于 `main` @ `e7c52b0`（PR #1 合并后），在独立工作目录中读取与运行；本步骤未移动任何 `Assets/` 文件。第 3 步搬迁完成后，用本文件逐项对照：文件 GUID 不变、引用不丢、测试名与结果一致。

## 1. 环境

| 项 | 值 |
| --- | --- |
| Unity | 6000.3.26f1 (de8167e7440c)，来自 `ProjectSettings/ProjectVersion.txt` |
| Active Input Handling | `activeInputHandler: 0`（仅旧输入 Input Manager） |
| API Compatibility | `apiCompatibilityLevel: 6`（.NET Standard 2.1） |
| Scripting Backend | 未按平台覆盖（Windows Standalone 默认 Mono） |
| Build Settings 场景 | `Assets/Game/Scenes/Glade.unity`（guid `a8dca94985d26c743a7648af9d18619c`），唯一场景 |

## 2. `Assets/` 布局与 GUID

受版本控制的非 `.meta` 文件（`git ls-files Assets`），附各自 `.meta` 中的 GUID：

| 路径 | GUID |
| --- | --- |
| `Assets/CLAUDE.md` | 无 `.meta`（未入库；Unity 导入时会自动生成 `CLAUDE.md.meta`，本 PR 未提交） |
| `Assets/DefaultVolumeProfile.asset` | `caeb0337860cf5a45bab886ca5337ad0` |
| `Assets/UniversalRenderPipelineGlobalSettings.asset` | `74b5a60616355a3459dd5a8fc6943508` |
| `Assets/Game/`（目录） | `09530748288ecb54c8e7abf764e12aa4` |
| `Assets/Game/Art/`（目录） | `dfa6ee28efa23cb418681771006000e7` |
| `Assets/Game/Art/Hero.png` | `e79f44649ad5d9a4d994c71d591e6ce3` |
| `Assets/Game/Art/Heropixels.mat` | `80ac7605d01a59f44ac3e0cea54d40ce` |
| `Assets/Game/Art/Moss.mat` | `d39b42fd764bb0b47aa88fd02524be6e` |
| `Assets/Game/Art/Slime.png` | `b00ea977c6a9300418debdda79d8b023` |
| `Assets/Game/Art/Slimepixels.mat` | `3cfee4c9769598f42a6dbfc994d7286e` |
| `Assets/Game/Art/Stone.mat` | `7f5df19727eefab45817568155fea7a2` |
| `Assets/Game/Data/`（目录） | `e24bf616d4edcba4c9492f638e691394` |
| `Assets/Game/Data/ClearGlade.asset` | `afc2d183d4bb73744b56b7252a7a761b` |
| `Assets/Game/Data/Hero.asset` | `257874624b125494595208895c626bf8` |
| `Assets/Game/Data/Slime.asset` | `6edf2c551fe20ce4d8a42132edfce521` |
| `Assets/Game/Data/Strike.asset` | `a7f23e607da56264e80779ca44c62cff` |
| `Assets/Game/Editor/`（目录） | `48782f5d54b31814f9506f95882522e9` |
| `Assets/Game/Editor/Jrpg.Editor.asmdef` | `6836e05ce98971d4db8d2890b7a7ebbd` |
| `Assets/Game/Editor/ProjectSetup.cs` | `d282bebf36dc10b4a873d4aef3781203` |
| `Assets/Game/Runtime/`（目录） | `8fcd903bd8e28da42873f408a7ccdb70` |
| `Assets/Game/Runtime/Jrpg.Runtime.asmdef` | `4be5ac34193aee7478f65f1fa4b2b0c7` |
| `Assets/Game/Runtime/CharacterDefinition.cs` | `2f1dadf066a53ee4a98fbb203ef6cb4c` |
| `Assets/Game/Runtime/Definitions.cs` | `ea0344825adcee249916bd4e61df237e` |
| `Assets/Game/Runtime/DemoEncounter.cs` | `bc1ec8e8a76b32049b015a6f616c4a30` |
| `Assets/Game/Runtime/FollowCamera.cs` | `98b24963f9129a24f92cd421cd01b939` |
| `Assets/Game/Runtime/PlayerMover.cs` | `395ba593abe33e7499d4a0dd47689088` |
| `Assets/Game/Runtime/QuestDefinition.cs` | `3b360c661ba80d34e89bae0a948f073a` |
| `Assets/Game/Runtime/SkillDefinition.cs` | `784e700fb65671049b59f1be67773d4a` |
| `Assets/Game/Scenes/`（目录） | `d7bec67dd4124c14ebe62f4d67f6708c` |
| `Assets/Game/Scenes/Glade.unity` | `a8dca94985d26c743a7648af9d18619c` |
| `Assets/Game/Settings/`（目录） | `29848f810f9869d49aa31e8f59739d3f` |
| `Assets/Game/Settings/GladeRenderer.asset` | `ac14a6d8266295c4081daf5039094bee` |
| `Assets/Game/Settings/GladeURP.asset` | `57e044b245a1cab4e806afe5f843a507` |
| `Assets/Game/Tests/`（目录） | `497e6c7ebd06ae448842eab23d396571` |
| `Assets/Game/Tests/EditMode/`（目录） | `85d0cb880739a2c4abf87f807b9f1f00` |
| `Assets/Game/Tests/EditMode/Jrpg.EditModeTests.asmdef` | `ce3b7fa5c96e4e645a05dd1829add205` |
| `Assets/Game/Tests/EditMode/BattleTests.cs` | `f4ed0d3585df1754ca7f6b13ac2f95a5` |
| `Assets/Game/Tests/PlayMode/`（目录） | `d7f78c4c07f479c4a825fa6fc9ed7193` |
| `Assets/Game/Tests/PlayMode/Jrpg.PlayModeTests.asmdef` | `f6192f7b2b11f0a47bc010576c431254` |
| `Assets/Game/Tests/PlayMode/MovementTests.cs` | `140d6d60b1fd2c24ea27dac59b57526d` |

无预制体（`.prefab`）；无 `Assets/Plugins/`。

## 3. 程序集（asmdef）

| 程序集 | 位置 | 引用 | 备注 |
| --- | --- | --- | --- |
| `Jrpg.Runtime` | `Assets/Game/Runtime/` | 无显式引用 | `rootNamespace: Jrpg`；所有运行时代码与 ScriptableObject 定义都在此 |
| `Jrpg.Editor` | `Assets/Game/Editor/` | `Jrpg.Runtime`、`Unity.RenderPipelines.Universal.Runtime` | 仅 Editor；`ProjectSetup` 无命名空间 |
| `Jrpg.EditModeTests` | `Assets/Game/Tests/EditMode/` | `Jrpg.Runtime` | 仅 Editor；使用已弃用的 `optionalUnityReferences: TestAssemblies` |
| `Jrpg.PlayModeTests` | `Assets/Game/Tests/PlayMode/` | `Jrpg.Runtime` | 使用已弃用的 `optionalUnityReferences: TestAssemblies` |

运行时类型（均在 `namespace Jrpg`）：

| 文件 | 类型 | 依赖 UnityEngine |
| --- | --- | --- |
| `CharacterDefinition.cs` | `CharacterDefinition : ScriptableObject` | 是 |
| `SkillDefinition.cs` | `SkillDefinition : ScriptableObject` | 是 |
| `QuestDefinition.cs` | `QuestDefinition : ScriptableObject` | 是 |
| `Definitions.cs` | `QuestProgress`、`Combatant`、`TurnBattle` | 有 `using UnityEngine`；逻辑本身只用 `System.Math`，但构造参数是上面的 ScriptableObject |
| `DemoEncounter.cs` | `DemoEncounter : MonoBehaviour` | 是（含 `Input.GetKeyDown`） |
| `PlayerMover.cs` | `PlayerMover : MonoBehaviour` | 是（含 `Input.GetAxisRaw`） |
| `FollowCamera.cs` | `FollowCamera : MonoBehaviour` | 是 |

旧输入调用（第 4 步要替换）：`DemoEncounter.cs:21` `Input.GetKeyDown(KeyCode.E)`、`DemoEncounter.cs:74` `Input.GetKeyDown(KeyCode.Escape)`、`PlayerMover.cs:14` `Input.GetAxisRaw("Horizontal"/"Vertical")`。

## 4. 场景与资产引用

按 `guid:` 提取的引用（`0000…` 内建资源与包内 GUID 已标注）：

| 引用方 | 引用的项目资产 | 包／内建 |
| --- | --- | --- |
| `Glade.unity` | 脚本 `PlayerMover`、`FollowCamera`、`DemoEncounter`；数据 `Hero.asset`、`Slime.asset`、`Strike.asset`、`ClearGlade.asset`；贴图 `Hero.png`、`Slime.png`；材质 `Heropixels.mat`、`Slimepixels.mat`、`Moss.mat`、`Stone.mat` | `a79441f348de89743a2939f4d699eac1` = URP `UniversalAdditionalCameraData.cs`；`0000000000000000e000000000000000`／`f000…` 等 = 内建资源 |
| `Hero.asset`、`Slime.asset` | 脚本 `CharacterDefinition`；技能 `Strike.asset` | — |
| `Strike.asset` | 脚本 `SkillDefinition` | — |
| `ClearGlade.asset` | 脚本 `QuestDefinition` | — |
| `GladeURP.asset` | `GladeRenderer.asset` | URP 包内默认资源 3 项 |
| `GladeRenderer.asset`、`DefaultVolumeProfile.asset`、`UniversalRenderPipelineGlobalSettings.asset` | —（`UniversalRenderPipelineGlobalSettings` 也引用 `DefaultVolumeProfile`） | URP 包内着色器、Volume 组件与设置 |

搬迁时只要通过 AssetDatabase 移动（`.meta` 随行、GUID 不变），以上引用都不会断。

## 5. 测试基线（Unity batchmode，2026-10-19）

命令：`Unity.exe -batchmode -projectPath <worktree> -runTests -testPlatform EditMode|PlayMode -testResults <xml>`，两次均退出码 0。

| 平台 | 结果 | 测试 |
| --- | --- | --- |
| EditMode | 3/3 Passed（0 failed，0 skipped） | `BattleTests.DamageClampsToRemainingHpAndDoesNotMutateAsset`、`BattleTests.QuestIgnoresOtherTargetsAndCapsProgress`、`BattleTests.TurnOrderUsesSpeedAndSkipsDefeatedUnits` |
| PlayMode | 2/2 Passed（0 failed，0 skipped） | `MovementTests.ControllerStopsAtWall`、`MovementTests.GladeHasPlayableWiring` |

第 3 步验收要求：迁移后同样 5 个测试被发现且全部通过（不是 “0 tests, Passed”）。

## 6. 对第 3 步的影响

- 现有代码全部在单一 `Jrpg.Runtime` 中，`Combatant`／`TurnBattle` 直接持有 ScriptableObject，不能原样放入 `noEngineReferences` 的 `ThreeKingdoms.Core`。第 3 步只做“按职责搬位置 + 建 asmdef”，需决定：要么暂时把 `Definitions.cs` 放进 `ThreeKingdoms.Data`/`Gameplay`，Core 先建空壳；要么在第 3 步内顺带把战斗逻辑改为吃纯数据（这属于代码改动，建议留给 ADR-0002 的战斗核心故事）。
- 改程序集名或命名空间（`Jrpg` → `ThreeKingdoms.*`）不影响 MonoScript 的 GUID 引用，但若类名或命名空间变化，场景与 `.asset` 中的 `m_Script` 仍按 GUID 解析，可正常保留；`[FormerlySerializedAs]` 仅在字段改名时需要。
- 测试 asmdef 需按 ADR 改为 `overrideReferences` + `nunit.framework.dll` + `UNITY_INCLUDE_TESTS`，替换已弃用的 `optionalUnityReferences`。

## 7. 迁移后结果（第 3 步，2026-10-19）

用户选择“先只搬位置”：`Definitions.cs` 暂放 `ThreeKingdoms.Data`，`ThreeKingdoms.Core` 先建空程序集，改纯数据留给 ADR-0002。命名空间仍为 `Jrpg`，未改。

**搬迁方式**：临时编辑器脚本经 `Unity.exe -batchmode -executeMethod` 调用 `AssetDatabase.CreateFolder`／`AssetDatabase.MoveAsset`，每步校验 GUID 不变；`Assets/Game/` 搬空后用 `AssetDatabase.DeleteAsset` 删除。临时脚本用完已删除，未提交。

**文件去向**：

| 原位置 | 新位置 |
| --- | --- |
| `Game/Runtime/{Character,Skill,Quest}Definition.cs`、`Definitions.cs` | `_Project/Scripts/Data/` |
| `Game/Runtime/{DemoEncounter,PlayerMover,FollowCamera}.cs` | `_Project/Scripts/Gameplay/` |
| `Game/Editor/ProjectSetup.cs` | `_Project/Scripts/EditorTools/` |
| `Game/Tests/EditMode|PlayMode/*.cs` | `Tests/EditMode|PlayMode/` |
| `Game/Art/*.png`／`*.mat` | `_Project/Art/Sprites/`／`_Project/Art/Materials/` |
| `Game/Data/Hero|Slime.asset`、`Strike.asset`、`ClearGlade.asset` | `_Project/Data/Characters/`、`Skills/`、`Quests/` |
| `Game/Scenes/Glade.unity` | `_Project/Scenes/`（Build Settings 路径自动更新） |
| `Game/Settings/*`、`Assets/DefaultVolumeProfile.asset`、`Assets/UniversalRenderPipelineGlobalSettings.asset` | `_Project/Settings/` |

**程序集与依赖**（旧 `Jrpg.*` asmdef 搬移并改名，`.meta` GUID 保留；其余为新建）：

| 程序集 | 引用 | 备注 |
| --- | --- | --- |
| `ThreeKingdoms.Core` | — | 新建，`noEngineReferences: true`，暂无代码 |
| `ThreeKingdoms.Data` | Core | 原 `Jrpg.Runtime`（GUID `4be5ac34…`）；含 3 个定义类与 `Definitions.cs` |
| `ThreeKingdoms.Save` | Core | 新建，暂无代码 |
| `ThreeKingdoms.Gameplay` | Save、Data、Core | 新建；含 `DemoEncounter`、`PlayerMover`、`FollowCamera` |
| `ThreeKingdoms.UI` | Gameplay、Core | 新建，暂无代码 |
| `ThreeKingdoms.Narrative` | UI、Gameplay、Core | 新建，暂无代码 |
| `ThreeKingdoms.App` | Narrative、UI、Gameplay、Save、Data、Core | 新建，暂无代码 |
| `ThreeKingdoms.EditorTools` | 全部运行时程序集 + `Unity.RenderPipelines.Universal.Runtime` | 原 `Jrpg.Editor`（GUID `6836e05c…`）；仅 Editor |
| `ThreeKingdoms.Tests.EditMode` | TestRunner ×2、Core、Data、Save | 原 `Jrpg.EditModeTests`；改为 `overrideReferences` + `nunit.framework.dll` + `UNITY_INCLUDE_TESTS`，仅 Editor |
| `ThreeKingdoms.Tests.PlayMode` | TestRunner ×2、Gameplay、UI、Narrative、App、**Data** | 原 `Jrpg.PlayModeTests`；同上写法，不限平台 |

**与 ADR 的两处偏离**（用户 2026-10-19 知悉）：
1. `Tests.PlayMode` 额外引用 `ThreeKingdoms.Data`：`MovementTests` 读取 `DemoEncounter.HeroDefinition` 等 Data 类型字段，不引用无法编译。
2. `ProjectSetup.cs` 只改了硬编码路径字符串（`Assets/Game/...` → `Assets/_Project/...` 对应子目录），否则 `BuildWindows()` 会在旧位置重建空场景；逻辑与命名空间未改。

**验证**：

| 项 | 结果 |
| --- | --- |
| 编译 | 通过（batchmode 无 `error CS`） |
| EditMode | 3/3 Passed，测试名与第 5 节一致 |
| PlayMode | 2/2 Passed，测试名与第 5 节一致；`GladeHasPlayableWiring` 加载场景并检查脚本与数据引用 |
| 场景脚本引用 | `Glade.unity` 的 `m_Script` 全部解析到新位置的同一 GUID（另 1 个为 URP 包内脚本） |
| GUID | 第 2 节所有文件 GUID 保留；只有 10 个旧文件夹 `.meta`（`Assets/Game/**`）随文件夹删除，仓库中无任何引用 |
| git | 文件与其 `.meta` 识别为改名；4 个旧 asmdef 因内容整体改写显示为删除 + 新增，但对应 `.meta` 识别为改名、GUID 不变 |

另：一并提交 `Assets/CLAUDE.md.meta`，避免每次导入都重新生成。

## 8. 第 4 步：切换到 Input System（2026-10-19）

**改动**：

- 新增 `Assets/_Project/Settings/GameControls.inputactions`（不生成 C# 包装类），含两个 action map，键盘与手柄都有绑定。
- `PlayerMover` 新增 `MoveAction`；`DemoEncounter` 新增 `InteractAction`、`MenuAction`（均为 `InputActionReference`，在 `OnEnable`／`OnDisable` 中启用／停用）。三处旧调用已替换：
  - `PlayerMover.cs`：`Input.GetAxisRaw("Horizontal"/"Vertical")` → `MoveAction.action.ReadValue<Vector2>()`
  - `DemoEncounter.cs`：`Input.GetKeyDown(KeyCode.E)` → `InteractAction.action.WasPressedThisFrame()`
  - `DemoEncounter.cs`：`OnGUI` 里的 `Input.GetKeyDown(KeyCode.Escape)` → `Update` 中 `MenuAction.action.WasPressedThisFrame()`
- 战斗中按 Interact（E／Enter／手柄 A）也会执行“攻击”，这样手柄不用鼠标点 IMGUI 按钮也能打完；画面提示文字同步更新。
- 场景 `Glade.unity` 用一次性 batchmode 脚本挂上三个引用（场景 diff 仅 +3 行）；临时脚本已删除、未提交。`ProjectSetup.Create()` 同步挂上这些引用。
- `ThreeKingdoms.Gameplay`、`ThreeKingdoms.EditorTools` 增加对 `Unity.InputSystem` 的引用。
- `ProjectSettings.asset`：`activeInputHandler: 0` → `1`（Input System Package (New)）。
- EventSystem：场景中没有 EventSystem（当前 UI 是 IMGUI `OnGUI`），无需替换；以后加 uGUI/UI Toolkit 运行时 UI 时按 ADR 使用 `InputSystemUIInputModule`。

**按键绑定**：

| Map／Action | 键盘 | 手柄 | 当前用途 |
| --- | --- | --- | --- |
| Player/Move | WASD、方向键 | 左摇杆、十字键 | 移动 |
| Player/Interact | E、Enter | South（A／×） | 接触史莱姆开战；战斗中攻击 |
| Player/Cancel | X、Backspace | East（B／○） | 预留，暂未使用 |
| Player/Menu | Esc | Start | 退出原型（原 Esc 行为） |
| UI/Navigate | WASD、方向键 | 左摇杆、十字键 | 预留给菜单 |
| UI/Submit | Enter | South | 预留 |
| UI/Cancel | Esc | East | 预留 |
| UI/Point、Click、ScrollWheel | 鼠标位置、左键、滚轮 | — | 预留 |

**验证**：

| 项 | 结果 |
| --- | --- |
| `Assets/` 搜索 `\bInput\.(Get|any|mouse)` | 0 命中 |
| `Packages/`（本地嵌入包） | 无 `.cs`，0 命中 |
| 已安装包中的旧输入调用 | Cinemachine、URP Core 的调用都在 `ENABLE_LEGACY_INPUT_MANAGER` 条件编译内；uGUI 的旧输入只经 `StandaloneInputModule`／`BaseInput` 使用，本项目没有 EventSystem，也不会使用它们。没有包要求保持 “Both” |
| 编译 | 通过 |
| EditMode | 3/3 Passed（名字同第 5 节） |
| PlayMode | 2/2 Passed（名字同第 5 节）；测试本身不依赖旧输入，未修改 |

**需要人工验证**（batchmode 测试覆盖不到真实按键，见第 9 节补充）：键盘、手柄各一遍——移动；靠近史莱姆按 Interact 开战并攻击到胜利；柱子和墙能挡住；Esc／Start 退出（在编辑器 Play 模式里不会真的退出，需构建后验证）。

## 9. 第 5 步：Build Profile、IL2CPP 构建与性能基线（2026-10-19）

**构建环境**：Unity 6000.3.26f1，已装 IL2CPP 模块（`win64_player_*_il2cpp`）；Visual Studio Community 2022 17.14 带 C++ 工具集（VC.Tools.x86.x64）；Windows SDK 10.0.26100 / 19041。无需额外安装。

**Build Profile**（`Assets/_Project/Settings/BuildProfiles/`）：

| Profile | 平台 | Player Settings | 构建选项 |
| --- | --- | --- | --- |
| `Win64-Dev` | Windows x64 Player | 不覆盖，沿用全局（Mono、.NET Standard 2.1、不剥离） | Development Build |
| `Win64-Release` | Windows x64 Player | 覆盖：IL2CPP、.NET Standard 2.1、托管剥离 Low、IL2CPP 编译配置 Release | 非 Development |

- 两个 Profile 都不覆盖场景列表，用全局 Build Settings（`Glade.unity`）。全局 Player Settings 未改动，仍为 Mono。
- 创建方式：Unity 6.3 没有公开的“创建 Build Profile”API（`BuildProfile.CreateInstance(BuildTarget, StandaloneBuildSubtarget)` 为 internal），本次用一次性 batchmode 脚本通过反射调用它，并经 `BuildProfile.SetActiveBuildProfile` 写入 Release 的 Player Settings 覆盖；脚本未提交。之后在编辑器的 Build Profiles 窗口里查看或修改即可，不需要再跑脚本。
- 注意：`Win64-Release` 保存的是一整份 Player Settings 副本（约 34 KB YAML）。以后改全局 Player Settings（例如公司名、图标、分辨率）时，Release 不会自动跟着变，需要在 Profile 里同步。
- 尚未加 `link.xml`：本次 Low 剥离下构建与运行都正常。

**构建命令与结论**：

| 方式 | 结果 |
| --- | --- |
| 编辑器脚本 `BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions { buildProfile = Win64-Release, … })` | 成功，0 错误；首次全量约 3 分 53 秒，增量约 35–47 秒 |
| 命令行 `Unity.exe -batchmode -quit -activeBuildProfile Assets/_Project/Settings/BuildProfiles/Win64-Release.asset -buildWindows64Player <exe>` | 成功，输出含 `GameAssembly.dll`（IL2CPP 产物），日志显示 “Build profile assigned via command line”。**结论：与 `-activeBuildProfile` 同用时，`-buildWindows64Player` 会遵守 Profile 的 Player Settings 覆盖** |

输出目录（仓库外，未提交）：`D:\BuildArtifacts\Codex\hd2d-jrpg\builds\adr0001-il2cpp\MoonlitGlade.exe`（可发布部分约 93 MB；`*_DoNotShip`／`*_DontShip` 目录为调试符号）。命令行实验产物在 `...\builds\adr0001-cli-test\`。

**启动与性能基线**（IL2CPP 发行构建，1920×1080 窗口，场景 `Glade`，5 秒预热后采样 30 秒）：

采样用 `ThreeKingdoms.App.PerfBaselineProbe`：默认不启用，只有带 `-perfBaseline <秒> -perfOut <文件>` 启动时才采样，结束后写 JSON 并退出；加 `-perfUncapped` 时关闭垂直同步。进程内存由外部每 0.5 秒读取一次。

| 指标 | 垂直同步开（默认） | 不限帧（`-perfUncapped`） | ADR 预算 |
| --- | --- | --- | --- |
| 平均帧时间 | 16.67 ms（被 60 Hz 显示器锁帧） | 0.68 ms | 16.6 ms |
| p50／p95／p99／最大 | 16.67／16.69／16.69／16.69 ms | 0.62／0.92／2.20／7.34 ms | — |
| 采样帧数 | 1801 | 44342 | — |
| 进程工作集（平均／峰值） | 181.7／189.0 MB | 190.1／192.6 MB | ≤ 2 GB |
| 进程私有内存峰值 | 312 MB | 313 MB | — |
| Unity 托管堆已用 | 3.1 MB | 3.5 MB | — |
| Unity 总分配／总保留 | 105.1／176.6 MB | 105.1／176.6 MB | — |

开发机：Intel Xeon E-2146G、32 GB 内存、NVIDIA Quadro P2000（5 GB），Direct3D 11。播放器日志无异常，Input System 正常初始化（未出现旧输入 `InvalidOperationException`）。这是“原型场景、无 HD-2D 后处理”的基线；最低配置 60 fps 的验证按 ADR 移交 ADR-0005。

**IL2CPP 冒烟发现的问题**：采样脚本最初调用了 `System.Diagnostics.Process`，在 IL2CPP 下运行时报 `NotSupportedException`（Mono 下不会出现），已改为从外部读取进程内存。这正是 ADR 要求每个里程碑出 IL2CPP 冒烟构建的原因。

**Esc 退出**：用脚本向窗口发送合成的 Esc 按键后进程没有退出。合成按键很可能没有进入 Input System 的原始输入通道，因此结论不确定，需要人工确认：启动上面的 exe，按 Esc（手柄按 Start），窗口应立即关闭。
