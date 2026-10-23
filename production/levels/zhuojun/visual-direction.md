# 涿郡城镇环境贴图｜视觉方向与出图计划

日期：2026-10-23　角色：art-director（编排运行）　状态：**建议**，待用户确认后才派发 Codex；本文不生成图片、不改代码。

目标：涿郡环境贴图向用户两张参考图靠近到约 90%。人物维持 V3 128 Q 版不动，本文只管环境贴图。

- 参考图 A（全城概念图）：`C:\Users\fanta\.claude\uploads\63260ae0-7de7-4eee-9081-c440c41ecc50\71a10f5f-image.png`
- 参考图 B（民居风格，**本轮主要风格依据**）：`C:\Users\fanta\.claude\uploads\63260ae0-7de7-4eee-9081-c440c41ecc50\7e008526-image.png`
- 现状截图：`D:\BuildArtifacts\Codex\hd2d-jrpg\screenshots\{houses,market,barracks,casino}-codex-20261023.png`
- 现有贴图：`Assets/_Project/Art/Environment/ZhuojunTown/`、`.../ZhuojunCorner/`（manifest.json 为尺寸/平铺轴依据）

## 0. 不可越过的边界（历史教训）

2026-10-23 用户否决“歧路旅人级精修”：冷环境光、SSAO、法线读起来“又暗又脏”，60 px/m 新图“太锐、颜色不平衡”。因此本计划：

1. **不改后处理/LUT/灯光**，不加法线图、高度图、AO 图，不提高像素密度。保持 **30 px/m** 与现有暖色 LUT。
2. 只靠贴图本身的**造型、配色、明度分层和细节**接近参考图。
3. 继续用 corner 的 **48 色原表**（`ZhuojunCorner/palette.png`，town 引用同一张）。已核对：参考图 B 需要的炭灰瓦（#242c31 / #41494b / #3c4d52）、青苔绿（#4c5940 / #69734d / #879068）、窗内暖光（#e5a34b / #f3cf7a）全部在原表内，**无需扩色**。
4. 二值透明（alpha 仅 0/255）；建筑立面正投影、道具约 30° 斜视；**不烘焙定向日光和地面投影**，只保留材质局部体积（瓦缝、石缝、木纹凹处的暗色）。
5. 同名同尺寸替换，代码 `Metres()` 从贴图尺寸推算建筑大小，**尺寸一变建筑就变形**，所以全部 P0 维持原尺寸。

## 1. 现状 vs 参考：分项差距评估

近似度为美术总监目测估计，基于四张现状截图与参考图 A/B 对照，不是测量值。

| 分项 | 现状 | 参考图 | 主要差距 | 近似度 |
| --- | --- | --- | --- | --- |
| 屋顶 | `roof_tiles_grey` 主色 #56666a 偏青蓝，高光 #92a1a0 亮且碎，截图里读成“蓝绿色铁皮”；脊端 `roof_ridge_end` 是细长龙吻；檐口勾头一排小圆点很碎。**20 栋中约 11 栋用 `roof_thatch`**（house_b、house_c、stable 等），茅草贴图在截图里是满屏高频稻草噪点 | B：深炭灰筒瓦、粗壮瓦垄、零星青苔、厚实叠瓦屋脊、方盒形脊端。A：除粮仓外几乎全是灰瓦 | 颜色冷艳而非炭灰；瓦垄细、高光多；脊端造型错误；茅草比例过高 | **45%** |
| 墙体 | `house_front` 已有夯土＋木构框架，较接近；`house_b_front` 白灰墙斑驳块面大、显脏；`house_c_front` 缺立柱；`wall_rammed_earth` 横纹偏平 | B：暖米黄夯土、粗糙抹面与细裂纹、深褐立柱横梁明确分格 | 三套民居风格不统一；立柱/横梁不够粗、不够深 | **65%** |
| 门窗 | `house_front` 已有双扇木门铁环、方格窗 | B：门更厚重带铁件，方格窗内发暖光 | 窗内暖光缺失（这是参考图 B 最醒目的情绪点）；house_b/c 是单扇门和铁栅小窗 | **70%** |
| 墙基 | `stone_plinth` 及立面底部是规整灰石条 | B：大小不一的毛石，灰绿混杂，有青苔 | 太规整、色太单 | **65%** |
| 地面道路 | `ground_packed_earth` 覆盖全城 92×68 m，单像素刮痕噪点、4 m 重复明显、饱和橙；`ground_street_stone` 是大圆卵石 | B：方整石板路，边缘碎石过渡；A：柔和暖土路＋石板主轴 | 土路太“糙”、太橙，是全屏单色感的主因；石板形状不对 | **50%** |
| 植被 | 槐/榆/桃树贴图质量可用，但城内数量少；灌木 1 种、无草丛团块 | A：城内树木成片、墙根灌木与草丛点缀，绿色占画面 15–20% | 主要是**缺品种与摆放密度**，不全是贴图问题 | **35%** |
| 城墙城门 | 夯土墙＋垛口＋平梁门洞已有；截图前景城墙被景深糊成褐色块 | A：夯土墙、石基、墙顶灰瓦压顶、角楼/门楼灰瓦顶 | 墙顶缺瓦压顶；无角楼；城门楼屋顶随 roof_tiles_grey 一起改即可 | **60%** |
| 色彩 | 屋顶冷青蓝 vs 地面/茅草饱和橙，中间缺层次；全屏明度集中在中亮橙 | 明度三层：深炭灰屋顶（暗）—米黄墙（中）—浅暖土石路（亮）＋绿植点缀；暖逆光来自 LUT 而非贴图 | 需要**屋顶压暗、去青**，**地面降饱和、提明度、降噪**，增加绿 | **50%** |

**整体约 55%。** 预测：P0 两批完成＋把多数民居改回瓦顶（见风险 R1）→ 约 80–85%；P1 完成 → 约 88–90%；剩余差距主要来自摆放密度与概念图特有的大俯角构图，贴图无法单独解决。

## 2. 新出图清单

约定：沿用现有 key 名以便代码直接替换；尺寸 = 现有同名贴图尺寸；透明 = 二值。“新 key”需要程序员接一行代码，已单独标注。

### P0（达到 90% 必需，12 张，2 批）

| # | 文件名 | 原生尺寸 | 透明 | 平铺轴 | 要点 |
| --- | --- | --- | --- | --- | --- |
| 1 | `roof_tiles_grey.png`（corner） | 120×120 | 不透明 | xy | 深炭灰筒瓦＋板瓦，**每米约 2 条瓦垄（每垄约 15 px）**，比现在更粗；主色 #41494b/#3c4d52，暗缝 #242c31/#181c20，高光只用 #56666a、少量 #69716d，**不用 #92a1a0 及以上**；约 8–12% 面积零星青苔 #4c5940/#69734d，团块 2–4 px；四边无缝 |
| 2 | `roof_eave.png`（corner） | 120×18 | 下缘二值透明 | x | 檐口一排瓦当，圆头**大而少**（每 15 px 一个，与瓦垄对齐），深炭灰，去掉现有细碎花纹 |
| 3 | `roof_ridge.png`（corner） | 120×24 | 上缘二值透明 | x | 厚实叠瓦正脊：2–3 层平瓦叠砌＋顶部一条筒瓦压顶，炭灰；零星青苔 |
| 4 | `roof_ridge_end.png`（corner） | 24×36 | 二值透明 | 无 | 按参考图 B 改为**方盒形脊端**（叠砌方墩、顶部微出挑），去掉龙吻；朴素民居可用，郡署若要鸱吻另立 key（P2） |
| 5 | `ground_packed_earth.png`（corner） | 120×120 | 不透明 | xy | 柔和夯实土路：主色 #b08b61/#af9472，暗 #967a5b，亮 #c4ae88；**低频大色块（8–20 px）**，零星小石子 2–3 px，极淡车辙；**禁止单像素刮痕噪点**；整体比现在降饱和、略提亮；4 m 重复不可见明显图案 |
| 6 | `ground_street_stone.png`（corner） | 120×120 | 不透明 | xy | 参考图 B 的**方整石板**：每块约 0.5–0.8 m（15–24 px），错缝排列，圆角；暖米灰 #c9bfa0/#b9b5a2/#a69a7d，缝隙 #79634f；缝中零星青苔/草点；四边无缝 |
| 7 | `house_front.png`（corner） | 180×90 | 不透明（或轮廓外二值透明，同现状） | 无 | 以参考图 B 为本：粗深褐立柱 3 根＋上横梁分格；夯土抹面带细裂纹；双扇木门＋铁环＋铁箍；方格木窗，**格内填 #e5a34b/#f3cf7a 暖色表示室内灯光**（这是物体颜色，不是方向光）；底部毛石墙基约 0.5 m；**门、窗像素位置保持与现图一致** |
| 8 | `house_side.png`（corner） | 150×159 | 山尖外二值透明 | 无 | 夯土山墙＋木构三角框架（中柱、横梁）；山尖上沿一条炭灰瓦边与新屋顶同色；毛石墙基；可加靠墙竹席轮廓但不画投影 |
| 9 | `house_b_front.png`（town） | 180×90 | 同现状 | 无 | 去掉大块斑驳白灰，改为**米白灰抹面＋露木构**，与 house_front 同一建造语言但换开间节奏（门居右、两扇窗）；方格窗同样填暖色 |
| 10 | `house_c_front.png`（town） | 150×90 | 同现状 | 无 | 小屋：补两侧立柱与横梁，单扇厚木门带铁件，小方格窗填暖色；毛石墙基 |
| 11 | `wall_rammed_earth.png`（corner） | 120×120 | 不透明 | xy | 背墙与城内无立面墙用：暖米黄夯土，版筑横层 2–3 条（每层约 0.6 m），细裂纹、补土块；低频；比现在少一点横向条纹 |
| 12 | `stone_plinth.png`（corner） | 120×12 | 不透明 | x | 毛石墙基条：大小不一的块石，灰 #8f9993/#718080 与暖灰 #a69a7d 交错，缝中青苔点 |

### P1（从约 83% 推到约 90%，12 张，2 批）

| # | 文件名 | 原生尺寸 | 透明 | 平铺轴 | 要点 |
| --- | --- | --- | --- | --- | --- |
| 13 | `house_b_side.png` | 150×159 | 山尖外透明 | 无 | 与 #9 同语言；山尖上沿炭灰瓦边（配合 R1 改瓦顶） |
| 14 | `house_c_side.png` | 120×144 | 同上 | 无 | 与 #10 同语言 |
| 15 | `house_back.png` | 180×90 | 同现状 | 无 | 背立面：夯土＋立柱，一扇小方格窗（不填暖色亦可） |
| 16 | `house_b_back.png` | 180×90 | 同现状 | 无 | 同上 |
| 17 | `house_c_back.png` | 150×90 | 同现状 | 无 | 同上 |
| 18 | `roof_thatch.png` | 120×120 | 不透明 | xy | 仅粮仓/马厩保留时用：**降低噪点**，成束茅草 6–10 px 一束，每 12 px 一道压条，颜色降饱和偏 #af9472/#967a5b |
| 19 | `ground_transition.png`（corner） | 120×120 | 不透明 | xy | 新石板 ↔ 新土路过渡，边缘散落碎石 |
| 20 | `ground_earth_to_grass.png` | 120×120 | 不透明 | xy | 新土路 ↔ 草地过渡，与 #5 同色 |
| 21 | `grass_edge.png`（corner） | 120×24 | 上缘透明 | x | 墙根草更饱满成团，夹少量小黄花 |
| 22 | `citywall_face.png` | 120×150 | 不透明 | x | 版筑层更清楚，底部 0.5 m 毛石基与 #12 同语言 |
| 23 | `courtyard_wall_cap.png` | 120×24 | 透明边 | x | 改炭灰小瓦压顶，与新屋顶统一（郡署、院墙） |
| 24 | `house_window_glow_mask.png`（**新 key**） | 180×90 | 二值黑白 | 无 | 对齐 #7 方格窗暖色像素的自发光遮罩；`Building()` 已有 `glowMask` 参数，程序员只需传参。b/c 两版可后续补 |

### P2（锦上添花，按需）

| 文件名（均为新 key） | 建议尺寸 | 要点 |
| --- | --- | --- |
| `citywall_cap_tiles.png` | 120×18，x 平铺 | 城墙顶灰瓦压顶条（参考图 A） |
| `corner_watchtower_front/side.png` | 150×120 / 120×150 | 城墙四角小角楼，灰瓦顶走通用屋顶 |
| `prop_tree_cypress.png` | 60×120 | 深绿柏树，竖向轮廓，打破圆树冠重复 |
| `prop_bush_b.png`、`prop_grass_tuft.png` | 48×30、24×15 | 灌木第二款、草丛团块，用于墙根和路边 |
| `prop_awning_canopy.png` | 120×75 | 军营/市集的单坡布篷（参考图 A 军营） |
| `prop_bamboo_mat_lean.png` | 30×54 | 参考图 B 墙边斜靠竹席（若 `prop_straw_mats` 不够像） |
| `roof_ridge_end_chiwen.png` | 30×42 | 郡署/门楼专用鸱吻，民居继续用方盒脊端 |

### 分批建议（每批约 6 张，按 Codex 桥预算）

| 批次 | 内容 | 理由 |
| --- | --- | --- |
| **第 1 批（P0）** | #1 roof_tiles_grey、#2 roof_eave、#3 roof_ridge、#4 roof_ridge_end、#5 ground_packed_earth、#6 ground_street_stone | 屋顶＋地面占屏面积最大，一批就能把明度三层（暗顶—中墙—亮地）建立起来，收益最高；全是平铺/小件，风险低 |
| **第 2 批（P0）** | #7 house_front、#8 house_side、#9 house_b_front、#10 house_c_front、#11 wall_rammed_earth、#12 stone_plinth | 统一民居建造语言，落实参考图 B 的门窗、木构与毛石墙基 |
| 第 3 批（P1） | #13–#18 | 侧/背立面与茅草收尾，需在 R1 决定后做 |
| 第 4 批（P1） | #19–#24 | 过渡地面、墙根草、城墙与窗光遮罩 |
| 第 5 批（P2） | 按需挑 6 张 | — |

每批验收后先在 Unity 里截同四个机位对比，再决定下一批，避免重演一次性大改被否决。

## 3. Codex brief 草稿

以下为草稿，派发时按 CODEX_ASSET_BRIDGE.md 流程走：默认只出提示词，**生图须用户同意并记录原话**；批准后复制到 `art/` 下新文件夹，不覆盖旧资源，Unity 导入另做。

### 通用规范段（每批 brief 开头都贴）

```
Project: Three Kingdoms HD-2D JRPG, Zhuojun town environment textures (rework).
Style authority: reference B (attached: 7e008526-image.png) — late Eastern Han
northern Chinese village house: deep charcoal-grey cylindrical roof tiles with
sparse moss, heavy stacked ridge with box-shaped ridge ends, warm rammed-earth
walls with dark-brown timber posts and beams, rough rubble-stone plinth,
thick double wooden doors with iron rings, square lattice windows glowing warm
from inside, square stone-slab paving, wooden fences, water vat, firewood.
Layout reference: reference A (attached: 71a10f5f-image.png), mostly grey-tile roofs.

HARD SPECS (do not deviate):
- Scale 30 px = 1 m. Output EXACT native size listed per file. Do NOT author at
  60 px/m or upscale; if you generate larger, downsample nearest-neighbour and
  re-quantize. The previous 60 px/m attempt was rejected as "too sharp".
- Palette: exactly the 48-colour palette ZhuojunCorner/palette.png (attached).
  Every pixel must be one of those 48 colours. No new colours.
- Alpha: binary only (0 or 255). No soft edges, no anti-aliasing to transparent.
- Building façades: strict orthographic front view. Props: ~30° top-down.
- Do NOT bake directional sunlight, rim light, cast shadows or ground shadows.
  Only local material volume (darker tile gaps, stone joints, wood grain recesses).
  Lighting/LUT are handled in engine and must stay warm and soft.
- No normal / height / AO maps.
- Pixel clusters 2–4 px minimum for texture detail; avoid single-pixel speckle noise
  (current ground reads as scratchy noise — fix that).
- Tileable files must wrap seamlessly on the listed axes (check 3×3 repeat).
- Keep door/window pixel positions identical to the existing file of the same name
  (attached as layout template) — gameplay interaction points depend on them.
- Overall value hierarchy target: roofs dark, walls mid, ground light, greens as accent.

Deliverables per file: native PNG, 4× nearest preview, 3×3 tile-repeat check for
tileables, palette/alpha/size check log, the exact prompt used.
```

### 第 1 批 brief（屋顶＋地面）

```
Batch 1 — roofs and ground (6 files). Attach: ref B, ref A, palette.png, and the
current file of each name as size template.

1. roof_tiles_grey.png 120×120, tile xy. Charcoal cylindrical + flat tile roof,
   about 2 tile ridges per metre (~15 px per ridge), chunkier than current.
   Main #41494b/#3c4d52, gaps #242c31/#181c20, highlights only #56666a and a
   little #69716d; never use #92a1a0 or lighter. 8–12 % sparse moss in 2–4 px
   clumps (#4c5940, #69734d). Must NOT read as blue-green metal.
2. roof_eave.png 120×18, tile x, transparent below the tile ends. One row of
   round tile-end caps, large and few (one per ~15 px, aligned to #1 ridges),
   charcoal; remove the fine ornament pattern.
3. roof_ridge.png 120×24, tile x, transparent above. Heavy main ridge: 2–3 courses
   of stacked flat tiles capped by one cylindrical tile, charcoal, sparse moss.
4. roof_ridge_end.png 24×36, no tiling, binary alpha. Box-shaped ridge end as in
   ref B (stacked square block, slight top overhang). No dragon / chiwen.
5. ground_packed_earth.png 120×120, tile xy. Soft compacted earth road covering
   most of the town. Main #b08b61/#af9472, darks #967a5b, lights #c4ae88.
   Low-frequency patches 8–20 px, a few 2–3 px pebbles, very faint wheel ruts.
   No single-pixel scratches. Slightly less saturated and lighter than current.
   No obvious repeating motif at 4 m.
6. ground_street_stone.png 120×120, tile xy. Square-ish stone slabs as in ref B,
   each 15–24 px, staggered joints, rounded corners. Warm grey-beige
   #c9bfa0/#b9b5a2/#a69a7d, joints #79634f, occasional moss/grass dots in joints.
```

### 第 2 批 brief（民居立面）

```
Batch 2 — house walls (6 files). Attach: ref B, palette.png, current files as
layout templates, and batch-1 approved roof_tiles_grey for colour matching.

7. house_front.png 180×90 (6×3 m), orthographic. Follow ref B: three thick dark-
   brown posts + top beam; rammed-earth plaster with fine cracks; thick double
   door with iron rings and straps; square lattice window whose openings are
   filled with #e5a34b/#f3cf7a (warm interior light as surface colour, not a
   light source); rubble-stone plinth ~0.5 m (15 px). Keep door and window at the
   same pixel positions as the current file.
8. house_side.png 150×159 (5 m deep, 3 m wall + 2.3 m gable), binary alpha
   outside the gable. Rammed-earth gable with timber triangle frame (king post,
   tie beam), a charcoal tile edge along the gable top matching #1, rubble plinth.
9. house_b_front.png 180×90. Same building language as #7 but off-white lime
   plaster with exposed timber (remove the big blotchy patches); door on the
   right, two lattice windows with warm fill. Keep existing door position.
10. house_c_front.png 150×90. Small hut: add side posts and beam, single thick
   door with iron fittings, small lattice window with warm fill, rubble plinth.
11. wall_rammed_earth.png 120×120, tile xy. Warm beige rammed earth, 2–3 layer
   seams (~18 px apart), fine cracks, patch repairs, low-frequency, fewer
   horizontal streaks than current.
12. stone_plinth.png 120×12, tile x. Irregular rubble stones, alternating
   #8f9993/#718080 and #a69a7d, moss dots in joints.
```

### 第 3／4 批（P1）

沿用通用段；第 3 批写明“与第 2 批已批准立面同语言”，附第 2 批成品；`roof_thatch` 要求“成束 6–10 px、每 12 px 一道压条、降饱和、无单像素噪点”。第 4 批 `house_window_glow_mask.png` 写明“由 #7 最终量化像素中 #e5a34b/#f3cf7a 窗格位置直接导出，纯黑白，像素级对齐”。

## 4. 概念图中有、现场缺的道具/植被

| 概念图元素 | 现状 | 处理 |
| --- | --- | --- |
| 落叶大树（槐/榆） | 有 `prop_tree_locust`、`prop_tree_elm` | **复用**，主要是增加摆放数量（关卡摆放，非出图） |
| 桃树（西门桃林） | 有 `prop_tree_peach`、`_small`、`ground_petals` | 复用 |
| 柏树等竖向树形 | 无 | P2 `prop_tree_cypress` |
| 灌木、草丛 | 只有 `prop_bush` 1 款、`prop_flowers` | 复用＋P2 第二款灌木与草丛团块 |
| 高脚粮仓 | 有 `granary_front/side`（架高、下部透明） | 复用；屋顶可保留茅草（参考图 A 粮仓为茅草/木） |
| 粮袋、麻袋堆 | 有 `prop_grain_sacks`、`prop_sacks_pile` | 复用 |
| 牛车/板车 | 有 `prop_ox_cart`、`prop_handcart` | 复用 |
| 马 | 环境库与 Sprites 均无 | 属**角色/动物精灵**，按 V3 Q 版语言另立任务，不在本环境计划内 |
| 军帐 | 有 `prop_army_tent` | 复用；单坡布篷缺，P2 `prop_awning_canopy` |
| 兵器架、弓架、靶、木人 | 有 `prop_weapon_rack`、`prop_bow_rack`、`prop_archery_target`、`prop_training_dummy` | 复用 |
| 水井 | 有 `prop_well` | 复用 |
| 市集布篷摊 | 有 `prop_stall*` 4 款 | 复用 |
| 水缸、柴堆、竹筐、竹席（参考图 B） | 有 `prop_water_vat`、`prop_firewood`、`prop_basket_stack`/`prop_produce_basket`、`prop_straw_mats` | 复用；斜靠竹席若不够像列 P2 |
| 木栅栏 | 有 `prop_fence`（代码中 0 处引用）、`fence_wattle` | **复用**：建议程序在民居院前摆 `prop_fence`（参考图 B 主要元素） |
| 城墙角楼、墙顶瓦压顶 | 无 | P2 |
| 旗帜 | 有漢字军旗、酒/鐵/藥/驛幌子 | 复用 |

## 5. 风险

| # | 风险 | 影响 | 建议 |
| --- | --- | --- | --- |
| R1 | **约 11 栋民居/马厩用茅草顶**，参考图 A 几乎全是灰瓦 | 不处理则屋顶分项上限约 60%，整体到不了 90% | 由程序员把 house_b/house_c 的 `roofKey: "thatch"` 改为 `"tile"`（只粮仓、马厩保留茅草）；这是代码改动，需另行授权，本文不改 |
| R2 | 植被/道具**摆放密度**不足属关卡摆放，不是贴图 | 植被分项贴图做得再好也约 60% | 另派关卡摆放任务；本计划只出缺失品种 |
| R3 | 屋顶压暗后，在暖色 LUT 下可能与人物（深色衣）明度接近 | Q 版人物在屋顶背景前辨识度下降 | 人物多数站在亮土路上，影响小；第 1 批导入后在四个机位截图核查 |
| R4 | 去噪过度会变“塑料平涂”，加噪又回到“脏” | 用户两次都可能不满意 | brief 已限定 2–4 px 团块、低频；每批先导入截图给用户看再继续 |
| R5 | 生成模型容易越出 48 色、产生半透明边、或改变门窗位置 | 交付验收失败或交互点错位 | 每张必须附调色板/二值透明/尺寸检查日志；门窗位置用旧图作版式模板对比 |
| R6 | 窗内暖色在白天场景可能显得“灯亮着” | 时间感不一致 | 暖色只用在窗格内少量像素；夜景再靠 #24 发光遮罩增强 |
| R7 | 同一 roof_tiles_grey 被郡署、酒肆、赌坊、城门楼共用 | 一改全城联动，郡署可能显得不够气派 | 预期内效果（统一）；郡署若需更高级可在 P2 加鸱吻与专用瓦 |
| R8 | 生图消耗用户 Codex 额度 | 预算 | 首批 6 张验证方向后再续；未获用户原话同意不得开启生图 |
