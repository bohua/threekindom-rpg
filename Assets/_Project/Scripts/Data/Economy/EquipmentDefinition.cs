using ThreeKingdoms.Core.Battle;
using ThreeKingdoms.Core.Economy;
using UnityEngine;

namespace ThreeKingdoms.Data.Economy
{
    // One kind of equipment (equipment-items GDD catalogue). Values are adopted drafts, to be tuned after play-testing.
    [CreateAssetMenu(menuName = "ThreeKingdoms/Economy/Equipment")]
    public sealed class EquipmentDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        public EquipSlot Slot;
        [Tooltip("只对武器有效：限定兵种")] public bool LimitTroop;
        public TroopType Troop;
        [Header("加值")]
        public int Troops;
        public int Tech;
        public int PhysicalAttack;
        public int PhysicalDefense;
        public int StrategyAttack;
        public int StrategyDefense;
        public int Speed;
        [Header("价格")]
        [Tooltip("0 = 商店不出售（初始装备、支线奖励）")] [Min(0)] public int BuyPrice;
        [Tooltip("没有买价的物品，铁匠收购的配置售价")] [Min(0)] public int GiftSellPrice;
        [Tooltip("1＝涿郡与卢植大营有售，2＝安喜市集起有售（0.2）")] [Min(1)] public int ShopTier = 1;

        public EquipmentSpec ToRuntime() => new EquipmentSpec(Id, DisplayName, Slot, LimitTroop ? Troop : (TroopType?)null,
            new StatBonus
            {
                Troops = Troops, Tech = Tech, PhysicalAttack = PhysicalAttack, PhysicalDefense = PhysicalDefense,
                StrategyAttack = StrategyAttack, StrategyDefense = StrategyDefense, Speed = Speed,
            },
            BuyPrice, GiftSellPrice, ShopTier);
    }
}
