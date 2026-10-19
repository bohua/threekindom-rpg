using System.Collections.Generic;
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
    // Texture-driven HD-2D validation (ADR-0005 §9 / user 2026-10-19): the Zhuojun corner built the Octopath way —
    // simple boxes and quads carrying hand-made native pixel textures (30 px per metre, 48 colours), props as lit
    // pixel billboards, street running across the screen with façades facing the camera.
    public static class ZhuojunCornerBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Zhuojun_Corner.unity";
        public const string TextureDir = "Assets/_Project/Art/Environment/ZhuojunCorner";
        const string MaterialDir = TextureDir + "/Materials";
        const string MeshPath = TextureDir + "/Corner_Meshes.asset";
        const float Ppm = 30f;

        static readonly HashSet<string> TileX = new HashSet<string> { "roof_ridge", "roof_eave", "timber_beam", "stone_plinth", "curb_drain", "grass_edge", "prop_fence" };
        static readonly HashSet<string> TileY = new HashSet<string> { "timber_post" };
        static readonly HashSet<string> TileXY = new HashSet<string> { "roof_tiles_grey", "roof_underside", "wall_rammed_earth", "wall_plaster_white", "ground_street_stone", "ground_packed_earth", "ground_transition" };

        static Mesh container;
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        [MenuItem("JRPG/Build Zhuojun Corner (native textures)")]
        public static void Build()
        {
            ConfigureTextures();
            materials.Clear();
            Directory.CreateDirectory(MaterialDir);
            BeginMeshes();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.80f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.70f, 0.64f, 0.58f);
            RenderSettings.ambientGroundColor = new Color(0.36f, 0.30f, 0.24f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.006f;
            RenderSettings.fogColor = new Color(0.78f, 0.74f, 0.68f);

            var env = new GameObject("Environment").transform;
            BuildGround(env);
            Building(env, "Tavern", centerX: -7f, frontZ: 4f, "tavern_front", "tavern_side", "tavern_back", width: 8f, wall: 3.6f, depth: 6f, gable: 2.4f);
            Building(env, "Smithy", centerX: 1.6f, frontZ: 4.4f, "smithy_front", "smithy_side", null, width: 6f, wall: 3.2f, depth: 5f, gable: 2.4f, glowMask: "smithy_forge_glow_mask");
            Building(env, "House", centerX: 9f, frontZ: 4.8f, "house_front", "house_side", null, width: 6f, wall: 3.0f, depth: 5f, gable: 2.3f);
            Dress(env);
            EndMeshes();

            var sun = new GameObject("Late-afternoon sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.92f, 0.80f);   // the shared LUT and the textures already carry the warmth
            sun.intensity = 2.2f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.8f;
            sun.transform.rotation = Quaternion.Euler(42f, -32f, 0f);

            var player = HD2DSampleBuilder.Character("Liu Bei", "LiuBei", new Vector3(0, 0, 2.4f), 0, isPlayer: true);
            HD2DSampleBuilder.Character("Guan Yu", "GuanYu", new Vector3(-1.8f, 0, 1.8f), 0, isPlayer: false);
            HD2DSampleBuilder.Character("Zhang Fei", "ZhangFei", new Vector3(1.9f, 0, 1.8f), 0, isPlayer: false);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = HD2DSampleBuilder.CameraFov;
            cam.nearClipPlane = 1f; cam.farClipPlane = 250f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.None;
            Quaternion pitch = Quaternion.Euler(HD2DSampleBuilder.CameraPitch, 0f, 0f);
            camGo.transform.rotation = pitch;
            var follow = camGo.AddComponent<FollowCamera>();
            follow.Target = player.transform;
            follow.Offset = new Vector3(0, 0.8f, 0) + pitch * new Vector3(0, 0, -HD2DSampleBuilder.CameraDistance);
            camGo.transform.position = player.transform.position + follow.Offset;

            var high = AssetDatabase.LoadAssetAtPath<VolumeProfile>(HD2DSampleBuilder.HighProfilePath);
            var low = AssetDatabase.LoadAssetAtPath<VolumeProfile>(HD2DSampleBuilder.LowProfilePath);
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
            AssetDatabase.SaveAssets();
            Debug.Log("ZHUOJUN_CORNER_BUILT");
        }

        public static void BuildFromCommandLine()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        // ---------- textures & materials ----------

        static void ConfigureTextures()
        {
            foreach (string path in Directory.GetFiles(TextureDir, "*.png"))
            {
                string assetPath = path.Replace('\\', '/');
                string key = Path.GetFileNameWithoutExtension(assetPath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
                if (importer == null) continue;
                bool tiled = TileX.Contains(key) || TileY.Contains(key) || TileXY.Contains(key);
                importer.textureType = TextureImporterType.Default;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;                          // pixel-exact prototype, as the art hand-off recommends
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.sRGBTexture = !key.Contains("mask");
                importer.alphaIsTransparency = !key.Contains("mask");
                importer.wrapModeU = TileX.Contains(key) || TileXY.Contains(key) ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.wrapModeV = TileY.Contains(key) || TileXY.Contains(key) ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
        }

        public static Texture2D Tex(string key) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/{key}.png");

        static Material Mat(string key, bool clip, bool twoSided = false, string emissionMask = null)
        {
            string id = key + (clip ? "_clip" : "") + (twoSided ? "_2s" : "");
            if (materials.TryGetValue(id, out var cached)) return cached;
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = id };
            m.SetTexture("_BaseMap", Tex(key));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0f);
            m.SetFloat("_SpecularHighlights", 0f); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            if (clip)
            {
                m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON");
                m.renderQueue = (int)RenderQueue.AlphaTest;
            }
            if (twoSided) m.SetFloat("_Cull", (float)CullMode.Off);
            if (emissionMask != null)
            {
                m.SetTexture("_EmissionMap", Tex(emissionMask));
                m.SetColor("_EmissionColor", new Color(2.6f, 1.2f, 0.45f));   // forge glow, white mask pixels only
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;   // URP keeps _EMISSION only when a GI flag says "emissive"
            }
            string path = $"{MaterialDir}/{id}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            materials[id] = m;
            return m;
        }

        // ---------- meshes ----------

        static void BeginMeshes()
        {
            meshes.Clear();
            if (AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath)) AssetDatabase.DeleteAsset(MeshPath);
            container = new Mesh { name = "Corner_Meshes" };
            AssetDatabase.CreateAsset(container, MeshPath);
        }

        static void EndMeshes() => AssetDatabase.SaveAssets();

        // Quad in the XY plane facing -Z, centred, with UVs repeating `repeat` times (tiling lives in the mesh, not the material).
        static Mesh QuadMesh(Vector2 size, Vector2 repeat)
        {
            string key = $"Quad_{size.x:0.###}x{size.y:0.###}_{repeat.x:0.###}x{repeat.y:0.###}";
            if (meshes.TryGetValue(key, out var mesh)) return mesh;
            float w = size.x / 2f, h = size.y / 2f;
            mesh = new Mesh { name = key };
            mesh.SetVertices(new[] { new Vector3(-w, -h, 0), new Vector3(w, -h, 0), new Vector3(w, h, 0), new Vector3(-w, h, 0) });
            mesh.SetNormals(Enumerable.Repeat(Vector3.back, 4).ToList());
            mesh.SetUVs(0, new[] { new Vector2(0, 0), new Vector2(repeat.x, 0), new Vector2(repeat.x, repeat.y), new Vector2(0, repeat.y) });
            mesh.SetUVs(1, new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            AssetDatabase.AddObjectToAsset(mesh, container);
            meshes[key] = mesh;
            return mesh;
        }

        static GameObject Quad(Transform parent, string name, Vector3 center, Vector3 euler, Vector2 size, Material m, Vector2? repeat = null, bool castShadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.AddComponent<MeshFilter>().sharedMesh = QuadMesh(size, repeat ?? Vector2.one);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        static Vector2 Repeat(string key, Vector2 size)
        {
            var t = Tex(key);
            return new Vector2(size.x * Ppm / t.width, size.y * Ppm / t.height);
        }

        // ---------- ground ----------

        static void BuildGround(Transform env)
        {
            var g = new GameObject("Ground").transform;
            g.SetParent(env, false);
            // Street runs across the screen (X). Paving under and in front of the shops, a stone-to-earth transition band,
            // packed earth toward the camera and behind the row.
            GroundStrip(g, "Street paving", "ground_street_stone", zMin: -1f, zMax: 11f);
            GroundStrip(g, "Paving to earth", "ground_transition", zMin: -5f, zMax: -1f);
            GroundStrip(g, "Earth near", "ground_packed_earth", zMin: -29f, zMax: -5f);
            GroundStrip(g, "Earth far", "ground_packed_earth", zMin: 11f, zMax: 47f);
            var floor = new GameObject("Ground collider").AddComponent<BoxCollider>();
            floor.transform.SetParent(g, false);
            floor.center = new Vector3(0, -0.05f, 8f); floor.size = new Vector3(80f, 0.1f, 80f);
        }

        static void GroundStrip(Transform parent, string name, string key, float zMin, float zMax)
        {
            var size = new Vector2(48f, zMax - zMin);
            // Rotated +90° about X the quad faces up and texture "up" points away from the camera.
            Quad(parent, name, new Vector3(0, 0, (zMin + zMax) / 2f), new Vector3(90f, 0, 0), size, Mat(key, clip: false), Repeat(key, size), castShadows: false);
        }

        // ---------- buildings ----------

        static void Building(Transform env, string name, float centerX, float frontZ, string front, string side, string back,
            float width, float wall, float depth, float gable, string glowMask = null)
        {
            var b = new GameObject(name).transform;
            b.SetParent(env, false);
            b.localPosition = new Vector3(centerX, 0, frontZ);
            var box = b.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0, wall / 2f, depth / 2f); box.size = new Vector3(width, wall, depth);

            // Walls: the façade textures carry all the detail (doors, windows, timber, plinth).
            Quad(b, "Front", new Vector3(0, wall / 2f, 0), Vector3.zero, new Vector2(width, wall), Mat(front, clip: false, emissionMask: glowMask));
            if (back != null) Quad(b, "Back", new Vector3(0, wall / 2f, depth), new Vector3(0, 180, 0), new Vector2(width, wall), Mat(back, clip: false));
            else Quad(b, "Back", new Vector3(0, wall / 2f, depth), new Vector3(0, 180, 0), new Vector2(width, wall), Mat("wall_rammed_earth", false), Repeat("wall_rammed_earth", new Vector2(width, wall)));
            float sideH = wall + gable;
            Quad(b, "Gable L", new Vector3(-width / 2f, sideH / 2f, depth / 2f), new Vector3(0, 90, 0), new Vector2(depth, sideH), Mat(side, clip: true));
            Quad(b, "Gable R", new Vector3(width / 2f, sideH / 2f, depth / 2f), new Vector3(0, -90, 0), new Vector2(depth, sideH), Mat(side, clip: true));
            Quad(b, "Grass at base", new Vector3(0, 0.4f, -0.03f), Vector3.zero, new Vector2(width, 0.8f), Mat("grass_edge", clip: true, twoSided: true), Repeat("grass_edge", new Vector2(width, 0.8f)), castShadows: false);

            // Roof: two tiled slabs at the gable pitch with eave overhang, an eave strip, a ridge strip and ridge ends.
            const float eave = 0.6f, sideOverhang = 0.35f;
            float half = depth / 2f, slope = Mathf.Atan2(gable, half), ridgeY = wall + gable, roofW = width + 2 * sideOverhang;
            float run = half + eave, length = run / Mathf.Cos(slope), drop = run * Mathf.Tan(slope);
            float eaveY = ridgeY - drop, deg = slope * Mathf.Rad2Deg;
            var roofSize = new Vector2(roofW, length);
            var roofMat = Mat("roof_tiles_grey", clip: false);
            Quad(b, "Roof front", new Vector3(0, (ridgeY + eaveY) / 2f + 0.05f, half - run / 2f), new Vector3(90f - deg, 0, 0), roofSize, roofMat, Repeat("roof_tiles_grey", roofSize));
            Quad(b, "Roof back", new Vector3(0, (ridgeY + eaveY) / 2f + 0.05f, half + run / 2f), new Vector3(90f - deg, 180, 0), roofSize, roofMat, Repeat("roof_tiles_grey", roofSize));
            var under = Mat("roof_underside", clip: false);
            Quad(b, "Roof underside", new Vector3(0, (ridgeY + eaveY) / 2f - 0.02f, half - run / 2f), new Vector3(-90f - deg, 0, 0), roofSize, under, Repeat("roof_underside", roofSize), castShadows: false);
            var eaveSize = new Vector2(roofW, 0.6f);
            Quad(b, "Eave", new Vector3(0, eaveY - 0.22f, -eave - 0.02f), new Vector3(deg * 0.5f, 0, 0), eaveSize, Mat("roof_eave", clip: true, twoSided: true), Repeat("roof_eave", eaveSize));
            var ridgeSize = new Vector2(roofW + 0.1f, 0.8f);
            Quad(b, "Ridge", new Vector3(0, ridgeY + 0.22f, half - 0.12f), Vector3.zero, ridgeSize, Mat("roof_ridge", clip: true, twoSided: true), Repeat("roof_ridge", ridgeSize));
            foreach (int s in new[] { -1, 1 })
            {
                var end = Quad(b, "Ridge end", new Vector3(s * (roofW / 2f - 0.15f), ridgeY + 0.6f, half - 0.16f), Vector3.zero, new Vector2(0.8f, 1.2f), Mat("roof_ridge_end", clip: true, twoSided: true));
                if (s > 0) end.transform.localScale = new Vector3(-1, 1, 1);   // mirror so both ends curl outward
            }
        }

        // ---------- props (lit pixel billboards) ----------

        static void Prop(Transform parent, string key, float x, float z, float y = 0f, float scale = 1f)
        {
            var t = Tex(key);
            var size = new Vector2(t.width / Ppm, t.height / Ppm) * scale;
            var root = new GameObject(key).transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(x, y, z);
            var q = Quad(root, "Sprite", new Vector3(0, size.y / 2f, 0), Vector3.zero, size, Mat(key, clip: true, twoSided: true));
            q.AddComponent<PixelBillboard>().BaseSize = size;
        }

        static void Dress(Transform env)
        {
            var p = new GameObject("Props").transform;
            p.SetParent(env, false);
            // Tavern (x -11..-3, front z 4)
            Prop(p, "prop_wine_jars", -9.7f, 3.3f);
            Prop(p, "prop_barrel", -4.5f, 3.4f);
            Prop(p, "prop_produce_basket", -3.6f, 3.0f);
            Prop(p, "prop_banner_wine", -2.6f, 3.7f, y: 0.9f);
            Prop(p, "prop_lantern_red", -11.4f, 3.6f, y: 2.2f);
            Prop(p, "prop_stall", -13.6f, 1.6f);
            // Smithy (x -1.4..4.6, front z 4.4)
            Prop(p, "prop_water_vat", -0.7f, 3.8f);
            Prop(p, "prop_anvil", 1.0f, 3.5f);
            Prop(p, "prop_weapon_rack", 4.0f, 3.9f);
            Prop(p, "prop_banner_iron", 5.3f, 4.1f, y: 0.9f);
            Prop(p, "prop_lantern_red", -1.6f, 4.1f, y: 2.0f);
            // House (x 6..12, front z 4.8)
            Prop(p, "prop_firewood", 6.9f, 4.3f);
            Prop(p, "prop_flowers", 8.6f, 4.5f);
            Prop(p, "prop_handcart", 11.0f, 3.2f);
            Prop(p, "prop_bush", 12.6f, 4.6f);
            // Trees and the distant gate
            // Out-of-focus foreground, as in HD-2D town shots: a tree and bushes between camera and street.
            Prop(p, "prop_tree_locust", -10.5f, -9.5f, scale: 0.85f);
            Prop(p, "prop_bush", 6.5f, -7.5f, scale: 1.8f);
            Prop(p, "prop_bush", 8.3f, -8.2f, scale: 1.5f);
            Prop(p, "prop_flowers", -3.5f, -4.0f, scale: 1.2f);
            Prop(p, "prop_bush", -12.4f, 4.2f);
            Prop(p, "bg_city_gate_far", 1.0f, 30f, scale: 2.2f);
            // Fence along the near edge of the paving band, tiled in X
            var fenceSize = new Vector2(9f, 1.2f);
            Quad(p, "Fence", new Vector3(-10.5f, 0.6f, -4.6f), Vector3.zero, fenceSize, Mat("prop_fence", clip: true, twoSided: true), Repeat("prop_fence", fenceSize));
            Quad(p, "Fence", new Vector3(10.5f, 0.6f, -4.6f), Vector3.zero, fenceSize, Mat("prop_fence", clip: true, twoSided: true), Repeat("prop_fence", fenceSize));

            // Light sources painted into the textures get real lights: forge glow and the two lanterns.
            PointLight(p, "Forge light", new Vector3(2.9f, 1.0f, 4.0f), new Color(1f, 0.48f, 0.18f), 3.5f, 4.5f);
            PointLight(p, "Lantern light", new Vector3(-11.4f, 2.5f, 3.4f), new Color(1f, 0.55f, 0.3f), 2.0f, 5f);
            PointLight(p, "Lantern light", new Vector3(-1.6f, 2.3f, 3.9f), new Color(1f, 0.55f, 0.3f), 2.0f, 5f);
        }

        static void PointLight(Transform parent, string name, Vector3 at, Color color, float intensity, float range)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.transform.SetParent(parent, false);
            l.transform.localPosition = at;
            l.type = LightType.Point; l.color = color; l.intensity = intensity; l.range = range;
            l.shadows = LightShadows.None;
        }
    }
}
