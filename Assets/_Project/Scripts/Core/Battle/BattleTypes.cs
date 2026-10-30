namespace ThreeKingdoms.Core.Battle
{
    // Closed set (ADR-0002 §3), extended to 8 main + 6 special types (BALANCE_MODEL_v1 §3.14, user 2026-10-27).
    // Values of the first five are kept for the existing assets: YellowTurban is now 贼众 (Yellow Turbans, bandits, raiders).
    public enum TroopType
    {
        Infantry, Cavalry, Archer, YellowTurban, Strategist, Spearman, Crossbow, HorseArcher,
        Swordswoman, Dancer, Shaman, Pirate, Barbarian, Elephant,
        // User 2026-10-30: civil officers get three lines after 曹操传 / 英杰传 — Strategist 军师 (offensive, the "black
        // and red mage"), Geomancer 风水师 (healing and support, the "white mage"), Shaman now 妖术师 (status and disruption).
        Geomancer,
    }

    // 大系: the matchup triangle is horse → foot → bow → horse; the mind class stands outside it.
    public enum TroopClass { Foot, Horse, Bow, Mind }

    // A unit's five-element (单位五行): a named general's school leaning, or set per enemy in the data (BALANCE_MODEL_v1 §3.5).
    public enum UnitElement { None, Metal, Wood, Water, Fire, Earth }

    // Ally (0.2, SYSTEMS_V02 §2): a unit on our side that takes no orders, e.g. Dong Zhuo's army in battle 5.
    public enum Side { Player, Enemy, Ally }

    // Why a unit left the field: troops at 0 (被击破) or a battle event sent it away (溃走, SYSTEMS_V02 §1.2).
    public enum RetreatReason { Defeated, Routed }

    // Derived stats that buffs / debuffs can modify.
    public enum BattleStat { PhysicalAttack, PhysicalDefense, StrategyAttack, StrategyDefense, Speed }

    public enum DamageKind { None, Physical, Strategy }

    public enum SkillTarget { SingleEnemy, AllEnemies, SingleAlly }

    public enum CommandKind { Attack, Skill, Defend, Item }

    public enum BattleOutcome { Ongoing, Victory, Defeat }

    // Fixed enemy AI policies selected per encounter (ADR-0002 §5).
    public enum EnemyAiKind { HighestTroops, FacingSlot }

    // External bonuses, i.e. equipment. Owned by the equipment system; the battle core only reads them.
    public struct StatBonus
    {
        public int Troops, Tech, PhysicalAttack, PhysicalDefense, StrategyAttack, StrategyDefense, Speed;
    }
}
