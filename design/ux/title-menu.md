# 标题画面（开始菜单）UX 规格

状态：v2 已实装（2026-10-26），用户尚未试玩。需求来源：用户 2026-10-26 “现在我们把游戏的start menu做一下，让codex出一个类似于最终幻想的start menu，毕竟我们的logo就是这么设计的”；v2 改版原话：“把上面的 logo 做的再小一点，然后主菜单直接做到正当中。当点击主菜单的时候，它如果有副菜单，那么就把主菜单做一个动画拉到左面，然后把当中的部分留给二级菜单。如果是比如说新开始游戏，它没有二级菜单的情况下，那么就直接点击之后几次闪烁开始进入，并且有那种最终幻想进入游戏的音效。”用户看过 v2 设计图后确认“可以了，按照这个来”。

## 1. 范围

- 替换原深色纯文字标题，改为《最终幻想》式明亮白底：上部居中的「三国志 Fantasy」正式 logo，正中一列菜单，白色手套光标。
- 只改标题场景（`Title.unity`、`TitleHud`、`TitleWhiteFade`、`TitleController`）。不改存档格式与读档规则（ADR-0004）、行旅菜单、城镇或战斗场景。
- 统一 UI 风格（UI_STYLE_GUIDE，苍蓝银辉）仍是游戏内界面基线；标题按用户要求用白底，面板细框、青蓝选中借用统一风格的色彩语言。

## 2. 菜单项

| 项 | 行为 | 可用条件 |
| --- | --- | --- |
| 继续游戏 | 闪烁进白场，读取 `savedAt` 最新的可读存档（自动档＋手动 1–3） | 有可读存档，否则置灰 |
| 新的游戏 | 闪烁进白场，`NewGame()` 进入涿郡 | 总是 |
| 读取存档 | 主菜单左滑，中间出存档面板（4 张固定卡片，空位置灰，损坏／版本不符显示原因且不改文件） | 有任一存档文件，否则置灰 |
| 设置 | 主菜单左滑，中间出设置面板：音乐、音效（←→ 10 一档）、文字速度、全屏、恢复默认；与行旅菜单“系统”页共用 `GameSettings` | 总是 |
| 退出游戏 | `Application.Quit()` | 总是 |

默认焦点：有存档在“继续游戏”，否则“新的游戏”。取消键在面板里返回主菜单（焦点回到入口项），在主菜单不做事。

## 3. 动画与音效（按 Codex v2 DESIGN_NOTES）

- 左滑：菜单中心 x 960→240，320 ms easeOutCubic；返回 220 ms。细线由左右对称渐隐变为向右渐隐（x 150–420）。
- 面板：点击后 100 ms 开始，从右 24 px 处淡入，240 ms；返回 160 ms 淡出右移 16 px。面板到位前不显示光标。
- 进入游戏：3 次闪烁（0–80、180–260、360–460 ms，入口字发青白光＋光带＋35% 白屏），再 260 ms 升到全白，保持 120 ms 后切场景；新场景里白色 600 ms easeOutCubic 淡出（`TitleWhiteFade`，IMGUI 盖在所有 UI 上）。
- 音效：`Resources/Audio/Sfx/title_start.wav`，程序合成的原创上行琶音＋水晶钟声约 2 秒，不引用任何现有游戏旋律。标题音乐 `Resources/Audio/Music/title_theme` 预留（文件不存在时静音），进入游戏时淡出。

## 4. 美术

- 设计图：D:\BuildArtifacts\Codex\hd2d-jrpg\art\title-menu-concept（v1）、title-menu-concept-v2（v2，现行）。
- `Resources/TitleMenu/title_plate.png`：背景与 logo 按设计脚本（compose.py 的 background()+place_logo(1120, 5)）预合成，避免运行时混合冲淡 logo 蓝色。
- `title_cursor.png`：设计稿原创白手套 144×88（显示 72×44）。
- `title_serif.ttf`：Noto Serif SC（SIL OFL）字重 500 的固定词子集，只含标题用到的字；系统可变字体在 Unity 中不显示。

## 5. 验收

- 有／无存档两种状态默认焦点与置灰正确；继续游戏读取最新存档（PlayMode TitleMenuTests）。
- 读取面板 4 张卡、损坏档提示且不改文件（SaveFlowTests）。
- 设置面板显示行旅菜单同一组设置值。
- 键鼠、手柄可操作；光标跟随焦点。
- Chapter1 构建从新标题进入涿郡；截图 builds/title-menu-shots（`-titleShots <dir>`）。
