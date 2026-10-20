using System;
using System.Collections.Generic;
using System.Linq;

namespace ThreeKingdoms.Core.Battle
{
    public sealed class StatModifier
    {
        public BattleStat Stat { get; }
        public int Percent { get; internal set; }
        // Includes the casting round; −1 at each round end, removed at 0.
        public int RemainingTurns { get; internal set; }
        internal StatModifier(BattleStat stat, int percent, int turns) { Stat = stat; Percent = percent; RemainingTurns = turns; }
    }

    // Mutable state of one unit for one battle; built from specs, never written back to data.
    public sealed class BattleUnit
    {
        readonly List<StatModifier> modifiers = new List<StatModifier>();
        readonly BattleConfig config;

        public Side Side { get; }
        public int Slot { get; }
        public GeneralSpec General { get; }
        public string Id => General.Id;
        public string Name => General.DisplayName;
        public TroopType Troop => General.Troop;
        public int Level { get; }
        public UnitStats Stats { get; }
        public IReadOnlyList<SkillSpec> Skills { get; }
        public int Troops { get; private set; }
        public int Tech { get; private set; }
        public bool Defending { get; internal set; }
        public bool Active => Troops > 0;
        public IReadOnlyList<StatModifier> Modifiers => modifiers;

        public BattleUnit(Side side, int slot, GeneralSpec general, int level, IEnumerable<SkillSpec> skills,
            TroopTable troops, BattleConfig config, StatBonus bonus = default)
        {
            General = general ?? throw new ArgumentNullException(nameof(general));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            Side = side; Slot = slot;
            Level = BattleRules.ClampLevel(level, config);
            Stats = BattleRules.Compute(general, Level, bonus, troops, config);
            Skills = (skills ?? Enumerable.Empty<SkillSpec>()).Where(s => s != null).ToArray();
            Troops = Stats.MaxTroops;
            Tech = Stats.MaxTech;
        }

        public bool Knows(SkillSpec skill) => skill != null && Skills.Any(s => s.Id == skill.Id);

        // Same stat + same direction keeps only the stronger value; buff and debuff on one stat add up, floor 0.2×.
        public int ModifierPercent(BattleStat stat)
        {
            int sum = 0;
            foreach (var m in modifiers) if (m.Stat == stat) sum += m.Percent;
            return Math.Max(config.MinModifierPercent, 100 + sum);
        }

        // Only used for ordering and display; damage uses the exact integer path in BattleRules.Damage.
        public int EffectiveFloor(BattleStat stat) => Stats.Get(stat) * ModifierPercent(stat) / 100;

        internal void ApplyModifier(BattleStat stat, int percent, int turns)
        {
            if (percent == 0 || turns <= 0) return;
            var existing = modifiers.FirstOrDefault(m => m.Stat == stat && Math.Sign(m.Percent) == Math.Sign(percent));
            if (existing == null) { modifiers.Add(new StatModifier(stat, percent, turns)); return; }
            if (Math.Abs(percent) > Math.Abs(existing.Percent)) existing.Percent = percent;
            existing.RemainingTurns = turns;
        }

        internal StatModifier FindModifier(BattleStat stat, int sign) =>
            modifiers.FirstOrDefault(m => m.Stat == stat && Math.Sign(m.Percent) == sign);

        internal List<StatModifier> TickModifiers()
        {
            var expired = new List<StatModifier>();
            foreach (var m in modifiers) if (--m.RemainingTurns <= 0) expired.Add(m);
            modifiers.RemoveAll(m => m.RemainingTurns <= 0);
            return expired;
        }

        internal int TakeDamage(int amount)
        {
            int actual = Math.Min(Troops, Math.Max(0, amount));
            Troops -= actual;
            return actual;
        }

        internal int Heal(int amount)
        {
            int actual = Math.Min(Stats.MaxTroops - Troops, Math.Max(0, amount));
            Troops += actual;
            return actual;
        }

        internal bool SpendTech(int cost)
        {
            if (cost > Tech) return false;
            Tech -= cost;
            return true;
        }

        public override string ToString() => $"{Side}:{Slot}:{Id}";
    }
}
