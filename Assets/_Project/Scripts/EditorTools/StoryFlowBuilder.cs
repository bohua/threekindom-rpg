using System.Linq;
using Jrpg;
using ThreeKingdoms.Data.Battle;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.UI.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThreeKingdoms.EditorTools
{
    // Builds the Alpha 0.1 main line (ADR-0006): the three battle scenes, the Zhuojun town, then wires the town's story
    // (Guan Yu and Zhang Fei, the peach-garden oath, the south gate) and makes the town the first build scene, so a
    // player build starts a new game outside the south gate.
    public static class StoryFlowBuilder
    {
        const string PanelSettingsPath = "Assets/_Project/Settings/BattlePanelSettings.asset";

        [MenuItem("JRPG/Build Main Line (town + battles 1-3)")]
        public static void Build()
        {
            BattleSceneBuilder.Build();
            ZhuojunTownBuilder.Build();
            WireTown();

            string town = ZhuojunTownBuilder.ScenePath;
            var battles = new[] { BattleSceneBuilder.ScenePath, "Assets/_Project/Scenes/Battle_Tutorial02.unity", "Assets/_Project/Scenes/Battle_Tutorial03.unity" };
            var first = new[] { town }.Concat(battles).ToArray();
            var scenes = EditorBuildSettings.scenes.Where(s => !first.Contains(s.path)).ToList();
            scenes.InsertRange(0, first.Select(p => new EditorBuildSettingsScene(p, true)));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("MAIN_LINE_BUILT");
        }

        public static void BuildFromCommandLine()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static void WireTown()
        {
            var scene = EditorSceneManager.OpenScene(ZhuojunTownBuilder.ScenePath, OpenSceneMode.Single);
            var spots = new GameObject("Story spots").transform;
            Transform Spot(string name, Vector3 at) { var t = new GameObject(name).transform; t.SetParent(spots, false); t.position = at; return t; }

            var go = new GameObject("Town Story");
            var c = go.AddComponent<TownStoryController>();
            c.Catalog = AssetDatabase.LoadAssetAtPath<BattleCatalog>(BattleDataBuilder.CatalogPath);
            c.Player = Object.FindFirstObjectByType<PlayerMover>();
            c.Camera = Object.FindFirstObjectByType<FollowCamera>();
            c.GuanYu = GameObject.Find("Guan Yu").transform;
            c.ZhangFei = GameObject.Find("Zhang Fei").transform;
            c.InteractAction = ProjectSetup.Action("Interact");
            c.MenuAction = ProjectSetup.Action("Menu");
            c.NewGameSpot = Spot("New game (outside the south gate)", ZhuojunTownBuilder.PlayerStart);
            c.ReturnSpot = Spot("Return from battle (inside the south gate)", new Vector3(0, 0, -29f));      // GDD answer 3
            c.OathEndSpot = Spot("After the oath (inside the west gate)", new Vector3(-43.5f, 0, 0f));      // GDD answer 7
            c.GroveFocus = Spot("Peach grove focus", new Vector3(-66f, 0, 0f));
            c.GroveSpots = new[]
            {
                Spot("Grove Liu Bei", new Vector3(-66f, 0, -1.2f)),
                Spot("Grove Guan Yu", new Vector3(-67.8f, 0, 0.2f)),
                Spot("Grove Zhang Fei", new Vector3(-64.2f, 0, 0.2f)),
            };

            var hudGo = new GameObject("Town HUD");
            var doc = hudGo.AddComponent<UIDocument>();
            doc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (!doc.panelSettings) throw new System.InvalidOperationException("Town HUD has no PanelSettings");
            hudGo.AddComponent<TownHud>().Controller = c;

            EditorSceneManager.SaveScene(scene);
        }
    }
}
