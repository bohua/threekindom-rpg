using System.IO;
using System.Linq;
using ThreeKingdoms.Gameplay.Battle;
using UnityEditor;
using UnityEngine;

namespace ThreeKingdoms.EditorTools
{
    // Copies the 2026-10-22 Codex deliveries into the project (user 2026-10-22): the gambling house and street props
    // (art/zhuojun-casino, building and prop textures join the town library, the dice UI goes to UI/Casino), the archery
    // range (art/archery-minigame → UI/Archery) and the battle effect sheets (art/battle-vfx → Battle/Vfx). Pixel
    // import: point filtering, no mipmaps, no compression. A missing source keeps whatever is already in the project.
    public static class CodexArtImporter
    {
        const string Root = @"D:\BuildArtifacts\Codex\hd2d-jrpg\art";
        public const string CasinoUiDir = "Assets/_Project/Art/UI/Casino";
        public const string ArcheryDir = "Assets/_Project/Art/UI/Archery";
        public const string VfxDir = "Assets/_Project/Art/Battle/Vfx/Sheets";

        static readonly string[] CasinoTown =
        {
            "casino_front", "casino_side", "casino_back", "plaque_casino", "prop_banner_casino", "prop_dice_table",
            "prop_recruit_desk", "prop_wash_basin", "prop_juggle_props", "prop_beggar_bowl", "prop_bookstand", "prop_hoe_field",
        };
        static readonly string[] CasinoUi =
        {
            "dice_1", "dice_2", "dice_3", "dice_4", "dice_5", "dice_6", "dice_cup", "dice_cup_lifted", "dice_mat",
            "bet_big", "bet_small", "bet_big_selected", "bet_small_selected", "coin_stack",
        };
        public static readonly string[] Archery =
        {
            "range_bg_sky", "range_bg_far", "range_ground", "range_front", "archer_liubei_aim", "bow_arm_overlay", "arrow",
            "arrow_stuck", "target_straw", "target_straw_hit", "target_post_tall", "target_post_short",
            "hit_burst_1", "hit_burst_2", "hit_burst_3", "hit_burst_4", "gauge_angle", "gauge_needle",
            "gauge_power_frame", "gauge_power_fill", "arrow_icon", "arrow_icon_used",
        };

        // Building and prop textures go into the town texture folder before the town is built (ZhuojunTownBuilder
        // configures everything there).
        [MenuItem("JRPG/Import Codex casino, archery and battle effects")]
        public static void Import()
        {
            foreach (var f in CasinoTown) Copy(Path.Combine(Root, "zhuojun-casino"), f, ZhuojunTownBuilder.TownDir, configure: false);
            foreach (var f in CasinoUi) Copy(Path.Combine(Root, "zhuojun-casino"), f, CasinoUiDir, configure: true);
            foreach (var f in Archery) Copy(Path.Combine(Root, "archery-minigame"), f, ArcheryDir, configure: true);
            AssetDatabase.SaveAssets();
        }

        public static Texture2D Load(string dir, string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{name}.png");

        [System.Serializable] sealed class VfxFile { public string filename; public int frames; public float fps; public bool additive; }
        [System.Serializable] sealed class VfxManifest { public VfxFile[] files; }

        // The effect sheets with their frame counts and blending, from the delivery's manifest.
        public static BattleVfx.Sheet[] VfxSheets()
        {
            string source = Path.Combine(Root, "battle-vfx");
            string manifestPath = Path.Combine(source, "manifest.json");
            string copied = $"{VfxDir}/manifest.json";
            Directory.CreateDirectory(VfxDir);
            if (File.Exists(manifestPath)) File.Copy(manifestPath, copied, true);
            if (!File.Exists(copied)) return new BattleVfx.Sheet[0];
            var manifest = JsonUtility.FromJson<VfxManifest>(File.ReadAllText(copied));
            return manifest.files.Where(f => f.filename.StartsWith("vfx_")).Select(f =>
            {
                string name = Path.GetFileNameWithoutExtension(f.filename);
                Copy(source, name, VfxDir, configure: true);
                return new BattleVfx.Sheet
                {
                    Key = name.Substring(4), Texture = Load(VfxDir, name), Frames = Mathf.Max(1, f.frames), Fps = f.fps > 0 ? f.fps : 12f, Additive = f.additive,
                };
            }).Where(x => x.Texture).ToArray();
        }

        static void Copy(string sourceDir, string name, string targetDir, bool configure)
        {
            Directory.CreateDirectory(targetDir);
            string source = Path.Combine(sourceDir, name + ".png"), target = $"{targetDir}/{name}.png";
            if (File.Exists(source)) File.Copy(source, target, true);
            if (!File.Exists(target)) { Debug.Log($"Codex texture {name} not found; keeping the fallback."); return; }
            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceUpdate);
            if (!configure) return;
            var importer = (TextureImporter)AssetImporter.GetAtPath(target);
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 4096;
            importer.SaveAndReimport();
        }
    }
}
