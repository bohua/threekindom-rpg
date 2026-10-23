<!-- PROTOTYPE - NOT FOR PRODUCTION -->
<!-- Question: 空轨式 AT 行动条＋小范围格子战斗，配合“学派回路镶嵌兵书→策略”，是否好玩、是否看得懂？ -->
<!-- Date: 2026-10-22 -->
# 兵书回路·半即时战棋 原型（spike）

**不并入首版主线。** 分支 `prototype/bingshu-tactics-poc`；代码在 `Assets/Prototypes/BingshuTactics/`（独立程序集，不引用 `_Project` 正式代码，只引用刘关张、黄巾、张角的像素图与胸像）。设计规则见 `design/gdd/bingshu-system.md` 第 14 节。

## 运行

- 构建：`D:\BuildArtifacts\Codex\hd2d-jrpg\builds\bingshu-tactics-poc\BingshuTacticsPOC.exe`
- 编辑器：菜单 `Prototype/兵书战棋原型/生成场景`，打开 `Assets/Prototypes/BingshuTactics/BingshuTactics.unity` 运行。
- 自检：`BingshuTacticsPOC.exe -poc-shots <目录>` 自动截图各界面，再由 AI 代双方打完一场后退出（日志里有 `POC_AUTOPLAY_RESULT`）。

## 操作（鼠标、键盘、手柄都可以，2026-10-23）

| 动作 | 键盘 | 手柄 | 鼠标 |
| --- | --- | --- | --- |
| 移动地图光标 | 方向键／WASD | 十字键／左摇杆 | 指向格子 |
| 确认 | 回车／空格／Z | A | 左键（环形菜单单击直接触发） |
| 返回 | Esc／退格／X | B | 右键 |
| 转动环形菜单、选列表 | ↑↓ | 十字键／摇杆上下 | 点选 |
| 旋转镜头 | Q／E | LB／RB | — |
| 布阵：打开兵书回路／出阵／换关 | Tab／F／T | Y／Start／Select | 点按钮 |
| 兵书回路：换武将／换学派／卸下 | Q E／Tab／C 或 Delete | LB RB／Y／X | 点选 |
| 分出胜负后重来 | R 或回车 | A 或 Start | — |

光标方向随镜头换算：屏幕上的“上”总是对应画面里斜上方的那条格线。

## 参考图

`D:\BuildArtifacts\Codex\hd2d-jrpg\refs\battle-poc\`：ref1 零之轨迹环形指令、ref2 空之轨迹格子范围、ref3 空之轨迹导力器回路。
