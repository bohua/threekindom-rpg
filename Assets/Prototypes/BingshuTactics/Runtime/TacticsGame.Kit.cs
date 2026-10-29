// 虎牢关高清翻新（用户 2026-10-26：“可以用安溪县使用的美术技巧来翻新虎牢关战场”）。
// 照安喜县的做法：获批效果图（art/v03-tactics-concept/T8_hulao_gate.png）→ Codex 每米 120 像素的高清手绘贴图与道具
// （art/hulao-hd）→ 地面整张融合烘焙、断崖／河岸／关墙立面按世界坐标连续铺、关门城楼与角楼用立面贴图、道具用高清精灵。
// 一张战场一套“高清套件”（HdKit），名称带前缀，缺图时全部退回原有画法。
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Prototype.BingshuTactics
{
    public sealed partial class TacticsGame
    {
        // 由主线的战棋场景构建脚本填入（例如虎牢关：art/hulao-hd）
        public Texture2D[] HdKit = new Texture2D[0];
        const float KitPxPerM = 120f;      // 高清套件统一每米 120 像素
        const float KitScale = 0.55f;      // 道具按真实米数画，本战场人物缩了一半，与涿郡道具同比缩小

        Texture2D Kit(string n) => HdKit?.FirstOrDefault(x => x && x.name == n);
        bool HulaoKit => IsChapter && Kit("hd_hulao_grass");

        // 一张贴图对应多少米（套件按每米 120 像素，其余按 ZTile）
        float MetersOf(Texture2D t) => t && HdKit != null && HdKit.Contains(t) ? t.width / KitPxPerM : ZTile;

        Material KitMat(string n)
        {
            var t = Kit(n);
            return t ? LitMat("kit_" + n, t) : null;
        }

        Material KitClip(string n)
        {
            if (spriteMats.TryGetValue("kit_" + n, out var m)) return m;
            var t = Kit(n);
            if (!t) return null;
            m = new Material(LiuBeiMat) { name = "kit_" + n, mainTexture = t };
            m.SetTexture("_BaseMap", t); m.SetVector("_BaseMap_ST", new Vector4(1, 1, 0, 0));
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", (float)CullMode.Off);
            spriteMats["kit_" + n] = m;
            return m;
        }

        // 高清精灵：按贴图的真实米数（每米 120 像素）乘道具比例
        Renderer KitBillboard(string n, Vector3 foot, float scale = 1f, bool shadow = true)
        {
            var t = Kit(n);
            if (!t) return null;
            var m = KitClip(n);
            float w = t.width / KitPxPerM * KitScale * scale, h = t.height / KitPxPerM * KitScale * scale;
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.name = "Kit " + n;
            var pivot = new GameObject("BB").transform; pivot.position = foot;
            q.transform.SetParent(pivot, false);
            q.transform.localPosition = new Vector3(0, h / 2f, 0); q.transform.localScale = new Vector3(w, h, 1);
            var r = q.GetComponent<MeshRenderer>(); r.sharedMaterial = m;
            r.shadowCastingMode = shadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            billboards.Add(pivot);
            return r;
        }

        // 地面融合用的高清地块
        Texture2D KitGround(Ground g) => g switch
        {
            Ground.Road => Kit("hd_hulao_road"),
            Ground.Sand => Kit("hd_hulao_loess"),
            Ground.Gravel => Kit("hd_hulao_gravel"),
            Ground.Water => Kit("hd_hulao_water"),
            _ => Kit("hd_hulao_grass"),
        };

        // 侧面：断崖、河岸、关墙立面，按世界坐标连续铺（宽高取贴图真实米数）
        bool KitSide(Vector2Int p, Vector2Int d, Vector3 a, Vector3 b, Vector3 ab, Vector3 at, Vector3 bt, Vector3 bb, float yb, float y, bool nbWater)
        {
            if (!HulaoKit) return false;
            string n = IsWallish(p) ? "wall_hulao_face" : nbWater && yb < 0.01f ? "bank_hulao" : "cliff_hulao_rock";
            var t = Kit(n);
            if (!t) return false;
            float mw = t.width / KitPxPerM, mh = t.height / KitPxPerM;
            float ua = (d.y != 0 ? a.x : a.z) / mw, ub = (d.y != 0 ? b.x : b.z) / mw;
            // 断崖：顶边对齐台地顶面，往下铺；关墙与河岸：底边对齐地面
            float top = TopOf(p);
            float va = IsWallish(p) || n == "bank_hulao" ? yb / mh : 1f - (top - yb) / mh, vb = IsWallish(p) || n == "bank_hulao" ? y / mh : 1f - (top - y) / mh;
            B(KitMat(n)).Quad(ab, at, bt, bb, new Vector2(ua, va), new Vector2(ua, vb), new Vector2(ub, vb), new Vector2(ub, va));
            return true;
        }

        // 墙顶墙道与桥面
        Material KitTop(Terrain t) => !HulaoKit ? null : t switch
        {
            Terrain.Wall or Terrain.Gate => KitMat("wall_hulao_walkway"),
            Terrain.Bridge => KitMat("bridge_hulao_deck"),
            _ => null
        };

        float KitTopMeters(Terrain t) => MetersOf(t == Terrain.Bridge ? Kit("bridge_hulao_deck") : Kit("wall_hulao_walkway"));

        // 竖立的连续条带（垛口、营寨木栅、石桥栏板）：沿一条边铺，双面
        void KitStrip(string n, Vector3 a0, Vector3 a1, float baseY, float scale = 1f)
        {
            var t = Kit(n); var m = KitClip(n);
            if (!t || !m) return;
            float mw = t.width / KitPxPerM * KitScale * scale, h = t.height / KitPxPerM * KitScale * scale;
            var along = a1 - a0; along.y = 0;
            float u0 = (Mathf.Abs(along.x) > Mathf.Abs(along.z) ? a0.x : a0.z) / mw, u1 = u0 + along.magnitude / mw;
            Vector3 p0 = new Vector3(a0.x, baseY, a0.z), p1 = new Vector3(a1.x, baseY, a1.z);
            B(m).Quad(p0, p0 + Vector3.up * h, p1 + Vector3.up * h, p1, new Vector2(u0, 0), new Vector2(u0, 1), new Vector2(u1, 1), new Vector2(u1, 0));
        }

        // 按地形放的高清道具；返回 true 表示已用高清件代替原画法
        bool KitProp(Vector2Int p, Terrain t, Vector3 c)
        {
            if (!HulaoKit) return false;
            float h = Hash(p.x, p.y, 41), h2 = Hash(p.x, p.y, 42);
            var jitter = new Vector3((h - 0.5f) * 0.3f, 0, (h2 - 0.5f) * 0.3f);
            switch (t)
            {
                case Terrain.JuMa: return KitBillboard("juma_hulao", c, 0.75f);
                case Terrain.Rock: return KitBillboard(h < 0.5f ? "rock_hulao_a" : "rock_hulao_b", c + jitter, h < 0.5f ? 0.55f : 0.8f);
                case Terrain.Woods:
                    KitBillboard("pine_hulao", c + jitter, 0.85f);
                    if (h2 > 0.5f) KitBillboard("shrub_hulao", c - jitter * 1.6f, 0.7f);
                    return true;
                case Terrain.Camp:
                    foreach (var d in Dirs4)
                    {
                        var nb = p + d;
                        if (grid.In(nb) && (grid.TerrainAt(nb) == Terrain.Camp || IsWallish(nb))) continue;
                        var along = new Vector3(Mathf.Abs(d.y), 0, Mathf.Abs(d.x)); var edge = c + new Vector3(d.x, 0, d.y) * T * 0.47f;
                        KitStrip("palisade_hulao", edge - along * (T * 0.5f), edge + along * (T * 0.5f), c.y);
                    }
                    if (h < 0.3f) KitBillboard("tent_hulao", c + new Vector3(0, 0, 0.15f), 0.8f);
                    else if (h < 0.5f) KitBillboard("weapon_rack_hulao", c + jitter, 0.75f);
                    else if (h < 0.7f) KitBillboard("supplies_hulao", c + jitter, 0.8f);
                    return true;
                case Terrain.Wall:
                    foreach (var d in Dirs4)
                    {
                        var nb = p + d;
                        if (TopOrBorder(nb) >= TopOf(p) - 0.01f) continue;
                        var along = new Vector3(Mathf.Abs(d.y), 0, Mathf.Abs(d.x)); var edge = c + new Vector3(d.x, 0, d.y) * (T * 0.5f - WallInset - 0.02f);
                        KitStrip("wall_hulao_crenel", edge - along * (T * 0.5f), edge + along * (T * 0.5f), c.y);
                    }
                    return true;
                case Terrain.Bridge:
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var e = c + new Vector3(side * T * 0.47f, 0, 0);
                        KitStrip("bridge_hulao_parapet", e - new Vector3(0, 0, T * 0.5f), e + new Vector3(0, 0, T * 0.5f), c.y);
                    }
                    return true;
            }
            return false;
        }

        // 关门城楼、角楼、军旗、火盆、远山
        void KitLandmarks()
        {
            if (!HulaoKit) return;
            Vector2Int? gate = null;
            for (int x = 0; x < W && gate == null; x++)
                for (int y = 0; y < H; y++)
                    if (grid.TerrainAt(new Vector2Int(x, y)) == Terrain.Gate) { gate = new Vector2Int(x, y); break; }
            if (gate.HasValue)
            {
                var g = gate.Value; var gc = Tile(g);
                float zFront = g.y * T + WallInset - 0.04f;   // 关墙南面
                var front = Kit("gate_hulao_front"); var side = Kit("gate_hulao_side");
                if (front)
                {
                    float fw = front.width / KitPxPerM * KitScale, fh = front.height / KitPxPerM * KitScale;
                    float fd = side ? side.width / KitPxPerM * KitScale : fw * 0.5f;
                    Vector3 P(float x, float y, float z) => new Vector3(gc.x + x, y, zFront + z);
                    float hx = fw / 2f;
                    var fm = KitClip("gate_hulao_front");
                    B(fm).Quad(P(-hx, 0, 0), P(-hx, fh, 0), P(hx, fh, 0), P(hx, 0, 0), new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
                    B(fm).Quad(P(hx, 0, fd), P(hx, fh, fd), P(-hx, fh, fd), P(-hx, 0, fd), new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
                    if (side)
                    {
                        var sm = KitClip("gate_hulao_side");
                        B(sm).Quad(P(-hx, 0, fd), P(-hx, fh, fd), P(-hx, fh, 0), P(-hx, 0, 0), new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
                        B(sm).Quad(P(hx, 0, 0), P(hx, fh, 0), P(hx, fh, fd), P(hx, 0, fd), new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
                    }
                    // 立面里的屋顶从上往下看是空的：用青灰瓦四坡顶封上，楼身用实心块挡住立面之间的空隙
                    float ey = fh * 0.66f, ry = fh * 0.97f;
                    var roof = B(RoofTile);
                    float ox = hx * 0.86f, oz = fd * 0.5f, rx = hx * 0.38f, cz = fd * 0.5f;
                    roof.Quad(P(-ox, ey, cz - oz), P(-rx, ry, cz), P(rx, ry, cz), P(ox, ey, cz - oz), new Vector2(0, 0), new Vector2(0.2f, 1), new Vector2(0.8f, 1), new Vector2(1, 0));
                    roof.Quad(P(ox, ey, cz + oz), P(rx, ry, cz), P(-rx, ry, cz), P(-ox, ey, cz + oz), new Vector2(0, 0), new Vector2(0.2f, 1), new Vector2(0.8f, 1), new Vector2(1, 0));
                    roof.Tri(P(-ox, ey, cz + oz), P(-rx, ry, cz), P(-ox, ey, cz - oz), new Vector2(0, 0), new Vector2(0.5f, 1), new Vector2(1, 0));
                    roof.Tri(P(ox, ey, cz - oz), P(rx, ry, cz), P(ox, ey, cz + oz), new Vector2(0, 0), new Vector2(0.5f, 1), new Vector2(1, 0));
                    B(KitMat("wall_hulao_face") ?? Stone).Box(P(0, ey * 0.5f, fd * 0.5f), new Vector3(fw * 0.82f, ey, fd * 0.8f), 1f);
                }
                // 门前两侧火盆（火焰沿用原型的火焰帧）
                foreach (float s in new[] { -1f, 1f })
                {
                    var foot = Tile(new Vector2Int(Mathf.Clamp(g.x + (s < 0 ? -2 : 2), 0, W - 1), Mathf.Max(0, g.y - 1))) + new Vector3(0, 0, T * 0.35f);
                    if (KitBillboard("brazier_hulao", foot, 0.7f))
                    {
                        Billboard("fx_fire", foot + Vector3.up * 0.75f, 1f, 6, additive: true, shadow: false);
                        var l = new GameObject("Brazier light").AddComponent<Light>();
                        l.type = LightType.Point; l.color = new Color(1f, 0.62f, 0.30f); l.range = 3.2f; l.intensity = 1.2f; l.shadows = LightShadows.None;
                        l.transform.position = foot + Vector3.up * 0.9f;
                    }
                }
                // 墙两端角楼与墙上军旗（呂、董相间）
                int wy = g.y;
                foreach (int x in new[] { 0, W - 1 })
                    KitBillboard("tower_hulao_corner", Tile(new Vector2Int(x, wy)) + new Vector3(0, 0, 0.1f), 0.9f);
                int k = 0;
                for (int x = 2; x < W - 2; x += 3)
                {
                    if (Mathf.Abs(x - g.x) < 3) continue;
                    KitBillboard(k++ % 2 == 0 ? "banner_hulao_lu" : "banner_hulao_dong", Tile(new Vector2Int(x, wy)) + new Vector3(0, 0, T * 0.25f), 0.6f);
                }
            }
            // 关后远山：一张大公告板立在关墙北面远处
            var far = Kit("mountains_hulao_far");
            if (far)
            {
                float w = 90f, h = w * far.height / far.width;
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(q.GetComponent<Collider>());
                q.name = "Kit far mountains";
                var pivot = new GameObject("BB").transform; pivot.position = new Vector3(W * T / 2f, -2f, H * T + 28f);
                q.transform.SetParent(pivot, false);
                q.transform.localPosition = new Vector3(0, h / 2f, 0); q.transform.localScale = new Vector3(w, h, 1);
                var r = q.GetComponent<MeshRenderer>(); r.sharedMaterial = KitClip("mountains_hulao_far"); r.shadowCastingMode = ShadowCastingMode.Off;
                billboards.Add(pivot);
            }
        }
    }
}
