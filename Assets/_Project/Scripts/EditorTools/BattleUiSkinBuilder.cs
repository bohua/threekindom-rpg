using System.IO;
using ThreeKingdoms.UI.Battle;
using UnityEditor;
using UnityEngine;

namespace ThreeKingdoms.EditorTools
{
    // Copies the Codex battle UI skin (BATTLE_UI_SKIN.md, art/battle-ui) into the project and fills the
    // BattleUiSkin asset the HUD reads. Point filtering, no mipmaps, no compression: the pixels are drawn at 2×.
    public static class BattleUiSkinBuilder
    {
        const string DefaultSource = @"D:\BuildArtifacts\Codex\hd2d-jrpg\art\battle-ui";
        public const string Dir = "Assets/_Project/Art/UI/Battle";
        public const string SkinPath = Dir + "/BattleUiSkin.asset";

        static readonly string[] Files =
        {
            "ui_command_panel", "ui_plaque_panel", "ui_hint_panel",
            "ui_command_item_normal", "ui_command_item_selected", "ui_command_item_disabled",
            "ui_card_normal", "ui_card_active", "ui_card_active_glow", "ui_card_wounded", "ui_card_routed", "ui_card_routed_seal",
            "ui_portrait_frame", "ui_portrait_frame_active", "ui_unit_badge_frame",
            "ui_bar_frame", "ui_bar_fill_troops", "ui_bar_fill_troops_low", "ui_bar_fill_skill",
        };

        [MenuItem("JRPG/Build Battle UI Skin")]
        public static BattleUiSkin Build()
        {
            string root = System.Environment.GetEnvironmentVariable("HD2D_BATTLE_UI_SOURCE");
            if (string.IsNullOrEmpty(root)) root = DefaultSource;
            Directory.CreateDirectory(Dir);
            foreach (var f in Files)
            {
                string source = Path.Combine(root, f + ".png"), target = $"{Dir}/{f}.png";
                if (File.Exists(source)) File.Copy(source, target, true);
                else if (!File.Exists(target)) { Debug.Log($"Battle UI texture {f} not found; the HUD keeps its flat style."); return null; }
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceUpdate);
                var importer = (TextureImporter)AssetImporter.GetAtPath(target);
                importer.textureType = TextureImporterType.Default;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Repeat;   // bar fills tile; nine-slices ignore it
                importer.SaveAndReimport();
            }

            Texture2D T(string f) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{Dir}/{f}.png");
            var skin = AssetDatabase.LoadAssetAtPath<BattleUiSkin>(SkinPath);
            bool created = !skin;
            if (created) skin = ScriptableObject.CreateInstance<BattleUiSkin>();
            skin.CommandPanel = T("ui_command_panel"); skin.PlaquePanel = T("ui_plaque_panel"); skin.HintPanel = T("ui_hint_panel");
            skin.ItemNormal = T("ui_command_item_normal"); skin.ItemSelected = T("ui_command_item_selected"); skin.ItemDisabled = T("ui_command_item_disabled");
            skin.CardNormal = T("ui_card_normal"); skin.CardActive = T("ui_card_active"); skin.CardActiveGlow = T("ui_card_active_glow");
            skin.CardWounded = T("ui_card_wounded"); skin.CardRouted = T("ui_card_routed"); skin.RoutedSeal = T("ui_card_routed_seal");
            skin.PortraitFrame = T("ui_portrait_frame"); skin.PortraitFrameActive = T("ui_portrait_frame_active"); skin.UnitBadge = T("ui_unit_badge_frame");
            skin.BarFrame = T("ui_bar_frame"); skin.BarTroops = T("ui_bar_fill_troops"); skin.BarTroopsLow = T("ui_bar_fill_troops_low"); skin.BarSkill = T("ui_bar_fill_skill");
            if (created) AssetDatabase.CreateAsset(skin, SkinPath);
            EditorUtility.SetDirty(skin);
            AssetDatabase.SaveAssets();
            return skin;
        }
    }
}
