using System.Collections.Generic;
using System.Linq;
using Jrpg;
using ThreeKingdoms.Data.Battle;
using ThreeKingdoms.Gameplay;
using ThreeKingdoms.Gameplay.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ThreeKingdoms.EditorTools
{
    // 0.4 河北篇 (V04_HEBEI_PLAN §3.1, REVIEW_V04 R1, R19–R20): the story layer over the probe's Bohai plain. The region
    // itself is BohaiPlainBuilder's (art probe, PR #18) and is not rebuilt here; this opens the built scene and adds the
    // Pingyuan base's people at their doors, the story controller, the overlays and the save points. Built after the probe
    // scene, so rebuilding the region keeps this layer by running both.
    public static partial class ChapterFieldBuilder
    {
        public static void BuildBohaiStory()
        {
            if (!System.IO.File.Exists(BohaiPlainBuilder.ScenePath)) { Debug.LogWarning("Bohai_Plain not built; skipping the 0.4 story layer."); return; }
            mats.Clear();
            EditorSceneManager.OpenScene(BohaiPlainBuilder.ScenePath, OpenSceneMode.Single);
            var old = GameObject.Find("Pingyuan Story");
            if (old) Object.DestroyImmediate(old);
            foreach (var n in new[] { "Pingyuan People", "Pingyuan Spots", "Stage HUD", "Shop HUD", "Save HUD", "Main Menu HUD", "EventSystem" })
            {
                var g = GameObject.Find(n);
                if (g) Object.DestroyImmediate(g);
            }

            var player = Object.FindFirstObjectByType<PlayerMover>();
            var follow = Object.FindFirstObjectByType<FollowCamera>();
            var followers = Object.FindObjectsByType<PartyFollower>(FindObjectsSortMode.None);
            var people = new GameObject("Pingyuan People").transform;
            var spots = new GameObject("Pingyuan Spots").transform;
            Transform Spot3(string name, Vector3 at) { var t = new GameObject(name).transform; t.SetParent(spots, false); t.position = at; return t; }

            float cx = BohaiPlainBuilder.CityCx;
            var list = new List<Transform>
            {
                // R19: the steps stay free for Liu Bei; the clerk keeps the records at their foot, to the side.
                Person(people, "py_clerk 县吏", Resident("N03"), new Vector3(cx - 6f, 0, -146.5f)),
                Person(people, "jian_yong 简雍", Folk("D03"), new Vector3(cx + 12f, 0, -152f)),
                Person(people, "tian_yu 田豫", Folk("D04"), new Vector3(-28f, 0, -147f)),
                Person(people, "py_elder 平原老者", Folk("E14"), new Vector3(cx - 22f, 0, -160f)),
                Person(people, "py_farmer_a 东村农夫", Folk("E12"), new Vector3(cx - 30f, 0, -176f)),
                Person(people, "py_farmer_b 西村农夫", Resident("N27"), new Vector3(cx - 27f, 0, -178f)),
                Person(people, "py_courier 驿卒", Resident("N07"), new Vector3(cx + 3f, 0, -188f)),
                Person(people, "py_ferry 渡口吏", Resident("N01"), new Vector3(cx + 4f, 0, -205f)),
                Person(people, "py_post_guard 西路驿卒", Resident("N07"), new Vector3(-385f, 0, 22f)),
                Person(people, "py_merchant 商贩", Resident("N20"), new Vector3(cx - 24f, 0, -146f)),
                Person(people, "py_man 平原县民", Folk("E12"), new Vector3(cx - 8f, 0, -164f)),
                Person(people, "py_woman 平原县民", Folk("E13"), new Vector3(cx + 7f, 0, -166f)),
                Person(people, "py_child 孩童", Folk("E15"), new Vector3(cx + 2f, 0, -170f)),
                Person(people, "assassin 刺客", Folk("E10"), new Vector3(cx, 0, -300f)),   // stands off-map until E9
            };

            var go = new GameObject("Pingyuan Story");
            var c = go.AddComponent<FieldStoryController>();
            c.Location = FieldStoryController.Place.Pingyuan;
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
            c.Spots = new[] { Spot3("canal 水渠闸口", new Vector3(cx - 36f, 0, -180f)) };
            c.ArrivalSpot = Spot3("Arrival (county office square)", BohaiPlainBuilder.PlayerStart);
            c.ReturnSpot = Spot3("Return (inside the south gate)", new Vector3(cx, 0, BohaiPlainBuilder.CityZ0 + 9f));
            Huds(c);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), BohaiPlainBuilder.ScenePath);
            Debug.Log("BOHAI_STORY_BUILT");
        }
    }
}
