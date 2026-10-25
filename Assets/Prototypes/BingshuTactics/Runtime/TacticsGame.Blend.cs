// 地形过渡（用户 2026-10-25：“地图的某些贴图块还是四方形的很粗糙。精修这些画块，地形地貌变化一定要有过渡，
// 同时把这个原则作为之后战场，城镇，大地图制作的原则”）。
// 0.2 的三张战棋地图不再每格贴一块地面：开战时把整张地图（连同外圈三格）烘成一张地面贴图。
// 每类地面的占比先在细网格上做“扰动取样＋模糊”，边界就变成弯曲、渐变的；再叠一层细噪声让交界处碎成斑块，
// 所以草地吃进土路、土路边有散落碎石、河边泛起湿浅水，没有方格切口。规则格仍只由高亮与光标表示。
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Prototype.BingshuTactics
{
    public sealed partial class TacticsGame
    {
        const int BlendPad = 3;          // 地图外圈也一起烘，外圈景物下的草地与图内连成一片
        const int BlendPx = 64;          // 每格像素
        const int BlendSub = 8;          // 占比网格每格细分
        Material blendMat;
        Vector2 blendOrigin, blendSize;

        enum Ground { Grass, Road, Sand, Gravel, Water }

        Ground GroundOf(Terrain t) => t switch
        {
            Terrain.Road or Terrain.Bridge => Ground.Road,
            Terrain.Sand => Ground.Sand,
            Terrain.Camp or Terrain.Rock => Ground.Gravel,
            Terrain.Ford or Terrain.River => Ground.Water,
            _ => Ground.Grass,
        };

        Texture2D GroundTex(Ground g) => g switch
        {
            Ground.Road => HdNamed("hd_road"),
            Ground.Sand => Chapter.Night ? HdNamed("hd_drygrass") ?? HdNamed("hd_sand") : HdNamed("hd_sand"),
            Ground.Gravel => HdNamed("hd_camp"),
            Ground.Water => HdNamed("hd_ford"),
            _ => HdNamed("hd_grass"),
        };

        Texture2D HdNamed(string n) => HdGround?.FirstOrDefault(x => x && x.name == n);

        bool UsesBlend(Terrain t) => blendMat && t != Terrain.Bridge && t != Terrain.Wall && t != Terrain.Gate;

        // 地面贴图上 (x, z) 世界坐标对应的 UV
        Vector2 BlendUV(float wx, float wz) => new Vector2((wx - blendOrigin.x) / blendSize.x, (wz - blendOrigin.y) / blendSize.y);

        void BakeChapterGround()
        {
            var kinds = new[] { Ground.Grass, Ground.Road, Ground.Sand, Ground.Gravel, Ground.Water };
            var src = new Dictionary<Ground, (Color32[] px, int w, int h)>();
            foreach (var k in kinds)
            {
                var t = GroundTex(k);
                if (!t || !t.isReadable) continue;
                src[k] = (t.GetPixels32(), t.width, t.height);
            }
            if (!src.ContainsKey(Ground.Grass)) return;   // 没有高清地块时仍用原型的逐格贴图

            int gw = W + BlendPad * 2, gh = H + BlendPad * 2;
            Ground Cell(int x, int y) => GroundOf(grid.TerrainAt(new Vector2Int(Mathf.Clamp(x - BlendPad, 0, W - 1), Mathf.Clamp(y - BlendPad, 0, H - 1))));

            // 1. 细网格上的扰动取样：每个细格取它被噪声推开后落在哪一格
            int fw = gw * BlendSub, fh = gh * BlendSub;
            var present = kinds.Where(k => src.ContainsKey(k)).ToArray();
            var field = present.ToDictionary(k => k, k => new float[fw * fh]);
            for (int fy = 0; fy < fh; fy++)
                for (int fx = 0; fx < fw; fx++)
                {
                    float u = (fx + 0.5f) / BlendSub, v = (fy + 0.5f) / BlendSub;
                    float wu = u + (Mathf.PerlinNoise(u * 0.9f + 11.3f, v * 0.9f) - 0.5f) * 0.9f + (Mathf.PerlinNoise(u * 2.7f, v * 2.7f + 5.1f) - 0.5f) * 0.35f;
                    float wv = v + (Mathf.PerlinNoise(u * 0.9f, v * 0.9f + 23.7f) - 0.5f) * 0.9f + (Mathf.PerlinNoise(u * 2.7f + 9.4f, v * 2.7f) - 0.5f) * 0.35f;
                    var g = Cell(Mathf.FloorToInt(wu), Mathf.FloorToInt(wv));
                    // 水岸只轻微弯曲：水面不越过格子太多，免得能走的格子看着像水
                    if ((g == Ground.Water) != (Cell(Mathf.FloorToInt(u), Mathf.FloorToInt(v)) == Ground.Water))
                        g = Cell(Mathf.FloorToInt(u + (wu - u) * 0.35f), Mathf.FloorToInt(v + (wv - v) * 0.35f));
                    if (!field.ContainsKey(g)) g = Ground.Grass;
                    field[g][fy * fw + fx] = 1f;
                }
            // 2. 模糊：交界变成约半格宽的渐变带
            foreach (var k in present) field[k] = Blur(field[k], fw, fh, BlendSub / 2 + 1);
            // 水面向内收：岸上的格子最多泛一点湿边，看起来是水的地方都确实不能走
            if (field.TryGetValue(Ground.Water, out var water))
                for (int i = 0; i < water.Length; i++) water[i] = Mathf.Clamp01((water[i] - 0.4f) / 0.6f);

            // 3. 逐像素：取占比、加细噪声碎边、按占比锐化后混合各地面贴图（贴图按 4 米一张平铺）
            int pw = gw * BlendPx, ph = gh * BlendPx;
            var outPx = new Color32[pw * ph];
            float texPerTile = T / ZTile;
            var weights = new float[present.Length];
            for (int py = 0; py < ph; py++)
                for (int px = 0; px < pw; px++)
                {
                    float u = (px + 0.5f) / BlendPx, v = (py + 0.5f) / BlendPx;
                    float sum = 0f;
                    for (int i = 0; i < present.Length; i++)
                    {
                        float w = Sample(field[present[i]], fw, fh, u * BlendSub - 0.5f, v * BlendSub - 0.5f);
                        if (w > 0.001f)
                        {
                            float n = Mathf.PerlinNoise(u * 3.1f + i * 17.7f, v * 3.1f - i * 9.3f);
                            w = Mathf.Clamp01(w + (n - 0.5f) * 0.45f * Mathf.Min(w, 1f - w) * 4f);
                            w = w * w * w * w;
                        }
                        else w = 0f;
                        weights[i] = w; sum += w;
                    }
                    if (sum <= 0f) { weights[0] = 1f; sum = 1f; }
                    float r = 0, gC = 0, b = 0;
                    float tu = u * texPerTile, tv = v * texPerTile;
                    for (int i = 0; i < present.Length; i++)
                    {
                        if (weights[i] <= 0f) continue;
                        var s = src[present[i]];
                        int sx = ((int)(tu * s.w) % s.w + s.w) % s.w, sy = ((int)(tv * s.h) % s.h + s.h) % s.h;
                        var c = s.px[sy * s.w + sx];
                        float k = weights[i] / sum;
                        r += c.r * k; gC += c.g * k; b += c.b * k;
                    }
                    outPx[py * pw + px] = new Color32((byte)r, (byte)gC, (byte)b, 255);
                }
            var tex = new Texture2D(pw, ph, TextureFormat.RGBA32, true) { name = "Chapter ground", filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
            tex.SetPixels32(outPx); tex.Apply(true, true);
            blendOrigin = new Vector2(-BlendPad * T, -BlendPad * T);
            blendSize = new Vector2(gw * T, gh * T);
            blendMat = new Material(GroundMat) { name = "Chapter ground", mainTexture = tex, color = Color.white };
            blendMat.SetTexture("_BaseMap", tex);
            blendMat.SetVector("_BaseMap_ST", new Vector4(1, 1, 0, 0));
        }

        static float[] Blur(float[] a, int w, int h, int r)
        {
            var tmp = new float[a.Length]; var o = new float[a.Length];
            float inv = 1f / (2 * r + 1);
            for (int y = 0; y < h; y++)
            {
                float acc = 0; int row = y * w;
                for (int k = -r; k <= r; k++) acc += a[row + Mathf.Clamp(k, 0, w - 1)];
                for (int x = 0; x < w; x++)
                {
                    tmp[row + x] = acc * inv;
                    acc += a[row + Mathf.Min(x + r + 1, w - 1)] - a[row + Mathf.Max(x - r, 0)];
                }
            }
            for (int x = 0; x < w; x++)
            {
                float acc = 0;
                for (int k = -r; k <= r; k++) acc += tmp[Mathf.Clamp(k, 0, h - 1) * w + x];
                for (int y = 0; y < h; y++)
                {
                    o[y * w + x] = acc * inv;
                    acc += tmp[Mathf.Min(y + r + 1, h - 1) * w + x] - tmp[Mathf.Max(y - r, 0) * w + x];
                }
            }
            return o;
        }

        static float Sample(float[] a, int w, int h, float x, float y)
        {
            x = Mathf.Clamp(x, 0, w - 1.001f); y = Mathf.Clamp(y, 0, h - 1.001f);
            int x0 = (int)x, y0 = (int)y; float fx = x - x0, fy = y - y0;
            float a00 = a[y0 * w + x0], a10 = a[y0 * w + x0 + 1], a01 = a[(y0 + 1) * w + x0], a11 = a[(y0 + 1) * w + x0 + 1];
            return Mathf.Lerp(Mathf.Lerp(a00, a10, fx), Mathf.Lerp(a01, a11, fx), fy);
        }
    }
}
