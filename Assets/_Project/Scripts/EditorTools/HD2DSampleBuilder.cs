using System.IO;
using System.Linq;
using Jrpg;
using ThreeKingdoms.App;
using ThreeKingdoms.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ThreeKingdoms.EditorTools
{
    // Builds the ADR-0005 HD-2D sample: URP asset + Forward+ renderer, High/Low volume profiles,
    // pixel character atlases converted from the AI pose sheets, and a grey-box Zhuojun street scene.
    // Re-running regenerates everything at the same paths (asset GUIDs are kept where the asset exists).
    public static class HD2DSampleBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/HD2D_Sample.unity";
        public const string PipelinePath = "Assets/_Project/Settings/HD2D_URP.asset";
        public const string RendererPath = "Assets/_Project/Settings/HD2D_Renderer.asset";
        public const string HighProfilePath = "Assets/_Project/Settings/HD2D_Default.asset";
        public const string LowProfilePath = "Assets/_Project/Settings/HD2D_Low.asset";
        const string MaterialDir = "Assets/_Project/Art/Materials/HD2D";
        const string CharacterDir = "Assets/_Project/Art/Sprites/Characters";
        const string DefaultArtSource = @"D:\BuildArtifacts\Codex\hd2d-jrpg";

        // ADR-0005 camera and pixel-density decisions.
        public const float CameraFov = 20f, CameraPitch = 30f, CameraDistance = 34f;
        public const float PixelsPerUnit = 30f;
        public const int FrameSize = 48;

        static readonly (string Key, string Sheet, int FigureHeight)[] Characters =
        {
            ("LiuBei", @"liu-bei-hd2d-sprites-20261018-v3\LIU_BEI_HD2D_3X3.png", 44),
            ("GuanYu", @"guan-zhang-zhao-art-20261018-v1\GUAN_YU_HD2D_3X3.png", 47),
            ("ZhangFei", @"guan-zhang-zhao-art-20261018-v1\ZHANG_FEI_HD2D_3X3.png", 46),
            ("YellowTurban", @"initial-scenario-enemy-art-20261018-v1\YELLOW_TURBAN_GENERIC_HD2D_3X3.png", 43),
        };

        [MenuItem("JRPG/Build HD-2D Sample Scene")]
        public static void Build()
        {
            ConvertCharacters();
            var pipeline = CreatePipeline();
            var high = CreateProfile(HighProfilePath, lowTier: false);
            var low = CreateProfile(LowProfilePath, lowTier: true);
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            BuildScene(high, low);
            AssetDatabase.SaveAssets();
            Debug.Log("HD2D_SAMPLE_BUILT");
        }

        public static void BuildFromCommandLine()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static string AtlasPath(string key) => $"{CharacterDir}/{key}/{key}_Atlas.png";

        static void ConvertCharacters()
        {
            string root = System.Environment.GetEnvironmentVariable("HD2D_ART_SOURCE");
            if (string.IsNullOrEmpty(root)) root = DefaultArtSource;
            foreach (var (key, sheet, figureHeight) in Characters)
            {
                string source = Path.Combine(root, sheet);
                if (!File.Exists(source))
                {
                    if (!File.Exists(AtlasPath(key))) throw new FileNotFoundException("Missing source sheet and atlas for " + key, source);
                    Debug.Log($"Source sheet for {key} not found; keeping the committed atlas.");
                    continue;
                }
                var settings = PixelSpriteConverter.Settings.Default;
                settings.FrameSize = FrameSize;
                settings.FigureHeight = figureHeight;
                PixelSpriteConverter.ConvertSheet(source, AtlasPath(key), settings);
            }
        }

        static UniversalRenderPipelineAsset CreatePipeline()
        {
            var data = LoadOrCreate<UniversalRendererData>(RendererPath, () => ScriptableObject.CreateInstance<UniversalRendererData>());
            data.renderingMode = RenderingMode.ForwardPlus;
            data.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            EditorUtility.SetDirty(data);

            var pipeline = LoadOrCreate(PipelinePath, () => UniversalRenderPipelineAsset.Create(data));
            // Serialized field names verified against URP 17.3 (UniversalRenderPipelineAsset.cs); a missing
            // name fails loudly instead of silently leaving a default.
            var so = new SerializedObject(pipeline);
            SerializedProperty P(string name) => so.FindProperty(name) ?? throw new System.MissingMemberException(nameof(UniversalRenderPipelineAsset), name);
            P("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue = data;
            P("m_SupportsHDR").boolValue = true;
            P("m_MSAA").intValue = 1;
            P("m_RenderScale").floatValue = 1f;
            P("m_MainLightShadowsSupported").boolValue = true;
            P("m_MainLightShadowmapResolution").intValue = 2048;
            P("m_ShadowDistance").floatValue = 40f;
            P("m_ShadowCascadeCount").intValue = 2;
            P("m_SoftShadowsSupported").boolValue = true;
            P("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
            P("m_AdditionalLightShadowsSupported").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            return pipeline;
        }

        static VolumeProfile CreateProfile(string path, bool lowTier)
        {
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path)) AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var dof = profile.Add<DepthOfField>(true);
            dof.active = !lowTier;
            dof.mode.Override(lowTier ? DepthOfFieldMode.Off : DepthOfFieldMode.Bokeh);
            dof.focusDistance.Override(CameraDistance);
            dof.focalLength.Override(300f);
            dof.aperture.Override(2f);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(lowTier ? 0.4f : 0.6f);
            bloom.scatter.Override(0.7f);
            bloom.highQualityFiltering.Override(!lowTier);

            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.4f);

            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0f);
            color.contrast.Override(10f);
            color.saturation.Override(10f);
            color.colorFilter.Override(new Color(1f, 0.96f, 0.9f));

            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        static void BuildScene(VolumeProfile high, VolumeProfile low)
        {
            Directory.CreateDirectory(MaterialDir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            // Linear colour space (ADR-0005): ambient and sun are brighter than the Gamma-era values to keep the same exposure.
            RenderSettings.ambientEquatorColor = new Color(0.72f, 0.60f, 0.50f);
            RenderSettings.ambientGroundColor = new Color(0.36f, 0.29f, 0.23f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.006f;
            RenderSettings.fogColor = new Color(0.78f, 0.62f, 0.50f);

            var dirt = Lit("Dirt", new Color(0.55f, 0.44f, 0.31f));
            var stone = Lit("Paving", new Color(0.63f, 0.59f, 0.52f));
            var plaster = Lit("Plaster", new Color(0.80f, 0.71f, 0.56f));
            var wood = Lit("Timber", new Color(0.40f, 0.26f, 0.16f));
            var tile = Lit("RoofTile", new Color(0.26f, 0.28f, 0.33f));
            var leaves = Lit("Leaves", new Color(0.30f, 0.42f, 0.22f));
            var peach = Lit("PeachBlossom", new Color(0.95f, 0.62f, 0.72f));
            var lanternGlow = Lit("LanternGlow", new Color(0.9f, 0.3f, 0.12f), new Color(4f, 1.6f, 0.5f));
            var cloth = Lit("StallCloth", new Color(0.82f, 0.78f, 0.66f));

            var env = new GameObject("Environment").transform;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground"; ground.transform.SetParent(env); ground.transform.localScale = new Vector3(20, 1, 20);
            ground.GetComponent<Renderer>().sharedMaterial = dirt;
            Box(env, "Main street", new Vector3(0, 0.02f, 10), new Vector3(5, 0.04f, 52), stone);

            float[] rows = { -6, 3, 12, 21 };
            for (int i = 0; i < rows.Length; i++)
            {
                House(env, $"House W{i}", new Vector3(-7.5f, 0, rows[i]), i % 2 == 0 ? 6f : 5f, plaster, wood, tile);
                House(env, $"House E{i}", new Vector3(7.5f, 0, rows[i] + 2f), i % 2 == 0 ? 5f : 6.5f, plaster, wood, tile);
            }
            Gate(env, new Vector3(0, 0, 32), wood, tile, plaster);
            Stall(env, new Vector3(3.6f, 0, 7f), wood, cloth);
            Stall(env, new Vector3(-3.6f, 0, 15f), wood, cloth);
            Well(env, new Vector3(-3.4f, 0, -1.5f), stone, wood);
            for (int i = 0; i < 6; i++)
            {
                Tree(env, new Vector3(-13f, 0, -6 + i * 7f), wood, i % 3 == 0 ? peach : leaves);
                Tree(env, new Vector3(13f, 0, -3 + i * 7f), wood, leaves);
            }
            for (int i = 0; i < 5; i++)
            {
                Lantern(env, new Vector3(-2.9f, 0, -3 + i * 7f), wood, lanternGlow);
                Lantern(env, new Vector3(2.9f, 0, 0.5f + i * 7f), wood, lanternGlow);
            }

            var sun = new GameObject("Evening sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.80f, 0.58f);
            sun.intensity = 2.3f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.transform.rotation = Quaternion.Euler(38f, -35f, 0f);

            var player = Character("Liu Bei", "LiuBei", new Vector3(0, 0, -2), 0, isPlayer: true);
            Character("Guan Yu", "GuanYu", new Vector3(-1.5f, 0, 0.6f), 0, isPlayer: false);
            Character("Zhang Fei", "ZhangFei", new Vector3(1.6f, 0, 0.9f), 0, isPlayer: false);
            Character("Yellow Turban", "YellowTurban", new Vector3(2.2f, 0, 17f), 0, isPlayer: false);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = false;
            cam.fieldOfView = CameraFov;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 250f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.None;
            Quaternion pitch = Quaternion.Euler(CameraPitch, 0f, 0f);
            camGo.transform.rotation = pitch;
            var follow = camGo.AddComponent<FollowCamera>();
            follow.Target = player.transform;
            follow.Offset = new Vector3(0, 0.8f, 0) + pitch * new Vector3(0, 0, -CameraDistance);
            camGo.transform.position = player.transform.position + follow.Offset;

            var volumeGo = new GameObject("Global Volume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = high;
            var tier = volumeGo.AddComponent<HD2DQualityTier>();
            tier.Volume = volume; tier.High = high; tier.Low = low;

            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static GameObject Character(string name, string key, Vector3 position, int frame, bool isPlayer)
        {
            var root = new GameObject(name);
            root.transform.position = position;
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath(key));
            var material = PixelMaterial(key, atlas);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.name = "Pixel sprite";
            quad.transform.SetParent(root.transform, false);
            float size = FrameSize / PixelsPerUnit;
            quad.transform.localPosition = new Vector3(0, size / 2f, 0);
            quad.transform.localScale = new Vector3(size, size, 1);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            quad.AddComponent<PixelBillboard>().BaseSize = new Vector2(size, size);
            var animator = quad.AddComponent<SpriteFrameAnimator>();
            animator.Target = renderer; animator.Columns = 9; animator.Rows = 1; animator.Frame = frame;

            if (isPlayer)
            {
                var controller = root.AddComponent<CharacterController>();
                controller.height = 1.4f; controller.radius = 0.3f; controller.center = new Vector3(0, 0.7f, 0);
                var mover = root.AddComponent<PlayerMover>();
                mover.MoveAction = ProjectSetup.Action("Move");
                root.AddComponent<PixelWalkFrames>().Animator = animator;
            }
            else
            {
                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.height = 1.4f; capsule.radius = 0.3f; capsule.center = new Vector3(0, 0.7f, 0);
            }
            return root;
        }

        static Material PixelMaterial(string key, Texture2D atlas)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = key + "_Pixel" };
            m.SetTexture("_BaseMap", atlas);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", (float)CullMode.Off);
            m.SetFloat("_Smoothness", 0f);
            m.SetFloat("_SpecularHighlights", 0f); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            m.renderQueue = (int)RenderQueue.AlphaTest;
            return SaveMaterial(m, $"{CharacterDir}/{key}/{key}_Pixel.mat");
        }

        static Material Lit(string name, Color color, Color? emission = null)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", 0.12f);
            if (emission.HasValue)
            {
                m.SetColor("_EmissionColor", emission.Value);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            return SaveMaterial(m, $"{MaterialDir}/{name}.mat");
        }

        static Material SaveMaterial(Material m, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) { existing.CopyPropertiesFromMaterial(m); existing.shaderKeywords = m.shaderKeywords; existing.renderQueue = m.renderQueue; EditorUtility.SetDirty(existing); Object.DestroyImmediate(m); return existing; }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static T LoadOrCreate<T>(string path, System.Func<T> create) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset) return asset;
            asset = create();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material, Vector3 euler = default)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localRotation = Quaternion.Euler(euler);
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }

        static void House(Transform parent, string name, Vector3 at, float width, Material plaster, Material wood, Material tile)
        {
            var house = new GameObject(name).transform;
            house.SetParent(parent, false);
            house.localPosition = at;
            const float depth = 4.2f, wall = 2.4f;
            Box(house, "Walls", new Vector3(0, wall / 2, 0), new Vector3(width, wall, depth), plaster);
            Box(house, "Beam", new Vector3(0, wall - 0.1f, -depth / 2 - 0.02f), new Vector3(width + 0.1f, 0.2f, 0.1f), wood);
            Box(house, "Door", new Vector3(at.x < 0 ? width / 2 + 0.02f : -width / 2 - 0.02f, 0.9f, 0), new Vector3(0.06f, 1.8f, 1.1f), wood);
            float slope = 26f, half = depth / 2 + 0.5f, slab = half / Mathf.Cos(slope * Mathf.Deg2Rad);
            float rise = Mathf.Tan(slope * Mathf.Deg2Rad) * half;
            Box(house, "Roof N", new Vector3(0, wall + rise / 2, half / 2), new Vector3(width + 0.8f, 0.18f, slab), tile, new Vector3(slope, 0, 0));
            Box(house, "Roof S", new Vector3(0, wall + rise / 2, -half / 2), new Vector3(width + 0.8f, 0.18f, slab), tile, new Vector3(-slope, 0, 0));
            Box(house, "Ridge", new Vector3(0, wall + rise + 0.05f, 0), new Vector3(width + 1.0f, 0.22f, 0.3f), tile);
        }

        static void Gate(Transform parent, Vector3 at, Material wood, Material tile, Material plaster)
        {
            var gate = new GameObject("Town gate").transform;
            gate.SetParent(parent, false);
            gate.localPosition = at;
            Box(gate, "Wall W", new Vector3(-9f, 1.8f, 0), new Vector3(12f, 3.6f, 1.6f), plaster);
            Box(gate, "Wall E", new Vector3(9f, 1.8f, 0), new Vector3(12f, 3.6f, 1.6f), plaster);
            Box(gate, "Pillar W", new Vector3(-2.8f, 2.2f, 0), new Vector3(0.6f, 4.4f, 0.6f), wood);
            Box(gate, "Pillar E", new Vector3(2.8f, 2.2f, 0), new Vector3(0.6f, 4.4f, 0.6f), wood);
            Box(gate, "Lintel", new Vector3(0, 4.3f, 0), new Vector3(6.8f, 0.5f, 1.2f), wood);
            Box(gate, "Gate roof", new Vector3(0, 4.85f, 0), new Vector3(8.2f, 0.4f, 2.6f), tile);
            Box(gate, "Gate ridge", new Vector3(0, 5.2f, 0), new Vector3(6.4f, 0.35f, 0.6f), tile);
        }

        static void Stall(Transform parent, Vector3 at, Material wood, Material cloth)
        {
            var stall = new GameObject("Market stall").transform;
            stall.SetParent(parent, false);
            stall.localPosition = at;
            Box(stall, "Counter", new Vector3(0, 0.45f, 0), new Vector3(1.8f, 0.9f, 1.0f), wood);
            for (int i = 0; i < 4; i++)
                Box(stall, "Post", new Vector3(i % 2 == 0 ? -0.85f : 0.85f, 1.1f, i < 2 ? -0.45f : 0.45f), new Vector3(0.08f, 2.2f, 0.08f), wood);
            Box(stall, "Awning", new Vector3(0, 2.25f, 0), new Vector3(2.2f, 0.06f, 1.4f), cloth, new Vector3(-8f, 0, 0));
        }

        static void Well(Transform parent, Vector3 at, Material stone, Material wood)
        {
            var well = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            well.name = "Well"; well.transform.SetParent(parent, false);
            well.transform.localPosition = at + new Vector3(0, 0.4f, 0);
            well.transform.localScale = new Vector3(1.4f, 0.4f, 1.4f);
            well.GetComponent<Renderer>().sharedMaterial = stone;
            Box(parent, "Well frame", at + new Vector3(0, 1.3f, 0), new Vector3(1.6f, 0.12f, 0.12f), wood);
        }

        static void Tree(Transform parent, Vector3 at, Material trunk, Material crown)
        {
            var t = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            t.name = "Tree trunk"; t.transform.SetParent(parent, false);
            t.transform.localPosition = at + new Vector3(0, 1.2f, 0);
            t.transform.localScale = new Vector3(0.35f, 1.2f, 0.35f);
            t.GetComponent<Renderer>().sharedMaterial = trunk;
            var c = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            c.name = "Tree crown"; c.transform.SetParent(parent, false);
            c.transform.localPosition = at + new Vector3(0, 3.0f, 0);
            c.transform.localScale = new Vector3(2.6f, 2.2f, 2.6f);
            c.GetComponent<Renderer>().sharedMaterial = crown;
        }

        static void Lantern(Transform parent, Vector3 at, Material wood, Material glow)
        {
            Box(parent, "Lantern post", at + new Vector3(0, 1.1f, 0), new Vector3(0.1f, 2.2f, 0.1f), wood);
            Box(parent, "Lantern", at + new Vector3(0, 2.05f, 0), new Vector3(0.32f, 0.42f, 0.32f), glow);
            var light = new GameObject("Lantern light").AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.localPosition = at + new Vector3(0, 2.0f, 0);
            light.type = LightType.Point;
            light.range = 6f;
            light.intensity = 2.2f;
            light.color = new Color(1f, 0.58f, 0.28f);
            light.shadows = LightShadows.None;
        }
    }
}
