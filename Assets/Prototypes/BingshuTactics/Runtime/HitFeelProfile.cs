// 打击感强度表（design/hit-feel/HIT_FEEL_DESIGN.md 第 3 节建议初值）。时长用毫秒，震屏用 1080p 屏幕像素，形变用源像素。
// 放一份同名资产到 Resources/BingshuTactics/HitFeelProfile 即可调参；没有资产时用代码里的初值。
using UnityEngine;

namespace Prototype.BingshuTactics
{
    [System.Serializable]
    public struct HitTierFx
    {
        public HitTier Tier;
        public int FlashMs; public float FlashBright;      // 闪白时长与亮度（≤0.9 不触发泛光，1.3 自然发光）
        public int SecondFlashGapMs, SecondFlashMs; public bool SecondFlashGold;   // 击破、破将的二闪（破将为金色 #FFD23A）
        public float KnockM;                                // 击退（米，视觉位移，回弹原格）
        public int SquashPx, SquashMs;                      // 挤压拉伸（源像素，上限 10%）
        public int StopMs;                                  // 顿帧
        public int ShakePx, ShakeMs;                        // 方向性震屏
        public float ZoomPct;                               // 镜头推近
        public float SlowFactor; public int SlowMs;         // 慢放
    }

    [CreateAssetMenu(menuName = "Bingshu Tactics/Hit Feel Profile", fileName = "HitFeelProfile")]
    public sealed class HitFeelProfile : ScriptableObject
    {
        public HitTierFx[] Tiers = Defaults();
        public int WindupStretchPx = 2;        // 出手预备拉长
        public float MaxDeform = 0.10f;        // 形变上限 10%
        public float SourcePxHeight = 60f;     // Q 版人物身高约 60 源像素
        public float HeavyShakeCooldown = 1.5f;   // T1 震屏全局冷却（秒）
        public int MaxFlashesPerSecond = 3;    // 光敏安全：全屏合计每秒最多 3 次闪白
        public float RetreatPauseS = 0.1f, RetreatRunM = 0.6f, RetreatRunS = 0.45f, RetreatVanishS = 0.3f;

        public HitTierFx For(HitTier t)
        {
            foreach (var f in Tiers) if (f.Tier == t) return f;
            return Tiers[1];
        }

        static HitTierFx Row(HitTier t, int flashMs, float bright, float knock, int squashPx, int squashMs, int stopMs, int shakePx = 0, int shakeMs = 0) =>
            new HitTierFx { Tier = t, FlashMs = flashMs, FlashBright = bright, KnockM = knock, SquashPx = squashPx, SquashMs = squashMs, StopMs = stopMs, ShakePx = shakePx, ShakeMs = shakeMs };

        public static HitTierFx[] Defaults()
        {
            var dull = Row(HitTier.Dull, 0, 0f, 0.04f, 0, 0, 0);
            var normal = Row(HitTier.Normal, 50, 0.9f, 0.08f, 2, 33, 33);
            var heavy = Row(HitTier.Heavy, 70, 1.3f, 0.15f, 4, 50, 50, 2, 120);
            var brk = Row(HitTier.Break, 70, 1.3f, 0.25f, 4, 50, 83, 4, 150); brk.SecondFlashGapMs = 33; brk.SecondFlashMs = 33;
            var rout = Row(HitTier.Rout, 70, 1.4f, 0.30f, 6, 66, 133, 8, 250); rout.SecondFlashGapMs = 33; rout.SecondFlashMs = 33; rout.SecondFlashGold = true; rout.ZoomPct = 0.04f;
            var duel = Row(HitTier.Duel, 70, 1.3f, 0.20f, 6, 50, 67);
            var final = Row(HitTier.DuelFinal, 100, 1.4f, 0.40f, 6, 66, 200, 8, 300); final.ZoomPct = 0.04f; final.SlowFactor = 0.3f; final.SlowMs = 300;
            return new[] { dull, normal, heavy, brk, rout, duel, final };
        }

        static HitFeelProfile cached;
        public static HitFeelProfile Current
        {
            get
            {
                if (cached) return cached;
                cached = Resources.Load<HitFeelProfile>("BingshuTactics/HitFeelProfile");
                if (!cached) { cached = CreateInstance<HitFeelProfile>(); cached.name = "HitFeelProfile (defaults)"; cached.hideFlags = HideFlags.DontSave; }
                return cached;
            }
        }
    }
}
