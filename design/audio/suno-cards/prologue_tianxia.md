# prologue_tianxia · 序章「天下大势」片头配乐

用途：新游戏开头的序章（design/gdd/prologue.md），约 3 分 30 秒，**不循环**，游戏从头播到尾，旁白时自动压低到 40%。风格按 SOUND_BIBLE（wt-music-pipeline 分支 design/audio/SOUND_BIBLE.md）统一前缀。

Suno 里的歌名（Title）填：`prologue_tianxia`（收件箱靠它识别）。Custom 模式，Instrumental 打开。

Style：
```
Instrumental. Traditional Chinese orchestral cinematic, pentatonic, bright and heroic, clean modern film mix, no synths, no rock drums, no pop vocals. Historical documentary prologue, slow build. Opens solemn and sparse: guqin, xiao flute, soft low strings, 66 BPM, yu mode. Midway the mood breaks: war drums, gong, suona and pipa tremolo rise as rebellion spreads, tense low string ostinato, 112 BPM. Ends in morning light: bamboo dizi melody over warm strings and guzheng, hopeful, gentle resolution, no fade-out cut.
```

Lyrics：
```
[Intro]
[Instrumental: guqin and xiao, solemn, sparse]
[Verse]
[Instrumental: low strings join, slow and heavy]
[Build]
[Instrumental: war drums enter, suona cry, rising tension]
[Chorus]
[Instrumental: full orchestra, drums and pipa tremolo, urgent]
[Bridge]
[Instrumental: drums drop away, quiet strings, anticipation]
[Outro]
[Instrumental: dizi melody, warm strings, morning light, gentle ending]
[End]
```

Exclude styles：`vocals, lyrics, synth, electric guitar, edm, rock drums`

挑选要点（对应画面）：
- 0:00–1:00 画卷三段（汉室衰微、党锢卖官、天灾流民）：沉郁、留白，不要一开头就打鼓。
- 约 1:00 起天下图黄巾骤起：鼓和唢呐进来，是第一次转折。
- 约 2:00–2:50 群雄登场与三路官军：紧张但不嘈杂，中频别太亮（要给旁白让位）。
- 约 2:50 起刘关张登场卡与涿郡晨光：第二次转折，转为明亮温暖的笛子旋律，最好能听出桃园结义的主题动机；结尾自然收住，不要突然断。
- 长度 3:20–3:50 之间最合适；短一点没关系，游戏结尾会淡出。

下载后：放进收件箱脚本（tools/music-inbox，非循环曲）或直接存成 `Assets/_Project/Resources/Audio/Music/prologue_tianxia.wav`，游戏自动播放，不用改代码。
