using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Battle;
using ThreeKingdoms.Gameplay.Battle;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace ThreeKingdoms.UI.Battle
{
    // Battle HUD (UI Toolkit, built in code) following BATTLE_SCREEN_DESIGN.md V2: command menu on the left,
    // our cards along the bottom (portrait crop, level, troop-type icon top-right, troops / tech bars), enemy
    // plates over their units, round and action order at the top, result panel at the end.
    // Reads state from BattleController and submits commands through it; never touches the battle core directly.
    [RequireComponent(typeof(UIDocument))]
    public sealed class BattleHud : MonoBehaviour
    {
        public BattleController Controller;
        [Tooltip("Teaching hints; optional")] public BattleTutor Tutor;
        [Tooltip("Pixel frames (BATTLE_UI_SKIN.md); without it the HUD uses flat colours")] public BattleUiSkin Skin;
        [Tooltip("Speaker illustrations for the lines after a victory")] public ThreeKingdoms.UI.Story.DialoguePortraitSet Portraits;

        // The skin the static widget helpers draw with (one battle HUD at a time).
        static BattleUiSkin skin;
        public Camera BattleCamera;
        public string LocationName = "涿郡 · 街市";
        [Tooltip("Infantry, Cavalry, Archer, YellowTurban, Strategist")] public Texture2D[] TroopIcons = Array.Empty<Texture2D>();

        static readonly Color Gold = new Color(0.93f, 0.76f, 0.36f);
        static readonly Color Panel = new Color(0.07f, 0.09f, 0.14f, 0.88f);
        static readonly Color PanelEdge = new Color(0.62f, 0.50f, 0.28f);

        VisualElement root, menu, cards, plates, popups, resultPanel, prepPanel;
        // The retreating leader's parting words and the brothers' answer after a first victory (user 2026-10-22),
        // played before the result panel. A transparent full-screen button takes confirm / click to advance.
        ThreeKingdoms.UI.Story.DialogueView dialogue;
        Button dialogueCatcher;
        IReadOnlyList<ThreeKingdoms.Gameplay.Story.DialogueLine> aftermath;
        int aftermathStep;
        object aftermathFor;

        public bool DialoguePlaying => dialogueCatcher != null && dialogueCatcher.style.display == DisplayStyle.Flex;
        public string DialogueSpeaker => DialoguePlaying ? aftermath[aftermathStep].Speaker : null;
        public string PortraitSpeaker => dialogue?.PortraitSpeaker;
        Label hintLabel;
        float hintUntil;
        Label roundLabel, orderLabel, messageLabel, promptLabel;
        Button fastButton, autoButton, prepGo;
        // "End preparation and sortie?" (user 2026-10-22), opened by Start during preparation.
        VisualElement sortieConfirm;
        public bool SortieConfirmOpen => sortieConfirm != null && sortieConfirm.style.display == DisplayStyle.Flex;
        readonly Dictionary<BattleUnit, Card> cardOf = new Dictionary<BattleUnit, Card>();
        readonly Dictionary<BattleUnit, Plate> plateOf = new Dictionary<BattleUnit, Plate>();
        readonly List<(Label label, float until, Vector3 world)> floating = new List<(Label, float, Vector3)>();
        // What the bars show. The core resolves a whole round at once; during playback these follow the events
        // (each hit, heal or skill cost as it is played), and they snap to the live state otherwise.
        readonly Dictionary<BattleUnit, int> shownTroops = new Dictionary<BattleUnit, int>();
        readonly Dictionary<BattleUnit, int> shownTech = new Dictionary<BattleUnit, int>();

        int Troops(BattleUnit u) => shownTroops.TryGetValue(u, out int t) ? t : u.Troops;
        int Tech(BattleUnit u) => shownTech.TryGetValue(u, out int t) ? t : u.Tech;

        void SyncShown()
        {
            shownTroops.Clear(); shownTech.Clear();
            if (Controller?.Session == null) return;
            foreach (var u in Controller.Session.State.AllUnits) { shownTroops[u] = u.Troops; shownTech[u] = u.Tech; }
        }

        // Command entry state for the current actor.
        enum Step { Command, Skill, Item, Target, Hints }
        Step step;
        CommandKind pendingKind;
        SkillSpec pendingSkill;

        sealed class Card { public VisualElement Root, TroopFill, TechFill, Glow, Frame, Seal; public Label Name, TroopText, TechText, Status; }
        sealed class Plate { public VisualElement Root, Fill, NameBox, Mark; public Label Name; public bool Boss; }

        void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            skin = Skin;
            BattleFont.ApplyTo(root);
            root.style.flexGrow = 1;
            BuildLayout();
            root.RegisterCallback<NavigationCancelEvent>(e => { e.StopPropagation(); Cancel(); }, TrickleDown.TrickleDown);
            if (!Controller) return;
            Controller.BattleStarted += Rebuild;
            Controller.PreparationStarted += Refresh;
            Controller.Changed += Refresh;
            if (Tutor) Tutor.HintShown += ShowHint;
            Controller.EventPlayed += OnEvent;
            Controller.SortieRequested += AskSortie;
            if (Controller.Session != null) Rebuild();
        }

        void OnDisable()
        {
            if (!Controller) return;
            Controller.BattleStarted -= Rebuild;
            Controller.PreparationStarted -= Refresh;
            Controller.Changed -= Refresh;
            if (Tutor) Tutor.HintShown -= ShowHint;
            Controller.EventPlayed -= OnEvent;
            Controller.SortieRequested -= AskSortie;
        }

        // Auto battle on / off (user 2026-10-22); the command menu stays hidden while it is on.
        public void ToggleAuto()
        {
            Controller.Auto = !Controller.Auto;
            if (!Controller.Auto) step = Step.Command;
            Refresh();
        }

        public void AskSortie()
        {
            if (!Controller.CanConfirm || SortieConfirmOpen) return;
            sortieConfirm.Clear();
            sortieConfirm.Add(Text(BattleText.SortieQuestion, 28, Color.white));
            var row = Row(sortieConfirm); row.style.marginTop = 20; row.style.justifyContent = Justify.Center;
            var yes = MenuButton(row, BattleText.ConfirmSortie, () => { CloseSortie(); Controller.ConfirmPreparation(); }, 24, 220);
            var no = MenuButton(row, BattleText.KeepPreparing, CloseSortie, 24, 220);
            no.style.marginLeft = 16;
            sortieConfirm.style.display = DisplayStyle.Flex;
            yes.Focus();
        }

        void CloseSortie()
        {
            sortieConfirm.style.display = DisplayStyle.None;
            prepGo?.Focus();
        }

        // ---------- layout ----------

        void BuildLayout()
        {
            var top = Row(root); Absolute(top, 24, 20, 24, -1);
            top.style.justifyContent = Justify.SpaceBetween;
            var location = Framed(top, plaque: true); location.Add(Text(LocationName, 30, Color.white));
            var right = Row(top);
            autoButton = MenuButton(right, BattleText.Auto, ToggleAuto, 22, 120);
            autoButton.style.marginRight = 12;
            fastButton = MenuButton(right, BattleText.Fast, () => { Controller.Fast = !Controller.Fast; Refresh(); }, 22, 120);
            var roundBox = Framed(right, plaque: true); roundBox.style.marginLeft = 12;
            roundLabel = Text("", 28, Color.white); roundBox.Add(roundLabel);

            orderLabel = Text("", 20, new Color(0.95f, 0.92f, 0.85f));
            Absolute(orderLabel, 0, 92, 0, -1); orderLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            root.Add(orderLabel);

            messageLabel = Text("", 24, Color.white);
            Absolute(messageLabel, 380, 130, 380, -1);
            messageLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            messageLabel.style.backgroundColor = new Color(0, 0, 0, 0.45f);
            messageLabel.style.paddingTop = messageLabel.style.paddingBottom = 6;
            messageLabel.style.whiteSpace = WhiteSpace.Normal;
            root.Add(messageLabel);

            plates = new VisualElement { pickingMode = PickingMode.Ignore }; Absolute(plates, 0, 0, 0, 0); root.Add(plates);
            popups = new VisualElement { pickingMode = PickingMode.Ignore }; Absolute(popups, 0, 0, 0, 0); root.Add(popups);

            menu = Framed(root); Absolute(menu, 40, 330, -1, -1);
            menu.style.width = skin ? 320 : 300; menu.style.flexDirection = FlexDirection.Column;
            if (skin) menu.style.alignItems = Align.Center;   // 224-px options centred inside the panel's 40-px border

            promptLabel = Text(BattleText.ChooseCommand, 20, Gold);
            Absolute(promptLabel, 40, 290, -1, -1);
            root.Add(promptLabel);

            cards = Row(root); Absolute(cards, 40, -1, 40, 24);
            cards.style.justifyContent = Justify.SpaceBetween;

            dialogueCatcher = new Button(AdvanceDialogue) { text = "" };
            Absolute(dialogueCatcher, 0, 0, 0, 0);
            dialogueCatcher.style.backgroundColor = Color.clear;
            dialogueCatcher.style.borderTopWidth = dialogueCatcher.style.borderBottomWidth = dialogueCatcher.style.borderLeftWidth = dialogueCatcher.style.borderRightWidth = 0;
            dialogueCatcher.style.marginLeft = dialogueCatcher.style.marginRight = dialogueCatcher.style.marginTop = dialogueCatcher.style.marginBottom = 0;
            dialogueCatcher.style.display = DisplayStyle.None;
            root.Add(dialogueCatcher);
            dialogue = new ThreeKingdoms.UI.Story.DialogueView(root, skin, Portraits);

            resultPanel = Framed(root); Absolute(resultPanel, 560, 250, 560, -1);
            resultPanel.style.flexDirection = FlexDirection.Column;
            resultPanel.style.alignItems = Align.Center;
            resultPanel.style.display = DisplayStyle.None;
            resultPanel.style.backgroundColor = new Color(0.05f, 0.06f, 0.10f, 0.96f);
            resultPanel.style.paddingTop = resultPanel.style.paddingBottom = 24;

            // Teaching hint (game-ui rule 25): shown beside the command menu, never blocks a command.
            hintLabel = Text("", 22, Color.white);
            Absolute(hintLabel, 520, 190, 520, -1);   // under the battle message, clear of both lines of units
            hintLabel.style.whiteSpace = WhiteSpace.Normal;
            hintLabel.style.backgroundColor = new Color(0.10f, 0.08f, 0.03f, 0.92f);
            hintLabel.style.borderLeftWidth = 6; hintLabel.style.borderLeftColor = Gold;
            hintLabel.style.paddingLeft = hintLabel.style.paddingRight = 16; hintLabel.style.paddingTop = hintLabel.style.paddingBottom = 10;
            if (skin) { skin.Hint(hintLabel); hintLabel.style.paddingLeft = hintLabel.style.paddingRight = 28; hintLabel.style.paddingTop = hintLabel.style.paddingBottom = 18; }
            hintLabel.pickingMode = PickingMode.Ignore;
            hintLabel.style.display = DisplayStyle.None;
            root.Add(hintLabel);

            prepPanel = Framed(root); Absolute(prepPanel, 300, 150, 300, 150);
            prepPanel.style.flexDirection = FlexDirection.Column;
            prepPanel.style.backgroundColor = new Color(0.05f, 0.06f, 0.10f, 0.96f);
            prepPanel.style.display = DisplayStyle.None;
            sortieConfirm = Framed(root); Absolute(sortieConfirm, 620, 400, 620, -1);
            sortieConfirm.style.flexDirection = FlexDirection.Column; sortieConfirm.style.alignItems = Align.Center;
            sortieConfirm.style.backgroundColor = new Color(0.05f, 0.06f, 0.10f, 0.98f);
            sortieConfirm.style.paddingTop = sortieConfirm.style.paddingBottom = 28;
            sortieConfirm.style.display = DisplayStyle.None;
        }

        // ---------- teaching hints ----------

        public string CurrentHint => hintLabel.style.display == DisplayStyle.Flex ? hintLabel.text : null;

        void ShowHint(string key)
        {
            hintLabel.text = BattleText.Hint(key);
            hintLabel.style.display = DisplayStyle.Flex;
            hintUntil = Time.unscaledTime + 12f;
            // The first hint can arrive after the command menu was built: rebuild it so 「提示」 is offered at once.
            if (Controller.Current == BattleController.Phase.Commanding && step == Step.Command) BuildMenu();
        }

        // ---------- pre-battle preparation (game-ui GDD rules 16–17) ----------

        int prepFocus = -1;

        void ShowPreparation()
        {
            bool open = Controller.Panel == null;
            prepPanel.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            menu.style.display = promptLabel.style.display = resultPanel.style.display = DisplayStyle.None;
            cards.style.display = plates.style.display = DisplayStyle.None;
            orderLabel.text = "";
            messageLabel.text = Controller.EncounterName;
            if (!open) return;
            prepPanel.Clear();
            prepPanel.Add(Text(BattleText.Prepare, 36, Gold));
            var body = Row(prepPanel); body.style.flexGrow = 1; body.style.marginTop = 14;
            var left = new VisualElement(); left.style.width = new Length(55, LengthUnit.Percent); body.Add(left);
            var right = new VisualElement(); right.style.flexGrow = 1; right.style.marginLeft = 28; body.Add(right);

            left.Add(Text(BattleText.LineupHeader, 22, new Color(0.85f, 0.85f, 0.85f)));
            var available = Controller.Available.ToList();
            var buttons = new List<Button>();
            for (int i = 0; i < available.Count; i++)
            {
                var g = available[i];
                int index = i, slot = Controller.Selected.ToList().IndexOf(g);
                var stats = Controller.Progress.Stats(g, Controller.Progress.Inventory.Bonus(g.Id));
                string mark = slot >= 0 ? BattleText.SlotMark(slot) : BattleText.Standby;
                buttons.Add(MenuButton(left, $"{mark}　{BattleText.GeneralLine(g.DisplayName, g.Troop, Controller.Progress.Level(g), stats.MaxTroops)}",
                    () => { prepFocus = index; Controller.ToggleSelected(g); }, 24));
            }

            right.Add(Text(BattleText.IntelHeader, 22, new Color(0.85f, 0.85f, 0.85f)));
            foreach (var (g, level, troops) in Controller.EnemyIntel)
            {
                var line = Text(BattleText.GeneralLine(g.DisplayName, g.Troop, level, troops), 22, Color.white);
                line.style.marginTop = 10;
                right.Add(line);
            }

            var hint = Text(Controller.CanConfirm ? BattleText.PrepHint : BattleText.NeedOne, 20,
                Controller.CanConfirm ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.9f, 0.5f, 0.42f));
            hint.style.marginTop = 10; hint.style.whiteSpace = WhiteSpace.Normal;
            prepPanel.Add(hint);
            var actions = Row(prepPanel); actions.style.marginTop = 12;
            var outfit = MenuButton(actions, BattleText.Outfit, () => { prepFocus = -1; Controller.OpenPanel("outfit"); }, 24, 220);
            var go = MenuButton(actions, BattleText.ConfirmSortie, Controller.ConfirmPreparation, 24, 260, enabled: Controller.CanConfirm);
            go.style.marginLeft = 16;
            prepGo = Controller.CanConfirm ? go : outfit;
            // The cursor starts on 「确认出战」; the player moves off it to adjust (user 2026-10-22).
            (prepFocus >= 0 && prepFocus < buttons.Count ? buttons[prepFocus] : prepGo).Focus();
        }

        void Rebuild()
        {
            cards.Clear(); plates.Clear(); popups.Clear(); floating.Clear();
            cardOf.Clear(); plateOf.Clear();
            SyncShown();
            resultPanel.style.display = DisplayStyle.None;
            messageLabel.text = Controller.Session.Encounter.DisplayName;
            foreach (var u in Controller.Session.State.Players) cardOf[u] = MakeCard(u);
            var names = Controller.Session.State.Enemies.GroupBy(e => e.Name).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
            var counter = new Dictionary<string, int>();
            foreach (var u in Controller.Session.State.Enemies)
            {
                string label = u.Name;
                if (names.Contains(u.Name)) { counter.TryGetValue(u.Name, out int n); counter[u.Name] = n + 1; label += "甲乙丙丁"[n]; }
                plateOf[u] = MakePlate(label, boss: !u.Id.StartsWith("yt_"));   // named commanders get the boss plate
            }
            Refresh();
        }

        Card MakeCard(BattleUnit u)
        {
            var c = new Card { Root = Framed(cards) };
            c.Root.style.width = new Length(32, LengthUnit.Percent);
            c.Root.style.height = skin ? 72 * BattleUiSkin.Scale : 150;   // the 292×72 card at 2×
            c.Root.style.flexDirection = FlexDirection.Row;
            c.Root.style.paddingLeft = skin ? 16 : 14;
            if (skin)
            {
                c.Root.style.paddingTop = c.Root.style.paddingBottom = 12;
                // Selected card: the outer glow frame breathes (LateUpdate), no blur filter (BATTLE_UI_SKIN.md).
                c.Glow = new VisualElement { pickingMode = PickingMode.Ignore };
                Absolute(c.Glow, 0, 0, 0, 0);
                skin.Card(c.Glow, skin.CardActiveGlow);
                c.Glow.style.display = DisplayStyle.None;
                c.Root.Add(c.Glow);
            }
            var view = Controller.ViewOf(u);
            var portrait = new VisualElement();
            float side = skin ? 56 * BattleUiSkin.Scale : 120;
            portrait.style.width = side; portrait.style.height = side; portrait.style.marginRight = 16; portrait.style.flexShrink = 0;
            if (!skin)
            {
                portrait.style.borderTopWidth = portrait.style.borderBottomWidth = portrait.style.borderLeftWidth = portrait.style.borderRightWidth = 2;
                portrait.style.borderTopColor = portrait.style.borderBottomColor = portrait.style.borderLeftColor = portrait.style.borderRightColor = PanelEdge;
            }
            if (view && view.Portrait)
            {
                var r = view.PortraitCrop;
                var sprite = Sprite.Create(view.Portrait, new Rect(r.x, r.y, r.width, r.height), new Vector2(0.5f, 0.5f), 100f);
                portrait.style.backgroundImage = new StyleBackground(sprite);
            }
            c.Root.Add(portrait);
            if (skin)
            {
                c.Frame = new VisualElement { pickingMode = PickingMode.Ignore };
                Absolute(c.Frame, 0, 0, 0, 0);
                skin.Portrait(c.Frame, false);
                portrait.Add(c.Frame);
                // Routed seal over the portrait, clear of the name and numbers.
                c.Seal = new VisualElement { pickingMode = PickingMode.Ignore };
                BattleUiSkin.Image(c.Seal, skin.RoutedSeal);
                Absolute(c.Seal, 70 - 16, 54 - 12, -1, -1);
                c.Seal.style.display = DisplayStyle.None;
                c.Root.Add(c.Seal);
            }
            var info = new VisualElement(); info.style.flexGrow = 1; c.Root.Add(info);
            var head = Row(info);
            c.Name = Text($"{u.Name}   {BattleText.Level(u.Level)}", 26, Color.white); head.Add(c.Name);
            var icon = new VisualElement();
            int t = (int)u.Troop;
            if (skin)
            {
                // Troop icon on its 20×20 badge at the card's top right (266, 6) at 2×.
                var badge = new VisualElement { pickingMode = PickingMode.Ignore };
                BattleUiSkin.Image(badge, skin.UnitBadge);
                Absolute(badge, -1, 12, 12, -1);
                Absolute(icon, 4, 4, -1, -1); icon.style.width = icon.style.height = 32;
                badge.Add(icon);
                c.Root.Add(badge);
            }
            else
            {
                Absolute(icon, -1, -6, 4, -1); icon.style.width = 44; icon.style.height = 44;
                c.Root.Add(icon);
            }
            if (t < TroopIcons.Length && TroopIcons[t]) icon.style.backgroundImage = new StyleBackground(TroopIcons[t]);
            icon.tooltip = BattleText.Troop(u.Troop);
            (c.TroopText, c.TroopFill) = Bar(info, BattleText.Troops, new Color(0.35f, 0.78f, 0.36f));
            (c.TechText, c.TechFill) = Bar(info, BattleText.Tech, new Color(0.25f, 0.55f, 0.95f));
            c.Status = Text("", 16, new Color(1f, 0.8f, 0.6f)); info.Add(c.Status);
            return c;
        }

        Plate MakePlate(string name, bool boss)
        {
            if (skin && skin.HasEnemyPlates) return MakeSkinnedPlate(name, boss);
            var p = new Plate { Root = new VisualElement { pickingMode = PickingMode.Ignore } };
            p.Root.style.position = Position.Absolute;
            p.Root.style.width = 150;
            p.Name = Text(name, 18, Color.white); p.Name.style.unityTextAlign = TextAnchor.MiddleCenter;
            p.Name.style.unityTextOutlineWidth = 1; p.Name.style.unityTextOutlineColor = Color.black;
            p.Root.Add(p.Name);
            var bar = new VisualElement(); bar.style.height = 8; bar.style.backgroundColor = new Color(0, 0, 0, 0.7f);
            p.Fill = new VisualElement(); p.Fill.style.height = 8; p.Fill.style.backgroundColor = new Color(0.86f, 0.3f, 0.22f);
            bar.Add(p.Fill); p.Root.Add(bar);
            plates.Add(p.Root);
            return p;
        }

        // Enemy nameplate 70×14 and troop bar 70×6 at 2× (BATTLE_UI_ENEMY_SKIN.md): white name on the plate, the fill in
        // the groove at (3, 2), striped below half, the 退 seal over a routed enemy.
        Plate MakeSkinnedPlate(string name, bool boss)
        {
            const float scale = BattleUiSkin.Scale;
            var p = new Plate { Root = new VisualElement { pickingMode = PickingMode.Ignore }, Boss = boss };
            p.Root.style.position = Position.Absolute;
            p.Root.style.width = 70 * scale;
            p.NameBox = new VisualElement { pickingMode = PickingMode.Ignore };
            p.NameBox.style.height = 14 * scale;
            p.NameBox.style.justifyContent = Justify.Center;
            skin.EnemyName(p.NameBox, boss ? skin.EnemyPlateBoss : skin.EnemyPlate);
            p.Name = Text(name, 18, Color.white); p.Name.style.unityTextAlign = TextAnchor.MiddleCenter;
            p.NameBox.Add(p.Name);
            p.Root.Add(p.NameBox);
            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.style.height = 6 * scale; bar.style.marginTop = 2;
            skin.EnemyBar(bar);
            var inner = new VisualElement { pickingMode = PickingMode.Ignore }; Absolute(inner, 3 * scale, 2 * scale, 3 * scale, 2 * scale);
            p.Fill = new VisualElement { pickingMode = PickingMode.Ignore };
            p.Fill.style.height = new Length(100, LengthUnit.Percent);
            BattleUiSkin.Fill(p.Fill, skin.EnemyBarFill);
            inner.Add(p.Fill); bar.Add(inner); p.Root.Add(bar);
            p.Mark = new VisualElement { pickingMode = PickingMode.Ignore };
            BattleUiSkin.Image(p.Mark, skin.EnemyRouted);
            Absolute(p.Mark, -1, -4, -6, -1);
            p.Mark.style.display = DisplayStyle.None;
            p.Root.Add(p.Mark);
            plates.Add(p.Root);
            return p;
        }

        (Label, VisualElement) Bar(VisualElement parent, string caption, Color fillColor)
        {
            var row = Row(parent); row.style.alignItems = Align.Center; row.style.marginTop = 8;
            var cap = Text(caption, 20, new Color(0.85f, 0.85f, 0.85f)); cap.style.width = 48; row.Add(cap);
            var value = Text("", 22, Color.white); value.style.width = 110; row.Add(value);
            var track = new VisualElement(); track.style.flexGrow = 1; track.style.height = 14; track.style.marginRight = 20;
            track.style.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
            var fill = new VisualElement(); fill.style.height = 14; fill.style.backgroundColor = fillColor;
            if (skin)
            {
                // Bar frame 48×8 (slices 4/4/2/2) at 2×; the fill tiles inside its 2 px border.
                skin.Bar(track);
                track.style.height = 8 * BattleUiSkin.Scale; track.style.marginRight = 28;
                var inner = new VisualElement(); Absolute(inner, 4, 4, 4, 4);
                fill.style.height = new Length(100, LengthUnit.Percent);
                BattleUiSkin.Fill(fill, caption == BattleText.Tech ? skin.BarSkill : skin.BarTroops);
                inner.Add(fill); track.Add(inner); row.Add(track);
                return (value, fill);
            }
            track.Add(fill); row.Add(track);
            return (value, fill);
        }

        // ---------- refresh ----------

        void Refresh()
        {
            if (Controller.Current == BattleController.Phase.Preparing) { ShowPreparation(); return; }
            prepPanel.style.display = DisplayStyle.None;
            cards.style.display = plates.style.display = DisplayStyle.Flex;
            if (Controller.Session == null) return;
            var state = Controller.Session.State;
            if (Controller.Current != BattleController.Phase.Playback) SyncShown();
            roundLabel.text = BattleText.Round(state.Round);
            if (skin) skin.Item(fastButton, Controller.Fast ? skin.ItemSelected : skin.ItemNormal);
            else fastButton.style.backgroundColor = Controller.Fast ? new Color(0.5f, 0.38f, 0.12f) : Panel;
            orderLabel.text = BattleText.Order(string.Join(" → ", state.ActionOrder().Select(u => u.Name)));
            foreach (var pair in cardOf) UpdateCard(pair.Key, pair.Value);
            bool commanding = Controller.Current == BattleController.Phase.Commanding;
            if (commanding && step != Step.Command && pendingActor != Controller.Actor) step = Step.Command;
            pendingActor = Controller.Actor;
            menu.style.display = commanding && !Controller.Auto ? DisplayStyle.Flex : DisplayStyle.None;
            if (skin) skin.Item(autoButton, Controller.Auto ? skin.ItemSelected : skin.ItemNormal);
            else autoButton.style.backgroundColor = Controller.Auto ? new Color(0.5f, 0.38f, 0.12f) : Panel;
            promptLabel.style.display = menu.style.display;
            if (commanding) BuildMenu();
            if (Controller.Current == BattleController.Phase.Finished && Controller.Session.Result != null)
            {
                if (AftermathDue()) PlayAftermath();
                else if (!DialoguePlaying) ShowResult();
            }
            else resultPanel.style.display = DisplayStyle.None;
        }

        BattleUnit pendingActor;

        void UpdateCard(BattleUnit u, Card c)
        {
            c.Name.text = $"{u.Name}   {BattleText.Level(u.Level)}";
            int troops = Troops(u), tech = Tech(u);
            c.TroopText.text = $"{troops}/{u.Stats.MaxTroops}";
            c.TechText.text = $"{tech}/{u.Stats.MaxTech}";
            c.TroopFill.style.width = new Length(100f * troops / u.Stats.MaxTroops, LengthUnit.Percent);
            c.TechFill.style.width = new Length(100f * tech / Mathf.Max(1, u.Stats.MaxTech), LengthUnit.Percent);
            var commanded = Controller.Current == BattleController.Phase.Commanding && Controller.Commands.TryGetValue(u, out var cmd) ? $"  已选：{BattleText.Command(cmd.Kind)}" : "";
            c.Status.text = string.Join("  ", u.Modifiers.Select(BattleText.Remaining)) + commanded;
            bool selected = Controller.Actor == u;
            if (skin)
            {
                // Four states with different frames, not colour alone: active > wounded > normal > routed.
                bool routed = troops <= 0, wounded = troops * 2 < u.Stats.MaxTroops;
                skin.Card(c.Root, routed ? skin.CardRouted : selected ? skin.CardActive : wounded ? skin.CardWounded : skin.CardNormal);
                BattleUiSkin.Fill(c.TroopFill, wounded ? skin.BarTroopsLow : skin.BarTroops);
                if (c.Frame != null) skin.Portrait(c.Frame, selected);
                if (c.Seal != null) c.Seal.style.display = routed ? DisplayStyle.Flex : DisplayStyle.None;
                if (c.Glow != null) c.Glow.style.display = selected && !routed ? DisplayStyle.Flex : DisplayStyle.None;
                c.Root.style.opacity = 1f;
                return;
            }
            var edge = selected ? Gold : PanelEdge;
            c.Root.style.borderTopColor = c.Root.style.borderBottomColor = c.Root.style.borderLeftColor = c.Root.style.borderRightColor = edge;
            c.Root.style.borderTopWidth = c.Root.style.borderBottomWidth = c.Root.style.borderLeftWidth = c.Root.style.borderRightWidth = selected ? 4 : 2;
            c.Root.style.opacity = troops > 0 ? 1f : 0.45f;
        }

        void BuildMenu()
        {
            menu.Clear();
            var actor = Controller.Actor;
            var state = Controller.Session.State;
            if (actor == null) return;
            switch (step)
            {
                case Step.Command:
                    promptLabel.text = $"{actor.Name}：{BattleText.ChooseCommand}";
                    MenuButton(menu, BattleText.Command(CommandKind.Attack), () => PickTarget(CommandKind.Attack, null), card: "attack");
                    MenuButton(menu, BattleText.Command(CommandKind.Skill), () => { step = Step.Skill; BuildMenu(); }, enabled: actor.Skills.Count > 0, card: "skill");
                    var defend = MenuButton(menu, BattleText.Command(CommandKind.Defend), () => Controller.Choose(BattleCommand.Defend(actor)), card: "defend");
                    defend.tooltip = BattleText.DefendHelp;
                    MenuButton(menu, BattleText.Command(CommandKind.Item), () => { step = Step.Item; BuildMenu(); }, enabled: state.CanUseRation(actor), card: "item");
                    if (Tutor && Tutor.Seen.Count > 0) MenuButton(menu, BattleText.Hints, () => { step = Step.Hints; BuildMenu(); }, 20, card: "hints");
                    if (Controller.Commands.Count > 0) MenuButton(menu, BattleText.Back, () => { step = Step.Command; Controller.Undo(); }, 20, card: "back");
                    break;
                case Step.Hints:
                    // Every hint seen so far can be read again (game-ui answer 5).
                    promptLabel.text = BattleText.Hints;
                    foreach (var key in Tutor.Seen.ToList())
                    {
                        var k = key;
                        MenuButton(menu, BattleText.HintTitle(k), () => ShowHint(k), 20);
                    }
                    MenuButton(menu, BattleText.Back, () => { step = Step.Command; BuildMenu(); }, 20);
                    break;
                case Step.Skill:
                    promptLabel.text = $"{actor.Name}：{BattleText.Command(CommandKind.Skill)}";
                    foreach (var skill in actor.Skills)
                    {
                        var s = skill;
                        var b = MenuButton(menu, $"{s.DisplayName}  {BattleText.SkillCost(s.TechCost)}", () =>
                        {
                            if (s.Target == SkillTarget.AllEnemies) { step = Step.Command; Controller.Choose(BattleCommand.UseSkill(actor, s)); }
                            else PickTarget(CommandKind.Skill, s);
                        }, 22, enabled: state.CanUseSkill(actor, s));
                        b.tooltip = s.Description;
                    }
                    MenuButton(menu, BattleText.Back, () => { step = Step.Command; BuildMenu(); }, 20);
                    break;
                case Step.Item:
                    promptLabel.text = $"{actor.Name}：{BattleText.Command(CommandKind.Item)}";
                    MenuButton(menu, BattleText.Ration(state.RationItem.DisplayName, state.Rations), () => PickTarget(CommandKind.Item, null), 22, enabled: state.CanUseRation(actor));
                    MenuButton(menu, BattleText.Back, () => { step = Step.Command; BuildMenu(); }, 20);
                    break;
                case Step.Target:
                    promptLabel.text = $"{actor.Name}：{BattleText.ChooseTarget}";
                    foreach (var t in state.LegalTargets(actor, pendingKind, pendingSkill))
                    {
                        var target = t;
                        string adv = target.Side != actor.Side && state.HasAdvantage(actor, target) && pendingKind != CommandKind.Item &&
                                     (pendingSkill == null || pendingSkill.Damage != DamageKind.None) ? "  " + BattleText.Advantaged : "";
                        string label = (plateOf.TryGetValue(target, out var p) ? p.Name.text : target.Name) + $"  {target.Troops}/{target.Stats.MaxTroops}{adv}";
                        var b = MenuButton(menu, label, () => Commit(target), 20);
                        b.RegisterCallback<FocusInEvent>(_ => { Controller.PreviewTarget(target); HighlightPlate(target); });
                        b.RegisterCallback<FocusOutEvent>(_ => { Controller.PreviewTarget(null); HighlightPlate(null); });
                    }
                    MenuButton(menu, BattleText.Back, () => { step = Step.Command; BuildMenu(); }, 20);
                    break;
            }
            // Text options (skills, targets, hints) take the command cards' width, centred inside the panel's border.
            if (skin) foreach (var item in menu.Query<Button>().ToList()) item.style.width = 112 * BattleUiSkin.Scale;
            menu.Query<Button>().Where(b => b.enabledSelf).First()?.Focus();
        }

        // Cancel (gamepad B / Esc): sub-menu → command menu → previous general's command.
        void Cancel()
        {
            if (SortieConfirmOpen) { CloseSortie(); return; }
            if (Controller == null || Controller.Current != BattleController.Phase.Commanding) return;
            Controller.PreviewTarget(null); HighlightPlate(null);
            if (step != Step.Command) { step = Step.Command; BuildMenu(); }
            else if (Controller.Commands.Count > 0) Controller.Undo();
        }

        void HighlightPlate(BattleUnit unit)
        {
            foreach (var pair in plateOf)
            {
                if (pair.Value.NameBox != null)
                    skin.EnemyName(pair.Value.NameBox, pair.Key == unit && skin.EnemyPlateTarget ? skin.EnemyPlateTarget : pair.Value.Boss ? skin.EnemyPlateBoss : skin.EnemyPlate);
                else pair.Value.Name.style.color = pair.Key == unit ? Gold : Color.white;
            }
        }

        void PickTarget(CommandKind kind, SkillSpec skill)
        {
            pendingKind = kind; pendingSkill = skill; step = Step.Target;
            BuildMenu();
        }

        void Commit(BattleUnit target)
        {
            var actor = Controller.Actor;
            step = Step.Command;
            Controller.PreviewTarget(null); HighlightPlate(null);
            Controller.Choose(pendingKind switch
            {
                CommandKind.Attack => BattleCommand.Attack(actor, target),
                CommandKind.Item => BattleCommand.Ration(actor, target),
                _ => BattleCommand.UseSkill(actor, pendingSkill, target),
            });
        }

        // Only once per battle, and only for the victory that advanced the story.
        bool AftermathDue()
        {
            if (aftermathFor == Controller.Session || Controller.Session.Result.Outcome != BattleOutcome.Victory) return false;
            if (Controller.LastApplied == null || !Controller.LastApplied.RewardGranted) return false;
            return ThreeKingdoms.Gameplay.Story.BattleScenes.AfterVictory(Controller.Session.Encounter.Id).Count > 0;
        }

        void PlayAftermath()
        {
            aftermathFor = Controller.Session;
            aftermath = ThreeKingdoms.Gameplay.Story.BattleScenes.AfterVictory(Controller.Session.Encounter.Id);
            aftermathStep = 0;
            resultPanel.style.display = DisplayStyle.None;
            dialogueCatcher.style.display = DisplayStyle.Flex;
            dialogue.Show(aftermath[0].Speaker, aftermath[0].Line);
            dialogueCatcher.Focus();
        }

        public void AdvanceDialogue()
        {
            if (!DialoguePlaying) return;
            if (++aftermathStep < aftermath.Count) { dialogue.Show(aftermath[aftermathStep].Speaker, aftermath[aftermathStep].Line); return; }
            dialogue.Hide();
            dialogueCatcher.style.display = DisplayStyle.None;
            ShowResult();
        }

        void ShowResult()
        {
            resultPanel.Clear();
            resultPanel.style.display = DisplayStyle.Flex;
            var result = Controller.Session.Result;
            bool won = result.Outcome == BattleOutcome.Victory;
            resultPanel.Add(Text(won ? BattleText.Victory : BattleText.Defeat, 44, won ? Gold : new Color(0.85f, 0.4f, 0.35f)));
            var lines = new List<string>();
            var applied = Controller.LastApplied;
            if (!won) lines.Add(BattleText.DefeatHint);
            else if (applied != null && applied.RewardGranted)
            {
                lines.Add(BattleText.ExpGain(result.Exp));
                lines.Add(BattleText.CopperGain(result.Copper));
                foreach (var up in applied.LevelUps)
                {
                    lines.Add(BattleText.LevelUp(up.general.DisplayName, up.to));
                    foreach (var s in up.learned) lines.Add(BattleText.Learned(up.general.DisplayName, s.DisplayName));
                }
            }
            else lines.Add(BattleText.NoRewardAgain);
            foreach (var l in lines) { var t = Text(l, 22, Color.white); t.style.marginTop = 6; resultPanel.Add(t); }
            var buttons = new VisualElement(); buttons.style.flexDirection = FlexDirection.Row; buttons.style.marginTop = 18;
            resultPanel.Add(buttons);
            Button first = null;
            if (Controller.ReachedEnding) first = MenuButton(buttons, BattleText.Continue, ShowEnding, 24, 220);
            else if (Controller.CanReturnToTown) first = MenuButton(buttons, BattleText.ReturnToTown, Controller.ReturnToTown, 24, 220);
            else if (Controller.HasNextBattle) first = MenuButton(buttons, BattleText.NextBattle, Controller.GoToNextBattle, 24, 220);
            if (!won || Controller.Run.IsDevelopment)
            {
                // A play-through retry restores the pre-battle state and goes back to preparation (save-system rule 11).
                Action retry = !won && Controller.NeedsPreparation ? Controller.BeginPreparation : (Action)Controller.StartBattle;
                var again = MenuButton(buttons, won ? BattleText.Again : BattleText.Retry, retry, 24, 220);
                again.style.marginLeft = first != null ? 16 : 0;
                first ??= again;
            }
            if (!won && !Controller.Run.IsDevelopment)
                MenuButton(buttons, BattleText.ToTitle, Controller.ReturnToTitle, 24, 220).style.marginLeft = 16;
            first.Focus();
        }

        // Alpha 0.1 ending (story-events rule 19, answer 5): a couple of narration lines, then "test version ends".
        int endingStep;

        void ShowEnding()
        {
            Controller.BeginEnding();
            endingStep = 0;
            ShowEndingStep();
        }

        void ShowEndingStep()
        {
            resultPanel.Clear();
            resultPanel.style.display = DisplayStyle.Flex;
            resultPanel.style.left = 0; resultPanel.style.right = 0; resultPanel.style.top = 0; resultPanel.style.bottom = 0;
            resultPanel.style.justifyContent = Justify.Center;
            resultPanel.style.backgroundColor = new Color(0.02f, 0.02f, 0.03f, 0.97f);
            menu.style.display = DisplayStyle.None;
            bool narration = endingStep < BattleText.EndingNarration.Length;
            if (narration)
            {
                var line = Text(BattleText.EndingNarration[endingStep], 30, new Color(0.95f, 0.92f, 0.85f));
                line.style.whiteSpace = WhiteSpace.Normal; line.style.maxWidth = 1100; line.style.unityTextAlign = TextAnchor.MiddleCenter;
                resultPanel.Add(line);
                var next = MenuButton(resultPanel, BattleText.Continue, () => { endingStep++; ShowEndingStep(); }, 24, 220);
                next.style.marginTop = 40;
                next.Focus();
                return;
            }
            Controller.FinishEnding();
            resultPanel.Add(Text(BattleText.TestVersionEnd, 48, Gold));
            var thanks = Text(BattleText.ThanksForPlaying, 24, Color.white); thanks.style.marginTop = 16; resultPanel.Add(thanks);
            var restart = MenuButton(resultPanel, BattleText.ToTitle, Controller.ReturnToTitle, 24, 220);
            restart.style.marginTop = 40;
            restart.Focus();
        }

        // ---------- events & per-frame ----------

        void OnEvent(BattleEvent e)
        {
            if (e.Actor != null) shownTech[e.Actor] = e.ActorTechAfter;
            if (e.Target != null && e.Kind != BattleEventKind.ActionStarted && e.Kind != BattleEventKind.Cancelled)
                shownTroops[e.Target] = e.TargetTroopsAfter;
            bool quiet = e.Kind == BattleEventKind.RoundEnded || (e.Kind == BattleEventKind.ActionStarted && e.Command == CommandKind.Attack);
            if (!quiet) messageLabel.text = BattleText.Describe(e);
            switch (e.Kind)
            {
                case BattleEventKind.Damage: Popup(e.Target, $"-{e.Amount}", e.Advantage ? Gold : Color.white, e.Advantage ? 40 : 32); break;
                case BattleEventKind.Heal:
                case BattleEventKind.ItemUsed: Popup(e.Target, $"+{e.Amount}", new Color(0.5f, 1f, 0.55f), 32); break;
                case BattleEventKind.ModifierApplied: Popup(e.Target, $"{BattleText.Stat(e.Modifier.Stat)}{e.ModifierPercent}%", new Color(0.7f, 0.8f, 1f), 24); break;
            }
            foreach (var pair in cardOf) UpdateCard(pair.Key, pair.Value);
            roundLabel.text = BattleText.Round(Controller.Session.State.Round);
        }

        void Popup(BattleUnit unit, string text, Color color, int size)
        {
            var view = Controller.ViewOf(unit);
            if (!view) return;
            var label = Text(text, size, color);
            label.style.position = Position.Absolute;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.unityTextOutlineWidth = 2; label.style.unityTextOutlineColor = Color.black;
            popups.Add(label);
            floating.Add((label, Time.time + 1.1f, view.transform.position + Vector3.up * 1.9f));
        }

        // Gamepad / keyboard navigation goes through the EventSystem's selection. Focusing a button before the
        // panel's event handler exists (first frames) leaves nothing selected, so keep the menu focused until it is.
        void Update()
        {
            dialogue?.Tick();
            if (Controller == null) return;
            if (DialoguePlaying)
            {
                var focused = root.panel?.focusController.focusedElement;
                var current = EventSystem.current;
                if (focused != dialogueCatcher || current == null || current.currentSelectedGameObject == null) { dialogueCatcher.Blur(); dialogueCatcher.Focus(); }
                return;
            }
            bool preparing = Controller.Current == BattleController.Phase.Preparing;
            if (preparing && Controller.Panel != null) return;   // the outfit panel owns focus
            if (!preparing && Controller.Current == BattleController.Phase.Commanding && Controller.Auto) return;
            if (!preparing && Controller.Current != BattleController.Phase.Commanding && Controller.Current != BattleController.Phase.Finished) return;
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject != null && root.panel?.focusController.focusedElement != null) return;
            var target = preparing ? (SortieConfirmOpen ? sortieConfirm : prepPanel) : resultPanel.style.display == DisplayStyle.Flex ? (VisualElement)resultPanel : menu;
            var button = preparing && !SortieConfirmOpen && prepGo != null && prepFocus < 0 ? prepGo : target.Query<Button>().Where(b => b.enabledSelf).First();
            if (button == null) return;
            button.Blur();     // a focus that happened before the handler existed must be re-sent
            button.Focus();
        }

        void LateUpdate()
        {
            if (skin)
            {
                float breathe = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 3f);
                foreach (var c in cardOf.Values) if (c.Glow != null) c.Glow.style.opacity = breathe;
            }
            if (hintLabel != null && hintLabel.style.display == DisplayStyle.Flex && Time.unscaledTime > hintUntil) hintLabel.style.display = DisplayStyle.None;
            if (Controller == null || Controller.Session == null || root?.panel == null || !BattleCamera) return;
            foreach (var pair in plateOf)
            {
                var view = Controller.ViewOf(pair.Key);
                var plate = pair.Value;
                int troops = Troops(pair.Key);
                // A skinned plate stays up with its 退 seal while the routed unit plays its retreat.
                bool show = view && view.gameObject.activeInHierarchy && (troops > 0 || plate.Mark != null);
                plate.Root.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (!show) continue;
                var p = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, view.transform.position + Vector3.up * 1.75f, BattleCamera);
                float ratio = (float)troops / pair.Key.Stats.MaxTroops;
                if (plate.Mark != null)
                {
                    plate.Root.style.left = p.x - 70; plate.Root.style.top = p.y - 46;
                    // Whole native pixels of the 64-px groove, at 2×.
                    plate.Fill.style.width = Mathf.Floor(64 * ratio) * BattleUiSkin.Scale;
                    BattleUiSkin.Fill(plate.Fill, ratio < 0.5f && skin.EnemyBarFillLow ? skin.EnemyBarFillLow : skin.EnemyBarFill);
                    plate.Mark.style.display = troops <= 0 ? DisplayStyle.Flex : DisplayStyle.None;
                    continue;
                }
                plate.Root.style.left = p.x - 75; plate.Root.style.top = p.y - 34;
                plate.Fill.style.width = new Length(100f * ratio, LengthUnit.Percent);
            }
            for (int i = floating.Count - 1; i >= 0; i--)
            {
                var (label, until, world) = floating[i];
                float left = until - Time.time;
                if (left <= 0f) { label.RemoveFromHierarchy(); floating.RemoveAt(i); continue; }
                var p = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, world + Vector3.up * (1.1f - left) * 0.8f, BattleCamera);
                label.style.left = p.x - 40; label.style.top = p.y;
                label.style.opacity = Mathf.Clamp01(left * 3f);
            }
        }

        // ---------- helpers ----------

        static Label Text(string text, int size, Color color)
        {
            var l = new Label(text);
            l.style.fontSize = size; l.style.color = color;
            l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
            return l;
        }

        static VisualElement Row(VisualElement parent)
        {
            var e = new VisualElement(); e.style.flexDirection = FlexDirection.Row;
            parent.Add(e);
            return e;
        }

        static VisualElement Framed(VisualElement parent, bool plaque = false)
        {
            var e = new VisualElement();
            e.style.backgroundColor = Panel;
            e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = 2;
            e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = PanelEdge;
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = 4;
            e.style.paddingLeft = e.style.paddingRight = 18; e.style.paddingTop = e.style.paddingBottom = 10;
            if (skin)
            {
                if (plaque) { skin.Plaque(e); e.style.paddingLeft = e.style.paddingRight = 28; e.style.paddingTop = e.style.paddingBottom = 14; }
                // Content starts inside the 20-px (40 at 2×) border art so option frames are never covered by it.
                else { skin.Panel(e); e.style.paddingLeft = e.style.paddingRight = 40; e.style.paddingTop = e.style.paddingBottom = 36; }
            }
            parent.Add(e);
            return e;
        }

        static void Absolute(VisualElement e, float left, float top, float right, float bottom)
        {
            e.style.position = Position.Absolute;
            if (left >= 0) e.style.left = left;
            if (top >= 0) e.style.top = top;
            if (right >= 0) e.style.right = right;
            if (bottom >= 0) e.style.bottom = bottom;
        }

        static Button MenuButton(VisualElement parent, string text, Action onClick, int size = 26, float width = -1, bool enabled = true, string card = null)
        {
            // Fixed commands are whole cards with the label drawn in (user 2026-10-21): swap normal / selected /
            // disabled images; the text stays on the button (for lookups and accessibility) but is not drawn.
            if (skin && card != null && skin.TryCommand(card, out var normal, out var selected, out var disabled))
            {
                var c = new Button(onClick) { text = text };
                c.style.width = 112 * BattleUiSkin.Scale; c.style.height = 24 * BattleUiSkin.Scale;
                c.style.marginTop = 4; c.style.marginLeft = c.style.marginRight = 0;
                c.style.paddingLeft = c.style.paddingRight = c.style.paddingTop = c.style.paddingBottom = 0;
                c.style.borderTopWidth = c.style.borderBottomWidth = c.style.borderLeftWidth = c.style.borderRightWidth = 0;
                c.style.color = Color.clear; c.style.fontSize = 1;
                c.SetEnabled(enabled);
                BattleUiSkin.CommandCard(c, enabled ? normal : disabled);
                c.RegisterCallback<FocusInEvent>(_ => BattleUiSkin.CommandCard(c, selected));
                c.RegisterCallback<FocusOutEvent>(_ => BattleUiSkin.CommandCard(c, c.enabledSelf ? normal : disabled));
                c.RegisterCallback<PointerEnterEvent>(_ => { if (c.enabledSelf) c.Focus(); });
                parent.Add(c);
                return c;
            }
            var b = new Button(onClick) { text = text };
            b.style.fontSize = size;
            b.style.color = enabled ? Color.white : new Color(0.5f, 0.5f, 0.5f);
            b.style.backgroundColor = new Color(0.12f, 0.15f, 0.22f, 0.95f);
            b.style.borderTopColor = b.style.borderBottomColor = b.style.borderLeftColor = b.style.borderRightColor = new Color(0.35f, 0.3f, 0.2f);
            b.style.unityTextAlign = TextAnchor.MiddleLeft;
            b.style.paddingLeft = 16; b.style.height = size + 22; b.style.marginTop = 4;
            if (width > 0) b.style.width = width;
            b.SetEnabled(enabled);
            // Visible focus for keyboard / gamepad; the mouse moves focus on hover so both stay in sync.
            var idle = new Color(0.12f, 0.15f, 0.22f, 0.95f);
            var dim = new Color(0.35f, 0.3f, 0.2f);
            if (skin)
            {
                // Command item frames (64×24, slices 14/6/6/6) at 2×; the selected frame carries its own small cursor.
                var shown = skin;
                shown.Item(b, enabled ? shown.ItemNormal : shown.ItemDisabled);
                b.style.paddingLeft = 34; b.style.height = Mathf.Max(48, size + 22);
                b.RegisterCallback<FocusInEvent>(_ => shown.Item(b, shown.ItemSelected));
                b.RegisterCallback<FocusOutEvent>(_ => shown.Item(b, b.enabledSelf ? shown.ItemNormal : shown.ItemDisabled));
            }
            else
            {
                b.RegisterCallback<FocusInEvent>(_ =>
                {
                    b.style.backgroundColor = new Color(0.45f, 0.33f, 0.10f, 0.98f);
                    b.style.borderTopColor = b.style.borderBottomColor = b.style.borderLeftColor = b.style.borderRightColor = Gold;
                    b.style.borderLeftWidth = 6;
                });
                b.RegisterCallback<FocusOutEvent>(_ =>
                {
                    b.style.backgroundColor = idle;
                    b.style.borderTopColor = b.style.borderBottomColor = b.style.borderLeftColor = b.style.borderRightColor = dim;
                    b.style.borderLeftWidth = 1;
                });
            }
            b.RegisterCallback<PointerEnterEvent>(_ => { if (b.enabledSelf) b.Focus(); });
            parent.Add(b);
            return b;
        }
    }
}
