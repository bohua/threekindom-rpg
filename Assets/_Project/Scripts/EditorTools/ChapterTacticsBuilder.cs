using System.IO;
using System.Linq;
using Prototype.BingshuTactics;
using Prototype.BingshuTactics.EditorTools;
using ThreeKingdoms.Data.Battle;
using ThreeKingdoms.UI.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ThreeKingdoms.EditorTools
{
    // 0.2 battles 4–6 as tactics-map scenes (user 2026-10-25, ADR-0010). Same scene names as before, so the story flow,
    // saves ("battle_4" location) and GameRun.SceneFor are unchanged. Each scene: the tactics prototype's camera and
    // TacticsGame with its art, the 0.2 character sheets, the HD ground tiles once Codex delivers them, and the
    // ChapterTactics bridge that fills the stage from the play-through.
    public static class ChapterTacticsBuilder
    {
        const string Chars = "Assets/_Project/Art/Sprites/Characters";
        const string GroundDir = "Assets/_Project/Art/Chapter2/Tactics";
        const string DefaultGroundSource = @"D:\BuildArtifacts\Codex\hd2d-jrpg\art\v02-tactics-ground";
        static readonly string[] CharKeys = { "ZhangBao", "ZhangLiang", "NpcDongZhuo", "NpcBandit", "NpcBanditChief", "NpcYellowGuard", "YellowTurban", "NpcCaoCao", "NpcHanSoldier",
            "NpcBaimaRider", "NpcZhaoYun", "NpcGongsunZan", "NpcWenChou", "NpcQuYi", "NpcXiandeng", "NpcCrossbow",
            "NpcTaishiCi", "NpcGuanHai", "NpcYtRemnant", "NpcCaoBao", "NpcYuJin", "NpcCaoRen", "NpcCaoInfantry", "NpcCaoCavalry", "NpcDanyang" };   // 0.5   // 0.4   // 0.3: the White Horse riders of the drill (Lü Bu and the Bingzhou riders use the prototype's own sheets)
        // 0.3 battles 7–8 (V03_TAODONG_PLAN §4).
        public static readonly string[] Chapter3Paths = { "Assets/_Project/Scenes/Battle_Drill07.unity", "Assets/_Project/Scenes/Battle_Hulao08.unity" };
        // 0.4 battles 9–11 (V04_HEBEI_PLAN §4).
        public static readonly string[] Chapter4Paths =
            { "Assets/_Project/Scenes/Battle_Pingyuan09.unity", "Assets/_Project/Scenes/Battle_Panhe10.unity", "Assets/_Project/Scenes/Battle_Jieqiao11.unity" };
        // 0.5 battles 12–15 (V05_XUZHOU_PLAN §4).
        public static readonly string[] Chapter5Paths =
        {
            "Assets/_Project/Scenes/Battle_Duchang12.unity", "Assets/_Project/Scenes/Battle_XiapiEast13.unity",
            "Assets/_Project/Scenes/Battle_XiapiWalls14.unity", "Assets/_Project/Scenes/Battle_Xiaopei15.unity",
        };

        static string GroundSource
        {
            get
            {
                string env = System.Environment.GetEnvironmentVariable("HD2D_V02_TACTICS_GROUND");
                return string.IsNullOrEmpty(env) ? DefaultGroundSource : env;
            }
        }

        public static void BuildFromCommandLine()
        {
            try { BattleSceneBuilder.BuildChapterBattles(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void Build(BattleCatalog catalog)
        {
            var ground = ImportGround();
            EnsurePixelMaterials();
            var ids = new[] { "battle_4", "battle_5", "battle_6", "battle_7", "battle_8", "battle_9", "battle_10", "battle_11", "battle_12", "battle_13", "battle_14", "battle_15" };
            var paths = BattleSceneBuilder.ChapterBattlePaths.Concat(Chapter3Paths).Concat(Chapter4Paths).Concat(Chapter5Paths).ToArray();
            for (int i = 0; i < ids.Length; i++)
            {
                var game = BingshuTacticsBuilder.CreateScene();
                game.CharMats = game.CharMats.Concat(CharKeys.Select(k => AssetDatabase.LoadAssetAtPath<Material>($"{Chars}/{k}/{k}_Pixel.mat")).Where(m => m)).ToList();   // mounted Zhao Yun first
                game.HdGround = ground;
                // Daylight battles take the bright noon grade (user 2026-10-25); the night camp keeps the default HD-2D look.
                if (ids[i] != "battle_4") game.HD2DProfile = ChapterFieldBuilder.NoonProfile(ChapterFieldBuilder.NoonBattlePath, depthOfField: false);
                var bridge = new GameObject("Chapter Tactics").AddComponent<ChapterTactics>();
                bridge.Catalog = catalog;
                bridge.EncounterId = ids[i];
                bridge.Portraits = AssetDatabase.LoadAssetAtPath<ThreeKingdoms.UI.Story.DialoguePortraitSet>(DialoguePortraitImporter.SetPath);
                EditorUtility.SetDirty(game);
                EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), paths[i]);
            }
            var scenes = EditorBuildSettings.scenes.Where(s => !paths.Contains(s.path)).ToList();
            scenes.AddRange(paths.Select(p => new EditorBuildSettingsScene(p, true)));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("CHAPTER_TACTICS_BUILT ground=" + ground.Length);
        }

        // Characters that only had an atlas (no battle material yet, e.g. Cao Cao): a material like the Han soldier's, with
        // that atlas.
        static void EnsurePixelMaterials()
        {
            var template = AssetDatabase.LoadAssetAtPath<Material>($"{Chars}/NpcHanSoldier/NpcHanSoldier_Pixel.mat");
            if (!template) return;
            foreach (var k in CharKeys)
            {
                string matPath = $"{Chars}/{k}/{k}_Pixel.mat", atlas = $"{Chars}/{k}/{k}_Atlas.png";
                if (AssetDatabase.LoadAssetAtPath<Material>(matPath)) continue;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(atlas);
                if (!tex) continue;
                var m = new Material(template) { name = k + "_Pixel" };
                m.SetTexture("_BaseMap", tex); m.mainTexture = tex;
                AssetDatabase.CreateAsset(m, matPath);
            }
            AssetDatabase.SaveAssets();
        }

        // hd_grass, hd_road, hd_sand, hd_camp, hd_ford, hd_bridge …: high-resolution painterly tiles, one per 4 m (ZTile).
        static Texture2D[] ImportGround()
        {
            Directory.CreateDirectory(GroundDir);
            if (Directory.Exists(GroundSource))
                foreach (var f in Directory.GetFiles(GroundSource, "hd_*.png")) File.Copy(f, $"{GroundDir}/{Path.GetFileName(f)}", true);
            AssetDatabase.Refresh();
            var list = new System.Collections.Generic.List<Texture2D>();
            foreach (var path in Directory.GetFiles(GroundDir, "hd_*.png"))
            {
                string p = path.Replace('\\', '/');
                var imp = (TextureImporter)AssetImporter.GetAtPath(p);
                if (imp == null) continue;
                imp.textureType = TextureImporterType.Default; imp.mipmapEnabled = true; imp.filterMode = FilterMode.Trilinear;
                imp.wrapMode = TextureWrapMode.Repeat; imp.npotScale = TextureImporterNPOTScale.None; imp.maxTextureSize = 2048;
                imp.textureCompression = TextureImporterCompression.Uncompressed; imp.alphaIsTransparency = false;
                imp.isReadable = true;   // the tactics map bakes one blended ground texture from these at battle start
                imp.SaveAndReimport();
                list.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(p));
            }
            return list.ToArray();
        }
    }
}
