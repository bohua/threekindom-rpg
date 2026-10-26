namespace ThreeKingdoms.Core.Battle
{
    // Closed set (ADR-0002 §3). Yellow Turban and Strategist are enemy-only (combat GDD, user 2026-10-18).
    public enum TroopType { Infantry, Cavalry, Archer, YellowTurban, Strategist }

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
