# 剧情演出：桃园结义过场、破黄巾后对白、水墨画卷收尾

状态：设计中（2026-10-24 用户提出；设计图待用户确认）。范围属 Alpha 0.1 主线的演出增强，不新增战斗、地图或系统。

## 1. Overview

三项演出：① 桃园三结义改为约 30 秒的 Q 版自动过场，有入园、焚香跪拜、齐诵誓词、碰碗、拜兄等动作和更多对白；② 第三战战胜黄巾后，兄弟三人互相打气、志在天下的对白，随后播放已做好的五幕片头；③ 每段主要剧情结束时的“子弹时间”收尾：画面定格、变灰变暗，展开水墨画卷（三国连环画画风的典故画面＋故事主题），画卷上从右到左、自上而下快速打出不超过 100 字的繁体典故与出处，最后盖红色“完成”印章。三处都预留音乐（Suno 生成）。

## 2. Player Fantasy

桃园一段让玩家“看着”三兄弟结拜，而不是读几行字；画卷收尾像翻完一页连环画，给每段剧情一个郑重、唯美的句号；破黄巾后的豪言把三场教学战接到“天下”的大舞台，片头随之响起。

## 3. Detailed Rules

### 3.1 桃园结义自动过场（约 30 秒）

触发不变（V1_SPEC §2.1：关张都入队后自动开始；状态未开始／播放中／已完成；读档从头重播）。过场按时间轴自动推进，对白按时长自动换行；玩家按确认可提前跳到下一拍，不能整段跳过（沿用 GDD answer 9 的“不可跳过”，只是可加速）。

| 时间 | 动作 | 对白（暂定，简体，与游戏其他对白一致） |
| --- | --- | --- |
| 0–4s | 桃园淡入，三人从园路走到香案后站定 | 旁白：城西桃园，桃花正盛。香案已备，三牲俱全。 |
| 4–7s | 刘备转向二人 | 刘备：二位贤弟，今日焚香告天，结为兄弟。 |
| 7–9.5s | 张飞大笑 | 张飞：痛快！俺盼这一天好久了！ |
| 9.5–12.5s | 关羽拱手 | 关羽：兄长在上，关某愿同生死，共扶汉室。 |
| 12.5–15.5s | 三人持香、同时跪拜，桃花瓣飘落 | 旁白：三人焚香，再拜。 |
| 15.5–19.5s | 跪姿 | 三人：念刘备、关羽、张飞，虽然异姓，既结为兄弟，则同心协力，救困扶危；上报国家，下安黎庶。 |
| 19.5–23.5s | 叩首 | 三人：不求同年同月同日生，但愿同年同月同日死。皇天后土，实鉴此心！ |
| 23.5–26s | 起身、举碗共饮、碰碗 | 刘备：饮此一碗，从今往后，我等便是兄弟！ |
| 26–28.5s | 关张向刘备行礼 | 张飞：大哥在上，二哥在上，受小弟一拜！ |
| 28.5–31s | 镜头拉远，暖色 | 刘备：好！自今日起，我三人同心破贼，共图大业！ |
| 31s | 定格 → 水墨画卷「桃園結義」 | — |

誓词依《三国演义》第一回（公版），其余为本作对白。

### 3.2 破黄巾后的兄弟对白

第三战胜利结算后按“继续”：先播水墨画卷「大破黃巾」，再在战场黄昏背景上播放兄弟对白（对白框＋主立绘），最后一句后淡黑接五幕片头（`opening-assembled-20261021-v1/THREE_KINGDOMS_FANTASY_OPENING_720P.mp4`，约 59 秒，可按取消跳过）。片头后接原有结尾旁白与“测试版结束”。

| 说话人 | 台词（暂定） |
| --- | --- |
| 张飞 | 哈哈！大哥、二哥，黄巾贼被咱们杀得屁滚尿流！ |
| 关羽 | 张角虽败，天下流民未安。此一战，不过是开端。 |
| 刘备 | 云长说得是。黄巾易破，人心难安。我等起兵，本为上报国家、下安黎庶。 |
| 张飞 | 那还等什么！大哥指到哪里，俺的蛇矛就杀到哪里！ |
| 关羽 | 兄长胸怀天下，关某愿执刀相随，万死不辞。 |
| 刘备 | 好！愿我兄弟三人，同心同德，匡扶汉室，还天下一个太平！ |
| 三人 | 志在天下！ |

### 3.3 水墨画卷收尾（子弹时间）

用于每段主要剧情的结束点；本次接入两处：桃园结义完成、三战黄巾胜利。之后的篇章按同一组件增加条目即可。

1. 0–0.5s：当前画面定格（`Time.timeScale = 0`），截图转为冷灰并压暗，中心墨滴晕开。
2. 0.5–1.5s：画卷从中间向左右展开（两端木轴外移），连环画画风的典故画面随之显现。
3. 1.5s 起：画卷右上竖写标题；正文竖排，从右列开始自上而下逐字打出（约 45 字／秒），出处另起一列、字稍小。按确认可立即显示全文。
4. 打完后红色“完成”印章由大到小压下，轻微震动。
5. 显示“▼ 確認”，按确认后淡黑、恢复时间，回到剧情。

文字一律繁体，正文含出处不超过 100 字（自动测试检查）。条目见 `InkScrollText`：

- 桃園結義（86 字）：典出《三國演義》第一回。
- 大破黃巾（91 字）：事本《三國志·先主傳》與《三國演義》第一、二回，戰事為本作改編（V1_SPEC §2.3：三兄弟同场为游戏改编，不写死亡）。

## 4. Formulas

打字：第 n 个字在 `t0 + n / TypeSpeed` 秒显示，`TypeSpeed = 45`。每列 `PerColumn = 13` 字，出处另起一列。

## 5. Edge Cases

- 读档到桃园结义安全阶段：从过场开头重播（原规则），画卷也随之重播一次；结义完成后不再播。
- 画卷期间时间冻结；关闭时恢复到进入前的 timeScale（战斗加速时也不例外）。
- 片头文件缺失（开发机外的构建或未复制）：直接跳过，不报错。
- 音乐文件缺失：静音继续。
- 截图失败（无图形设备的测试环境）：用纯暗底代替定格画面。

## 6. Dependencies

story-events（桃园结义状态机、结尾流程）、game-ui（对白框、统一 UI 风格）、ADR-0006（场景流程）。新增 `StoryCinematics`（剧情→演出的挂钩）、`StoryMusic`（音乐挂钩）、`StoryCinematicsView`（UI Toolkit 全屏层）、`InkScrollText`（繁体条目）。工程需启用内置模块 audio、video。

## 7. Tuning Knobs

`StoryCinematicsView`：FreezeTime 0.5、UnrollTime 1.0、SealTime 0.22、TypeSpeed 45、PerColumn 13。桃园各拍时长见 `TownStoryController.OathBeats`。

## 8. Acceptance Criteria

- 关张入队后自动播放约 30 秒桃园过场，含走入、跪拜、齐诵、碰碗、拜兄；结束后出现「桃園結義」画卷，确认后恢复控制，结义只完成一次。
- 第三战胜利后依次出现「大破黃巾」画卷、兄弟对白、片头（可跳过）、结尾旁白、测试版结束。
- 画卷文字为繁体、从右到左自上而下、每条不超过 100 字；印章“完成”为红色；期间时间冻结、结束后恢复。
- 音乐按下表放入即播放，缺失时不报错。

## 9. 音乐接口与 Suno 提示词

Suno 没有可供本机调用的接口，以下提示词交给用户在 Suno 网页生成；生成后把文件按名放入 `Assets/_Project/Resources/Audio/Music/`（mp3/ogg/wav 均可），无需改代码。

| 文件名（不含扩展名） | 用途 | 目标长度 |
| --- | --- | --- |
| `taoyuan_oath` | 桃园结义过场 | 约 32 秒 |
| `scroll_finale` | 水墨画卷收尾（气势恢宏的英雄赞歌） | 约 15 秒（前 4.5 秒对动画） |
| `victory_aspiration` | 破黄巾后兄弟对白（可选） | 约 30 秒，可循环 |

音效（`Resources/Audio/Sfx/`，可用任意音效库，Suno 不擅长）：`ink_drop` 墨滴、`scroll_unroll` 画卷展开、`seal_stamp` 盖印。

### taoyuan_oath

Style（风格栏）：
```
Instrumental, no vocals. Traditional Chinese orchestral cinematic, 76 BPM, about 32 seconds. Guzheng arpeggios, dizi bamboo flute, xiao, erhu, warm strings, soft taiko and a deep gong. Solemn, sincere brotherhood oath under blooming peach trees, rising to heartfelt warmth and quiet heroic resolve.
```
Song description（编曲说明栏）：
```
0-4s: peach blossoms drifting, light guzheng arpeggio and airy dizi, three heroes walk in.
4-12s: gentle strings under spoken dialogue, a warm melodic motif on erhu, leave room for dialogue.
12-16s: a single deep gong and low xiao as they light incense and kneel; hush.
16-24s: the oath: strings swell slowly, steady soft taiko like a heartbeat, wordless low choir hum, solemn and moving.
24-29s: they rise and share wine: brighter, dizi carries the motif, fuller drums, joyful brotherhood.
29-32s: broad warm resolution on a sustained chord that can be held while the picture freezes; no abrupt ending, no fade to silence before 32s.
```
Exclude（避免）：
```
vocals, lyrics, rap, EDM, electric guitar, modern pop drums, horror, long silence, abrupt ending
```

### scroll_finale

用户 2026-10-25 改为：短音乐，气势恢宏，英雄赞歌（取代原先的静谧古琴版）。

Style：
```
Instrumental, no vocals. Epic heroic Chinese orchestral anthem, short cue about 15 seconds, 90 BPM. Grand brass fanfare, full strings, big taiko and war drums, cymbal swells, suona and dizi carrying a soaring heroic melody, wordless male choir "ah". Majestic, triumphant, a hymn to heroes, like the closing page of a legendary chronicle.
```
Song description：
```
0-0.5s: one deep taiko boom and a rising cymbal swell as the picture freezes.
0.5-1.5s: strings sweep upward as a scroll unrolls.
1.5-3.6s: brass and suona state a bold heroic motif over driving war drums while the story is written.
3.6s: a massive orchestral hit with taiko and gong for a red seal being stamped.
3.6-15s: the full heroic anthem: soaring melody, wordless male choir, broad brass chords, building to a majestic final chord held with natural reverb; no fade-out before the end.
```
Exclude：
```
vocals, lyrics, rap, EDM, electric guitar, rock drums, sad mood, long quiet intro, abrupt cut-off
```

### victory_aspiration（可选）

Style：
```
Instrumental, no vocals. Heroic Chinese orchestral, 100 BPM, about 30 seconds, loopable. Sunset after battle, three sworn brothers vow to bring peace to the realm. Warm strings, French horns, erhu and dizi melody, taiko building, hopeful and determined, leading toward an epic opening theme.
```
Exclude：
```
vocals, lyrics, EDM, rock guitar, sad ending, long silence
```
