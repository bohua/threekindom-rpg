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
        [Tooltip("Codex range art (art/archery-minigame), by file name; empty draws the range in code")] public Texture2D[] Art = new Texture2D[0];

        Texture2D T(string name) => System.Array.Find(Art, t => t && t.name == name);
        bool HasArt => T("range_ground") && T("target_straw") && T("archer_liubei_aim") && T("arrow");
        const float ArtScale = 3f;   // 640 × 360 native → 1920 × 1080
        // Bigger than the backdrop's scale so they read well (user 2026-10-23): archer ×4, straw targets ×4, arrow ×6.
        const float ArcherScale = 4f, TargetScale = 4f, ArrowScale = 6f;
        const float Horizon = 450f;
        VisualElement front, sprites, archer, flyingArrow, arrowIcons;
        readonly List<(VisualElement root, VisualElement straw, VisualElement burst)> targetViews = new List<(VisualElement, VisualElement, VisualElement)>();
        readonly Dictionary<ArcheryGame.Target, float> hitAt = new Dictionary<ArcheryGame.Target, float>();
        float releasedAt = -10f;

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

            if (HasArt)
            {
                // Painted range: sky, far camp, ground behind everything; the front layer over the sprites.
                if (T("range_bg_sky")) panel.Add(Layer(T("range_bg_sky")));
                if (T("ground_grass")) Camp();
                else foreach (var layer in new[] { "range_bg_far", "range_ground" }) if (T(layer)) panel.Add(Layer(T(layer)));
            }
            range = new VisualElement { pickingMode = PickingMode.Ignore };
            range.style.position = Position.Absolute;
            range.style.left = range.style.top = range.style.right = range.style.bottom = 0;
            range.generateVisualContent += Draw;
            panel.Add(range);
            sprites = new VisualElement { pickingMode = PickingMode.Ignore };
            sprites.style.position = Position.Absolute;
            sprites.style.left = sprites.style.top = sprites.style.right = sprites.style.bottom = 0;
            panel.Add(sprites);
            if (HasArt && T("range_front")) { front = Layer(T("range_front")); panel.Add(front); }

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
            gauges.style.position = Position.Absolute; gauges.style.top = 24; gauges.style.left = new Length(50, LengthUnit.Percent);
            gauges.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);   // top centre, clear of the range (user 2026-10-23)
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
            banner.style.position = Position.Absolute; banner.style.left = 0; banner.style.right = 0; banner.style.top = 230;
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
            flight = null; lastShot = null; spent.Clear(); hitAt.Clear();
            foreach (var v in targetViews) v.root.RemoveFromHierarchy();
            targetViews.Clear();
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
            releasedAt = Time.unscaledTime;
            foreach (var t in lastShot.Hits)
            {
                int k = flight.FindIndex(pt => (pt.x - t.X) * (pt.x - t.X) + (pt.y - t.Y) * (pt.y - t.Y) <= ArcheryGame.TargetRadius * ArcheryGame.TargetRadius * 1.2f);
                hitAt[t] = Time.unscaledTime + Mathf.Max(0, k) / (100f * FlightSpeed);
            }
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
            UpdateSprites();
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

        // Green grass from the horizon down, a palisade along it with tents and Han banners (user 2026-10-23: the
        // yellow straw targets must stand out from the ground).
        void Camp()
        {
            var grass = new VisualElement { pickingMode = PickingMode.Ignore };
            grass.style.position = Position.Absolute;
            grass.style.left = 0; grass.style.right = 0; grass.style.top = Horizon; grass.style.bottom = 0;
            grass.style.backgroundImage = new StyleBackground(T("ground_grass"));
            grass.style.backgroundRepeat = new BackgroundRepeat(Repeat.Repeat, Repeat.Repeat);
            grass.style.backgroundSize = new BackgroundSize(T("ground_grass").width * ArtScale, T("ground_grass").height * ArtScale);
            panel.Add(grass);
            var stakes = T("palisade_stakes");
            if (stakes)
            {
                var wall = new VisualElement { pickingMode = PickingMode.Ignore };
                wall.style.position = Position.Absolute;
                wall.style.left = 0; wall.style.right = 0;
                wall.style.height = stakes.height * 2; wall.style.top = Horizon - stakes.height * 2 + 16;
                wall.style.backgroundImage = new StyleBackground(stakes);
                wall.style.backgroundRepeat = new BackgroundRepeat(Repeat.Repeat, Repeat.NoRepeat);
                wall.style.backgroundSize = new BackgroundSize(stakes.width * 2, stakes.height * 2);
                panel.Add(wall);
            }
            void Stand(string key, float x, float scale)
            {
                var t = T(key);
                if (!t) return;
                var e = Sprite(panel, t, scale);
                e.style.left = x - t.width * scale / 2f; e.style.top = Horizon + 20 - t.height * scale;
            }
            Stand("prop_army_tent", 1180, 2f);
            Stand("prop_army_tent", 1560, 2f);
            Stand("prop_banner_han", 980, 2f);
            Stand("prop_banner_han", 1380, 2f);
            Stand("prop_banner_han", 1780, 2f);
        }

        static VisualElement Layer(Texture2D tex)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute;
            e.style.left = e.style.top = e.style.right = e.style.bottom = 0;
            e.style.backgroundImage = new StyleBackground(tex);
            return e;
        }

        static VisualElement Sprite(VisualElement parent, Texture2D tex, float scale)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute;
            if (tex) { e.style.backgroundImage = new StyleBackground(tex); e.style.width = tex.width * scale; e.style.height = tex.height * scale; }
            parent.Add(e);
            return e;
        }

        // The painted range: targets on posts (struck straw once hit, with a burst), Liu Bei drawing the bow, the arrow
        // in flight along its arc, and the arrows left. Rebuilt when the set of targets changes.
        void UpdateSprites()
        {
            var g = Game;
            if (!HasArt || g == null) return;
            if (targetViews.Count != g.Targets.Count || targetViews.Count == 0 || targetViews[0].root.userData != g.Targets[0])
            {
                foreach (var v in targetViews) v.root.RemoveFromHierarchy();
                targetViews.Clear();
                foreach (var t in g.Targets)
                {
                    var c = Screen((t.X, t.Y));
                    var root = new VisualElement { pickingMode = PickingMode.Ignore, userData = t };
                    root.style.position = Position.Absolute;
                    root.style.left = 0; root.style.top = 0;
                    sprites.Add(root);
                    var post = Sprite(root, T(GroundY - c.y > 150 ? "target_post_tall" : "target_post_short"), 3f);
                    post.style.left = c.x - 12; post.style.top = c.y + 14 * TargetScale; post.style.height = Mathf.Max(0, GroundY - c.y - 14 * TargetScale);
                    var straw = Sprite(root, T("target_straw"), TargetScale);
                    straw.style.left = c.x - 14 * TargetScale; straw.style.top = c.y - 14 * TargetScale;
                    var burst = Sprite(root, T("hit_burst_1"), TargetScale);
                    burst.style.left = c.x - 16 * TargetScale; burst.style.top = c.y - 24 * TargetScale;
                    burst.style.display = DisplayStyle.None;
                    targetViews.Add((root, straw, burst));
                }
            }
            for (int i = 0; i < targetViews.Count; i++)
            {
                var t = g.Targets[i];
                var v = targetViews[i];
                bool shownHit = t.Hit && (!hitAt.TryGetValue(t, out float when) || Time.unscaledTime >= when);
                v.straw.style.backgroundImage = new StyleBackground(T(shownHit && T("target_straw_hit") ? "target_straw_hit" : "target_straw"));
                // Four burst frames over 0.32 s once the arrow reaches it.
                float since = hitAt.TryGetValue(t, out float at) ? Time.unscaledTime - at : 99f;
                int frame = Mathf.FloorToInt(since / 0.08f);
                bool bursting = since >= 0f && frame < 4 && T("hit_burst_" + (frame + 1));
                v.burst.style.display = bursting ? DisplayStyle.Flex : DisplayStyle.None;
                if (bursting) v.burst.style.backgroundImage = new StyleBackground(T("hit_burst_" + (frame + 1)));
            }

            // Liu Bei: draw (aiming), full draw while the arrow is loosed, release just after.
            var sheet = T("archer_liubei_aim");
            if (archer == null)
            {
                archer = Sprite(sprites, sheet, ArcherScale);
                archer.style.width = 64 * ArcherScale; archer.style.height = 64 * ArcherScale;
                archer.style.backgroundSize = new BackgroundSize(sheet.width * ArcherScale, sheet.height * ArcherScale);
                var release = Screen(ArcheryGame.Release);
                archer.style.left = release.x - 40 * ArcherScale; archer.style.top = release.y - 30 * ArcherScale;
            }
            int pose = Flying ? 3 : Time.unscaledTime - releasedAt < 0.3f ? 3 : g.Finished ? 0 : 2;
            archer.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Left, -pose * 64 * ArcherScale);

            if (flyingArrow == null) { flyingArrow = Sprite(sprites, T("arrow"), ArrowScale); flyingArrow.style.transformOrigin = new TransformOrigin(0, Length.Percent(50)); }
            flyingArrow.style.display = Flying ? DisplayStyle.Flex : DisplayStyle.None;
            if (Flying)
            {
                int n = Mathf.Min(flight.Count - 1, Mathf.FloorToInt(flightT));
                var head = Screen(flight[n]);
                var prev = Screen(flight[Mathf.Max(0, n - 3)]);
                var dir = (head - prev).sqrMagnitude > 0.01f ? (head - prev).normalized : Vector2.right;
                float len = 24 * ArrowScale;
                var tail = head - dir * len;
                flyingArrow.style.left = tail.x; flyingArrow.style.top = tail.y - 2.5f * ArrowScale;
                flyingArrow.style.rotate = new Rotate(Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            }

            if (arrowIcons == null)
            {
                arrowIcons = new VisualElement { pickingMode = PickingMode.Ignore };
                arrowIcons.style.position = Position.Absolute; arrowIcons.style.left = 40; arrowIcons.style.top = 120;
                arrowIcons.style.flexDirection = FlexDirection.Row;
                panel.Add(arrowIcons);
            }
            if (arrowIcons.childCount != ArcheryGame.Arrows)
            {
                arrowIcons.Clear();
                for (int i = 0; i < ArcheryGame.Arrows; i++) Sprite(arrowIcons, T("arrow_icon"), ArtScale).style.position = Position.Relative;
            }
            for (int i = 0; i < ArcheryGame.Arrows; i++)
                arrowIcons[i].style.backgroundImage = new StyleBackground(T(i < g.ArrowsLeft ? "arrow_icon" : (T("arrow_icon_used") ? "arrow_icon_used" : "arrow_icon")));
        }

        static Vector2 Screen((float x, float y) m) => new Vector2(OriginX + m.x * PxPerMetre, GroundY - m.y * PxPerMetre);

        void Draw(MeshGenerationContext mgc)
        {
            var p = mgc.painter2D;
            var size = range.contentRect.size;
            if (HasArt) { DrawArcs(p); return; }
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

        // Over the painted range: a short dotted aim guide from the bow, and the arcs already flown.
        void DrawArcs(Painter2D p)
        {
            var g = Game;
            if (g == null) return;
            var release = Screen(ArcheryGame.Release);
            if (!Flying && !g.Finished)
            {
                float a = Angle * Mathf.Deg2Rad;
                var aim = new Vector2(Mathf.Cos(a), -Mathf.Sin(a));
                p.strokeColor = new Color(1f, 0.95f, 0.75f, 0.9f); p.lineWidth = 4;
                for (int i = 1; i <= 6; i++)
                {
                    float d = 40 + i * (14 + 22 * Power);
                    p.BeginPath(); p.MoveTo(release + aim * d); p.LineTo(release + aim * (d + 10)); p.Stroke();
                }
            }
            p.lineWidth = 2;
            p.strokeColor = new Color(1f, 1f, 1f, 0.3f);
            foreach (var path in spent) StrokePath(p, path, path.Count);
            if (flight != null)
            {
                p.strokeColor = new Color(1f, 0.95f, 0.7f, 0.6f); p.lineWidth = 3;
                StrokePath(p, flight, Mathf.Min(flight.Count, Mathf.FloorToInt(flightT) + 1));
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
