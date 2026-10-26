using System.IO;
using UnityEditor;
using UnityEngine;

namespace ThreeKingdoms.EditorTools
{
    // Converts the Codex mounted sheets (art/mounted-heroes: 5×4 grid of 768-px cells, rows down/left/right/up gallop
    // and the mount sequence facing right, shared root at (384, 720)) into 96-px pixel atlases, one row of 20 frames,
    // at the same pixel scale as the walk cycles (a rider 361 source px tall becomes 44 px).
    public static class MountedImporter
    {
        public const int FrameSize = 96, Columns = 4, Rows = 5;
        const float WalkScale = 361f / 44f;   // source pixels per atlas pixel in the walk sheets
        const string DefaultSource = @"D:\BuildArtifacts\Codex\hd2d-jrpg\art\mounted-heroes";

        public static readonly (string Key, string Sheet)[] Sheets =
        {
            ("LiuBei", "LIU_BEI_MOUNTED_5X4.png"), ("GuanYu", "GUAN_YU_MOUNTED_5X4.png"), ("ZhangFei", "ZHANG_FEI_MOUNTED_5X4.png"),
        };

        public static string AtlasPath(string key) => $"Assets/_Project/Art/Sprites/Characters/{key}/{key}_Mounted.png";

        [MenuItem("JRPG/Import Mounted Sheets")]
        public static void Import()
        {
            foreach (var (key, sheet) in Sheets)
            {
                string source = Path.Combine(DefaultSource, sheet);
                if (!File.Exists(source)) { Debug.Log($"Mounted sheet for {key} not found; riding uses the walk sprite."); continue; }
                var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                src.LoadImage(File.ReadAllBytes(source));
                int cellW = src.width / Columns, cellH = src.height / Rows;
                // Frame 0 is the top-left cell (gallop facing down); its opaque height sets the scale.
                var px = src.GetPixels32();
                int top = -1, bottom = -1;
                for (int y = src.height - cellH; y < src.height; y++)
                    for (int x = 0; x < cellW; x++)
                        if (px[y * src.width + x].a > 127) { if (bottom < 0) bottom = y; top = y; break; }
                int figure = Mathf.Max(8, Mathf.RoundToInt((top - bottom + 1) / WalkScale));
                var settings = PixelSpriteConverter.Settings.Default;
                settings.Columns = Columns; settings.Rows = Rows; settings.FrameSize = FrameSize; settings.FigureHeight = figure;
                settings.FixedRoot = new Vector2Int(cellW / 2, 720 * cellH / 768);
                var atlas = PixelSpriteConverter.BuildAtlas(px, src.width, src.height, settings, out int w, out int h);
                Object.DestroyImmediate(src);
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.SetPixels32(atlas);
                tex.Apply();
                string path = AtlasPath(key);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                PixelSpriteConverter.ConfigureImporter(path);
                Debug.Log($"Mounted atlas {key}: figure {figure} px, {w}x{h}");
            }
            AssetDatabase.SaveAssets();
        }
    }
}
