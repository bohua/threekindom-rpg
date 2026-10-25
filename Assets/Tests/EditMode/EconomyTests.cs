using System.Linq;
using NUnit.Framework;
using ThreeKingdoms.Core.Battle;
using ThreeKingdoms.Core.Economy;
using ThreeKingdoms.Data.Battle;
using UnityEditor;

namespace ThreeKingdoms.Tests.EditMode
{
    // equipment-items GDD acceptance criteria, against the shipped catalogue.
    public sealed class EconomyTests
    {
        const string CatalogPath = "Assets/_Project/Data/Battle/BattleCatalog.asset";

        BattleData data;
        EconomyData eco;
        Inventory inv;
        GeneralSpec liu, guan, zhang;

        [SetUp]
        public void Load()
        {
            data = AssetDatabase.LoadAssetAtPath<BattleCatalog>(CatalogPath).ToRuntime();
            eco = data.Economy;
            inv = new Inventory(eco);
            liu = data.Party.First(g => g.Id == "liu_bei");
            guan = data.Party.First(g => g.Id == "guan_yu");
            zhang = data.Party.First(g => g.Id == "zhang_fei");
        }

        EquipmentSpec Item(string id) => eco.Find(id);

        [Test]
        public void CatalogueHasTwelveKindsAndTheShopSellsEight()
        {
            Assert.AreEqual(23, eco.Equipment.Count);   // 0.2: 12 + the Anxi tier (5) + two story keepsakes; 0.3: + Gongsun tier (3) + 白马铃
            Assert.AreEqual(7, eco.StockAt(1).Count(), "涿郡与大营只卖第一档");
            Assert.AreEqual(12, eco.StockAt(2).Count(), "安喜市集加 5 件");
            Assert.That(eco.StockAt(1).Select(e => e.Id), Is.EquivalentTo(new[]
                { "iron_spear", "iron_shuo", "horn_bow", "leather_armor", "leather_bracers", "war_banner", "war_manual_fragment" }));
            Assert.IsFalse(Item("cloth_armor").InShop, "starting gear cannot be bought back");
            Assert.IsFalse(Item("swift_whip").InShop, "the side-quest reward is not sold");
        }

        [Test]
        public void NewGameStartsWithCopperRationsAndStartingGear()
        {
            Assert.AreEqual(120, inv.Copper);
            Assert.AreEqual(2, inv.Rations);
            Assert.AreEqual("hunting_bow", inv.Worn("liu_bei", EquipSlot.Weapon).Id);
            Assert.AreEqual("cloth_armor", inv.Worn("liu_bei", EquipSlot.Armor).Id);
            Assert.IsNull(inv.Worn("liu_bei", EquipSlot.Accessory));
            Assert.AreEqual("cavalry_lance", inv.Worn("guan_yu", EquipSlot.Weapon).Id);
            Assert.AreEqual("ring_pommel_saber", inv.Worn("zhang_fei", EquipSlot.Weapon).Id);
            Assert.IsFalse(inv.Bag.Any());
        }

        [Test]
        public void StartingGearCountsInBattleStats()
        {
            // 1 级关羽：物攻 10 + 90/2 = 55，骑枪 +4 → 59；物防 5 + 80/3 = 31，布甲 +3 → 34.
            var s = BattleRules.Compute(guan, 1, inv.Bonus("guan_yu"), data.Troops, data.Config);
            Assert.AreEqual(59, s.PhysicalAttack);
            Assert.AreEqual(34, s.PhysicalDefense);
        }

        [Test]
        public void PreviewShowsTheDifferenceAndWhoCannotUseIt()
        {
            var shuo = Item("iron_shuo");
            var now = BattleRules.Compute(guan, 1, inv.Bonus("guan_yu"), data.Troops, data.Config);
            var then = BattleRules.Compute(guan, 1, inv.BonusWith("guan_yu", shuo), data.Troops, data.Config);
            Assert.AreEqual(5, then.PhysicalAttack - now.PhysicalAttack, "+9 replaces +4");
            Assert.IsTrue(shuo.Fits(guan.Troop));
            Assert.IsFalse(shuo.Fits(liu.Troop));
            Assert.IsFalse(shuo.Fits(zhang.Troop));
        }

        [Test]
        public void BuyingSpendsCopperAndRefusesWhenShort()
        {
            var poor = new Inventory(eco, copper: 30);
            Assert.AreEqual(TradeResult.NotEnoughCopper, poor.Buy(Item("iron_shuo")));
            Assert.AreEqual(30, poor.Copper, "copper never goes negative");

            var rich = new Inventory(eco, copper: 150);
            Assert.AreEqual(TradeResult.Ok, rich.Buy(Item("iron_shuo")));
            Assert.AreEqual(90, rich.Copper);
            Assert.AreEqual(1, rich.Count(Item("iron_shuo")));
            Assert.AreEqual(TradeResult.NotForSale, rich.Buy(Item("cloth_armor")));
        }

        [Test]
        public void TheSameKindCanBeBoughtTwice()
        {
            inv.Buy(Item("leather_armor"));
            inv.Buy(Item("leather_armor"));
            Assert.AreEqual(2, inv.Count(Item("leather_armor")));
            Assert.AreEqual(20, inv.Copper);
        }

        [Test]
        public void EquippedItemsMustBeTakenOffBeforeSelling()
        {
            var saber = Item("ring_pommel_saber");
            Assert.AreEqual(TradeResult.StillEquipped, inv.Sell(saber));
            Assert.AreEqual(120, inv.Copper);
            Assert.AreEqual(TradeResult.Ok, inv.Unequip("zhang_fei", EquipSlot.Weapon));
            Assert.AreEqual(TradeResult.Ok, inv.Sell(saber));
            Assert.AreEqual(135, inv.Copper, "starting weapon sells for its configured 15");
            Assert.IsNull(inv.Worn("zhang_fei", EquipSlot.Weapon), "an empty weapon slot is allowed");
        }

        [Test]
        public void ShopItemsSellForHalfAndClothArmourForTen()
        {
            var inv150 = new Inventory(eco, copper: 150);
            inv150.Buy(Item("iron_shuo"));
            inv150.Sell(Item("iron_shuo"));
            Assert.AreEqual(120, inv150.Copper, "bought 60, sold 30");
            inv150.Unequip("liu_bei", EquipSlot.Armor);
            inv150.Sell(Item("cloth_armor"));
            Assert.AreEqual(130, inv150.Copper);
        }

        [Test]
        public void RationsBuyForTenAndSellForFive()
        {
            Assert.AreEqual(TradeResult.Ok, inv.BuyRation());
            Assert.AreEqual(3, inv.Rations);
            Assert.AreEqual(110, inv.Copper);
            Assert.AreEqual(TradeResult.Ok, inv.SellRation());
            Assert.AreEqual(2, inv.Rations);
            Assert.AreEqual(115, inv.Copper);
            inv.SellRation(); inv.SellRation();
            Assert.AreEqual(TradeResult.NoRations, inv.SellRation());
        }

        [Test]
        public void WeaponsFollowTheTroopTypeAndSwapsReturnTheOldItem()
        {
            var shuo = Item("iron_shuo");
            inv.Buy(shuo);
            Assert.AreEqual(TradeResult.WrongTroop, inv.Equip(liu, shuo));
            Assert.AreEqual(TradeResult.Ok, inv.Equip(guan, shuo));
            Assert.AreEqual(shuo, inv.Worn("guan_yu", EquipSlot.Weapon));
            Assert.AreEqual(1, inv.Count(Item("cavalry_lance")), "the lance goes back to the bag");
            Assert.AreEqual(0, inv.Count(shuo));
        }

        [Test]
        public void AccessoryAndArmourBonusesAddUp()
        {
            var shop = new Inventory(eco, copper: 200);
            shop.Buy(Item("leather_armor")); shop.Buy(Item("leather_bracers"));
            shop.Equip(guan, Item("leather_armor")); shop.Equip(guan, Item("leather_bracers"));
            Assert.AreEqual(9, shop.Bonus("guan_yu").PhysicalDefense, "6 + 3");
            Assert.AreEqual(10, shop.Bonus("guan_yu").Troops);
        }

        [Test]
        public void CloneIsTheRetrySnapshot()
        {
            var before = inv.Clone();
            inv.SpendRations(1);
            inv.Buy(Item("leather_bracers"));
            Assert.AreEqual(2, before.Rations);
            Assert.AreEqual(120, before.Copper);
            Assert.AreEqual(0, before.Count(Item("leather_bracers")));
        }
    }
}
