using System;
using UnityEngine;

namespace ThreeKingdoms.EditorTools
{
    // Procedural pixel-art textures for the environment kit (ADR-0005: 30 px per world unit).
    // Deterministic (fixed seeds) so regenerating gives identical PNGs. Placeholder-quality art meant
    // to fix palette, texel density and detail level before hand-painted textures replace them.
    public static class HD2DTexturePainter
    {
        public const int TilePixels = 60;           // one texture repeat = 2 world units at 30 px/unit
        public const float TileWorldSize = 2f;

        public sealed class Canvas
        {
            public readonly int Width, Height;
            public readonly Color32[] Pixels;
            public Canvas(int width, int height) { Width = width; Height = height; Pixels = new Color32[width * height]; }
            public void Set(int x, int y, Color32 c) { x = Mod(x, Width); y = Mod(y, Height); Pixels[y * Width + x] = c; }
            public Color32 Get(int x, int y) => Pixels[Mod(y, Height) * Width + Mod(x, Width)];
            public void Fill(Color32 c) { for (int i = 0; i < Pixels.Length; i++) Pixels[i] = c; }
            public void Rect(int x, int y, int w, int h, Color32 c) { for (int j = y; j < y + h; j++) for (int i = x; i < x + w; i++) Set(i, j, c); }
            static int Mod(int v, int m) => ((v % m) + m) % m;
        }

        static Color32 C(int r, int g, int b, int a = 255) => new Color32((byte)Mathf.Clamp(r, 0, 255), (byte)Mathf.Clamp(g, 0, 255), (byte)Mathf.Clamp(b, 0, 255), (byte)a);
        static Color32 Shade(Color32 c, int d) => C(c.r + d, c.g + d, c.b + d, c.a);

        // Small palette noise: per-pixel jitter in a few discrete steps keeps the result "pixel art" rather than gradient noise.
        static void Jitter(Canvas cv, System.Random rng, int step, int range)
        {
            for (int i = 0; i < cv.Pixels.Length; i++)
                if (cv.Pixels[i].a > 0) cv.Pixels[i] = Shade(cv.Pixels[i], rng.Next(-range, range + 1) * step);
        }

        // Tileable value noise in [0,1] on a cells×cells lattice: soft "painted" variation instead of per-pixel static.
        static float Noise(int x, int y, int size, int cells, int seed)
        {
            float fx = x * cells / (float)size, fy = y * cells / (float)size;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3 - 2 * tx); ty = ty * ty * (3 - 2 * ty);
            float H(int i, int j) { i = ((i % cells) + cells) % cells; j = ((j % cells) + cells) % cells; uint h = (uint)(i * 374761393 + j * 668265263 + seed * 2246822519); h = (h ^ (h >> 13)) * 1274126177u; return (h & 0xffff) / 65535f; }
            return Mathf.Lerp(Mathf.Lerp(H(x0, y0), H(x0 + 1, y0), tx), Mathf.Lerp(H(x0, y0 + 1), H(x0 + 1, y0 + 1), tx), ty);
        }

        // Quantise a 0–1 value to a few tone steps so painted variation stays readable as pixel art.
        static int Step(float v, int steps, int amplitude) => Mathf.RoundToInt((Mathf.Round(v * steps) / steps - 0.5f) * 2f * amplitude);

        public static Canvas Plaster()
        {
            var rng = new System.Random(11);
            var cv = new Canvas(TilePixels, TilePixels);
            for (int y = 0; y < 60; y++)
            for (int x = 0; x < 60; x++)
            {
                int d = Step(Noise(x, y, 60, 4, 1) * 0.65f + Noise(x, y, 60, 10, 2) * 0.35f, 4, 10);
                cv.Set(x, y, C(212 + d, 190 + d, 150 + d - 2));
            }
            Jitter(cv, rng, 3, 1);
            for (int k = 0; k < 14; k++) cv.Set(rng.Next(60), rng.Next(60), C(176, 154, 118));
            for (int k = 0; k < 3; k++)
            {
                int x = rng.Next(60), y = rng.Next(60);
                for (int s = 0; s < 6; s++) { cv.Set(x, y, C(170, 146, 110)); x += rng.Next(-1, 2); y += 1; }
            }
            return cv;
        }

        public static Canvas RammedEarth()
        {
            var rng = new System.Random(12);
            var cv = new Canvas(TilePixels, TilePixels);
            // Tamped layers ~12 px (0.4 unit) thick with soft, broken seams and gravel, so it reads as earth, not planks.
            for (int y = 0; y < 60; y++)
            {
                int layer = y / 12;
                var baseColor = layer % 2 == 0 ? C(180, 148, 104) : C(172, 140, 98);
                for (int x = 0; x < 60; x++)
                {
                    bool seam = y % 12 == 0 && (x * 13 + layer * 7) % 9 > 2;
                    cv.Set(x, y, seam ? C(150, 120, 82) : baseColor);
                }
            }
            Jitter(cv, rng, 5, 2);
            for (int k = 0; k < 60; k++)
            {
                int x = rng.Next(60), y = rng.Next(60);
                cv.Set(x, y, rng.Next(2) == 0 ? C(132, 104, 72) : C(204, 178, 138));
            }
            return cv;
        }

        public static Canvas Timber()
        {
            var rng = new System.Random(13);
            var cv = new Canvas(30, TilePixels);
            cv.Fill(C(116, 74, 44));
            for (int x = 0; x < 30; x += rng.Next(3, 6))
                for (int y = 0; y < 60; y++) if (rng.NextDouble() < 0.8) cv.Set(x, y, C(92, 58, 34));
            Jitter(cv, rng, 4, 1);
            return cv;
        }

        public static Canvas RoofTile()
        {
            // Rows of semi-cylindrical tiles: light ridges (convex tiles) and dark channels, a shadow line under each row.
            var rng = new System.Random(14);
            var cv = new Canvas(TilePixels, TilePixels);
            for (int y = 0; y < 60; y++)
            for (int x = 0; x < 60; x++)
            {
                int column = x % 6, row = y % 10, tile = (x / 6) * 7 + (y / 10) * 3;
                int tone = (tile * 37 % 5 - 2) * 4 + Step(Noise(x, y, 60, 3, 9), 3, 6);   // per-tile and broad colour variation
                int course = (9 - row) * 2;                                                 // each course lighter toward its lip
                Color32 c = column < 3 ? C(92 + course, 100 + course, 116 + course) : column == 3 ? C(70, 76, 90) : C(54, 58, 70);
                if (column == 1 && row > 1) c = C(116 + course, 124 + course, 138 + course);
                if (row == 0) c = C(36, 38, 46);
                if (row == 1 && column < 3) c = C(132, 136, 146);
                c = Shade(c, tone);
                if (column >= 4 && row > 6 && Noise(x, y, 60, 6, 4) > 0.72f) c = C(92, 108, 64);  // moss in the channels
                cv.Set(x, y, c);
            }
            Jitter(cv, rng, 3, 1);
            return cv;
        }

        public static Canvas Paving()
        {
            // Irregular flagstones: Voronoi cells with mortar where the two nearest seeds are close.
            var rng = new System.Random(15);
            var cv = new Canvas(TilePixels, TilePixels);
            var seeds = new Vector2[22];
            var tones = new Color32[seeds.Length];
            for (int i = 0; i < seeds.Length; i++)
            {
                seeds[i] = new Vector2(rng.Next(60), rng.Next(60));
                int t = rng.Next(-2, 3) * 7;
                tones[i] = C(150 + t, 142 + t, 128 + t);
            }
            for (int y = 0; y < 60; y++)
            for (int x = 0; x < 60; x++)
            {
                float best = float.MaxValue, second = float.MaxValue; int bestIndex = 0;
                for (int i = 0; i < seeds.Length; i++)
                {
                    float dx = Mathf.Abs(x - seeds[i].x); dx = Mathf.Min(dx, 60 - dx);
                    float dy = Mathf.Abs(y - seeds[i].y); dy = Mathf.Min(dy, 60 - dy);
                    float d = dx * dx + dy * dy;
                    if (d < best) { second = best; best = d; bestIndex = i; } else if (d < second) second = d;
                }
                float edge = Mathf.Sqrt(second) - Mathf.Sqrt(best);
                Vector2 toSeed = new Vector2(x, y) - seeds[bestIndex];
                int bevel = edge < 3.2f ? (toSeed.y + toSeed.x > 0 ? 12 : -14) : 0;   // light upper-right rim, dark lower-left rim
                var stone = Shade(tones[bestIndex], bevel + Step(Noise(x, y, 60, 8, bestIndex), 3, 5));
                bool mortar = edge < 1.6f;
                cv.Set(x, y, mortar ? (Noise(x, y, 60, 12, 5) > 0.7f ? C(98, 112, 66) : C(96, 88, 76)) : stone);
            }
            Jitter(cv, rng, 3, 1);
            return cv;
        }

        public static Canvas Dirt()
        {
            var rng = new System.Random(16);
            var cv = new Canvas(TilePixels, TilePixels);
            cv.Fill(C(150, 118, 82));
            Jitter(cv, rng, 5, 1);
            for (int k = 0; k < 40; k++)
            {
                int x = rng.Next(60), y = rng.Next(60);
                cv.Set(x, y, C(182, 164, 136)); cv.Set(x + 1, y, C(120, 96, 68));
            }
            for (int k = 0; k < 18; k++) cv.Set(rng.Next(60), rng.Next(60), C(118, 128, 70)); // stray grass
            return cv;
        }

        public static Canvas DoorPlanks()
        {
            var rng = new System.Random(17);
            var cv = new Canvas(30, TilePixels);
            for (int y = 0; y < 60; y++)
            for (int x = 0; x < 30; x++)
                cv.Set(x, y, x % 6 == 0 ? C(52, 32, 20) : C(104, 64, 38));
            Jitter(cv, rng, 4, 1);
            for (int y = 8; y < 60; y += 20) for (int x = 3; x < 30; x += 6) cv.Set(x, y, C(40, 40, 44)); // iron studs
            for (int x = 0; x < 30; x++) { cv.Set(x, 14, C(78, 48, 28)); cv.Set(x, 44, C(78, 48, 28)); }    // battens
            return cv;
        }

        public static Canvas Lattice()
        {
            var cv = new Canvas(30, 30);
            for (int y = 0; y < 30; y++)
            for (int x = 0; x < 30; x++)
            {
                bool frame = x < 2 || x > 27 || y < 2 || y > 27;
                bool bar = x % 6 == 2 || y % 6 == 2;
                cv.Set(x, y, frame ? C(92, 58, 34) : bar ? C(132, 92, 56) : C(48, 36, 30));
            }
            return cv;
        }

        public static Canvas Thatch()
        {
            var rng = new System.Random(18);
            var cv = new Canvas(TilePixels, TilePixels);
            // Short straw strands of mixed tones running down the slope (texture V), plus a shaggy course edge.
            cv.Fill(C(150, 120, 70));
            for (int k = 0; k < 520; k++)
            {
                int x = rng.Next(60), y = rng.Next(60), len = rng.Next(3, 8), tone = rng.Next(4);
                var c = tone == 0 ? C(196, 164, 98) : tone == 1 ? C(176, 144, 84) : tone == 2 ? C(132, 104, 58) : C(212, 184, 120);
                for (int s = 0; s < len; s++) cv.Set(x + (s % 3 == 2 ? 1 : 0), y + s, c);
            }
            for (int x = 0; x < 60; x++) if (rng.Next(3) > 0) cv.Set(x, 0, C(108, 84, 46));
            return cv;
        }

        public static Canvas Leaves()
        {
            var rng = new System.Random(19);
            var cv = new Canvas(30, 30);
            for (int i = 0; i < cv.Pixels.Length; i++)
            {
                int t = rng.Next(4);
                cv.Pixels[i] = t == 0 ? C(62, 92, 48) : t == 1 ? C(84, 116, 58) : t == 2 ? C(100, 132, 66) : C(126, 150, 78);
            }
            return cv;
        }

        public static Canvas Blossom()
        {
            var rng = new System.Random(20);
            var cv = new Canvas(30, 30);
            for (int i = 0; i < cv.Pixels.Length; i++)
            {
                int t = rng.Next(5);
                cv.Pixels[i] = t == 0 ? C(196, 106, 132) : t == 1 ? C(230, 150, 172) : t == 2 ? C(244, 186, 200) : t == 3 ? C(252, 222, 228) : C(110, 124, 70);
            }
            return cv;
        }

        public static Canvas Lantern()
        {
            var cv = new Canvas(16, 16);
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                Color32 c = C(214, 58, 34);
                if (y < 2 || y > 13) c = C(196, 160, 72);                       // gold caps
                else if (x % 5 == 0) c = C(170, 40, 26);                         // ribs
                cv.Set(x, y, c);
            }
            return cv;
        }

        // 酒 as a 10×10 glyph: 氵 on the left, 酉 on the right. Used on the wine banner.
        static readonly string[] WineGlyph =
        {
            "##..######",
            "....#.#..#",
            "#...#.#..#",
            ".#..######",
            "....#....#",
            "##..######",
            "...##....#",
            "..#.#....#",
            ".#..######",
            "#.........",
        };

        public static Canvas WineBanner()
        {
            var cv = new Canvas(30, 60);
            PaintBanner(cv, WineGlyph);
            for (int x = 1; x < 29; x += 3) { cv.Set(x, 0, C(0, 0, 0, 0)); }                 // ragged hem
            return cv;
        }

        // 铁 as an 8-row glyph: 钅 on the left, 失 on the right. Placeholder until pixel calligraphy (ADR-0005 §9).
        static readonly string[] IronGlyph =
        {
            ".#....#...",
            "###.######",
            ".#....#...",
            "###.######",
            ".#....#...",
            ".#...#.#..",
            ".##.#...#.",
            "##.#.....#",
        };

        public static Canvas IronBanner()
        {
            var cv = new Canvas(30, 60);
            PaintBanner(cv, IronGlyph);
            for (int x = 2; x < 29; x += 4) cv.Set(x, 0, C(0, 0, 0, 0));
            return cv;
        }

        // Red cloth banner with a darker border, soft folds and a near-black glyph, as in the battle reference.
        static void PaintBanner(Canvas cv, string[] glyph)
        {
            for (int y = 0; y < cv.Height; y++)
            for (int x = 0; x < cv.Width; x++)
            {
                int fold = (x % 10 < 2) ? -14 : (x % 10 > 7 ? 8 : 0);
                cv.Set(x, y, Shade(C(176, 50, 40), fold));
            }
            cv.Rect(0, 0, cv.Width, 3, C(120, 30, 26));
            cv.Rect(0, cv.Height - 4, cv.Width, 4, C(120, 30, 26));
            DrawGlyph(cv, glyph, 5, 20, 2, C(36, 20, 18));
        }

        public static Canvas Cloth()
        {
            var rng = new System.Random(21);
            var cv = new Canvas(TilePixels, TilePixels);
            for (int y = 0; y < 60; y++)
            for (int x = 0; x < 60; x++)
                cv.Set(x, y, Shade(C(196, 180, 150), (x % 12 < 2 ? -12 : 0) + Step(Noise(x, y, 60, 3, 21), 3, 6)));
            Jitter(cv, rng, 2, 1);
            return cv;
        }

        public static Canvas Clay()
        {
            var cv = new Canvas(30, 30);
            for (int y = 0; y < 30; y++)
            for (int x = 0; x < 30; x++)
                cv.Set(x, y, Shade(C(170, 92, 56), (y % 10 == 0 ? -18 : 0) + Step(Noise(x, y, 30, 3, 22), 3, 8)));
            return cv;
        }

        public static Canvas Iron()
        {
            var rng = new System.Random(23);
            var cv = new Canvas(30, 30);
            cv.Fill(C(70, 72, 78));
            Jitter(cv, rng, 6, 1);
            return cv;
        }

        public static Canvas ForgeCoals()
        {
            var rng = new System.Random(24);
            var cv = new Canvas(16, 16);
            for (int i = 0; i < cv.Pixels.Length; i++)
            {
                int t = rng.Next(4);
                cv.Pixels[i] = t == 0 ? C(255, 210, 90) : t == 1 ? C(250, 120, 30) : t == 2 ? C(180, 50, 20) : C(50, 30, 26);
            }
            return cv;
        }

        public static Canvas Produce(int r, int g, int b)
        {
            var cv = new Canvas(30, 30);
            for (int y = 0; y < 30; y++)
            for (int x = 0; x < 30; x++)
                cv.Set(x, y, Shade(C(r, g, b), Step(Noise(x, y, 30, 5, r + g), 3, 18)));
            return cv;
        }

        public static Canvas Signboard()
        {
            var cv = new Canvas(60, 20);
            cv.Fill(C(64, 40, 26));
            for (int x = 0; x < 60; x++) { cv.Set(x, 0, C(186, 150, 70)); cv.Set(x, 19, C(186, 150, 70)); }
            for (int y = 0; y < 20; y++) { cv.Set(0, y, C(186, 150, 70)); cv.Set(59, y, C(186, 150, 70)); }
            // Two abstract gilded "characters" (not real text) so the board reads as lettered at a distance.
            for (int g = 0; g < 2; g++)
            {
                int ox = 12 + g * 22;
                cv.Rect(ox, 5, 12, 1, C(214, 182, 96)); cv.Rect(ox + 5, 5, 1, 10, C(214, 182, 96));
                cv.Rect(ox + 1, 10, 10, 1, C(214, 182, 96)); cv.Rect(ox + 2, 14, 8, 1, C(214, 182, 96));
                cv.Rect(ox + 1 + g * 7, 6, 1, 8, C(214, 182, 96));
            }
            return cv;
        }

        static void DrawGlyph(Canvas cv, string[] rows, int x0, int y0, int scale, Color32 color)
        {
            for (int r = 0; r < rows.Length; r++)
            for (int c = 0; c < rows[r].Length; c++)
                if (rows[r][c] == '#')
                    cv.Rect(x0 + c * scale, y0 + (rows.Length - 1 - r) * scale, scale, scale, color);
        }
    }
}
