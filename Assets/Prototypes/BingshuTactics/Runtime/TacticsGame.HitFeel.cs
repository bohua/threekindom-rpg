// 战斗打击感 P0＋P1（design/hit-feel/HIT_FEEL_DESIGN.md 第 2、3、4、6 节；用户 2026-10-30 批准）
// P0：随机源隔离、命中事件层、档位解析、强度表、行动级峰值槽、统一表现时钟、分层调试开关。
// P1：闪白插值到白并分档、击退分档、挤压拉伸、顿帧分档、方向性震屏、破将推近、转身溃退、设置项。
// 铁律：这里只读战斗结果，不写回；顿帧与慢放只停表现与动作的播放，不推进、不改变 AT。
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Prototype.BingshuTactics
{
    public sealed partial class TacticsGame
    {
        // ---------------- 随机源（稿第 6 节第 1 条） ----------------
        // 战斗随机（敌军放策略的六成判定、单挑吕布出招）用带种子的独立随机源，重开同一场结果一致；
        // 表现随机（呼吸相位、火焰相位、溃退抖动）用另一个随机源，表现增减不会挤动战局。
        public static int? SeedOverride;   // 测试：同一种子跑打击感开／关两次，逐行比对战斗日志
        System.Random battleRng;
        readonly System.Random fxRng = new System.Random();
        System.Random BattleRng => battleRng ??= new System.Random(IsChapter ? Chapter.Seed : SeedOverride ?? System.Environment.TickCount);

        // 测试与统计：战斗日志行、每一次命中事件及其档位
        public static System.Action<string> BattleLogged;
        public static System.Action<HitEvent, HitTier> HitLogged;

        // ---------------- 统一表现时钟（稿第 6 节第 6 条） ----------------
        // 顿帧：剩余时长倒计时，至少停一帧；慢放：表现时间乘系数。人物、闪白、飘字、格子闪色、踏步与行走都读 fxDt。
        float fxDt, stopLeft, slowLeft, slowFactor = 1f; int stopFrames;

        void TickFxClock()
        {
            float dt = Time.deltaTime;
            if (!HitFeelSettings.Enabled) { fxDt = dt; stopLeft = slowLeft = 0f; stopFrames = 0; return; }
            if (stopLeft > 0f && ConfirmPressed && mode == Mode.Busy) stopLeft = 0f;   // 按键跳过剩余停顿
            if (stopFrames > 0 || stopLeft > 0f) { fxDt = 0f; stopLeft -= dt; if (stopFrames > 0) stopFrames--; }
            else if (slowLeft > 0f) { fxDt = dt * slowFactor; slowLeft -= dt; }
            else fxDt = dt;
        }

        // 动作流程里的等待改用表现时钟：顿帧期间整段动作一起停，下一个人不会在定格时走动
        IEnumerator FxWait(float seconds)
        {
            for (float t = 0f; t < seconds; t += fxDt) yield return null;
        }

        // ---------------- 命中事件（稿第 6 节第 2 条） ----------------
        SkillDef hitSkill;   // Apply 循环里当前这一招（五行原因要用它的属性）
        Unit coveredBy;      // Cover 换了目标：本次命中算“代受”

        HitEvent MakeHitEvent(Unit a, Unit t, HitKind kind)
        {
            bool strategy = kind == HitKind.Strategy;
            var element = kind == HitKind.Attack || kind == HitKind.Counter ? Element.None : hitSkill?.Element ?? Element.None;
            bool melee = !strategy && Grid.Dist(a.Pos, t.Pos) <= 1;
            int troopPts = strategy ? TroopTraits.StrategyPoints(t.Troop, element) : TroopTraits.Points(a.Troop, t.Troop, melee, a.Charged, a.Rank);
            var dir = Tile(t.Pos) - Tile(a.Pos); dir.y = 0f;
            return new HitEvent
            {
                Attacker = a.Name, Target = t.Name, AttackerTeam = a.Team, TargetTeam = t.Team,
                Source = kind switch { HitKind.Technique => HitSource.Technique, HitKind.Strategy => HitSource.Strategy, HitKind.Counter => HitSource.Counter, _ => HitSource.Attack },
                Element = element,
                AdvMul = Combat.AdvMul(a, t, element, strategy, melee), HeightMul = HMul(a, t),
                TroopAdvantage = troopPts > 0, ElementAdvantage = element != Element.None && Wuxing.Overcomes(element) == t.Element,
                Blocked = t.Defending || t.Guard > 0f || coveredBy == t,
                NamedTarget = t.Leader || t.Escort || (t.Team == Team.Player && !t.Ai),
                BossTarget = t.Floor > 0f, BossAttacker = a.Floor > 0f || (a.Team == Team.Enemy && a.Leader && kind == HitKind.Technique),
                Dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : (rigs.TryGetValue(a, out var ar) ? ar.Dir : Vector3.forward),
            };
        }

        // Hit 调用：只登记，LateUpdate 取本帧最高档统一触发
        void RecordHit(HitEvent e, Unit a, Unit t, int damage, bool floored)
        {
            e.Damage = damage; e.Floored = floored; e.Broke = !t.Alive;
            if (e.Floored) e.Blocked = true;
            e.LastBlow = e.Broke && ((t.Team == Team.Enemy && !units.Any(x => x.Alive && x.Team == Team.Enemy)) || (!IsChapter && stage == 1 && t.Name == "吕布"));
            coveredBy = null;
            var tier = HitFeel.Resolve(e);
            HitLogged?.Invoke(e, tier);
            pendingHits.Add((e, tier, a, t));
        }

        // ---------------- 行动级峰值槽（稿第 6 节第 5 条） ----------------
        // 一次行动只有一个峰值：顿帧、震屏、推近各最多一次，取这次行动的最高档；更高档出现才再触发一次。
        readonly List<(HitEvent e, HitTier tier, Unit a, Unit t)> pendingHits = new List<(HitEvent, HitTier, Unit, Unit)>();
        HitTier slotPeak = (HitTier)(-9);
        readonly Queue<float> flashTimes = new Queue<float>();
        float lastHeavyShake = -99f;

        // 光敏：全屏合计每秒最多 3 次闪白（受击闪白、二闪、特写眼部横光都计数）
        bool FlashBudgetLeft
        {
            get
            {
                while (flashTimes.Count > 0 && Time.time - flashTimes.Peek() > 1f) flashTimes.Dequeue();
                return flashTimes.Count < HitFeelProfile.Current.MaxFlashesPerSecond;
            }
        }

        void CountFlash() { flashTimes.Enqueue(Time.time); FlashCounted?.Invoke(Time.time); }

        public static System.Action<float> FlashCounted;   // 测试：每一次计入的闪白
        public static System.Action<float> ShowTimeAdded;  // 测试：演出等待的累计时间（节奏预算）
        public static bool ForceFullShows;                 // 测试：电脑代打也放完整版

        void OpenSlot() { FlushHits(); slotPeak = (HitTier)(-9); }
        void CloseSlot() { FlushHits(); slotPeak = (HitTier)(-9); }

        void LateUpdate() => FlushHits();

        void FlushHits()
        {
            if (pendingHits.Count == 0) return;
            var batch = pendingHits.ToList(); pendingHits.Clear();
            if (!HitFeelSettings.Enabled) return;   // 旧反馈已在 Hit 里直接播放
            var p = HitFeelProfile.Current;
            // 光敏：全屏合计每秒最多 3 次闪白，超过时改为柔和的亮边；同一帧的范围命中一起闪
            while (flashTimes.Count > 0 && Time.time - flashTimes.Peek() > 1f) flashTimes.Dequeue();
            bool soft = flashTimes.Count >= p.MaxFlashesPerSecond;
            bool flashed = false;
            foreach (var h in batch) flashed |= TargetFx(h.e, h.tier, h.a, h.t, soft, p);
            if (flashed) CountFlash();

            var top = batch.OrderByDescending(h => (int)h.tier).First();
            if ((int)top.tier <= (int)slotPeak) return;
            slotPeak = top.tier;
            PeakFx(top.e, top.tier, top.t, p);
        }

        // 每个受击者自己的反馈：闪白（或亮边）、击退、挤压拉伸、击破后的溃退方向。返回是否闪了白。
        bool TargetFx(HitEvent e, HitTier tier, Unit a, Unit t, bool soft, HitFeelProfile p)
        {
            if (!rigs.TryGetValue(t, out var rig)) return false;
            bool friendlyLoss = HitFeel.FriendlyLoss(e);
            var fx = p.For(friendlyLoss && tier == HitTier.Rout ? HitTier.Break : tier);
            bool flashed = false;
            if (HitFeelLayers.On(HitLayer.Flash))
            {
                if (e.Blocked || HitFeelSettings.Flash == 0 || soft)
                {
                    // 挡住了：一道短而暗的亮边，不触发泛光（盾面亮边的美术留给 P2）
                    if (tier > HitTier.Dull) { rig.FlashT = 0.04f; rig.FlashBright = 0.4f; rig.Flash2Delay = rig.Flash2T = 0f; rig.FlashGoldNow = false; }
                }
                else if (fx.FlashMs > 0)
                {
                    float bright = HitFeelSettings.Flash == 1 ? Mathf.Min(0.75f, fx.FlashBright * 0.6f) : fx.FlashBright;
                    rig.FlashT = fx.FlashMs / 1000f; rig.FlashBright = bright; rig.FlashGoldNow = false;
                    bool second = fx.SecondFlashMs > 0 && HitFeelSettings.Flash == 2;
                    rig.Flash2Delay = second ? rig.FlashT + fx.SecondFlashGapMs / 1000f : 0f;
                    rig.Flash2T = second ? fx.SecondFlashMs / 1000f : 0f;
                    rig.Flash2Gold = fx.SecondFlashGold && !friendlyLoss;
                    flashed = true;
                }
            }
            if (HitFeelLayers.On(HitLayer.Knock) && !(e.Floored && e.BossTarget))   // 不动如山：首领不后退
            {
                rig.Knock = e.Dir * fx.KnockM; rig.KnockT = e.Broke ? 0.2f + p.RetreatPauseS : 0.2f;
            }
            rig.KnockHold = e.Broke ? p.RetreatPauseS : 0f;
            if (HitFeelLayers.On(HitLayer.Squash) && fx.SquashPx > 0) { rig.SquashPx = fx.SquashPx; rig.SquashDur = rig.SquashT = Mathf.Max(fx.SquashMs, 17) / 1000f; }
            if (e.Broke) { rig.RetreatDir = e.Dir; rig.RetreatT = 0f; rig.Retreating = true; }
            return flashed;
        }

        // 这次行动的峰值：顿帧、方向性震屏、破将推近、单挑决胜慢放
        void PeakFx(HitEvent e, HitTier tier, Unit t, HitFeelProfile p)
        {
            var fx = p.For(tier);
            bool fast = autoPlay && !FullFeelInAuto;   // 电脑代打：顿帧减半，普通／重击不震屏（稿第 3 节第 5 条）；录像用 -hitfeelFull 取消
            if (HitFeelLayers.On(HitLayer.Stop) && fx.StopMs > 0)
            {
                stopLeft = Mathf.Max(stopLeft, fx.StopMs / 1000f * (fast ? 0.5f : 1f)); stopFrames = 1;
            }
            if (HitFeelLayers.On(HitLayer.Shake) && !HitFeel.FriendlyLoss(e))
            {
                var st = HitFeel.ShakeTier(e, tier);
                var sfx = p.For(st);
                bool cooled = st != HitTier.Heavy || Time.time - lastHeavyShake >= p.HeavyShakeCooldown;
                if (sfx.ShakePx > 0 && cooled && !(fast && st <= HitTier.Heavy) && HitFeelSettings.ShakeScale > 0f)
                {
                    if (st == HitTier.Heavy) lastHeavyShake = Time.time;
                    shakeDir = e.Dir; shakePx = sfx.ShakePx * HitFeelSettings.ShakeScale; shakeDur = sfx.ShakeMs / 1000f; shakeT = 0f;
                }
            }
            if (HitFeelLayers.On(HitLayer.Zoom) && HitFeelSettings.ZoomAndSlow && fx.ZoomPct > 0f && !HitFeel.FriendlyLoss(e))
            {
                zoomPct = fx.ZoomPct; zoomT = 0f; zoomPoint = views.TryGetValue(t, out var v) ? v.position : camTarget;
                if (fx.SlowMs > 0) { slowLeft = fx.SlowMs / 1000f; slowFactor = fx.SlowFactor; }
            }
            lastTier = tier; lastTierT = Time.time;
        }

        // ---------------- 方向性震屏与推近（稿 4.6、4.7） ----------------
        Vector3 shakeDir, zoomPoint; float shakePx, shakeDur, shakeT = 99f, zoomPct, zoomT = 99f;

        // 由 UpdateCamera 在摆好镜头后调用：返回镜头的位移（世界坐标），distK 为推近后的距离系数
        Vector3 HitCamOffset(float dist, out float distK, out Vector3 lookShift)
        {
            distK = 1f; lookShift = Vector3.zero;
            if (!HitFeelSettings.Enabled) return Vector3.zero;
            var off = Vector3.zero;
            if (shakeT < shakeDur)
            {
                shakeT += Time.deltaTime;
                float x = Mathf.Clamp01(shakeT / shakeDur);
                // 先推后回，再小幅回摆一次
                float k = x < 0.25f ? x / 0.25f : x < 0.65f ? Mathf.Lerp(1f, -0.35f, (x - 0.25f) / 0.4f) : Mathf.Lerp(-0.35f, 0f, (x - 0.65f) / 0.35f);
                var right = cam.transform.right; var up = cam.transform.up;
                var sd = new Vector2(Vector3.Dot(shakeDir, right), Vector3.Dot(shakeDir, up));
                if (sd.sqrMagnitude < 1e-4f) sd = Vector2.right;
                sd.Normalize();
                // 1080p 像素换算到当前分辨率并取整，避免亚像素发抖
                float px = Mathf.Round(shakePx * k * Screen.height / 1080f);
                float worldPerPx = 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, Screen.height);
                off = (right * sd.x + up * sd.y) * px * worldPerPx;
            }
            if (zoomT < 0.6f)
            {
                zoomT += Time.deltaTime;
                float k = zoomT < 0.1f ? zoomT / 0.1f : zoomT < 0.3f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (zoomT - 0.3f) / 0.3f);
                distK = 1f - zoomPct * k;
                lookShift = (zoomPoint - camTarget) * 0.25f * k;
            }
            return off;
        }

        // ---------------- 人物：闪白、挤压拉伸、溃退 ----------------
        // 闪白（稿 4.1）：底色压暗、自发光推白＝像素颜色插值到白，描边也变白；亮度 ≤0.9 不触发泛光，1.3 起自然发光。
        // 人物材质沿用 URP Lit（HD-2D 打光不变），每人一份打开自发光的材质；Resources 里的模板保证打包时保留这个变体。
        static Material FlashMaterial(Material shared)
        {
            if (!shared) return shared;
            var m = new Material(shared) { name = shared.name + " (hit flash)" };
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            return m;
        }

        static readonly Color Gold = new Color(1f, 0.824f, 0.227f);   // #FFD23A

        // 返回本帧的闪白强度与颜色（由 UpdateRigs 写进材质块）
        (float f, Color c) TickFlash(Rig rig, float dt)
        {
            if (rig.Flash2Delay > 0f)
            {
                rig.Flash2Delay -= dt;
                if (rig.Flash2Delay <= 0f)
                {
                    // 二闪也计入全屏每秒 3 次的光敏上限；满了就不闪
                    if (FlashBudgetLeft) { rig.FlashT = rig.Flash2T; rig.FlashGoldNow = rig.Flash2Gold; CountFlash(); }
                }
            }
            if (rig.FlashT > 0f) { rig.FlashT -= dt; return (1f, (rig.FlashGoldNow ? Gold : Color.white) * rig.FlashBright); }
            return (0f, Color.black);
        }

        // 挤压拉伸（稿 4.3）：正＝拉高变窄，负＝压扁变宽；按源像素取整、保持面积、以脚底为锚点，上限 10%
        Vector2 Deform(Rig rig, float dt, HitFeelProfile p)
        {
            float px = 0f;
            if (rig.SquashT > 0f) { rig.SquashT -= dt; px = Mathf.Round(rig.SquashPx * Mathf.Clamp01(rig.SquashT / rig.SquashDur) + 0.49f); }
            else if (rig.StrikeT > 0f) { rig.StrikeT -= dt; px = -p.WindupStretchPx; }
            else if (rig.WindupT > 0f) { rig.WindupT -= dt; px = p.WindupStretchPx; }
            if (px == 0f || !HitFeelLayers.On(HitLayer.Squash)) return Vector2.one;
            float sy = Mathf.Clamp(1f + px / p.SourcePxHeight, 1f - p.MaxDeform, 1f + p.MaxDeform);
            return new Vector2(1f / sy, sy);
        }

        // 撤退（稿 4.2）：击退停顿后转身背向跑 0.6 m，再像素抖动消失；“撤退不是阵亡”，不再下沉
        Vector3 RetreatOffset(Unit u, Rig rig, float dt, HitFeelProfile p)
        {
            if (!rig.Retreating) { rig.Retreating = true; rig.RetreatT = 0f; rig.RetreatDir = -rig.Dir; }   // 剧情退走、换下后备：没有命中方向时转身走开
            rig.RetreatT += dt;
            float run0 = rig.KnockHold, run1 = run0 + p.RetreatRunS, end = run1 + p.RetreatVanishS;
            var dir = rig.RetreatDir; dir.y = 0f; if (dir.sqrMagnitude < 1e-4f) dir = Vector3.back; dir.Normalize();
            rig.Shadow.gameObject.SetActive(rig.RetreatT < run1);
            if (rig.RetreatT < run0) { rig.R.enabled = true; return Vector3.zero; }
            Face(u, dir);
            if (rig.RetreatT < run1)
            {
                float x = (rig.RetreatT - run0) / p.RetreatRunS;
                rig.Anim = Anim.Walk; rig.WalkPhase += dt * 4f; rig.R.enabled = true;
                return dir * p.RetreatRunM * (x * (2f - x));
            }
            rig.Anim = Anim.Idle;
            if (rig.RetreatT >= end || !HitFeelLayers.On(HitLayer.Retreat)) { rig.R.enabled = false; return dir * p.RetreatRunM; }
            // 像素抖动：每帧左右跳 1 个源像素，并越来越频繁地消失
            float v = (rig.RetreatT - run1) / p.RetreatVanishS;
            rig.R.enabled = fxRng.NextDouble() > v;
            float onePx = rig.Size / 96f * 2f;
            return dir * p.RetreatRunM + cam.transform.right * (fxRng.Next(0, 2) == 0 ? -onePx : onePx);
        }

        // ---------------- 单挑（稿 5.6）：每回合交锋单独开槽，决胜一击单独一档 ----------------
        void DuelHit(Unit a, Unit t, int amount, bool final)
        {
            if (!HitFeelSettings.Enabled) { HitFx(a, t); return; }
            OpenSlot();
            var dir = Tile(t.Pos) - Tile(a.Pos); dir.y = 0f;
            var e = new HitEvent
            {
                Attacker = a.Name, Target = t.Name, AttackerTeam = a.Team, TargetTeam = t.Team, Source = HitSource.Duel,
                Damage = amount, LastBlow = final, BossTarget = t.Floor > 0f, NamedTarget = true, Dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward
            };
            var tier = HitFeel.Resolve(e);
            HitLogged?.Invoke(e, tier);
            pendingHits.Add((e, tier, a, t));
        }

        // ---------------- 开发版分层调试开关（稿第 6 节第 8 条） ----------------
        // F1 打击感总开关（关＝改动前的旧反馈）；F2–F8 依次开关：闪白、击退、挤压拉伸、顿帧、震屏、推近慢放、转身溃退
        HitTier lastTier = HitTier.Normal; float lastTierT = -99f, debugShowT = -99f;
        static bool DevBuild => Application.isEditor || Debug.isDebugBuild;
        static readonly bool FullFeelInAuto = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-hitfeelFull") >= 0;

        void HitFeelDebugKeys()
        {
            if (!DevBuild || Kb == null) return;
            if (Kb.f1Key.wasPressedThisFrame) { HitFeelSettings.Enabled = !HitFeelSettings.Enabled; debugShowT = Time.time; }
            var keys = new[] { Kb.f2Key, Kb.f3Key, Kb.f4Key, Kb.f5Key, Kb.f6Key, Kb.f7Key, Kb.f8Key };
            for (int i = 0; i < keys.Length; i++)
                if (keys[i].wasPressedThisFrame) { HitFeelLayers.Toggle((HitLayer)i); debugShowT = Time.time; }
        }

        // 录像（-hitfeelCapture <目录> [秒]）：固定 30 帧／秒逐帧存图，同一种子开／关各录一次做前后对比；-hitfeelOff 以旧反馈启动
        IEnumerator HitFeelCapture()
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-hitfeelCapture");
            if (i < 0 || i + 1 >= args.Length) yield break;
            string dir = args[i + 1]; System.IO.Directory.CreateDirectory(dir);
            float seconds = i + 2 < args.Length && float.TryParse(args[i + 2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : 40f;
            Time.captureFramerate = 30;
            int frames = Mathf.RoundToInt(seconds * 30f);
            for (int f = 0; f < frames && !ChapterFinished; f++)
            {
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"f{f:00000}.png"));
            }
            Debug.Log($"HITFEEL_CAPTURE done {dir} feel={HitFeelSettings.Enabled}");
            Application.Quit();
        }

        void DrawHitFeelDebug()
        {
            if (!DevBuild || Time.time - debugShowT > 4f) return;
            var r = new Rect(12, 120, 230, 24 + 20 * 8);
            Rect(r, new Color(0.04f, 0.06f, 0.1f, 0.85f));
            GUI.Label(new Rect(r.x + 10, r.y + 4, 220, 20), $"F1 打击感：{(HitFeelSettings.Enabled ? "新" : "旧（改动前）")}", small);
            for (int i = 0; i < 7; i++)
            {
                var l = (HitLayer)i;
                GUI.Label(new Rect(r.x + 10, r.y + 24 + 20 * i, 220, 20), $"F{i + 2} {HitFeelLayers.Name(l)}：{(HitFeelLayers.On(l) ? "开" : "<color=#ff8a70>关</color>")}", small);
            }
            if (Time.time - lastTierT < 4f) GUI.Label(new Rect(r.x + 10, r.y + 24 + 20 * 7, 220, 20), $"上一峰值：{HitFeel.Name(lastTier)}", small);
        }
    }
}
