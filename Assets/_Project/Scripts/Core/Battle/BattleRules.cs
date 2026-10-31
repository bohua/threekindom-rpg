using System;

namespace ThreeKingdoms.Core.Battle
{
    public readonly struct UnitStats
    {
        public readonly int Force, Command, Intellect;
        public readonly int MaxTroops, MaxTech, PhysicalAttack, PhysicalDefense, StrategyAttack, StrategyDefense, Speed;

        public UnitStats(int force, int command, int intellect, int maxTroops, int maxTech,
            int physicalAttack, int physicalDefense, int strategyAttack, int strategyDefense, int speed)
        {
            Force = force; Command = command; Intellect = intellect;
            MaxTroops = maxTroops; MaxTech = maxTech;
            PhysicalAttack = physicalAttack; PhysicalDefense = physicalDefense;
            StrategyAttack = strategyAttack; StrategyDefense = strategyDefense; Speed = speed;
        }

        public int Get(BattleStat stat) => stat switch
        {
            BattleStat.PhysicalAttack => PhysicalAttack,
            BattleStat.PhysicalDefense => PhysicalDefense,
            BattleStat.StrategyAttack => StrategyAttack,
            BattleStat.StrategyDefense => StrategyDefense,
            BattleStat.Speed => Speed,
            _ => throw new ArgumentOutOfRangeException(nameof(stat)),
        };
    }

    // Combat GDD formulas. Pure functions; the structural constants (80 + 20 × level …) are the formulas
    // themselves, every variable the GDD marks as data comes from BattleConfig / TroopTable / SkillSpec.
    public static class BattleRules
    {
        public static int LevelForExp(int exp, BattleConfig config)
        {
            int level = 1;
            for (int i = 1; i < config.ExpTable.Count; i++) if (exp >= config.ExpTable[i]) level = i + 1;
            return level;
        }

        public static int ClampLevel(int level, BattleConfig config) => Math.Clamp(level, 1, config.MaxLevel);

        // Attributes are fixed (user 2026-10-27, BALANCE_MODEL_v1 §3.1): only story events change them, never the level.
        public static int Attribute(int baseValue, BattleConfig config) => Math.Min(config.MaxAttribute, baseValue);

        // v1.3 derived formulas: all growth sits in the level terms (troops 16, attacks 4, defences 2 per level) plus the
        // troop rank (levels 1 / 10 / 20: troops +10, defence +2 per rank).
        public static UnitStats Compute(GeneralSpec general, int level, StatBonus bonus, TroopTable troops, BattleConfig config)
        {
            if (general == null) throw new ArgumentNullException(nameof(general));
            level = ClampLevel(level, config);
            var troop = troops.Get(general.Troop);
            int rank = TroopRules.Rank(level);
            int force = Attribute(general.Force, config);
            int command = Attribute(general.Command, config);
            int intellect = Attribute(general.Intellect, config);
            return new UnitStats(force, command, intellect,
                maxTroops: 80 + 16 * level + troop.TroopBonus + TroopRules.RankTroops(rank) + bonus.Troops,
                maxTech: 4 + 2 * level + bonus.Tech,
                physicalAttack: 10 + force / 2 + 4 * (level - 1) + bonus.PhysicalAttack,
                physicalDefense: 5 + command / 3 + 2 * (level - 1) + troop.DefenseBonus + TroopRules.RankDefense(rank) + bonus.PhysicalDefense,
                strategyAttack: 10 + intellect / 2 + 4 * (level - 1) + bonus.StrategyAttack,
                strategyDefense: 5 + intellect / 3 + 2 * (level - 1) + bonus.StrategyDefense,
                speed: troop.BaseSpeed + bonus.Speed);
        }

        // final = max(1, floor((atk·mA·coef − def·mD·0.5) × matchup × guard)), all factors as integer percents.
        // Exact integer arithmetic: identical on every platform and scripting backend (ADR-0002 §6).
        public static int Damage(int attack, int attackModPercent, int defense, int defenseModPercent,
            int powerPercent, int advantagePercent, int guardPercent, BattleConfig config)
        {
            // Scale 20000: atk·mA·coef has scale 100·100; def·mD·0.5 = def·mD·50 / 10000 → ×2 on both terms.
            long baseScaled = 2L * attack * attackModPercent * powerPercent - 100L * defense * defenseModPercent;
            long numerator = baseScaled * advantagePercent * guardPercent;
            const long denominator = 20000L * 100 * 100;
            long floored = numerator >= 0 ? numerator / denominator : -((-numerator + denominator - 1) / denominator);
            return (int)Math.Max(1, floored);
        }

        public static int PercentOf(int max, int percent) => max * percent / 100;
    }
}
