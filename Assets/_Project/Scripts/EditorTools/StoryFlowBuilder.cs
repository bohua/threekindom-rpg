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

        // The Yellow Turbans blocking the south gate (user 2026-10-22), just outside its passage: one group per battle,
        // the leader in front and first. Sprite keys follow INITIAL_SCENARIO_ART: Cheng Yuanzhi and the unnamed officers
        // share the generic sheet, the three brothers have their own. A wall across the passage's outer end keeps Liu
        // Bei from walking past while a group stands there.
        static void GateBlockers(TownStoryController c)
        {
            var parent = new GameObject("Gate blockers").transform;
            // A squad with its general in front (user 2026-10-22): the battle's named units plus rank-and-file.
            string[][] groups =
            {
                new[] { "YellowTurban", "YellowTurban", "YellowTurban", "YellowTurban", "YellowTurban", "YellowTurban" },
                new[] { "ZhangLiang", "YellowTurban", "YellowTurban", "YellowTurban", "YellowTurban", "YellowTurban" },
                new[] { "ZhangJiao", "ZhangBao", "ZhangLiang", "YellowTurban", "YellowTurban", "YellowTurban" },
            };
            string[][] names =
            {
                new[] { "程远志", "黄巾头领 甲", "黄巾头领 乙", "黄巾兵 甲", "黄巾兵 乙", "黄巾兵 丙" },
                new[] { "张梁", "黄巾将领 甲", "黄巾将领 乙", "黄巾将领 丙", "黄巾兵 甲", "黄巾兵 乙" },
                new[] { "张角", "张宝", "张梁", "黄巾兵 甲", "黄巾兵 乙", "黄巾兵 丙" },
            };
            // Leader at the centre, the rest in two ranks behind him, all facing the gate.
            Vector3[] posts =
            {
                new Vector3(0f, 0, -42f), new Vector3(-1.6f, 0, -43f), new Vector3(1.6f, 0, -43f),
                new Vector3(-3f, 0, -44f), new Vector3(0f, 0, -44.2f), new Vector3(3f, 0, -44f),
            };
            c.GateBlockers = new Transform[groups.Length];
            for (int g = 0; g < groups.Length; g++)
            {
                var group = new GameObject($"battle_{g + 1}").transform;
                group.SetParent(parent, false);
                for (int i = 0; i < groups[g].Length; i++)
                {
                    var npc = HD2DSampleBuilder.Character(names[g][i], groups[g][i], posts[i], 0, isPlayer: false);
                    npc.transform.SetParent(group, true);
                }
                group.gameObject.SetActive(false);
                c.GateBlockers[g] = group;
            }
            var wall = new GameObject("Gate wall (while blocked)").AddComponent<BoxCollider>();
            wall.transform.SetParent(parent, false);
            wall.transform.position = new Vector3(0, 1f, -40.4f);
            wall.size = new Vector3(c.GateSize.x + 2f, 2f, 0.4f);
            wall.enabled = false;
            c.GateWall = wall;

            // Two guards inside each gate (user 2026-10-22), in the gate guard's look (N01).
            string guardKey = ResidentImporter.Residents.First(r => r.Id == "N01").Key;
            Transform Guards(string name, Vector3 a, Vector3 b)
            {
                var group = new GameObject(name).transform;
                group.SetParent(parent, false);
                HD2DSampleBuilder.Character(name + " 甲", guardKey, a, 0, isPlayer: false).transform.SetParent(group, true);
                HD2DSampleBuilder.Character(name + " 乙", guardKey, b, 0, isPlayer: false).transform.SetParent(group, true);
                return group;
            }
            c.Gates = new[]
            {
                new TownStoryController.CityGate { Id = "south", Center = c.GateCenter, Size = c.GateSize, Inward = Vector3.forward,
                    Guards = Guards("南门卫兵", new Vector3(-2.5f, 0, -32.8f), new Vector3(2.5f, 0, -32.8f)) },
                new TownStoryController.CityGate { Id = "west", Center = new Vector3(-48f, 1f, 0f), Size = new Vector3(5f, 3f, 3f), Inward = Vector3.right,
                    Guards = Guards("西门卫兵", new Vector3(-44.8f, 0, -1.9f), new Vector3(-44.8f, 0, 1.9f)) },
            };
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
            GateBlockers(c);
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
            townHud.Portraits = AssetDatabase.LoadAssetAtPath<DialoguePortraitSet>(DialoguePortraitImporter.SetPath);

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
