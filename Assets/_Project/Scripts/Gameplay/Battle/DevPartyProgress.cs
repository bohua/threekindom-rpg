using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Battle;
using ThreeKingdoms.Core.Economy;
using ThreeKingdoms.Data.Battle;

namespace ThreeKingdoms.Gameplay.Battle
{
    // Out-of-battle party state: progression (exp, still a stand-in until its own system) and the equipment & items
    // Inventory (copper, rations, the three slots). Persistence is ADR-0004. It only applies a finished BattleResult
    // (ADR-0002 §7); the battle core never touches it.
    public sealed class DevPartyProgress
    {
        readonly BattleData data;
        readonly Dictionary<string, int> exp = new Dictionary<string, int>();

        public Inventory Inventory { get; }
        public int Rations => Inventory.Rations;
        public int Copper => Inventory.Copper;

        public DevPartyProgress(BattleData data, Inventory inventory, int startExp = 0)
        {
            this.data = data;
            Inventory = inventory;
            foreach (var g in data.Party) exp[g.Id] = startExp;
        }

        public IReadOnlyList<GeneralSpec> Party => data.Party;

        // Derived stats out of battle (shop preview, general page): the same formulas the battle uses.
        public UnitStats Stats(GeneralSpec g, StatBonus bonus) => BattleRules.Compute(g, Level(g), bonus, data.Troops, data.Config);

        public IReadOnlyDictionary<string, int> ExpByGeneral => exp;

        // A saved play-through (ADR-0004); unknown generals are ignored, missing ones keep 0.
        public void RestoreExp(IEnumerable<(string generalId, int exp)> saved)
        {
            foreach (var (id, e) in saved)
                if (exp.ContainsKey(id)) exp[id] = System.Math.Max(0, e);
        }

        public int Exp(GeneralSpec g) => exp.TryGetValue(g.Id, out int e) ? e : 0;
        public int Level(GeneralSpec g) => BattleRules.LevelForExp(Exp(g), data.Config);
        // A chosen line-up, in slot order (pre-battle preparation).
        public IReadOnlyList<CombatantInput> Lineup(IEnumerable<GeneralSpec> ordered) =>
            ordered.Select(g => CombatantInput.AtLevel(g, Level(g), Inventory.Bonus(g.Id))).ToList();

        // The roster is whoever the story says has joined (ADR-0006: StoryState join flags), in party order, with
        // what each one wears.
        public IReadOnlyList<CombatantInput> Lineup(System.Func<string, bool> hasJoined) =>
            data.Party.Where(g => hasJoined(g.Id)).Select(g => CombatantInput.AtLevel(g, Level(g), Inventory.Bonus(g.Id))).ToList();

        public sealed class Applied
        {
            public bool RewardGranted;
            public readonly List<(GeneralSpec general, int from, int to, List<SkillSpec> learned)> LevelUps = new List<(GeneralSpec, int, int, List<SkillSpec>)>();
        }

        // Victory: rations used are spent either way; exp and copper only when the story recorded this battle as a
        // first clear (StoryState.RecordVictory is the single reward-once gate, ADR-0006).
        // Defeat: nothing is applied, which is the "restore pre-battle state" rule.
        public Applied Apply(BattleResult result, bool firstClear)
        {
            var applied = new Applied();
            if (result.Outcome != BattleOutcome.Victory) return applied;
            Inventory.SpendRations(result.RationsUsed);
            if (!firstClear) return applied;
            applied.RewardGranted = true;
            Inventory.AddCopper(result.Copper);
            foreach (var g in data.Party)
            {
                int from = Level(g);
                var before = g.SkillsAt(from).ToList();
                exp[g.Id] = Exp(g) + result.Exp;   // equal exp for everyone, not divided by line-up size
                int to = Level(g);
                if (to > from) applied.LevelUps.Add((g, from, to, g.SkillsAt(to).Where(s => !before.Contains(s)).ToList()));
            }
            return applied;
        }
    }
}
