using System;
using ThreeKingdoms.Core.Battle;
using UnityEngine;

namespace ThreeKingdoms.Data.Battle
{
    [Serializable]
    public struct EnemyEntry
    {
        public GeneralDefinition General;
        [Min(1)] public int Level;
    }

    [CreateAssetMenu(menuName = "ThreeKingdoms/Battle/Encounter")]
    public sealed class EncounterDefinition : ScriptableObject
    {
        [Tooltip("与剧情标志一致：battle_1／battle_2／battle_3")]
        public string Id;
        public string DisplayName;
        public EnemyEntry[] Enemies = Array.Empty<EnemyEntry>();
        public EnemyAiKind Ai;
        [Min(1)] public int SkillEveryRounds = 3;
        [Min(0)] public int ExpReward;
        [Min(0)] public int CopperReward;
    }
}
