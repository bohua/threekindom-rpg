using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ThreeKingdoms.EditorTools
{
    // ADR-0005 sprite pipeline, step 2: turns an AI-generated pose sheet (e.g. 3×3 cells of ~418 px with
    // soft edges and hundreds of thousands of colours) into a crisp pixel atlas: one FrameSize×FrameSize
    // frame per cell in a single row, feet on the second-lowest row, figure scaled so frame 0 is
    // FigureHeight pixels tall, block-mode colour sampling, binary alpha and per-channel quantisation.
    // The output is a starting point for manual clean-up in Aseprite, not final art.
    public static class PixelSpriteConverter
    {
        public struct Settings
        {
            public int Columns, Rows, FrameSize, FigureHeight, ChannelBits;
            public byte AlphaThreshold;

            public static Settings Default => new Settings
            {
                Columns = 3, Rows = 3, FrameSize = 48, FigureHeight = 44, ChannelBits = 5, AlphaThreshold = 128,
            };
        }

        public static void ConvertSheet(string sourcePng, string assetPath, Settings settings)
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!source.LoadImage(File.ReadAllBytes(sourcePng))) throw new IOException("Not an image: " + sourcePng);
            Color32[] pixels = source.GetPixels32();
            int width = source.width, height = source.height;
            Object.DestroyImmediate(source);

            Color32[] atlas = BuildAtlas(pixels, width, height, settings, out int atlasWidth, out int atlasHeight);
            var texture = new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGBA32, false);
            texture.SetPixels32(atlas);
            texture.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
            File.WriteAllBytes(assetPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            ConfigureImporter(assetPath);
        }

        public static void ConfigureImporter(string assetPath)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
        }

        // Pure pixel transform, kept separate from file IO so it can be unit-tested.
        // Texture rows are bottom-up (Unity convention); sheet rows are counted top-down.
        public static Color32[] BuildAtlas(Color32[] pixels, int width, int height, Settings s, out int atlasWidth, out int atlasHeight)
        {
            int cellW = width / s.Columns, cellH = height / s.Rows, frames = s.Columns * s.Rows, size = s.FrameSize;
            atlasWidth = frames * size;
            atlasHeight = size;
            var atlas = new Color32[atlasWidth * atlasHeight];

            RectInt first = OpaqueBounds(pixels, width, CellRect(0, s, cellW, cellH, height), s.AlphaThreshold);
            if (first.height <= 0) throw new System.ArgumentException("Frame 0 is empty; cannot derive the pixel scale.");
            float scale = first.height / (float)s.FigureHeight;
            var counts = new Dictionary<int, int>();

            for (int f = 0; f < frames; f++)
            {
                RectInt cell = CellRect(f, s, cellW, cellH, height);
                RectInt bounds = OpaqueBounds(pixels, width, cell, s.AlphaThreshold);
                if (bounds.height <= 0) continue;
                float centerX = OpaqueCentroidX(pixels, width, bounds, s.AlphaThreshold);
                for (int oy = 0; oy < size; oy++)
                for (int ox = 0; ox < size; ox++)
                {
                    int x0 = Mathf.FloorToInt(centerX + (ox - size / 2f) * scale), x1 = Mathf.FloorToInt(centerX + (ox + 1 - size / 2f) * scale);
                    int y0 = Mathf.FloorToInt(bounds.yMin + (oy - 1) * scale), y1 = Mathf.FloorToInt(bounds.yMin + oy * scale);
                    atlas[oy * atlasWidth + f * size + ox] = SampleBlock(pixels, width, cell, x0, Mathf.Max(x1, x0 + 1), y0, Mathf.Max(y1, y0 + 1), s, counts);
                }
            }
            return atlas;
        }

        static RectInt CellRect(int frame, Settings s, int cellW, int cellH, int height)
        {
            int column = frame % s.Columns, rowFromTop = frame / s.Columns;
            return new RectInt(column * cellW, height - (rowFromTop + 1) * cellH, cellW, cellH);
        }

        static RectInt OpaqueBounds(Color32[] px, int width, RectInt cell, byte threshold)
        {
            int xMin = int.MaxValue, yMin = int.MaxValue, xMax = -1, yMax = -1;
            for (int y = cell.yMin; y < cell.yMax; y++)
            for (int x = cell.xMin; x < cell.xMax; x++)
            {
                if (px[y * width + x].a < threshold) continue;
                if (x < xMin) xMin = x; if (x > xMax) xMax = x;
                if (y < yMin) yMin = y; if (y > yMax) yMax = y;
            }
            return xMax < 0 ? new RectInt(cell.xMin, cell.yMin, 0, 0) : new RectInt(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1);
        }

        // Column centroid of opaque pixels: keeps the body centred even when a drawn sword widens the bounds.
        static float OpaqueCentroidX(Color32[] px, int width, RectInt bounds, byte threshold)
        {
            double sum = 0; long count = 0;
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            for (int x = bounds.xMin; x < bounds.xMax; x++)
                if (px[y * width + x].a >= threshold) { sum += x; count++; }
            return count == 0 ? bounds.center.x : (float)(sum / count);
        }

        static Color32 SampleBlock(Color32[] px, int width, RectInt cell, int x0, int x1, int y0, int y1, Settings s, Dictionary<int, int> counts)
        {
            counts.Clear();
            int total = 0, opaque = 0, shift = 8 - s.ChannelBits, bestKey = 0, bestCount = 0;
            for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
            {
                total++;
                if (x < cell.xMin || x >= cell.xMax || y < cell.yMin || y >= cell.yMax) continue;
                Color32 c = px[y * width + x];
                if (c.a < s.AlphaThreshold) continue;
                opaque++;
                int key = (c.r >> shift) << (2 * s.ChannelBits) | (c.g >> shift) << s.ChannelBits | (c.b >> shift);
                counts.TryGetValue(key, out int n);
                counts[key] = ++n;
                if (n > bestCount) { bestCount = n; bestKey = key; }
            }
            if (total == 0 || opaque * 2 < total) return new Color32(0, 0, 0, 0);
            int mask = (1 << s.ChannelBits) - 1, max = mask;
            byte Expand(int q) => (byte)(q * 255 / max);
            return new Color32(Expand(bestKey >> (2 * s.ChannelBits) & mask), Expand(bestKey >> s.ChannelBits & mask), Expand(bestKey & mask), 255);
        }
    }
}
