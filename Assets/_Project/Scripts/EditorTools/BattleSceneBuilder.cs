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

        [MenuItem("JRPG/Build Battle Tutorial 01 (playable)")]
        public static void Build()
        {
            var catalog = BattleDataBuilder.Build();
            var icons = BuildTroopIcons();
            var panel = BuildPanelSettings();

            if (!File.Exists(BattleBackdropBaker.ScenePath)) BattleBackdropBaker.Build();
            var scene = EditorSceneManager.OpenScene(BattleBackdropBaker.ScenePath, OpenSceneMode.Single);
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
            var enemyViews = Enumerable.Range(1, 4).Select(i => View(units.Find("Yellow Turban " + i), EnemyFeet[i - 1], ringMat, targetMat, idle: 1, flipAttack: false)).ToArray();

            var controller = new GameObject("Battle Controller").AddComponent<BattleController>();
            controller.Catalog = catalog;
            controller.EncounterId = "battle_1";
            controller.PlayerViews = playerViews;
            controller.EnemyViews = enemyViews;
            controller.QuitAction = ProjectSetup.Action("Menu");

            var hudGo = new GameObject("Battle HUD");
            var doc = hudGo.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            var hud = hudGo.AddComponent<BattleHud>();
            hud.Controller = controller;
            hud.BattleCamera = Camera.main;
            hud.TroopIcons = icons;

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            // First in the list = what a player build opens without arguments (user 2026-10-19: start in battle 1).
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("BATTLE_TUTORIAL_BUILT");
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
