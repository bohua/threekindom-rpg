using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Battle;

namespace ThreeKingdoms.Core.Economy
{
    public enum EquipSlot { Weapon, Armor, Accessory }

    // One kind of equipment (equipment-items GDD catalogue; "12 件" means 12 kinds, user 2026-10-19).
    public sealed class EquipmentSpec
    {
        public string Id { get; }
        public string DisplayName { get; }
        public EquipSlot Slot { get; }
        // Weapons are limited to one troop type (unit loadout, not the general's own weapon); null = anyone.
        public TroopType? Troop { get; }
        public StatBonus Bonus { get; }
        // 0 = not sold in the shop (starting gear, side-quest rewards); then GiftSellPrice is what the smith pays.
        public int BuyPrice { get; }
        public int GiftSellPrice { get; }

        public bool InShop => BuyPrice > 0;

        public EquipmentSpec(string id, string displayName, EquipSlot slot, TroopType? troop, StatBonus bonus, int buyPrice, int giftSellPrice)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("装备缺少 Id。");
            if (slot != EquipSlot.Weapon && troop != null) throw new ArgumentException($"{id}: 只有武器受兵种限制。");
            Id = id; DisplayName = displayName; Slot = slot; Troop = troop; Bonus = bonus;
            BuyPrice = Math.Max(0, buyPrice); GiftSellPrice = Math.Max(0, giftSellPrice);
        }

        public bool Fits(TroopType troop) => Troop == null || Troop == troop;

        public override string ToString() => Id;
    }

    public sealed class EconomyConfig
    {
        public int StartCopper { get; }
        public int StartRations { get; }
        public int RationPrice { get; }
        public int SellPercent { get; }   // 50: sell for half the buy price, rounded down

        public EconomyConfig(int startCopper, int startRations, int rationPrice, int sellPercent)
        {
            StartCopper = startCopper; StartRations = startRations; RationPrice = rationPrice; SellPercent = sellPercent;
        }

        public int SellPriceFor(int buyPrice) => buyPrice * SellPercent / 100;
    }

    // Immutable economy rule data, built from ScriptableObjects by ThreeKingdoms.Data or directly in tests.
    public sealed class EconomyData
    {
        public EconomyConfig Config { get; }
        public IReadOnlyList<EquipmentSpec> Equipment { get; }
        // What each of our generals wears when the game starts (Guan Yu and Zhang Fei bring theirs when they join).
        public IReadOnlyDictionary<string, IReadOnlyList<EquipmentSpec>> StartingGear { get; }

        public EconomyData(EconomyConfig config, IEnumerable<EquipmentSpec> equipment, IDictionary<string, IReadOnlyList<EquipmentSpec>> startingGear)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            Equipment = equipment.ToArray();
            var dup = Equipment.GroupBy(e => e.Id).FirstOrDefault(g => g.Count() > 1);
            if (dup != null) throw new ArgumentException($"装备 Id「{dup.Key}」重复。");
            StartingGear = new Dictionary<string, IReadOnlyList<EquipmentSpec>>(startingGear ?? new Dictionary<string, IReadOnlyList<EquipmentSpec>>());
        }

        public IEnumerable<EquipmentSpec> ShopStock => Equipment.Where(e => e.InShop);

        public EquipmentSpec Find(string id) =>
            Equipment.FirstOrDefault(e => e.Id == id) ?? throw new KeyNotFoundException($"没有装备 {id}。");

        public int SellPrice(EquipmentSpec item) => item.InShop ? Config.SellPriceFor(item.BuyPrice) : item.GiftSellPrice;
        public int RationSellPrice => Config.SellPriceFor(Config.RationPrice);
    }
}
