# music-inbox

把下载好的配乐（Suno 官方下载，或官方音乐 API 的输出）变成游戏里能直接用的曲目。决策见 `docs/architecture/adr-0011-music-pipeline.md`。

## 用法（Windows）：半自动 Suno

在仓库根目录的 PowerShell 里运行：

```powershell
.\tools\music-inbox\Start-SunoSession.ps1                        # 监视你的“下载”文件夹
.\tools\music-inbox\Start-SunoSession.ps1 -Downloads D:\Suno     # 或者 Chrome 保存到的任何文件夹
```

它会：
1. 生成并打开 `D:\BuildArtifacts\Codex\hd2d-jrpg\audio\suno-cards.html`：每张卡片的 Title、Style、Lyrics、Exclude 都有“复制”按钮，已入库的卡片变灰。
2. 开始监视下载文件夹：文件名或歌名里带曲目编号（如 `mus_battle_boss`）的音频一下载完就移进 `audio\_inbox\` 并处理，结果用中文打印在窗口里。其他下载不受影响；Chrome 未下完的 `.crdownload` 会等它下完。
3. 也可以在 Chrome 设置 → 下载内容 → 位置，直接设成 `audio\_inbox`，再用 `-Downloads` 指向同一个文件夹，脚本就在原地处理。

你在 suno.com 上自己操作：Custom 模式 → 逐栏复制粘贴 → Create → 听两个候选 → 用官方下载按钮下载挑中的那首。不要用脚本或扩展去操作 Suno 网页（条款禁止，且会让商用权不清楚）。

只想手动跑一次：

```powershell
python tools/music-inbox/music_inbox.py `
  --inbox D:\BuildArtifacts\Codex\hd2d-jrpg\audio\_inbox `
  --cue-sheet design/audio/cue-sheet.csv `
  --out Assets/_Project/Resources/Audio/Music `
  --work D:\BuildArtifacts\Codex\hd2d-jrpg\audio `
  --ledger design/audio/LICENSE_LEDGER.csv
```

需要 Python 3.10+、`pip install numpy`、ffmpeg（`winget install Gyan.FFmpeg`）。

## 它做什么

- 按文件名或歌名标签匹配 cue_id（最长的完整词优先）；对不上的移到 `_unmatched`。
- 循环曲：在“满音量”的部分里找跳转最平滑的一对点（cue-sheet 给了 BPM 就对齐小节），接缝做 80 ms 等功率交叉淡化，拆成 `<cue>__intro.wav`（只播一次）和 `<cue>.wav`（循环）。`StoryMusic.Play(cue, loop: true)` 会自动先播前奏再无缝接循环。cue-sheet 填了 `loop_start_s`／`loop_end_s` 就直接用人工点。
- 非循环曲（jingle、过场）：去掉首尾静音。
- 响度：同一个增益让循环段到 −16 LUFS；如果会超过真峰 −1.5 dBTP，就整体调小（报告里 `peak_limited: true`）。
- 输出 48 kHz 16 位 WAV；`_review\<时间>\` 里有每首的接缝试听（循环尾 5 秒＋循环头 5 秒）和 `report.json`；`loop_score < 0.90` 标 `needs_review`。
- 在授权台账追加一行（引擎、账号档位、下载渠道、日期、文件哈希），原文件移进 `_archive\<cue>\`。
- 已存在的曲目不覆盖，除非加 `--replace`。

## 测试

```
python -m pytest tools/music-inbox/tests
```
