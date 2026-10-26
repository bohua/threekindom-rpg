using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Battle;
using ThreeKingdoms.Core.Economy;
using ThreeKingdoms.UI.Shop;
using UnityEngine;

namespace ThreeKingdoms.UI.Menu
{
    // 物品 (01_ITEMS_MENU), 装备 (02_EQUIPMENT_MENU) and 技能 (03_SKILLS_MENU).
    public sealed partial class MainMenuHud
    {
        // ================= 物品 =================

        public readonly struct ItemRow
        {
            public readonly EquipmentSpec Item;   // null: 军粮
            public readonly int Count, Worn;
            public ItemRow(EquipmentSpec item, int count, int worn) { Item = item; Count = count; Worn = worn; }
            public bool IsRation => Item == null;
            public string Name => Item?.DisplayName ?? ShopText.Ration;
            public string Icon => Item == null ? "item_ration" : "item_" + Item.Id;
        }

        int itemIndex, itemFilter, itemScroll;
        static readonly string[] Filters = { "全部", "消耗品", "装备" };

        public int ItemFilter { get => itemFilter; set { itemFilter = Wrap(value, 3); itemIndex = 0; } }

        // Everything carried: rations, then each kind of gear with how many are owned (in the bag or worn).
        public List<ItemRow> ItemRows()
        {
            var rows = new List<ItemRow>();
            if (itemFilter != 2 && Inv.Rations > 0) rows.Add(new ItemRow(null, Inv.Rations, 0));
            if (itemFilter == 1) return rows;
            var worn = Inv.WornItems.GroupBy(w => w.item).ToDictionary(g => g.Key, g => g.Count());
            var kinds = Inv.Bag.Select(b => b.item).Concat(worn.Keys).Distinct()
                .OrderBy(i => i.Slot).ThenBy(i => Inv.Data.Equipment.ToList().IndexOf(i));
            foreach (var item in kinds)
            {
                int w = worn.TryGetValue(item, out int n) ? n : 0;
                rows.Add(new ItemRow(item, Inv.Count(item) + w, w));
            }
            return rows;
        }

        void ConfirmItem()
        {
            var row = ItemRows().ElementAtOrDefault(itemIndex);
            if (row.IsRation) { Message = row.Count > 0 ? MenuText.TroopsFull : ""; return; }
            // Gear is changed on 装备: jump there with that slot open for the chosen general.
            Show(Tab.Equip);
            slotIndex = (int)row.Item.Slot;
            OpenCandidates();
            candidateIndex = Mathf.Max(0, Candidates().FindIndex(c => c.item == row.Item));
        }

        string CategoryIcon(ItemRow r) => r.IsRation ? "cat_all" : r.Item.Slot == EquipSlot.Weapon ? "cat_equipment" : r.Item.Slot == EquipSlot.Armor ? "stat_pdef" : "stat_command";

        void ItemPicture(float x, float y, float s, ItemRow r)
        {
            if (paint.Icon(x, y, s, s, r.Icon)) return;
            paint.Disc(paint.P(x + s / 2, y + s / 2), s * 0.38f * paint.K, new Color(0.25f, 0.33f, 0.42f, 0.8f));
            paint.Text(x, y, s, s, r.Name.Substring(0, 1), s * 0.42f, MenuPaint.Ivory, TextAnchor.MiddleCenter, true);
        }

        void DrawItems()
        {
            var p = paint;
            p.PanelBox(468, 158, 542, 690, "随行物资");
            for (int i = 0; i < 3; i++)
            {
                bool sel = itemFilter == i;
                var r = p.R(484 + i * 174, 222, 160, 56);
                p.Frame(r, sel ? "filter_chip_selected" : "filter_chip", sel ? MenuPaint.Cyan : (Color?)null, sel ? new Color(MenuPaint.SelectBg.r, MenuPaint.SelectBg.g, MenuPaint.SelectBg.b, 0.9f) : (Color?)null);
                string icon = i == 0 ? "cat_all" : i == 1 ? "cat_consumable" : "cat_equipment";
                p.Icon(484 + i * 174 + 26, 234, 32, 32, icon);
                p.Text(484 + i * 174 + 66, 222, 90, 56, Filters[i], 21, MenuPaint.Ivory);
                int f = i;
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) ItemFilter = f;
            }
            var rows = ItemRows();
            itemIndex = Mathf.Clamp(itemIndex, 0, Mathf.Max(0, rows.Count - 1));
            const int visible = 5;
            itemScroll = Mathf.Clamp(itemScroll, Mathf.Max(0, itemIndex - visible + 1), Mathf.Min(itemIndex, Mathf.Max(0, rows.Count - visible)));
            for (int k = 0; k < visible && itemScroll + k < rows.Count; k++)
            {
                int i = itemScroll + k; var r = rows[i]; bool sel = i == itemIndex;
                float y = 302 + k * 103;
                Row(486, y, 486, 92, sel);
                if (sel && column == 1) Pointer(470, y + 20);
                ItemPicture(500, y + 4, 84, r);
                p.Text(616, y, 140, 92, r.Name, 26, MenuPaint.Ivory, TextAnchor.MiddleLeft, true);
                p.Icon(726, y + 30, 32, 32, CategoryIcon(r), tint: r.IsRation ? (Color?)null : MenuPaint.Silver);
                p.Text(880, y, 80, 92, $"× <size={p.F(28)}>{r.Count}</size>", 22, MenuPaint.Ivory, TextAnchor.MiddleRight);
                if (GUI.Button(p.R(486, y, 486, 92), GUIContent.none, GUIStyle.none)) { itemIndex = i; column = 1; }
            }
            if (rows.Count == 0) p.Text(486, 320, 486, 60, "没有可显示的物品。", 22, MenuPaint.Mist, TextAnchor.MiddleCenter);
            ScrollBar(986, 306, 830, itemScroll, visible, rows.Count);

            // Detail.
            p.PanelBox(1030, 158, 612, 690, "物品详情");
            if (rows.Count == 0) return;
            var it = rows[itemIndex];
            p.Frame(1046, 228, 260, 270, "art_frame", SilverA(0.6f), new Color(0.05f, 0.09f, 0.16f, 0.95f));
            ItemPicture(1066, 252, 220, it);
            p.Text(1333, 236, 290, 52, it.Name, 36, MenuPaint.Ivory, TextAnchor.MiddleLeft, true);
            p.HLine(1333, 290, 290, SilverA(0.35f));
            p.Icon(1336, 304, 30, 30, CategoryIcon(it));
            p.Text(1382, 298, 240, 40, it.IsRation ? "消耗品" : ShopText.Kind(it.Item), 21, MenuPaint.Ivory);
            p.Frame(1333, 346, 290, 46, "subpanel", SilverA(0.4f), new Color(0.02f, 0.05f, 0.10f, 0.7f));
            p.Text(1352, 346, 120, 46, "持有数", 20, MenuPaint.Mist);
            p.Text(1500, 346, 110, 46, it.Count.ToString(), 30, MenuPaint.Gold, TextAnchor.MiddleRight);
            if (it.IsRation)
            {
                p.Text(1333, 404, 300, 76, $"回复一支友军最大兵力的 <color=#D6BA78>30%</color>。\n仅对存活单位有效。", 20, MenuPaint.Ivory, TextAnchor.UpperLeft, false, true);
                p.HLine(1333, 486, 290, SilverA(0.35f));
                p.Text(1333, 494, 300, 36, "战斗中使用将消耗一次行动。", 19, MenuPaint.Mist);
            }
            else
            {
                p.Text(1333, 404, 300, 76, ShopText.Bonus(it.Item.Bonus) + (it.Worn > 0 ? $"\n其中 {it.Worn} 件正在装备。" : ""), 20, MenuPaint.Ivory, TextAnchor.UpperLeft, false, true);
                p.HLine(1333, 486, 290, SilverA(0.35f));
                p.Text(1333, 494, 300, 36, "在「装备」页为武将更换。", 19, MenuPaint.Mist);
            }

            // 使用对象: the general chosen in the party column.
            p.SubBox(1044, 544, 582, 194, "使用对象");
            var g = General;
            if (g != null)
            {
                var s = Progress.Stats(g, Inv.Bonus(g.Id));
                var bust = Bust(g.Id);
                p.Frame(1060, 606, 116, 116, "art_frame", SilverA(0.5f));
                if (bust) GUI.DrawTexture(p.R(1063, 609, 110, 110), bust, ScaleMode.ScaleAndCrop);
                p.Text(1198, 606, 200, 40, g.DisplayName, 24, MenuPaint.Ivory, TextAnchor.MiddleLeft, true);
                p.Bar(1198, 650, 290, 16, 1f, MenuPaint.Jade);
                p.Text(1500, 640, 120, 36, $"{s.MaxTroops} / {s.MaxTroops}", 20, MenuPaint.Ivory);
                p.Text(1198, 678, 420, 38, it.IsRation ? MenuText.TroopsFullLong : ShopText.Restriction(it.Item) + (it.Item.Fits(g.Troop) ? $"，{g.DisplayName}可装备。" : $"，{g.DisplayName}不能装备。"), 18, MenuPaint.Mist);
            }
            if (it.IsRation)
            {
                Button(1054, 754, 274, 68, "使用", ButtonKind.Disabled);
                Info(1350, 770, "该角色兵力已满");
            }
            else
            {
                Button(1054, 754, 274, 68, "装备", ButtonKind.Primary, ConfirmItem);
                Info(1350, 770, "前往「装备」页更换");
            }
        }

        void ScrollBar(float x, float y0, float y1, int first, int visible, int total)
        {
            var p = paint;
            var track = p.R(x, y0, 10, y1 - y0);
            p.Frame(track, "scroll_track", SilverA(0.3f), new Color(0.02f, 0.04f, 0.08f, 0.6f));
            float frac = total <= visible ? 1f : visible / (float)total, pos = total <= visible ? 0f : first / (float)(total - visible);
            float th = Mathf.Max(20, track.height * frac), ty = track.y + (track.height - th) * pos;
            var thumb = new Rect(track.x + 1, ty, track.width - 2, th);
            if (!p.Icon(thumb, "scroll_thumb", ScaleMode.StretchToFill)) p.Fill(thumb, new Color(0.55f, 0.70f, 0.85f, 0.85f));
            if (p.Has("scroll_arrow_up"))
            {
                p.Icon(x - 4, y0 - 16, 18, 12, "scroll_arrow_up");
                var m = GUI.matrix; var a = p.R(x - 4, y1 + 4, 18, 12);
                GUI.matrix = m * Matrix4x4.TRS(a.center, Quaternion.identity, new Vector3(1, -1, 1)) * Matrix4x4.TRS(-(Vector3)a.center, Quaternion.identity, Vector3.one);
                p.Icon(a, "scroll_arrow_up"); GUI.matrix = m;
            }
        }

        // ================= 装备 =================

        bool picking;
        int slotIndex, candidateIndex;

        public bool Picking => picking;
        public EquipSlot Slot => (EquipSlot)slotIndex;

        // What can go in the chosen slot: bag items of that kind first, then what is worn now (marked).
        public List<(EquipmentSpec item, bool worn)> Candidates()
        {
            var g = General;
            if (g == null) return new List<(EquipmentSpec, bool)>();
            var slot = (EquipSlot)slotIndex;
            var list = Inv.Bag.Select(b => b.item).Where(i => i.Slot == slot).Distinct().Select(i => (i, false)).ToList();
            var worn = Inv.Worn(g.Id, slot);
            if (worn != null) list.Add((worn, true));
            return list;
        }

        public void PickFor(EquipSlot slot) { slotIndex = (int)slot; OpenCandidates(); }

        void OpenCandidates()
        {
            if (Candidates().Count == 0) { Message = ShopText.NothingToEquip; return; }
            picking = true; candidateIndex = 0; column = 2;
        }

        void ConfirmEquip()
        {
            if (!picking) { OpenCandidates(); return; }
            var (item, worn) = Candidates().ElementAtOrDefault(candidateIndex);
            if (item == null) return;
            if (worn) { Unequip(item.Slot); return; }
            var g = General;
            var result = Inv.Equip(g, item);
            Message = result == TradeResult.Ok ? ShopText.Equipped(g.DisplayName, item.DisplayName) : ShopText.CannotEquip(item);
            if (result == TradeResult.Ok) { picking = false; column = 1; }
        }

        void Unequip(EquipSlot slot)
        {
            var g = General;
            var worn = Inv.Worn(g.Id, slot);
            if (worn == null) return;
            if (Inv.Unequip(g.Id, slot) == TradeResult.Ok) Message = ShopText.Unequipped(g.DisplayName, worn.DisplayName);
            picking = false; column = 1;
        }

        static string SlotIcon(EquipSlot s, EquipmentSpec worn) => worn != null ? "item_" + worn.Id : s == EquipSlot.Accessory ? "slot_empty_ring" : null;

        void GearPicture(float x, float y, float s, EquipmentSpec item, EquipSlot slot)
        {
            if (item != null) { ItemPicture(x, y, s, new ItemRow(item, 1, 0)); return; }
            if (paint.Icon(x, y, s, s, "slot_empty_ring")) return;
            paint.Disc(paint.P(x + s / 2, y + s / 2), s * 0.34f * paint.K, MenuPaint.Silver);
            paint.Disc(paint.P(x + s / 2, y + s / 2), s * 0.29f * paint.K, new Color(0.04f, 0.07f, 0.13f));
        }

        void DrawEquip()
        {
            var p = paint;
            var g = General;
            if (g == null) return;
            p.PanelBox(466, 158, 498, 690, "当前装备");
            // Who: mini bust, name, troop.
            Row(486, 226, 458, 70, false);
            var bust = Bust(g.Id);
            if (bust) GUI.DrawTexture(p.R(490, 229, 112, 64), bust, ScaleMode.ScaleAndCrop);
            p.Text(626, 226, 120, 70, g.DisplayName, 30, MenuPaint.Ivory, TextAnchor.MiddleLeft, true);
            p.Text(700, 226, 20, 70, "·", 24, MenuPaint.Mist, TextAnchor.MiddleCenter);
            TroopMark(722, 244, 32, g.Troop);
            p.Text(764, 226, 120, 70, BattleTroop(g.Troop), 21, MenuPaint.Ivory);

            float[] ys = { 310, 424, 538 };
            for (int i = 0; i < 3; i++)
            {
                var slot = (EquipSlot)i; var worn = Inv.Worn(g.Id, slot);
                bool sel = i == slotIndex;
                Row(486, ys[i], 458, 96, sel);
                if (sel && column == 1) Pointer(470, ys[i] + 22);
                GearPicture(508, ys[i] + 8, 80, worn, slot);
                p.Text(628, ys[i] + 12, 200, 30, ShopText.Slot(slot), 20, MenuPaint.Gold);
                p.Text(628, ys[i] + 44, 280, 40, worn != null ? worn.DisplayName : "未装备", 28, worn != null ? MenuPaint.Ivory : MenuPaint.Mist, TextAnchor.MiddleLeft, true);
                int idx = i;
                if (GUI.Button(p.R(486, ys[i], 458, 96), GUIContent.none, GUIStyle.none)) { if (slotIndex == idx && column == 1) OpenCandidates(); else { slotIndex = idx; picking = false; column = 1; } }
            }

            var now = Progress.Stats(g, Inv.Bonus(g.Id));
            p.SubBox(478, 652, 474, 188, "当前属性");
            StatCell(512, 708, "stat_patk", "物攻", now.PhysicalAttack, 690);
            StatCell(512, 750, "stat_pdef", "物防", now.PhysicalDefense, 690);
            StatCell(512, 792, "stat_satk", "计攻", now.StrategyAttack, 690);
            StatCell(732, 708, "stat_sdef", "计防", now.StrategyDefense, 916);
            StatCell(732, 750, "stat_speed", "速度", now.Speed, 916);

            // Candidates for the slot and the comparison.
            var slotNow = (EquipSlot)slotIndex;
            p.PanelBox(984, 158, 658, 690, "替换" + ShopText.Slot(slotNow));
            string restrict = slotNow == EquipSlot.Weapon ? $"{BattleTroop(g.Troop)}可用" : "通用";
            p.Icon(1488, 172, 34, 34, slotNow == EquipSlot.Weapon ? "cat_equipment" : "stat_pdef");
            p.Text(1528, 168, 100, 42, restrict, 20, MenuPaint.Ivory);
            var cands = Candidates();
            candidateIndex = Mathf.Clamp(candidateIndex, 0, Mathf.Max(0, cands.Count - 1));
            for (int k = 0; k < cands.Count && k < 2; k++)
            {
                int i = cands.Count > 2 ? Mathf.Clamp(candidateIndex - 1, 0, cands.Count - 2) + k : k;
                var (item, worn) = cands[i];
                bool sel = picking && i == candidateIndex;
                float y = 230 + k * 86;
                Row(998, y, 626, 76, sel);
                if (sel) Pointer(982, y + 12);
                ItemPicture(1022, y + 6, 64, new ItemRow(item, 1, 0));
                p.Text(1120, y, 160, 76, item.DisplayName, 26, MenuPaint.Ivory, TextAnchor.MiddleLeft, true);
                if (worn) Tag(1478, y + 20, "当前装备");
                else
                {
                    var delta = Delta(now, Progress.Stats(g, Inv.BonusWith(g.Id, item)));
                    if (!item.Fits(g.Troop)) p.Text(1280, y, 300, 76, $"限{BattleTroop(item.Troop.Value)}", 22, MenuPaint.Red);
                    else if (delta.name != null)
                    {
                        if (!p.Icon(1270, y + 22, 30, 32, delta.value > 0 ? "icon_up" : null))
                            p.Text(1270, y + 18, 30, 40, delta.value > 0 ? "▲" : "▼", 22, delta.value > 0 ? MenuPaint.Green : MenuPaint.Red, TextAnchor.MiddleCenter);
                        p.Text(1318, y, 260, 76, $"{delta.name} {(delta.value > 0 ? "+" : "")}{delta.value}", 24, MenuPaint.Ivory);
                    }
                }
                int idx = i;
                if (GUI.Button(p.R(998, y, 626, 76), GUIContent.none, GUIStyle.none)) { if (picking && candidateIndex == idx) ConfirmEquip(); else { picking = true; candidateIndex = idx; column = 2; } }
            }
            if (cands.Count > 2) p.Text(1500, 396, 120, 20, $"{candidateIndex + 1}/{cands.Count}", 16, MenuPaint.Mist, TextAnchor.MiddleRight);
            if (cands.Count == 0) p.Text(998, 230, 626, 76, ShopText.NothingToEquip, 22, MenuPaint.Mist, TextAnchor.MiddleCenter);

            var worn0 = Inv.Worn(g.Id, slotNow);
            var preview = cands.Count == 0 ? ((EquipmentSpec)null, false) :picking ? cands[candidateIndex] : cands.FirstOrDefault(c => !c.worn).item != null ? cands.First(c => !c.worn) : cands[0];
            p.HLine(998, 410, 626, SilverA(0.3f));
            CompareBox(998, 424, "当前装备", worn0, slotNow);
            if (!p.Icon(1152, 520, 28, 40, "icon_chevron_right")) p.Text(1152, 520, 28, 40, "›", 40, MenuPaint.Cyan, TextAnchor.MiddleCenter, true);
            CompareBox(1186, 424, "新装备预览", preview.Item1 != null && !preview.Item2 ? preview.Item1 : null, slotNow);

            var then = preview.Item1 == null ? now : preview.Item2 ? Progress.Stats(g, BonusWithout(g.Id, slotNow)) : Progress.Stats(g, Inv.BonusWith(g.Id, preview.Item1));
            string desc = preview.Item1 == null ? ShopText.NothingToEquip : preview.Item2 ? $"卸下{preview.Item1.DisplayName}。" : ShopText.Kind(preview.Item1) + "\n" + ShopText.Bonus(preview.Item1.Bonus);
            p.Text(1362, 430, 270, 64, desc, 19, MenuPaint.Ivory, TextAnchor.UpperLeft, false, true);
            p.HLine(1362, 492, 262, SilverA(0.35f));
            p.Text(1362, 496, 262, 38, "装备后变化", 22, MenuPaint.Ivory, TextAnchor.MiddleLeft, true);
            var rows = new List<(string icon, string name, int a, int b)>();
            if (now.MaxTroops != then.MaxTroops) rows.Add(("stat_command", "兵力", now.MaxTroops, then.MaxTroops));
            if (now.MaxTech != then.MaxTech) rows.Add(("stat_intellect", "技力", now.MaxTech, then.MaxTech));
            rows.Add(("stat_patk", "物攻", now.PhysicalAttack, then.PhysicalAttack));
            rows.Add(("stat_pdef", "物防", now.PhysicalDefense, then.PhysicalDefense));
            rows.Add(("stat_satk", "计攻", now.StrategyAttack, then.StrategyAttack));
            rows.Add(("stat_sdef", "计防", now.StrategyDefense, then.StrategyDefense));
            rows.Add(("stat_speed", "速度", now.Speed, then.Speed));
            float pitch = rows.Count > 5 ? 28 : 37;
            for (int i = 0; i < rows.Count; i++)
            {
                var (icon, name, a, b) = rows[i]; float y = 532 + i * pitch;
                p.Icon(1364, y + 4, 26, 26, icon);
                p.Text(1396, y, 60, 34, name, 19, MenuPaint.Ivory);
                p.Text(1462, y, 100, 34, $"{a}  →  <color={(b > a ? "#5CDB6B" : b < a ? "#D86B70" : "#F4F1E8")}>{b}</color>", 20, MenuPaint.Ivory);
                p.Text(1560, y, 64, 34, b == a ? "-" : $"{(b > a ? "+" : "")}{b - a}", 20, b > a ? MenuPaint.Green : b < a ? MenuPaint.Red : MenuPaint.Mist, TextAnchor.MiddleRight);
                if (i < rows.Count - 1) p.HLine(1362, y + pitch - 2, 262, SilverA(0.15f));
            }

            bool canAct = preview.Item1 != null && (preview.Item2 || preview.Item1.Fits(g.Troop));
            string act = preview.Item2 ? "卸下" : "装备";
            Button(1074, 756, 252, 64, act, canAct ? ButtonKind.Primary : ButtonKind.Disabled, () =>
            {
                if (!picking) { picking = true; column = 2; candidateIndex = Mathf.Max(0, cands.IndexOf(preview)); }
                ConfirmEquip();
            });
            Button(1348, 756, 218, 64, "返回", ButtonKind.Normal, Back);
        }

        StatBonus BonusWithout(string id, EquipSlot slot)
        {
            var b = new StatBonus();
            foreach (EquipSlot s in System.Enum.GetValues(typeof(EquipSlot)))
            {
                if (s == slot) continue;
                var w = Inv.Worn(id, s); if (w == null) continue;
                b.Troops += w.Bonus.Troops; b.Tech += w.Bonus.Tech; b.PhysicalAttack += w.Bonus.PhysicalAttack; b.PhysicalDefense += w.Bonus.PhysicalDefense;
                b.StrategyAttack += w.Bonus.StrategyAttack; b.StrategyDefense += w.Bonus.StrategyDefense; b.Speed += w.Bonus.Speed;
            }
            return b;
        }

        // The biggest single change an item makes, for the one-line summary in the candidate row (物攻 +4).
        static (string name, int value) Delta(UnitStats a, UnitStats b)
        {
            var all = new[] { ("物攻", b.PhysicalAttack - a.PhysicalAttack), ("物防", b.PhysicalDefense - a.PhysicalDefense), ("兵力", b.MaxTroops - a.MaxTroops),
                ("技力", b.MaxTech - a.MaxTech), ("计攻", b.StrategyAttack - a.StrategyAttack), ("计防", b.StrategyDefense - a.StrategyDefense), ("速度", b.Speed - a.Speed) };
            var best = all.OrderByDescending(x => System.Math.Abs(x.Item2)).First();
            return best.Item2 == 0 ? (null, 0) : best;
        }

        void Tag(float x, float y, string text)
        {
            paint.Fill(x, y, 130, 38, new Color(0.10f, 0.16f, 0.24f, 0.95f));
            paint.Text(x, y, 130, 38, text, 19, MenuPaint.Ivory, TextAnchor.MiddleCenter);
        }

        void CompareBox(float x, float y, string title, EquipmentSpec item, EquipSlot slot)
        {
            var p = paint;
            p.SubBox(x, y, 150, 234);
            p.Text(x, y + 4, 150, 34, title, 19, MenuPaint.Ivory, TextAnchor.MiddleCenter);
            p.Frame(x + 8, y + 42, 134, 140, "art_frame", SilverA(0.35f), new Color(0.03f, 0.06f, 0.12f, 0.9f));
            GearPicture(x + 18, y + 54, 114, item, slot);
            p.Text(x, y + 190, 150, 36, item != null ? item.DisplayName : "未装备", 22, item != null ? MenuPaint.Ivory : MenuPaint.Mist, TextAnchor.MiddleCenter);
        }

        void StatCell(float x, float y, string icon, string name, int value, float valueRight)
        {
            var p = paint;
            if (!p.Icon(x, y + 6, 30, 30, icon)) p.Text(x, y, 30, 42, "◆", 16, MenuPaint.Mist, TextAnchor.MiddleCenter);
            p.Text(x + 46, y, 80, 42, name, 21, MenuPaint.Ivory);
            p.Text(valueRight - 80, y, 80, 42, value.ToString(), 26, MenuPaint.Gold, TextAnchor.MiddleRight);
            p.HLine(x - 6, y + 40, valueRight - x + 12, SilverA(0.15f));
        }

        // ================= 技能 =================

        int skillIndex;

        public IReadOnlyList<SkillUnlock> SkillRows() => General?.Skills ?? new SkillUnlock[0];

        public bool Learned(SkillUnlock u) => General != null && u.Level <= Progress.Level(General);

        // Why 使用 is greyed out here (skills are used in battle; 整队 also heals out of battle, but troops are always
        // full in town because every battle ends with a full recovery).
        public string SkillUseBlocked(SkillUnlock u)
        {
            if (u.Skill == null) return null;
            if (!Learned(u)) return $"Lv.{u.Level} 解锁";
            return u.Skill.TargetsAllies && u.Skill.HealPercent > 0 ? MenuText.TroopsFullSkill : MenuText.BattleOnly;
        }

        static string SkillArt(string id) => id switch
        {
            "regroup" => "skill_zhengdui", "fire_arrows" => "skill_huoshi", "charge" => "skill_tuji",
            "break_formation" => "skill_pozhen", "sweep" => "skill_hengsao", "intimidate" => "skill_weishe", _ => "skill_" + id,
        };

        static string SkillKind(SkillSpec s) => s.TargetsAllies ? "支援" : s.Damage != DamageKind.None ? "攻击" : "妨害";
        static string SkillKindIcon(SkillSpec s) => s.TargetsAllies ? "cat_all" : s.Damage != DamageKind.None ? "cat_equipment" : "stat_command";
        static string SkillRange(SkillSpec s) => s.Target switch { SkillTarget.SingleAlly => "我方单体", SkillTarget.AllEnemies => "敌方全体", _ => "敌方单体" };

        void SkillPicture(float x, float y, float w, float h, SkillSpec s)
        {
            var r = paint.R(x, y, w, h);
            if (paint.Icon(r, SkillArt(s.Id), ScaleMode.ScaleAndCrop)) return;
            paint.Fill(r, s.TargetsAllies ? new Color(0.45f, 0.12f, 0.10f) : new Color(0.18f, 0.12f, 0.08f));
            GUI.Label(r, s.DisplayName.Substring(0, 1), paint.St(h * 0.5f, MenuPaint.Ivory, TextAnchor.MiddleCenter, true));
        }

        void TechLeaf(float x, float y)
        {
            if (!paint.Icon(x, y, 28, 28, "cat_all")) paint.Text(x, y, 28, 28, "❧", 20, MenuPaint.Jade, TextAnchor.MiddleCenter);
        }

        void DrawSkills()
        {
            var p = paint;
            var g = General;
            if (g == null) return;
            int level = Progress.Level(g);
            p.PanelBox(468, 158, 558, 690, "武将技能", $"{g.DisplayName} · <color=#D6BA78>Lv.{level}</color>");
            var rows = SkillRows();
            skillIndex = Mathf.Clamp(skillIndex, 0, Mathf.Max(0, rows.Count - 1));
            for (int i = 0; i < rows.Count && i < 2; i++)
            {
                var u = rows[i]; bool sel = i == skillIndex, learned = u.Level <= level;
                float y = 226 + i * 172, h = 156;
                Row(482, y, 532, h, sel);
                if (sel && column == 1) Pointer(466, y + 50);
                var tint = GUI.color; if (!learned) GUI.color = new Color(1, 1, 1, 0.55f);
                p.Frame(494, y + 8, 142, 140, "art_frame", SilverA(0.5f));
                SkillPicture(498, y + 12, 134, 132, u.Skill);
                GUI.color = tint;
                p.Text(660, y + 10, 220, 50, u.Skill.DisplayName, 32, learned ? MenuPaint.Ivory : MenuPaint.Mist, TextAnchor.MiddleLeft, true);
                TechLeaf(662, y + 70);
                p.Text(708, y + 62, 80, 42, "技力", 20, MenuPaint.Ivory);
                p.Text(768, y + 60, 60, 44, u.Skill.TechCost.ToString(), 28, MenuPaint.Ivory);
                p.Text(660, y + 104, 290, 40, MenuText.SkillBrief(u.Skill), 20, MenuPaint.Ivory);
                if (learned)
                {
                    if (!p.Icon(956, y + 58, 38, 38, "checkbox_checked")) { p.Outline(p.R(956, y + 58, 38, 38), MenuPaint.Ivory, Mathf.Max(1, 1.5f * p.K)); p.Text(956, y + 58, 38, 38, "✓", 26, MenuPaint.Ivory, TextAnchor.MiddleCenter, true); }
                }
                else
                {
                    if (!p.Icon(934, y + 40, 34, 38, "icon_lock")) p.Text(934, y + 40, 34, 38, "🔒", 22, MenuPaint.Mist, TextAnchor.MiddleCenter);
                    p.Text(886, y + 82, 120, 40, $"Lv.{u.Level} 解锁", 21, MenuPaint.Mist, TextAnchor.MiddleCenter);
                }
                int idx = i;
                if (GUI.Button(p.R(482, y, 532, h), GUIContent.none, GUIStyle.none)) { skillIndex = idx; column = 1; }
            }

            // 习得进度: the unlock levels and the current level on one track.
            p.SubBox(478, 592, 540, 250, "习得进度");
            var nodes = rows.Select(u => u.Level).Append(level).Distinct().OrderBy(l => l).Take(4).ToList();
            float x0 = 568, x1 = 922;
            for (int i = 0; i < nodes.Count; i++)
            {
                float x = nodes.Count == 1 ? (x0 + x1) / 2 : Mathf.Lerp(x0, x1, i / (float)(nodes.Count - 1));
                if (i < nodes.Count - 1)
                {
                    float xn = Mathf.Lerp(x0, x1, (i + 1) / (float)(nodes.Count - 1));
                    p.Fill(x + 24, 681, xn - x - 48, 2, nodes[i + 1] <= level ? MenuPaint.Cyan : SilverA(0.5f));
                }
                int lv = nodes[i];
                var skill = rows.FirstOrDefault(u => u.Level == lv).Skill;
                string node = lv == level ? "node_current" : lv < level ? "node_done" : "node_locked";
                if (!p.Icon(x - 24, 658, 48, 48, node))
                {
                    p.Disc(p.P(x, 682), 22 * p.K, lv == level ? MenuPaint.Cyan : MenuPaint.Silver);
                    p.Disc(p.P(x, 682), 18 * p.K, MenuPaint.Base);
                    p.Text(x - 24, 658, 48, 48, lv < level ? "✓" : lv == level ? "◆" : "🔒", 20, lv == level ? MenuPaint.Cyan : MenuPaint.Ivory, TextAnchor.MiddleCenter, true);
                }
                p.Text(x - 60, 712, 120, 30, $"Lv.{lv}", 21, lv == level ? MenuPaint.Gold : MenuPaint.Ivory, TextAnchor.MiddleCenter);
                if (lv == level && skill == null)
                {
                    p.Frame(x - 45, 744, 90, 32, "tab_selected", MenuPaint.Cyan, new Color(MenuPaint.SelectBg.r, MenuPaint.SelectBg.g, MenuPaint.SelectBg.b, 0.9f));
                    p.Text(x - 45, 744, 90, 32, "当前", 20, MenuPaint.Ivory, TextAnchor.MiddleCenter);
                }
                else if (skill != null) p.Text(x - 70, 744, 140, 32, skill.DisplayName + (lv == level ? "·当前" : ""), 22, lv <= level ? MenuPaint.Ivory : MenuPaint.Mist, TextAnchor.MiddleCenter, true);
            }
            Info(600, 796, "切换武将查看各自技能。", 19);

            // 技能详情.
            p.PanelBox(1044, 158, 598, 690, "技能详情");
            if (rows.Count == 0) return;
            var sel0 = rows[skillIndex]; var s = sel0.Skill;
            p.Frame(1060, 224, 248, 214, "art_frame", SilverA(0.6f));
            SkillPicture(1064, 228, 240, 206, s);
            p.Text(1332, 228, 290, 54, s.DisplayName, 36, MenuPaint.Ivory, TextAnchor.MiddleLeft, true);
            p.HLine(1332, 282, 290, SilverA(0.35f));
            p.Icon(1334, 292, 34, 34, SkillKindIcon(s));
            p.Text(1380, 288, 100, 42, SkillKind(s), 21, MenuPaint.Ivory);
            p.Frame(1492, 286, 130, 44, "subpanel", SilverA(0.4f), new Color(0.02f, 0.05f, 0.10f, 0.7f));
            p.Text(1506, 286, 60, 44, "技力", 20, MenuPaint.Ivory);
            p.Text(1560, 286, 52, 44, s.TechCost.ToString(), 30, MenuPaint.Gold, TextAnchor.MiddleRight);
            p.Text(1332, 340, 300, 84, MenuText.SkillDetail(s), 20, MenuPaint.Ivory, TextAnchor.UpperLeft, false, true);
            p.HLine(1332, 432, 290, SilverA(0.35f));

            p.SubBox(1058, 452, 566, 120, "作用范围");
            p.Text(1058, 504, 400, 60, SkillRange(s), 24, MenuPaint.Ivory, TextAnchor.MiddleCenter);
            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                {
                    bool on = s.Target == SkillTarget.AllEnemies || (x == 1 && y == 1);
                    var c = on ? (s.TargetsAllies ? MenuPaint.Cyan : MenuPaint.Red) : new Color(0.12f, 0.18f, 0.26f, 0.9f);
                    var cr = p.R(1488 + x * 31, 466 + y * 31, 28, 28);
                    p.Fill(cr, c); p.Outline(cr, SilverA(0.5f), 1);
                }

            p.SubBox(1058, 588, 566, 168, "使用对象");
            if (s.TargetsAllies)
            {
                var gs = Progress.Stats(g, Inv.Bonus(g.Id));
                var bust = Bust(g.Id);
                p.Frame(1072, 640, 112, 108, "art_frame", SilverA(0.5f));
                if (bust) GUI.DrawTexture(p.R(1075, 643, 106, 102), bust, ScaleMode.ScaleAndCrop);
                p.Text(1200, 636, 200, 40, g.DisplayName, 24, MenuPaint.Ivory, TextAnchor.MiddleLeft, true);
                p.Bar(1200, 680, 288, 16, 1f, MenuPaint.Jade);
                p.Text(1500, 670, 120, 36, $"{gs.MaxTroops} / {gs.MaxTroops}", 20, MenuPaint.Ivory);
                p.Text(1200, 708, 60, 40, "技力", 20, MenuPaint.Ivory);
                p.Text(1258, 708, 140, 40, $"<color=#5BAFE3>{gs.MaxTech}</color>  →  <color=#5BAFE3>{Mathf.Max(0, gs.MaxTech - s.TechCost)}</color>", 24, MenuPaint.Ivory);
                p.Text(1392, 708, 230, 40, "当前兵力已满。", 19, MenuPaint.Mist);
            }
            else p.Text(1072, 636, 540, 110, s.Target == SkillTarget.AllEnemies ? "战斗中作用于全部敌军。" : "战斗中选择一支敌军为目标。", 21, MenuPaint.Mist, TextAnchor.MiddleCenter);

            Button(1066, 770, 278, 64, "使用", ButtonKind.Disabled);
            Info(1360, 784, SkillUseBlocked(sel0), 19);
        }
    }
}
