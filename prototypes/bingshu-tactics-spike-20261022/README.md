<!-- PROTOTYPE - NOT FOR PRODUCTION -->
<!-- Question: 空轨式 AT 行动条＋小范围格子战斗，配合“学派回路镶嵌兵书→策略”，是否好玩、是否看得懂？ -->
<!-- Date: 2026-10-22 -->
# 兵书回路·半即时战棋 原型（spike）

**不并入首版主线。** 分支 `prototype/bingshu-tactics-poc`；代码在 `Assets/Prototypes/BingshuTactics/`（独立程序集，不引用 `_Project` 正式代码，只引用刘关张、黄巾、张角的像素图与胸像）。设计规则见 `design/gdd/bingshu-system.md` 第 14 节。

## 运行

- 构建：`D:\BuildArtifacts\Codex\hd2d-jrpg\builds\bingshu-tactics-poc\BingshuTacticsPOC.exe`
- 编辑器：菜单 `Prototype/兵书战棋原型/生成场景`，打开 `Assets/Prototypes/BingshuTactics/BingshuTactics.unity` 运行。
- 自检：`BingshuTacticsPOC.exe -poc-shots <目录>` 自动截图各界面，再由 AI 代双方打完一场后退出（日志里有 `POC_AUTOPLAY_RESULT`）。

## 操作

| 场合 | 操作 |
| --- | --- |
| 布阵 | 点我方武将，再点左侧蓝格换位置；「兵书回路」配书；「出阵！」开战 |
| 兵书回路 | 左侧选武将、切换学派；点回路槽位，再点右侧兵书镶嵌；再点一次已镶嵌槽卸下；右侧即时显示点亮的策略和威力倍率 |
| 环形菜单 | 鼠标点选（再点一次确定），或 ↑↓ 转动、回车确定 |
| 选目标 | 蓝＝射程／可达，橙＝效果范围；直线策略点相邻格定方向，自身范围技点自己 |
| 通用 | 右键／Esc 返回；Q/E 旋转镜头；分出胜负后按 R 重来 |

## 参考图

`D:\BuildArtifacts\Codex\hd2d-jrpg\refs\battle-poc\`：ref1 零之轨迹环形指令、ref2 空之轨迹格子范围、ref3 空之轨迹导力器回路。
