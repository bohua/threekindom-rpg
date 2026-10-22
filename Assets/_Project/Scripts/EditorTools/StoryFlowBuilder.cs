using System.Linq;
using Jrpg;
using ThreeKingdoms.Data.Battle;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.UI.Save;
using ThreeKingdoms.UI.Shop;
using ThreeKingdoms.UI.Story;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
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
            WalkCycleImporter.Import();   // 9 poses + 16 walk frames per character atlas (CHARACTER_WALK_CYCLES.md)
            ResidentImporter.Import();    // the 12 Zhuojun residents (ZHUOJUN_RESIDENTS.md)
            BattleSceneBuilder.Build();
            ZhuojunTownBuilder.Build();
            WireTown();
            BuildTitle();

            string town = ZhuojunTownBuilder.ScenePath;
            var battles = new[] { BattleSceneBuilder.ScenePath, "Assets/_Project/Scenes/Battle_Tutorial02.unity", "Assets/_Project/Scenes/Battle_Tutorial03.unity" };
            var first = new[] { TitlePath, town }.Concat(battles).ToArray();
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

        // A walking copy of a companion without a collider (it must never block Liu Bei); hidden until they join.
        static ThreeKingdoms.Gameplay.PartyFollower Follower(string name, string key)
        {
            var go = HD2DSampleBuilder.Character(name, key, Vector3.zero, 9, isPlayer: false);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var animator = go.GetComponentInChildren<ThreeKingdoms.Gameplay.SpriteFrameAnimator>();
            go.AddComponent<ThreeKingdoms.Gameplay.PixelWalkFrames>().Animator = animator;
            var f = go.AddComponent<ThreeKingdoms.Gameplay.PartyFollower>();
            go.SetActive(false);
            return f;
        }

        public const string TitlePath = "Assets/_Project/Scenes/Title.unity";

        // Title screen (game-ui GDD): new game, load, quit. A plain dark backdrop until key art exists.
        static void BuildTitle()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.05f, 0.08f);
            var go = new GameObject("Title");
            var controller = go.AddComponent<TitleController>();
            controller.Catalog = AssetDatabase.LoadAssetAtPath<BattleCatalog>(BattleDataBuilder.CatalogPath);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (!doc.panelSettings) throw new System.InvalidOperationException("Title has no PanelSettings");
            var titleHud = go.AddComponent<TitleHud>(); titleHud.Controller = controller; titleHud.Skin = AssetDatabase.LoadAssetAtPath<ThreeKingdoms.UI.Battle.BattleUiSkin>(BattleUiSkinBuilder.SkinPath);
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
            EditorSceneManager.SaveScene(scene, TitlePath);
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
            c.Player.gameObject.AddComponent<ThreeKingdoms.Gameplay.PartyTrail>();
            c.GuanYuFollower = Follower("Guan Yu (follower)", "GuanYu");
            c.ZhangFeiFollower = Follower("Zhang Fei (follower)", "ZhangFei");
            c.Smith = GameObject.Find("N04 铁匠兼装备商")?.transform;
            if (!c.Smith) throw new System.InvalidOperationException("Town has no N04 smith post");
            c.Innkeeper = GameObject.Find("N05 酒馆掌柜")?.transform;
            if (!c.Innkeeper) throw new System.InvalidOperationException("Town has no N05 innkeeper post");
            c.Residents = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.parent && t.parent.name == "Residents").OrderBy(t => t.name).ToArray();
            if (c.Residents.Length != 12) throw new System.InvalidOperationException($"Town has {c.Residents.Length} residents, expected 12");
            c.InteractAction = ProjectSetup.Action("Interact");
            c.MenuAction = ProjectSetup.Action("Menu");
            c.NewGameSpot = Spot("New game (outside the south gate)", ZhuojunTownBuilder.PlayerStart);
            c.ReturnSpot = Spot("Return from battle (inside the south gate)", new Vector3(0, 0, -29f));      // GDD answer 3
            c.OathEndSpot = Spot("After the oath (inside the west gate)", new Vector3(-43.5f, 0, 0f));      // GDD answer 7
            c.TavernSpot = Spot("Loaded save (before the tavern keeper)", c.Innkeeper.position + new Vector3(0, 0, -1.4f));
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
            var townHud = hudGo.AddComponent<TownHud>(); townHud.Controller = c; townHud.Skin = AssetDatabase.LoadAssetAtPath<ThreeKingdoms.UI.Battle.BattleUiSkin>(BattleUiSkinBuilder.SkinPath);
            townHud.Portraits = DialoguePortraitImporter.Import();

            // The shop sits above the town HUD; menus need an EventSystem for gamepad and keyboard navigation.
            var shopGo = new GameObject("Shop HUD");
            var shopDoc = shopGo.AddComponent<UIDocument>();
            shopDoc.panelSettings = doc.panelSettings;
            shopDoc.sortingOrder = 10;
            var shopHud = shopGo.AddComponent<ShopHud>(); shopHud.Host = c; shopHud.Skin = AssetDatabase.LoadAssetAtPath<ThreeKingdoms.UI.Battle.BattleUiSkin>(BattleUiSkinBuilder.SkinPath);
            var saveGo = new GameObject("Save HUD");
            var saveDoc = saveGo.AddComponent<UIDocument>();
            saveDoc.panelSettings = doc.panelSettings;
            saveDoc.sortingOrder = 10;
            var saveHud = saveGo.AddComponent<SaveHud>(); saveHud.Controller = c; saveHud.Skin = AssetDatabase.LoadAssetAtPath<ThreeKingdoms.UI.Battle.BattleUiSkin>(BattleUiSkinBuilder.SkinPath);
            var menuGo = new GameObject("Main Menu HUD");
            var menuDoc = menuGo.AddComponent<UIDocument>();
            menuDoc.panelSettings = doc.panelSettings;
            menuDoc.sortingOrder = 10;
            var mainMenu = menuGo.AddComponent<ThreeKingdoms.UI.Menu.MainMenuHud>(); mainMenu.Controller = c; mainMenu.Skin = AssetDatabase.LoadAssetAtPath<ThreeKingdoms.UI.Battle.BattleUiSkin>(BattleUiSkinBuilder.SkinPath);
            if (!Object.FindFirstObjectByType<EventSystem>())
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }

            EditorSceneManager.SaveScene(scene);
        }
    }
}
