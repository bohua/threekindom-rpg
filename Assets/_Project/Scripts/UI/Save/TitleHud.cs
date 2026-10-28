using System;
using System.Collections;
using System.Collections.Generic;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.Save;
using ThreeKingdoms.UI.Battle;
using ThreeKingdoms.UI.Common;
using ThreeKingdoms.UI.Menu;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThreeKingdoms.UI.Save
{
    // Title screen after the Codex design v2 (design/ux/title-menu.md, art/title-menu-concept-v2): Final Fantasy style
    // white backdrop with the 三国·江山如画 logo above a centred menu and a glove pointer. Load and settings slide the
    // menu to the left and bring their panel into the middle; continue, new game and load go into the game through
    // the clouds (CloudCurtain) with the start chime. Positions are the design's own 1920×1080 pixels (the panel settings
    // scale to that resolution). An unreadable save shows why and stays on screen; the file is never changed.
    [RequireComponent(typeof(UIDocument))]
    public sealed class TitleHud : MonoBehaviour
    {
        [Tooltip("Kept for the scene builder; the title draws its own light style")] public BattleUiSkin Skin;
        public TitleController Controller;

        public enum Mode { Main, Load, Settings }

        public const string StartSfx = "title_start";   // Resources/Audio/Sfx, original rising arpeggio and bell

        // Design colours (DESIGN_NOTES.md).
        static readonly Color Ink = Hex(0x1B2A3F), Cyan = Hex(0x2F8FB8), Disabled = Hex(0xA0ACB6), Frame = Hex(0xB4C9D5);
        static readonly Color CardEdge = Hex(0xCDDBE4), Second = Hex(0x6D8293), Faint = Hex(0x7B8791), Lit = Hex(0xE8F3F8), Warn = Hex(0xC0574F);

        // Menu: text rows from y 640, 54 apart, centred on x 960; slid left they are centred on x 240.
        const float MenuCentre = 960, MenuLeftCentre = 240, MenuY = 640, MenuStep = 54, EntryW = 300;
        const float PanelX = 560, PanelY = 600, PanelW = 1140, PanelH = 408;

        VisualElement root, menu, panel, rows, cursor;
        Label panelTitle, hint, message;
        readonly List<Button> mainEntries = new List<Button>();
        Font serif, sans;
        Texture2D ruleCentre, ruleCentreLit, ruleRight, ruleRightLit, glow;
        int openedFrame;
        float slideT, panelT, panelDelay;
        bool busy;   // going into the game: nothing else is taken
        Button menuMark;   // the main entry whose page is open
        Focusable shownFocus;   // the focus the highlights were last painted for (focus events arrive before it moves)

        public Mode Current { get; private set; }
        public string Message => message?.text;
        // The buttons of the page now shown (main: the five entries; load: one per slot; settings: the rows).
        public IReadOnlyList<Button> Entries => Current == Mode.Main ? mainEntries : rows.Query<Button>().ToList();

        void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            BattleFont.ApplyTo(root);
            // Noto Serif SC (weight 500) cut down to the title's fixed words; the OS variable font does not render here.
            serif = Resources.Load<Font>("TitleMenu/title_serif");
            if (!serif) serif = Font.CreateDynamicFontFromOSFont(new[] { "SimSun", "NSimSun" }, 32);
            sans = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Microsoft YaHei UI", "SimHei", "DengXian" }, 32);
            ruleCentre = Rule(Ink, 0.5f, true); ruleCentreLit = Rule(Cyan, 1f, true);
            ruleRight = Rule(Ink, 0.5f, false); ruleRightLit = Rule(Cyan, 1f, false);
            glow = Glow();

            root.style.flexGrow = 1;
            root.style.backgroundColor = Color.white;
            // Backdrop and logo composed exactly like the design (art/title-menu-concept compose.py); blending the logo
            // at run time washed its blue out.
            var plate = Resources.Load<Texture2D>("TitleMenu/title_plate");
            if (plate) { root.style.backgroundImage = plate; root.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover); }

            menu = Box(root, 0, 0, 1920, 1080);
            for (int i = 0; i < 5; i++) mainEntries.Add(MenuEntry(i));

            var copyright = Text(root, SaveText.Copyright, 16, Faint, sans);
            copyright.style.position = Position.Absolute; copyright.style.left = 0; copyright.style.right = 0; copyright.style.top = 1032;
            copyright.style.unityTextAlign = TextAnchor.MiddleCenter;

            BuildPanel();

            cursor = new VisualElement { pickingMode = PickingMode.Ignore };
            cursor.style.position = Position.Absolute; cursor.style.width = 72; cursor.style.height = 44;
            var glove = Resources.Load<Texture2D>("TitleMenu/title_cursor");
            if (glove) cursor.style.backgroundImage = glove;
            root.Add(cursor);


            root.RegisterCallback<NavigationCancelEvent>(e => { e.StopPropagation(); if (Current != Mode.Main && !busy) ShowMain(); }, TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationMoveEvent>(OnMove, TrickleDown.TrickleDown);
            openedFrame = Time.frameCount;
            ShowMain();
            slideT = panelT = 0;
            CloudCurtain.Preload();
            int shots = Array.IndexOf(Environment.GetCommandLineArgs(), "-titleShots");
            if (shots >= 0 && shots + 1 < Environment.GetCommandLineArgs().Length) StartCoroutine(Shots(Environment.GetCommandLineArgs()[shots + 1]));
        }

        // -titleShots <dir>: one frame of each page for comparing with the design images, then quit.
        IEnumerator Shots(string dir)
        {
            System.IO.Directory.CreateDirectory(dir);
            yield return new WaitForSecondsRealtime(2f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "title_main.png"));
            yield return new WaitForSecondsRealtime(0.5f);
            ShowLoad();
            yield return new WaitForSecondsRealtime(1f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "title_load.png"));
            yield return new WaitForSecondsRealtime(0.5f);
            ShowSettings();
            yield return new WaitForSecondsRealtime(1f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "title_settings.png"));
            ShowMain();
            yield return new WaitForSecondsRealtime(1f);
            mainEntries[1].Focus();
            yield return null;
            // The curtain outlives this scene, so the rest of the run follows it from a persistent runner.
            CloudCurtain.Persist(CurtainShots(dir));
            StartNewGame();
        }

        static IEnumerator CurtainShots(string dir)
        {
            while (!CloudCurtain.Busy) yield return null;
            while (CloudCurtain.Busy && CloudCurtain.Closing < 0.5f) yield return null;
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "cloud_closing.png"));
            while (CloudCurtain.Busy && !CloudCurtain.Holding) yield return null;
            yield return new WaitForSecondsRealtime(0.3f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "cloud_loading.png"));
            while (CloudCurtain.Busy && CloudCurtain.Opening < 0.4f) yield return null;
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "cloud_opening.png"));
            while (CloudCurtain.Busy) yield return null;
            yield return new WaitForSecondsRealtime(0.5f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "cloud_done.png"));
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit();
        }

        // ---- main menu ----

        Button MenuEntry(int index)
        {
            string text = index switch { 0 => SaveText.Continue, 1 => SaveText.NewGame, 2 => SaveText.Load, 3 => SaveText.Settings, _ => SaveText.Quit };
            Action act = index switch { 0 => Continue, 1 => StartNewGame, 2 => ShowLoad, 3 => ShowSettings, _ => Controller.Quit };
            var b = Plain(menu, text, () => { if (Time.frameCount > openedFrame && !busy && Current == Mode.Main) act(); });
            b.style.position = Position.Absolute;
            b.style.top = MenuY + index * MenuStep - 4; b.style.width = EntryW; b.style.height = 46;
            b.style.fontSize = 30; b.style.unityFontDefinition = FontDefinition.FromFont(serif);
            b.style.unityTextAlign = TextAnchor.UpperCenter;
            var band = new VisualElement { name = "band", pickingMode = PickingMode.Ignore };
            band.style.position = Position.Absolute; band.style.left = -60; band.style.top = 30; band.style.width = 420; band.style.height = 20;
            band.style.backgroundImage = glow; band.style.opacity = 0;
            b.Add(band);
            foreach (var name in new[] { "ruleCentre", "ruleRight" })
            {
                var line = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
                line.style.position = Position.Absolute; line.style.top = 44; line.style.height = 2;
                b.Add(line);
            }
            return b;
        }

        void Paint(Button b)
        {
            bool lit = shownFocus == b || b == menuMark;
            b.style.color = !b.enabledSelf ? Disabled : lit ? Cyan : Ink;
            float dim = b.enabledSelf ? 1f : 0.45f;
            b.Q("ruleCentre").style.backgroundImage = lit ? ruleCentreLit : ruleCentre;
            b.Q("ruleRight").style.backgroundImage = lit ? ruleRightLit : ruleRight;
            b.Q("ruleCentre").userData = dim;
        }

        public void ShowMain()
        {
            Current = Mode.Main;
            message.text = "";
            bool hasLatest = Controller.LatestSlot() != null;
            mainEntries[0].SetEnabled(hasLatest);
            mainEntries[2].SetEnabled(Controller.AnySave());
            var back = menuMark;
            menuMark = null;
            foreach (var b in mainEntries) { b.focusable = true; Paint(b); }
            (back != null && back.enabledSelf ? back : hasLatest ? mainEntries[0] : mainEntries[1]).Focus();
        }

        // Continue: the slot saved last (TitleController.LatestSlot), through the clouds like a new game.
        public void Continue()
        {
            var slot = Controller.LatestSlot();
            if (slot != null) EnterThen(mainEntries[0], () => LoadNow(slot));
        }

        void StartNewGame() => EnterThen(mainEntries[1], Controller.NewGame);

        // Into the game through the clouds (CloudCurtain): the chosen entry glows once with the start chime, the
        // clouds close, `then` starts the scene load under them and they part over the new scene. A save refused
        // after all (TryLoad) leaves the title as it was, the clouds parting over it with the reason shown.
        void EnterThen(Button entry, Action then)
        {
            if (busy || CloudCurtain.Busy) return;
            busy = true;
            StoryMusic.Stop(0.6f);
            StoryMusic.Sfx(StartSfx);
            if (entry != null) StartCoroutine(Pulse(entry));
            CloudCurtain.Play(GetComponent<UIDocument>().panelSettings, () => { then(); busy = false; });
        }

        IEnumerator Pulse(Button entry)
        {
            var band = entry.Q("band");
            for (float t = 0; t < 0.3f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Sin(t / 0.3f * Mathf.PI);
                Glow(entry, k);
                if (band != null) band.style.opacity = k;
                yield return null;
            }
            Glow(entry, 0);
            if (band != null) band.style.opacity = 0;
        }

        static void Glow(Button entry, float k)
        {
            entry.style.textShadow = k > 0.01f
                ? new TextShadow { offset = Vector2.zero, blurRadius = 4f + 9f * k, color = new Color(0.75f, 0.95f, 1f, k) }
                : new TextShadow();
            if (k > 0.01f) entry.style.color = Color.Lerp(Cyan, Color.white, k);
        }

        // ---- the panel (load / settings) ----

        void BuildPanel()
        {
            panel = Box(root, PanelX, PanelY, PanelW, PanelH);
            panel.style.backgroundColor = new Color(1f, 1f, 1f, 0.93f);
            Border(panel, Frame, 1);
            var inner = Box(panel, 5, 5, PanelW - 12, PanelH - 12);
            Border(inner, new Color(Frame.r, Frame.g, Frame.b, 0.6f), 1);

            panelTitle = Text(panel, "", 28, Ink, serif);
            panelTitle.style.position = Position.Absolute; panelTitle.style.left = 30; panelTitle.style.top = 16;
            var chapter = Text(panel, SaveText.ChapterTag, 17, Second, sans);
            chapter.style.position = Position.Absolute; chapter.style.right = 30; chapter.style.top = 24;
            var divider = Box(panel, 24, 57, PanelW - 48, 1);
            divider.style.backgroundColor = CardEdge;

            rows = Box(panel, 0, 0, PanelW, PanelH);

            message = Text(panel, "", 19, Warn, sans);
            message.style.position = Position.Absolute; message.style.left = 0; message.style.right = 0; message.style.top = 352;
            message.style.unityTextAlign = TextAnchor.MiddleCenter;
            hint = Text(panel, "", 19, Second, sans);
            hint.style.position = Position.Absolute; hint.style.left = 0; hint.style.right = 0; hint.style.top = 374;
            hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            panel.style.display = DisplayStyle.None;
        }

        void OpenPanel(Mode mode, string title, string hintText, Button mark)
        {
            Current = mode;
            menuMark = mark;
            foreach (var b in mainEntries) { b.focusable = false; Paint(b); }
            rows.Clear();
            message.text = "";
            panelTitle.text = title;
            hint.text = hintText;
            panel.style.display = DisplayStyle.Flex;
            panelDelay = 0.1f;
        }

        public void ShowLoad()
        {
            OpenPanel(Mode.Load, SaveText.LoadTitle, SaveText.LoadHint, mainEntries[2]);
            var slots = Controller.Slots;
            var names = Controller.SlotNames;
            bool any = false;
            for (int i = 0; i < names.Length; i++)
                any |= SlotCard(i, i == 0 ? SaveText.Auto : SaveText.Slot(i), names[i], slots);
            if (!any) message.text = SaveText.NoSaves;
            MenuKit.FocusFirst(rows);
        }

        bool SlotCard(int index, string name, string slot, SaveSlots slots)
        {
            var status = slots.Read(slot, out var data);
            bool usable = status != SaveReadStatus.Empty;
            var card = Plain(rows, "", () => { if (Time.frameCount > openedFrame && !busy) Load(slot); });
            card.text = status == SaveReadStatus.Ok ? $"{name}\n{SaveText.Summary(data)}" : $"{name}\n{SaveText.Status(status)}";   // read by tests
            card.style.color = Color.clear;   // the visible text is drawn by the labels below
            Card(card, 28, 68 + index * 72, 1084, 64);
            var slotName = Text(card, name, 22, usable ? Ink : Disabled, serif);
            slotName.style.position = Position.Absolute; slotName.style.left = 33; slotName.style.top = 17;
            var sep = Box(card, 236, 12, 1, 40); sep.style.backgroundColor = CardEdge;
            if (status == SaveReadStatus.Ok)
            {
                var first = Text(card, SaveText.CardPlace(data), 23, Ink, sans);
                first.name = "first";
                first.style.position = Position.Absolute; first.style.left = 261; first.style.top = 3;
                var second = Text(card, SaveText.CardDetail(data), 20, Second, sans);
                second.style.position = Position.Absolute; second.style.left = 261; second.style.top = 33;
            }
            else
            {
                var line = Text(card, status == SaveReadStatus.Empty ? SaveText.Empty : SaveText.Status(status), 22,
                    status == SaveReadStatus.Empty ? Disabled : Warn, sans);
                line.style.position = Position.Absolute; line.style.left = 270; line.style.top = 17; line.style.right = 20;
                line.style.whiteSpace = WhiteSpace.NoWrap; line.style.overflow = Overflow.Hidden;
            }
            card.SetEnabled(usable);
            return usable;
        }

        // A slot that cannot be read says why at once; a readable one goes into the game through the clouds.
        public void Load(string slot)
        {
            var status = Controller.Slots.Read(slot, out _);
            if (status != SaveReadStatus.Ok) { message.text = SaveText.Status(status); return; }
            EnterThen(null, () => LoadNow(slot));
        }

        void LoadNow(string slot)
        {
            var status = Controller.Load(slot, out string error);
            if (status == SaveReadStatus.Ok) return;   // the saved scene is loading
            message.text = error != null ? SaveText.LoadFailed(error) : SaveText.Status(status);
        }

        // Settings: the same values as the travel menu's 系统 page (GameSettings, per machine).
        public void ShowSettings()
        {
            OpenPanel(Mode.Settings, SaveText.Settings, SaveText.SettingsHint, mainEntries[3]);
            SettingRow(0, SaveText.MusicVolume);
            SettingRow(1, SaveText.EffectsVolume);
            SettingRow(2, SaveText.TextSpeed);
            SettingRow(3, SaveText.FullScreen);
            var restore = Plain(rows, SaveText.Restore, () => { GameSettings.Restore(); RefreshSettings(); });
            restore.name = "restore";
            restore.style.position = Position.Absolute; restore.style.left = 470; restore.style.top = 330; restore.style.width = 200; restore.style.height = 38;
            restore.style.fontSize = 23; restore.style.unityTextAlign = TextAnchor.MiddleCenter;
            RefreshSettings();
            MenuKit.FocusFirst(rows);
        }

        void SettingRow(int index, string label)
        {
            var row = Plain(rows, label, () => { if (index >= 2) Adjust(index, 1); });
            row.name = "row" + index;
            row.style.color = Color.clear;
            Card(row, 28, 66 + index * 64, 1084, 60);
            row.style.borderTopWidth = row.style.borderLeftWidth = row.style.borderRightWidth = 0;
            var name = Text(row, label, 27, Ink, serif);
            name.name = "label";
            name.style.position = Position.Absolute; name.style.left = 34; name.style.top = 13;
            if (index <= 1)
            {
                var track = Box(row, 372, 27, 500, 6); track.name = "track"; track.style.backgroundColor = Hex(0xDCE7EE);
                track.pickingMode = PickingMode.Position;
                var fill = Box(track, 0, 0, 0, 6); fill.name = "fill"; fill.style.backgroundColor = Hex(0x5AA6C4);
                var knob = Box(track, 0, -5, 16, 16); knob.name = "knob";
                knob.style.rotate = new Rotate(45); knob.style.backgroundColor = Color.white; Border(knob, Cyan, 2);
                var value = Text(row, "", 24, Ink, sans); value.name = "value";
                value.style.position = Position.Absolute; value.style.left = 950; value.style.width = 90; value.style.top = 14;
                value.style.unityTextAlign = TextAnchor.MiddleCenter;
                track.RegisterCallback<PointerDownEvent>(e =>
                {
                    int v = Mathf.RoundToInt(Mathf.Clamp01(e.localPosition.x / 500f) * 10f) * 10;
                    if (index == 0) GameSettings.Music = v; else GameSettings.Effects = v;
                    row.Focus(); RefreshSettings();
                });
            }
            else if (index == 2)
            {
                float[] xs = { 371, 551, 753 };
                for (int s = 0; s < GameSettings.TextSpeeds.Length; s++)
                {
                    int speed = s;
                    var opt = Text(row, GameSettings.TextSpeeds[s], 25, Second, sans); opt.name = "speed" + s;
                    opt.pickingMode = PickingMode.Position;
                    opt.style.position = Position.Absolute; opt.style.left = xs[s]; opt.style.top = 8; opt.style.width = 111; opt.style.height = 44;
                    opt.style.unityTextAlign = TextAnchor.MiddleCenter;
                    opt.RegisterCallback<PointerDownEvent>(_ => { GameSettings.TextSpeed = speed; row.Focus(); RefreshSettings(); });
                }
            }
            else
            {
                var toggle = Box(row, 829, 14, 76, 32); toggle.name = "toggle"; Border(toggle, Frame, 1); Round(toggle, 16);
                var dot = Box(toggle, 0, 4, 22, 22); dot.name = "dot"; Round(dot, 11);
                var state = Text(row, "", 25, Cyan, sans); state.name = "state";
                state.style.position = Position.Absolute; state.style.left = 967; state.style.top = 12;
            }
            if (index < 3)
            {
                var sep = Box(row, 30, 62, 1024, 1); sep.style.backgroundColor = CardEdge;
            }
        }

        void Adjust(int index, int dx)
        {
            switch (index)
            {
                case 0: GameSettings.Music += dx * 10; break;
                case 1: GameSettings.Effects += dx * 10; break;
                case 2: GameSettings.TextSpeed += dx; break;
                case 3: GameSettings.FullScreen = !GameSettings.FullScreen; break;
            }
            RefreshSettings();
        }

        void RefreshSettings()
        {
            if (Current != Mode.Settings) return;
            var focused = shownFocus;
            for (int i = 0; i < 4; i++)
            {
                var row = rows.Q<Button>("row" + i);
                if (row == null) continue;
                bool lit = focused == row;
                row.style.backgroundColor = lit ? Lit : Color.clear;
                row.style.borderBottomColor = lit ? Cyan : Color.clear;
                row.style.borderBottomWidth = lit ? 1 : 0;
                row.Q<Label>("label").style.color = lit ? Cyan : Ink;
                if (i <= 1)
                {
                    int v = i == 0 ? GameSettings.Music : GameSettings.Effects;
                    row.Q("fill").style.width = 500f * v / 100f;
                    row.Q("knob").style.left = 500f * v / 100f - 8f;
                    var value = row.Q<Label>("value"); value.text = v.ToString(); value.style.color = lit ? Cyan : Ink;
                }
                else if (i == 2)
                {
                    for (int s = 0; s < GameSettings.TextSpeeds.Length; s++)
                    {
                        var opt = row.Q<Label>("speed" + s);
                        bool on = s == GameSettings.TextSpeed;
                        opt.style.color = on ? Cyan : Second;
                        opt.style.backgroundColor = on ? Hex(0xE3F0F6) : Color.clear;
                        Border(opt, on ? Frame : Color.clear, 1);
                    }
                }
                else
                {
                    bool full = GameSettings.FullScreen;
                    row.Q("toggle").style.backgroundColor = full ? Hex(0xD9ECF4) : Hex(0xEEF2F5);
                    var dot = row.Q("dot"); dot.style.left = full ? 50 : 4; dot.style.backgroundColor = full ? Hex(0x3C97BE) : Disabled;
                    var state = row.Q<Label>("state"); state.text = full ? SaveText.On : SaveText.Off; state.style.color = full ? Cyan : Second;
                }
            }
            var restore = rows.Q<Button>("restore");
            if (restore != null)
            {
                bool lit = focused == restore;
                restore.style.color = lit ? Cyan : Second;
                restore.style.borderBottomWidth = 1; restore.style.borderBottomColor = lit ? Cyan : CardEdge;
            }
        }

        // Left / right adjust the focused setting; on the other pages they do nothing (the menu is one column).
        void OnMove(NavigationMoveEvent e)
        {
            bool sideways = e.direction == NavigationMoveEvent.Direction.Left || e.direction == NavigationMoveEvent.Direction.Right;
            if (busy) { e.StopPropagation(); return; }
            if (!sideways) return;
            e.StopPropagation();
            if (Current != Mode.Settings) return;
            var focused = root.panel?.focusController.focusedElement as VisualElement;
            for (int i = 0; i < 4; i++)
                if (focused != null && focused.name == "row" + i) Adjust(i, e.direction == NavigationMoveEvent.Direction.Right ? 1 : -1);
        }

        // ---- per frame: the menu slide, the panel easing in, the pointer following the focus ----

        void Update()
        {
            if (!busy) MenuKit.KeepFocus(root, Current == Mode.Main ? menu : rows);
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            bool open = Current != Mode.Main;
            // Out 320 ms easeOutCubic, back 220 ms; the panel follows 100 ms later over 240 ms, leaves in 160 ms.
            slideT = Mathf.MoveTowards(slideT, open ? 1f : 0f, dt / (open ? 0.32f : 0.22f));
            float slide = open ? EaseOut(slideT) : 1f - EaseOut(1f - slideT);
            if (open && panelDelay > 0) panelDelay -= dt;
            else panelT = Mathf.MoveTowards(panelT, open ? 1f : 0f, dt / (open ? 0.24f : 0.16f));
            LayoutMenu(slide);
            float p = EaseOut(panelT);
            panel.style.opacity = p;
            panel.style.left = PanelX + (open ? 24f : 16f) * (1f - p);
            if (!open && panelT <= 0f) panel.style.display = DisplayStyle.None;

            var focused = root.panel?.focusController.focusedElement as VisualElement;
            if (focused != shownFocus) Repaint(focused);
            bool settled = !open || panelT >= 1f;
            cursor.style.display = focused is Button && settled ? DisplayStyle.Flex : DisplayStyle.None;
            if (focused is Button b)
            {
                var at = root.WorldToLocal(b.worldBound.position);
                float sway = busy ? 0f : Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / 1.2f) * 3f;
                Vector2 to;
                if (Current == Mode.Main) to = new Vector2(at.x + EntryW / 2f - b.text.Length * 15f - 74f, at.y - 1f);
                else if (b.name == "restore") to = new Vector2(at.x + 10f, at.y - 2f);
                else to = new Vector2(at.x - 38f, at.y + (b.name.StartsWith("row") ? 8f : 12f));
                cursor.style.left = to.x + sway; cursor.style.top = to.y;
            }
        }

        // slide 0: centred on x 960 with lines fading both ways; 1: centred on x 240 with lines fading right.
        void LayoutMenu(float slide)
        {
            float centre = Mathf.Lerp(MenuCentre, MenuLeftCentre, slide);
            foreach (var b in mainEntries)
            {
                b.style.left = centre - EntryW / 2f;
                float dim = b.Q("ruleCentre").userData is float d ? d : 1f;
                var c = b.Q("ruleCentre"); var r = b.Q("ruleRight");
                c.style.left = 0; c.style.width = EntryW; c.style.opacity = (1f - slide) * dim;
                // From x 810–1110 under the centred menu to x 150–420 at its left stop.
                r.style.left = Mathf.Lerp(MenuCentre - 150f, MenuLeftCentre - 90f, slide) - (centre - EntryW / 2f);
                r.style.width = Mathf.Lerp(EntryW, 270f, slide);
                r.style.opacity = slide * dim;
            }
        }

        static float EaseOut(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);

        void Repaint(Focusable focused)
        {
            shownFocus = focused;
            foreach (var b in mainEntries) Paint(b);
            foreach (var card in rows.Query<Button>().ToList()) PaintCard(card, card == focused);
            RefreshSettings();
        }

        // ---- building blocks ----

        static VisualElement Box(VisualElement parent, float x, float y, float w, float h)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute;
            e.style.left = x; e.style.top = y; e.style.width = w; e.style.height = h;
            parent.Add(e);
            return e;
        }

        Label Text(VisualElement parent, string text, int size, Color color, Font font)
        {
            var l = MenuKit.Text(parent, text, size, color);
            if (font) l.style.unityFontDefinition = FontDefinition.FromFont(font);
            return l;
        }

        // A button with none of the default theme's box; focus follows the mouse.
        Button Plain(VisualElement parent, string text, Action onClick)
        {
            var b = new Button(onClick) { text = text };
            b.style.backgroundColor = Color.clear;
            b.style.borderTopWidth = b.style.borderBottomWidth = b.style.borderLeftWidth = b.style.borderRightWidth = 0;
            b.style.marginLeft = b.style.marginRight = b.style.marginTop = b.style.marginBottom = 0;
            b.style.paddingLeft = b.style.paddingRight = b.style.paddingTop = b.style.paddingBottom = 0;
            b.style.color = Ink;
            if (sans) b.style.unityFontDefinition = FontDefinition.FromFont(sans);
            b.RegisterCallback<PointerEnterEvent>(_ => { if (b.enabledSelf && b.focusable && !busy) b.Focus(); });
            parent.Add(b);
            return b;
        }

        void Card(Button card, float x, float y, float w, float h)
        {
            card.style.position = Position.Absolute;
            card.style.left = x; card.style.top = y; card.style.width = w; card.style.height = h;
            card.style.backgroundColor = new Color(1, 1, 1, 0.6f);
            Border(card, CardEdge, 1);
        }

        void PaintCard(Button card, bool lit)
        {
            if (card.name.StartsWith("row") || card.name == "restore") return;   // settings repaint in RefreshSettings
            card.style.backgroundColor = lit ? Lit : new Color(1, 1, 1, 0.6f);
            Border(card, lit ? Cyan : CardEdge, 1);
            card.style.borderLeftWidth = lit ? 3 : 1;
            var first = card.Q<Label>("first");
            if (first != null) first.style.color = lit ? Cyan : Ink;
        }

        static void Border(VisualElement e, Color c, float w)
        {
            e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = w;
            e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = c;
        }

        static void Round(VisualElement e, float r) =>
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;

        // The thin line under each menu entry: fading both ways from the centre, or to the right (like the logo's).
        static Texture2D Rule(Color c, float strength, bool centre)
        {
            var t = new Texture2D(128, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            for (int x = 0; x < 128; x++)
            {
                float u = centre ? Mathf.Abs(x / 127f - 0.5f) * 2f : x / 127f;
                t.SetPixel(x, 0, new Color(c.r, c.g, c.b, strength * Mathf.Pow(1f - u, 1.4f)));
            }
            t.Apply();
            return t;
        }

        // The soft light band under the chosen entry.
        static Texture2D Glow()
        {
            var t = new Texture2D(128, 16, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 128; x++)
                {
                    float u = Mathf.Abs(x / 127f - 0.5f) * 2f, v = Mathf.Abs(y / 15f - 0.5f) * 2f;
                    t.SetPixel(x, y, new Color(0.8f, 0.96f, 1f, Mathf.Pow(1f - u, 1.6f) * Mathf.Pow(1f - v, 2f)));
                }
            t.Apply();
            return t;
        }

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
    }
}
