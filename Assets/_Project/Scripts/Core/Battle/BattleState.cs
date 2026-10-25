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
        readonly List<BattleUnit> allies;
        readonly HashSet<BattleUnit> escorts;
        readonly BattleScript script;
        readonly HashSet<string> fired = new HashSet<string>();
        readonly BattleConfig config;
        readonly TroopTable troops;
        BattleOutcome forced = BattleOutcome.Ongoing;

        public IReadOnlyList<BattleUnit> Players => players;
        public IReadOnlyList<BattleUnit> Enemies => enemies;
        // Friendly units that take no orders (0.2); they stand after our three and never count as our line-up.
        public IReadOnlyList<BattleUnit> AllyUnits => allies;
        // Escort targets: if one retreats the battle is lost at once (SYSTEMS_V02 §2.2).
        public IEnumerable<BattleUnit> Escorts => escorts;
        // The turn-goal bar text set by battle events (SYSTEMS_V02 §1.2 set_objective), or null.
        public string Objective { get; private set; }
        // Event ids that have fired this battle (runtime only, never saved: a retry starts over).
        public IReadOnlyCollection<string> FiredEvents => fired;
        // Events dropped by the depth limit (diagnostics only).
        public List<string> IgnoredEvents { get; } = new List<string>();
        public int Round { get; private set; } = 1;
        public ItemSpec RationItem { get; }
        public int Rations { get; private set; }
        public int RationsUsed { get; private set; }
        public BattleOutcome Outcome { get; private set; } = BattleOutcome.Ongoing;
        public BattleConfig Config => config;
        public TroopTable Troops => troops;

        internal BattleState(IEnumerable<BattleUnit> playerUnits, IEnumerable<BattleUnit> enemyUnits, ItemSpec rationItem, int rations,
            BattleConfig config, TroopTable troops, IEnumerable<BattleUnit> allyUnits = null, BattleScript script = null)
        {
            allies = (allyUnits ?? Enumerable.Empty<BattleUnit>()).OrderBy(u => u.Slot).ToList();
            escorts = new HashSet<BattleUnit>();
            this.script = script ?? BattleScript.Empty;
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.troops = troops ?? throw new ArgumentNullException(nameof(troops));
            players = playerUnits.OrderBy(u => u.Slot).ToList();
            enemies = enemyUnits.OrderBy(u => u.Slot).ToList();
            if (players.Count == 0 || players.Count > MaxLineup) throw new ArgumentException("我方出战需要 1–3 名武将。");
            if (enemies.Count == 0) throw new ArgumentException("敌方编组为空。");
            if (players.Any(u => u.Side != Side.Player) || enemies.Any(u => u.Side != Side.Enemy) || allies.Any(u => u.Side != Side.Ally))
                throw new ArgumentException("单位阵营不符。");
            RationItem = rationItem;
            Rations = rationItem == null ? 0 : Math.Max(0, rations);
        }

        internal void MarkEscort(BattleUnit unit) { if (allies.Contains(unit)) escorts.Add(unit); }

        // Our support targets: our line-up, then the allies (SYSTEMS_V02 §2.2 target pools).
        public IReadOnlyList<BattleUnit> Allies(BattleUnit unit) => unit.Side == Side.Enemy ? enemies : players.Concat(allies).ToList();
        // The enemies' target pool is our line-up first, the allies after.
        public IReadOnlyList<BattleUnit> Opponents(BattleUnit unit) => unit.Side == Side.Enemy ? players.Concat(allies).ToList() : enemies;
        public IEnumerable<BattleUnit> AllUnits => players.Concat(allies).Concat(enemies);
        public static bool SameSide(BattleUnit a, BattleUnit b) => (a.Side == Side.Enemy) == (b.Side == Side.Enemy);

        static int Rank(Side side) => side == Side.Player ? 0 : side == Side.Ally ? 1 : 2;

        // Non-defend order: speed high → low; equal speed: our side, then allies, then the enemy; same side: slot.
        public static int CompareOrder(BattleUnit a, BattleUnit b)
        {
            int speed = b.EffectiveFloor(BattleStat.Speed).CompareTo(a.EffectiveFloor(BattleStat.Speed));
            if (speed != 0) return speed;
            int side = Rank(a.Side).CompareTo(Rank(b.Side));
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
                var before = Snapshot();
                log.Add(new BattleEvent(BattleEventKind.ActionStarted, actor, command.Target, command.Skill, command: command.Kind));
                Execute(command, log);
                UpdateOutcome(log);
                // Conditions this action produced go through the event queue (SYSTEMS_V02 §1.2 pipeline steps 4–5).
                if (Outcome == BattleOutcome.Ongoing) RunEvents(Conditions(before, 1).ToList(), log);
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
            if (target == null || !target.Active || SameSide(target, c.Actor))
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
                // Single-target skill without a legal target is cancelled; tech is not spent.
                if (c.Target == null || !c.Target.Active || SameSide(c.Target, actor) != skill.TargetsAllies) { Cancel(c, CancelReason.TargetInvalid, log); return; }
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
            if (target == null || !target.Active || !SameSide(target, c.Actor)) { Cancel(c, CancelReason.TargetInvalid, log); return; }
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

        // Checked after every action and every battle event. Defeat first (SYSTEMS_V02 §1.2): our line-up gone, an escort
        // gone, or end_battle(defeat); then victory: every enemy gone or end_battle(victory). Allies never count as ours.
        void UpdateOutcome(List<BattleEvent> log)
        {
            if (Outcome != BattleOutcome.Ongoing) return;
            if (players.All(u => !u.Active) || escorts.Any(u => !u.Active) || forced == BattleOutcome.Defeat)
            { Outcome = BattleOutcome.Defeat; log.Add(new BattleEvent(BattleEventKind.Defeat)); }
            else if (enemies.All(u => !u.Active) || forced == BattleOutcome.Victory)
            { Outcome = BattleOutcome.Victory; log.Add(new BattleEvent(BattleEventKind.Victory)); }
        }

        void EndRound(List<BattleEvent> log)
        {
            foreach (var unit in AllUnits)
            {
                unit.Defending = false;
                foreach (var expired in unit.TickModifiers())
                    log.Add(new BattleEvent(BattleEventKind.ModifierExpired, target: unit, modifier: expired));
                foreach (var status in unit.TickStatuses())
                    log.Add(new BattleEvent(BattleEventKind.StatusExpired, target: unit, text: status));
            }
            log.Add(new BattleEvent(BattleEventKind.RoundEnded, amount: Round));
            Round++;
        }

        // ---------- battle events (SYSTEMS_V02 §1) ----------

        int startedRound;

        // turn_start(N): once per round, before the AI reads the round-start state and before our orders.
        internal List<BattleEvent> StartRound()
        {
            var log = new List<BattleEvent>();
            if (Outcome != BattleOutcome.Ongoing || startedRound == Round) return log;
            startedRound = Round;
            RunEvents(script.Events.Where(e => e.When.Kind == TriggerKind.TurnStart && e.When.Round == Round).Select(e => (e, 1)).ToList(), log);
            return log;
        }

        Dictionary<BattleUnit, (int troops, bool active)> Snapshot() => AllUnits.ToDictionary(u => u, u => (u.Troops, u.Active));

        // Events whose conditions the change since `before` met: a unit crossing below a share of its troops (one hit
        // straight to 0 counts), or a unit leaving the field (optionally only for one reason).
        IEnumerable<(ScriptEvent e, int depth)> Conditions(Dictionary<BattleUnit, (int troops, bool active)> before, int depth)
        {
            foreach (var e in script.Events)
            {
                if (e.Once && fired.Contains(e.Id)) continue;
                var w = e.When;
                if (w.Kind == TriggerKind.UnitHpBelow)
                {
                    foreach (var u in Matching(w.Unit))
                    {
                        if (!before.TryGetValue(u, out var b)) continue;
                        long limit = (long)u.Stats.MaxTroops * w.Percent;
                        if (b.troops * 100L >= limit && u.Troops * 100L < limit) { yield return (e, depth); break; }
                    }
                }
                else if (w.Kind == TriggerKind.UnitRetreated)
                {
                    foreach (var u in Matching(w.Unit))
                        if (before.TryGetValue(u, out var b) && b.active && !u.Active && (w.Reason == null || w.Reason == u.RetreatReason))
                        { yield return (e, depth); break; }
                }
            }
        }

        IEnumerable<BattleUnit> Matching(string target) => target switch
        {
            BattleScript.EnemyAll => enemies,
            BattleScript.PlayerAll => players,
            BattleScript.AllyAll => allies,
            _ => AllUnits.Where(u => u.Id == target),
        };

        // First in, first out; a full outcome check after each event. Conditions an event's own actions produce queue one
        // level deeper, up to BattleScript.MaxDepth; deeper ones are dropped and only noted in IgnoredEvents.
        void RunEvents(List<(ScriptEvent e, int depth)> start, List<BattleEvent> log)
        {
            var queue = new Queue<(ScriptEvent e, int depth)>(start);
            while (queue.Count > 0 && Outcome == BattleOutcome.Ongoing)
            {
                var (ev, depth) = queue.Dequeue();
                if (ev.Once && fired.Contains(ev.Id)) continue;
                fired.Add(ev.Id);
                var before = Snapshot();
                foreach (var action in ev.Actions)
                    if (Do(action, log)) break;   // end_battle stops the rest
                UpdateOutcome(log);
                if (Outcome != BattleOutcome.Ongoing) break;
                foreach (var next in Conditions(before, depth + 1).ToList())
                {
                    if (next.depth > BattleScript.MaxDepth) { IgnoredEvents.Add(next.e.Id); continue; }
                    queue.Enqueue(next);
                }
            }
        }

        // True when the action ended the battle.
        bool Do(ScriptAction a, List<BattleEvent> log)
        {
            switch (a.Kind)
            {
                case ScriptActionKind.Banner: log.Add(new BattleEvent(BattleEventKind.Banner, text: a.Text)); break;
                case ScriptActionKind.Dialogue: log.Add(new BattleEvent(BattleEventKind.Dialogue, text: a.Text)); break;
                case ScriptActionKind.Fx: log.Add(new BattleEvent(BattleEventKind.Fx, text: a.Text)); break;
                case ScriptActionKind.SetObjective:
                    Objective = a.Text;
                    log.Add(new BattleEvent(BattleEventKind.ObjectiveChanged, text: a.Text));
                    break;
                case ScriptActionKind.ApplyStatus:
                    var status = StatusSpec.Find(a.Text);
                    foreach (var u in Matching(a.Target).Where(u => u.Active).ToList())
                    {
                        u.ApplyStatus(status, a.Value);
                        log.Add(new BattleEvent(BattleEventKind.StatusApplied, target: u, amount: a.Value, text: status.Id));
                    }
                    break;
                case ScriptActionKind.DamageRatio:
                    foreach (var u in Matching(a.Target).Where(u => u.Active).ToList())
                    {
                        int lost = u.LoseTroops(BattleRules.PercentOf(u.Stats.MaxTroops, a.Value));
                        log.Add(new BattleEvent(BattleEventKind.EventDamage, target: u, amount: lost));
                    }
                    break;
                case ScriptActionKind.Retreat:
                    foreach (var u in Matching(a.Target).Where(u => u.Active).ToList())
                    {
                        u.Rout();
                        log.Add(new BattleEvent(BattleEventKind.Retreated, target: u));
                    }
                    break;
                case ScriptActionKind.EndBattle:
                    forced = a.Result;
                    UpdateOutcome(log);
                    return true;
            }
            return false;
        }
    }
}
