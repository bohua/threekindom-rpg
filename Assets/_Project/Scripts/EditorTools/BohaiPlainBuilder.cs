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
    // Chapter 1 open-region art probe (design/chapter1-rise/ART_PROBE_BRIEF.md): R4 Bohai plain with the Pingyuan base,
    // built like Zhuojun (boxes and quads carrying the 30 px/m native textures, props as pixel billboards). Step one is
    // the whitebox of the whole region (grey boxes plus textures that already exist); the layout is
    // production/levels/bohai-plain/layout.md.
    //
    // Metres, x east, z north, camera looks north. The Yellow River runs along the south edge (water z -322..-252),
    // the levee (the region's landmark) on its north bank (base z -244..-212, crest z -232..-224 at 5 m).
    //   Pingyuan city walls x -110..-10, z -195..-115, gates on all four sides; the ferry right below its south gate.
    //   Gaotang gate scene (330, -150); post station (460, 10) and the Jinan-road checkpoint (545, 20) in the east;
    //   villages west (-380, 40) and north (40, 200); beacon ruin (-330, -170), reed marsh (220, -70), tomb mound (-220, 260).
    public static class BohaiPlainBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Bohai_Plain.unity";
        public const string ArtDir = "Assets/_Project/Art/Environment/BohaiPlain";
        // Pingyuan's own textures (Codex pingyuan-b1/-b2), searched first.
        public const string PingyuanDir = ArtDir + "/Pingyuan";
        // Open-country textures and props (Codex bohai-wild-b1).
        public const string WildDir = ArtDir + "/Wild";
        public const string HdDir = ArtDir + "/HD";
        const string MaterialDir = ArtDir + "/Materials";
        const string MeshPath = ArtDir + "/Bohai_Meshes.asset";

        public const float XMin = -560f, XMax = 560f, ZMin = -330f, ZMax = 340f;
        public const float LeveeH = 5f, LeveeNorth = -212f, LeveeCrestN = -224f, LeveeCrestS = -232f, LeveeSouth = -244f;
        public const float WaterN = -252f, WaterS = -322f, WaterY = -0.6f;
        // Pingyuan city
        public const float CityX0 = -110f, CityX1 = -10f, CityZ0 = -195f, CityZ1 = -115f, CityCx = -60f;
        public static readonly Vector3 PlayerStart = new Vector3(CityCx, 0f, -151f);   // the county office square

        static readonly Dictionary<float, Material> greys = new Dictionary<float, Material>();
        static Material water;

        [MenuItem("JRPG/Build Bohai Plain (art probe)")]
        public static void Build()
        {
            greys.Clear(); water = null;
            MountedImporter.Import();
            // High-res painted ground replaces the pixel set wherever Codex has delivered it (user 2026-10-25).
            T.HdAliases.Clear();
            foreach (var (pixel, hd) in HdGround) T.HdAliases[pixel] = hd;
            T.BeginSession(MaterialDir, MeshPath, HdDir, PingyuanDir, WildDir, ArtDir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            // Bright midday (user 2026-10-25: the plain looked like dawn or dusk): cooler, brighter sky light and a pale blue
            // haze instead of Zhuojun's warm late-afternoon set; the town scenes keep theirs.
            RenderSettings.ambientSkyColor = new Color(0.88f, 0.93f, 1.05f);
            RenderSettings.ambientEquatorColor = new Color(0.80f, 0.80f, 0.76f);
            RenderSettings.ambientGroundColor = new Color(0.46f, 0.42f, 0.34f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.0022f;
            RenderSettings.fogColor = new Color(0.80f, 0.87f, 0.94f);

            var env = new GameObject("Environment").transform;
            Ground(env);
            LoadMask();
            Roads(env);
            River(env);
            Levee(env);
            LeveeDressing(env);
            var labels = new GameObject("Whitebox labels").transform;
            PingyuanCity(env, labels);
            Ferry(env, labels);
            Gaotang(env, labels);
            PostStationAndBarrier(env, labels);
            Village(env, labels, "西村", new Vector3(-380f, 0, 40f), 11);
            Village(env, labels, "北村", new Vector3(40f, 0, 200f), 17);
            Explore(env, labels);
            Exits(env, labels);
            Terrain(env, labels);
            SplatGround(env);
            GroundVariety(env);
            EdgeDecals(env);
            Signposts(env, labels);
            Scatter(env);
            T.EndSession();

            var sun = new GameObject("Midday sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.98f, 0.93f);
            sun.intensity = 2.35f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.65f;
            sun.transform.rotation = Quaternion.Euler(58f, -28f, 0f);

            var map = BakeMinimap();
            Locations();

            var people = new GameObject("People").transform;
            var player = HD2DSampleBuilder.Character("Liu Bei", "LiuBei", PlayerStart, 3, isPlayer: true);
            player.transform.SetParent(people, true);
            var trail = player.AddComponent<PartyTrail>();
            trail.Length = 24f;   // room for two horses spaced a horse length apart
            int slot = 1;
            var companions = new List<MountRider>();
            foreach (var (name, key) in new[] { ("Guan Yu (follower)", "GuanYu"), ("Zhang Fei (follower)", "ZhangFei") })
            {
                var go = HD2DSampleBuilder.Character(name, key, PlayerStart + new Vector3(0, 0, -1.5f * slot), 0, isPlayer: false);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.AddComponent<PixelWalkFrames>().Animator = go.GetComponentInChildren<SpriteFrameAnimator>();
                var f = go.AddComponent<PartyFollower>();
                f.Trail = trail; f.Slot = slot++;
                go.transform.SetParent(people, true);
                companions.Add(AddRider(go, key));
            }
            // Mount on roads and open country, dismount in towns and at places (user 2026-10-24).
            var ride = player.AddComponent<RideController>();
            ride.Mover = player.GetComponent<PlayerMover>();
            ride.Leader = AddRider(player, "LiuBei");
            ride.Companions = companions.ToArray();
            People(people);

            Camera(player.transform);
            Hud(map, player.transform);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("BOHAI_PLAIN_BUILT");
        }

        public static void BuildFromCommandLine()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        // ---------- helpers ----------

        // Untextured whitebox material, one per shade (0 dark .. 1 light).
        static Material Grey(float shade)
        {
            if (greys.TryGetValue(shade, out var m)) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = $"wb_grey_{shade:0.00}" };
            float v = Mathf.Lerp(0.35f, 0.82f, shade);
            m.SetColor("_BaseColor", new Color(v, v * 0.98f, v * 0.95f));
            m.SetFloat("_Smoothness", 0f);
            m.SetFloat("_SpecularHighlights", 0f); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            Directory.CreateDirectory(MaterialDir);
            string path = $"{MaterialDir}/{m.name}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            return greys[shade] = m;
        }

        // A grey block standing on the ground (or at y), with a collider; tall ones cut away in front of the player.
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

        // A grey building with a pitched grey roof block and a darker door, front facing south (rotated by yaw).
        static Transform GreyHouse(Transform parent, string name, Vector3 frontCentre, float width, float depth, float wall, float yaw = 0f, float shade = 0.62f)
        {
            var g = T.Group(parent, name, frontCentre, yaw);
            Box(g, "Walls", new Vector3(0, 0, depth / 2f), new Vector3(width, wall, depth), shade);
            Box(g, "Roof", new Vector3(0, wall, depth / 2f), new Vector3(width + 0.8f, wall * 0.45f, depth * 0.72f), shade - 0.25f, solid: false);
            Box(g, "Door", new Vector3(0, 0, -0.05f), new Vector3(Mathf.Min(2.2f, width * 0.3f), Mathf.Min(2.6f, wall * 0.8f), 0.12f), 0.15f, solid: false);
            return g;
        }

        static void Label(Transform labels, string text, Vector3 at, float height = 8f)
        {
            var go = new GameObject("Label " + text);
            go.transform.SetParent(labels, false);
            go.transform.position = at;
            var l = go.AddComponent<WhiteboxLabel>();
            l.Text = text; l.Height = height;
        }

        static void Spot(string name, Vector3 at) => new GameObject("Spot " + name).transform.position = at;

        // A ground strip from a to b (any direction), width w, lying at y. Texture "up" runs along the strip.
        static void Strip(Transform parent, string name, string key, Vector3 a, Vector3 b, float w, float y)
        {
            var d = b - a; d.y = 0;
            float len = d.magnitude;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            var size = new Vector2(w, len + w * 0.5f);   // overlap at the bends
            T.Quad(parent, name, (a + b) / 2f + new Vector3(0, y, 0), new Vector3(90f, yaw, 0), size, T.Mat(key), T.Repeat(key, size), castShadows: false);
        }

        static void Road(Transform parent, string name, float width, params Vector3[] points)
        {
            var g = T.Group(parent, name);
            for (int i = 0; i + 1 < points.Length; i++)
            {
                Strip(g, "Road", K("dirt_road_v2", "ground_packed_earth"), points[i], points[i + 1], width, 0.004f + i * 0.0002f);
                if (width >= 7f && T.Tex("ground_earth_ruts", optional: true) && !T.Tex("dirt_road_v2", optional: true))
                    Strip(g, "Ruts", "ground_earth_ruts", points[i], points[i + 1], width * 0.55f, 0.006f + i * 0.0002f);
            }
        }

        static void Solid(Transform parent, Vector3 centre, Vector3 size, float yaw = 0f)
        {
            var go = new GameObject("Collider");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            go.AddComponent<BoxCollider>().size = size;
        }

        // ---------- ground, roads, river, levee ----------

        static void Ground(Transform env)
        {
            var g = T.Group(env, "Ground");
            T.Patch(g, "Plain grass", "ground_grass", XMin - 60f, XMax + 60f, WaterN, ZMax + 80f, -0.01f);
            // Yellower dry plain in broad bands so the ground is not one texture end to end.
            var rng = new System.Random(5);
            for (int i = 0; i < 26 && !T.Tex("decal_dry_grass_a", optional: true); i++)
            {
                float x = XMin + (float)rng.NextDouble() * (XMax - XMin), z = -200f + (float)rng.NextDouble() * 520f;
                float w = 40f + (float)rng.NextDouble() * 90f, d = 25f + (float)rng.NextDouble() * 50f;
                T.Patch(g, "Dry plain", "wb_plain_grass", x - w / 2f, x + w / 2f, z - d / 2f, z + d / 2f, -0.008f + i * 0.00005f);
            }
            var floor = new GameObject("Ground collider").AddComponent<BoxCollider>();
            floor.transform.SetParent(g, false);
            floor.center = new Vector3(0, -0.05f, (ZMin + ZMax) / 2f); floor.size = new Vector3(XMax - XMin + 200f, 0.1f, ZMax - ZMin + 200f);
            // Region bounds: the west road and north road leave through exits (world map, not built); the south is the
            // river, the east the Jinan-road checkpoint.
            Solid(g, new Vector3(XMin - 0.5f, 3f, 0), new Vector3(1f, 6f, 800f));
            Solid(g, new Vector3(XMax + 0.5f, 3f, 0), new Vector3(1f, 6f, 800f));
            Solid(g, new Vector3(0, 3f, ZMax + 0.5f), new Vector3(1200f, 6f, 1f));
        }

        static readonly Vector3 WestGate = new Vector3(CityX0, 0, -155f), EastGate = new Vector3(CityX1, 0, -155f),
            NorthGate = new Vector3(CityCx, 0, CityZ1), SouthGate = new Vector3(CityCx, 0, CityZ0);
        static readonly Vector3 GaotangFront = new Vector3(330f, 0, -176f), PostFront = new Vector3(460f, 0, 6f);

        static void Roads(Transform env)
        {
            var g = T.Group(env, "Roads");
            BeginRibbons();
            // Dirt spurs first, so their junctions are known when the stone roads lay their curbs.
            DirtRibbon(g, "Spur to the beacon", 5f, BeaconSpur);
            DirtRibbon(g, "Spur to the reeds", 5f, ReedSpur);
            DirtRibbon(g, "Spur to the tomb", 5f, TombSpur);
            DirtRibbon(g, "Lane in the north village", 5f, ExtraRoads[0]);
            // The ferry road: south gate down to the levee foot (the ramp over the levee is laid in Levee()).
            DirtRibbon(g, "Ferry road", 7f, ExtraRoads[1]);
            StoneRoad(g, "West road", 8f, WestRoad);
            StoneRoad(g, "North road", 8f, NorthRoad);
            StoneRoad(g, "East road", 8f, EastRoad);
            StoneRoad(g, "Gaotang to the post station", 8f, PostRoad);
            AssetDatabase.SaveAssets();
        }

        static void River(Transform env)
        {
            var g = T.Group(env, "Yellow River");
            if (!water)
            {
                water = new Material(T.Mat(K("yellow_river_v2", K("water_yellow_river", "wb_water_muddy")))) { name = "yellow_river_water" };
                water.SetFloat("_Smoothness", 0.55f);
                water.SetFloat("_SpecularHighlights", 1f); water.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
                string path = $"{MaterialDir}/{water.name}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(path)) AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(water, path);
            }
            var size = new Vector2(XMax - XMin + 200f, WaterN - WaterS);
            T.Quad(g, "Water", new Vector3(0, WaterY, (WaterN + WaterS) / 2f), new Vector3(90f, 0, 0), size, water, T.Repeat(K("yellow_river_v2", K("water_yellow_river", "wb_water_muddy")), size), castShadows: false);
            // Mud shelf from the levee foot down into the water, and the far (south) bank: R6, not open.
            float shelf = WaterN - 1f - LeveeSouth;   // negative run southward
            var mud = new Vector2(XMax - XMin + 200f, Mathf.Sqrt(shelf * shelf + WaterY * WaterY));
            T.Quad(g, "Mud shelf", new Vector3(0, WaterY / 2f, (LeveeSouth + WaterN - 1f) / 2f), new Vector3(90f - Mathf.Atan2(-WaterY, -shelf) * Mathf.Rad2Deg, 0, 0), mud,
                T.Mat(K("sand_flat_v2", K("river_mud_shelf", "wb_sand_bank"))), T.Repeat(K("sand_flat_v2", K("river_mud_shelf", "wb_sand_bank")), mud), castShadows: false);
            T.Patch(g, "Far bank", "wb_sand_bank", XMin - 100f, XMax + 100f, WaterS - 12f, WaterS + 1f, WaterY + 0.25f);
            T.Patch(g, "Far fields", "wb_plain_grass", XMin - 100f, XMax + 100f, WaterS - 120f, WaterS - 11f, WaterY + 0.3f);
            // Keep the player out of the mud and water except on the ferry dock (gap x -64..-56).
            Solid(g, new Vector3((XMin - 64f) / 2f, 2f, LeveeSouth - 1f), new Vector3(-64f - XMin, 4f, 1f));
            Solid(g, new Vector3((XMax - 56f) / 2f, 2f, LeveeSouth - 1f), new Vector3(XMax + 56f, 4f, 1f));
        }

        // The Yellow River levee: a 5 m earthen bank the length of the region, its crest a dike road. Walkable all along
        // (slopes 23°, under the controller's 50° limit); the ferry road climbs it in line with Pingyuan's south gate.
        static void Levee(Transform env)
        {
            var g = T.Group(env, "Levee (landmark)");
            float len = XMax - XMin + 200f;
            float nRun = LeveeNorth - LeveeCrestN, sRun = LeveeCrestS - LeveeSouth;
            float nLen = Mathf.Sqrt(nRun * nRun + LeveeH * LeveeH), sLen = Mathf.Sqrt(sRun * sRun + LeveeH * LeveeH);
            float nDeg = Mathf.Atan2(LeveeH, nRun) * Mathf.Rad2Deg, sDeg = Mathf.Atan2(LeveeH, sRun) * Mathf.Rad2Deg;
            var crest = new Vector2(len, LeveeCrestN - LeveeCrestS);
            // The dike road along the crest: the V2 dirt road (8 m wide, tiling along its length) laid east–west.
            if (T.Tex("dirt_road_v2", optional: true))
                T.Quad(g, "Crest road", new Vector3(0, LeveeH, (LeveeCrestN + LeveeCrestS) / 2f), new Vector3(90f, 90f, 0), new Vector2(crest.y, crest.x), T.Mat("dirt_road_v2"),
                    new Vector2(crest.y / T.Metres("dirt_road_v2").x, crest.x / T.Metres("dirt_road_v2").y), castShadows: false);
            else
                T.Quad(g, "Crest road", new Vector3(0, LeveeH, (LeveeCrestN + LeveeCrestS) / 2f), new Vector3(90f, 0, 0), crest, T.Mat("ground_packed_earth"), T.Repeat("ground_packed_earth", crest), castShadows: false);
            var ns = new Vector2(len, nLen);
            T.Quad(g, "North slope", new Vector3(0, LeveeH / 2f, (LeveeNorth + LeveeCrestN) / 2f), new Vector3(90f + nDeg, 0, 0), ns, T.Mat(K("plain_grass_b", K("levee_slope", "ground_grass"))), T.Repeat(K("plain_grass_b", K("levee_slope", "ground_grass")), ns));
            var ss = new Vector2(len, sLen);
            T.Quad(g, "South slope", new Vector3(0, LeveeH / 2f, (LeveeCrestS + LeveeSouth) / 2f), new Vector3(90f - sDeg, 0, 0), ss, T.Mat(K("plain_grass_dry", K("levee_slope", "wb_levee_face"))), T.Repeat(K("plain_grass_dry", K("levee_slope", "wb_levee_face")), ss));
            // Colliders: the crest block and one tilted slab under each slope.
            Solid(g, new Vector3(0, LeveeH / 2f, (LeveeCrestN + LeveeCrestS) / 2f), new Vector3(len, LeveeH, LeveeCrestN - LeveeCrestS));
            SlopeSolid(g, "North slope collider", (LeveeNorth + LeveeCrestN) / 2f, LeveeH / 2f, nLen, nDeg, len);
            SlopeSolid(g, "South slope collider", (LeveeCrestS + LeveeSouth) / 2f, LeveeH / 2f, sLen, -sDeg, len);
            // Rammed-earth bank with a stone toe along the river side's foot (art/bohai-water-v2 shore_embankment_band).
            if (T.Tex("shore_embankment_band", optional: true))
            {
                float dep = T.Metres("shore_embankment_band").y, t = (dep / 2f) / sLen;
                var band = new Vector2(len, dep);
                T.Quad(g, "Embankment band", new Vector3(0, LeveeH * t + 0.03f, LeveeSouth + (LeveeCrestS - LeveeSouth) * t), new Vector3(90f - sDeg, 0, 0), band,
                    T.Mat("shore_embankment_band", clip: true), new Vector2(len / T.Metres("shore_embankment_band").x, 1f), castShadows: false);
            }
            // Ferry ramps: the ferry road laid over both slopes in line with the gate.
            var ramp = new Vector2(7f, nLen);
            T.Quad(g, "Ferry ramp N", new Vector3(CityCx, LeveeH / 2f + 0.02f, (LeveeNorth + LeveeCrestN) / 2f), new Vector3(90f + nDeg, 0, 0), ramp, T.Mat("ground_packed_earth"), T.Repeat("ground_packed_earth", ramp), castShadows: false);
            var rampS = new Vector2(7f, sLen);
            T.Quad(g, "Ferry ramp S", new Vector3(CityCx, LeveeH / 2f + 0.02f, (LeveeCrestS + LeveeSouth) / 2f), new Vector3(90f - sDeg, 0, 0), rampS, T.Mat("ground_packed_earth"), T.Repeat("ground_packed_earth", rampS), castShadows: false);
        }

        // Near the ferry the levee foot gets its fieldstone toe and the crest its timber rail (concept art), gaps at the ramp.
        static void LeveeDressing(Transform env)
        {
            var g = T.Group(env, "Levee dressing at the ferry");
            float x0 = CityCx - 70f, x1 = CityCx + 70f, gap = 4.5f;
            foreach (var (a, b) in new[] { (x0, CityCx - gap), (CityCx + gap, x1) })
            {
                if (T.Tex("levee_stone_toe", optional: true))
                {
                    float h = T.Metres("levee_stone_toe").y;
                    var size = new Vector2(b - a, h);
                    T.Quad(g, "Stone toe", new Vector3((a + b) / 2f, h / 2f, LeveeNorth - 0.02f), Vector3.zero, size, T.Mat("levee_stone_toe"), new Vector2(size.x / T.Metres("levee_stone_toe").x, 1f));
                }
                string rail = K("levee_fence", "fence_wattle");
                var r = new Vector2(b - a, T.Metres(rail).y);
                foreach (float z in new[] { LeveeCrestN - 0.3f, LeveeCrestS + 0.3f })
                    T.Quad(g, "Crest rail", new Vector3((a + b) / 2f, LeveeH + r.y / 2f, z), Vector3.zero, r, T.Mat(rail, clip: true, twoSided: true), T.Repeat(rail, r));
            }
        }

        // A thin slab whose top face lies on a slope rising toward the crest (tilt in degrees about x).
        static void SlopeSolid(Transform parent, string name, float z, float y, float length, float tilt, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(tilt, 0, 0);
            go.transform.localPosition = new Vector3(0, y, z) + go.transform.localRotation * new Vector3(0, -0.5f, 0);
            go.AddComponent<BoxCollider>().size = new Vector3(width, 1f, length);
        }

        // ---------- Pingyuan (refined corner) ----------
        // One city, one look (user 2026-10-24): Pingyuan follows its own approved concept art (art/pingyuan-concept),
        // not Zhuojun. A flood-wary ferry town: buildings on 1 m rammed-earth plinths with fieldstone toes, pale loess
        // walls, golden reed thatch (grey tiles only on the county office), open drains with plank bridges along the
        // streets, a congee shed on the office square, a low loess town wall with timber-and-thatch gates. Textures
        // come from Codex batches pingyuan-b1/-b2; until a file is in, the whitebox stand-in or a Zhuojun texture fills.

        const float PlinthH = 0.99f, WallTk = 3f, TownWallH = 3.5f;

        // The Pingyuan texture if delivered, else the fallback.
        static readonly (string pixel, string hd)[] HdGround =
        {
            ("plain_grass_a", "plain_grass_hd_a"), ("plain_grass_b", "plain_grass_hd_b"), ("plain_grass_dry", "plain_grass_dry_hd"),
            ("dirt_road_v2", "dirt_road_hd"), ("stone_road_v2", "stone_road_hd"), ("road_edge_band", "road_edge_hd"),
            ("gaotang_square", "gaotang_square_hd"), ("ground_packed_earth", "packed_earth_hd"),
            ("plain_meadow", "plain_grass_hd_b"), ("plain_loess", "packed_earth_hd"),
        };

        static string K(string key, string fallback) => T.Tex(key, optional: true) ? key : fallback;
        static string Thatch => T.Tex("roof_thatch_gold", optional: true) ? "roof_thatch_gold" : null;

        static void PingyuanCity(Transform env, Transform labels)
        {
            var c = T.Group(env, "Pingyuan city");
            T.Patch(c, "City earth", "ground_packed_earth", CityX0 + 1.5f, CityX1 - 1.5f, CityZ0 + 1.5f, CityZ1 - 1.5f, 0.002f);
            // Low loess walls with 8 m gaps for the four gates.
            LoessWall(c, "South wall W", new Vector3(CityX0, 0, CityZ0), new Vector3(CityCx - 4f, 0, CityZ0));
            LoessWall(c, "South wall E", new Vector3(CityCx + 4f, 0, CityZ0), new Vector3(CityX1, 0, CityZ0));
            LoessWall(c, "North wall W", new Vector3(CityX0, 0, CityZ1), new Vector3(CityCx - 4f, 0, CityZ1));
            LoessWall(c, "North wall E", new Vector3(CityCx + 4f, 0, CityZ1), new Vector3(CityX1, 0, CityZ1));
            LoessWall(c, "West wall S", new Vector3(CityX0, 0, CityZ0), new Vector3(CityX0, 0, WestGate.z - 4f));
            LoessWall(c, "West wall N", new Vector3(CityX0, 0, WestGate.z + 4f), new Vector3(CityX0, 0, CityZ1));
            LoessWall(c, "East wall S", new Vector3(CityX1, 0, CityZ0), new Vector3(CityX1, 0, EastGate.z - 4f));
            LoessWall(c, "East wall N", new Vector3(CityX1, 0, EastGate.z + 4f), new Vector3(CityX1, 0, CityZ1));
            TownGate(c, "South gate", SouthGate, 0f);
            TownGate(c, "North gate", NorthGate, 180f);
            TownGate(c, "West gate", WestGate, 90f);
            TownGate(c, "East gate", EastGate, -90f);
            Label(labels, "平原城（据点）", new Vector3(CityCx, 0, CityZ1 + 2f), 14f);

            // County office square (36 × 27 m, kept clear) and the main street down to the south gate, paved in loess
            // stone, with open drains and plank bridges either side of the street.
            string paving = K("ground_loess_paving", "ground_street_stone");
            T.Patch(c, "Office square", paving, CityCx - 14f, CityCx + 14f, -162f, -140.5f, 0.006f);
            T.Patch(c, "Main street", paving, CityCx - 3f, CityCx + 3f, CityZ0 + 1.5f, -162f, 0.005f);
            Drain(c, "Drain W", CityCx - 4.2f, CityZ0 + 2.5f, -163f, new[] { -186f, -174f });
            Drain(c, "Drain square W", CityCx - 15.2f, -162f, -142f, new[] { -151f });
            Drain(c, "Drain E", CityCx + 4.2f, CityZ0 + 2.5f, -163f, new[] { -186f, -174f });
            Drain(c, "Drain square E", CityCx + 15.2f, -162f, -142f, new[] { -151f });
            Label(labels, "县衙（相府）·门前广场", new Vector3(CityCx, 0, -140f), 9f);

            // County office: low and wide, five bays, the only grey-tiled roof in town; three shallow steps up its plinth.
            var hallKey = K("pingyuan_hall_front", "yamen_hall_front");
            var hallSide = K("pingyuan_hall_side", "yamen_hall_side");
            OnPlinth(c, "County office (县衙)", new Vector3(CityCx, 0, -140f), hallKey, hallSide, null, "tile", null, steps: 3.6f);
            var op = T.Group(c, "Office square props");
            T.Prop(op, "prop_banner_han", CityCx - 9.8f, -141.2f);
            T.Prop(op, "prop_banner_han", CityCx + 9.8f, -141.2f);
            T.Prop(op, "prop_lantern_red", CityCx - 3.4f, -140.6f, y: PlinthH + 2.6f);
            T.Prop(op, "prop_lantern_red", CityCx + 3.4f, -140.6f, y: PlinthH + 2.6f);
            // The congee shed and communal tables (“同簋而食”) on the square's west edge, the notice board on the east.
            if (T.Tex("prop_congee_shed", optional: true)) T.Prop(op, "prop_congee_shed", CityCx - 10.5f, -146f, solid: true);
            else T.Prop(op, "Congee shed (stand-in)", CityCx - 10.5f, -146f, key: "prop_cooking_stove", solid: true);
            string table = K("prop_long_table", "prop_low_table");
            T.Prop(op, table, CityCx - 10f, -150.5f, solid: true);
            T.Prop(op, table, CityCx - 10f, -154.5f, solid: true);
            T.Prop(op, "prop_notice_board", CityCx + 10.5f, -146.5f, solid: true);
            T.Prop(op, "prop_wine_jars", CityCx - 13f, -143.5f);
            foreach (float x in new[] { CityCx - 17.5f, CityCx + 17.5f })
                for (float z = -160f; z < -143f; z += 6f) T.Prop(op, "prop_tree_willow", x, z, scale: 0.75f, solid: true);

            // Warehouses and inns on plinths along the street (fronts face south, clear of the street and the square),
            // houses behind; barracks in the north-east with open drill ground before its gate.
            string wh = K("warehouse_front", "house_b_front"), whs = K("warehouse_side", "house_b_side");
            string hf = K("house_loess_front", "house_c_front"), hs = K("house_loess_side", "house_c_side");
            OnPlinth(c, "Warehouse W", new Vector3(CityCx - 13f, 0, -187f), wh, whs, null, "thatch", Thatch);
            OnPlinth(c, "Warehouse E", new Vector3(CityCx + 13f, 0, -187f), wh, whs, null, "thatch", Thatch);
            OnPlinth(c, "Inn W", new Vector3(CityCx - 13f, 0, -175f), wh, whs, null, "thatch", Thatch);
            OnPlinth(c, "Inn E", new Vector3(CityCx + 13f, 0, -175f), wh, whs, null, "thatch", Thatch);
            OnPlinth(c, "House SW", new Vector3(-95f, 0, -186f), hf, hs, null, "thatch", Thatch);
            OnPlinth(c, "House SE", new Vector3(-25f, 0, -186f), hf, hs, null, "thatch", Thatch);
            OnPlinth(c, "House W", new Vector3(-97f, 0, -171f), hf, hs, null, "thatch", Thatch);
            OnPlinth(c, "Residence (能人居所)", new Vector3(-96f, 0, -133f), hf, hs, null, "thatch", Thatch);
            OnPlinth(c, "Inn SW", new Vector3(CityCx - 24f, 0, -165f), wh, whs, null, "thatch", Thatch);
            OnPlinth(c, "Inn SE", new Vector3(CityCx + 24f, 0, -165f), wh, whs, null, "thatch", Thatch);
            OnPlinth(c, "House W2", new Vector3(-84f, 0, -186f), hf, hs, null, "thatch", Thatch);
            OnPlinth(c, "House E2", new Vector3(-36f, 0, -186f), hf, hs, null, "thatch", Thatch);
            OnPlinth(c, "House E3", new Vector3(-24f, 0, -171f), hf, hs, null, "thatch", Thatch);
            OnPlinth(c, "House E4", new Vector3(-17f, 0, -162f), hf, hs, null, "thatch", Thatch);
            OnPlinth(c, "House NW", new Vector3(-84f, 0, -133f), hf, hs, null, "thatch", Thatch);
            OnPlinth(c, "House NW2", new Vector3(-75f, 0, -123f), hf, hs, null, "thatch", Thatch);
            var green = T.Group(c, "Yard greens");
            foreach (var (gx0, gx1, gz0, gz1) in new[] { (-108f, -100f, -128f, -118f), (-20f, -12f, -193f, -188f), (-108f, -101f, -193f, -189f), (-48f, -40f, -160f, -150f) })
                T.Patch(green, "Green", "ground_grass", gx0, gx1, gz0, gz1, 0.0035f);
            (string key, float x, float z)[] trees = { ("prop_tree_elm", -104f, -123f), ("prop_tree_willow", -16f, -190f), ("prop_tree_elm", -104f, -191f),
                ("prop_tree_locust", -44f, -155f), ("prop_tree_willow", -80f, -140f), ("prop_tree_elm", -40f, -133f), ("prop_tree_locust", -89f, -176f) };
            foreach (var (key, x, z) in trees) T.Prop(green, key, x, z, scale: 0.85f, solid: true);
            var st = T.Group(c, "Street goods");
            string cargo = K("prop_cargo_pile", "prop_sacks_pile"), stall = K("prop_reed_blind_stall", "prop_stall");
            T.Prop(st, cargo, CityCx - 7.6f, -189f, solid: true);
            T.Prop(st, cargo, CityCx + 7.8f, -180.5f, solid: true);
            T.Prop(st, "prop_handcart", CityCx - 7.4f, -181f, solid: true);
            T.Prop(st, stall, CityCx + 7.6f, -170.5f, solid: true);
            T.Prop(st, "prop_wine_jars", CityCx - 7.8f, -171f);
            // Market west of the square: reed-blind stalls.
            var mk = T.Group(c, "Market (市集)");
            for (int i = 0; i < 4; i++) T.Prop(mk, stall, -100f + (i % 2) * 9f, -149f - (i / 2) * 8f, solid: true);
            T.Prop(mk, "prop_grain_sacks", -95.5f, -146f);
            T.Prop(mk, "prop_basket_stack", -86f, -161f);
            Label(labels, "市集", new Vector3(-95f, 0, -153f), 6f);
            var bk = T.Group(c, "Barracks (兵营)");
            float stake = T.Metres("palisade_stakes").y;
            T.Strip(bk, "Palisade W", "palisade_stakes", new Vector3(-38f, 0, -143f), new Vector3(-38f, 0, -119f), stake);
            T.Strip(bk, "Palisade E", "palisade_stakes", new Vector3(-14f, 0, -143f), new Vector3(-14f, 0, -119f), stake);
            T.Strip(bk, "Palisade S W", "palisade_stakes", new Vector3(-38f, 0, -143f), new Vector3(-29f, 0, -143f), stake);
            T.Strip(bk, "Palisade S E", "palisade_stakes", new Vector3(-23f, 0, -143f), new Vector3(-14f, 0, -143f), stake);
            T.Prop(bk, "prop_army_tent", -32f, -126f, solid: true);
            T.Prop(bk, "prop_army_tent", -20f, -126f, solid: true);
            T.Prop(bk, "prop_weapon_rack", -26f, -131f, solid: true);
            T.Prop(bk, "prop_training_dummy", -33f, -136f);
            T.Prop(bk, "prop_banner_han", -30.4f, -143.8f);
            T.Prop(bk, "prop_banner_han", -21.6f, -143.8f);
            Label(labels, "兵营", new Vector3(-26f, 0, -134f), 7f);
            Spot("Square", PlayerStart);
            Spot("Main street", new Vector3(CityCx, 0, -180f));
            Spot("East road", new Vector3(150f, 0, -170f));
            Spot("North road", new Vector3(-10f, 0, 50f));
            Spot("Barracks", new Vector3(-26f, 0, -149f));
        }

        // A building standing on a 1 m rammed-earth plinth with a fieldstone toe, steps up to its door.
        static void OnPlinth(Transform parent, string name, Vector3 front, string frontKey, string sideKey, string backKey, string roofKey, string roofTex, float steps = 2.4f)
        {
            var f = T.Metres(frontKey); var sd = T.Metres(sideKey);
            var g = T.Group(parent, name, front);
            float w = f.x + 1.6f, d = sd.x + 1.6f, zc = sd.x / 2f;
            string face = K("earth_plinth", "steps_riser");
            foreach (var (pos, yaw, len) in new[] { (new Vector3(0, PlinthH / 2f, zc - d / 2f), 0f, w), (new Vector3(0, PlinthH / 2f, zc + d / 2f), 180f, w),
                (new Vector3(-w / 2f, PlinthH / 2f, zc), 90f, d), (new Vector3(w / 2f, PlinthH / 2f, zc), -90f, d) })
                T.Quad(g, "Plinth face", pos, new Vector3(0, yaw, 0), new Vector2(len, PlinthH), T.Mat(face), new Vector2(len / T.Metres(face).x, 1f));
            var top = new Vector2(w, d);
            T.Quad(g, "Plinth top", new Vector3(0, PlinthH + 0.003f, zc), new Vector3(90f, 0, 0), top, T.Mat("ground_packed_earth"), T.Repeat("ground_packed_earth", top), castShadows: false);
            T.Solid(g, new Vector3(0, PlinthH / 2f, zc), new Vector3(w, PlinthH, d));
            T.Building(g, "Building", new Vector3(0, PlinthH, 0), 0f, frontKey, sideKey, backKey, roofKey: roofKey, roofTexture: roofTex,
                eaveTexture: roofTex != null ? "roof_thatch_eave" : null);
            T.Stairs(g, "Steps", new Vector3(0, 0, -0.8f), steps, 3, 0.33f, 0.4f, 0.01f);
        }

        // A low loess town wall from a to b: 3.5 m high, 3 m thick, its top a beaten-earth walk behind a timber rail.
        static void LoessWall(Transform parent, string name, Vector3 a, Vector3 b)
        {
            bool alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
            float len = (alongX ? Mathf.Abs(b.x - a.x) : Mathf.Abs(b.z - a.z)) + WallTk;
            var run = T.Group(parent, name, (a + b) / 2f, alongX ? 0f : 90f);
            run.gameObject.AddComponent<Occluder>();
            string face = K("town_wall_loess", "wall_rammed_earth");
            var size = new Vector2(len, TownWallH);
            T.Quad(run, "Face out", new Vector3(0, TownWallH / 2f, -WallTk / 2f), Vector3.zero, size, T.Mat(face), T.Repeat(face, size));
            T.Quad(run, "Face in", new Vector3(0, TownWallH / 2f, WallTk / 2f), new Vector3(0, 180, 0), size, T.Mat(face), T.Repeat(face, size));
            var top = new Vector2(len, WallTk);
            T.Quad(run, "Wall walk", new Vector3(0, TownWallH, 0), new Vector3(90, 0, 0), top, T.Mat("ground_packed_earth"), T.Repeat("ground_packed_earth", top), castShadows: false);
            string rail = K("levee_fence", "fence_wattle");
            var railSize = new Vector2(len, T.Metres(rail).y);
            T.Quad(run, "Rail", new Vector3(0, TownWallH + railSize.y / 2f, -WallTk / 2f + 0.2f), Vector3.zero, railSize, T.Mat(rail, clip: true, twoSided: true), T.Repeat(rail, railSize));
            T.Solid(run, new Vector3(0, TownWallH / 2f, 0), new Vector3(len, TownWallH, WallTk));
        }

        // Pingyuan's timber-and-thatch gate in an 8 m gap of the loess wall, facing outward along yaw.
        static void TownGate(Transform parent, string name, Vector3 at, float yaw)
        {
            var g = T.Group(parent, name, at, yaw);
            g.gameObject.AddComponent<Occluder>();
            if (!T.Tex("town_gate_front", optional: true)) { Gate(parent, name + " (stand-in)", at, yaw); return; }
            var front = T.Metres("town_gate_front");   // 8 × 5.5, opening about 3.5 × 3.5
            T.Quad(g, "Gate outside", new Vector3(0, front.y / 2f, -WallTk / 2f - 0.05f), Vector3.zero, front, T.Mat("town_gate_front", clip: true, twoSided: true));
            T.Quad(g, "Gate inside", new Vector3(0, front.y / 2f, WallTk / 2f + 0.05f), new Vector3(0, 180, 0), front, T.Mat("town_gate_front", clip: true, twoSided: true));
            string face = K("town_wall_loess", "wall_rammed_earth");
            foreach (int s in new[] { -1, 1 })
            {
                var side = new Vector2(WallTk, 3.5f);
                T.Quad(g, "Passage side", new Vector3(s * 1.75f, 1.75f, 0), new Vector3(0, s * -90f, 0), side, T.Mat(face), T.Repeat(face, side));
                T.Solid(g, new Vector3(s * 3f, 2.75f, 0), new Vector3(2.5f, 5.5f, WallTk));
            }
            T.Quad(g, "Passage ceiling", new Vector3(0, 3.5f, 0), new Vector3(-90, 0, 0), new Vector2(3.5f, WallTk), T.Mat("timber_beam"), T.Repeat("timber_beam", new Vector2(3.5f, WallTk)), castShadows: false);
            T.Solid(g, new Vector3(0, 4.5f, 0), new Vector3(4f, 2f, WallTk));
        }

        // An open drain along a street (x fixed, z0 < z1). The ground has no holes, so it is drawn at street level: a
        // strip of water between two low fieldstone kerbs; plank bridges at the given z. Kerb colliders keep the player
        // out except on the bridges.
        static void Drain(Transform parent, string name, float x, float z0, float z1, float[] bridges)
        {
            var g = T.Group(parent, name);
            const float w = 1f, kerbH = 0.24f;
            float len = z1 - z0, zc = (z0 + z1) / 2f;
            string wall = K("drain_channel_wall", "steps_riser"), water = K("water_channel", "wb_water_muddy");
            T.Quad(g, "Drain water", new Vector3(x, 0.012f, zc), new Vector3(90f, 0, 0), new Vector2(w, len), T.Mat(water), T.Repeat(water, new Vector2(w, len)), castShadows: false);
            foreach (int s in new[] { -1, 1 })
            {
                float kx = x + s * (w / 2f + 0.12f);
                T.Quad(g, "Kerb top", new Vector3(kx, kerbH, zc), new Vector3(90f, 90f, 0), new Vector2(len, 0.24f), T.Mat(wall), new Vector2(len / T.Metres(wall).x, 0.3f), castShadows: false);
                foreach (int f in new[] { -1, 1 })
                    T.Quad(g, "Kerb face", new Vector3(kx + f * 0.12f, kerbH / 2f, zc), new Vector3(0, f * -90f, 0), new Vector2(len, kerbH), T.Mat(wall), new Vector2(len / T.Metres(wall).x, 1f), castShadows: false);
            }
            string plank = K("plank_bridge", "wb_dock_planks");
            var cuts = new List<float> { z0 };
            foreach (float bz in bridges)
            {
                T.Quad(g, "Plank bridge", new Vector3(x, kerbH + 0.03f, bz), new Vector3(90f, 90f, 0), new Vector2(1.5f, 2f), T.Mat(plank, clip: true), castShadows: false);
                cuts.Add(bz - 0.8f); cuts.Add(bz + 0.8f);
            }
            cuts.Add(z1);
            for (int i = 0; i + 1 < cuts.Count; i += 2)
                if (cuts[i + 1] > cuts[i]) Solid(g, new Vector3(x, 0.4f, (cuts[i] + cuts[i + 1]) / 2f), new Vector3(w + 0.5f, 0.8f, cuts[i + 1] - cuts[i]));
        }

        // A city gate in a 9 m wall gap (Zhuojun's south-gate textures), facing outward along yaw.
        static void Gate(Transform parent, string name, Vector3 at, float yaw)
        {
            var g = T.Group(parent, name, at, yaw);
            g.gameObject.AddComponent<Occluder>();
            var front = T.Metres("south_gate_wall_front");   // 9 × 5, opening 4 × 3.6
            T.Quad(g, "Gate wall outside", new Vector3(0, front.y / 2f, -2f), Vector3.zero, front, T.Mat("south_gate_wall_front", clip: true));
            T.Quad(g, "Gate wall inside", new Vector3(0, front.y / 2f, 2f), new Vector3(0, 180, 0), front, T.Mat("south_gate_wall_front", clip: true));
            foreach (int s in new[] { -1, 1 })
            {
                var side = new Vector2(4f, 3.6f);
                T.Quad(g, "Passage side", new Vector3(s * 2f, 1.8f, 0), new Vector3(0, s * -90f, 0), side, T.Mat("citywall_face"), T.Repeat("citywall_face", side));
                T.Solid(g, new Vector3(s * 3.25f, 2.5f, 0), new Vector3(2.5f, 5f, 4f));
            }
            var ceiling = new Vector2(4f, 4f);
            T.Quad(g, "Passage ceiling", new Vector3(0, 3.6f, 0), new Vector3(-90, 0, 0), ceiling, T.Mat("timber_beam"), T.Repeat("timber_beam", ceiling), castShadows: false);
            var top = new Vector2(9f, 4f);
            T.Quad(g, "Walkway", new Vector3(0, 5f, 0), new Vector3(90, 0, 0), top, T.Mat("citywall_walkway"), T.Repeat("citywall_walkway", top), castShadows: false);
            T.Solid(g, new Vector3(0, 4.3f, 0), new Vector3(4f, 1.4f, 4f));
            T.Building(g, "Gate tower", new Vector3(0, 5f, -1.75f), 0f, "south_gate_tower_front", "south_gate_tower_side", null, roofKey: "tile", collide: false);
        }

        // ---------- the ferry ----------

        static void Ferry(Transform env, Transform labels)
        {
            var f = T.Group(env, "Pingyuan ferry (平原津)");
            // Dock: deck 0.35 m above the bank (one step), x -64..-56, out to z -266 over the water on timber piles.
            const float deckY = 0.35f, x0 = -64f, x1 = -56f, zStart = LeveeSouth + 0.5f, zEnd = -266f;
            string planks = K("dock_planks", "wb_dock_planks");
            var deck = new Vector2(x1 - x0, zStart - zEnd);
            T.Quad(f, "Dock deck", new Vector3((x0 + x1) / 2f, deckY, (zStart + zEnd) / 2f), new Vector3(90f, 0, 0), deck, T.Mat(planks), T.Repeat(planks, deck), castShadows: false);
            Solid(f, new Vector3((x0 + x1) / 2f, deckY / 2f - 0.6f, (zStart + zEnd) / 2f), new Vector3(x1 - x0, deckY + 1.2f, zStart - zEnd));
            if (T.Tex("dock_side", optional: true))
            {
                var side = new Vector2(zStart - zEnd, T.Metres("dock_side").y);
                foreach (var (x, yaw) in new[] { (x0, 90f), (x1, -90f) })
                    T.Quad(f, "Dock side", new Vector3(x, deckY - side.y / 2f, (zStart + zEnd) / 2f), new Vector3(0, yaw, 0), side, T.Mat("dock_side", clip: true, twoSided: true), new Vector2(side.x / T.Metres("dock_side").x, 1f));
                var end = new Vector2(x1 - x0, side.y);
                T.Quad(f, "Dock end", new Vector3((x0 + x1) / 2f, deckY - side.y / 2f, zEnd), new Vector3(0, 180f, 0), end, T.Mat("dock_side", clip: true, twoSided: true), new Vector2(end.x / T.Metres("dock_side").x, 1f));
            }
            else
                for (float z = zStart - 2f; z > zEnd; z -= 4f)
                    foreach (float x in new[] { x0 + 0.3f, x1 - 0.3f })
                        Box(f, "Pile", new Vector3(x, WaterY - 0.5f, z), new Vector3(0.4f, deckY - WaterY + 1.1f, 0.4f), 0.3f, solid: false);
            // Keep the player on the deck.
            Solid(f, new Vector3(x0 - 0.5f, 1.5f, (zStart + zEnd) / 2f), new Vector3(1f, 3f, zStart - zEnd));
            Solid(f, new Vector3(x1 + 0.5f, 1.5f, (zStart + zEnd) / 2f), new Vector3(1f, 3f, zStart - zEnd));
            Solid(f, new Vector3((x0 + x1) / 2f, 1.5f, zEnd - 0.5f), new Vector3(x1 - x0 + 2f, 3f, 1f));
            // Boats, the ferry flag, mooring posts, cargo waiting at the dock head, reeds along the bank.
            if (T.Tex("ferry_boat", optional: true))
            {
                T.Prop(f, "ferry_boat", x1 + 5.4f, -259f, y: WaterY + 0.1f);
                T.Prop(f, "Ferry boat 2", x0 - 6f, -262f, y: WaterY + 0.1f, key: "ferry_boat", scale: 0.9f);
            }
            else
            {
                var boat = T.Group(f, "Ferry boat", new Vector3(x1 + 3.4f, WaterY, -260f));
                Box(boat, "Hull", Vector3.zero, new Vector3(4.2f, 1.2f, 11f), 0.38f, solid: false);
                Box(boat, "Cabin", new Vector3(0, 1.2f, 1f), new Vector3(3f, 1.6f, 3.5f), 0.55f, solid: false);
            }
            if (T.Tex("prop_ferry_flag", optional: true)) T.Prop(f, "prop_ferry_flag", x0 + 0.6f, -247f, y: deckY);
            else T.Prop(f, "Ferry flag (stand-in)", x0 + 0.6f, -247f, y: deckY, key: "prop_banner_post");
            string post = K("prop_mooring_post", "prop_hitching_post");
            foreach (float z in new[] { -254f, -260f, -265f }) { T.Prop(f, post, x1 - 0.4f, z, y: deckY); T.Prop(f, post, x0 + 0.4f, z, y: deckY); }
            string cargo = K("prop_cargo_pile", "prop_sacks_pile");
            T.Prop(f, cargo, x1 - 1.8f, -249f, y: deckY);
            T.Prop(f, "prop_barrel", x0 + 1.6f, -251f, y: deckY);
            string reeds = K("prop_reeds", "prop_grass_tuft");
            var rr = new System.Random(13);
            for (int i = 0; i < 40; i++)
            {
                float x = CityCx - 60f + (float)rr.NextDouble() * 120f;
                if (Mathf.Abs(x - CityCx) < 9f) continue;
                T.Prop(f, reeds, x, LeveeSouth - 3f - (float)rr.NextDouble() * 6f, y: -0.2f, scale: reeds == "prop_reeds" ? 1f : 1.8f);
            }
            if (T.Tex("prop_reed_blind_stall", optional: true)) T.Prop(f, "Toll stall", CityCx + 9f, LeveeCrestS + 1.6f, y: LeveeH, key: "prop_reed_blind_stall", solid: true);
            // Landmark on the crest: the river-view pavilion among willows.
            if (T.Tex("prop_river_pavilion", optional: true)) T.Prop(f, "prop_river_pavilion", CityCx - 20f, (LeveeCrestN + LeveeCrestS) / 2f, y: LeveeH, solid: true);
            else GreyHouse(f, "River pavilion (stand-in)", new Vector3(CityCx - 20f, LeveeH, LeveeCrestN - 1f), 4f, 4f, 3f, shade: 0.6f);
            foreach (float x in new[] { -32f, -14f, 8f, 26f, 44f })
                T.Prop(f, "prop_tree_willow", CityCx + x + 0.5f, LeveeCrestN - 0.6f, y: LeveeH, scale: 1.1f, solid: true);
            Label(labels, "平原津（渡口·阶段门）", new Vector3(CityCx, 0, -258f), 5f);
            Label(labels, "黄河大堤·望河亭（地标）", new Vector3(CityCx - 20f, LeveeH, LeveeCrestS), 8f);
            Spot("Ferry", new Vector3(CityCx, deckY, -252f));
            Spot("Levee", new Vector3(CityCx - 140f, LeveeH, (LeveeCrestN + LeveeCrestS) / 2f));
            Spot("Pavilion", new Vector3(CityCx - 12f, LeveeH, (LeveeCrestN + LeveeCrestS) / 2f));
            Spot("South gate", new Vector3(CityCx, 0, CityZ0 - 8f));
            Spot("Inside south gate", new Vector3(CityCx, 0, CityZ0 + 9f));
        }

        // ---------- Gaotang gate scene (门前小场景) ----------

        static void Gaotang(Transform env, Transform labels)
        {
            var g = T.Group(env, "Gaotang (高唐·门前小场景)");
            float cx = GaotangFront.x, wz = -150f;
            if (GaotangRefined(g, cx, wz)) { Label(labels, "高唐（门前小场景）", new Vector3(cx, 0, wz), 12f); Spot("Gaotang", new Vector3(cx, 0, wz - 14f)); Spot("Gaotang square", new Vector3(cx, 0, wz + 14f)); return; }
            // A short stretch of town wall with its gate; inside, one square with the county hall and one enterable shop.
            T.WallRun(g, "Wall W", new Vector3(cx - 40f, 0, wz), new Vector3(cx - 4.5f, 0, wz));
            T.WallRun(g, "Wall E", new Vector3(cx + 4.5f, 0, wz), new Vector3(cx + 40f, 0, wz));
            T.WallRun(g, "Wall side W", new Vector3(cx - 38f, 0, wz + 2f), new Vector3(cx - 38f, 0, wz + 50f));
            T.WallRun(g, "Wall side E", new Vector3(cx + 38f, 0, wz + 2f), new Vector3(cx + 38f, 0, wz + 50f));
            T.WallRun(g, "Wall back", new Vector3(cx - 40f, 0, wz + 52f), new Vector3(cx + 40f, 0, wz + 52f));
            Gate(g, "Gaotang gate", new Vector3(cx, 0, wz), 0f);
            T.Patch(g, "Gaotang square", "ground_street_stone", cx - 15f, cx + 15f, wz + 2f, wz + 30f, 0.006f);
            EarthPatch(g, new Vector3(cx, 0, wz - 12f), 12f, 10f, 67);
            GreyHouse(g, "County hall (县府)", new Vector3(cx, 0, wz + 30f), 14f, 9f, 4.5f, shade: 0.7f);
            GreyHouse(g, "Shop (enterable)", new Vector3(cx - 24f, 0, wz + 20f), 8f, 6f, 3.4f, yaw: 90f);
            Label(labels, "高唐（门前小场景）", new Vector3(cx, 0, wz), 12f);
            Spot("Gaotang", new Vector3(cx, 0, wz - 14f));
        }

        // ---------- post station and checkpoint ----------

        static void PostStationAndBarrier(Transform env, Transform labels)
        {
            var p = T.Group(env, "Post station (驿站)");
            EarthPatch(p, PostFront + new Vector3(0, 0, -5f), 14f, 7f, 61);
            T.Building(p, "Post station", PostFront, 0f, "post_station_front", "post_station_side", null);
            T.Building(p, "Stable", PostFront + new Vector3(-11f, 0, 0), 0f, "stable_front", "stable_side", null, roofKey: "thatch", interior: "wall_rammed_earth");
            T.Prop(p, "prop_banner_post", PostFront.x + 4.6f, PostFront.z - 1f, y: 0.9f);
            T.Prop(p, "prop_hitching_post", PostFront.x + 7f, PostFront.z - 3f, solid: true);
            T.Prop(p, "prop_trough", PostFront.x - 9f, PostFront.z - 2.5f);
            Label(labels, "驿站（存档·传闻·驿马）", PostFront, 8f);
            Spot("Post station", PostFront + new Vector3(0, 0, -9f));

            // Checkpoint on the Jinan road (R7 opens in 193): palisade across the road with a closed gate and a tower,
            // marsh on both sides so the reason is visible.
            var b = T.Group(env, "Jinan-road checkpoint (屏障口)");
            float bx = 545f;
            float stake = T.Metres("palisade_stakes").y;
            T.Strip(b, "Palisade N", "palisade_stakes", new Vector3(bx, 0, 26f), new Vector3(bx, 0, 90f), stake);
            T.Strip(b, "Palisade S", "palisade_stakes", new Vector3(bx, 0, -50f), new Vector3(bx, 0, 14f), stake);
            if (T.Tex("checkpoint_gate", optional: true))
            {
                var cg = T.Metres("checkpoint_gate");
                T.Quad(b, "Closed gate", new Vector3(bx, cg.y / 2f, 20f), new Vector3(0, 90f, 0), cg, T.Mat("checkpoint_gate", clip: true, twoSided: true));
                Solid(b, new Vector3(bx, 1.5f, 20f), new Vector3(0.6f, 3f, 12f));
            }
            else Box(b, "Closed gate", new Vector3(bx, 0, 20f), new Vector3(0.6f, 3f, 12f), 0.32f);
            if (T.Tex("watchtower_front", optional: true))
                T.Building(b, "Watchtower", new Vector3(bx - 6f, 0, 31f), 0f, "watchtower_front", "watchtower_side", "watchtower_front", interior: "wall_rammed_earth");
            T.Patch(b, "Marsh N", "wb_water_muddy", bx - 6f, bx + 15f, 34f, 110f, 0.002f);
            T.Patch(b, "Marsh S", "wb_water_muddy", bx - 6f, bx + 15f, -80f, 8f, 0.002f);
            T.Prop(b, "prop_banner_han", bx - 1.5f, 13.5f);
            T.Prop(b, "prop_banner_han", bx - 1.5f, 26.5f);
            Label(labels, "济南方向哨卡（屏障：193年开放）", new Vector3(bx, 0, 20f), 7f);
            Spot("Barrier", new Vector3(bx - 12f, 0, 20f));
        }

        // ---------- villages ----------

        static void Village(Transform env, Transform labels, string name, Vector3 centre, int seed)
        {
            var v = T.Group(env, name + " village", centre);
            // Five houses round a threshing floor with a well; fields outside. Doors face south onto the floor.
            EarthPatch(env, centre, 16f, 12f, seed);   // threshing floor with a ragged edge
            (string front, string side, string back, bool thatch, Vector3 at)[] houses =
            {
                ("house_b_front", "house_b_side", "house_b_back", true, new Vector3(-12f, 0, 12f)),
                ("house_front", "house_side", "house_back", false, new Vector3(2f, 0, 13f)),
                ("house_c_front", "house_c_side", "house_c_back", true, new Vector3(16f, 0, 12f)),
                ("house_b_front", "house_b_side", "house_b_back", true, new Vector3(-22f, 0, -2f)),
                ("house_c_front", "house_c_side", "house_c_back", false, new Vector3(23f, 0, -4f)),
            };
            // Bohai villages build like the plain around them (loess walls, golden thatch), not like Zhuojun.
            bool loess = T.Tex("house_loess_front", optional: true);
            foreach (var (front, side, back, thatch, at) in houses)
                if (loess) T.Building(v, "House", at, 0f, thatch ? "house_loess_front" : K("warehouse_front", "house_loess_front"), thatch ? "house_loess_side" : K("warehouse_side", "house_loess_side"), null,
                    roofKey: "thatch", roofTexture: Thatch, eaveTexture: "roof_thatch_eave");
                else T.Building(v, "House", at, 0f, front, side, back, roofKey: thatch ? "thatch" : "tile");
            T.Prop(v, "prop_well", 0f, 0f, solid: true);
            T.Prop(v, "prop_hay_pile", -8f, -6f);
            T.Prop(v, "prop_firewood", 9f, 8f);
            T.Prop(v, "prop_straw_mats", 6f, -5f);
            var rng = new System.Random(seed);
            for (int i = 0; i < 4; i++)
            {
                float x = -60f + i * 30f + (float)rng.NextDouble() * 6f, z = -26f - (float)rng.NextDouble() * 10f;
                T.Patch(v, "Field", "ground_vegetable_plot", x - 11f, x + 11f, z - 9f, z + 2f, 0.002f);
            }
            for (int i = 0; i < 9; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 28f + (float)rng.NextDouble() * 12f;
                T.Prop(v, i % 3 == 0 ? "prop_tree_elm" : "prop_tree_locust", Mathf.Cos(a) * r, 8f + Mathf.Abs(Mathf.Sin(a)) * r * 0.6f, scale: 0.9f, solid: true);
            }
            Label(labels, name + "（村落）", centre, 9f);
            Spot(name == "西村" ? "West village" : "North village", centre + new Vector3(0, 0, -8f));
        }

        // ---------- wild exploration points ----------

        static void Explore(Transform env, Transform labels)
        {
            // 1 Beacon ruin by the levee: a broken rammed-earth tower, rubble, a dead tree.
            var b = T.Group(env, "Beacon ruin (废烽燧)", new Vector3(-330f, 0, -170f));
            if (!Landmark(b, "beacon"))
            {
            Box(b, "Tower stump", Vector3.zero, new Vector3(6f, 7f, 6f), 0.5f);
            Box(b, "Tower crown (broken)", new Vector3(1f, 7f, 0.5f), new Vector3(3.5f, 1.8f, 3f), 0.5f);
            for (int i = 0; i < 5; i++) Box(b, "Rubble", new Vector3(-5f + i * 2.4f, 0, -5f + (i % 2) * 1.5f), new Vector3(1.4f, 0.6f, 1.2f), 0.45f, yaw: i * 23f);
            }
            T.Prop(b, "prop_tree_locust", 7f, 3f, scale: 0.8f, solid: true);
            Label(labels, "废烽燧（探索点）", b.position, 10f);
            Spot("Beacon", b.position + new Vector3(0, 0, -9f));

            // 2 Reed marsh: open water patches among reeds, a punt pulled up.
            var r = T.Group(env, "Reed marsh (芦苇荡)", new Vector3(220f, 0, -70f));
            string pool = K("pond_water_v2", "wb_water_muddy");
            // Irregular pools with reed-mud shores (no rectangles: user 2026-10-24, no tofu blocks).
            Pool(r, "Pool A", new Vector2(-7f, 3f), 11f, 9f, 31, pool);
            Pool(r, "Pool B", new Vector2(14f, 12f), 8f, 8f, 47, pool);
            var rr = new System.Random(3);
            for (int i = 0; i < 46; i++)
            {
                float x = -26f + (float)rr.NextDouble() * 54f, z = -12f + (float)rr.NextDouble() * 38f;
                if (T.Tex("prop_reed_clump_large", optional: true)) T.Prop(r, rr.Next(3) == 0 ? "prop_reed_clump_large" : K("prop_reeds", "prop_grass_tuft"), x, z, scale: 0.9f + (float)rr.NextDouble() * 0.4f);
                else T.Prop(r, rr.Next(3) == 0 ? "prop_bush_b" : "prop_grass_tuft", x, z, scale: 1.6f);
            }
            if (T.Tex("prop_punt_boat", optional: true)) T.Prop(r, "prop_punt_boat", -4f, -9f);
            else Box(r, "Punt", new Vector3(-4f, 0, -9f), new Vector3(1.4f, 0.5f, 5f), 0.35f, yaw: 20f, solid: false);
            Label(labels, "芦苇荡（探索点）", r.position, 6f);
            Spot("Reeds", r.position + new Vector3(0, 0, -16f));

            // 3 Tomb mound with a pair of stone que pillars on its spirit road.
            var t = T.Group(env, "Tomb mound (古冢石阙)", new Vector3(-220f, 0, 260f));
            if (!Landmark(t, "tomb"))
            {
            for (int i = 0; i < 4; i++) Box(t, "Mound tier", Vector3.zero + new Vector3(0, i * 1.6f, 0), new Vector3(24f - i * 5f, 1.6f, 20f - i * 4.5f), 0.48f);
            Box(t, "Que W", new Vector3(-4f, 0, -18f), new Vector3(2f, 5f, 2f), 0.75f);
            Box(t, "Que E", new Vector3(4f, 0, -18f), new Vector3(2f, 5f, 2f), 0.75f);
            }
            {
                // Spirit road: stone slabs that narrow and fade out into the grass at the south end, no hard rectangle.
                var sp = Smooth(new[] { t.position + new Vector3(0, 0, -26f), t.position + new Vector3(0, 0, -10f) }, 1f);
                var tk = Taper(sp, true, false, flare: false);
                (sp, tk) = RoundEnds(sp, tk, true, false, 2.2f);
                string sk = K("stone_road_v2", "ground_street_stone");
                Ribbon(t, "Spirit road", T.Mat(sk), sp, -2.2f, 2.2f, 0.005f, T.Metres(sk).y * (4.4f / T.Metres(sk).x), tk);
                if (T.Tex("road_edge_band", optional: true))
                {
                    Ribbon(t, "Spirit edge R", T.Mat("road_edge_band", clip: true), sp, 1.9f, 1.9f + T.Metres("road_edge_band").y, 0.0058f, T.Metres("road_edge_band").x, tk, vAcross: true, band: true);
                    Ribbon(t, "Spirit edge L", T.Mat("road_edge_band", clip: true), sp, -1.9f, -(1.9f + T.Metres("road_edge_band").y), 0.0058f, T.Metres("road_edge_band").x, tk, vAcross: true, band: true);
                }
            }
            Label(labels, "古冢石阙（探索点）", t.position, 11f);
            Spot("Tomb", t.position + new Vector3(0, 0, -26f));
        }

        static void Exits(Transform env, Transform labels)
        {
            var e = T.Group(env, "Region exits");
            T.Prop(e, "prop_notice_board", XMin + 8f, 46f, solid: true);
            Label(labels, "往清河（R3·天下图）", new Vector3(XMin + 8f, 0, 46f), 5f);
            T.Prop(e, "prop_notice_board", 46f, ZMax - 8f, solid: true);
            Label(labels, "往南皮（天下图）", new Vector3(46f, 0, ZMax - 8f), 5f);
        }

        // Trees along roads and in clumps over the plain, never on a landmark's open ground.
        static void Scatter(Transform env)
        {
            var g = T.Group(env, "Plain trees");
            var rng = new System.Random(29);
            int placed = 0;
            for (int i = 0; i < 600 && placed < 170; i++)
            {
                float x = XMin + 10f + (float)rng.NextDouble() * (XMax - XMin - 20f), z = LeveeNorth + 8f + (float)rng.NextDouble() * (ZMax - LeveeNorth - 16f);
                if (Near(x, z)) continue;
                string key = rng.Next(5) switch { 0 => "prop_tree_elm", 1 => "prop_tree_willow", 2 => "prop_bush", 3 => "prop_bush_b", _ => "prop_tree_locust" };
                if (!T.Tex(key, optional: true)) key = "prop_tree_locust";
                bool tree = key.Contains("tree");
                T.Prop(g, key, x, z, scale: tree ? 0.8f + (float)rng.NextDouble() * 0.3f : 1.2f, solid: tree);
                placed++;
            }
            // Willows along the levee foot.
            for (float x = XMin + 20f; x < XMax - 20f; x += 34f)
                if (Mathf.Abs(x - CityCx) > 20f && T.Tex("prop_tree_willow", optional: true))
                    T.Prop(g, "prop_tree_willow", x + (float)rng.NextDouble() * 8f, LeveeNorth + 3f, scale: 0.9f, solid: true);
        }

        // Clear of the city, the villages, the wild spots, the post station and every road.
        static bool Near(float x, float z)
        {
            var cell = At(x, z);
            if (Blocked(cell) || cell == Cell.Town || cell == Cell.Bridge) return true;
            (float x, float z, float r)[] keepOut =
            {
                (CityCx, -155f, 70f), (330f, -140f, 55f), (460f, 0f, 35f), (545f, 20f, 25f), (-380f, 40f, 48f), (40f, 200f, 48f),
                (-330f, -170f, 18f), (220f, -70f, 32f), (-220f, 260f, 32f),
            };
            foreach (var k in keepOut) if ((x - k.x) * (x - k.x) + (z - k.z) * (z - k.z) < k.r * k.r) return true;
            Vector3[][] roads =
            {
                new[] { new Vector3(XMin, 0, 40f), new Vector3(-380f, 0, 40f), new Vector3(-250f, 0, -100f), WestGate },
                new[] { NorthGate, new Vector3(CityCx, 0, -20f), new Vector3(40f, 0, 120f), new Vector3(40f, 0, ZMax) },
                new[] { EastGate, new Vector3(150f, 0, -170f), new Vector3(310f, 0, -182f), GaotangFront, new Vector3(420f, 0, -100f), PostFront, new Vector3(XMax, 0, 20f) },
                new[] { new Vector3(-250f, 0, -100f), new Vector3(-330f, 0, -165f) },
                new[] { new Vector3(150f, 0, -170f), new Vector3(215f, 0, -88f) },
                new[] { new Vector3(-380f, 0, 40f), new Vector3(-300f, 0, 160f), new Vector3(-224f, 0, 244f) },
            };
            var p = new Vector2(x, z);
            foreach (var road in roads)
                for (int i = 0; i + 1 < road.Length; i++)
                {
                    var a = new Vector2(road[i].x, road[i].z); var b = new Vector2(road[i + 1].x, road[i + 1].z);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude);
                    if ((a + t * (b - a) - p).sqrMagnitude < 9f * 9f) return true;
                }
            return false;
        }

        // ---------- location signboard and mini map (user 2026-10-24) ----------

        // Named places for the signboard: town spots inside Pingyuan and Gaotang, road segments "A ↔ B" outside,
        // villages, wild spots, the post station and checkpoint; "渤海 · 野外" everywhere else.
        static void Locations()
        {
            var g = new GameObject("Location zones").transform;
            void Box(string major, string minor, float x0, float x1, float z0, float z1, int priority)
            {
                var z = new GameObject($"{major} · {minor}").AddComponent<LocationZone>();
                z.transform.SetParent(g, false);
                z.transform.position = new Vector3((x0 + x1) / 2f, 0, (z0 + z1) / 2f);
                z.Major = major; z.Minor = minor; z.Priority = priority; z.Size = new Vector2(x1 - x0, z1 - z0);
            }
            void Road(string minor, params Vector3[] pts) => RoadAt(minor, 2, pts);
            void Spur(string minor, params Vector3[] pts) => RoadAt(minor, 1, pts);
            void RoadAt(string minor, int priority, Vector3[] pts)
            {
                for (int i = 0; i + 1 < pts.Length; i++)
                {
                    var z = new GameObject($"渤海 · {minor}").AddComponent<LocationZone>();
                    z.transform.SetParent(g, false);
                    z.Major = "渤海"; z.Minor = minor; z.Priority = priority; z.IsSegment = true; z.A = pts[i]; z.B = pts[i + 1]; z.HalfWidth = 12f;
                }
            }
            void Place(string minor, float x, float z, float r) => Box("渤海", minor, x - r, x + r, z - r, z + r, 3);

            Box("渤海", "野外", XMin - 50f, XMax + 50f, ZMin - 50f, ZMax + 50f, 0);
            Box("渤海", "黄河大堤", XMin - 50f, XMax + 50f, LeveeSouth, LeveeNorth, 1);
            // Pingyuan
            Box("平原", "城内", CityX0, CityX1, CityZ0, CityZ1, 2);
            Box("平原", "县衙", CityCx - 15f, CityCx + 15f, -163f, -128f, 3);
            Box("平原", "主街", CityCx - 6f, CityCx + 6f, -189f, -163f, 3);
            Box("平原", "兵营", -40f, -12f, -150f, -116f, 3);
            Box("平原", "市集", -109f, -78f, -163f, -140f, 3);
            Box("平原", "南城门", CityCx - 10f, CityCx + 10f, CityZ0 - 10f, CityZ0 + 6f, 4);
            Box("平原", "北城门", CityCx - 8f, CityCx + 8f, CityZ1 - 6f, CityZ1 + 8f, 4);
            Box("平原", "西城门", CityX0 - 8f, CityX0 + 6f, WestGate.z - 8f, WestGate.z + 8f, 4);
            Box("平原", "东城门", CityX1 - 6f, CityX1 + 8f, EastGate.z - 8f, EastGate.z + 8f, 4);
            Box("平原", "黄河大堤", CityCx - 70f, CityCx + 70f, LeveeSouth, CityZ0 - 10f, 3);
            Box("平原", "望河亭", CityCx - 27f, CityCx - 13f, LeveeCrestS - 1f, LeveeCrestN + 1f, 5);
            Box("平原", "码头", CityCx - 12f, CityCx + 12f, -270f, LeveeSouth, 5);
            // Gaotang
            Box("高唐", "城门", GaotangFront.x - 14f, GaotangFront.x + 14f, -180f, -146f, 4);
            Box("高唐", "县府", GaotangFront.x - 38f, GaotangFront.x + 38f, -146f, -98f, 4);
            // Roads: the two ends name what lies at each end.
            Road("西村 ↔ 往清河", new Vector3(XMin, 0, 40f), new Vector3(-380f, 0, 40f));
            Road("平原西 ↔ 西村", new Vector3(-380f, 0, 40f), new Vector3(-250f, 0, -100f), WestGate);
            Road("平原北 ↔ 北村", NorthGate, new Vector3(CityCx, 0, -20f), new Vector3(40f, 0, 120f), new Vector3(40f, 0, 200f));
            Road("北村 ↔ 往南皮", new Vector3(40f, 0, 200f), new Vector3(40f, 0, ZMax));
            Road("平原东 ↔ 高唐", EastGate, new Vector3(150f, 0, -170f), new Vector3(310f, 0, -182f), GaotangFront);
            Road("高唐 ↔ 驿站", GaotangFront, new Vector3(420f, 0, -100f), PostFront);
            Road("驿站 ↔ 济南哨卡", PostFront, new Vector3(XMax, 0, 20f));
            Spur("西路 ↔ 废烽燧", new Vector3(-250f, 0, -100f), new Vector3(-330f, 0, -165f));
            Spur("东路 ↔ 芦苇荡", new Vector3(150f, 0, -170f), new Vector3(215f, 0, -88f));
            Spur("西村 ↔ 古冢", new Vector3(-380f, 0, 40f), new Vector3(-300f, 0, 160f), new Vector3(-224f, 0, 244f));
            // Places
            Place("西村", -380f, 40f, 40f);
            Place("北村", 40f, 200f, 40f);
            Place("废烽燧", -330f, -170f, 18f);
            Place("芦苇荡", 220f, -70f, 30f);
            Place("古冢石阙", -220f, 252f, 30f);
            Place("驿站", PostFront.x, PostFront.z + 4f, 22f);
            Place("济南哨卡", 545f, 20f, 18f);
            // The party rides on roads, the levee and open country of the region, and walks in towns and at places.
            foreach (var zone in g.GetComponentsInChildren<LocationZone>())
                zone.Ride = zone.Major == "渤海" && (zone.IsSegment || zone.Minor == "野外" || zone.Minor == "黄河大堤");
        }

        // A character's horse: the mounted pixel quad (hidden until mounting) and the MountRider that swaps sprites.
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
            Directory.CreateDirectory($"Assets/_Project/Art/Sprites/Characters/{key}Mounted");
            r.sharedMaterial = HD2DSampleBuilder.PixelMaterial(key + "Mounted", atlas);
            r.shadowCastingMode = ShadowCastingMode.On;
            quad.AddComponent<PixelBillboard>().BaseSize = new Vector2(size, size);
            var anim = quad.AddComponent<SpriteFrameAnimator>();
            anim.Target = r; anim.Columns = MountedImporter.Columns * MountedImporter.Rows; anim.Rows = 1; anim.Frame = MountRider.MountStart;
            rider.RideAnimator = anim;
            quad.SetActive(false);
            return rider;
        }

        // The region's mini map, drawn as a clean 2D chart in the unified UI palette (UI_STYLE_GUIDE.md) from the same
        // layout data as the scene: land, the river and its levee, roads, the two walled towns, villages and wild spots.
        // 2 px per metre; the HUD lays the place names over it.
        static Texture2D BakeMinimap()
        {
            string dir = ArtDir + "/Minimap", path = dir + "/bohai_minimap.png";
            Directory.CreateDirectory(dir);
            const float ppm = 1f;
            int w = Mathf.RoundToInt((XMax - XMin) * ppm), h = Mathf.RoundToInt((ZMax - ZMin) * ppm);
            var px = new Color32[w * h];
            Color32 land = new Color32(0x1F, 0x34, 0x48, 255), field = new Color32(0x26, 0x3E, 0x52, 255),
                river = new Color32(0x3A, 0x5E, 0x78, 255), levee = new Color32(0xC4, 0xA3, 0x6D, 255), road = new Color32(0x8C, 0xA7, 0xBA, 255),
                wall = new Color32(0xD6, 0xBA, 0x78, 255), town = new Color32(0x3B, 0x4C, 0x5A, 255), spot = new Color32(0xD6, 0xBA, 0x78, 255),
                dock = new Color32(0xB5, 0xC8, 0xD5, 255);
            int X(float x) => Mathf.RoundToInt((x - XMin) * ppm);
            int Y(float z) => Mathf.RoundToInt((z - ZMin) * ppm);
            void Rect(float x0, float x1, float z0, float z1, Color32 c)
            {
                for (int y = Mathf.Max(0, Y(z0)); y < Mathf.Min(h, Y(z1)); y++)
                    for (int x = Mathf.Max(0, X(x0)); x < Mathf.Min(w, X(x1)); x++) px[y * w + x] = c;
            }
            void Disc(float cx, float cz, float r, Color32 c)
            {
                for (int y = Y(cz - r); y <= Y(cz + r); y++)
                    for (int x = X(cx - r); x <= X(cx + r); x++)
                    {
                        if (x < 0 || y < 0 || x >= w || y >= h) continue;
                        float dx = (x - X(cx)) / ppm, dz = (y - Y(cz)) / ppm;
                        if (dx * dx + dz * dz <= r * r) px[y * w + x] = c;
                    }
            }
            void Line(float width, Color32 c, params Vector3[] pts)
            {
                for (int i = 0; i + 1 < pts.Length; i++)
                {
                    float len = Vector3.Distance(pts[i], pts[i + 1]);
                    for (float t = 0; t <= len; t += 1f / ppm)
                    {
                        var p = Vector3.Lerp(pts[i], pts[i + 1], t / len);
                        Disc(p.x, p.z, width / 2f, c);
                    }
                }
            }
            void Frame(float x0, float x1, float z0, float z1, float t, Color32 c)
            {
                Rect(x0, x1, z0, z0 + t, c); Rect(x0, x1, z1 - t, z1, c); Rect(x0, x0 + t, z0, z1, c); Rect(x1 - t, x1, z0, z1, c);
            }

            for (int i = 0; i < px.Length; i++) px[i] = land;
            var rng = new System.Random(5);   // the same dry-plain bands as Ground()
            for (int i = 0; i < 26; i++)
            {
                float x = XMin + (float)rng.NextDouble() * (XMax - XMin), z = -200f + (float)rng.NextDouble() * 520f;
                float bw = 40f + (float)rng.NextDouble() * 90f, bd = 25f + (float)rng.NextDouble() * 50f;
                Rect(x - bw / 2f, x + bw / 2f, z - bd / 2f, z + bd / 2f, field);
            }
            Rect(XMin, XMax, ZMin, WaterN, river);
            // Hills, woods, rivers and marsh from the terrain mask.
            if (mask != null)
            {
                Color32 hill = new Color32(0x4E, 0x4A, 0x44, 255), foot = new Color32(0x3E, 0x44, 0x46, 255), wood = new Color32(0x1E, 0x3E, 0x36, 255),
                    stream = new Color32(0x3A, 0x6E, 0x8E, 255), marsh = new Color32(0x2E, 0x4A, 0x52, 255);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        var c = At(XMin + (x + 0.5f) / ppm, ZMin + (y + 0.5f) / ppm);
                        if (c == Cell.Mountain) px[y * w + x] = hill;
                        else if (c == Cell.Foothill) px[y * w + x] = foot;
                        else if (c == Cell.Forest) px[y * w + x] = wood;
                        else if (c == Cell.River || c == Cell.Bridge) px[y * w + x] = stream;
                        else if (c == Cell.Marsh) px[y * w + x] = marsh;
                    }
            }
            Rect(XMin, XMax, LeveeSouth, LeveeNorth, new Color32(0x4A, 0x55, 0x48, 255));
            Rect(XMin, XMax, LeveeCrestS, LeveeCrestN, levee);
            float rw = 7f;
            Line(rw, road, new Vector3(XMin, 0, 40f), new Vector3(-380f, 0, 40f), new Vector3(-250f, 0, -100f), WestGate);
            Line(rw, road, NorthGate, new Vector3(CityCx, 0, -20f), new Vector3(40f, 0, 120f), new Vector3(40f, 0, 200f), new Vector3(40f, 0, ZMax));
            Line(rw, road, EastGate, new Vector3(150f, 0, -170f), new Vector3(310f, 0, -182f), GaotangFront, new Vector3(420f, 0, -100f), PostFront, new Vector3(XMax, 0, 20f));
            Line(rw * 0.6f, road, new Vector3(-250f, 0, -100f), new Vector3(-330f, 0, -165f));
            Line(rw * 0.6f, road, new Vector3(150f, 0, -170f), new Vector3(215f, 0, -88f));
            Line(rw * 0.6f, road, new Vector3(-380f, 0, 40f), new Vector3(-300f, 0, 160f), new Vector3(-224f, 0, 244f));
            Line(rw, road, SouthGate, new Vector3(CityCx, 0, LeveeSouth));
            // Towns: filled blocks inside a gold wall line.
            Rect(CityX0, CityX1, CityZ0, CityZ1, town);
            Frame(CityX0 - 2f, CityX1 + 2f, CityZ0 - 2f, CityZ1 + 2f, 5f, wall);
            Rect(CityCx - 14f, CityCx + 14f, -162f, -140f, new Color32(0x5A, 0x63, 0x66, 255));
            Rect(GaotangFront.x - 38f, GaotangFront.x + 38f, -150f, -98f, town);
            Frame(GaotangFront.x - 40f, GaotangFront.x + 40f, -152f, -96f, 5f, wall);
            Rect(CityCx - 4f, CityCx + 4f, -266f, LeveeSouth, dock);   // the ferry dock
            Rect(536f, 552f, -50f, 90f, new Color32(0x2E, 0x4A, 0x5C, 255));   // checkpoint marsh
            Line(4f, wall, new Vector3(545f, 0, -50f), new Vector3(545f, 0, 90f));
            foreach (var (x, z) in new[] { (-380f, 40f), (40f, 200f) }) { Disc(x, z, 22f, town); Disc(x, z, 9f, spot); }
            foreach (var (x, z) in new[] { (-330f, -170f), (220f, -70f), (-220f, 252f), (PostFront.x, PostFront.z + 4f) }) Disc(x, z, 9f, spot);

            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // Names laid over the mini map by the HUD.
        static readonly (string name, Vector3 at)[] MapNames =
        {
            ("平原", new Vector3(CityCx, 0, CityZ1 + 22f)), ("平原津", new Vector3(CityCx + 34f, 0, -262f)), ("高唐", new Vector3(330f, 0, -80f)),
            ("驿站", new Vector3(460f, 0, 36f)), ("哨卡", new Vector3(498f, 0, 108f)), ("西村", new Vector3(-380f, 0, 74f)),
            ("北村", new Vector3(40f, 0, 236f)), ("废烽燧", new Vector3(-330f, 0, -138f)), ("芦苇荡", new Vector3(220f, 0, -36f)), ("古冢", new Vector3(-220f, 0, 286f)),
            ("黄河", new Vector3(-420f, 0, -282f)),
        };

        static void Hud(Texture2D map, Transform player)
        {
            var go = new GameObject("Region HUD");
            var doc = go.AddComponent<UnityEngine.UIElements.UIDocument>();
            doc.panelSettings = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>("Assets/_Project/Settings/BattlePanelSettings.asset");
            if (!doc.panelSettings) throw new System.InvalidOperationException("Region HUD has no PanelSettings");
            var hud = go.AddComponent<ThreeKingdoms.UI.Region.RegionHud>();
            // The approved Codex region map (2 px per metre, aligned to the world) is the mini map and the full map
            // (user 2026-10-24: the map should have texture and match the design); the drawn chart is the fallback.
            Texture2D Painted(string file)
            {
                string path = ArtDir + "/Minimap/" + file;
                if (!File.Exists(path)) return null;
                AssetDatabase.ImportAsset(path);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                imp.textureType = TextureImporterType.Default; imp.mipmapEnabled = true; imp.npotScale = TextureImporterNPOTScale.None;
                imp.textureCompression = TextureImporterCompression.Uncompressed; imp.filterMode = FilterMode.Trilinear; imp.wrapMode = TextureWrapMode.Clamp;
                imp.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            var painted = Painted("bohai_region_map.png");
            hud.Map = painted ? painted : map; hud.FullMap = Painted("bohai_region_map_labeled.png") ?? painted ?? map;
            hud.Player = player; hud.RegionName = "渤海平原";
            hud.MapBounds = new Vector4(XMin, ZMin, XMax, ZMax);
            hud.PlaceNames = MapNames.Select(n => n.name).ToArray();
            hud.PlacePositions = MapNames.Select(n => n.at).ToArray();
        }

        // ---------- the post road (user 2026-10-24) ----------
        // Stone-slab paving (Zhuojun's ground_street_stone) with Zhuojun's stone curbs on both edges, a tree every
        // 10 m on each side, and a signpost at every fork.

        const float CurbHeight = 0.2f, CurbWidth = 0.3f, AvenueSpacing = 10f;

        static void StoneRoad(Transform parent, string name, float width, params Vector3[] points)
        {
            var g = T.Group(parent, name);
            string top = K("curb_top", "steps_tread"), side = K("curb_side", "steps_riser");
            var raw = points;
            points = Smooth(raw, 4f);   // rounded bends (user 2026-10-25)
            string paving = K("stone_road_v2", "ground_street_stone");
            Ribbon(g, "Stone paving", T.Mat(paving), points, -width / 2f, width / 2f, 0.005f, T.Metres(paving).y * (width / T.Metres(paving).x));
            for (int i = 0; i + 1 < points.Length; i++)
            {
                Vector3 a = points[i], b = points[i + 1];
                var d = (b - a); d.y = 0f; float len = d.magnitude; d /= len;
                float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                var right = new Vector3(d.z, 0, -d.x);
                foreach (int s in new[] { -1, 1 })
                {
                    var mid = (a + b) / 2f + right * s * (width / 2f + CurbWidth / 2f);
                    if (junctions.Any(j => Vector3.Distance(j, mid) < 5.5f)) continue;   // open where a side road joins
                    var c = T.Group(g, "Curb", mid);
                    T.Quad(c, "Curb top", new Vector3(0, CurbHeight, 0), new Vector3(90f, yaw, 0), new Vector2(CurbWidth, len), T.Mat(top), new Vector2(1f, len / T.Metres(top).x), castShadows: false);
                    foreach (int f in new[] { -1, 1 })
                        T.Quad(c, "Curb face", right * f * CurbWidth / 2f + new Vector3(0, CurbHeight / 2f, 0), new Vector3(0, yaw + (f > 0 ? -90f : 90f), 0), new Vector2(len, CurbHeight),
                            T.Mat(side, twoSided: true), new Vector2(len / T.Metres(side).x, 1f), castShadows: false);
                }
            }
            if (T.Tex("road_edge_band", optional: true))
            {
                float dep = T.Metres("road_edge_band").y, e0 = width / 2f + CurbWidth - 0.2f;
                Ribbon(g, "Road edge R", T.Mat("road_edge_band", clip: true), points, e0, e0 + dep, 0.0058f, T.Metres("road_edge_band").x, vAcross: true);
                Ribbon(g, "Road edge L", T.Mat("road_edge_band", clip: true), points, -e0, -(e0 + dep), 0.0058f, T.Metres("road_edge_band").x, vAcross: true);
            }
            Avenue(g, width, points);
        }

        // A tree every 10 m on both sides of the road, species alternating; none in towns, at places, on other roads,
        // over water or in a wood.
        static void Avenue(Transform parent, float width, Vector3[] points)
        {
            var g = T.Group(parent, "Avenue trees");
            string[] species = new[] { "prop_tree_poplar", "prop_tree_willow", "prop_tree_locust" }.Where(k => T.Tex(k, optional: true)).ToArray();
            int n = 0;
            float carry = AvenueSpacing / 2f;
            for (int i = 0; i + 1 < points.Length; i++)
            {
                Vector3 a = points[i], b = points[i + 1];
                var d = (b - a); d.y = 0f; float len = d.magnitude; d /= len;
                var right = new Vector3(d.z, 0, -d.x);
                for (float t = carry; t < len; t += AvenueSpacing)
                {
                    foreach (int s in new[] { -1, 1 })
                    {
                        var p = a + d * t + right * s * (width / 2f + 2.6f);
                        if (AvenueBlocked(p, a, b)) continue;
                        T.Prop(g, species[(n + (s > 0 ? 1 : 0)) % species.Length], p.x, p.z, scale: 0.9f, solid: true);
                    }
                    n++;
                    carry = t + AvenueSpacing - len;
                }
            }
        }

        static bool AvenueBlocked(Vector3 p, Vector3 a, Vector3 b)
        {
            var cell = At(p.x, p.z);
            if (Blocked(cell) || cell == Cell.Town || cell == Cell.Bridge || cell == Cell.Levee || cell == Cell.YellowRiver) return true;
            if (p.z < LeveeNorth + 2f) return true;
            (float x, float z, float r)[] places =
            {
                (CityCx, -155f, 62f), (330f, -130f, 52f), (460f, 0f, 26f), (545f, 20f, 16f), (-380f, 40f, 44f), (40f, 200f, 44f),
                (-330f, -170f, 20f), (220f, -70f, 32f), (-220f, 252f, 32f),
            };
            foreach (var k in places) if ((p.x - k.x) * (p.x - k.x) + (p.z - k.z) * (p.z - k.z) < k.r * k.r) return true;
            // Clear of every road but this segment (forks and crossings stay open for the signposts).
            var q = new Vector2(p.x, p.z);
            foreach (var road in AllRoads)
                for (int i = 0; i + 1 < road.Length; i++)
                {
                    if ((road[i] == a && road[i + 1] == b)) continue;
                    var ra = new Vector2(road[i].x, road[i].z); var rb = new Vector2(road[i + 1].x, road[i + 1].z);
                    float t = Mathf.Clamp01(Vector2.Dot(q - ra, rb - ra) / (rb - ra).sqrMagnitude);
                    if ((ra + t * (rb - ra) - q).sqrMagnitude < 8.5f * 8.5f) return true;
                }
            return false;
        }

        static readonly Vector3[] WestRoad = { new Vector3(XMin, 0, 40f), new Vector3(-380f, 0, 40f), new Vector3(-250f, 0, -100f), WestGate + new Vector3(-6f, 0, 0) };
        static readonly Vector3[] NorthRoad = { NorthGate + new Vector3(0, 0, 6f), new Vector3(CityCx, 0, -20f), new Vector3(40f, 0, 120f), new Vector3(40f, 0, 200f), new Vector3(40f, 0, ZMax) };
        static readonly Vector3[] EastRoad = { EastGate + new Vector3(6f, 0, 0), new Vector3(150f, 0, -170f), new Vector3(310f, 0, -182f), GaotangFront };
        static readonly Vector3[] PostRoad = { GaotangFront + new Vector3(6f, 0, 0), new Vector3(420f, 0, -100f), PostFront, new Vector3(XMax, 0, 20f) };
        static readonly Vector3[] BeaconSpur = { new Vector3(-250f, 0, -100f), new Vector3(-330f, 0, -165f) };
        static readonly Vector3[] ReedSpur = { new Vector3(150f, 0, -170f), new Vector3(215f, 0, -88f) };
        static readonly Vector3[] TombSpur = { new Vector3(-380f, 0, 40f), new Vector3(-300f, 0, 160f), new Vector3(-224f, 0, 244f) };
        static readonly Vector3[][] AllRoads = { WestRoad, NorthRoad, EastRoad, PostRoad, BeaconSpur, ReedSpur, TombSpur };

        // Signposts at the forks: each way points a little along its road and names where it leads.
        static void Signposts(Transform env, Transform labels)
        {
            var g = T.Group(env, "Signposts");
            void Sign(string name, Vector3 at, params (string to, Vector3 toward, float metres)[] ways)
            {
                var root = T.Group(g, "Signpost " + name, at);
                root.position = at;
                if (T.Tex("prop_signpost", optional: true)) T.Prop(root, "prop_signpost", 0f, 0f, solid: true);
                else T.Prop(root, "Signpost (stand-in)", 0f, 0f, key: "prop_notice_board", solid: true);
                var s = root.gameObject.AddComponent<Signpost>();
                s.Names = ways.Select(w => w.to).ToArray();
                s.Toward = ways.Select(w => w.toward).ToArray();
                s.Metres = ways.Select(w => w.metres).ToArray();
            }
            Sign("西村口", new Vector3(-392f, 0, 29f),
                ("西村", new Vector3(-380f, 0, 40f), 20f), ("往清河", new Vector3(-420f, 0, 40f), 200f),
                ("古冢石阙", new Vector3(-370f, 0, 66f), 330f), ("平原城", new Vector3(-350f, 0, 8f), 330f));
            Sign("烽燧岔口", new Vector3(-242f, 0, -110f),
                ("废烽燧", new Vector3(-270f, 0, -122f), 105f), ("平原城", new Vector3(-220f, 0, -112f), 150f), ("西村", new Vector3(-262f, 0, -86f), 190f));
            Sign("苇塘岔口", new Vector3(158f, 0, -180f),
                ("芦苇荡", new Vector3(170f, 0, -145f), 105f), ("平原城", new Vector3(120f, 0, -167f), 160f), ("高唐", new Vector3(190f, 0, -173f), 185f));
            Sign("北村口", new Vector3(52f, 0, 186f),
                ("北村", new Vector3(40f, 0, 205f), 15f), ("往南皮", new Vector3(40f, 0, 240f), 140f), ("平原城", new Vector3(30f, 0, 160f), 370f));
            Sign("高唐道口", new Vector3(352f, 0, -190f),
                ("高唐", new Vector3(330f, 0, -160f), 30f), ("驿站", new Vector3(370f, 0, -160f), 150f), ("平原城", new Vector3(300f, 0, -182f), 345f));
            Spot("Sign west", new Vector3(-390f, 0, 24f));
            Spot("Sign north", new Vector3(52f, 0, 181f));
            Spot("Bridge", new Vector3(-5f, 0, 46f));
            Spot("Forest edge", new Vector3(-180f, 0, -58f));
            Spot("Hill pass", new Vector3(40f, 0, 286f));
            Spot("Hills", new Vector3(-100f, 0, 262f));
            Spot("Pond", new Vector3(256f, 0, -54f));
            Spot("Checkpoint", new Vector3(528f, 0, 20f));
            // On the levee crest above the ferry (terrain plan sign_dike).
            Sign("大堤渡口", new Vector3(CityCx + 10f, LeveeH, LeveeCrestN - 0.8f),
                ("平原城", new Vector3(CityCx, 0, CityZ0), 40f), ("平原津", new Vector3(CityCx, 0, -258f), 30f),
                ("大堤西段", new Vector3(CityCx - 40f, 0, -228f), 300f), ("大堤东段", new Vector3(CityCx + 60f, 0, -228f), 300f));
        }

        // ---------- terrain from the Codex mask (user 2026-10-24: mountains, plains, rivers, forests) ----------
        // art/bohai-terrain-map/BOHAI_TERRAIN_MASK.png: 1 px per metre, north up, exact colours per terrain class.
        // Mountains become a raised mesh (height grows with the distance from the foot) with pines and rocks; forests
        // dense mixed trees; rivers and marsh flat water; all three impassable through merged box colliders. Bridges
        // stay walkable. Without the mask the region keeps its plain whitebox ground.

        public const string TerrainDir = ArtDir + "/Terrain";
        enum Cell : byte { Plain, Road, Mountain, Foothill, Forest, River, Bridge, YellowRiver, Levee, Town, Marsh, Unknown }

        static readonly (Color32 c, Cell cell)[] MaskColours =
        {
            (new Color32(0x7F, 0xA0, 0x5A, 255), Cell.Plain), (new Color32(0xC8, 0xA0, 0x60, 255), Cell.Road),
            (new Color32(0x6E, 0x5A, 0x46, 255), Cell.Mountain), (new Color32(0x8C, 0x7A, 0x5E, 255), Cell.Foothill),
            (new Color32(0x2E, 0x5A, 0x32, 255), Cell.Forest), (new Color32(0x3C, 0x78, 0xA0, 255), Cell.River),
            (new Color32(0xD2, 0xB4, 0x8C, 255), Cell.Bridge), (new Color32(0x28, 0x5A, 0x82, 255), Cell.YellowRiver),
            (new Color32(0xB4, 0x9A, 0x6E, 255), Cell.Levee), (new Color32(0xA0, 0x8C, 0x78, 255), Cell.Town),
            (new Color32(0x4A, 0x6E, 0x5A, 255), Cell.Marsh),
        };

        static Cell[,] mask;   // [x, z] in metres from (XMin, ZMin)

        static bool LoadMask()
        {
            mask = null;
            string path = TerrainDir + "/bohai_terrain_mask.png";
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
                    var p = px[y * w + x];   // LoadImage rows run bottom-up, so y already counts northward from ZMin
                    var cell = Cell.Unknown; int best = int.MaxValue;
                    foreach (var (c, k) in MaskColours)
                    {
                        int d = Mathf.Abs(c.r - p.r) + Mathf.Abs(c.g - p.g) + Mathf.Abs(c.b - p.b);
                        if (d < best) { best = d; cell = k; }
                    }
                    mask[x, y] = best <= 24 ? cell : Cell.Plain;
                }
            return true;
        }

        static Cell At(float x, float z)
        {
            if (mask == null) return Cell.Plain;
            int ix = Mathf.FloorToInt(x - XMin), iz = Mathf.FloorToInt(z - ZMin);
            if (ix < 0 || iz < 0 || ix >= mask.GetLength(0) || iz >= mask.GetLength(1)) return Cell.Plain;
            return mask[ix, iz];
        }

        // True when every cell within r metres (8 directions) is of the kind.
        static bool Inside(int x, int z, int r, System.Func<Cell, bool> kind)
        {
            int w = mask.GetLength(0), h = mask.GetLength(1);
            for (int dz = -r; dz <= r; dz += r) for (int dx = -r; dx <= r; dx += r)
                if (!kind(mask[Mathf.Clamp(x + dx, 0, w - 1), Mathf.Clamp(z + dz, 0, h - 1)])) return false;
            return true;
        }

        static bool Blocked(Cell c) => c == Cell.Mountain || c == Cell.Foothill || c == Cell.Forest || c == Cell.River || c == Cell.Marsh;

        // Rectangles covering every cell of a class: runs along x, then stacked rows with the same run merged.
        static List<RectInt> Rects(System.Func<Cell, bool> want, int step = 1) => Rects((x, z) => want(mask[x, z]), step);

        static List<RectInt> Rects(System.Func<int, int, bool> want, int step = 1)
        {
            int w = mask.GetLength(0), h = mask.GetLength(1);
            var open = new Dictionary<(int, int), RectInt>();
            var done = new List<RectInt>();
            for (int z = 0; z < h; z += step)
            {
                var runs = new List<(int, int)>();
                for (int x = 0; x < w;)
                {
                    if (!want(x, z)) { x++; continue; }
                    int x0 = x; while (x < w && want(x, z)) x++;
                    runs.Add((x0, x));
                }
                var next = new Dictionary<(int, int), RectInt>();
                foreach (var run in runs)
                {
                    if (open.TryGetValue(run, out var r)) { r.height += step; next[run] = r; open.Remove(run); }
                    else next[run] = new RectInt(run.Item1, z, run.Item2 - run.Item1, step);
                }
                done.AddRange(open.Values);
                open = next;
            }
            done.AddRange(open.Values);
            return done;
        }

        static void Terrain(Transform env, Transform labels)
        {
            if (!LoadMask()) return;
            var g = T.Group(env, "Terrain (mask)");
            var walls = T.Group(g, "Impassable");
            bool Wall(int x, int z)
            {
                var c = mask[x, z];
                if (c == Cell.River)
                {
                    float px = XMin + x + 0.5f, pz = ZMin + z + 0.5f;
                    // The pond blocks over its whole outline (its water is drawn from it); rivers get their own walls.
                    return InPolygon(ReedPond, new Vector2(px, pz)) || RiverDistance(px, pz, out _) > 3f;
                }
                // Woods and hills block only where they visibly stand (user 2026-10-25: invisible walls on grass): the
                // wall starts 2 m inside a wood (its edge row of trees stands there) and 3 m inside a hill (where the slope rises).
                if (c == Cell.Forest) return Inside(x, z, 2, k => k == Cell.Forest);
                if (c == Cell.Mountain || c == Cell.Foothill) return Inside(x, z, 3, k => k == Cell.Mountain || k == Cell.Foothill);
                return Blocked(c);
            }
            foreach (var r in Rects(Wall))
                Solid(walls, new Vector3(XMin + r.x + r.width / 2f, 3f, ZMin + r.y + r.height / 2f), new Vector3(r.width, 6f, r.height));

            // Ponds and marsh from the mask (the rivers are drawn from the plan's centre lines below).
            string water = K("water_channel", "water_yellow_river");
            var wg = T.Group(g, "Ponds and marsh");
            foreach (var r in Rects(c => c == Cell.River || c == Cell.Marsh))
            {
                float cx = XMin + r.x + r.width / 2f, cz = ZMin + r.y + r.height / 2f;
                if (RiverDistance(cx, cz, out _) < Mathf.Max(r.width, r.height) + 3f) continue;
                var size = new Vector2(r.width, r.height);
                T.Quad(wg, "Water", new Vector3(cx, 0.011f, cz), new Vector3(90f, 0, 0), size, T.Mat(water), T.Repeat(water, size), castShadows: false);
            }
            RiverStrips(g);
            Ponds(g);
            Forests(g);
            ForestEdgeRow(g);
            Mountains(g);
            Fields(g);
        }

        // Rivers and bridges after the approved terrain plan (art/bohai-terrain-map/terrain_layout.json): centre lines and
        // widths, drawn as smooth strips with banks; the mask still supplies their colliders, the pond and the marsh.
        static readonly (string name, float width, Vector3[] points)[] Rivers =
        {
            ("笃马河", 24f, new[] { new Vector3(-520f, 0, 255f), new Vector3(-420f, 0, 235f), new Vector3(-355f, 0, 205f), new Vector3(-285f, 0, 175f), new Vector3(-210f, 0, 125f), new Vector3(-135f, 0, 95f), new Vector3(-50f, 0, 72f), new Vector3(20f, 0, 54f), new Vector3(125f, 0, 56f), new Vector3(210f, 0, 90f), new Vector3(305f, 0, 125f), new Vector3(410f, 0, 155f), new Vector3(560f, 0, 170f) }),
            ("西苇汊", 10f, new[] { new Vector3(-355f, 0, 205f), new Vector3(-410f, 0, 150f), new Vector3(-470f, 0, 125f), new Vector3(-560f, 0, 100f) }),
            ("盐苇汊", 10f, new[] { new Vector3(250f, 0, 104.74f), new Vector3(285f, 0, 65f), new Vector3(302f, 0, 15f), new Vector3(285f, 0, -30f), new Vector3(285f, 0, -60f) }),
        };
        static readonly (Vector3 a, Vector3 b, float width)[] Bridges =
        {
            (new Vector3(-14.56f, 0, 43.62f), new Vector3(8.69f, 0, 76.17f), 10f),
            (new Vector3(-299.45f, 0, 160.61f), new Vector3(-272.61f, 0, 190.27f), 7f),
        };

        // The reed pond at the end of the 盐苇汊 (plan pond reed_pond): drawn as its own water surface from the plan's
        // outline, with mud and reeds round it. Its wall comes from the mask (user 2026-10-25: the pond blocked the way
        // but showed as grass, because the strip-shaped water skipped it).
        static readonly Vector2[] ReedPond =
        {
            new Vector2(267f, -40f), new Vector2(276.74f, -40.02f), new Vector2(286f, -37f), new Vector2(296.98f, -43.27f), new Vector2(307f, -51f),
            new Vector2(308.12f, -61.47f), new Vector2(308f, -72f), new Vector2(299.93f, -79.43f), new Vector2(292f, -87f), new Vector2(281.4f, -82.26f),
            new Vector2(270f, -80f), new Vector2(263.82f, -71.65f), new Vector2(260f, -62f), new Vector2(262.15f, -50.57f),
        };

        static bool InPolygon(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }

        // A small pond of noisy outline round a local centre (inside a group), with a reed-mud shore band.
        static void Pool(Transform parent, string name, Vector2 c, float rx, float rz, int seed, string waterKey)
        {
            var rng = new System.Random(seed);
            const int n = 20;
            var pts = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f, k = 0.75f + (float)rng.NextDouble() * 0.4f;
                pts[i] = c + new Vector2(Mathf.Cos(a) * rx * k, Mathf.Sin(a) * rz * k);
            }
            var origin = parent.position;
            var verts = new List<Vector3> { new Vector3(c.x, 0.012f, c.y) };
            var uvs = new List<Vector2> { (c + new Vector2(origin.x, origin.z)) / 12f };
            foreach (var p in pts) { verts.Add(new Vector3(p.x, 0.012f, p.y)); uvs.Add((p + new Vector2(origin.x, origin.z)) / 12f); }
            var tris = new List<int>();
            for (int i = 0; i < n; i++) tris.AddRange(new[] { 0, 1 + (i + 1) % n, 1 + i });
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            if (mesh.normals[0].y < 0f) { tris.Reverse(); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); }
            string path = TerrainDir + "/Bohai_" + name.Replace(' ', '_') + ".asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = T.Mat(waterKey); mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Solid(parent, new Vector3(c.x, 1f, c.y), new Vector3(rx * 1.3f, 2f, rz * 1.3f));
            string band = T.Tex("shore_reed_mud_band", optional: true) ? "shore_reed_mud_band" : null;
            if (band == null) return;
            float dep = T.Metres(band).y;
            for (int i = 0; i < n; i++)
            {
                var a2 = pts[i]; var b2 = pts[(i + 1) % n]; var d2 = b2 - a2; float l2 = d2.magnitude; d2 /= l2;
                var outw = new Vector2(d2.y, -d2.x); if (Vector2.Dot(outw, (a2 + b2) / 2f - c) < 0f) outw = -outw;
                var m2 = (a2 + b2) / 2f + outw * (dep / 2f - 0.8f);
                T.Quad(parent, "Pool shore", new Vector3(m2.x, 0.0135f + i * 0.00002f, m2.y), new Vector3(90f, Mathf.Atan2(outw.x, outw.y) * Mathf.Rad2Deg, 0), new Vector2(l2 + 1.2f, dep),
                    T.Mat(band, clip: true), new Vector2((l2 + 1.2f) / T.Metres(band).x, 1f), castShadows: false);
            }
        }

        static void Ponds(Transform g)
        {
            var pg = T.Group(g, "Reed pond");
            var centre = ReedPond.Aggregate(Vector2.zero, (a, b) => a + b) / ReedPond.Length;
            var verts = new List<Vector3> { new Vector3(centre.x, 0.013f, centre.y) };
            var uvs = new List<Vector2> { centre / 2f };   // water_channel is 2 m per tile
            foreach (var p in ReedPond) { verts.Add(new Vector3(p.x, 0.013f, p.y)); uvs.Add(p / 2f); }
            var tris = new List<int>();
            for (int i = 0; i < ReedPond.Length; i++) { int a = 1 + i, b = 1 + (i + 1) % ReedPond.Length; tris.AddRange(new[] { 0, b, a }); }
            var mesh = new Mesh { name = "Reed pond" };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            if (Vector3.Dot(mesh.normals[0], Vector3.up) < 0f) { tris.Reverse(); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); }
            string meshPath = TerrainDir + "/Bohai_ReedPond.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath)) AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);
            var go = new GameObject("Pond water");
            go.transform.SetParent(pg, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = T.Mat(K("pond_water_v2", K("water_channel", "water_yellow_river")));
            if (T.Tex("pond_water_v2", optional: true)) { for (int u = 0; u < uvs.Count; u++) uvs[u] = uvs[u] / 6f; mesh.SetUVs(0, uvs); }   // 12 m tiles
            string shoreBand = T.Tex("shore_reed_mud_band", optional: true) ? "shore_reed_mud_band" : null;
            if (shoreBand != null)
                for (int i = 0; i < ReedPond.Length; i++)
                {
                    var a2 = ReedPond[i]; var b2 = ReedPond[(i + 1) % ReedPond.Length];
                    var d2 = (b2 - a2); float l2 = d2.magnitude; d2 /= l2;
                    var outw = new Vector2(d2.y, -d2.x); if (Vector2.Dot(outw, (a2 + b2) / 2f - centre) < 0f) outw = -outw;
                    float dep = T.Metres(shoreBand).y; var m2 = (a2 + b2) / 2f + outw * (dep / 2f - 0.8f);
                    float yaw2 = Mathf.Atan2(outw.x, outw.y) * Mathf.Rad2Deg;   // texture up (away from water) along outw
                    T.Quad(pg, "Pond shore", new Vector3(m2.x, 0.0145f + i * 0.00002f, m2.y), new Vector3(90f, yaw2, 0), new Vector2(l2 + 1.2f, dep), T.Mat(shoreBand, clip: true), new Vector2((l2 + 1.2f) / T.Metres(shoreBand).x, 1f), castShadows: false);
                }
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Mud and reeds round the shore.
            var rng = new System.Random(59);
            string[] mud = Have("decal_mud_bank_a", "decal_mud_bank_b");
            string reeds = K("prop_reeds", "prop_grass_tuft");
            for (int i = 0; i < ReedPond.Length; i++)
            {
                var a = ReedPond[i]; var b = ReedPond[(i + 1) % ReedPond.Length];
                for (float t = 0f; t < 1f; t += 0.34f)
                {
                    var q = Vector2.Lerp(a, b, t);
                    var outward = (q - centre).normalized;
                    var shore = q + outward * 0.8f;
                    if (mud.Length > 0 && rng.Next(2) == 0) Decal(pg, mud[rng.Next(mud.Length)], shore.x, shore.y, rng, 0.6f + (float)rng.NextDouble() * 0.4f);
                    var reed = q + outward * (0.3f + (float)rng.NextDouble());
                    T.Prop(pg, reeds, reed.x, reed.y);
                }
            }
        }

        static float RiverDistance(float x, float z, out float halfWidth)
        {
            float best = float.MaxValue; halfWidth = 0f;
            var q = new Vector2(x, z);
            foreach (var (_, width, pts) in Rivers)
                for (int i = 0; i + 1 < pts.Length; i++)
                {
                    var a = new Vector2(pts[i].x, pts[i].z); var b = new Vector2(pts[i + 1].x, pts[i + 1].z);
                    float t = Mathf.Clamp01(Vector2.Dot(q - a, b - a) / (b - a).sqrMagnitude);
                    float d = (a + t * (b - a) - q).magnitude - width / 2f;
                    if (d < best) { best = d; halfWidth = width / 2f; }
                }
            return best;
        }

        static void RiverStrips(Transform g)
        {
            var rg = T.Group(g, "Rivers (plan)");
            string water = K("river_water_v2", K("water_channel", "water_yellow_river"));
            string bank = T.Tex("river_bank_slope", optional: true) ? "river_bank_slope" : T.Tex("river_bank", optional: true) ? "river_bank" : null;
            // Shore bands (art/bohai-water-v2): sand, pebble or reed mud chosen along the river by noise, so the shore
            // changes from stretch to stretch (user 2026-10-25: water needs a gradual edge, not one look everywhere).
            string[] shores = Have("shore_sand_band", "shore_pebble_band", "shore_reed_mud_band");
            if (shores.Length > 0) bank = null;
            string[] mud = Have("decal_mud_bank_a", "decal_mud_bank_b"), meadow = Have("decal_green_meadow_a", "decal_green_meadow_b");
            string reeds = K("prop_reeds", "prop_grass_tuft");
            foreach (var (name, width, raw) in Rivers)
            {
                var r = T.Group(rg, name);
                var pts = Smooth(raw);
                var rng = new System.Random(name.GetHashCode());
                float bankDepth = bank != null ? T.Metres(bank).y : 0f;
                for (int i = 0; i + 1 < pts.Length; i++)
                {
                    float y = 0.012f + (i % 50) * 0.00002f;
                    Strip(r, "Water", water, pts[i], pts[i + 1], width, y);
                    var d = pts[i + 1] - pts[i]; d.y = 0f; float len = d.magnitude; if (len < 0.01f) continue; d /= len;
                    float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                    var right = new Vector3(d.z, 0, -d.x);
                    var mid = (pts[i] + pts[i + 1]) / 2f;
                    // The water's own wall, except under the bridges.
                    if (!UnderBridge(mid, 2f)) Solid(r, mid + Vector3.up * 3f, new Vector3(width - 1f, 6f, len + 0.6f), yaw);
                    foreach (int s2 in new[] { -1, 1 })
                    {
                        var edge = mid + right * s2 * (width / 2f);
                        if (shores.Length > 0)
                        {
                            float n = Mathf.PerlinNoise(mid.x * 0.012f + (s2 > 0 ? 40f : 0f), mid.z * 0.012f);
                            string key = shores[Mathf.Min(shores.Length - 1, (int)(n * shores.Length))];
                            float depth = T.Metres(key).y;
                            T.Quad(r, "Shore", edge + right * s2 * (depth / 2f - 0.8f) + new Vector3(0, 0.015f + (i % 50) * 0.00002f, 0),
                                new Vector3(90f, yaw + (s2 > 0 ? 90f : -90f), 0), new Vector2(len + 0.8f, depth), T.Mat(key, clip: true), new Vector2((len + 0.8f) / T.Metres(key).x, 1f), castShadows: false);
                        }
                        if (bank != null)
                            T.Quad(r, "Bank", edge + right * s2 * (bankDepth / 2f - 0.6f) + new Vector3(0, 0.015f + (i % 50) * 0.00002f, 0),
                                new Vector3(90f, yaw + (s2 > 0 ? 90f : -90f), 0), new Vector2(len + 0.6f, bankDepth), T.Mat(bank, clip: true), new Vector2((len + 0.6f) / T.Metres(bank).x, 1f), castShadows: false);
                        if (UnderBridge(edge, 4f)) continue;
                        if (mud.Length > 0 && rng.Next(3) == 0) Decal(r, mud[rng.Next(mud.Length)], edge.x + right.x * s2 * 1.5f, edge.z + right.z * s2 * 1.5f, rng, 0.7f + (float)rng.NextDouble() * 0.5f);
                        if (meadow.Length > 0 && rng.Next(4) == 0) { var m = edge + right * s2 * (5f + (float)rng.NextDouble() * 6f); if (!Near(m.x, m.z)) Decal(r, meadow[rng.Next(meadow.Length)], m.x, m.z, rng, 0.8f + (float)rng.NextDouble() * 0.6f); }
                        // Reed clumps (2–4 stems) on most stretches, stones where the shore is pebbly.
                        string clump = K("prop_reed_clump_large", reeds), stones = K("prop_shore_stones", "prop_rock_small");
                        if (rng.NextDouble() < 0.7)
                        {
                            int n2 = 2 + rng.Next(3);
                            var c0 = edge + right * s2 * (0.3f + (float)rng.NextDouble() * 1.2f) + d * (float)(rng.NextDouble() - 0.5) * len;
                            for (int k = 0; k < n2; k++)
                            {
                                var q = c0 + d * ((float)rng.NextDouble() - 0.5f) * 2.4f + right * s2 * (float)rng.NextDouble() * 1.4f;
                                if (!Near(q.x, q.z) || At(q.x, q.z) == Cell.River) T.Prop(r, k == 0 ? clump : reeds, q.x, q.z, scale: 0.8f + (float)rng.NextDouble() * 0.5f);
                            }
                        }
                        if (rng.NextDouble() < 0.35) { var q = edge + right * s2 * (1.5f + (float)rng.NextDouble() * 2f) + d * (float)(rng.NextDouble() - 0.5) * len; if (!Near(q.x, q.z)) T.Prop(r, stones, q.x, q.z); }
                    }
                }
            }
            string deck = K("bridge_deck", "dock_planks"), rail = T.Tex("bridge_rail", optional: true) ? "bridge_rail" : null;
            foreach (var (a, b, width) in Bridges)
            {
                var bg = T.Group(rg, "Bridge");
                var d = b - a; float len = d.magnitude; d /= len;
                float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                Strip(bg, "Bridge deck", deck, a, b, width, 0.04f);
                if (rail == null) continue;
                var right = new Vector3(d.z, 0, -d.x);
                float h = T.Metres(rail).y;
                foreach (int s in new[] { -1, 1 })
                {
                    var mid = (a + b) / 2f + right * s * (width / 2f) + new Vector3(0, h / 2f, 0);
                    T.Quad(bg, "Bridge rail", mid, new Vector3(0, yaw - 90f, 0), new Vector2(len, h), T.Mat(rail, clip: true, twoSided: true), new Vector2(len / T.Metres(rail).x, 1f));
                    Solid(bg, mid, new Vector3(0.4f, 2f, len), yaw);
                }
            }
        }

        // Dense mixed woods: a tree every 3–4 m with jitter, clusters on the edge so the wood reads as a wall.
        static void Forests(Transform g)
        {
            var fg = T.Group(g, "Forests");
            string floor = K("forest_floor", "ground_grass");
            // Floor only well inside the wood (3 m in), so its stepped edge hides under the trees.
            bool Inner(int x, int z)
            {
                for (int dz = -3; dz <= 3; dz += 3) for (int dx = -3; dx <= 3; dx += 3)
                    if (At(XMin + x + dx + 0.5f, ZMin + z + dz + 0.5f) != Cell.Forest) return false;
                return true;
            }
            foreach (var r in Rects(Inner))
            {
                var size = new Vector2(r.width, r.height);
                T.Quad(fg, "Forest floor", new Vector3(XMin + r.x + r.width / 2f, 0.009f, ZMin + r.y + r.height / 2f), new Vector3(90f, 0, 0), size, T.Mat(floor), T.Repeat(floor, size), castShadows: false);
            }
            string[] trees = new[] { "prop_tree_oak", "prop_tree_poplar", "prop_tree_jujube", "prop_tree_willow", "prop_tree_elm", "prop_tree_locust" }
                .Where(k => T.Tex(k, optional: true)).ToArray();
            string cluster = T.Tex("prop_tree_dense_cluster", optional: true) ? "prop_tree_dense_cluster" : null;
            string under = K("prop_undergrowth", "prop_bush");
            var rng = new System.Random(71);
            int w = mask.GetLength(0), h = mask.GetLength(1);
            for (int z = 1; z < h - 1; z += 4)
                for (int x = 1; x < w - 1; x += 4)
                {
                    float fx = x + (float)rng.NextDouble() * 3f, fz = z + (float)rng.NextDouble() * 3f;
                    if (At(XMin + fx, ZMin + fz) != Cell.Forest) continue;
                    bool edge = At(XMin + fx, ZMin + fz - 5f) != Cell.Forest || At(XMin + fx - 5f, ZMin + fz) != Cell.Forest || At(XMin + fx + 5f, ZMin + fz) != Cell.Forest;
                    string key = edge && cluster != null && rng.Next(3) == 0 ? cluster : trees[rng.Next(trees.Length)];
                    T.Prop(fg, key, XMin + fx, ZMin + fz, scale: 0.85f + (float)rng.NextDouble() * 0.35f);
                    if (edge && rng.Next(2) == 0) T.Prop(fg, under, XMin + fx + 1.2f, ZMin + fz - 1.6f);
                }
        }

        // A close row of trees along each wood's edge, 2 m in (where its wall begins), so no grass shows in front of
        // an invisible wall.
        static void ForestEdgeRow(Transform g)
        {
            var fg = T.Group(g, "Forest edge row");
            string[] trees = new[] { "prop_tree_oak", "prop_tree_jujube", "prop_tree_willow", "prop_tree_elm", "prop_tree_poplar", "prop_tree_locust" }
                .Where(k => T.Tex(k, optional: true)).ToArray();
            string under = K("prop_undergrowth", "prop_bush");
            var rng = new System.Random(73);
            int w = mask.GetLength(0), h = mask.GetLength(1);
            for (int z = 1; z < h - 1; z += 3)
                for (int x = 1; x < w - 1; x += 3)
                {
                    if (mask[x, z] != Cell.Forest) continue;
                    if (Inside(x, z, 2, k => k == Cell.Forest)) continue;   // only the 2 m edge band
                    T.Prop(fg, trees[rng.Next(trees.Length)], XMin + x + 0.5f, ZMin + z + 0.5f, scale: 0.75f + (float)rng.NextDouble() * 0.35f);
                    if (rng.Next(2) == 0) T.Prop(fg, under, XMin + x + (float)rng.NextDouble() * 2f - 1f, ZMin + z - 1.2f, scale: 0.9f);
                }
        }

        // Hills: a height field over the mountain and foothill cells, rising with the distance from the hill foot,
        // skinned with the slope texture; pines and rocks on the slopes.
        static void Mountains(Transform g)
        {
            var mg = T.Group(g, "Hills");
            int w = mask.GetLength(0), h = mask.GetLength(1);
            const int s = 2;   // metres per vertex
            int gw = w / s + 1, gh = h / s + 1;
            // Distance (in cells) from the nearest non-hill cell, by two chamfer passes.
            var dist = new float[w, h];
            for (int z = 0; z < h; z++) for (int x = 0; x < w; x++) dist[x, z] = mask[x, z] == Cell.Mountain || mask[x, z] == Cell.Foothill ? 9999f : 0f;
            for (int z = 0; z < h; z++) for (int x = 0; x < w; x++)
            {
                if (dist[x, z] == 0f) continue;
                float d = dist[x, z];
                if (x > 0) d = Mathf.Min(d, dist[x - 1, z] + 1f);
                if (z > 0) d = Mathf.Min(d, dist[x, z - 1] + 1f);
                if (x > 0 && z > 0) d = Mathf.Min(d, dist[x - 1, z - 1] + 1.414f);
                if (x < w - 1 && z > 0) d = Mathf.Min(d, dist[x + 1, z - 1] + 1.414f);
                dist[x, z] = d;
            }
            for (int z = h - 1; z >= 0; z--) for (int x = w - 1; x >= 0; x--)
            {
                if (dist[x, z] == 0f) continue;
                float d = dist[x, z];
                if (x < w - 1) d = Mathf.Min(d, dist[x + 1, z] + 1f);
                if (z < h - 1) d = Mathf.Min(d, dist[x, z + 1] + 1f);
                if (x < w - 1 && z < h - 1) d = Mathf.Min(d, dist[x + 1, z + 1] + 1.414f);
                if (x > 0 && z < h - 1) d = Mathf.Min(d, dist[x - 1, z + 1] + 1.414f);
                dist[x, z] = d;
            }
            float Height(int x, int z)
            {
                x = Mathf.Clamp(x, 0, w - 1); z = Mathf.Clamp(z, 0, h - 1);
                float d = dist[x, z];
                if (d <= 0f) return -0.6f;   // just under the ground, so the hill meets the plain along a sloped line, not a stepped edge
                float noise = Mathf.PerlinNoise(x * 0.035f, z * 0.035f);
                return Mathf.Min(36f, Mathf.Pow(d, 0.85f) * 1.4f) * (0.6f + 0.8f * noise);
            }
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            var index = new int[gw, gh];
            for (int gz = 0; gz < gh; gz++)
                for (int gx = 0; gx < gw; gx++)
                {
                    index[gx, gz] = -1;
                    int x = gx * s, z = gz * s;
                    bool near = false;
                    for (int dz = -s; dz <= s && !near; dz++) for (int dx = -s; dx <= s && !near; dx++)
                    {
                        int cx = Mathf.Clamp(x + dx, 0, w - 1), cz = Mathf.Clamp(z + dz, 0, h - 1);
                        near = dist[cx, cz] > 0f;
                    }
                    if (!near) continue;
                    index[gx, gz] = verts.Count;
                    verts.Add(new Vector3(XMin + x, Height(x, z) - 0.05f, ZMin + z));
                    uvs.Add(new Vector2(x / 4f, z / 4f));   // hill_slope is 4 m per tile
                }
            for (int gz = 0; gz + 1 < gh; gz++)
                for (int gx = 0; gx + 1 < gw; gx++)
                {
                    int a = index[gx, gz], b = index[gx + 1, gz], c = index[gx + 1, gz + 1], d = index[gx, gz + 1];
                    if (a < 0 || b < 0 || c < 0 || d < 0) continue;
                    tris.AddRange(new[] { a, d, c, a, c, b });
                }
            if (verts.Count == 0) return;
            var mesh = new Mesh { name = "Bohai hills", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            string meshPath = TerrainDir + "/Bohai_Hills.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath)) AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);
            var go = new GameObject("Hill mesh");
            go.transform.SetParent(mg, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var slope = K("hill_slope", "wb_levee_face");
            var mat = new Material(T.Mat(slope)) { name = "bohai_hills" };
            mat.SetTextureScale("_BaseMap", Vector2.one);
            string mp = $"{MaterialDir}/bohai_hills.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(mp)) AssetDatabase.DeleteAsset(mp);
            AssetDatabase.CreateAsset(mat, mp);
            mr.sharedMaterial = mat;
            // Undergrowth and rocks along the foot, every few metres, so the line between hill and plain reads natural.
            var footRng = new System.Random(89);
            string bush = K("prop_undergrowth", "prop_bush");
            for (int z = 1; z < h - 1; z += 3)
                for (int x = 1; x < w - 1; x += 3)
                {
                    if (dist[x, z] <= 0f || dist[x, z] > 2.5f) continue;
                    if (footRng.Next(3) == 0) continue;
                    float fx = x + (float)footRng.NextDouble() * 2f - 1f, fz = z - 1.5f;
                    if (Near(XMin + fx, ZMin + fz) && At(XMin + fx, ZMin + fz) != Cell.Mountain && At(XMin + fx, ZMin + fz) != Cell.Foothill) continue;
                    T.Prop(mg, footRng.Next(4) == 0 ? K("prop_rock_large", "prop_bush") : bush, XMin + fx, ZMin + fz, scale: 0.9f + (float)footRng.NextDouble() * 0.4f);
                }
            // Pines on the hills, rocks scattered along the foot.
            var rng = new System.Random(83);
            string pine = K("prop_tree_pine", "prop_tree_cypress"), rock = K("prop_rock_large", "prop_bush"), pebble = K("prop_rock_small", "prop_grass_tuft");
            for (int z = 2; z < h - 2; z += 7)
                for (int x = 2; x < w - 2; x += 7)
                {
                    float fx = x + (float)rng.NextDouble() * 5f, fz = z + (float)rng.NextDouble() * 5f;
                    int ix = Mathf.Clamp((int)fx, 0, w - 1), iz = Mathf.Clamp((int)fz, 0, h - 1);
                    float d = dist[ix, iz];
                    if (d <= 0f) continue;
                    float y = Height(ix, iz);
                    if (d < 6f) { if (rng.Next(2) == 0) T.Prop(mg, rng.Next(3) == 0 ? rock : pebble, XMin + fx, ZMin + fz, y: Mathf.Max(0f, y)); }
                    else if (rng.Next(3) != 0) T.Prop(mg, pine, XMin + fx, ZMin + fz, y: y, scale: 0.9f + (float)rng.NextDouble() * 0.4f);
                }
        }

        // Farmland and saline flats in open plain cells away from roads (texture variety only, still walkable).
        static void Fields(Transform g)
        {
            var fg = T.Group(g, "Fields and saline flats");
            string farm = K("farm_field", "ground_vegetable_plot"), salt = K("saline_flat", "wb_sand_bank");
            var rng = new System.Random(97);
            for (int i = 0; i < 40; i++)
            {
                float x = XMin + 30f + (float)rng.NextDouble() * (XMax - XMin - 60f), z = LeveeNorth + 20f + (float)rng.NextDouble() * (ZMax - LeveeNorth - 40f);
                float bw = 24f + (float)rng.NextDouble() * 40f, bd = 16f + (float)rng.NextDouble() * 26f;
                bool clear = true;
                for (float sx = -bw / 2f; sx <= bw / 2f && clear; sx += 4f)
                    for (float sz = -bd / 2f; sz <= bd / 2f && clear; sz += 4f)
                        clear = At(x + sx, z + sz) == Cell.Plain && !Near(x + sx, z + sz);
                if (!clear) continue;
                string key = z < -100f && rng.Next(2) == 0 ? salt : farm;
                if (key == salt && T.Tex("decal_saline_a", optional: true)) continue;   // saline ground comes as decals (GroundVariety)
                T.Patch(fg, key == farm ? "Field" : "Saline flat", key, x - bw / 2f, x + bw / 2f, z - bd / 2f, z + bd / 2f, 0.0055f + i * 0.00002f);
                if (key == farm && T.Tex("decal_field_edge", optional: true))
                {
                    // Ridges with weeds round the field, slightly past its corners, and dry grass along them.
                    float ridge = T.Metres("decal_field_edge").y;
                    void Ridge(Vector3 at, float yaw, float length) =>
                        T.Quad(fg, "Field ridge", at + new Vector3(0, 0.0058f + i * 0.00002f, 0), new Vector3(90f, yaw, 0), new Vector2(length, ridge), T.Mat("decal_field_edge", clip: true), new Vector2(length / T.Metres("decal_field_edge").x, 1f), castShadows: false);
                    Ridge(new Vector3(x, 0, z + bd / 2f), 0f, bw + 1f); Ridge(new Vector3(x, 0, z - bd / 2f), 180f, bw + 1f);
                    Ridge(new Vector3(x + bw / 2f, 0, z), 90f, bd + 1f); Ridge(new Vector3(x - bw / 2f, 0, z), 270f, bd + 1f);
                }
            }
        }

        // ---------- ground transitions (user 2026-10-24: no tofu blocks, nothing uniform) ----------
        // Irregular, dithered-edge ground decals (Codex art/bohai-transitions) layered over the grass: dry grass, bare
        // loess, saline crust and green meadow by broad noise fields; litter spilling out of the woods, scree at the hill
        // foot, mud and meadow along the rivers, ridges round the fields. Decals turn only by right angles and mirror,
        // so the pixel grid stays square.

        static int decalCount;

        static string[] Have(params string[] keys) => keys.Where(k => T.Tex(k, optional: true)).ToArray();

        // Pixel grass/earth decals look like stains on the high-res painted ground, so they stay out once it is in.
        static readonly string[] PixelGroundDecals = { "decal_loess_dirt", "decal_dry_grass", "decal_saline", "decal_wild_flowers", "decal_green_meadow" };
        static bool HdGroundIn => T.IsHd(T.Tex("plain_grass_a", optional: true));

        static void Decal(Transform parent, string key, float x, float z, System.Random rng, float scale = 1f)
        {
            if (HdGroundIn && !T.IsHd(T.Tex(key)) && PixelGroundDecals.Any(key.StartsWith)) return;
            var size = T.Metres(key) * scale;
            float y = 0.0062f + (decalCount++ % 400) * 0.000004f;
            var q = T.Quad(parent, key, new Vector3(x, y, z), new Vector3(90f, rng.Next(4) * 90f, 0), size, T.Mat(key, clip: true), castShadows: false);
            if (rng.Next(2) == 0) q.transform.localScale = new Vector3(-1f, 1f, 1f);
        }

        static void GroundVariety(Transform env)
        {
            string[] dry = Have("decal_dry_grass_a", "decal_dry_grass_b", "decal_dry_grass_c"), dirt = Have("decal_loess_dirt_a", "decal_loess_dirt_b"),
                salt = Have("decal_saline_a", "decal_saline_b"), meadow = Have("decal_green_meadow_a", "decal_green_meadow_b"), flowers = Have("decal_wild_flowers");
            if (dry.Length == 0) return;
            if (splatGround) { dry = new string[0]; meadow = new string[0]; }   // the splat ground already paints dry grass and meadow
            var g = T.Group(env, "Ground variety");
            var rng = new System.Random(131);
            // Broad zones from noise; inside a zone the decals overlap into one continuous ground, toward its edge they
            // thin out (probability ramps with the noise), so zones meet in ragged, natural borders (design: art/bohai-transitions).
            float Ramp(float v, float from, float width) => Mathf.Clamp01((v - from) / width);
            for (float z = LeveeNorth + 4f; z < ZMax - 3f; z += 6f)
                for (float x = XMin + 3f; x < XMax - 3f; x += 6f)
                {
                    float px = x + (float)rng.NextDouble() * 5f, pz = z + (float)rng.NextDouble() * 5f;
                    if (At(px, pz) != Cell.Plain || Near(px, pz)) continue;
                    float dryness = Mathf.PerlinNoise(px * 0.007f + 11f, pz * 0.007f + 3f) * 0.75f + Mathf.PerlinNoise(px * 0.03f, pz * 0.03f) * 0.25f;
                    float saltness = Mathf.PerlinNoise(px * 0.009f + 70f, pz * 0.009f + 20f) - (pz + 120f) * 0.004f;   // more toward the river
                    float river = RiverDistance(px, pz, out _);
                    string[] set = null; float chance = 0f, scale = 1f;
                    if (river < 26f && meadow.Length > 0) { set = meadow; chance = Ramp(26f - river, 0f, 10f); }
                    else if (river < 26f) continue;
                    else if (salt.Length > 0 && saltness > 0.63f) { set = salt; chance = Ramp(saltness, 0.66f, 0.12f) * 0.3f; scale = 0.7f; }
                    else if (dirt.Length > 0 && dryness > 0.68f) { set = dirt; chance = Ramp(dryness, 0.68f, 0.08f) * 0.8f; }
                    else if (dryness > 0.46f && dry.Length > 0) { set = dry; chance = Ramp(dryness, 0.46f, 0.1f); scale = 1.2f; }
                    else if (flowers.Length > 0) { set = flowers; chance = 0.12f; }
                    if (set == null || rng.NextDouble() > chance) continue;
                    Decal(g, set[rng.Next(set.Length)], px, pz, rng, scale * (0.9f + (float)rng.NextDouble() * 0.8f));
                }
        }

        // Litter spilling out of every wood, scree and skirt below every hill.
        static void EdgeDecals(Transform env)
        {
            if (mask == null) return;
            string[] litter = Have("decal_forest_litter_a", "decal_forest_litter_b"), scree = Have("decal_scree_a", "decal_scree_b");
            var g = T.Group(env, "Edge transitions");
            var rng = new System.Random(137);
            int w = mask.GetLength(0), h = mask.GetLength(1);
            for (int z = 2; z < h - 2; z += 4)
                for (int x = 2; x < w - 2; x += 4)
                {
                    var c = mask[x, z];
                    if (c != Cell.Plain && c != Cell.Road) continue;
                    // Nearest edge type within 5 m.
                    bool wood = false, hill = false;
                    for (int dz = -5; dz <= 5; dz += 5) for (int dx = -5; dx <= 5; dx += 5)
                    {
                        var n = mask[Mathf.Clamp(x + dx, 0, w - 1), Mathf.Clamp(z + dz, 0, h - 1)];
                        wood |= n == Cell.Forest; hill |= n == Cell.Mountain || n == Cell.Foothill;
                    }
                    float px = XMin + x + (float)rng.NextDouble() * 3f, pz = ZMin + z + (float)rng.NextDouble() * 3f;
                    if (Near(px, pz) && c != Cell.Plain) continue;
                    if (wood && litter.Length > 0 && rng.Next(3) != 0) Decal(g, litter[rng.Next(litter.Length)], px, pz, rng, 0.7f + (float)rng.NextDouble() * 0.6f);
                    if (hill && scree.Length > 0 && rng.Next(3) != 0) Decal(g, scree[rng.Next(scree.Length)], px, pz, rng, 0.7f + (float)rng.NextDouble() * 0.6f);
                }
        }

        // Rivers as smooth curves: Catmull-Rom through the plan's points, about every 5 m.
        static Vector3[] Smooth(Vector3[] pts, float step = 5f)
        {
            var o = new List<Vector3>();
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                Vector3 p0 = pts[Mathf.Max(0, i - 1)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Mathf.Min(pts.Length - 1, i + 2)];
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(p1, p2) / step));
                for (int k = 0; k < n; k++)
                {
                    float t = k / (float)n, t2 = t * t, t3 = t2 * t;
                    o.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }
            o.Add(pts[pts.Length - 1]);
            return o.ToArray();
        }

        static bool UnderBridge(Vector3 p, float margin) =>
            Bridges.Any(b =>
            {
                var a = new Vector2(b.a.x, b.a.z); var c = new Vector2(b.b.x, b.b.z); var q = new Vector2(p.x, p.z);
                float t = Mathf.Clamp01(Vector2.Dot(q - a, c - a) / (c - a).sqrMagnitude);
                return (a + t * (c - a) - q).magnitude < b.width / 2f + margin;
            });

        // ---------- splat ground (user 2026-10-25: the plain looked like one small repeating carpet) ----------
        // One ground quad over the region drawn by ThreeKingdoms/GroundSplat with the Codex V2 ground set (12 m tiles,
        // art/bohai-ground-v2): base grass, then a second grass, dry grass, meadow and loess where a 1 px/m control map
        // says so; the shader breaks every border into ragged pixel clusters.

        static bool splatGround;

        static void SplatGround(Transform env)
        {
            splatGround = false;
            var shader = Shader.Find("ThreeKingdoms/GroundSplat");
            string[] keys = { "plain_grass_a", "plain_grass_b", "plain_grass_dry", "plain_meadow", "plain_loess" };
            if (!shader || keys.Any(k => !T.Tex(k, optional: true))) return;
            int w = Mathf.RoundToInt(XMax - XMin), h = Mathf.RoundToInt(ZMax - ZMin);
            var px = new Color32[w * h];
            for (int z = 0; z < h; z++)
                for (int x = 0; x < w; x++)
                {
                    float wx = XMin + x + 0.5f, wz = ZMin + z + 0.5f;
                    float second = Mathf.PerlinNoise(wx * 0.012f + 5f, wz * 0.012f + 9f);
                    float dryness = Mathf.PerlinNoise(wx * 0.007f + 11f, wz * 0.007f + 3f) * 0.75f + Mathf.PerlinNoise(wx * 0.03f, wz * 0.03f) * 0.25f;
                    float river = RiverDistance(wx, wz, out _);
                    float bare = Mathf.PerlinNoise(wx * 0.02f + 31f, wz * 0.02f + 17f);
                    byte B(float v) => (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);
                    px[z * w + x] = new Color32(
                        B((second - 0.45f) / 0.15f),                       // R: the yellower main grass in broad drifts
                        B((dryness - 0.48f) / 0.12f),                      // G: dry grass zones
                        B((22f - river) / 12f),                            // B: meadow along the rivers
                        B((dryness - 0.66f) / 0.08f * (bare > 0.45f ? 1f : 0.3f)));   // A: bare loess inside the driest zones
                }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, linear: true);
            tex.SetPixels32(px); tex.Apply();
            string path = TerrainDir + "/bohai_ground_control.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.sRGBTexture = false; imp.mipmapEnabled = false; imp.npotScale = TextureImporterNPOTScale.None;
            imp.textureCompression = TextureImporterCompression.Uncompressed; imp.filterMode = FilterMode.Bilinear; imp.wrapMode = TextureWrapMode.Clamp;
            imp.alphaIsTransparency = false;
            imp.SaveAndReimport();

            var m = new Material(shader) { name = "bohai_ground_splat" };
            m.SetTexture("_Control", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            m.SetTexture("_Base", T.Tex(keys[0])); m.SetTexture("_LayerR", T.Tex(keys[1])); m.SetTexture("_LayerG", T.Tex(keys[2]));
            m.SetTexture("_LayerB", T.Tex(keys[3])); m.SetTexture("_LayerA", T.Tex(keys[4]));
            m.SetFloat("_TileMetres", T.Metres(keys[0]).x);
            m.SetFloat("_Smooth", T.IsHd(T.Tex(keys[0])) ? 1f : 0f);
            m.SetVector("_Bounds", new Vector4(XMin, ZMin, XMax, ZMax));
            string mp = $"{MaterialDir}/bohai_ground_splat.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(mp)) AssetDatabase.DeleteAsset(mp);
            AssetDatabase.CreateAsset(m, mp);
            var size = new Vector2(XMax - XMin + 120f, ZMax + 80f - LeveeNorth + 4f);
            T.Quad(env, "Splat ground", new Vector3((XMin + XMax) / 2f, -0.009f, (LeveeNorth - 4f + ZMax + 80f) / 2f), new Vector3(90f, 0, 0), size, m, castShadows: false);
            splatGround = true;
        }

        // ---------- landmarks and Gaotang after their Codex designs (art/bohai-landmarks-b1, art/gaotang-b1) ----------
        // User 2026-10-25: put textures on every grey box and refine where needed. Each falls back to its whitebox
        // when the art is missing.

        static bool HasAll(params string[] keys) => keys.All(k => T.Tex(k, optional: true));

        // A free-standing block whose four faces carry front/side art (front and back the same), top capped.
        static void TexturedBlock(Transform parent, string name, Vector3 baseCentre, string front, string side, string top, float yaw = 0f)
        {
            var f = T.Metres(front); var sd = T.Metres(side);
            var g = T.Group(parent, name, baseCentre, yaw);
            g.gameObject.AddComponent<Occluder>();
            float w = f.x, d = sd.x, h = Mathf.Max(f.y, sd.y);
            T.Quad(g, "Front", new Vector3(0, f.y / 2f, -d / 2f), Vector3.zero, f, T.Mat(front, clip: true));
            T.Quad(g, "Back", new Vector3(0, f.y / 2f, d / 2f), new Vector3(0, 180f, 0), f, T.Mat(front, clip: true));
            T.Quad(g, "Side L", new Vector3(-w / 2f, sd.y / 2f, 0), new Vector3(0, 90f, 0), sd, T.Mat(side, clip: true));
            T.Quad(g, "Side R", new Vector3(w / 2f, sd.y / 2f, 0), new Vector3(0, -90f, 0), sd, T.Mat(side, clip: true));
            if (top != null)
            {
                var ts = new Vector2(w * 0.8f, d * 0.8f);
                T.Quad(g, "Top", new Vector3(0, h * 0.82f, 0), new Vector3(90f, 0, 0), ts, T.Mat(top), T.Repeat(top, ts), castShadows: false);
            }
            T.Solid(g, new Vector3(0, h / 2f, 0), new Vector3(w * 0.9f, h, d * 0.9f));
        }

        // A smooth earthen mound (elliptical dome) with planar UVs, for the tomb.
        static void Mound(Transform parent, string name, Vector3 centre, float rx, float rz, float height, string tex)
        {
            const int seg = 36, rings = 10;
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            var tile = T.Metres(tex);
            for (int r = 0; r <= rings; r++)
            {
                float t = r / (float)rings, rad = Mathf.Sin(t * Mathf.PI / 2f), y = height * Mathf.Cos(t * Mathf.PI / 2f);
                for (int s = 0; s <= seg; s++)
                {
                    float a = s / (float)seg * Mathf.PI * 2f;
                    var v = new Vector3(Mathf.Cos(a) * rx * rad, y, Mathf.Sin(a) * rz * rad);
                    verts.Add(v); uvs.Add(new Vector2(v.x / tile.x, v.z / tile.y));
                }
            }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < seg; s++)
                {
                    int a = r * (seg + 1) + s, b = a + seg + 1;
                    tris.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            if (mesh.normals[verts.Count / 2].y < 0f) { tris.Reverse(); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); }
            string path = TerrainDir + "/" + name.Replace(' ', '_') + ".asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = centre;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = T.Mat(tex);
            go.AddComponent<Occluder>();
            var col = new GameObject("Collider").AddComponent<BoxCollider>();
            col.transform.SetParent(go.transform, false);
            col.center = new Vector3(0, height / 2f, 0); col.size = new Vector3(rx * 1.6f, height, rz * 1.6f);
        }

        static bool Landmark(Transform root, string kind)
        {
            switch (kind)
            {
                case "beacon":
                    if (!HasAll("beacon_ruin_front", "beacon_ruin_side")) return false;
                    TexturedBlock(root, "Beacon tower (ruin)", Vector3.zero, "beacon_ruin_front", "beacon_ruin_side", K("hill_top", null));
                    string rubble = K("prop_rubble_heap", "prop_rock_small");
                    foreach (var (x, z) in new[] { (-5f, -4.5f), (-2.4f, -5.5f), (3.6f, -4.8f), (5.4f, -2f), (-5.6f, 1.5f) }) T.Prop(root, rubble, x, z, solid: true);
                    var rng = new System.Random(5);
                    string[] scree = Have("decal_scree_a", "decal_scree_b");
                    for (int i = 0; i < 6 && scree.Length > 0; i++) Decal(root, scree[rng.Next(scree.Length)], root.position.x - 6f + (float)rng.NextDouble() * 12f, root.position.z - 6f + (float)rng.NextDouble() * 10f, rng);
                    return true;
                case "tomb":
                    if (!HasAll("tomb_mound_slope", "que_pillar_front", "que_pillar_side")) return false;
                    Mound(root, "Tomb mound", root.position, 13f, 11f, 7f, "tomb_mound_slope");
                    TexturedBlock(root, "Que W", new Vector3(-4.5f, 0, -18f), "que_pillar_front", "que_pillar_side", null);
                    TexturedBlock(root, "Que E", new Vector3(4.5f, 0, -18f), "que_pillar_front", "que_pillar_side", null);
                    string beast = K("prop_stone_beast", "prop_rock_large"), tablet = K("prop_stone_tablet", "prop_rock_large");
                    T.Prop(root, beast, -3f, -14f, solid: true); T.Prop(root, beast, 3f, -14f, solid: true);
                    T.Prop(root, tablet, 0f, -12.5f, solid: true);
                    return true;
                default: return false;
            }
        }

        // Gaotang, a frontier garrison town (one city, one look): grey-brick walls with merlons, an iron-studded gate,
        // dark-brown tile roofs, a gravel square with a weapon cart, alarm drum and red banners.
        static bool GaotangRefined(Transform g, float cx, float wz)
        {
            if (!HasAll("gaotang_wall_face", "gaotang_gate_front", "gaotang_hall_front", "gaotang_hall_side")) return false;
            void Wall(string name, Vector3 a, Vector3 b)
            {
                bool alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
                float len = alongX ? Mathf.Abs(b.x - a.x) : Mathf.Abs(b.z - a.z);
                var run = T.Group(g, name, (a + b) / 2f, alongX ? 0f : 90f);
                run.gameObject.AddComponent<Occluder>();
                var face = T.Metres("gaotang_wall_face"); var size = new Vector2(len, face.y);
                T.Quad(run, "Face out", new Vector3(0, face.y / 2f, -2f), Vector3.zero, size, T.Mat("gaotang_wall_face", clip: true), new Vector2(len / face.x, 1f));
                T.Quad(run, "Face in", new Vector3(0, face.y / 2f, 2f), new Vector3(0, 180f, 0), size, T.Mat("gaotang_wall_face", clip: true), new Vector2(len / face.x, 1f));
                string topKey = K("gaotang_wall_top", "citywall_walkway");
                var top = new Vector2(len, 4f);
                T.Quad(run, "Walk", new Vector3(0, face.y * 0.86f, 0), new Vector3(90f, 0, 0), top, T.Mat(topKey), T.Repeat(topKey, top), castShadows: false);
                T.Solid(run, new Vector3(0, face.y / 2f, 0), new Vector3(len, face.y, 4f));
            }
            Wall("Wall W", new Vector3(cx - 40f, 0, wz), new Vector3(cx - 5f, 0, wz));
            Wall("Wall E", new Vector3(cx + 5f, 0, wz), new Vector3(cx + 40f, 0, wz));
            Wall("Wall side W", new Vector3(cx - 38f, 0, wz + 2f), new Vector3(cx - 38f, 0, wz + 50f));
            Wall("Wall side E", new Vector3(cx + 38f, 0, wz + 2f), new Vector3(cx + 38f, 0, wz + 50f));
            Wall("Wall back", new Vector3(cx - 40f, 0, wz + 52f), new Vector3(cx + 40f, 0, wz + 52f));
            var gate = T.Group(g, "Gaotang gate", new Vector3(cx, 0, wz));
            gate.gameObject.AddComponent<Occluder>();
            var gf = T.Metres("gaotang_gate_front");
            T.Quad(gate, "Gate outside", new Vector3(0, gf.y / 2f, -2.05f), Vector3.zero, gf, T.Mat("gaotang_gate_front", clip: true, twoSided: true));
            T.Quad(gate, "Gate inside", new Vector3(0, gf.y / 2f, 2.05f), new Vector3(0, 180f, 0), gf, T.Mat("gaotang_gate_front", clip: true, twoSided: true));
            string face2 = K("gaotang_wall_face", "citywall_face");
            foreach (int s in new[] { -1, 1 })
            {
                T.Quad(gate, "Passage side", new Vector3(s * 2f, 2.25f, 0), new Vector3(0, s * -90f, 0), new Vector2(4f, 4.5f), T.Mat(face2, clip: true), new Vector2(4f / T.Metres(face2).x, 0.75f));
                T.Solid(gate, new Vector3(s * 3.5f, gf.y / 2f, 0), new Vector3(3f, gf.y, 4f));
            }
            T.Quad(gate, "Passage ceiling", new Vector3(0, 4.5f, 0), new Vector3(-90f, 0, 0), new Vector2(4f, 4f), T.Mat("timber_beam"), T.Repeat("timber_beam", new Vector2(4f, 4f)), castShadows: false);
            T.Solid(gate, new Vector3(0, (4.5f + gf.y) / 2f, 0), new Vector3(4f, gf.y - 4.5f, 4f));
            string square = K("gaotang_square", "ground_street_stone");
            T.Patch(g, "Gaotang square", square, cx - 16f, cx + 16f, wz + 2f, wz + 30f, 0.006f);
            EarthPatch(g, new Vector3(cx, 0, wz - 12f), 12f, 10f, 67);
            string roof = T.Tex("gaotang_roof", optional: true) ? "gaotang_roof" : null;
            var hall = T.Building(g, "County hall (县府)", new Vector3(cx, 0, wz + 31f), 0f, "gaotang_hall_front", "gaotang_hall_side", null, roofKey: "tile", roofTexture: roof);
            T.Stairs(g, "Hall steps", new Vector3(cx, 0, wz + 31f), 4f, 3, 0.3f, 0.4f, 0.6f);
            if (HasAll("gaotang_shop_front", "gaotang_shop_side"))
                T.Building(g, "Shop (enterable)", new Vector3(cx - 26f, 0, wz + 18f), 90f, "gaotang_shop_front", "gaotang_shop_side", null, roofKey: "tile", roofTexture: roof,
                    interior: "interior_shop_back_wall", floor: "floor_wood_planks");
            var props = T.Group(g, "Square props");
            if (T.Tex("prop_gaotang_watch_drum", optional: true)) T.Prop(props, "prop_gaotang_watch_drum", cx + 10f, wz + 27f, solid: true);
            if (T.Tex("prop_spear_rack_cart", optional: true)) { T.Prop(props, "prop_spear_rack_cart", cx + 12f, wz + 10f, solid: true); T.Prop(props, "prop_spear_rack_cart", cx - 12f, wz + 6f, solid: true); }
            string banner = K("prop_gaotang_banner", "prop_banner_han");
            foreach (float x in new[] { -6f, 6f }) T.Prop(props, banner, cx + x, wz + 29.5f);
            foreach (float x in new[] { -6.5f, 6.5f }) T.Prop(props, banner, cx + x, wz - 3f);
            T.Prop(props, "prop_army_tent", cx + 26f, wz + 40f, solid: true);
            T.Prop(props, "prop_army_tent", cx - 26f, wz + 40f, solid: true);
            return true;
        }

        // ---------- road ribbons (user 2026-10-25: no square corners or square road ends) ----------
        // Roads are smooth ribbons along a Catmull-Rom curve through their points, so bends are rounded. Each side gets
        // a ragged edge band onto the grass; an open end (a road that stops in open country) tapers and breaks up into
        // loose earth decals instead of stopping in a straight line. Curbs leave gaps where side roads join.

        static Mesh roadContainer;
        static readonly List<Vector3> junctions = new List<Vector3>();
        static int ribbonCount;

        static void BeginRibbons()
        {
            string path = TerrainDir + "/Bohai_Roads.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path)) AssetDatabase.DeleteAsset(path);
            roadContainer = new Mesh { name = "Bohai_Roads" };
            AssetDatabase.CreateAsset(roadContainer, path);
            junctions.Clear();
            ribbonCount = 0;
        }

        // A ribbon from offset `inner` to `outer` metres right of the centre line (negative = left), u across 0..1
        // (or uScale), v along by tileLength. taper: per-point width factor (null = none).
        static GameObject Ribbon(Transform parent, string name, Material mat, Vector3[] pts, float inner, float outer, float y, float tileLength, float[] taper = null, bool vAcross = false, bool band = false)
        {
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            float dist = 0f;
            for (int i = 0; i < pts.Length; i++)
            {
                var tan = (pts[Mathf.Min(pts.Length - 1, i + 1)] - pts[Mathf.Max(0, i - 1)]); tan.y = 0f; tan.Normalize();
                var right = new Vector3(tan.z, 0, -tan.x);
                if (i > 0) { var d = pts[i] - pts[i - 1]; d.y = 0f; dist += d.magnitude; }
                float k = taper != null ? taper[i] : 1f;
                verts.Add(pts[i] + right * inner * k + Vector3.up * y);
                verts.Add(pts[i] + right * (band ? inner * k + (outer - inner) : outer * k) + Vector3.up * y);
                float along = dist / tileLength;
                if (vAcross) { uvs.Add(new Vector2(along, 0f)); uvs.Add(new Vector2(along, 1f)); }
                else { uvs.Add(new Vector2(0f, along)); uvs.Add(new Vector2(1f, along)); }
            }
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                int a = i * 2;
                tris.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 });
            }
            var mesh = new Mesh { name = $"{name} {ribbonCount++}", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            if (mesh.normals.Length > 0 && mesh.normals[0].y < 0f) { tris.Reverse(); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); }
            mesh.RecalculateBounds();
            AssetDatabase.AddObjectToAsset(mesh, roadContainer);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = Vector3.zero;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        // True when p is within `r` of any road other than `self` (so a road end there is a junction, not open).
        static bool NearOtherRoad(Vector3 p, Vector3[] self, float r)
        {
            var q = new Vector2(p.x, p.z);
            foreach (var road in AllRoads.Concat(ExtraRoads))
            {
                if (road == self) continue;
                for (int i = 0; i + 1 < road.Length; i++)
                {
                    var a = new Vector2(road[i].x, road[i].z); var b = new Vector2(road[i + 1].x, road[i + 1].z);
                    float t = Mathf.Clamp01(Vector2.Dot(q - a, b - a) / Mathf.Max(0.001f, (b - a).sqrMagnitude));
                    if ((a + t * (b - a) - q).magnitude < r) return true;
                }
            }
            return false;
        }

        static bool OpenEnd(Vector3 p, Vector3[] self) =>
            !NearOtherRoad(p, self, 6f) && p.z > LeveeNorth + 3f && Mathf.Abs(p.x) < XMax - 2f && p.z < ZMax - 2f && At(p.x, p.z) != Cell.Town
            && !(p.x > CityX0 - 12f && p.x < CityX1 + 12f && p.z > CityZ0 - 12f && p.z < CityZ1 + 12f);

        // Width factors: 1 along the road, easing to 0.55 over the last 5 m of an open end.
        static float[] Taper(Vector3[] pts, bool openStart, bool openEnd, bool flare = true)
        {
            var k = new float[pts.Length];
            float total = 0f; var at = new float[pts.Length];
            for (int i = 1; i < pts.Length; i++) { total += Vector3.Distance(pts[i], pts[i - 1]); at[i] = total; }
            for (int i = 0; i < pts.Length; i++)
            {
                float f = 1f;
                if (openStart) f = Mathf.Min(f, Mathf.Lerp(0.55f, 1f, Mathf.SmoothStep(0f, 1f, at[i] / 5f)));
                if (openEnd) f = Mathf.Min(f, Mathf.Lerp(0.55f, 1f, Mathf.SmoothStep(0f, 1f, (total - at[i]) / 5f)));
                // Where a side road meets another road its mouth flares out, like worn ground at a fork (the first 6 m lie
                // under the main road, so the flare eases back over the 9 m after that).
                if (flare && !openStart) f = Mathf.Max(f, Mathf.Lerp(1.8f, 1f, Mathf.SmoothStep(0f, 1f, (at[i] - 6f) / 9f)));
                if (flare && !openEnd) f = Mathf.Max(f, Mathf.Lerp(1.8f, 1f, Mathf.SmoothStep(0f, 1f, (total - at[i] - 6f) / 9f)));
                k[i] = f;
            }
            return k;
        }

        // Rounds the open ends: the centre line runs on past the end by one half-width while the width closes on a
        // quarter circle, so a road that stops in the open finishes in a rounded tip, never a straight cut.
        static (Vector3[] pts, float[] k) RoundEnds(Vector3[] pts, float[] k, bool openStart, bool openEnd, float halfWidth)
        {
            var p = new List<Vector3>(pts); var w = new List<float>(k);
            const int n = 6;
            if (openEnd)
            {
                var o = pts[pts.Length - 1] - pts[pts.Length - 2]; o.y = 0f; o.Normalize();
                float r = halfWidth * k[k.Length - 1], k0 = k[k.Length - 1];
                for (int i = 1; i <= n; i++) { float a = i / (float)n * Mathf.PI / 2f; p.Add(pts[pts.Length - 1] + o * r * Mathf.Sin(a)); w.Add(k0 * Mathf.Max(0.02f, Mathf.Cos(a))); }
            }
            if (openStart)
            {
                var o = pts[0] - pts[1]; o.y = 0f; o.Normalize();
                float r = halfWidth * k[0], k0 = k[0];
                for (int i = 1; i <= n; i++) { float a = i / (float)n * Mathf.PI / 2f; p.Insert(0, pts[0] + o * r * Mathf.Sin(a)); w.Insert(0, k0 * Mathf.Max(0.02f, Mathf.Cos(a))); }
            }
            return (p.ToArray(), w.ToArray());
        }

        // Loose earth breaking up an open road end: decals of bare loess and dry grass fanning out past the end.
        static void EndScatter(Transform parent, Vector3 end, Vector3 outward, float width, int seed)
        {
            string[] dirt = Have("decal_loess_dirt_a", "decal_loess_dirt_b"), dry = Have("decal_dry_grass_a", "decal_dry_grass_b", "decal_dry_grass_c");
            if (dirt.Length == 0) return;
            var rng = new System.Random(seed);
            outward.y = 0f; outward.Normalize();
            var right = new Vector3(outward.z, 0, -outward.x);
            for (int i = 0; i < 7; i++)
            {
                float fwd = -1f + (float)rng.NextDouble() * 6f, side = ((float)rng.NextDouble() - 0.5f) * width * (1.2f + fwd * 0.15f);
                var p = end + outward * fwd + right * side;
                Decal(parent, i < 5 ? dirt[rng.Next(dirt.Length)] : (dry.Length > 0 ? dry[rng.Next(dry.Length)] : dirt[0]), p.x, p.z, rng, 0.6f + (float)rng.NextDouble() * 0.5f);
            }
        }

        // A dirt road ribbon with edge bands and broken-up open ends.
        static void DirtRibbon(Transform parent, string name, float width, Vector3[] raw)
        {
            var g = T.Group(parent, name);
            var pts = Smooth(raw, 2f);
            bool o0 = OpenEnd(raw[0], raw), o1 = OpenEnd(raw[raw.Length - 1], raw);

            var taper = Taper(pts, o0, o1);
            (pts, taper) = RoundEnds(pts, taper, o0, o1, width / 2f);
            string key = K("dirt_road_v2", "ground_packed_earth");
            Ribbon(g, "Road", T.Mat(key), pts, -width / 2f, width / 2f, 0.0045f, T.Metres(key).y * (width / T.Metres(key).x), taper);
            if (T.Tex("road_edge_band", optional: true))
            {
                float dep = T.Metres("road_edge_band").y;
                Ribbon(g, "Edge R", T.Mat("road_edge_band", clip: true), pts, width / 2f - 0.4f, width / 2f - 0.4f + dep, 0.0047f, T.Metres("road_edge_band").x, taper, vAcross: true, band: true);
                Ribbon(g, "Edge L", T.Mat("road_edge_band", clip: true), pts, -(width / 2f - 0.4f), -(width / 2f - 0.4f + dep), 0.0047f, T.Metres("road_edge_band").x, taper, vAcross: true, band: true);
            }
            if (o0) EndScatter(g, raw[0], raw[0] - raw[1], width, name.GetHashCode());
            if (o1) EndScatter(g, raw[raw.Length - 1], raw[raw.Length - 1] - raw[raw.Length - 2], width, name.GetHashCode() + 1);
            if (!o0) junctions.Add(raw[0]);
            if (!o1) junctions.Add(raw[raw.Length - 1]);
        }

        static readonly Vector3[][] ExtraRoads =
        {
            new[] { new Vector3(40f, 0, 200f), new Vector3(-10f, 0, 214f) },
            new[] { SouthGate + new Vector3(0, 0, -5f), new Vector3(CityCx, 0, LeveeNorth + 0.5f) },
        };

        // A patch of bare ground with a ragged outline (village threshing floors, yards): a filled blob whose radius
        // wanders, ringed by the same broken earth-to-grass band as the roads, plus a few loose clods past the rim.
        static void EarthPatch(Transform parent, Vector3 centre, float rx, float rz, int seed)
        {
            var rng = new System.Random(seed);
            const int n = 96;
            float ph1 = (float)rng.NextDouble() * 6.3f, ph2 = (float)rng.NextDouble() * 6.3f;
            var rim = new Vector3[n + 1];
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                float f = 0.86f + 0.08f * Mathf.Sin(a * 3f + ph1) + 0.05f * Mathf.Sin(a * 5f + ph2) + 0.025f * Mathf.Sin(a * 11f + ph1 * 2f);
                rim[i] = centre + new Vector3(Mathf.Cos(a) * rx * f, 0, Mathf.Sin(a) * rz * f);
            }
            rim[n] = rim[0];
            // Loess fill, the same light earth as the road edge band, so the rim reads as worn ground and not an outline.
            string fill = T.Tex("packed_earth_hd", optional: true) ? "ground_packed_earth" : K("sand_flat_v2", "ground_packed_earth");
            var tile = T.Metres(fill);
            var verts = new List<Vector3> { centre + Vector3.up * 0.0040f }; var uvs = new List<Vector2> { new Vector2(centre.x / tile.x, centre.z / tile.y) }; var tris = new List<int>();
            for (int i = 0; i < n; i++) { verts.Add(rim[i] + Vector3.up * 0.0040f); uvs.Add(new Vector2(rim[i].x / tile.x, rim[i].z / tile.y)); }
            for (int i = 0; i < n; i++) tris.AddRange(new[] { 0, 1 + (i + 1) % n, 1 + i });
            var mesh = new Mesh { name = $"Earth patch {ribbonCount++}" };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals();
            if (mesh.normals[0].y < 0f) { tris.Reverse(); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); }
            mesh.RecalculateBounds();
            AssetDatabase.AddObjectToAsset(mesh, roadContainer);
            var go = new GameObject("Earth patch"); go.transform.SetParent(parent, false); go.transform.position = Vector3.zero;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = T.Mat(fill); mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (T.Tex("road_edge_band", optional: true))
            {
                string edge = K("earth_edge_hd", "road_edge_band");
                Ribbon(parent, "Earth patch rim", T.Mat(edge, clip: true), rim, -0.8f, T.Metres(edge).y * 0.8f, 0.0044f, T.Metres(edge).x, vAcross: true, band: true);
            }
            string[] dirt = Have("decal_loess_dirt_a", "decal_loess_dirt_b");
            for (int i = 0; i < 4 && dirt.Length > 0; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, d = 1.05f + (float)rng.NextDouble() * 0.2f;
                Decal(parent, dirt[rng.Next(dirt.Length)], centre.x + Mathf.Cos(a) * rx * d, centre.z + Mathf.Sin(a) * rz * d, rng, 0.5f + (float)rng.NextDouble() * 0.3f);
            }
        }

        // ---------- people ----------

        // Stand-ins from the Zhuojun residents until the chapter's own characters are drawn: Jian Yong at the edge of
        // the office square, Tian Yu at the barracks gate, the ferryman, guards at Gaotang and the checkpoint.
        static void People(Transform people)
        {
            var g = T.Group(people, "Stand-ins");
            void Npc(string name, string id, Vector3 at, int frame = 0)
            {
                var npc = HD2DSampleBuilder.Character(name, ResidentImporter.KeyFor(id), at, frame, isPlayer: false);
                npc.transform.SetParent(g, true);
            }
            Npc("简雍（暂代）", "N24", new Vector3(CityCx + 12.6f, 0, -152f));
            Npc("田豫（暂代）", "N01", new Vector3(-30.2f, 0, -144.4f));
            Npc("渡口船夫（暂代）", "N12", new Vector3(-58.5f, 0.35f, -262f));
            Npc("高唐守卫", "N01", new Vector3(GaotangFront.x + 3.5f, 0, -154f));
            Npc("哨卡守兵 甲", "N01", new Vector3(541f, 0, 15f), 1);
            Npc("哨卡守兵 乙", "N01", new Vector3(541f, 0, 25f), 1);
            Npc("驿卒", "N07", new Vector3(PostFront.x, 0, PostFront.z - 1.6f));
        }

        // ---------- camera ----------

        // The region's own post profiles: the shared HD-2D look minus most of Zhuojun's warm LUT, a touch more exposure and
        // saturation, a lighter vignette (user 2026-10-25: brighter daytime).
        static (VolumeProfile high, VolumeProfile low) DaylightProfiles()
        {
            VolumeProfile Make(string src, string dst)
            {
                AssetDatabase.CopyAsset(src, dst);
                var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(dst);
                if (p.TryGet<UnityEngine.Rendering.Universal.ColorLookup>(out var lut)) lut.contribution.Override(0.25f);
                if (p.TryGet<UnityEngine.Rendering.Universal.ColorAdjustments>(out var c)) { c.postExposure.Override(0f); c.saturation.Override(18f); c.contrast.Override(10f); }
                if (p.TryGet<UnityEngine.Rendering.Universal.Vignette>(out var v)) v.intensity.Override(0.15f);
                var wb = p.TryGet<UnityEngine.Rendering.Universal.WhiteBalance>(out var w) ? w : p.Add<UnityEngine.Rendering.Universal.WhiteBalance>(true);
                if (!AssetDatabase.IsSubAsset(wb)) AssetDatabase.AddObjectToAsset(wb, p);
                wb.temperature.Override(-8f);
                EditorUtility.SetDirty(p);
                return p;
            }
            var high = Make(HD2DSampleBuilder.HighProfilePath, ArtDir + "/Bohai_Daylight_High.asset");
            var low = Make(HD2DSampleBuilder.LowProfilePath, ArtDir + "/Bohai_Daylight_Low.asset");
            AssetDatabase.SaveAssets();
            return (high, low);
        }

        static void Camera(Transform player)
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<UnityEngine.Camera>();
            cam.fieldOfView = HD2DSampleBuilder.CameraFov;
            cam.nearClipPlane = 1f; cam.farClipPlane = 600f;
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

            // Open plain: a higher, wider shot than the town so the next landmark is in view; closer at the ferry.
            var zones = new GameObject("Camera zones").transform;
            void Zone(string name, float x0, float x1, float z0, float z1, float pitchDeg, float distance, float fov = 20f, int priority = 0)
            {
                var z = new GameObject(name).AddComponent<CameraZone>();
                z.transform.SetParent(zones, false);
                z.transform.position = new Vector3((x0 + x1) / 2f, 0, (z0 + z1) / 2f);
                z.Size = new Vector2(x1 - x0, z1 - z0);
                z.Pitch = pitchDeg; z.Distance = distance; z.Fov = fov; z.LookOffset = new Vector3(0, 0.8f, 0); z.Priority = priority;
            }
            Zone("Open plain", XMin, XMax, LeveeNorth, ZMax, 32f, 40f, fov: 21f);   // closer than before (52 m) so ground texels read larger (user 2026-10-25)
            Zone("Pingyuan city", CityX0, CityX1, CityZ0, CityZ1, 28f, 36f, priority: 1);
            Zone("Levee and ferry", CityCx - 30f, CityCx + 30f, -270f, CityZ0, 22f, 32f, priority: 1);
            Zone("Gaotang", 290f, 370f, -180f, -98f, 26f, 34f, priority: 1);

            // Overview for the whole region (fog off in CameraPresetProbe).
            var over = new GameObject("Review Overview").AddComponent<UnityEngine.Camera>();
            over.enabled = false;
            over.fieldOfView = 34f;
            over.farClipPlane = 3000f;
            over.transform.SetPositionAndRotation(new Vector3(0f, 980f, -1180f), Quaternion.Euler(40f, 0f, 0f));
            var city = new GameObject("Review Overview Pingyuan").AddComponent<UnityEngine.Camera>();
            city.enabled = false;
            city.fieldOfView = 30f; city.farClipPlane = 1200f;
            city.transform.SetPositionAndRotation(new Vector3(CityCx, 120f, -330f), Quaternion.Euler(32f, 0f, 0f));
        }
    }
}
