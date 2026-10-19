using System;
using UnityEngine;

namespace Jrpg.Battle
{
    [Serializable]
    public struct SkillUnlock
    {
        [Min(1)] public int Level;
        public BattleSkillDefinition Skill;
    }

    // 一名武将率一支部队；兵力即HP（V1_SPEC §1、§5）。敌我共用同一定义。
    [CreateAssetMenu(menuName = "JRPG/Battle/General")]
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
        public SkillUnlock[] Skills = Array.Empty<SkillUnlock>();
    }
}
