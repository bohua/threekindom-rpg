using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Gameplay.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ThreeKingdoms.EditorTools
{
    // 0.5 徐州篇: Xiapi, seat of Xu province (V05_XUZHOU_PLAN §3, REVIEW_V05 R6, R25–R26). One axis from the east gate
    // (south edge, z −40) through the market to the hall square (z 4…21) and the hall steps (z 22); the Mi family shop and
    // the post station face the market, the Chen house and the guest house the square. A water town: the moat runs round
    // the walls with a wet-earth bank, the wharf and the White Gate tower are only the far view. Environment art is the
    // Codex high-resolution delivery (art/v05-xiapi-hd after art/v05-xiapi-concept); white boxes until it lands. Every
    // ground patch is edged with an irregular fringe strip (the terrain-transition rule, user 2026-10-25).
    public static partial class ChapterFieldBuilder
    {
        public const string XiapiPath = "Assets/_Project/Scenes/Xiapi_City.unity";

        static void BuildXiapi()
        {
            dir = $"{ArtRoot3}/Xiapi";
            matDir = $"{ArtRoot3}/Materials/Xiapi";
            var f = NewField("Xiapi", new Vector3(0, 0, -36f), new Color(0.82f, 0.90f, 0.98f));
            var env = f.Env;
            var paving = new Color(0.66f, 0.68f, 0.68f);
            var plaza = new Color(0.80f, 0.78f, 0.72f);
            var market = new Color(0.72f, 0.66f, 0.56f);
            var grass = new Color(0.50f, 0.68f, 0.40f);
            var bank = new Color(0.42f, 0.36f, 0.30f);
            var water = new Color(0.34f, 0.58f, 0.70f);
            var brick = new Color(0.50f, 0.54f, 0.56f);
            var red = new Color(0.62f, 0.22f, 0.18f);

            // Outside the walls: grass, the wet bank, the moat (the bank and water edges are fringed, never a square cut).
            Ground(env, "River grass", "ground_xp_grass", grass, -90, 90, -90, 90, -0.04f);
            Ground(env, "Moat bank", "ground_xp_wetbank", bank, -56, 56, -56, 56, -0.03f);
            Fringe(env, "ground_xp_grass_fringe", -56, 56, -56, 56, -0.025f, inward: true);
            Ground(env, "Moat water", "ground_xp_water", water, -54, 54, -54, 54, -0.02f);
            Fringe(env, "ground_xp_bank_fringe", -54, 54, -54, 54, -0.015f, inward: true);
            // Inside: the city paving, the market street and the hall square.
            Ground(env, "City paving", "ground_xp_paving", paving, -46, 46, -44, 44, -0.01f);
            Ground(env, "Gate road and market", "ground_xp_market", market, -14, 14, -40, -6, 0f);
            Fringe(env, "ground_xp_market_fringe", -14, 14, -40, -6, 0.005f);
            Ground(env, "Hall square", "ground_xp_plaza", plaza, -16, 16, 2, 22, 0.01f);
            // The bridge over the moat to the east gate (the brothers come in here).
            Ground(env, "Gate bridge", "ground_xp_paving", paving, -4, 4, -56, -44, 0.012f);

            // The walls (青砖, 8 m) with the east gate in the south side, the moat rail outside it.
            Strip(env, "Wall S-W", "wall_xp_brick", new Vector3(-46, 0, -44), new Vector3(-7, 0, -44), 8f, brick);
            Strip(env, "Wall S-E", "wall_xp_brick", new Vector3(7, 0, -44), new Vector3(46, 0, -44), 8f, brick);
            Strip(env, "Wall W", "wall_xp_brick", new Vector3(-46, 0, -44), new Vector3(-46, 0, 44), 8f, brick);
            Strip(env, "Wall E", "wall_xp_brick", new Vector3(46, 0, -44), new Vector3(46, 0, 44), 8f, brick);
            Strip(env, "Wall N", "wall_xp_brick", new Vector3(-46, 0, 44), new Vector3(46, 0, 44), 8f, brick);
            Billboard(env, "East gate", "gate_xp_east", 0, -44.2f, new Vector3(12f, 11f, 3f), brick);
            foreach (var (a, b) in new[] { (-54f, -6f), (6f, 54f) })
                Strip(env, $"Moat rail {a}", "rail_xp_moat", new Vector3(a, 0, -56.5f), new Vector3(b, 0, -56.5f), 1f, new Color(0.70f, 0.70f, 0.66f));

            // The hall: platform and steps (z 22–25), the double-eaved front behind; bronze ding and Tao banners at the
            // steps' sides, 18 m of open square before them.
            Billboard(env, "Hall steps", "hall_xp_steps", 0, 22.4f, new Vector3(12f, 3f, 3f), new Color(0.74f, 0.72f, 0.68f), solid: true);
            Billboard(env, "Hall front", "hall_xp_front", 0, 25.6f, new Vector3(20f, 10f, 6f), red, solid: true);
            Billboard(env, "Ding W", "bronze_ding", -7.5f, 21.2f, new Vector3(1.5f, 1.5f, 1.5f), new Color(0.40f, 0.44f, 0.36f), solid: true);
            Billboard(env, "Ding E", "bronze_ding", 7.5f, 21.2f, new Vector3(1.5f, 1.5f, 1.5f), new Color(0.40f, 0.44f, 0.36f), solid: true);
            Billboard(env, "Banner W", "banner_tao_xp", -10.5f, 22f, new Vector3(0.9f, 5.5f, 0.3f), new Color(0.70f, 0.82f, 0.80f));
            Billboard(env, "Banner E", "banner_tao_xp", 10.5f, 22f, new Vector3(0.9f, 5.5f, 0.3f), new Color(0.70f, 0.82f, 0.80f));

            // The square's sides: the Chen house (W) and the guest house (E), each with 6 m clear before the door.
            Billboard(env, "Chen house", "chen_house", -24f, 10f, new Vector3(7f, 4.5f, 6f), new Color(0.62f, 0.56f, 0.50f), solid: true);
            Billboard(env, "Guest house", "guest_house", 24f, 10f, new Vector3(7f, 4.5f, 6f), new Color(0.66f, 0.60f, 0.52f), solid: true);
            // The market's sides: the Mi family shop (W) and the post station (E); stalls along the street edges.
            Billboard(env, "Mi family shop", "shop_mi", -24f, -14f, new Vector3(9f, 5f, 6f), new Color(0.58f, 0.30f, 0.26f), solid: true);
            Billboard(env, "Post station", "post_station", 24f, -14f, new Vector3(9f, 5f, 6f), new Color(0.56f, 0.46f, 0.36f), solid: true);
            foreach (var (x, z, k) in new[] { (-11f, -30f, "stall_xp_a"), (11f, -30f, "stall_xp_b"), (-11f, -22f, "stall_xp_b"), (11f, -22f, "stall_xp_a") })
                Billboard(env, $"Stall {x},{z}", k, x, z, new Vector3(3f, 2.5f, 2f), new Color(0.82f, 0.74f, 0.58f), solid: true);
            // Houses in the corners (not before any door), willows by the water.
            foreach (var (x, z, k) in new[] { (-34f, -30f, "house_xp_a"), (34f, -30f, "house_xp_b"), (-36f, 30f, "house_xp_b"), (36f, 30f, "house_xp_a"), (-34f, -2f, "house_xp_a"), (34f, -2f, "house_xp_b") })
                Billboard(env, $"House {x},{z}", k, x, z, new Vector3(7f, 4f, 5f), new Color(0.76f, 0.74f, 0.70f), solid: true);
            foreach (var (x, z) in new[] { (-20f, -36f), (20f, -36f), (-40f, 14f), (40f, 14f), (-18f, 30f), (18f, 30f), (-30f, -60f), (30f, -60f) })
                Billboard(env, $"Willow {x},{z}", "willow_xp", x, z, new Vector3(3f, 4f, 1f), new Color(0.46f, 0.62f, 0.36f));
            Billboard(env, "Boat W", "boat_xp_small", -26f, -51f, new Vector3(4f, 2f, 1f), new Color(0.50f, 0.40f, 0.30f));
            Billboard(env, "Boat E", "boat_xp_small", 32f, -50f, new Vector3(4f, 2f, 1f), new Color(0.50f, 0.40f, 0.30f));
            Far(env, "far_xiapi_river", 82f, 220f, 40f, Color.white);

            var p = f.People;
            var people = new List<Transform>
            {
                Person(p, "tao_qian 陶谦", Folk("D09"), new Vector3(0, 0, 20.4f)),
                Person(p, "xp_clerk 州府吏", Resident("N03"), new Vector3(2f, 0, -300f)),   // keeps the door after F9
                Person(p, "chen_deng 陈登", Folk("F04"), new Vector3(-24f, 0, 7.4f)),
                Person(p, "kong_rong 孔融", Folk("D07"), new Vector3(24f, 0, 7.4f)),
                Person(p, "mi_zhu 糜竺", Folk("F03"), new Vector3(-24f, 0, -16.6f)),
                Person(p, "sun_qian 孙乾", Folk("F05"), new Vector3(24f, 0, -16.6f)),
                Person(p, "xp_officer 守门军官", Folk("F13"), new Vector3(-4.5f, 0, -40.5f)),
                Person(p, "xp_old_soldier 老卒", Folk("F16"), new Vector3(4.5f, 0, -40.5f)),
                Person(p, "xp_refugee 北海难民", Folk("F15"), new Vector3(-7f, 0, -26f)),
                Person(p, "xp_scholar 读书人", Resident("N24"), new Vector3(7f, 0, -18f)),
                Person(p, "xp_merchant 行商", Resident("N20"), new Vector3(8.5f, 0, -27f)),
                Person(p, "jian_yong 简雍", Folk("D03"), new Vector3(-5f, 0, -2f)),
                Person(p, "tian_yu 田豫", Folk("D04"), new Vector3(6f, 0, -6f)),
                Person(p, "zhao_yun 赵云", Folk("E01"), new Vector3(-8f, 0, 14f)),
            };
            // Danyang troops at the gate and the steps (scenery).
            foreach (var (x, z) in new[] { (-6.5f, -38.5f), (6.5f, -38.5f), (-5f, 20f), (5f, 20f) })
                Person(p, $"Danyang soldier {x},{z}", Folk("F13"), new Vector3(x, 0, z));
            var c = f.Controller;
            c.Location = FieldStoryController.Place.Xiapi;
            c.People = people.ToArray();
            c.ArrivalSpot = Spot(f, "Arrival (inside the east gate)", new Vector3(0, 0, -36f));
            c.ReturnSpot = Spot(f, "Return (hall square)", new Vector3(0, 0, 8f));
            Huds(c);
            // The pale stone reads washed out at noon under the HD-2D grade: tone the city's stone down a little (the tiled
            // copies of the ground materials included).
            foreach (var m in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).SelectMany(r => r.sharedMaterials)
                         .Where(m => m && (m.name.StartsWith("ground_xp_plaza") || m.name.StartsWith("ground_xp_paving"))).Distinct())
            {
                m.SetColor("_BaseColor", new Color(0.72f, 0.72f, 0.72f));
                EditorUtility.SetDirty(m);
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), XiapiPath);
        }

        // Irregular transition strips along the four edges of a ground patch (texture: transparent top, ragged opaque bottom;
        // x tiles every 12 m). Outward: the patch's own ground reaches past its straight edge; inward: the surrounding ground
        // reaches in over it.
        static void Fringe(Transform parent, string key, float xMin, float xMax, float zMin, float zMax, float y, bool inward = false)
        {
            var tex = Tex(key);
            if (!tex) return;
            float depth = tex.height / Ppm, half = (depth / 2f - 0.6f) * (inward ? -1f : 1f), turn = inward ? 180f : 0f;
            var m = Mat(key, tex, Color.white, true);
            void Edge(string name, Vector3 center, float yaw, float len) =>
                Quad(parent, name, center, new Vector3(90f, yaw, 0), new Vector2(len, depth), m, new Vector2(len * Ppm / tex.width, 1f), shadows: false);
            Edge(key + " N", new Vector3((xMin + xMax) / 2f, y, zMax + half), 0f + turn, xMax - xMin);
            Edge(key + " S", new Vector3((xMin + xMax) / 2f, y, zMin - half), 180f + turn, xMax - xMin);
            Edge(key + " E", new Vector3(xMax + half, y, (zMin + zMax) / 2f), 90f + turn, zMax - zMin);
            Edge(key + " W", new Vector3(xMin - half, y, (zMin + zMax) / 2f), -90f + turn, zMax - zMin);
        }
    }
}
