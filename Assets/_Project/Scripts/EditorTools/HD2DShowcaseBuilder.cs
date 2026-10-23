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
    // The HD-2D polish sample (design/art/hd2d-visual-target.md): one corner of the Zhuojun market built in three
    // depth layers like the Octopath Traveler II reference (terrace and steps at the back, cobbled square in the
    // middle, a balustrade over a retaining wall in front), lit and graded with the proposed look: low warm sun,
    // cool dark ambient, SSAO, normal maps on the pixel textures, split toning instead of the warm LUT.
    // The town keeps its own lighting and profiles; nothing here changes other scenes.
    // -showcaseBaseline builds the same geometry with the town's current look (HD2D_Default, no AO, no normals)
    // so the two can be compared shot for shot.
    public static class HD2DShowcaseBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/HD2D_Showcase.unity";
        public const string BaselineScenePath = "Assets/_Project/Scenes/HD2D_Showcase_Baseline.unity";
        // New Codex art for the sample, named after the town keys it replaces (e.g. roof_tiles_grey.png), plus
        // optional <key>_height.png height maps. Empty until the art arrives; the town library is used meanwhile.
        public const string ArtDir = "Assets/_Project/Art/Environment/HD2DShowcase";
        const string MaterialDir = ArtDir + "/Materials";
        const string BaselineMaterialDir = ArtDir + "/BaselineMaterials";
        const string NormalDir = ArtDir + "/Normals";
        const string MeshPath = ArtDir + "/Showcase_Meshes.asset";
        public const string ProfilePath = "Assets/_Project/Settings/HD2D_Showcase.asset";
        public const string RendererPath = "Assets/_Project/Settings/HD2D_Showcase_Renderer.asset";

        public const float TerraceHeight = 1.8f, LowerStreet = -2.0f;   // the drop equals the 2 m retaining-wall texture
        const float NormalStrength = 1.6f;

        [MenuItem("JRPG/Build HD-2D Showcase (polish sample)")]
        public static void Build() => Build(baseline: false);

        [MenuItem("JRPG/Build HD-2D Showcase (current look, for comparison)")]
        public static void BuildBaseline() => Build(baseline: true);

        // Build both scenes and capture them in one editor run (-shotDir <dir>).
        public static void BuildAndCaptureFromCommandLine()
        {
            try
            {
                Build(baseline: false);
                Build(baseline: true);
                CaptureAll();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                Build(baseline: false);
                Build(baseline: true);
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static void Build(bool baseline)
        {
            Directory.CreateDirectory(ArtDir);
            ConfigureOverrideTextures();
            ZhuojunTownBuilder.OverrideDir = baseline ? null : ArtDir;
            try
            {
                string materials = baseline ? BaselineMaterialDir : MaterialDir;
                ZhuojunTownBuilder.BeginSession(materials, baseline ? MeshPath.Replace(".asset", "_Baseline.asset") : MeshPath);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Atmosphere(baseline);
                var env = new GameObject("Environment").transform;
                Layout(env);
                ZhuojunTownBuilder.EndSession();
                if (!baseline) AddNormalMaps(materials);
                Sun(baseline);
                People();
                CameraAndVolume(baseline);
            }
            finally { ZhuojunTownBuilder.OverrideDir = null; }

            string path = baseline ? BaselineScenePath : ScenePath;
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), path);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log(baseline ? "HD2D_SHOWCASE_BASELINE_BUILT" : "HD2D_SHOWCASE_BUILT");
        }

        // ---------- look ----------

        static void Atmosphere(bool baseline)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            if (baseline)
            {
                // ZhuojunTownBuilder.Build's values.
                RenderSettings.ambientSkyColor = new Color(0.78f, 0.80f, 0.92f);
                RenderSettings.ambientEquatorColor = new Color(0.70f, 0.64f, 0.58f);
                RenderSettings.ambientGroundColor = new Color(0.36f, 0.30f, 0.24f);
                RenderSettings.fogDensity = 0.006f;
                RenderSettings.fogColor = new Color(0.78f, 0.74f, 0.68f);
                return;
            }
            // Cooler and lower than the town, so the shade reads blue-grey against the warm sun, but bright enough that shade stays clean (user 2026-10-23: the first pass read dim and dirty).
            RenderSettings.ambientSkyColor = new Color(0.50f, 0.54f, 0.64f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.40f, 0.40f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.19f, 0.16f);
            RenderSettings.fogDensity = 0.005f;
            RenderSettings.fogColor = new Color(0.50f, 0.52f, 0.58f);
        }

        static void Sun(bool baseline)
        {
            var sun = new GameObject("Late-afternoon sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            if (baseline)
            {
                sun.color = new Color(1f, 0.92f, 0.80f);
                sun.intensity = 2.2f;
                sun.shadowStrength = 0.8f;
                sun.transform.rotation = Quaternion.Euler(42f, -32f, 0f);
                return;
            }
            // Low from the south-west, in front-left of the camera so the façades and the pixel characters (quads
            // facing the camera) stay lit; a backlight turns them into silhouettes. Shadows fall north-east.
            sun.color = new Color(1f, 0.84f, 0.64f);
            sun.intensity = 4.0f;
            sun.shadowStrength = 1f;
            sun.transform.rotation = Quaternion.Euler(32f, 48f, 0f);
            // Passing-cloud cookie: breaks the square into pools of sun and shade like the reference, without
            // adding geometry. Tiles every CookieSize metres.
            sun.cookie = CloudCookie();
            var data = sun.gameObject.AddComponent<UniversalAdditionalLightData>();
            data.lightCookieSize = new Vector2(CookieSize, CookieSize);
            data.lightCookieOffset = new Vector2(6f, 3f);
        }

        const float CookieSize = 26f;
        const string CookiePath = ArtDir + "/Generated_SunCookie.png";

        // Smooth value noise in [0.6, 1]: a few big soft blobs, tileable.
        static Texture2D CloudCookie()
        {
            const int size = 128, cells = 4;
            var rng = new System.Random(1123);
            var lattice = new float[cells, cells];
            for (int j = 0; j < cells; j++) for (int i = 0; i < cells; i++) lattice[i, j] = (float)rng.NextDouble();
            float Smooth(float t) => t * t * (3 - 2 * t);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float fx = x * cells / (float)size, fy = y * cells / (float)size;
                int x0 = (int)fx, y0 = (int)fy, x1 = (x0 + 1) % cells, y1 = (y0 + 1) % cells;
                float tx = Smooth(fx - x0), ty = Smooth(fy - y0);
                float v = Mathf.Lerp(Mathf.Lerp(lattice[x0, y0], lattice[x1, y0], tx), Mathf.Lerp(lattice[x0, y1], lattice[x1, y1], tx), ty);
                v = Mathf.Lerp(0.6f, 1f, Mathf.SmoothStep(0.25f, 0.7f, v));
                byte b = (byte)(v * 255);
                px[y * size + x] = new Color32(b, b, b, 255);
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            File.WriteAllBytes(CookiePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(CookiePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(CookiePath);
            importer.textureType = TextureImporterType.Cookie;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.alphaSource = TextureImporterAlphaSource.FromGrayScale;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(CookiePath);
        }

        static void CameraAndVolume(bool baseline)
        {
            var player = GameObject.Find("Liu Bei");
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = HD2DSampleBuilder.CameraFov;
            cam.nearClipPlane = 1f; cam.farClipPlane = 250f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.None;
            if (!baseline) camData.SetRenderer(ShowcaseRendererIndex());
            Quaternion pitch = Quaternion.Euler(HD2DSampleBuilder.CameraPitch, 0f, 0f);
            camGo.transform.rotation = pitch;
            var follow = camGo.AddComponent<FollowCamera>();
            follow.Target = player.transform;
            follow.Offset = new Vector3(0, 0.8f, 0) + pitch * new Vector3(0, 0, -HD2DSampleBuilder.CameraDistance);
            camGo.transform.position = player.transform.position + follow.Offset;

            var high = baseline ? AssetDatabase.LoadAssetAtPath<VolumeProfile>(HD2DSampleBuilder.HighProfilePath) : ShowcaseProfile();
            var low = AssetDatabase.LoadAssetAtPath<VolumeProfile>(HD2DSampleBuilder.LowProfilePath);
            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = high;
            var tier = volume.gameObject.AddComponent<HD2DQualityTier>();
            tier.Volume = volume; tier.High = high; tier.Low = low;
        }

        // §2 grading. The first passes dropped the town's warm LUT for neutral ACES; the user found the result too
        // sharp and the colours unbalanced (2026-10-23), so the LUT is back at half strength to pull the cool stone
        // and the warm wood into one palette, and the split toning is gentler.
        static VolumeProfile ShowcaseProfile()
        {
            var lut = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Settings/HD2D_WarmLUT.png");
            var profile = HD2DSampleBuilder.CreateProfile(ProfilePath, depthOfField: true, bloomThreshold: 1.0f, bloomIntensity: 0.9f, highQualityBloom: true, lut);
            profile.TryGet<ColorLookup>(out var lookup);
            lookup.contribution.Override(0.5f);
            profile.TryGet<ColorAdjustments>(out var color);
            color.contrast.Override(4f);
            color.saturation.Override(0f);
            color.postExposure.Override(0.55f);
            profile.TryGet<Vignette>(out var vignette);
            vignette.intensity.Override(0.32f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(new Color(0.05f, 0.04f, 0.06f));
            profile.TryGet<DepthOfField>(out var dof);
            dof.aperture.Override(1.4f);   // softer: more blur at the top as well as the front

            var split = profile.Add<SplitToning>(true);
            split.shadows.Override(new Color(0.46f, 0.50f, 0.54f));
            split.highlights.Override(new Color(0.86f, 0.74f, 0.60f));
            split.balance.Override(10f);
            var smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.94f, 0.98f, 1.06f, 0f));
            smh.highlights.Override(new Vector4(1.04f, 1.0f, 0.94f, 0.02f));
            foreach (var c in new VolumeComponent[] { split, smh }) AssetDatabase.AddObjectToAsset(c, profile);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        // A copy of the HD-2D renderer with SSAO, appended to HD2D_URP's renderer list so only cameras that ask
        // for it (this sample) pay for it.
        static int ShowcaseRendererIndex()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (!data)
            {
                AssetDatabase.CopyAsset(HD2DSampleBuilder.RendererPath, RendererPath);
                data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            }
            var ssao = data.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().FirstOrDefault();
            if (!ssao)
            {
                ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                ssao.name = "SSAO";
                AssetDatabase.AddObjectToAsset(ssao, data);
                data.rendererFeatures.Add(ssao);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out _, out long localId);
                var so = new SerializedObject(data);
                var map = so.FindProperty("m_RendererFeatureMap");
                map.arraySize = data.rendererFeatures.Count;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            var s = new SerializedObject(ssao);
            s.FindProperty("m_Settings.Intensity").floatValue = 0.8f;
            s.FindProperty("m_Settings.Radius").floatValue = 0.4f;
            s.FindProperty("m_Settings.DirectLightingStrength").floatValue = 0.35f;
            s.FindProperty("m_Settings.Falloff").floatValue = 60f;
            s.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(HD2DSampleBuilder.PipelinePath);
            var pso = new SerializedObject(pipeline);
            var list = pso.FindProperty("m_RendererDataList");
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == data) return i;
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = data;
            pso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
            return list.arraySize - 1;
        }

        // ---------- normal maps ----------

        // Every environment material in the session gets a point-filtered normal map. The height comes from a
        // delivered <key>_height.png when there is one; otherwise from the albedo's luminance, which is a stand-in
        // (dark mortar reads low, pale stone high) until Codex draws real height maps.
        static void AddNormalMaps(string materialDir)
        {
            Directory.CreateDirectory(NormalDir);
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { materialDir }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                var albedo = m.GetTexture("_BaseMap") as Texture2D;
                if (!albedo) continue;
                var normal = NormalMapFor(albedo);
                if (!normal) continue;
                m.SetTexture("_BumpMap", normal);
                m.SetFloat("_BumpScale", 0.55f);   // relief that reads, not etched
                m.EnableKeyword("_NORMALMAP");
                EditorUtility.SetDirty(m);
            }
            AssetDatabase.SaveAssets();
        }

        static Texture2D NormalMapFor(Texture2D albedo)
        {
            string src = AssetDatabase.GetAssetPath(albedo);
            string key = Path.GetFileNameWithoutExtension(src);
            string outPath = $"{NormalDir}/{key}_n.png";
            string heightPath = $"{ArtDir}/{key}_height.png";
            bool fromHeight = File.Exists(heightPath);

            var source = new Texture2D(2, 2);
            source.LoadImage(File.ReadAllBytes(fromHeight ? heightPath : src));
            int w = source.width, h = source.height;
            var px = source.GetPixels32();
            var alpha = fromHeight ? null : px.Select(p => p.a).ToArray();
            float[] height = px.Select(p => (p.a < 128 && !fromHeight) ? 0f : (0.299f * p.r + 0.587f * p.g + 0.114f * p.b) / 255f).ToArray();
            // Blur delivered height maps too: their hard steps made every stone edge a razor line.
            height = Blur(height, w, h, albedo.wrapModeU == TextureWrapMode.Repeat, albedo.wrapModeV == TextureWrapMode.Repeat);
            if (fromHeight) height = Blur(height, w, h, albedo.wrapModeU == TextureWrapMode.Repeat, albedo.wrapModeV == TextureWrapMode.Repeat);

            bool wrapU = albedo.wrapModeU == TextureWrapMode.Repeat, wrapV = albedo.wrapModeV == TextureWrapMode.Repeat;
            float H(int x, int y)
            {
                x = wrapU ? (x + w) % w : Mathf.Clamp(x, 0, w - 1);
                y = wrapV ? (y + h) % h : Mathf.Clamp(y, 0, h - 1);
                return height[y * w + x];
            }
            var outPx = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (H(x + 1, y - 1) + 2 * H(x + 1, y) + H(x + 1, y + 1)) - (H(x - 1, y - 1) + 2 * H(x - 1, y) + H(x - 1, y + 1));
                float dy = (H(x - 1, y + 1) + 2 * H(x, y + 1) + H(x + 1, y + 1)) - (H(x - 1, y - 1) + 2 * H(x, y - 1) + H(x + 1, y - 1));
                var n = new Vector3(-dx * NormalStrength, -dy * NormalStrength, 1f).normalized;
                outPx[y * w + x] = new Color32((byte)((n.x * 0.5f + 0.5f) * 255), (byte)((n.y * 0.5f + 0.5f) * 255), (byte)((n.z * 0.5f + 0.5f) * 255), 255);
            }
            Object.DestroyImmediate(source);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(outPx);
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(outPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(outPath);
            importer.textureType = TextureImporterType.NormalMap;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapModeU = albedo.wrapModeU;
            importer.wrapModeV = albedo.wrapModeV;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
        }

        // One 3×3 box pass: pixel art is flat-shaded in steps, and the luminance edges alone make every colour
        // band a cliff; a light blur turns them into slopes so stones read rounded.
        static float[] Blur(float[] v, int w, int h, bool wrapU, bool wrapV)
        {
            var o = new float[v.Length];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float sum = 0;
                for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    int xx = wrapU ? (x + i + w) % w : Mathf.Clamp(x + i, 0, w - 1);
                    int yy = wrapV ? (y + j + h) % h : Mathf.Clamp(y + j, 0, h - 1);
                    sum += v[yy * w + xx];
                }
                o[y * w + x] = sum / 9f;
            }
            return o;
        }

        static void ConfigureOverrideTextures()
        {
            foreach (string path in Directory.GetFiles(ArtDir, "*.png"))
            {
                string assetPath = path.Replace('\\', '/');
                var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
                string key = Path.GetFileNameWithoutExtension(assetPath);
                if (importer == null || key.StartsWith("Generated_")) continue;
                bool data = key.EndsWith("_height") || key.Contains("mask");
                bool tiles = key.StartsWith("ground_") || key.StartsWith("roof_tiles") || key.StartsWith("roof_thatch") || key.StartsWith("wall_") || key.StartsWith("terrace_") || key.StartsWith("platform_top");
                bool tilesX = tiles || key.StartsWith("retaining_wall") || key.StartsWith("balustrade") || key.StartsWith("roof_eave") || key.StartsWith("platform_edge");
                importer.textureType = TextureImporterType.Default;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.sRGBTexture = !data;
                importer.alphaIsTransparency = !data;
                importer.isReadable = data;
                importer.wrapModeU = tilesX ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.wrapModeV = tiles ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
        }

        // ---------- layout ----------
        // Metres, x east, z north; the camera looks north at the square's centre (0, 0, 0).

        static bool Has(string key) => File.Exists($"{ArtDir}/{key}.png") && ZhuojunTownBuilder.OverrideDir != null;

        static void Layout(Transform env)
        {
            var ground = ZhuojunTownBuilder.Group(env, "Ground");
            string cobble = Has("ground_cobble_small") ? "ground_cobble_small" : "ground_street_stone";
            ZhuojunTownBuilder.Patch(ground, "Square", cobble, -18f, 18f, -5.5f, 8.2f, 0f);
            if (Has("decal_dirt_patch"))
                foreach (var (x, z) in new[] { (-4.5f, 1.2f), (5.5f, -1.8f), (0.8f, 4.6f) })
                    ZhuojunTownBuilder.Quad(ground, "Worn patch", new Vector3(x, 0.004f, z), new Vector3(90f, 0, 0), ZhuojunTownBuilder.Metres("decal_dirt_patch"),
                        ZhuojunTownBuilder.Mat("decal_dirt_patch", clip: true), castShadows: false);
            ZhuojunTownBuilder.Patch(ground, "Lower street", Has("ground_dirt_worn") ? "ground_dirt_worn" : "ground_packed_earth", -18f, 18f, -18f, -5.5f, LowerStreet);
            ZhuojunTownBuilder.Solid(ground, new Vector3(0, -0.5f, 1.35f), new Vector3(36f, 1f, 13.7f));

            Front(env);
            Back(env);
            Dress(env);
        }

        // Foreground: the square ends in a retaining wall dropping to a lower street, with a stone balustrade on top.
        static void Front(Transform env)
        {
            var g = ZhuojunTownBuilder.Group(env, "Front edge");
            string wallKey = Has("retaining_wall_stone") ? "retaining_wall_stone" : "citywall_face";
            float drop = -LowerStreet;
            var face = new Vector2(36f, drop);
            ZhuojunTownBuilder.Quad(g, "Retaining wall", new Vector3(0, LowerStreet + drop / 2f, -5.5f), new Vector3(0, 0, 0), face,
                ZhuojunTownBuilder.Mat(wallKey), new Vector2(36f / ZhuojunTownBuilder.Metres(wallKey).x, drop / ZhuojunTownBuilder.Metres(wallKey).y));
            if (Has("balustrade_stone"))
                ZhuojunTownBuilder.Strip(g, "Balustrade", "balustrade_stone", new Vector3(-18f, 0, -5.3f), new Vector3(18f, 0, -5.3f), ZhuojunTownBuilder.Metres("balustrade_stone").y);
            else
                ZhuojunTownBuilder.Strip(g, "Balustrade", "courtyard_wall", new Vector3(-18f, 0, -5.3f), new Vector3(18f, 0, -5.3f), 0.9f, capKey: "courtyard_wall_cap");
        }

        // Back: the tavern and the smithy on the square, steps between them up to a terrace with two houses.
        static void Back(Transform env)
        {
            var row = ZhuojunTownBuilder.Group(env, "Back");
            var tavern = ZhuojunTownBuilder.Building(row, "Tavern", new Vector3(-7.2f, 0, 3.6f), 0f, "tavern_front", "tavern_side", "tavern_back");
            ZhuojunTownBuilder.EaveSign(tavern, "plaque_tavern", "tavern_front", "tavern_side");
            ZhuojunTownBuilder.Building(row, "Smithy", new Vector3(7.0f, 0, 3.8f), 0f, "smithy_front", "smithy_side", null, glowMask: "smithy_forge_glow_mask");

            // Terrace: its retaining face along z = 8.2 and its paving.
            float top = TerraceHeight;
            string pave = Has("terrace_paving") ? "terrace_paving" : "platform_top";
            ZhuojunTownBuilder.Patch(row, "Terrace", pave, -18f, 18f, 8.2f, 30f, top);
            string wallKey = Has("retaining_wall_stone") ? "retaining_wall_stone" : "platform_edge";
            foreach (var (a, b) in new[] { (-18f, -1.6f), (1.6f, 18f) })
            {
                var size = new Vector2(b - a, top);
                ZhuojunTownBuilder.Quad(row, "Terrace face", new Vector3((a + b) / 2f, top / 2f, 8.2f), Vector3.zero, size,
                    ZhuojunTownBuilder.Mat(wallKey), new Vector2(size.x / ZhuojunTownBuilder.Metres(wallKey).x, top / ZhuojunTownBuilder.Metres(wallKey).y));
                ZhuojunTownBuilder.Solid(row, new Vector3((a + b) / 2f, top / 2f, 19f), new Vector3(b - a, top, 21.6f));
            }
            // Six 0.3 m steps from the square to the landing at the terrace edge.
            ZhuojunTownBuilder.Stairs(row, "Steps to the terrace", new Vector3(0, 0, 9.7f), 3.2f, 6, 0.3f, 0.35f, 1.5f);
            ZhuojunTownBuilder.Building(row, "House on the terrace W", new Vector3(-4.2f, top, 13.5f), 0f, "house_front", "house_side", null);
            ZhuojunTownBuilder.Building(row, "House on the terrace E", new Vector3(4.6f, top, 14.0f), 0f, "house_b_front", "house_b_side", "house_b_back");
            ZhuojunTownBuilder.Prop(row, "prop_tree_locust", -10.5f, 12f, y: top);
            ZhuojunTownBuilder.Prop(row, "prop_tree_elm", 11f, 12.5f, y: top);
        }

        // Clutter along the wall feet and around the stalls; the tavern and smithy doorways stay clear (§5).
        static void Dress(Transform env)
        {
            var p = ZhuojunTownBuilder.Group(env, "Dressing");
            // Tavern (door at x ≈ -7.2): jars and a barrel to the west, the wine banner and flowers to the east.
            ZhuojunTownBuilder.Prop(p, "prop_wine_jars", -10.6f, 2.9f);
            ZhuojunTownBuilder.Prop(p, "prop_barrel", -9.6f, 3.1f);
            ZhuojunTownBuilder.Prop(p, "prop_banner_wine", -4.4f, 3.0f);
            ZhuojunTownBuilder.Prop(p, "prop_flowers", -3.4f, 3.2f);
            // Smithy (door at x ≈ 7): weapon rack and firewood to the sides.
            ZhuojunTownBuilder.Prop(p, "prop_weapon_rack", 3.9f, 3.2f);
            ZhuojunTownBuilder.Prop(p, "prop_firewood", 10.2f, 3.3f);
            ZhuojunTownBuilder.Prop(p, "prop_water_vat", 11.2f, 2.9f);
            // Stalls on the square, off the line from the steps to the front.
            ZhuojunTownBuilder.Prop(p, "prop_stall", -5.2f, -0.6f, solid: true);
            ZhuojunTownBuilder.Prop(p, "prop_produce_basket", -3.6f, -1.4f);
            ZhuojunTownBuilder.Prop(p, "prop_stall_cloth", 5.4f, -0.4f, solid: true);
            ZhuojunTownBuilder.Prop(p, "prop_basket_stack", 3.7f, -1.3f);
            ZhuojunTownBuilder.Prop(p, "prop_handcart", 9.6f, -1.6f, solid: true);
            ZhuojunTownBuilder.Prop(p, "prop_straw_mats", -9.2f, -1.9f);
            ZhuojunTownBuilder.Prop(p, "prop_bush", -1.9f, 6.0f);
            ZhuojunTownBuilder.Prop(p, "prop_bush", 2.0f, 6.0f);
            foreach (var key in new[] { "prop_jars_cluster", "prop_crates_stack", "prop_mat_produce", "prop_potted_plants", "prop_cart_hand" })
                if (Has(key))
                {
                    var at = key switch
                    {
                        "prop_jars_cluster" => (-11.6f, 3.0f), "prop_crates_stack" => (12.3f, 3.2f), "prop_mat_produce" => (-7.6f, -1.4f),
                        "prop_potted_plants" => (2.2f, 2.9f), _ => (13.0f, -1.0f),
                    };
                    ZhuojunTownBuilder.Prop(p, key, at.Item1, at.Item2);
                }
            // Red lanterns under the eaves, each with a small warm light (the reference's glowing accents).
            string lanternGlow = null;   // the default town glow colour blows the small lantern out to white; the point light carries it
            foreach (var x in new[] { -9.4f, -5.0f, 4.6f, 9.4f })
            {
                ZhuojunTownBuilder.Prop(p, "prop_lantern_red", x, 3.35f, y: 2.3f, glowMask: lanternGlow);
                ZhuojunTownBuilder.PointLight(p, "Lantern light", new Vector3(x, 2.5f, 3.0f), new Color(1f, 0.48f, 0.26f), 2.2f, 4.5f);
            }
            ZhuojunTownBuilder.PointLight(p, "Forge light", new Vector3(7.6f, 1.0f, 3.2f), new Color(1f, 0.48f, 0.18f), 3.0f, 4.5f);
        }

        static void People()
        {
            var people = new GameObject("People").transform;
            HD2DSampleBuilder.Character("Liu Bei", "LiuBei", new Vector3(0f, 0, 0.6f), 0, isPlayer: true).transform.SetParent(people, true);
            HD2DSampleBuilder.Character("Guan Yu", "GuanYu", new Vector3(-1.5f, 0, 0.2f), 0, isPlayer: false).transform.SetParent(people, true);
            HD2DSampleBuilder.Character("Zhang Fei", "ZhangFei", new Vector3(1.6f, 0, 0.1f), 0, isPlayer: false).transform.SetParent(people, true);
            foreach (var (id, at) in new[] { ("N05", new Vector3(-8.6f, 0, 2.4f)), ("N04", new Vector3(8.6f, 0, 2.6f)), ("N08", new Vector3(-4.0f, 0, -2.2f)), ("N10", new Vector3(4.6f, 0, -2.0f)), ("N07", new Vector3(-0.6f, TerraceHeight, 10.6f)) })
                HD2DSampleBuilder.Character(id, ResidentImporter.KeyFor(id), at, 0, isPlayer: false).transform.SetParent(people, true);
        }

        // ---------- capture ----------

        // -executeMethod ThreeKingdoms.EditorTools.HD2DShowcaseBuilder.CaptureFromCommandLine -shotDir <dir>:
        // renders both scenes' main cameras at 1920×1080 with post-processing to <dir>/showcase.png and
        // <dir>/showcase-baseline.png (editor batch mode with graphics, no player build).
        public static void CaptureFromCommandLine()
        {
            try
            {
                CaptureAll();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static void CaptureAll()
        {
            {
                var args = System.Environment.GetCommandLineArgs();
                int at = System.Array.IndexOf(args, "-shotDir");
                if (at < 0 || at + 1 >= args.Length) throw new System.Exception("-shotDir missing");
                string dir = args[at + 1];
                Directory.CreateDirectory(dir);
                Capture(ScenePath, Path.Combine(dir, "showcase.png"));
                Capture(BaselineScenePath, Path.Combine(dir, "showcase-baseline.png"));
            }
        }

        static void Capture(string scenePath, string file)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            foreach (var a in Object.FindObjectsByType<SpriteFrameAnimator>(FindObjectsSortMode.None)) a.Apply();
            var cam = Camera.main;
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = rt;
            for (int i = 0; i < 3; i++) cam.Render();   // a few frames so temporal effects and AO settle
            RenderTexture.active = rt;
            var frame = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            frame.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            frame.Apply();
            File.WriteAllBytes(file, frame.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Debug.Log("HD2D_SHOWCASE_SHOT " + file);
        }
    }
}
