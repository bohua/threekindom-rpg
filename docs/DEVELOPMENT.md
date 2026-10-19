# 开发接续与验证记录

更新日期：2026-10-19（按 ADR-0001 的工程目录迁移更新路径）。游戏设计以 `PROJECT.md` 为准；本文件记录开发入口与已有证据，不替代实时检查。架构决定见 `docs/architecture/`（ADR-0001 工程结构，ADR-0005 HD-2D 渲染）。

## Codex 与 Claude Code 共享文档目录

用户明确只共享文档，独立文档目录为：

```text
C:\Users\fanta\Documents\SharedProjects\ThreeKingdomsJRPG-Docs
```

Claude Code Project添加这个独立文档目录。游戏设定唯一正文是其中的 `PROJECT.md`；工程内 `docs/PROJECT.md`只保留入口。共享目录还提供文档专用CLAUDE.md、协作约定和简明状态，不含Unity源码、Assets、依赖、Git仓库或构建文件。本开发接续文档仍留在工程内。

双方在本机读写同一份共享文档，修改前重新读取并合并更新。此安排未配置云端双向同步；上传式云端Project需按其机制更新文件。原Unity工程和大型构建输出位置保持不变，先前将整个工程作为共同共享目录的约定已撤销。

## 技术基线

- Unity `6000.3.26f1`，URP `17.3.0`，Test Framework `1.6.0`，C#。
- 构建目标：Windows x64。两个 Build Profile（`Assets/_Project/Settings/BuildProfiles/`）：`Win64-Dev`（Mono、Development）与 `Win64-Release`（IL2CPP、托管剥离 Low）。命令行：`Unity.exe -batchmode -quit -projectPath <工程> -activeBuildProfile Assets/_Project/Settings/BuildProfiles/Win64-Release.asset -buildWindows64Player <exe>`。
- 输入只用 Input System（Active Input Handling = Input System Package (New)），按键配置在 `Assets/_Project/Settings/GameControls.inputactions`；不得使用旧 `UnityEngine.Input`。
- 原型场景 Glade 使用正交相机、CharacterController 和 3D 碰撞；HD-2D 方案（透视相机、受光像素面片）见 ADR-0005。
- VS Code、C#／Unity 扩展、Git、Python、Blender、Krita、LibreSprite 已在环境搭建阶段准备；在其他机器上需重新检测。
- 2026-10-18 工具链补充：Visual Studio 2022 Community（Unity 与 C++ 桌面工作负载）、jq、Audacity；Git 升级到 2.55、Git LFS 3.7。Aseprite、REAPER 为付费软件，未安装。

### 2026-10-18 新增 UPM 包及理由

版本除 Cinemachine 外均取编辑器 6000.3.26f1 内置清单的推荐版本。

| 包 | 版本 | 理由 |
| --- | --- | --- |
| `com.unity.inputsystem` | 1.20.0 | 正式版手柄／键位重绑定输入；2026-10-19 起为唯一输入后端（ADR-0001 迁移第 4 步） |
| `com.unity.cinemachine` | 3.1.7 | HD-2D 场景镜头与过场；编辑器推荐 2.10.7，按 3.x API 方向选用 registry 最新 3.x |
| `com.unity.timeline` | 1.8.13 | 剧情过场演出 |
| `com.unity.2d.sprite` | 1.0.0 | Sprite Editor 切图 |
| `com.unity.2d.aseprite` | 3.0.2 | 直接导入 .aseprite／.ase 像素素材 |
| `com.unity.nuget.newtonsoft-json` | 3.2.2 | 存档与数据表 JSON 序列化 |

加包后 batchmode 复测：EditMode 3/3、PlayMode 2/2 通过，无编译错误；结果见 `D:\BuildArtifacts\Codex\hd2d-jrpg\toolchain-20261018\` 与 `toolchain-20261018-pm\`。未加入 DOTS、Netcode、Services、IAP、广告；Yarn Spinner、DOTween、UniTask 留到内容阶段。

### 版本控制

- GitHub 私有仓库 `bohua/threekindom-rpg`。png/jpg/tga/psd/kra/exr/hdr/aseprite/ase/fbx/blend/obj/wav/ogg/mp3/flac/ttf/otf/mp4/dll 走 Git LFS。
- `*.unity`、`*.prefab`、`*.asset` 使用 `merge=unityyamlmerge`。该合并驱动写在本地 `.git/config`，新克隆后需重新注册：

```powershell
git config merge.unityyamlmerge.name "Unity SmartMerge (UnityYAMLMerge)"
git config merge.unityyamlmerge.driver "'C:/Program Files/Unity/Hub/Editor/6000.3.26f1/Editor/Data/Tools/UnityYAMLMerge.exe' merge -h -p --force %O %B %A %A"
git config merge.unityyamlmerge.recursive binary
```
- 工具及素材的商业使用条件见 `../LICENSES.md`。Unity Personal 及部分 Microsoft 扩展有资格条件，不能把“免费”理解为无限制。

## 源码入口

项目代码按 ADR-0001 分在 `Assets/_Project/Scripts/<程序集>/`，每个文件夹一个 `ThreeKingdoms.*` 程序集（依赖方向见 ADR-0001 §4）；命名空间暂时仍是 `Jrpg`，新代码用 `ThreeKingdoms.<程序集>`。

| 文件／目录 | 程序集 | 职责 |
| --- | --- | --- |
| `Assets/_Project/Scripts/Core/` | `ThreeKingdoms.Core` | 纯 C# 规则（不引用 UnityEngine），目前为空，战斗核心待 ADR-0002 |
| `Assets/_Project/Scripts/Data/CharacterDefinition.cs` | `ThreeKingdoms.Data` | 角色静态数据 |
| `Assets/_Project/Scripts/Data/SkillDefinition.cs` | `ThreeKingdoms.Data` | 技能静态数据 |
| `Assets/_Project/Scripts/Data/QuestDefinition.cs` | `ThreeKingdoms.Data` | 任务静态数据 |
| `Assets/_Project/Scripts/Data/Definitions.cs` | `ThreeKingdoms.Data` | 运行时战斗状态、回合排序、任务进度（暂放 Data，见 ADR-0001 迁移基线） |
| `Assets/_Project/Scripts/Gameplay/PlayerMover.cs` | `ThreeKingdoms.Gameplay` | 移动输入（Input System）和 CharacterController 碰撞 |
| `Assets/_Project/Scripts/Gameplay/FollowCamera.cs` | `ThreeKingdoms.Gameplay` | 相机跟随 |
| `Assets/_Project/Scripts/Gameplay/DemoEncounter.cs` | `ThreeKingdoms.Gameplay` | 原型遭遇、输入与 IMGUI 界面 |
| `Assets/_Project/Scripts/App/PerfBaselineProbe.cs` | `ThreeKingdoms.App` | 构建版帧时间／内存采样（`-perfBaseline 30 -perfOut <文件>`） |
| `Assets/_Project/Scripts/EditorTools/ProjectSetup.cs` | `ThreeKingdoms.EditorTools` | 缺失时生成原型场景、Windows 构建入口 |
| `Assets/Tests/EditMode/`、`Assets/Tests/PlayMode/` | `ThreeKingdoms.Tests.*` | 自动化测试 |
| `Assets/_Project/Data/` | — | 占位角色（`Characters/`）、技能（`Skills/`）、任务（`Quests/`）资产 |
| `Assets/_Project/Settings/` | — | URP 资产、Volume Profile、按键配置、Build Profile |
| `production/migration/adr-0001-baseline.md` | — | 目录迁移前后基线、测试与性能记录 |

不要把现有 DemoEncounter 直接扩张成全部剧情系统。新需求落实前先确认它的设计状态，并保持静态定义与运行时状态分离。

## 打开与运行

Unity Hub 中项目名为 `hd2d-jrpg`。打开后双击 `Assets/_Project/Scenes/Glade.unity`，点击 Play。

当前原型操作：WASD／方向键／左摇杆移动；靠近史莱姆按 E（手柄 A）开战，战斗中再按 E／A 或点击 Attack 攻击；独立播放器中按 Escape（手柄 Start）退出。该操作方案尚不是正式游戏 UI 规范。

## 自动化命令

从工程根目录运行 PowerShell：

```powershell
.\tools\Invoke-Unity.ps1 -Action Setup
.\tools\Invoke-Unity.ps1 -Action EditMode
.\tools\Invoke-Unity.ps1 -Action PlayMode
.\tools\Invoke-Unity.ps1 -Action Build
```

`Setup` 是场景缺失时的准备步骤，不要为了新设定删除现有场景后重新生成。脚本支持 `-Editor` 和唯一 `-RunId`。同一工程上的 Unity 自动化操作需顺序执行，避免与用户正在编辑的实例抢占工程。

默认编辑器：`C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe`。输出由脚本写到 `D:\BuildArtifacts\Codex\hd2d-jrpg\<run-id>`。

遇到许可证问题时先使用官方工具检查状态。此前官方浏览器登录与 Personal 激活成功后，仅重启 Hub 即恢复界面；这是一条历史恢复记录，不是对未来所有登录故障的根因诊断。不要索取密码、复制凭据、清空许可证或绕过资格限制。

## 已有证据快照（2026-10-18）

| 层级 | 记录结果 | 证据／边界 |
| --- | --- | --- |
| 场景生成／编译 | 成功 | `Glade.unity` 与相关资产已生成；官方运行命令成功 |
| EditMode | 3/3 通过 | 伤害与数据隔离、回合顺序、任务计数 |
| PlayMode | 2/2 通过 | 场景配置与 CharacterController 碰撞 |
| Windows 构建 | 成功 | Windows x64 Mono，退出码 0 |
| 用户手动测试 | 移动、攻击史莱姆、柱子阻挡通过 | 来自用户反馈；未扩展到完整剧情或所有交互 |
| 三国题材美术／剧情 | 尚未完成 | 当前仍是技术占位内容 |
| 正式发布 | 尚未验收 | 无公开发布记录 |

历史证据位置：

- 测试结果：`D:\BuildArtifacts\Codex\hd2d-jrpg\tests-20261018\EditMode.xml` 与 `PlayMode.xml`。
- 构建报告：`D:\BuildArtifacts\Codex\hd2d-jrpg\windows-20261018\VALIDATION.md`。
- 原型播放器：`D:\BuildArtifacts\Codex\hd2d-jrpg\windows-20261018\Windows\MoonlitGlade.exe`。运行时保留旁边的数据目录和依赖。

上述是历史快照，不代表新代码自动通过。改动后按影响运行必要测试，并单独报告实际结果。
