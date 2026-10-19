namespace Jrpg.Battle
{
    public sealed class BattleCommand
    {
        public BattleUnit Actor { get; }
        public CommandKind Kind { get; }
        public BattleSkillDefinition Skill { get; }
        public BattleUnit Target { get; }

        BattleCommand(BattleUnit actor, CommandKind kind, BattleSkillDefinition skill, BattleUnit target)
        {
            Actor = actor; Kind = kind; Skill = skill; Target = target;
        }

        public static BattleCommand Attack(BattleUnit actor, BattleUnit target) => new BattleCommand(actor, CommandKind.Attack, null, target);
        public static BattleCommand UseSkill(BattleUnit actor, BattleSkillDefinition skill, BattleUnit target = null) => new BattleCommand(actor, CommandKind.Skill, skill, target);
        public static BattleCommand Defend(BattleUnit actor) => new BattleCommand(actor, CommandKind.Defend, null, null);
        // 首版唯一道具：军粮。
        public static BattleCommand Ration(BattleUnit actor, BattleUnit target) => new BattleCommand(actor, CommandKind.Item, null, target);
    }

    public enum BattleEventKind
    {
        Defend, Damage, Heal, ModifierApplied, ModifierExpired, Retargeted, Retreated, Cancelled, ItemUsed, RoundEnded, Victory, Defeat,
    }

    public enum CancelReason { None, TargetInvalid, NotEnoughTech, NoItem, TargetFullTroops, UnknownSkill }

    // 结构化结算记录；文字由表现层生成。
    public sealed class BattleEvent
    {
        public BattleEventKind Kind { get; }
        public BattleUnit Actor { get; }
        public BattleUnit Target { get; }
        public BattleSkillDefinition Skill { get; }
        public int Amount { get; }
        public bool Advantage { get; }
        public bool Guarded { get; }
        public CancelReason Reason { get; }
        public StatModifier Modifier { get; }

        internal BattleEvent(BattleEventKind kind, BattleUnit actor = null, BattleUnit target = null, BattleSkillDefinition skill = null,
            int amount = 0, bool advantage = false, CancelReason reason = CancelReason.None, StatModifier modifier = null, bool guarded = false)
        {
            Kind = kind; Actor = actor; Target = target; Skill = skill; Amount = amount; Advantage = advantage; Reason = reason; Modifier = modifier; Guarded = guarded;
        }
    }
}
