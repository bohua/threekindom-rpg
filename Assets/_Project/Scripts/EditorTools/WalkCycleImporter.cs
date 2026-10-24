using System.IO;
using UnityEditor;
using UnityEngine;

namespace ThreeKingdoms.EditorTools
{
    // Appends the Codex walk cycles (CHARACTER_WALK_CYCLES.md: 4×4 sheets of 418-px cells, rows down/left/right/up,
    // columns left contact / passing / right contact / passing, one shared root at (209, 378)) to each character's
    // pixel atlas: frames 0–8 stay the 3×3 poses, frames 9–24 are the walk cycle (9 + row × 4 + column).
    public static class WalkCycleImporter
    {
        public const int PoseFrames = 9, WalkFrames = 16, OathFrames = 6;
        // The six peach-garden oath poses (user 2026-10-24), same 418-px cell contract, appended as frames 25–30.
        const string OathSource = @"D:\BuildArtifacts\Codex\hd2d-jrpg\art\story-cinematics";
        const string DefaultSource = @"D:\BuildArtifacts\Codex\hd2d-jrpg\art\walk-cycles";

        static readonly (string Key, string Sheet, int FigureHeight)[] Sheets =
        {
            ("LiuBei", "LIU_BEI_WALK_4X4.png", 44),
            ("GuanYu", "GUAN_YU_WALK_4X4.png", 47),
            ("ZhangFei", "ZHANG_FEI_WALK_4X4.png", 46),
            ("YellowTurban", "YELLOW_TURBAN_GENERIC_WALK_4X4.png", 43),
        };

        [MenuItem("JRPG/Import Walk Cycles into Character Atlases")]
        public static void Import()
        {
            string root = System.Environment.GetEnvironmentVariable("HD2D_WALK_SOURCE");
            if (string.IsNullOrEmpty(root)) root = DefaultSource;
            int size = HD2DSampleBuilder.FrameSize;
            foreach (var (key, sheet, figureHeight) in Sheets)
            {
                string atlasPath = HD2DSampleBuilder.AtlasPath(key);
                string source = Path.Combine(root, sheet);
                if (!File.Exists(source))
                {
                    Debug.Log($"Walk sheet for {key} not found; keeping the committed atlas.");
                    continue;
                }
                var poses = Load(atlasPath);
                if (poses.width < PoseFrames * size) throw new IOException($"{atlasPath} has fewer than {PoseFrames} pose frames");

                var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                src.LoadImage(File.ReadAllBytes(source));
                var settings = PixelSpriteConverter.Settings.Default;
                settings.Columns = 4; settings.Rows = 4; settings.FrameSize = size; settings.FigureHeight = figureHeight;
                settings.FixedRoot = new Vector2Int(src.width / 4 / 2, 378 * (src.height / 4) / 418);
                var walk = PixelSpriteConverter.BuildAtlas(src.GetPixels32(), src.width, src.height, settings, out int walkWidth, out _);
                Object.DestroyImmediate(src);

                // The oath poses for the three brothers, when delivered: one row of six cells.
                Color32[] oath = null; int oathWidth = 0;
                string oathSheet = Path.Combine(OathSource, sheet.Replace("_WALK_4X4", "_OATH_6"));
                if (File.Exists(oathSheet))
                {
                    var osrc = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    osrc.LoadImage(File.ReadAllBytes(oathSheet));
                    var os = PixelSpriteConverter.Settings.Default;
                    os.Columns = OathFrames; os.Rows = 1; os.FrameSize = size; os.FigureHeight = figureHeight;
                    os.FixedRoot = new Vector2Int(osrc.width / OathFrames / 2, 378 * osrc.height / 418);
                    oath = PixelSpriteConverter.BuildAtlas(osrc.GetPixels32(), osrc.width, osrc.height, os, out oathWidth, out _);
                    Object.DestroyImmediate(osrc);
                }

                // Frames 0–8 from the pose atlas (also on re-import, when it already carries walk frames), then 16 walk
                // frames, then the oath poses if any.
                int width = (PoseFrames + WalkFrames) * size + oathWidth;
                var combined = new Texture2D(width, size, TextureFormat.RGBA32, false);
                var clear = new Color32[width * size];
                combined.SetPixels32(clear);
                combined.SetPixels(0, 0, PoseFrames * size, size, poses.GetPixels(0, 0, PoseFrames * size, size));
                var walkTex = new Texture2D(walkWidth, size, TextureFormat.RGBA32, false);
                walkTex.SetPixels32(walk);
                combined.SetPixels(PoseFrames * size, 0, walkWidth, size, walkTex.GetPixels());
                if (oath != null)
                {
                    var oathTex = new Texture2D(oathWidth, size, TextureFormat.RGBA32, false);
                    oathTex.SetPixels32(oath);
                    combined.SetPixels((PoseFrames + WalkFrames) * size, 0, oathWidth, size, oathTex.GetPixels());
                    Object.DestroyImmediate(oathTex);
                }
                combined.Apply();
                File.WriteAllBytes(atlasPath, combined.EncodeToPNG());
                Object.DestroyImmediate(combined); Object.DestroyImmediate(walkTex); Object.DestroyImmediate(poses);
                AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceUpdate);
                PixelSpriteConverter.ConfigureImporter(atlasPath);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("WALK_CYCLES_IMPORTED");
        }

        static Texture2D Load(string path)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.LoadImage(File.ReadAllBytes(path));
            return t;
        }
    }
}
