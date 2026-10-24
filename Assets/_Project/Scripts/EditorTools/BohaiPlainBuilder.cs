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
        const string MaterialDir = ArtDir + "/Materials";
        const string MeshPath = ArtDir + "/Bohai_Meshes.asset";

        public const float XMin = -560f, XMax = 560f, ZMin = -330f, ZMax = 340f;
        public const float LeveeH = 5f, LeveeNorth = -212f, LeveeCrestN = -224f, LeveeCrestS = -232f, LeveeSouth = -244f;
        public const float WaterN = -252f, WaterS = -322f, WaterY = -0.6f;
        // Pingyuan city
        public const float CityX0 = -110f, CityX1 = -10f, CityZ0 = -195f, CityZ1 = -115f, CityCx = -60f;
        public static readonly Vector3 PlayerStart = new Vector3(CityCx, 0f, -160f);   // the county office square

        static readonly Dictionary<float, Material> greys = new Dictionary<float, Material>();
        static Material water;

        [MenuItem("JRPG/Build Bohai Plain (art probe)")]
        public static void Build()
        {
            greys.Clear(); water = null;
            T.BeginSession(MaterialDir, MeshPath, ArtDir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.80f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.70f, 0.64f, 0.58f);
            RenderSettings.ambientGroundColor = new Color(0.36f, 0.30f, 0.24f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.0035f;
            RenderSettings.fogColor = new Color(0.80f, 0.76f, 0.68f);

            var env = new GameObject("Environment").transform;
            Ground(env);
            Roads(env);
            River(env);
            Levee(env);
            var labels = new GameObject("Whitebox labels").transform;
            PingyuanCity(env, labels);
            Ferry(env, labels);
            Gaotang(env, labels);
            PostStationAndBarrier(env, labels);
            Village(env, labels, "西村", new Vector3(-380f, 0, 40f), 11);
            Village(env, labels, "北村", new Vector3(40f, 0, 200f), 17);
            Explore(env, labels);
            Exits(env, labels);
            Scatter(env);
            T.EndSession();

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
            var trail = player.AddComponent<PartyTrail>();
            int slot = 1;
            foreach (var (name, key) in new[] { ("Guan Yu (follower)", "GuanYu"), ("Zhang Fei (follower)", "ZhangFei") })
            {
                var go = HD2DSampleBuilder.Character(name, key, PlayerStart + new Vector3(0, 0, -1.5f * slot), 0, isPlayer: false);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.AddComponent<PixelWalkFrames>().Animator = go.GetComponentInChildren<SpriteFrameAnimator>();
                var f = go.AddComponent<PartyFollower>();
                f.Trail = trail; f.Slot = slot++;
                go.transform.SetParent(people, true);
            }
            People(people);

            Camera(player.transform);
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
                Strip(g, "Road", "ground_packed_earth", points[i], points[i + 1], width, 0.004f + i * 0.0002f);
                if (width >= 7f && T.Tex("ground_earth_ruts", optional: true))
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
            for (int i = 0; i < 26; i++)
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
            // Main roads 8 m (ruts down the middle), spurs to villages and wild spots 5 m.
            Road(g, "West road", 8f, new Vector3(XMin, 0, 40f), new Vector3(-380f, 0, 40f), new Vector3(-250f, 0, -100f), WestGate + new Vector3(-6f, 0, 0));
            Road(g, "North road", 8f, NorthGate + new Vector3(0, 0, 6f), new Vector3(CityCx, 0, -20f), new Vector3(40f, 0, 120f), new Vector3(40f, 0, 200f), new Vector3(40f, 0, ZMax));
            Road(g, "East road", 8f, EastGate + new Vector3(6f, 0, 0), new Vector3(150f, 0, -170f), new Vector3(310f, 0, -182f), GaotangFront);
            Road(g, "Gaotang to the post station", 7f, GaotangFront + new Vector3(18f, 0, 0), new Vector3(420f, 0, -100f), PostFront, new Vector3(XMax, 0, 20f));
            Road(g, "Spur to the beacon", 5f, new Vector3(-250f, 0, -100f), new Vector3(-330f, 0, -165f));
            Road(g, "Spur to the reeds", 5f, new Vector3(150f, 0, -170f), new Vector3(215f, 0, -88f));
            Road(g, "Spur to the tomb", 5f, new Vector3(-380f, 0, 40f), new Vector3(-300f, 0, 160f), new Vector3(-224f, 0, 244f));
            Road(g, "Lane in the north village", 5f, new Vector3(40f, 0, 200f), new Vector3(-10f, 0, 214f));
            // The ferry road: south gate down to the levee foot (the ramp over the levee is laid in Levee()).
            Road(g, "Ferry road", 7f, SouthGate + new Vector3(0, 0, -5f), new Vector3(CityCx, 0, LeveeNorth + 0.5f));
        }

        static void River(Transform env)
        {
            var g = T.Group(env, "Yellow River");
            if (!water)
            {
                water = new Material(T.Mat("wb_water_muddy")) { name = "wb_water_muddy_river" };
                water.SetFloat("_Smoothness", 0.55f);
                water.SetFloat("_SpecularHighlights", 1f); water.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
                string path = $"{MaterialDir}/{water.name}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(path)) AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(water, path);
            }
            var size = new Vector2(XMax - XMin + 200f, WaterN - WaterS);
            T.Quad(g, "Water", new Vector3(0, WaterY, (WaterN + WaterS) / 2f), new Vector3(90f, 0, 0), size, water, T.Repeat("wb_water_muddy", size), castShadows: false);
            // Mud shelf from the levee foot down into the water, and the far (south) bank: R6, not open.
            float shelf = WaterN - 1f - LeveeSouth;   // negative run southward
            var mud = new Vector2(XMax - XMin + 200f, Mathf.Sqrt(shelf * shelf + WaterY * WaterY));
            T.Quad(g, "Mud shelf", new Vector3(0, WaterY / 2f, (LeveeSouth + WaterN - 1f) / 2f), new Vector3(90f - Mathf.Atan2(-WaterY, -shelf) * Mathf.Rad2Deg, 0, 0), mud,
                T.Mat("wb_sand_bank"), T.Repeat("wb_sand_bank", mud), castShadows: false);
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
            T.Quad(g, "Crest road", new Vector3(0, LeveeH, (LeveeCrestN + LeveeCrestS) / 2f), new Vector3(90f, 0, 0), crest, T.Mat("ground_packed_earth"), T.Repeat("ground_packed_earth", crest), castShadows: false);
            var ns = new Vector2(len, nLen);
            T.Quad(g, "North slope", new Vector3(0, LeveeH / 2f, (LeveeNorth + LeveeCrestN) / 2f), new Vector3(90f + nDeg, 0, 0), ns, T.Mat("ground_grass"), T.Repeat("ground_grass", ns));
            var ss = new Vector2(len, sLen);
            T.Quad(g, "South slope", new Vector3(0, LeveeH / 2f, (LeveeCrestS + LeveeSouth) / 2f), new Vector3(90f - sDeg, 0, 0), ss, T.Mat("wb_levee_face"), T.Repeat("wb_levee_face", ss));
            // Colliders: the crest block and one tilted slab under each slope.
            Solid(g, new Vector3(0, LeveeH / 2f, (LeveeCrestN + LeveeCrestS) / 2f), new Vector3(len, LeveeH, LeveeCrestN - LeveeCrestS));
            SlopeSolid(g, "North slope collider", (LeveeNorth + LeveeCrestN) / 2f, LeveeH / 2f, nLen, nDeg, len);
            SlopeSolid(g, "South slope collider", (LeveeCrestS + LeveeSouth) / 2f, LeveeH / 2f, sLen, -sDeg, len);
            // Ferry ramps: the ferry road laid over both slopes in line with the gate.
            var ramp = new Vector2(7f, nLen);
            T.Quad(g, "Ferry ramp N", new Vector3(CityCx, LeveeH / 2f + 0.02f, (LeveeNorth + LeveeCrestN) / 2f), new Vector3(90f + nDeg, 0, 0), ramp, T.Mat("ground_packed_earth"), T.Repeat("ground_packed_earth", ramp), castShadows: false);
            var rampS = new Vector2(7f, sLen);
            T.Quad(g, "Ferry ramp S", new Vector3(CityCx, LeveeH / 2f + 0.02f, (LeveeCrestS + LeveeSouth) / 2f), new Vector3(90f - sDeg, 0, 0), rampS, T.Mat("ground_packed_earth"), T.Repeat("ground_packed_earth", rampS), castShadows: false);
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

        // ---------- Pingyuan (whitebox) ----------

        static void PingyuanCity(Transform env, Transform labels)
        {
            var c = T.Group(env, "Pingyuan city");
            T.Patch(c, "City earth", "ground_packed_earth", CityX0 + 2f, CityX1 - 2f, CityZ0 + 2f, CityZ1 - 2f, 0.002f);
            // Walls with 9 m gaps for the four gates.
            T.WallRun(c, "South wall W", new Vector3(CityX0 - 2f, 0, CityZ0), new Vector3(CityCx - 4.5f, 0, CityZ0));
            T.WallRun(c, "South wall E", new Vector3(CityCx + 4.5f, 0, CityZ0), new Vector3(CityX1 + 2f, 0, CityZ0));
            T.WallRun(c, "North wall W", new Vector3(CityX0 - 2f, 0, CityZ1), new Vector3(CityCx - 4.5f, 0, CityZ1));
            T.WallRun(c, "North wall E", new Vector3(CityCx + 4.5f, 0, CityZ1), new Vector3(CityX1 + 2f, 0, CityZ1));
            T.WallRun(c, "West wall S", new Vector3(CityX0, 0, CityZ0 + 2f), new Vector3(CityX0, 0, WestGate.z - 4.5f));
            T.WallRun(c, "West wall N", new Vector3(CityX0, 0, WestGate.z + 4.5f), new Vector3(CityX0, 0, CityZ1 - 2f));
            T.WallRun(c, "East wall S", new Vector3(CityX1, 0, CityZ0 + 2f), new Vector3(CityX1, 0, EastGate.z - 4.5f));
            T.WallRun(c, "East wall N", new Vector3(CityX1, 0, EastGate.z + 4.5f), new Vector3(CityX1, 0, CityZ1 - 2f));
            Gate(c, "South gate", SouthGate, 0f);
            Gate(c, "North gate", NorthGate, 180f);
            Gate(c, "West gate", WestGate, 90f);
            Gate(c, "East gate", EastGate, -90f);
            Label(labels, "平原城（据点）", new Vector3(CityCx, 0, CityZ1 + 2f), 14f);

            // Inside, grey boxes only: the county office on the north side behind a 26 m deep square, the main street
            // down to the south gate, barracks north-east, market west, a few houses well away from the frontages.
            var wb = T.Group(c, "Interior (whitebox)");
            GreyHouse(wb, "County office (县衙)", new Vector3(CityCx, 0, -140f), 16f, 10f, 4.5f, shade: 0.7f);
            T.Patch(wb, "Office square", "ground_street_stone", CityCx - 18f, CityCx + 18f, -168f, -141f, 0.006f);
            T.Patch(wb, "Main street", "ground_street_stone", CityCx - 3f, CityCx + 3f, CityZ0 + 2f, -168f, 0.005f);
            Label(labels, "县衙（相府）·门前广场", new Vector3(CityCx, 0, -140f), 9f);
            // Barracks compound: palisade with its gate on the south side, open drill ground in front.
            var bk = T.Group(wb, "Barracks (兵营)");
            Box(bk, "Barracks hall", new Vector3(-28f, 0, -128f), new Vector3(14f, 4f, 7f), 0.55f);
            Box(bk, "Fence W", new Vector3(-37f, 0, -134f), new Vector3(0.4f, 2.5f, 18f), 0.4f);
            Box(bk, "Fence E", new Vector3(-19f, 0, -134f), new Vector3(0.4f, 2.5f, 18f), 0.4f);
            Box(bk, "Fence S W", new Vector3(-34f, 0, -143f), new Vector3(6f, 2.5f, 0.4f), 0.4f);
            Box(bk, "Fence S E", new Vector3(-22f, 0, -143f), new Vector3(6f, 2.5f, 0.4f), 0.4f);
            Label(labels, "兵营", new Vector3(-28f, 0, -136f), 7f);
            var mk = T.Group(wb, "Market (市集)");
            for (int i = 0; i < 4; i++) Box(mk, "Stall", new Vector3(-98f + (i % 2) * 9f, 0, -150f - (i / 2) * 8f), new Vector3(4f, 2.2f, 2.5f), 0.66f);
            Label(labels, "市集", new Vector3(-93f, 0, -154f), 6f);
            GreyHouse(wb, "House SW 1", new Vector3(-96f, 0, -188f), 7f, 6f, 3.2f);
            GreyHouse(wb, "House SW 2", new Vector3(-82f, 0, -188f), 7f, 6f, 3.2f);
            GreyHouse(wb, "House SE 1", new Vector3(-38f, 0, -188f), 7f, 6f, 3.2f);
            GreyHouse(wb, "House SE 2", new Vector3(-24f, 0, -188f), 7f, 6f, 3.2f);
            GreyHouse(wb, "Residence (能人居所)", new Vector3(-96f, 0, -132f), 8f, 7f, 3.4f, shade: 0.68f);
            Spot("Square", PlayerStart);
            Spot("Barracks", new Vector3(-28f, 0, -148f));
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
            var deck = new Vector2(x1 - x0, zStart - zEnd);
            T.Quad(f, "Dock deck", new Vector3((x0 + x1) / 2f, deckY, (zStart + zEnd) / 2f), new Vector3(90f, 0, 0), deck, T.Mat("wb_dock_planks"), T.Repeat("wb_dock_planks", deck), castShadows: false);
            Solid(f, new Vector3((x0 + x1) / 2f, deckY / 2f - 0.6f, (zStart + zEnd) / 2f), new Vector3(x1 - x0, deckY + 1.2f, zStart - zEnd));
            for (float z = zStart - 2f; z > zEnd; z -= 4f)
                foreach (float x in new[] { x0 + 0.3f, x1 - 0.3f })
                    Box(f, "Pile", new Vector3(x, WaterY - 0.5f, z), new Vector3(0.4f, deckY - WaterY + 1.1f, 0.4f), 0.3f, solid: false);
            // Keep the player on the deck.
            Solid(f, new Vector3(x0 - 0.5f, 1.5f, (zStart + zEnd) / 2f), new Vector3(1f, 3f, zStart - zEnd));
            Solid(f, new Vector3(x1 + 0.5f, 1.5f, (zStart + zEnd) / 2f), new Vector3(1f, 3f, zStart - zEnd));
            Solid(f, new Vector3((x0 + x1) / 2f, 1.5f, zEnd - 0.5f), new Vector3(x1 - x0 + 2f, 3f, 1f));
            // Ferry boat moored at the dock end (grey for now), the ferry hut on the levee crest, a mooring post.
            var boat = T.Group(f, "Ferry boat", new Vector3(x1 + 3.4f, WaterY, -260f));
            Box(boat, "Hull", Vector3.zero, new Vector3(4.2f, 1.2f, 11f), 0.38f, solid: false);
            Box(boat, "Cabin", new Vector3(0, 1.2f, 1f), new Vector3(3f, 1.6f, 3.5f), 0.55f, solid: false);
            GreyHouse(f, "Ferry hut", new Vector3(CityCx + 12f, LeveeH, LeveeCrestN - 0.5f), 6f, 4f, 3f, yaw: 180f, shade: 0.58f);
            Label(labels, "平原津（渡口·阶段门）", new Vector3(CityCx, 0, -258f), 5f);
            Label(labels, "黄河大堤（地标）", new Vector3(CityCx - 140f, LeveeH, LeveeCrestS), 7f);
            Spot("Ferry", new Vector3(CityCx, deckY, -255f));
            Spot("Levee", new Vector3(CityCx - 140f, LeveeH, (LeveeCrestN + LeveeCrestS) / 2f));
            Spot("South gate", new Vector3(CityCx, 0, CityZ0 - 8f));
        }

        // ---------- Gaotang gate scene (门前小场景) ----------

        static void Gaotang(Transform env, Transform labels)
        {
            var g = T.Group(env, "Gaotang (高唐·门前小场景)");
            float cx = GaotangFront.x, wz = -150f;
            // A short stretch of town wall with its gate; inside, one square with the county hall and one enterable shop.
            T.WallRun(g, "Wall W", new Vector3(cx - 40f, 0, wz), new Vector3(cx - 4.5f, 0, wz));
            T.WallRun(g, "Wall E", new Vector3(cx + 4.5f, 0, wz), new Vector3(cx + 40f, 0, wz));
            T.WallRun(g, "Wall side W", new Vector3(cx - 38f, 0, wz + 2f), new Vector3(cx - 38f, 0, wz + 50f));
            T.WallRun(g, "Wall side E", new Vector3(cx + 38f, 0, wz + 2f), new Vector3(cx + 38f, 0, wz + 50f));
            T.WallRun(g, "Wall back", new Vector3(cx - 40f, 0, wz + 52f), new Vector3(cx + 40f, 0, wz + 52f));
            Gate(g, "Gaotang gate", new Vector3(cx, 0, wz), 0f);
            T.Patch(g, "Gaotang square", "ground_street_stone", cx - 15f, cx + 15f, wz + 2f, wz + 30f, 0.006f);
            T.Patch(g, "Gate apron", "ground_packed_earth", cx - 12f, cx + 12f, wz - 26f, wz - 2f, 0.003f);
            GreyHouse(g, "County hall (县府)", new Vector3(cx, 0, wz + 30f), 14f, 9f, 4.5f, shade: 0.7f);
            GreyHouse(g, "Shop (enterable)", new Vector3(cx - 24f, 0, wz + 20f), 8f, 6f, 3.4f, yaw: 90f);
            Label(labels, "高唐（门前小场景）", new Vector3(cx, 0, wz), 12f);
            Spot("Gaotang", new Vector3(cx, 0, wz - 14f));
        }

        // ---------- post station and checkpoint ----------

        static void PostStationAndBarrier(Transform env, Transform labels)
        {
            var p = T.Group(env, "Post station (驿站)");
            T.Patch(p, "Post yard", "ground_packed_earth", PostFront.x - 14f, PostFront.x + 14f, PostFront.z - 12f, PostFront.z + 2f, 0.003f);
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
            Box(b, "Closed gate", new Vector3(bx, 0, 20f), new Vector3(0.6f, 3f, 12f), 0.32f);
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
            T.Patch(v, "Threshing floor", "ground_packed_earth", -16f, 16f, -12f, 12f, 0.003f);
            (string front, string side, string back, bool thatch, Vector3 at)[] houses =
            {
                ("house_b_front", "house_b_side", "house_b_back", true, new Vector3(-12f, 0, 12f)),
                ("house_front", "house_side", "house_back", false, new Vector3(2f, 0, 13f)),
                ("house_c_front", "house_c_side", "house_c_back", true, new Vector3(16f, 0, 12f)),
                ("house_b_front", "house_b_side", "house_b_back", true, new Vector3(-22f, 0, -2f)),
                ("house_c_front", "house_c_side", "house_c_back", false, new Vector3(23f, 0, -4f)),
            };
            foreach (var (front, side, back, thatch, at) in houses) T.Building(v, "House", at, 0f, front, side, back, roofKey: thatch ? "thatch" : "tile");
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
            Box(b, "Tower stump", Vector3.zero, new Vector3(6f, 7f, 6f), 0.5f);
            Box(b, "Tower crown (broken)", new Vector3(1f, 7f, 0.5f), new Vector3(3.5f, 1.8f, 3f), 0.5f);
            for (int i = 0; i < 5; i++) Box(b, "Rubble", new Vector3(-5f + i * 2.4f, 0, -5f + (i % 2) * 1.5f), new Vector3(1.4f, 0.6f, 1.2f), 0.45f, yaw: i * 23f);
            T.Prop(b, "prop_tree_locust", 7f, 3f, scale: 0.8f, solid: true);
            Label(labels, "废烽燧（探索点）", b.position, 10f);
            Spot("Beacon", b.position + new Vector3(0, 0, -9f));

            // 2 Reed marsh: open water patches among reeds, a punt pulled up.
            var r = T.Group(env, "Reed marsh (芦苇荡)", new Vector3(220f, 0, -70f));
            T.Patch(r, "Pool A", "wb_water_muddy", -18f, 4f, -6f, 12f, 0.004f);
            T.Patch(r, "Pool B", "wb_water_muddy", 6f, 22f, 4f, 20f, 0.004f);
            var rr = new System.Random(3);
            for (int i = 0; i < 46; i++)
            {
                float x = -26f + (float)rr.NextDouble() * 54f, z = -12f + (float)rr.NextDouble() * 38f;
                T.Prop(r, rr.Next(3) == 0 ? "prop_bush_b" : "prop_grass_tuft", x, z, scale: 1.6f);
            }
            Box(r, "Punt", new Vector3(-4f, 0, -9f), new Vector3(1.4f, 0.5f, 5f), 0.35f, yaw: 20f, solid: false);
            Label(labels, "芦苇荡（探索点）", r.position, 6f);
            Spot("Reeds", r.position + new Vector3(0, 0, -16f));

            // 3 Tomb mound with a pair of stone que pillars on its spirit road.
            var t = T.Group(env, "Tomb mound (古冢石阙)", new Vector3(-220f, 0, 260f));
            for (int i = 0; i < 4; i++) Box(t, "Mound tier", Vector3.zero + new Vector3(0, i * 1.6f, 0), new Vector3(24f - i * 5f, 1.6f, 20f - i * 4.5f), 0.48f);
            Box(t, "Que W", new Vector3(-4f, 0, -18f), new Vector3(2f, 5f, 2f), 0.75f);
            Box(t, "Que E", new Vector3(4f, 0, -18f), new Vector3(2f, 5f, 2f), 0.75f);
            T.Patch(t, "Spirit road", "ground_street_stone", -2f, 2f, -24f, -10f, 0.005f);
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
            Npc("简雍（暂代）", "N24", new Vector3(CityCx + 15f, 0, -150f));
            Npc("田豫（暂代）", "N01", new Vector3(-26.5f, 0, -144f));
            Npc("渡口船夫（暂代）", "N12", new Vector3(-58.5f, 0.35f, -262f));
            Npc("高唐守卫", "N01", new Vector3(GaotangFront.x + 3.5f, 0, -154f));
            Npc("哨卡守兵 甲", "N01", new Vector3(541f, 0, 15f), 1);
            Npc("哨卡守兵 乙", "N01", new Vector3(541f, 0, 25f), 1);
            Npc("驿卒", "N07", new Vector3(PostFront.x, 0, PostFront.z - 1.6f));
        }

        // ---------- camera ----------

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

            var high = AssetDatabase.LoadAssetAtPath<VolumeProfile>(HD2DSampleBuilder.HighProfilePath);
            var low = AssetDatabase.LoadAssetAtPath<VolumeProfile>(HD2DSampleBuilder.LowProfilePath);
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
            Zone("Open plain", XMin, XMax, LeveeNorth, ZMax, 34f, 52f, fov: 22f);
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
