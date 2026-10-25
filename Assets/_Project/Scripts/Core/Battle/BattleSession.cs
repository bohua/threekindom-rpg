using System;
using System.Collections.Generic;
using System.Linq;

namespace ThreeKingdoms.Core.Battle
{
    // What a finished battle hands back. The battle never writes outside state: on victory each owning system
    // applies its share (progression: Exp; equipment & items: Copper and RationsUsed; story: the clear flag for
    // EncounterId). On defeat nothing is applied, so retrying is just starting a new session (ADR-0002 §7).
    public sealed class BattleResult
    {
        public string EncounterId { get; }
        public BattleOutcome Outcome { get; }
        public int Rounds { get; }
        // Equal for every general in the party, not divided by line-up size (character-progression GDD).
        public int Exp { get; }
        public int Copper { get; }
        public int RationsUsed { get; }

        internal BattleResult(string encounterId, BattleOutcome outcome, int rounds, int exp, int copper, int rationsUsed)
        {
            EncounterId = encounterId; Outcome = outcome; Rounds = rounds; Exp = exp; Copper = copper; RationsUsed = rationsUsed;
        }
    }

    // The single entry point that advances a battle: enemy AI → round resolution → result.
    public sealed class BattleSession
    {
        readonly IEnemyAi ai;

        public EncounterSpec Encounter { get; }
        public BattleState State { get; }
        public BattleResult Result { get; private set; }

        public BattleSession(EncounterSpec encounter, IReadOnlyList<CombatantInput> lineup, ItemSpec rationItem, int rations,
            BattleConfig config, TroopTable troops, IEnemyAi ai = null)
        {
            Encounter = encounter ?? throw new ArgumentNullException(nameof(encounter));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (troops == null) throw new ArgumentNullException(nameof(troops));
            if (lineup == null || lineup.Count == 0) throw new ArgumentException("空队伍不可开战。");
            if (lineup.Count > BattleState.MaxLineup) throw new ArgumentException("最多 3 人出战。");
            if (lineup.Select(c => c.General.Id).Distinct().Count() != lineup.Count) throw new ArgumentException("同一武将不能重复出战。");
            foreach (var c in lineup)
                if (!troops.Get(c.General.Troop).PlayerUsable) throw new ArgumentException($"{c.General.DisplayName} 的兵种 {c.General.Troop} 只限敌方使用。");

            this.ai = ai ?? EnemyAi.For(encounter);
            var players = lineup.Select((c, slot) => new BattleUnit(Side.Player, slot, c.General, c.Level, c.Skills, troops, config, c.Equipment));
            // Enemies: fixed attributes, only the level differs between battles; no equipment.
            var enemies = encounter.Enemies.Select((e, slot) => new BattleUnit(Side.Enemy, slot, e.General, e.Level, e.General.SkillsAt(e.Level), troops, config)
                { FocusEscort = e.FocusEscort }).ToList();
            // Allies stand after our line-up (slot 3 on, SYSTEMS_V02 §2.2); like enemies they have no equipment.
            var allies = encounter.Allies.Select((a, i) => new BattleUnit(Side.Ally, BattleState.MaxLineup + i, a.General, a.Level, a.General.SkillsAt(a.Level), troops, config)).ToList();
            State = new BattleState(players, enemies, rationItem, rations, config, troops, allies, encounter.Script);
            for (int i = 0; i < allies.Count; i++) if (encounter.Allies[i].Escort) State.MarkEscort(allies[i]);
        }

        // turn_start events of the current round (SYSTEMS_V02 §1.2): call before our orders; PlayRound calls it too if
        // it was skipped. When an event ends the battle, Result is set and no orders are needed.
        public IReadOnlyList<BattleEvent> StartRound()
        {
            if (Result != null) return System.Array.Empty<BattleEvent>();
            var log = State.StartRound();
            Finalise();
            return log;
        }

        void Finalise()
        {
            if (State.Outcome == BattleOutcome.Ongoing || Result != null) return;
            bool won = State.Outcome == BattleOutcome.Victory;
            Result = new BattleResult(Encounter.Id, State.Outcome, State.Round,
                won ? Encounter.ExpReward : 0, won ? Encounter.CopperReward : 0, won ? State.RationsUsed : 0);
        }

        public IReadOnlyList<BattleEvent> PlayRound(IEnumerable<BattleCommand> playerCommands)
        {
            if (Result != null) throw new InvalidOperationException("战斗已结束。");
            var mine = playerCommands.ToList();
            if (mine.Any(c => c.Actor.Side != Side.Player)) throw new ArgumentException("只能为我方单位下令。");
            var log = new List<BattleEvent>(State.StartRound());
            Finalise();
            if (Result != null) return log;
            // Enemy and ally commands are fixed from the round-start view and never see ours.
            var view = new BattleView(State);
            var theirs = ai.Decide(view);
            var allied = AllyAi.Decide(view);
            log.AddRange(State.ResolveRound(mine.Concat(theirs).Concat(allied)));
            Finalise();
            return log;
        }
    }
}
