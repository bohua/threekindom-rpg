using UnityEngine;

namespace ThreeKingdoms.Data.Battle
{
    // A battle-usable item. V1 has one, army rations; prices and stock belong to the equipment & items system.
    [CreateAssetMenu(menuName = "ThreeKingdoms/Battle/Item")]
    public sealed class ItemDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        [Tooltip("回复目标最大兵力的百分比（向下取整）")] [Min(0)] public int HealPercent = 30;
    }
}
