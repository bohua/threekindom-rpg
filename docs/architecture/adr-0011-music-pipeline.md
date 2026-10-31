# ADR-0011: 配乐生产管线（Suno 半自动 + 官方音乐 API 补量）

## Status

Proposed（用户 2026-10-26 问“如何自动化 Suno 音乐的制作”；方案经 audio-director、producer 评审，producer：APPROVE WITH CHANGES）

## Context

此前三首曲子（`taoyuan_oath`、`scroll_finale`、`victory_aspiration`）是用户在 Suno 网页手工生成、下载，再放进 `Resources/Audio/Music/`。用户想把配乐制作自动化。

调研结论（2026-10-26，来源见共享目录 `design/audio-music-pipeline/SUNO_AUTOMATION_PLAN.md`）：

- Suno 没有公开 API，2026-07 起只对少数合作方试点。
- Suno 服务条款（2026-09-03 版）禁止机器人、抓取和非官方方式访问，并规定“未经官方下载渠道获得的输出不得商用”；下载有月度上限（Pro 20、Premier 60）。
- 因此第三方“Suno API”和网页自动化拿到的曲子不能用在要发售的游戏里。
- ElevenLabs Music、Google Lyria 等有正式 API 和商用条款，可以全自动。
- 用户 2026-10-26 确认：已上线的三首是付费会员时生成，可商用。

## Decision

1. **Suno 只走官方网页和官方下载**，人只做“粘贴提示词、生成、挑选、下载”；提示词卡片由 Claude 按 `design/audio/SOUND_BIBLE.md` 写在 `design/audio/suno-cards/`。主题动机曲、头目战、虎牢关、黄巾主题、片尾走 Suno。订阅 Pro 即可。
2. **器乐补量走官方音乐 API**（首选 ElevenLabs Music，备选 Lyria）。先在各家网页手工 A/B，确认能与 Suno 曲风格并存，再仿 ADR-0009 的 codex-assets 做本机 MCP 桥 `music-assets`（另立 PR）。API key 只放用户电脑环境变量。
3. **收件箱脚本 `tools/music-inbox`** 统一处理两条来源：匹配 cue、找循环点并拆前奏、−16 LUFS／−1.5 dBTP、48 kHz WAV 写进 `Assets/_Project/Resources/Audio/Music/`、接缝试听、授权台账 `design/audio/LICENSE_LEDGER.csv`。不覆盖已有曲目。
4. **Unity**：沿用 `StoryMusic` 的“按名放文件即播放”约定；循环曲若有 `<cue>__intro`，先播前奏，再用 DSP 时钟 `PlayScheduled` 无缝接循环。`MusicImportRules` 在首次导入时设定：Vorbis 0.6、不 normalize；循环段 Streaming，前奏与 `jng_` 常驻内存并预加载。
5. **曲目清单** `design/audio/cue-sheet.csv` 是唯一的曲目来源；新曲 `mus_`、提示音 `jng_` 前缀，已上线三首保持原名。

## Consequences

- 正面：商用权清楚、每首可追溯；用户每首只花挑选的时间（按 5–15 分钟排期）；响度、循环、导入全部一致且可重复。
- 负面：Suno 部分无法无人值守；Suno 与 API 曲可能风格不齐，靠声音圣经和 A/B 控制；Python＋numpy＋ffmpeg 成为用户电脑上的新依赖。
- 未做：对白压低（ducking）与 Music／Jingle／Ambience 混音组、`music-assets` 桥、战斗危急层切换，留给后续 PR。

## Verification

- `python -m pytest tools/music-inbox/tests`：合成音频验证小节对齐的循环、接缝连续、响度与真峰、jingle 去静音、不覆盖、未匹配归档。
- 用户电脑上对已上线三首跑一遍脚本（输出到临时目录）检查真实曲目的循环分数与响度。
- Unity 编译；现有 EditMode／PlayMode 测试保持通过。
