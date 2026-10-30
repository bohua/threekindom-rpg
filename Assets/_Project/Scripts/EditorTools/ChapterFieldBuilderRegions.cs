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
    // The story layer over chapter one's open regions (RegionTravel, coordinator 2026-10-30), as the 0.4 Pingyuan base is
    // laid over the Bohai plain (BuildBohaiStory): a FieldStoryController, the story HUDs, a post-station courier with the
    // realm map, the arrival spot every journey lands on, and the region's side-quest people (SidePeople). The region
    // scenes are generated (RegionGreyboxBuilder, ZhongshanBuilder), so this runs after them and again on every rebuild.
    public static partial class ChapterFieldBuilder
    {
        static readonly (FieldStoryController.Place place, string scene)[] RegionStories =
        {
            (FieldStoryController.Place.R2, "Assets/_Project/Scenes/Zhongshan_Region.unity"),
            (FieldStoryController.Place.R3, "Assets/_Project/Scenes/Region_R3.unity"),
            (FieldStoryController.Place.R5a, "Assets/_Project/Scenes/Region_R5A.unity"),
            (FieldStoryController.Place.R5b, "Assets/_Project/Scenes/Region_R5B.unity"),
            (FieldStoryController.Place.R6, "Assets/_Project/Scenes/Region_R6.unity"),
            (FieldStoryController.Place.R7, "Assets/_Project/Scenes/Region_R7.unity"),
            (FieldStoryController.Place.R8, "Assets/_Project/Scenes/Region_R8.unity"),
        };

        internal static FieldStoryController.Place? RegionPlace(string id) => id switch
        {
            "R3" => FieldStoryController.Place.R3, "R5A" => FieldStoryController.Place.R5a, "R5B" => FieldStoryController.Place.R5b,
            "R6" => FieldStoryController.Place.R6, "R7" => FieldStoryController.Place.R7, "R8" => FieldStoryController.Place.R8,
            _ => null,
        };

        [MenuItem("JRPG/Build region story layers (travel, side quests)")]
        public static void BuildRegionStories()
        {
            foreach (var (place, scene) in RegionStories)
                if (System.IO.File.Exists(scene)) BuildRegionStory(place, scene);
            var paths = RegionStories.Select(r => r.scene).Where(System.IO.File.Exists).ToArray();
            var scenes = EditorBuildSettings.scenes.Where(s => !paths.Contains(s.path)).ToList();
            scenes.AddRange(paths.Select(p => new EditorBuildSettingsScene(p, true)));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("REGION_STORIES_BUILT");
        }

        public static void BuildRegionStoriesFromCommandLine()
        {
            try { BuildRegionStories(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        internal static void BuildRegionStory(FieldStoryController.Place place, string scenePath)
        {
            mats.Clear();
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            foreach (var n in new[] { "Region Story", "Region People", "Region Spots", "Stage HUD", "Shop HUD", "Save HUD", "Main Menu HUD", "EventSystem" })
            {
                var g = GameObject.Find(n);
                if (g) Object.DestroyImmediate(g);
            }
            var player = Object.FindFirstObjectByType<PlayerMover>();
            var follow = Object.FindFirstObjectByType<FollowCamera>();
            var followers = Object.FindObjectsByType<PartyFollower>(FindObjectsSortMode.None);
            var people = new GameObject("Region People").transform;
            var spots = new GameObject("Region Spots").transform;
            Transform Spot3(string name, Vector3 at) { var t = new GameObject(name).transform; t.SetParent(spots, false); t.position = at; return t; }

            // The post station: the place zone named 驿 (each region's layout has one), else where the player starts.
            var post = Object.FindObjectsByType<LocationZone>(FindObjectsSortMode.None).FirstOrDefault(z => z.Minor != null && z.Minor.Contains("驿"));
            Vector3 station = Ground(post ? post.transform.position : player.transform.position);
            var list = new List<Transform>
            {
                Person(people, FieldScriptsRegions.Courier + " 驿卒", Resident("N07"), Ground(station + new Vector3(2.2f, 0, 1.2f))),
            };
            list.AddRange(SidePeople(place, people));

            var go = new GameObject("Region Story");
            var c = go.AddComponent<FieldStoryController>();
            c.Location = place;
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
            c.Spots = SideSpots(place, spots).ToArray();
            c.ArrivalSpot = Spot3(RegionTravel.PostArrival, station);
            c.ReturnSpot = c.ArrivalSpot;
            Huds(c);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), scenePath);
            Debug.Log("REGION_STORY_BUILT " + place);
        }

        // The ground under a point: the lowest solid hit (the terrain, not a roof above it).
        static Vector3 Ground(Vector3 at)
        {
            Physics.SyncTransforms();
            var hits = Physics.RaycastAll(new Vector3(at.x, 400f, at.z), Vector3.down, 800f);
            var ground = hits.Where(h => !h.collider.isTrigger).OrderByDescending(h => h.distance).Select(h => (Vector3?)h.point).FirstOrDefault();
            return ground ?? at;
        }
    }
}
