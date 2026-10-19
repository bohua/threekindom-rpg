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
