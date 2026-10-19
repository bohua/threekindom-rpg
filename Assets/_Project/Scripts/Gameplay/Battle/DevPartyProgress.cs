using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Battle;
using ThreeKingdoms.Data.Battle;

namespace ThreeKingdoms.Gameplay.Battle
{
    // TEMPORARY stand-in for the systems that own out-of-battle state until they exist: progression (exp),
    // equipment & items (rations, copper, equipment bonus) and story (clear flags). Persistence is ADR-0004.
    // It only applies a finished BattleResult, exactly as those owners will (ADR-0002 §7); the battle core
    // never touches it.
    public sealed class DevPartyProgress
    {
        readonly BattleData data;
        readonly Dictionary<string, int> exp = new Dictionary<string, int>();
        readonly HashSet<string> cleared = new HashSet<string>();

        public int Rations { get; private set; }
        public int Copper { get; private set; }

        public DevPartyProgress(BattleData data, int rations, int copper)
        {
            this.data = data;
            Rations = rations; Copper = copper;
            foreach (var g in data.Party) exp[g.Id] = 0;
        }

        public int Exp(GeneralSpec g) => exp.TryGetValue(g.Id, out int e) ? e : 0;
        public int Level(GeneralSpec g) => BattleRules.LevelForExp(Exp(g), data.Config);
        public bool HasCleared(string encounterId) => cleared.Contains(encounterId);

        public IReadOnlyList<CombatantInput> Lineup() =>
            data.Party.Select(g => CombatantInput.AtLevel(g, Level(g))).ToList();

        public sealed class Applied
        {
            public bool RewardGranted;
            public readonly List<(GeneralSpec general, int from, int to, List<SkillSpec> learned)> LevelUps = new List<(GeneralSpec, int, int, List<SkillSpec>)>();
        }

        // Victory: rations used are spent either way; rewards only the first time this save clears it.
        // Defeat: nothing is applied, which is the "restore pre-battle state" rule.
        public Applied Apply(BattleResult result)
        {
            var applied = new Applied();
            if (result.Outcome != BattleOutcome.Victory) return applied;
            Rations -= result.RationsUsed;
            if (!cleared.Add(result.EncounterId)) return applied;
            applied.RewardGranted = true;
            Copper += result.Copper;
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
