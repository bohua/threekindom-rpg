using System;
using ThreeKingdoms.Core.Battle;
using UnityEngine;

namespace ThreeKingdoms.Data.Battle
{
    [Serializable]
    public struct SkillUnlockEntry
    {
        [Min(1)] public int Level;
        public BattleSkillDefinition Skill;
    }

    // One general leads one unit; troops are its HP. Shared by both sides; enemies use zero growth
    // (fixed attributes across battles, only the level changes — combat GDD).
    [CreateAssetMenu(menuName = "ThreeKingdoms/Battle/General")]
    public sealed class GeneralDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        public TroopType Troop;
        [Range(1, 100)] public int Force = 50;
        [Range(1, 100)] public int Command = 50;
        [Range(1, 100)] public int Intellect = 50;
        [Min(0)] public int ForceGrowth;
        [Min(0)] public int CommandGrowth;
        [Min(0)] public int IntellectGrowth;
        [Tooltip("按顺序解锁；敌方的技能轮换也按此顺序")]
        public SkillUnlockEntry[] Skills = Array.Empty<SkillUnlockEntry>();
    }
}
