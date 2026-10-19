using UnityEngine;

namespace ThreeKingdoms.Data.Battle
{
    // Global combat numbers the GDD marks as data.
    [CreateAssetMenu(menuName = "ThreeKingdoms/Battle/Battle Config")]
    public sealed class BattleConfigDefinition : ScriptableObject
    {
        [Tooltip("兵种占优伤害百分数，140 = ×1.4")] public int AdvantagePercent = 140;
        [Tooltip("防御时受到伤害的百分数，50 = ×0.5")] public int GuardPercent = 50;
        [Tooltip("增减益后的属性倍率下限，20 = ×0.2")] public int MinModifierPercent = 20;
        [Tooltip("三核心属性上限")] public int MaxAttribute = 100;
        [Tooltip("1 级起的累计经验，长度即等级上限")] public int[] ExpTable = { 0, 10, 30, 60, 100 };
    }
}
