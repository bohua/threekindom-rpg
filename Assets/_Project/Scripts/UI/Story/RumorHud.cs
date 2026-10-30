using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.UI.Battle;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThreeKingdoms.UI.Story
{
    // 天下传闻 (npc-sidequests sq.lb1.r1.jian_yong_ear: the panel opens for the first time when Jian Yong sums up Ji), after
    // the approved Codex design art/rumor-panel-ui/RUMOR_PANEL_DESIGN.png in the unified 苍蓝银辉 style (UI_STYLE_GUIDE.md):
    // the 传闻录 list on the left (fixed cards, a 本地风闻／天下大势 tag, read／unread by icon and brightness, a 新 seal on
    // unread ones, greyed 尚未听闻 cards for what is still to come), the selected rumour on the right with who said what
    // and Jian Yong's note. Up / down chooses (and marks it read), cancel closes.
    [RequireComponent(typeof(UIDocument))]
    public sealed class RumorHud : MonoBehaviour
    {
        [Tooltip("The scene's story controller (any IPanelHost)")] public MonoBehaviour Host;
        IPanelHost Controller => Host as IPanelHost;

        public sealed class Rumour
        {
            public string Id, Flag, Title, Source, Tag, Note;
            public (string who, string line)[] Quotes;
            public string ReadFlag => "rumor_read_" + Id;
        }

        // In the order they are heard (design §R1.4; quotes from the people Liu Bei heard in Ji).
        public static readonly Rumour[] All =
        {
            new Rumour
            {
                Id = "ji_liu_yu", Flag = FieldScriptsR1.RumourJiKind, Title = "刘幽州宽厚", Source = "蓟城 · 市集粥棚", Tag = "本地风闻",
                Quotes = new[] { ("粥棚 · 青州流民妇许氏", "刘使君给分了田。俺这辈子没见过这么宽的官。"), ("东便门 · 通事老达", "胡人进城卖马，他们叫刘使君‘好官’。") },
                Note = "一座城里两种话。一种说宽了没用，一种说打了没完。",
            },
            new Rumour
            {
                Id = "ji_gongsun", Flag = FieldScriptsR1.RumourJiWar, Title = "公孙将军好战", Source = "蓟城 · 燕台酒楼", Tag = "天下大势",
                Quotes = new[] { ("楼上 · 营里的客", "公孙将军见胡人就打，痛快。"), ("楼下 · 州里的客", "打了胡人，明年谁来卖马？") },
                Note = "一座城里两种话。一种说宽了没用，一种说打了没完。",
            },
        };
        const int Placeholders = 2;

        static readonly Color Base = new Color32(0x10, 0x1C, 0x30, 0xF2), Panel = new Color32(0x20, 0x35, 0x4A, 0xFF), Inset = new Color32(0x0B, 0x14, 0x23, 0xFF),
            Ivory = new Color32(0xF4, 0xF1, 0xE8, 0xFF), Mist = new Color32(0xB5, 0xC8, 0xD5, 0xFF), Silver = new Color32(0x8C, 0xA7, 0xBA, 0xFF),
            SelectBg = new Color32(0x16, 0x4B, 0x5E, 0xFF), Cyan = new Color32(0x62, 0xD7, 0xE8, 0xFF), Gold = new Color32(0xD6, 0xBA, 0x78, 0xFF);

        VisualElement root, page, list, detail;
        Label yearLabel, countLabel;
        int selected, openedFrame;

        public bool IsOpen => page != null && page.style.display == DisplayStyle.Flex;
        public IReadOnlyList<Rumour> Heard => Controller?.Run == null ? new Rumour[0] : All.Where(r => Controller.Run.Chapter2.Has(r.Flag)).ToList();
        public string SelectedTitle => Heard.Count > 0 ? Heard[Mathf.Clamp(selected, 0, Heard.Count - 1)].Title : null;

        void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            BattleFont.ApplyTo(root);
            root.pickingMode = PickingMode.Ignore;
            root.style.flexGrow = 1;

            page = new VisualElement();
            page.style.position = Position.Absolute; page.style.left = 0; page.style.right = 0; page.style.top = 0; page.style.bottom = 0;
            page.style.backgroundColor = Base;
            page.style.paddingLeft = 40; page.style.paddingRight = 40; page.style.paddingTop = 24; page.style.paddingBottom = 20;
            root.Add(page);

            var head = Row(page);
            head.style.justifyContent = Justify.SpaceBetween; head.style.alignItems = Align.FlexEnd;
            var titleBox = Row(head); titleBox.style.alignItems = Align.FlexEnd;
            Text(titleBox, "天下传闻", 52, Ivory).style.unityFontStyleAndWeight = FontStyle.Bold;
            Text(titleBox, "  /  行旅", 24, Mist).style.marginBottom = 8;
            yearLabel = Text(head, "", 28, Ivory);
            Text(page, "街谈巷议，皆可留心。", 20, Mist).style.marginBottom = 16;

            var body = Row(page);
            body.style.flexGrow = 1;
            var left = Box(body); left.style.width = Length.Percent(42); left.style.marginRight = 20;
            var leftHead = Row(left); leftHead.style.justifyContent = Justify.SpaceBetween; leftHead.style.marginBottom = 12;
            Text(leftHead, "传闻录", 30, Ivory).style.unityFontStyleAndWeight = FontStyle.Bold;
            countLabel = Text(leftHead, "", 22, Mist);
            list = new ScrollView(ScrollViewMode.Vertical);
            list.style.flexGrow = 1;
            left.Add(list);

            var right = Box(body); right.style.flexGrow = 1;
            Text(right, "传闻详情", 30, Ivory).style.unityFontStyleAndWeight = FontStyle.Bold;
            detail = new VisualElement();
            detail.style.flexGrow = 1; detail.style.marginTop = 14;
            right.Add(detail);

            var foot = Row(page);
            foot.style.marginTop = 14;
            Text(foot, "↑↓ 选择　　确认 记入史事簿　　返回 关闭", 20, Mist);

            if (Controller != null) { Controller.PanelOpened += OnOpened; Controller.PanelClosed += OnClosed; }
            page.style.display = DisplayStyle.None;
        }

        void OnDisable()
        {
            if (Controller != null) { Controller.PanelOpened -= OnOpened; Controller.PanelClosed -= OnClosed; }
        }

        void OnOpened(string p) { if (p == FieldScriptsR1.RumourPanel) Open(); }
        void OnClosed(string p) { if (p == FieldScriptsR1.RumourPanel) page.style.display = DisplayStyle.None; }

        public void Open()
        {
            page.style.display = DisplayStyle.Flex;
            openedFrame = Time.frameCount;
            var heard = Heard;
            // Start on the newest unread one.
            selected = Mathf.Max(0, heard.Count - 1 - heard.Reverse().TakeWhile(r => Controller.Run.Chapter2.Has(r.ReadFlag)).Count());
            if (selected >= heard.Count) selected = heard.Count - 1;
            int y = Controller.Run.Chapter2.Year;
            yearLabel.text = YearName(y);
            Show();
        }

        static string YearName(int y) => y switch
        {
            184 => "中平元年（184）", 185 => "中平二年（185）", 189 => "中平六年（189）", 190 => "初平元年（190）", 191 => "初平二年（191）",
            192 => "初平三年（192）", 193 => "初平四年（193）", 194 => "兴平元年（194）", _ => y + "年",
        };

        public void Select(int delta)
        {
            var heard = Heard;
            if (heard.Count == 0) return;
            selected = Mathf.Clamp(selected + delta, 0, heard.Count - 1);
            Show();
        }

        void Show()
        {
            var heard = Heard;
            countLabel.text = heard.Count > 0 ? $"{"一二三四五六七八九十"[Mathf.Min(heard.Count, 10) - 1]}则" : "";
            if (heard.Count > 0) Controller.Run.Chapter2.Set(heard[selected].ReadFlag);
            list.Clear();
            for (int i = 0; i < heard.Count; i++) list.Add(Card(heard[i], i == selected));
            for (int i = 0; i < Placeholders; i++) list.Add(Unheard());
            detail.Clear();
            if (heard.Count > 0) Detail(heard[selected]);
        }

        VisualElement Card(Rumour r, bool on)
        {
            bool read = Controller.Run.Chapter2.Has(r.ReadFlag);
            var card = Row(null);
            card.style.height = 132; card.style.marginBottom = 12; card.style.alignItems = Align.Center;
            card.style.paddingLeft = 18; card.style.paddingRight = 18;
            card.style.backgroundColor = on ? SelectBg : Panel;
            Border(card, on ? Cyan : Silver, on ? 2 : 1);
            if (on)
            {
                var pointer = new VisualElement();
                pointer.style.position = Position.Absolute; pointer.style.left = -10; pointer.style.top = 56;
                pointer.style.width = 16; pointer.style.height = 16; pointer.style.rotate = new Rotate(45f); pointer.style.backgroundColor = Cyan;
                card.Add(pointer);
            }
            var icon = new VisualElement();
            icon.style.width = 72; icon.style.height = 96; icon.style.marginRight = 22; icon.style.justifyContent = Justify.FlexEnd; icon.style.alignItems = Align.Center;
            icon.style.backgroundColor = Inset; Border(icon, Silver, 1);
            Text(icon, read ? "卷" : "卷", 34, read ? Mist : Gold).style.marginBottom = 10;
            Text(icon, read ? "已读" : "未读", 18, read ? Mist : Ivory).style.marginBottom = 6;
            card.Add(icon);
            var words = new VisualElement(); words.style.flexGrow = 1;
            Text(words, r.Title, 32, read && !on ? Mist : Ivory).style.unityFontStyleAndWeight = FontStyle.Bold;
            Text(words, r.Source, 22, Mist).style.marginTop = 8;
            card.Add(words);
            var tag = Text(card, r.Tag, 20, r.Tag == "天下大势" ? Gold : Ivory);
            tag.style.paddingLeft = 12; tag.style.paddingRight = 12; tag.style.paddingTop = 4; tag.style.paddingBottom = 4;
            Border(tag, r.Tag == "天下大势" ? Gold : Silver, 1);
            if (!read)
            {
                var seal = Text(card, "新", 20, Base);
                seal.style.position = Position.Absolute; seal.style.right = 14; seal.style.top = 10;
                seal.style.backgroundColor = Gold; seal.style.paddingLeft = 6; seal.style.paddingRight = 6;
            }
            return card;
        }

        VisualElement Unheard()
        {
            var card = Row(null);
            card.style.height = 132; card.style.marginBottom = 12; card.style.alignItems = Align.Center;
            card.style.paddingLeft = 18; card.style.backgroundColor = Inset; card.style.opacity = 0.75f;
            Border(card, new Color(Silver.r, Silver.g, Silver.b, 0.5f), 1);
            var icon = new VisualElement(); icon.style.width = 72; icon.style.height = 96; icon.style.marginRight = 22;
            icon.style.justifyContent = Justify.Center; icon.style.alignItems = Align.Center; Border(icon, new Color(Silver.r, Silver.g, Silver.b, 0.4f), 1);
            Text(icon, "卷", 34, new Color(Mist.r, Mist.g, Mist.b, 0.6f));
            card.Add(icon);
            var words = new VisualElement();
            Text(words, "尚未听闻", 32, new Color(Mist.r, Mist.g, Mist.b, 0.8f));
            Text(words, "行旅途中，留意人言", 22, new Color(Mist.r, Mist.g, Mist.b, 0.7f)).style.marginTop = 8;
            card.Add(words);
            return card;
        }

        void Detail(Rumour r)
        {
            var head = Row(detail); head.style.alignItems = Align.Center;
            Text(head, r.Title, 46, Ivory).style.unityFontStyleAndWeight = FontStyle.Bold;
            var tag = Text(head, r.Tag, 22, r.Tag == "天下大势" ? Gold : Ivory);
            tag.style.marginLeft = 20; tag.style.paddingLeft = 12; tag.style.paddingRight = 12; tag.style.paddingTop = 4; tag.style.paddingBottom = 4;
            Border(tag, r.Tag == "天下大势" ? Gold : Silver, 1);
            Text(detail, r.Source, 24, Mist).style.marginTop = 8;
            var by = Text(detail, "简雍记", 22, Cyan);
            by.style.position = Position.Absolute; by.style.right = 0; by.style.top = 10;
            Rule(detail);
            foreach (var (who, line) in r.Quotes)
            {
                Text(detail, who, 22, Mist).style.marginTop = 12;
                var l = Text(detail, line, 32, Ivory); l.style.whiteSpace = WhiteSpace.Normal; l.style.marginLeft = 24; l.style.marginTop = 6;
                Rule(detail);
            }
            var note = new VisualElement();
            note.style.marginTop = 18; note.style.paddingLeft = 22; note.style.paddingRight = 22; note.style.paddingTop = 12; note.style.paddingBottom = 16;
            note.style.backgroundColor = Inset; Border(note, Silver, 1);
            Text(note, "简雍按语", 26, Ivory).style.unityFontStyleAndWeight = FontStyle.Bold;
            var n = Text(note, r.Note, 24, Ivory); n.style.whiteSpace = WhiteSpace.Normal; n.style.marginTop = 10;
            detail.Add(note);
        }

        void Update()
        {
            if (!IsOpen || Time.frameCount == openedFrame) return;
            var k = UnityEngine.InputSystem.Keyboard.current;
            var g = UnityEngine.InputSystem.Gamepad.current;
            if (k != null && (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame) || g != null && g.dpad.up.wasPressedThisFrame) Select(-1);
            if (k != null && (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame) || g != null && g.dpad.down.wasPressedThisFrame) Select(1);
            if (k != null && (k.escapeKey.wasPressedThisFrame || k.xKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame) || g != null && (g.buttonEast.wasPressedThisFrame || g.buttonSouth.wasPressedThisFrame))
                Controller?.ClosePanel();
        }

        // ---------- small builders ----------

        static VisualElement Row(VisualElement parent)
        {
            var r = new VisualElement();
            r.style.flexDirection = FlexDirection.Row;
            r.pickingMode = PickingMode.Ignore;
            parent?.Add(r);
            return r;
        }

        static VisualElement Box(VisualElement parent)
        {
            var b = new VisualElement();
            b.style.backgroundColor = Panel; b.style.paddingLeft = 24; b.style.paddingRight = 24; b.style.paddingTop = 16; b.style.paddingBottom = 16;
            Border(b, Silver, 1);
            parent.Add(b);
            return b;
        }

        static void Rule(VisualElement parent)
        {
            var r = new VisualElement();
            r.style.height = 1; r.style.marginTop = 12; r.style.backgroundColor = new Color(Silver.r, Silver.g, Silver.b, 0.45f);
            parent.Add(r);
        }

        static void Border(VisualElement e, Color c, float w)
        {
            e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = w;
            e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = c;
        }

        static Label Text(VisualElement parent, string text, int size, Color color)
        {
            var l = new Label(text);
            l.pickingMode = PickingMode.Ignore;
            l.style.fontSize = size; l.style.color = color;
            l.style.marginLeft = l.style.marginRight = l.style.paddingLeft = l.style.paddingRight = 0;
            parent?.Add(l);
            return l;
        }
    }
}
