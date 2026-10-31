using System.Collections.Generic;
using System.Linq;
using Jrpg;
using ThreeKingdoms.Data.Battle;
using ThreeKingdoms.Gameplay;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.UI.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThreeKingdoms.EditorTools
{
    // The R1 涿蓟 map's story layer (npc-sidequests R1_R2, core layer; user 2026-10-29): opens the built Region_R1 and
    // lays over it the people of Ji and Lousang at their doors, a FieldStoryController (Place.R1, FieldScriptsR1), the
    // stage, shop, save and menu HUDs and the 天下传闻 panel — as the 0.4 Pingyuan layer over the Bohai plain. Run after
    // RegionGreyboxBuilder has built R1 (it calls this itself).
    public static partial class ChapterFieldBuilder
    {
        const string R1ScenePath = "Assets/_Project/Scenes/Region_R1.unity";

        // A spot from the refined Lousang layout's "npc_spots" (local metres from its origin), in world x/z.
        static bool LousangSpot(string id, out Vector2 at)
        {
            at = default;
            string path = @"D:\BuildArtifacts\Codex\hd2d-jrpg\art\lousang-hd-blender\_checks\layout_placements.json";
            if (!System.IO.File.Exists(path)) return false;
            string json = System.IO.File.ReadAllText(path);
            int spots = json.IndexOf("\"npc_spots\"", System.StringComparison.Ordinal);
            if (spots < 0) return false;
            var m = System.Text.RegularExpressions.Regex.Match(json.Substring(spots), "\"" + id + "\"\\s*:\\s*\\[\\s*(-?[0-9.]+)\\s*,\\s*(-?[0-9.]+)");
            if (!m.Success) return false;
            var origin = new Vector2(170f, 64f);
            var o = System.Text.RegularExpressions.Regex.Match(json, "\"origin_world_xz_m\"\\s*:\\s*\\[\\s*(-?[0-9.]+)\\s*,\\s*(-?[0-9.]+)");
            if (o.Success) origin = new Vector2(float.Parse(o.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), float.Parse(o.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture));
            at = origin + new Vector2(float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), float.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture));
            return true;
        }

        public static void BuildR1Story()
        {
            if (!System.IO.File.Exists(R1ScenePath)) { Debug.LogWarning("Region_R1 not built; skipping its story layer."); return; }
            EditorSceneManager.OpenScene(R1ScenePath, OpenSceneMode.Single);
            foreach (var n in new[] { "R1 Story", "R1 People", "R1 Spots", "Stage HUD", "Shop HUD", "Save HUD", "Main Menu HUD", "Rumour HUD", "EventSystem" })
            {
                var g = GameObject.Find(n);
                if (g) Object.DestroyImmediate(g);
            }

            var player = Object.FindFirstObjectByType<PlayerMover>();
            var follow = Object.FindFirstObjectByType<FollowCamera>();
            var followers = Object.FindObjectsByType<PartyFollower>(FindObjectsSortMode.None);
            var people = new GameObject("R1 People").transform;
            var spots = new GameObject("R1 Spots").transform;
            // Standing on the region's ground (the terrain chunks' mesh colliders).
            Vector3 On(float x, float z)
            {
                foreach (var h in Physics.RaycastAll(new Vector3(x, 400f, z), Vector3.down, 800f).OrderBy(h => h.distance))
                    if (h.collider is MeshCollider || h.collider is BoxCollider && h.collider.name.StartsWith("COL")) return h.point;
                return new Vector3(x, RegionGreyboxBuilder.H(x, z), z);
            }
            Transform At(string name, string key, float x, float z, int frame = 0)
            {
                // Lousang's people stand where the refined village's layout puts them (art/lousang-hd-blender npc_spots).
                if (LousangSpot(name.Split(' ')[0], out var at)) { x = at.x; z = at.y; }
                return Person(people, name, key, On(x, z), frame);
            }
            Transform Spot3(string name, float x, float z) { var t = new GameObject(name).transform; t.SetParent(spots, false); t.position = On(x, z); return t; }

            var list = new List<Transform>
            {
                // Ji (centre 368, 219; south gate z 184; prefecture steps z ≈ 222; inn west street; east gate x ≈ 407)
                At("ji_jian_yong 简雍", Folk("D03"), 371f, 190.5f),
                At("ji_jian_yong_hall 简雍", Folk("D03"), 362.5f, 221f),
                At("liu_yu 刘虞", Resident("N03"), 368f, 222.5f),
                At("ji_gate_clerk 秦门吏", Resident("N01"), 373.5f, 221.5f),
                At("ji_inn_keeper 魏掌柜", Resident("N05"), 356.5f, 217f),
                At("ji_guest_camp 营里的客", Resident("N25"), 356.5f, 220.5f),
                At("ji_guest_state 州里的客", Resident("N16"), 356.5f, 213.5f),
                At("ji_refugee 许氏", Resident("N21"), 342f, 201f),
                At("ji_east_gate 通事老达", Resident("N19"), 407f, 222.5f),
                At("kumo 库莫", Resident("N12"), 407f, 215.5f),
                // 涿蓟驿 (264, 137): the courier for the realm map (RegionTravel), the arrival spot beside him.
                At(FieldScriptsRegions.Courier + " 驿卒", Resident("N07"), 257f, 128f),
                // Lousang (centre 170, 64; Liu Bei's home NE, the tile house NW, the school S, the great mulberry 182, 80)
                At("ls_mother 刘母", Resident("N08"), 178.5f, 72f),
                At("ls_zijing 刘子敬", Resident("N09"), 186.5f, 77.5f),
                At("ls_yuanqi 刘元起", Resident("N11"), 161.5f, 72.5f),
                At("ls_yuanqi_wife 元起妻", Resident("N26"), 159.2f, 70.6f),
                At("ls_deran 刘德然", Resident("N24"), 170f, 51.5f),
            };

            var go = new GameObject("R1 Story");
            var c = go.AddComponent<FieldStoryController>();
            c.Location = FieldStoryController.Place.R1;
            c.Catalog = AssetDatabase.LoadAssetAtPath<BattleCatalog>(BattleDataBuilder.CatalogPath);
            c.Player = player;
            c.Camera = follow;
            c.View = follow ? follow.GetComponent<Camera>() : Camera.main;
            c.GuanYuFollower = followers.FirstOrDefault(f => f.name.StartsWith("Guan Yu"));
            c.ZhangFeiFollower = followers.FirstOrDefault(f => f.name.StartsWith("Zhang Fei"));
            c.InteractAction = ProjectSetup.Action("Interact");
            c.MenuAction = ProjectSetup.Action("Menu");
            c.Stage = go.AddComponent<StagePlayer>();
            c.Stage.Confirm = ProjectSetup.Action("Interact");
            c.People = list.ToArray();
            var tree = LousangSpot("ls_tree", out var treeAt) ? treeAt : new Vector2(182f, 76.5f);
            c.Spots = new[] { Spot3("ls_tree 大桑树", tree.x, tree.y) };
            // The new game's opening (FieldStoryController.NewGameOpening): mother and son face to face in the yard.
            if (LousangSpot("opening_mother", out var om)) Spot3("opening_mother", om.x, om.y);
            if (LousangSpot("opening_liu_bei", out var ol)) Spot3("opening_liu_bei", ol.x, ol.y);
            Spot3(RegionTravel.PostArrival, 260f, 131f);
            var start = player ? player.transform.position : Vector3.zero;
            c.ArrivalSpot = Spot3("Arrival (region start)", start.x, start.z);
            c.ReturnSpot = c.ArrivalSpot;
            Huds(c);
            var rumourGo = new GameObject("Rumour HUD");
            var doc = rumourGo.AddComponent<UIDocument>();
            doc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            doc.sortingOrder = 50;
            rumourGo.AddComponent<RumorHud>().Host = c;
            LousangAmbienceBuilder.Build((x, z) => On(x, z).y);   // petals, motes, leaf light, the warm grade (user 2026-10-30)
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), R1ScenePath);
            Debug.Log("R1_STORY_BUILT");
        }
    }
}
