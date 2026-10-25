using System.Collections.Generic;
using System.Linq;

namespace ThreeKingdoms.Core.Battle
{
    // Read-only view of the battle at the start of a round. The AI gets this and nothing else: it never sees
    // the player's commands for the round (combat GDD, ADR-0002 §5).
    public readonly struct BattleView
    {
        readonly BattleState state;
        internal BattleView(BattleState state) { this.state = state; }
        public int Round => state.Round;
        public IReadOnlyList<BattleUnit> Players => state.Players;
        public IReadOnlyList<BattleUnit> Enemies => state.Enemies;
        public IReadOnlyList<BattleUnit> Allies => state.AllyUnits;
        // The living escort target, or null (SYSTEMS_V02 §2.2 focus_escort).
        public BattleUnit Escort => System.Linq.Enumerable.FirstOrDefault(state.Escorts, u => u.Active);
    }

    public interface IEnemyAi
    {
        IReadOnlyList<BattleCommand> Decide(BattleView view);
    }

    public static class EnemyAi
    {
        public static IEnemyAi For(EncounterSpec encounter) => encounter.Ai switch
        {
            EnemyAiKind.FacingSlot => new FacingSlotAi(encounter.SkillEveryRounds),
            _ => new HighestTroopsAi(),
        };

        // Lowest current troops among the living, ties by slot.
        internal static BattleUnit Weakest(IEnumerable<BattleUnit> living) => living.OrderBy(u => u.Troops).ThenBy(u => u.Slot).FirstOrDefault();

        // An enemy marked focus_escort attacks the escort while it stands; everyone else keeps their policy.
        internal static BattleUnit Focus(BattleUnit enemy, BattleView view) => enemy.FocusEscort ? view.Escort : null;
    }

    // Allies (SYSTEMS_V02 §2.2, Dong Zhuo's army): at half troops or more, attack the enemy with the fewest troops
    // (ties by slot); below half, defend. Reads the round-start state like the enemy AI.
    public static class AllyAi
    {
        public static IReadOnlyList<BattleCommand> Decide(BattleView view)
        {
            var commands = new List<BattleCommand>();
            var target = EnemyAi.Weakest(view.Enemies.Where(u => u.Active));
            foreach (var ally in view.Allies.Where(u => u.Active))
                commands.Add(ally.Troops * 2 >= ally.Stats.MaxTroops && target != null ? BattleCommand.Attack(ally, target) : BattleCommand.Defend(ally));
            return commands;
        }
    }

    // Battle 1: every enemy attacks the living unit with the most troops (ties by slot), so pressure lands on Zhang Fei.
    public sealed class HighestTroopsAi : IEnemyAi
    {
        public IReadOnlyList<BattleCommand> Decide(BattleView view)
        {
            var commands = new List<BattleCommand>();
            var target = view.Players.Where(u => u.Active).OrderByDescending(u => u.Troops).ThenBy(u => u.Slot).FirstOrDefault();
            if (target == null) return commands;
            foreach (var enemy in view.Enemies.Where(u => u.Active))
                commands.Add(BattleCommand.Attack(enemy, EnemyAi.Focus(enemy, view) ?? target));
            return commands;
        }
    }

    // Battles 2–3: attack the facing slot (extra enemies take our last slot); if that unit had already retreated
    // at round start, the weakest living one. Units with skills use them on rounds that are multiples of
    // SkillEveryRounds when they can afford it, rotating through their skills in unlock order.
    public sealed class FacingSlotAi : IEnemyAi
    {
        readonly int every;
        public FacingSlotAi(int skillEveryRounds) { every = skillEveryRounds < 1 ? 1 : skillEveryRounds; }

        public IReadOnlyList<BattleCommand> Decide(BattleView view)
        {
            var commands = new List<BattleCommand>();
            var living = view.Players.Where(u => u.Active).ToList();
            if (living.Count == 0) return commands;
            foreach (var enemy in view.Enemies.Where(u => u.Active))
            {
                var facing = view.Players[System.Math.Min(enemy.Slot, view.Players.Count - 1)];
                var target = EnemyAi.Focus(enemy, view) ?? (facing.Active ? facing : EnemyAi.Weakest(living));
                var skill = SkillThisRound(enemy, view.Round);
                if (skill != null && skill.TechCost <= enemy.Tech)
                    commands.Add(BattleCommand.UseSkill(enemy, skill, skill.IsSingleTarget ? target : null));
                else
                    commands.Add(BattleCommand.Attack(enemy, target));
            }
            return commands;
        }

        // Zhang Jiao: rounds 3, 9, 15 … the first skill, rounds 6, 12 … the second (combat GDD).
        SkillSpec SkillThisRound(BattleUnit enemy, int round)
        {
            if (enemy.Skills.Count == 0 || round % every != 0) return null;
            int index = (round / every - 1) % enemy.Skills.Count;
            return enemy.Skills[index];
        }
    }
}
