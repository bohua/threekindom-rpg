// PROTOTYPE - NOT FOR PRODUCTION
// Question: 空轨式 AT 行动条＋小范围格子战斗，配合“学派回路镶嵌兵书→策略”，是否好玩、是否看得懂？
// Date: 2026-10-24
// 战场细节按 Codex 效果图补全（art/battle-poc-hulao-keyart，用户 2026-10-24 认可方向）：
// 更高的关墙、重檐城楼、两侧角楼、营寨军需（兵器架、粮车、木箱粮袋）、火盆与篝火、卵石岸、灌木草丛与野花。
// 贴图优先取 Codex 贴图包（art/battle-poc-hulao-texpack），缺图时沿用程序贴图。
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Prototype.BingshuTactics
{
    public sealed partial class TacticsGame
    {
        // 由编辑器构建脚本填入（名称不含扩展名）
        public Texture2D[] EnvTextures = new Texture2D[0];
        Dictionary<string, Texture2D> env;

        // 关墙在规则上仍是 3 层（高差加成不变），视觉上再加高 1.5 米，接近效果图的 4.5 米以上
        const float WallExtra = 1.5f;

        Texture2D Env(string name)
        {
            if (env == null) env = EnvTextures.Where(t => t).ToDictionary(t => t.name, t => t);
            return env.TryGetValue(name, out var t) ? t : null;
        }

        // 程序材质键 → 贴图包文件名
        Texture2D EnvOverride(string key) => key switch
        {
            "topGrass" or "borderGrass" => Env("top_grass"), "topRoad" => Env("top_road"), "topSand" => Env("top_sand"),
            "topCamp" => Env("top_camp"), "topFord" => Env("top_ford"), "topBridge" => Env("top_bridge"),
            "topWall" or "topGate" => Env("top_walltop"),
            "sideGrassLip" => Env("side_grasslip"), "sideDirt" => Env("side_cliff"), "sideStoneBase" => Env("side_stonebase"),
            "sideWetBank" => Env("side_wetbank"), "sideRammed" => Env("side_rammed"),
            "wood" => Env("wood_plank"), "log" => Env("wood_log"), "cloth" => Env("tent_canvas"), "roof" => Env("roof_tile"),
            "whitewash" => Env("plaster_frame"), "door" => Env("gate_door"), "flagLu" => Env("banner_lu"), "flagDong" => Env("banner_dong"),
            "pillar" => Env("pillar_red"), "crate" => Env("crate"), "sack" => Env("sack"),
            "stone" => Env("rock_boulder") ?? Env("side_stonebase"),
            _ => null
        };

        Material LogWood => LitMat("log", Paint(16, 16, (x, y) =>
        {
            var c = Color.Lerp(new Color(0.42f, 0.30f, 0.20f), new Color(0.32f, 0.22f, 0.14f), Hash(x, y / 4, 14));
            if (y % 6 == 0) c *= 0.85f; c.a = 1; return c;
        }));
        Material Pillar => LitMat("pillar", Paint(8, 16, (x, y) => { var c = new Color(0.62f, 0.20f, 0.14f) * (x < 2 ? 0.8f : 1f); c.a = 1; return c; }));
        Material CrateMat => LitMat("crate", Paint(16, 16, (x, y) => { var c = (x % 15 == 0 || y % 15 == 0 || x == y) ? new Color(0.32f, 0.22f, 0.13f) : new Color(0.55f, 0.40f, 0.24f); c.a = 1; return c; }));
        Material SackMat => LitMat("sack", Paint(16, 16, (x, y) => { var c = Color.Lerp(new Color(0.78f, 0.68f, 0.50f), new Color(0.66f, 0.56f, 0.40f), Hash(x, y, 21)); c.a = 1; return c; }));
        Material FlagDongMat => LitMat("flagDong", FlagMat.mainTexture as Texture2D);

        // ---------------- 公告板精灵（树、灌木、草丛、兵器架、粮车、火盆） ----------------
        readonly List<Transform> billboards = new List<Transform>();
        readonly List<(Renderer r, int frames, float phase)> animSprites = new List<(Renderer, int, float)>();
        readonly Dictionary<string, Material> spriteMats = new Dictionary<string, Material>();

        Material SpriteMat(string name, bool additive = false)
        {
            if (spriteMats.TryGetValue(name, out var m)) return m;
            var t = Env(name);
            if (!t) return null;
            m = additive ? new Material(OverlayMat) : new Material(LiuBeiMat);
            m.mainTexture = t; m.SetTexture("_BaseMap", t); m.SetVector("_BaseMap_ST", new Vector4(1, 1, 0, 0));
            spriteMats[name] = m;
            return m;
        }

        // 以脚底为锚点、面朝镜头的公告板；高度按每米 30 像素换算
        Renderer Billboard(string name, Vector3 foot, float scale = 1f, int frames = 1, bool additive = false, bool shadow = true)
        {
            var m = SpriteMat(name, additive);
            if (!m) return null;
            var t = m.mainTexture;
            float w = t.width / (float)frames / 30f * scale, h = t.height / 30f * scale;
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.name = "Billboard " + name;
            var pivot = new GameObject("BB").transform; pivot.position = foot;
            q.transform.SetParent(pivot, false);
            q.transform.localPosition = new Vector3(0, h / 2f, 0); q.transform.localScale = new Vector3(w, h, 1);
            var r = q.GetComponent<MeshRenderer>(); r.sharedMaterial = m;
            r.shadowCastingMode = shadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (frames > 1)
            {
                r.GetPropertyBlock(block); block.SetVector("_BaseMap_ST", new Vector4(1f / frames, 1, 0, 0)); block.SetVector("_MainTex_ST", new Vector4(1f / frames, 1, 0, 0)); r.SetPropertyBlock(block);
                animSprites.Add((r, frames, Hash((int)(foot.x * 7), (int)(foot.z * 7), 3)));
            }
            billboards.Add(pivot);
            return r;
        }

        void UpdateBillboards()
        {
            foreach (var b in billboards) if (b) b.rotation = cam.transform.rotation;
            foreach (var (r, frames, phase) in animSprites)
            {
                int f = (int)((Time.time + phase) * 10f) % frames;
                r.GetPropertyBlock(block);
                var st = new Vector4(1f / frames, 1, f / (float)frames, 0);
                block.SetVector("_BaseMap_ST", st); block.SetVector("_MainTex_ST", st);
                r.SetPropertyBlock(block);
            }
        }

        // ---------------- 细节 ----------------
        void BuildDetails()
        {
            if (stage != 1) { ScatterFoliage(); return; }
            // 两侧角楼（关墙两端）与城楼两侧的望楼平台
            CornerTower(new Vector2Int(0, 12));
            CornerTower(new Vector2Int(13, 12));
            // 火盆：关门两侧、两座营寨
            foreach (var p in new[] { new Vector2Int(6, 11), new Vector2Int(8, 11) }) Brazier(Tile(p) + new Vector3(p.x < 7 ? 0.35f : -0.35f, 0, 0.3f));
            Brazier(Tile(new Vector2Int(1, 11)) + new Vector3(0, 0, -0.25f));
            Brazier(Tile(new Vector2Int(13, 10)) + new Vector3(0.3f, 0, 0.3f));
            // 营寨军需（营寨格：y=11 的 0–2、12–13，y=10 的 0、2、13；西凉铁骑站 (1,10)(12,10)）
            Tent(Tile(new Vector2Int(2, 11)));
            Tent(Tile(new Vector2Int(12, 11)));
            foreach (var (p, off) in new[] { (new Vector2Int(0, 10), new Vector3(-0.3f, 0, 0.25f)), (new Vector2Int(13, 10), new Vector3(0.25f, 0, -0.3f)) })
                if (!Billboard("sprite_weapon_rack", Tile(p) + off)) WeaponRack(Tile(p) + off);
            foreach (var (p, off) in new[] { (new Vector2Int(2, 10), new Vector3(0.15f, 0, -0.3f)) })
                if (!Billboard("sprite_supply_cart", Tile(p) + off)) Cart(Tile(p) + off);
            foreach (var (p, off) in new[] { (new Vector2Int(0, 11), new Vector3(0.1f, 0, 0.35f)), (new Vector2Int(13, 11), new Vector3(-0.1f, 0, 0.35f)), (new Vector2Int(0, 10), new Vector3(0.3f, 0, -0.3f)) })
                Supplies(Tile(p) + off, p.x * 5 + p.y);
            // 关墙上加旗（呂、董相间）
            int k = 0;
            foreach (int x in new[] { 2, 5, 9, 12 })
            {
                var wp = Tile(new Vector2Int(x, 12)) + new Vector3(0, 0, T * 0.2f);
                Flag(wp, (k++ % 2 == 0) ? FlagMat : FlagDongMat);
            }
            ScatterFoliage();
        }

        void ScatterFoliage()
        {
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    var p = new Vector2Int(x, y); var t = grid.TerrainAt(p);
                    float h = Hash(x, y, 77), h2 = Hash(x, y, 78);
                    var c = Tile(p) + new Vector3((h - 0.5f) * T * 0.7f, 0, (h2 - 0.5f) * T * 0.7f);
                    switch (t)
                    {
                        case Terrain.Grass:
                            if (h < 0.18f) Billboard(h < 0.06f ? "sprite_bush_a" : h < 0.12f ? "sprite_bush_b" : "sprite_bush_c", c, 1f);
                            else if (h < 0.55f) Billboard(h2 < 0.5f ? "sprite_grass_tuft_a" : "sprite_grass_tuft_b", c, 1f, shadow: false);
                            else if (h < 0.65f) Billboard("sprite_flowers", c, 1f, shadow: false);
                            break;
                        case Terrain.Sand:
                            if (h < 0.15f) Billboard("sprite_grass_tuft_a", c, 1f, shadow: false);
                            break;
                        case Terrain.Ford:
                            Billboard("sprite_pebbles", c, 1f, shadow: false);
                            break;
                        case Terrain.River:
                            // 河岸卵石：挨着陆地的河格
                            if (Dirs4.Any(d => grid.In(p + d) && grid.TerrainAt(p + d) != Terrain.River && grid.TerrainAt(p + d) != Terrain.Ford) && h < 0.6f)
                                Billboard("sprite_pebbles", Tile(p) + new Vector3((h - 0.5f) * T * 0.6f, -0.12f, (h2 - 0.5f) * T * 0.4f), 1f, shadow: false);
                            if (h > 0.8f) Billboard("sprite_grass_tuft_b", c + Vector3.down * 0.1f, 1f, shadow: false);
                            break;
                        case Terrain.Camp:
                            if (h < 0.25f) Billboard("sprite_grass_tuft_a", c, 1f, shadow: false);
                            break;
                    }
                }
        }

        // 角楼：夯土墩台上的木构望楼，四柱、平坐栏杆、四坡顶
        void CornerTower(Vector2Int p)
        {
            var b = Tile(p); float y0 = b.y; float w = T * 0.95f;
            var wood = B(Wood);
            B(SideMat(Side.Rammed)).Box(new Vector3(b.x, y0 + 0.3f, b.z), new Vector3(w * 1.05f, 0.6f, w * 1.05f), 1.5f);
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    B(Pillar).Box(new Vector3(b.x + sx * w * 0.38f, y0 + 0.6f + 0.55f, b.z + sz * w * 0.38f), new Vector3(0.09f, 1.1f, 0.09f), 2f);
            wood.Box(new Vector3(b.x, y0 + 0.65f, b.z), new Vector3(w, 0.08f, w), 2f);
            foreach (float sz in new[] { -1f, 1f }) wood.Box(new Vector3(b.x, y0 + 0.9f, b.z + sz * w * 0.46f), new Vector3(w, 0.05f, 0.05f), 2f);
            foreach (float sx in new[] { -1f, 1f }) wood.Box(new Vector3(b.x + sx * w * 0.46f, y0 + 0.9f, b.z), new Vector3(0.05f, 0.05f, w), 2f);
            HipRoof(new Vector3(b.x, y0 + 1.7f, b.z), w * 0.62f, w * 0.62f, 0.45f, w * 0.12f);
        }

        // 四坡顶：eave 为屋檐中心高度，hx/hz 为出檐半宽，rh 为屋脊高出屋檐的高度，rx 为正脊半长
        void HipRoof(Vector3 eaveCenter, float hx, float hz, float rh, float rx)
        {
            var roof = B(RoofTile);
            Vector3 A(float x, float y, float z) => eaveCenter + new Vector3(x, y, z);
            Vector2 U(float x, float y) => new Vector2(x * 1.5f, y * 1.5f);
            roof.Quad(A(-hx, 0, -hz), A(-rx, rh, 0), A(rx, rh, 0), A(hx, 0, -hz), U(0, 0), U(0.2f, 1), U(0.8f, 1), U(1, 0));
            roof.Quad(A(hx, 0, hz), A(rx, rh, 0), A(-rx, rh, 0), A(-hx, 0, hz), U(0, 0), U(0.2f, 1), U(0.8f, 1), U(1, 0));
            roof.Tri(A(-hx, 0, hz), A(-rx, rh, 0), A(-hx, 0, -hz), U(0, 0), U(0.5f, 1), U(1, 0));
            roof.Tri(A(hx, 0, -hz), A(rx, rh, 0), A(hx, 0, hz), U(0, 0), U(0.5f, 1), U(1, 0));
            if (rx > 0.01f) roof.Box(A(0, rh + 0.03f, 0), new Vector3(rx * 2 + 0.08f, 0.07f, 0.08f), 2f);
        }

        // 一圈腰檐（重檐城楼的下层屋檐）：四片向外下斜的瓦面
        void SkirtRoof(Vector3 c, float ix, float iz, float ox, float oz, float drop)
        {
            var roof = B(RoofTile);
            Vector3 A(float x, float y, float z) => c + new Vector3(x, y, z);
            Vector2 U(float x, float y) => new Vector2(x * 1.5f, y * 1.5f);
            roof.Quad(A(-ox, -drop, -oz), A(-ix, 0, -iz), A(ix, 0, -iz), A(ox, -drop, -oz), U(0, 0), U(0, 1), U(1, 1), U(1, 0));
            roof.Quad(A(ox, -drop, oz), A(ix, 0, iz), A(-ix, 0, iz), A(-ox, -drop, oz), U(0, 0), U(0, 1), U(1, 1), U(1, 0));
            roof.Quad(A(-ox, -drop, oz), A(-ix, 0, iz), A(-ix, 0, -iz), A(-ox, -drop, -oz), U(0, 0), U(0, 1), U(1, 1), U(1, 0));
            roof.Quad(A(ox, -drop, -oz), A(ix, 0, -iz), A(ix, 0, iz), A(ox, -drop, oz), U(0, 0), U(0, 1), U(1, 1), U(1, 0));
        }

        // 火盆：铁三脚盆＋火焰动画＋暖色点光
        void Brazier(Vector3 foot)
        {
            if (!Billboard("sprite_brazier", foot))
            {
                B(Stone).Box(foot + Vector3.up * 0.25f, new Vector3(0.08f, 0.5f, 0.08f), 2f);
                B(Stone).Box(foot + Vector3.up * 0.52f, new Vector3(0.32f, 0.08f, 0.32f), 2f);
            }
            var fire = Billboard("fx_fire", foot + Vector3.up * 0.5f, 1f, 6, additive: true, shadow: false);
            if (!fire)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(q.GetComponent<Collider>());
                var pivot = new GameObject("BB").transform; pivot.position = foot + Vector3.up * 0.55f; q.transform.SetParent(pivot, false);
                q.transform.localPosition = Vector3.up * 0.2f; q.transform.localScale = new Vector3(0.3f, 0.4f, 1);
                var r = q.GetComponent<MeshRenderer>(); r.sharedMaterial = new Material(OverlayMat) { color = new Color(1f, 0.55f, 0.15f, 0.9f), mainTexture = disc };
                r.shadowCastingMode = ShadowCastingMode.Off; billboards.Add(pivot);
            }
            Billboard("fx_smoke", foot + Vector3.up * 1.0f, 1.2f, 4, shadow: false);
            var l = new GameObject("Brazier light").AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, 0.62f, 0.30f); l.range = 3.2f; l.intensity = 1.6f; l.shadows = LightShadows.None;
            l.transform.position = foot + Vector3.up * 0.9f;
        }

        void WeaponRack(Vector3 c)
        {
            var wood = B(Wood);
            foreach (float s in new[] { -1f, 1f }) wood.Box(c + new Vector3(s * 0.3f, 0.35f, 0), new Vector3(0.06f, 0.7f, 0.06f), 2f);
            wood.Box(c + new Vector3(0, 0.6f, 0), new Vector3(0.66f, 0.05f, 0.05f), 2f);
            for (int i = -2; i <= 2; i++) B(Stone).Box(c + new Vector3(i * 0.12f, 0.55f, 0.05f), new Vector3(0.025f, 1.1f, 0.025f), 2f, Quaternion.Euler(-12f, 0, 0));
        }

        void Cart(Vector3 c)
        {
            var wood = B(Wood);
            wood.Box(c + new Vector3(0, 0.42f, 0), new Vector3(0.9f, 0.12f, 0.5f), 2f);
            foreach (float s in new[] { -1f, 1f }) wood.Box(c + new Vector3(0, 0.3f, s * 0.3f), new Vector3(0.42f, 0.42f, 0.05f), 2f, Quaternion.Euler(0, 0, 45f));
            B(SackMat).Box(c + new Vector3(-0.15f, 0.6f, 0), new Vector3(0.3f, 0.22f, 0.36f), 2f);
            B(SackMat).Box(c + new Vector3(0.18f, 0.58f, 0.02f), new Vector3(0.26f, 0.18f, 0.32f), 2f);
            wood.Box(c + new Vector3(0.75f, 0.35f, 0), new Vector3(0.7f, 0.04f, 0.04f), 2f, Quaternion.Euler(0, 0, -12f));
        }

        void Supplies(Vector3 c, int seed)
        {
            float h = Hash(seed, 3, 5);
            B(CrateMat).Box(c + new Vector3(-0.25f, 0.16f, 0.2f), new Vector3(0.32f, 0.32f, 0.32f), 3f, Quaternion.Euler(0, h * 40f, 0));
            B(CrateMat).Box(c + new Vector3(-0.22f, 0.44f, 0.18f), new Vector3(0.26f, 0.24f, 0.26f), 3f, Quaternion.Euler(0, -h * 30f, 0));
            B(SackMat).Box(c + new Vector3(0.2f, 0.11f, 0.25f), new Vector3(0.3f, 0.22f, 0.24f), 3f, Quaternion.Euler(0, 20f, 0));
        }
    }
}
