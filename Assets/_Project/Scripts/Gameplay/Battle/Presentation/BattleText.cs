namespace Jrpg.Battle.Presentation
{
    // 结算记录→中文文本，仅属表现层。
    public static class BattleText
    {
        public static string Troop(TroopType t) => t switch
        {
            TroopType.Infantry => "步",
            TroopType.Cavalry => "骑",
            _ => "弓",
        };

        public static string Stat(BattleStat s) => s switch
        {
            BattleStat.PhysicalAttack => "物攻",
            BattleStat.PhysicalDefense => "物防",
            BattleStat.StrategyAttack => "计攻",
            BattleStat.StrategyDefense => "计防",
            _ => "速度",
        };

        public static string Modifier(StatModifier m) => $"{Stat(m.Stat)}{(m.Percent > 0 ? "+" : "")}{m.Percent}%（剩{m.RemainingTurns}回合）";

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
                case BattleEventKind.Damage: return $"{actor} {action} → {target} 损失兵力 {e.Amount}{(e.Advantage ? "（兵种克制×1.4）" : "")}{(e.Guarded ? "（防御减半）" : "")}";
                case BattleEventKind.Heal: return $"{actor} {action} → {target} 恢复兵力 {e.Amount}";
                case BattleEventKind.ItemUsed: return $"{actor} 使用军粮 → {target} 恢复兵力 {e.Amount}";
                case BattleEventKind.ModifierApplied: return $"{target} {Stat(e.Modifier.Stat)}{(e.Amount > 0 ? "+" : "")}{e.Amount}%（含本回合共{e.Skill.ModifierTurns}回合，同向不叠加）";
                case BattleEventKind.ModifierExpired: return $"{target} 的{Stat(e.Modifier.Stat)}变化结束";
                case BattleEventKind.Retargeted: return $"{actor} 原目标已撤退，转攻 {target}";
                case BattleEventKind.Retreated: return $"{target} 兵力耗尽，本场撤退";
                case BattleEventKind.Cancelled: return $"{actor} 的{(e.Skill != null ? action : "行动")}取消：{Reason(e.Reason)}（不消耗）";
                case BattleEventKind.RoundEnded: return $"—— 第{e.Amount}回合结束 ——";
                case BattleEventKind.Victory: return "敌军全部撤退，我军胜利！";
                case BattleEventKind.Defeat: return "我军出战部队全部撤退……";
                default: return e.Kind.ToString();
            }
        }
    }
}
