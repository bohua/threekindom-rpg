using System;
using System.Collections.Generic;
using System.Linq;

namespace Jrpg.Battle
{
    // 战斗之外持续存在的武将成长；不保存兵力／技力，因此战后天然全恢复。
    public sealed class GeneralState
    {
        public GeneralDefinition Definition { get; }
        public int Exp { get; internal set; }
        public int Level => BattleRules.LevelForExp(Exp);

        public GeneralState(GeneralDefinition definition, int exp = 0)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Exp = Math.Max(0, exp);
        }

        // 固定成长、自动学技：等级达到即掌握。
        public IEnumerable<BattleSkillDefinition> KnownSkills =>
            Definition.Skills.Where(s => s.Skill != null && s.Level <= Level).OrderBy(s => s.Level).Select(s => s.Skill);
    }

    public sealed class PartySnapshot
    {
        internal Dictionary<GeneralDefinition, int> Exp;
        internal int Rations, Copper;
        internal HashSet<string> Cleared;
    }

    public sealed class PartyState
    {
        readonly List<GeneralState> members = new List<GeneralState>();
        readonly HashSet<string> clearedEncounters = new HashSet<string>();

        public IReadOnlyList<GeneralState> Members => members;
        public int Rations { get; internal set; }
        public int Copper { get; internal set; }
        public IReadOnlyCollection<string> ClearedEncounters => clearedEncounters;

        public PartyState(IEnumerable<GeneralState> generals, int rations, int copper = 0)
        {
            members.AddRange(generals);
            Rations = rations; Copper = copper;
        }

        public GeneralState Find(GeneralDefinition def) => members.FirstOrDefault(m => m.Definition == def);
        public bool HasCleared(string encounterId) => clearedEncounters.Contains(encounterId);

        public PartySnapshot Capture() => new PartySnapshot
        {
            Exp = members.ToDictionary(m => m.Definition, m => m.Exp),
            Rations = Rations,
            Copper = Copper,
            Cleared = new HashSet<string>(clearedEncounters),
        };

        public void Restore(PartySnapshot snapshot)
        {
            foreach (var m in members) if (snapshot.Exp.TryGetValue(m.Definition, out int exp)) m.Exp = exp;
            Rations = snapshot.Rations;
            Copper = snapshot.Copper;
            clearedEncounters.Clear();
            clearedEncounters.UnionWith(snapshot.Cleared);
        }

        internal void MarkCleared(string id) => clearedEncounters.Add(id);
    }

    public sealed class LevelUp
    {
        public GeneralState General { get; }
        public int From { get; }
        public int To { get; }
        public IReadOnlyList<BattleSkillDefinition> LearnedSkills { get; }
        internal LevelUp(GeneralState general, int from, int to, IReadOnlyList<BattleSkillDefinition> learned)
        {
            General = general; From = from; To = to; LearnedSkills = learned;
        }
    }

    public sealed class BattleResult
    {
        public BattleOutcome Outcome { get; internal set; }
        public bool RewardGranted { get; internal set; }
        public int Exp { get; internal set; }
        public int Copper { get; internal set; }
        public List<LevelUp> LevelUps { get; } = new List<LevelUp>();
    }

    // 战前快照→战斗→胜利发奖一次／战败还原（V1_SPEC §4.1、§4.9、§7）。
    public sealed class BattleSession
    {
        readonly PartySnapshot snapshot;

        public PartyState Party { get; }
        public EncounterDefinition Encounter { get; }
        public BattleState State { get; }
        public BattleResult Result { get; private set; }

        public BattleSession(PartyState party, EncounterDefinition encounter, IReadOnlyList<GeneralDefinition> lineup)
        {
            Party = party ?? throw new ArgumentNullException(nameof(party));
            Encounter = encounter ?? throw new ArgumentNullException(nameof(encounter));
            if (lineup == null || lineup.Count == 0) throw new ArgumentException("空队伍不可开战。");
            if (lineup.Count > BattleState.MaxLineup) throw new ArgumentException("最多3人出战。");
            if (lineup.Distinct().Count() != lineup.Count) throw new ArgumentException("同一武将不能重复出战。");

            snapshot = party.Capture();
            var players = lineup.Select((def, slot) =>
            {
                var member = party.Find(def) ?? throw new ArgumentException($"{def.DisplayName} 不在队伍中。");
                return new BattleUnit(Side.Player, slot, def, member.Level, member.KnownSkills);
            });
            var enemies = encounter.Enemies.Select((e, slot) =>
                new BattleUnit(Side.Enemy, slot, e.General, e.Level, e.General.Skills.Where(s => s.Level <= e.Level).Select(s => s.Skill)));
            State = new BattleState(players, enemies, party.Rations);
        }

        public List<BattleEvent> PlayRound(IEnumerable<BattleCommand> playerCommands)
        {
            // 敌方指令先按回合开始状态决定，不读取我方指令。
            var enemyCommands = EnemyAi.Decide(State);
            var log = State.ResolveRound(playerCommands.Concat(enemyCommands));
            if (State.Outcome != BattleOutcome.Ongoing) Finish();
            return log;
        }

        void Finish()
        {
            if (Result != null) return;
            Result = new BattleResult { Outcome = State.Outcome };
            if (State.Outcome == BattleOutcome.Defeat)
            {
                // 战败：还原战前状态，避免累计道具损失。
                Party.Restore(snapshot);
                return;
            }
            Party.Rations = State.Rations;
            if (Party.HasCleared(Encounter.Id)) return;
            Party.MarkCleared(Encounter.Id);
            Party.Copper += Encounter.CopperReward;
            Result.RewardGranted = true;
            Result.Exp = Encounter.ExpReward;
            Result.Copper = Encounter.CopperReward;
            // 等额经验，不按出战人数稀释。
            foreach (var member in Party.Members)
            {
                int from = member.Level;
                var before = member.KnownSkills.ToList();
                member.Exp += Encounter.ExpReward;
                if (member.Level > from)
                    Result.LevelUps.Add(new LevelUp(member, from, member.Level, member.KnownSkills.Except(before).ToList()));
            }
        }
    }
}
