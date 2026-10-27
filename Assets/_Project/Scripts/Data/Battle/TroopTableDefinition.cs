using System;
using ThreeKingdoms.Core.Battle;
using UnityEngine;

namespace ThreeKingdoms.Data.Battle
{
    [Serializable]
    public struct TroopTypeEntry
    {
        public TroopType Type;
        public int TroopBonus;
        public int DefenseBonus;
        public int BaseSpeed;
        public bool PlayerUsable;
        [Tooltip("移动力；0 = 兵种默认")] public int Move;
        [Tooltip("射程；0 = 兵种默认")] public int Range;
    }

    // Troop-type numbers (BALANCE_MODEL_v1 §3.14). The matchups are rules in TroopRules (压制 + 特长), no longer pairs here.
    [CreateAssetMenu(menuName = "ThreeKingdoms/Battle/Troop Table")]
    public sealed class TroopTableDefinition : ScriptableObject
    {
        public TroopTypeEntry[] Types = Array.Empty<TroopTypeEntry>();
    }
}
