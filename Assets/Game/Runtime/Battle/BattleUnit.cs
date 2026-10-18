using System;
using System.Collections.Generic;
using System.Linq;

namespace Jrpg.Battle
{
    public sealed class StatModifier
    {
        public BattleStat Stat { get; }
        public int Percent { get; internal set; }
        // 包含施放当回合；回合结束时减1，归零移除。
        public int RemainingTurns { get; internal set; }
        internal StatModifier(BattleStat stat, int percent, int turns) { Stat = stat; Percent = percent; RemainingTurns = turns; }
    }

    // 单场战斗内的可变状态；从静态定义复制，不回写资产。
    public sealed class BattleUnit
    {
        readonly List<StatModifier> modifiers = new List<StatModifier>();

        public Side Side { get; }
        public int Slot { get; }
        public string Id { get; }
        public string Name { get; }
        public TroopType Troop { get; }
        public int Level { get; }
        public UnitStats Stats { get; }
        public IReadOnlyList<BattleSkillDefinition> Skills { get; }
        public int Troops { get; private set; }
        public int Tech { get; private set; }
        public bool Defending { get; internal set; }
        public bool Active => Troops > 0;
        public IReadOnlyList<StatModifier> Modifiers => modifiers;

        public BattleUnit(Side side, int slot, GeneralDefinition general, int level, IEnumerable<BattleSkillDefinition> skills, StatBonus bonus = default)
        {
            if (general == null) throw new ArgumentNullException(nameof(general));
            Side = side; Slot = slot; Id = general.Id; Name = general.DisplayName; Troop = general.Troop;
            Level = Math.Clamp(level, 1, BattleRules.MaxLevel);
            Stats = BattleRules.Compute(general, Level, bonus);
            Skills = (skills ?? Enumerable.Empty<BattleSkillDefinition>()).Where(s => s != null).ToArray();
            Troops = Stats.MaxTroops;
            Tech = Stats.MaxTech;
        }

        public bool Knows(BattleSkillDefinition skill) => Skills.Contains(skill);

        // 同属性同方向只取较强效果并刷新持续；增益与减益倍率相加，最低0.2倍。
        public decimal Multiplier(BattleStat stat)
        {
            int sum = 0;
            foreach (var m in modifiers) if (m.Stat == stat) sum += m.Percent;
            return Math.Max(BattleRules.MinModifierPercent, 100 + sum) / 100m;
        }

        public decimal Effective(BattleStat stat) => Stats.Get(stat) * Multiplier(stat);

        internal void ApplyModifier(BattleStat stat, int percent, int turns)
        {
            if (percent == 0 || turns <= 0) return;
            var existing = modifiers.FirstOrDefault(m => m.Stat == stat && Math.Sign(m.Percent) == Math.Sign(percent));
            if (existing == null) { modifiers.Add(new StatModifier(stat, percent, turns)); return; }
            if (Math.Abs(percent) > Math.Abs(existing.Percent)) existing.Percent = percent;
            existing.RemainingTurns = turns;
        }

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

        public override string ToString() => $"{Side}:{Slot}:{Name}";
    }
}
