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
        public const string Prepare = "战前准备";
        public const string Hints = "提示";
        public const string Advantaged = "占优";

        // Teaching hints (story-events answer 8). 暂定 wording, to be replaced by the written script.
        static readonly System.Collections.Generic.Dictionary<string, (string title, string body)> HintText = new System.Collections.Generic.Dictionary<string, (string, string)>
        {
            ["b1.command"] = ("下令", "为每名出战武将依次选择指令：攻击、技能、防御或道具。全部选完才开始交锋，提交前可按「返回」修改。"),
            ["b1.target"] = ("选目标", "选择要攻击的敌人。光标指向的敌人脚下会出现标记，旁边显示其兵力。"),
            ["b1.order"] = ("行动顺序", "双方按速度从快到慢行动，防御最先生效。画面上方列出本回合的行动顺序。"),
            ["b1.retreat"] = ("撤退", "兵力降到 0 的部队撤退，本场不再行动。敌方全部撤退即为胜利。"),
            ["b1.recover"] = ("战后恢复", "战斗结束后全军兵力与技力回满，不会留下损失。"),
            ["b2.tech"] = ("技力与技能", "技能消耗技力，普通攻击不耗。战斗中技力不会自动回复，留给关键时刻；技力不足的技能会置灰。"),
            ["b2.defend"] = ("防御与整队", "兵力低于一半时，可让这支部队防御（本回合受到的伤害减半），或让刘备用「整队」回复 30% 兵力。"),
            ["b2.advantage"] = ("兵种克制", "骑克步、步克弓、弓克骑，占优时伤害 ×1.4。目标旁标有「占优」的敌人更好打；关羽的骑兵对黄巾军也占优。"),
            ["b2.focus"] = ("集火", "多名武将攻击同一目标，能更快令其撤退，减少敌方的行动次数。"),
            ["b3.skills"] = ("三级技能与目标优先", "三人已学会第二项技能：火矢、破阵、威慑。张角的计策伤害高，优先让他撤退。"),
            ["b3.debuff"] = ("剩余回合", "增益和减益会持续数回合；武将卡片和敌人状态上显示剩余回合，归零后效果消失。"),
            ["b3.ration"] = ("军粮", "「道具」里的军粮可回复一支部队 30% 兵力，占用一次行动，不能让已撤退的部队复活。"),
        };

        public static string HintTitle(string key) => HintText.TryGetValue(key, out var h) ? h.title : key;
        public static string HintBody(string key) => HintText.TryGetValue(key, out var h) ? h.body : key;
        public static string Hint(string key) => $"【{HintTitle(key)}】{HintBody(key)}";
        public const string LineupHeader = "出战武将（1–3 名，按选择顺序排位）";
        public const string IntelHeader = "敌方情报";
        public const string Outfit = "配装";
        public const string ConfirmSortie = "确认出战";
        public const string SortieQuestion = "结束整备并出战？";
        public const string KeepPreparing = "继续整备";
        public const string Auto = "自动";
        public const string Standby = "待命";
        public const string PrepHint = "确认出战后自动存档；战败可还原到此时的状态重新准备。";
        public const string NeedOne = "至少选择一名武将出战。";
        public static string SlotMark(int slot) => "①②③".Substring(slot, 1);
        public static string GeneralLine(string name, TroopType t, int level, int troops) => $"{name}　{Troop(t)}　{Level(level)}　兵力 {troops}";
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
        // 0.2 statuses and battle events (SYSTEMS_V02 §1.3, §2.2).
        public static string StatusName(string id) => StatusSpec.Find(id)?.DisplayName ?? id;
        public static string StatusRemaining(string id, int turns) => $"{StatusName(id)}（剩{turns}回合）";
        public const string EscortMark = "护";
        public const string AllyCaption = "友军";
        public const string EscortLost = "护卫目标撤退，战斗失败。可还原战前状态重新挑战。";
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
                case BattleEventKind.Retreated: return e.RetreatReason == RetreatReason.Routed ? $"{target} 溃走" : $"{target} 部队溃退，撤出战场";
                case BattleEventKind.Banner: return e.Text;
                case BattleEventKind.ObjectiveChanged: return e.Text;
                case BattleEventKind.Fx: return "";
                case BattleEventKind.Dialogue: return "";
                case BattleEventKind.RoundStarted: return $"—— 第{e.Amount}回合 ——";
                case BattleEventKind.StatusApplied: return $"{target} 陷入{StatusName(e.Text)}（{e.Amount}回合，含本回合）";
                case BattleEventKind.StatusExpired: return $"{target} 的{StatusName(e.Text)}解除";
                case BattleEventKind.EventDamage: return $"{target} 损失兵力 {e.Amount}";
                case BattleEventKind.Cancelled: return $"{actor} 的{(e.Skill != null ? action : "行动")}取消：{Reason(e.Reason)}（不消耗）";
                case BattleEventKind.RoundEnded: return $"—— 第{e.Amount}回合结束 ——";
                case BattleEventKind.Victory: return "敌军全部撤退，我军胜利！";
                case BattleEventKind.Defeat: return e.Target == null ? "战斗失败……" : "我军出战部队全部撤退……";
                default: return e.Kind.ToString();
            }
        }
    }
}
