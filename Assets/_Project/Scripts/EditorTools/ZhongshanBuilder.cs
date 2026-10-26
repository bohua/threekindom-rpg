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
using T = ThreeKingdoms.EditorTools.ZhuojunTownBuilder;

namespace ThreeKingdoms.EditorTools
{
    // Chapter 1 open region R2 中山 (user 2026-10-26: "安喜所在的中山地图" → "做"; one look per region): the Taihang
    // piedmont round Anxi in 185. Built the way the Bohai probe was (BohaiPlainBuilder), from the user-approved Codex
    // layout (art/zhongshan-terrain-mask: a 400 × 240 m class mask, 1 px per metre, and terrain_layout.json, compiled
    // into ZhongshanLayout) and Zhongshan's own high-res ground (art/zhongshan-hd-ground) and structures
    // (art/zhongshan-hd-structures), 120 px per metre; characters stay the 128 px pixel Q style.
    //
    // Metres, origin south-west, x east, z north, camera looks north. The Taihang massif fills the west (x < 90), its
    // foothill belt with terraces and woods runs to x ≈ 200; the Hutuo winds east–west across the south (z 40–90) in a
    // broad pebble bed, crossed only by the ferry; Anxi stands at x 232–324, z 130–193, its south gate open to the
    // region (portal to Anxi_Town), the other three shut while the county is on guard. The Jingxing and Feihu ways west
    // are blocked (the Black Mountain stockade, a collapsed plank road).
    //
    // What makes it Zhongshan and not Bohai (PROJECT.md §5 大地图一区一貌): grey limestone slopes and cypress instead
    // of loess and reeds, dry-stone terraces, a clear blue-green river over pebbles, persimmon and jujube by the
    // villages, the snow range on the western horizon.
    public static class ZhongshanBuilder
    {
        public const string SceneName = "Zhongshan_Region";
        public const string ScenePath = "Assets/_Project/Scenes/" + SceneName + ".unity";
        public const string ArtDir = "Assets/_Project/Art/Environment/Zhongshan";
        public const string HdDir = ArtDir + "/HD";
        const string TerrainDir = ArtDir + "/Terrain";
        const string MinimapDir = ArtDir + "/Minimap";
        const string MaterialDir = ArtDir + "/Materials";
        const string MeshPath = ArtDir + "/Zhongshan_Meshes.asset";
        const string CodexArt = "D:/BuildArtifacts/Codex/hd2d-jrpg/art";
        // Where Liu Bei stands after coming out of Anxi's south gate (named so RegionPortal finds it).
        public const string AnxiSouthArrival = "Arrival outside Anxi south gate";

        public const float XMin = 0f, XMax = 400f, ZMin = 0f, ZMax = 240f;
        const float WallH = 3.5f, WallTk = 2f;
        static readonly Vector3 SouthGateOut = new Vector3(278f, 0, 124f);

        [MenuItem("JRPG/Build Zhongshan Region (R2)")]
        public static void Build()
        {
            ImportArt();
            graded.Clear(); greys.Clear(); forkPieces = null;
            MountedImporter.Import();
            T.HdAliases.Clear();
            T.BeginSession(MaterialDir, MeshPath, HdDir, BohaiPlainBuilder.WildDir, BohaiPlainBuilder.ArtDir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Bright midday like the Bohai plain, a touch cooler and clearer at the mountain foot.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.86f, 0.93f, 1.06f);
            RenderSettings.ambientEquatorColor = new Color(0.78f, 0.80f, 0.78f);
            RenderSettings.ambientGroundColor = new Color(0.44f, 0.42f, 0.36f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.0018f;
            RenderSettings.fogColor = new Color(0.80f, 0.88f, 0.96f);

            if (!LoadMask()) throw new FileNotFoundException("Zhongshan terrain mask missing: " + TerrainDir);
            var env = new GameObject("Environment").transform;
            var labels = new GameObject("Whitebox labels").transform;
            BeginRibbons();
            Ground(env);
            SplatGround(env);
            Roads(env);
            Rivers(env);
            Bridges(env);
            Walls(env);
            Mountains(env);
            Terraces(env);
            Forests(env);
            Anxi(env, labels);
            PostStation(env, labels);
            TerraceVillage(env, labels);
            RiverVillage(env, labels);
            Explore(env, labels);
            Passes(env, labels);
            Exits(env, labels);
            Signposts(env);
            Scatter(env);
            Backdrop(env);
            T.EndSession();

            var sun = new GameObject("Midday sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.98f, 0.94f);
            sun.intensity = 2.3f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.65f;
            sun.transform.rotation = Quaternion.Euler(56f, -24f, 0f);

            Locations();
            var people = new GameObject("People").transform;
            var player = HD2DSampleBuilder.Character("Liu Bei", "LiuBei", SouthGateOut, 0, isPlayer: true);
            player.transform.SetParent(people, true);
            var trail = player.AddComponent<PartyTrail>();
            trail.Length = 24f;
            int slot = 1;
            var companions = new List<MountRider>();
            foreach (var (name, key) in new[] { ("Guan Yu (follower)", "GuanYu"), ("Zhang Fei (follower)", "ZhangFei") })
            {
                var go = HD2DSampleBuilder.Character(name, key, SouthGateOut + new Vector3(0, 0, 1.5f * slot), 0, isPlayer: false);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.AddComponent<PixelWalkFrames>().Animator = go.GetComponentInChildren<SpriteFrameAnimator>();
                var f = go.AddComponent<PartyFollower>();
                f.Trail = trail; f.Slot = slot++;
                go.transform.SetParent(people, true);
                companions.Add(AddRider(go, key));
            }
            var ride = player.AddComponent<RideController>();
            ride.Mover = player.GetComponent<PlayerMover>();
            ride.Leader = AddRider(player, "LiuBei");
            ride.Companions = companions.ToArray();
            People(people);

            Camera(player.transform);
            Hud(player.transform);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("ZHONGSHAN_REGION_BUILT");
        }

        public static void BuildFromCommandLine()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        // ---------- art ----------

        // Anxi's own high-res pieces (art/v02-anxi-town-hd, v02-roofs-hd) so its walls and roofs read the same from the
        // region as inside the town.
        static readonly string[] AnxiPieces =
        {
            "wall_anxi_rammed", "wall_anxi_coping", "gate_anxi_south", "gate_anxi_east", "house_anxi_a_front", "house_anxi_b_front", "house_anxi_c_front",
            "house_anxi_side", "yamen_front", "yamen_side", "yiguan_front", "yiguan_side", "locust_tree", "willow_anxi", "far_taihang_snow",
            "bridge_rail", "hitching_post",
        };

        static void ImportArt()
        {
            Directory.CreateDirectory(HdDir); Directory.CreateDirectory(TerrainDir); Directory.CreateDirectory(MinimapDir);
            var axes = new Dictionary<string, string>();
            void Copy(string src, string dst)
            {
                if (!File.Exists(src)) return;
                if (!File.Exists(dst) || new FileInfo(dst).Length != new FileInfo(src).Length) File.Copy(src, dst, true);
            }
            void Folder(string name, System.Func<string, bool> keep = null)
            {
                string dir = $"{CodexArt}/{name}";
                if (!Directory.Exists(dir)) return;
                foreach (var f in Directory.GetFiles(dir, "*.png"))
                {
                    string key = Path.GetFileNameWithoutExtension(f);
                    if (key == "preview" || key.StartsWith("contact") || (keep != null && !keep(key))) continue;
                    Copy(f, $"{HdDir}/{key}.png");
                }
                string manifest = $"{dir}/manifest.json";
                if (!File.Exists(manifest)) return;
                var m = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifest));
                if (m.files == null) return;
                foreach (var e in m.files) if (!string.IsNullOrEmpty(e.filename)) axes[Path.GetFileNameWithoutExtension(e.filename)] = e.tile_axes ?? "none";
            }
            Folder("zhongshan-hd-ground");
            Folder("zhongshan-hd-structures");
            Folder("zhongshan-road-fork-set");
            Folder("v02-anxi-town-hd", k => AnxiPieces.Contains(k));
            Folder("v02-roofs-hd", k => k.StartsWith("roof_anxi"));
            foreach (var k in new[] { "wall_anxi_rammed", "wall_anxi_coping", "bridge_rail" }) axes[k] = "x";
            foreach (var k in new[] { "roof_anxi_tiles" }) axes[k] = "xy";
            foreach (var k in new[] { "roof_anxi_ridge", "roof_anxi_eave" }) axes[k] = "x";
            File.WriteAllText($"{HdDir}/manifest.json", JsonUtility.ToJson(new Manifest
            {
                files = axes.OrderBy(a => a.Key).Select(a => new ManifestFile { filename = a.Key + ".png", tile_axes = a.Value }).ToArray(),
            }, true));
            Copy($"{CodexArt}/zhongshan-terrain-mask/ZHONGSHAN_TERRAIN_MASK.png", $"{TerrainDir}/zhongshan_terrain_mask.png");
            Copy($"{CodexArt}/zhongshan-concept/ZHONGSHAN_TERRAIN_MAP.png", $"{MinimapDir}/zhongshan_region_map.png");
            Copy($"{CodexArt}/zhongshan-concept/ZHONGSHAN_TERRAIN_MAP_LABELED.png", $"{MinimapDir}/zhongshan_region_map_labeled.png");
            AssetDatabase.Refresh();
        }

        [System.Serializable] sealed class ManifestFile { public string filename; public string tile_axes; }
        [System.Serializable] sealed class Manifest { public ManifestFile[] files; }

        static bool Has(string key) => T.Tex(key, optional: true);

        // A copy of a shared texture material with Zhongshan's own grade (the painted sets come out pale under the
        // midday light; the approved concept has warm earth roads, grey-brown limestone and deep blue-green water).
        static readonly Dictionary<string, Material> graded = new Dictionary<string, Material>();

        static Material Graded(string key, Color tint, bool clip = false, float smoothness = 0f, bool twoSided = false)
        {
            string id = $"zs_{key}{(clip ? "_clip" : "")}{(twoSided ? "_2s" : "")}";
            if (graded.TryGetValue(id, out var m) && m) return m;
            m = new Material(T.Mat(key, clip: clip, twoSided: twoSided)) { name = id };
            m.SetColor("_BaseColor", tint);
            if (smoothness > 0f)
            {
                m.SetFloat("_Smoothness", smoothness);
                m.SetFloat("_SpecularHighlights", 1f); m.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
                m.SetFloat("_EnvironmentReflections", 1f); m.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            }
            string path = $"{MaterialDir}/{id}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            return graded[id] = m;
        }

        static readonly Color RoadTint = new Color(0.90f, 0.80f, 0.66f), HillTint = new Color(0.80f, 0.78f, 0.72f), WaterTint = new Color(0.50f, 0.66f, 0.74f);
        static string K(string key, string fallback) => Has(key) ? key : fallback;
        static string K(string key, string a, string b) => Has(key) ? key : Has(a) ? a : b;

        // ---------- helpers ----------

        static readonly Dictionary<float, Material> greys = new Dictionary<float, Material>();

        static Material Grey(float shade)
        {
            if (greys.TryGetValue(shade, out var m) && m) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = $"zs_grey_{shade:0.00}" };
            float v = Mathf.Lerp(0.35f, 0.82f, shade);
            m.SetColor("_BaseColor", new Color(v, v * 0.99f, v * 0.97f));
            m.SetFloat("_Smoothness", 0f);
            Directory.CreateDirectory(MaterialDir);
            string path = $"{MaterialDir}/{m.name}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            return greys[shade] = m;
        }

        static GameObject Box(Transform parent, string name, Vector3 baseCentre, Vector3 size, float shade = 0.6f, float yaw = 0f, bool solid = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = baseCentre + new Vector3(0, size.y / 2f, 0);
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = Grey(shade);
            if (size.y > 3f) go.AddComponent<Occluder>();
            return go;
        }

        static void Label(Transform labels, string text, Vector3 at, float height = 8f)
        {
            var go = new GameObject("Label " + text);
            go.transform.SetParent(labels, false);
            go.transform.position = at;
            var l = go.AddComponent<WhiteboxLabel>();
            l.Text = text; l.Height = height;
        }

        static Transform Spot(string name, Vector3 at)
        {
            var go = new GameObject(name.StartsWith("Arrival") ? name : "Spot " + name);
            go.transform.position = at;
            return go.transform;
        }

        static void Solid(Transform parent, Vector3 centre, Vector3 size, float yaw = 0f)
        {
            var go = new GameObject("Collider");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            go.AddComponent<BoxCollider>().size = size;
        }

        static Vector3 Loc(string id) => ZhongshanLayout.Locations.First(l => l.id == id).at;

        // ---------- the class mask ----------

        enum Cell : byte { Plain, Road, Mountain, Foothill, Forest, River, Bridge, Town, Gravel, Terrace }

        static readonly (Color32 c, Cell cell)[] MaskColours =
        {
            (new Color32(0x7F, 0xA0, 0x5A, 255), Cell.Plain), (new Color32(0xC8, 0xA0, 0x60, 255), Cell.Road),
            (new Color32(0x6E, 0x5A, 0x46, 255), Cell.Mountain), (new Color32(0x8C, 0x7A, 0x5E, 255), Cell.Foothill),
            (new Color32(0x2E, 0x5A, 0x32, 255), Cell.Forest), (new Color32(0x3C, 0x78, 0xA0, 255), Cell.River),
            (new Color32(0xD2, 0xB4, 0x8C, 255), Cell.Bridge), (new Color32(0xA0, 0x8C, 0x78, 255), Cell.Town),
            (new Color32(0x4A, 0x6E, 0x5A, 255), Cell.Gravel), (new Color32(0xA8, 0xB0, 0x60, 255), Cell.Terrace),
        };

        static Cell[,] mask;   // [x, z], 1 m cells from (XMin, ZMin)
        static int W => mask.GetLength(0);
        static int H => mask.GetLength(1);

        static bool LoadMask()
        {
            mask = null;
            string path = TerrainDir + "/zhongshan_terrain_mask.png";
            if (!File.Exists(path)) return false;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(path));
            int w = tex.width, h = tex.height;
            var px = tex.GetPixels32();
            Object.DestroyImmediate(tex);
            mask = new Cell[w, h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var p = px[y * w + x];   // LoadImage rows run bottom-up: y counts northward from ZMin
                    var cell = Cell.Plain; int best = int.MaxValue;
                    foreach (var (c, k) in MaskColours)
                    {
                        int d = Mathf.Abs(c.r - p.r) + Mathf.Abs(c.g - p.g) + Mathf.Abs(c.b - p.b);
                        if (d < best) { best = d; cell = k; }
                    }
                    mask[x, y] = cell;
                }
            // Room round the places (open space before landmarks, user 2026-10-23): the layout's clearings are only
            // a few metres across, too small for the post station's yard or a village, so slopes and woods give way here.
            foreach (var (cx, cz, rx, rz) in Clearings)
                for (int z = Mathf.Max(0, (int)(cz - rz)); z < Mathf.Min(h, (int)(cz + rz) + 1); z++)
                    for (int x = Mathf.Max(0, (int)(cx - rx)); x < Mathf.Min(w, (int)(cx + rx) + 1); x++)
                    {
                        float dx = (x + 0.5f - cx) / rx, dz = (z + 0.5f - cz) / rz;
                        if (dx * dx + dz * dz <= 1f && (Hill(mask[x, z]) || mask[x, z] == Cell.Forest)) mask[x, z] = Cell.Plain;
                    }
            // Every country house's plot and apron is flat ground (user 2026-10-26: a house sank into the slope): no
            // slope, wood or terrace riser within a few metres of its walls.
            foreach (var (front, fk, sk) in HousePlots())
            {
                float hw = T.Metres(fk).x / 2f + 6f, d = T.Metres(sk).x;
                for (int z = Mathf.Max(0, (int)(front.z - 6f)); z < Mathf.Min(h, (int)(front.z + d + 6f) + 1); z++)
                    for (int x = Mathf.Max(0, (int)(front.x - hw)); x < Mathf.Min(w, (int)(front.x + hw) + 1); x++)
                        if (Hill(mask[x, z]) || mask[x, z] == Cell.Forest || mask[x, z] == Cell.Terrace) mask[x, z] = Cell.Plain;
            }
            return true;
        }

        static readonly (float x, float z, float rx, float rz)[] Clearings =
        {
            (140f, 138f, 16f, 13f),   // 井陉路口驿站: yard south, the station north of it
            (148f, 177f, 18f, 13f),   // 石坎村
            (106f, 76f, 8f, 7f), (99f, 203f, 8f, 7f),   // the two guard posts
            (94f, 171f, 6f, 5f),      // 石窟小庙 (the cliff behind stays)
            (151f, 230f, 9f, 8f),     // 古烽燧
        };

        static Cell At(float x, float z)
        {
            int ix = Mathf.FloorToInt(x - XMin), iz = Mathf.FloorToInt(z - ZMin);
            if (ix < 0 || iz < 0 || ix >= W || iz >= H) return Cell.Plain;
            return mask[ix, iz];
        }

        static bool Hill(Cell c) => c == Cell.Mountain || c == Cell.Foothill;
        static bool Blocked(Cell c) => Hill(c) || c == Cell.Forest || c == Cell.River;

        static bool Inside(int x, int z, int r, System.Func<Cell, bool> kind)
        {
            for (int dz = -r; dz <= r; dz += Mathf.Max(1, r)) for (int dx = -r; dx <= r; dx += Mathf.Max(1, r))
                if (!kind(mask[Mathf.Clamp(x + dx, 0, W - 1), Mathf.Clamp(z + dz, 0, H - 1)])) return false;
            return true;
        }

        // Rectangles covering every wanted cell: runs along x, merged down while a run repeats.
        static List<RectInt> Rects(System.Func<int, int, bool> want)
        {
            var open = new Dictionary<(int, int), RectInt>();
            var done = new List<RectInt>();
            for (int z = 0; z < H; z++)
            {
                var runs = new List<(int, int)>();
                for (int x = 0; x < W;)
                {
                    if (!want(x, z)) { x++; continue; }
                    int x0 = x; while (x < W && want(x, z)) x++;
                    runs.Add((x0, x));
                }
                var next = new Dictionary<(int, int), RectInt>();
                foreach (var run in runs)
                {
                    if (open.TryGetValue(run, out var r)) { r.height += 1; next[run] = r; open.Remove(run); }
                    else next[run] = new RectInt(run.Item1, z, run.Item2 - run.Item1, 1);
                }
                done.AddRange(open.Values);
                open = next;
            }
            done.AddRange(open.Values);
            return done;
        }

        // 0..1 share of matching cells within `radius`, by two box-blur passes.
        static float[,] Blurred(System.Func<Cell, bool> match, int radius)
        {
            var a = new float[W, H]; var b = new float[W, H];
            for (int z = 0; z < H; z++) for (int x = 0; x < W; x++) a[x, z] = match(mask[x, z]) ? 1f : 0f;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int z = 0; z < H; z++)
                {
                    float sum = 0f; int n = 0;
                    for (int x = -radius; x < W + radius; x++)
                    {
                        int add = x + radius, rem = x - radius - 1;
                        if (add >= 0 && add < W) { sum += a[add, z]; n++; }
                        if (rem >= 0 && rem < W) { sum -= a[rem, z]; n--; }
                        if (x >= 0 && x < W) b[x, z] = n > 0 ? sum / n : 0f;
                    }
                }
                for (int x = 0; x < W; x++)
                {
                    float sum = 0f; int n = 0;
                    for (int z = -radius; z < H + radius; z++)
                    {
                        int add = z + radius, rem = z - radius - 1;
                        if (add >= 0 && add < H) { sum += b[x, add]; n++; }
                        if (rem >= 0 && rem < H) { sum -= b[x, rem]; n--; }
                        if (z >= 0 && z < H) a[x, z] = n > 0 ? sum / n : 0f;
                    }
                }
            }
            return a;
        }

        // ---------- ground ----------

        static void Ground(Transform env)
        {
            var g = T.Group(env, "Ground");
            var floor = new GameObject("Ground collider").AddComponent<BoxCollider>();
            floor.transform.SetParent(g, false);
            floor.center = new Vector3((XMin + XMax) / 2f, -0.05f, (ZMin + ZMax) / 2f); floor.size = new Vector3(XMax - XMin + 200f, 0.1f, ZMax - ZMin + 200f);
            // Region bounds; the three exits are signposted (world map, not built).
            Solid(g, new Vector3(XMin - 0.5f, 3f, (ZMin + ZMax) / 2f), new Vector3(1f, 6f, ZMax - ZMin + 20f));
            Solid(g, new Vector3(XMax + 0.5f, 3f, (ZMin + ZMax) / 2f), new Vector3(1f, 6f, ZMax - ZMin + 20f));
            Solid(g, new Vector3((XMin + XMax) / 2f, 3f, ZMax + 0.5f), new Vector3(XMax - XMin + 20f, 6f, 1f));
            Solid(g, new Vector3((XMin + XMax) / 2f, 3f, ZMin - 0.5f), new Vector3(XMax - XMin + 20f, 6f, 1f));
            // Grass beyond the edges, so the camera never looks past the region onto nothing.
            foreach (var (x0, x1, z0, z1) in new[] { (XMin - 160f, XMin, ZMin - 160f, ZMax + 160f), (XMax, XMax + 160f, ZMin - 160f, ZMax + 160f),
                         (XMin, XMax, ZMax, ZMax + 160f), (XMin, XMax, ZMin - 160f, ZMin) })
                T.Patch(g, "Outer grass", "plain_grass_hd_b", x0, x1, z0, z1, -0.012f);
        }

        // One ground quad drawn by ThreeKingdoms/GroundSplat: the green piedmont grass, a second grass in drifts, dry
        // grass and bare earth toward the hills, the pebble bed along the Hutuo, the terrace fields; every border broken
        // up by the shader, so the ground changes through transitions, never squares (user 2026-10-25).
        static void SplatGround(Transform env)
        {
            var shader = Shader.Find("ThreeKingdoms/GroundSplat");
            if (!shader) throw new System.InvalidOperationException("GroundSplat shader missing");
            var hillNear = Blurred(Hill, 10);
            var gravel = Blurred(c => c == Cell.Gravel || c == Cell.River, 4);
            var terrace = Blurred(c => c == Cell.Terrace, 2);
            var town = Blurred(c => c == Cell.Town, 3);
            var px = new Color32[W * H];
            byte B(float v) => (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);
            for (int z = 0; z < H; z++)
                for (int x = 0; x < W; x++)
                {
                    float wx = XMin + x + 0.5f, wz = ZMin + z + 0.5f;
                    float second = Mathf.PerlinNoise(wx * 0.015f + 5f, wz * 0.015f + 9f);
                    float dry = Mathf.PerlinNoise(wx * 0.009f + 11f, wz * 0.009f + 3f) * 0.7f + Mathf.PerlinNoise(wx * 0.04f, wz * 0.04f) * 0.3f;
                    dry = Mathf.Max(dry, 0.45f + hillNear[x, z] * 0.45f, 0.45f + town[x, z] * 0.3f);
                    float g = gravel[x, z] * (0.85f + 0.3f * Mathf.PerlinNoise(wx * 0.08f, wz * 0.08f));
                    // No fields under the villages and yards: the clearings fade the terraces out over a few metres.
                    float field = terrace[x, z];
                    foreach (var (cx, cz, rx, rz) in Clearings)
                    {
                        float dx = (wx - cx) / rx, dz = (wz - cz) / rz;
                        field *= Mathf.SmoothStep(0f, 1f, (Mathf.Sqrt(dx * dx + dz * dz) - 0.7f) / 0.5f);
                    }
                    px[z * W + x] = new Color32(B((second - 0.5f) / 0.15f), B((dry - 0.52f) / 0.12f), B((g - 0.3f) / 0.35f), B((field - 0.35f) / 0.3f));
                }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false, linear: true);
            tex.SetPixels32(px); tex.Apply();
            string path = TerrainDir + "/zhongshan_ground_control.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.sRGBTexture = false; imp.mipmapEnabled = false; imp.npotScale = TextureImporterNPOTScale.None;
            imp.textureCompression = TextureImporterCompression.Uncompressed; imp.filterMode = FilterMode.Bilinear; imp.wrapMode = TextureWrapMode.Clamp;
            imp.alphaIsTransparency = false;
            imp.SaveAndReimport();

            var m = new Material(shader) { name = "zhongshan_ground_splat" };
            m.SetTexture("_Control", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            m.SetTexture("_Base", T.Tex("plain_grass_hd_a")); m.SetTexture("_LayerR", T.Tex("plain_grass_hd_b")); m.SetTexture("_LayerG", T.Tex("plain_grass_dry_hd"));
            m.SetTexture("_LayerB", T.Tex("pebble_flat_hd")); m.SetTexture("_LayerA", T.Tex("farm_field_hd"));
            m.SetFloat("_TileMetres", T.Metres("plain_grass_hd_a").x);
            m.SetFloat("_Smooth", 1f);
            m.SetVector("_Bounds", new Vector4(XMin, ZMin, XMax, ZMax));
            m.SetColor("_Tint", new Color(0.93f, 0.93f, 0.88f));
            string mp = $"{MaterialDir}/zhongshan_ground_splat.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(mp)) AssetDatabase.DeleteAsset(mp);
            AssetDatabase.CreateAsset(m, mp);
            T.Quad(env, "Splat ground", new Vector3((XMin + XMax) / 2f, -0.009f, (ZMin + ZMax) / 2f), new Vector3(90f, 0, 0), new Vector2(XMax - XMin, ZMax - ZMin), m, castShadows: false);
        }

        // ---------- ribbons (roads, rivers, bands) ----------

        static Mesh ribbonContainer;
        static int ribbonCount;
        static readonly List<Vector3> junctions = new List<Vector3>();

        static void BeginRibbons()
        {
            Directory.CreateDirectory(TerrainDir);
            string path = TerrainDir + "/Zhongshan_Ribbons.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path)) AssetDatabase.DeleteAsset(path);
            ribbonContainer = new Mesh { name = "Zhongshan_Ribbons" };
            AssetDatabase.CreateAsset(ribbonContainer, path);
            junctions.Clear();
            ribbonCount = 0;
        }

        // A strip along pts from offset inner[i] to outer[i] metres right of the centre line (negative = left). u runs
        // across 0..1 and v along by tileLength, or the other way round (vAcross: bands whose solid side is v = 0).
        static GameObject Band(Transform parent, string name, Material mat, IList<Vector3> pts, IList<float> inner, IList<float> outer, float y, float tileLength, bool vAcross = false, bool planar = false,
            IList<float> u0 = null, IList<float> u1 = null)
        {
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            float dist = 0f;
            for (int i = 0; i < pts.Count; i++)
            {
                var tan = pts[Mathf.Min(pts.Count - 1, i + 1)] - pts[Mathf.Max(0, i - 1)]; tan.y = 0f; tan.Normalize();
                var right = new Vector3(tan.z, 0, -tan.x);
                if (i > 0) { var d = pts[i] - pts[i - 1]; d.y = 0f; dist += d.magnitude; }
                verts.Add(pts[i] + right * inner[i] + Vector3.up * y);
                verts.Add(pts[i] + right * outer[i] + Vector3.up * y);
                float along = dist / tileLength;
                if (planar) { foreach (var v in verts.Skip(verts.Count - 2)) uvs.Add(new Vector2(v.x, v.z) / tileLength); }
                else if (vAcross) { uvs.Add(new Vector2(along, 0f)); uvs.Add(new Vector2(along, 1f)); }
                else { uvs.Add(new Vector2(u0 != null ? u0[i] : 0f, along)); uvs.Add(new Vector2(u1 != null ? u1[i] : 1f, along)); }
            }
            for (int i = 0; i + 1 < pts.Count; i++) { int a = i * 2; tris.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 }); }
            var mesh = new Mesh { name = $"{name} {ribbonCount++}", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            if (mesh.normals.Length > 0 && mesh.normals[0].y < 0f) { tris.Reverse(); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); }
            mesh.RecalculateBounds();
            AssetDatabase.AddObjectToAsset(mesh, ribbonContainer);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = Vector3.zero;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        static float[] Fill(int n, float v) => Enumerable.Repeat(v, n).ToArray();

        // Catmull-Rom through the points about every `step` metres.
        static List<Vector3> Smooth(IList<Vector3> pts, float step, IList<float> values, out List<float> outValues)
        {
            var o = new List<Vector3>(); outValues = new List<float>();
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                Vector3 p0 = pts[Mathf.Max(0, i - 1)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Mathf.Min(pts.Count - 1, i + 2)];
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(p1, p2) / step));
                for (int k = 0; k < n; k++)
                {
                    float t = k / (float)n, t2 = t * t, t3 = t2 * t;
                    o.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                    outValues.Add(values != null ? Mathf.Lerp(values[i], values[i + 1], t) : 1f);
                }
            }
            o.Add(pts[pts.Count - 1]); outValues.Add(values != null ? values[values.Count - 1] : 1f);
            return o;
        }

        // ---------- roads ----------

        // Inside the walls the town scene has its own streets; the region draws only what lies outside.
        static bool InTown(string id) => id.StartsWith("town_") || id == "magistrate_path" || id == "inn_path";

        // Layout roads left out: the west-fork shortcut to the terraces crossed the post station's plot, and the road
        // from the station north already reaches the same fork.
        static bool Dropped(string id) => id == "west_fork_terraces";

        static bool NearTown(Vector3 p, float margin) =>
            p.x > ZhongshanLayout.TownX0 - margin && p.x < ZhongshanLayout.TownX1 + margin && p.z > ZhongshanLayout.TownZ0 - margin && p.z < ZhongshanLayout.TownZ1 + margin;

        // Round the walls to the three shut gates (the county is on guard: only the south gate opens).
        static readonly (string id, float width, string surface, Vector3[] points)[] Links =
        {
            ("link_southwest", 6f, "earth", new[] { new Vector3(277.5f, 0, 112f), new Vector3(260f, 0, 116f), new Vector3(238f, 0, 125f), new Vector3(224f, 0, 136f), new Vector3(217f, 0, 150f), new Vector3(217f, 0, 158f) }),
            ("link_southeast", 6f, "earth", new[] { new Vector3(278.5f, 0, 112f), new Vector3(298f, 0, 116f), new Vector3(320f, 0, 125f), new Vector3(331f, 0, 138f), new Vector3(334f, 0, 152f), new Vector3(338f, 0, 161f) }),
            ("link_north", 6f, "earth", new[] { new Vector3(338f, 0, 161f), new Vector3(336f, 0, 182f), new Vector3(322f, 0, 197f), new Vector3(298f, 0, 199f), new Vector3(280f, 0, 199f) }),
        };

        static IEnumerable<(string id, float width, string surface, Vector3[] points)> AllRoads =>
            ZhongshanLayout.Roads.Where(r => !InTown(r.id) && !Dropped(r.id)).Concat(Links);

        static bool NearOtherRoad(Vector3 p, Vector3[] self, float r)
        {
            var q = new Vector2(p.x, p.z);
            foreach (var road in AllRoads)
            {
                if (road.points == self) continue;
                var pts = road.points;
                for (int i = 0; i + 1 < pts.Length; i++)
                {
                    var a = new Vector2(pts[i].x, pts[i].z); var b = new Vector2(pts[i + 1].x, pts[i + 1].z);
                    float t = Mathf.Clamp01(Vector2.Dot(q - a, b - a) / Mathf.Max(0.001f, (b - a).sqrMagnitude));
                    if ((a + t * (b - a) - q).magnitude < r) return true;
                }
            }
            return false;
        }

        // An end left in the open (a village, a wild spot, a pass): no other road there, not at a gate or an edge.
        static bool OpenEnd(Vector3 p, Vector3[] self) =>
            !NearOtherRoad(p, self, 5f) && !NearTown(p, 4f) && p.x > XMin + 2f && p.x < XMax - 2f && p.z > ZMin + 2f && p.z < ZMax - 2f;

        static float DistAlong(IList<Vector3> pts, int upTo)
        {
            float d = 0f; for (int i = 1; i <= upTo; i++) d += Vector3.Distance(pts[i], pts[i - 1]); return d;
        }

        // Roads are drawn after the forks are worked out (user 2026-10-26: ruts must meet exactly). Every road is cut
        // where a fork piece lies over it and drawn up to the piece's edges; a side road starts at the piece's outlet.
        // The rut texture keeps one scale on every road (u in metres across, 8 m per tile), so ruts sit the same
        // distance from the centre whatever the road's width and line up with the pieces' ruts.
        static void Roads(Transform env)
        {
            var g = T.Group(env, "Roads");
            // The fork set's standard strip (its grooves run dead straight at fixed offsets), falling back to the
            // first painted road.
            string key = K("dirt_road_std_hd", "dirt_road_hd"), edge = "road_edge_hd";
            var roads = AllRoads.ToList();
            var forks = new List<ForkPlan>();
            var lines = new Dictionary<Vector3[], List<Vector3>>();
            foreach (var road in roads) lines[road.points] = VillageCut(road.points).ToList();
            // 1 Plan every fork from the layout lines (each road end that meets another road part-way, or the odd one
            // of three roads meeting at a node).
            foreach (var road in roads)
            {
                var line = lines[road.points];
                if (line.Count < 2) continue;
                foreach (bool atStart in new[] { true, false })
                {
                    var end = atStart ? line[0] : line[line.Count - 1];
                    if (OpenEnd(end, road.points)) continue;
                    var plan = PlanFork(line, road.points, atStart);
                    if (plan != null) forks.Add(plan);
                }
            }
            // 2 Lay the pieces; side roads start from their outlets.
            foreach (var f in forks)
            {
                var piece = f.Piece;
                float yaw = Mathf.Atan2(f.Tangent.x, f.Tangent.z) * Mathf.Rad2Deg;
                var q = T.Quad(g, "Fork " + piece.Key, f.Local(piece.W / 2f - piece.MainX, piece.L / 2f) + Vector3.up * 0.0064f, new Vector3(90f, yaw, 0),
                    new Vector2(piece.W, piece.L), Graded(piece.Key, RoadTint, clip: true, twoSided: true), castShadows: false);
                if (f.Side < 0f) q.transform.localScale = new Vector3(-1f, 1f, 1f);
                // The side road, running away from the junction: drop what lies on the piece or just past its outlet.
                var away = new List<Vector3>(lines[f.Side_Road]);
                if (!f.AtStart) away.Reverse();
                int from = away.FindIndex(p => !f.Covers(p, 3f) && Vector3.Distance(p, f.Outlet) > 6f);
                var head = new List<Vector3> { f.Outlet, f.Outlet + f.OutletDir * 5f };
                if (from >= 0) head.AddRange(away.Skip(from));
                if (!f.AtStart) head.Reverse();
                lines[f.Side_Road] = head;
            }
            // 3 Draw every road in the pieces between the forks.
            foreach (var road in roads)
            {
                var line = lines[road.points];
                if (line.Count < 2) continue;
                var parts = new List<(List<Vector3> pts, bool join0, bool join1)> { (Dense(line), false, false) };
                // This road's own outlet end joins a piece.
                var mine = forks.Where(f => f.Side_Road == road.points).ToList();
                if (mine.Any(f => f.AtStart)) parts[0] = (parts[0].pts, true, parts[0].join1);
                if (mine.Any(f => !f.AtStart)) parts[0] = (parts[0].pts, parts[0].join0, true);
                foreach (var f in forks.Where(f => f.Side_Road != road.points))
                    parts = parts.SelectMany(p => f.Cut(p.pts, p.join0, p.join1)).ToList();
                int n = 0;
                foreach (var (dense, j0, j1) in parts)
                {
                    if (dense.Count < 2) continue;
                    // The rut pattern is not symmetric about the centre line: a part meeting a piece takes the piece's
                    // handedness (mirrored pieces mirror it), so every groove continues into the piece's.
                    bool flip = false;
                    var need = j0 ? RequiredRight(forks, dense[0]) : j1 ? RequiredRight(forks, dense[dense.Count - 1]) : null;
                    if (need.HasValue)
                    {
                        int a = j0 ? 0 : dense.Count - 2;
                        var dir = dense[a + 1] - dense[a]; dir.y = 0f;
                        flip = Vector3.Dot(new Vector3(dir.z, 0, -dir.x), need.Value) < 0f;
                    }
                    DrawRoad(T.Group(g, $"Road {road.id} {n++}"), key, edge, dense, road.width, road.points, j0, j1, flip,
                        j0 ? JoinWidth(forks, dense[0]) : 0f, j1 ? JoinWidth(forks, dense[dense.Count - 1]) : 0f);
                }
            }
        }

        // The direction the rut texture's +u must point where a road meets a fork piece at p (its bottom or top edge, or
        // its outlet), or null when p is not at a piece.
        static Vector3? RequiredRight(List<ForkPlan> forks, Vector3 p)
        {
            foreach (var f in forks)
            {
                if (Vector3.Distance(p, f.Origin) < 1.5f || Vector3.Distance(p, f.Origin + f.Tangent * f.Piece.L) < 1.5f) return f.Right * f.Side;
                if (Vector3.Distance(p, f.Outlet) < 1.5f) return new Vector3(f.OutletDir.z, 0, -f.OutletDir.x) * f.Side;
            }
            return null;
        }

        static float JoinWidth(List<ForkPlan> forks, Vector3 p) =>
            forks.Any(f => Vector3.Distance(p, f.Outlet) < 1.5f) ? 6f : forks.Any(f => Vector3.Distance(p, f.Origin) < 1.5f || Vector3.Distance(p, f.Origin + f.Tangent * f.Piece.L) < 1.5f) ? ForkPiece.MainWidth : 0f;

        static void DrawRoad(Transform rg, string key, string edge, List<Vector3> raw, float width, Vector3[] self, bool join0, bool join1, bool flip = false, float joinW0 = 8f, float joinW1 = 8f)
        {
            bool o0 = !join0 && OpenEnd(raw[0], self), o1 = !join1 && OpenEnd(raw[raw.Count - 1], self);
            var pts = Smooth(raw, 2f, null, out _);
            float total = DistAlong(pts, pts.Count - 1), half = width / 2f;
            var k = new float[pts.Count];
            // The road meets a piece at the piece's road width there (8 m main road, 6 m outlet).
            float at0 = (joinW0 > 0f ? joinW0 : width) / width, at1 = (joinW1 > 0f ? joinW1 : width) / width;
            for (int i = 0; i < pts.Count; i++)
            {
                float at = DistAlong(pts, i), f = 1f;
                if (o0) f = Mathf.Min(f, Mathf.Lerp(0.35f, 1f, Mathf.SmoothStep(0f, 1f, at / 6f)));
                if (o1) f = Mathf.Min(f, Mathf.Lerp(0.35f, 1f, Mathf.SmoothStep(0f, 1f, (total - at) / 6f)));
                if (join0) f = Mathf.Lerp(at0, f, Mathf.SmoothStep(0f, 1f, at / 7f));
                if (join1) f = Mathf.Lerp(at1, f, Mathf.SmoothStep(0f, 1f, (total - at) / 7f));
                // Other mouths (end-to-end joins) flare a little, like worn ground.
                if (!o0 && !join0 && !NearTown(raw[0], 4f)) f = Mathf.Max(f, Mathf.Lerp(1.4f, 1f, Mathf.SmoothStep(0f, 1f, (at - 3f) / 8f)));
                if (!o1 && !join1 && !NearTown(raw[raw.Count - 1], 4f)) f = Mathf.Max(f, Mathf.Lerp(1.4f, 1f, Mathf.SmoothStep(0f, 1f, (total - at - 3f) / 8f)));
                k[i] = f;
            }
            (pts, k) = RoundEnds(pts, k, o0, o1, half);
            var tile = T.Metres(key);
            float y = 0.0050f + (ribbonCount % 40) * 0.00002f, ye = 0.0034f + (ribbonCount % 40) * 0.000005f;
            // u in metres: 0.5 is the centre line, the tile is tile.x metres across.
            Band(rg, "Road", Graded(key, RoadTint), pts, k.Select(f => -half * f).ToList(), k.Select(f => half * f).ToList(), y, tile.y,
                u0: k.Select(f => 0.5f - (flip ? -1f : 1f) * half * f / tile.x).ToList(), u1: k.Select(f => 0.5f + (flip ? -1f : 1f) * half * f / tile.x).ToList());
            float dep = T.Metres(edge).y;
            Band(rg, "Edge R", Graded(edge, RoadTint, clip: true), pts, k.Select(f => half * f - 0.5f).ToList(), k.Select(f => half * f - 0.5f + dep).ToList(), ye, T.Metres(edge).x, vAcross: true);
            Band(rg, "Edge L", Graded(edge, RoadTint, clip: true), pts, k.Select(f => -(half * f - 0.5f)).ToList(), k.Select(f => -(half * f - 0.5f + dep)).ToList(), ye, T.Metres(edge).x, vAcross: true);
        }

        // The road's line about every metre (the cuts work on it).
        static List<Vector3> Dense(List<Vector3> line) => Smooth(line, 1f, null, out _);

        // ---------- forks (user 2026-10-26: a side road's ruts branch out of the main road's like a railway switch, and
        // every rut meets exactly — pick the right pattern from a pre-made set) ----------
        // Codex art/zhongshan-road-fork-set: five 22 × 22 m pieces, the main road 8 m wide up the piece with its centre
        // 6 m from the left edge, the branch leaving its ruts 6 m up and running out at 25°, 40°, 55°, 70° or 90°; the
        // manifest gives each outlet's centre. Local frame: origin on the main centre line at the piece's bottom edge,
        // z along the main road, x toward the branch.

        sealed class ForkPiece
        {
            public const float MainWidth = 8f;
            public string Key; public float W, L, MainX, ForkStart, Angle; public Vector2 Outlet;   // outlet in local x, z
        }

        [System.Serializable] sealed class ForkOutletJson { public string edge; public float[] centre_m; public float width_m; }
        [System.Serializable] sealed class ForkFileJson { public string filename; public int width, height; public float main_centre_x_m, fork_start_m, branch_angle_deg; public ForkOutletJson outlet; }
        [System.Serializable] sealed class ForkManifestJson { public float px_per_m; public ForkFileJson[] files; }

        static List<ForkPiece> forkPieces;

        static List<ForkPiece> ForkPieces()
        {
            if (forkPieces != null) return forkPieces;
            forkPieces = new List<ForkPiece>();
            string path = $"{CodexArt}/zhongshan-road-fork-set/manifest.json";
            if (!File.Exists(path)) return forkPieces;
            var m = JsonUtility.FromJson<ForkManifestJson>(File.ReadAllText(path));
            float ppm = m.px_per_m > 0f ? m.px_per_m : 120f;
            foreach (var f in m.files ?? new ForkFileJson[0])
            {
                if (f.outlet == null || f.outlet.centre_m == null || f.outlet.centre_m.Length < 2) continue;
                string key = Path.GetFileNameWithoutExtension(f.filename);
                if (!Has(key)) continue;
                forkPieces.Add(new ForkPiece
                {
                    Key = key, W = f.width / ppm, L = f.height / ppm, MainX = f.main_centre_x_m, ForkStart = f.fork_start_m, Angle = f.branch_angle_deg,
                    Outlet = new Vector2(f.outlet.centre_m[0] - f.main_centre_x_m, f.outlet.centre_m[1]),
                });
            }
            return forkPieces;
        }

        sealed class ForkPlan
        {
            public ForkPiece Piece; public Vector3 Origin, Tangent, Right, Outlet, OutletDir; public float Side; public Vector3[] Side_Road; public bool AtStart;
            public Vector3 Local(float x, float z) => Origin + Tangent * z + Right * Side * x;

            // Inside the piece's main-road strip (with a margin), where the main road gives way to the piece.
            bool OnMain(Vector3 p, float margin)
            {
                var d = p - Origin; d.y = 0f;
                float s = Vector3.Dot(d, Tangent), lat = Mathf.Abs(Vector3.Dot(d, Right));
                return s > -margin && s < Piece.L + margin && lat < ForkPiece.MainWidth / 2f + 2f;
            }

            // Anywhere on the piece's road area (the side road gives way there).
            public bool Covers(Vector3 p, float margin)
            {
                var d = p - Origin; d.y = 0f;
                float s = Vector3.Dot(d, Tangent), x = Vector3.Dot(d, Right) * Side;
                return OnMain(p, margin) || (s > -margin && s < Piece.L + margin && x > -margin && x < Piece.W - Piece.MainX + margin);
            }

            // Splits a road running through the piece into the parts outside it; each part ends exactly at the piece's
            // bottom or top edge, heading along the main road, so the ruts meet the piece's.
            public IEnumerable<(List<Vector3>, bool, bool)> Cut(List<Vector3> pts, bool join0, bool join1)
            {
                if (!pts.Any(p => OnMain(p, 0.2f))) { yield return (pts, join0, join1); yield break; }
                var bottom = Origin; var top = Origin + Tangent * Piece.L;
                var run = new List<Vector3>(); bool runJoin0 = join0;
                for (int i = 0; i < pts.Count; i++)
                {
                    bool inside = OnMain(pts[i], 0.2f);
                    if (!inside) { run.Add(pts[i]); continue; }
                    if (run.Count > 0)
                    {
                        // The run reaches the piece here: finish it at the nearer edge.
                        var edge = Vector3.Dot(run[run.Count - 1] - Origin, Tangent) < Piece.L / 2f ? bottom : top;
                        var outward = edge == bottom ? -Tangent : Tangent;
                        run.RemoveAll(p => Vector3.Dot(p - edge, outward) < 2.5f && Vector3.Distance(p, edge) < 6f);
                        run.Add(edge + outward * 2.5f); run.Add(edge);
                        yield return (run, runJoin0, true);
                        run = new List<Vector3>();
                    }
                    // Skip to the next point outside, then start the next run at the edge it leaves by.
                    while (i + 1 < pts.Count && OnMain(pts[i + 1], 0.2f)) i++;
                    if (i + 1 < pts.Count)
                    {
                        var next = pts[i + 1];
                        var edge = Vector3.Dot(next - Origin, Tangent) < Piece.L / 2f ? bottom : top;
                        var outward = edge == bottom ? -Tangent : Tangent;
                        run.Add(edge); run.Add(edge + outward * 2.5f);
                        while (i + 1 < pts.Count && Vector3.Dot(pts[i + 1] - edge, outward) < 2.5f && Vector3.Distance(pts[i + 1], edge) < 6f) i++;
                        runJoin0 = true;
                    }
                }
                if (run.Count > 0) yield return (run, runJoin0, join1);
            }
        }

        // The fork where this end of `line` meets another road, or null.
        static ForkPlan PlanFork(List<Vector3> line, Vector3[] self, bool atStart)
        {
            var pieces = ForkPieces();
            if (pieces.Count == 0) return null;
            var list = new List<Vector3>(line);
            if (!atStart) list.Reverse();
            var end = list[0];
            if (NearTown(end, 6f)) return null;
            Vector3 best = default, tangent = default; float bestD = 7f;
            foreach (var road in AllRoads)
            {
                if (road.points == self) continue;
                var m = road.points;
                float along = 0f, length = 0f;
                for (int i = 0; i + 1 < m.Length; i++) length += Vector3.Distance(m[i], m[i + 1]);
                for (int i = 0; i + 1 < m.Length; i++)
                {
                    var a = m[i]; var b = m[i + 1]; float seg = Vector3.Distance(a, b);
                    float t = Mathf.Clamp01(Vector3.Dot(end - a, b - a) / Mathf.Max(0.001f, (b - a).sqrMagnitude));
                    var q = a + t * (b - a);
                    float d = Vector3.Distance(q, end), at = along + t * seg;
                    if (d < bestD && at > 12f && at < length - 12f) { bestD = d; best = q; tangent = (b - a).normalized; }
                    along += seg;
                }
            }
            var away = Heading(list);
            if (bestD >= 7f)
            {
                // Three roads ending at one node: the straightest pair runs through, the third forks off it.
                var ends = new List<Vector3> { away };
                foreach (var road in AllRoads)
                {
                    if (road.points == self) continue;
                    var m = road.points.ToList();
                    if (Vector3.Distance(m[0], end) < 3f) ends.Add(Heading(m));
                    else if (Vector3.Distance(m[m.Count - 1], end) < 3f) { m.Reverse(); ends.Add(Heading(m)); }
                }
                if (ends.Count < 3) return null;
                int bi = 0, bj = 1; float straight = 2f;
                for (int i = 0; i < ends.Count; i++) for (int j = i + 1; j < ends.Count; j++)
                    if (Vector3.Dot(ends[i], ends[j]) < straight) { straight = Vector3.Dot(ends[i], ends[j]); bi = i; bj = j; }
                if (bi == 0) return null;   // this road is part of the through pair
                best = end; tangent = ends[bj];
            }
            tangent.y = 0f; tangent.Normalize();
            if (Vector3.Dot(tangent, away) < 0f) tangent = -tangent;
            var right = new Vector3(tangent.z, 0, -tangent.x);
            float side = Mathf.Sign(Vector3.Dot(away, right));
            float angle = Vector3.Angle(tangent, away);
            if (angle < 12f) return null;   // running alongside, not a fork
            var piece = pieces.OrderBy(p => Mathf.Abs(p.Angle - angle)).First();
            var plan = new ForkPlan { Piece = piece, Tangent = tangent, Right = right, Side = side, Side_Road = self, AtStart = atStart };
            plan.Origin = best - tangent * piece.ForkStart;
            float rad = piece.Angle * Mathf.Deg2Rad;
            plan.OutletDir = (tangent * Mathf.Cos(rad) + right * side * Mathf.Sin(rad)).normalized;
            plan.Outlet = plan.Local(piece.Outlet.x, piece.Outlet.y) - plan.OutletDir * 0.05f;
            return plan;
        }

        // The direction a road leaves its first point, measured some metres out.
        static Vector3 Heading(IList<Vector3> list)
        {
            Vector3 away = list[Mathf.Min(1, list.Count - 1)] - list[0];
            float run = 0f;
            for (int i = 1; i < list.Count && run < 10f; i++) { run += Vector3.Distance(list[i], list[i - 1]); away = list[i] - list[0]; }
            away.y = 0f;
            return away.normalized;
        }

        // A road that stops in the open finishes in a rounded tip: the centre line runs on by one half-width while the
        // width closes on a quarter circle, never a straight cut (user 2026-10-25).
        static (List<Vector3>, float[]) RoundEnds(List<Vector3> pts, float[] k, bool openStart, bool openEnd, float halfWidth)
        {
            var p = new List<Vector3>(pts); var w = new List<float>(k);
            const int n = 7;
            if (openEnd)
            {
                var o = pts[pts.Count - 1] - pts[pts.Count - 2]; o.y = 0f; o.Normalize();
                float r = halfWidth * k[k.Length - 1], k0 = k[k.Length - 1];
                for (int i = 1; i <= n; i++) { float a = i / (float)n * Mathf.PI / 2f; p.Add(pts[pts.Count - 1] + o * r * Mathf.Sin(a)); w.Add(k0 * Mathf.Max(0.02f, Mathf.Cos(a))); }
            }
            if (openStart)
            {
                var o = pts[0] - pts[1]; o.y = 0f; o.Normalize();
                float r = halfWidth * k[0], k0 = k[0];
                for (int i = 1; i <= n; i++) { float a = i / (float)n * Mathf.PI / 2f; p.Insert(0, pts[0] + o * r * Mathf.Sin(a)); w.Insert(0, k0 * Mathf.Max(0.02f, Mathf.Cos(a))); }
            }
            return (p, w.ToArray());
        }

        // ---------- the Hutuo and its streams ----------

        static void Rivers(Transform env)
        {
            var g = T.Group(env, "Rivers");
            var water = Graded("river_water_hd", WaterTint, smoothness: 0.8f);
            string shore = "shore_pebble_hd";
            float dep = T.Metres(shore).y, tile = T.Metres("river_water_hd").x;
            var stones = new[] { K("prop_rock_small_hd", "prop_rock_small"), K("prop_rock_limestone_hd", "prop_rock_large") };
            var rng = new System.Random(41);
            foreach (var r in ZhongshanLayout.Rivers)
            {
                var rg = T.Group(g, r.name);
                var pts = Smooth(r.points, 2.5f, r.water, out var widths);
                // Water reaches a little under the shore band so no grass shows between them.
                float y = 0.012f + (ribbonCount % 20) * 0.00003f;
                Band(rg, "Water", water, pts, widths.Select(w => -w / 2f - 0.3f).ToList(), widths.Select(w => w / 2f + 0.3f).ToList(), y, tile * 1.6f, planar: true);
                // The braids run inside the pebble bed and over the main stream: no shore bands of their own.
                if (r.id.Contains("braid")) continue;
                Band(rg, "Shore R", T.Mat(shore, clip: true), pts, widths.Select(w => w / 2f - 0.9f).ToList(), widths.Select(w => w / 2f - 0.9f + dep).ToList(), y + 0.002f, T.Metres(shore).x, vAcross: true);
                Band(rg, "Shore L", T.Mat(shore, clip: true), pts, widths.Select(w => -(w / 2f - 0.9f)).ToList(), widths.Select(w => -(w / 2f - 0.9f + dep)).ToList(), y + 0.002f, T.Metres(shore).x, vAcross: true);
                // Stones scattered on the pebble bed.
                for (int i = 0; i < pts.Count; i += 3)
                    foreach (int s in new[] { -1, 1 })
                    {
                        if (rng.NextDouble() > 0.35) continue;
                        var tan = pts[Mathf.Min(pts.Count - 1, i + 1)] - pts[Mathf.Max(0, i - 1)]; tan.y = 0f; tan.Normalize();
                        var right = new Vector3(tan.z, 0, -tan.x);
                        var p = pts[i] + right * s * (widths[i] / 2f + 1.5f + (float)rng.NextDouble() * 8f);
                        if (At(p.x, p.z) != Cell.Gravel || NearRoad(p, 4f)) continue;
                        T.Prop(rg, stones[rng.Next(stones.Length)], p.x, p.z, scale: 0.7f + (float)rng.NextDouble() * 0.5f);
                    }
            }
        }

        static bool NearRoad(Vector3 p, float r) => NearOtherRoad(p, null, r);

        static void Bridges(Transform env)
        {
            var g = T.Group(env, "Bridges and landings");
            string deck = K("zs_dock_planks", "dock_planks_hd", "wb_dock_planks");
            string rail = Has("bridge_rail") ? "bridge_rail" : null;
            foreach (var b in ZhongshanLayout.Bridges)
            {
                var bg = T.Group(g, b.id);
                var a = b.points[0]; var e = b.points[b.points.Length - 1];
                var d = e - a; float len = d.magnitude; d /= len;
                float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                var size = new Vector2(b.width, len);
                T.Quad(bg, "Deck", (a + e) / 2f + new Vector3(0, 0.05f, 0), new Vector3(90f, yaw, 0), size, T.Mat(deck), T.Repeat(deck, size), castShadows: false);
                if (b.id != "north_creek_bridge" || rail == null) continue;
                var right = new Vector3(d.z, 0, -d.x);
                float h = T.Metres(rail).y;
                foreach (int s in new[] { -1, 1 })
                {
                    var mid = (a + e) / 2f + right * s * (b.width / 2f) + new Vector3(0, h / 2f + 0.05f, 0);
                    T.Quad(bg, "Rail", mid, new Vector3(0, yaw - 90f, 0), new Vector2(len, h), T.Mat(rail, clip: true, twoSided: true), new Vector2(len / T.Metres(rail).x, 1f));
                    Solid(bg, mid, new Vector3(0.4f, 2f, len), yaw);
                }
            }
            // The ferry: a raft moored mid-stream; stepping onto either landing's end crosses to the other bank.
            var north = ZhongshanLayout.Bridges.First(b => b.id == "hutuo_north_landing").points;
            var south = ZhongshanLayout.Bridges.First(b => b.id == "hutuo_south_landing").points;
            var boat = Loc("ferry_boat");
            T.Prop(g, K("prop_ferry_boat_hd", "prop_punt_boat"), boat.x, boat.z, y: 0.02f);
            Portal(g, "Ferry to the south bank", north.Last() + new Vector3(0, 0, 0.8f), new Vector2(6f, 2f), null, "Spot Ferry south", Vector3.back);
            Portal(g, "Ferry to the north bank", south.Last() + new Vector3(0, 0, -0.8f), new Vector2(6f, 2f), null, "Spot Ferry north", Vector3.forward);
            Spot("Ferry north", north[0] + new Vector3(0, 0, 1.5f));
            Spot("Ferry south", south[0] + new Vector3(0, 0, -1.5f));
            foreach (var p in new[] { north.Last(), south.Last() })
                foreach (int s in new[] { -1, 1 }) T.Prop(g, K("prop_mooring_post", "prop_hitching_post"), p.x + s * 2.6f, p.z, y: 0.05f);
        }

        static RegionPortal Portal(Transform parent, string name, Vector3 at, Vector2 size, string scene, string arrival, Vector3 facing)
        {
            var p = new GameObject(name).AddComponent<RegionPortal>();
            p.transform.SetParent(parent, false);
            p.transform.position = at;
            p.Size = size; p.TargetScene = scene; p.ArrivalSpot = arrival; p.Facing = facing;
            return p;
        }

        // ---------- walls: hills, woods and water block where they visibly stand ----------

        static void Walls(Transform env)
        {
            var g = T.Group(env, "Impassable");
            bool Wall(int x, int z)
            {
                var c = mask[x, z];
                if (c == Cell.River) return true;
                if (c == Cell.Forest) return Inside(x, z, 2, k => k == Cell.Forest);
                if (c == Cell.Mountain) return Inside(x, z, 3, Hill);
                if (c == Cell.Foothill) return Inside(x, z, 1, Hill);
                return false;
            }
            foreach (var r in Rects(Wall))
                Solid(g, new Vector3(XMin + r.x + r.width / 2f, 3f, ZMin + r.y + r.height / 2f), new Vector3(r.width, 6f, r.height));
        }

        // Hills: a height field over the mountain and foothill cells rising with the distance from their foot; the
        // Taihang climbs to about 55 m in the west, the foothill belt and the terrace risers stay low.
        static float[,] hillDist;

        static float HillHeight(int x, int z)
        {
            x = Mathf.Clamp(x, 0, W - 1); z = Mathf.Clamp(z, 0, H - 1);
            float d = hillDist[x, z];
            if (d <= 0f) return -0.6f;   // just under the ground, so a slope meets the plain on a line, not a step
            float noise = Mathf.PerlinNoise(x * 0.03f, z * 0.03f);
            bool mountain = mask[x, z] == Cell.Mountain;
            float h = (mountain ? Mathf.Pow(d, 0.9f) * 1.7f : Mathf.Pow(d, 0.8f) * 1.1f) * Mathf.SmoothStep(0.25f, 1f, d / 9f);   // eases out of the ground
            float cap = mountain ? 30f + 25f * Mathf.Clamp01((90f - x) / 90f) : 14f;
            return Mathf.Min(cap, h) * (0.65f + 0.7f * noise);
        }

        static void Mountains(Transform env)
        {
            var mg = T.Group(env, "Hills");
            hillDist = new float[W, H];
            for (int z = 0; z < H; z++) for (int x = 0; x < W; x++) hillDist[x, z] = Hill(mask[x, z]) ? 9999f : 0f;
            for (int z = 0; z < H; z++) for (int x = 0; x < W; x++)
            {
                if (hillDist[x, z] == 0f) continue;
                float d = hillDist[x, z];
                if (x > 0) d = Mathf.Min(d, hillDist[x - 1, z] + 1f);
                if (z > 0) d = Mathf.Min(d, hillDist[x, z - 1] + 1f);
                if (x > 0 && z > 0) d = Mathf.Min(d, hillDist[x - 1, z - 1] + 1.414f);
                if (x < W - 1 && z > 0) d = Mathf.Min(d, hillDist[x + 1, z - 1] + 1.414f);
                hillDist[x, z] = d;
            }
            for (int z = H - 1; z >= 0; z--) for (int x = W - 1; x >= 0; x--)
            {
                if (hillDist[x, z] == 0f) continue;
                float d = hillDist[x, z];
                if (x < W - 1) d = Mathf.Min(d, hillDist[x + 1, z] + 1f);
                if (z < H - 1) d = Mathf.Min(d, hillDist[x, z + 1] + 1f);
                if (x < W - 1 && z < H - 1) d = Mathf.Min(d, hillDist[x + 1, z + 1] + 1.414f);
                if (x > 0 && z < H - 1) d = Mathf.Min(d, hillDist[x - 1, z + 1] + 1.414f);
                hillDist[x, z] = d;
            }
            // The region's west edge is cut off by the map: the massif keeps rising past it.
            for (int z = 0; z < H; z++) for (int x = 0; x < 6; x++) if (mask[x, z] == Cell.Mountain) hillDist[x, z] += 40f;
            const int s = 2;
            int gw = W / s + 1, gh = H / s + 1;
            float slopeTile = T.Metres("hill_slope_hd").x;
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var colours = new List<Color>(); var tris = new List<int>();
            var index = new int[gw, gh];
            for (int gz = 0; gz < gh; gz++)
                for (int gx = 0; gx < gw; gx++)
                {
                    index[gx, gz] = -1;
                    int x = gx * s, z = gz * s;
                    bool near = false;
                    for (int dz = -s; dz <= s && !near; dz++) for (int dx = -s; dx <= s && !near; dx++)
                        near = hillDist[Mathf.Clamp(x + dx, 0, W - 1), Mathf.Clamp(z + dz, 0, H - 1)] > 0f;
                    if (!near) continue;
                    index[gx, gz] = verts.Count;
                    float y = HillHeight(x, z);
                    verts.Add(new Vector3(XMin + x, y - 0.05f, ZMin + z));
                    uvs.Add(new Vector2(x / slopeTile, z / slopeTile));
                }
            for (int gz = 0; gz + 1 < gh; gz++)
                for (int gx = 0; gx + 1 < gw; gx++)
                {
                    int a = index[gx, gz], b = index[gx + 1, gz], c = index[gx + 1, gz + 1], d = index[gx, gz + 1];
                    if (a < 0 || b < 0 || c < 0 || d < 0) continue;
                    tris.AddRange(new[] { a, d, c, a, c, b });
                }
            var mesh = new Mesh { name = "Zhongshan hills", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            string meshPath = TerrainDir + "/Zhongshan_Hills.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath)) AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);
            var go = new GameObject("Hill mesh");
            go.transform.SetParent(mg, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Graded("hill_slope_hd", HillTint);

            // Scrub and limestone along every foot (so slope and grass meet through a fringe, not a line); cypress and
            // pine up the Taihang, cypress and scrub on the foothills.
            var rng = new System.Random(83);
            string scrub = K("prop_scrub_hd", "prop_undergrowth"), rock = K("prop_rock_limestone_hd", "prop_rock_large"), pebble = K("prop_rock_small_hd", "prop_rock_small");
            string cypress = K("prop_tree_cypress_hd", "prop_tree_pine"), pine = K("prop_tree_pine_hd", "prop_tree_pine");
            for (int z = 1; z < H - 1; z += 3)
                for (int x = 1; x < W - 1; x += 3)
                {
                    float d = hillDist[x, z];
                    if (d <= 0f || d > 2.5f || rng.Next(3) == 0) continue;
                    float fx = x + (float)rng.NextDouble() * 2f - 1f, fz = z - 1.2f;
                    if (NearRoad(new Vector3(fx, 0, fz), 4f)) continue;
                    T.Prop(mg, rng.Next(4) == 0 ? rock : rng.Next(3) == 0 ? pebble : scrub, XMin + fx, ZMin + fz, scale: 0.8f + (float)rng.NextDouble() * 0.4f);
                }
            for (int z = 2; z < H - 2; z += 6)
                for (int x = 2; x < W - 2; x += 6)
                {
                    float fx = x + (float)rng.NextDouble() * 4f, fz = z + (float)rng.NextDouble() * 4f;
                    int ix = Mathf.Clamp((int)fx, 0, W - 1), iz = Mathf.Clamp((int)fz, 0, H - 1);
                    float d = hillDist[ix, iz];
                    if (d <= 3f) continue;
                    float y = HillHeight(ix, iz);
                    bool mountain = mask[ix, iz] == Cell.Mountain;
                    int roll = rng.Next(10);
                    if (roll < (mountain ? 5 : 3)) T.Prop(mg, mountain && roll < 2 ? pine : cypress, XMin + fx, ZMin + fz, y: y, scale: 0.85f + (float)rng.NextDouble() * 0.35f);
                    else if (roll < 7) T.Prop(mg, scrub, XMin + fx, ZMin + fz, y: y, scale: 0.9f + (float)rng.NextDouble() * 0.4f);
                    else if (roll < 8) T.Prop(mg, rock, XMin + fx, ZMin + fz, y: y);
                }
        }

        // Terraces: the dry-stone riser at the foot of every slope that borders a terrace field.
        static void Terraces(Transform env)
        {
            var tg = T.Group(env, "Terrace walls");
            string wall = "terrace_wall_hd";
            if (!Has(wall)) return;
            var size = T.Metres(wall);
            var rng = new System.Random(19);
            // Walk the terrace cells; where the cell north (or east/west) is a hill, stand a stretch of wall facing the field.
            for (int z = 1; z < H - 1; z += 2)
                for (int x = 1; x < W - 1; x += 2)
                {
                    if (mask[x, z] != Cell.Terrace) continue;
                    foreach (var (dx, dz, yaw) in new[] { (0, 1, 0f), (1, 0, 90f), (-1, 0, -90f), (0, -1, 180f) })
                    {
                        if (!Hill(mask[x + dx, z + dz])) continue;
                        if (yaw == 180f) continue;   // risers facing away from the camera are hidden by the slope anyway
                        float len = 2.4f;
                        var at = new Vector3(XMin + x + 0.5f + dx * 0.6f, size.y / 2f - 0.05f, ZMin + z + 0.5f + dz * 0.6f);
                        T.Quad(tg, "Terrace wall", at, new Vector3(0, yaw, 0), new Vector2(len, size.y), T.Mat(wall, clip: true), new Vector2(len / size.x, 1f));
                        if (rng.Next(9) == 0) T.Prop(tg, K("prop_grass_tuft_hd", "prop_grass_tuft"), at.x - dx * 0.8f, at.z - dz * 0.8f);
                    }
                }
        }

        // ---------- woods ----------

        // Each wood keeps its own trees (layout region names): cypress on the ridges, jujube by the terrace village,
        // persimmon east of town and at the river bend, mixed groves elsewhere.
        static string[] TreesFor(string name)
        {
            string cypress = K("prop_tree_cypress_hd", "prop_tree_pine"), jujube = K("prop_tree_jujube_hd", "prop_tree_jujube"),
                persimmon = K("prop_tree_persimmon_hd", "locust_tree"), pine = K("prop_tree_pine_hd", "prop_tree_pine"),
                locust = K("locust_tree", "prop_tree_oak"), willow = K("willow_anxi", "prop_tree_willow");
            if (name.Contains("柏")) return new[] { cypress, cypress, pine };
            if (name.Contains("枣")) return new[] { jujube, jujube, locust };
            if (name.Contains("柿")) return new[] { persimmon, persimmon, jujube };
            if (name.Contains("南岸")) return new[] { willow, locust, persimmon };
            return new[] { locust, persimmon, cypress, jujube };
        }

        static void Forests(Transform env)
        {
            var fg = T.Group(env, "Woods");
            // Floor only well inside each wood, so its edge hides under the trees.
            foreach (var r in Rects((x, z) => Inside(x, z, 3, c => c == Cell.Forest)))
            {
                var size = new Vector2(r.width, r.height);
                T.Quad(fg, "Forest floor", new Vector3(XMin + r.x + r.width / 2f, 0.008f, ZMin + r.y + r.height / 2f), new Vector3(90f, 0, 0), size,
                    T.Mat("forest_floor_hd"), T.Repeat("forest_floor_hd", size), castShadows: false);
            }
            var rng = new System.Random(71);
            string scrub = K("prop_scrub_hd", "prop_undergrowth");
            foreach (var region in ZhongshanLayout.Regions.Where(r => r.cls == "Forest"))
            {
                var trees = TreesFor(region.name);
                var wg = T.Group(fg, region.name + " " + region.id);
                float x0 = region.polygon.Min(p => p.x), x1 = region.polygon.Max(p => p.x), z0 = region.polygon.Min(p => p.y), z1 = region.polygon.Max(p => p.y);
                for (float z = z0; z <= z1; z += 3.2f)
                    for (float x = x0; x <= x1; x += 3.2f)
                    {
                        float fx = x + (float)rng.NextDouble() * 2.6f, fz = z + (float)rng.NextDouble() * 2.6f;
                        if (At(fx, fz) != Cell.Forest) continue;
                        bool edge = At(fx, fz - 4f) != Cell.Forest || At(fx - 4f, fz) != Cell.Forest || At(fx + 4f, fz) != Cell.Forest;
                        float y = hillDist != null ? Mathf.Max(0f, HillHeight((int)fx, (int)fz)) : 0f;
                        T.Prop(wg, trees[rng.Next(trees.Length)], fx, fz, y: y, scale: 0.8f + (float)rng.NextDouble() * 0.35f);
                        if (edge && rng.Next(2) == 0) T.Prop(wg, scrub, fx + 1f, fz - 1.8f, scale: 0.9f);
                    }
            }
        }

        // ---------- Anxi from outside ----------

        // The county seat's rammed-earth wall (Anxi's own wall and gate art, so it matches the town scene), the tops of
        // the yamen, the post house and homes showing over it. Only the south gate opens: it leads into Anxi_Town.
        static void Anxi(Transform env, Transform labels)
        {
            var a = T.Group(env, "Anxi (安喜县城)");
            float x0 = ZhongshanLayout.TownX0, x1 = ZhongshanLayout.TownX1, z0 = ZhongshanLayout.TownZ0, z1 = ZhongshanLayout.TownZ1;
            var inner = new Vector2(x1 - x0 - WallTk, z1 - z0 - WallTk);
            T.Quad(a, "Town ground", new Vector3((x0 + x1) / 2f, 0.004f, (z0 + z1) / 2f), new Vector3(90f, 0, 0), inner, Graded("packed_earth_hd", RoadTint), T.Repeat("packed_earth_hd", inner), castShadows: false);
            var gates = ZhongshanLayout.Gates;
            void Run(string name, Vector3 p, Vector3 q)
            {
                // Split around any gate on this line.
                bool alongX = Mathf.Abs(q.x - p.x) > Mathf.Abs(q.z - p.z);
                var cuts = new List<(float, float)>();
                foreach (var gt in gates)
                    if (gt.alongX == alongX && Mathf.Abs(gt.line - (alongX ? p.z : p.x)) < 0.5f) cuts.Add((gt.a, gt.b));
                float from = alongX ? Mathf.Min(p.x, q.x) : Mathf.Min(p.z, q.z), to = alongX ? Mathf.Max(p.x, q.x) : Mathf.Max(p.z, q.z);
                foreach (var (ca, cb) in cuts.OrderBy(c => c.Item1)) { Wall(a, name, alongX, alongX ? p.z : p.x, from, ca); from = cb; }
                Wall(a, name, alongX, alongX ? p.z : p.x, from, to);
            }
            Run("South wall", new Vector3(x0, 0, z0), new Vector3(x1, 0, z0));
            Run("North wall", new Vector3(x0, 0, z1), new Vector3(x1, 0, z1));
            Run("West wall", new Vector3(x0, 0, z0), new Vector3(x0, 0, z1));
            Run("East wall", new Vector3(x1, 0, z0), new Vector3(x1, 0, z1));
            foreach (var gt in gates)
            {
                float mid = (gt.a + gt.b) / 2f;
                var at = gt.alongX ? new Vector3(mid, 0, gt.line) : new Vector3(gt.line, 0, mid);
                float yaw = gt.id == "gate_south" ? 0f : gt.id == "gate_north" ? 180f : gt.id == "gate_west" ? 90f : -90f;
                bool open = gt.id == "gate_south";
                Gate(a, gt.id, at, yaw, open);
                Label(labels, gt.id switch { "gate_south" => "安喜·南门（入城）", "gate_north" => "北门（戒严·闭）", "gate_west" => "西门（戒严·闭）", _ => "东门（戒严·闭）" }, at, 7f);
            }
            // Roofs over the wall: the yamen at the head of the main street, the post house, homes; the town scene's art.
            var inside = T.Group(a, "Inside the walls (seen over the wall)");
            string tiles = K("roof_anxi_tiles", "roof_tiles_grey"), eave = K("roof_anxi_eave", "roof_eave");
            T.Building(inside, "Yamen", new Vector3(278f, 0, 176f), 0f, "yamen_front", "yamen_side", null, roofTexture: tiles, eaveTexture: eave);
            T.Building(inside, "Post house", new Vector3(300f, 0, 174f), 0f, "yiguan_front", "yiguan_side", null, roofTexture: tiles, eaveTexture: eave);
            (Vector3 at, string front)[] homes =
            {
                (new Vector3(250f, 0, 178f), "house_anxi_a_front"), (new Vector3(262f, 0, 140f), "house_anxi_b_front"), (new Vector3(296f, 0, 140f), "house_anxi_c_front"),
                (new Vector3(312f, 0, 150f), "house_anxi_a_front"), (new Vector3(246f, 0, 146f), "house_anxi_c_front"), (new Vector3(316f, 0, 178f), "house_anxi_b_front"),
                (new Vector3(258f, 0, 166f), "house_anxi_b_front"), (new Vector3(300f, 0, 158f), "house_anxi_a_front"),
            };
            foreach (var (at, front) in homes) T.Building(inside, "Home", at, 0f, front, "house_anxi_side", null, roofTexture: tiles, eaveTexture: eave);
            foreach (var (x, z) in new[] { (240f, 186f), (268f, 186f), (318f, 136f), (240f, 134f), (288f, 186f) }) T.Prop(inside, K("locust_tree", "prop_tree_locust"), x, z, scale: 0.9f);
            // Persimmons outside the east wall, willows by the south gate's road.
            foreach (var (x, z) in new[] { (264f, 116f), (292f, 116f) }) T.Prop(a, K("willow_anxi", "prop_tree_willow"), x, z, solid: true);
            // The way in: the south gate opens onto Anxi_Town; a little worn earth before it.
            Portal(a, "Into Anxi (south gate)", new Vector3(278f, 0, z0 - 1.6f), new Vector2(6f, 2.2f), ThreeKingdoms.Gameplay.Story.GameRun.AnxiScene, "Arrival (inside the south gate)", Vector3.forward);
            Spot(AnxiSouthArrival, SouthGateOut);
            Label(labels, "安喜县城", new Vector3(278f, 0, 162f), 14f);
        }

        // A straight run of Anxi's rammed-earth wall, at `line` (z for an east–west run, x for north–south) from a to b.
        static void Wall(Transform parent, string name, bool alongX, float line, float a, float b)
        {
            float len = b - a;
            if (len < 0.5f) return;
            var centre = alongX ? new Vector3((a + b) / 2f, 0, line) : new Vector3(line, 0, (a + b) / 2f);
            var run = T.Group(parent, name, centre, alongX ? 0f : 90f);
            run.gameObject.AddComponent<Occluder>();
            string face = K("wall_anxi_rammed", "wall_rammed_earth");
            var size = new Vector2(len + WallTk, WallH);
            T.Quad(run, "Face out", new Vector3(0, WallH / 2f, -WallTk / 2f), Vector3.zero, size, T.Mat(face), T.Repeat(face, size));
            T.Quad(run, "Face in", new Vector3(0, WallH / 2f, WallTk / 2f), new Vector3(0, 180, 0), size, T.Mat(face), T.Repeat(face, size));
            var top = new Vector2(len + WallTk, WallTk);
            T.Quad(run, "Wall top", new Vector3(0, WallH, 0), new Vector3(90, 0, 0), top, Graded("packed_earth_hd", RoadTint), T.Repeat("packed_earth_hd", top), castShadows: false);
            if (Has("wall_anxi_coping"))
            {
                var cop = new Vector2(len + WallTk, T.Metres("wall_anxi_coping").y);
                T.Quad(run, "Coping", new Vector3(0, WallH + cop.y / 2f - 0.15f, -WallTk / 2f - 0.02f), Vector3.zero, cop, T.Mat("wall_anxi_coping", clip: true), T.Repeat("wall_anxi_coping", cop));
            }
            T.Solid(run, new Vector3(0, WallH / 2f, 0), new Vector3(len + WallTk, WallH, WallTk));
        }

        // Anxi's gate face over a 6 m gap; a shut gate gets a closed leaf and a collider.
        static void Gate(Transform parent, string name, Vector3 at, float yaw, bool open)
        {
            var g = T.Group(parent, name, at, yaw);
            g.gameObject.AddComponent<Occluder>();
            string key = K("gate_anxi_south", "south_gate_wall_front");
            var f = T.Metres(key);
            T.Quad(g, "Gate outside", new Vector3(0, f.y / 2f, -WallTk / 2f - 0.05f), Vector3.zero, f, T.Mat(key, clip: true, twoSided: true));
            T.Quad(g, "Gate inside", new Vector3(0, f.y / 2f, WallTk / 2f + 0.05f), new Vector3(0, 180, 0), f, T.Mat(key, clip: true, twoSided: true));
            foreach (int s in new[] { -1, 1 }) T.Solid(g, new Vector3(s * 3.6f, f.y / 2f, 0), new Vector3(1.2f, f.y, WallTk));
            if (open) return;
            // Shut: two plank leaves across the opening (timber, so it reads as a closed door, not a wall).
            var leaf = new Vector2(6f, 3.6f);
            T.Quad(g, "Shut leaves", new Vector3(0, leaf.y / 2f, -WallTk / 2f + 0.15f), Vector3.zero, leaf, T.Mat(K("zs_dock_planks", "dock_planks_hd", "wb_dock_planks")), new Vector2(1f, 0.6f));
            T.Solid(g, new Vector3(0, 2f, 0), new Vector3(6f, 4f, WallTk));
        }

        // ---------- the post station at the Jingxing road head ----------

        static void PostStation(Transform env, Transform labels)
        {
            var at = Loc("post_station");
            var p = T.Group(env, "Post station (井陉路口驿站)");
            EarthPatch(env, at + new Vector3(3f, 0, 1f), 12f, 7f, 61);
            // Stands north-east of its yard, door facing the road (south), clear of every road; the yard stays open
            // (open space before landmarks).
            WildHouse(env, p, "Post station", PostFront, PostFrontKey, PostSideKey, K("roof_anxi_tiles", "roof_tiles_grey"), false, 67);
            T.Prop(p, K("hitching_post", "prop_hitching_post"), at.x + 7f, at.z + 2f, solid: true);
            T.Prop(p, K("prop_millet_stack_hd", "prop_hay_pile"), at.x - 8.5f, at.z + 3.5f, solid: true);
            T.Prop(p, K("prop_tree_jujube_hd", "prop_tree_jujube"), at.x - 11f, at.z + 6f, solid: true);
            Label(labels, "井陉路口驿站", at + new Vector3(0, 0, 4f), 9f);
            Spot("Post station", at + new Vector3(0, 0, -6f));
        }

        // Where the country houses stand: front centre, front and side art (the places below build from the same list).
        static readonly (float dx, float dz)[] TerraceHouses = { (-9f, 7f), (2f, 9f), (12f, 6f), (-14f, -4f) };
        static readonly (float dx, float dz)[] RiverHouses = { (-10f, 6f), (1f, 8f), (12f, 6f), (16f, -3f) };
        static Vector3 PostFront => Loc("post_station") + new Vector3(6f, 0, 7f);
        static string PostFrontKey => K("zs_post_front", "yiguan_front");
        static string PostSideKey => K("zs_post_side", "yiguan_side");
        static string StoneFront => K("zs_stonehouse_front", "house_anxi_a_front");
        static string StoneSide => K("zs_stonehouse_side", "house_anxi_side");
        static string RiverFront => K("zs_riverhouse_front", "house_anxi_c_front");
        static string RiverSide => K("zs_riverhouse_side", "house_anxi_side");

        static IEnumerable<(Vector3 front, string frontKey, string sideKey)> HousePlots()
        {
            yield return (PostFront, PostFrontKey, PostSideKey);
            foreach (var (dx, dz) in TerraceHouses) yield return (Loc("terrace_village") + new Vector3(dx, 0, dz), StoneFront, StoneSide);
            foreach (var (dx, dz) in RiverHouses) yield return (Loc("river_village") + new Vector3(dx, 0, dz), RiverFront, RiverSide);
        }

        // A house out in the country stands on its own worn apron of beaten earth, a few metres wider than the house
        // all round and a little deeper at the door, with a ragged edge into the grass, so it never sits straight on
        // the turf or across a road (user 2026-10-26).
        static void WildHouse(Transform env, Transform parent, string name, Vector3 front, string frontKey, string sideKey, string roof, bool thatch, int seed)
        {
            float w = T.Metres(frontKey).x, d = T.Metres(sideKey).x;
            EarthPatch(env, front + new Vector3(0, 0, d / 2f - 0.8f), w / 2f + 3.4f, d / 2f + 3.4f, seed);
            T.Building(parent, name, front + new Vector3(0, 0.001f, 0), 0f, frontKey, sideKey, null, roofKey: thatch ? "thatch" : "tile", roofTexture: roof, eaveTexture: K("roof_anxi_eave", "roof_eave"));
        }

        // ---------- villages ----------

        // 石坎村: dry-stone houses with slate roofs on the terrace floor, a stone well, millet stacks, jujube trees.
        static void TerraceVillage(Transform env, Transform labels)
        {
            var c = Loc("terrace_village");
            var v = T.Group(env, "石坎村 (terrace village)");
            foreach (var g in VillageGrounds().Take(1)) EarthPatch(env, g.centre, g.rx, g.rz, 23);
            string roof = K("zs_roof_slate", "roof_anxi_tiles");
            foreach (var (dx, dz) in TerraceHouses)
            {
                var at = c + new Vector3(dx, 0, dz);
                WildHouse(env, v, "Stone house", at, StoneFront, StoneSide, roof, false, (int)(at.x * 7 + at.z));
            }
            T.Prop(v, K("prop_well_stone_hd", "prop_well"), c.x + 3f, c.z - 1f, solid: true);
            T.Prop(v, K("prop_millet_stack_hd", "prop_hay_pile"), c.x - 5f, c.z + 1.5f, solid: true);
            T.Prop(v, K("prop_millet_stack_hd", "prop_hay_pile"), c.x + 9f, c.z - 2f, solid: true);
            T.Prop(v, K("prop_persimmon_rack_hd", "prop_firewood"), c.x - 2f, c.z + 4.5f);
            Label(labels, "石坎村（梯田）", c + new Vector3(0, 0, 3f), 9f);
            Spot("Terrace village", c + new Vector3(0, 0, -4f));
        }

        // 河湾村: mud-brick houses on pebble footings under reed roofs, persimmon trees, nets drying, at the river bend.
        static void RiverVillage(Transform env, Transform labels)
        {
            var c = Loc("river_village");
            var v = T.Group(env, "河湾村 (river village)");
            foreach (var g in VillageGrounds().Skip(1)) EarthPatch(env, g.centre, g.rx, g.rz, 29);
            string roof = K("zs_roof_reed", "roof_thatch");
            foreach (var (dx, dz) in RiverHouses)
            {
                var at = c + new Vector3(dx, 0, dz);
                WildHouse(env, v, "River house", at, RiverFront, RiverSide, roof, true, (int)(at.x * 7 + at.z));
            }
            foreach (var (dx, dz) in new[] { (-16f, 2f), (7f, 14f), (20f, 8f) }) T.Prop(v, K("prop_tree_persimmon_hd", "locust_tree"), c.x + dx, c.z + dz, solid: true);
            T.Prop(v, K("prop_persimmon_rack_hd", "prop_firewood"), c.x + 5f, c.z + 3f);
            T.Prop(v, K("prop_well_stone_hd", "prop_well"), c.x - 4f, c.z - 1f, solid: true);
            Label(labels, "河湾村", c + new Vector3(0, 0, 3f), 9f);
            Spot("River village", c + new Vector3(0, 0, -4f));
        }

        // ---------- wild places ----------

        static void Explore(Transform env, Transform labels)
        {
            // 1 石窟小庙: a niche cut into the cliff above the shrine trail's end.
            var sh = Loc("rock_shrine");
            var s = T.Group(env, "Rock shrine (石窟小庙)");
            if (Has("zs_cave_shrine_front"))
            {
                var size = T.Metres("zs_cave_shrine_front");
                T.Quad(s, "Cave shrine", new Vector3(sh.x - 1f, size.y / 2f, sh.z + 5f), Vector3.zero, size, T.Mat("zs_cave_shrine_front", clip: true, twoSided: true));
                Solid(s, new Vector3(sh.x - 1f, 2f, sh.z + 6f), new Vector3(size.x, 4f, 2f));
            }
            else Box(s, "Cave shrine (stand-in)", new Vector3(sh.x - 1f, 0, sh.z + 6f), new Vector3(8f, 6f, 3f), 0.7f);
            EarthPatch(env, sh, 5f, 4f, 31);
            Label(labels, "石窟小庙（探索点）", sh + new Vector3(0, 0, 5f), 8f);
            Spot("Shrine", sh + new Vector3(0, 0, -3f));

            // 2 古烽燧: a fieldstone beacon on the north ridge.
            var bc = Loc("old_beacon");
            var b = T.Group(env, "Old beacon (古烽燧)");
            if (Has("zs_beacon_front") && Has("zs_beacon_side")) TexturedBlock(b, "Beacon", bc + new Vector3(0, 0, 5f), "zs_beacon_front", "zs_beacon_side");
            else Box(b, "Beacon (stand-in)", bc + new Vector3(0, 0, 5f), new Vector3(5f, 8f, 5f), 0.6f);
            T.Prop(b, K("prop_rock_limestone_hd", "prop_rock_large"), bc.x + 4f, bc.z + 1.5f);
            T.Prop(b, K("prop_tree_cypress_hd", "prop_tree_pine"), bc.x - 5f, bc.z + 3f, solid: true);
            EarthPatch(env, bc, 6f, 4f, 37);
            Label(labels, "古烽燧（探索点）", bc + new Vector3(0, 0, 5f), 11f);
            Spot("Beacon", bc + new Vector3(0, 0, -4f));

            // 3 河滩沉船: a wreck half sunk in the pebbles of the south bank.
            var wk = Loc("shipwreck");
            var w = T.Group(env, "Shipwreck (河滩沉船)");
            T.Prop(w, K("prop_wrecked_boat_hd", "prop_punt_boat"), wk.x, wk.z + 2f, solid: true);
            foreach (var (dx, dz) in new[] { (-4f, 0f), (5f, 1f), (2f, -2f) }) T.Prop(w, K("prop_rock_small_hd", "prop_rock_small"), wk.x + dx, wk.z + dz);
            Label(labels, "河滩沉船（探索点）", wk, 6f);
            Spot("Wreck", wk + new Vector3(0, 0, -4f));
        }

        // A free-standing block whose four faces carry front/side art (front and back the same).
        static void TexturedBlock(Transform parent, string name, Vector3 baseCentre, string front, string side)
        {
            var f = T.Metres(front); var sd = T.Metres(side);
            var g = T.Group(parent, name, baseCentre);
            g.gameObject.AddComponent<Occluder>();
            float w = f.x * 0.9f, d = sd.x * 0.9f, h = Mathf.Max(f.y, sd.y);
            T.Quad(g, "Front", new Vector3(0, f.y / 2f, -d / 2f), Vector3.zero, f, T.Mat(front, clip: true));
            T.Quad(g, "Back", new Vector3(0, f.y / 2f, d / 2f), new Vector3(0, 180f, 0), f, T.Mat(front, clip: true));
            T.Quad(g, "Side L", new Vector3(-w / 2f, sd.y / 2f, 0), new Vector3(0, 90f, 0), sd, T.Mat(side, clip: true));
            T.Quad(g, "Side R", new Vector3(w / 2f, sd.y / 2f, 0), new Vector3(0, -90f, 0), sd, T.Mat(side, clip: true));
            T.Solid(g, new Vector3(0, h / 2f, 0), new Vector3(w * 0.9f, h, d * 0.9f));
        }

        // The two ways west, shut in 185 (layout passes): the Black Mountain stockade across the Jingxing road between
        // cliffs, and the Feihu plank road collapsed over its chasm, each with the last guard post before it.
        static void Passes(Transform env, Transform labels)
        {
            var g = T.Group(env, "Passes");
            string cliff = K("zs_cliff_face", "cliff_face");
            foreach (var pass in ZhongshanLayout.Passes)
            {
                var pg = T.Group(g, pass.name);
                var a = pass.barrier[0]; var b = pass.barrier[pass.barrier.Length - 1];
                var mid = (a + b) / 2f;
                float len = Vector3.Distance(a, b);
                // Cliffs along the blocker polygon's edges that face the region (east side), so the way ends at rock.
                var poly = pass.blocker;
                for (int i = 0; i < poly.Length; i++)
                {
                    var p = poly[i]; var q = poly[(i + 1) % poly.Length];
                    var d = new Vector3(q.x - p.x, 0, q.y - p.y); float l = d.magnitude; if (l < 2f) continue; d /= l;
                    var outward = new Vector3(d.z, 0, -d.x);
                    var centroid = new Vector3(poly.Average(v => v.x), 0, poly.Average(v => v.y));
                    var c = new Vector3((p.x + q.x) / 2f, 0, (p.y + q.y) / 2f);
                    if (Vector3.Dot(outward, c - centroid) < 0f) outward = -outward;
                    if (outward.x < -0.3f) continue;   // the west faces lie beyond the region
                    float yaw = Mathf.Atan2(-outward.x, -outward.z) * Mathf.Rad2Deg;   // quad front faces outward
                    float h = Has(cliff) ? Mathf.Min(T.Metres(cliff).y * 1.4f, 11f) : 9f;
                    T.Quad(pg, "Cliff", c + outward * 0.2f + new Vector3(0, h / 2f - 0.3f, 0), new Vector3(0, yaw, 0), new Vector2(l + 1f, h), T.Mat(cliff, clip: true, twoSided: true),
                        new Vector2((l + 1f) / T.Metres(cliff).x, 1f));
                }
                Solid(pg, new Vector3(poly.Average(v => v.x), 4f, poly.Average(v => v.y)), new Vector3(poly.Max(v => v.x) - poly.Min(v => v.x), 8f, poly.Max(v => v.y) - poly.Min(v => v.y)));
                if (pass.id.StartsWith("jingxing"))
                {
                    string gate = K("zs_checkpoint_gate", "checkpoint_gate");
                    var gs = T.Metres(gate) * (Has("zs_checkpoint_gate") ? 1f : 2.4f);
                    T.Quad(pg, "Black Mountain stockade", mid + new Vector3(1.2f, gs.y / 2f, 0), new Vector3(0, -90f, 0), gs, T.Mat(gate, clip: true, twoSided: true));
                    Solid(pg, mid + new Vector3(0.5f, 2f, 0), new Vector3(2f, 4f, len + 2f));
                    foreach (float dz in new[] { -4.5f, 0f, 4.5f }) T.Prop(pg, K("prop_cheval_hd", "prop_weapon_rack"), mid.x + 4.5f, mid.z + dz, solid: true);
                    foreach (float dz in new[] { -6.5f, 6.5f }) T.Prop(pg, K("prop_banner_heishan_hd", "prop_banner_han"), mid.x + 2.4f, mid.z + dz);
                    Label(labels, "井陉口（黑山军设寨·不可通过）", mid, 9f);
                }
                else
                {
                    if (Has("zs_plank_road_broken"))
                    {
                        var ps = T.Metres("zs_plank_road_broken");
                        T.Quad(pg, "Collapsed plank road", mid + new Vector3(1.2f, ps.y / 2f, 0), new Vector3(0, -90f, 0), ps, T.Mat("zs_plank_road_broken", clip: true, twoSided: true));
                    }
                    else Box(pg, "Collapsed plank road (stand-in)", mid + new Vector3(1.5f, 0, 0), new Vector3(1f, 5f, len), 0.45f);
                    Solid(pg, mid + new Vector3(0.5f, 2f, 0), new Vector3(2f, 4f, len + 2f));
                    Label(labels, "飞狐陉（栈道坍塌·不可通过）", mid, 9f);
                }
            }
            // The guard posts on this side.
            var jx = Loc("checkpoint_jingxing");
            EarthPatch(env, jx, 6f, 5f, 43);
            T.Prop(g, K("prop_banner_han", "prop_banner_post"), jx.x + 3f, jx.z + 4f);
            Label(labels, "井陉哨卡", jx + new Vector3(0, 0, 3f), 6f);
            Spot("Jingxing checkpoint", jx + new Vector3(4f, 0, 0));
            var fh = Loc("checkpoint_feihu");
            EarthPatch(env, fh, 6f, 5f, 47);
            if (Has("zs_toll_booth")) T.Prop(g, "zs_toll_booth", fh.x + 2f, fh.z + 4f, solid: true);
            else Box(g, "Toll booth (stand-in)", fh + new Vector3(2f, 0, 4f), new Vector3(3.5f, 3f, 3f), 0.6f);
            Label(labels, "飞狐关吏", fh + new Vector3(0, 0, 3f), 6f);
            Spot("Feihu checkpoint", fh + new Vector3(4f, 0, -2f));
        }

        static void Exits(Transform env, Transform labels)
        {
            var e = T.Group(env, "Region exits");
            foreach (var x in ZhongshanLayout.Exits)
            {
                var at = x.at;
                var inward = x.edge == "north" ? Vector3.back : x.edge == "south" ? Vector3.forward : Vector3.left;
                var post = at + inward * 6f + (x.edge == "east" ? new Vector3(0, 0, 6f) : new Vector3(6f, 0, 0));
                T.Prop(e, K("prop_signpost_hd", "prop_signpost"), post.x, post.z, solid: true);
                string name = ZhongshanLayout.Locations.First(l => l.id == x.id).name;
                Label(labels, name + "（天下图）", post, 5f);
            }
        }

        // ---------- signposts at the forks ----------

        static void Signposts(Transform env)
        {
            var g = T.Group(env, "Signposts");
            void Sign(string name, Vector3 at, params (string to, Vector3 toward, float metres)[] ways)
            {
                var root = T.Group(g, "Signpost " + name, at);
                root.position = at;
                T.Prop(root, K("prop_signpost_hd", "prop_signpost"), 0f, 0f, solid: true);
                var s = root.gameObject.AddComponent<Signpost>();
                s.Names = ways.Select(w => w.to).ToArray();
                s.Toward = ways.Select(w => w.toward).ToArray();
                s.Metres = ways.Select(w => w.metres).ToArray();
            }
            Sign("西岔口", new Vector3(172f, 0, 129f),
                ("井陉路口驿站", new Vector3(150f, 0, 133f), 30f), ("石坎村", new Vector3(150f, 0, 133f), 75f), ("安喜", new Vector3(195f, 0, 144f), 70f));
            Sign("驿站后路", new Vector3(134f, 0, 128f),
                ("井陉哨卡", new Vector3(122f, 0, 121f), 80f), ("石坎村", new Vector3(132f, 0, 145f), 50f));
            Sign("梯田岔口", new Vector3(137f, 0, 156f),
                ("飞狐关吏", new Vector3(128f, 0, 162f), 80f), ("石窟小庙", new Vector3(120f, 0, 154f), 40f), ("驿站", new Vector3(150f, 0, 141f), 40f));
            Sign("南岔口", new Vector3(283f, 0, 101f),
                ("滹沱河渡口", new Vector3(271f, 0, 90f), 15f), ("河湾村", new Vector3(296f, 0, 95f), 36f), ("安喜南门", new Vector3(278f, 0, 115f), 33f));
        }

        // Grass tufts, scrub, rocks and the odd tree over open ground, never on roads, water or at places.
        static void Scatter(Transform env)
        {
            var g = T.Group(env, "Scatter");
            var rng = new System.Random(29);
            string tuft = K("prop_grass_tuft_hd", "prop_grass_tuft"), scrub = K("prop_scrub_hd", "prop_undergrowth"), rock = K("prop_rock_small_hd", "prop_rock_small");
            string[] trees = { K("prop_tree_persimmon_hd", "locust_tree"), K("prop_tree_jujube_hd", "prop_tree_jujube"), K("locust_tree", "prop_tree_locust"), K("prop_tree_cypress_hd", "prop_tree_pine") };
            int placed = 0;
            for (int i = 0; i < 2600 && placed < 700; i++)
            {
                float x = XMin + 3f + (float)rng.NextDouble() * (XMax - XMin - 6f), z = ZMin + 3f + (float)rng.NextDouble() * (ZMax - ZMin - 6f);
                var c = At(x, z);
                if (c != Cell.Plain && c != Cell.Gravel && c != Cell.Terrace) continue;
                if (NearRoad(new Vector3(x, 0, z), 5f) || NearPlace(x, z) || NearTown(new Vector3(x, 0, z), 6f)) continue;
                int roll = rng.Next(20);
                if (c == Cell.Gravel) { if (roll < 6) T.Prop(g, rock, x, z, scale: 0.7f + (float)rng.NextDouble() * 0.5f); else continue; }
                else if (c == Cell.Terrace) { if (roll < 4) T.Prop(g, tuft, x, z); else continue; }
                else if (roll < 9) T.Prop(g, tuft, x, z, scale: 0.8f + (float)rng.NextDouble() * 0.5f);
                else if (roll < 13) T.Prop(g, scrub, x, z, scale: 0.8f + (float)rng.NextDouble() * 0.4f);
                else if (roll < 15) T.Prop(g, rock, x, z);
                else if (roll < 17) T.Prop(g, trees[rng.Next(trees.Length)], x, z, scale: 0.8f + (float)rng.NextDouble() * 0.3f, solid: true);
                else continue;
                placed++;
            }
        }

        static bool NearPlace(float x, float z)
        {
            foreach (var l in ZhongshanLayout.Locations)
            {
                if (l.kind == "town_gate" || l.kind == "civic" || l.kind == "town" || l.kind == "region_exit") continue;
                float r = l.kind == "village" ? 24f : 12f;
                if ((x - l.at.x) * (x - l.at.x) + (z - l.at.z) * (z - l.at.z) < r * r) return true;
            }
            return false;
        }

        // The snow-capped Taihang range on the western and northern horizon (Anxi's own backdrop art): the region's
        // landmark, in view from everywhere (layout western_landmark).
        static void Backdrop(Transform env)
        {
            if (!Has("far_taihang_snow")) return;
            var g = T.Group(env, "Taihang snow range (backdrop)");
            var size = T.Metres("far_taihang_snow") * 9f;
            var m = new Material(T.Mat("far_taihang_snow", clip: true)) { name = "zhongshan_far_taihang" };
            string mp = $"{MaterialDir}/{m.name}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(mp)) AssetDatabase.DeleteAsset(mp);
            AssetDatabase.CreateAsset(m, mp);
            T.Quad(g, "Snow range north", new Vector3(80f, size.y / 2f - 6f, ZMax + 260f), Vector3.zero, size, m, castShadows: false);
            T.Quad(g, "Snow range west", new Vector3(XMin - 240f, size.y / 2f - 6f, 120f), new Vector3(0, -90f, 0), size, m, castShadows: false);
        }

        // A patch of bare ground with a ragged outline (yards, threshing floors), ringed by the road's edge band.
        // The villages' common ground (user 2026-10-26: no cart ruts among the houses, one sweep of bare earth round
        // them): an ellipse of beaten earth over the whole cluster; roads stop at its edge.
        static IEnumerable<(Vector3 centre, float rx, float rz)> VillageGrounds()
        {
            yield return (Loc("terrace_village") + new Vector3(-1f, 0, 5f), 22f, 15f);
            yield return (Loc("river_village") + new Vector3(3f, 0, 5f), 22f, 14f);
        }

        static bool InVillage(Vector3 p, float scale) => VillageGrounds().Any(v =>
        {
            float dx = (p.x - v.centre.x) / (v.rx * scale), dz = (p.z - v.centre.z) / (v.rz * scale);
            return dx * dx + dz * dz <= 1f;
        });

        // A road running into a village ends a little inside its earth (its rounded tip hidden in the bare ground).
        static Vector3[] VillageCut(Vector3[] raw)
        {
            var dense = Smooth(raw, 1f, null, out _);
            if (!dense.Any(q => InVillage(q, 0.8f))) return raw;
            if (InVillage(dense[0], 0.8f)) dense.Reverse();
            int n = dense.FindIndex(q => InVillage(q, 0.8f));
            if (n < 1) return raw;
            var kept = dense.Take(n + 1).Where((q, i) => i % 4 == 0 || i == n).ToArray();
            return kept;
        }

        static void EarthPatch(Transform parent, Vector3 centre, float rx, float rz, int seed)
        {
            var rng = new System.Random(seed);
            const int n = 72;
            float ph1 = (float)rng.NextDouble() * 6.3f, ph2 = (float)rng.NextDouble() * 6.3f;
            var rim = new List<Vector3>();
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                float f = 0.86f + 0.08f * Mathf.Sin(a * 3f + ph1) + 0.05f * Mathf.Sin(a * 5f + ph2) + 0.025f * Mathf.Sin(a * 11f + ph1 * 2f);
                rim.Add(centre + new Vector3(Mathf.Cos(a) * rx * f, 0, Mathf.Sin(a) * rz * f));
            }
            string fill = "packed_earth_hd";
            var tile = T.Metres(fill);
            // Fills above every rim, so where yards overlap no grass fringe crosses a yard.
            var verts = new List<Vector3> { centre + Vector3.up * 0.0041f }; var uvs = new List<Vector2> { new Vector2(centre.x / tile.x, centre.z / tile.y) }; var tris = new List<int>();
            foreach (var r in rim) { verts.Add(r + Vector3.up * 0.0041f); uvs.Add(new Vector2(r.x / tile.x, r.z / tile.y)); }
            for (int i = 0; i < n; i++) tris.AddRange(new[] { 0, 1 + (i + 1) % n, 1 + i });
            var mesh = new Mesh { name = $"Earth patch {ribbonCount++}" };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals();
            if (mesh.normals[0].y < 0f) { tris.Reverse(); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); }
            mesh.RecalculateBounds();
            AssetDatabase.AddObjectToAsset(mesh, ribbonContainer);
            var go = new GameObject("Earth patch"); go.transform.SetParent(parent, false); go.transform.position = Vector3.zero;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = Graded(fill, RoadTint); mr.shadowCastingMode = ShadowCastingMode.Off;
            rim.Add(rim[0]);
            string edge = "road_edge_hd";
            float dep = T.Metres(edge).y;
            Band(parent, "Earth patch rim", Graded(edge, RoadTint, clip: true), rim, Fill(rim.Count, -0.6f), Fill(rim.Count, dep - 0.6f), 0.0038f, T.Metres(edge).x, vAcross: true);
        }

        // ---------- names, people, camera, HUD ----------

        static void Locations()
        {
            var g = new GameObject("Location zones").transform;
            void Box2(string major, string minor, float x0, float x1, float z0, float z1, int priority, bool ride = false)
            {
                var z = new GameObject($"{major} · {minor}").AddComponent<LocationZone>();
                z.transform.SetParent(g, false);
                z.transform.position = new Vector3((x0 + x1) / 2f, 0, (z0 + z1) / 2f);
                z.Major = major; z.Minor = minor; z.Priority = priority; z.Size = new Vector2(x1 - x0, z1 - z0); z.Ride = ride;
            }
            Box2("中山", "野外", XMin - 50f, XMax + 50f, ZMin - 50f, ZMax + 50f, 0, ride: true);
            Box2("中山", "滹沱河滩", XMin, XMax, 20f, 100f, 1, ride: true);
            Box2("安喜", "城外", ZhongshanLayout.TownX0 - 8f, ZhongshanLayout.TownX1 + 8f, ZhongshanLayout.TownZ0 - 8f, ZhongshanLayout.TownZ1 + 8f, 2);
            Box2("安喜", "南门", 270f, 286f, 118f, 132f, 3);
            foreach (var l in ZhongshanLayout.Locations)
            {
                if (l.kind == "town_gate" || l.kind == "civic" || l.kind == "town" || l.kind == "region_exit" || l.kind == "ferry_boat") continue;
                float r = l.kind == "village" ? 22f : l.kind == "blocked_pass" ? 14f : 12f;
                string minor = l.name.Split('（')[0];
                Box2("中山", minor, l.at.x - r, l.at.x + r, l.at.z - r, l.at.z + r, 3);
            }
            foreach (var road in AllRoads)
                for (int i = 0; i + 1 < road.points.Length; i++)
                {
                    var z = new GameObject($"中山 · 道路").AddComponent<LocationZone>();
                    z.transform.SetParent(g, false);
                    z.Major = "中山"; z.Minor = RoadName(road.id); z.Priority = 2; z.IsSegment = true; z.A = road.points[i]; z.B = road.points[i + 1]; z.HalfWidth = 9f; z.Ride = true;
                }
        }

        static string RoadName(string id) => id switch
        {
            "west_gate_road" or "link_southwest" => "安喜 ↔ 西岔口",
            "station_approach" => "西岔口 ↔ 驿站",
            "terrace_village_road" or "west_fork_terraces" => "驿站 ↔ 石坎村",
            "feihu_approach" or "feihu_last_approach" => "石坎村 ↔ 飞狐陉",
            "shrine_trail" => "梯田岔口 ↔ 石窟小庙",
            "beacon_trail" => "飞狐关吏 ↔ 古烽燧",
            "station_trail" or "jingxing_approach" or "jingxing_last_approach" => "驿站 ↔ 井陉口",
            "north_gate_bridge" or "north_exit" or "link_north" => "安喜北 ↔ 往卢奴",
            "east_exit" or "link_southeast" => "安喜东 ↔ 往河间",
            "south_gate_ferry_road" or "ferry_north_approach" => "安喜南门 ↔ 滹沱河渡口",
            "river_village_road" => "南岔口 ↔ 河湾村",
            "zhending_road" => "南岸渡头 ↔ 往真定",
            "shipwreck_gravel_trail" => "南岸渡头 ↔ 河滩沉船",
            _ => "道路",
        };

        static MountRider AddRider(GameObject character, string key)
        {
            var rider = character.AddComponent<MountRider>();
            var walkAnimator = character.GetComponentInChildren<SpriteFrameAnimator>();
            rider.WalkSprite = walkAnimator.gameObject;
            rider.Walk = character.GetComponent<PixelWalkFrames>();
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(MountedImporter.AtlasPath(key));
            if (!atlas) return rider;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.name = "Mounted sprite";
            quad.transform.SetParent(character.transform, false);
            float size = MountedImporter.FrameSize / HD2DSampleBuilder.PixelsPerUnit;
            quad.transform.localPosition = new Vector3(0, size / 2f, 0);
            quad.transform.localScale = new Vector3(size, size, 1);
            var r = quad.GetComponent<MeshRenderer>();
            r.sharedMaterial = HD2DSampleBuilder.PixelMaterial(key + "Mounted", atlas);
            r.shadowCastingMode = ShadowCastingMode.On;
            quad.AddComponent<PixelBillboard>().BaseSize = new Vector2(size, size);
            var anim = quad.AddComponent<SpriteFrameAnimator>();
            anim.Target = r; anim.Columns = MountedImporter.Columns * MountedImporter.Rows; anim.Rows = 1; anim.Frame = MountRider.MountStart;
            rider.RideAnimator = anim;
            quad.SetActive(false);
            return rider;
        }

        // Stand-ins from the Zhuojun residents: the post-station courier, villagers, the ferryman, the guards.
        static void People(Transform people)
        {
            var g = T.Group(people, "Stand-ins");
            void Npc(string name, string id, Vector3 at, int frame = 0)
            {
                var npc = HD2DSampleBuilder.Character(name, ResidentImporter.KeyFor(id), at, frame, isPlayer: false);
                npc.transform.SetParent(g, true);
            }
            var post = Loc("post_station");
            Npc("驿卒", "N07", post + new Vector3(2.5f, 0, 3.2f));
            var tv = Loc("terrace_village");
            Npc("石坎村 老农", "N09", tv + new Vector3(-3f, 0, 2f));
            Npc("石坎村 妇人", "N08", tv + new Vector3(5f, 0, 1f));
            var rv = Loc("river_village");
            Npc("河湾村 渔人", "N12", rv + new Vector3(2f, 0, 1.5f));
            Npc("河湾村 孩童", "N10", rv + new Vector3(-2f, 0, -1f));
            var fn = ZhongshanLayout.Bridges.First(b => b.id == "hutuo_north_landing").points;
            Npc("渡口船夫", "N12", fn[0] + new Vector3(3.2f, 0, -1f));
            Npc("井陉哨卡 守兵", "N01", Loc("checkpoint_jingxing") + new Vector3(2f, 0, 2.5f), 1);
            Npc("飞狐关吏", "N03", Loc("checkpoint_feihu") + new Vector3(0.5f, 0, 2.2f));
            Npc("安喜南门 守卫", "N01", new Vector3(282.5f, 0, 127.5f));
        }

        static (VolumeProfile high, VolumeProfile low) DaylightProfiles()
        {
            VolumeProfile Make(string src, string dst)
            {
                AssetDatabase.DeleteAsset(dst);
                AssetDatabase.CopyAsset(src, dst);
                var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(dst);
                if (p.TryGet<ColorLookup>(out var lut)) lut.contribution.Override(0.2f);
                if (p.TryGet<ColorAdjustments>(out var c)) { c.postExposure.Override(-0.15f); c.saturation.Override(24f); c.contrast.Override(16f); }
                if (p.TryGet<Vignette>(out var v)) v.intensity.Override(0.14f);
                var wb = p.TryGet<WhiteBalance>(out var w) ? w : p.Add<WhiteBalance>(true);
                if (!AssetDatabase.IsSubAsset(wb)) AssetDatabase.AddObjectToAsset(wb, p);
                wb.temperature.Override(-2f);
                EditorUtility.SetDirty(p);
                return p;
            }
            var high = Make(HD2DSampleBuilder.HighProfilePath, ArtDir + "/Zhongshan_Daylight_High.asset");
            var low = Make(HD2DSampleBuilder.LowProfilePath, ArtDir + "/Zhongshan_Daylight_Low.asset");
            AssetDatabase.SaveAssets();
            return (high, low);
        }

        static void Camera(Transform player)
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<UnityEngine.Camera>();
            cam.fieldOfView = HD2DSampleBuilder.CameraFov;
            cam.nearClipPlane = 1f; cam.farClipPlane = 900f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.None;
            Quaternion pitch = Quaternion.Euler(HD2DSampleBuilder.CameraPitch, 0f, 0f);
            camGo.transform.rotation = pitch;
            var follow = camGo.AddComponent<FollowCamera>();
            follow.Target = player;
            follow.Offset = new Vector3(0, 0.8f, 0) + pitch * new Vector3(0, 0, -HD2DSampleBuilder.CameraDistance);
            camGo.transform.position = player.position + follow.Offset;
            follow.UseRig = true;
            follow.Pitch = HD2DSampleBuilder.CameraPitch; follow.Distance = HD2DSampleBuilder.CameraDistance; follow.Fov = HD2DSampleBuilder.CameraFov;
            camGo.AddComponent<SeeThroughOccluders>().Target = player;

            var (high, low) = DaylightProfiles();
            var volumeGo = new GameObject("Global Volume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = high;
            var tier = volumeGo.AddComponent<HD2DQualityTier>();
            tier.Volume = volume; tier.High = high; tier.Low = low;
            follow.FocusVolume = volume;

            var zones = new GameObject("Camera zones").transform;
            void Zone(string name, float x0, float x1, float z0, float z1, float pitchDeg, float distance, float fov = 20f, int priority = 0)
            {
                var z = new GameObject(name).AddComponent<CameraZone>();
                z.transform.SetParent(zones, false);
                z.transform.position = new Vector3((x0 + x1) / 2f, 0, (z0 + z1) / 2f);
                z.Size = new Vector2(x1 - x0, z1 - z0);
                z.Pitch = pitchDeg; z.Distance = distance; z.Fov = fov; z.LookOffset = new Vector3(0, 0.8f, 0); z.Priority = priority;
            }
            Zone("Open country", XMin, XMax, ZMin, ZMax, 32f, 40f, fov: 21f);
            Zone("Anxi south gate", 262f, 294f, 112f, 134f, 26f, 32f, priority: 1);
            Zone("Ferry", 255f, 290f, 44f, 92f, 34f, 42f, priority: 1);
            Zone("Mountain roads", 80f, 175f, 60f, 240f, 36f, 42f, fov: 21f, priority: 1);

            var over = new GameObject("Review Overview").AddComponent<UnityEngine.Camera>();
            over.enabled = false;
            over.fieldOfView = 34f; over.farClipPlane = 3000f;
            over.transform.SetPositionAndRotation(new Vector3(200f, 360f, -300f), Quaternion.Euler(42f, 0f, 0f));
            var gate = new GameObject("Review Overview Anxi").AddComponent<UnityEngine.Camera>();
            gate.enabled = false;
            gate.fieldOfView = 30f; gate.farClipPlane = 1200f;
            gate.transform.SetPositionAndRotation(new Vector3(278f, 70f, 60f), Quaternion.Euler(32f, 0f, 0f));
        }

        static readonly (string name, Vector3 at)[] MapNames =
        {
            ("安喜", new Vector3(278f, 0, 200f)), ("驿站", new Vector3(140f, 0, 142f)), ("石坎村", new Vector3(148f, 0, 184f)), ("河湾村", new Vector3(311f, 0, 104f)),
            ("渡口", new Vector3(262f, 0, 80f)), ("石窟庙", new Vector3(94f, 0, 182f)), ("古烽燧", new Vector3(151f, 0, 236f)), ("沉船", new Vector3(351f, 0, 42f)),
            ("井陉口", new Vector3(87f, 0, 90f)), ("飞狐陉", new Vector3(84f, 0, 214f)), ("滹沱河", new Vector3(170f, 0, 60f)),
        };

        static void Hud(Transform player)
        {
            var go = new GameObject("Region HUD");
            var doc = go.AddComponent<UnityEngine.UIElements.UIDocument>();
            doc.panelSettings = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>("Assets/_Project/Settings/BattlePanelSettings.asset");
            if (!doc.panelSettings) throw new System.InvalidOperationException("Region HUD has no PanelSettings");
            var hud = go.AddComponent<ThreeKingdoms.UI.Region.RegionHud>();
            Texture2D Painted(string file)
            {
                string path = MinimapDir + "/" + file;
                if (!File.Exists(path)) return null;
                AssetDatabase.ImportAsset(path);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                imp.textureType = TextureImporterType.Default; imp.mipmapEnabled = true; imp.npotScale = TextureImporterNPOTScale.None;
                imp.textureCompression = TextureImporterCompression.Uncompressed; imp.filterMode = FilterMode.Trilinear; imp.wrapMode = TextureWrapMode.Clamp;
                imp.maxTextureSize = 4096;
                imp.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            var painted = Painted("zhongshan_region_map.png");
            hud.Map = painted; hud.FullMap = Painted("zhongshan_region_map_labeled.png") ?? painted;
            hud.Player = player; hud.RegionName = "中山";
            hud.MapBounds = new Vector4(XMin, ZMin, XMax, ZMax);
            hud.PlaceNames = MapNames.Select(n => n.name).ToArray();
            hud.PlacePositions = MapNames.Select(n => n.at).ToArray();
        }
    }
}
