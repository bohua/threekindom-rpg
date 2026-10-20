using System.IO;
using System.Linq;
using ThreeKingdoms.App;
using ThreeKingdoms.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ThreeKingdoms.EditorTools
{
    // ADR-0005 §8 pipeline for battle backgrounds: build a small 3D diorama from the Codex native textures, render
    // it as far / mid / foreground layers at 2× and box-downsample to 640×360 pixel art, then assemble the
    // battle scene (orthographic camera, unlit backdrop quads, lit character quads, blob shadows, shared LUT).
    // Final backgrounds are meant to be painted over or replaced; the layer contract stays the same.
    public static class BattleBackdropBaker
    {
        const string Root = "Assets/_Project/Art/Battle/ZhuojunMarket";
        public const string BackdropPath = Root + "/ZhuojunMarket.asset";
        public const string ScenePath = "Assets/_Project/Scenes/Battle_ZhuojunMarket.unity";
        public const string ProfilePath = "Assets/_Project/Settings/HD2D_Battle.asset";
        const string DioramaMeshes = Root + "/Diorama_Meshes_TEMP.asset";
        const string DioramaMaterials = Root + "/Diorama_Materials_TEMP";
        const int FarLayer = 8, MidLayer = 9, ForeLayer = 10, Supersample = 2;
        public const float OrthoSize = 6f;                                   // 360 / 2 / 30 px per unit
        static readonly Vector2 LightPitchYaw = new Vector2(30f, 50f);       // from the front-left, as in the reference
        // Near-neutral key light and haze: the shared LUT adds the golden-hour warmth at runtime, so the
        // backdrop must not be pre-warmed (ADR-0005 §8.2, avoids grading twice).
        static readonly Color SunColor = new Color(1f, 0.92f, 0.80f);
        const float SunIntensity = 2.4f;

        [MenuItem("JRPG/Bake Zhuojun Market Battle Backdrop")]
        public static void Build()
        {
            Directory.CreateDirectory(Root);
            var layers = BakeLayers();
            var backdrop = AssetDatabase.LoadAssetAtPath<BattleBackdrop>(BackdropPath);
            if (!backdrop) { backdrop = ScriptableObject.CreateInstance<BattleBackdrop>(); AssetDatabase.CreateAsset(backdrop, BackdropPath); }
            backdrop.DisplayName = "涿郡 · 街市";
            backdrop.Layers = layers;
            backdrop.LightPitchYaw = LightPitchYaw;
            backdrop.LightColor = SunColor;
            backdrop.LightIntensity = SunIntensity;
            EditorUtility.SetDirty(backdrop);
            AssetDatabase.SaveAssets();
            BuildBattleScene(backdrop);
            Debug.Log("BATTLE_BACKDROP_BUILT");
        }

        public static void BuildFromCommandLine()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        // ---------- diorama & layer rendering ----------

        static Texture2D[] BakeLayers()
        {
            // The diorama is built from the Codex native textures (ZHUOJUN_CORNER / ZHUOJUN_TOWN deliveries) with the
            // town builder's helpers; materials and meshes go to a temporary folder deleted after the bake.
            ZhuojunTownBuilder.BeginSession(DioramaMaterials, DioramaMeshes);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.80f, 0.78f, 0.86f);
            RenderSettings.ambientEquatorColor = new Color(0.76f, 0.62f, 0.48f);
            RenderSettings.ambientGroundColor = new Color(0.40f, 0.30f, 0.22f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.014f;
            RenderSettings.fogColor = new Color(0.86f, 0.82f, 0.76f);

            var far = new GameObject("Far").transform;
            var mid = new GameObject("Mid").transform;
            var fore = new GameObject("Fore").transform;
            BuildDiorama(far, mid, fore);
            ZhuojunTownBuilder.EndSession();
            SetLayer(far, FarLayer); SetLayer(mid, MidLayer); SetLayer(fore, ForeLayer);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = SunColor;
            sun.intensity = SunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            sun.transform.rotation = Quaternion.Euler(LightPitchYaw.x, LightPitchYaw.y, 0f);

            var cam = new GameObject("Bake camera").AddComponent<Camera>();
            cam.transform.SetPositionAndRotation(new Vector3(0, 4.6f, -4.5f), Quaternion.Euler(14f, 0f, 0f));
            cam.fieldOfView = 38f;
            cam.nearClipPlane = 0.5f; cam.farClipPlane = 220f;
            cam.allowHDR = false; cam.allowMSAA = false;
            var data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;

            WarmUpShaders(cam);
            // Haze belongs to the distance: heavy fog for the far layer, a touch for the mid layer, none up close.
            RenderSettings.fogDensity = 0.016f;
            var farTex = RenderLayer(cam, 1 << FarLayer, RenderSettings.fogColor, opaque: true, blur: 0, "Far");
            RenderSettings.fogDensity = 0.004f;
            var midTex = RenderLayer(cam, 1 << MidLayer, Color.black, opaque: false, blur: 0, "Mid");
            RenderSettings.fog = false;
            var foreTex = RenderLayer(cam, 1 << ForeLayer, Color.black, opaque: false, blur: 3, "Fore");
            AssetDatabase.DeleteAsset(DioramaMeshes);
            AssetDatabase.DeleteAsset(DioramaMaterials);
            return new[] { farTex, midTex, foreTex };
        }

        // Opaque layers render once; transparent layers render over black and over white and recover alpha
        // from the difference (robust even when URP's intermediate target has no alpha channel).
        // The editor compiles shader variants asynchronously and draws a cyan placeholder until they are ready;
        // keep rendering until nothing is compiling so the captured layers never contain placeholders.
        static void WarmUpShaders(Camera cam)
        {
            var rt = RenderTexture.GetTemporary(64, 36, 24);
            cam.targetTexture = rt;
            cam.cullingMask = ~0;
            for (int i = 0; i < 200; i++)
            {
                cam.Render();
                if (i > 2 && !ShaderUtil.anythingCompiling) break;
                System.Threading.Thread.Sleep(100);
            }
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
        }
        static Texture2D RenderLayer(Camera cam, int mask, Color background, bool opaque, int blur, string name)
        {
            int w = BattleBackdrop.Width * Supersample, h = BattleBackdrop.Height * Supersample;
            cam.cullingMask = mask;
            cam.clearFlags = CameraClearFlags.SolidColor;
            Color32[] onBlack = Capture(cam, w, h, opaque ? background : Color.black);
            Color32[] onWhite = opaque ? onBlack : Capture(cam, w, h, Color.white);

            var full = new Color[w * h];
            for (int i = 0; i < full.Length; i++)
            {
                Color b = onBlack[i], wh = onWhite[i];
                float a = opaque ? 1f : Mathf.Clamp01(1f - ((wh.r - b.r) + (wh.g - b.g) + (wh.b - b.b)) / 3f);
                full[i] = a > 0.004f ? new Color(b.r / a, b.g / a, b.b / a, a) : Color.clear;
            }

            var small = Downsample(full, w, h, Supersample);
            if (blur > 0) small = BoxBlur(small, BattleBackdrop.Width, BattleBackdrop.Height, blur);
            var pixels = new Color32[small.Length];
            for (int i = 0; i < small.Length; i++)
            {
                Color c = small[i];
                float a = blur > 0 ? c.a : (c.a >= 0.5f ? 1f : 0f);                   // crisp edges except the soft foreground
                pixels[i] = new Color32(Posterize(c.r), Posterize(c.g), Posterize(c.b), (byte)Mathf.RoundToInt(a * 255));
            }
            string path = $"{Root}/ZhuojunMarket_{name}.png";
            var tex = new Texture2D(BattleBackdrop.Width, BattleBackdrop.Height, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = !opaque;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Color32[] Capture(Camera cam, int w, int h, Color background)
        {
            cam.backgroundColor = background;
            var rt = RenderTexture.GetTemporary(new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 24) { sRGB = true });
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var read = new Texture2D(w, h, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            read.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            var pixels = read.GetPixels32();
            Object.DestroyImmediate(read);
            return pixels;
        }

        static Color[] Downsample(Color[] src, int w, int h, int f)
        {
            int ow = w / f, oh = h / f;
            var dst = new Color[ow * oh];
            for (int y = 0; y < oh; y++)
            for (int x = 0; x < ow; x++)
            {
                Color sum = Color.clear; float alpha = 0f;
                for (int j = 0; j < f; j++)
                for (int i = 0; i < f; i++)
                {
                    Color c = src[(y * f + j) * w + x * f + i];
                    sum += new Color(c.r * c.a, c.g * c.a, c.b * c.a, 0);   // premultiplied average
                    alpha += c.a;
                }
                alpha /= f * f;
                dst[y * ow + x] = alpha > 0f ? new Color(sum.r / (alpha * f * f), sum.g / (alpha * f * f), sum.b / (alpha * f * f), alpha) : Color.clear;
            }
            return dst;
        }

        static Color[] BoxBlur(Color[] src, int w, int h, int r)
        {
            var dst = new Color[src.Length];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color sum = Color.clear; float alpha = 0f; int n = 0;
                for (int j = -r; j <= r; j++)
                for (int i = -r; i <= r; i++)
                {
                    int xx = Mathf.Clamp(x + i, 0, w - 1), yy = Mathf.Clamp(y + j, 0, h - 1);
                    Color c = src[yy * w + xx];
                    sum += new Color(c.r * c.a, c.g * c.a, c.b * c.a, 0); alpha += c.a; n++;
                }
                dst[y * w + x] = alpha > 0f ? new Color(sum.r / alpha, sum.g / alpha, sum.b / alpha, alpha / n) : Color.clear;
            }
            return dst;
        }

        static byte Posterize(float v) => (byte)(Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(v) * 63f), 0, 63) * 255 / 63);   // 6 bits per channel

        static void SetLayer(Transform root, int layer)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        // Market plaza modelled on the user's reference, built from the Codex native textures: tavern (酒肆) left and
        // weapons shop (鐵匠鋪) with its forge right, both turned toward the plaza; houses and trees behind; the city
        // wall, a distant gate and hazy hills far back; fence and a barrel blurred in the near corners.
        static void BuildDiorama(Transform far, Transform mid, Transform fore)
        {
            // far: hills, city wall, distant gate and rooftops
            ZhuojunTownBuilder.Patch(far, "Far ground", "ground_packed_earth", -60f, 60f, 30f, 80f, 0f);
            ZhuojunTownBuilder.WallRun(far, "City wall", new Vector3(-40f, 0, 48f), new Vector3(40f, 0, 48f));
            ZhuojunTownBuilder.Prop(far, "bg_city_gate_far", 1.5f, 46f, scale: 1.4f);
            ZhuojunTownBuilder.Prop(far, "bg_town_rooftops_far", -14f, 44f, scale: 1.6f);
            ZhuojunTownBuilder.Prop(far, "bg_town_rooftops_far", 18f, 44f, scale: 1.6f);
            ZhuojunTownBuilder.Prop(far, "bg_hills_far", 0f, 75f, scale: 5f);

            // mid: plaza, the two shops, houses and trees behind, market props
            ZhuojunTownBuilder.Patch(mid, "Plaza paving", "ground_street_stone", -24f, 24f, 2f, 30f, 0f);
            var tavern = ZhuojunTownBuilder.Building(mid, "Tavern", new Vector3(-7.4f, 0, 11.6f), 8f, "tavern_front", "tavern_side", "tavern_back");
            ZhuojunTownBuilder.EaveSign(tavern, "plaque_tavern", "tavern_front", "tavern_side");
            var smithy = ZhuojunTownBuilder.Building(mid, "Smithy", new Vector3(7.6f, 0, 11.8f), -8f, "smithy_front", "smithy_side", null, glowMask: "smithy_forge_glow_mask");
            ZhuojunTownBuilder.EaveSign(smithy, "plaque_smithy", "smithy_front", "smithy_side");
            ZhuojunTownBuilder.Building(mid, "Back house W", new Vector3(-4.4f, 0, 24f), 0f, "house_front", "house_side", "house_back");
            ZhuojunTownBuilder.Building(mid, "Back house E", new Vector3(5.4f, 0, 25f), 0f, "house_b_front", "house_b_side", "house_b_back", roofKey: "thatch");
            ZhuojunTownBuilder.Prop(mid, "prop_tree_locust", 0.6f, 19.5f, scale: 0.9f);
            ZhuojunTownBuilder.Prop(mid, "prop_tree_peach", -13f, 20f);

            ZhuojunTownBuilder.Prop(mid, "prop_stall_cloth", -2.4f, 15.0f);
            ZhuojunTownBuilder.Prop(mid, "prop_stall_grain", 2.8f, 15.4f);
            ZhuojunTownBuilder.Prop(mid, "prop_notice_board", 0.3f, 17.4f);
            ZhuojunTownBuilder.Prop(mid, "prop_wine_jars", -9.6f, 10.0f);
            ZhuojunTownBuilder.Prop(mid, "prop_barrel", -4.6f, 10.2f);
            ZhuojunTownBuilder.Prop(mid, "prop_produce_basket", -3.6f, 9.8f);
            ZhuojunTownBuilder.Prop(mid, "prop_banner_wine", -2.8f, 10.8f, y: 0.9f);
            ZhuojunTownBuilder.Prop(mid, "prop_lantern_red", -11.2f, 10.4f, y: 2.2f);
            ZhuojunTownBuilder.Prop(mid, "prop_anvil", 6.4f, 10.2f);
            ZhuojunTownBuilder.Prop(mid, "prop_weapon_rack", 10.0f, 10.6f);
            ZhuojunTownBuilder.Prop(mid, "prop_banner_iron", 4.0f, 11.0f, y: 0.9f);
            ZhuojunTownBuilder.Prop(mid, "prop_water_vat", 5.0f, 10.0f);
            ZhuojunTownBuilder.Prop(mid, "prop_grain_sacks", 4.6f, 14.0f);
            ZhuojunTownBuilder.Prop(mid, "prop_basket_stack", -4.4f, 13.8f);
            ZhuojunTownBuilder.PointLight(mid, "Forge light", new Vector3(8.0f, 1.2f, 11.0f), new Color(1f, 0.45f, 0.15f), 5f, 5f);
            ZhuojunTownBuilder.PointLight(mid, "Lantern light", new Vector3(-11.2f, 2.5f, 10.2f), new Color(1f, 0.55f, 0.3f), 2.2f, 5f);

            // fore: blurred fence posts, a bush and a barrel in the bottom corners
            ZhuojunTownBuilder.Prop(fore, "prop_fence", -5.6f, 3.2f);
            ZhuojunTownBuilder.Prop(fore, "prop_fence", 5.8f, 3.2f);
            ZhuojunTownBuilder.Prop(fore, "prop_barrel", -6.8f, 3.0f);
            ZhuojunTownBuilder.Prop(fore, "prop_bush", 7.2f, 2.8f, scale: 1.3f);
        }

        // ---------- battle scene ----------

        static void BuildBattleScene(BattleBackdrop backdrop)
        {
            var lut = AssetDatabase.LoadAssetAtPath<Texture2D>(HD2DLutBuilder.LutPath) ?? HD2DLutBuilder.Build();
            // No depth of field (orthographic, ADR-0005 §8.1); bloom threshold above 1 so painted highlights never glow (§8.2).
            var profile = HD2DSampleBuilder.CreateProfile(ProfilePath, depthOfField: false, bloomThreshold: 1.1f, bloomIntensity: 0.5f, highQualityBloom: true, lut);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.80f, 0.78f, 0.86f);
            RenderSettings.ambientEquatorColor = new Color(0.76f, 0.62f, 0.48f);
            RenderSettings.ambientGroundColor = new Color(0.40f, 0.30f, 0.22f);
            RenderSettings.fog = false;

            var camGo = new GameObject("Battle Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = OrthoSize;
            cam.transform.position = new Vector3(0, 0, -10);
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.None;

            float width = BattleBackdrop.Width / HD2DSampleBuilder.PixelsPerUnit, height = BattleBackdrop.Height / HD2DSampleBuilder.PixelsPerUnit;
            var layerRoot = new GameObject("Backdrop").transform;
            float[] depth = { 40f, 30f, 1f };
            for (int i = 0; i < backdrop.Layers.Length; i++)
            {
                var mode = i == 0 ? UnlitMode.Opaque : i == backdrop.Layers.Length - 1 ? UnlitMode.Blend : UnlitMode.Cutout;
                Quad(layerRoot, "Layer " + backdrop.Layers[i].name, new Vector3(0, 0, depth[i]), new Vector2(width, height), UnlitMaterial(backdrop.Layers[i], mode));
            }

            var sun = new GameObject("Battle key light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = backdrop.LightRotation;
            sun.color = backdrop.LightColor;
            sun.intensity = backdrop.LightIntensity;
            sun.shadows = LightShadows.None;            // backdrops are unlit and cannot receive shadows; blob shadows instead
            var stage = new GameObject("Battle stage").AddComponent<BattleStage>();
            stage.Backdrop = backdrop; stage.KeyLight = sun;

            var shadow = UnlitMaterial(BlobTexture("BlobShadow", ring: false), UnlitMode.Blend, saveAs: "BlobShadow");
            var ring = UnlitMaterial(BlobTexture("ActiveRing", ring: true), UnlitMode.Blend, saveAs: "ActiveRing", hdr: 1.8f);
            var units = new GameObject("Units").transform;
            // Party left (3), enemies right (4: the second battle's Zhang Liang + 3 officers is the largest V1 line-up).
            Unit(units, "Liu Bei", "LiuBei", new Vector2(-6.0f, -1.1f), 2, shadow);
            var guan = Unit(units, "Guan Yu", "GuanYu", new Vector2(-4.4f, -2.6f), 2, shadow);
            Unit(units, "Zhang Fei", "ZhangFei", new Vector2(-6.2f, -4.1f), 2, shadow);
            Quad(guan, "Active ring", new Vector3(0, 0.02f, 0.04f), new Vector2(1.6f, 0.6f), ring);
            Unit(units, "Yellow Turban 1", "YellowTurban", new Vector2(4.6f, -1.0f), 1, shadow);
            Unit(units, "Yellow Turban 2", "YellowTurban", new Vector2(6.3f, -2.0f), 1, shadow);
            Unit(units, "Yellow Turban 3", "YellowTurban", new Vector2(4.6f, -3.0f), 1, shadow);
            Unit(units, "Yellow Turban 4", "YellowTurban", new Vector2(6.3f, -4.1f), 1, shadow);

            var volumeGo = new GameObject("Battle Volume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
            var tier = volumeGo.AddComponent<HD2DQualityTier>();
            tier.Volume = volume; tier.High = profile; tier.Low = profile;

            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
        }

        static Transform Unit(Transform parent, string name, string key, Vector2 feet, int frame, Material shadow)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(feet.x, feet.y, 10f + feet.y * 0.01f);   // lower units sit nearer the camera and draw in front
            float size = HD2DSampleBuilder.FrameSize / HD2DSampleBuilder.PixelsPerUnit;
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(HD2DSampleBuilder.AtlasPath(key));
            var quad = Quad(root, "Pixel sprite", new Vector3(0, size / 2f, 0), new Vector2(size, size), HD2DSampleBuilder.PixelMaterial(key, atlas));
            var animator = quad.gameObject.AddComponent<SpriteFrameAnimator>();
            animator.Target = quad.GetComponent<MeshRenderer>(); animator.Columns = 9; animator.Rows = 1; animator.Frame = frame;
            animator.Apply();
            Quad(root, "Blob shadow", new Vector3(0, 0.05f, 0.05f), new Vector2(1.2f, 0.4f), shadow);
            return root;
        }

        static Transform Quad(Transform parent, string name, Vector3 local, Vector2 size, Material m)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            q.transform.localPosition = local;
            q.transform.localScale = new Vector3(size.x, size.y, 1f);
            var r = q.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return q.transform;
        }

        enum UnlitMode { Opaque, Cutout, Blend }

        static Material UnlitMaterial(Texture2D texture, UnlitMode mode, string saveAs = null, float hdr = 1f)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = saveAs ?? texture.name };
            m.SetTexture("_BaseMap", texture);
            m.SetColor("_BaseColor", Color.white * hdr);
            if (mode == UnlitMode.Cutout)
            {
                m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON");
                m.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else if (mode == UnlitMode.Blend)
            {
                m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            string path = $"{Root}/{m.name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // Soft dark ellipse (blob shadow) or a thin gold ellipse ring (active-unit marker), 32×12 pixels.
        static Texture2D BlobTexture(string name, bool ring)
        {
            const int w = 32, h = 12;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f - w / 2f) / (w / 2f), dy = (y + 0.5f - h / 2f) / (h / 2f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                Color c = ring
                    ? (d > 0.72f && d <= 1f ? new Color(1f, 0.82f, 0.3f, 1f) : Color.clear)
                    : (d <= 1f ? new Color(0.12f, 0.07f, 0.04f, Mathf.Round((1f - d) * 3f) / 3f * 0.55f + 0.1f) : Color.clear);
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            string path = $"{Root}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
