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
    // The whole of Zhuojun (ZHUOJUN_TOWN_DESIGN.md), built like the corner: simple boxes and quads carrying the native
    // pixel textures (corner library + the 97-texture town delivery), props as lit pixel billboards.
    //
    // Layout, metres (x east, z north; camera looks north):
    //   walls x -50..50, z -38..38 (4 m thick, 5 m high); south gate (0, -36); west gate (-48, 0)
    //   north row  (fronts face south at z 14): 08 granary, 01 commandery office (courtyard + hall), 07 barracks
    //   北横街 z 9..14 · middle row (fronts at z -4): 03 tavern, 02 weapons shop · 中街 + 05 market z -14..-2
    //   south row (fronts at z -20): 04 houses, 06 clinic, 09 post station + stable · 南回环街 z -25..-21
    //   main axis x -3..3 from the south gate to the office gate; 11 well yard at (-21, -13); 12 peach grove outside the west gate
    public static class ZhuojunTownBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Zhuojun_Town.unity";
        public const string TownDir = "Assets/_Project/Art/Environment/ZhuojunTown";
        const string CornerDir = ZhuojunCornerBuilder.TextureDir;
        const string MaterialDir = TownDir + "/Materials";
        const string MeshPath = TownDir + "/Town_Meshes.asset";
        const float Ppm = 30f;
        public static readonly Vector3 PlayerStart = new Vector3(0f, 0f, -46f);   // outside the south gate (design §5 step 1)

        static Mesh container;
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static readonly Dictionary<string, string> axes = new Dictionary<string, string>();

        [MenuItem("JRPG/Build Zhuojun Town (native textures)")]
        public static void Build()
        {
            ReadManifests();
            ConfigureTextures(TownDir);
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
            Ground(env);
            Walls(env);
            NorthRow(env);
            MiddleRow(env);
            SouthRows(env);
            Outside(env);
            EndMeshes();

            var sun = new GameObject("Late-afternoon sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.92f, 0.80f);
            sun.intensity = 2.2f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.8f;
            sun.transform.rotation = Quaternion.Euler(42f, -32f, 0f);

            var people = new GameObject("People").transform;
            var player = HD2DSampleBuilder.Character("Liu Bei", "LiuBei", PlayerStart, 3, isPlayer: true);
            player.transform.SetParent(people, true);
            // N13 / N14 (design §4): the two companions, standing where the player meets them.
            HD2DSampleBuilder.Character("Guan Yu", "GuanYu", new Vector3(-11.6f, 0, -5.4f), 0, isPlayer: false).transform.SetParent(people, true);
            HD2DSampleBuilder.Character("Zhang Fei", "ZhangFei", new Vector3(-9.6f, 0, -10.5f), 0, isPlayer: false).transform.SetParent(people, true);
            NpcMarkers(people);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = HD2DSampleBuilder.CameraFov;
            cam.nearClipPlane = 1f; cam.farClipPlane = 300f;
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
            camGo.AddComponent<SeeThroughOccluders>().Target = player.transform;

            var high = AssetDatabase.LoadAssetAtPath<VolumeProfile>(HD2DSampleBuilder.HighProfilePath);
            var low = AssetDatabase.LoadAssetAtPath<VolumeProfile>(HD2DSampleBuilder.LowProfilePath);
            var volumeGo = new GameObject("Global Volume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = high;
            var tier = volumeGo.AddComponent<HD2DQualityTier>();
            tier.Volume = volume; tier.High = high; tier.Low = low;

            // Review cameras for screenshots (-camera <name>): one per district.
            ReviewCamera("Review South Gate", new Vector3(0, 0, -46f));
            ReviewCamera("Review Market", new Vector3(0, 0, -9f));
            ReviewCamera("Review Tavern", new Vector3(-14, 0, -8f));
            ReviewCamera("Review Main Axis", new Vector3(0, 0, -24f));
            ReviewCamera("Review Office", new Vector3(0, 0, 11f));
            ReviewCamera("Review Barracks", new Vector3(29, 0, 18f));
            ReviewCamera("Review Houses", new Vector3(-26, 0, -16f));
            ReviewCamera("Review West Gate", new Vector3(-55, 0, 0f));

            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("ZHUOJUN_TOWN_BUILT");
        }

        public static void BuildFromCommandLine()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        // ---------- textures, materials, meshes ----------

        [System.Serializable] sealed class ManifestFile { public string filename; public string tile_axes; }
        [System.Serializable] sealed class Manifest { public ManifestFile[] files; }

        static void ReadManifests()
        {
            axes.Clear();
            foreach (var dir in new[] { CornerDir, TownDir })
            {
                var m = JsonUtility.FromJson<Manifest>(File.ReadAllText($"{dir}/manifest.json"));
                foreach (var f in m.files) axes[Path.GetFileNameWithoutExtension(f.filename)] = f.tile_axes ?? "none";
            }
        }

        static void ConfigureTextures(string dir)
        {
            foreach (string path in Directory.GetFiles(dir, "*.png"))
            {
                string assetPath = path.Replace('\\', '/');
                string key = Path.GetFileNameWithoutExtension(assetPath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
                if (importer == null) continue;
                axes.TryGetValue(key, out string a); a ??= "none";
                bool mask = key.Contains("mask");
                importer.textureType = TextureImporterType.Default;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.sRGBTexture = !mask;
                importer.alphaIsTransparency = !mask;
                importer.wrapModeU = a.Contains("x") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.wrapModeV = a.Contains("y") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
        }

        static Texture2D Tex(string key) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>($"{TownDir}/{key}.png") ?? AssetDatabase.LoadAssetAtPath<Texture2D>($"{CornerDir}/{key}.png")
            ?? throw new FileNotFoundException("Missing town texture " + key);

        static Material Mat(string key, bool clip = false, bool twoSided = false, string emissionMask = null, Color? glow = null)
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
                m.SetColor("_EmissionColor", glow ?? new Color(2.6f, 1.2f, 0.45f));
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            string path = $"{MaterialDir}/{id}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            materials[id] = m;
            return m;
        }

        static void BeginMeshes()
        {
            meshes.Clear();
            if (AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath)) AssetDatabase.DeleteAsset(MeshPath);
            container = new Mesh { name = "Town_Meshes" };
            AssetDatabase.CreateAsset(container, MeshPath);
        }

        static void EndMeshes() => AssetDatabase.SaveAssets();

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

        static Vector2 Metres(string key) { var t = Tex(key); return new Vector2(t.width / Ppm, t.height / Ppm); }

        static Transform Group(Transform parent, string name, Vector3 at = default, float yaw = 0f)
        {
            var g = new GameObject(name).transform;
            g.SetParent(parent, false);
            g.localPosition = at;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            return g;
        }

        static void Solid(Transform parent, Vector3 center, Vector3 size)
        {
            var box = new GameObject("Collider").AddComponent<BoxCollider>();
            box.transform.SetParent(parent, false);
            box.center = center; box.size = size;
        }

        // ---------- ground ----------

        // A ground patch facing up; texture "up" points north. y separates overlapping layers.
        static void Patch(Transform parent, string name, string key, float xMin, float xMax, float zMin, float zMax, float y)
        {
            var size = new Vector2(xMax - xMin, zMax - zMin);
            Quad(parent, name, new Vector3((xMin + xMax) / 2f, y, (zMin + zMax) / 2f), new Vector3(90f, 0, 0), size, Mat(key), Repeat(key, size), castShadows: false);
        }

        static void Ground(Transform env)
        {
            var g = Group(env, "Ground");
            Patch(g, "Grass outside", "ground_grass", -110f, 90f, -80f, 80f, -0.01f);
            Patch(g, "Town earth", "ground_packed_earth", -46f, 46f, -34f, 34f, 0f);
            Patch(g, "Main axis paving", "ground_street_stone", -3f, 3f, -34f, 14f, 0.004f);
            Patch(g, "Market paving", "ground_street_stone", -9f, 9f, -14f, -2f, 0.006f);
            Patch(g, "Office forecourt bricks", "ground_square_brick", -9.8f, 9.8f, 14f, 22f, 0.004f);
            Patch(g, "Road to the south gate", "ground_packed_earth", -3.5f, 3.5f, -80f, -38f, 0.002f);
            Patch(g, "Lane to the west gate", "ground_packed_earth", -80f, -50f, -2f, 2f, 0.002f);
            Patch(g, "Earth edge at the road", "ground_earth_to_grass", -9f, 9f, -46f, -38f, 0.001f);
            Patch(g, "Peach grove petals", "ground_petals", -78f, -55f, -14f, 14f, 0.001f);
            Patch(g, "Vegetable plot", "ground_vegetable_plot", -44f, -38f, -32f, -27f, 0.003f);
            // Curb and drain along both sides of the main axis.
            foreach (float x in new[] { -3.15f, 3.15f })
            {
                var size = new Vector2(0.6f, 20f);
                Quad(g, "Drain", new Vector3(x, 0.008f, -24f), new Vector3(90f, 0, 0), size, Mat("ground_transition"), Repeat("ground_transition", size), castShadows: false);
            }
            var floor = new GameObject("Ground collider").AddComponent<BoxCollider>();
            floor.transform.SetParent(g, false);
            floor.center = new Vector3(-10f, -0.05f, 0f); floor.size = new Vector3(200f, 0.1f, 160f);
            // Invisible bounds of the walkable outside area (south road and west grove).
            Solid(g, new Vector3(-10f, 2f, -79f), new Vector3(200f, 4f, 1f));
            Solid(g, new Vector3(-79f, 2f, 0f), new Vector3(1f, 4f, 160f));
            Solid(g, new Vector3(56f, 2f, 0f), new Vector3(1f, 4f, 160f));
            Solid(g, new Vector3(-10f, 2f, 44f), new Vector3(200f, 4f, 1f));
        }

        // ---------- city walls & gates ----------

        const float WallH = 5f, WallT = 4f;

        // A straight wall run along X (horizontal) or Z, from a to b on its centre line. Faces carry citywall_face,
        // the top the walkway, both top edges a parapet. The whole run is one occluder.
        static void WallRun(Transform parent, string name, Vector3 a, Vector3 b)
        {
            bool alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
            float len = alongX ? Mathf.Abs(b.x - a.x) : Mathf.Abs(b.z - a.z);
            var run = Group(parent, name, (a + b) / 2f, alongX ? 0f : 90f);
            run.gameObject.AddComponent<Occluder>();
            var face = new Vector2(len, WallH);
            Quad(run, "Face south", new Vector3(0, WallH / 2f, -WallT / 2f), Vector3.zero, face, Mat("citywall_face"), Repeat("citywall_face", face));
            Quad(run, "Face north", new Vector3(0, WallH / 2f, WallT / 2f), new Vector3(0, 180, 0), face, Mat("citywall_face"), Repeat("citywall_face", face));
            var top = new Vector2(len, WallT);
            Quad(run, "Walkway", new Vector3(0, WallH, 0), new Vector3(90, 0, 0), top, Mat("citywall_walkway"), Repeat("citywall_walkway", top), castShadows: false);
            var par = new Vector2(len, 1.2f);
            Quad(run, "Parapet south", new Vector3(0, WallH + 0.6f, -WallT / 2f), Vector3.zero, par, Mat("citywall_parapet", clip: true, twoSided: true), Repeat("citywall_parapet", par));
            Quad(run, "Parapet north", new Vector3(0, WallH + 0.6f, WallT / 2f), new Vector3(0, 180, 0), par, Mat("citywall_parapet", clip: true, twoSided: true), Repeat("citywall_parapet", par));
            Solid(run, new Vector3(0, WallH / 2f, 0), new Vector3(len, WallH, WallT));
        }

        static void Walls(Transform env)
        {
            var w = Group(env, "City walls");
            // South wall with the 9 m gate block in the middle; west wall with the 6 m west gate.
            WallRun(w, "South wall W", new Vector3(-52f, 0, -36f), new Vector3(-4.5f, 0, -36f));
            WallRun(w, "South wall E", new Vector3(4.5f, 0, -36f), new Vector3(52f, 0, -36f));
            WallRun(w, "North wall", new Vector3(-52f, 0, 36f), new Vector3(52f, 0, 36f));
            WallRun(w, "East wall", new Vector3(48f, 0, -34f), new Vector3(48f, 0, 34f));
            WallRun(w, "West wall S", new Vector3(-48f, 0, -34f), new Vector3(-48f, 0, -3f));
            WallRun(w, "West wall N", new Vector3(-48f, 0, 3f), new Vector3(-48f, 0, 34f));
            SouthGate(w);
            WestGate(w);
        }

        static void SouthGate(Transform walls)
        {
            var g = Group(walls, "10 South gate", new Vector3(0, 0, -36f));
            g.gameObject.AddComponent<Occluder>();
            var front = Metres("south_gate_wall_front");   // 9 × 5, flat timber-lintel opening 4 × 3.6
            Quad(g, "Gate wall outside", new Vector3(0, front.y / 2f, -WallT / 2f), Vector3.zero, front, Mat("south_gate_wall_front", clip: true));
            Quad(g, "Gate wall inside", new Vector3(0, front.y / 2f, WallT / 2f), new Vector3(0, 180, 0), front, Mat("south_gate_wall_front", clip: true));
            foreach (int s in new[] { -1, 1 })
            {
                var side = new Vector2(WallT, 3.6f);
                Quad(g, "Passage side", new Vector3(s * 2f, 1.8f, 0), new Vector3(0, s * -90f, 0), side, Mat("citywall_face"), Repeat("citywall_face", side));
                // Opened door leaves lying against the passage sides at the inner end.
                var leaf = Metres("gate_door_leaf");
                var l = Quad(g, "Door leaf", new Vector3(s * 1.93f, leaf.y / 2f, WallT / 2f - leaf.x / 2f - 0.05f), new Vector3(0, s * -90f, 0), leaf, Mat("gate_door_leaf", clip: true, twoSided: true));
                if (s > 0) l.transform.localScale = new Vector3(-1, 1, 1);
                Solid(g, new Vector3(s * 3.25f, WallH / 2f, 0), new Vector3(2.5f, WallH, WallT));
            }
            var ceiling = new Vector2(4f, WallT);
            Quad(g, "Passage ceiling", new Vector3(0, 3.6f, 0), new Vector3(-90, 0, 0), ceiling, Mat("timber_beam"), Repeat("timber_beam", ceiling), castShadows: false);
            var top = new Vector2(9f, WallT);
            Quad(g, "Walkway", new Vector3(0, WallH, 0), new Vector3(90, 0, 0), top, Mat("citywall_walkway"), Repeat("citywall_walkway", top), castShadows: false);
            Solid(g, new Vector3(0, 4.3f, 0), new Vector3(4f, 1.4f, WallT));
            Plaque(g, "plaque_south_gate", new Vector3(0, 4.3f, -WallT / 2f - 0.04f), 0f);
            // Gate tower on the wall: 9 × 3.6 timber hall with a tiled gable roof.
            Building(g, "Gate tower", new Vector3(0, WallH, -WallT / 2f + 0.25f), 0f, "south_gate_tower_front", "south_gate_tower_side", null, roofKey: "tile", collide: false);
        }

        static void WestGate(Transform walls)
        {
            // The west gate faces west; its front texture is seen side-on from the town camera, so it is mirrored on both faces.
            var g = Group(walls, "12 West gate", new Vector3(-48f, 0, 0f), 90f);
            g.gameObject.AddComponent<Occluder>();
            var front = Metres("west_gate_front");   // 6 × 4.5, opening 2.5 × 3
            Quad(g, "Gate outside", new Vector3(0, front.y / 2f, WallT / 2f), new Vector3(0, 180, 0), front, Mat("west_gate_front", clip: true));
            Quad(g, "Gate inside", new Vector3(0, front.y / 2f, -WallT / 2f), Vector3.zero, front, Mat("west_gate_front", clip: true));
            var cap = new Vector2(6f, WallH - front.y);
            foreach (float z in new[] { -WallT / 2f, WallT / 2f })
                Quad(g, "Wall above gate", new Vector3(0, front.y + cap.y / 2f, z), new Vector3(0, z > 0 ? 180 : 0, 0), cap, Mat("citywall_face"), Repeat("citywall_face", cap));
            var top = new Vector2(6f, WallT);
            Quad(g, "Walkway", new Vector3(0, WallH, 0), new Vector3(90, 0, 0), top, Mat("citywall_walkway"), Repeat("citywall_walkway", top), castShadows: false);
            foreach (int s in new[] { -1, 1 }) Solid(g, new Vector3(s * 2.1f, WallH / 2f, 0), new Vector3(1.8f, WallH, WallT));
            Solid(g, new Vector3(0, 4f, 0), new Vector3(2.5f, 2f, WallT));
        }

        // ---------- buildings ----------

        // A building whose front faces south (yaw 0) or north (yaw 180, back toward the camera). Size comes from the
        // textures: front = width × wall height, side = depth × (wall + gable). roofKey "tile" or "thatch".
        static Transform Building(Transform parent, string name, Vector3 frontCenter, float yaw, string front, string side, string back,
            string roofKey = "tile", string glowMask = null, bool collide = true)
        {
            var f = Metres(front); var sd = Metres(side);
            float width = f.x, wall = f.y, depth = sd.x, gable = Mathf.Max(0.4f, sd.y - wall);
            var b = Group(parent, name, frontCenter, yaw);
            b.gameObject.AddComponent<Occluder>();
            if (collide) Solid(b, new Vector3(0, wall / 2f, depth / 2f), new Vector3(width, wall, depth));

            Quad(b, "Front", new Vector3(0, wall / 2f, 0), Vector3.zero, f, Mat(front, clip: true, emissionMask: glowMask));
            if (back != null) Quad(b, "Back", new Vector3(0, wall / 2f, depth), new Vector3(0, 180, 0), new Vector2(width, wall), Mat(back, clip: true));
            else
            {
                var size = new Vector2(width, wall);
                Quad(b, "Back", new Vector3(0, wall / 2f, depth), new Vector3(0, 180, 0), size, Mat("wall_rammed_earth"), Repeat("wall_rammed_earth", size));
            }
            float sideH = wall + gable;
            Quad(b, "Gable L", new Vector3(-width / 2f, sideH / 2f, depth / 2f), new Vector3(0, 90, 0), new Vector2(depth, sideH), Mat(side, clip: true));
            Quad(b, "Gable R", new Vector3(width / 2f, sideH / 2f, depth / 2f), new Vector3(0, -90, 0), new Vector2(depth, sideH), Mat(side, clip: true));
            if (frontCenter.y == 0f)
                Quad(b, "Grass at base", new Vector3(0, 0.4f, -0.03f), Vector3.zero, new Vector2(width, 0.8f), Mat("grass_edge", clip: true, twoSided: true), Repeat("grass_edge", new Vector2(width, 0.8f)), castShadows: false);

            bool thatch = roofKey == "thatch";
            const float eave = 0.6f, sideOverhang = 0.35f;
            float half = depth / 2f, slope = Mathf.Atan2(gable, half), ridgeY = wall + gable, roofW = width + 2 * sideOverhang;
            float run = half + eave, length = run / Mathf.Cos(slope), drop = run * Mathf.Tan(slope);
            float eaveY = ridgeY - drop, deg = slope * Mathf.Rad2Deg;
            var roofSize = new Vector2(roofW, length);
            string roofTex = thatch ? "roof_thatch" : "roof_tiles_grey";
            var roofMat = Mat(roofTex);
            Quad(b, "Roof front", new Vector3(0, (ridgeY + eaveY) / 2f + 0.05f, half - run / 2f), new Vector3(90f - deg, 0, 0), roofSize, roofMat, Repeat(roofTex, roofSize));
            Quad(b, "Roof back", new Vector3(0, (ridgeY + eaveY) / 2f + 0.05f, half + run / 2f), new Vector3(90f - deg, 180, 0), roofSize, roofMat, Repeat(roofTex, roofSize));
            Quad(b, "Roof underside", new Vector3(0, (ridgeY + eaveY) / 2f - 0.02f, half - run / 2f), new Vector3(-90f - deg, 0, 0), roofSize, Mat("roof_underside"), Repeat("roof_underside", roofSize), castShadows: false);
            string eaveTex = thatch ? "roof_thatch_eave" : "roof_eave";
            var eaveSize = new Vector2(roofW, 0.6f);
            foreach (int s in new[] { 1, -1 })
                Quad(b, "Eave", new Vector3(0, eaveY - 0.22f, s > 0 ? -eave - 0.02f : depth + eave + 0.02f), new Vector3(deg * 0.5f * s, s > 0 ? 0 : 180, 0), eaveSize,
                    Mat(eaveTex, clip: true, twoSided: true), Repeat(eaveTex, eaveSize));
            if (!thatch)
            {
                var ridgeSize = new Vector2(roofW + 0.1f, 0.8f);
                Quad(b, "Ridge", new Vector3(0, ridgeY + 0.22f, half - 0.12f), Vector3.zero, ridgeSize, Mat("roof_ridge", clip: true, twoSided: true), Repeat("roof_ridge", ridgeSize));
                foreach (int s in new[] { -1, 1 })
                {
                    var end = Quad(b, "Ridge end", new Vector3(s * (roofW / 2f - 0.15f), ridgeY + 0.6f, half - 0.16f), Vector3.zero, new Vector2(0.8f, 1.2f), Mat("roof_ridge_end", clip: true, twoSided: true));
                    if (s > 0) end.transform.localScale = new Vector3(-1, 1, 1);
                }
            }
            return b;
        }

        // Signboards are flat on the façade, not billboards.
        static void Plaque(Transform parent, string key, Vector3 at, float yaw) =>
            Quad(parent, key, at, new Vector3(0, yaw, 0), Metres(key), Mat(key, clip: true, twoSided: true), castShadows: false);

        // Height of the eave line for a building made from these textures (same geometry as Building()).
        static float EaveY(string front, string side)
        {
            var f = Metres(front); var sd = Metres(side);
            float wall = f.y, depth = sd.x, gable = Mathf.Max(0.4f, sd.y - wall), half = depth / 2f;
            float slope = Mathf.Atan2(gable, half), run = half + 0.6f;
            return wall + gable - run * Mathf.Tan(slope);
        }

        // A shop sign hung on the front edge of the eave, where the eave strip cannot hide it.
        static void EaveSign(Transform building, string key, string front, string side) =>
            Plaque(building, key, new Vector3(0, EaveY(front, side) - 0.25f, -0.72f), 0f);

        // A flat run of a tileable fence-like strip (courtyard wall, palisade, wattle) along X or Z.
        static void Strip(Transform parent, string name, string key, Vector3 a, Vector3 b, float height, bool solid = true, string capKey = null)
        {
            bool alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
            float len = alongX ? Mathf.Abs(b.x - a.x) : Mathf.Abs(b.z - a.z);
            var g = Group(parent, name, (a + b) / 2f, alongX ? 0f : 90f);
            g.gameObject.AddComponent<Occluder>();
            var size = new Vector2(len, height);
            Quad(g, "Strip", new Vector3(0, height / 2f, 0), Vector3.zero, size, Mat(key, clip: true, twoSided: true), Repeat(key, size));
            if (capKey != null)
            {
                var cap = new Vector2(len + 0.2f, Metres(capKey).y);
                Quad(g, "Cap", new Vector3(0, height + cap.y / 2f - 0.1f, 0), Vector3.zero, cap, Mat(capKey, clip: true, twoSided: true), Repeat(capKey, cap));
            }
            if (solid) Solid(g, new Vector3(0, height / 2f, 0), new Vector3(len, height, 0.5f));
        }

        // ---------- districts ----------

        static void NorthRow(Transform env)
        {
            var row = Group(env, "North row");
            // 08 Granary (west).
            Building(row, "08 Granary", new Vector3(-30f, 0, 14f), 0f, "granary_front", "granary_side", null);
            Building(row, "House 北 W1", new Vector3(-40f, 0, 14f), 0f, "house_front", "house_side", "house_back");
            Building(row, "House 北 W2", new Vector3(-20f, 0, 14f), 0f, "house_c_front", "house_c_side", "house_c_back", roofKey: "thatch");
            Building(row, "House 北 E1", new Vector3(14f, 0, 14f), 0f, "house_c_front", "house_c_side", "house_c_back", roofKey: "thatch");
            var gp = Group(row, "Granary props");
            Prop(gp, "prop_sacks_pile", -25.2f, 13.0f, solid: true);
            Prop(gp, "prop_grain_sacks", -34.4f, 13.2f);
            Prop(gp, "prop_grain_measure", -26.5f, 12.4f);
            Prop(gp, "prop_carry_pole", -33.0f, 11.6f);
            Prop(gp, "prop_handcart", -22.5f, 11.8f, solid: true);

            // 01 Commandery office: courtyard wall with the gate, square-brick forecourt, hall at the back.
            var office = Group(row, "01 Commandery office");
            float wallH = Metres("courtyard_wall").y;
            Strip(office, "Courtyard wall W", "courtyard_wall", new Vector3(-10f, 0, 14f), new Vector3(-3f, 0, 14f), wallH, capKey: "courtyard_wall_cap");
            Strip(office, "Courtyard wall E", "courtyard_wall", new Vector3(3f, 0, 14f), new Vector3(10f, 0, 14f), wallH, capKey: "courtyard_wall_cap");
            Strip(office, "Courtyard wall side W", "courtyard_wall", new Vector3(-10f, 0, 14f), new Vector3(-10f, 0, 30f), wallH, capKey: "courtyard_wall_cap");
            Strip(office, "Courtyard wall side E", "courtyard_wall", new Vector3(10f, 0, 14f), new Vector3(10f, 0, 30f), wallH, capKey: "courtyard_wall_cap");
            var gate = Building(office, "Office gate", new Vector3(0, 0, 13f), 0f, "yamen_gate_front", "yamen_gate_side", null, collide: false);
            foreach (int s in new[] { -1, 1 }) Solid(gate, new Vector3(s * 2.1f, 2.25f, 2f), new Vector3(1.8f, 4.5f, 4f));
            EaveSign(gate, "plaque_yamen", "yamen_gate_front", "yamen_gate_side");
            var hall = Building(office, "Office hall", new Vector3(0, 0, 22f), 0f, "yamen_hall_front", "yamen_hall_side", null);
            var op = Group(office, "Office props");
            Prop(op, "prop_banner_han", -4.2f, 12.2f);
            Prop(op, "prop_banner_han", 4.2f, 12.2f);
            Prop(op, "steps_stone", 0f, 21.2f);
            Prop(op, "prop_lantern_red", -2.6f, 21.6f, y: 2.6f);
            Prop(op, "prop_lantern_red", 2.6f, 21.6f, y: 2.6f);
            Prop(op, "prop_potted_tree", -7.5f, 18f, key: "prop_tree_elm", scale: 0.7f);

            // 07 Barracks: palisade compound with a drill yard.
            var camp = Group(row, "07 Barracks");
            float stakeH = Metres("palisade_stakes").y;
            Strip(camp, "Palisade front W", "palisade_stakes", new Vector3(18f, 0, 14f), new Vector3(26f, 0, 14f), stakeH);
            Strip(camp, "Palisade front E", "palisade_stakes", new Vector3(32f, 0, 14f), new Vector3(42f, 0, 14f), stakeH);
            Strip(camp, "Palisade W", "palisade_stakes", new Vector3(18f, 0, 14f), new Vector3(18f, 0, 32f), stakeH);
            Strip(camp, "Palisade E", "palisade_stakes", new Vector3(42f, 0, 14f), new Vector3(42f, 0, 32f), stakeH);
            Strip(camp, "Palisade back", "palisade_stakes", new Vector3(18f, 0, 32f), new Vector3(42f, 0, 32f), stakeH);
            var cp = Group(camp, "Barracks props");
            Prop(cp, "prop_army_tent", 23.5f, 27f, solid: true);
            Prop(cp, "prop_army_tent", 36f, 27.5f, solid: true);
            Prop(cp, "prop_archery_target", 38.5f, 19f);
            Prop(cp, "prop_archery_target", 40f, 21f);
            Prop(cp, "prop_training_dummy", 21f, 19f);
            Prop(cp, "prop_training_dummy", 22.4f, 18f);
            Prop(cp, "prop_weapon_rack", 26.8f, 22f, solid: true);
            Prop(cp, "prop_war_drum", 30.5f, 25f);
            Prop(cp, "prop_banner_han", 29f, 13.2f);
            Prop(cp, "prop_banner_han", 25.4f, 25.6f);
            Prop(cp, "prop_brazier", 31.8f, 16f, glowMask: "prop_brazier_glow_mask");
            Prop(cp, "prop_hay_pile", 40f, 29.5f);
            PointLight(cp, "Brazier light", new Vector3(31.8f, 1.0f, 15.8f), new Color(1f, 0.5f, 0.2f), 2.5f, 5f);
        }

        static void MiddleRow(Transform env)
        {
            var row = Group(env, "Middle row");
            // 03 Tavern, west of the market (x -18..-10, z -4..2).
            var tavern = Building(row, "03 Tavern", new Vector3(-14f, 0, -4f), 0f, "tavern_front", "tavern_side", "tavern_back");
            EaveSign(tavern, "plaque_tavern", "tavern_front", "tavern_side");
            var tp = Group(row, "Tavern props");
            Prop(tp, "prop_wine_jars", -16.6f, -4.7f);
            Prop(tp, "prop_barrel", -10.9f, -4.6f);
            Prop(tp, "prop_banner_wine", -9.7f, -4.3f, y: 0.9f);
            Prop(tp, "prop_lantern_red", -18.4f, -4.4f, y: 2.2f);
            PointLight(tp, "Lantern light", new Vector3(-18.4f, 2.5f, -4.6f), new Color(1f, 0.55f, 0.3f), 2f, 5f);

            // 02 Weapons & equipment shop (the corner smithy), east of the market (x 11..17, z -4..1).
            var smithy = Building(row, "02 Weapons shop", new Vector3(14f, 0, -4f), 0f, "smithy_front", "smithy_side", null, glowMask: "smithy_forge_glow_mask");
            EaveSign(smithy, "plaque_smithy", "smithy_front", "smithy_side");
            var sp = Group(row, "Shop props");
            Prop(sp, "prop_anvil", 13.4f, -4.9f);
            Prop(sp, "prop_weapon_rack", 17.6f, -4.5f, solid: true);
            Prop(sp, "prop_bow_rack", 10.2f, -4.6f, solid: true);
            Prop(sp, "prop_armor_stand", 9.4f, -4.4f);
            Prop(sp, "prop_banner_iron", 18.4f, -4.1f, y: 0.9f);
            Prop(sp, "prop_water_vat", 11.4f, -5.2f);
            PointLight(sp, "Forge light", new Vector3(15.3f, 1.0f, -4.4f), new Color(1f, 0.48f, 0.18f), 3.5f, 4.5f);

            // 05 Market & notice board on the square (x -9..9, z -14..-2), stalls either side of the main axis.
            var mk = Group(row, "05 Market");
            Prop(mk, "prop_stall_cloth", -5.6f, -5.2f, solid: true);
            Prop(mk, "prop_stall", -5.6f, -10.8f, solid: true);
            Prop(mk, "prop_stall_bamboo", 5.6f, -5.2f, solid: true);
            Prop(mk, "prop_stall_grain", 5.6f, -10.8f, solid: true);
            Prop(mk, "prop_notice_board", 3.6f, -2.6f, solid: true);
            Prop(mk, "prop_cloth_bolts", -3.8f, -6.4f);
            Prop(mk, "prop_grain_sacks", 7.8f, -12.4f);
            Prop(mk, "prop_basket_stack", 3.6f, -12.2f);
            Prop(mk, "prop_produce_basket", -3.6f, -12.2f);
            Prop(mk, "prop_carry_pole", 8.0f, -7.0f);
            Prop(mk, "prop_wine_jars", -7.8f, -12.6f);

            // Street frontage around the market: houses facing 中街 (fronts at z -4), backs facing 北横街 (fronts at z 8),
            // and gable ends along the main axis.
            var st = Group(row, "Middle streets");
            Building(st, "House 中街 W1", new Vector3(-24f, 0, -4f), 0f, "house_front", "house_side", "house_back");
            Building(st, "House 中街 W2", new Vector3(-33f, 0, -4f), 0f, "house_b_front", "house_b_side", "house_b_back", roofKey: "thatch");
            Building(st, "House 中街 E1", new Vector3(22f, 0, -4f), 0f, "house_c_front", "house_c_side", "house_c_back", roofKey: "thatch");
            Building(st, "House 中街 E2", new Vector3(30f, 0, -4f), 0f, "house_front", "house_side", "house_back");
            Building(st, "House 北横街 W1", new Vector3(-14f, 0, 8f), 180f, "house_front", "house_side", "house_back");
            Building(st, "House 北横街 W2", new Vector3(-24f, 0, 8f), 180f, "house_b_front", "house_b_side", "house_b_back", roofKey: "thatch");
            Building(st, "House 北横街 W3", new Vector3(-32f, 0, 8f), 180f, "house_c_front", "house_c_side", "house_c_back", roofKey: "thatch");
            Building(st, "House 北横街 E1", new Vector3(14f, 0, 8f), 180f, "house_b_front", "house_b_side", "house_b_back", roofKey: "thatch");
            Building(st, "House 北横街 E2", new Vector3(24f, 0, 8f), 180f, "house_front", "house_side", "house_back");
            Building(st, "House 北横街 E3", new Vector3(32f, 0, 8f), 180f, "house_c_front", "house_c_side", "house_c_back", roofKey: "thatch");
            Building(st, "House axis NW", new Vector3(-4f, 0, 5f), -90f, "house_c_front", "house_c_side", "house_c_back", roofKey: "thatch");
            Building(st, "House axis NE", new Vector3(4f, 0, 5f), 90f, "house_c_front", "house_c_side", "house_c_back", roofKey: "thatch");
        }

        static void SouthRows(Transform env)
        {
            var row = Group(env, "South rows");
            // South row: fronts face 南回环街 at z -20 (5 m deep: z -20..-15; 4 m: -20..-16).
            Building(row, "04 House A", new Vector3(-34f, 0, -20f), 0f, "house_front", "house_side", "house_back");
            Building(row, "04 House B", new Vector3(-25f, 0, -20f), 0f, "house_b_front", "house_b_side", "house_b_back", roofKey: "thatch");
            Building(row, "04 House C", new Vector3(-16f, 0, -20f), 0f, "house_c_front", "house_c_side", "house_c_back", roofKey: "thatch");
            Building(row, "06 Clinic", new Vector3(16f, 0, -20f), 0f, "clinic_front", "clinic_side", null);
            Building(row, "09 Post station", new Vector3(26f, 0, -20f), 0f, "post_station_front", "post_station_side", null);
            Building(row, "09 Stable", new Vector3(36f, 0, -20f), 0f, "stable_front", "stable_side", null, roofKey: "thatch");
            // Backs facing 中街 (fronts at z -11, 4 m deep: z -15..-11).
            Building(row, "House 中街 S W", new Vector3(-14f, 0, -11f), 180f, "house_c_front", "house_c_side", "house_c_back", roofKey: "thatch");
            Building(row, "House 中街 S E1", new Vector3(14f, 0, -11f), 180f, "house_c_front", "house_c_side", "house_c_back", roofKey: "thatch");
            Building(row, "House 中街 S E2", new Vector3(24f, 0, -11f), 180f, "clinic_front", "clinic_side", "house_c_back");
            Building(row, "House 中街 S E3", new Vector3(33f, 0, -11f), 180f, "house_c_front", "house_c_side", "house_c_back", roofKey: "thatch");
            // Gable ends along the main axis between 南回环街 and the market.
            Building(row, "House axis SW", new Vector3(-4f, 0, -17.2f), -90f, "house_c_front", "house_c_side", "house_c_back", roofKey: "thatch");
            Building(row, "House axis SE", new Vector3(4f, 0, -17.2f), 90f, "house_c_front", "house_c_side", "house_c_back", roofKey: "thatch");
            // South band: backs facing 南回环街 (fronts at z -26).
            Building(row, "House S band W1", new Vector3(-10f, 0, -26f), 180f, "house_front", "house_side", "house_back");
            Building(row, "House S band W2", new Vector3(-19f, 0, -26f), 180f, "house_b_front", "house_b_side", "house_b_back", roofKey: "thatch");
            Building(row, "House S band W3", new Vector3(-28f, 0, -26f), 180f, "house_c_front", "house_c_side", "house_c_back", roofKey: "thatch");
            Building(row, "House S band E1", new Vector3(10f, 0, -26f), 180f, "house_c_front", "house_c_side", "house_c_back", roofKey: "thatch");
            Building(row, "House S band E2", new Vector3(19f, 0, -26f), 180f, "house_front", "house_side", "house_back");
            Building(row, "House S band E3", new Vector3(28f, 0, -26f), 180f, "house_b_front", "house_b_side", "house_b_back", roofKey: "thatch");
            Building(row, "House S band E4", new Vector3(38f, 0, -26f), 180f, "house_c_front", "house_c_side", "house_c_back", roofKey: "thatch");

            var hp = Group(row, "House props");
            Prop(hp, "prop_mat_loom", -36.2f, -21.3f);
            Prop(hp, "prop_straw_mats", -31.8f, -20.6f);
            Prop(hp, "prop_firewood", -29.2f, -20.6f);
            Prop(hp, "prop_chopping_block", -21.4f, -21.2f);
            Prop(hp, "prop_laundry_line", -25f, -23.2f);
            Prop(hp, "prop_flowers", -33f, -21f);
            float fenceH = Metres("fence_wattle").y;
            Strip(hp, "Garden fence S", "fence_wattle", new Vector3(-44.5f, 0, -32.5f), new Vector3(-37.5f, 0, -32.5f), fenceH);
            Strip(hp, "Garden fence E", "fence_wattle", new Vector3(-37.5f, 0, -32.5f), new Vector3(-37.5f, 0, -27f), fenceH);

            // 11 Well yard between the south row and the market street (x -30..-18, z -15..-11).
            var wy = Group(row, "11 Well yard");
            Prop(wy, "prop_well", -24f, -13.4f, solid: true);
            Prop(wy, "prop_stone_bench", -21.2f, -14.2f, solid: true);
            Prop(wy, "prop_tree_elm", -28.6f, -12.6f, solid: true);
            Prop(wy, "prop_bush", -19.4f, -12.2f);

            var cl = Group(row, "Clinic props");
            Prop(cl, "prop_herb_rack", 18.8f, -20.9f);
            Prop(cl, "prop_herb_mortar", 13.4f, -20.6f);
            Prop(cl, "prop_banner_medicine", 12.8f, -20.4f, y: 0.9f);
            Prop(cl, "prop_potted_plant", 15.2f, -20.6f, key: "prop_flowers");

            var ps = Group(row, "Post station props");
            Prop(ps, "prop_banner_post", 29.9f, -20.4f, y: 0.9f);
            Prop(ps, "prop_hitching_post", 31.0f, -21.8f, solid: true);
            Prop(ps, "prop_trough", 34.6f, -21.4f);
            Prop(ps, "prop_hay_pile", 41.4f, -20.8f);
            Prop(ps, "prop_ox_cart", 23.6f, -22.6f, solid: true);
        }

        static void Outside(Transform env)
        {
            var o = Group(env, "Outside");
            // The road south of the gate and the peach grove outside the west gate (design §3 #12).
            Prop(o, "prop_tree_locust", -12f, -48f, scale: 0.9f, solid: true);
            Prop(o, "prop_tree_locust", 14f, -52f, scale: 0.8f, solid: true);
            Prop(o, "prop_bush", -6.5f, -42f, scale: 1.4f);
            Prop(o, "prop_bush", 7f, -44f, scale: 1.6f);
            Prop(o, "prop_flowers", 5.2f, -40.5f);
            var rng = new System.Random(7);
            for (int i = 0; i < 18; i++)
            {
                float x = -76f + (float)rng.NextDouble() * 19f, z = -12f + (float)rng.NextDouble() * 24f;
                if (Mathf.Abs(z) < 3f) continue;   // keep the lane clear
                Prop(o, i % 3 == 0 ? "prop_tree_peach_small" : "prop_tree_peach", x, z, solid: true);
            }
            // Distant hills beyond the north wall.
            Prop(o, "bg_hills_far", 0f, 70f, scale: 7f);
            Prop(o, "bg_town_rooftops_far", -70f, 60f, scale: 3f);
        }

        // ---------- props, people, lights ----------

        static void Prop(Transform parent, string name, float x, float z, float y = 0f, float scale = 1f, bool solid = false, string key = null, string glowMask = null)
        {
            key ??= name;
            var t = Tex(key);
            var size = new Vector2(t.width / Ppm, t.height / Ppm) * scale;
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(x, y, z);
            // Unit quad: PixelBillboard scales it to the prop size (and adds the square-pixel stretch).
            var q = Quad(root, "Sprite", new Vector3(0, size.y / 2f, 0), Vector3.zero, Vector2.one, Mat(key, clip: true, twoSided: true, emissionMask: glowMask));
            q.AddComponent<PixelBillboard>().BaseSize = size;
            if (solid) Solid(root, new Vector3(0, 0.75f, 0), new Vector3(Mathf.Min(size.x * 0.7f, 4f), 1.5f, Mathf.Min(size.x * 0.4f, 2f)));
            if (size.y > 3.5f) root.gameObject.AddComponent<Occluder>();
        }

        // The 12 other residents of design §4 have no sprites yet; their posts are marked so dialogue can attach later.
        static void NpcMarkers(Transform people)
        {
            var g = Group(people, "NPC posts (no sprites yet)");
            (string name, Vector3 at)[] posts =
            {
                ("N01 南门守卫", new Vector3(3.6f, 0, -32.5f)), ("N02 巡城兵", new Vector3(6f, 0, -20f)),
                ("N03 城主府掌士", new Vector3(-3f, 0, 18f)), ("N04 铁匠兼装备商", new Vector3(14f, 0, -5.4f)),
                ("N05 酒馆掌柜", new Vector3(-14f, 0, -5.2f)), ("N06 医者", new Vector3(16f, 0, -21.2f)),
                ("N07 驿卒", new Vector3(33f, 0, -21.6f)), ("N08 织席妇人", new Vector3(-35.4f, 0, -22f)),
                ("N09 街坊老者", new Vector3(-21.2f, 0, -13.2f)), ("N10 孩童", new Vector3(-26f, 0, -12.6f)),
                ("N11 粮贩", new Vector3(5.6f, 0, -12.6f)), ("N12 运粮脚夫", new Vector3(-26f, 0, 11f)),
            };
            foreach (var (name, at) in posts) { var t = new GameObject(name).transform; t.SetParent(g, false); t.localPosition = at; }
        }

        static void PointLight(Transform parent, string name, Vector3 at, Color color, float intensity, float range)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.transform.SetParent(parent, false);
            l.transform.localPosition = at;
            l.type = LightType.Point; l.color = color; l.intensity = intensity; l.range = range;
            l.shadows = LightShadows.None;
        }

        // A disabled camera preset (-camera) plus a "Spot …" marker for -teleport, which moves the player there so the
        // follow camera and the occluder cut-away behave exactly as in play.
        static void ReviewCamera(string name, Vector3 focus)
        {
            new GameObject(name.Replace("Review ", "Spot ")).transform.position = focus;
            var cam = new GameObject(name).AddComponent<Camera>();
            cam.enabled = false;
            Quaternion pitch = Quaternion.Euler(HD2DSampleBuilder.CameraPitch, 0f, 0f);
            cam.transform.SetPositionAndRotation(focus + new Vector3(0, 0.8f, 0) + pitch * new Vector3(0, 0, -HD2DSampleBuilder.CameraDistance), pitch);
            cam.fieldOfView = HD2DSampleBuilder.CameraFov;
        }
    }
}
