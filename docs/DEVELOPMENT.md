# 开发接续与验证记录

更新日期：2026-10-18。游戏设计以 `PROJECT.md` 为准；本文件记录开发入口与已有证据，不替代实时检查。

## Codex 与 Claude Code 共享文档目录

用户明确只共享文档，独立文档目录为：

```text
C:\Users\fanta\Documents\SharedProjects\ThreeKingdomsJRPG-Docs
```

Claude Code Project添加这个独立文档目录。游戏设定唯一正文是其中的 `PROJECT.md`；工程内 `docs/PROJECT.md`只保留入口。共享目录还提供文档专用CLAUDE.md、协作约定和简明状态，不含Unity源码、Assets、依赖、Git仓库或构建文件。本开发接续文档仍留在工程内。

双方在本机读写同一份共享文档，修改前重新读取并合并更新。此安排未配置云端双向同步；上传式云端Project需按其机制更新文件。原Unity工程和大型构建输出位置保持不变，先前将整个工程作为共同共享目录的约定已撤销。

## 技术基线

- Unity `6000.3.26f1`，URP `17.3.0`，Test Framework `1.6.0`，C#。
- 首个构建目标：Windows x64 Mono。其他平台和 IL2CPP 尚未配置为交付目标。
- 现有工程使用正交相机、SpriteRenderer、CharacterController 和 3D 碰撞。
- VS Code、C#／Unity 扩展、Git、Python、Blender、Krita、LibreSprite 已在环境搭建阶段准备；在其他机器上需重新检测。
- 工具及素材的商业使用条件见 `../LICENSES.md`。Unity Personal 及部分 Microsoft 扩展有资格条件，不能把“免费”理解为无限制。

## 源码入口

| 文件／目录 | 职责 |
| --- | --- |
| `Assets/Game/Runtime/CharacterDefinition.cs` | 角色静态数据 |
| `Assets/Game/Runtime/SkillDefinition.cs` | 技能静态数据 |
| `Assets/Game/Runtime/QuestDefinition.cs` | 任务静态数据 |
| `Assets/Game/Runtime/Definitions.cs` | 运行时战斗状态、回合排序、任务进度 |
| `Assets/Game/Runtime/PlayerMover.cs` | 移动输入和 CharacterController 碰撞 |
| `Assets/Game/Runtime/FollowCamera.cs` | 相机跟随 |
| `Assets/Game/Runtime/DemoEncounter.cs` | 原型遭遇、输入与 IMGUI 界面 |
| `Assets/Game/Editor/ProjectSetup.cs` | 缺失时生成原型场景、Windows 构建入口 |
| `Assets/Game/Tests/` | EditMode 和 PlayMode 自动化测试 |
| `Assets/Game/Data/` | 当前占位角色、技能和任务资产 |

不要把现有 DemoEncounter 直接扩张成全部剧情系统。新需求落实前先确认它的设计状态，并保持静态定义与运行时状态分离。

## 打开与运行

Unity Hub 中项目名为 `hd2d-jrpg`。打开后双击 `Assets/Game/Scenes/Glade.unity`，点击 Play。

当前原型操作：WASD／方向键移动；靠近史莱姆按 E；点击 Attack 战斗；独立播放器中按 Escape 退出。该操作方案尚不是正式游戏 UI 规范。

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
