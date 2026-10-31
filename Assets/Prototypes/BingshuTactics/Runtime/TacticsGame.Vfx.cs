// 战棋特效与专属技能特写（共享目录 design/hit-feel/BATTLE_VFX_DESIGN.md v0.2；用户 2026-10-30 22:54 “按建议走”）
// 序列帧播放器（战场像素层）、策略与技能配方、屏幕层特写（A 横条／B 漫画分格／简短版）、书法横幅、去色、频率与跳过。
// 只读战斗结果：演出只是让动作流程多等一会儿（表现时钟 fxDt），不改任何结算，也不推进行动条。
// 素材放 Resources/BingshuTactics/Vfx；缺图的策略、技能照旧（没有特写、没有新特效），素材到了自动生效。
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Prototype.BingshuTactics
{
    public sealed partial class TacticsGame
    {
        const float VfxPpm = 36f;   // 1 源像素 ＝ 1/36 米，与 Q 版人物一致
        const float VfxFps = 12f;

        // ---------------- 素材 ----------------
        readonly Dictionary<string, Texture2D> vfxTex = new Dictionary<string, Texture2D>();
        readonly Dictionary<Texture2D, Material> vfxMat = new Dictionary<Texture2D, Material>();

        Texture2D VfxTex(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (!vfxTex.TryGetValue(name, out var t)) vfxTex[name] = t = Resources.Load<Texture2D>("BingshuTactics/Vfx/" + name);
            return t;
        }

        Material VfxMaterial(Texture2D tex, float glow)
        {
            if (!vfxMat.TryGetValue(tex, out var m))
            {
                m = new Material(OverlayMat ? OverlayMat.shader : Shader.Find("Sprites/Default")) { mainTexture = tex, name = "vfx " + tex.name };
                vfxMat[tex] = m;
            }
            m.color = new Color(glow, glow, glow, 1f);   // 亮度略超 1，只让特效自身越过泛光阈值
            return m;
        }

        // 每条序列帧的帧数（帧格不一定是正方形，不能用宽÷高推算）
        static readonly Dictionary<string, int> VfxFrames = new Dictionary<string, int>
        {
            ["fx_fire_bolt"] = 8, ["fx_fire_tile"] = 12, ["fx_fire_wall"] = 12, ["fx_embers"] = 8,
            ["fx_gy_thrust"] = 8, ["fx_gy_downcut"] = 8, ["fx_gy_arc"] = 10, ["fx_sparks"] = 8,
            // S2 水木土金
            ["fx_water_tile"] = 12, ["fx_water_wave"] = 12, ["fx_water_flood"] = 12, ["fx_mist"] = 8,
            ["fx_wood_heal"] = 12, ["fx_drum"] = 8, ["fx_flag_wave"] = 12, ["fx_leaves"] = 8,
            ["fx_earth_wall"] = 10, ["fx_bazhen"] = 12, ["fx_flag_rise"] = 8, ["fx_dust"] = 8,
            ["fx_arrow_volley"] = 10, ["fx_arrow_streak"] = 8, ["fx_glint"] = 8,
            // S3 刘备、张飞、赵云
            ["fx_lb_rally"] = 12, ["fx_lb_arrow"] = 8, ["fx_lb_guard"] = 12,
            ["fx_zf_sweep"] = 10, ["fx_zf_shout"] = 8, ["fx_zf_quake"] = 10,
            ["fx_zy_shield"] = 12, ["fx_zy_thrust2"] = 10,
            // S4 吕布、三英、通用武技
            ["fx_lvbu_cleave"] = 10, ["fx_sanying_cross"] = 10, ["fx_zj_flag"] = 8,
            ["fx_tech_thrust"] = 8, ["fx_tech_smash"] = 8, ["fx_tech_guard"] = 8, ["fx_tech_pillar"] = 10, ["fx_tech_qi"] = 8, ["fx_tech_swirl"] = 8,
        };

        // 格子地面的实际高度：战场有高低与 3D 地形，用射线取地表，避免特效埋进地里
        Vector3 GroundAt(Vector2Int p)
        {
            var at = Tile(p);
            foreach (var hit in Physics.RaycastAll(at + Vector3.up * 40f, Vector3.down, 80f))
                if (tileOf.TryGetValue(hit.collider, out var hp) && hp == p) { at.y = Mathf.Max(at.y, hit.point.y); break; }
            var u = At(p);
            if (u != null && views.TryGetValue(u, out var v)) at.y = Mathf.Max(at.y, v.position.y);
            return at;
        }

        // 打散格子：每团火焰在格内随机错位、大小略变，不让一排火焰连成方块（地形过渡原则）
        Vector3 Scatter(Vector3 at) => at + new Vector3((float)fxRng.NextDouble() - 0.5f, 0f, (float)fxRng.NextDouble() - 0.5f) * (T * 0.45f);
        float Vary() => 0.85f + 0.3f * (float)fxRng.NextDouble();

        // ---------------- 序列帧播放器 ----------------
        sealed class Flip
        {
            public Transform T; public Mesh Mesh; public Texture2D Tex; public int Frames;
            public float Time, Delay; public bool Mirror; public float Roll;
            public Vector3 From, To; public float Travel;   // 飞行物：在 Travel 秒内从 From 飞到 To
            public Light Light; public float LightPeak;
            public bool Flat;   // 贴地平放（俯视条带，如八阵阵线）
        }
        readonly List<Flip> flips = new List<Flip>();

        // 播放一条横向序列帧：pos 为落地点（帧底边中心）；frames 由图宽／帧宽推出时可传 0
        Flip PlayFlip(string tex, int frames, Vector3 pos, float delay = 0f, float scale = 1f, float glow = 1.2f, Color? light = null, bool mirror = false, float roll = 0f, bool flat = false)
        {
            var t = VfxTex(tex);
            if (!t) return null;
            if (frames <= 0) frames = VfxFrames.TryGetValue(tex, out var known) ? known : Mathf.Max(1, t.width / t.height);
            float w = t.width / (float)frames / VfxPpm * scale, h = t.height / VfxPpm * scale;
            var go = new GameObject("vfx " + tex);
            go.transform.position = pos;
            var mesh = new Mesh { name = tex };
            float y0 = flat ? -h / 2 : 0f;   // 贴地的以格子中心为中心，立着的以底边为落地点
            if (flat) pos += Vector3.up * 0.04f;
            mesh.vertices = new[] { new Vector3(-w / 2, y0, 0), new Vector3(w / 2, y0, 0), new Vector3(w / 2, y0 + h, 0), new Vector3(-w / 2, y0 + h, 0) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.uv = new Vector2[4];
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = VfxMaterial(t, glow); r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            var f = new Flip { T = go.transform, Mesh = mesh, Tex = t, Frames = frames, Delay = delay, Mirror = mirror, Roll = roll, From = pos, To = pos, Flat = flat };
            go.transform.position = pos;
            if (light.HasValue)
            {
                var l = new GameObject("vfx light").AddComponent<Light>();
                l.transform.SetParent(go.transform, false); l.transform.localPosition = Vector3.up * 0.8f;
                l.type = LightType.Point; l.color = light.Value; l.range = 3.5f * scale; l.intensity = 0f; f.Light = l; f.LightPeak = 2.2f;
            }
            go.SetActive(delay <= 0f);
            flips.Add(f);
            SetFrame(f, 0);
            return f;
        }

        // 飞行物（火矢）：边飞边播放，到达后消失
        void PlayBolt(string tex, Vector3 from, Vector3 to, float travel, float delay = 0f)
        {
            var f = PlayFlip(tex, 0, from, delay, 1f, 1.4f);
            if (f == null) return;
            f.From = from; f.To = to; f.Travel = travel;
        }

        void SetFrame(Flip f, int i)
        {
            float u0 = i / (float)f.Frames, u1 = (i + 1) / (float)f.Frames;
            if (f.Mirror) (u0, u1) = (u1, u0);
            f.Mesh.uv = new[] { new Vector2(u0, 0), new Vector2(u1, 0), new Vector2(u1, 1), new Vector2(u0, 1) };
        }

        void UpdateFlips()
        {
            var camRot = cam.transform.rotation;
            for (int i = flips.Count - 1; i >= 0; i--)
            {
                var f = flips[i];
                if (f.Delay > 0f) { f.Delay -= fxDt; if (f.Delay > 0f) continue; f.T.gameObject.SetActive(true); }
                f.Time += fxDt;
                float life = f.Travel > 0f ? f.Travel : f.Frames / VfxFps;
                if (f.Time >= life) { Destroy(f.Mesh); Destroy(f.T.gameObject); flips.RemoveAt(i); continue; }
                int frame = f.Travel > 0f ? Mathf.FloorToInt(f.Time * VfxFps) % f.Frames : Mathf.Min(f.Frames - 1, Mathf.FloorToInt(f.Time * VfxFps));
                SetFrame(f, frame);
                if (f.Travel > 0f)
                {
                    float x = f.Time / f.Travel;
                    var p = Vector3.Lerp(f.From, f.To, x) + Vector3.up * (Mathf.Sin(x * Mathf.PI) * 0.8f);   // 抛物线
                    var next = Vector3.Lerp(f.From, f.To, Mathf.Min(1f, x + 0.05f)) + Vector3.up * (Mathf.Sin(Mathf.Min(1f, x + 0.05f) * Mathf.PI) * 0.8f);
                    f.T.position = p;
                    var sp = cam.WorldToScreenPoint(next) - cam.WorldToScreenPoint(p);
                    f.Roll = Mathf.Atan2(sp.y, sp.x) * Mathf.Rad2Deg;
                }
                f.T.rotation = f.Flat ? Quaternion.Euler(90f, yaw, 0f) : camRot * Quaternion.Euler(0, 0, f.Roll);
                if (f.Light) f.Light.intensity = f.LightPeak * Mathf.Sin(Mathf.Clamp01(f.Time / life) * Mathf.PI);
            }
        }

        // ---------------- 演出状态：跳过与频率 ----------------
        bool vfxSkip, skipEnemyTurn; float inputBlockUntil;
        float showScale = 1f;   // 同场再放的策略：简短版，主体演出按 60% 播（节奏预算，BATTLE_VFX_DESIGN 2.6）
        readonly HashSet<string> strategiesShown = new HashSet<string>();
        readonly HashSet<string> enemyCutinsShown = new HashSet<string>();   // 敌方首领、具名将领：每场首次完整特写
        readonly HashSet<string> cutinsThisBattle = new HashSet<string>();  // 我方：同一场内每个特写最多完整一次（节奏预算）
        bool ShortShows => autoPlay && !FullFeelInAuto && !ForceFullShows;   // 电脑代打、快进：一律简短版

        // 演出期间等待：可按确认键跳过（跳过后 150 ms 内不吃输入，防止一键穿透到下一个菜单）
        IEnumerator ShowWait(float seconds)
        {
            seconds *= showScale;
            for (float t = 0f; t < seconds && !vfxSkip; t += fxDt)
            {
                ShowTimeAdded?.Invoke(fxDt);
                if (ConfirmPressed && Time.unscaledTime >= inputBlockUntil)
                {
                    vfxSkip = true; inputBlockUntil = Time.unscaledTime + 0.15f;
                    if (active != null && active.Team == Team.Enemy) skipEnemyTurn = true;   // 敌方回合：一键跳过余下的敌方演出
                }
                yield return null;
            }
        }

        bool Skipping(Unit u) => u.Team == Team.Enemy && skipEnemyTurn;

        // ---------------- 策略配方（S1 火系、S2 水木土金） ----------------
        static readonly Color FireLight = new Color(1f, 0.55f, 0.15f), WaterLight = new Color(0.35f, 0.65f, 1f),
            WoodLight = new Color(0.4f, 1f, 0.6f), EarthLight = new Color(1f, 0.8f, 0.4f), MetalLight = new Color(0.95f, 0.92f, 0.82f);

        // 屏幕上向左：条带素材按向右画，向左时镜像
        bool ScreenLeft(Vector3 from, Vector3 to) => Vector3.Dot(to - from, cam.transform.right) < 0f;

        IEnumerator StrategyFx(Unit u, SkillDef s, List<Vector2Int> area)
        {
            if (!HitFeelSettings.Enabled || Skipping(u) || area.Count == 0) yield break;
            vfxSkip = false;
            int tier = Strategies.Tier(s);
            bool first = strategiesShown.Add(s.Name);
            bool full = first && !ShortShows && tier >= 2;   // 二、三阶策略每场首次完整：镜头与天色
            showScale = first ? 1f : 0.6f;   // 同场再放：不出横幅，主体缩短
            bannerAllowed = first;
            try {
            var center = area.Aggregate(Vector3.zero, (acc, p) => acc + GroundAt(p)) / area.Count;
            FocusOn(center);   // 镜头移到作用范围（换人行动时自动回到跟随）
            switch (s.Name)
            {
                case "纵火":   // 张角专属：黄巾令旗特写（敌方 A 横条，每场首次），令旗一挥、火从目标脚下窜起；火色与我方相同，不画妖术
                    if (!VfxTex("fx_fire_tile")) yield break;
                    yield return SkillCutIn(u, s);
                    vfxSkip = false;
                    PlayFlip("fx_zj_flag", 0, views[u].position + cam.transform.right * 0.3f, 0f, 1f, 1.15f);
                    yield return ShowWait(0.2f);
                    PlayFlip("fx_fire_tile", 0, center, 0f, 1f, 1.3f, FireLight);
                    PlayFlip("fx_embers", 0, center + Vector3.up * 0.9f, 0.4f);
                    yield return ShowWait(0.45f);   // 同场再放时只有这段就地演出（约 0.8 s）
                    break;
                case "引火":
                    if (!VfxTex("fx_fire_tile")) yield break;
                    PlayBolt("fx_fire_bolt", views[u].position + Vector3.up * 0.9f, center + Vector3.up * 0.5f, 0.35f);
                    yield return ShowWait(0.3f);
                    PlayFlip("fx_fire_tile", 0, center, 0f, 1f, 1.3f, FireLight);
                    PlayFlip("fx_embers", 0, center + Vector3.up * 0.9f, 0.4f);
                    yield return ShowWait(0.33f);   // 焰舌卷起到最旺：结算
                    break;
                case "火烧营寨":
                    if (!VfxTex("fx_fire_tile")) yield break;
                    ShowBanner("banner_huoshao", u, false);
                    if (full) PushCamera(center, 0.03f);
                    for (int k = 0; k < 3; k++)
                        PlayBolt("fx_fire_bolt", views[u].position + Vector3.up * 0.9f, center + new Vector3((k - 1) * 0.5f, 0.4f, (k % 2) * 0.4f - 0.2f), 0.35f, k * 0.08f);
                    yield return ShowWait(0.45f);
                    foreach (var p in area.OrderBy(p => Vector3.Distance(Tile(p), center)))
                        PlayFlip("fx_fire_tile", 0, Scatter(GroundAt(p)), Vector3.Distance(Tile(p), center) / T * 0.06f + (float)fxRng.NextDouble() * 0.05f, Vary(), 1.3f, p == area[0] ? FireLight : (Color?)null);
                    PlayFlip("fx_embers", 0, center + Vector3.up * 1f, 0.6f, 1.5f);
                    yield return ShowWait(0.4f);
                    break;
                case "火海":
                    if (!VfxTex("fx_fire_wall")) yield break;
                    ShowBanner("banner_huohai", u, true);
                    if (full) { PushCamera(center, 0.04f); TintSky(new Color(1f, 0.62f, 0.38f)); }
                    // 火线从施术者一侧扫入：按到施术者的距离错开
                    var from = views[u].position;
                    float near = area.Min(p => Vector3.Distance(Tile(p), from));
                    yield return ShowWait(0.35f);
                    foreach (var p in area)
                    {
                        float d = Vector3.Distance(Tile(p), from) - near;
                        PlayFlip("fx_fire_wall", 0, Scatter(GroundAt(p)), d / T * 0.08f + (float)fxRng.NextDouble() * 0.06f, Vary(), 1.35f, (Mathf.Abs(p.x + p.y) % 3 == 0) ? FireLight : (Color?)null);
                    }
                    for (int k = 0; k < 4; k++) PlayFlip("fx_embers", 0, center + new Vector3((k - 1.5f) * 0.8f, 1.2f, 0), 0.7f + k * 0.1f, 1.5f);
                    yield return ShowWait(0.6f);
                    break;
                // ---- 水 ----
                case "截流":
                    if (!VfxTex("fx_water_tile")) yield break;
                    PlayFlip("fx_water_tile", 0, center, 0f, 1f, 1.25f, WaterLight);
                    PlayFlip("fx_mist", 0, center, 0.5f);
                    yield return ShowWait(0.35f);
                    break;
                case "水攻":
                {
                    if (!VfxTex("fx_water_wave")) yield break;
                    ShowBanner("banner_shuigong", u, false);
                    if (full) PushCamera(center, 0.03f);
                    var src = views[u].position;
                    bool left = ScreenLeft(src, center);
                    var line = area.OrderBy(p => Vector3.Distance(Tile(p), src)).ToList();
                    for (int k = 0; k < line.Count; k++)
                        PlayFlip("fx_water_wave", 0, GroundAt(line[k]), k * 0.08f, 1f, 1.2f, k == line.Count / 2 ? WaterLight : (Color?)null, left);
                    yield return ShowWait(0.45f);
                    break;
                }
                case "决堤":
                {
                    if (!VfxTex("fx_water_flood")) yield break;
                    ShowBanner("banner_juedi", u, true);
                    if (full) { PushCamera(center, 0.04f); TintSky(new Color(0.55f, 0.78f, 1f)); }
                    var src = views[u].position;
                    bool left = ScreenLeft(src, center);
                    float near2 = area.Min(p => Vector3.Distance(Tile(p), src));
                    yield return ShowWait(0.3f);
                    foreach (var p in area)
                    {
                        float d = Vector3.Distance(Tile(p), src) - near2;
                        PlayFlip("fx_water_flood", 0, Scatter(GroundAt(p)), d / T * 0.06f + (float)fxRng.NextDouble() * 0.05f, Vary(), 1.25f, (Mathf.Abs(p.x + p.y) % 3 == 0) ? WaterLight : (Color?)null, left);
                    }
                    for (int k = 0; k < 4; k++) PlayFlip("fx_mist", 0, Scatter(center), 0.8f + k * 0.08f, 1.6f);
                    yield return ShowWait(0.6f);
                    break;
                }
                // ---- 木（回复：兵力回涨，不画溃卒跑回） ----
                case "抚军":
                    if (!VfxTex("fx_wood_heal")) yield break;
                    PlayFlip("fx_wood_heal", 0, center, 0f, 1f, 1.25f, WoodLight);
                    PlayFlip("fx_leaves", 0, center + Vector3.up * 1.2f, 0.2f);
                    yield return ShowWait(0.35f);
                    break;
                case "鼓舞":
                {
                    if (!VfxTex("fx_drum")) yield break;
                    ShowBanner("banner_guwu", u, false);
                    if (full) PushCamera(center, 0.03f);
                    PlayFlip("fx_drum", 0, center, 0f, 1f, 1.15f, WoodLight);
                    var allies = area.Where(p => At(p) != null && At(p).Team == u.Team).ToList();
                    foreach (var p in allies) { PlayFlip("fx_flag_wave", 0, GroundAt(p) + cam.transform.right * 0.35f, 0.2f); PlayFlip("fx_wood_heal", 0, GroundAt(p), 0.3f, 1f, 1.2f); }
                    yield return ShowWait(0.55f);
                    break;
                }
                case "收拢溃卒":
                {
                    if (!VfxTex("fx_wood_heal")) yield break;
                    ShowBanner("banner_shoulong", u, true);
                    if (full) { PushCamera(center, 0.03f); TintSky(new Color(0.8f, 1f, 0.82f)); }
                    // 只照亮在场友军：每人脚下青气，旗帜扬起
                    int k = 0;
                    foreach (var p in area.OrderBy(p => Vector3.Distance(Tile(p), center)))
                    {
                        PlayFlip("fx_wood_heal", 0, GroundAt(p), 0.15f + k * 0.06f, 1.1f, 1.3f, k % 2 == 0 ? WoodLight : (Color?)null);
                        PlayFlip("fx_flag_wave", 0, GroundAt(p) + cam.transform.right * 0.35f, 0.1f + k * 0.06f);
                        k++;
                    }
                    PlayFlip("fx_leaves", 0, center + Vector3.up * 1.5f, 0.4f, 1.6f);
                    yield return ShowWait(0.6f);
                    break;
                }
                // ---- 土 ----
                case "坚壁":
                {
                    if (!VfxTex("fx_earth_wall")) yield break;
                    var toward = (views[u].position - center); toward.y = 0f;
                    var front = center + (toward.sqrMagnitude > 1e-3f ? toward.normalized * 0.45f : Vector3.zero);
                    PlayFlip("fx_earth_wall", 0, front, 0f, 1f, 1.1f, EarthLight);
                    PlayFlip("fx_dust", 0, front, 0.1f);
                    yield return ShowWait(0.35f);
                    break;
                }
                case "八阵":
                {
                    if (!VfxTex("fx_bazhen")) yield break;
                    ShowBanner("banner_bazhen", u, true);
                    if (full) { PushCamera(center, 0.04f); TintSky(new Color(1f, 0.86f, 0.58f)); }
                    var top = center; top.y = area.Max(p => GroundAt(p).y) + 0.06f;   // 贴地阵线放在范围内最高的地面上，不被高低地形埋住
                    PlayFlip("fx_bazhen", 0, top, 0f, 0.95f, 1.2f, EarthLight, flat: true);
                    int k = 0;
                    foreach (var p in area.Where(p => Grid.Dist(p, area.OrderBy(q => Vector3.Distance(Tile(q), center)).First()) == 2).Take(8))
                        PlayFlip("fx_flag_rise", 0, GroundAt(p), 0.25f + 0.07f * k++);
                    yield return ShowWait(0.8f);
                    break;
                }
                // ---- 金（箭雨：箭落地即散，不留插地的箭） ----
                case "箭雨":
                    if (!VfxTex("fx_arrow_volley")) yield break;
                    foreach (var p in area) { PlayFlip("fx_arrow_volley", 0, Scatter(GroundAt(p)), (float)fxRng.NextDouble() * 0.12f, Vary(), 1.2f); PlayFlip("fx_glint", 0, GroundAt(p) + Vector3.up * 0.2f, 0.45f); }
                    yield return ShowWait(0.45f);
                    break;
                case "万箭齐发":
                {
                    if (!VfxTex("fx_arrow_volley")) yield break;
                    ShowBanner("banner_wanjian", u, true);
                    if (full) PushCamera(center, 0.04f);
                    // 三波：高空箭队掠过，随后落进范围；数字只在第三波之后结算（Apply 在本协程返回后才执行）
                    for (int wave = 0; wave < 3; wave++)
                    {
                        PlayFlip("fx_arrow_streak", 0, center + Vector3.up * 2.6f + cam.transform.right * (wave - 1) * 0.6f, wave * 0.35f, 1.4f, 1.15f);
                        foreach (var p in area) PlayFlip("fx_arrow_volley", 0, Scatter(GroundAt(p)), wave * 0.35f + 0.25f + (float)fxRng.NextDouble() * 0.08f, Vary(), 1.2f, wave == 2 && p == area[0] ? MetalLight : (Color?)null);
                    }
                    foreach (var p in area) PlayFlip("fx_glint", 0, GroundAt(p) + Vector3.up * 0.2f, 1.3f);
                    yield return ShowWait(1.35f);
                    break;
                }
            }
        } finally { showScale = 1f; bannerAllowed = true; }
        }

        // ---------------- 专属技能（S1：关羽） ----------------
        sealed class CutinArt { public string[] Busts; public string Name; public bool Panels; public Color Color; public string Emblem; public bool FacesLeft; }

        // 每人最强绝学（众望、拖刀、断桥、天下无双）用 B 漫画分格，其余专属技能用 A 横条（用户 2026-10-30）
        static readonly Dictionary<string, CutinArt> Cutins = new Dictionary<string, CutinArt>
        {
            ["关羽/突击"] = new CutinArt { Busts = new[] { "cutin_guanyu_tuji" }, Name = "name_tuji", Color = new Color(0.16f, 0.45f, 0.30f) },
            ["关羽/破阵"] = new CutinArt { Busts = new[] { "cutin_guanyu_pozhen" }, Name = "name_pozhen", Color = new Color(0.16f, 0.45f, 0.30f) },
            ["关羽/拖刀"] = new CutinArt { Busts = new[] { "cutin_guanyu_tuodao_eye", "cutin_guanyu_tuodao_blade", "cutin_guanyu_tuodao_full" }, Name = "name_tuodao", Panels = true, Color = new Color(0.16f, 0.45f, 0.30f) },
            // S3：刘备橙、张飞墨黑、赵云银白
            ["刘备/整队"] = new CutinArt { Busts = new[] { "cutin_liubei_zhengdui" }, Name = "name_zhengdui", Color = new Color(0.78f, 0.42f, 0.12f) },
            ["刘备/流矢"] = new CutinArt { Busts = new[] { "cutin_liubei_liushi" }, Name = "name_liushi", Color = new Color(0.78f, 0.42f, 0.12f) },
            ["刘备/火矢"] = new CutinArt { Busts = new[] { "cutin_liubei_liushi" }, Name = "name_liushi", Color = new Color(0.78f, 0.42f, 0.12f) },   // 旧名，主稿 K12 改为流矢
            ["刘备/众望"] = new CutinArt { Busts = new[] { "cutin_liubei_zhongwang_eye", "cutin_liubei_zhongwang_hand", "cutin_liubei_zhongwang_full" }, Name = "name_zhongwang", Panels = true, Color = new Color(0.78f, 0.42f, 0.12f) },
            ["张飞/横扫"] = new CutinArt { Busts = new[] { "cutin_zhangfei_hengsao" }, Name = "name_hengsao", Color = new Color(0.13f, 0.13f, 0.15f) },
            ["张飞/威慑"] = new CutinArt { Busts = new[] { "cutin_zhangfei_weishe" }, Name = "name_weishe", Color = new Color(0.13f, 0.13f, 0.15f) },
            ["张飞/断桥"] = new CutinArt { Busts = new[] { "cutin_zhangfei_duanqiao_eye", "cutin_zhangfei_duanqiao_bridge", "cutin_zhangfei_duanqiao_full" }, Name = "name_duanqiao", Panels = true, Color = new Color(0.13f, 0.13f, 0.15f) },
            ["赵云/援护"] = new CutinArt { Busts = new[] { "cutin_zhaoyun_yuanhu" }, Name = "name_yuanhu", Color = new Color(0.52f, 0.58f, 0.66f) },
            ["赵云/连击"] = new CutinArt { Busts = new[] { "cutin_zhaoyun_lianji" }, Name = "name_lianji", Color = new Color(0.52f, 0.58f, 0.66f) },
            // S4：吕布（敌方，赤金，画像本身朝左）与刘关张合体技三英（不分是谁发动）
            ["吕布/天下无双"] = new CutinArt { Busts = new[] { "cutin_lvbu_wushuang_eye", "cutin_lvbu_wushuang_halberd", "cutin_lvbu_wushuang_full" }, Name = "name_wushuang", Panels = true, Color = new Color(0.45f, 0.24f, 0.08f), Emblem = "emblem_dongzhuo", FacesLeft = true },
            ["张角/纵火"] = new CutinArt { Busts = new[] { "cutin_zhangjiao_zonghuo" }, Name = "name_zonghuo", Color = new Color(0.55f, 0.44f, 0.10f), Emblem = "emblem_huangjin", FacesLeft = true },
            ["*/三英"] = new CutinArt { Busts = new[] { "cutin_zhangfei_weishe", "cutin_guanyu_pozhen", "cutin_sanying_trio" }, Name = "name_sanying", Panels = true, Color = new Color(0.30f, 0.22f, 0.12f) },
        };

        CutinArt CutinFor(Unit u, SkillDef s) =>
            (Cutins.TryGetValue(u.Name + "/" + s.Name, out var c) || Cutins.TryGetValue("*/" + s.Name, out c)) && c.Busts.All(b => VfxTex(b)) ? c : null;

        IEnumerator SkillCutIn(Unit u, SkillDef s)
        {
            if (!HitFeelSettings.Enabled || HitFeelSettings.Cutin == 0 || Skipping(u)) yield break;
            var art = CutinFor(u, s);
            if (art == null) yield break;
            string key = Cutins.ContainsKey(u.Name + "/" + s.Name) ? u.Name + "/" + s.Name : "*/" + s.Name;
            bool enemy = u.Team == Team.Enemy;
            if (enemy && !enemyCutinsShown.Add(key)) yield break;   // 敌方：每场首次完整，同名招再放只就地演出
            bool seen = !enemy && (Chapter?.CutinKnown?.Invoke(key) ?? false);
            bool firstThisBattle = cutinsThisBattle.Add(key);
            bool full = !ShortShows && firstThisBattle && (enemy || HitFeelSettings.Cutin == 2 || !seen);   // “每次完整”＝每场首次完整，同场再放用简短版
            vfxSkip = false;
            cutin = new CutinShow { Art = art, Unit = u, Short = !full, T = 0f, Enemy = u.Team == Team.Enemy };
            if (full) { if (!enemy) Chapter?.CutinSeen?.Invoke(key); Desaturate(true); }   // 敌方的不记进存档
            cutin.Glint = full && FlashBudgetLeft;   // 眼部横光计入光敏上限
            if (cutin.Glint) CountFlash();
            float dur = full ? (art.Panels ? 0.9f : 0.7f) : 0.3f;
            yield return ShowWait(dur);
            cutin = null;
            Desaturate(false);
        }

        IEnumerator SkillFx(Unit u, SkillDef s, Vector2Int targetTile)
        {
            if (!HitFeelSettings.Enabled || Skipping(u)) yield break;
            vfxSkip = false;
            var at = GroundAt(targetTile); var me = views[u].position;
            bool left = Vector3.Dot(at - me, cam.transform.right) < 0f;
            FocusOn((me + at) / 2f);
            var green = new Color(0.35f, 1f, 0.6f);
            switch (u.Name + "/" + s.Name)
            {
                case "关羽/突击":
                    if (PlayFlip("fx_gy_thrust", 0, (me + at) / 2f + Vector3.up * 0.35f, 0f, 1f, 1.35f, green, left) == null) yield break;
                    yield return ShowWait(0.25f);
                    break;
                case "关羽/破阵":
                    if (PlayFlip("fx_gy_downcut", 0, at, 0f, 1f, 1.35f, green) == null) yield break;
                    yield return ShowWait(0.3f);
                    break;
                case "关羽/拖刀":
                    if (!VfxTex("fx_gy_arc")) yield break;
                    PlayFlip("fx_sparks", 0, me, 0f, 1f, 1.3f, FireLight);
                    PlayFlip("fx_sparks", 0, me + cam.transform.right * 0.3f, 0.12f);
                    yield return ShowWait(0.35f);
                    PlayFlip("fx_gy_arc", 0, (me + at) / 2f, 0f, 1.1f, 1.4f, green, left);
                    yield return ShowWait(0.3f);
                    break;
                // ---- S3 刘备 ----
                case "刘备/整队":
                    if (PlayFlip("fx_lb_rally", 0, at, 0f, 1f, 1.25f, FireLight) == null) yield break;
                    yield return ShowWait(0.35f);
                    break;
                case "刘备/流矢":
                case "刘备/火矢":
                    if (!VfxTex("fx_lb_arrow")) yield break;
                    for (int k = 0; k < 3; k++) PlayBolt("fx_lb_arrow", me + Vector3.up * 0.8f, at + Vector3.up * 0.5f, 0.3f, k * 0.1f);
                    yield return ShowWait(0.45f);
                    break;
                case "刘备/众望":
                    if (PlayFlip("fx_lb_guard", 0, at, 0f, 1f, 1.3f, new Color(1f, 0.85f, 0.45f)) == null) yield break;
                    yield return ShowWait(0.4f);
                    break;
                // ---- S3 张飞（墨黑，不加亮） ----
                case "张飞/横扫":
                    if (PlayFlip("fx_zf_sweep", 0, me, 0f, 1f, 1f) == null) yield break;
                    PlayFlip("fx_dust", 0, me, 0.2f, 1.6f);
                    yield return ShowWait(0.35f);
                    break;
                case "张飞/威慑":
                    if (PlayFlip("fx_zf_shout", 0, me + Vector3.up * 0.4f, 0f, 1.2f, 1f) == null) yield break;
                    yield return ShowWait(0.3f);
                    break;
                case "张飞/断桥":
                    if (PlayFlip("fx_zf_quake", 0, at, 0f, 1f, 1.1f) == null) yield break;
                    PlayFlip("fx_dust", 0, at, 0.15f, 1.3f);
                    yield return ShowWait(0.35f);
                    break;
                // ---- S3 赵云（骑兵，原格） ----
                case "赵云/援护":
                    if (PlayFlip("fx_zy_shield", 0, me, 0f, 1.05f, 1.35f, new Color(0.85f, 0.92f, 1f)) == null) yield break;
                    yield return ShowWait(0.35f);
                    break;
                case "赵云/连击":
                    if (PlayFlip("fx_zy_thrust2", 0, (me + at) / 2f + Vector3.up * 0.35f, 0f, 1f, 1.35f, new Color(0.85f, 0.92f, 1f), left) == null) yield break;
                    yield return ShowWait(0.3f);
                    break;
                // ---- S4 ----
                case "吕布/天下无双":
                {
                    var dir = at - me; dir.y = 0f;
                    var mid = me + (dir.sqrMagnitude > 1e-3f ? dir.normalized * T : Vector3.zero);   // 巨刃只覆盖正前方 2 格
                    if (PlayFlip("fx_lvbu_cleave", 0, mid, 0f, 1f, 1.35f, new Color(1f, 0.6f, 0.2f), left) == null) yield break;
                    yield return ShowWait(0.3f);
                    break;
                }
                default:
                    yield return GenericTechFx(s, me, at, left);
                    break;
            }
        }

        // 通用武技（功法书）：没有特写，按五行一套轻量特效（BATTLE_VFX_DESIGN 4.3）
        IEnumerator GenericTechFx(SkillDef s, Vector3 me, Vector3 at, bool left)
        {
            Flip f = s.Name switch
            {
                "刺击" or "突阵" => PlayFlip("fx_tech_thrust", 0, (me + at) / 2f + Vector3.up * 0.35f, 0f, 1f, 1.2f, null, left),
                "猛击" or "疾风骤雨" => PlayFlip("fx_tech_smash", 0, at, 0f, 1f, 1.2f),
                "迂回" => PlayFlip("fx_tech_swirl", 0, at, 0f, 1f, 1.15f),
                "闪避" => PlayFlip("fx_tech_swirl", 0, me, 0f, 1f, 1.15f),
                "调息" or "死战" => PlayFlip("fx_tech_qi", 0, me, 0f, 1f, 1.15f),
                "格挡" => PlayFlip("fx_tech_guard", 0, me, 0f, 1f, 1.15f),
                "中流砥柱" => PlayFlip("fx_tech_pillar", 0, me, 0f, 1f, 1.2f, EarthLight),
                _ => null
            };
            if (f != null) yield return ShowWait(0.25f);
        }

        // 合体技三英（Chapter3.SanYing 调用）：特写＋三色刀光在吕布格交叉
        IEnumerator SanYingFx(Unit u, Unit lubu)
        {
            var s = new SkillDef { Name = "三英" };
            yield return SkillCutIn(u, s);
            if (!HitFeelSettings.Enabled || !VfxTex("fx_sanying_cross")) yield break;
            FocusOn(views[lubu].position);
            PlayFlip("fx_sanying_cross", 0, views[lubu].position, 0f, 1.1f, 1.4f, new Color(1f, 0.85f, 0.5f));
            yield return ShowWait(0.55f);
        }

        // ---------------- 屏幕层：特写、横幅 ----------------
        sealed class CutinShow { public CutinArt Art; public Unit Unit; public bool Short, Enemy, Glint; public float T; }
        CutinShow cutin;
        (Texture2D tex, bool enemy, bool big, float t)? banner;

        bool bannerAllowed = true;

        void ShowBanner(string tex, Unit u, bool big)
        {
            if (!bannerAllowed) return;
            var t = VfxTex(tex);
            if (t) banner = (t, u.Team == Team.Enemy, big, 0f);
        }

        void TickScreenLayer()
        {
            if (cutin != null) cutin.T += Time.deltaTime;   // 屏幕层不受慢放影响
            if (banner.HasValue) { var b = banner.Value; b.t += Time.deltaTime; banner = b.t > 1.1f ? null : ((Texture2D, bool, bool, float)?)b; }
        }

        static float Ease(float x) => 1f - (1f - Mathf.Clamp01(x)) * (1f - Mathf.Clamp01(x));

        void DrawScreenLayer(float sw)
        {
            if (banner.HasValue)
            {
                var (tex, enemy, big, t) = banner.Value;
                float h = big ? 150f : 72f, w = h * tex.width / tex.height;
                float slide = Ease(t / 0.18f), fade = t > 0.85f ? 1f - (t - 0.85f) / 0.25f : 1f;
                float x = enemy ? Mathf.Lerp(sw, sw - w - 40, slide) : Mathf.Lerp(-w, 40, slide);
                var o = GUI.color; GUI.color = new Color(1, 1, 1, fade);
                GUI.DrawTexture(new Rect(x, big ? 150 : 170, w, h), tex, ScaleMode.ScaleToFit);   // 让开顶栏与右上小地图
                GUI.color = o;
            }
            if (cutin == null) return;
            var c = cutin; var art = c.Art;
            if (c.Short) { DrawCutinBand(sw, c, 560f, 110f, Ease(c.T / 0.08f), true); return; }
            Rect(new Rect(0, 0, sw, RefH), new Color(0, 0, 0, 0.28f * Ease(c.T / 0.1f)));
            if (!art.Panels) { DrawCutinBand(sw, c, 240f, 230f, Ease((c.T - 0.05f) / 0.15f), false); return; }
            // B 漫画分格：眼、刀依次斜切滑入，最后胸像与技能名
            var m = GUI.matrix;
            DrawPanel(sw, c, VfxTex(art.Busts[0]), new Rect(0, 70, sw * 0.62f, 150), Ease((c.T - 0.05f) / 0.12f), -4f);
            DrawPanel(sw, c, VfxTex(art.Busts[1]), new Rect(0, 238, sw * 0.62f, 150), Ease((c.T - 0.18f) / 0.12f), -4f);
            DrawPanel(sw, c, VfxTex(art.Busts[2]), new Rect(sw * 0.36f, 120, sw * 0.62f, 330), Ease((c.T - 0.3f) / 0.14f), 3f);
            GUI.matrix = m;
            DrawName(sw, c, new Rect(sw * 0.58f, 430, 360, 150), (c.T - 0.42f) / 0.1f);
            if (c.Enemy) { var em = VfxTex(art.Emblem); if (em) GUI.DrawTexture(new Rect(sw * 0.05f, 430, 110, 110), em, ScaleMode.ScaleToFit); }
            if (c.Glint && c.T > 0.6f && c.T < 0.7f) Rect(new Rect(0, 140, sw * 0.62f, 3), new Color(1, 1, 1, 0.9f));   // 眼部横光（计入闪白）
        }

        // A 横条（完整版或简短版）：主色底＋速度线，胸像在前，技能名在后
        void DrawCutinBand(float sw, CutinShow c, float y, float h, float slide, bool small)
        {
            var art = c.Art; var bust = VfxTex(art.Busts[art.Busts.Length - 1]); var name = VfxTex(art.Name);
            float w = small ? 520f : sw;
            float x = c.Enemy ? Mathf.Lerp(sw, sw - w, slide) : Mathf.Lerp(-w, 0, slide);
            var band = new Rect(x, y, w, h);
            Rect(band, new Color(art.Color.r, art.Color.g, art.Color.b, 0.94f));
            // 速度线：沿滑入方向，背景里流动（人物保持清楚）
            for (int i = 0; i < 14; i++)
            {
                float ly = y + 6 + (i * 37 % (int)(h - 12)), lx = Mathf.Repeat(i * 173f + c.T * 1400f * (c.Enemy ? -1 : 1), w) + x - 60;
                Rect(new Rect(lx, ly, 60 + (i % 3) * 40, 2), new Color(1, 1, 1, 0.22f));
            }
            Rect(new Rect(x, y, w, 2), new Color(0.85f, 0.9f, 0.95f, 0.9f)); Rect(new Rect(x, y + h - 2, w, 2), new Color(0.85f, 0.9f, 0.95f, 0.9f));
            DrawEnemyMarks(c, band);
            if (bust)
            {
                float bh = h * (small ? 1.05f : 1.25f), bw = bh * bust.width / bust.height;
                var br = c.Enemy ? new Rect(x + w - bw - 20, y + h - bh, bw, bh) : new Rect(x + 20, y + h - bh, bw, bh);
                GUI.DrawTextureWithTexCoords(br, bust, Mirror(c) ? new Rect(1, 0, -1, 1) : new Rect(0, 0, 1, 1));
            }
            if (name)
            {
                float nh = h * 0.78f, nw = nh * name.width / name.height;
                float nx = c.Enemy ? x + 30 : x + w - nw - 30;
                float a = small ? 1f : Mathf.Clamp01((c.T - 0.2f) / 0.08f);
                var o = GUI.color; GUI.color = new Color(1, 1, 1, a);
                GUI.DrawTexture(new Rect(nx, y + (h - nh) / 2, nw, nh), name, ScaleMode.ScaleToFit);
                GUI.color = o;
            }
            if (!small && c.Glint && c.T > 0.55f && c.T < 0.62f) Rect(new Rect(x, y + h * 0.32f, w * 0.45f, 3), new Color(1, 1, 1, 0.9f));   // 眼部横光
        }

        void DrawPanel(float sw, CutinShow c, Texture2D tex, Rect r, float slide, float angle)
        {
            if (!tex || slide <= 0f) return;
            var m = GUI.matrix;
            float dx = c.Enemy ? (1f - slide) * sw : -(1f - slide) * sw;
            if (c.Enemy) r.x = sw - r.x - r.width;
            r.x += dx;
            GUIUtility.RotateAroundPivot(angle * (c.Enemy ? -1 : 1), r.center * S);
            Rect(new Rect(r.x - 4, r.y - 4, r.width + 8, r.height + 8), new Color(0.88f, 0.92f, 0.96f, 1f));
            Rect(r, new Color(c.Art.Color.r, c.Art.Color.g, c.Art.Color.b, 1f));
            // 按格子比例裁切，不拉伸：已构好图的横条（眼、兵器）居中裁，胸像取中偏上一段（脸的位置）；敌方画像朝向按素材标注镜像
            float want = r.width / r.height, have = tex.width / (float)tex.height, frac = have / want;
            var uv = have < want ? new Rect(0, have > 2.5f ? (1f - frac) / 2f : (1f - frac) * 0.65f, 1, frac) : new Rect((1f - want / have) / 2f, 0, want / have, 1);
            if (Mirror(c)) uv = new Rect(uv.x + uv.width, uv.y, -uv.width, uv.height);
            GUI.DrawTextureWithTexCoords(r, tex, uv);
            GUI.matrix = m;
        }

        bool Mirror(CutinShow c) => c.Enemy != c.Art.FacesLeft;

        // 敌方横幅：锯齿边＋阵营徽记（敌我不靠颜色区分，BATTLE_VFX_DESIGN 2.4）
        void DrawEnemyMarks(CutinShow c, Rect band)
        {
            if (!c.Enemy) return;
            var m = GUI.matrix;
            for (float y = band.y; y < band.yMax; y += 18f)
            {
                var d = new Rect(band.x - 9f, y, 18f, 18f);
                GUIUtility.RotateAroundPivot(45f, d.center * S);
                Rect(d, new Color(c.Art.Color.r, c.Art.Color.g, c.Art.Color.b, 0.94f));
                GUI.matrix = m;
            }
            var em = VfxTex(c.Art.Emblem);
            if (em) GUI.DrawTexture(new Rect(band.x + 14f, band.y + 10f, 76f, 76f), em, ScaleMode.ScaleToFit);
        }

        void DrawName(float sw, CutinShow c, Rect r, float a)
        {
            var name = VfxTex(c.Art.Name);
            if (!name || a <= 0f) return;
            if (c.Enemy) r.x = sw - r.x - r.width;
            var o = GUI.color; GUI.color = new Color(1, 1, 1, Mathf.Clamp01(a));
            GUI.DrawTexture(r, name, ScaleMode.ScaleToFit);
            GUI.color = o;
        }

        bool HudHiddenByCutin => cutin != null && !cutin.Short && cutin.T > 0.03f && cutin.T < (cutin.Art.Panels ? 0.88f : 0.68f);

        // ---------------- 演示（-vfxDemo）：在真实战场上依次播放已有素材的全部特效，供录像审看；不进入战斗循环 ----------------
        static readonly bool VfxDemoArg = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-vfxDemo") >= 0;

        IEnumerator VfxDemo()
        {
            yield return FxWait(1.2f);
            var hero = units.FirstOrDefault(x => x.Alive && x.Name == "关羽") ?? units.First(x => x.Alive && x.Team == Team.Player);
            // 刘备、张飞、赵云（在场时）的专属技能先演
            foreach (var (who, names) in new[] { ("刘备", new[] { "整队", "流矢", "众望" }), ("张飞", new[] { "横扫", "威慑", "断桥" }), ("赵云", new[] { "援护", "连击" }) })
            {
                var g = units.FirstOrDefault(x => x.Alive && x.Name == who);
                if (g == null) continue;
                var gFoe = units.Where(x => x.Alive && x.Team == Team.Enemy).OrderBy(x => Grid.Dist(x.Pos, g.Pos)).First();
                var gAlly = units.FirstOrDefault(x => x.Alive && x.Team == g.Team && x != g) ?? g;
                foreach (var name in names)
                {
                    var s = g.Crafts.FirstOrDefault(c => c.Name == name) ?? new SkillDef { Name = name };
                    var tgt = name == "整队" || name == "众望" ? gAlly : gFoe;
                    active = g; Log($"演示：{g.Name}「{name}」");
                    yield return SkillCutIn(g, s);
                    yield return Lunge(g, tgt.Pos);
                    yield return SkillFx(g, s, tgt.Pos);
                    yield return FxWait(1.0f);
                }
            }
            var caster = units.FirstOrDefault(x => x.Alive && x.Name == "刘备") ?? hero;
            var foe = units.Where(x => x.Alive && x.Team == Team.Enemy).OrderBy(x => Grid.Dist(x.Pos, hero.Pos)).First();
            foreach (var name in new[] { "突击", "破阵", "拖刀" })
            {
                var s = hero.Crafts.FirstOrDefault(c => c.Name == name) ?? new SkillDef { Name = name };
                active = hero; Log($"演示：{hero.Name}「{name}」");
                yield return SkillCutIn(hero, s);
                yield return Lunge(hero, foe.Pos);
                yield return SkillFx(hero, s, foe.Pos);
                yield return FxWait(1.0f);
            }
            // S4：吕布天下无双（敌方 B 分格）、三英合体、通用武技
            var lubu = units.FirstOrDefault(x => x.Alive && x.Name == "吕布");
            if (lubu != null)
            {
                var near = units.Where(x => x.Alive && x.Team == Team.Player).OrderBy(x => Grid.Dist(x.Pos, lubu.Pos)).First();
                var ws = lubu.Crafts.FirstOrDefault(c => c.Name == "天下无双") ?? new SkillDef { Name = "天下无双" };
                active = lubu; Log("演示：吕布「天下无双」");
                yield return SkillCutIn(lubu, ws);
                yield return Lunge(lubu, near.Pos);
                yield return SkillFx(lubu, ws, near.Pos);
                yield return FxWait(1.0f);
                active = near; Log("演示：合体技「三英」");
                yield return SanYingFx(near, lubu);
                yield return FxWait(1.0f);
            }
            var zj = units.FirstOrDefault(x => x.Alive && x.Name == "张角");
            if (zj != null)
            {
                var zt = units.Where(x => x.Alive && x.Team == Team.Player).OrderBy(x => Grid.Dist(x.Pos, zj.Pos)).First();
                for (int k = 0; k < 2; k++)   // 第二次：同场再放，只有就地演出
                {
                    active = zj; Log($"演示：张角「纵火」{(k == 0 ? "（首次）" : "（同场第二次）")}");
                    yield return Lunge(zj, zt.Pos);
                    yield return StrategyFx(zj, Zonghuo, new List<Vector2Int> { zt.Pos });
                    yield return FxWait(1.0f);
                }
            }
            foreach (var name in new[] { "刺击", "猛击", "格挡", "中流砥柱", "调息", "闪避" })
            {
                active = hero; Log($"演示：通用武技「{name}」");
                yield return Lunge(hero, foe.Pos);
                yield return SkillFx(hero, new SkillDef { Name = name }, foe.Pos);
                yield return FxWait(0.6f);
            }
            foreach (var name in new[] { "引火", "火烧营寨", "火海", "截流", "水攻", "决堤", "抚军", "鼓舞", "收拢溃卒", "坚壁", "八阵", "箭雨", "万箭齐发" })
            {
                var s = Strategies.All.First(x => x.Name == name);
                active = caster; Log($"演示：策略「{name}」");
                popups.Add((views[caster].position + Vector3.up * (2.4f * HeadK), "策略·" + name, Wuxing.Tint(s.Element), 0f));
                yield return Lunge(caster, foe.Pos);
                var target = s.TargetsAllies ? units.FirstOrDefault(x => x.Alive && x.Team == caster.Team && x != caster) ?? caster : foe;
                var area = s.Radius >= 99 ? Friends(caster).Select(f => f.Pos).ToList()
                         : s.Area == Area.Single ? new List<Vector2Int> { target.Pos }
                         : s.Area == Area.Line ? grid.LineArea(target.Pos + Grid.DirTo(target.Pos, caster.Pos) * 2, Grid.DirTo(caster.Pos, target.Pos), s.Length, s.Width).ToList()
                         : grid.Diamond(target.Pos, s.Radius).ToList();   // 演示不受射程限制
                yield return StrategyFx(caster, s, area);
                yield return FxWait(1.6f);
            }
            yield return FxWait(1f);
            Application.Quit();
        }

        // ---------------- 画面：去色、天色、推近 ----------------
        Volume desatVolume; ColorAdjustments desat; float desatTarget, desatWeight;

        void Desaturate(bool on)
        {
            if (!desatVolume)
            {
                desatVolume = new GameObject("VFX Desaturate").AddComponent<Volume>();
                desatVolume.isGlobal = true; desatVolume.priority = 50; desatVolume.weight = 0f;
                desatVolume.sharedProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                desat = desatVolume.sharedProfile.Add<ColorAdjustments>(true);
                desat.saturation.Override(-30f); desat.postExposure.Override(-0.6f);   // 退化方案：全屏去色 30%、压暗约 65%
            }
            desatTarget = on ? 1f : 0f;
        }

        // 三阶策略：环境光与雾短暂染五行色 0.8 s（渐入渐出 ≥100 ms）
        Color skyTint; float skyT = 99f; Color baseSky, baseEq, baseGround, baseFog; bool skySaved;

        void TintSky(Color c)
        {
            if (!HitFeelSettings.ZoomAndSlow) return;
            if (!skySaved) { baseSky = RenderSettings.ambientSkyColor; baseEq = RenderSettings.ambientEquatorColor; baseGround = RenderSettings.ambientGroundColor; baseFog = RenderSettings.fogColor; skySaved = true; }
            skyTint = c; skyT = 0f;
        }

        void PushCamera(Vector3 at, float pct)
        {
            if (!HitFeelSettings.ZoomAndSlow || !HitFeelLayers.On(HitLayer.Zoom)) return;
            zoomPct = pct; zoomT = 0f; zoomPoint = at;
        }

        void UpdateVfx()
        {
            UpdateFlips();
            TickScreenLayer();
            if (desatVolume)
            {
                desatWeight = Mathf.MoveTowards(desatWeight, desatTarget, Time.deltaTime / 0.12f);
                desatVolume.weight = desatWeight;
            }
            if (skySaved && skyT < 1.2f)
            {
                skyT += Time.deltaTime;
                float k = skyT < 0.15f ? skyT / 0.15f : skyT < 0.95f ? 1f : 1f - (skyT - 0.95f) / 0.25f;
                k = Mathf.Clamp01(k) * 0.55f;
                RenderSettings.ambientSkyColor = Color.Lerp(baseSky, skyTint, k);
                RenderSettings.ambientEquatorColor = Color.Lerp(baseEq, skyTint, k);
                RenderSettings.ambientGroundColor = Color.Lerp(baseGround, skyTint * 0.6f, k);
                RenderSettings.fogColor = Color.Lerp(baseFog, skyTint, k);
            }
            if (active != null && active.Team == Team.Player && !active.Ai && !autoPlay) skipEnemyTurn = false;
        }
    }
}
