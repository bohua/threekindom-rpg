# ADR-0012: 建筑由 Codex 在 Blender 建模导出 FBX（涿郡试点）

## Status

Proposed（用户 2026-10-26 要求试点；是否推广待用户看对比图后决定）

## Context

用户 2026-10-26 原话：“让 Codex 做出一个效果图，然后直接让 Codex 把 Blender 的工作也做好，然后再把输出给到 Claude，直接进 Unity。”并担心是否会变成全 3D 写实。

城镇建筑目前由 `ZhuojunTownBuilder.Building()` 等用面片和方块拼出，再贴 Codex 的手绘高清贴图（120 像素/米）。拼法难做屋面厚度、出檐、台基和台阶，常见拉伸、接缝、穿插和低垂檐口挡门。本机已装 Blender 4.5 LTS，Codex 沙箱内可以后台运行它。

## Decision

1. 试点两件：涿郡民居 A（04 House A）与南门（城台＋城楼）。其他建筑仍用代码拼法。
2. 分工：Claude 写任务单经 codex-assets 桥（ADR-0009）派给 Codex；Codex 先出效果图，再用 Blender Python 脚本建模、展 UV、导出 FBX、自检、渲染预览；Claude 审阅批准后导入 Unity。
3. 风格：低面数；表面只用现有手绘贴图；不用 PBR 贴图、不烘焙光照/AO/顶点色；Unity 实时光照；人物仍为 2D 精灵；地面仍在 Unity 做。
4. 交付规范：1 单位＝1 米，Z 向上，正面朝 Blender −Y，原点在底面中心，变换已应用；贴图米制不拉伸、全部 120 像素/米；材质名＝贴图键名，FBX 不嵌贴图，另附 materials.json；对象按 Walls/Roof/Ridge/Plinth/Steps 等命名，可选 `COL_` 碰撞盒；FBX forward −Z、up Y、FBX_SCALE_UNITS、bake_space_transform。详细见共享文档 BLENDER_PIPELINE.md。
5. Unity：`EditorTools/BlenderModels.cs` 复制交付到 `Art/Environment/ZhuojunHD/HD/zhuojun-blender-pilot/`，按材质名换成城镇现有材质（同一 URP Lit 设置、同一贴图），`COL_` 变碰撞盒，加 `Occluder`。按上述导出设置模型进 Unity 后正面朝北，放置时统一转 180°。南门碰撞沿用原来的门墩与门楣盒，门洞可通行。缺少模型或带 `-noBlender` 时退回代码拼法。
6. `EditorTools/BlenderPilotShots.cs` 两版同机位各拍一张，用于对比。

## Consequences

- 正面：几何封闭、无接缝；屋面有厚度和出檐；台基、台阶、门扇都是模型的一部分；面数很低（民居 138、南门 190 三角面）；建模脚本可复现，改尺寸只需改脚本重跑。
- 负面：多一个外部工具与一轮 Codex 任务（试点约 20 分钟、2 张生图、约 226 万 token）；材质依赖命名约定；试点模型的脊饰与瓦当比代码版的雕花卡片简化。
- 待定：是否推广到其他城镇建筑与场景，由用户看对比图后决定。
