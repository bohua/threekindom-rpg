using System.Collections.Generic;
using System.IO;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Gameplay;
using ThreeKingdoms.Gameplay.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ThreeKingdoms.EditorTools
{
    // 0.3 讨董篇 scenes (V03_TAODONG_PLAN §3, REVIEW_V03 R26–R28): Gongsun Zan's garrison (S4) and the coalition camp at
    // Suanzao (S5), plus the story stage's new backgrounds and actors. Environment art is the Codex high-resolution
    // delivery (art/v03-gongsun-camp-hd, art/v03-suanzao-camp-hd, 120 px per metre, after the approved design images
    // art/v03-*-concept); every piece falls back to a white-box shape of the right size and its own palette while its
    // texture is missing, so the scenes play from the first build. Open ground of 6 m or more before every important door;
    // key people stand at their doors.
    public static partial class ChapterFieldBuilder
    {
        public const string GongsunPath = "Assets/_Project/Scenes/Camp_Gongsun.unity";
        public const string SuanzaoPath = "Assets/_Project/Scenes/Camp_Suanzao.unity";
        const string ArtRoot3 = "Assets/_Project/Art/Chapter3";

        // The art first: the story stage (built before the 0.3 scenes) reads the stage paintings.
        static void ImportChapter3Art()
        {
            Copy(Path.Combine(Art, "v03-gongsun-camp-hd"), $"{ArtRoot3}/Gongsun");
            Copy(Path.Combine(Art, "v03-suanzao-camp-hd"), $"{ArtRoot3}/Suanzao");
            Copy(Path.Combine(Art, "v03-stage-bg"), $"{ArtRoot3}/Stage", f => f.StartsWith("stage_"));
            Copy(Path.Combine(Art, "v04-stage-bg"), $"{ArtRoot3}/Stage", f => f.StartsWith("stage_"));
            Copy(Path.Combine(Art, "v05-stage-bg"), $"{ArtRoot3}/Stage", f => f.StartsWith("stage_"));
            Copy(Path.Combine(Art, "v05-xiapi-hd"), $"{ArtRoot3}/Xiapi");   // 0.5
            AssetDatabase.Refresh();
            foreach (var sub in new[] { "Gongsun", "Suanzao", "Stage", "Xiapi" })
            {
                string d = $"{ArtRoot3}/{sub}";
                if (!Directory.Exists(d)) continue;
                var axes = ReadAxes(d);
                foreach (var path in Directory.GetFiles(d, "*.png"))
                {
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
                    if (importer == null) continue;
                    string key = Path.GetFileNameWithoutExtension(path);
                    axes.TryGetValue(key, out string a);
                    if (a == null) a = key.StartsWith("ground_") ? "xy" : "none";
                    importer.textureType = TextureImporterType.Default;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.mipmapEnabled = sub != "Stage";
                    importer.textureCompression = TextureImporterCompression.CompressedHQ;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.maxTextureSize = key.StartsWith("far_") ? 8192 : 2048;
                    importer.alphaIsTransparency = true;
                    importer.wrapModeU = a.Contains("x") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                    importer.wrapModeV = a.Contains("y") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                    importer.SaveAndReimport();
                }
            }
        }

        // Only Gongsun Zan's garrison (-executeMethod …ChapterFieldBuilder.BuildGongsunFromCommandLine), leaving the other
        // chapter scenes as they are (Lu Zhi's camp is a Blender scene of the region thread now).
        public static void BuildGongsunFromCommandLine()
        {
            try
            {
                ImportChapter3Art();
                BuildGongsun();
                AssetDatabase.SaveAssets();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static void BuildChapter3()
        {
            BuildGongsun();
            BuildSuanzao();
            BuildXiapi();   // 0.5 (ChapterFieldBuilder5)
        }

        // ---------- the story stage (0.3) ----------

        static IEnumerable<(string id, Texture2D tex, Color fallback)> Stage3Backgrounds()
        {
            Texture2D T(string key) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtRoot3}/Stage/{key}.png");
            yield return ("luoyang_palace", T("stage_luoyang_palace"), new Color(0.46f, 0.30f, 0.26f));
            yield return ("lv_farm", T("stage_lv_farm_night"), new Color(0.12f, 0.16f, 0.26f));
            yield return ("mengzhu_tent", T("stage_mengzhu_tent"), new Color(0.50f, 0.18f, 0.16f));
            yield return ("hulao_gate", T("stage_hulao_gate"), new Color(0.62f, 0.58f, 0.50f));
            yield return ("luoyang_ruins", T("stage_luoyang_ruins"), new Color(0.40f, 0.38f, 0.36f));
            // 0.4 (Codex art/v04-stage-bg)
            yield return ("panhe_bank", T("stage_panhe_bank"), new Color(0.50f, 0.62f, 0.70f));
            yield return ("jieqiao_after", T("stage_jieqiao_after"), new Color(0.48f, 0.52f, 0.56f));
            yield return ("zhending_home", T("stage_zhending_home"), new Color(0.70f, 0.64f, 0.52f));
            yield return ("gongsun_hall", T("stage_gongsun_hall"), new Color(0.66f, 0.62f, 0.56f));
            // 0.5 (Codex art/v05-stage-bg)
            yield return ("duchang_outside", T("stage_duchang_outside"), new Color(0.78f, 0.70f, 0.54f));
            yield return ("beihai_hall", T("stage_beihai_hall"), new Color(0.62f, 0.58f, 0.54f));
            yield return ("refugee_road", T("stage_refugee_road"), new Color(0.70f, 0.66f, 0.56f));
            yield return ("xiapi_hall", T("stage_xiapi_hall"), new Color(0.56f, 0.40f, 0.32f));
        }

        static IEnumerable<(string name, string key)> Stage3Actors()
        {
            yield return ("NpcDongZhuo", Folk("C03"));
            yield return ("NpcCaoCao", Folk("C02"));
            yield return ("NpcGongsunZan", Folk("D01"));
            yield return ("NpcYuanShao", Folk("D05"));
            yield return ("NpcYuanShu", Folk("D06"));
            yield return ("NpcKongRong", Folk("D07"));
            yield return ("NpcSunJian", Folk("D08"));
            yield return ("NpcChenGong", Folk("D11"));
            yield return ("NpcLvBoshe", Folk("D12"));
            yield return ("NpcElderSoldier", Resident("N09"));
            yield return ("NpcTianKai", Folk("E07"));
            yield return ("NpcZhaoYun", Folk("E01"));
            yield return ("NpcZhaoBrother", Folk("E11"));
            // 0.5
            yield return ("NpcTaoQian", Folk("D09"));
            yield return ("NpcTaishiCi", Folk("F01"));
            yield return ("NpcMiZhu", Folk("F03"));
            yield return ("NpcChenDeng", Folk("F04"));
            yield return ("NpcCaoBao", Folk("F06"));
            yield return ("NpcLuBu", Folk("F10"));
            yield return ("NpcRefugee", Folk("F15"));
            foreach (var a in SideStageActors()) yield return a;
        }

        // ---------- Gongsun Zan's garrison (S4, about 70 × 50 m; south gate z −22, command hall steps z 15.5) ----------

        static void BuildGongsun()
        {
            dir = $"{ArtRoot3}/Gongsun";
            matDir = $"{ArtRoot3}/Materials/Gongsun";
            var f = NewField("Gongsun", new Vector3(0, 0, -19f), new Color(0.80f, 0.86f, 0.94f));
            var env = f.Env;
            var earth = new Color(0.74f, 0.66f, 0.52f);      // cool rammed earth (north)
            var grass = new Color(0.52f, 0.64f, 0.40f);
            var sand = new Color(0.84f, 0.78f, 0.62f);
            var wall = new Color(0.68f, 0.60f, 0.48f);
            var timber = new Color(0.46f, 0.36f, 0.26f);
            var white = new Color(0.94f, 0.94f, 0.92f);

            Ground(env, "Steppe outside", "ground_gs_grass", grass, -80, 80, -70, 70, -0.02f);
            Ground(env, "Garrison ground", "ground_gs_earth", earth, -35, 35, -22, 24);
            Ground(env, "Main road", "ground_gs_road", new Color(0.70f, 0.62f, 0.50f), -2.6f, 2.6f, -24, 6, 0.01f);
            Ground(env, "Hall square", "ground_gs_road", new Color(0.72f, 0.64f, 0.52f), -7, 7, 5, 14.2f, 0.011f);
            Ground(env, "Drill ground", "ground_gs_drill", sand, -27, -9, -8, 12, 0.012f);
            Ground(env, "Stable yard", "ground_gs_earth", new Color(0.70f, 0.62f, 0.48f), 12, 28, -2, 12, 0.012f);

            // Rammed-earth walls all round (5 m) with the gate tower in the south.
            Strip(env, "Wall S-W", "wall_gs_rammed", new Vector3(-35, 0, -22), new Vector3(-3.5f, 0, -22), 5f, wall);
            Strip(env, "Wall S-E", "wall_gs_rammed", new Vector3(3.5f, 0, -22), new Vector3(35, 0, -22), 5f, wall);
            Strip(env, "Wall W", "wall_gs_rammed", new Vector3(-35, 0, -22), new Vector3(-35, 0, 24), 5f, wall);
            Strip(env, "Wall E", "wall_gs_rammed", new Vector3(35, 0, -22), new Vector3(35, 0, 24), 5f, wall);
            Strip(env, "Wall N", "wall_gs_rammed", new Vector3(-35, 0, 24), new Vector3(35, 0, 24), 5f, wall);
            Billboard(env, "South gate", "gate_gs_south", 0, -22.2f, new Vector3(8f, 7f, 0.8f), wall);

            // Command hall north with a 14 × 9 m square; white 公孫 standards either side.
            Building(env, "Command hall", "hall_gs_front", "hall_gs_side", new Vector3(0, 0, 16f), 14f, 5.5f, 8f, new Color(0.78f, 0.72f, 0.62f));
            Billboard(env, "Standard W", "banner_gongsun", -8.5f, 14.4f, new Vector3(0.7f, 5.5f, 0.3f), white);
            Billboard(env, "Standard E", "banner_white_ya", 8.5f, 14.4f, new Vector3(0.9f, 6.5f, 0.3f), white);
            // Long barracks rows (rammed earth and timber, not tents) along the road, none before a door.
            foreach (var (x, z) in new[] { (-14f, -18f), (14f, -18f), (-24f, -18f), (24f, -18f), (-24f, 18f), (24f, 18f) })
                Building(env, $"Barracks {x},{z}", "barracks_gs_front", "barracks_gs_side", new Vector3(x, 0, z), 8f, 3f, 5f, new Color(0.72f, 0.64f, 0.52f));
            // Quartermaster's store (east, 6 m clear) and Liu Bei's quarters (west, 6 m clear).
            Building(env, "Quartermaster store", "store_gs_front", "barracks_gs_side", new Vector3(9f, 0, -4f), 8f, 3.5f, 6f, new Color(0.70f, 0.60f, 0.46f));
            Billboard(env, "Grain sacks", "grain_sacks_gs", 12.6f, -6.4f, new Vector3(2f, 1.4f, 1.2f), new Color(0.82f, 0.74f, 0.54f), solid: true);
            Building(env, "Liu Bei's quarters", "quarters_gs_front", "barracks_gs_side", new Vector3(-9f, 0, -6f), 7f, 3.2f, 5f, new Color(0.74f, 0.66f, 0.54f));
            // Drill ground (west): reviewing stand, weapon racks, targets.
            Billboard(env, "Reviewing stand", "review_stand", -18f, 13.6f, new Vector3(6f, 2.4f, 4f), timber, solid: true);
            Billboard(env, "Weapon rack A", "weapon_rack_gs", -26f, 2f, new Vector3(2.5f, 2f, 0.6f), timber);
            Billboard(env, "Weapon rack B", "weapon_rack_gs", -26f, -4f, new Vector3(2.5f, 2f, 0.6f), timber);
            Billboard(env, "Target A", "archery_target_gs", -22f, 10.2f, new Vector3(1.5f, 2f, 0.3f), white);
            Billboard(env, "Target B", "archery_target_gs", -13f, 10.2f, new Vector3(1.5f, 2f, 0.3f), white);
            // Stable (east): long shed, white horses, trough, hay (6 m clear before the door).
            Billboard(env, "Stable", "stable_gs_shed", 20f, 13f, new Vector3(14f, 4.5f, 5f), timber, solid: true);
            foreach (var x in new[] { 15.5f, 18.5f, 21.5f, 24.5f })
                Billboard(env, "White horse " + x, "horse_white", x, 11f, new Vector3(2.4f, 2f, 0.8f), white);
            Billboard(env, "Trough", "horse_trough_gs", 20f, 9.6f, new Vector3(3f, 0.9f, 1f), timber);
            Billboard(env, "Hay", "hay_pile", 27f, 12.4f, new Vector3(2.2f, 1.8f, 1.8f), new Color(0.86f, 0.76f, 0.42f), solid: true);
            foreach (var x in new[] { -30f, -16f, 16f, 30f })
                Billboard(env, "Banner " + x, "banner_gongsun", x, 22.6f, new Vector3(0.5f, 4.5f, 0.2f), white);
            Far(env, "far_yanshan", 64f, 160f, 34f, Color.white);

            var p = f.People;
            var people = new List<Transform>
            {
                Person(p, "gongsun_zan 公孙瓒", Folk("D01"), new Vector3(0, 0, 15.5f)),
                Person(p, "baima_rider 白马义从", Folk("D02"), new Vector3(-9.6f, 0, 2f)),
                Person(p, "tian_yu 田豫", Folk("D04"), new Vector3(-9.6f, 0, -1.6f)),
                Person(p, "jian_yong 简雍", Folk("D03"), new Vector3(-9f, 0, -8f)),
                Person(p, "groom 马夫", Resident("N12"), new Vector3(16f, 0, 4f)),
                Person(p, "gs_quartermaster 军需官", Resident("N11"), new Vector3(9f, 0, -6.2f)),
                Person(p, "guan_jing 关靖", Resident("N03"), new Vector3(4.6f, 0, -7.6f)),   // the chief clerk by the store (胡市马契)
                Person(p, "gs_soldier_a 士卒", Folk("C09"), new Vector3(-3.6f, 0, -3f)),
                Person(p, "gs_soldier_b 士卒", Folk("C09"), new Vector3(3.6f, 0, 2f)),
                Person(p, "gs_guard 南门守卒", Folk("C09"), new Vector3(-2.8f, 0, -20.4f)),
            };
            // 田豫点兵 (npc-sidequests sq.lb1.r1.tian_yu_roll): four squads on the drill ground, each with its head; the
            // missing man lies sick behind the hay at the stable, shown once the third squad reports him gone.
            for (int i = 0; i < 4; i++)
            {
                var at = new Vector3(-24f + i * 4.2f, 0, 6.4f);
                people.Add(Person(p, $"roll_head_{i + 1} 第{"一二三四"[i]}队队头", Folk("C09"), at));
                for (int k = 0; k < 3; k++) Person(p, $"Squad {i + 1}.{k}", Folk("C09"), at + new Vector3(-1.2f + k * 1.2f, 0, 1.6f), 1);
            }
            var sickFlag = new GameObject("roll_h3 (the sick soldier)").transform;
            sickFlag.SetParent(p, false);
            people.Add(Person(sickFlag, "roll_sick 病兵", Resident("N02"), new Vector3(29.4f, 0, 12.4f), 7));
            Person(p, "Gate guard E", Folk("C09"), new Vector3(2.8f, 0, -20.4f));
            Person(p, "Hall guard W", Folk("D02"), new Vector3(-3.4f, 0, 15.2f));
            Person(p, "Hall guard E", Folk("D02"), new Vector3(3.4f, 0, 15.2f));
            var c = f.Controller;
            c.Location = FieldStoryController.Place.Gongsun;
            c.People = people.ToArray();
            c.Spots = new[] { Spot(f, "bell 草料堆", new Vector3(27f, 0, 10.6f)) };
            c.FlagObjects = new[] { sickFlag };
            // Out through the south gate onto the R1 涿蓟 map while the garrison is the party's base (190): Ji, the wild
            // spots and their people (npc-sidequests R1). Back in through the R1 camp gate arrives inside the gate.
            var south = new GameObject("Portal to the R1 region").AddComponent<RegionPortal>();
            south.transform.SetParent(env, false);
            south.transform.position = new Vector3(0, 0, -28f);
            south.Size = new Vector2(8f, 3f);
            south.TargetScene = "Region_R1";
            south.ArrivalSpot = "Spot 公孙瓒营";
            south.Facing = Vector3.back;
            south.Steps = new[] { (int)ChapterStep.Gongsun };
            c.ArrivalSpot = Spot(f, "Arrival (inside the gate)", new Vector3(0, 0, -19f));
            c.ReturnSpot = Spot(f, "Return (drill ground)", new Vector3(-14f, 0, 0f));
            Huds(c);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), GongsunPath);
        }

        // ---------- the Suanzao coalition camp (S5, about 90 × 60 m; gate z −28, road 12 m, altar z 22–28) ----------

        static void BuildSuanzao()
        {
            dir = $"{ArtRoot3}/Suanzao";
            matDir = $"{ArtRoot3}/Materials/Suanzao";
            var f = NewField("Suanzao", new Vector3(0, 0, -25f), new Color(0.82f, 0.88f, 0.96f));
            var env = f.Env;
            var dirt = new Color(0.80f, 0.72f, 0.56f);
            var grass = new Color(0.54f, 0.68f, 0.40f);
            var wood = new Color(0.50f, 0.38f, 0.26f);

            Ground(env, "Plain outside", "ground_sz_grass", grass, -90, 90, -80, 80, -0.02f);
            Ground(env, "Camp ground", "ground_sz_dirt", dirt, -45, 45, -30, 40);
            Ground(env, "Main road (12 m)", "ground_sz_road", new Color(0.74f, 0.66f, 0.50f), -6, 6, -32, 20, 0.01f);
            Ground(env, "Oath plaza", "ground_sz_plaza", new Color(0.82f, 0.76f, 0.62f), -12, 12, 18, 33, 0.011f);

            // The camp gate: two carts facing (辕门) with banner poles; palisade round the camp.
            Billboard(env, "Gate cart W", "yuanmen_cart", -4.2f, -28.6f, new Vector3(3f, 2.4f, 2f), wood, solid: true);
            Billboard(env, "Gate cart E", "yuanmen_cart", 4.2f, -28.6f, new Vector3(3f, 2.4f, 2f), wood, solid: true);
            Strip(env, "Palisade S-W", "palisade_sz", new Vector3(-45, 0, -30), new Vector3(-6, 0, -30), 2.5f, wood);
            Strip(env, "Palisade S-E", "palisade_sz", new Vector3(6, 0, -30), new Vector3(45, 0, -30), 2.5f, wood);
            Strip(env, "Palisade W", "palisade_sz", new Vector3(-45, 0, -30), new Vector3(-45, 0, 40), 2.5f, wood);
            Strip(env, "Palisade E", "palisade_sz", new Vector3(45, 0, -30), new Vector3(45, 0, 40), 2.5f, wood);
            Strip(env, "Palisade N", "palisade_sz", new Vector3(-45, 0, 40), new Vector3(45, 0, 40), 2.5f, wood);

            // Gongsun Zan's white-flag camp by the gate (the last seat).
            Billboard(env, "Gongsun tent", "tent_small_gongsun", -12f, -24f, new Vector3(6f, 3.5f, 4f), new Color(0.94f, 0.94f, 0.92f), solid: true);
            Billboard(env, "Gongsun banner", "banner_gongsun_sz", -8.6f, -25.6f, new Vector3(0.5f, 4.5f, 0.2f), new Color(0.94f, 0.94f, 0.92f));

            // Five lords' tents (each 6 m clear before the door) and their banners (shape and pattern differ, R28).
            var lords = new (string tent, string banner, float x, float z, Color color)[]
            {
                ("tent_lord_cao", "banner_cao", -14f, -12f, new Color(0.18f, 0.18f, 0.20f)),
                ("tent_lord_sun", "banner_sun", 14f, -12f, new Color(0.80f, 0.26f, 0.18f)),
                ("tent_lord_yuanshu", "banner_yuanshu", -14f, 3f, new Color(0.86f, 0.74f, 0.30f)),
                ("tent_lord_kong", "banner_kong", 14f, 3f, new Color(0.36f, 0.56f, 0.62f)),
                ("tent_lord_tao", "banner_tao", -14f, 15f, new Color(0.70f, 0.82f, 0.80f)),
            };
            foreach (var l in lords)
            {
                float side = Mathf.Sign(l.x);
                Billboard(env, "Tent " + l.tent, l.tent, l.x + side * 4f, l.z, new Vector3(9f, 5f, 6f), l.color, solid: true);
                Billboard(env, "Banner " + l.banner, l.banner, l.x - side * 1.4f, l.z + 1.6f, new Vector3(0.6f, 5f, 0.2f), l.color);
            }
            Billboard(env, "Envoys' tent", "tent_small_generic", 18f, 15f, new Vector3(7f, 4f, 5f), new Color(0.82f, 0.78f, 0.70f), solid: true);
            // Supply carts and cook-fires along the road edges (not before any door).
            foreach (var (x, z) in new[] { (-30f, -20f), (30f, -20f), (-32f, 8f), (32f, 8f), (30f, 24f) })
                Billboard(env, $"Supply cart {x},{z}", "supply_cart", x, z, new Vector3(3f, 2f, 1.6f), wood, solid: true);
            // The oath altar (three rammed-earth tiers) and the coalition leader's great tent behind it.
            Billboard(env, "Oath altar", "altar_huimeng", 0, 26f, new Vector3(9f, 3f, 6f), new Color(0.74f, 0.64f, 0.48f), solid: true);
            Billboard(env, "Leader's tent", "tent_lord_yuanshao", 0, 35f, new Vector3(16f, 8f, 8f), new Color(0.56f, 0.12f, 0.14f), solid: true);
            Billboard(env, "Banner 袁 W", "banner_yuan", -9f, 30f, new Vector3(0.9f, 6.5f, 0.3f), new Color(0.56f, 0.12f, 0.14f));
            Billboard(env, "Banner 袁 E", "banner_yuan", 9f, 30f, new Vector3(0.9f, 6.5f, 0.3f), new Color(0.56f, 0.12f, 0.14f));
            Far(env, "far_central_plain", 70f, 200f, 36f, Color.white);

            var p = f.People;
            var people = new List<Transform>
            {
                Person(p, "yuan_shao 袁绍", Folk("D05"), new Vector3(0, 0, 21.6f)),
                Person(p, "cao_cao 曹操", Folk("C02"), new Vector3(-10.8f, 0, -12.4f)),
                Person(p, "sun_jian 孙坚", Folk("D08"), new Vector3(10.8f, 0, -12.4f)),
                Person(p, "yuan_shu 袁术", Folk("D06"), new Vector3(-10.8f, 0, 2.6f)),
                Person(p, "kong_rong 孔融", Folk("D07"), new Vector3(10.8f, 0, 2.6f)),
                Person(p, "tao_qian 陶谦", Folk("D09"), new Vector3(-10.8f, 0, 14.6f)),
                Person(p, "sz_envoy 诸侯使者", Resident("N07"), new Vector3(15f, 0, 11.6f)),
                Person(p, "sz_soldier_a 联军士卒", Folk("D13"), new Vector3(5.4f, 0, -5f)),
                Person(p, "sz_soldier_b 联军士卒", Folk("D13"), new Vector3(-5.2f, 0, 8f)),
                Person(p, "sz_merchant 随军商贩", Resident("N20"), new Vector3(-5.4f, 0, -18f)),
                Person(p, "sz_gs_soldier 公孙瓒营士卒", Folk("D02"), new Vector3(-8.6f, 0, -22.6f)),
            };
            for (int i = 0; i < 4; i++)
                Person(p, "Coalition soldier " + i, Folk("D13"), new Vector3(i % 2 == 0 ? -4.6f : 4.6f, 0, -26.4f + (i / 2) * 50f));
            people.AddRange(SidePeople(FieldStoryController.Place.Suanzao, p));
            var c = f.Controller;
            c.Location = FieldStoryController.Place.Suanzao;
            c.People = people.ToArray();
            c.Spots = new[] { Spot(f, "edict 绢帛", new Vector3(5.4f, 0, 30.4f)) };
            c.ArrivalSpot = Spot(f, "Arrival (inside the gate)", new Vector3(0, 0, -25f));
            c.ReturnSpot = Spot(f, "Return (plaza)", new Vector3(0, 0, 18f));
            Huds(c);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), SuanzaoPath);
        }
    }
}
