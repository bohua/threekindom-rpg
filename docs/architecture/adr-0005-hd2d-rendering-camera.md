# ADR-0005: HD-2D 渲染、相机与像素人物管线

## Status

Accepted（2026-10-19，用户确认；评审意见已按文末“评审记录”处理）

## Date

2026-10-19

## Last Verified

2026-10-19（Unity 6000.3.26f1 本机编辑器；URP 17.3）

## Decision Makers

- 用户 Bohua：2026-10-19 确认 7 项技术选项、混合画面方案与 4 项环境美术决定（合为“用户确认”表的 9 行），并授权在评审后将本 ADR 改为 Accepted
- 起草：Claude（`/architecture-decision` 流程，在用户电脑上操作仓库的线程）
- 评审：`unity-specialist` 引擎校验与 `technical-director` TD-ADR 门，结论与处理见文末“评审记录”

## Summary

本 ADR 决定 HD-2D 画面的技术方案：URP Universal Renderer 使用 Forward+ 渲染路径；相机采用低视场角透视（约 20°、俯角约 25°）而不是正交；像素人物用竖直的四边形面片 + URP Lit 材质（Alpha Clip、投影、受 3D 光），按相机偏航角做 Y 轴朝向；人物逻辑像素 32×48，30 像素／单位；后处理用 Volume 组合（Bokeh 景深模拟移轴、Bloom、ACES 色调映射、暗角、调色），并按画质档位降级。同时规定 AI 生成精灵到游戏素材的导入管线，以及在样板场景里可验收的标准。画面采用混合方案：城镇与探索用上述 3D 场景；战斗用 2–4 层视差的 2D 像素背景，人物仍是同一套受光面片，两者共用 Linear 颜色空间与同一套暖色 LUT 调色。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS（6000.3.26f1），URP 17.3 |
| **Domain** | Rendering / Camera / Asset pipeline |
| **Layer** | Foundation（表现层基础） |
| **Knowledge Risk** | HIGH — Unity 6 / URP 17 晚于模型训练数据；Forward+、Render Graph、Volume 组件参数名需对照本机版本核实 |
| **References Consulted** | ADR-0001；`docs/engine-reference/unity/`；共享文档 PROJECT.md §5、V1_SPEC §10、INITIAL_SCENARIO_ART.md、ZHUOJUN_TOWN_DESIGN.md；`D:\BuildArtifacts\Codex\hd2d-jrpg\` 下的涿郡概念图与刘关张／黄巾 3×3 精灵表 |
| **Post-Cutoff APIs Used** | `UniversalRendererData.renderingMode = ForwardPlus`；URP Volume 组件 `DepthOfField`（Bokeh）、`Bloom`、`Tonemapping`、`Vignette`、`ColorAdjustments`、`ColorLookup`；`UniversalAdditionalCameraData.renderPostProcessing`；光照烘焙（`Lightmapping.Bake`、Lighting Settings）与 `ScreenSpaceAmbientOcclusion` 渲染特性；Build Profile 的 Player Settings 覆盖。都必须在样板场景中实际编译、运行验证 |
| **Verification Required** | ① 城镇样板与战斗样板在本机编辑器与 IL2CPP 构建中渲染正常，无粉色材质、无着色器变体缺失（`_ALPHATEST_ON`、Unlit 背景）；② 像素人物受方向光与点光影响、投影到地面、边缘无半透明毛边；③ 1920×1080 下开发机帧时间记录在案；④ `ColorLookup` 的 LUT 规格通过校验（否则效果会静默失效）；⑤ 最低配置（GTX 1050 Ti／Iris Xe 级）60 fps 的实机验证（需要对应硬件，未完成前标记为待验证） |

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

战斗和城镇对画面的需求不同：战斗镜头固定，靠构图和细节取胜（用户给出的参考图是一张细节丰富的街市战斗画面）；城镇需要自由行走、纵深和光影变化。样板实测表明，用简单 3D 几何体很难在战斗画面里达到参考图的细节密度，因此需要决定两种场景各用什么表现方式。

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
- 战斗画面：我方最多 3 人、敌方最多 4 人（V1_SPEC §2.2 第二战）同屏且互不遮挡；背景细节密度接近参考图；人物与背景的光向、色调一致。
- 可在样板场景中逐条验收。

## Decision

### 1. 渲染路径

- Universal Renderer（3D），渲染路径 **Forward+**。理由：城镇有大量灯笼点光；Forward 每物体最多 8 盏附加光，Forward+ 改为每台相机最多 256 盏可见附加光（桌面平台）、反射探针最多 64 个，对 V1 城镇足够，小场景开销低；Forward+ 也支持光照贴图与 Subtractive／Shadowmask 混合光照。城镇灯笼优先用 Baked／Mixed 模式，减少实时光数量。Deferred／Deferred+ 与透明物体、MSAA 组合更复杂，V1 不需要。
- MSAA 关闭，后处理抗锯齿关闭（SMAA／FXAA 会把像素人物边缘抹糊）；环境几何的锯齿由景深与后续美术解决。
- HDR 开启（Bloom 需要）；**颜色空间 Linear**（用户 2026-10-19 确认，在正式美术铺开前切换）。颜色贴图与像素图集保持 sRGB 导入，数据贴图（LUT、遮罩）用线性导入。
- 阴影：主方向光软阴影，阴影距离 40，2 级级联。V1 点光不投影：URP 资产的附加光阴影保持关闭（打开后每盏点光占阴影图集 6 个面）；若以后需要灯笼投影，须在 URP 资产中打开附加光阴影并限制投影灯不超过 2 盏，另行记录。
- 阴影法线偏移在样板中为 1；很薄的面片可能出现影子与脚底分离，样板验收时人工检查。

### 2. 相机

- **低视场角透视相机**：垂直 FOV 20°，俯角 25°（用户 2026-10-19 由 30° 改为 25°：立面露得更多、屋顶占画面更少），相机与主角距离约 34 单位。此时 1920×1080 下主角附近 1 世界单位 ≈ 90 屏幕像素。
- 理由：透视能让远景自然缩小、建筑有纵深，景深也依赖真实深度；正交下景深与远近层次都弱，HD-2D 的立体感来自透视。窄 FOV 把透视畸变压到很小，像素人物不会明显变形。
- 跟随：沿用 `FollowCamera` 的固定偏移跟随（Cinemachine 3 已安装，正式场景可换成 Cinemachine 摄像机，不影响本 ADR 的参数）。相机偏航角在一个场景内固定，人物面片只需对齐这一个偏航角。
- 像素闪烁：透视相机连续跟随、约 3 倍非整数缩放加 Point 过滤，移动时人物像素会有轻微抖动。V1 接受这一点，列为人工验收项（“移动时人物闪烁可接受”）；若用户认为不可接受，再把相机位置按焦平面上 1/30 单位对齐取整。

### 3. 像素密度与人物规格

- 人物逻辑尺寸 **32×48 像素**（Q 版约 2.5 头身），**30 像素／单位**，人物高 1.6 单位。主角在 1080p 下约 144 屏幕像素高，即每个逻辑像素约 3×3 屏幕像素。
- 场景贴图也按 30 像素／单位设计（贴图密度一致，避免人物和环境“像素大小”差异过大）；地面等 3D 材质不强制像素化。
- 人物一律用 Point 过滤、无 Mipmap、不压缩（或无损），确保整数倍放大清晰。

### 4. 像素人物材质与朝向

- 人物用**竖直四边形面片（Quad）+ URP Lit 材质**：Alpha Clip 开（阈值 0.5）、双面渲染、Receive Shadows 开、Cast Shadows 开。URP Lit 自带 ShadowCaster Pass，因此人物能投影、受方向光与点光照明，也参与 Forward+ 光照。
- 不使用 SpriteRenderer + Sprite-Lit-Default（只响应 2D 灯光，见 ADR-0001 风险）。不在 V1 写自定义精灵着色器；若之后需要轮廓光、受击闪白等效果，再用 Shader Graph 做 Lit 变体（单独 ADR 补充）。
- 朝向：面片保持竖直，只绕 Y 轴对齐相机偏航角（不是完全朝向相机的 billboard），这样影子形状稳定、人物不随相机俯仰倾倒。
- 方形像素补偿：竖直面片在俯角 θ 下会被压扁到 cos θ（25° 时约 0.91；早先 30° 时样板实测人物从约 132 像素变成约 115 像素高，像素变成横向长方形）。面片高度乘以 1/cos(俯角)，并上移半个增量保持脚贴地，使每个美术像素在屏幕上保持正方形。代价是影子略长（25° 时约 10%）。
- 动作帧：一张图集 + 按帧切换 UV（`MaterialPropertyBlock` 设置 `_BaseMap_ST`），避免为每帧建材质。代价：用了 `MaterialPropertyBlock` 的渲染器会退出 SRP Batcher；同屏人物最多 7 个，开销可忽略。阴影 Pass 同样读取 `_BaseMap_ST` 和 Alpha Clip，影子形状与当前帧一致。

### 5. 后处理 Volume

全局 Volume Profile `HD2D_Default`（`Assets/_Project/Settings/`），按画质档位降级：

| 效果 | 高档（默认） | 低档（最低配置） |
| --- | --- | --- |
| 景深 | Bokeh：对焦距离 = 相机到主角距离，焦距 300 mm、光圈 f/2（样板场景实测 f/5.6 时几乎看不出虚化，因为俯视镜头下前后景与焦点的距离差只有十几个单位），近景和远景都虚化，模拟移轴 | 关闭（URP 的 Gaussian 模式只虚化远景，效果不像移轴，宁可关闭） |
| Bloom | 阈值 0.9、强度 0.6、散射 0.7 | 强度 0.4，降采样 |
| 色调映射 | ACES | ACES |
| 暗角 | 强度 0.3、平滑 0.4 | 同左 |
| 调色 | Color Adjustments：曝光 0、对比 +10、饱和 +10；暖色由灯光与 Color Filter 提供 | 同左 |

- 焦距 300 mm 已是 URP 参数上限（范围 1–300）；需要更强虚化只能把光圈往 f/1 调，或改用自写移轴 Pass。
- 若 Bokeh 在最低配置上超预算，备选是写一个 Render Graph 屏幕空间移轴 Pass（按屏幕纵向位置模糊），作为本 ADR 的补充决定，不在 V1 默认范围。
- 调色 LUT（URP `ColorLookup`）：1024×32 横条图（高 = URP 资产的 `colorGradingLutSize` 32，宽 = 高²），关闭 sRGB、无 Mipmap、不压缩、Clamp 环绕，`contribution` = 1。LUT 作用在 ACES 之后，以“ACES 之后的中性 LUT”为底图调色。规格不符时 `ColorLookup` 会静默失效，因此由测试检查。

### 6. 精灵导入与资产管线

1. 原始 AI 稿保存在仓库外（`D:\BuildArtifacts\...`），不入库。
2. 转换：编辑器工具 `ThreeKingdoms.EditorTools.PixelSpriteConverter` 读入 3×3 动作表，按格切片 → 计算包围盒 → 以“块内众数色”缩小到 32×48 逻辑像素 → Alpha 二值化（阈值 0.5）→ 颜色量化（默认每通道 5 位）→ 输出一张横向图集 PNG 到 `Assets/_Project/Art/Sprites/Characters/<角色>/`。
3. 导入设置：Texture Type Default、Point 过滤、无 Mipmap、无压缩、Alpha Is Transparency、Wrap Clamp、sRGB。
4. 自动转换只是起点；正式素材在 Aseprite（已装 2D Aseprite 导入包）里人工清理调色板与轮廓，补齐行走帧。V1 目标每角色：待机 1、行走 4×4 方向（或镜像 3 方向）、攻击 2、受击 1、撤退 1。
5. 场景用的地面、墙面贴图同样 Point 过滤、按 30 像素／单位制作。
6. 场景内招牌、幌子等画进贴图的文字用繁体（如「鐵」「酒肆」）；界面文字（菜单、对话、战斗 HUD）用简体。两条均为用户 2026-10-19 确定。

### 7. 画质档位

两档：`High`（上表高档，阴影 2 级级联）与 `Low`（关闭景深、Bloom 降采样）。Volume 参数不属于 `QualitySettings`，所以档位由 `ThreeKingdoms.App.HD2DQualityTier` 切换 Volume Profile（`HD2D_Default`／`HD2D_Low`）实现；阴影级联等管线参数以后按档位各绑定一个 URP 资产。`QualitySettings` 目前仍是 Unity 默认 6 档，精简为两档并各自绑定 URP 资产放在画质设置界面的故事里做。默认 High。

### 8. 混合画面方案：战斗用分层 2D 像素背景，城镇／探索用 3D

用户 2026-10-19 看过样板与参考图后确定：

- **战斗**：背景是 2–4 层 2D 像素图（远景／中景建筑与地面／前景虚化），按视差排列在正交或近正交相机前；战斗双方仍用第 4 节的受光面片人物，放在地面层与前景层之间。背景层用不受光材质（光照已画进图里），人物受一盏与背景光向一致的方向光照明，保证明暗一致。逻辑分辨率 640×360，1080p 下整数 3 倍放大，与人物 30 像素／单位、每逻辑像素 3×3 屏幕像素一致。
- **背景制作**：正式背景由美术绘制（或 AI 稿 + 人工精修）。在正式稿到位前，用 3D 环境套件搭场景、按 640×360 渲染成各层 PNG 作为占位与构图稿（编辑器工具烘焙），这样战斗与城镇共用一套建筑语言。
- **城镇／探索**：保持本 ADR 的 3D 方案，美感来自五个因素：① 手绘风格贴图与场景布置（招牌、货物、杂物）；② 烘焙全局光照与环境光遮蔽（GI／AO）；③ 雾与空气透视；④ LUT 调色；⑤ Linear 颜色空间。
- 两种场景共用同一张暖色 LUT（URP `ColorLookup`）与 ACES 色调映射，避免进出战斗时色调跳变。

#### 8.1 战斗相机与人物

- **正交相机**，正交尺寸 6（360 ÷ 2 ÷ 30），不俯仰、不旋转；人物面片正对相机，**不做** 1/cos 方形像素补偿（正交且无俯角时面片不被压扁）。
- **战斗 Volume 不开景深**：URP 景深按透视深度计算，正交相机下范围会算错；前景虚化直接画进前景层。
- **人物影子**：背景层不受光，接收不到实时阴影，所以战斗人物不投实时影子，改用脚底的椭圆影子贴片（半透明不受光面片）。以后需要真实投影时，再做一个只接收阴影的透明地面材质（Shader Graph Lit 变体，属于第 4 节“单独补充”的范围）。
- **光向一致**：每张背景附一份元数据（`BattleBackdrop` 资产），记录画面主光的偏航角／俯角、颜色与强度；战斗方向光从元数据读取，不在场景里手调。

#### 8.2 背景的颜色管线

- 背景在最终画面里仍要经过 ACES、共用 LUT 与 Bloom。为避免“画里调好的颜色被二次调色”，采用：**背景按游戏内最终效果预览来定稿**。背景本身不预先加暖色（暖色由共用 LUT 统一提供）；美术用“套用 ACES + 同一 LUT”的预览检查成品。
- 战斗 Volume 的 Bloom 阈值高于 1（不受光背景的颜色最大为 1），只让人物技能光效、火光等 HDR 元素发光，画中的白色高光不会被 Bloom 放大。
- 验收：同一张参考色板在背景预览与游戏内截图中的色差在阈值以内（人工对比，阈值在首张正式背景时确定）。

#### 8.3 分辨率与缩放

- 逻辑分辨率 640×360，16:9。1920×1080 为 3 倍、2560×1440 为 4 倍，都是整数倍。
- 其他分辨率（如 Steam Deck 1280×800、16:10、超宽屏）：取不超过屏幕的最大整数倍缩放，四周留黑边，画面保持 16:9 安全区。具体实现（渲染到 640×360 RenderTexture 后 Point 放大，或 Pixel Perfect Camera）在画质与分辨率设置的故事中选定，并在那时核实 Pixel Perfect Camera 在 Universal Renderer 下的可用性（截止日期后 API 风险）。V1 验收以 1920×1080 为准。

#### 8.4 场景加载、内存与资产数量

- 战斗场景以叠加方式加载，城镇根节点在战斗期间停用，战斗结束后卸载战斗场景、恢复城镇；目标切换时间 ≤ 3 s（同 ADR-0001），进出战斗的耗时与内存在战斗样板中实测。
- 内存初值：一张战斗背景 3–4 层 640×360 RGBA 不压缩约 3.7 MB；城镇光照贴图总量 ≤ 64 MB。
- V1 战斗背景最多 2 套（V1_SPEC §2.3）。桃园结义演出复用城镇 3D 管线；徐州结尾过场背景使用 2D 分层管线，单独计数，不占战斗背景的 2 套名额。

#### 8.5 代码归属（对照 ADR-0001）

- 运行时组件（视差背景、战斗相机设置、影子贴片）放在 `ThreeKingdoms.Gameplay`；只用 `Camera`、`Volume` 等时引用 `Unity.RenderPipelines.Core.Runtime`，若需要 `UniversalAdditionalCameraData` 则为 Gameplay 增加 `Unity.RenderPipelines.Universal.Runtime` 引用（ADR-0001 依赖图的第三方引用补充，不改变程序集之间的方向）。
- 画质档位留在 `ThreeKingdoms.App`；背景烘焙、贴图生成与精灵转换工具放在 `ThreeKingdoms.EditorTools`。
- IL2CPP 构建会剥离未被引用的着色器变体：人物 `_ALPHATEST_ON` 与背景用的 Unlit 变体，必须由构建内场景引用的材质保留；构建验收时检查无粉色材质。

### 9. 环境美术方向（用户 2026-10-19 确认）

1. 程序生成贴图只用于定风格和贴图密度；正式贴图人工绘制或精修，先做用量最大的屋顶与墙面。
2. 招牌、酒幌等文字用手绘像素书法或固定的像素字库，不用程序拼字。
3. 屋脊、脊端装饰与起翘檐角做成单独的模型件，不用方块近似。
4. 颜色空间 Linear（同第 1 节）；切换后所有贴图的颜色需要复核。

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

### Alternative 6：战斗也用实时 3D 场景

- **Pros**：与城镇一套资产、一套光照，镜头可以运动。
- **Cons**：要达到参考图那种细节密度，3D 资产与布光成本远高于一张分层背景；战斗镜头基本固定，3D 的优势用不上。
- **Rejection Reason**：战斗画面以构图和细节取胜，分层 2D 背景性价比更高。

### Alternative 7：城镇也用 2D 绘制背景

- **Pros**：每个画面都能像插画一样精致。
- **Cons**：可自由行走的城镇需要大量画面拼接，失去纵深、光影和景深这些 HD-2D 特征。
- **Rejection Reason**：与 HD-2D 方向冲突；探索保留 3D。

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

- 混合方案意味着两套表现管线：两种相机、两种光照模型（3D 实时光／画进背景的光）、两套测试与验收；每张背景都要对齐光向元数据。
- 新增 `BattleBackdropBaker` 编辑器工具（用 3D 建筑件搭场景、渲染并缩小到 640×360 各层）也是一笔工作量；如果正式美术稿不到位，首版会以烘焙占位稿发布。
- 战斗人物没有实时投影，只有影子贴片。

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
| 2D 战斗背景与 3D 城镇的色调、光向不一致，进出战斗有割裂感 | 中 | 中 | 共用 LUT 与色调映射；战斗方向光与背景画面光向一致；背景构图稿由同一套 3D 建筑件烘焙 |
| 每个战斗地点都要一张分层背景，美术量随地点数增长 | 中 | 中 | V1 只有 3 场战斗、最多 2 套背景（V1_SPEC §2.3）；先用烘焙稿占位 |
| 烘焙 GI 增加构建时间与仓库体积 | 中 | 低 | 只烘焙城镇静态物体；光照贴图分辨率按 30 像素／单位上限控制 |

## Performance Implications

| Metric | Before（Glade 原型，ADR-0001 基线） | Expected After（样板场景） | Budget |
|--------|--------|---------------|--------|
| CPU+GPU 帧时间 | 0.68 ms（不限帧，开发机） | 城镇样板实测（IL2CPP，1080p，不限帧，Quadro P2000，Gamma 下测得）：High 平均 2.94 ms（p95 3.53、p99 4.09），Low 平均 2.42 ms（p95 3.45）；Linear、战斗样板与城镇美感样张的数据见实现 PR，并回填到 `production/` 记录 | 16.6 ms @1080p；最低配置待实测 |
| Memory | 约 190 MB 工作集 | 城镇样板实测约 195–201 MB 工作集；战斗背景一套约 3.7 MB（640×360×4 层 RGBA） | ≤ 2 GB；城镇光照贴图 ≤ 64 MB |
| Load Time | — | 城镇↔战斗叠加加载切换 ≤ 3 s，战斗样板实测 | 同 ADR-0001 |

验证方法：IL2CPP 构建 + `PerfBaselineProbe`（`-scene <场景> -perfBaseline 30 -perfUncapped`，Low 档加 `-hd2dLow`），城镇与战斗样板各在 High 与 Low 档测一次；最低配置在对应硬件上复测（未测前标为待验证）。开发机 Quadro P2000 的性能大致介于 GTX 1050 Ti 与 GTX 1060 之间，城镇样板在其上的帧时间不到预算的 20%，最低配置达标的可能性较高，但仍需实机确认。

## Migration Plan

1. 新建样板场景 `Assets/_Project/Scenes/HD2D_Sample.unity`（不改 Glade）：灰盒涿郡街道、灯笼点光、暖色方向光、刘备像素人物面片。（已完成，PR #6）
2. URP 资产与 Renderer：新增 `HD2D_URP` / `HD2D_Renderer`（Forward+），保留 `GladeURP` 给原型；全局默认管线切到 `HD2D_URP` 前确认 Glade 测试仍通过。（已完成，PR #6）
3. 实现 `PixelSpriteConverter`（EditorTools）与 `PixelBillboard`、`SpriteFrameAnimator`（Gameplay）。（已完成，PR #6）
4. 建 `HD2D_Default` Volume Profile 与 High／Low 画质档位。（已完成，PR #6）
5. PlayMode 测试覆盖样板场景装配；IL2CPP 构建并记录性能。（已完成，PR #6）
6. **切换 Linear 颜色空间**，单独提交：Player Settings 与 `Win64-Release` Build Profile 中复制的 Player Settings 两处都要改（后者不会跟随全局设置）→ 全部贴图重新导入 → 核对 sRGB：像素图集与颜色贴图开，LUT、遮罩、数据贴图关 → 重调方向光、环境光、点光强度与 Bloom 阈值 → 确认 Glade 与样板材质无异常 → 重跑 EditMode／PlayMode 与 IL2CPP 性能测量并更新数据。
7. 建共用暖色 LUT（规格见第 5 节）并接入城镇与战斗 Volume；测试断言两边引用的是同一个 LUT 资源。
8. 战斗背景样板：`BattleBackdropBaker` 烘焙 640×360 分层背景 + 光向元数据，战斗样板场景（正交相机、无景深、影子贴片、我方 3 人对敌方 4 人站位）；IL2CPP 构建并测量帧时间与内存。
9. 城镇美感样张：选一处街角，按第 8 节的五个因素精修（手绘风格贴图与场景布置、烘焙 GI／AO、雾、LUT、Linear），记录光照贴图大小与帧时间。
10. 回填 GDD：`design/gdd/combat-system.md` 中“战斗背景待定”由本 ADR 关闭。

## Validation Criteria

- [ ] 样板场景存在且能在编辑器 Play 与 IL2CPP 构建中运行，无报错、无粉色材质
- [ ] 相机为透视、FOV 20°、俯角 25°；主角在 1080p 下约 130–144 像素高（含方形像素补偿）
- [ ] 刘备像素人物为 32×48 逻辑像素，Point 过滤、无 Mipmap，边缘无半透明毛边
- [ ] 人物受方向光与灯笼点光照明，并在地面投影
- [ ] 人物面片只绕 Y 轴对齐相机偏航角，移动时不倾倒；移动时人物像素闪烁程度由用户确认可接受
- [ ] Renderer 为 Forward+；Volume 含 Bokeh 景深、Bloom、ACES、暗角、调色；Low 档关闭景深
- [ ] 开发机 1080p 帧时间（High／Low）已记录；最低配置 60 fps 为待验证项或已实测
- [ ] 原有 EditMode／PlayMode 测试仍全部通过，并新增样板场景装配测试
- [ ] Player Settings 与 `Win64-Release` 的颜色空间均为 Linear，构建日志显示 Color Space = 1，样板在 Linear 下重新截图
- [ ] `ColorLookup` 的 LUT 通过规格校验；PlayMode 测试断言城镇与战斗 Volume 引用的 LUT 资源 GUID 相同
- [ ] 战斗背景样板：至少 3 层 640×360 背景，正交相机，1080p 下 3 倍整数放大；我方 3 人对敌方 4 人互不遮挡且在安全区内；战斗 Volume 无景深；方向光取自背景元数据；人物有影子贴片
- [ ] 战斗背景样板在 IL2CPP 下的帧时间与内存已记录
- [ ] 城镇美感样张：光照贴图已烘焙、雾开启、Volume 引用 LUT（客观项）；整体观感由用户看截图确认（主观项）

## 用户确认（2026-10-19）

| # | 问题 | 确认结果 |
| --- | --- | --- |
| 1 | 相机 | 低 FOV 透视（20°／25°，俯角 2026-10-19 由 30° 改为 25°），面片高度乘 1/cos(俯角) |
| 2 | 人物逻辑尺寸 | 32×48 |
| 3 | 人物材质 | URP Lit 面片 + Alpha Clip |
| 4 | 景深 | Bokeh（300 mm、f/2），Low 档关闭；超预算时备选自写移轴 Pass |
| 5 | 渲染路径 | Forward+ |
| 6 | 颜色空间 | Linear |
| 7 | 美术管线 | AI 稿自动缩小 + Aseprite 人工清理 |
| 8 | 画面方案 | 战斗用分层 2D 像素背景，城镇／探索用 3D（第 8 节） |
| 9 | 环境美术 | 正式贴图人工精修（先屋顶、墙）；像素书法／字库；独立屋脊与檐角模型；Linear（第 9 节） |
| 10 | 文字 | 场景内招牌、幌子用繁体；界面用简体（第 6 节第 6 条） |

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| PROJECT.md §5、V1_SPEC §10 | 画面 | HD-2D：像素人物 + 立体场景光影；首版基本一致的中式 HD-2D 样板 | 透视相机 + 受光像素面片 + Volume 后处理 + 统一像素密度 |
| V1_SPEC §10 | 画面 | 刘关张 3 名可操作像素武将、步骑弓区别、简化动画 | 32×48 逻辑尺寸与帧规格、转换管线 |
| V1_SPEC §2.2、§2.3 | 战斗 | 3 场战斗、最多 2 套战斗背景；第二战敌方 4 人 | 第 8 节分层背景、640×360 站位安全区、背景数量计数规则 |
| `design/gdd/combat-system.md`（docs/systems-gdds 分支） | 战斗表现 | “最多 2 套战斗背景”“战斗背景待定” | 第 8 节关闭“战斗背景待定”，GDD 需回填 |
| `design/gdd/town-exploration.md`（docs/systems-gdds 分支） | 城镇 | 涿郡可探索城镇、桃园演出区域 | 城镇／桃园走 3D 管线（第 2–5、8 节） |
| INITIAL_SCENARIO_ART.md | 美术 | 普通黄巾共用 Q 版，张角三兄弟独立 Q 版 | 同一管线转换各 3×3 表 |
| ADR-0001 风险表 | 渲染 | Sprite-Lit-Default 在 Universal Renderer 下不受 3D 光 | URP Lit 面片方案 |

## Related

- ADR-0001：引擎、平台目标与工程结构基线
- 后续：画质设置界面与分辨率缩放（UI 阶段，含 Pixel Perfect Camera 核实）；可能的 Render Graph 移轴 Pass；Lit 精灵着色器变体与阴影接收地面材质；GDD 回填（combat-system.md 战斗背景）

## 评审记录（2026-10-19）

两项评审均在本 ADR 写入混合方案后运行（只读），结论都是 **CONCERNS**，没有 REJECT。所有意见已改进正文，处理如下：

| 来源 | 意见 | 处理 |
| --- | --- | --- |
| unity-specialist 1／technical-director 1 | 正交相机下 URP 景深按透视深度计算会出错；§8 战斗相机未定 | 新增 8.1：正交尺寸 6、不补偿、战斗无景深、前景虚化画进图 |
| unity-specialist 2／technical-director 3 | 不受光背景经 ACES＋LUT＋Bloom 会二次调色 | 新增 8.2：按游戏内最终效果预览定稿；战斗 Bloom 阈值高于 1；色板核对 |
| unity-specialist 3／technical-director 1 | 背景不接收阴影，人物投影不会出现 | 8.1：战斗用影子贴片；阴影接收材质列为后续补充 |
| technical-director 2／unity-specialist 9 | Linear 没有迁移步骤，现有数据测于 Gamma | Migration Plan 第 6 步；性能表注明 Gamma；Build Profile 副本也要改 |
| unity-specialist 5 | `ColorLookup` LUT 规格不符会静默失效 | 第 5 节写明 LUT 规格；验收项测试 LUT |
| technical-director 4 | 问题陈述与后果没覆盖混合方案代价 | Problem Statement、Requirements、Negative 已补 |
| technical-director 5 | 整数倍缩放只对 1080p 成立 | 新增 8.3：最大整数倍 + 黑边；实现与 Pixel Perfect Camera 核实放到设置故事 |
| technical-director 6 | 战斗与切换、光照贴图没有预算 | 新增 8.4；性能表增加战斗与光照贴图预算 |
| technical-director 7 | 验收写成 3 对 3、主观项不可核对 | 改为 3 对 4、光向元数据、LUT GUID 测试、主观项由用户确认 |
| technical-director 8 | 新组件归属与 IL2CPP 变体剥离 | 新增 8.5 |
| technical-director 9 | GDD 关联不全，过场背景管线未定 | GDD 表增加两行；8.4 写明桃园与徐州过场的管线 |
| technical-director 10 | 文本不一致 | Status、Decision Makers 计数与第 7 节已修正 |
| unity-specialist 4 | 灯笼投影开关在 URP 资产关闭时无效 | 第 1 节写明 V1 点光不投影及开启条件 |
| unity-specialist 6 | 透视跟随下像素闪烁 | 第 2 节：V1 接受，列为人工验收项，备选相机取整 |
| unity-specialist 7 | Forward+ 限制说法不准 | 第 1 节改为 256 盏／相机、64 反射探针，支持混合光照 |
| unity-specialist 8 | 焦距 300 mm 已是上限 | 第 5 节注明 |
| unity-specialist 10 | `MaterialPropertyBlock` 退出 SRP Batcher、阴影法线偏移、档位实现 | 第 1、4、7 节写明 |
