using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.UI.Story;
using UnityEngine;

namespace ThreeKingdoms.UI.Menu
{
    // 史事 (史事簿, design/gdd/prologue.md §1, layout after art/prologue-ui-concept TRAVEL_MENU_CHRONICLE.png): the
    // chronicle of what has been told. 0.2 has one entry, the prologue 天下大势; confirm replays it where Liu Bei stands.
    // The right panel lists the entry's historical sources; up / down opens one at a time. Later entries are locked cards.
    public sealed partial class MainMenuHud
    {
        const int ChronicleLocked = 3;   // placeholders under the one entry 0.2 has
        int sourceIndex;
        Texture2D chronicleThumb;

        public int SourceIndex => sourceIndex;

        void ConfirmChronicle() => Controller.PlayChronicle(TownStoryController.PrologueEntry);

        public void ToggleNarration() => GameSettings.ClassicNarration = !GameSettings.ClassicNarration;

        void MoveChronicle(int dy)
        {
            if (dy != 0) sourceIndex = Wrap(sourceIndex + dy, PrologueText.Sources.Length);
        }

        void DrawChronicle()
        {
            var p = paint;
            if (!chronicleThumb) chronicleThumb = Resources.Load<Texture2D>("Prologue/map_base") ?? Resources.Load<Texture2D>("Prologue/scroll_1");

            // Left: the entries (fixed cards, own scroll track); later ones are locked placeholders.
            p.PanelBox(28, 156, 668, 694, "史事簿", "已解锁   1 / " + (ChronicleLocked + 1));
            var card = p.R(58, 232, 588, 126);
            p.Frame(card, "slot_row_selected", MenuPaint.Cyan, new Color(MenuPaint.SelectBg.r, MenuPaint.SelectBg.g, MenuPaint.SelectBg.b, 0.92f));
            Thumb(p.R(78, 248, 150, 94));
            p.Text(248, 240, 380, 34, "中平元年前 · 序章", 21, MenuPaint.Mist);
            p.Text(248, 276, 380, 46, "天下大势", 32, MenuPaint.Ivory, TextAnchor.MiddleLeft, true);
            p.Text(248, 322, 380, 30, "已解锁 · 可重看", 18, MenuPaint.Mist);
            if (!p.Icon(38, 284, 18, 22, "select_arrow")) p.Text(36, 280, 22, 30, "◆", 16, MenuPaint.Cyan, TextAnchor.MiddleCenter);
            if (GUI.Button(card, GUIContent.none, GUIStyle.none)) ConfirmChronicle();
            for (int i = 0; i < ChronicleLocked; i++)
            {
                float y = 374 + i * 142;
                p.Frame(58, y, 588, 126, "slot_row", SilverA(0.45f), new Color(0.06f, 0.11f, 0.18f, 0.85f));
                p.Frame(78, y + 16, 150, 94, "art_frame", SilverA(0.35f), new Color(0.12f, 0.19f, 0.27f, 0.9f));
                p.Text(78, y + 16, 150, 94, "？", 30, MenuPaint.Grey, TextAnchor.MiddleCenter);
                p.Text(248, y + 16, 380, 44, "？ ？ ？", 26, MenuPaint.Grey);
                p.Text(248, y + 70, 380, 36, "尚未解锁", 22, MenuPaint.Grey);
            }
            p.Fill(662, 232, 3, 560, SilverA(0.25f));
            p.Fill(662, 232, 3, 140, SilverA(0.7f));
            p.HLine(58, 804, 588, SilverA(0.3f));
            p.Text(66, 808, 400, 34, "随剧情推进解锁", 19, MenuPaint.Mist);

            // Right: the entry, its sources (one open at a time), and the replay button.
            p.PanelBox(718, 156, 926, 694, "条目详情", "可重看");
            Thumb(p.R(748, 232, 320, 180));
            p.Outline(p.R(748, 232, 320, 180), SilverA(0.6f), Mathf.Max(1, p.K));
            p.Text(1092, 232, 520, 46, PrologueText.EntryTitle, 32, MenuPaint.Ivory, TextAnchor.MiddleLeft, true);
            p.Text(1092, 280, 520, 32, PrologueText.EntryWhen, 21, MenuPaint.Mist);
            p.Text(1092, 318, 524, 120, PrologueText.EntrySummary, 21, MenuPaint.Ivory, TextAnchor.UpperLeft, false, true);
            p.HLine(748, 444, 862, SilverA(0.35f));
            p.Text(750, 452, 300, 44, "史实依据", 29, MenuPaint.Ivory, TextAnchor.MiddleLeft, true);
            p.Text(1300, 456, 310, 40, "↑ ↓ 展开说明与出处", 18, MenuPaint.Mist, TextAnchor.MiddleRight);
            var src = PrologueText.Sources;
            float ry = 500;
            for (int i = 0; i < src.Length; i++)
            {
                bool open = i == sourceIndex;
                float h = open ? 112 : 42;
                if (open) p.Fill(748, ry, 862, h, new Color(MenuPaint.Base.r, MenuPaint.Base.g, MenuPaint.Base.b, 0.75f));
                p.HLine(748, ry + h, 862, SilverA(0.25f));
                p.Text(756, ry, 24, 42, open ? "▼" : "›", open ? 14 : 22, open ? MenuPaint.Cyan : MenuPaint.Mist, TextAnchor.MiddleCenter);
                p.Text(784, ry, 600, 42, src[i].Title, 23, MenuPaint.Ivory);
                p.Text(1440, ry, 160, 42, SourceKind(src[i].Title), 17, MenuPaint.Mist, TextAnchor.MiddleRight);
                if (open) p.Text(784, ry + 42, 816, 68, src[i].Text, 19, MenuPaint.Ivory, TextAnchor.UpperLeft, false, true);
                int idx = i;
                if (GUI.Button(p.R(748, ry, 862, 42), GUIContent.none, GUIStyle.none)) sourceIndex = idx;
                ry += h;
            }
            Button(1138, 792, 298, 42, "重看序章", ButtonKind.Normal, ConfirmChronicle, size: 26);
            // 旁白文体 (user 2026-10-28): modern or 演义体; Y / Tab or a click switches, kept per machine.
            bool classic = GameSettings.ClassicNarration;
            p.Text(752, 792, 120, 42, "旁白", 21, MenuPaint.Mist);
            for (int i = 0; i < 2; i++)
            {
                bool on = (i == 1) == classic;
                var r = p.R(810 + i * 132, 794, 126, 38);
                p.Frame(r, on ? "tab_selected" : "slot_row", on ? MenuPaint.Cyan : SilverA(0.4f), on ? new Color(MenuPaint.SelectBg.r, MenuPaint.SelectBg.g, MenuPaint.SelectBg.b, 0.92f) : new Color(0.06f, 0.11f, 0.18f, 0.85f));
                GUI.Label(r, i == 0 ? PrologueText.StyleModern : PrologueText.StyleClassic, p.St(20, on ? Color.white : MenuPaint.Mist, TextAnchor.MiddleCenter));
                bool pick = i == 1;
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) GameSettings.ClassicNarration = pick;
            }
        }

        // 史实 unless the title carries its kind in 〔〕.
        static string SourceKind(string title)
        {
            int a = title.IndexOf('〔'), b = title.IndexOf('〕');
            return a >= 0 && b > a ? title.Substring(a + 1, b - a - 1) : "史实";
        }

        void Thumb(Rect r)
        {
            if (chronicleThumb) GUI.DrawTexture(r, chronicleThumb, ScaleMode.ScaleAndCrop);
            else paint.Fill(r, new Color(0.80f, 0.76f, 0.66f));
        }
    }
}
