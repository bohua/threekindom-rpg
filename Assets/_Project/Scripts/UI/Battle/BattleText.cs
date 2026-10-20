using ThreeKingdoms.Core.Battle;

namespace ThreeKingdoms.UI.Battle
{
    // Every battle string in one place (simplified Chinese, user 2026-10-19). ADR-0007 replaces this with the
    // localisation tables; callers only use these methods, never inline text.
    public static class BattleText
    {
        public static string Troop(TroopType t) => t switch
        {
            TroopType.Infantry => "步兵",
            TroopType.Cavalry => "骑兵",
            TroopType.Archer => "弓兵",
            TroopType.YellowTurban => "黄巾军",
            _ => "文官／军师",
        };

        public static string Stat(BattleStat s) => s switch
        {
            BattleStat.PhysicalAttack => "物攻",
            BattleStat.PhysicalDefense => "物防",
            BattleStat.StrategyAttack => "计攻",
            BattleStat.StrategyDefense => "计防",
            _ => "速度",
        };

        public static string Command(CommandKind k) => k switch
        {
            CommandKind.Attack => "攻击",
            CommandKind.Skill => "技能",
            CommandKind.Defend => "防御",
            _ => "道具",
        };

        public const string Troops = "兵力";
        public const string Tech = "技力";
        public const string ChooseCommand = "选择指令";
        public const string ChooseTarget = "选择目标";
        public const string Back = "返回";
        public const string DefendHelp = "防御：本回合最先生效，受到的伤害减半";
        public const string Victory = "胜利";
        public const string Defeat = "战败";
        public const string Retry = "重新挑战";
        public const string Again = "再战一次";
        public const string NextBattle = "下一战";
        public const string ReturnToTown = "回到涿郡";
        public const string Continue = "继续";
        public const string ToTitle = "返回标题";
        public const string TestVersionEnd = "测试版结束";
        public const string ThanksForPlaying = "感谢试玩";
        // Alpha 0.1 ending narration (story-events answer 5). 暂定 — final wording to be written.
        public static readonly string[] EndingNarration =
        {
            "黄巾主力兵败，张角三兄弟各自退走，涿郡城外重归平静。",
            "刘备、关羽、张飞三人义兵之名，自此渐渐传开。",
        };
        public const string AllBattlesDone = "三场黄巾教学战全部完成";
        public const string Fast = "加速";
        public static string Level(int level) => $"Lv.{level}";
        public static string Round(int round) => $"第{round}回合";
        public static string Ration(string name, int count) => $"{name} ×{count}";
        public static string SkillCost(int tech) => $"{tech} 技力";
        public static string Remaining(StatModifier m) => $"{Stat(m.Stat)}{(m.Percent > 0 ? "+" : "")}{m.Percent}%（剩{m.RemainingTurns}回合）";
        public static string Order(string names) => $"行动顺序：{names}";
        public static string ExpGain(int exp) => $"全员获得经验 {exp}";
        public static string CopperGain(int copper) => $"获得铜钱 {copper}";
        public static string LevelUp(string name, int to) => $"{name} 升到 {to} 级";
        public static string Learned(string name, string skill) => $"{name} 学会【{skill}】";
        public const string DefeatHint = "我军出战部队全部撤退。可还原战前状态重新挑战，不损失道具。";
        public const string NoRewardAgain = "本场奖励已领取过，不再重复发放。";

        static string Reason(CancelReason r) => r switch
        {
            CancelReason.TargetInvalid => "目标已无效",
            CancelReason.NotEnoughTech => "技力不足",
            CancelReason.NoItem => "军粮已用尽",
            CancelReason.TargetFullTroops => "目标兵力已满",
            _ => "无法执行",
        };

        public static string Describe(BattleEvent e)
        {
            string actor = e.Actor?.Name, target = e.Target?.Name;
            string action = e.Skill != null ? $"【{e.Skill.DisplayName}】" : "普通攻击";
            switch (e.Kind)
            {
                case BattleEventKind.Defend: return $"{actor} 进入防御（本回合受到伤害减半）";
                case BattleEventKind.ActionStarted:
                    return e.Command switch
                    {
                        CommandKind.Skill => $"{actor} 发动【{e.Skill?.DisplayName}】",
                        CommandKind.Item => $"{actor} 使用军粮",
                        _ => $"{actor} 发起攻击",
                    };
                case BattleEventKind.Damage: return $"{actor} {action} → {target} 损失兵力 {e.Amount}{(e.Advantage ? "（兵种克制）" : "")}{(e.Guarded ? "（防御减半）" : "")}";
                case BattleEventKind.Heal: return $"{actor} {action} → {target} 恢复兵力 {e.Amount}";
                case BattleEventKind.ItemUsed: return $"{actor} 使用军粮 → {target} 恢复兵力 {e.Amount}";
                case BattleEventKind.ModifierApplied: return $"{target} {Stat(e.Modifier.Stat)}{(e.ModifierPercent > 0 ? "+" : "")}{e.ModifierPercent}%（{e.ModifierTurns}回合，含本回合）";
                case BattleEventKind.ModifierExpired: return $"{target} 的{Stat(e.Modifier.Stat)}变化结束";
                case BattleEventKind.Retargeted: return $"{actor} 原目标已撤退，转攻 {target}";
                case BattleEventKind.Retreated: return $"{target} 部队溃退，撤出战场";
                case BattleEventKind.Cancelled: return $"{actor} 的{(e.Skill != null ? action : "行动")}取消：{Reason(e.Reason)}（不消耗）";
                case BattleEventKind.RoundEnded: return $"—— 第{e.Amount}回合结束 ——";
                case BattleEventKind.Victory: return "敌军全部撤退，我军胜利！";
                case BattleEventKind.Defeat: return "我军出战部队全部撤退……";
                default: return e.Kind.ToString();
            }
        }
    }
}
