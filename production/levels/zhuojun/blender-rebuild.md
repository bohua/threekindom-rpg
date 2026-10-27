# 涿郡｜Codex＋Blender 补齐 3D 建模

日期：2026-10-27　角色：level-designer / technical-artist（Claude 执行）　状态：进行中
依据：用户 2026-10-27 原话“现在我们结合codex + blender的调研结果补齐涿郡的3D建模，可以把更细节的部分比如小摊贩，军营帐篷，以及野外部分的地势和山川都做出来”；共享文档 PROJECT.md §5（2026-10-27 场景建模统一走 Codex＋Blender）、BLENDER_PIPELINE.md；ADR-0012。
基线：分支 feature/zhuojun-blender，从 feature/blender-pilot（含涿郡高清翻新、初到涿郡巡览与试点民居A、南门）开出。布局、站位、门前净空沿用 `layout.md`，不改。地形做法与渤海线程（production/levels/bohai-plain/blender-rebuild.md）一致：Blender 地形网格按世界坐标建，Unity 套用现有融合地面材质。

## 范围核对

| 项 | 做法 | 与基线 |
| --- | --- | --- |
| 建筑 13 栋 | 民居A、南门（试点）＋9 栋（第1批）＋郡署整组（第2批） | 替换几何 |
| 城墙、角楼、西门 | 8 m／4 m 城墙模块、角楼、西门（第2批） | 替换几何 |
| 街景与军营道具 | 4 个摊位、告示栏、军营帐篷、兵器架、弓架、箭靶、木人桩、战鼓、征兵桌、水井、手推车、牛车、赌桌（第3批） | 纸片 → 模型 |
| 城外地势 | 城外近景地形（可走区与墙脚保持 z=0）、岩石、桃林小土岗、远景燕山（第4批） | 新增起伏，替换远山立牌 |
| 地面融合、道路、人物、树木花草精灵 | 不变；城外树木按地形高度落位，丘陵上补种树 | 0 |

不增加地点、剧情或战斗。

## Codex 分批（一批完成再派下一批）

1. zhuojun-blender-b1：tavern、smithy、casino、clinic、stable、post_station、house_b、house_c、granary（原点＝正面墙底中点）
2. zhuojun-blender-b3：16 件道具（原点＝底面中心）
3. zhuojun-blender-b4：terrain_near、rocks、peach_knoll、yan_mountains（世界坐标，原点＝世界原点）
4. zhuojun-blender-b2：commandery（原点＝最下一级台阶前沿正中，z 14.5）、wall_8、wall_4、corner_tower、west_gate

## Unity 接入

- `BlenderModels`：读取所有已交付的批次，按材质名换成城镇现有材质（含新画的可平铺贴图），`emission_mask` 接发光，`COL_` 变碰撞盒；地形用网格碰撞、不做遮挡。
- `ZhuojunTownBuilder`：Building() 与 Prop() 按贴图名查模型，有则放模型、无则沿用面片；城墙按 8 m 模块排满；地形存在时融合地面平面只铺城内，城外由地形网格承接同一材质；树木按地形高度落位，丘陵补种约 150 棵；燕山模型存在时去掉远山立牌并把远裁剪面放到 700 m。
- `-noBlender` 退回面片版本，便于同机位对比。

## 验收

- 每批与面片版同机位截图对比，用户看后再定细节。
- 门洞可通行、台阶可走、城墙不可穿；告示栏、郡署台阶、酒肆门前等剧情点位置不变；主线与城镇测试通过。
- 人物仍为 128 像素 Q 版；正午光照；地形过渡原则不变。
