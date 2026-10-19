# ADR-0005: HD-2D 渲染、相机与像素人物管线

## Status

Proposed

> 只有用户，或在用户明确确认后由 `technical-director`，可以把本 ADR 改为 `Accepted`。下文“待用户拍板”一节列出需要确认的选项及推荐。

## Date

2026-10-19

## Last Verified

2026-10-19（Unity 6000.3.26f1 本机编辑器；URP 17.3）

## Decision Makers

- 用户 Bohua（待确认）
- 起草：Claude（`/architecture-decision` 流程，在用户电脑上操作仓库的线程）
- 评审：本稿尚未经过 `unity-specialist` 引擎校验和 `technical-director` TD-ADR 门；起草时已按 TD-ADR 的检查项自查（问题陈述、被否方案、取舍、引擎版本、截止日期后 API 风险、GDD 关联）。正式接受前可补跑这两项评审。

## Summary

本 ADR 决定 HD-2D 画面的技术方案：URP Universal Renderer 使用 Forward+ 渲染路径；相机采用低视场角透视（约 20°、俯角约 30°）而不是正交；像素人物用竖直的四边形面片 + URP Lit 材质（Alpha Clip、投影、受 3D 光），按相机偏航角做 Y 轴朝向；人物逻辑像素 32×48，30 像素／单位；后处理用 Volume 组合（Bokeh 景深模拟移轴、Bloom、ACES 色调映射、暗角、调色），并按画质档位降级。同时规定 AI 生成精灵到游戏素材的导入管线，以及在样板场景里可验收的标准。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS（6000.3.26f1），URP 17.3 |
| **Domain** | Rendering / Camera / Asset pipeline |
| **Layer** | Foundation（表现层基础） |
| **Knowledge Risk** | HIGH — Unity 6 / URP 17 晚于模型训练数据；Forward+、Render Graph、Volume 组件参数名需对照本机版本核实 |
| **References Consulted** | ADR-0001；`docs/engine-reference/unity/`；共享文档 PROJECT.md §5、V1_SPEC §10、INITIAL_SCENARIO_ART.md、ZHUOJUN_TOWN_DESIGN.md；`D:\BuildArtifacts\Codex\hd2d-jrpg\` 下的涿郡概念图与刘关张／黄巾 3×3 精灵表 |
| **Post-Cutoff APIs Used** | `UniversalRendererData.renderingMode = ForwardPlus`；URP Volume 组件 `DepthOfField`（Bokeh）、`Bloom`、`Tonemapping`、`Vignette`、`ColorAdjustments`；`UniversalAdditionalCameraData.renderPostProcessing`。都必须在样板场景中实际编译、运行验证 |
| **Verification Required** | ① 样板场景在本机编辑器与 IL2CPP 构建中渲染正常；② 像素人物受方向光与点光影响、投影到地面、边缘无半透明毛边；③ 1920×1080 下开发机帧时间记录在案；④ 最低配置（GTX 1050 Ti／Iris Xe 级）60 fps 的实机验证（需要对应硬件，未完成前标记为待验证） |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001（引擎、URP Universal Renderer、工程目录、Build Profile） |
| **Enables** | 涿郡城镇场景、桃园演出、城外战斗背景的美术制作；战斗表现层（ADR-0002 之后）；画质设置界面 |
| **Blocks** | 所有需要正式像素人物、相机或后处理的美术／场景故事 |
| **Ordering Note** | ADR-0002～0004（战斗核心、数据格式、存档）与本 ADR 互不依赖，可并行 |

## Context

### Problem Statement

V1_SPEC §10 要求首版达到“基本一致的中式 HD-2D 样板”：像素人物放在有立体空间和光影的 3D 场景中。目前原型只有正交相机、Sprite-Unlit 材质的像素人物和无后处理的场景，不足以判断 HD-2D 的观感。ADR-0001 已记下一项高风险：URP 默认精灵材质（Sprite-Lit-Default）只响应 2D Renderer 的灯光，在 Universal Renderer 下人物不会被 3D 光照亮。相机、像素密度、人物材质和后处理一旦铺开美术就难以更改，需要先定。

### Current State

- Glade 原型：正交相机（size 6、俯角 35°）、`Universal Render Pipeline/2D/Sprite-Unlit-Default` 人物材质、无 Volume 后处理；`MovementTests.GladeHasPlayableWiring` 断言相机为正交。
- URP 资产 `GladeURP` 使用 Universal Renderer（Forward），MSAA 1×，阴影距离 25。
- 美术现状（AI 生成）：3×3 动作表 1254×1254，每格约 418 px；人物约 370 px 高，可见的“像素块”约 8 px，即逻辑分辨率约 32×48（与生成提示里的 32×48 目标一致）；颜色数为几十万级、边缘有半透明过渡；只有 9 个单帧姿势，没有逐帧行走循环。涿郡概念图为 1659×948 的等距俯视插画。
- IL2CPP 发行构建和性能基线已就绪（ADR-0001 第 5 步）：原型场景不限帧约 0.68 ms／帧。

### Constraints

- 一人开发 + AI 代理；美术以 AI 生成稿为基础，需要可重复的转换管线。
- 首发 Windows x64；ADR-0001 的帧预算 60 fps @1920×1080，最低配置草案 GTX 1050 Ti／Iris Xe／Radeon 680M。
- 自定义渲染 Pass 必须走 Render Graph（ADR-0001）。
- 不引入新的第三方渲染插件。

### Requirements

- 像素人物在 3D 场景中受光、有影子、边缘清晰，不随相机移动出现闪烁或抖动。
- 景深营造“微缩模型”感，主角所在层清晰。
- 暖色光照、Bloom、暗角等氛围效果可通过 Volume 统一调整，并可按画质档位关闭。
- 可在样板场景中逐条验收。

## Decision

### 1. 渲染路径

- Universal Renderer（3D），渲染路径 **Forward+**。理由：城镇有大量灯笼点光，Forward 每物体最多 8 盏附加光，Forward+ 去掉了这个限制且对小场景开销低；Deferred／Deferred+ 与透明物体、MSAA 组合更复杂，V1 不需要。
- MSAA 关闭，后处理抗锯齿关闭（SMAA／FXAA 会把像素人物边缘抹糊）；环境几何的锯齿由景深与后续美术解决。
- HDR 开启（Bloom 需要）；颜色空间沿用项目现状，正式美术前统一为 Linear（单独确认，见“待用户拍板”）。
- 阴影：主方向光软阴影，阴影距离 40，2 级级联；点光默认不投影（性能），关键灯笼可单独开启。

### 2. 相机

- **低视场角透视相机**：垂直 FOV 20°，俯角 30°，相机与主角距离约 34 单位。此时 1920×1080 下主角附近 1 世界单位 ≈ 90 屏幕像素。
- 理由：透视能让远景自然缩小、建筑有纵深，景深也依赖真实深度；正交下景深与远近层次都弱，HD-2D 的立体感来自透视。窄 FOV 把透视畸变压到很小，像素人物不会明显变形。
- 跟随：沿用 `FollowCamera` 的固定偏移跟随（Cinemachine 3 已安装，正式场景可换成 Cinemachine 摄像机，不影响本 ADR 的参数）。相机偏航角在一个场景内固定，人物面片只需对齐这一个偏航角。

### 3. 像素密度与人物规格

- 人物逻辑尺寸 **32×48 像素**（Q 版约 2.5 头身），**30 像素／单位**，人物高 1.6 单位。主角在 1080p 下约 144 屏幕像素高，即每个逻辑像素约 3×3 屏幕像素。
- 场景贴图也按 30 像素／单位设计（贴图密度一致，避免人物和环境“像素大小”差异过大）；地面等 3D 材质不强制像素化。
- 人物一律用 Point 过滤、无 Mipmap、不压缩（或无损），确保整数倍放大清晰。

### 4. 像素人物材质与朝向

- 人物用**竖直四边形面片（Quad）+ URP Lit 材质**：Alpha Clip 开（阈值 0.5）、双面渲染、Receive Shadows 开、Cast Shadows 开。URP Lit 自带 ShadowCaster Pass，因此人物能投影、受方向光与点光照明，也参与 Forward+ 光照。
- 不使用 SpriteRenderer + Sprite-Lit-Default（只响应 2D 灯光，见 ADR-0001 风险）。不在 V1 写自定义精灵着色器；若之后需要轮廓光、受击闪白等效果，再用 Shader Graph 做 Lit 变体（单独 ADR 补充）。
- 朝向：面片保持竖直，只绕 Y 轴对齐相机偏航角（不是完全朝向相机的 billboard），这样影子形状稳定、人物不随相机俯仰倾倒。
- 方形像素补偿：竖直面片在 30° 俯角下会被压扁到 cos 30° ≈ 0.87（样板场景实测人物从约 132 像素变成约 115 像素高，像素变成横向长方形）。面片高度乘以 1/cos(俯角)，并上移半个增量保持脚贴地，使每个美术像素在屏幕上保持正方形。代价是影子略长约 15%。
- 动作帧：一张图集 + 按帧切换 UV（`MaterialPropertyBlock` 设置 `_BaseMap_ST`），避免为每帧建材质。

### 5. 后处理 Volume

全局 Volume Profile `HD2D_Default`（`Assets/_Project/Settings/`），按画质档位降级：

| 效果 | 高档（默认） | 低档（最低配置） |
| --- | --- | --- |
| 景深 | Bokeh：对焦距离 = 相机到主角距离，焦距 300 mm、光圈 f/2（样板场景实测 f/5.6 时几乎看不出虚化，因为俯视镜头下前后景与焦点的距离差只有十几个单位），近景和远景都虚化，模拟移轴 | 关闭（URP 的 Gaussian 模式只虚化远景，效果不像移轴，宁可关闭） |
| Bloom | 阈值 0.9、强度 0.6、散射 0.7 | 强度 0.4，降采样 |
| 色调映射 | ACES | ACES |
| 暗角 | 强度 0.3、平滑 0.4 | 同左 |
| 调色 | Color Adjustments：曝光 0、对比 +10、饱和 +10；暖色由灯光与 Color Filter 提供 | 同左 |

- 若 Bokeh 在最低配置上超预算，备选是写一个 Render Graph 屏幕空间移轴 Pass（按屏幕纵向位置模糊），作为本 ADR 的补充决定，不在 V1 默认范围。

### 6. 精灵导入与资产管线

1. 原始 AI 稿保存在仓库外（`D:\BuildArtifacts\...`），不入库。
2. 转换：编辑器工具 `ThreeKingdoms.EditorTools.PixelSpriteConverter` 读入 3×3 动作表，按格切片 → 计算包围盒 → 以“块内众数色”缩小到 32×48 逻辑像素 → Alpha 二值化（阈值 0.5）→ 颜色量化（默认每通道 5 位）→ 输出一张横向图集 PNG 到 `Assets/_Project/Art/Sprites/Characters/<角色>/`。
3. 导入设置：Texture Type Default、Point 过滤、无 Mipmap、无压缩、Alpha Is Transparency、Wrap Clamp、sRGB。
4. 自动转换只是起点；正式素材在 Aseprite（已装 2D Aseprite 导入包）里人工清理调色板与轮廓，补齐行走帧。V1 目标每角色：待机 1、行走 4×4 方向（或镜像 3 方向）、攻击 2、受击 1、撤退 1。
5. 场景用的地面、墙面贴图同样 Point 过滤、按 30 像素／单位制作。

### 7. 画质档位

`QualitySettings` 两档：`High`（上表高档，阴影 2 级级联）与 `Low`（关闭景深、Bloom 降采样、阴影 1 级级联、渲染缩放 1.0）。默认 High；设置界面在 UI 阶段另做。

## Alternatives Considered

### Alternative 1：正交相机（延续原型）

- **Pros**：像素对齐简单，人物不受透视影响；原型已在用。
- **Cons**：没有真实远近缩放，景深只能做成屏幕空间假效果，城镇纵深弱，不像 HD-2D。
- **Rejection Reason**：HD-2D 的核心观感（立体空间 + 景深）依赖透视。

### Alternative 2：SpriteRenderer + 2D Renderer（2D 光照）

- **Pros**：Unity 原生 2D 工作流，Sprite 动画工具齐全。
- **Cons**：2D Renderer 不渲染 3D 光照与阴影，3D 场景的灯光、阴影和人物光照会分裂成两套系统。
- **Rejection Reason**：与“3D 场景 + 光影”目标冲突。

### Alternative 3：自定义 Shader Graph 精灵着色器（V1 即做）

- **Pros**：可加轮廓光、受击闪白、调色板替换。
- **Cons**：多一份需维护的着色器与变体，V1 需求用 URP Lit + Alpha Clip 即可满足。
- **Rejection Reason**：先用内置 Lit，需求出现再补充。

### Alternative 4：低分辨率渲染目标整体放大（像素化整个画面）

- **Pros**：全画面像素风统一。
- **Cons**：3D 场景、景深与 Bloom 都会变成大像素块，不是 HD-2D（HD-2D 是“高清 3D + 像素人物”）。
- **Rejection Reason**：与目标风格不符。

### Alternative 5：人物逻辑尺寸 48×64

- **Pros**：细节更多，接近部分 HD-2D 作品的比例。
- **Cons**：现有 AI 稿的实际像素粒度约 32×48，放大到 48×64 会把块状像素插值变糊或需全部重画；人物在 1080p 下会更大，城镇显得拥挤。
- **Rejection Reason**：与现有美术粒度不符；可作为后续美术升级时的选项。

## Consequences

### Positive

- 人物与环境共享一套 3D 光照与阴影，HD-2D 观感成立。
- 相机与像素密度先定，美术可按 30 像素／单位、32×48 统一制作。
- 后处理集中在一个 Volume Profile，可按档位降级。

### Negative

- 透视相机下，像素人物不能严格保证“逻辑像素 = 整数屏幕像素”（主角在焦点距离处约 3×，前后景人物略大略小）；需要接受轻微不均匀。
- URP Lit 面片侧面受光时会变暗；需要用补光或环境光控制，正式美术期可能要调 Lit 变体。
- 原型 Glade 的正交断言测试与新相机方案不一致，Glade 会保留为旧原型，新场景另建。
- Bokeh 景深是主要 GPU 成本，最低配置可能只能用低档。

### Neutral

- 自动转换管线只是起点，人工清理工作量取决于 AI 稿质量。

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| Bokeh 景深在 GTX 1050 Ti／Iris Xe 上超预算 | 中 | 中 | 低档关闭景深；必要时写 Render Graph 移轴 Pass |
| AI 稿缩小后细节丢失、轮廓脏 | 高 | 中 | 众数采样 + Alpha 二值化 + Aseprite 人工清理；按格调整缩小参数 |
| 面片人物在侧光下发黑或缺少体积感 | 中 | 低 | 环境光 + 补光；后续 Lit 变体加法线／轮廓光 |
| 透视下人物像素大小不均、移动时闪烁 | 中 | 中 | Point 过滤 + 无 Mipmap；相机距离固定；样板场景实测 |
| URP 17 API／Volume 参数名与训练知识不符 | 中 | 低 | 在样板场景中编译、运行验证；对照本机包源码 |

## Performance Implications

| Metric | Before（Glade 原型，ADR-0001 基线） | Expected After（样板场景） | Budget |
|--------|--------|---------------|--------|
| CPU+GPU 帧时间 | 0.68 ms（不限帧，开发机） | 样板实测（IL2CPP，1080p，不限帧，Quadro P2000）：High 平均 2.94 ms（p95 3.53、p99 4.09），Low 平均 2.42 ms（p95 3.45） | 16.6 ms @1080p；最低配置待实测 |
| Memory | 约 190 MB 工作集 | 样板实测约 195–201 MB 工作集 | ≤ 2 GB |
| Load Time | — | 场景切换 ≤ 3 s | 同 ADR-0001 |

验证方法：IL2CPP 构建 + `PerfBaselineProbe`（`-scene HD2D_Sample -perfBaseline 30 -perfUncapped`，Low 档加 `-hd2dLow`），分别在 High 与 Low 档各测一次；最低配置在对应硬件上复测（或在有该档硬件时补测，未测前标为待验证）。开发机 Quadro P2000 的性能大致介于 GTX 1050 Ti 与 GTX 1060 之间，样板在其上的帧时间不到预算的 20%，最低配置达标的可能性较高，但仍需实机确认。

## Migration Plan

1. 新建样板场景 `Assets/_Project/Scenes/HD2D_Sample.unity`（不改 Glade）：灰盒涿郡街道、灯笼点光、暖色方向光、刘备像素人物面片。
2. URP 资产与 Renderer：新增 `HD2D_URP` / `HD2D_Renderer`（Forward+），保留 `GladeURP` 给原型；全局默认管线切到 `HD2D_URP` 前确认 Glade 测试仍通过。
3. 实现 `PixelSpriteConverter`（EditorTools）与 `PixelBillboard`、`SpriteFrameAnimator`（Gameplay）。
4. 建 `HD2D_Default` Volume Profile 与 High／Low 画质档位。
5. PlayMode 测试覆盖样板场景装配（相机为透视、FOV、人物材质为 URP Lit 且开 Alpha Clip、Volume 含景深）；IL2CPP 构建并记录性能。

## Validation Criteria

- [ ] 样板场景存在且能在编辑器 Play 与 IL2CPP 构建中运行，无报错
- [ ] 相机为透视、FOV 20°、俯角 30°；主角在 1080p 下约 144 像素高
- [ ] 刘备像素人物为 32×48 逻辑像素，Point 过滤、无 Mipmap，边缘无半透明毛边
- [ ] 人物受方向光与灯笼点光照明，并在地面投影
- [ ] 人物面片只绕 Y 轴对齐相机偏航角，移动时不倾倒
- [ ] Renderer 为 Forward+；Volume 含 Bokeh 景深、Bloom、ACES、暗角、调色；Low 档关闭景深
- [ ] 开发机 1080p 帧时间（High／Low）已记录；最低配置 60 fps 为待验证项或已实测
- [ ] 原有 EditMode／PlayMode 测试仍全部通过，并新增样板场景装配测试

## 待用户拍板

| # | 问题 | 选项 | 推荐 |
| --- | --- | --- | --- |
| 1 | 相机 | A 低 FOV 透视（20°／30°） B 正交 | **A**：HD-2D 纵深与景深依赖透视 |
| 2 | 人物逻辑尺寸 | A 32×48 B 48×64 | **A**：与现有 AI 稿像素粒度一致 |
| 3 | 人物材质 | A URP Lit 面片 + Alpha Clip B 自定义 Shader Graph C 不受光 | **A**：V1 够用，需求出现再加 B |
| 4 | 景深 | A Bokeh（低档关闭） B Gaussian（只虚化远景） C 自写移轴 Pass | **A**，C 作为超预算时的备选 |
| 5 | 渲染路径 | A Forward+ B Forward C Deferred+ | **A**：灯笼多，限制少 |
| 6 | 颜色空间 | A 切到 Linear B 保持当前 | **A**：光照与 Bloom 更正确；需在美术铺开前切 |
| 7 | 美术管线 | A AI 稿自动缩小 + Aseprite 人工清理 B 全部手绘 | **A**：先用转换稿验证，再逐步人工精修 |

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| PROJECT.md §5、V1_SPEC §10 | 画面 | HD-2D：像素人物 + 立体场景光影；首版基本一致的中式 HD-2D 样板 | 透视相机 + 受光像素面片 + Volume 后处理 + 统一像素密度 |
| V1_SPEC §10 | 画面 | 刘关张 3 名可操作像素武将、步骑弓区别、简化动画 | 32×48 逻辑尺寸与帧规格、转换管线 |
| INITIAL_SCENARIO_ART.md | 美术 | 普通黄巾共用 Q 版，张角三兄弟独立 Q 版 | 同一管线转换各 3×3 表 |
| ADR-0001 风险表 | 渲染 | Sprite-Lit-Default 在 Universal Renderer 下不受 3D 光 | URP Lit 面片方案 |

## Related

- ADR-0001：引擎、平台目标与工程结构基线
- 后续：画质设置界面（UI 阶段）；可能的 Render Graph 移轴 Pass 补充；Lit 精灵着色器变体
