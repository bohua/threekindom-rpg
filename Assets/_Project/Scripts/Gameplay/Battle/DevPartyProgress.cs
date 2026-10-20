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

        public int Rations { get; private set; }
        public int Copper { get; private set; }

        public DevPartyProgress(BattleData data, int rations, int copper, int startExp = 0)
        {
            this.data = data;
            Rations = rations; Copper = copper;
            foreach (var g in data.Party) exp[g.Id] = startExp;
        }

        public int Exp(GeneralSpec g) => exp.TryGetValue(g.Id, out int e) ? e : 0;
        public int Level(GeneralSpec g) => BattleRules.LevelForExp(Exp(g), data.Config);
        // The roster is whoever the story says has joined (ADR-0006: StoryState join flags), in party order.
        public IReadOnlyList<CombatantInput> Lineup(System.Func<string, bool> hasJoined) =>
            data.Party.Where(g => hasJoined(g.Id)).Select(g => CombatantInput.AtLevel(g, Level(g))).ToList();

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
            Rations -= result.RationsUsed;
            if (!firstClear) return applied;
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
