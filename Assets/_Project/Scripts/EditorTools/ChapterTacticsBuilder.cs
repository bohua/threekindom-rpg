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
        static readonly string[] CharKeys = { "ZhangBao", "ZhangLiang", "NpcDongZhuo", "NpcBandit", "NpcBanditChief", "NpcYellowGuard", "YellowTurban" };

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
            var ids = new[] { "battle_4", "battle_5", "battle_6" };
            for (int i = 0; i < ids.Length; i++)
            {
                var game = BingshuTacticsBuilder.CreateScene();
                game.CharMats = CharKeys.Select(k => AssetDatabase.LoadAssetAtPath<Material>($"{Chars}/{k}/{k}_Pixel.mat")).Where(m => m).ToList();
                game.HdGround = ground;
                // Daylight battles take the bright noon grade (user 2026-10-25); the night camp keeps the default HD-2D look.
                if (ids[i] != "battle_4") game.HD2DProfile = ChapterFieldBuilder.NoonProfile(ChapterFieldBuilder.NoonBattlePath, depthOfField: false);
                var bridge = new GameObject("Chapter Tactics").AddComponent<ChapterTactics>();
                bridge.Catalog = catalog;
                bridge.EncounterId = ids[i];
                EditorUtility.SetDirty(game);
                EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), BattleSceneBuilder.ChapterBattlePaths[i]);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("CHAPTER_TACTICS_BUILT ground=" + ground.Length);
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
