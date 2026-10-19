namespace ThreeKingdoms.Core.Battle
{
    // Closed set (ADR-0002 §3). Yellow Turban and Strategist are enemy-only (combat GDD, user 2026-10-18).
    public enum TroopType { Infantry, Cavalry, Archer, YellowTurban, Strategist }

    public enum Side { Player, Enemy }

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
