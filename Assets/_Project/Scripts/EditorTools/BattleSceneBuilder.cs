using System.IO;
using System.Linq;
using ThreeKingdoms.Gameplay;
using ThreeKingdoms.Gameplay.Battle;
using ThreeKingdoms.UI.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;

namespace ThreeKingdoms.EditorTools
{
    // Builds the playable acceptance scene for battle 1 (Liu / Guan / Zhang vs Cheng Yuanzhi + 2 headmen) on top
    // of the Zhuojun market battle sample: unit views, BattleController, the UI Toolkit HUD and an EventSystem.
    public static class BattleSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Battle_Tutorial01.unity";
        const string UiRoot = "Assets/_Project/Art/UI";
        const string PortraitRoot = "Assets/_Project/Art/Portraits";
        const string PanelSettingsPath = "Assets/_Project/Settings/BattlePanelSettings.asset";
        const string ThemePath = "Assets/_Project/Settings/BattleRuntimeTheme.tss";

        // Feet positions in battle-camera units (ortho size 6): kept above the card strip at the bottom.
        static readonly Vector2[] PlayerFeet = { new Vector2(-5.4f, -0.4f), new Vector2(-3.9f, -1.8f), new Vector2(-5.6f, -3.2f) };
        static readonly Vector2[] EnemyFeet = { new Vector2(4.2f, -0.4f), new Vector2(5.8f, -1.3f), new Vector2(4.2f, -2.3f), new Vector2(5.8f, -3.2f) };

        // One playable scene per V1 battle (V1_SPEC §2.2). Battle 1 is fought in the market; battles 2–3 in front of the
        // Yellow Turban camp outside the walls (the second of the two backdrops V1_SPEC §2.3 allows). Enemy sprite keys follow INITIAL_SCENARIO_ART: generic
        // Yellow Turban officers share one sheet; the three brothers each have their own.
        sealed class BattleSpec
        {
            public string Path, EncounterId, Next, Location, BaseScene = BattleBackdropBaker.ScenePath;
            public string[] EnemySprites;
            public int StartExp, StartCopper;
        }

        static readonly BattleSpec[] Battles =
        {
            new BattleSpec { Path = ScenePath, EncounterId = "battle_1", Next = "Battle_Tutorial02", Location = "涿郡 · 街市",
                EnemySprites = new[] { "YellowTurban", "YellowTurban", "YellowTurban" }, StartExp = 0, StartCopper = 120 },
            new BattleSpec { Path = "Assets/_Project/Scenes/Battle_Tutorial02.unity", EncounterId = "battle_2", Next = "Battle_Tutorial03", Location = "涿郡 · 城外", BaseScene = BattleBackdropBaker.OutskirtsScenePath,
                EnemySprites = new[] { "ZhangLiang", "YellowTurban", "YellowTurban", "YellowTurban" }, StartExp = 10, StartCopper = 150 },
            new BattleSpec { Path = "Assets/_Project/Scenes/Battle_Tutorial03.unity", EncounterId = "battle_3", Next = "", Location = "涿郡 · 城外", BaseScene = BattleBackdropBaker.OutskirtsScenePath,
                EnemySprites = new[] { "ZhangJiao", "ZhangBao", "ZhangLiang" }, StartExp = 30, StartCopper = 200 },
        };

        // The three brothers' 3×3 sheets (INITIAL_SCENARIO_ART.md); the generic Yellow Turban atlas already exists.
        static readonly (string Key, string Sheet, int FigureHeight)[] EnemySheets =
        {
            ("ZhangJiao", @"initial-scenario-enemy-art-20261018-v1\ZHANG_JIAO_HD2D_3X3.png", 45),
            ("ZhangBao", @"initial-scenario-enemy-art-20261018-v1\ZHANG_BAO_HD2D_3X3.png", 45),
            ("ZhangLiang", @"initial-scenario-enemy-art-20261018-v1\ZHANG_LIANG_HD2D_3X3.png", 45),
        };

        [MenuItem("JRPG/Build Battle Tutorials 01-03 (playable)")]
        public static void Build()
        {
            // Base scenes first: building them switches scenes, which unloads assets loaded before (panel settings, icons).
            BattleBackdropBaker.BuildOutskirts();
            var catalog = BattleDataBuilder.Build();
            BattleUiSkinBuilder.Build();   // BATTLE_UI_SKIN.md pixel frames
            var icons = BuildTroopIcons();
            var panel = BuildPanelSettings();
            ConvertEnemySheets();
            if (!File.Exists(BattleBackdropBaker.ScenePath)) BattleBackdropBaker.Build();
            foreach (var battle in Battles) BuildScene(battle, catalog, icons, panel);

            // Battle 1 first = what a player build opens without arguments (user 2026-10-19); 2 and 3 follow.
            var paths = Battles.Select(b => b.Path).ToArray();
            var scenes = EditorBuildSettings.scenes.Where(s => !paths.Contains(s.path)).ToList();
            scenes.InsertRange(0, paths.Select(p => new EditorBuildSettingsScene(p, true)));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("BATTLE_TUTORIAL_BUILT");
        }

        static void ConvertEnemySheets()
        {
            string root = System.Environment.GetEnvironmentVariable("HD2D_ART_SOURCE");
            if (string.IsNullOrEmpty(root)) root = @"D:\BuildArtifacts\Codex\hd2d-jrpg";
            foreach (var (key, sheet, figureHeight) in EnemySheets)
            {
                string source = Path.Combine(root, sheet);
                if (!File.Exists(source))
                {
                    if (!File.Exists(HD2DSampleBuilder.AtlasPath(key))) throw new FileNotFoundException("Missing source sheet and atlas for " + key, source);
                    continue;   // keep the committed atlas
                }
                Directory.CreateDirectory(Path.GetDirectoryName(HD2DSampleBuilder.AtlasPath(key)));
                var settings = PixelSpriteConverter.Settings.Default;
                settings.FrameSize = HD2DSampleBuilder.FrameSize;
                settings.FigureHeight = figureHeight;
                PixelSpriteConverter.ConvertSheet(source, HD2DSampleBuilder.AtlasPath(key), settings);
            }
        }

        static void BuildScene(BattleSpec battle, Data.Battle.BattleCatalog catalog, Texture2D[] icons, PanelSettings panel)
        {
            var scene = EditorSceneManager.OpenScene(battle.BaseScene, OpenSceneMode.Single);
            var units = GameObject.Find("Units").transform;
            var ringMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Battle/ZhuojunMarket/ActiveRing.mat");
            var targetMat = TargetRingMaterial(ringMat);
            foreach (var old in units.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Active ring").ToArray())
                Object.DestroyImmediate(old.gameObject);

            string[] heroes = { "Liu Bei", "Guan Yu", "Zhang Fei" };
            string[] busts = { "LiuBei", "GuanYu", "ZhangFei" };
            var playerViews = heroes.Select((n, i) =>
            {
                var v = View(units.Find(n), PlayerFeet[i], ringMat, targetMat, idle: 2, flipAttack: true);
                v.Portrait = LoadPortrait(busts[i]);
                v.PortraitCrop = new RectInt(0, 0, 256, 256);
                return v;
            }).ToArray();
            var enemyViews = Enumerable.Range(1, 4).Select(i =>
            {
                var unit = units.Find("Yellow Turban " + i);
                if (i <= battle.EnemySprites.Length && battle.EnemySprites[i - 1] != "YellowTurban")
                {
                    string key = battle.EnemySprites[i - 1];
                    unit.name = key;
                    var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(HD2DSampleBuilder.AtlasPath(key));
                    unit.GetComponentInChildren<SpriteFrameAnimator>(true).Target.sharedMaterial = HD2DSampleBuilder.PixelMaterial(key, atlas);
                }
                return View(unit, EnemyFeet[i - 1], ringMat, targetMat, idle: 1, flipAttack: false);
            }).ToArray();

            var controller = new GameObject("Battle Controller").AddComponent<BattleController>();
            controller.Catalog = catalog;
            controller.EncounterId = battle.EncounterId;
            controller.NextScene = battle.Next;
            controller.StartExp = battle.StartExp;
            controller.StartCopper = battle.StartCopper;
            controller.PlayerViews = playerViews;
            controller.EnemyViews = enemyViews;
            controller.QuitAction = ProjectSetup.Action("Menu");

            var hudGo = new GameObject("Battle HUD");
            var doc = hudGo.AddComponent<UIDocument>();
            doc.panelSettings = panel ? panel : AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (!doc.panelSettings) throw new System.InvalidOperationException("Battle HUD has no PanelSettings");
            var hud = hudGo.AddComponent<BattleHud>();
            hud.Controller = controller;
            hud.BattleCamera = Camera.main;
            hud.TroopIcons = icons;
            hud.LocationName = battle.Location;
            hud.Skin = AssetDatabase.LoadAssetAtPath<ThreeKingdoms.UI.Battle.BattleUiSkin>(BattleUiSkinBuilder.SkinPath);

            var tutor = controller.gameObject.AddComponent<ThreeKingdoms.Gameplay.Battle.BattleTutor>();
            tutor.Controller = controller;
            hud.Tutor = tutor;

            // Pre-battle "配装" reuses the shop panel in outfit-only mode, above the battle HUD.
            var outfitGo = new GameObject("Outfit HUD");
            var outfitDoc = outfitGo.AddComponent<UIDocument>();
            outfitDoc.panelSettings = doc.panelSettings;
            outfitDoc.sortingOrder = 10;
            var outfitHud = outfitGo.AddComponent<ThreeKingdoms.UI.Shop.ShopHud>(); outfitHud.Host = controller; outfitHud.Skin = hud.Skin;

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();

            EditorSceneManager.SaveScene(scene, battle.Path);
        }

        public static void BuildFromCommandLine()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static Material TargetRingMaterial(Material ring)
        {
            const string path = "Assets/_Project/Art/Battle/ZhuojunMarket/TargetRing.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(ring); AssetDatabase.CreateAsset(m, path); }
            m.CopyPropertiesFromMaterial(ring);
            m.SetColor("_BaseColor", new Color(2.2f, 0.55f, 0.4f, 1f));   // warm red, distinct from the gold actor ring
            EditorUtility.SetDirty(m);
            return m;
        }

        static GameObject Ring(Transform unit, string name, Material material, float scale)
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ring.name = name;
            Object.DestroyImmediate(ring.GetComponent<Collider>());
            ring.transform.SetParent(unit, false);
            ring.transform.localPosition = new Vector3(0, 0.02f, 0.04f);
            ring.transform.localScale = new Vector3(1.6f * scale, 0.6f * scale, 1f);
            var r = ring.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.SetActive(false);
            return ring;
        }

        static BattleUnitView View(Transform unit, Vector2 feet, Material ringMat, Material targetMat, int idle, bool flipAttack)
        {
            unit.localPosition = new Vector3(feet.x, feet.y, 10f + feet.y * 0.01f);
            var view = unit.gameObject.AddComponent<BattleUnitView>();
            view.Sprite = unit.GetComponentInChildren<SpriteFrameAnimator>(true);
            view.IdleFrame = idle;
            view.FlipAttack = flipAttack;
            view.SelectionRing = Ring(unit, "Selection ring", ringMat, 1f);
            view.TargetMarker = Ring(unit, "Target marker", targetMat, 1.25f);
            return view;
        }

        static Texture2D LoadPortrait(string key)
        {
            string path = $"{PortraitRoot}/{key}_Bust.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer && (importer.mipmapEnabled || !importer.alphaIsTransparency || importer.textureType != TextureImporterType.Default))
            {
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp; importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static PanelSettings BuildPanelSettings()
        {
            if (!File.Exists(ThemePath))
            {
                File.WriteAllText(ThemePath, "@import url(\"unity-theme://default\");\n");
                AssetDatabase.ImportAsset(ThemePath);
            }
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (!panel) { panel = ScriptableObject.CreateInstance<PanelSettings>(); AssetDatabase.CreateAsset(panel, PanelSettingsPath); }
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            EditorUtility.SetDirty(panel);
            return panel;
        }

        // Placeholder 16×16 troop-type badges (gold on a dark disc), drawn at 2×. Order matches TroopType.
        static readonly string[][] IconArt =
        {
            new[] // infantry: sword and shield
            {
                "................", ".##.............", ".###............", "..###...........", "...###..........", "....###.######..",
                ".....###########", "......##########", ".....###########", "....#.##########", "...#...#########", "........#######.",
                "........#######.", ".........#####..", "..........###...", "...........#....",
            },
            new[] // cavalry: horse head
            {
                "................", ".......#.#......", "......######....", ".....########...", "....##########..", "...###########..",
                "..####.#######..", ".#####..######..", ".###.....#####..", "..#......######.", "........#######.", ".......########.",
                ".......########.", "......#########.", "......#########.", "................",
            },
            new[] // archer: bow and arrow
            {
                "................", "..........##....", "........##.#....", ".......#...#....", "......#....#....", ".....#.....#....",
                "....#......#....", "...#.......#.##.", ".############.##", "...#.......#.##.", "....#......#....", ".....#.....#....",
                "......#....#....", ".......#...#....", "........##.#....", "..........##....",
            },
        };

        static Texture2D[] BuildTroopIcons()
        {
            Directory.CreateDirectory(UiRoot);
            string[] names = { "TroopIcon_Infantry", "TroopIcon_Cavalry", "TroopIcon_Archer" };
            var result = new Texture2D[5];
            var gold = new Color32(236, 194, 92, 255);
            var disc = new Color32(22, 26, 38, 235);
            var rim = new Color32(158, 128, 72, 255);
            for (int i = 0; i < names.Length; i++)
            {
                const int s = 24, scale = 2;   // 16×16 art centred in a 24×24 badge, then ×2
                var tex = new Texture2D(s * scale, s * scale, TextureFormat.RGBA32, false);
                for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = x + 0.5f - s / 2f, dy = y + 0.5f - s / 2f, d = Mathf.Sqrt(dx * dx + dy * dy);
                    Color32 c = d <= s / 2f - 0.5f ? (d >= s / 2f - 1.7f ? rim : disc) : new Color32(0, 0, 0, 0);
                    int ax = x - 4, ay = 15 - (y - 4);
                    if (ax >= 0 && ax < 16 && ay >= 0 && ay < 16 && IconArt[i][ay][ax] == '#') c = gold;
                    for (int py = 0; py < scale; py++) for (int px = 0; px < scale; px++) tex.SetPixel(x * scale + px, y * scale + py, c);
                }
                tex.Apply();
                string path = $"{UiRoot}/{names[i]}.png";
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed; importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
                result[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            return result;
        }
    }
}
