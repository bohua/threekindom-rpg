// PROTOTYPE - NOT FOR PRODUCTION
// Question: 空轨式 AT 行动条＋小范围格子战斗，配合“学派回路镶嵌兵书→策略”，是否好玩、是否看得懂？
// Date: 2026-10-23
// 战场 3D 简模＋像素贴图（美术总监评审 docs/art/bingshu-tactics-poc-art-review.md 第 2 节）：
// 每格顶面 36×36 像素、每层侧面 36×15 像素（30 像素/米），颜色全部量化到城外背景 64 色；点采样、无 mipmap、硬边平面。
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Prototype.BingshuTactics
{
    public sealed partial class TacticsGame
    {
        // 由编辑器构建脚本填入：城外背景 64 色调色板
        public Color32[] Palette;

        const int TopPx = 36, SidePx = 15, Variants = 4;
        const float Border = -0.25f;   // 地图外一圈的地面高度

        enum Side { GrassLip, Dirt, Rammed, StoneBase, WetBank }

        readonly Dictionary<Material, MeshBuilder> builders = new Dictionary<Material, MeshBuilder>();
        readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        Material waterMat;
        readonly List<(Mesh mesh, Vector3[] baseVerts, float phase)> flags = new List<(Mesh, Vector3[], float)>();

        // ---------------- 调色板与像素贴图 ----------------
        readonly Dictionary<int, Color32> quantCache = new Dictionary<int, Color32>();

        Color32 Q(Color c)
        {
            var c32 = (Color32)c;
            int key = (c32.r << 16) | (c32.g << 8) | c32.b;
            if (quantCache.TryGetValue(key, out var hit)) return hit;
            if (Palette == null || Palette.Length == 0) return c32;
            Color32 best = Palette[0]; int bd = int.MaxValue;
            foreach (var p in Palette)
            {
                int dr = p.r - c32.r, dg = p.g - c32.g, db = p.b - c32.b;
                int d = dr * dr * 3 + dg * dg * 4 + db * db * 2;
                if (d < bd) { bd = d; best = p; }
            }
            best.a = 255;
            quantCache[key] = best;
            return best;
        }

        static Texture2D NewTex(int w, int h) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };

        static float Hash(int x, int y, int s)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + s * 2147483647;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        Texture2D Paint(int w, int h, System.Func<int, int, Color> f)
        {
            var t = NewTex(w, h);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var c = f(x, y);
                    px[y * w + x] = c.a < 0.5f ? new Color32(0, 0, 0, 0) : Q(c);
                }
            t.SetPixels32(px); t.Apply(false, false);
            return t;
        }

        // 顶面：4 个变体横排；外沿 1 像素亮边
        Texture2D TopStrip(Terrain t, int frame = 0)
        {
            return Paint(TopPx * Variants, TopPx, (x, y) =>
            {
                int v = x / TopPx, lx = x % TopPx;
                float n = Hash(lx, y, (int)t * 31 + v * 7 + frame * 101);
                Color c;
                switch (t)
                {
                    case Terrain.Sand:
                        c = Color.Lerp(new Color(0.80f, 0.69f, 0.47f), new Color(0.70f, 0.58f, 0.38f), n * 0.8f);
                        if (n > 0.93f) c = new Color(0.55f, 0.45f, 0.30f);
                        if ((lx + y * 2 + v * 5) % 13 == 0 && n > 0.4f) c *= 0.9f;   // 风纹
                        break;
                    case Terrain.Road:
                        c = Color.Lerp(new Color(0.58f, 0.51f, 0.40f), new Color(0.47f, 0.41f, 0.32f), n);
                        if (n > 0.9f) c = new Color(0.66f, 0.62f, 0.55f);           // 碎石
                        if (Mathf.Abs(lx - 11) < 2 || Mathf.Abs(lx - 25) < 2) c *= 0.88f;   // 车辙
                        break;
                    case Terrain.Camp:
                        c = Color.Lerp(new Color(0.52f, 0.43f, 0.31f), new Color(0.43f, 0.35f, 0.25f), n);
                        if (n > 0.95f) c = new Color(0.40f, 0.52f, 0.28f);
                        break;
                    case Terrain.Bridge:
                        c = (lx % 9 == 0) ? new Color(0.28f, 0.18f, 0.10f) : Color.Lerp(new Color(0.55f, 0.37f, 0.21f), new Color(0.45f, 0.30f, 0.17f), n);
                        if (y % 12 == 6 && lx % 9 == 4) c = new Color(0.25f, 0.22f, 0.20f);   // 钉
                        break;
                    case Terrain.Wall:
                    case Terrain.Gate:
                        c = Color.Lerp(new Color(0.66f, 0.56f, 0.41f), new Color(0.56f, 0.47f, 0.34f), n);
                        break;
                    case Terrain.River:
                        c = Color.Lerp(new Color(0.20f, 0.36f, 0.55f), new Color(0.26f, 0.44f, 0.63f), n * 0.6f);
                        if ((lx + y * 3 + frame * 4) % 17 < 2 && n > 0.3f) c = new Color(0.55f, 0.70f, 0.82f);   // 波光，按整像素步进
                        break;
                    case Terrain.Ford:
                        c = Color.Lerp(new Color(0.38f, 0.55f, 0.66f), new Color(0.45f, 0.62f, 0.70f), n);
                        if (Hash(lx / 4, y / 4, v) > 0.72f) c = new Color(0.58f, 0.56f, 0.50f);   // 卵石
                        break;
                    default:   // 草地
                        c = Color.Lerp(new Color(0.40f, 0.56f, 0.28f), new Color(0.33f, 0.48f, 0.23f), n);
                        if (n > 0.92f) c = new Color(0.52f, 0.66f, 0.33f);
                        if (n < 0.04f) c = new Color(0.80f, 0.74f, 0.42f);   // 小花
                        break;
                }
                if (lx == 0 || y == TopPx - 1) c *= 1.12f;   // 外沿亮边
                if (lx == TopPx - 1 || y == 0) c *= 0.86f;
                c.a = 1f;
                return c;
            });
        }

        Texture2D SideTex(Side s) => Paint(TopPx, SidePx, (x, y) =>
        {
            float n = Hash(x, y, (int)s * 13 + 5);
            Color c;
            switch (s)
            {
                case Side.GrassLip:
                    c = Color.Lerp(new Color(0.47f, 0.37f, 0.25f), new Color(0.38f, 0.29f, 0.19f), n);
                    if (y >= SidePx - 4 + (Hash(x, 0, 9) > 0.6f ? -1 : 0)) c = Color.Lerp(new Color(0.40f, 0.56f, 0.28f), new Color(0.32f, 0.46f, 0.22f), n);
                    break;
                case Side.Rammed:
                    // 夯土墙：横向版筑夯层线，每层 5 像素
                    c = Color.Lerp(new Color(0.64f, 0.53f, 0.38f), new Color(0.55f, 0.45f, 0.32f), n);
                    if (y % 5 == 0) c *= 0.82f;
                    if (n > 0.95f) c *= 0.85f;
                    break;
                case Side.StoneBase:
                    c = Color.Lerp(new Color(0.55f, 0.53f, 0.49f), new Color(0.45f, 0.43f, 0.40f), n);
                    if (y % 7 == 0 || (x + (y / 7 % 2) * 6) % 12 == 0) c = new Color(0.32f, 0.30f, 0.28f);
                    break;
                case Side.WetBank:
                    c = Color.Lerp(new Color(0.36f, 0.28f, 0.20f), new Color(0.28f, 0.22f, 0.16f), n);
                    if (y <= 2) c = new Color(0.62f, 0.74f, 0.80f);   // 水沫线
                    break;
                default:
                    c = Color.Lerp(new Color(0.50f, 0.39f, 0.26f), new Color(0.40f, 0.31f, 0.20f), n);
                    if (n > 0.93f) c = new Color(0.58f, 0.55f, 0.50f);
                    break;
            }
            if (y == 0) c *= 0.8f;   // 下沿暗边
            c.a = 1f;
            return c;
        });

        Material LitMat(string key, Texture2D tex, Color? tint = null)
        {
            if (mats.TryGetValue(key, out var m)) return m;
            var over = EnvOverride(key);   // Codex 贴图包优先
            if (over) tex = over;
            m = new Material(GroundMat) { mainTexture = tex, color = tint ?? Color.white };
            m.SetTexture("_BaseMap", tex);
            mats[key] = m;
            return m;
        }

        Material TopMat(Terrain t) => LitMat("top" + t, TopStrip(t));
        Material SideMat(Side s) => LitMat("side" + s, SideTex(s));

        Material Wood => LitMat("wood", Paint(16, 16, (x, y) =>
        {
            var c = Color.Lerp(new Color(0.48f, 0.32f, 0.19f), new Color(0.38f, 0.25f, 0.14f), Hash(x / 3, y, 4));
            if (x % 5 == 0) c *= 0.85f; c.a = 1; return c;
        }));

        Material Cloth => LitMat("cloth", Paint(16, 16, (x, y) =>
        {
            var c = Color.Lerp(new Color(0.84f, 0.78f, 0.64f), new Color(0.76f, 0.70f, 0.56f), Hash(x, y, 8));
            if (x % 8 == 0) c *= 0.88f; c.a = 1; return c;
        }));

        Material RoofTile => LitMat("roof", Paint(30, 30, (x, y) =>
        {
            // 青灰筒瓦：竖向瓦垄，3 像素一垄
            var c = new Color(0.36f, 0.38f, 0.42f);
            if (x % 3 == 0) c = new Color(0.25f, 0.27f, 0.30f);
            if (x % 3 == 1) c = new Color(0.44f, 0.46f, 0.50f);
            if (y % 10 == 0) c *= 0.85f; c.a = 1; return c;
        }));

        Material Stone => LitMat("stone", Paint(16, 16, (x, y) =>
        {
            var c = Color.Lerp(new Color(0.55f, 0.53f, 0.50f), new Color(0.42f, 0.40f, 0.38f), Hash(x / 2, y / 2, 6)); c.a = 1; return c;
        }));

        Material Whitewash => LitMat("whitewash", Paint(16, 16, (x, y) =>
        {
            var c = Color.Lerp(new Color(0.85f, 0.81f, 0.72f), new Color(0.76f, 0.72f, 0.64f), Hash(x, y, 11));
            if (y == 15 || y == 0) c = new Color(0.45f, 0.30f, 0.18f); c.a = 1; return c;
        }));

        Material DoorMat => LitMat("door", Paint(18, 24, (x, y) =>
        {
            var c = Color.Lerp(new Color(0.36f, 0.20f, 0.12f), new Color(0.28f, 0.15f, 0.09f), Hash(x / 3, y, 2));
            if (x == 9) c = new Color(0.15f, 0.08f, 0.05f);
            if (x % 4 == 2 && y % 5 == 2) c = new Color(0.78f, 0.64f, 0.32f);   // 门钉
            c.a = 1; return c;
        }));

        Material FlagMat => LitMat("flagLu", Paint(18, 27, (x, y) =>
        {
            // 红底旗面，米白「呂」：上下两个口，中间一竖相连
            var c = new Color(0.66f, 0.15f, 0.12f);
            bool topBox = x >= 5 && x <= 12 && y >= 15 && y <= 22 && (x == 5 || x == 12 || y == 15 || y == 22 || x == 6 || x == 11 || y == 16 || y == 21);
            bool botBox = x >= 4 && x <= 13 && y >= 3 && y <= 12 && (x == 4 || x == 13 || y == 3 || y == 12 || x == 5 || x == 12 || y == 4 || y == 11);
            bool link = (x == 8 || x == 9) && y > 12 && y < 15;
            if (topBox || botBox || link) c = new Color(0.92f, 0.86f, 0.72f);
            if (x == 0) c *= 0.8f;
            c.a = 1; return c;
        }));

        // ---------------- 网格拼装 ----------------
        sealed class MeshBuilder
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector2> UV = new List<Vector2>();
            public readonly List<int> I = new List<int>();

            // a b c d 逆时针（从外侧看），uv 依次对应
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
            {
                int k = V.Count;
                V.Add(a); V.Add(b); V.Add(c); V.Add(d);
                UV.Add(ua); UV.Add(ub); UV.Add(uc); UV.Add(ud);
                I.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 });
            }

            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc)
            {
                int k = V.Count;
                V.Add(a); V.Add(b); V.Add(c); UV.Add(ua); UV.Add(ub); UV.Add(uc);
                I.AddRange(new[] { k, k + 1, k + 2 });
            }

            // 轴对齐长方体（硬边，6 面）；uvScale：每米多少贴图单位
            public void Box(Vector3 center, Vector3 size, float uvPerM = 1f, Quaternion? rot = null)
            {
                var r = rot ?? Quaternion.identity; var h = size / 2f;
                Vector3 P(float x, float y, float z) => center + r * new Vector3(x * h.x, y * h.y, z * h.z);
                Vector2 U(float a, float b) => new Vector2(a * uvPerM, b * uvPerM);
                float sx = size.x, sy = size.y, sz = size.z;
                Quad(P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1), U(0, 0), U(0, sy), U(sx, sy), U(sx, 0));     // 南
                Quad(P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), P(-1, -1, 1), U(0, 0), U(0, sy), U(sx, sy), U(sx, 0));         // 北
                Quad(P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), P(-1, -1, -1), U(0, 0), U(0, sy), U(sz, sy), U(sz, 0));     // 西
                Quad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), U(0, 0), U(0, sy), U(sz, sy), U(sz, 0));         // 东
                Quad(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), U(0, 0), U(0, sz), U(sx, sz), U(sx, 0));         // 顶
                Quad(P(-1, -1, 1), P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), U(0, 0), U(0, sz), U(sx, sz), U(sx, 0));     // 底
            }

            // 四棱锥尖头
            public void Spike(Vector3 baseCenter, float half, float height, Quaternion rot)
            {
                Vector3 P(float x, float y, float z) => baseCenter + rot * new Vector3(x, y, z);
                var top = P(0, height, 0);
                var c = new[] { P(-half, 0, -half), P(half, 0, -half), P(half, 0, half), P(-half, 0, half) };
                for (int i = 0; i < 4; i++) Tri(c[i], top, c[(i + 1) % 4], new Vector2(0, 0), new Vector2(0.5f, 1), new Vector2(1, 0));
            }
        }

        MeshBuilder B(Material m)
        {
            if (!builders.TryGetValue(m, out var b)) builders[m] = b = new MeshBuilder();
            return b;
        }

        void FlushMeshes(string name, bool receiveOnly = false)
        {
            foreach (var kv in builders)
            {
                if (kv.Value.V.Count == 0) continue;
                var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(kv.Value.V); mesh.SetUVs(0, kv.Value.UV); mesh.SetTriangles(kv.Value.I, 0);
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                var go = new GameObject($"{name} ({kv.Key.name})");
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = kv.Key;
                mr.shadowCastingMode = receiveOnly ? ShadowCastingMode.Off : ShadowCastingMode.On;
                trianglesBuilt += kv.Value.I.Count / 3;
            }
            builders.Clear();
        }

        public int trianglesBuilt;

        // ---------------- 地形 ----------------
        float TopOf(Vector2Int p)
        {
            var t = grid.TerrainAt(p);
            return t == Terrain.River ? -0.15f : t == Terrain.Ford ? -0.06f : grid.HeightAt(p) * HS + (t == Terrain.Wall || t == Terrain.Gate ? WallExtra : 0f);
        }

        float TopOrBorder(Vector2Int p) => grid.In(p) ? TopOf(p) : Border;

        static readonly Vector2Int[] Dirs4 = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        bool IsWallish(Vector2Int p) => grid.In(p) && (grid.TerrainAt(p) == Terrain.Wall || grid.TerrainAt(p) == Terrain.Gate);

        // 关墙收分：朝外露出的边，顶部向内收 0.09 m（约 6%）
        const float WallInset = 0.09f;

        float Inset(Vector2Int p, Vector2Int d) =>
            IsWallish(p) && TopOrBorder(p + d) < TopOf(p) - 0.01f ? WallInset : 0f;

        void BuildTerrain()
        {
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    var p = new Vector2Int(x, y);
                    var t = grid.TerrainAt(p);
                    var groundT = t == Terrain.JuMa || t == Terrain.Rock ? Terrain.Grass : t;
                    float top = TopOf(p);
                    float x0 = x * T, x1 = (x + 1) * T, z0 = y * T, z1 = (y + 1) * T;
                    float iN = Inset(p, Vector2Int.up), iS = Inset(p, Vector2Int.down), iE = Inset(p, Vector2Int.right), iW = Inset(p, Vector2Int.left);

                    // 顶面：随机变体，草地与土地随机转 90°
                    int v = (int)(Hash(x, y, 3) * Variants) % Variants;
                    int rot = groundT == Terrain.Grass || groundT == Terrain.Camp || groundT == Terrain.Sand ? (int)(Hash(x, y, 4) * 4) % 4 : 0;
                    var uvs = new[] { new Vector2(v / 4f, 0), new Vector2(v / 4f, 1), new Vector2((v + 1) / 4f, 1), new Vector2((v + 1) / 4f, 0) };
                    var r = Enumerable.Range(0, 4).Select(i => uvs[(i + rot) % 4]).ToArray();
                    var zTop = groundT == Terrain.River ? null : ZMat(ZTop(groundT));
                    if (zTop)
                    {
                        Vector2 W2(float wx, float wz) => new Vector2(wx / ZTile, wz / ZTile);
                        r = new[] { W2(x0 + iW, z0 + iS), W2(x0 + iW, z1 - iN), W2(x1 - iE, z1 - iN), W2(x1 - iE, z0 + iS) };
                    }
                    var topMat = groundT == Terrain.River ? waterMat : zTop ? zTop : TopMat(groundT);
                    B(topMat).Quad(new Vector3(x0 + iW, top, z0 + iS), new Vector3(x0 + iW, top, z1 - iN), new Vector3(x1 - iE, top, z1 - iN), new Vector3(x1 - iE, top, z0 + iS), r[0], r[1], r[2], r[3]);

                    // 侧面：只画比邻格高出来的部分
                    foreach (var d in Dirs4)
                    {
                        var nb = p + d;
                        float low = TopOrBorder(nb);
                        if (low >= top - 0.001f) continue;
                        SideFaces(p, d, top, low, t, nb);
                    }
                }
            FlushMeshes("Terrain");
        }

        void SideFaces(Vector2Int p, Vector2Int d, float top, float low, Terrain t, Vector2Int nb)
        {
            float x0 = p.x * T, x1 = (p.x + 1) * T, z0 = p.y * T, z1 = (p.y + 1) * T;
            float inset = Inset(p, d);
            // 这条边的两端（从外侧看左→右）
            Vector3 a, b; Vector3 inward = new Vector3(-d.x, 0, -d.y);
            if (d == Vector2Int.down) { a = new Vector3(x0, 0, z0); b = new Vector3(x1, 0, z0); }
            else if (d == Vector2Int.up) { a = new Vector3(x1, 0, z1); b = new Vector3(x0, 0, z1); }
            else if (d == Vector2Int.left) { a = new Vector3(x0, 0, z1); b = new Vector3(x0, 0, z0); }
            else { a = new Vector3(x1, 0, z0); b = new Vector3(x1, 0, z1); }
            bool nbWater = grid.In(nb) && (grid.TerrainAt(nb) == Terrain.River || grid.TerrainAt(nb) == Terrain.Ford);
            float span = top - low;
            // 从上往下每 0.5 m 一段
            float y = top; int layer = 0;
            while (y > low + 0.001f)
            {
                float seg = Mathf.Min(HS, y - low);
                float yb = y - seg;
                Side s;
                if (nbWater && yb <= 0.001f && y <= 0.01f) s = Side.WetBank;
                else if (IsWallish(p)) s = yb < 0.01f && layer > 0 ? Side.StoneBase : Side.Rammed;
                else if (t == Terrain.Grass && layer == 0) s = Side.GrassLip;
                else s = Side.Dirt;
                if (nbWater && yb < 0.01f) s = Side.WetBank;
                float tInset = inset * (top - y) / Mathf.Max(0.01f, span), bInset = inset * (top - yb) / Mathf.Max(0.01f, span);
                // 收分：越往上越往内收（顶部内收 inset，底部为 0）
                float topIn = inset - tInset, botIn = inset - bInset;
                var at = a + Vector3.up * y + inward * topIn; var bt = b + Vector3.up * y + inward * topIn;
                var ab = a + Vector3.up * yb + inward * botIn; var bb = b + Vector3.up * yb + inward * botIn;
                float v1 = seg / HS;
                var zWall = IsWallish(p) ? ZMat("citywall_face") : null;
                if (zWall)
                {
                    // 涿郡城墙立面：120×150 一张对应 4 米宽、5 米高，按世界坐标连续铺
                    float ua = (d.y != 0 ? a.x : a.z) / ZTile, ub = (d.y != 0 ? b.x : b.z) / ZTile;
                    B(zWall).Quad(ab, at, bt, bb, new Vector2(ua, yb / 5f), new Vector2(ua, y / 5f), new Vector2(ub, y / 5f), new Vector2(ub, yb / 5f));
                }
                else B(SideMat(s)).Quad(ab, at, bt, bb, new Vector2(0, 1 - v1), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 1 - v1));
                y = yb; layer++;
            }
            // 相差一层、两边都能走的地方加三级踏步（只是视觉；规则仍按“登高 +1”）
            if (grid.In(nb) && Mathf.Abs(span - HS) < 0.01f && !Grid.Impassable(t) && !Grid.Impassable(grid.TerrainAt(nb)) && !nbWater && Hash(p.x, p.y, d.x * 3 + d.y + 7) > 0.35f)
            {
                var outv = new Vector3(d.x, 0, d.y); var along = new Vector3(Mathf.Abs(d.y), 0, Mathf.Abs(d.x));
                var mid = (a + b) / 2f + Vector3.up * low;
                for (int i = 0; i < 3; i++)
                {
                    float h = (i + 1) * HS / 3f, depth = (3 - i) * 0.12f;
                    B(Stone).Box(mid + outv * (depth / 2f) + Vector3.up * (h / 2f), along * (T * 0.55f) + new Vector3(Mathf.Abs(d.x), 0, Mathf.Abs(d.y)) * depth + Vector3.up * h, 2f);
                }
            }
        }

        // ---------------- 道具 ----------------
        void BuildProps()
        {
            var wood = Wood;
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    var p = new Vector2Int(x, y); var t = grid.TerrainAt(p); var c = Tile(p);
                    switch (t)
                    {
                        case Terrain.JuMa: JuMa(c); break;
                        case Terrain.Rock:   // 精修件：岩石露头精灵；没有时退回低多边形岩块
                            if (!Billboard("sprite_rock_outcrop_a", c, 0.7f)) RockMesh(B(Stone), c, 0.5f, x * 7 + y);
                            break;
                        case Terrain.Wall: Parapet(p, c); break;
                        case Terrain.Gate: GateFront(p, c); break;
                        case Terrain.Camp: Palisade(p, c); break;
                        case Terrain.Bridge:
                            foreach (float side in new[] { -1f, 1f })
                            {
                                B(wood).Box(c + new Vector3(side * T * 0.46f, 0.25f, 0), new Vector3(0.06f, 0.06f, T), 2f);
                                for (int k = -1; k <= 1; k++) B(wood).Box(c + new Vector3(side * T * 0.46f, 0.13f, k * T * 0.4f), new Vector3(0.06f, 0.26f, 0.06f), 2f);
                            }
                            break;
                    }
                }
            if (stage == 1)
            {
                GateTower(new Vector2Int(7, 12));
                foreach (var tp in new[] { new Vector2Int(0, 11), new Vector2Int(13, 11) }) Tent(Tile(tp));
                Flag(Tile(new Vector2Int(2, 10)) + new Vector3(-0.4f, 0, 0.4f));
                Flag(Tile(new Vector2Int(11, 10)) + new Vector3(0.4f, 0, 0.4f));
            }
            BuildDetails();
            Scenery();
            FlushMeshes("Props");
        }

        // 拒马：横木上斜插三组 X 形削尖木桩
        void JuMa(Vector3 c)
        {
            var b = B(LogWood);
            b.Box(c + Vector3.up * 0.45f, new Vector3(T * 0.98f, 0.12f, 0.12f), 2f);
            for (int k = -1; k <= 1; k++)
                foreach (float ang in new[] { 38f, -38f })
                {
                    var rot = Quaternion.Euler(ang, 0, 0);
                    var mid = c + new Vector3(k * T * 0.32f, 0.45f, 0);
                    b.Box(mid, new Vector3(0.08f, 1.1f, 0.08f), 2f, rot);
                    b.Spike(mid + rot * new Vector3(0, 0.55f, 0), 0.04f, 0.14f, rot);
                }
        }

        void Palisade(Vector2Int p, Vector3 c)
        {
            var b = B(LogWood);
            foreach (var d in Dirs4)
            {
                var nb = p + d;
                if (grid.In(nb) && (grid.TerrainAt(nb) == Terrain.Camp || grid.TerrainAt(nb) == Terrain.Wall)) continue;
                bool gate = grid.In(nb) && !Grid.Impassable(grid.TerrainAt(nb));
                var along = new Vector3(d.y, 0, d.x); var edge = c + new Vector3(d.x, 0, d.y) * T * 0.46f;
                for (int k = -2; k <= 2; k++)
                {
                    if (gate && Mathf.Abs(k) <= 0) continue;   // 正中留寨门
                    var pos = edge + along * (k * T * 0.2f);
                    b.Box(pos + Vector3.up * 0.32f, new Vector3(0.08f, 0.64f, 0.08f), 2f);
                    b.Spike(pos + Vector3.up * 0.64f, 0.04f, 0.12f, Quaternion.identity);
                }
                foreach (float hy in new[] { 0.2f, 0.45f })
                    b.Box(edge + Vector3.up * hy, new Vector3(Mathf.Abs(along.x) * T * 0.9f + 0.05f, 0.05f, Mathf.Abs(along.z) * T * 0.9f + 0.05f), 2f);
            }
        }

        // 女墙与垛口：外露边上 0.25 m 女墙，每格两个垛口
        void Parapet(Vector2Int p, Vector3 c)
        {
            var b = B(Env("merlon") ? LitMat("merlon", Env("merlon")) : SideMat(Side.Rammed));
            foreach (var d in Dirs4)
            {
                var nb = p + d;
                if (TopOrBorder(nb) >= TopOf(p) - 0.01f) continue;
                var outv = new Vector3(d.x, 0, d.y); var along = new Vector3(Mathf.Abs(d.y), 0, Mathf.Abs(d.x));
                var edge = c + outv * (T * 0.5f - WallInset - 0.07f);
                b.Box(edge + Vector3.up * 0.12f, along * T + new Vector3(Mathf.Abs(d.x), 0, Mathf.Abs(d.y)) * 0.14f + Vector3.up * 0.25f, 2f);
                foreach (float k in new[] { -0.3f, 0.3f })
                    b.Box(edge + along * (k * T) + Vector3.up * 0.37f, along * (T * 0.25f) + new Vector3(Mathf.Abs(d.x), 0, Mathf.Abs(d.y)) * 0.14f + Vector3.up * 0.25f, 2f);
            }
        }

        // 关门：平顶木过梁门洞，两扇木门加门钉
        void GateFront(Vector2Int p, Vector3 c)
        {
            float zf = p.y * T - 0.01f;
            var door = B(DoorMat);
            // 深门洞（暗色内壁）＋包铁双扇门＋平顶木过梁
            B(LitMat("passage", Env("side_rammed") ?? (Texture2D)Stone.mainTexture, new Color(0.45f, 0.40f, 0.36f))).Box(new Vector3(c.x, 1.1f, zf + 0.25f), new Vector3(T * 0.86f, 2.2f, 0.5f), 1f);
            door.Quad(new Vector3(c.x - T * 0.40f, 0, zf), new Vector3(c.x - T * 0.40f, 2.0f, zf), new Vector3(c.x, 2.0f, zf), new Vector3(c.x, 0, zf),
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
            door.Quad(new Vector3(c.x, 0, zf), new Vector3(c.x, 2.0f, zf), new Vector3(c.x + T * 0.40f, 2.0f, zf), new Vector3(c.x + T * 0.40f, 0, zf),
                new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, 0));
            B(Wood).Box(new Vector3(c.x, 2.1f, zf - 0.05f), new Vector3(T * 1.0f, 0.18f, 0.14f), 2f);
            foreach (float s in new[] { -1f, 1f }) B(Pillar).Box(new Vector3(c.x + s * T * 0.46f, 1.05f, zf - 0.05f), new Vector3(0.12f, 2.1f, 0.12f), 2f);
        }

        // 城楼：参照汉代陶楼——下层木柱与平坐栏杆，上层粉墙，庑殿顶，屋檐平直
        void GateTower(Vector2Int gate)
        {
            var baseC = Tile(gate); float y0 = baseC.y;
            float w = T * 3f, dz = T * 1.0f; float cx = baseC.x, cz = baseC.z;
            var wood = B(Wood);
            // 柱与平坐
            for (int i = 0; i < 4; i++)
                foreach (float sz in new[] { -1f, 1f })
                    B(Pillar).Box(new Vector3(cx - w * 0.42f + i * w * 0.28f, y0 + 0.4f, cz + sz * dz * 0.4f), new Vector3(0.12f, 0.8f, 0.12f), 2f);
            wood.Box(new Vector3(cx, y0 + 0.85f, cz), new Vector3(w * 0.96f, 0.1f, dz * 0.96f), 2f);
            foreach (float sz in new[] { -1f, 1f })
            {
                wood.Box(new Vector3(cx, y0 + 1.05f, cz + sz * dz * 0.47f), new Vector3(w * 0.96f, 0.05f, 0.05f), 2f);
                for (int i = 0; i <= 8; i++) wood.Box(new Vector3(cx - w * 0.47f + i * w * 0.1175f, y0 + 0.97f, cz + sz * dz * 0.47f), new Vector3(0.04f, 0.2f, 0.04f), 2f);
            }
            // 下层腰檐（重檐），上层粉墙
            SkirtRoof(new Vector3(cx, y0 + 0.95f, cz), w * 0.36f, dz * 0.31f, w * 0.56f, dz * 0.6f, 0.28f);
            B(Whitewash).Box(new Vector3(cx, y0 + 1.3f, cz), new Vector3(w * 0.72f, 0.8f, dz * 0.62f), 1.2f);
            // 庑殿顶：四坡，正脊沿 x 方向，出檐平直
            float eave = y0 + 1.66f, ridge = y0 + 2.25f, ox = w * 0.56f, oz = dz * 0.62f, rx = w * 0.30f;
            var roof = B(RoofTile);
            Vector3 A(float x, float y, float z) => new Vector3(cx + x, y, cz + z);
            Vector2 U(float x, float y) => new Vector2(x * 1.5f, y * 1.5f);
            roof.Quad(A(-ox, eave, -oz), A(-rx, ridge, 0), A(rx, ridge, 0), A(ox, eave, -oz), U(0, 0), U(0.2f, 1), U(0.8f, 1), U(1, 0));   // 南坡
            roof.Quad(A(ox, eave, oz), A(rx, ridge, 0), A(-rx, ridge, 0), A(-ox, eave, oz), U(0, 0), U(0.2f, 1), U(0.8f, 1), U(1, 0));     // 北坡
            roof.Tri(A(-ox, eave, oz), A(-rx, ridge, 0), A(-ox, eave, -oz), U(0, 0), U(0.5f, 1), U(1, 0));                                 // 西坡
            roof.Tri(A(ox, eave, -oz), A(rx, ridge, 0), A(ox, eave, oz), U(0, 0), U(0.5f, 1), U(1, 0));                                     // 东坡
            B(RoofTile).Box(A(0, ridge + 0.03f, 0), new Vector3(rx * 2 + 0.1f, 0.08f, 0.1f), 2f);                                          // 正脊
            // 屋檐下的檐枋
            wood.Box(A(0, eave - 0.05f, -oz * 0.95f), new Vector3(ox * 1.9f, 0.08f, 0.06f), 2f);
            Flag(A(-ox - 0.2f, y0 - 0.0f, -oz * 0.3f)); Flag(A(ox + 0.2f, y0 - 0.0f, -oz * 0.3f));
        }

        // 坡顶布帐：双立杆、屋脊、拉绳
        void Tent(Vector3 c)
        {
            var cloth = B(Cloth); var wood = B(Wood);
            float hw = T * 0.42f, hd = T * 0.4f, h = 0.85f;
            Vector3 A(float x, float y, float z) => c + new Vector3(x, y, z);
            Vector2 U(float x, float y) => new Vector2(x, y);
            cloth.Quad(A(-hw, 0.02f, hd), A(0, h, hd), A(0, h, -hd), A(-hw, 0.02f, -hd), U(0, 0), U(0, 1), U(1, 1), U(1, 0));
            cloth.Quad(A(hw, 0.02f, -hd), A(0, h, -hd), A(0, h, hd), A(hw, 0.02f, hd), U(0, 0), U(0, 1), U(1, 1), U(1, 0));
            cloth.Tri(A(hw, 0.02f, hd), A(0, h, hd), A(-hw, 0.02f, hd), U(0, 0), U(0.5f, 1), U(1, 0));
            B(DoorMat).Tri(A(-hw * 0.4f, 0.02f, -hd - 0.005f), A(0, h * 0.6f, -hd - 0.005f), A(hw * 0.4f, 0.02f, -hd - 0.005f), U(0, 0), U(0.5f, 1), U(1, 0));
            cloth.Tri(A(-hw, 0.02f, -hd), A(0, h, -hd), A(-hw * 0.4f, 0.02f, -hd), U(0, 0), U(0.5f, 1), U(0.3f, 0));
            cloth.Tri(A(hw * 0.4f, 0.02f, -hd), A(0, h, -hd), A(hw, 0.02f, -hd), U(0.7f, 0), U(0.5f, 1), U(1, 0));
            foreach (float s in new[] { -1f, 1f }) wood.Box(A(0, h / 2 + 0.05f, s * hd * 1.02f), new Vector3(0.05f, h + 0.1f, 0.05f), 2f);
            wood.Box(A(0, h + 0.02f, 0), new Vector3(0.05f, 0.05f, hd * 2.1f), 2f);
        }

        // 旗帜：旗杆＋背对背两片旗面，顶点按 1 像素取整飘动
        void Flag(Vector3 basePos) => Flag(basePos, FlagMat);

        void Flag(Vector3 basePos, Material face)
        {
            B(Wood).Box(basePos + Vector3.up * 1.0f, new Vector3(0.05f, 2.0f, 0.05f), 2f);
            float w = 0.6f, h = 0.9f; var o = basePos + new Vector3(0.03f, 1.05f, 0);
            var mesh = new Mesh();
            var verts = new[] { o, o + Vector3.up * h, o + new Vector3(w, h, 0), o + new Vector3(w, 0, 0), o, o + Vector3.up * h, o + new Vector3(w, h, 0), o + new Vector3(w, 0, 0) };
            mesh.vertices = verts;
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, 0) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6 };
            mesh.RecalculateNormals();
            var go = new GameObject("Flag"); go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = face;
            flags.Add((mesh, verts, Hash((int)(basePos.x * 10), (int)(basePos.z * 10), 1) * 6f));
        }

        void AnimateFlags()
        {
            foreach (var (mesh, baseVerts, phase) in flags)
            {
                var v = (Vector3[])baseVerts.Clone();
                for (int i = 0; i < v.Length; i++)
                {
                    float along = (v[i].x - baseVerts[0].x) / 0.6f;
                    float off = Mathf.Sin(Time.time * 3f + phase + along * 3f) * 0.12f * along;
                    v[i].z += Mathf.Round(off * 30f) / 30f;   // 按 1 像素取整
                }
                mesh.vertices = v;
            }
        }

        // 低面数山石：扰动的八面体细分，平面着色
        static void RockMesh(MeshBuilder b, Vector3 c, float size, int seed)
        {
            var dirs = new[] { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back,
                new Vector3(1, 1, 1).normalized, new Vector3(-1, 1, 1).normalized, new Vector3(1, 1, -1).normalized, new Vector3(-1, 1, -1).normalized };
            var pts = dirs.Select((d, i) => c + Vector3.up * size * 0.35f + Vector3.Scale(d, new Vector3(1f, 0.7f, 1f)) * size * (0.75f + 0.5f * Hash(i, seed, 2))).ToArray();
            int[] tris = { 0, 6, 7, 0, 7, 9, 0, 9, 8, 0, 8, 6, 6, 3, 4, 7, 4, 2, 9, 2, 5, 8, 5, 3, 6, 4, 7, 7, 2, 9, 9, 5, 8, 8, 3, 6,
                           1, 4, 3, 1, 2, 4, 1, 5, 2, 1, 3, 5 };
            for (int i = 0; i < tris.Length; i += 3)
            {
                var a = pts[tris[i]]; var bb = pts[tris[i + 1]]; var cc = pts[tris[i + 2]];
                // 保证朝外
                var n = Vector3.Cross(bb - a, cc - a);
                if (Vector3.Dot(n, (a + bb + cc) / 3f - (c + Vector3.up * size * 0.35f)) < 0) { var tmp = bb; bb = cc; cc = tmp; }
                b.Tri(a, bb, cc, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 1));
            }
        }

        // 地图外一圈：略低的草地，柏、槐、柳与山石
        void Scenery()
        {
            const int ring = 3;
            var top = TopMat(Terrain.Grass);
            float x0 = -ring * T, x1 = (W + ring) * T, z0 = -ring * T, z1 = (H + ring) * T;
            var b = B(LitMat("borderGrass", TopStrip(Terrain.Grass), new Color(0.82f, 0.86f, 0.80f)));
            for (int x = -ring; x < W + ring; x++)
                for (int y = -ring; y < H + ring; y++)
                {
                    if (x >= 0 && y >= 0 && x < W && y < H) continue;
                    int v = (int)(Hash(x + 50, y + 50, 3) * 4) % 4;
                    b.Quad(new Vector3(x * T, Border, y * T), new Vector3(x * T, Border, (y + 1) * T), new Vector3((x + 1) * T, Border, (y + 1) * T), new Vector3((x + 1) * T, Border, y * T),
                        new Vector2(v / 4f, 0), new Vector2(v / 4f, 1), new Vector2((v + 1) / 4f, 1), new Vector2((v + 1) / 4f, 0));
                    float h = Hash(x + 50, y + 50, 9);
                    var c = new Vector3((x + 0.5f) * T, Border, (y + 0.5f) * T);
                    if (y >= H && stage == 1) continue;   // 关墙背后留空
                    if (h > 0.72f) Tree(c, (int)(h * 100) % 3);
                    else if (h < 0.12f) { if (!Billboard(h < 0.06f ? "sprite_rock_outcrop_b" : "sprite_rock_small", c)) RockMesh(B(Stone), c, 0.35f + h, x * 13 + y); }
                    else if (h < 0.2f && y < H) Billboard(stage == 1 && h < 0.16f ? "sprite_stone_wall_low" : "sprite_fence_wood", c);   // 外圈田埂矮石墙与木栅，只作远景
                }
            // 外圈底下的土
            B(SideMat(Side.Dirt)).Quad(new Vector3(x0, Border - 0.6f, z0), new Vector3(x0, Border, z0), new Vector3(x1, Border, z0), new Vector3(x1, Border - 0.6f, z0),
                new Vector2(0, 0), new Vector2(0, 1), new Vector2((x1 - x0) / T, 1), new Vector2((x1 - x0) / T, 0));
        }

        readonly List<(Transform t, float size)> treeCrowns = new List<(Transform, float)>();
        readonly Dictionary<int, Material> treeMats = new Dictionary<int, Material>();

        // 树：六棱柱树干＋与人物同角度的树冠公告板
        void Tree(Vector3 c, int kind)
        {
            if (Billboard(kind == 0 ? "prop_tree_cypress" : kind == 1 ? "prop_tree_elm" : "prop_tree_willow", c, 1f)) return;   // 涿郡整树精灵
            var trunk = B(Wood);
            float th = kind == 0 ? 0.9f : 0.7f;
            for (int i = 0; i < 6; i++)
            {
                float a0 = i * 60f * Mathf.Deg2Rad, a1 = (i + 1) * 60f * Mathf.Deg2Rad; float r = 0.09f;
                var p0 = c + new Vector3(Mathf.Cos(a0) * r, 0, Mathf.Sin(a0) * r); var p1 = c + new Vector3(Mathf.Cos(a1) * r, 0, Mathf.Sin(a1) * r);
                trunk.Quad(p0, p0 + Vector3.up * th, p1 + Vector3.up * th, p1, new Vector2(0, 0), new Vector2(0, 2), new Vector2(0.3f, 2), new Vector2(0.3f, 0));
            }
            if (!treeMats.TryGetValue(kind, out var m))
            {
                var tex = Env(kind == 0 ? "sprite_tree_cypress" : kind == 1 ? "sprite_tree_locust" : "sprite_tree_willow") ?? TreeTexture(kind);
                m = new Material(LiuBeiMat) { mainTexture = tex };
                m.SetTexture("_BaseMap", tex); m.SetVector("_BaseMap_ST", new Vector4(1, 1, 0, 0));
                treeMats[kind] = m;
            }
            var crown = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(crown.GetComponent<Collider>());
            crown.name = "Tree crown";
            float size = kind == 0 ? 2.2f : 2.6f;
            crown.transform.position = c + Vector3.up * (th + size * 0.32f);
            crown.transform.localScale = new Vector3(size * 0.67f, size, 1);
            var cr = crown.GetComponent<MeshRenderer>(); cr.sharedMaterial = m; cr.shadowCastingMode = ShadowCastingMode.On;
            treeCrowns.Add((crown.transform, size));
        }

        // 树冠：0 柏（深绿、竖长），1 槐（团状），2 柳（下垂枝条）
        Texture2D TreeTexture(int kind)
        {
            const int w = 60, h = 90;
            var t = Paint(w, h, (x, y) =>
            {
                float cx = (x - w / 2f) / (w / 2f), cy = (y - h * 0.55f) / (h * 0.45f);
                float n = Hash(x / 2, y / 2, kind + 20);
                bool inside;
                if (kind == 0) inside = Mathf.Abs(cx) < (1f - Mathf.Abs(cy)) * 0.75f + 0.1f * n && y > 10;
                else if (kind == 1) inside = cx * cx + cy * cy * 1.3f < 0.75f + 0.25f * n && y > 18;
                else inside = (cx * cx + (cy - 0.3f) * (cy - 0.3f) * 2f < 0.5f + 0.2f * n || (Mathf.Abs(cx) < 0.9f && y > 14 && y < h * 0.6f && (x % 5 < 2) && n > 0.35f)) && y > 12;
                if (!inside) return new Color(0, 0, 0, 0);
                var dark = kind == 0 ? new Color(0.13f, 0.27f, 0.18f) : kind == 1 ? new Color(0.20f, 0.36f, 0.18f) : new Color(0.32f, 0.48f, 0.22f);
                var lite = kind == 0 ? new Color(0.24f, 0.40f, 0.26f) : kind == 1 ? new Color(0.36f, 0.54f, 0.26f) : new Color(0.50f, 0.64f, 0.30f);
                return Color.Lerp(dark, lite, Mathf.Clamp01(n * 0.8f + (cy + 1f) * 0.25f - (cx > 0 ? 0.15f : 0f)));
            });
            t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        void UpdateScenery()
        {
            foreach (var (t, _) in treeCrowns) t.rotation = cam.transform.rotation;
            AnimateFlags();
            UpdateBillboards();
            if (waterMat != null)
            {
                int f = (int)(Time.time / 0.25f) % 4;
                waterMat.mainTexture = waterFrames[f];
                waterMat.SetTexture("_BaseMap", waterFrames[f]);
            }
        }

        Texture2D[] waterFrames;

        // 贴图包的河水是 4 帧横排；把第 f 帧重复 4 次拼成与地面相同的“4 变体”条带
        static Texture2D WaterStrip(Texture2D src, int f)
        {
            int n = src.height;
            var px = src.GetPixels(f * n, 0, n, n);
            var t = new Texture2D(n * 4, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            for (int k = 0; k < 4; k++) t.SetPixels(k * n, 0, n, n, px);
            t.Apply(false, false);
            return t;
        }

        void BuildWorld3D()
        {
            var wt = Env("top_water");
            waterFrames = wt && wt.isReadable && wt.width == wt.height * 4
                ? Enumerable.Range(0, 4).Select(f => WaterStrip(wt, f)).ToArray()
                : Enumerable.Range(0, 4).Select(f => TopStrip(Terrain.River, f)).ToArray();
            waterMat = LitMat("water", waterFrames[0]);
            BuildTerrain();
            BuildProps();
            // 地形碰撞：每格一个盒体，只用于鼠标拾取，不渲染
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    var p = new Vector2Int(x, y);
                    float top = TopOf(p);
                    var go = new GameObject($"Pick {x},{y}");
                    go.transform.position = new Vector3((x + 0.5f) * T, (top + Border) / 2f, (y + 0.5f) * T);
                    var col = go.AddComponent<BoxCollider>(); col.size = new Vector3(T, Mathf.Max(0.05f, top - Border), T);
                    tileOf[col] = p;
                }
            Debug.Log($"POC_WORLD_TRIANGLES {trianglesBuilt}");
        }
    }
}
