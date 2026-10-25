using System.IO;
using ThreeKingdoms.UI.Story;
using UnityEditor;
using UnityEngine;

namespace ThreeKingdoms.EditorTools
{
    // Copies the full-body illustrations of the speakers that have one (user 2026-10-22) into the project and lists them
    // in one DialoguePortraitSet for the town and battle dialogue: the three heroes' main poses
    // (four-heroes-pose-redesign), the castle steward, who is N03 at the yamen forecourt, and the Yellow Turban leaders
    // (INITIAL_SCENARIO_ART.md). Mipmapped and filtered: they are painted, not pixel art.
    public static class DialoguePortraitImporter
    {
        const string Dir = "Assets/_Project/Art/Portraits/Full";
        public const string SetPath = Dir + "/DialoguePortraits.asset";
        const string Heroes = @"D:\BuildArtifacts\Codex\hd2d-jrpg\four-heroes-pose-redesign-20261018-v1";
        const string Scenario = @"D:\BuildArtifacts\Codex\hd2d-jrpg\initial-scenario-enemy-art-20261018-v1";

        static readonly (string speaker, string source, string file, bool right, bool flip, float crop, float axis)[] Entries =
        {
            // Liu Bei's pose faces left; mirrored so he looks in toward the box from the left.
            ("liu_bei", Heroes, "LIU_BEI_MAIN_POSE", false, true, 0.6f, 0.52f),
            ("guan_yu", Heroes, "GUAN_YU_MAIN_POSE", true, false, 0.6f, 0.42f),
            ("zhang_fei", Heroes, "ZHANG_FEI_MAIN_POSE", true, false, 0.6f, 0.5f),
            ("npc.N03", Scenario, "CASTLE_STEWARD_PORTRAIT", true, false, 0.62f, 0.45f),
            // The leaders stand at the right; those drawn facing right are mirrored to face Liu Bei.
            ("cheng_yuanzhi", Scenario, "CHENG_YUANZHI_PORTRAIT", true, true, 0.6f, 0.45f),
            ("zhang_liang", Scenario, "ZHANG_LIANG_PORTRAIT", true, true, 0.6f, 0.5f),
            ("zhang_bao", Scenario, "ZHANG_BAO_PORTRAIT", true, false, 0.6f, 0.5f),
            ("zhang_jiao", Scenario, "ZHANG_JIAO_PORTRAIT", true, false, 0.6f, 0.5f),
            // 0.2 黄巾篇 (Codex art/v02-portraits); missing ones talk without a portrait until delivered. Zou Jing and the
            // county people the brothers meet stand at the right.
            ("lu_zhi", V02, "LU_ZHI_PORTRAIT", true, false, 0.6f, 0.5f),
            ("cao_cao", V02, "CAO_CAO_PORTRAIT", true, false, 0.6f, 0.5f),
            ("dong_zhuo", V02, "DONG_ZHUO_PORTRAIT", true, false, 0.6f, 0.5f),
            ("zou_jing", V02, "ZOU_JING_PORTRAIT", true, false, 0.6f, 0.5f),
            ("sun_jian", V02, "SUN_JIAN_PORTRAIT", true, false, 0.6f, 0.5f),
            ("du_you", V02, "DU_YOU_PORTRAIT", true, false, 0.6f, 0.5f),
            ("bandit_chief", V02, "BANDIT_CHIEF_PORTRAIT", true, false, 0.6f, 0.5f),
            ("clerk", Scenario, "CASTLE_STEWARD_PORTRAIT", true, false, 0.62f, 0.45f),
            // 0.3 讨董篇 (Codex art/v03-portraits after the approved design art/v03-characters-concept); Lü Bu from the
            // tactics prototype, Tao Qian his own main portrait.
            ("gongsun_zan", V03, "GONGSUN_ZAN_PORTRAIT", true, false, 0.6f, 0.5f),
            ("jian_yong", V03, "JIAN_YONG_PORTRAIT", true, false, 0.6f, 0.5f),
            ("tian_yu", V03, "TIAN_YU_PORTRAIT", true, false, 0.6f, 0.5f),
            ("yuan_shao", V03, "YUAN_SHAO_PORTRAIT", true, false, 0.6f, 0.5f),
            ("yuan_shu", V03, "YUAN_SHU_PORTRAIT", true, false, 0.6f, 0.5f),
            ("kong_rong", V03, "KONG_RONG_PORTRAIT", true, false, 0.6f, 0.5f),
            ("hua_xiong", V03, "HUA_XIONG_PORTRAIT", true, false, 0.6f, 0.5f),
            ("chen_gong", V03, "CHEN_GONG_PORTRAIT", true, false, 0.6f, 0.5f),
            ("lu_bu", @"D:\BuildArtifacts\Codex\hd2d-jrpg\art\battle-poc-8dir-v3", "char_lubu_mainpose", true, false, 0.55f, 0.5f),
            // 0.4 (Codex art/v04-portraits); Zhao Yun's main portrait from the four heroes.
            ("zhao_yun", Heroes, "ZHAO_YUN_MAIN_POSE", true, false, 0.6f, 0.5f),
            ("wen_chou", V04, "WEN_CHOU_PORTRAIT", true, false, 0.6f, 0.5f),
            ("qu_yi", V04, "QU_YI_PORTRAIT", true, false, 0.6f, 0.5f),
            ("tian_kai", V04, "TIAN_KAI_PORTRAIT", true, false, 0.6f, 0.5f),
            ("assassin", V04, "ASSASSIN_PORTRAIT", true, false, 0.6f, 0.5f),
            ("tao_qian", @"D:\BuildArtifacts\Codex\hd2d-jrpg\art\tao-qian-portrait", "TAO_QIAN_MAIN_PORTRAIT", true, false, 0.6f, 0.5f),
        };
        const string V04 = @"D:\BuildArtifacts\Codex\hd2d-jrpg\art\v04-portraits";
        const string V03 = @"D:\BuildArtifacts\Codex\hd2d-jrpg\art\v03-portraits";
        const string V02 = @"D:\BuildArtifacts\Codex\hd2d-jrpg\art\v02-portraits";

        public static DialoguePortraitSet Import()
        {
            Directory.CreateDirectory(Dir);
            var list = new System.Collections.Generic.List<DialoguePortraitSet.Entry>();
            foreach (var e in Entries)
            {
                string target = $"{Dir}/{e.file}.png", source = Path.Combine(e.source, e.file + ".png");
                if (File.Exists(source)) File.Copy(source, target, true);
                if (!File.Exists(target)) { Debug.Log($"Dialogue portrait {e.file} not found; {e.speaker} talks without one."); continue; }
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceUpdate);
                var importer = (TextureImporter)AssetImporter.GetAtPath(target);
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
                list.Add(new DialoguePortraitSet.Entry
                {
                    Speaker = e.speaker, Texture = AssetDatabase.LoadAssetAtPath<Texture2D>(target),
                    Right = e.right, Flip = e.flip, Crop = e.crop, Axis = e.axis,
                });
            }
            var set = AssetDatabase.LoadAssetAtPath<DialoguePortraitSet>(SetPath);
            bool created = !set;
            if (created) set = ScriptableObject.CreateInstance<DialoguePortraitSet>();
            set.Entries = list.ToArray();
            if (created) AssetDatabase.CreateAsset(set, SetPath);
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            return set;
        }
    }
}
