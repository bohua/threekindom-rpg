using System.Collections;
using System.Linq;
using ThreeKingdoms.UI.Story;
using UnityEngine;

namespace ThreeKingdoms.UI.Menu
{
    // ================= 成就 =================
    // User 2026-10-26: every ink scroll is sealed, the seal unlocks that scroll's achievement (kept in the save), and the
    // travel menu gets a page for them. Laid out after Codex art/travel-menu-achievements/ACHIEVEMENTS_MENU.png: the
    // chapter's scrolls as two columns of cards on the left (unlocked: painting, title, year, seal; locked: ？？？), the
    // chosen scroll's painting, text and 重看畫卷 on the right.
    public sealed partial class MainMenuHud
    {
        const int AchColumns = 2, AchRowsShown = 4;
        int achIndex, achScroll;
        bool replaying;
        public int AchievementIndex => achIndex;
        public bool Replaying => replaying;

        static StoryCinematicsView ScrollView => ThreeKingdoms.Gameplay.Story.StoryCinematics.View as StoryCinematicsView;
        bool Unlocked(string id) => Controller.Run.Chapter2.Has(ThreeKingdoms.Core.Story.ChapterState.AchievementPrefix + id);

        void MoveAchievements(int dx, int dy)
        {
            int n = InkScrollText.Chapter1.Length;
            int next = achIndex + dx + dy * AchColumns;
            if (next < 0 || next >= n) return;
            achIndex = next;
            int row = achIndex / AchColumns;
            if (row < achScroll) achScroll = row;
            if (row >= achScroll + AchRowsShown) achScroll = row - AchRowsShown + 1;
        }

        void ConfirmAchievement()
        {
            var id = InkScrollText.Chapter1[achIndex].Id;
            if (!Unlocked(id)) { Message = "此卷尚未解鎖。"; return; }
            if (ScrollView != null && !replaying) StartCoroutine(Replay(id));
        }

        // The scroll again, over the dimmed menu; no achievement line the second time.
        IEnumerator Replay(string id)
        {
            replaying = true;
            try { yield return ScrollView.PlayScroll(id, false); }
            finally { replaying = false; openedFrame = Time.frameCount; }
        }

        void DrawAchievements()
        {
            var p = paint;
            var all = InkScrollText.Chapter1;
            int got = all.Count(s => Unlocked(s.Id));
            p.PanelBox(30, 158, 966, 690, "成就畫卷");
            p.Text(640, 164, 120, 50, "已解鎖", 22, MenuPaint.Mist, TextAnchor.MiddleRight);
            p.Text(766, 162, 100, 52, $"{got} / {all.Length}", 28, MenuPaint.Gold, TextAnchor.MiddleLeft);
            p.Bar(868, 186, 94, 6, got / (float)all.Length, MenuPaint.Cyan);

            var view = ScrollView;
            int rows = (all.Length + AchColumns - 1) / AchColumns;
            for (int r = achScroll; r < Mathf.Min(rows, achScroll + AchRowsShown); r++)
            for (int c = 0; c < AchColumns; c++)
            {
                int i = r * AchColumns + c;
                if (i >= all.Length) break;
                float x = 52 + c * 455, y = 232 + (r - achScroll) * 145;
                AchievementCard(x, y, 437, 130, all[i], i == achIndex, view);
                int idx = i;
                if (GUI.Button(p.R(x, y, 437, 130), GUIContent.none, GUIStyle.none)) achIndex = idx;
            }
            // Scroll bar.
            p.Fill(970, 236, 4, 556, SilverA(0.2f));
            float frac = AchRowsShown / (float)Mathf.Max(AchRowsShown, rows);
            p.Fill(969, 236 + 556 * (achScroll / (float)Mathf.Max(1, rows)), 6, 556 * frac, SilverA(0.75f));
            p.Text(66, 812, 500, 30, "蓋章即解鎖・成就隨存檔保存", 17, MenuPaint.Mist);
            p.Text(700, 812, 262, 30, $"共 {all.Length} 卷", 17, MenuPaint.Mist, TextAnchor.MiddleRight);

            DrawAchievementDetail(all[achIndex], view);
        }

        void AchievementCard(float x, float y, float w, float h, InkScrollText.Chapter1Scroll s, bool sel, StoryCinematicsView view)
        {
            var p = paint;
            bool open = Unlocked(s.Id);
            Row(x, y, w, h, sel);
            if (sel) Pointer(x - 16, y + h / 2 - 26);
            var thumb = p.R(x + 12, y + 12, 142, h - 24);
            var art = open ? view?.PaintingFor(s.Id) : null;
            if (art) GUI.DrawTexture(thumb, art, ScaleMode.ScaleAndCrop);
            else p.Fill(thumb, open ? new Color(0.90f, 0.87f, 0.79f) : new Color(0.55f, 0.57f, 0.60f, 0.85f));
            p.Outline(thumb, SilverA(0.45f), 1);
            p.Text(x + 172, y + 16, 250, 50, open ? s.Title : "？？？", 30, open ? MenuPaint.Ivory : MenuPaint.Mist, TextAnchor.MiddleLeft, true);
            p.Text(x + 172, y + 64, 250, 40, open ? $"{s.Year}年 解鎖" : "尚未解鎖", 21, MenuPaint.Mist);
            if (open) SealMark(x + w - 58, y + h - 58, 44, view);
        }

        void SealMark(float x, float y, float size, StoryCinematicsView view)
        {
            var p = paint;
            var r = p.R(x, y, size, size);
            if (view != null && view.Seal) { GUI.DrawTexture(r, view.Seal, ScaleMode.ScaleToFit); return; }
            p.Fill(r, new Color(0.72f, 0.12f, 0.10f, 0.92f));
            GUI.Label(r, InkScrollText.Seal, p.St(size * 0.32f, new Color(0.93f, 0.90f, 0.82f), TextAnchor.MiddleCenter, true));
        }

        void DrawAchievementDetail(InkScrollText.Chapter1Scroll s, StoryCinematicsView view)
        {
            var p = paint;
            bool open = Unlocked(s.Id);
            p.PanelBox(1016, 158, 626, 690, "畫卷詳情", open ? "已蓋章" : "未蓋章");
            var pic = p.R(1046, 232, 566, 274);
            var art = open ? view?.PaintingFor(s.Id) : null;
            if (art) GUI.DrawTexture(pic, art, ScaleMode.ScaleAndCrop);
            else p.Fill(pic, open ? new Color(0.90f, 0.87f, 0.79f) : new Color(0.32f, 0.36f, 0.42f, 0.85f));
            p.Outline(pic, SilverA(0.5f), 1);
            if (!open)
            {
                p.Text(1048, 522, 500, 56, "？？？", 38, MenuPaint.Mist, TextAnchor.MiddleLeft, true);
                p.Text(1048, 574, 500, 36, "尚未解鎖", 21, MenuPaint.Mist);
                p.HLine(1048, 618, 562, SilverA(0.35f));
                p.Text(1048, 632, 562, 100, "隨劇情推進，畫卷蓋章後解鎖。", 21, MenuPaint.Mist, TextAnchor.UpperLeft, false, true);
                Button(1178, 790, 306, 46, "重看畫卷", ButtonKind.Disabled, size: 26);
                return;
            }
            InkScrollText.TryGet(s.Id, out var entry);
            p.Text(1048, 522, 470, 56, s.Title, 38, MenuPaint.Ivory, TextAnchor.MiddleLeft, true);
            p.Text(1048, 574, 470, 36, $"{s.Year}年 解鎖", 21, MenuPaint.Mist);
            SealMark(1532, 526, 76, view);
            p.HLine(1048, 618, 562, SilverA(0.35f));
            int dash = entry.Body.IndexOf("——", System.StringComparison.Ordinal);
            string story = dash > 0 ? entry.Body.Substring(0, dash) : entry.Body;
            string source = dash > 0 ? entry.Body.Substring(dash) : "";
            p.Text(1048, 628, 566, 118, story, 20, MenuPaint.Ivory, TextAnchor.UpperLeft, false, true);
            p.Text(1048, 740, 566, 42, source, 16, MenuPaint.Mist, TextAnchor.UpperLeft, false, true);
            Button(1178, 790, 306, 46, "重看畫卷", ButtonKind.Primary, ConfirmAchievement, size: 26);
        }
    }
}
