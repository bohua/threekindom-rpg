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
            var kit = HD2DEnvironmentKit.CreatePalette();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            // Linear colour space (ADR-0005): ambient and sun are brighter than the Gamma-era values to keep the same exposure.
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.80f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.72f, 0.60f, 0.50f);
            RenderSettings.ambientGroundColor = new Color(0.36f, 0.29f, 0.23f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.006f;
            RenderSettings.fogColor = new Color(0.78f, 0.62f, 0.50f);

            var env = new GameObject("Environment").transform;
            HD2DEnvironmentKit.Ground(env, kit);

            // Houses face the street: west row yaw -90 (front toward +X), east row yaw +90.
            float[] rows = { -6, 3, 12, 21 };
            var styles = new[] { HD2DEnvironmentKit.HouseStyle.Tiled, HD2DEnvironmentKit.HouseStyle.Tavern, HD2DEnvironmentKit.HouseStyle.Thatched, HD2DEnvironmentKit.HouseStyle.Tiled };
            for (int i = 0; i < rows.Length; i++)
            {
                HD2DEnvironmentKit.House(env, $"House W{i}", new Vector3(-7.5f, 0, rows[i]), -90f, i % 2 == 0 ? 6f : 5f, styles[i], kit);
                HD2DEnvironmentKit.House(env, $"House E{i}", new Vector3(7.5f, 0, rows[i] + 2f), 90f, i % 2 == 0 ? 5f : 6.5f, styles[(i + 2) % styles.Length], kit);
            }
            HD2DEnvironmentKit.GateTower(env, new Vector3(0, 0, 32), kit);
            HD2DEnvironmentKit.CityWall(env, "City wall W", new Vector3(-11.4f, 0, 32), 14f, kit);
            HD2DEnvironmentKit.CityWall(env, "City wall E", new Vector3(11.4f, 0, 32), 14f, kit);
            HD2DEnvironmentKit.Stall(env, new Vector3(3.6f, 0, 7f), 0f, kit);
            HD2DEnvironmentKit.Stall(env, new Vector3(-3.6f, 0, 15f), 0f, kit);
            HD2DEnvironmentKit.Well(env, new Vector3(-3.4f, 0, -1.5f), kit);
            for (int i = 0; i < 6; i++)
            {
                HD2DEnvironmentKit.Tree(env, new Vector3(-13f, 0, -6 + i * 7f), kit, i % 3 == 0 ? kit.Blossom : null);
                HD2DEnvironmentKit.Tree(env, new Vector3(13f, 0, -3 + i * 7f), kit);
            }
            for (int i = 0; i < 5; i++)
            {
                HD2DEnvironmentKit.Lantern(env, new Vector3(-2.9f, 0, -3 + i * 7f), kit);
                HD2DEnvironmentKit.Lantern(env, new Vector3(2.9f, 0, 0.5f + i * 7f), kit);
            }
            HD2DEnvironmentKit.EndMeshes();

            // Inactive review cameras for close-up shots of the kit (launch with -camera <name>, ideally -hd2dLow so
            // the gameplay-distance depth of field does not blur them).
            ReviewCamera("TavernView", new Vector3(2f, 4.5f, -2f), new Vector3(16f, -58f, 0f), 35f);
            ReviewCamera("GateView", new Vector3(0f, 6f, 17f), new Vector3(12f, 0f, 0f), 35f);

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

        static void ReviewCamera(string name, Vector3 position, Vector3 euler, float fov)
        {
            var cam = new GameObject(name).AddComponent<Camera>();
            cam.enabled = false;
            cam.fieldOfView = fov;
            cam.transform.SetPositionAndRotation(position, Quaternion.Euler(euler));
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

    }
}
