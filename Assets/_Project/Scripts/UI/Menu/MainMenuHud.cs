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
using UnityEngine;
using UnityEngine.UIElements;

namespace ThreeKingdoms.UI.Menu
{
    // Town main menu on the menu key (game-ui GDD answer 10): 武将 (level, exp, attributes, derived stats, skills and
    // gear of each joined general, with a way into the outfit panel), 物品 (copper, rations, the bag) and 设置
    // (Alpha 0.1 has no options yet: return to title, quit). Read-only except for those two system actions.
    [RequireComponent(typeof(UIDocument))]
    public sealed class MainMenuHud : MonoBehaviour
    {
        [Tooltip("Pixel skin (BATTLE_UI_SKIN.md); empty keeps the flat style")] public ThreeKingdoms.UI.Battle.BattleUiSkin Skin;
        public TownStoryController Controller;

        public enum Mode { Closed, Root, Generals, Items, Settings }

        VisualElement root, panel, list, detail;
        Label message;
        int openedFrame, rootIndex;

        public Mode Current { get; private set; } = Mode.Closed;
        public string Detail => string.Join("\n", detail.Query<Label>().ToList().Select(l => l.text));
        public IReadOnlyList<Button> Entries => list.Query<Button>().ToList();

        DevPartyProgress Progress => Controller.Run.Progress;
        Inventory Inv => Progress.Inventory;
        IEnumerable<GeneralSpec> Joined => Progress.Party.Where(g => Controller.Run.Story.HasJoined(g.Id));

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
            panel.style.left = 160; panel.style.right = 160; panel.style.top = 90; panel.style.bottom = 90;
            MenuKit.Frame(panel);
            root.Add(panel);
            var header = new VisualElement(); header.style.flexDirection = FlexDirection.Row; header.style.justifyContent = Justify.SpaceBetween;
            panel.Add(header);
            MenuKit.Text(header, MenuText.Title, 34, MenuKit.Gold);
            MenuKit.Text(header, MenuText.Chapter(GameRun.Chapter), 24, MenuKit.Muted);
            var body = new VisualElement(); body.style.flexDirection = FlexDirection.Row; body.style.flexGrow = 1; body.style.marginTop = 16;
            panel.Add(body);
            list = new VisualElement(); list.style.width = 420; list.style.marginRight = 28; body.Add(list);
            detail = new VisualElement(); detail.style.flexGrow = 1; MenuKit.Frame(detail); body.Add(detail);
            var footer = new VisualElement(); footer.style.flexDirection = FlexDirection.Row; footer.style.justifyContent = Justify.SpaceBetween; footer.style.marginTop = 12;
            panel.Add(footer);
            message = MenuKit.Text(footer, "", 22, MenuKit.Gold);
            MenuKit.Text(footer, MenuText.Back, 20, MenuKit.Muted);

            root.RegisterCallback<NavigationCancelEvent>(e => { e.StopPropagation(); Back(); }, TrickleDown.TrickleDown);
            if (Controller) { Controller.PanelOpened += OnOpened; Controller.PanelClosed += OnClosed; }
            Hide();
        }

        void OnDisable()
        {
            if (Controller) { Controller.PanelOpened -= OnOpened; Controller.PanelClosed -= OnClosed; }
        }

        void OnOpened(string p) { if (p == TownStoryController.MenuPanel) { panel.style.display = DisplayStyle.Flex; openedFrame = Time.frameCount; rootIndex = 0; message.text = ""; Go(Mode.Root); } }
        void OnClosed(string p) { if (p == TownStoryController.MenuPanel) Hide(); }

        void Hide()
        {
            Current = Mode.Closed;
            panel.style.display = DisplayStyle.None;
            list.Clear();
        }

        public void Back()
        {
            if (Current == Mode.Root) Controller.ClosePanel();
            else if (Current != Mode.Closed) Go(Mode.Root);
        }

        public void Go(Mode mode)
        {
            Current = mode;
            list.Clear();
            ShowDetail(null);
            switch (mode)
            {
                case Mode.Root:
                    Entry(MenuText.Generals, () => Go(Mode.Generals), () => { rootIndex = 0; ShowDetail(MenuText.Generals, MenuText.GeneralsHint); });
                    Entry(MenuText.Items, () => Go(Mode.Items), () => { rootIndex = 1; ShowItems(); });
                    Entry(MenuText.Settings, () => Go(Mode.Settings), () => { rootIndex = 2; ShowDetail(MenuText.Settings, MenuText.SettingsHint); });
                    Entry(MenuText.Close, Controller.ClosePanel, () => { rootIndex = 3; ShowDetail(null); });
                    MenuKit.FocusFirst(list, rootIndex);
                    return;
                case Mode.Generals:
                    foreach (var g in Joined) Entry(g.DisplayName, () => ShowGeneral(g), () => ShowGeneral(g));
                    Entry(ShopText.Outfit, () => Controller.SwitchPanel(TownStoryController.OutfitPanel), () => ShowDetail(ShopText.Outfit, ShopText.OutfitHint));
                    break;
                case Mode.Items:
                    Entry(MenuText.Back2, () => Go(Mode.Root), ShowItems);
                    break;
                case Mode.Settings:
                    Entry(MenuText.ToTitle, Controller.ReturnToTitle, () => ShowDetail(MenuText.ToTitle, MenuText.ToTitleHint));
                    Entry(MenuText.Quit, Application.Quit, () => ShowDetail(MenuText.Quit, MenuText.QuitHint));
                    break;
            }
            MenuKit.FocusFirst(list);
        }

        // ---------- detail ----------

        void ShowDetail(string title, string body = null)
        {
            detail.Clear();
            if (title == null) return;
            MenuKit.Text(detail, title, 30, MenuKit.Gold);
            if (body != null) MenuKit.Text(detail, body, 24, Color.white).style.marginTop = 10;
        }

        public void ShowGeneral(GeneralSpec g)
        {
            int level = Progress.Level(g);
            var stats = Progress.Stats(g, Inv.Bonus(g.Id));
            var next = Progress.ExpForNextLevel(g);
            ShowDetail($"{g.DisplayName}　{BattleText.Troop(g.Troop)}　{BattleText.Level(level)}",
                $"{MenuText.Exp(Progress.Exp(g), next)}\n{MenuText.Attributes(stats)}\n{ShopText.Stats(stats)}");
            var skills = MenuKit.Text(detail, MenuText.SkillsHeader, 22, MenuKit.Gold); skills.style.marginTop = 14;
            foreach (var unlock in g.Skills)
            {
                bool learned = unlock.Level <= level;
                var line = MenuKit.Text(detail, MenuText.Skill(unlock.Skill.DisplayName, unlock.Skill.TechCost, unlock.Skill.Description, learned, unlock.Level), 20,
                    learned ? Color.white : MenuKit.Muted);
                line.style.marginTop = 4;
            }
            var gear = MenuKit.Text(detail, MenuText.GearHeader, 22, MenuKit.Gold); gear.style.marginTop = 14;
            foreach (EquipSlot s in Enum.GetValues(typeof(EquipSlot)))
            {
                var w = Inv.Worn(g.Id, s);
                MenuKit.Text(detail, $"{ShopText.Slot(s)}：{(w != null ? $"{w.DisplayName}（{ShopText.Bonus(w.Bonus)}）" : ShopText.Empty)}", 20, Color.white).style.marginTop = 4;
            }
        }

        void ShowItems()
        {
            ShowDetail(MenuText.Items, $"{ShopText.Copper(Inv.Copper)}　{ShopText.Rations(Inv.Rations)}");
            var bag = Inv.Bag.ToList();
            if (bag.Count == 0) MenuKit.Text(detail, MenuText.BagEmpty, 20, MenuKit.Muted).style.marginTop = 10;
            foreach (var (item, count) in bag)
                MenuKit.Text(detail, $"{item.DisplayName} {ShopText.Count(count)}　{ShopText.Kind(item)}　{ShopText.Bonus(item.Bonus)}", 20, Color.white).style.marginTop = 6;
        }

        void Entry(string text, Action onClick, Action onFocus) =>
            MenuKit.Button(list, text, () => { if (Time.frameCount > openedFrame) onClick(); }, onFocus);

        void Update()
        {
            if (Current != Mode.Closed) MenuKit.KeepFocus(root, list);
        }
    }
}
