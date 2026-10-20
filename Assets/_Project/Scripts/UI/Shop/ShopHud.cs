using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Battle;
using ThreeKingdoms.Core.Economy;
using ThreeKingdoms.Gameplay.Battle;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.UI.Battle;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace ThreeKingdoms.UI.Shop
{
    // Zhuojun weapons & equipment shop (equipment-items GDD, game-ui GDD §商店与配装): buy, sell (equipped items must
    // be taken off first, every sale is confirmed), and change the three slots, with a new-vs-old preview for each
    // general. All rules live in Inventory; this panel only offers them and shows the result. Cancel steps back one
    // level and closes the shop from the top.
    [RequireComponent(typeof(UIDocument))]
    public sealed class ShopHud : MonoBehaviour
    {
        public TownStoryController Controller;

        static readonly Color Gold = new Color(0.93f, 0.76f, 0.36f);
        static readonly Color PanelColor = new Color(0.07f, 0.09f, 0.14f, 0.94f);
        static readonly Color Edge = new Color(0.62f, 0.50f, 0.28f);
        static readonly Color Idle = new Color(0.12f, 0.15f, 0.22f, 0.95f);
        static readonly Color Dim = new Color(0.35f, 0.3f, 0.2f);
        static readonly Color Muted = new Color(0.62f, 0.62f, 0.62f);
        static readonly Color Good = new Color(0.55f, 0.85f, 0.55f);

        public enum Mode { Closed, Tabs, Buy, Sell, ConfirmSell, PickGeneral, PickSlot, PickItem }

        VisualElement root, panel, list, detail;
        Label money, message;
        readonly Dictionary<Mode, int> lastIndex = new Dictionary<Mode, int>();
        int openedFrame;
        GeneralSpec general;
        EquipSlot slot;
        EquipmentSpec pendingSell;     // null with pendingRation = selling a ration
        bool pendingRation;

        public Mode Current { get; private set; } = Mode.Closed;
        public string Message => message?.text;

        DevPartyProgress Progress => Controller.Run.Progress;
        Inventory Inv => Progress.Inventory;
        IEnumerable<GeneralSpec> Joined => Progress.Party.Where(g => Controller.Run.Story.HasJoined(g.Id));

        void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;
            BattleFont.ApplyTo(root);
            root.style.flexGrow = 1;
            BuildLayout();
            root.RegisterCallback<NavigationCancelEvent>(e => { e.StopPropagation(); Back(); }, TrickleDown.TrickleDown);
            if (Controller) { Controller.ShopOpened += Open; Controller.ShopClosed += Hide; }
            Hide();
        }

        void OnDisable()
        {
            if (Controller) { Controller.ShopOpened -= Open; Controller.ShopClosed -= Hide; }
        }

        void BuildLayout()
        {
            panel = new VisualElement();
            panel.style.position = Position.Absolute;
            panel.style.left = 160; panel.style.right = 160; panel.style.top = 90; panel.style.bottom = 90;
            Frame(panel);
            root.Add(panel);

            var header = Row(panel);
            header.style.justifyContent = Justify.SpaceBetween;
            Text(header, ShopText.Title, 34, Gold);
            money = Text(header, "", 28, Color.white);

            var body = Row(panel);
            body.style.flexGrow = 1; body.style.marginTop = 16;
            list = new VisualElement();
            list.style.width = 560; list.style.marginRight = 28;
            body.Add(list);
            detail = new VisualElement();
            detail.style.flexGrow = 1;
            Frame(detail);
            body.Add(detail);

            var footer = Row(panel);
            footer.style.justifyContent = Justify.SpaceBetween; footer.style.marginTop = 12;
            message = Text(footer, "", 24, Gold);
            message.style.whiteSpace = WhiteSpace.Normal; message.style.flexShrink = 1;
            Text(footer, ShopText.Back, 20, Muted);
        }

        // ---------- open / close ----------

        void Open()
        {
            panel.style.display = DisplayStyle.Flex;
            openedFrame = Time.frameCount;
            lastIndex.Clear();
            Say(ShopText.Welcome);
            Go(Mode.Tabs);
        }

        void Hide()
        {
            Current = Mode.Closed;
            if (panel != null) panel.style.display = DisplayStyle.None;
            list?.Clear();
        }

        // ---------- navigation (public so tests and other UI can drive it) ----------

        public void Back()
        {
            switch (Current)
            {
                case Mode.Tabs: Controller.CloseShop(); break;
                case Mode.Buy: case Mode.Sell: case Mode.PickGeneral: Go(Mode.Tabs); break;
                case Mode.ConfirmSell: Go(Mode.Sell); break;
                case Mode.PickSlot: Go(Mode.PickGeneral); break;
                case Mode.PickItem: Go(Mode.PickSlot); break;
            }
        }

        public void Go(Mode mode)
        {
            Current = mode;
            list.Clear();
            ShowDetail(null);
            money.text = $"{ShopText.Copper(Inv.Copper)}　{ShopText.Rations(Inv.Rations)}";
            switch (mode)
            {
                case Mode.Tabs:
                    Entry(ShopText.Buy, null, () => Go(Mode.Buy), () => ShowDetail(ShopText.Buy, ShopText.BuyHint));
                    Entry(ShopText.Sell, null, () => Go(Mode.Sell), () => ShowDetail(ShopText.Sell, ShopText.SellHint));
                    Entry(ShopText.Outfit, null, () => Go(Mode.PickGeneral), () => ShowDetail(ShopText.Outfit, ShopText.OutfitHint));
                    Entry(ShopText.Leave, null, Controller.CloseShop, () => ShowDetail(ShopText.Leave, ShopText.LeaveHint));
                    break;
                case Mode.Buy:
                    foreach (var item in Inv.Data.ShopStock)
                        Entry(item.DisplayName, ShopText.Price(item.BuyPrice), () => BuyItem(item), () => ShowItem(item, ShopText.BuyPrice(item.BuyPrice)),
                            dim: Inv.CanBuy(item) != TradeResult.Ok);
                    Entry(ShopText.Ration, ShopText.Price(Inv.Data.Config.RationPrice), BuyRation,
                        () => ShowDetail(ShopText.Ration, $"{ShopText.RationInfo}\n\n{ShopText.BuyPrice(Inv.Data.Config.RationPrice)}　持有 {Inv.Rations}"),
                        dim: Inv.CanBuyRation() != TradeResult.Ok);
                    break;
                case Mode.Sell:
                    foreach (var (item, count) in Inv.Bag)
                        Entry($"{item.DisplayName} {ShopText.Count(count)}", ShopText.Price(Inv.SellPrice(item)), () => AskSell(item),
                            () => ShowItem(item, ShopText.SellPrice(Inv.SellPrice(item))));
                    if (Inv.Rations > 0)
                        Entry($"{ShopText.Ration} {ShopText.Count(Inv.Rations)}", ShopText.Price(Inv.Data.RationSellPrice), () => AskSell(null),
                            () => ShowDetail(ShopText.Ration, $"{ShopText.RationInfo}\n\n{ShopText.SellPrice(Inv.Data.RationSellPrice)}"));
                    // Worn items are listed so the player learns why they cannot be sold (GDD rule 15).
                    foreach (var g in Joined)
                        foreach (EquipSlot s in Enum.GetValues(typeof(EquipSlot)))
                            if (Inv.Worn(g.Id, s) is EquipmentSpec w)
                                Entry(w.DisplayName, ShopText.Wearing(g.DisplayName), () => Say(ShopText.Refused(Inv.CanSell(w), w.DisplayName)),
                                    () => ShowItem(w, ShopText.SellPrice(Inv.SellPrice(w))), dim: true);
                    break;
                case Mode.ConfirmSell:
                    string name = pendingRation ? ShopText.Ration : pendingSell.DisplayName;
                    int price = pendingRation ? Inv.Data.RationSellPrice : Inv.SellPrice(pendingSell);
                    ShowDetail(name, ShopText.ConfirmSell(name, price));
                    Entry(ShopText.Confirm, ShopText.Price(price), ConfirmSell, () => ShowDetail(name, ShopText.ConfirmSell(name, price)));
                    Entry(ShopText.Cancel, null, () => Go(Mode.Sell), () => ShowDetail(name, ShopText.ConfirmSell(name, price)));
                    break;
                case Mode.PickGeneral:
                    foreach (var g in Joined)
                        Entry(g.DisplayName, BattleText.Troop(g.Troop), () => { general = g; Go(Mode.PickSlot); }, () => ShowGeneral(g));
                    break;
                case Mode.PickSlot:
                    foreach (EquipSlot s in Enum.GetValues(typeof(EquipSlot)))
                    {
                        var w = Inv.Worn(general.Id, s);
                        Entry($"{ShopText.Slot(s)}　{(w != null ? w.DisplayName : ShopText.Empty)}", null, () => { slot = s; Go(Mode.PickItem); }, () => ShowGeneral(general));
                    }
                    break;
                case Mode.PickItem:
                    var worn = Inv.Worn(general.Id, slot);
                    foreach (var (item, count) in Inv.Bag.Where(x => x.item.Slot == slot))
                    {
                        bool fits = item.Fits(general.Troop);
                        Entry($"{item.DisplayName} {ShopText.Count(count)}", fits ? null : ShopText.CannotEquip(item),
                            () => EquipItem(item), () => ShowItem(item, null), enabled: fits);
                    }
                    if (worn != null)
                        Entry(ShopText.Unequip, worn.DisplayName, UnequipSlot,
                            () => ShowDetail(ShopText.Unequip, $"{general.DisplayName}：{ShopText.Diff(Progress.Stats(general, Inv.Bonus(general.Id)), Progress.Stats(general, Without(general.Id, slot)))}"));
                    if (!list.Children().Any())   // something focusable keeps Cancel and Submit working
                        Entry(ShopText.Return, null, Back, () => ShowDetail(ShopText.Slot(slot), ShopText.NothingToEquip));
                    break;
            }
            FocusEntry(lastIndex.TryGetValue(mode, out int i) ? i : 0);
        }

        // ---------- actions ----------

        public void BuyItem(EquipmentSpec item)
        {
            var r = Inv.Buy(item);
            Say(r == TradeResult.Ok ? ShopText.Bought(item.DisplayName, item.BuyPrice) : ShopText.Refused(r, item.DisplayName));
            Go(Mode.Buy);
        }

        public void BuyRation()
        {
            var r = Inv.BuyRation();
            Say(r == TradeResult.Ok ? ShopText.Bought(ShopText.Ration, Inv.Data.Config.RationPrice) : ShopText.Refused(r, ShopText.Ration));
            Go(Mode.Buy);
        }

        // Selling always goes through a confirmation (GDD rule 15); a null item is a ration.
        public void AskSell(EquipmentSpec item)
        {
            var r = item == null ? (Inv.Rations > 0 ? TradeResult.Ok : TradeResult.NoRations) : Inv.CanSell(item);
            if (r != TradeResult.Ok) { Say(ShopText.Refused(r, item?.DisplayName ?? ShopText.Ration)); return; }
            pendingSell = item; pendingRation = item == null;
            Go(Mode.ConfirmSell);
        }

        public void ConfirmSell()
        {
            if (Current != Mode.ConfirmSell) return;
            if (pendingRation)
            {
                int price = Inv.Data.RationSellPrice;
                var r = Inv.SellRation();
                Say(r == TradeResult.Ok ? ShopText.Sold(ShopText.Ration, price) : ShopText.Refused(r, ShopText.Ration));
            }
            else
            {
                int price = Inv.SellPrice(pendingSell);
                var r = Inv.Sell(pendingSell);
                Say(r == TradeResult.Ok ? ShopText.Sold(pendingSell.DisplayName, price) : ShopText.Refused(r, pendingSell.DisplayName));
            }
            pendingSell = null; pendingRation = false;
            Go(Mode.Sell);
        }

        public void SelectGeneral(GeneralSpec g, EquipSlot s) { general = g; slot = s; Go(Mode.PickItem); }

        public void EquipItem(EquipmentSpec item)
        {
            var r = Inv.Equip(general, item);
            Say(r == TradeResult.Ok ? ShopText.Equipped(general.DisplayName, item.DisplayName) : ShopText.Refused(r, item.DisplayName));
            if (r == TradeResult.Ok) Go(Mode.PickSlot); else Go(Mode.PickItem);
        }

        void UnequipSlot()
        {
            var old = Inv.Worn(general.Id, slot);
            if (old != null && Inv.Unequip(general.Id, slot) == TradeResult.Ok) Say(ShopText.Unequipped(general.DisplayName, old.DisplayName));
            Go(Mode.PickSlot);
        }

        StatBonus Without(string generalId, EquipSlot s)
        {
            var b = Inv.Bonus(generalId);
            var w = Inv.Worn(generalId, s);
            if (w == null) return b;
            var x = w.Bonus;
            b.Troops -= x.Troops; b.Tech -= x.Tech; b.Speed -= x.Speed;
            b.PhysicalAttack -= x.PhysicalAttack; b.PhysicalDefense -= x.PhysicalDefense;
            b.StrategyAttack -= x.StrategyAttack; b.StrategyDefense -= x.StrategyDefense;
            return b;
        }

        // ---------- detail panel ----------

        void ShowDetail(string title, string body = null)
        {
            detail.Clear();
            if (title == null) return;
            Text(detail, title, 30, Gold);
            if (body != null) { var b = Text(detail, body, 24, Color.white); b.style.whiteSpace = WhiteSpace.Normal; b.style.marginTop = 10; }
        }

        // Item card plus, for every joined general, what it would change (or why they cannot use it).
        void ShowItem(EquipmentSpec item, string price)
        {
            ShowDetail(item.DisplayName, $"{ShopText.Kind(item)}\n{ShopText.Bonus(item.Bonus)}{(price != null ? "\n" + price : "")}");
            var who = Current == Mode.PickItem ? new[] { general } : Joined.ToArray();
            foreach (var g in who)
            {
                string line;
                Color color = Color.white;
                if (!item.Fits(g.Troop)) { line = ShopText.CannotEquip(item); color = Muted; }
                else if (Inv.Worn(g.Id, item.Slot) == item) { line = ShopText.Wearing(g.DisplayName); color = Muted; }
                else
                {
                    line = ShopText.Diff(Progress.Stats(g, Inv.Bonus(g.Id)), Progress.Stats(g, Inv.BonusWith(g.Id, item)));
                    color = Good;
                }
                var row = Text(detail, $"{g.DisplayName}：{line}", 22, color);
                row.style.whiteSpace = WhiteSpace.Normal; row.style.marginTop = 8;
            }
        }

        void ShowGeneral(GeneralSpec g)
        {
            var lines = Enum.GetValues(typeof(EquipSlot)).Cast<EquipSlot>()
                .Select(s => $"{ShopText.Slot(s)}：{Inv.Worn(g.Id, s)?.DisplayName ?? ShopText.Empty}");
            ShowDetail($"{g.DisplayName}（{BattleText.Troop(g.Troop)}　{Progress.Level(g)} 级）",
                string.Join("\n", lines) + "\n\n" + ShopText.Stats(Progress.Stats(g, Inv.Bonus(g.Id))));
        }

        void Say(string text) { if (message != null) message.text = text; }

        // ---------- widgets ----------

        Button Entry(string text, string right, Action onClick, Action onFocus, bool enabled = true, bool dim = false)
        {
            int index = list.childCount;
            var mode = Current;
            var b = new Button(() => { if (Time.frameCount > openedFrame) onClick(); }) { text = text };
            b.style.fontSize = 26;
            b.style.color = enabled && !dim ? Color.white : Muted;
            b.style.backgroundColor = Idle;
            b.style.borderTopColor = b.style.borderBottomColor = b.style.borderLeftColor = b.style.borderRightColor = Dim;
            b.style.unityTextAlign = TextAnchor.MiddleLeft;
            b.style.paddingLeft = 16; b.style.height = 48; b.style.marginTop = 4;
            b.SetEnabled(enabled);
            if (right != null)
            {
                var r = new Label(right) { pickingMode = PickingMode.Ignore };
                r.style.position = Position.Absolute; r.style.right = 16; r.style.top = 0; r.style.bottom = 0;
                r.style.unityTextAlign = TextAnchor.MiddleRight; r.style.fontSize = 22; r.style.color = Muted;
                b.Add(r);
            }
            b.RegisterCallback<FocusInEvent>(_ =>
            {
                b.style.backgroundColor = new Color(0.45f, 0.33f, 0.10f, 0.98f);
                b.style.borderTopColor = b.style.borderBottomColor = b.style.borderLeftColor = b.style.borderRightColor = Gold;
                b.style.borderLeftWidth = 6;
                lastIndex[mode] = index;
                onFocus?.Invoke();
            });
            b.RegisterCallback<FocusOutEvent>(_ =>
            {
                b.style.backgroundColor = Idle;
                b.style.borderTopColor = b.style.borderBottomColor = b.style.borderLeftColor = b.style.borderRightColor = Dim;
                b.style.borderLeftWidth = 1;
            });
            b.RegisterCallback<PointerEnterEvent>(_ => { if (b.enabledSelf) b.Focus(); });
            list.Add(b);
            return b;
        }

        public IReadOnlyList<Button> Entries => list.Query<Button>().ToList();

        void FocusEntry(int index)
        {
            var buttons = list.Query<Button>().Where(b => b.enabledSelf).ToList();
            if (buttons.Count == 0) return;
            buttons[Mathf.Clamp(index, 0, buttons.Count - 1)].Focus();
        }

        // Gamepad / keyboard navigation goes through the EventSystem's selection; a focus set before the panel's
        // event handler exists (first frames after opening) is lost, so keep re-sending it until it sticks.
        void Update()
        {
            if (Current == Mode.Closed || root?.panel == null) return;
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject != null && root.panel.focusController.focusedElement != null) return;
            var button = list.Query<Button>().Where(b => b.enabledSelf).First();
            if (button == null) return;
            button.Blur();
            button.Focus();
        }

        static VisualElement Row(VisualElement parent)
        {
            var r = new VisualElement();
            r.style.flexDirection = FlexDirection.Row;
            parent.Add(r);
            return r;
        }

        static Label Text(VisualElement parent, string text, int size, Color color)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.style.fontSize = size; l.style.color = color;
            l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
            parent.Add(l);
            return l;
        }

        static void Frame(VisualElement e)
        {
            e.style.backgroundColor = PanelColor;
            e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = 2;
            e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = Edge;
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = 4;
            e.style.paddingLeft = e.style.paddingRight = 22; e.style.paddingTop = e.style.paddingBottom = 14;
        }
    }
}
