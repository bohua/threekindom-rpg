using System;

namespace Jrpg.Battle
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

    // V1_SPEC §5、§7 的全部数值公式。只做纯计算，不持有状态。
    public static class BattleRules
    {
        public const int MaxLevel = 5;
        public const int MaxAttribute = 100;
        public const int AdvantagePercent = 140;
        public const int DefendPercent = 50;
        public const int MinModifierPercent = 20;
        public const int RationHealPercent = 30;

        // 累计经验：1至5级。
        static readonly int[] ExpTable = { 0, 10, 30, 60, 100 };

        public static int LevelForExp(int exp)
        {
            int level = 1;
            for (int i = 1; i < ExpTable.Length; i++) if (exp >= ExpTable[i]) level = i + 1;
            return level;
        }

        public static int ExpForLevel(int level) => ExpTable[Math.Clamp(level, 1, MaxLevel) - 1];

        public static int Attribute(int baseValue, int growth, int level) =>
            Math.Min(MaxAttribute, baseValue + growth * (Math.Clamp(level, 1, MaxLevel) - 1));

        public static int TroopModifier(TroopType troop) => troop switch
        {
            TroopType.Infantry => 30,
            TroopType.Cavalry => 10,
            _ => 0,
        };

        public static int BaseSpeed(TroopType troop) => troop switch
        {
            TroopType.Cavalry => 10,
            TroopType.Archer => 8,
            _ => 6,
        };

        public static UnitStats Compute(GeneralDefinition general, int level, StatBonus bonus = default)
        {
            if (general == null) throw new ArgumentNullException(nameof(general));
            level = Math.Clamp(level, 1, MaxLevel);
            int force = Attribute(general.Force, general.ForceGrowth, level);
            int command = Attribute(general.Command, general.CommandGrowth, level);
            int intellect = Attribute(general.Intellect, general.IntellectGrowth, level);
            int infantryDefense = general.Troop == TroopType.Infantry ? 5 : 0;
            return new UnitStats(force, command, intellect,
                maxTroops: 80 + 20 * level + TroopModifier(general.Troop) + bonus.Troops,
                maxTech: 4 + 2 * level + bonus.Tech,
                physicalAttack: 10 + force / 2 + 2 * (level - 1) + bonus.PhysicalAttack,
                physicalDefense: 5 + command / 3 + (level - 1) + infantryDefense + bonus.PhysicalDefense,
                strategyAttack: 10 + intellect / 2 + 2 * (level - 1) + bonus.StrategyAttack,
                strategyDefense: 5 + intellect / 3 + (level - 1) + bonus.StrategyDefense,
                speed: BaseSpeed(general.Troop) + bonus.Speed);
        }

        public static bool HasAdvantage(TroopType attacker, TroopType defender) =>
            (attacker == TroopType.Cavalry && defender == TroopType.Infantry) ||
            (attacker == TroopType.Infantry && defender == TroopType.Archer) ||
            (attacker == TroopType.Archer && defender == TroopType.Cavalry);

        // 基础伤害＝攻×系数−防×0.5；先应用增减益；克制×1.4一次；防御×0.5；最终向下取整且至少为1。
        public static int Damage(decimal attack, decimal defense, int powerPercent, bool advantage, bool defending)
        {
            decimal value = attack * powerPercent / 100m - defense * 0.5m;
            if (advantage) value = value * AdvantagePercent / 100m;
            if (defending) value = value * DefendPercent / 100m;
            return Math.Max(1, (int)Math.Floor(value));
        }

        public static int PercentOf(int max, int percent) => max * percent / 100;
    }
}
