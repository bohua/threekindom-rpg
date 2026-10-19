using UnityEngine;

namespace Jrpg.Battle
{
    // 静态技能数据。系数与百分比用整数百分数保存，保证结算是精确的十进制运算。
    [CreateAssetMenu(menuName = "JRPG/Battle/Skill")]
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
        [Min(0)] public int ModifierTurns;

        public bool TargetsAllies => Target == SkillTarget.SingleAlly;
    }
}
