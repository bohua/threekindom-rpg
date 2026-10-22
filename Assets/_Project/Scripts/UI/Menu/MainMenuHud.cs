using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Battle;
using ThreeKingdoms.Core.Economy;
using ThreeKingdoms.Gameplay.Battle;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.UI.Battle;
using ThreeKingdoms.UI.Common;
using ThreeKingdoms.UI.Shop;
using ThreeKingdoms.UI.Story;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThreeKingdoms.UI.Menu
{
    // The pause menu on the menu key (user 2026-10-22, after the reference image 1): tabs along the top (物品, 装备,
    // 技能, 武将, 系统); at the left the chosen general's card (bust, level, troop type, troops and tech, attributes)
    // with ◀ ▶ to change general; the tab's list in the middle; its detail at the right; a description bar along the
    // bottom. 武将 opens the general's page (reference image 2): name banner, large portrait, a radar of five
    // values, the value table, and tabs for 状态, 技能, 装备 and 列传.
    [RequireComponent(typeof(UIDocument))]
    public sealed class MainMenuHud : MonoBehaviour
    {
        [Tooltip("Pixel skin (BATTLE_UI_SKIN.md); empty keeps the flat style")] public BattleUiSkin Skin;
        public TownStoryController Controller;
        [Tooltip("Full illustrations for the general's page")] public DialoguePortraitSet Portraits;
        [Tooltip("Bust crops for the card: liu_bei, guan_yu, zhang_fei")] public Texture2D[] Busts = Array.Empty<Texture2D>();

        public enum Mode { Closed, Root, Info }
        public enum Tab { Items, Equip, Skills, Generals, System }
        public enum InfoTab { Status, Skills, Gear, Biography }

        VisualElement root, panel, tabRow, card, list, side, info, infoBody, radar;
        Label message, description, money;
        readonly Dictionary<Tab, Button> tabButtons = new Dictionary<Tab, Button>();
        int openedFrame, generalIndex;
        EquipSlot? pickingSlot;
        InfoTab infoTab;
        GeneralSpec infoGeneral;

        public Mode Current { get; private set; } = Mode.Closed;
        public Tab CurrentTab { get; private set; }
        public InfoTab CurrentInfoTab => infoTab;
        public IReadOnlyList<Button> Entries => list.Query<Button>().ToList();
        public IReadOnlyList<Button> SideEntries => side.Query<Button>().ToList();
        // All text on screen now, for tests.
        public string Detail => string.Join("\n", (Current == Mode.Info ? info : panel).Query<Label>().ToList().Select(l => l.text)
            .Concat((Current == Mode.Info ? info : panel).Query<Button>().ToList().Select(b => b.text)));
        public string Description => description.text;
        public GeneralSpec General => Joined.ElementAtOrDefault(generalIndex);

        DevPartyProgress Progress => Controller.Run.Progress;
        Inventory Inv => Progress.Inventory;
        List<GeneralSpec> Joined => Progress.Party.Where(g => Controller.Run.Story.HasJoined(g.Id)).ToList();

        void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            MenuKit.Skin = Skin;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;
            BattleFont.ApplyTo(root);
            root.style.flexGrow = 1;

            panel = new VisualElement();
            panel.style.position = Position.Absolute;
            panel.style.left = 80; panel.style.right = 80; panel.style.top = 60; panel.style.bottom = 60;
            MenuKit.Frame(panel);
            root.Add(panel);

            // Tabs, money and reputation, and 返回.
            var top = Row(panel);
            top.style.justifyContent = Justify.SpaceBetween; top.style.alignItems = Align.Center;
            tabRow = Row(top);
            foreach (Tab t in Enum.GetValues(typeof(Tab)))
            {
                var tab = t;
                var b = MenuKit.Button(tabRow, MenuText.TabName(t), () => { if (Time.frameCount > openedFrame) Show(tab); }, size: 26);
                b.style.width = 150; b.style.marginRight = 8; b.style.marginTop = 0;
                b.style.unityTextAlign = TextAnchor.MiddleCenter; b.style.paddingLeft = 0;
                tabButtons[t] = b;
            }
            var right = Row(top); right.style.alignItems = Align.Center;
            money = MenuKit.Text(right, "", 24, MenuKit.Gold); money.style.marginRight = 24;
            var back = MenuKit.Button(right, MenuText.Back2, Back, size: 24);
            back.style.width = 120; back.style.marginTop = 0;

            var body = Row(panel);
            body.style.flexGrow = 1; body.style.marginTop = 16;
            card = new VisualElement(); card.style.width = 400; card.style.marginRight = 20; MenuKit.Frame(card); body.Add(card);
            list = new VisualElement(); list.style.width = 480; list.style.marginRight = 20; body.Add(list);
            side = new VisualElement(); side.style.flexGrow = 1; MenuKit.Frame(side); body.Add(side);

            var bottom = new VisualElement(); bottom.style.marginTop = 14; MenuKit.Frame(bottom); panel.Add(bottom);
            description = MenuKit.Text(bottom, "", 24, Color.white);
            var foot = Row(panel); foot.style.justifyContent = Justify.SpaceBetween; foot.style.marginTop = 8;
            message = MenuKit.Text(foot, "", 22, MenuKit.Gold);
            MenuKit.Text(foot, MenuText.Back, 20, MenuKit.Muted);

            BuildInfo();

            root.RegisterCallback<NavigationCancelEvent>(e => { e.StopPropagation(); Back(); }, TrickleDown.TrickleDown);
            if (Controller) { Controller.PanelOpened += OnOpened; Controller.PanelClosed += OnClosed; }
            Hide();
        }

        void OnDisable()
        {
            if (Controller) { Controller.PanelOpened -= OnOpened; Controller.PanelClosed -= OnClosed; }
        }

        void OnOpened(string p)
        {
            if (p != TownStoryController.MenuPanel) return;
            openedFrame = Time.frameCount;
            generalIndex = 0;
            message.text = "";
            Current = Mode.Root;
            panel.style.display = DisplayStyle.Flex;
            info.style.display = DisplayStyle.None;
            Show(Tab.Generals);
        }

        void OnClosed(string p) { if (p == TownStoryController.MenuPanel) Hide(); }

        void Hide()
        {
            Current = Mode.Closed;
            panel.style.display = DisplayStyle.None;
            info.style.display = DisplayStyle.None;
            list.Clear();
        }

        public void Back()
        {
            if (Current == Mode.Info) { CloseInfo(); return; }
            if (Current != Mode.Root) return;
            if (pickingSlot != null) { pickingSlot = null; Show(CurrentTab); return; }
            Controller.ClosePanel();
        }

        // ---------- the menu ----------

        public void Show(Tab tab)
        {
            CurrentTab = tab;
            pickingSlot = null;
            foreach (var pair in tabButtons)
                pair.Value.style.color = pair.Key == tab ? MenuKit.Gold : Color.white;
            money.text = $"{ShopText.Copper(Inv.Copper)}　{MenuText.Reputation(Controller.Run.Life.Reputation)}";
            ShowCard();
            list.Clear(); side.Clear();
            description.text = "";
            switch (tab)
            {
                case Tab.Items:
                    Entry($"{ShopText.Ration} {ShopText.Count(Inv.Rations)}", () => { }, () => Describe(ShopText.RationInfo));
                    foreach (var (item, count) in Inv.Bag.ToList())
                    {
                        var it = item;
                        Entry($"{it.DisplayName} {ShopText.Count(count)}", () => { }, () => Describe($"{ShopText.Kind(it)}　{ShopText.Bonus(it.Bonus)}"));
                    }
                    if (!Inv.Bag.Any()) SideText(MenuText.BagEmpty);
                    break;
                case Tab.Equip:
                    foreach (EquipSlot s in Enum.GetValues(typeof(EquipSlot)))
                    {
                        var slot = s;
                        var worn = Inv.Worn(General.Id, slot);
                        Entry($"{ShopText.Slot(slot)}　{(worn != null ? worn.DisplayName : ShopText.Empty)}", () => PickFor(slot),
                            () => { Describe(worn != null ? $"{ShopText.Kind(worn)}　{ShopText.Bonus(worn.Bonus)}" : MenuText.EmptySlot); ShowOptions(slot, false); });
                    }
                    break;
                case Tab.Skills:
                    int level = Progress.Level(General);
                    foreach (var unlock in General.Skills)
                    {
                        var u = unlock;
                        bool learned = u.Level <= level;
                        var b = Entry(learned ? $"{u.Skill.DisplayName}（技力 {u.Skill.TechCost}）" : $"{u.Skill.DisplayName}（{u.Level} 级习得）", () => { },
                            () => Describe(u.Skill.Description));
                        if (!learned) b.style.color = MenuKit.Muted;
                    }
                    break;
                case Tab.Generals:
                    foreach (var g in Joined)
                    {
                        var gen = g;
                        Entry($"{g.DisplayName}　{BattleText.Troop(g.Troop)}　{BattleText.Level(Progress.Level(g))}", () => ShowInfo(gen),
                            () => { generalIndex = Joined.IndexOf(gen); ShowCard(); Describe(MenuText.GeneralsHint); });
                    }
                    break;
                case Tab.System:
                    Entry(MenuText.ToTitle, Controller.ReturnToTitle, () => Describe(MenuText.ToTitleHint));
                    Entry(MenuText.Quit, Application.Quit, () => Describe(MenuText.QuitHint));
                    break;
            }
            MenuKit.FocusFirst(list);
        }

        public void SelectGeneral(int delta)
        {
            var joined = Joined;
            if (joined.Count == 0) return;
            generalIndex = (generalIndex + delta + joined.Count) % joined.Count;
            Show(CurrentTab);
        }

        void ShowCard()
        {
            card.Clear();
            var g = General;
            if (g == null) return;
            var head = Row(card); head.style.alignItems = Align.Center; head.style.justifyContent = Justify.Center;
            var prev = MenuKit.Button(head, "◀", () => SelectGeneral(-1), size: 24);
            prev.style.width = 56; prev.style.paddingLeft = 0; prev.style.unityTextAlign = TextAnchor.MiddleCenter;
            var bust = new VisualElement { pickingMode = PickingMode.Ignore };
            bust.style.width = bust.style.height = 168; bust.style.marginLeft = 12; bust.style.marginRight = 12;
            var tex = Bust(g.Id);
            if (Skin) Skin.Portrait(bust, true);
            if (tex)
            {
                var img = new Image { image = tex, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                img.style.flexGrow = 1; img.style.marginLeft = img.style.marginRight = img.style.marginTop = img.style.marginBottom = 8;
                bust.Add(img);
            }
            head.Add(bust);
            var next = MenuKit.Button(head, "▶", () => SelectGeneral(1), size: 24);
            next.style.width = 56; next.style.paddingLeft = 0; next.style.unityTextAlign = TextAnchor.MiddleCenter;

            int level = Progress.Level(g);
            var stats = Progress.Stats(g, Inv.Bonus(g.Id));
            MenuKit.Text(card, $"{g.DisplayName}　{BattleText.Level(level)}", 30, MenuKit.Gold).style.marginTop = 10;
            MenuKit.Text(card, BattleText.Troop(g.Troop), 22, MenuKit.Muted);
            Bar(card, "兵力", stats.MaxTroops, stats.MaxTroops, new Color(0.35f, 0.75f, 0.4f));
            Bar(card, "技力", stats.MaxTech, stats.MaxTech, new Color(0.35f, 0.55f, 0.95f));
            var grid = new VisualElement(); grid.style.marginTop = 10; card.Add(grid);
            foreach (var (name, value) in new[] { ("武力", stats.Force), ("统率", stats.Command), ("智力", stats.Intellect),
                         ("物攻", stats.PhysicalAttack), ("物防", stats.PhysicalDefense), ("计攻", stats.StrategyAttack),
                         ("计防", stats.StrategyDefense), ("速度", stats.Speed) })
            {
                var r = Row(grid); r.style.justifyContent = Justify.SpaceBetween; r.style.marginTop = 2;
                MenuKit.Text(r, name, 22, new Color(0.85f, 0.8f, 0.7f));
                MenuKit.Text(r, value.ToString(), 22, Color.white);
            }
            MenuKit.Text(card, MenuText.Exp(Progress.Exp(g), Progress.ExpForNextLevel(g)), 20, MenuKit.Muted).style.marginTop = 8;
        }

        Texture2D Bust(string id)
        {
            int i = Array.IndexOf(new[] { "liu_bei", "guan_yu", "zhang_fei" }, id);
            return i >= 0 && i < Busts.Length ? Busts[i] : null;
        }

        static void Bar(VisualElement parent, string name, int value, int max, Color color)
        {
            var r = Row(parent); r.style.alignItems = Align.Center; r.style.marginTop = 6;
            var l = MenuKit.Text(r, name, 22, Color.white); l.style.width = 56;
            var track = new VisualElement { pickingMode = PickingMode.Ignore };
            track.style.flexGrow = 1; track.style.height = 14; track.style.backgroundColor = new Color(0, 0, 0, 0.5f);
            r.Add(track);
            var fill = new VisualElement { pickingMode = PickingMode.Ignore };
            fill.style.height = 14; fill.style.width = new Length(max > 0 ? 100f * value / max : 0, LengthUnit.Percent);
            fill.style.backgroundColor = color;
            track.Add(fill);
            MenuKit.Text(r, $" {value}/{max}", 20, Color.white).style.width = 110;
        }

        // ---------- equipment ----------

        public void PickFor(EquipSlot slot)
        {
            pickingSlot = slot;
            ShowOptions(slot, true);
            MenuKit.FocusFirst(side);
        }

        // The right column: the general's options for the slot, with what each would change.
        void ShowOptions(EquipSlot slot, bool pickable)
        {
            side.Clear();
            var g = General;
            MenuKit.Text(side, $"{ShopText.Slot(slot)}　{MenuText.Candidates}", 24, MenuKit.Gold);
            var now = Progress.Stats(g, Inv.Bonus(g.Id));
            var worn = Inv.Worn(g.Id, slot);
            if (worn != null && pickable)
                MenuKit.Button(side, ShopText.Unequip, () => { Inv.Unequip(g.Id, slot); message.text = ShopText.Unequipped(g.DisplayName, worn.DisplayName); Show(Tab.Equip); },
                    () => Describe($"{ShopText.Unequip}{worn.DisplayName}：{ShopText.Bonus(worn.Bonus)}"), size: 22);
            var options = Inv.Bag.Select(b => b.item).Where(i => i.Slot == slot).ToList();
            if (options.Count == 0) { SideText(ShopText.NothingToEquip); return; }
            foreach (var item in options)
            {
                var it = item;
                bool ok = Inv.CanEquip(g, it) == TradeResult.Ok;
                var then = Progress.Stats(g, Inv.BonusWith(g.Id, it));
                if (!pickable) { SideText($"{it.DisplayName}　{(ok ? ShopText.Bonus(it.Bonus) : ShopText.CannotEquip(it))}"); continue; }
                MenuKit.Button(side, $"{it.DisplayName} {ShopText.Count(Inv.Count(it))}", () =>
                {
                    if (Inv.Equip(g, it) == TradeResult.Ok) message.text = ShopText.Equipped(g.DisplayName, it.DisplayName);
                    Show(Tab.Equip);
                }, () => Describe(ok ? ShopText.Diff(now, then) : ShopText.CannotEquip(it)), enabled: ok, size: 22);
            }
        }

        void SideText(string text) => MenuKit.Text(side, text, 22, MenuKit.Muted).style.marginTop = 8;
        void Describe(string text) => description.text = text ?? "";

        Button Entry(string text, Action onClick, Action onFocus) =>
            MenuKit.Button(list, text, () => { if (Time.frameCount > openedFrame) onClick(); }, onFocus);

        // ---------- the general's page (reference image 2) ----------

        Label nameBanner;
        VisualElement portraitFrame, valuesBox, infoTabsRow, infoLeft;

        void BuildInfo()
        {
            info = new VisualElement();
            info.style.position = Position.Absolute;
            info.style.left = 80; info.style.right = 80; info.style.top = 60; info.style.bottom = 60;
            MenuKit.Frame(info);
            root.Add(info);

            nameBanner = MenuKit.Text(info, "", 34, Color.white);
            if (Skin) { Skin.Plaque(nameBanner); nameBanner.style.paddingLeft = nameBanner.style.paddingRight = 28; nameBanner.style.paddingTop = nameBanner.style.paddingBottom = 10; }
            nameBanner.style.alignSelf = Align.FlexStart;

            var upper = Row(info); upper.style.marginTop = 12; upper.style.height = 420;
            portraitFrame = new VisualElement { pickingMode = PickingMode.Ignore };
            portraitFrame.style.width = 420; portraitFrame.style.height = 420;
            portraitFrame.style.backgroundColor = new Color(0.1f, 0.12f, 0.14f);
            portraitFrame.style.borderTopWidth = portraitFrame.style.borderBottomWidth = portraitFrame.style.borderLeftWidth = portraitFrame.style.borderRightWidth = 3;
            portraitFrame.style.borderTopColor = portraitFrame.style.borderBottomColor = portraitFrame.style.borderLeftColor = portraitFrame.style.borderRightColor = MenuKit.Edge;
            portraitFrame.style.overflow = Overflow.Hidden;
            upper.Add(portraitFrame);
            radar = new VisualElement { pickingMode = PickingMode.Ignore };
            radar.style.flexGrow = 1; radar.style.marginLeft = 20; radar.style.marginRight = 20;
            radar.generateVisualContent += DrawRadar;
            upper.Add(radar);
            valuesBox = new VisualElement(); valuesBox.style.width = 340; MenuKit.Frame(valuesBox); upper.Add(valuesBox);

            infoTabsRow = Row(info); infoTabsRow.style.marginTop = 14;
            foreach (InfoTab t in Enum.GetValues(typeof(InfoTab)))
            {
                var tab = t;
                var b = MenuKit.Button(infoTabsRow, MenuText.InfoTabName(t), () => ShowInfoTab(tab), size: 24);
                b.style.width = 150; b.style.marginRight = 8; b.style.paddingLeft = 0; b.style.unityTextAlign = TextAnchor.MiddleCenter;
            }
            var lower = Row(info); lower.style.flexGrow = 1; lower.style.marginTop = 10;
            infoLeft = new VisualElement(); infoLeft.style.width = 420; MenuKit.Frame(infoLeft); lower.Add(infoLeft);
            infoBody = new VisualElement(); infoBody.style.flexGrow = 1; infoBody.style.marginLeft = 20; MenuKit.Frame(infoBody); lower.Add(infoBody);
            info.style.display = DisplayStyle.None;
        }

        public void ShowInfo(GeneralSpec g)
        {
            if (g == null) return;
            infoGeneral = g;
            Current = Mode.Info;
            panel.style.display = DisplayStyle.None;
            info.style.display = DisplayStyle.Flex;
            nameBanner.text = $"{g.DisplayName}（{MenuText.CourtesyName(g.Id)}）";

            portraitFrame.Clear();
            if (Portraits && Portraits.TryGet(g.Id, out var p))
            {
                // Head and shoulders of the full illustration, square, facing as in dialogue.
                float h = 0.4f, w = h * p.Texture.height / p.Texture.width;
                float axis = Mathf.Clamp(p.Axis, w / 2f, 1f - w / 2f);
                var img = new Image { image = p.Texture, scaleMode = ScaleMode.StretchToFill, pickingMode = PickingMode.Ignore };
                img.uv = new Rect(axis - w / 2f, 1f - h, w, h);
                img.style.flexGrow = 1;
                if (p.Flip) img.style.scale = new Scale(new Vector3(-1, 1, 1));
                portraitFrame.Add(img);
            }

            var stats = Progress.Stats(g, Inv.Bonus(g.Id));
            valuesBox.Clear();
            foreach (var (name, value) in new[] { ("武力", stats.Force), ("统率", stats.Command), ("智力", stats.Intellect), ("兵力", stats.MaxTroops),
                         ("速度", stats.Speed), ("物攻", stats.PhysicalAttack), ("物防", stats.PhysicalDefense), ("计攻", stats.StrategyAttack), ("计防", stats.StrategyDefense) })
            {
                var r = Row(valuesBox); r.style.justifyContent = Justify.SpaceBetween; r.style.marginTop = 4;
                MenuKit.Text(r, name, 24, MenuKit.Gold);
                MenuKit.Text(r, value.ToString(), 24, Color.white);
            }

            infoLeft.Clear();
            int level = Progress.Level(g);
            var next = Progress.ExpForNextLevel(g);
            foreach (var (k, v) in new[] { ("等级", level.ToString()), ("经验", next is int n ? $"{Progress.Exp(g)}／{n}" : $"{Progress.Exp(g)}（上限）"),
                         ("兵种", BattleText.Troop(g.Troop)), ("声望", Controller.Run.Life.Reputation.ToString()) })
            {
                var r = Row(infoLeft); r.style.justifyContent = Justify.SpaceBetween; r.style.marginTop = 4;
                MenuKit.Text(r, k, 24, MenuKit.Gold);
                MenuKit.Text(r, v, 24, Color.white);
            }
            radar.Clear();
            radar.MarkDirtyRepaint();
            ShowInfoTab(InfoTab.Status);
        }

        public void ShowInfoTab(InfoTab tab)
        {
            infoTab = tab;
            var g = infoGeneral;
            infoBody.Clear();
            switch (tab)
            {
                case InfoTab.Status:
                    MenuKit.Text(infoBody, $"所率兵种：{BattleText.Troop(g.Troop)}", 24, Color.white);
                    MenuKit.Text(infoBody, MenuText.Counter(g.Troop), 22, new Color(0.85f, 0.85f, 0.85f)).style.marginTop = 8;
                    break;
                case InfoTab.Skills:
                    int level = Progress.Level(g);
                    foreach (var u in g.Skills)
                        MenuKit.Text(infoBody, MenuText.Skill(u.Skill.DisplayName, u.Skill.TechCost, u.Skill.Description, u.Level <= level, u.Level), 22,
                            u.Level <= level ? Color.white : MenuKit.Muted).style.marginTop = 6;
                    break;
                case InfoTab.Gear:
                    foreach (EquipSlot s in Enum.GetValues(typeof(EquipSlot)))
                    {
                        var w = Inv.Worn(g.Id, s);
                        MenuKit.Text(infoBody, $"{ShopText.Slot(s)}：{(w != null ? $"{w.DisplayName}（{ShopText.Bonus(w.Bonus)}）" : ShopText.Empty)}", 22, Color.white).style.marginTop = 6;
                    }
                    break;
                case InfoTab.Biography:
                    MenuKit.Text(infoBody, MenuText.Biography(g.Id), 22, Color.white);
                    break;
            }
            var buttons = infoTabsRow.Query<Button>().ToList();
            for (int i = 0; i < buttons.Count; i++) buttons[i].style.color = i == (int)tab ? MenuKit.Gold : Color.white;
            buttons[(int)tab].Focus();
        }

        void CloseInfo()
        {
            Current = Mode.Root;
            info.style.display = DisplayStyle.None;
            panel.style.display = DisplayStyle.Flex;
            Show(Tab.Generals);
        }

        // Five values on a pentagon: 武力, 统率, 智力 out of 100, 兵力 out of 300, 速度 out of 15.
        void DrawRadar(MeshGenerationContext mgc)
        {
            if (infoGeneral == null) return;
            var s = Progress.Stats(infoGeneral, Inv.Bonus(infoGeneral.Id));
            float[] v = { s.Force / 100f, s.Command / 100f, s.Intellect / 100f, s.MaxTroops / 300f, s.Speed / 15f };
            var rect = radar.contentRect;
            var c = rect.center + new Vector2(0, 10);
            float r = Mathf.Min(rect.width, rect.height) * 0.36f;
            Vector2 Corner(int i, float k)
            {
                float a = (-90f + i * 72f) * Mathf.Deg2Rad;
                return c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * k;
            }
            var p = mgc.painter2D;
            p.strokeColor = new Color(0.62f, 0.50f, 0.28f, 0.8f); p.lineWidth = 2;
            for (int ring = 1; ring <= 4; ring++)
            {
                p.BeginPath();
                for (int i = 0; i < 5; i++) { if (i == 0) p.MoveTo(Corner(i, ring / 4f)); else p.LineTo(Corner(i, ring / 4f)); }
                p.ClosePath(); p.Stroke();
            }
            for (int i = 0; i < 5; i++) { p.BeginPath(); p.MoveTo(c); p.LineTo(Corner(i, 1f)); p.Stroke(); }
            p.fillColor = new Color(0.95f, 0.65f, 0.25f, 0.55f);
            p.strokeColor = new Color(1f, 0.85f, 0.45f); p.lineWidth = 3;
            p.BeginPath();
            for (int i = 0; i < 5; i++) { var q = Corner(i, Mathf.Clamp01(v[i])); if (i == 0) p.MoveTo(q); else p.LineTo(q); }
            p.ClosePath(); p.Fill(); p.Stroke();
        }

        void Update()
        {
            if (Current == Mode.Root) MenuKit.KeepFocus(root, pickingSlot != null ? side : list);
            // Corner labels for the radar once its size is known.
            if (Current == Mode.Info && radar.childCount == 0 && radar.contentRect.width > 0) LabelRadar();
        }

        void LabelRadar()
        {
            string[] names = { "武力", "统率", "智力", "兵力", "速度" };
            var rect = radar.contentRect;
            var c = rect.center + new Vector2(0, 10);
            float r = Mathf.Min(rect.width, rect.height) * 0.36f + 30f;
            for (int i = 0; i < 5; i++)
            {
                float a = (-90f + i * 72f) * Mathf.Deg2Rad;
                var l = MenuKit.Text(radar, names[i], 24, MenuKit.Gold);
                l.style.position = Position.Absolute;
                l.style.left = c.x + Mathf.Cos(a) * r - 24; l.style.top = c.y + Mathf.Sin(a) * r - 16;
            }
        }

        static VisualElement Row(VisualElement parent)
        {
            var r = new VisualElement();
            r.style.flexDirection = FlexDirection.Row;
            parent.Add(r);
            return r;
        }
    }
}
