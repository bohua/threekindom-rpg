namespace Jrpg.Battle
{
    // 兵种：骑克步、步克弓、弓克骑（V1_SPEC §1、§5）。
    public enum TroopType { Infantry, Cavalry, Archer }

    public enum Side { Player, Enemy }

    // 可被增益／减益修改的派生属性。
    public enum BattleStat { PhysicalAttack, PhysicalDefense, StrategyAttack, StrategyDefense, Speed }

    public enum DamageKind { None, Physical, Strategy }

    public enum SkillTarget { SingleEnemy, AllEnemies, SingleAlly }

    public enum CommandKind { Attack, Skill, Defend, Item }

    public enum BattleOutcome { Ongoing, Victory, Defeat }

    // 装备等外部加成；首版第1步尚无装备系统，统一传 default。
    public struct StatBonus
    {
        public int Troops, Tech, PhysicalAttack, PhysicalDefense, StrategyAttack, StrategyDefense, Speed;
    }
}
