using System;
using System.Collections.Generic;
using System.Linq;

namespace ThreeKingdoms.Core.Battle
{
    // One line-up battle (combat GDD, ADR-0002 §4). Deterministic: no randomness, no time, no frames.
    // Only BattleSession advances it; ResolveRound is internal so callers cannot skip the enemy AI or the result.
    public sealed class BattleState
    {
        public const int MaxLineup = 3;

        readonly List<BattleUnit> players;
        readonly List<BattleUnit> enemies;
        readonly BattleConfig config;
        readonly TroopTable troops;

        public IReadOnlyList<BattleUnit> Players => players;
        public IReadOnlyList<BattleUnit> Enemies => enemies;
        public int Round { get; private set; } = 1;
        public ItemSpec RationItem { get; }
        public int Rations { get; private set; }
        public int RationsUsed { get; private set; }
        public BattleOutcome Outcome { get; private set; } = BattleOutcome.Ongoing;
        public BattleConfig Config => config;
        public TroopTable Troops => troops;

        internal BattleState(IEnumerable<BattleUnit> playerUnits, IEnumerable<BattleUnit> enemyUnits, ItemSpec rationItem, int rations,
            BattleConfig config, TroopTable troops)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.troops = troops ?? throw new ArgumentNullException(nameof(troops));
            players = playerUnits.OrderBy(u => u.Slot).ToList();
            enemies = enemyUnits.OrderBy(u => u.Slot).ToList();
            if (players.Count == 0 || players.Count > MaxLineup) throw new ArgumentException("我方出战需要 1–3 名武将。");
            if (enemies.Count == 0) throw new ArgumentException("敌方编组为空。");
            if (players.Any(u => u.Side != Side.Player) || enemies.Any(u => u.Side != Side.Enemy)) throw new ArgumentException("单位阵营不符。");
            RationItem = rationItem;
            Rations = rationItem == null ? 0 : Math.Max(0, rations);
        }

        public IReadOnlyList<BattleUnit> Allies(BattleUnit unit) => unit.Side == Side.Player ? players : enemies;
        public IReadOnlyList<BattleUnit> Opponents(BattleUnit unit) => unit.Side == Side.Player ? enemies : players;
        public IEnumerable<BattleUnit> AllUnits => players.Concat(enemies);

        // Non-defend order: speed high → low; equal speed: our side first; same side: line-up slot.
        public static int CompareOrder(BattleUnit a, BattleUnit b)
        {
            int speed = b.EffectiveFloor(BattleStat.Speed).CompareTo(a.EffectiveFloor(BattleStat.Speed));
            if (speed != 0) return speed;
            int side = a.Side.CompareTo(b.Side);
            return side != 0 ? side : a.Slot.CompareTo(b.Slot);
        }

        public IReadOnlyList<BattleUnit> ActionOrder()
        {
            var order = AllUnits.Where(u => u.Active).ToList();
            order.Sort(CompareOrder);
            return order;
        }

        public bool HasAdvantage(BattleUnit attacker, BattleUnit defender) => troops.HasAdvantage(attacker.Troop, defender.Troop);

        // Command-menu query: unaffordable skills are greyed out (user 2026-10-18).
        public bool CanUseSkill(BattleUnit actor, SkillSpec skill) =>
            actor != null && actor.Active && actor.Knows(skill) && skill.TechCost <= actor.Tech && LegalTargets(actor, CommandKind.Skill, skill).Count > 0;

        public bool CanUseRation(BattleUnit actor) =>
            actor != null && actor.Active && Rations > 0 && LegalTargets(actor, CommandKind.Item).Count > 0;

        public IReadOnlyList<BattleUnit> LegalTargets(BattleUnit actor, CommandKind kind, SkillSpec skill = null)
        {
            switch (kind)
            {
                case CommandKind.Attack: return Opponents(actor).Where(u => u.Active).ToList();
                case CommandKind.Defend: return Array.Empty<BattleUnit>();
                case CommandKind.Item: return Allies(actor).Where(u => u.Active && u.Troops < u.Stats.MaxTroops).ToList();
                default:
                    if (skill == null) return Array.Empty<BattleUnit>();
                    if (skill.TargetsAllies)
                        return Allies(actor).Where(u => u.Active && (skill.HealPercent == 0 || u.Troops < u.Stats.MaxTroops)).ToList();
                    return Opponents(actor).Where(u => u.Active).ToList();
            }
        }

        internal List<BattleEvent> ResolveRound(IEnumerable<BattleCommand> commands)
        {
            if (Outcome != BattleOutcome.Ongoing) throw new InvalidOperationException("战斗已结束。");
            var list = commands.ToList();
            foreach (var unit in AllUnits.Where(u => u.Active))
            {
                int count = list.Count(c => c.Actor == unit);
                if (count != 1) throw new ArgumentException($"{unit.Name} 每回合必须且只能有 1 条指令（当前 {count}）。");
            }
            if (list.Any(c => !c.Actor.Active)) throw new ArgumentException("已撤退的单位不能下令。");

            var log = new List<BattleEvent>();
            // Fixed at round start; buffs applied during the round do not reorder it.
            var order = ActionOrder();

            // Defend is the only command that goes first: halves damage before anything else this round.
            foreach (var c in list.Where(c => c.Kind == CommandKind.Defend).OrderBy(c => order.ToList().IndexOf(c.Actor)))
            {
                c.Actor.Defending = true;
                log.Add(new BattleEvent(BattleEventKind.Defend, c.Actor, command: CommandKind.Defend));
            }

            foreach (var actor in order)
            {
                if (Outcome != BattleOutcome.Ongoing) break;
                var command = list.First(c => c.Actor == actor);
                if (command.Kind == CommandKind.Defend || !actor.Active) continue;
                log.Add(new BattleEvent(BattleEventKind.ActionStarted, actor, command.Target, command.Skill, command: command.Kind));
                Execute(command, log);
                UpdateOutcome(log);
            }

            if (Outcome == BattleOutcome.Ongoing) EndRound(log);
            return log;
        }

        void Execute(BattleCommand c, List<BattleEvent> log)
        {
            switch (c.Kind)
            {
                case CommandKind.Attack: ExecuteAttack(c, log); break;
                case CommandKind.Skill: ExecuteSkill(c, log); break;
                case CommandKind.Item: ExecuteRation(c, log); break;
            }
        }

        void ExecuteAttack(BattleCommand c, List<BattleEvent> log)
        {
            var target = c.Target;
            if (target == null || !target.Active || target.Side == c.Actor.Side)
            {
                // Attack target already retreated: switch to the first living opponent.
                target = Opponents(c.Actor).FirstOrDefault(u => u.Active);
                if (target == null) return;
                log.Add(new BattleEvent(BattleEventKind.Retargeted, c.Actor, target, command: CommandKind.Attack));
            }
            Strike(c.Actor, target, DamageKind.Physical, 100, null, log);
        }

        void ExecuteSkill(BattleCommand c, List<BattleEvent> log)
        {
            var skill = c.Skill;
            var actor = c.Actor;
            if (skill == null || !actor.Knows(skill)) { Cancel(c, CancelReason.UnknownSkill, log); return; }

            List<BattleUnit> targets;
            if (skill.Target == SkillTarget.AllEnemies)
            {
                targets = Opponents(actor).Where(u => u.Active).ToList();
                if (targets.Count == 0) { Cancel(c, CancelReason.TargetInvalid, log); return; }
            }
            else
            {
                var expected = skill.TargetsAllies ? actor.Side : Opposite(actor.Side);
                // Single-target skill without a legal target is cancelled; tech is not spent.
                if (c.Target == null || !c.Target.Active || c.Target.Side != expected) { Cancel(c, CancelReason.TargetInvalid, log); return; }
                targets = new List<BattleUnit> { c.Target };
            }

            // Tech is spent when the action actually executes; never goes negative.
            if (!actor.SpendTech(skill.TechCost)) { Cancel(c, CancelReason.NotEnoughTech, log); return; }

            foreach (var target in targets)
            {
                if (!target.Active) continue;
                // Damage first: a debuff from the same skill (e.g. 破阵) applies from the next hit on (user 2026-10-18).
                if (skill.Damage != DamageKind.None && skill.PowerPercent > 0)
                    Strike(actor, target, skill.Damage, skill.PowerPercent, skill, log);
                if (skill.HealPercent > 0 && target.Active)
                {
                    int healed = target.Heal(BattleRules.PercentOf(target.Stats.MaxTroops, skill.HealPercent));
                    log.Add(new BattleEvent(BattleEventKind.Heal, actor, target, skill, healed, command: CommandKind.Skill));
                }
                if (skill.ModifierPercent != 0 && target.Active)
                {
                    target.ApplyModifier(skill.ModifierStat, skill.ModifierPercent, skill.ModifierTurns);
                    var applied = target.FindModifier(skill.ModifierStat, Math.Sign(skill.ModifierPercent));
                    log.Add(new BattleEvent(BattleEventKind.ModifierApplied, actor, target, skill, applied.Percent, modifier: applied, command: CommandKind.Skill));
                }
            }
        }

        void ExecuteRation(BattleCommand c, List<BattleEvent> log)
        {
            var target = c.Target;
            // Invalid target or nothing to heal: cancelled, the ration is kept.
            if (target == null || !target.Active || target.Side != c.Actor.Side) { Cancel(c, CancelReason.TargetInvalid, log); return; }
            if (target.Troops >= target.Stats.MaxTroops) { Cancel(c, CancelReason.TargetFullTroops, log); return; }
            if (RationItem == null || Rations <= 0) { Cancel(c, CancelReason.NoItem, log); return; }
            Rations--;
            RationsUsed++;
            int healed = target.Heal(BattleRules.PercentOf(target.Stats.MaxTroops, RationItem.HealPercent));
            log.Add(new BattleEvent(BattleEventKind.ItemUsed, c.Actor, target, amount: healed, command: CommandKind.Item));
        }

        void Strike(BattleUnit actor, BattleUnit target, DamageKind kind, int powerPercent, SkillSpec skill, List<BattleEvent> log)
        {
            var (atkStat, defStat) = kind == DamageKind.Strategy
                ? (BattleStat.StrategyAttack, BattleStat.StrategyDefense)
                : (BattleStat.PhysicalAttack, BattleStat.PhysicalDefense);
            bool advantage = HasAdvantage(actor, target);
            int damage = BattleRules.Damage(actor.Stats.Get(atkStat), actor.ModifierPercent(atkStat),
                target.Stats.Get(defStat), target.ModifierPercent(defStat), powerPercent, advantage, target.Defending, config);
            int dealt = target.TakeDamage(damage);
            log.Add(new BattleEvent(BattleEventKind.Damage, actor, target, skill, dealt, advantage, guarded: target.Defending,
                command: skill == null ? CommandKind.Attack : CommandKind.Skill));
            if (!target.Active) log.Add(new BattleEvent(BattleEventKind.Retreated, target: target));
        }

        void Cancel(BattleCommand c, CancelReason reason, List<BattleEvent> log) =>
            log.Add(new BattleEvent(BattleEventKind.Cancelled, c.Actor, c.Target, c.Skill, reason: reason, command: c.Kind));

        // Checked after every action: the battle ends the moment one side has no active unit left.
        void UpdateOutcome(List<BattleEvent> log)
        {
            if (enemies.All(u => !u.Active)) { Outcome = BattleOutcome.Victory; log.Add(new BattleEvent(BattleEventKind.Victory)); }
            else if (players.All(u => !u.Active)) { Outcome = BattleOutcome.Defeat; log.Add(new BattleEvent(BattleEventKind.Defeat)); }
        }

        void EndRound(List<BattleEvent> log)
        {
            foreach (var unit in AllUnits)
            {
                unit.Defending = false;
                foreach (var expired in unit.TickModifiers())
                    log.Add(new BattleEvent(BattleEventKind.ModifierExpired, target: unit, modifier: expired));
            }
            log.Add(new BattleEvent(BattleEventKind.RoundEnded, amount: Round));
            Round++;
        }

        static Side Opposite(Side side) => side == Side.Player ? Side.Enemy : Side.Player;
    }
}
