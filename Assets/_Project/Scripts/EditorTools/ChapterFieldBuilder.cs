using System.Collections.Generic;
using System.IO;
using System.Linq;
using Jrpg;
using ThreeKingdoms.App;
using ThreeKingdoms.Data.Battle;
using ThreeKingdoms.Gameplay;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.UI.Save;
using ThreeKingdoms.UI.Shop;
using ThreeKingdoms.UI.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace ThreeKingdoms.EditorTools
{
    // Builds the 0.2 scenes (V02_HUANGJIN_PLAN §3): the story stage, Lu Zhi's camp and Anxi county. Environment art is the
    // Codex high-resolution delivery (user 2026-10-25: painterly, 120 px per metre; art/v02-luzhi-camp-hd, -anxi-town-hd);
    // every piece falls back to a white-box shape of the right size while its texture is missing, so the scenes play
    // from the first build. Layouts follow the approved concept images: open ground of 6–8 m before the command tent,
    // the yamen and the post station, key people at their doors.
    public static class ChapterFieldBuilder
    {
        public const string StagePath = "Assets/_Project/Scenes/Story_Stage.unity";
        public const string CampPath = "Assets/_Project/Scenes/Camp_LuZhi.unity";
        public const string AnxiPath = "Assets/_Project/Scenes/Anxi_Town.unity";
        const string ArtRoot = "Assets/_Project/Art/Chapter2";
        static Transform hitchingPost;
        static string roofKey, ridgeKey, eaveKey;   // this place's roof (Building)   // Anxi: the 鞭督邮 scene hides it while the bound poses draw their own
        const string PanelSettingsPath = "Assets/_Project/Settings/BattlePanelSettings.asset";
        const float Ppm = 120f;   // the HD environment art's density
        const string Art = @"D:\BuildArtifacts\Codex\hd2d-jrpg\art";

        static string dir;      // the texture folder of the scene being built
        static string matDir;
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

        [MenuItem("JRPG/Build 0.2 Scenes (stage, camp, Anxi)")]
        public static void Build()
        {
            DuyouPoseImporter.Import();   // the 鞭督邮 story poses into the brothers' and the inspector's atlases
            ImportArt();
            BuildStage();
            BuildCamp();
            BuildAnxi();
            var paths = new[] { StagePath, CampPath, AnxiPath };
            var scenes = EditorBuildSettings.scenes.Where(s => !paths.Contains(s.path)).ToList();
            scenes.AddRange(paths.Select(p => new EditorBuildSettingsScene(p, true)));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("CHAPTER_SCENES_BUILT");
        }

        public static void BuildFromCommandLine()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        // ---------- art ----------

        static void ImportArt()
        {
            Copy(Path.Combine(Art, "v02-luzhi-camp-hd"), $"{ArtRoot}/Camp");
            Copy(Path.Combine(Art, "v02-anxi-town-hd"), $"{ArtRoot}/Anxi");
            // Roofs (user 2026-10-26: "安喜城里的房顶都是灰模"; Codex art/v02-roofs-hd): grey tiles in Anxi, thatch in the camp.
            Copy(Path.Combine(Art, "v02-roofs-hd"), $"{ArtRoot}/Anxi", f => f.StartsWith("roof_anxi_"));
            Copy(Path.Combine(Art, "v02-roofs-hd"), $"{ArtRoot}/Camp", f => f.StartsWith("roof_camp_"));
            Copy(Path.Combine(Art, "v02-characters-sprites"), $"{ArtRoot}/Props", f => f.StartsWith("PROP_") || f == "DU_YOU_BOUND.png" || f == "LU_ZHI_CART_SIT.png");
            Copy(Path.Combine(Art, "world-map-chapter1"), $"{ArtRoot}/Map", f => f == "worldmap_chapter1_zoom.png");
            Copy(Path.Combine(Art, "v02-battle-bg-hd"), $"{ArtRoot}/Stage", f => f.StartsWith("stage_") || f == "B2_road_scene.png");
            Copy(Path.Combine(Art, "v02-ui-slices"), $"{ArtRoot}/UI", f => f.StartsWith("ui_"));
            AssetDatabase.Refresh();
            foreach (var sub in new[] { "Camp", "Anxi", "Props", "Map", "Stage", "UI" })
            {
                string d = $"{ArtRoot}/{sub}";
                if (!Directory.Exists(d)) continue;
                var axes = ReadAxes(d);
                foreach (var path in Directory.GetFiles(d, "*.png"))
                {
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
                    if (importer == null) continue;
                    string key = Path.GetFileNameWithoutExtension(path);
                    axes.TryGetValue(key, out string a);
                    if (a == null) a = key.StartsWith("ground_") || key.StartsWith("water_") ? "xy" : "none";
                    if (key.StartsWith("roof_")) a = key.EndsWith("_tiles") || key.EndsWith("_thatch") ? "xy" : "x";
                    bool pixel = sub == "Props" || sub == "UI";
                    importer.textureType = TextureImporterType.Default;
                    importer.filterMode = pixel ? FilterMode.Point : FilterMode.Bilinear;
                    importer.mipmapEnabled = !pixel && sub != "Map" && sub != "Stage";
                    importer.textureCompression = pixel ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.maxTextureSize = key.StartsWith("far_") ? 8192 : 2048;   // the 5120-px horizon strips keep their detail
                    importer.alphaIsTransparency = true;
                    importer.wrapModeU = a.Contains("x") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                    importer.wrapModeV = a.Contains("y") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                    importer.SaveAndReimport();
                }
            }
        }

        static void Copy(string from, string to, System.Func<string, bool> keep = null)
        {
            if (!Directory.Exists(from)) return;
            Directory.CreateDirectory(to);
            foreach (var f in Directory.GetFiles(from, "*.png"))
            {
                string name = Path.GetFileName(f);
                if (name == "preview.png" || (keep != null && !keep(name))) continue;
                File.Copy(f, Path.Combine(to, name), true);
            }
            // A filtered copy adds a few files to a folder: it keeps that folder's own manifest (its tiling axes).
            if (keep == null && File.Exists(Path.Combine(from, "manifest.json"))) File.Copy(Path.Combine(from, "manifest.json"), Path.Combine(to, "manifest.json"), true);
        }

        [System.Serializable] sealed class ManifestFile { public string filename; public string tile_axes; }
        [System.Serializable] sealed class Manifest { public ManifestFile[] files; }

        static Dictionary<string, string> ReadAxes(string d)
        {
            var axes = new Dictionary<string, string>();
            string m = $"{d}/manifest.json";
            if (!File.Exists(m)) return axes;
            try
            {
                var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(m));
                foreach (var f in manifest.files ?? new ManifestFile[0])
                    if (!string.IsNullOrEmpty(f.filename)) axes[Path.GetFileNameWithoutExtension(f.filename)] = f.tile_axes ?? "none";
            }
            catch (System.Exception) { }
            return axes;
        }

        static Texture2D Tex(string key) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{key}.png") ?? AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtRoot}/Props/{key}.png");

        // ---------- materials and shapes ----------

        static Material Mat(string key, Texture2D tex, Color color, bool clip)
        {
            string id = (key ?? "c" + ColorUtility.ToHtmlStringRGB(color)) + (clip ? "_clip" : "");
            if (mats.TryGetValue(id, out var cached)) return cached;
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = id };
            if (tex) m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", tex ? Color.white : color);
            m.SetFloat("_Smoothness", 0f);
            m.SetFloat("_SpecularHighlights", 0f); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            if (clip)
            {
                m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Cull", (float)CullMode.Off);
                m.renderQueue = (int)RenderQueue.AlphaTest;
            }
            Directory.CreateDirectory(matDir);
            string path = $"{matDir}/{id}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            mats[id] = m;
            return m;
        }

        static GameObject Quad(Transform parent, string name, Vector3 pos, Vector3 euler, Vector2 size, Material m, Vector2 tiling, bool shadows = true)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            q.transform.localPosition = pos;
            q.transform.localRotation = Quaternion.Euler(euler);
            q.transform.localScale = new Vector3(size.x, size.y, 1f);
            var r = q.GetComponent<MeshRenderer>();
            if (tiling != Vector2.one)
            {
                // A per-object tiled copy keeps one shared material per texture otherwise.
                var tiled = new Material(m) { name = m.name + $"_{tiling.x:0.##}x{tiling.y:0.##}" };
                tiled.SetTextureScale("_BaseMap", tiling);
                string path = $"{matDir}/{tiled.name}.mat";
                var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (existing) tiled = existing; else AssetDatabase.CreateAsset(tiled, path);
                r.sharedMaterial = tiled;
            }
            else r.sharedMaterial = m;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return q;
        }

        static void Solid(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.AddComponent<BoxCollider>().size = size;
        }

        // Ground: the texture tiles every 12 m; without it a flat colour.
        static void Ground(Transform parent, string name, string key, Color fallback, float xMin, float xMax, float zMin, float zMax, float y = 0f)
        {
            var tex = Tex(key);
            var size = new Vector2(xMax - xMin, zMax - zMin);
            var tiling = tex ? new Vector2(size.x * Ppm / tex.width, size.y * Ppm / tex.height) : Vector2.one;
            Quad(parent, name, new Vector3((xMin + xMax) / 2f, y, (zMin + zMax) / 2f), new Vector3(90f, 0, 0), size, Mat(key, tex, fallback, false), tiling, shadows: false);
        }

        // A standing picture (tent, tree, prop) facing the camera; a white-box block of the given size until delivered.
        static Transform Billboard(Transform parent, string name, string key, float x, float z, Vector3 fallbackSize, Color fallback, bool solid = false, int frames = 1)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(x, 0, z);
            var tex = Tex(key);
            Vector3 footprint;
            if (tex)
            {
                // Pixel props (Codex 418-px cells) stand at the characters' scale: one cell = 1.6 m.
                float ppm = AssetDatabase.GetAssetPath(tex).Contains("/Props/") ? 418f / 1.6f : Ppm;
                var size = new Vector2(tex.width / ppm / frames, tex.height / ppm);
                var q = Quad(root, "Picture", new Vector3(0, size.y / 2f, 0), Vector3.zero, size, Mat(key, tex, Color.white, true), new Vector2(1f / frames, 1f));
                q.AddComponent<PixelBillboard>().BaseSize = size;
                footprint = new Vector3(size.x * 0.8f, Mathf.Min(size.y, 3f), Mathf.Min(size.x * 0.5f, 4f));
            }
            else
            {
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(box.GetComponent<Collider>());
                box.name = "White box";
                box.transform.SetParent(root, false);
                // The block stands behind its foot line (like a picture's footprint), so people at the door stay outside it.
                box.transform.localPosition = new Vector3(0, fallbackSize.y / 2f, fallbackSize.z / 2f);
                box.transform.localScale = fallbackSize;
                box.GetComponent<MeshRenderer>().sharedMaterial = Mat(null, null, fallback, false);
                footprint = fallbackSize;
            }
            if (solid) Solid(root, "Collider", new Vector3(0, footprint.y / 2f, tex ? 0f : footprint.z / 2f), footprint);
            if (footprint.y > 3.5f) root.gameObject.AddComponent<Occluder>();
            return root;
        }

        // A building from a front façade and a side gable (the Zhuojun convention), a grey roof over it; a box until the
        // textures are delivered. frontCenter is the middle of the front wall's foot; the building extends north (+z).
        static Transform Building(Transform parent, string name, string front, string side, Vector3 frontCenter, float width, float wall, float depth, Color fallback)
        {
            var b = new GameObject(name).transform;
            b.SetParent(parent, false);
            b.localPosition = frontCenter;
            b.gameObject.AddComponent<Occluder>();
            var f = Tex(front);
            var s = Tex(side);
            if (f) { width = f.width / Ppm; wall = f.height / Ppm; }
            if (s) depth = s.width / Ppm;
            Solid(b, "Collider", new Vector3(0, wall / 2f, depth / 2f), new Vector3(width, wall, depth));
            if (f) Quad(b, "Front", new Vector3(0, wall / 2f, 0), Vector3.zero, new Vector2(width, wall), Mat(front, f, Color.white, true), Vector2.one);
            else Box(b, "Front box", new Vector3(0, wall / 2f, depth / 2f), new Vector3(width, wall, depth), fallback);
            float gable = s ? Mathf.Max(0.5f, s.height / Ppm - wall) : depth * 0.35f;
            if (s)
            {
                Quad(b, "Gable L", new Vector3(-width / 2f, (wall + gable) / 2f, depth / 2f), new Vector3(0, 90, 0), new Vector2(depth, wall + gable), Mat(side, s, Color.white, true), Vector2.one);
                Quad(b, "Gable R", new Vector3(width / 2f, (wall + gable) / 2f, depth / 2f), new Vector3(0, -90, 0), new Vector2(depth, wall + gable), Mat(side, s, Color.white, true), Vector2.one);
            }
            // Roof: two slopes of this place's roof (Anxi grey tiles, the camp's thatch; Codex art/v02-roofs-hd, one tile ≈ 4 m),
            // a ridge along the top and a row of eave tiles at the front edge. Before the art arrives: the Zhuojun grey tiles.
            var roofTex = Tex(roofKey) ?? AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Environment/ZhuojunMap/roof_tiles_grey.png");
            bool hd = roofTex && roofTex.name == roofKey;
            var roofMat = Mat(roofTex ? roofTex.name : "roof_grey", roofTex, new Color(0.36f, 0.38f, 0.42f), false);
            float half = depth / 2f + 0.5f, slope = Mathf.Atan2(gable, depth / 2f);
            float len = half / Mathf.Cos(slope);
            var roofSize = new Vector2(width + 0.7f, len);
            var tiling = !roofTex ? Vector2.one : hd ? roofSize / 4f : new Vector2(roofSize.x * 30f / roofTex.width, roofSize.y * 30f / roofTex.height);
            float y = wall + gable / 2f - 0.15f;
            Quad(b, "Roof front", new Vector3(0, y + 0.05f, depth / 2f - half / 2f), new Vector3(90f - slope * Mathf.Rad2Deg, 0, 0), roofSize, roofMat, tiling);
            Quad(b, "Roof back", new Vector3(0, y + 0.05f, depth / 2f + half / 2f), new Vector3(90f - slope * Mathf.Rad2Deg, 180, 0), roofSize, roofMat, tiling);
            float rise = Mathf.Sin(slope) * len / 2f;
            var ridge = Tex(ridgeKey);
            if (ridge)
            {
                float h = 0.38f, tileW = h * ridge.width / ridge.height;
                Quad(b, "Ridge", new Vector3(0, y + 0.05f + rise + h * 0.3f, depth / 2f), Vector3.zero, new Vector2(width + 0.9f, h), Mat(ridgeKey, ridge, Color.white, true), new Vector2((width + 0.9f) / tileW, 1f));
            }
            var eave = Tex(eaveKey);
            if (eave)
            {
                float h = 0.26f, tileW = h * eave.width / eave.height;
                Quad(b, "Eave", new Vector3(0, y + 0.05f - rise - h * 0.25f, depth / 2f - half - 0.02f), Vector3.zero, new Vector2(width + 0.7f, h), Mat(eaveKey, eave, Color.white, true), new Vector2((width + 0.7f) / tileW, 1f));
            }
            return b;
        }

        static void Box(Transform parent, string name, Vector3 center, Vector3 size, Color color)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(box.GetComponent<Collider>());
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = center;
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = Mat(null, null, color, false);
        }

        // A straight run of a tiling strip (palisade, earth wall), solid.
        static void Strip(Transform parent, string name, string key, Vector3 a, Vector3 b, float height, Color fallback)
        {
            var tex = Tex(key);
            var mid = (a + b) / 2f;
            float len = Vector3.Distance(a, b);
            float yaw = Mathf.Atan2(b.x - a.x, b.z - a.z) * Mathf.Rad2Deg - 90f;
            var g = new GameObject(name).transform;
            g.SetParent(parent, false);
            g.localPosition = mid;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            if (tex)
            {
                float h = tex.height / Ppm;
                Quad(g, "Strip", new Vector3(0, h / 2f, 0), Vector3.zero, new Vector2(len, h), Mat(key, tex, Color.white, true), new Vector2(len * Ppm / tex.width, 1f));
                height = h;
            }
            else Box(g, "Strip box", new Vector3(0, height / 2f, 0), new Vector3(len, height, 0.5f), fallback);
            Solid(g, "Collider", new Vector3(0, 1f, 0), new Vector3(len, 2f, 0.6f));
        }

        static Transform Person(Transform parent, string name, string key, Vector3 at, int frame = 0)
        {
            var go = HD2DSampleBuilder.Character(name, key, at, frame, isPlayer: false);
            go.transform.SetParent(parent, true);
            return go.transform;
        }

        static string Resident(string id) => ResidentImporter.KeyFor(id);
        static string Folk(string id) => ResidentImporter.ChapterKey(id);

        // ---------- shared scene setup ----------

        sealed class Field
        {
            public Transform Env, People, Spots;
            public FieldStoryController Controller;
        }

        static Field NewField(string name, Vector3 playerStart, Color fog)
        {
            mats.Clear();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.86f, 0.90f, 1.0f);       // bright noon (user 2026-10-25: no yellow cast)
            RenderSettings.ambientEquatorColor = new Color(0.78f, 0.78f, 0.76f);
            RenderSettings.ambientGroundColor = new Color(0.42f, 0.40f, 0.36f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.005f;
            RenderSettings.fogColor = fog;

            var sun = new GameObject("Noon sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.98f, 0.94f);
            sun.intensity = 2.3f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.7f;
            sun.transform.rotation = Quaternion.Euler(55f, -25f, 0f);

            var f = new Field
            {
                Env = new GameObject("Environment").transform,
                People = new GameObject("People").transform,
                Spots = new GameObject("Spots").transform,
            };
            // The floor everyone stands on (the ground pictures are only quads).
            Solid(f.Env, "Floor collider", new Vector3(0, -0.5f, 0), new Vector3(200f, 1f, 200f));
            var player = HD2DSampleBuilder.Character("Liu Bei", "LiuBei", playerStart, 3, isPlayer: true);
            player.transform.SetParent(f.People, true);
            player.AddComponent<PartyTrail>();

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = HD2DSampleBuilder.CameraFov;
            cam.nearClipPlane = 1f; cam.farClipPlane = 300f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.72f, 0.84f, 0.96f);
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.None;
            Quaternion pitch = Quaternion.Euler(HD2DSampleBuilder.CameraPitch, 0f, 0f);
            camGo.transform.rotation = pitch;
            var follow = camGo.AddComponent<FollowCamera>();
            follow.Target = player.transform;
            follow.Offset = new Vector3(0, 0.8f, 0) + pitch * new Vector3(0, 0, -HD2DSampleBuilder.CameraDistance);
            camGo.transform.position = player.transform.position + follow.Offset;
            follow.UseRig = true;
            follow.Pitch = HD2DSampleBuilder.CameraPitch; follow.Distance = HD2DSampleBuilder.CameraDistance; follow.Fov = HD2DSampleBuilder.CameraFov;
            camGo.AddComponent<SeeThroughOccluders>().Target = player.transform;

            // Bright noon (user 2026-10-25: 白天色调明媚，不要昏黄): no warm town LUT, neutral tonemapping, a touch of exposure.
            var high = NoonProfile(NoonHighPath, depthOfField: true);
            var low = NoonProfile(NoonLowPath, depthOfField: false);
            var volumeGo = new GameObject("Global Volume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = high;
            var tier = volumeGo.AddComponent<HD2DQualityTier>();
            tier.Volume = volume; tier.High = high; tier.Low = low;
            follow.FocusVolume = volume;

            var go = new GameObject(name + " Story");
            var c = go.AddComponent<FieldStoryController>();
            c.Catalog = AssetDatabase.LoadAssetAtPath<BattleCatalog>(BattleDataBuilder.CatalogPath);
            c.Player = player.GetComponent<PlayerMover>();
            c.Camera = follow;
            c.View = cam;
            c.GuanYuFollower = Follower("Guan Yu (follower)", "GuanYu");
            c.ZhangFeiFollower = Follower("Zhang Fei (follower)", "ZhangFei");
            c.InteractAction = ProjectSetup.Action("Interact");
            c.MenuAction = ProjectSetup.Action("Menu");
            c.Stage = go.AddComponent<StagePlayer>();
            c.Stage.Confirm = ProjectSetup.Action("Interact");
            f.Controller = c;
            return f;
        }

        internal const string NoonHighPath = "Assets/_Project/Settings/HD2D_Noon.asset", NoonLowPath = "Assets/_Project/Settings/HD2D_Noon_Low.asset";
        internal const string NoonBattlePath = "Assets/_Project/Settings/HD2D_Noon_Battle.asset";

        internal static VolumeProfile NoonProfile(string path, bool depthOfField)
        {
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (existing) return existing;   // built once per run; both scenes share it
            var p = HD2DSampleBuilder.CreateProfile(path, depthOfField, bloomThreshold: 1.05f, bloomIntensity: 0.45f, highQualityBloom: depthOfField, lut: null);
            foreach (var c in p.components)
            {
                if (c is Tonemapping t) t.mode.Override(TonemappingMode.Neutral);
                if (c is ColorAdjustments a) { a.postExposure.Override(0.25f); a.contrast.Override(8f); a.saturation.Override(10f); }
                if (c is ColorLookup l) { l.contribution.Override(0f); l.active = false; }
                if (c is Vignette v) v.intensity.Override(0.18f);
            }
            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssets();
            return p;
        }

        static PartyFollower Follower(string name, string key)
        {
            var go = HD2DSampleBuilder.Character(name, key, Vector3.zero, 9, isPlayer: false);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var animator = go.GetComponentInChildren<SpriteFrameAnimator>();
            go.AddComponent<PixelWalkFrames>().Animator = animator;
            return go.AddComponent<PartyFollower>();
        }

        static Transform Spot(Field f, string name, Vector3 at)
        {
            var t = new GameObject(name).transform;
            t.SetParent(f.Spots, false);
            t.position = at;
            return t;
        }

        // The overlay, the shop, the save slots, the travel menu, the ink scroll and an EventSystem.
        static void Huds(FieldStoryController c)
        {
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            var skin = AssetDatabase.LoadAssetAtPath<ThreeKingdoms.UI.Battle.BattleUiSkin>(BattleUiSkinBuilder.SkinPath);
            var portraits = AssetDatabase.LoadAssetAtPath<DialoguePortraitSet>(DialoguePortraitImporter.SetPath);
            var hudGo = new GameObject("Stage HUD");
            var doc = hudGo.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            var hud = hudGo.AddComponent<StageHud>();
            hud.Screen = c; hud.Skin = skin; hud.Portraits = portraits;
            hud.Map = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtRoot}/Map/worldmap_chapter1_zoom.png");
            CinematicsArtImporter.AddView(panel);
            foreach (var (name, add) in new (string, System.Action<GameObject>)[]
            {
                ("Shop HUD", g => { var s = g.AddComponent<ShopHud>(); s.Host = c; s.Skin = skin; }),
                ("Save HUD", g => { var s = g.AddComponent<SaveHud>(); s.Host = c; s.Skin = skin; }),
            })
            {
                var g = new GameObject(name);
                var d = g.AddComponent<UIDocument>();
                d.panelSettings = panel;
                d.sortingOrder = 10;
                add(g);
            }
            var menuGo = new GameObject("Main Menu HUD");
            var menu = menuGo.AddComponent<ThreeKingdoms.UI.Menu.MainMenuHud>();
            menu.Host = c;
            TravelMenuArtImporter.Import(out menu.Art, out menu.Borders);
            menu.BingshuArt = TravelMenuArtImporter.BingshuSlices();
            menu.Portraits = portraits;
            menu.Busts = new[] { "LiuBei", "GuanYu", "ZhangFei" }.Select(k => AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/_Project/Art/Portraits/{k}_Bust.png")).ToArray();
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        static void Far(Transform env, string key, float z, float width, float height, Color fallback)
        {
            var tex = Tex(key);
            if (!tex) return;
            var size = new Vector2(width, width * tex.height / tex.width);
            Quad(env, "Far " + key, new Vector3(0, size.y / 2f - 2f, z), Vector3.zero, size, Mat(key, tex, fallback, true), Vector2.one, shadows: false);
        }

        // ---------- Lu Zhi's camp (S1, about 60 × 40 m; the south gate at z −20, the command tent at z 12) ----------

        static void BuildCamp()
        {
            dir = $"{ArtRoot}/Camp";
            roofKey = "roof_camp_thatch"; ridgeKey = "roof_camp_ridge"; eaveKey = null;
            matDir = $"{ArtRoot}/Materials/Camp";
            var f = NewField("Camp", new Vector3(0, 0, -17f), new Color(0.80f, 0.86f, 0.92f));
            var env = f.Env;
            var dirt = new Color(0.78f, 0.68f, 0.50f);
            var grass = new Color(0.52f, 0.66f, 0.38f);
            var canvas = new Color(0.92f, 0.88f, 0.80f);
            var wood = new Color(0.50f, 0.36f, 0.22f);
            var red = new Color(0.72f, 0.16f, 0.12f);

            Ground(env, "Grass outside", "ground_camp_grass", grass, -70, 70, -60, 60, -0.02f);
            Ground(env, "Camp ground", "ground_camp_dirt_a", dirt, -30, 30, -20, 20);
            Ground(env, "Main road", "ground_camp_ruts", new Color(0.72f, 0.62f, 0.46f), -2.5f, 2.5f, -24, 4, 0.01f);
            Ground(env, "Command square", "ground_camp_dirt_b", new Color(0.82f, 0.72f, 0.54f), -6, 6, 0, 11, 0.012f);

            // Palisade all round with the gate in the south; abatis outside; two watchtowers at the gate.
            Strip(env, "Palisade S-W", "palisade_segment", new Vector3(-30, 0, -20), new Vector3(-3, 0, -20), 2.5f, wood);
            Strip(env, "Palisade S-E", "palisade_segment", new Vector3(3, 0, -20), new Vector3(30, 0, -20), 2.5f, wood);
            Strip(env, "Palisade W", "palisade_segment", new Vector3(-30, 0, -20), new Vector3(-30, 0, 20), 2.5f, wood);
            Strip(env, "Palisade E", "palisade_segment", new Vector3(30, 0, -20), new Vector3(30, 0, 20), 2.5f, wood);
            Strip(env, "Palisade N", "palisade_segment", new Vector3(-30, 0, 20), new Vector3(30, 0, 20), 2.5f, wood);
            Billboard(env, "Gate frame", "palisade_gate", 0, -20.2f, new Vector3(6f, 4f, 0.4f), wood);
            Billboard(env, "Watchtower W", "watchtower", -4.6f, -20.6f, new Vector3(3f, 8f, 3f), wood, solid: true);
            Billboard(env, "Watchtower E", "watchtower", 4.6f, -20.6f, new Vector3(3f, 8f, 3f), wood, solid: true);
            for (int i = 0; i < 6; i++)
            {
                Billboard(env, "Abatis W" + i, "luzhai_abatis", -26 + i * 4f, -21.6f, new Vector3(3.6f, 1.2f, 0.6f), wood);
                Billboard(env, "Abatis E" + i, "luzhai_abatis", 7 + i * 4f, -21.6f, new Vector3(3.6f, 1.2f, 0.6f), wood);
            }

            // The command tent with its open square (≥ 8 × 10 m), the 盧 banner and the great standard.
            Billboard(env, "Command tent", "tent_han_command", 0, 13.5f, new Vector3(13f, 8f, 7f), canvas, solid: true);
            Billboard(env, "Banner 盧", "banner_lu_general", -7.5f, 11.5f, new Vector3(0.6f, 5f, 0.3f), red);
            Billboard(env, "Great standard", "banner_dadu_red", 7.5f, 11.5f, new Vector3(0.8f, 6f, 0.3f), red);
            // Rows of tents on both sides of the main road.
            float[] rows = { -14f, -7f };
            foreach (float z in rows)
                foreach (float x in new[] { -24f, -17f, 17f, 24f })
                    Billboard(env, $"Tent {x},{z}", "tent_han_small", x, z, new Vector3(5f, 3.5f, 4f), canvas, solid: true);
            foreach (float x in new[] { -8f, 8f })
                Billboard(env, $"Tent road {x}", "tent_han_small", x, -14f, new Vector3(5f, 3.5f, 4f), canvas, solid: true);
            // Supply tent (east) and the wounded (west), each with 6 m clear before the door.
            Billboard(env, "Supply tent", "tent_han_supply", 12f, 1f, new Vector3(7f, 4f, 5f), canvas, solid: true);
            Billboard(env, "Grain sacks", "grain_sacks", 15.4f, -1f, new Vector3(2f, 1.5f, 1.5f), new Color(0.80f, 0.72f, 0.52f), solid: true);
            Billboard(env, "Arrow bundles", "arrow_bundles", 8.6f, -0.6f, new Vector3(1.5f, 1.5f, 1f), wood);
            Billboard(env, "Wounded tent", "tent_han_wounded", -12f, 1f, new Vector3(7f, 4f, 5f), canvas, solid: true);
            Billboard(env, "Straw mat A", "straw_mat", -10.8f, -2.4f, new Vector3(2f, 0.2f, 1f), new Color(0.80f, 0.70f, 0.44f));
            Billboard(env, "Straw mat B", "straw_mat", -13.4f, -2.4f, new Vector3(2f, 0.2f, 1f), new Color(0.80f, 0.70f, 0.44f));
            // Stable (north-east) and training ground (north-west).
            Billboard(env, "Stable", "stable_shed", 19f, 14f, new Vector3(10f, 5f, 5f), wood, solid: true);
            Billboard(env, "Horse", "horse_brown", 17.4f, 12.6f, new Vector3(2.5f, 2f, 0.8f), new Color(0.48f, 0.30f, 0.18f));
            Billboard(env, "Trough", "horse_trough", 21f, 12.2f, new Vector3(3f, 1f, 1f), wood);
            Billboard(env, "Hay", "grain_sacks", 23.4f, 14.6f, new Vector3(2f, 1.6f, 1.6f), new Color(0.86f, 0.76f, 0.40f), solid: true);
            Billboard(env, "Weapon rack", "weapon_rack", -20f, 14f, new Vector3(2.5f, 2f, 0.6f), wood);
            Billboard(env, "Target A", "archery_target", -24f, 15f, new Vector3(1.5f, 2f, 0.3f), new Color(0.90f, 0.86f, 0.74f));
            Billboard(env, "Target B", "archery_target", -21.6f, 15.4f, new Vector3(1.5f, 2f, 0.3f), new Color(0.90f, 0.86f, 0.74f));
            Billboard(env, "Brazier", "brazier", 3.4f, -17.6f, new Vector3(1f, 1.2f, 1f), new Color(0.3f, 0.3f, 0.3f));
            Billboard(env, "Water jar", "water_jar", -3.4f, -17.6f, new Vector3(1f, 1.2f, 1f), new Color(0.45f, 0.36f, 0.28f));
            foreach (var x in new[] { -28f, -14f, 14f, 28f })
                Billboard(env, "Banner " + x, "banner_han_red", x, 18.6f, new Vector3(0.4f, 4f, 0.2f), red);
            Far(env, "far_julu_plain_guangzong", 60f, 140f, 30f, Color.white);

            // People at their doors (V02_HUANGJIN_PLAN §6.2).
            var p = f.People;
            var people = new List<Transform>
            {
                Person(p, "lu_zhi 卢植", Folk("C01"), new Vector3(0, 0, 10.4f)),
                Person(p, "lu_guard 亲兵", Folk("C09"), new Vector3(-3.2f, 0, 10.6f)),
                Person(p, "lu_guard 亲兵乙", Folk("C09"), new Vector3(3.2f, 0, 10.6f)),
                Person(p, "camp_guard 营门守卒", Folk("C09"), new Vector3(-2.6f, 0, -18.6f)),
                Person(p, "camp_guard 营门守卒乙", Folk("C09"), new Vector3(2.6f, 0, -18.6f)),
                Person(p, "quartermaster 军需官", Resident("N11"), new Vector3(12f, 0, -1.2f)),
                Person(p, "wounded_a 伤兵", Resident("N02"), new Vector3(-10.8f, 0, -2.6f), 7),
                Person(p, "wounded_b 伤兵", Resident("N12"), new Vector3(-13.4f, 0, -2.6f), 7),
                Person(p, "veteran 老卒", Resident("N09"), new Vector3(-17f, 0, 10.6f)),
            };
            for (int i = 0; i < 3; i++)
                Person(p, "Han soldier " + i, Folk("C09"), new Vector3(-6f + i * 6f, 0, -10f + (i % 2) * 2f));
            var c = f.Controller;
            c.Location = FieldStoryController.Place.Camp;
            c.People = people.ToArray();
            c.Spots = new[] { Spot(f, "quiver 草堆", new Vector3(23.4f, 0, 13.2f)) };
            c.ArrivalSpot = Spot(f, "Arrival (inside the gate)", new Vector3(0, 0, -17f));
            c.ReturnSpot = c.ArrivalSpot;
            Huds(c);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), CampPath);
        }

        // ---------- Anxi county (S2, about 70 × 50 m; south gate z −25, yamen z 18, post station x 22, east gate x 35) ----------

        static void BuildAnxi()
        {
            dir = $"{ArtRoot}/Anxi";
            roofKey = "roof_anxi_tiles"; ridgeKey = "roof_anxi_ridge"; eaveKey = "roof_anxi_eave";
            matDir = $"{ArtRoot}/Materials/Anxi";
            var f = NewField("Anxi", new Vector3(0, 0, -21f), new Color(0.82f, 0.88f, 0.94f));
            var env = f.Env;
            var loess = new Color(0.80f, 0.70f, 0.52f);
            var stone = new Color(0.66f, 0.66f, 0.62f);
            var grass = new Color(0.50f, 0.66f, 0.36f);
            var wall = new Color(0.74f, 0.62f, 0.46f);
            var house = new Color(0.82f, 0.74f, 0.60f);
            var wood = new Color(0.52f, 0.38f, 0.24f);

            Ground(env, "Grass outside", "ground_anxi_grass", grass, -80, 80, -70, 70, -0.02f);
            Ground(env, "Town ground", "ground_anxi_loess", loess, -36, 36, -26, 26);
            Ground(env, "Main street", "ground_anxi_flagstone", stone, -3f, 3f, -26, 9, 0.01f);
            Ground(env, "Yamen square", "ground_anxi_flagstone", stone, -8, 8, 9, 17.6f, 0.011f);
            Ground(env, "Post station yard", "ground_anxi_loess", new Color(0.84f, 0.74f, 0.56f), 15, 29, 5, 13.6f, 0.012f);
            Ground(env, "East road", "ground_anxi_loess", new Color(0.76f, 0.66f, 0.48f), 3, 40, -2, 2, 0.013f);
            // The river through the west of town, pebbles along its banks.
            Ground(env, "River banks", "ground_anxi_pebbles", new Color(0.70f, 0.68f, 0.62f), -33, -22, -26, 26, 0.014f);
            Ground(env, "River", "water_anxi_river", new Color(0.36f, 0.62f, 0.80f), -30.5f, -24.5f, -26, 26, 0.02f);
            Solid(env, "River (no wading)", new Vector3(-27.5f, 1f, 12f), new Vector3(6f, 2f, 28f));
            Solid(env, "River (no wading) S", new Vector3(-27.5f, 1f, -17f), new Vector3(6f, 2f, 18f));

            // Low rammed-earth walls (3.5 m), gates south and east.
            Strip(env, "Wall S-W", "wall_anxi_rammed", new Vector3(-36, 0, -26), new Vector3(-3.5f, 0, -26), 3.5f, wall);
            Strip(env, "Wall S-E", "wall_anxi_rammed", new Vector3(3.5f, 0, -26), new Vector3(36, 0, -26), 3.5f, wall);
            Strip(env, "Wall W", "wall_anxi_rammed", new Vector3(-36, 0, -26), new Vector3(-36, 0, 26), 3.5f, wall);
            Strip(env, "Wall N", "wall_anxi_rammed", new Vector3(-36, 0, 26), new Vector3(36, 0, 26), 3.5f, wall);
            Strip(env, "Wall E-S", "wall_anxi_rammed", new Vector3(36, 0, -26), new Vector3(36, 0, -3f), 3.5f, wall);
            Strip(env, "Wall E-N", "wall_anxi_rammed", new Vector3(36, 0, 3f), new Vector3(36, 0, 26), 3.5f, wall);
            Billboard(env, "South gate", "gate_anxi_south", 0, -26.2f, new Vector3(8f, 5.5f, 0.6f), wall);
            var eastGate = Billboard(env, "East gate", "gate_anxi_east", 36.2f, 0, new Vector3(0.6f, 5.5f, 8f), wall);
            Solid(env, "East gate (closed)", new Vector3(37.4f, 1f, 0f), new Vector3(1f, 2f, 6f));

            // Yamen north with a 6 m square, the drum and stone lions; the clerk on the steps.
            Building(env, "Yamen", "yamen_front", "yamen_side", new Vector3(0, 0, 18f), 12f, 5f, 8f, house);
            Billboard(env, "Yamen plaque", "plaque_yamen", 0, 17.9f, new Vector3(1f, 0.3f, 0.1f), wood).localPosition = new Vector3(0, 4.1f, 17.9f);
            Billboard(env, "Drum", "drum_stand", 5.4f, 16.6f, new Vector3(1.2f, 1.6f, 0.8f), new Color(0.6f, 0.2f, 0.16f));
            Billboard(env, "Lion W", "stone_lion", -3.6f, 17.2f, new Vector3(0.8f, 1.1f, 0.8f), stone, solid: true);
            Billboard(env, "Lion E", "stone_lion", 3.6f, 17.2f, new Vector3(0.8f, 1.1f, 0.8f), stone, solid: true);
            // Post station north-east: 8 m yard and the hitching post where C12 plays.
            Building(env, "Post station", "yiguan_front", "yiguan_side", new Vector3(22f, 0, 14f), 10f, 4f, 7f, house);
            Billboard(env, "Post plaque", "plaque_yiguan", 22f, 13.9f, new Vector3(0.8f, 0.3f, 0.1f), wood).localPosition = new Vector3(22f, 3.3f, 13.9f);
            hitchingPost = Billboard(env, "hitching_post 马枊", "hitching_post", 20f, 9f, new Vector3(0.3f, 1.6f, 0.3f), wood, solid: true);
            Billboard(env, "Post stable", "stable_shed", 30f, 13f, new Vector3(6f, 4f, 4f), wood, solid: true);
            // Two shops facing the market street; the Wang and Li homes face each other mid-street (the ox case).
            Building(env, "Weapons shop", "shop_weapons_front", "shop_side", new Vector3(-9f, 0, -8f), 7f, 3.5f, 6f, house);
            Billboard(env, "Sign 兵器", "plaque_bingqi", -5.4f, -8.2f, new Vector3(0.25f, 0.8f, 0.1f), wood);
            Building(env, "Goods shop", "shop_goods_front", "shop_side", new Vector3(9f, 0, -8f), 7f, 3.5f, 6f, house);
            Billboard(env, "Sign 雜貨", "plaque_zahuo", 12.6f, -8.2f, new Vector3(0.25f, 0.8f, 0.1f), wood);
            Building(env, "Wang home", "house_anxi_a_front", "house_anxi_side", new Vector3(-10f, 0, 2f), 6f, 3.2f, 5f, house);
            Building(env, "Li home", "house_anxi_b_front", "house_anxi_side", new Vector3(10f, 0, 2f), 7f, 3.2f, 5f, house);
            Billboard(env, "Wang trough", "horse_trough", -10f, 0.6f, new Vector3(2f, 0.8f, 0.8f), wood);
            // Homes filling in, none before a door (V02_HUANGJIN_PLAN §3.1).
            foreach (var (x, z, front) in new[] { (-14f, -18f, "house_anxi_c_front"), (-20f, -10f, "house_anxi_a_front"), (14f, -18f, "house_anxi_b_front"),
                         (22f, -12f, "house_anxi_c_front"), (-18f, 10f, "house_anxi_b_front"), (28f, -20f, "house_anxi_a_front") })
                Building(env, $"Home {x},{z}", front, "house_anxi_side", new Vector3(x, 0, z), 6f, 3f, 5f, house);
            // River west: the bridge (broken until mended), timber upstream, the mortar and wash stones.
            var broken = Ground2(env, "!bridge Bridge broken", "bridge_deck_broken", new Color(0.45f, 0.32f, 0.20f), -32f, -23f, -6.5f, -3.5f);
            var mended = Ground2(env, "bridge Bridge mended", "bridge_deck_fixed", new Color(0.56f, 0.40f, 0.24f), -32f, -23f, -6.5f, -3.5f);
            var gap = new GameObject("!bridge Gap (no crossing)").transform;
            gap.SetParent(env, false);
            Solid(gap, "Collider", new Vector3(-27.5f, 1f, -5f), new Vector3(2f, 2f, 3.2f));
            var timber = Billboard(env, "!wood Timber pile", "timber_pile", -21f, 15f, new Vector3(3f, 1.2f, 1.2f), wood);
            Billboard(env, "Stone mortar", "stone_mortar", -21f, 20f, new Vector3(1.4f, 1.2f, 1f), stone, solid: true);
            Billboard(env, "Wash stone", "wash_stone", -23f, -12f, new Vector3(1.2f, 0.4f, 0.6f), stone);
            foreach (var (x, z) in new[] { (-23f, 6f), (-22f, -16f), (-34f, 18f), (-34f, -8f) })
                Billboard(env, $"Willow {x},{z}", "willow_anxi", x, z, new Vector3(3f, 5.5f, 3f), new Color(0.40f, 0.58f, 0.30f), solid: true);
            foreach (var (x, z) in new[] { (-14f, 20f), (14f, 20f), (30f, 5f), (-6f, -20f) })
                Billboard(env, $"Locust {x},{z}", "locust_tree", x, z, new Vector3(3.5f, 5.5f, 3.5f), new Color(0.36f, 0.52f, 0.28f), solid: true);
            Far(env, "far_taihang_snow", 70f, 170f, 36f, Color.white);

            var p = f.People;
            var people = new List<Transform>
            {
                Person(p, "clerk 县吏", Resident("N03"), new Vector3(0, 0, 15.6f)),
                Person(p, "courier 驿卒", Resident("N07"), new Vector3(22f, 0, 12.6f)),
                Person(p, "old_woman 乞讨老妇", Resident("N08"), new Vector3(-3.6f, 0, -19f)),
                Person(p, "wang 王老汉", Resident("N09"), new Vector3(-4f, 0, -1f)),
                Person(p, "li 李老汉", Resident("N11"), new Vector3(4f, 0, -1f)),
                Person(p, "bridge_elder 桥边老者", Resident("N09"), new Vector3(-21.6f, 0, -3.4f)),
                Person(p, "merchant_a 兵器铺掌柜", Resident("N04"), new Vector3(-9f, 0, -9.6f)),
                Person(p, "merchant_b 杂货铺掌柜", Resident("N05"), new Vector3(9f, 0, -9.6f)),
                Person(p, "townsman_a 县民", Resident("N12"), new Vector3(5f, 0, -15f)),
                Person(p, "townsman_b 县民", Resident("N08"), new Vector3(-6f, 0, 5f)),
                Person(p, "townsman_c 县民", Resident("N11"), new Vector3(16f, 0, -3.4f)),
                Person(p, "townsman_d 县民", Resident("N06"), new Vector3(-16f, 0, 4f)),
                Person(p, "anxi_child 孩童", Resident("N10"), new Vector3(2f, 0, 6f)),
                Person(p, "du_you_aide 督邮随从", Resident("N07"), new Vector3(22f, 0, 16f)),
            };
            var aide = people.Last();
            aide.gameObject.SetActive(false);
            // The inspector himself, for the 鞭督邮 scene (his story poses after his nine, DuyouPoseImporter); hidden until then.
            var duyou = Person(p, "du_you 督邮", Folk("C06"), new Vector3(23f, 0, 13.2f));
            duyou.gameObject.SetActive(false);
            people.Add(duyou);
            var bound = Billboard(p, "du_you_bound 督邮", "DU_YOU_BOUND", 20f, 9f, new Vector3(0.8f, 1.6f, 0.6f), new Color(0.46f, 0.24f, 0.46f));
            bound.gameObject.SetActive(false);
            var ox = Billboard(p, "ox 黄牛", "PROP_OX_WALK_STRIP", 0, -1.2f, new Vector3(1.8f, 1.3f, 0.8f), new Color(0.62f, 0.42f, 0.22f), frames: 4);
            people.Add(ox);
            var c = f.Controller;
            c.Location = FieldStoryController.Place.Anxi;
            c.People = people.ToArray();
            var spots = new List<Transform>
            {
                Spot(f, "east_gate 东门", new Vector3(34f, 0, 0)),
                Spot(f, "timber 木料", new Vector3(-21f, 0, 13.6f)),
                Spot(f, "seal 石碓后", new Vector3(-21f, 0, 21.4f)),
            };
            if (hitchingPost) spots.Add(hitchingPost);
            c.Spots = spots.ToArray();
            c.People = c.People.Append(bound).ToArray();
            c.FlagObjects = new[] { broken, mended, gap, timber };
            c.ArrivalSpot = Spot(f, "Arrival (inside the south gate)", new Vector3(0, 0, -21f));
            c.ReturnSpot = Spot(f, "Return (inside the east gate)", new Vector3(31f, 0, 0));
            Huds(c);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), AnxiPath);
        }

        // A flat piece lying on the ground (a bridge deck), named for FlagObjects.
        static Transform Ground2(Transform parent, string name, string key, Color fallback, float xMin, float xMax, float zMin, float zMax)
        {
            var g = new GameObject(name).transform;
            g.SetParent(parent, false);
            var tex = Tex(key);
            Quad(g, "Deck", new Vector3((xMin + xMax) / 2f, 0.06f, (zMin + zMax) / 2f), new Vector3(90f, 0, 0), new Vector2(xMax - xMin, zMax - zMin),
                Mat(key, tex, fallback, tex), Vector2.one, shadows: false);
            return g;
        }

        // ---------- the story stage ----------

        static void BuildStage()
        {
            dir = $"{ArtRoot}/Stage";
            matDir = $"{ArtRoot}/Materials/Stage";
            mats.Clear();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.86f, 0.88f, 0.96f);
            RenderSettings.ambientEquatorColor = new Color(0.78f, 0.74f, 0.70f);
            RenderSettings.ambientGroundColor = new Color(0.42f, 0.36f, 0.30f);
            var camGo = new GameObject("Stage Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = BattleBackdropBaker.OrthoSize;
            cam.transform.position = new Vector3(0, 0, -10);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            var sun = new GameObject("Key light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(35f, 30f, 0f);
            sun.color = new Color(1f, 0.97f, 0.92f);
            sun.intensity = 2.2f;

            var size = new Vector2(21.34f, 12f);
            var backgrounds = new GameObject("Backgrounds").transform;
            void Picture(string id, Texture2D tex, Color fallback)
            {
                var g = new GameObject(id).transform;
                g.SetParent(backgrounds, false);
                Quad(g, "Picture", new Vector3(0, 0, 30f), Vector3.zero, size, Unlit(id, tex, fallback), Vector2.one, shadows: false);
            }
            Picture("road", Tex("B2_road_scene") ?? AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Battle/ZhuojunOutskirts/preview.png"), new Color(0.56f, 0.66f, 0.42f));
            Picture("tent", Tex("stage_tent_interior"), new Color(0.30f, 0.20f, 0.14f));
            Picture("wancheng", Tex("stage_wancheng"), new Color(0.50f, 0.48f, 0.42f));
            Picture("map", AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtRoot}/Map/worldmap_chapter1_zoom.png"), new Color(0.86f, 0.82f, 0.70f));
            // Without the road painting, the outskirts layers stand in.
            var road = backgrounds.Find("road");
            if (!Tex("B2_road_scene"))
            {
                Object.DestroyImmediate(road.GetChild(0).gameObject);
                string[] layers = { "bg_outskirts_sky", "bg_outskirts_far", "bg_outskirts_mid", "bg_outskirts_ground" };
                for (int i = 0; i < layers.Length; i++)
                {
                    var t = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/_Project/Art/Battle/ZhuojunOutskirts/{layers[i]}.png");
                    if (t) Quad(road, layers[i], new Vector3(0, 0, 40f - i * 7.5f), Vector3.zero, size, Unlit(layers[i], t, Color.gray, i > 0), Vector2.one, shadows: false);
                }
            }

            var actors = new GameObject("Actors").transform;
            void ActorSprite(string name, string key)
            {
                var go = HD2DSampleBuilder.Character(name, key, new Vector3(0, -3f, 10f), 0, isPlayer: false);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(actors, true);
            }
            ActorSprite("LiuBei", "LiuBei");
            ActorSprite("GuanYu", "GuanYu");
            ActorSprite("ZhangFei", "ZhangFei");
            ActorSprite("HanOfficer", Folk("C13"));
            ActorSprite("EscortOfficer", Folk("C12"));
            ActorSprite("PrisonCart", Folk("C01"));   // Lu Zhi in the cart; the cart picture replaces this when delivered
            var cart = Tex("PROP_PRISON_CART_LU_ZHI");
            if (cart)
            {
                var t = actors.Find("PrisonCart");
                var r = t.GetComponentInChildren<MeshRenderer>();
                Object.DestroyImmediate(t.GetComponentInChildren<SpriteFrameAnimator>());
                r.sharedMaterial = Mat("PROP_PRISON_CART_LU_ZHI", cart, Color.white, true);
                r.transform.localScale = new Vector3(3.2f, 1.6f, 1f);
                r.transform.localPosition = new Vector3(0, 0.8f, 0);
            }

            var go2 = new GameObject("Stage");
            var stage = go2.AddComponent<StageController>();
            stage.Catalog = AssetDatabase.LoadAssetAtPath<BattleCatalog>(BattleDataBuilder.CatalogPath);
            stage.Player = go2.AddComponent<StagePlayer>();
            stage.Player.Confirm = ProjectSetup.Action("Interact");
            stage.Camera = cam;
            stage.Backgrounds = backgrounds;
            stage.Actors = actors;

            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            var hudGo = new GameObject("Stage HUD");
            hudGo.AddComponent<UIDocument>().panelSettings = panel;
            var hud = hudGo.AddComponent<StageHud>();
            hud.Screen = stage;
            hud.Skin = AssetDatabase.LoadAssetAtPath<ThreeKingdoms.UI.Battle.BattleUiSkin>(BattleUiSkinBuilder.SkinPath);
            hud.Portraits = AssetDatabase.LoadAssetAtPath<DialoguePortraitSet>(DialoguePortraitImporter.SetPath);
            hud.Map = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtRoot}/Map/worldmap_chapter1_zoom.png");
            CinematicsArtImporter.AddView(panel);
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), StagePath);
        }

        static Material Unlit(string id, Texture2D tex, Color fallback, bool clip = false)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "stage_" + id };
            if (tex) m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", tex ? Color.white : fallback);
            if (clip) { m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON"); m.renderQueue = (int)RenderQueue.AlphaTest; }
            Directory.CreateDirectory(matDir);
            string path = $"{matDir}/{m.name}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
