using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Economy;
using ThreeKingdoms.Data.Battle;
using UnityEngine;

namespace ThreeKingdoms.Data.Economy
{
    [Serializable]
    public struct StartingGearEntry
    {
        public GeneralDefinition General;
        public EquipmentDefinition[] Items;
    }

    // Equipment catalogue, prices and starting gear (equipment-items GDD). ToRuntime() validates and converts once.
    [CreateAssetMenu(menuName = "ThreeKingdoms/Economy/Economy Catalog")]
    public sealed class EconomyCatalog : ScriptableObject
    {
        [Min(0)] public int StartCopper = 120;
        [Min(0)] public int StartRations = 2;
        [Min(0)] public int RationPrice = 10;
        [Range(0, 100)] public int SellPercent = 50;
        public EquipmentDefinition[] Equipment = Array.Empty<EquipmentDefinition>();
        public StartingGearEntry[] StartingGear = Array.Empty<StartingGearEntry>();

        public EconomyData ToRuntime()
        {
            var specs = new Dictionary<EquipmentDefinition, EquipmentSpec>();
            foreach (var def in Equipment)
            {
                if (!def) throw new InvalidOperationException($"{name}: 装备引用为空。");
                specs[def] = def.ToRuntime();
            }
            var gear = new Dictionary<string, IReadOnlyList<EquipmentSpec>>();
            foreach (var entry in StartingGear)
            {
                if (!entry.General) throw new InvalidOperationException($"{name}: 初始装备缺少武将。");
                var items = entry.Items.Select(i =>
                    i && specs.TryGetValue(i, out var s) ? s : throw new InvalidOperationException($"{name}: {entry.General.DisplayName} 的初始装备不在目录中。")).ToArray();
                if (items.GroupBy(i => i.Slot).Any(g => g.Count() > 1)) throw new InvalidOperationException($"{name}: {entry.General.DisplayName} 同一槽有两件初始装备。");
                foreach (var w in items.Where(i => i.Troop != null && i.Troop != entry.General.Troop))
                    throw new InvalidOperationException($"{name}: {entry.General.DisplayName} 不能装备 {w.DisplayName}。");
                gear[entry.General.Id] = items;
            }
            return new EconomyData(new EconomyConfig(StartCopper, StartRations, RationPrice, SellPercent), Equipment.Select(e => specs[e]), gear);
        }
    }
}
