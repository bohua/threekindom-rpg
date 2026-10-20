namespace ThreeKingdoms.Core.Battle
{
    public sealed class BattleCommand
    {
        public BattleUnit Actor { get; }
        public CommandKind Kind { get; }
        public SkillSpec Skill { get; }
        public BattleUnit Target { get; }

        BattleCommand(BattleUnit actor, CommandKind kind, SkillSpec skill, BattleUnit target)
        {
            Actor = actor; Kind = kind; Skill = skill; Target = target;
        }

        public static BattleCommand Attack(BattleUnit actor, BattleUnit target) => new BattleCommand(actor, CommandKind.Attack, null, target);
        public static BattleCommand UseSkill(BattleUnit actor, SkillSpec skill, BattleUnit target = null) => new BattleCommand(actor, CommandKind.Skill, skill, target);
        public static BattleCommand Defend(BattleUnit actor) => new BattleCommand(actor, CommandKind.Defend, null, null);
        // The only V1 battle item: army rations (军粮).
        public static BattleCommand Ration(BattleUnit actor, BattleUnit target) => new BattleCommand(actor, CommandKind.Item, null, target);

        public override string ToString() => $"{Actor} {Kind} {Skill?.Id} → {Target}";
    }

    public enum BattleEventKind
    {
        Defend, ActionStarted, Damage, Heal, ModifierApplied, ModifierExpired, Retargeted, Retreated, Cancelled, ItemUsed, RoundEnded, Victory, Defeat,
    }

    public enum CancelReason { None, TargetInvalid, NotEnoughTech, NoItem, TargetFullTroops, UnknownSkill }

    // Structured resolution record; the UI turns it into text and the controller into animation (ADR-0002 §4).
    public sealed class BattleEvent
    {
        public BattleEventKind Kind { get; }
        public BattleUnit Actor { get; }
        public BattleUnit Target { get; }
        public SkillSpec Skill { get; }
        public CommandKind Command { get; }
        public int Amount { get; }
        // Target troops / actor tech right after this event, so playback shows bars changing hit by hit
        // without recomputing state.
        public int TargetTroopsAfter { get; }
        public int ActorTechAfter { get; }
        public bool Advantage { get; }
        public bool Guarded { get; }
        public CancelReason Reason { get; }
        public StatModifier Modifier { get; }
        public int ModifierPercent { get; }
        public int ModifierTurns { get; }

        internal BattleEvent(BattleEventKind kind, BattleUnit actor = null, BattleUnit target = null, SkillSpec skill = null,
            int amount = 0, bool advantage = false, CancelReason reason = CancelReason.None, StatModifier modifier = null,
            bool guarded = false, CommandKind command = CommandKind.Attack)
        {
            Kind = kind; Actor = actor; Target = target; Skill = skill; Amount = amount; Advantage = advantage; Reason = reason;
            Modifier = modifier; Guarded = guarded; Command = command;
            TargetTroopsAfter = target?.Troops ?? 0;
            ActorTechAfter = actor?.Tech ?? 0;
            ModifierPercent = modifier?.Percent ?? 0;
            ModifierTurns = modifier?.RemainingTurns ?? 0;
        }

        public override string ToString() =>
            $"{Kind} {Actor?.Id} → {Target?.Id} {Skill?.Id} {Amount}{(Advantage ? " adv" : "")}{(Guarded ? " guard" : "")}{(Reason != CancelReason.None ? " " + Reason : "")}";
    }
}
