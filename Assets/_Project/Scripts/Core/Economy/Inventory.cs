using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Battle;

namespace ThreeKingdoms.Core.Economy
{
    public enum TradeResult { Ok, NotEnoughCopper, NotForSale, NotInBag, StillEquipped, WrongTroop, NoRations }

    // The party's copper, rations, shared bag and each general's three slots (equipment-items GDD). Pure C#, so the
    // save system can serialise it and the pre-battle snapshot is a Clone. Every operation either succeeds or
    // changes nothing and says why.
    public sealed class Inventory
    {
        static readonly EquipSlot[] Slots = { EquipSlot.Weapon, EquipSlot.Armor, EquipSlot.Accessory };

        readonly Dictionary<string, int> bag = new Dictionary<string, int>();
        readonly Dictionary<(string general, EquipSlot slot), EquipmentSpec> worn = new Dictionary<(string, EquipSlot), EquipmentSpec>();

        public EconomyData Data { get; }
        public int Copper { get; private set; }
        public int Rations { get; private set; }

        public Inventory(EconomyData data, int? copper = null, int? rations = null)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Copper = copper ?? data.Config.StartCopper;
            Rations = rations ?? data.Config.StartRations;
            foreach (var kv in data.StartingGear)
                foreach (var item in kv.Value) worn[(kv.Key, item.Slot)] = item;
        }

        Inventory(Inventory other)
        {
            Data = other.Data; Copper = other.Copper; Rations = other.Rations;
            foreach (var kv in other.bag) bag[kv.Key] = kv.Value;
            foreach (var kv in other.worn) worn[kv.Key] = kv.Value;
        }

        public Inventory Clone() => new Inventory(this);

        // A saved inventory (ADR-0004). Unknown item ids or impossible slots throw: the save is treated as damaged.
        public static Inventory Restore(EconomyData data, int copper, int rations,
            IEnumerable<(string itemId, int count)> bagItems, IEnumerable<(string generalId, string itemId)> wornItems)
        {
            if (copper < 0 || rations < 0) throw new ArgumentException("铜钱或军粮为负。");
            var inv = new Inventory(data, copper, rations);
            inv.worn.Clear();
            foreach (var (id, count) in bagItems)
            {
                if (count <= 0) throw new ArgumentException($"背包数量无效：{id}");
                var item = data.Find(id);
                inv.bag[item.Id] = inv.Count(item) + count;
            }
            foreach (var (general, id) in wornItems)
            {
                var item = data.Find(id);
                if (inv.worn.ContainsKey((general, item.Slot))) throw new ArgumentException($"{general} 的{item.Slot}槽重复。");
                inv.worn[(general, item.Slot)] = item;
            }
            return inv;
        }

        // Every equipped item, for saving.
        public IEnumerable<(string generalId, EquipmentSpec item)> WornItems => worn.Select(kv => (kv.Key.general, kv.Value));

        // ---------- queries ----------

        public int Count(EquipmentSpec item) => item != null && bag.TryGetValue(item.Id, out int n) ? n : 0;

        // Unequipped items, in catalogue order, with how many of each.
        public IEnumerable<(EquipmentSpec item, int count)> Bag =>
            Data.Equipment.Where(e => Count(e) > 0).Select(e => (e, Count(e)));

        public EquipmentSpec Worn(string generalId, EquipSlot slot) => worn.TryGetValue((generalId, slot), out var e) ? e : null;

        public bool IsWornByAnyone(EquipmentSpec item) => worn.Values.Contains(item);

        public StatBonus Bonus(string generalId) => Sum(Slots.Select(s => Worn(generalId, s)));

        // The bonus this general would have with `item` in its slot (the shop's new-vs-old preview).
        public StatBonus BonusWith(string generalId, EquipmentSpec item) =>
            Sum(Slots.Select(s => s == item.Slot ? item : Worn(generalId, s)));

        public int SellPrice(EquipmentSpec item) => Data.SellPrice(item);

        // ---------- shop ----------

        public TradeResult CanBuy(EquipmentSpec item) =>
            !item.InShop ? TradeResult.NotForSale : Copper < item.BuyPrice ? TradeResult.NotEnoughCopper : TradeResult.Ok;

        public TradeResult Buy(EquipmentSpec item)
        {
            var r = CanBuy(item);
            if (r != TradeResult.Ok) return r;
            Copper -= item.BuyPrice;
            bag[item.Id] = Count(item) + 1;
            return TradeResult.Ok;
        }

        // Equipped items must be taken off first (GDD rule 15); the confirmation is the UI's job.
        public TradeResult CanSell(EquipmentSpec item) =>
            Count(item) > 0 ? TradeResult.Ok : IsWornByAnyone(item) ? TradeResult.StillEquipped : TradeResult.NotInBag;

        public TradeResult Sell(EquipmentSpec item)
        {
            var r = CanSell(item);
            if (r != TradeResult.Ok) return r;
            Take(item);
            Copper += SellPrice(item);
            return TradeResult.Ok;
        }

        public TradeResult CanBuyRation() => Copper < Data.Config.RationPrice ? TradeResult.NotEnoughCopper : TradeResult.Ok;

        public TradeResult BuyRation()
        {
            var r = CanBuyRation();
            if (r != TradeResult.Ok) return r;
            Copper -= Data.Config.RationPrice;
            Rations++;
            return TradeResult.Ok;
        }

        public TradeResult SellRation()
        {
            if (Rations <= 0) return TradeResult.NoRations;
            Rations--;
            Copper += Data.RationSellPrice;
            return TradeResult.Ok;
        }

        // ---------- outfitting ----------

        public TradeResult CanEquip(GeneralSpec general, EquipmentSpec item) =>
            !item.Fits(general.Troop) ? TradeResult.WrongTroop : Count(item) <= 0 ? TradeResult.NotInBag : TradeResult.Ok;

        // Puts a bag item into its slot; whatever was there goes back to the bag.
        public TradeResult Equip(GeneralSpec general, EquipmentSpec item)
        {
            var r = CanEquip(general, item);
            if (r != TradeResult.Ok) return r;
            Take(item);
            var old = Worn(general.Id, item.Slot);
            if (old != null) bag[old.Id] = Count(old) + 1;
            worn[(general.Id, item.Slot)] = item;
            return TradeResult.Ok;
        }

        public TradeResult Unequip(string generalId, EquipSlot slot)
        {
            var old = Worn(generalId, slot);
            if (old == null) return TradeResult.NotInBag;
            worn.Remove((generalId, slot));
            bag[old.Id] = Count(old) + 1;
            return TradeResult.Ok;
        }

        // ---------- battle ----------

        // Rations spent in a won battle; a lost battle restores the pre-battle state instead (GDD rule 20).
        public void SpendRations(int count) => Rations = Math.Max(0, Rations - Math.Max(0, count));

        public void AddCopper(int amount) => Copper += Math.Max(0, amount);

        // Copper paid out in town (alms, a bet); false and nothing taken when there is not enough.
        public bool SpendCopper(int amount)
        {
            if (amount < 0 || amount > Copper) return false;
            Copper -= amount;
            return true;
        }

        void Take(EquipmentSpec item)
        {
            if (--bag[item.Id] == 0) bag.Remove(item.Id);
        }

        static StatBonus Sum(IEnumerable<EquipmentSpec> items)
        {
            var b = new StatBonus();
            foreach (var e in items)
            {
                if (e == null) continue;
                var x = e.Bonus;
                b.Troops += x.Troops; b.Tech += x.Tech; b.Speed += x.Speed;
                b.PhysicalAttack += x.PhysicalAttack; b.PhysicalDefense += x.PhysicalDefense;
                b.StrategyAttack += x.StrategyAttack; b.StrategyDefense += x.StrategyDefense;
            }
            return b;
        }
    }
}
