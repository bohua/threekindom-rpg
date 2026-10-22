using System.IO;
using ThreeKingdoms.UI.Story;
using UnityEditor;
using UnityEngine;

namespace ThreeKingdoms.EditorTools
{
    // Copies the full-body illustrations of the speakers that have one (user 2026-10-22) into the project and lists them
    // for the town dialogue: the three heroes' main poses (four-heroes-pose-redesign) and the castle steward, who is
    // N03 at the yamen forecourt (INITIAL_SCENARIO_ART.md). Mipmapped and filtered: they are painted, not pixel art.
    public static class DialoguePortraitImporter
    {
        const string Dir = "Assets/_Project/Art/Portraits/Full";
        const string Heroes = @"D:\BuildArtifacts\Codex\hd2d-jrpg\four-heroes-pose-redesign-20261018-v1";
        const string Scenario = @"D:\BuildArtifacts\Codex\hd2d-jrpg\initial-scenario-enemy-art-20261018-v1";

        static readonly (string speaker, string source, string file, bool right, bool flip, float crop, float axis)[] Entries =
        {
            // Liu Bei's pose faces left; mirrored so he looks in toward the box from the left.
            ("liu_bei", Heroes, "LIU_BEI_MAIN_POSE", false, true, 0.6f, 0.52f),
            ("guan_yu", Heroes, "GUAN_YU_MAIN_POSE", true, false, 0.6f, 0.42f),
            ("zhang_fei", Heroes, "ZHANG_FEI_MAIN_POSE", true, false, 0.6f, 0.5f),
            ("npc.N03", Scenario, "CASTLE_STEWARD_PORTRAIT", true, false, 0.62f, 0.45f),
        };

        public static TownHud.DialoguePortrait[] Import()
        {
            Directory.CreateDirectory(Dir);
            var list = new System.Collections.Generic.List<TownHud.DialoguePortrait>();
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
                list.Add(new TownHud.DialoguePortrait
                {
                    Speaker = e.speaker, Texture = AssetDatabase.LoadAssetAtPath<Texture2D>(target),
                    Right = e.right, Flip = e.flip, Crop = e.crop, Axis = e.axis,
                });
            }
            return list.ToArray();
        }
    }
}
