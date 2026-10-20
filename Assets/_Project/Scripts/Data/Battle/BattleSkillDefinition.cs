using ThreeKingdoms.Core.Battle;
using UnityEngine;

namespace ThreeKingdoms.Data.Battle
{
    // Static skill data. Coefficients and percentages are integer percents so the rules stay exact.
    [CreateAssetMenu(menuName = "ThreeKingdoms/Battle/Skill")]
    public sealed class BattleSkillDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        [TextArea] public string Description;
        [Min(0)] public int TechCost;
        public SkillTarget Target;
        public DamageKind Damage;
        [Tooltip("伤害系数百分数，例如 150 表示 1.5 倍；0 表示不造成伤害")]
        [Min(0)] public int PowerPercent;
        [Tooltip("回复目标最大兵力的百分比；0 表示不回复")]
        [Min(0)] public int HealPercent;
        public BattleStat ModifierStat;
        [Tooltip("属性变化百分数，例如 -20；0 表示无增减益")]
        public int ModifierPercent;
        [Tooltip("持续回合，含施放当回合")]
        [Min(0)] public int ModifierTurns;

        public SkillSpec ToRuntime() =>
            new SkillSpec(Id, DisplayName, Description, TechCost, Target, Damage, PowerPercent, HealPercent, ModifierStat, ModifierPercent, ModifierTurns);
    }
}
