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
    }

    [Serializable]
    public struct AdvantagePair
    {
        public TroopType Attacker;
        public TroopType Defender;
    }

    // Troop-type numbers and the advantage pairs (combat GDD; enemy-only types user-confirmed 2026-10-18).
    [CreateAssetMenu(menuName = "ThreeKingdoms/Battle/Troop Table")]
    public sealed class TroopTableDefinition : ScriptableObject
    {
        public TroopTypeEntry[] Types = Array.Empty<TroopTypeEntry>();
        public AdvantagePair[] Advantages = Array.Empty<AdvantagePair>();
    }
}
