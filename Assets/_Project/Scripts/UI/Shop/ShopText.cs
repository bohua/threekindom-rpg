using System.Collections.Generic;
using ThreeKingdoms.Core.Battle;
using ThreeKingdoms.Core.Economy;
using ThreeKingdoms.UI.Battle;

namespace ThreeKingdoms.UI.Shop
{
    // Every shop string in one place (simplified Chinese). ADR-0007 replaces this with the localisation tables.
    public static class ShopText
    {
        public const string Title = "武器装备店";
        public const string Smith = "铁匠";
        public const string Buy = "购买", Sell = "出售", Outfit = "更换装备", Leave = "离开";
        public const string Welcome = "要买点什么？刀枪弓甲都有，旧货也收。";
        public const string BuyHint = "选择要购买的物品。买下的装备放进背包，可在「更换装备」中换上。";
        public const string SellHint = "选择要出售的物品。正在装备的物品需先卸下。";
        public const string OutfitHint = "选择要更换装备的武将。";
        public const string LeaveHint = "离开商店。";
        public const string Ration = "军粮";
        public const string RationInfo = "道具・回复一支存活部队 30% 最大兵力，占一次行动，不能复活。";
        public const string Confirm = "确定出售", Cancel = "取消";
        public const string Unequip = "卸下";
        public const string Empty = "—";
        public const string NoChange = "数值不变";
        public const string Back = "[取消] 返回";
        public const string Return = "返回";
        public const string NothingToEquip = "背包里没有可换的装备。";

        public static string Copper(int n) => $"铜钱 {n}";
        public static string Rations(int n) => $"军粮 {n}";
        public static string Price(int n) => $"{n} 铜钱";
        public static string Count(int n) => $"×{n}";

        public static string Slot(EquipSlot s) => s switch
        {
            EquipSlot.Weapon => "武器",
            EquipSlot.Armor => "防具",
            _ => "饰品",
        };

        public static string Restriction(EquipmentSpec e) => e.Troop is TroopType t ? $"限{BattleText.Troop(t)}" : "通用";

        public static string Kind(EquipmentSpec e) => $"{Slot(e.Slot)}・{Restriction(e)}";

        public static string Bonus(StatBonus b)
        {
            var parts = new List<string>();
            void Add(string name, int v) { if (v != 0) parts.Add($"{name} {(v > 0 ? "+" : "")}{v}"); }
            Add("兵力", b.Troops); Add("技力", b.Tech);
            Add("物攻", b.PhysicalAttack); Add("物防", b.PhysicalDefense);
            Add("计攻", b.StrategyAttack); Add("计防", b.StrategyDefense); Add("速度", b.Speed);
            return parts.Count == 0 ? "无加值" : string.Join("　", parts);
        }

        // "物攻 59→64（+5）" for every stat the change touches.
        public static string Diff(UnitStats now, UnitStats then)
        {
            var parts = new List<string>();
            void Add(string name, int a, int b) { if (a != b) parts.Add($"{name} {a}→{b}（{(b > a ? "+" : "")}{b - a}）"); }
            Add("兵力", now.MaxTroops, then.MaxTroops); Add("技力", now.MaxTech, then.MaxTech);
            Add("物攻", now.PhysicalAttack, then.PhysicalAttack); Add("物防", now.PhysicalDefense, then.PhysicalDefense);
            Add("计攻", now.StrategyAttack, then.StrategyAttack); Add("计防", now.StrategyDefense, then.StrategyDefense);
            Add("速度", now.Speed, then.Speed);
            return parts.Count == 0 ? NoChange : string.Join("　", parts);
        }

        public static string Stats(UnitStats s) =>
            $"兵力 {s.MaxTroops}　技力 {s.MaxTech}\n物攻 {s.PhysicalAttack}　物防 {s.PhysicalDefense}\n计攻 {s.StrategyAttack}　计防 {s.StrategyDefense}　速度 {s.Speed}";

        public static string CannotEquip(EquipmentSpec e) => $"不可装备（{Restriction(e)}）";
        public static string Wearing(string general) => $"装备中：{general}";
        public static string BuyPrice(int n) => $"买价 {n} 铜钱";
        public static string SellPrice(int n) => $"卖价 {n} 铜钱";
        public static string ConfirmSell(string name, int price) => $"以 {price} 铜钱出售{name}？";

        public static string Bought(string name, int price) => $"买下了{name}（−{price} 铜钱）。";
        public static string Sold(string name, int price) => $"卖出了{name}（+{price} 铜钱）。";
        public static string Equipped(string general, string name) => $"{general}装备了{name}。";
        public static string Unequipped(string general, string name) => $"{general}卸下了{name}。";

        public static string Refused(TradeResult r, string name) => r switch
        {
            TradeResult.NotEnoughCopper => "铜钱不足，买不起。",
            TradeResult.StillEquipped => $"{name}正在装备中，需先在「更换装备」中卸下。",
            TradeResult.WrongTroop => $"兵种不符，不能装备{name}。",
            TradeResult.NoRations => "没有军粮可卖。",
            TradeResult.NotForSale => $"本店不卖{name}。",
            _ => $"背包里没有{name}。",
        };
    }
}
