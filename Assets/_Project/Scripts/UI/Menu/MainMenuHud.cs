using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Battle;
using ThreeKingdoms.Core.Economy;
using ThreeKingdoms.Gameplay.Battle;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.UI.Story;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ThreeKingdoms.UI.Menu
{
    // The pause menu 行旅 (user 2026-10-24: "1:1还原这些菜单设计到游戏菜单", GAME_MENU_UI_REDESIGN.md): one screen per
    // tab after the five mock-ups (物品, 装备, 技能, 武将, 系统), plus 兵书 with the military-book screen's four
    // panels. Shared chrome: 行旅 title with the place, copper and reputation, 返回, the objective line, the tab row,
    // and the hint bar. The party column (bust cards) is on every general page. Drawn with IMGUI in the mock-up's
    // pixel space (MenuPaint); 技能 is the generals' own level-up skills only, unrelated to gear and books.
    public sealed partial class MainMenuHud : MonoBehaviour
    {
        [Tooltip("TownStoryController or FieldStoryController (0.2): any IFieldHost")] public MonoBehaviour Host;
        ThreeKingdoms.Gameplay.Story.IFieldHost Controller => Host as ThreeKingdoms.Gameplay.Story.IFieldHost;
        [Tooltip("Full illustrations (武将 page)")] public DialoguePortraitSet Portraits;
        [Tooltip("Bust crops for the party cards: liu_bei, guan_yu, zhang_fei")] public Texture2D[] Busts = Array.Empty<Texture2D>();
        [Tooltip("The menu's own slices (art/travel-menu-ui-slices)")] public Texture2D[] Art = Array.Empty<Texture2D>();
        [Tooltip("The military-book screen's slices (shared frames, gems, scrolls, busts)")] public Texture2D[] BingshuArt = Array.Empty<Texture2D>();
        [Tooltip("Nine-slice borders of the menu's own slices (name,left,right,top,bottom), from their manifest")] public string[] Borders = Array.Empty<string>();

        public enum Mode { Closed, Root }
        public enum Tab { Items, Equip, Skills, Bingshu, Generals, Achievements, System }

        public Mode Current { get; private set; } = Mode.Closed;
        public Tab CurrentTab { get; private set; }
        public string Message { get; private set; } = "";
        public GeneralSpec General => Joined.ElementAtOrDefault(generalIndex);

        MenuPaint paint;
        int openedFrame, generalIndex;
        // Which column has the cursor: 0 the party, 1 the page's main list, 2 its right-hand list.
        int column = 1;
        bool padMode;

        DevPartyProgress Progress => Controller.Run.Progress;
        Inventory Inv => Progress.Inventory;
        List<GeneralSpec> Joined => Progress.Party.Where(g => Controller.Run.Story.HasJoined(g.Id)).ToList();

        void OnEnable()
        {
            if (Controller != null) { Controller.PanelOpened += OnOpened; Controller.PanelClosed += OnClosed; }
        }

        void OnDisable()
        {
            if (Controller != null) { Controller.PanelOpened -= OnOpened; Controller.PanelClosed -= OnClosed; }
        }

        void OnOpened(string p)
        {
            if (p != TownStoryController.MenuPanel) return;
            openedFrame = Time.frameCount;
            generalIndex = 0;
            Message = "";
            Current = Mode.Root;
            Show(Tab.Items);
        }

        void OnClosed(string p) { if (p == TownStoryController.MenuPanel) Current = Mode.Closed; }

        public void Show(Tab tab)
        {
            CurrentTab = tab;
            column = tab == Tab.Generals ? 0 : 1;
            picking = false; confirmLoad = false;
            itemIndex = slotIndex = candidateIndex = skillIndex = 0;
            if (tab == Tab.Bingshu) { plateSlot = 0; bingshuFocus = 0; }
            if (tab == Tab.Achievements) { achIndex = 0; achScroll = 0; }
        }

        public void NextTab(int delta)
        {
            int n = Enum.GetValues(typeof(Tab)).Length;
            Show((Tab)(((int)CurrentTab + delta + n) % n));
        }

        public void SelectGeneral(int delta)
        {
            var joined = Joined;
            if (joined.Count == 0) return;
            generalIndex = (generalIndex + delta + joined.Count) % joined.Count;
            picking = false; candidateIndex = 0;
            if (CurrentTab == Tab.Skills) skillIndex = 0;
            if (CurrentTab == Tab.Bingshu) { plateSlot = 0; libScroll = 0; }
        }

        // B / Backspace: one level back; at the top the menu closes.
        public void Back()
        {
            if (Current != Mode.Root) return;
            if (confirmLoad) { confirmLoad = false; return; }
            if (picking) { picking = false; return; }
            if (CurrentTab == Tab.Bingshu && bingshuFocus != 0) { bingshuFocus = 0; return; }
            if (column == 0 && CurrentTab != Tab.Generals) { column = 1; return; }
            Controller.ClosePanel();
        }

        // A / Enter on whatever has the cursor.
        public void Confirm()
        {
            if (Current != Mode.Root) return;
            if (column == 0 && CurrentTab != Tab.Generals && CurrentTab != Tab.System && CurrentTab != Tab.Bingshu && CurrentTab != Tab.Achievements) { column = 1; return; }
            switch (CurrentTab)
            {
                case Tab.Items: ConfirmItem(); break;
                case Tab.Equip: ConfirmEquip(); break;
                case Tab.Skills: Message = SkillUseBlocked(SkillRows().ElementAtOrDefault(skillIndex)) ?? ""; break;
                case Tab.Bingshu: ConfirmBingshu(); break;
                case Tab.Generals: Show(Tab.Equip); break;
                case Tab.Achievements: ConfirmAchievement(); break;
                case Tab.System: ConfirmSystem(); break;
            }
        }

        // Arrow keys / stick: up and down move in the column, left and right change column (or adjust a setting).
        public void Move(int dx, int dy)
        {
            if (Current != Mode.Root || (dx == 0 && dy == 0)) return;
            switch (CurrentTab)
            {
                case Tab.System: MoveSystem(dx, dy); return;
                case Tab.Bingshu: MoveBingshu(dx, dy); return;
                case Tab.Achievements: MoveAchievements(dx, dy); return;
            }
            if (dx != 0)
            {
                if (CurrentTab == Tab.Generals) return;
                if (dx < 0 && column == 1 && !picking) { column = 0; return; }
                if (dx > 0 && column == 0) { column = 1; return; }
                return;
            }
            if (column == 0 || CurrentTab == Tab.Generals) { SelectGeneral(dy); return; }
            switch (CurrentTab)
            {
                case Tab.Items: itemIndex = Wrap(itemIndex + dy, ItemRows().Count); break;
                case Tab.Equip:
                    if (picking) candidateIndex = Wrap(candidateIndex + dy, Candidates().Count);
                    else slotIndex = Wrap(slotIndex + dy, 3);
                    break;
                case Tab.Skills: skillIndex = Wrap(skillIndex + dy, SkillRows().Count); break;
            }
        }

        static int Wrap(int i, int n) => n <= 0 ? 0 : (i % n + n) % n;

        // ---------- input ----------

        float repeatAt; Vector2Int held;

        void Update()
        {
            if (Current != Mode.Root || replaying || Time.frameCount <= openedFrame) return;
            var kb = Keyboard.current; var pad = Gamepad.current;
            if (pad != null && (pad.leftStick.ReadValue().sqrMagnitude > 0.25f || pad.dpad.ReadValue() != Vector2.zero
                                || pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame || pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame)) padMode = true;
            if (kb != null && kb.anyKey.wasPressedThisFrame) padMode = false;

            bool P(params UnityEngine.InputSystem.Controls.ButtonControl[] keys) => keys.Any(k => k != null && k.wasPressedThisFrame);
            if (P(kb?.qKey, kb?.pageUpKey, pad?.leftShoulder)) { NextTab(-1); return; }
            if (P(kb?.eKey, kb?.pageDownKey, pad?.rightShoulder)) { NextTab(1); return; }
            if (P(kb?.enterKey, kb?.numpadEnterKey, kb?.spaceKey, pad?.buttonSouth)) { Confirm(); return; }
            if (P(kb?.xKey, kb?.backspaceKey, pad?.buttonEast)) { Back(); return; }
            if (P(kb?.tabKey, pad?.buttonNorth)) { Alternate(); return; }
            if (P(kb?.cKey, kb?.deleteKey, pad?.buttonWest)) { Remove(); return; }

            var dir = Vector2Int.zero;
            if (kb != null)
            {
                if (kb.upArrowKey.isPressed || kb.wKey.isPressed) dir.y = -1; else if (kb.downArrowKey.isPressed || kb.sKey.isPressed) dir.y = 1;
                if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) dir.x = -1; else if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) dir.x = 1;
            }
            if (pad != null && dir == Vector2Int.zero)
            {
                var v = pad.dpad.ReadValue() + pad.leftStick.ReadValue();
                if (Mathf.Abs(v.y) > 0.5f) dir.y = v.y > 0 ? -1 : 1;
                else if (Mathf.Abs(v.x) > 0.5f) dir.x = v.x > 0 ? 1 : -1;
            }
            if (dir != held) { held = dir; repeatAt = Time.unscaledTime + 0.35f; Move(dir.x, dir.y); }
            else if (dir != Vector2Int.zero && Time.unscaledTime >= repeatAt) { repeatAt = Time.unscaledTime + 0.09f; Move(dir.x, dir.y); }
        }

        // Y / Tab: 武将 → 技能; 兵书 cycles 命盘 / 藏书阁 / 策略一览; 系统 switches settings / saves.
        public void Alternate()
        {
            if (CurrentTab == Tab.Generals) Show(Tab.Skills);
            else if (CurrentTab == Tab.Bingshu) bingshuFocus = (bingshuFocus + 1) % 3;
            else if (CurrentTab == Tab.System) column = column == 2 ? 1 : 2;
        }

        // X / C: take a book out of the circuit, or the gear off the chosen slot.
        void Remove()
        {
            if (CurrentTab == Tab.Bingshu) RemoveBook();
            else if (CurrentTab == Tab.Equip && !picking) Unequip((EquipSlot)slotIndex);
        }

        // ---------- drawing ----------

        void OnGUI()
        {
            if (Current != Mode.Root || replaying || Controller == null || Controller.Run == null) return;
            if (paint == null)
            {
                paint = new MenuPaint(Art, BingshuArt);
                foreach (var line in Borders)
                {
                    var p = line.Split(',');
                    if (p.Length == 5) paint.SetBorder(p[0], new Vector4(float.Parse(p[1]), float.Parse(p[2]), float.Parse(p[3]), float.Parse(p[4])));
                }
            }
            GUI.depth = -10;
            paint.Begin();
            // The town stays visible behind, dimmed and cooled like the mock-ups' night street.
            paint.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.02f, 0.05f, 0.10f, 0.62f));
            DrawChrome();
            switch (CurrentTab)
            {
                case Tab.Items: DrawParty(); DrawItems(); break;
                case Tab.Equip: DrawParty(); DrawEquip(); break;
                case Tab.Skills: DrawParty(); DrawSkills(); break;
                case Tab.Bingshu: DrawBingshu(); break;
                case Tab.Generals: DrawParty(); DrawGeneral(); break;
                case Tab.Achievements: DrawAchievements(); break;
                case Tab.System: DrawSystem(); break;
            }
            DrawHints();
        }

        static readonly string[] TabNames = { "物品", "装备", "技能", "兵书", "武将", "成就", "系统" };
        public static string TabName(Tab t) => TabNames[(int)t];

        Color SilverA(float a) => new Color(MenuPaint.Silver.r, MenuPaint.Silver.g, MenuPaint.Silver.b, a);

        void DrawChrome()
        {
            var p = paint;
            var bar = new Color(0.03f, 0.06f, 0.12f, 0.97f);   // opaque: hides the town objective plaque underneath
            p.Fill(new Rect(0, 0, Screen.width, p.R(0, 0, 1, 76).height), bar);
            p.Fill(new Rect(0, p.R(0, 74, 1, 1).y, Screen.width, Mathf.Max(1.5f, 1.5f * p.K)), SilverA(0.55f));
            p.Text(78, 4, 140, 68, "行旅", 50, MenuPaint.Ivory, TextAnchor.MiddleLeft, true);
            p.Text(198, 24, 300, 44, "/  " + MenuText.Place(Controller), 22, MenuPaint.Mist);
            if (!p.Icon(1236, 28, 32, 32, "icon_coin")) { p.Disc(p.P(1252, 44), 14 * p.K, MenuPaint.Gold); p.Disc(p.P(1252, 44), 5 * p.K, MenuPaint.Base); }
            p.Text(1284, 22, 90, 44, Inv.Copper.ToString(), 26, MenuPaint.Ivory);
            if (!p.Icon(1376, 28, 32, 32, "icon_reputation")) p.Text(1376, 22, 32, 44, "✦", 26, MenuPaint.Gold, TextAnchor.MiddleCenter);
            p.Text(1420, 22, 60, 44, Controller.Run.Life.Reputation.ToString(), 26, MenuPaint.Ivory);
            Button(1494, 20, 152, 48, "‹  返回", ButtonKind.Back, () => Controller.ClosePanel(), size: 24);

            // Objective line.
            if (!p.Icon(48, 87, 28, 28, "icon_quest")) p.Text(48, 84, 30, 32, "◇", 26, MenuPaint.Gold, TextAnchor.MiddleCenter, true);
            p.Text(92, 82, 380, 36, MenuText.Quest(Controller.Run.Story, Controller.Run.Chapter2), 21, MenuPaint.Ivory);
            p.HLine(40, 120, 410, SilverA(0.45f));

            // Tabs.
            for (int i = 0; i < TabNames.Length; i++)
            {
                bool sel = (int)CurrentTab == i;
                var r = p.R(470 + i * 153, 88, 150, 52);   // seven tabs since 成就 (2026-10-26)
                p.Frame(r, sel ? "tab_selected" : "tab", sel ? MenuPaint.Cyan : (Color?)null, sel ? new Color(MenuPaint.SelectBg.r, MenuPaint.SelectBg.g, MenuPaint.SelectBg.b, 0.92f) : (Color?)null);
                GUI.Label(r, TabNames[i], p.St(28, sel ? Color.white : MenuPaint.Ivory, TextAnchor.MiddleCenter, true));
                int idx = i;
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) Show((Tab)idx);
            }
        }

        void DrawHints()
        {
            var p = paint;
            float top = p.R(0, 872, 1, 1).y;
            p.Fill(new Rect(0, top, Screen.width, Screen.height - top), new Color(0.02f, 0.04f, 0.08f, 0.78f));
            p.Fill(new Rect(0, top, Screen.width, Mathf.Max(1f, p.K)), SilverA(0.45f));
            float x = 48;
            void Key(string k, float w = 46) { p.Keycap(x, 887, w, k); x += w + 6; }
            void Label(string t) { x += 8; p.Text(x, 884, 200, 42, t, 21, MenuPaint.Ivory); x += t.Length * 21 + 30; p.Fill(x - 15, 891, 1, 28, SilverA(0.45f)); }
            if (padMode) { Key("LB"); Key("RB"); } else { Key("Q", 38); Key("E", 38); }
            Label("切换分页");
            Key("↑", 34); Key("↓", 34); Label("选择");
            foreach (var (pad, key, label) in HintsFor())
            {
                Key(padMode ? pad : key, padMode ? 34 : Mathf.Max(38, key.Length * 13 + 18));
                Label(label);
            }
            if (!string.IsNullOrEmpty(Message)) p.Text(980, 884, 660, 42, Message, 21, MenuPaint.Gold, TextAnchor.MiddleRight);
        }

        IEnumerable<(string pad, string key, string label)> HintsFor()
        {
            switch (CurrentTab)
            {
                case Tab.Equip: yield return ("A", "Enter", picking && Candidates().ElementAtOrDefault(candidateIndex).worn ? "卸下" : "装备"); break;
                case Tab.Skills: yield return ("A", "Enter", "使用"); break;
                case Tab.Generals: yield return ("A", "Enter", "查看装备"); yield return ("Y", "Tab", "查看技能"); break;
                case Tab.Achievements: yield return ("A", "Enter", "重看画卷"); break;
                case Tab.Bingshu: yield return ("A", "Enter", "镶嵌兵书"); yield return ("X", "C", "卸下"); yield return ("Y", "Tab", "切换列表"); break;
                default: yield return ("A", "Enter", "确认"); break;
            }
            yield return ("B", "X", "返回");
        }

        enum ButtonKind { Normal, Primary, Disabled, Back }

        // A framed button in mock pixels; true when clicked (never when disabled).
        bool Button(float x, float y, float w, float h, string text, ButtonKind kind, Action onClick = null, string icon = null, float size = 28, bool focused = false)
        {
            var p = paint;
            var r = p.R(x, y, w, h);
            string slice = kind switch { ButtonKind.Primary => "button_primary", ButtonKind.Disabled => "button_disabled", ButtonKind.Back => "button_back", _ => "button" };
            if (focused && kind == ButtonKind.Normal) slice = "button_primary";
            var edge = kind == ButtonKind.Primary || focused ? MenuPaint.Cyan : kind == ButtonKind.Disabled ? new Color(0.4f, 0.45f, 0.5f) : MenuPaint.Silver;
            var fill = kind == ButtonKind.Disabled ? new Color(0.20f, 0.24f, 0.28f, 0.92f) : kind == ButtonKind.Primary || focused ? new Color(0.06f, 0.20f, 0.28f, 0.95f) : new Color(0.04f, 0.08f, 0.15f, 0.92f);
            p.Frame(r, slice, edge, fill);
            if (focused) p.Outline(r, MenuPaint.Cyan, Mathf.Max(1.5f, 2 * p.K));
            var color = kind == ButtonKind.Disabled ? new Color(0.62f, 0.66f, 0.70f) : MenuPaint.Ivory;
            if (icon != null)
            {
                if (!p.Icon(x + 26, y + h / 2 - 17, 34, 34, icon, tint: kind == ButtonKind.Disabled ? new Color(1, 1, 1, 0.5f) : (Color?)null))
                    p.Text(x + 26, y + h / 2 - 17, 34, 34, IconGlyph(icon), 24, color, TextAnchor.MiddleCenter);
                p.Fill(x + 76, y + 10, 1, h - 20, SilverA(0.4f));
                GUI.Label(p.R(x + 78, y, w - 88, h), text, p.St(size, color, TextAnchor.MiddleCenter, true));
            }
            else GUI.Label(r, text, p.St(size, color, TextAnchor.MiddleCenter, true));
            if (kind != ButtonKind.Disabled && GUI.Button(r, GUIContent.none, GUIStyle.none)) { onClick?.Invoke(); return true; }
            return false;
        }

        static string IconGlyph(string icon) => icon switch
        {
            "icon_home" => "⌂", "icon_exit" => "⇥", "icon_lock" => "🔒", "icon_info" => "i", "icon_check" => "✓", _ => "·",
        };

        // ---------- the party column ----------

        Texture2D Bust(string id)
        {
            int i = Array.IndexOf(new[] { "liu_bei", "guan_yu", "zhang_fei" }, id);
            return i >= 0 && i < Busts.Length ? Busts[i] : null;
        }

        static string TroopIcon(TroopType t) => t switch { TroopType.Archer => "troop_archer", TroopType.Cavalry => "troop_cavalry", _ => "troop_infantry" };

        void TroopMark(float x, float y, float s, TroopType t)
        {
            if (paint.Icon(x, y, s, s, TroopIcon(t))) return;
            paint.Text(x, y, s, s, t == TroopType.Archer ? "⊗" : t == TroopType.Cavalry ? "♞" : "⛉", s * 0.85f, MenuPaint.Mist, TextAnchor.MiddleCenter);
        }

        void DrawParty()
        {
            var p = paint;
            p.PanelBox(30, 158, 422, 690, "队伍");
            var joined = Joined;
            for (int i = 0; i < joined.Count && i < 3; i++)
            {
                var g = joined[i];
                float y = 224 + i * 210;
                PartyCard(40, y, 402, 196, g, i == generalIndex, column == 0 || CurrentTab == Tab.Generals || CurrentTab == Tab.Items);
                int idx = i;
                if (GUI.Button(p.R(40, y, 402, 196), GUIContent.none, GUIStyle.none)) { generalIndex = idx; picking = false; if (CurrentTab != Tab.Generals) column = 0; }
            }
        }

        void PartyCard(float x, float y, float w, float h, GeneralSpec g, bool sel, bool pointer)
        {
            var p = paint;
            p.Frame(x, y, w, h, sel ? "party_card_selected" : "party_card", sel ? MenuPaint.Cyan : (Color?)null);
            var bust = Bust(g.Id);
            if (bust) GUI.DrawTexture(p.R(x + 6, y + 6, 190, h - 12), bust, ScaleMode.ScaleAndCrop);
            if (sel && !p.Has("party_card_selected")) p.Outline(p.R(x + 2, y + 2, w - 4, h - 4), MenuPaint.Cyan, Mathf.Max(1.5f, 2 * p.K));
            if (sel && pointer) Pointer(x - 12, y + h / 2 - 26);
            var s = Progress.Stats(g, Inv.Bonus(g.Id));
            p.Text(x + 195, y + 16, 140, 52, g.DisplayName, 34, MenuPaint.Ivory, TextAnchor.MiddleLeft, true);
            p.Text(x + 280, y + 20, 112, 44, $"Lv.{Progress.Level(g)}", 26, MenuPaint.Gold, TextAnchor.MiddleRight);
            p.HLine(x + 195, y + 70, 200, SilverA(0.35f));
            TroopMark(x + 196, y + 78, 30, g.Troop);
            p.Text(x + 240, y + 74, 120, 38, BattleTroop(g.Troop), 20, MenuPaint.Ivory);
            BarRow(x + 152, y + 124, "兵力", s.MaxTroops, s.MaxTroops, MenuPaint.Jade, 100);
            BarRow(x + 152, y + 152, "技力", s.MaxTech, s.MaxTech, MenuPaint.Sky, 100);
        }

        void BarRow(float x, float y, string label, int value, int max, Color c, float barW, float size = 19, string sep = "/")
        {
            var p = paint;
            p.Text(x, y, 50, 26, label, size, MenuPaint.Ivory);
            p.Bar(x + 50, y + 7, barW, 12, max > 0 ? value / (float)max : 0, c);
            p.Text(x + 62 + barW, y, 120, 26, $"{value}{sep}{max}", size, MenuPaint.Ivory);
        }

        // The ▷ pointer left of the focused row.
        void Pointer(float x, float y)
        {
            if (paint.Icon(x, y, 22, 52, "select_arrow")) return;
            paint.Text(x, y, 22, 52, "▶", 22, MenuPaint.Cyan, TextAnchor.MiddleCenter);
        }

        static string BattleTroop(TroopType t) => ThreeKingdoms.UI.Battle.BattleText.Troop(t);

        void Row(float x, float y, float w, float h, bool sel)
        {
            paint.Frame(x, y, w, h, sel ? "slot_row_selected" : "slot_row", sel ? MenuPaint.Cyan : (Color?)null,
                sel ? new Color(MenuPaint.SelectBg.r, MenuPaint.SelectBg.g, MenuPaint.SelectBg.b, 0.85f) : (Color?)null);
            if (sel && !paint.Has("slot_row_selected")) paint.Outline(paint.R(x, y, w, h), MenuPaint.Cyan, Mathf.Max(1.5f, 2 * paint.K));
        }

        // A small info line with the (i) mark, as under 使用 in the mock-ups.
        void Info(float x, float y, string text, float size = 20)
        {
            if (!paint.Icon(x, y + 4, 30, 30, "icon_info")) { paint.Disc(paint.P(x + 15, y + 19), 13 * paint.K, MenuPaint.Mist); paint.Text(x, y + 4, 30, 30, "i", 18, MenuPaint.Base, TextAnchor.MiddleCenter, true); }
            paint.Text(x + 42, y, 500, 38, text, size, MenuPaint.Ivory);
        }
    }
}
