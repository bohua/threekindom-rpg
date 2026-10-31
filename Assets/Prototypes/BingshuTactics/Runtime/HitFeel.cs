// 战斗打击感（design/hit-feel/HIT_FEEL_DESIGN.md，用户 2026-10-30 批准 P0＋P1）
// 铁律：打击感只读取战斗结果，不写回；不改伤害、命中、AT、气势、策力，不加暴击。
// 本文件是纯数据与纯函数：命中事件、档位解析、设置项与开发版分层开关。
using UnityEngine;

namespace Prototype.BingshuTactics
{
    // 档位（稿第 2 节）：钝 → 普通 → 重击 → 击破 → 破将 → 单挑（每回合／决胜）
    public enum HitTier { Dull = -1, Normal = 0, Heavy = 1, Break = 2, Rout = 3, Duel = 4, DuelFinal = 5 }

    public enum HitSource { Attack, Technique, Strategy, Counter, Duel }

    // 一次命中已经发生的事实；由 TacticsGame.Hit 在不改任何结果的前提下组装
    public sealed class HitEvent
    {
        public string Attacker, Target;
        public Team AttackerTeam, TargetTeam;
        public HitSource Source;
        public Element Element;
        public int Damage;                 // 实际扣除的兵力（下限截断之后）
        public float AdvMul = 1f;          // 兵种＋五行倍率（Combat.AdvMul）
        public float HeightMul = 1f;       // 居高倍率（Combat.HeightMul）
        public bool TroopAdvantage, ElementAdvantage, Charged;
        public bool Floored;               // 被“不可击败”下限截断（首领）
        public bool Blocked;               // 防御、结阵、营寨、众望代受：盾面亮边而非闪白
        public bool Broke;                 // 本次命中使目标撤退
        public bool NamedTarget, BossTarget, BossAttacker;
        public bool LastBlow;              // 结束整场战斗的最后一击（或单挑决胜）
        public Vector3 Dir;                // 世界方向：攻方 → 受方（水平）

        public bool PositiveReason => TroopAdvantage || ElementAdvantage || HeightMul > 1.001f || Charged;
        public bool NegativeReason => AdvMul < 0.999f || HeightMul < 0.999f;

        public string Reason => TroopAdvantage ? "压制" : ElementAdvantage ? "克制" : HeightMul > 1.001f ? "居高" : null;

        public override string ToString() =>
            $"{Attacker}>{Target} {Source} dmg={Damage} adv={AdvMul:0.00} h={HeightMul:0.00}{(Floored ? " floored" : "")}{(Blocked ? " blocked" : "")}{(Broke ? " broke" : "")}{(LastBlow ? " last" : "")}";
    }

    public static class HitFeel
    {
        // 取满足条件的最高一档（稿第 2 节）
        public static HitTier Resolve(HitEvent e)
        {
            if (e == null) return HitTier.Normal;
            if (e.Source == HitSource.Duel) return e.LastBlow ? HitTier.DuelFinal : HitTier.Duel;
            if (e.Broke) return e.NamedTarget || e.BossTarget || e.LastBlow ? HitTier.Rout : HitTier.Break;
            if (e.Floored && e.BossTarget) return HitTier.Rout;   // 变体“不动如山”
            // 重击是“我选对了”的反馈：兵种、五行、居高这些原因只在我方出手时算；武技、绝学两边都算。
            // （100 场统计里敌打我凭兵种压制的命中占了重击的四成，重击比例超过 30% 的目标，按稿第 2 节收紧）
            if ((e.PositiveReason && e.AttackerTeam == Team.Player) || e.Source == HitSource.Technique) return HitTier.Heavy;
            if (e.NegativeReason || e.Damage <= 1) return HitTier.Dull;
            return HitTier.Normal;
        }

        public static string Name(HitTier t) => t switch
        {
            HitTier.Dull => "钝", HitTier.Normal => "普通", HitTier.Heavy => "重击", HitTier.Break => "击破",
            HitTier.Rout => "破将", HitTier.Duel => "单挑", _ => "单挑决胜"
        };

        // 敌打我时震屏降一档（首领绝学除外，稿第 2 节“我军受击修正”）
        public static HitTier ShakeTier(HitEvent e, HitTier t)
        {
            bool enemyOnUs = e.AttackerTeam == Team.Enemy && e.TargetTeam == Team.Player;
            bool bossSignature = e.BossAttacker && e.Source == HitSource.Technique;
            return enemyOnUs && !bossSignature && t > HitTier.Dull && t < HitTier.Duel ? t - 1 : t;
        }

        // 我方被击破是“失利”：不升档表演，只作溃退与短暂去饱和
        public static bool FriendlyLoss(HitEvent e) => e.Broke && e.TargetTeam == Team.Player;
    }

    // 设置项（稿 4.13）：存本机 PlayerPrefs，与系统页其他设置同一处；主线菜单另做界面（需先出设计图）
    public static class HitFeelSettings
    {
        public const string ShakeKey = "settings.hitfeel.shake", FlashKey = "settings.hitfeel.flash",
                            ZoomKey = "settings.hitfeel.zoomSlow", RippleKey = "settings.hitfeel.gridRipple";

        // 震屏：0 关／1 弱（减半）／2 标准
        public static int Shake { get => PlayerPrefs.GetInt(ShakeKey, 2); set => PlayerPrefs.SetInt(ShakeKey, Mathf.Clamp(value, 0, 2)); }
        // 闪白：0 关（改描边亮边）／1 柔和／2 标准
        public static int Flash { get => PlayerPrefs.GetInt(FlashKey, 2); set => PlayerPrefs.SetInt(FlashKey, Mathf.Clamp(value, 0, 2)); }
        public static bool ZoomAndSlow { get => PlayerPrefs.GetInt(ZoomKey, 1) == 1; set => PlayerPrefs.SetInt(ZoomKey, value ? 1 : 0); }
        public static bool GridRipple { get => PlayerPrefs.GetInt(RippleKey, 1) == 1; set => PlayerPrefs.SetInt(RippleKey, value ? 1 : 0); }

        // 技能特写：0 关／1 存档首次完整（之后简短）／2 每场首次完整（同场再放仍用简短版）
        public const string CutinKey = "settings.hitfeel.cutin";
        public static int Cutin { get => PlayerPrefs.GetInt(CutinKey, 1); set => PlayerPrefs.SetInt(CutinKey, Mathf.Clamp(value, 0, 2)); }

        public static float ShakeScale => Shake == 0 ? 0f : Shake == 1 ? 0.5f : 1f;

        // 总开关：关＝改动前的旧反馈（闪白 70 ms、击退 0.15 m、停 70 ms、下沉消失）。测试用它做开／关对比。
        public static bool Enabled = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-hitfeelOff") < 0;
    }

    // 开发版分层调试开关（稿第 6 节第 8 条）：逐层开关，方便试玩时说“第几层太多了”
    public enum HitLayer { Flash, Knock, Squash, Stop, Shake, Zoom, Retreat }

    public static class HitFeelLayers
    {
        static readonly bool[] on = { true, true, true, true, true, true, true };
        public static bool On(HitLayer l) => on[(int)l];
        public static void Toggle(HitLayer l) => on[(int)l] = !on[(int)l];
        public static void Reset() { for (int i = 0; i < on.Length; i++) on[i] = true; }
        public static string Name(HitLayer l) => l switch
        {
            HitLayer.Flash => "闪白", HitLayer.Knock => "击退", HitLayer.Squash => "挤压拉伸", HitLayer.Stop => "顿帧",
            HitLayer.Shake => "震屏", HitLayer.Zoom => "推近慢放", _ => "转身溃退"
        };
    }
}
