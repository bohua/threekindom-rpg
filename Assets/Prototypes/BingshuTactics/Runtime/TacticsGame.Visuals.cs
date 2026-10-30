// PROTOTYPE - NOT FOR PRODUCTION
// Question: 空轨式 AT 行动条＋小范围格子战斗，配合“学派回路镶嵌兵书→策略”，是否好玩、是否看得懂？
// Date: 2026-10-23
// 人物八方向与流畅动画（美术总监评审第 3、4 节）：
// 八方向按“相对相机”换算，手绘 5 向（下、左下、左、左上、上）、右侧 3 向镜像；±5° 回滞防抖。
// 程序侧：整条路径匀速、首尾缓动；登高走抛物线；1 像素呼吸；受击闪白约 70 ms、击退 0.15 m、命中停顿；攻击踏前 0.3 m；圆片阴影。
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Prototype.BingshuTactics
{
    public sealed partial class TacticsGame
    {
        enum SheetKind { Legacy25, Legacy9, Eight }
        enum Anim { Idle, Walk, Attack, Cast, Retreat }

        sealed class Rig
        {
            public Transform Root, Quad, Shadow; public Renderer R; public SheetKind Kind; public int Cols, Rows;
            public float Size, PivotLift; public Color Tint = Color.white;
            public Vector3 Dir = Vector3.back; public int DirIdx = -1;
            public Anim Anim; public float AnimT, WalkPhase, BreathPhase;
            public float FlashT; public Vector3 Knock; public float KnockT;
            public Vector3 Step; public float StepHold; public float Jump; public float DeadT;
            public Texture BaseTex, DuelTex; public int DuelCol = -1;   // 单挑动作帧：10 列×2 行（左下、左上）
            // 打击感（TacticsGame.HitFeel.cs）：闪白与二闪、击退停顿、挤压拉伸、出手预备与命中帧、转身溃退
            public float FlashBright, Flash2Delay, Flash2T; public bool Flash2Gold, FlashGoldNow;
            public float KnockHold, SquashT, SquashDur, WindupT, StrikeT; public int SquashPx;
            public bool Retreating; public float RetreatT; public Vector3 RetreatDir;
        }

        // 用户 2026-10-24：人物太大、显得臃肿，缩小到一半；头顶血条、飘字等高度随之乘 HeadK
        const float SpriteScale = 0.5f, HeadK = 0.55f;
        readonly Dictionary<Unit, Rig> rigs = new Dictionary<Unit, Rig>();
        Material shadowMat;
        float hitStop, yawVel;

        // 屏幕方向角：上 0、右 90、下 180、左 −90；索引 0 下、1 左下、2 左、3 左上、4 上、5 右上、6 右、7 右下
        static readonly float[] DirAngles = { 180f, -135f, -90f, -45f, 0f, 45f, 90f, 135f };

        void InitRig(Unit u, Transform root, Renderer r, float scale, Color? tint)
        {
            var tex = r.sharedMaterial.GetTexture("_BaseMap");
            var rig = new Rig { Root = root, Quad = r.transform, R = r, Tint = tint ?? Color.white, BreathPhase = (float)fxRng.NextDouble() * 10f };
            r.sharedMaterial = FlashMaterial(r.sharedMaterial);   // 每人一份打开自发光的材质，闪白插值到白
            if (tex && tex.width * 5 == tex.height * 10 && tex.width >= 640) { rig.Kind = SheetKind.Eight; rig.Cols = 10; rig.Rows = 5; }
            else
            {
                int cols = tex ? Mathf.Max(1, tex.width / tex.height) : 9;
                rig.Kind = cols >= 25 ? SheetKind.Legacy25 : SheetKind.Legacy9; rig.Cols = cols; rig.Rows = 1;
            }
            // 八向图：V3 低像素 96 像素帧（pivot (48,93)，用户 2026-10-23 定稿），或早先规划的 64 像素帧（pivot (32,58)）；旧图 48 像素帧、脚底贴底
            float px = rig.Kind == SheetKind.Eight ? tex.width / 10f : 48f;
            // V3 人物身高约 50–64 像素，按每米 36 像素显示，约 1.6 米，和一格 1.2 米相称
            float ppu = rig.Kind == SheetKind.Eight && px >= 96f ? 36f : 30f;
            float footRows = rig.Kind == SheetKind.Eight ? (px >= 96f ? 2f : 6f) : 0f;
            // 新图本身就按角色体型画好了，放大只保留一点，避免吕布过大；也不再叠加占位用的偏色
            if (rig.Kind == SheetKind.Eight) { scale = scale > 1f ? 1.08f : 1f; rig.Tint = Color.white; }
            rig.Size = px / ppu * scale * SpriteScale;
            rig.PivotLift = -footRows / ppu * scale * SpriteScale;
            r.transform.localScale = new Vector3(rig.Size, rig.Size, 1f);
            r.shadowCastingMode = ShadowCastingMode.Off;   // 人物不投实时阴影，改用圆片
            // 圆片阴影
            if (!shadowMat) { shadowMat = new Material(OverlayMat) { mainTexture = disc, color = new Color(0.06f, 0.05f, 0.05f, 0.5f) }; }
            var sh = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(sh.GetComponent<Collider>());
            sh.name = "Shadow"; sh.transform.SetParent(root, false);
            sh.transform.localRotation = Quaternion.Euler(90, 0, 0);
            sh.transform.localScale = new Vector3(T * 0.5f, T * 0.3f, 1);
            var sr = sh.GetComponent<MeshRenderer>(); sr.sharedMaterial = shadowMat; sr.shadowCastingMode = ShadowCastingMode.Off; sr.receiveShadows = false;
            rig.Shadow = sh.transform;
            rig.Dir = u.Team == Team.Player ? new Vector3(0, 0, 1) : new Vector3(0, 0, -1);
            rig.BaseTex = tex;
            rig.DuelTex = u.Key switch { "LiuBei" => LiuBeiDuel, "GuanYu" => GuanYuDuel, "ZhangFei" => ZhangFeiDuel, "LuBu" => LuBuDuel, _ => null };
            rigs[u] = rig;
        }

        void Face(Unit u, Vector3 worldDir)
        {
            worldDir.y = 0;
            if (worldDir.sqrMagnitude > 1e-4f) rigs[u].Dir = worldDir.normalized;
        }

        int DirIndex(Rig rig)
        {
            var right = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;
            var fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            float a = Mathf.Atan2(Vector3.Dot(rig.Dir, right), Vector3.Dot(rig.Dir, fwd)) * Mathf.Rad2Deg;
            if (rig.DirIdx >= 0 && Mathf.Abs(Mathf.DeltaAngle(a, DirAngles[rig.DirIdx])) < 22.5f + 5f) return rig.DirIdx;
            int best = 0; float bd = 999f;
            for (int i = 0; i < 8; i++) { float d = Mathf.Abs(Mathf.DeltaAngle(a, DirAngles[i])); if (d < bd) { bd = d; best = i; } }
            return rig.DirIdx = best;
        }

        // 取帧：返回 _BaseMap_ST（镜像时 x 缩放为负）
        Vector4 FrameST(Rig rig)
        {
            int idx = DirIndex(rig);
            switch (rig.Kind)
            {
                case SheetKind.Eight:
                {
                    int row = idx <= 4 ? idx : 8 - idx; bool flip = idx >= 5;
                    int col = rig.Anim == Anim.Walk ? 4 + Mathf.FloorToInt(rig.WalkPhase * 6f) % 6 : Mathf.FloorToInt(rig.AnimT / 0.16f) % 4;
                    float w = 1f / rig.Cols, h = 1f / rig.Rows;
                    return flip ? new Vector4(-w, h, (col + 1) * w, 1f - (row + 1) * h) : new Vector4(w, h, col * w, 1f - (row + 1) * h);
                }
                case SheetKind.Legacy25:
                {
                    // 旧四方向图：斜向就近取左右侧面
                    int row = idx == 0 ? 0 : idx == 4 ? 3 : idx < 4 ? 1 : 2;
                    int frame = rig.Anim == Anim.Walk ? 9 + row * 4 + Mathf.FloorToInt(rig.WalkPhase * 4f) % 4 : 9 + row * 4 + 1;
                    float w = 1f / rig.Cols;
                    return new Vector4(w, 1f, frame * w, 0f);
                }
                default:
                {
                    float w = 1f / rig.Cols; bool flip = idx >= 5;
                    return flip ? new Vector4(-w, 1f, w, 0f) : new Vector4(w, 1f, 0f, 0f);
                }
            }
        }

        void UpdateRigs()
        {
            bool feel = HitFeelSettings.Enabled;
            float dt = feel ? fxDt : Time.time < hitStop ? 0f : Time.deltaTime;   // 新：统一表现时钟（顿帧、慢放）；旧：命中只停人物动画
            var profile = HitFeelProfile.Current;
            var camRot = cam.transform.rotation; var up = cam.transform.up;
            foreach (var kv in rigs)
            {
                var u = kv.Key; var rig = kv.Value;
                rig.AnimT += dt;
                var retreat = Vector3.zero;
                if (!u.Alive && (!feel || rig.DeadT >= 9f))   // 旧：下沉消失；换下后备（DeadT 9）直接隐去
                {
                    rig.DeadT += Time.deltaTime;
                    rig.R.enabled = rig.DeadT < 0.5f; rig.Shadow.gameObject.SetActive(false);
                }
                else if (!u.Alive) retreat = RetreatOffset(u, rig, dt, profile);
                else rig.Retreating = false;
                if (u.Alive && rig.Anim != Anim.Walk) rig.Anim = u.PendingCast != null ? Anim.Cast : (rig.StepHold > 0 ? Anim.Attack : Anim.Idle);

                // 踏前后的回位与击退衰减
                if (rig.StepHold > 0) rig.StepHold -= dt; else rig.Step = Vector3.MoveTowards(rig.Step, Vector3.zero, dt * 2f);
                if (rig.KnockT > 0) { rig.KnockT -= dt; if (rig.KnockT <= 0) rig.Knock = Vector3.zero; }
                var scale = feel ? Deform(rig, dt, profile) : Vector2.one;
                var knock = rig.KnockT > 0 ? rig.Knock * Mathf.Clamp01(rig.KnockT / 0.2f) : Vector3.zero;

                // 呼吸：每 0.8 s 起伏 1 像素；读条时更快
                float period = rig.Anim == Anim.Cast ? 0.4f : 0.8f;
                float bob = rig.Anim == Anim.Walk ? 0f : (Mathf.Repeat(Time.time + rig.BreathPhase, period) < period / 2 ? 1f / 30f : 0f);
                float sink = u.Alive || feel ? 0f : rig.DeadT * 0.6f;

                rig.Quad.rotation = camRot;
                rig.Quad.localScale = new Vector3(rig.Size * scale.x, rig.Size * scale.y, 1f);
                rig.Quad.position = rig.Root.position + rig.Step + knock + retreat + Vector3.up * (rig.Jump + bob - sink)
                                  + up * (rig.Size / 2f + rig.PivotLift) * 0.98f * scale.y;   // 形变以脚底为锚点
                rig.Shadow.position = new Vector3(rig.Root.position.x + rig.Step.x + retreat.x, rig.Root.position.y - rig.Jump * 0f + 0.03f, rig.Root.position.z + rig.Step.z + retreat.z);
                float shrink = 1f / (1f + rig.Jump * 2f);
                rig.Shadow.localScale = new Vector3(T * 0.5f * shrink, T * 0.3f * shrink, 1);

                rig.R.GetPropertyBlock(block);
                if (rig.DuelCol >= 0 && rig.DuelTex)
                {
                    if (rig.DuelCol <= 1) rig.DuelCol = (int)(Time.time * 3f) % 2;   // 对峙架势两帧交替
                    int idx = DirIndex(rig); bool upRow = idx >= 3 && idx <= 5; bool flip = idx >= 5;
                    float w = 0.1f, h = 0.5f, row = upRow ? 1 : 0;
                    block.SetTexture("_BaseMap", rig.DuelTex);
                    block.SetVector("_BaseMap_ST", flip ? new Vector4(-w, h, (rig.DuelCol + 1) * w, 1f - (row + 1) * h) : new Vector4(w, h, rig.DuelCol * w, 1f - (row + 1) * h));
                }
                else
                {
                    if (rig.BaseTex) block.SetTexture("_BaseMap", rig.BaseTex);
                    block.SetVector("_BaseMap_ST", FrameST(rig));
                }
                var tint = rig.Tint; var glow = Color.black;
                if (feel)
                {
                    // 闪白：底色压暗、自发光推到白（或破将二闪的金色）＝插值到白
                    var (f, c) = TickFlash(rig, dt);
                    if (f > 0f) { tint = new Color(tint.r * (1f - f), tint.g * (1f - f), tint.b * (1f - f), tint.a); glow = c * f; }
                }
                // 旧：把底色推亮约 70 ms
                else if (rig.FlashT > 0) { rig.FlashT -= Time.deltaTime; tint = new Color(4f, 4f, 4f, 1f); }
                block.SetColor("_BaseColor", tint);
                block.SetColor("_EmissionColor", glow);
                rig.R.SetPropertyBlock(block);
            }
        }

        // 整条路径匀速，首尾 0.08 s 缓入缓出；高差处走抛物线跳跃（0.25 m＋每层 0.15 m）
        IEnumerator Walk(Unit u, List<Vector2Int> path)
        {
            var rig = rigs[u];
            if (path.Count < 2) { u.Pos = path[path.Count - 1]; yield break; }
            const float perTile = 0.24f, ease = 0.08f;
            int segs = path.Count - 1; float dur = segs * perTile;
            float e = Mathf.Min(ease / dur, 0.5f);
            float vmax = 1f / (1f - e);   // 梯形速度曲线的峰值（归一化）
            rig.Anim = Anim.Walk;
            for (float t = 0; ; t += fxDt)
            {
                float x = Mathf.Clamp01(t / dur);
                float s = x < e ? vmax * x * x / (2 * e) : x > 1 - e ? 1 - vmax * (1 - x) * (1 - x) / (2 * e) : vmax * (x - e / 2);
                float dist = Mathf.Clamp01(s) * segs;
                int i = Mathf.Min(segs - 1, Mathf.FloorToInt(dist)); float f = dist - i;
                var a = Tile(path[i]); var b = Tile(path[i + 1]);
                Face(u, b - a);
                var pos = Vector3.Lerp(a, b, f);
                int dh = grid.HeightAt(path[i + 1]) - grid.HeightAt(path[i]);
                rig.Jump = dh != 0 ? (0.25f + 0.15f * Mathf.Abs(dh)) * 4f * f * (1 - f) : 0f;
                rig.Root.position = pos;
                rig.WalkPhase = dist;   // 一格一个循环
                if (x >= 1f) break;
                yield return null;
            }
            rig.Jump = 0; rig.Anim = Anim.Idle;
            rig.Root.position = Tile(path[path.Count - 1]);
            u.Pos = path[path.Count - 1];
        }

        // 攻击：向目标踏前 0.3 m，停在命中帧，稍后自动回位（取代原来的“冲一下”）
        IEnumerator Lunge(Unit u, Vector2Int target)
        {
            var rig = rigs[u];
            var dir = Tile(target) - Tile(u.Pos); dir.y = 0;
            Face(u, dir);
            var to = dir.sqrMagnitude > 1e-4f ? dir.normalized * 0.3f : Vector3.zero;
            rig.StepHold = 0.25f;
            if (HitFeelSettings.Enabled) { rig.WindupT = 0.12f; rig.StrikeT = 0f; }   // 预备拉长 2 源像素
            yield return FxWait(0.12f);   // 预备
            for (float t = 0; t < 0.1f; t += fxDt) { rig.Step = Vector3.Lerp(Vector3.zero, to, t / 0.1f); yield return null; }
            rig.Step = to; rig.StepHold = 0.22f;
            if (HitFeelSettings.Enabled) { rig.WindupT = 0f; rig.StrikeT = 0.05f; }   // 命中帧最扁
        }

        // 旧命中反馈（打击感总开关关闭时用）：受击方闪白约 70 ms，击退 0.15 m 后缓回；双方停顿 70 ms
        void HitFx(Unit a, Unit t)
        {
            if (!rigs.TryGetValue(t, out var rig)) return;
            rig.FlashT = 0.07f;
            var d = Tile(t.Pos) - Tile(a.Pos); d.y = 0;
            rig.Knock = d.sqrMagnitude > 1e-4f ? d.normalized * 0.15f : Vector3.zero; rig.KnockT = 0.2f;
            hitStop = Time.time + 0.07f;
        }

        // ---------------- 咏唱锁定圈（用户 2026-10-23） ----------------
        // 单体：锁定目标脚下一圈；范围：把整个范围用一个大圈圈起来；直线与全军：每个受影响的格子一圈
        readonly List<Renderer> castRings = new List<Renderer>();
        Material ringMat;

        static Texture2D MakeRing(int n)
        {
            var t = new Texture2D(n, n) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < n; x++)
                for (int y = 0; y < n; y++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.88f) / 0.09f);
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply();
            return t;
        }

        void UpdateCastRings()
        {
            var wanted = new List<(Vector3 c, float r, Color col)>();
            foreach (var u in units.Where(x => x.Alive && x.PendingCast != null))
            {
                var s = u.PendingCast;
                var col = u.Team == Team.Player ? new Color(0.75f, 0.45f, 1f) : new Color(1f, 0.25f, 0.45f);
                float small = T * 0.48f;
                if (s.Radius >= 99 || s.Area == Area.Line)
                    foreach (var p in CastArea(u)) wanted.Add((Tile(p), small, col));
                else
                {
                    var center = u.PendingFollow != null && u.PendingFollow.Alive ? u.PendingFollow.Pos : u.PendingTarget;
                    wanted.Add((Tile(center), s.Area == Area.Single ? small : (s.Radius + 0.5f) * T * 1.05f, col));
                }
            }
            if (!ringMat) { ringMat = new Material(OverlayMat) { mainTexture = MakeRing(128) }; }
            while (castRings.Count < wanted.Count)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(q.GetComponent<Collider>());
                q.name = "Cast ring"; q.transform.rotation = Quaternion.Euler(90, 0, 0);
                var r = q.GetComponent<MeshRenderer>(); r.sharedMaterial = ringMat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                castRings.Add(r);
            }
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 5f);
            for (int i = 0; i < castRings.Count; i++)
            {
                var r = castRings[i];
                r.enabled = i < wanted.Count;
                if (!r.enabled) continue;
                var (c, rad, col) = wanted[i];
                r.transform.position = c + Vector3.up * 0.05f;
                r.transform.localScale = new Vector3(rad * 2f * (0.97f + 0.03f * pulse), rad * 2f * (0.97f + 0.03f * pulse), 1f);
                r.GetPropertyBlock(block);
                block.SetColor("_Color", new Color(col.r, col.g, col.b, pulse));
                r.SetPropertyBlock(block);
            }
        }

        // 镜头旋转：约 0.35 s 缓入缓出
        void UpdateCameraYaw() => yaw = Mathf.SmoothDampAngle(yaw, yawTarget, ref yawVel, 0.11f);
    }
}
