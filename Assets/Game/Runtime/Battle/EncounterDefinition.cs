using System;
using UnityEngine;

namespace Jrpg.Battle
{
    [Serializable]
    public struct EnemyEntry
    {
        public GeneralDefinition General;
        [Range(1, BattleRules.MaxLevel)] public int Level;
    }

    [CreateAssetMenu(menuName = "JRPG/Battle/Encounter")]
    public sealed class EncounterDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        public EnemyEntry[] Enemies = Array.Empty<EnemyEntry>();
        [Min(0)] public int ExpReward;
        [Min(0)] public int CopperReward;
    }
}
