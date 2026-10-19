using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Battle;
using ThreeKingdoms.Gameplay.Battle;
using UnityEngine;
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
        public Camera BattleCamera;
        public string LocationName = "涿郡 · 街市";
        [Tooltip("Infantry, Cavalry, Archer, YellowTurban, Strategist")] public Texture2D[] TroopIcons = Array.Empty<Texture2D>();

        static readonly Color Gold = new Color(0.93f, 0.76f, 0.36f);
        static readonly Color Panel = new Color(0.07f, 0.09f, 0.14f, 0.88f);
        static readonly Color PanelEdge = new Color(0.62f, 0.50f, 0.28f);

        VisualElement root, menu, cards, plates, popups, resultPanel;
        Label roundLabel, orderLabel, messageLabel, promptLabel;
        Button fastButton;
        readonly Dictionary<BattleUnit, Card> cardOf = new Dictionary<BattleUnit, Card>();
        readonly Dictionary<BattleUnit, Plate> plateOf = new Dictionary<BattleUnit, Plate>();
        readonly List<(Label label, float until, Vector3 world)> floating = new List<(Label, float, Vector3)>();

        // Command entry state for the current actor.
        enum Step { Command, Skill, Item, Target }
        Step step;
        CommandKind pendingKind;
        SkillSpec pendingSkill;

        sealed class Card { public VisualElement Root, TroopFill, TechFill; public Label Name, TroopText, TechText, Status; }
        sealed class Plate { public VisualElement Root, Fill; public Label Name; }

        void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            BattleFont.ApplyTo(root);
            root.style.flexGrow = 1;
            BuildLayout();
            root.RegisterCallback<NavigationCancelEvent>(e => { e.StopPropagation(); Cancel(); }, TrickleDown.TrickleDown);
            if (!Controller) return;
            Controller.BattleStarted += Rebuild;
            Controller.Changed += Refresh;
            Controller.EventPlayed += OnEvent;
            if (Controller.Session != null) Rebuild();
        }

        void OnDisable()
        {
            if (!Controller) return;
            Controller.BattleStarted -= Rebuild;
            Controller.Changed -= Refresh;
            Controller.EventPlayed -= OnEvent;
        }

        // ---------- layout ----------

        void BuildLayout()
        {
            var top = Row(root); Absolute(top, 24, 20, 24, -1);
            top.style.justifyContent = Justify.SpaceBetween;
            var location = Framed(top); location.Add(Text(LocationName, 30, Color.white));
            var right = Row(top);
            fastButton = MenuButton(right, BattleText.Fast, () => { Controller.Fast = !Controller.Fast; Refresh(); }, 22, 120);
            var roundBox = Framed(right); roundBox.style.marginLeft = 12;
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
            menu.style.width = 300; menu.style.flexDirection = FlexDirection.Column;

            promptLabel = Text(BattleText.ChooseCommand, 20, Gold);
            Absolute(promptLabel, 40, 290, -1, -1);
            root.Add(promptLabel);

            cards = Row(root); Absolute(cards, 40, -1, 40, 24);
            cards.style.justifyContent = Justify.SpaceBetween;

            resultPanel = Framed(root); Absolute(resultPanel, 560, 250, 560, -1);
            resultPanel.style.flexDirection = FlexDirection.Column;
            resultPanel.style.alignItems = Align.Center;
            resultPanel.style.display = DisplayStyle.None;
            resultPanel.style.backgroundColor = new Color(0.05f, 0.06f, 0.10f, 0.96f);
            resultPanel.style.paddingTop = resultPanel.style.paddingBottom = 24;
        }

        void Rebuild()
        {
            cards.Clear(); plates.Clear(); popups.Clear(); floating.Clear();
            cardOf.Clear(); plateOf.Clear();
            resultPanel.style.display = DisplayStyle.None;
            messageLabel.text = Controller.Session.Encounter.DisplayName;
            foreach (var u in Controller.Session.State.Players) cardOf[u] = MakeCard(u);
            var names = Controller.Session.State.Enemies.GroupBy(e => e.Name).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
            var counter = new Dictionary<string, int>();
            foreach (var u in Controller.Session.State.Enemies)
            {
                string label = u.Name;
                if (names.Contains(u.Name)) { counter.TryGetValue(u.Name, out int n); counter[u.Name] = n + 1; label += "甲乙丙丁"[n]; }
                plateOf[u] = MakePlate(label);
            }
            Refresh();
        }

        Card MakeCard(BattleUnit u)
        {
            var c = new Card { Root = Framed(cards) };
            c.Root.style.width = new Length(32, LengthUnit.Percent);
            c.Root.style.height = 150;
            c.Root.style.flexDirection = FlexDirection.Row;
            c.Root.style.paddingLeft = 14;
            var view = Controller.ViewOf(u);
            var portrait = new VisualElement();
            portrait.style.width = 120; portrait.style.height = 120; portrait.style.marginRight = 16;
            portrait.style.borderTopWidth = portrait.style.borderBottomWidth = portrait.style.borderLeftWidth = portrait.style.borderRightWidth = 2;
            portrait.style.borderTopColor = portrait.style.borderBottomColor = portrait.style.borderLeftColor = portrait.style.borderRightColor = PanelEdge;
            if (view && view.Portrait)
            {
                var r = view.PortraitCrop;
                var sprite = Sprite.Create(view.Portrait, new Rect(r.x, r.y, r.width, r.height), new Vector2(0.5f, 0.5f), 100f);
                portrait.style.backgroundImage = new StyleBackground(sprite);
            }
            c.Root.Add(portrait);
            var info = new VisualElement(); info.style.flexGrow = 1; c.Root.Add(info);
            var head = Row(info);
            c.Name = Text($"{u.Name}   {BattleText.Level(u.Level)}", 26, Color.white); head.Add(c.Name);
            var icon = new VisualElement();
            Absolute(icon, -1, -6, 4, -1); icon.style.width = 44; icon.style.height = 44;
            int t = (int)u.Troop;
            if (t < TroopIcons.Length && TroopIcons[t]) icon.style.backgroundImage = new StyleBackground(TroopIcons[t]);
            icon.tooltip = BattleText.Troop(u.Troop);
            c.Root.Add(icon);
            (c.TroopText, c.TroopFill) = Bar(info, BattleText.Troops, new Color(0.35f, 0.78f, 0.36f));
            (c.TechText, c.TechFill) = Bar(info, BattleText.Tech, new Color(0.25f, 0.55f, 0.95f));
            c.Status = Text("", 16, new Color(1f, 0.8f, 0.6f)); info.Add(c.Status);
            return c;
        }

        Plate MakePlate(string name)
        {
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

        (Label, VisualElement) Bar(VisualElement parent, string caption, Color fillColor)
        {
            var row = Row(parent); row.style.alignItems = Align.Center; row.style.marginTop = 8;
            var cap = Text(caption, 20, new Color(0.85f, 0.85f, 0.85f)); cap.style.width = 48; row.Add(cap);
            var value = Text("", 22, Color.white); value.style.width = 110; row.Add(value);
            var track = new VisualElement(); track.style.flexGrow = 1; track.style.height = 14; track.style.marginRight = 20;
            track.style.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
            var fill = new VisualElement(); fill.style.height = 14; fill.style.backgroundColor = fillColor;
            track.Add(fill); row.Add(track);
            return (value, fill);
        }

        // ---------- refresh ----------

        void Refresh()
        {
            if (Controller.Session == null) return;
            var state = Controller.Session.State;
            roundLabel.text = BattleText.Round(state.Round);
            fastButton.style.backgroundColor = Controller.Fast ? new Color(0.5f, 0.38f, 0.12f) : Panel;
            orderLabel.text = BattleText.Order(string.Join(" → ", state.ActionOrder().Select(u => u.Name)));
            foreach (var pair in cardOf) UpdateCard(pair.Key, pair.Value);
            bool commanding = Controller.Current == BattleController.Phase.Commanding;
            if (commanding && step != Step.Command && pendingActor != Controller.Actor) step = Step.Command;
            pendingActor = Controller.Actor;
            menu.style.display = commanding ? DisplayStyle.Flex : DisplayStyle.None;
            promptLabel.style.display = menu.style.display;
            if (commanding) BuildMenu();
            if (Controller.Current == BattleController.Phase.Finished && Controller.Session.Result != null) ShowResult();
            else resultPanel.style.display = DisplayStyle.None;
        }

        BattleUnit pendingActor;

        void UpdateCard(BattleUnit u, Card c)
        {
            c.Name.text = $"{u.Name}   {BattleText.Level(u.Level)}";
            c.TroopText.text = $"{u.Troops}/{u.Stats.MaxTroops}";
            c.TechText.text = $"{u.Tech}/{u.Stats.MaxTech}";
            c.TroopFill.style.width = new Length(100f * u.Troops / u.Stats.MaxTroops, LengthUnit.Percent);
            c.TechFill.style.width = new Length(100f * u.Tech / Mathf.Max(1, u.Stats.MaxTech), LengthUnit.Percent);
            var commanded = Controller.Current == BattleController.Phase.Commanding && Controller.Commands.TryGetValue(u, out var cmd) ? $"  已选：{BattleText.Command(cmd.Kind)}" : "";
            c.Status.text = string.Join("  ", u.Modifiers.Select(BattleText.Remaining)) + commanded;
            bool selected = Controller.Actor == u;
            var edge = selected ? Gold : PanelEdge;
            c.Root.style.borderTopColor = c.Root.style.borderBottomColor = c.Root.style.borderLeftColor = c.Root.style.borderRightColor = edge;
            c.Root.style.borderTopWidth = c.Root.style.borderBottomWidth = c.Root.style.borderLeftWidth = c.Root.style.borderRightWidth = selected ? 4 : 2;
            c.Root.style.opacity = u.Active ? 1f : 0.45f;
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
                    MenuButton(menu, BattleText.Command(CommandKind.Attack), () => PickTarget(CommandKind.Attack, null));
                    MenuButton(menu, BattleText.Command(CommandKind.Skill), () => { step = Step.Skill; BuildMenu(); }, enabled: actor.Skills.Count > 0);
                    var defend = MenuButton(menu, BattleText.Command(CommandKind.Defend), () => Controller.Choose(BattleCommand.Defend(actor)));
                    defend.tooltip = BattleText.DefendHelp;
                    MenuButton(menu, BattleText.Command(CommandKind.Item), () => { step = Step.Item; BuildMenu(); }, enabled: state.CanUseRation(actor));
                    if (Controller.Commands.Count > 0) MenuButton(menu, BattleText.Back, () => { step = Step.Command; Controller.Undo(); }, 20);
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
                                     (pendingSkill == null || pendingSkill.Damage != DamageKind.None) ? "  克制" : "";
                        string label = (plateOf.TryGetValue(target, out var p) ? p.Name.text : target.Name) + $"  {target.Troops}/{target.Stats.MaxTroops}{adv}";
                        var b = MenuButton(menu, label, () => Commit(target), 20);
                        b.RegisterCallback<FocusInEvent>(_ => { Controller.PreviewTarget(target); HighlightPlate(target); });
                        b.RegisterCallback<FocusOutEvent>(_ => { Controller.PreviewTarget(null); HighlightPlate(null); });
                    }
                    MenuButton(menu, BattleText.Back, () => { step = Step.Command; BuildMenu(); }, 20);
                    break;
            }
            menu.Query<Button>().Where(b => b.enabledSelf).First()?.Focus();
        }

        // Cancel (gamepad B / Esc): sub-menu → command menu → previous general's command.
        void Cancel()
        {
            if (Controller == null || Controller.Current != BattleController.Phase.Commanding) return;
            Controller.PreviewTarget(null); HighlightPlate(null);
            if (step != Step.Command) { step = Step.Command; BuildMenu(); }
            else if (Controller.Commands.Count > 0) Controller.Undo();
        }

        void HighlightPlate(BattleUnit unit)
        {
            foreach (var pair in plateOf) pair.Value.Name.style.color = pair.Key == unit ? Gold : Color.white;
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
            var again = MenuButton(resultPanel, won ? BattleText.Again : BattleText.Retry, () => Controller.StartBattle(), 24, 220);
            again.style.marginTop = 18;
            again.Focus();
        }

        // ---------- events & per-frame ----------

        void OnEvent(BattleEvent e)
        {
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

        void LateUpdate()
        {
            if (Controller == null || Controller.Session == null || root?.panel == null || !BattleCamera) return;
            foreach (var pair in plateOf)
            {
                var view = Controller.ViewOf(pair.Key);
                bool show = view && view.gameObject.activeInHierarchy && pair.Key.Active;
                pair.Value.Root.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (!show) continue;
                var p = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, view.transform.position + Vector3.up * 1.75f, BattleCamera);
                pair.Value.Root.style.left = p.x - 75; pair.Value.Root.style.top = p.y - 34;
                pair.Value.Fill.style.width = new Length(100f * pair.Key.Troops / pair.Key.Stats.MaxTroops, LengthUnit.Percent);
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

        static VisualElement Framed(VisualElement parent)
        {
            var e = new VisualElement();
            e.style.backgroundColor = Panel;
            e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = 2;
            e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = PanelEdge;
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = 4;
            e.style.paddingLeft = e.style.paddingRight = 18; e.style.paddingTop = e.style.paddingBottom = 10;
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

        static Button MenuButton(VisualElement parent, string text, Action onClick, int size = 26, float width = -1, bool enabled = true)
        {
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
            b.RegisterCallback<PointerEnterEvent>(_ => { if (b.enabledSelf) b.Focus(); });
            parent.Add(b);
            return b;
        }
    }
}
