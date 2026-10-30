using System.IO;
using UnityEditor;
using UnityEngine;

namespace ThreeKingdoms.EditorTools
{
    // The 鞭督邮 story poses (user 2026-10-26: "做成游戏内Q版动画和对白…和桃园三结义一样"; Codex art/v02-duyou-anim): one
    // row of eight 418-px cells per character, root (209, 378), the same contract as the oath poses. The brothers' atlases
    // get them after the oath poses (frames 31–38, story pose 6–13); the inspector's after his nine poses (frames 9–16).
    // Idempotent: anything after those frames is cut off and rebuilt, so it can run after every walk / resident import.
    public static class DuyouPoseImporter
    {
        public const int Frames = 8;
        public const int HeroBase = WalkCycleImporter.PoseFrames + WalkCycleImporter.WalkFrames + WalkCycleImporter.OathFrames;   // 31
        public const int NpcBase = 9;
        const string DefaultSource = @"D:\BuildArtifacts\Codex\hd2d-jrpg\art\v02-duyou-anim";

        static readonly (string Key, string Sheet, int Keep, int FigureHeight)[] Sheets =
        {
            ("LiuBei", "LIU_BEI_DUYOU_8.png", HeroBase, 44),
            ("GuanYu", "GUAN_YU_DUYOU_8.png", HeroBase, 47),
            ("ZhangFei", "ZHANG_FEI_DUYOU_8.png", HeroBase, 46),
            ("NpcDuYou", "DU_YOU_DUYOU_8.png", NpcBase, 0),   // 0: scaled against Liu Bei's sheet
        };

        public static string Source
        {
            get
            {
                string env = System.Environment.GetEnvironmentVariable("HD2D_DUYOU_SOURCE");
                return string.IsNullOrEmpty(env) ? DefaultSource : env;
            }
        }

        [MenuItem("JRPG/Import 鞭督邮 Poses")]
        public static void Import()
        {
            int size = HD2DSampleBuilder.FrameSize;
            float liuHeight = Height(Path.Combine(Source, Sheets[0].Sheet));
            foreach (var (key, sheet, keep, figure) in Sheets)
            {
                string src = Path.Combine(Source, sheet), atlasPath = HD2DSampleBuilder.AtlasPath(key);
                if (!File.Exists(src) || !File.Exists(atlasPath)) { Debug.Log($"鞭督邮 poses for {key} not found; atlas unchanged."); continue; }
                int figureHeight = figure > 0 ? figure : Mathf.RoundToInt(44f * Height(src) / Mathf.Max(1f, liuHeight));

                var stex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                stex.LoadImage(File.ReadAllBytes(src));
                var s = PixelSpriteConverter.Settings.Default;
                s.Columns = Frames; s.Rows = 1; s.FrameSize = size; s.FigureHeight = Mathf.Max(8, figureHeight);
                s.FixedRoot = new Vector2Int(stex.width / Frames / 2, 378 * stex.height / 418);
                var poses = PixelSpriteConverter.BuildAtlas(stex.GetPixels32(), stex.width, stex.height, s, out int poseWidth, out _);
                Object.DestroyImmediate(stex);

                var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                atlas.LoadImage(File.ReadAllBytes(atlasPath));
                int keepWidth = keep * size;
                var combined = new Texture2D(keepWidth + poseWidth, size, TextureFormat.RGBA32, false);
                combined.SetPixels32(new Color32[combined.width * size]);
                int copy = Mathf.Min(keepWidth, atlas.width);   // an atlas without oath poses keeps empty frames there
                combined.SetPixels(0, 0, copy, size, atlas.GetPixels(0, 0, copy, size));
                var part = new Texture2D(poseWidth, size, TextureFormat.RGBA32, false);
                part.SetPixels32(poses);
                combined.SetPixels(keepWidth, 0, poseWidth, size, part.GetPixels());
                combined.Apply();
                File.WriteAllBytes(atlasPath, combined.EncodeToPNG());
                Object.DestroyImmediate(combined); Object.DestroyImmediate(part); Object.DestroyImmediate(atlas);
                AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceUpdate);
                PixelSpriteConverter.ConfigureImporter(atlasPath);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("DUYOU_POSES_IMPORTED");
        }

        // Opaque height of the first (standing) cell of a pose row, in source pixels.
        static float Height(string path)
        {
            if (!File.Exists(path)) return 0f;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.LoadImage(File.ReadAllBytes(path));
            int cell = t.width / Frames, top = -1, bottom = int.MaxValue;
            var px = t.GetPixels32();
            for (int y = 0; y < t.height; y++)
                for (int x = 0; x < cell; x++)
                    if (px[y * t.width + x].a >= 128) { top = Mathf.Max(top, y); bottom = Mathf.Min(bottom, y); }
            Object.DestroyImmediate(t);
            return top < 0 ? 0f : top - bottom + 1;
        }
    }
}
