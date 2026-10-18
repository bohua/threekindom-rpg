using System;
using System.Collections.Generic;
using System.Linq;

namespace Jrpg.Battle
{
    // 一场对列回合制战斗（V1_SPEC §4）。确定性：无随机、无帧依赖。
    public sealed class BattleState
    {
        public const int MaxLineup = 3;

        readonly List<BattleUnit> players;
        readonly List<BattleUnit> enemies;

        public IReadOnlyList<BattleUnit> Players => players;
        public IReadOnlyList<BattleUnit> Enemies => enemies;
        public int Round { get; private set; } = 1;
        public int Rations { get; private set; }
        public BattleOutcome Outcome { get; private set; } = BattleOutcome.Ongoing;

        public BattleState(IEnumerable<BattleUnit> playerUnits, IEnumerable<BattleUnit> enemyUnits, int rations)
        {
            players = playerUnits.OrderBy(u => u.Slot).ToList();
            enemies = enemyUnits.OrderBy(u => u.Slot).ToList();
            if (players.Count == 0 || players.Count > MaxLineup) throw new ArgumentException("我方出战需要1–3名武将。");
            if (enemies.Count == 0) throw new ArgumentException("敌方编组为空。");
            if (players.Any(u => u.Side != Side.Player) || enemies.Any(u => u.Side != Side.Enemy)) throw new ArgumentException("单位阵营不符。");
            Rations = Math.Max(0, rations);
        }

        public IReadOnlyList<BattleUnit> Allies(BattleUnit unit) => unit.Side == Side.Player ? players : enemies;
        public IReadOnlyList<BattleUnit> Opponents(BattleUnit unit) => unit.Side == Side.Player ? enemies : players;
        public IEnumerable<BattleUnit> AllUnits => players.Concat(enemies);

        // 非防御指令的执行顺序：速度高到低；同速我方先；同侧按出战位。
        public static int CompareOrder(BattleUnit a, BattleUnit b)
        {
            int speed = b.Effective(BattleStat.Speed).CompareTo(a.Effective(BattleStat.Speed));
            if (speed != 0) return speed;
            int side = a.Side.CompareTo(b.Side);
            return side != 0 ? side : a.Slot.CompareTo(b.Slot);
        }

        public List<BattleUnit> ActionOrder()
        {
            var order = AllUnits.Where(u => u.Active).ToList();
            order.Sort(CompareOrder);
            return order;
        }

        public List<BattleEvent> ResolveRound(IEnumerable<BattleCommand> commands)
        {
            if (Outcome != BattleOutcome.Ongoing) throw new InvalidOperationException("战斗已结束。");
            var list = commands.ToList();
            foreach (var unit in AllUnits.Where(u => u.Active))
            {
                int count = list.Count(c => c.Actor == unit);
                if (count != 1) throw new ArgumentException($"{unit.Name} 每回合必须且只能有1条指令（当前 {count}）。");
            }
            if (list.Any(c => !c.Actor.Active)) throw new ArgumentException("已撤退的单位不能下令。");

            var log = new List<BattleEvent>();
            // 顺序在回合开始时确定，本回合内的增减益不改变已排好的顺序。
            var order = ActionOrder();

            // 防御是唯一先行指令：在本轮伤害前建立减伤，持续到回合结束。
            foreach (var c in list.Where(c => c.Kind == CommandKind.Defend))
            {
                c.Actor.Defending = true;
                log.Add(new BattleEvent(BattleEventKind.Defend, c.Actor));
            }

            foreach (var actor in order)
            {
                if (Outcome != BattleOutcome.Ongoing) break;
                var command = list.First(c => c.Actor == actor);
                if (command.Kind == CommandKind.Defend || !actor.Active) continue;
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
                // 普通攻击目标已撤退：转向第一个存活敌人。
                target = Opponents(c.Actor).FirstOrDefault(u => u.Active);
                if (target == null) return;
                log.Add(new BattleEvent(BattleEventKind.Retargeted, c.Actor, target));
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
                var expectedSide = skill.TargetsAllies ? actor.Side : Opposite(actor.Side);
                // 单体技能无合法目标则取消，不扣技力。
                if (c.Target == null || !c.Target.Active || c.Target.Side != expectedSide) { Cancel(c, CancelReason.TargetInvalid, log); return; }
                targets = new List<BattleUnit> { c.Target };
            }

            // 技力在实际执行时扣除；不足则取消，不扣成负数。
            if (!actor.SpendTech(skill.TechCost)) { Cancel(c, CancelReason.NotEnoughTech, log); return; }

            foreach (var target in targets)
            {
                if (!target.Active) continue;
                if (skill.Damage != DamageKind.None && skill.PowerPercent > 0)
                    Strike(actor, target, skill.Damage, skill.PowerPercent, skill, log);
                if (skill.HealPercent > 0)
                {
                    int healed = target.Heal(BattleRules.PercentOf(target.Stats.MaxTroops, skill.HealPercent));
                    log.Add(new BattleEvent(BattleEventKind.Heal, actor, target, skill, healed));
                }
                if (skill.ModifierPercent != 0 && target.Active)
                {
                    target.ApplyModifier(skill.ModifierStat, skill.ModifierPercent, skill.ModifierTurns);
                    var applied = target.Modifiers.First(m => m.Stat == skill.ModifierStat && Math.Sign(m.Percent) == Math.Sign(skill.ModifierPercent));
                    log.Add(new BattleEvent(BattleEventKind.ModifierApplied, actor, target, skill, applied.Percent, modifier: applied));
                }
            }
        }

        void ExecuteRation(BattleCommand c, List<BattleEvent> log)
        {
            var target = c.Target;
            // 道具目标无效或不需要恢复则取消，不扣道具。
            if (target == null || !target.Active || target.Side != c.Actor.Side) { Cancel(c, CancelReason.TargetInvalid, log); return; }
            if (target.Troops >= target.Stats.MaxTroops) { Cancel(c, CancelReason.TargetFullTroops, log); return; }
            if (Rations <= 0) { Cancel(c, CancelReason.NoItem, log); return; }
            Rations--;
            int healed = target.Heal(BattleRules.PercentOf(target.Stats.MaxTroops, BattleRules.RationHealPercent));
            log.Add(new BattleEvent(BattleEventKind.ItemUsed, c.Actor, target, amount: healed));
        }

        void Strike(BattleUnit actor, BattleUnit target, DamageKind kind, int powerPercent, BattleSkillDefinition skill, List<BattleEvent> log)
        {
            var (atkStat, defStat) = kind == DamageKind.Strategy
                ? (BattleStat.StrategyAttack, BattleStat.StrategyDefense)
                : (BattleStat.PhysicalAttack, BattleStat.PhysicalDefense);
            bool advantage = BattleRules.HasAdvantage(actor.Troop, target.Troop);
            int damage = BattleRules.Damage(actor.Effective(atkStat), target.Effective(defStat), powerPercent, advantage, target.Defending);
            int dealt = target.TakeDamage(damage);
            log.Add(new BattleEvent(BattleEventKind.Damage, actor, target, skill, dealt, advantage, guarded: target.Defending));
            if (!target.Active) log.Add(new BattleEvent(BattleEventKind.Retreated, target: target));
        }

        void Cancel(BattleCommand c, CancelReason reason, List<BattleEvent> log) =>
            log.Add(new BattleEvent(BattleEventKind.Cancelled, c.Actor, c.Target, c.Skill, reason: reason));

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
