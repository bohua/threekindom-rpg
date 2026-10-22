using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Town;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.UI.Battle;
using ThreeKingdoms.UI.Common;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThreeKingdoms.UI.Town
{
    // The barracks archery range (user 2026-10-22, after 太阁立志传5), full screen: up/down set the angle, left/right
    // the draw, confirm looses the arrow, which flies its arc across the range; every target it passes through
    // falls. Three arrows; when all five have fallen a new set stands. Drawn in code (sky, ground, straw targets,
    // the arc) until the Codex range art (art/archery-minigame) lands. Reads and drives TownStoryController.Archery.
    [RequireComponent(typeof(UIDocument))]
    public sealed class ArcheryHud : MonoBehaviour
    {
        public TownStoryController Controller;
        [Tooltip("Pixel skin (BATTLE_UI_SKIN.md); empty keeps the flat style")] public BattleUiSkin Skin;

        const float PxPerMetre = 40f, OriginX = 200f, GroundY = 860f;
        const float AngleStep = 1f, PowerStep = 0.02f, FlightSpeed = 1.6f;   // path samples are 0.01 s apart

        VisualElement root, panel, range, pad;
        Label angleLabel, powerLabel, arrowsLabel, scoreLabel, help, banner;
        VisualElement powerFill;

        public float Angle { get; private set; } = 20f;
        public float Power { get; private set; } = 0.6f;
        public bool Open => panel != null && panel.style.display == DisplayStyle.Flex;
        public bool Flying => flight != null;

        ArcheryGame Game => Controller ? Controller.Archery : null;
        List<(float x, float y)> flight;
        float flightT;
        ArcheryGame.Shot lastShot;
        readonly List<List<(float x, float y)>> spent = new List<List<(float x, float y)>>();
        float bannerUntil;

        void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;
            BattleFont.ApplyTo(root);
            root.style.flexGrow = 1;

            panel = new VisualElement();
            panel.style.position = Position.Absolute;
            panel.style.left = panel.style.top = panel.style.right = panel.style.bottom = 0;
            panel.style.backgroundColor = new Color(0.55f, 0.70f, 0.82f);
            root.Add(panel);

            range = new VisualElement { pickingMode = PickingMode.Ignore };
            range.style.position = Position.Absolute;
            range.style.left = range.style.top = range.style.right = range.style.bottom = 0;
            range.generateVisualContent += Draw;
            panel.Add(range);

            var top = new VisualElement { pickingMode = PickingMode.Ignore };
            top.style.position = Position.Absolute; top.style.left = 40; top.style.top = 30; top.style.right = 40;
            top.style.flexDirection = FlexDirection.Row; top.style.justifyContent = Justify.SpaceBetween;
            panel.Add(top);
            var title = MenuKit.Text(top, "兵营 · 射箭", 34, MenuKit.Gold);
            if (Skin) { Skin.Plaque(title); title.style.paddingLeft = title.style.paddingRight = 28; title.style.paddingTop = title.style.paddingBottom = 12; }
            var stats = new VisualElement { pickingMode = PickingMode.Ignore };
            stats.style.flexDirection = FlexDirection.Row;
            top.Add(stats);
            arrowsLabel = MenuKit.Text(stats, "", 28, Color.white); arrowsLabel.style.marginRight = 40;
            scoreLabel = MenuKit.Text(stats, "", 28, Color.white);

            var gauges = new VisualElement { pickingMode = PickingMode.Ignore };
            gauges.style.position = Position.Absolute; gauges.style.left = 40; gauges.style.bottom = 40;
            gauges.style.width = 520;
            MenuKit.Frame(gauges);
            panel.Add(gauges);
            angleLabel = MenuKit.Text(gauges, "", 26, Color.white);
            var powerRow = new VisualElement { pickingMode = PickingMode.Ignore };
            powerRow.style.flexDirection = FlexDirection.Row; powerRow.style.alignItems = Align.Center; powerRow.style.marginTop = 8;
            gauges.Add(powerRow);
            powerLabel = MenuKit.Text(powerRow, "力度", 26, Color.white);
            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.style.width = 300; bar.style.height = 20; bar.style.marginLeft = 16;
            bar.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            powerRow.Add(bar);
            powerFill = new VisualElement { pickingMode = PickingMode.Ignore };
            powerFill.style.height = 20; powerFill.style.backgroundColor = new Color(0.9f, 0.55f, 0.15f);
            bar.Add(powerFill);
            help = MenuKit.Text(gauges, "↑↓ 角度　←→ 力度　[确认] 放箭　[取消] 离开", 20, MenuKit.Muted);
            help.style.marginTop = 10;

            banner = MenuKit.Text(panel, "", 40, MenuKit.Gold);
            banner.style.position = Position.Absolute; banner.style.left = 0; banner.style.right = 0; banner.style.top = 160;
            banner.style.unityTextAlign = TextAnchor.MiddleCenter;
            banner.style.unityTextOutlineWidth = 2; banner.style.unityTextOutlineColor = Color.black;

            // Keyboard and pad both arrive as UI navigation on this focusable element (held keys repeat).
            pad = new VisualElement { focusable = true };
            pad.style.position = Position.Absolute; pad.style.width = 1; pad.style.height = 1;
            panel.Add(pad);
            pad.RegisterCallback<NavigationMoveEvent>(OnMove);
            pad.RegisterCallback<NavigationSubmitEvent>(_ => Submit());
            pad.RegisterCallback<NavigationCancelEvent>(e => { e.StopPropagation(); Leave(); });

            if (Controller) { Controller.PanelOpened += OnOpened; Controller.PanelClosed += OnClosed; }
            panel.style.display = DisplayStyle.None;
        }

        void OnDisable()
        {
            if (Controller) { Controller.PanelOpened -= OnOpened; Controller.PanelClosed -= OnClosed; }
        }

        void OnOpened(string p)
        {
            if (p != TownStoryController.ArcheryPanel) return;
            panel.style.display = DisplayStyle.Flex;
            flight = null; lastShot = null; spent.Clear();
            Flash("三箭定输赢！");
            Refresh();
            pad.Focus();
        }

        void OnClosed(string p) { if (p == TownStoryController.ArcheryPanel) panel.style.display = DisplayStyle.None; }

        void OnMove(NavigationMoveEvent e)
        {
            if (Flying || Game == null || Game.Finished) return;
            switch (e.direction)
            {
                case NavigationMoveEvent.Direction.Up: Angle = Mathf.Min(ArcheryGame.MaxAngle, Angle + AngleStep); break;
                case NavigationMoveEvent.Direction.Down: Angle = Mathf.Max(ArcheryGame.MinAngle, Angle - AngleStep); break;
                case NavigationMoveEvent.Direction.Right: Power = Mathf.Min(ArcheryGame.MaxPower, Power + PowerStep); break;
                case NavigationMoveEvent.Direction.Left: Power = Mathf.Max(ArcheryGame.MinPower, Power - PowerStep); break;
            }
            e.StopPropagation();
            Refresh();
        }

        // Confirm: loose an arrow, or after the third has landed, return to the archer.
        public void Submit()
        {
            if (Game == null || Flying) return;
            if (Game.Finished) { Controller.FinishArchery(); return; }
            lastShot = Game.Shoot(Angle, Power);
            flight = lastShot.Path;
            flightT = 0f;
            Refresh();
        }

        public void Leave() { if (!Flying) Controller.FinishArchery(); }

        public void SetAim(float angle, float power) { Angle = angle; Power = power; Refresh(); }

        void Update()
        {
            if (!Open) return;
            if (root.panel != null && root.panel.focusController.focusedElement != pad) pad.Focus();
            if (Flying)
            {
                flightT += Time.deltaTime * 100f * FlightSpeed;
                if (flightT >= flight.Count - 1)
                {
                    spent.Add(flight);
                    flight = null;
                    int hits = lastShot.Hits.Count;
                    Flash(hits == 0 ? "未中……" : hits == 1 ? "命中！" : $"一箭{hits}靶！");
                    if (lastShot.Refreshed) { spent.Clear(); Flash("五靶尽倒！换一组新靶！"); }
                    if (Game.Finished) Flash($"三箭已尽，共中{Game.Score}靶　[确认] 返回");
                    Refresh();
                }
                range.MarkDirtyRepaint();
            }
            if (banner.text != "" && Time.unscaledTime > bannerUntil && !(Game != null && Game.Finished)) banner.text = "";
        }

        void Flash(string text) { banner.text = text; bannerUntil = Time.unscaledTime + 1.6f; }

        void Refresh()
        {
            var g = Game;
            angleLabel.text = $"角度 {Angle:0}°";
            powerFill.style.width = 300f * (Power - ArcheryGame.MinPower) / (ArcheryGame.MaxPower - ArcheryGame.MinPower);
            arrowsLabel.text = g != null ? $"剩余箭数 {g.ArrowsLeft}" : "";
            scoreLabel.text = g != null ? $"命中 {g.Score}" : "";
            range.MarkDirtyRepaint();
        }

        static Vector2 Screen((float x, float y) m) => new Vector2(OriginX + m.x * PxPerMetre, GroundY - m.y * PxPerMetre);

        void Draw(MeshGenerationContext mgc)
        {
            var p = mgc.painter2D;
            var size = range.contentRect.size;
            // Ground, a far fence line and the archer's mark.
            p.fillColor = new Color(0.46f, 0.38f, 0.24f);
            p.BeginPath(); p.MoveTo(new Vector2(0, GroundY)); p.LineTo(new Vector2(size.x, GroundY)); p.LineTo(new Vector2(size.x, size.y)); p.LineTo(new Vector2(0, size.y)); p.ClosePath(); p.Fill();
            p.fillColor = new Color(0.36f, 0.50f, 0.28f);
            p.BeginPath(); p.MoveTo(new Vector2(0, GroundY - 120)); p.LineTo(new Vector2(size.x, GroundY - 150)); p.LineTo(new Vector2(size.x, GroundY)); p.LineTo(new Vector2(0, GroundY)); p.ClosePath(); p.Fill();
            p.strokeColor = new Color(0.30f, 0.20f, 0.12f); p.lineWidth = 6;
            for (float x = 60; x < size.x; x += 90) { p.BeginPath(); p.MoveTo(new Vector2(x, GroundY - 150)); p.LineTo(new Vector2(x, GroundY - 205)); p.Stroke(); }

            var g = Game;
            if (g == null) return;
            // The archer: a figure with the bow raised to the angle.
            var release = Screen(ArcheryGame.Release);
            p.fillColor = new Color(0.88f, 0.48f, 0.16f);
            p.BeginPath(); p.Arc(release + new Vector2(-14, -10), 14, 0, 360); p.Fill();
            p.strokeColor = new Color(0.88f, 0.48f, 0.16f); p.lineWidth = 10;
            p.BeginPath(); p.MoveTo(release + new Vector2(-14, 4)); p.LineTo(new Vector2(release.x - 14, GroundY)); p.Stroke();
            float a = Angle * Mathf.Deg2Rad;
            var aim = new Vector2(Mathf.Cos(a), -Mathf.Sin(a));
            p.strokeColor = new Color(0.25f, 0.15f, 0.08f); p.lineWidth = 5;
            p.BeginPath(); p.MoveTo(release); p.LineTo(release + aim * (40 + 50 * Power)); p.Stroke();

            // Targets on their posts.
            foreach (var t in g.Targets)
            {
                var c = Screen((t.X, t.Y));
                p.strokeColor = new Color(0.36f, 0.24f, 0.12f); p.lineWidth = 6;
                p.BeginPath(); p.MoveTo(c + new Vector2(0, 20)); p.LineTo(new Vector2(c.x, GroundY)); p.Stroke();
                float r = ArcheryGame.TargetRadius * PxPerMetre;
                p.fillColor = t.Hit ? new Color(0.55f, 0.48f, 0.36f, 0.6f) : new Color(0.86f, 0.76f, 0.45f);
                p.BeginPath(); p.Arc(c, r, 0, 360); p.Fill();
                p.fillColor = t.Hit ? new Color(0.4f, 0.3f, 0.25f, 0.6f) : new Color(0.80f, 0.15f, 0.10f);
                p.BeginPath(); p.Arc(c, r * 0.45f, 0, 360); p.Fill();
            }

            // Spent arcs faintly, the arrow in flight brightly.
            p.lineWidth = 2;
            p.strokeColor = new Color(1f, 1f, 1f, 0.25f);
            foreach (var path in spent) StrokePath(p, path, path.Count);
            if (flight != null)
            {
                int n = Mathf.Min(flight.Count, Mathf.FloorToInt(flightT) + 1);
                p.strokeColor = new Color(1f, 0.95f, 0.7f, 0.8f); p.lineWidth = 3;
                StrokePath(p, flight, n);
                var head = Screen(flight[n - 1]);
                var prev = Screen(flight[Mathf.Max(0, n - 4)]);
                var dir = (head - prev).sqrMagnitude > 0.01f ? (head - prev).normalized : Vector2.right;
                p.strokeColor = new Color(0.3f, 0.2f, 0.1f); p.lineWidth = 4;
                p.BeginPath(); p.MoveTo(head - dir * 30); p.LineTo(head); p.Stroke();
            }
        }

        static void StrokePath(Painter2D p, List<(float x, float y)> path, int count)
        {
            if (count < 2) return;
            p.BeginPath();
            p.MoveTo(Screen(path[0]));
            for (int i = 1; i < count; i += 2) p.LineTo(Screen(path[i]));
            p.Stroke();
        }
    }
}
