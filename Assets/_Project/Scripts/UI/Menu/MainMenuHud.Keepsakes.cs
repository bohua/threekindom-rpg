using System.Linq;
using ThreeKingdoms.Gameplay.Story;
using UnityEngine;

namespace ThreeKingdoms.UI.Menu
{
    // ================= 物品｜信物 =================
    // The keepsake shelf (design/gdd/keepsake-shelf.md, Codex art/keepsake-shelf-ui): a sub-page of 物品. Two columns of
    // keepsake cards on the left (held: picture, name, the bound person's head; not yet: an empty frame and 未得), the
    // chosen keepsake on the right: picture beside the person's portrait, name, where and when, origin, description, 已得 N／总数.
    public sealed partial class MainMenuHud
    {
        const int KeepColumns = 2, KeepRowsShown = 5;
        int itemPage, keepIndex, keepScroll;
        public bool KeepsakePage => CurrentTab == Tab.Items && itemPage == 1;
        public int KeepsakeIndex => keepIndex;

        static readonly string[] ItemPages = { "物品", "信物" };

        public void ShowItemPage(int page)
        {
            itemPage = Mathf.Clamp(page, 0, 1);
            keepIndex = keepScroll = 0;
            column = 1;
        }

        bool KeepHeld(KeepsakeCatalog.Entry e) => KeepsakeCatalog.Held(Controller.Run.Chapter2, e);

        void MoveKeepsakes(int dx, int dy)
        {
            int n = KeepsakeCatalog.Shown.Count();
            int next = keepIndex + dx + dy * KeepColumns;
            if (next < 0 || next >= n) return;
            keepIndex = next;
            int row = keepIndex / KeepColumns;
            if (row < keepScroll) keepScroll = row;
            if (row >= keepScroll + KeepRowsShown) keepScroll = row - KeepRowsShown + 1;
        }

        void DrawItemPages()
        {
            var p = paint;
            for (int i = 0; i < ItemPages.Length; i++)
            {
                bool sel = itemPage == i;
                var r = p.R(40 + i * 132, 128, 124, 28);
                if (sel) p.Fill(r, new Color(MenuPaint.SelectBg.r, MenuPaint.SelectBg.g, MenuPaint.SelectBg.b, 0.85f));
                if (sel) p.Fill(40 + i * 132, 154, 124, 2, MenuPaint.Cyan);
                GUI.Label(r, ItemPages[i], p.St(20, sel ? Color.white : MenuPaint.Mist, TextAnchor.MiddleCenter, sel));
                int page = i;
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) ShowItemPage(page);
            }
            p.Text(172, 128, 8, 28, "｜", 18, SilverA(0.6f), TextAnchor.MiddleCenter);
        }

        void DrawKeepsakes()
        {
            var p = paint;
            var all = KeepsakeCatalog.Shown.ToArray();
            int got = all.Count(KeepHeld);
            keepIndex = Mathf.Clamp(keepIndex, 0, Mathf.Max(0, all.Length - 1));
            p.PanelBox(30, 168, 966, 680, "信物");
            int rows = (all.Length + KeepColumns - 1) / KeepColumns;
            for (int r = keepScroll; r < Mathf.Min(rows, keepScroll + KeepRowsShown); r++)
            for (int c = 0; c < KeepColumns; c++)
            {
                int i = r * KeepColumns + c;
                if (i >= all.Length) break;
                float x = 52 + c * 455, y = 236 + (r - keepScroll) * 118;
                KeepsakeCard(x, y, 437, 106, all[i], i == keepIndex);
                int idx = i;
                if (GUI.Button(p.R(x, y, 437, 106), GUIContent.none, GUIStyle.none)) keepIndex = idx;
            }
            ScrollBar(970, 240, 826, keepScroll, KeepRowsShown, rows);
            p.Text(66, 812, 600, 30, "信物不能使用、出售，随存档保存", 17, MenuPaint.Mist);

            DrawKeepsakeDetail(all.ElementAtOrDefault(keepIndex), got, all.Length);
        }

        void KeepsakeCard(float x, float y, float w, float h, KeepsakeCatalog.Entry e, bool sel)
        {
            var p = paint;
            bool held = KeepHeld(e);
            Row(x, y, w, h, sel);
            if (sel) Pointer(x - 16, y + h / 2 - 26);
            var pic = p.R(x + 12, y + 11, h - 22, h - 22);
            p.Fill(pic, new Color(0.04f, 0.08f, 0.14f, held ? 0.9f : 0.5f));
            p.Outline(pic, SilverA(held ? 0.5f : 0.25f), 1);
            if (!held)
            {
                p.Text(x + h + 4, y, w - h - 20, h, "未得", 26, new Color(MenuPaint.Mist.r, MenuPaint.Mist.g, MenuPaint.Mist.b, 0.55f), TextAnchor.MiddleLeft, true);
                return;
            }
            KeepsakePicture(x + 12, y + 11, h - 22, e);
            p.Text(x + h + 4, y + 8, w - h - 90, 56, e.Name, 26, MenuPaint.Ivory, TextAnchor.MiddleLeft, true);
            p.Text(x + h + 4, y + 58, w - h - 90, 36, e.WhoName, 19, MenuPaint.Mist);
            PersonHead(x + w - 78, y + h - 74, 62, e, true);
        }

        void KeepsakePicture(float x, float y, float s, KeepsakeCatalog.Entry e)
        {
            if (paint.Icon(x, y, s, s, e.Icon)) return;
            paint.Text(x, y, s, s, e.Name.Substring(0, 1), s * 0.45f, MenuPaint.Ivory, TextAnchor.MiddleCenter, true);
        }

        // The bound person: their own keepsake bust if the shelf has one, else the dialogue portrait's head and chest.
        // On a card it is round with a silver ring, as in the design image (art/keepsake-shelf-ui).
        void PersonHead(float x, float y, float s, KeepsakeCatalog.Entry e, bool round)
        {
            var p = paint;
            var r = p.R(x, y, s, s);
            Texture src = p.Art("keepsake_bust_" + e.Who);
            var uv = new Rect(0, 0, 1, 1);
            if (!src && Portraits != null && Portraits.TryGet(e.Who, out var entry) && entry.Texture)
            {
                src = entry.Texture;
                float w = 0.46f, h = w * entry.Texture.width / entry.Texture.height;
                uv = new Rect(Mathf.Clamp01(entry.Axis - w / 2), 1f - h - 0.04f, w, h);
            }
            if (!src)
            {
                p.Disc(p.P(x + s / 2, y + s / 2), s * 0.48f * p.K, new Color(0.16f, 0.25f, 0.34f));
                p.Text(x, y, s, s, e.WhoName.Substring(0, 1), s * 0.5f, MenuPaint.Ivory, TextAnchor.MiddleCenter, true);
                return;
            }
            if (!round)
            {
                if (src is Texture2D && uv.width >= 1f) GUI.DrawTexture(r, src, ScaleMode.ScaleAndCrop);
                else GUI.DrawTextureWithTexCoords(r, src, uv);
                return;
            }
            p.Disc(p.P(x + s / 2, y + s / 2), s * 0.53f * p.K, SilverA(0.7f));
            GUI.DrawTexture(r, RoundHead(e.Who, src, uv), ScaleMode.StretchToFill);
        }

        readonly System.Collections.Generic.Dictionary<string, Texture2D> roundHeads = new System.Collections.Generic.Dictionary<string, Texture2D>();

        // A circular crop of the head, made once per person: blit the head into a small render texture, read it back
        // and clear the corners (the source textures are not readable, and IMGUI has no masks).
        Texture2D RoundHead(string who, Texture src, Rect uv)
        {
            if (roundHeads.TryGetValue(who, out var done) && done) return done;
            const int n = 128;
            var rt = RenderTexture.GetTemporary(n, n, 0, RenderTextureFormat.ARGB32);
            var was = RenderTexture.active;
            Graphics.Blit(src, rt, new Vector2(uv.width, uv.height), new Vector2(uv.x, uv.y));
            RenderTexture.active = rt;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            t.ReadPixels(new Rect(0, 0, n, n), 0, 0);
            RenderTexture.active = was;
            RenderTexture.ReleaseTemporary(rt);
            var px = t.GetPixels32();
            float c = (n - 1) / 2f, radius = n / 2f - 1f;
            for (int yy = 0; yy < n; yy++)
            for (int xx = 0; xx < n; xx++)
            {
                float d = Mathf.Sqrt((xx - c) * (xx - c) + (yy - c) * (yy - c));
                var q = px[yy * n + xx];
                q.a = (byte)(q.a * Mathf.Clamp01(radius - d));
                px[yy * n + xx] = q;
            }
            t.SetPixels32(px);
            t.Apply();
            roundHeads[who] = t;
            return t;
        }

        void DrawKeepsakeDetail(KeepsakeCatalog.Entry e, int got, int total)
        {
            var p = paint;
            p.PanelBox(1016, 168, 626, 680, "信物详情");
            p.HLine(1032, 778, 594, SilverA(0.35f));
            p.Text(1040, 788, 80, 46, "已得", 21, MenuPaint.Mist);
            p.Text(1104, 786, 200, 50, $"{got} ／ {total}", 30, MenuPaint.Gold);
            if (e == null) return;
            bool held = KeepHeld(e);
            p.Frame(1032, 232, 330, 278, "art_frame", SilverA(0.6f), new Color(0.05f, 0.09f, 0.16f, 0.95f));
            p.Frame(1374, 232, 252, 278, "art_frame", SilverA(0.5f), new Color(0.05f, 0.09f, 0.16f, 0.95f));
            if (!held)
            {
                p.Text(1036, 528, 560, 56, "未得", 36, MenuPaint.Mist, TextAnchor.MiddleLeft, true);
                p.HLine(1036, 590, 586, SilverA(0.35f));
                p.Text(1036, 604, 586, 80, "支线中结识的人，会把东西交到手里。", 20, MenuPaint.Mist, TextAnchor.UpperLeft, false, true);
                return;
            }
            KeepsakePicture(1062, 241, 260, e);
            PersonHead(1378, 236, 244, e, false);
            p.Fill(1376, 476, 248, 32, new Color(0.03f, 0.06f, 0.11f, 0.85f));
            p.Text(1376, 476, 248, 32, "绑定：" + e.WhoName, 18, MenuPaint.Mist, TextAnchor.MiddleCenter);
            p.Text(1036, 528, 586, 56, e.Name, 34, MenuPaint.Ivory, TextAnchor.MiddleLeft, true);
            p.HLine(1036, 588, 586, SilverA(0.35f));
            p.Text(1036, 596, 586, 40, $"得于：{e.Place}·{e.Year}", 20, MenuPaint.Mist);
            p.HLine(1036, 640, 586, SilverA(0.2f));
            p.Text(1036, 648, 70, 34, "来历：", 19, MenuPaint.Mist);
            p.Text(1100, 648, 520, 64, e.Origin, 20, MenuPaint.Ivory, TextAnchor.UpperLeft, false, true);
            p.Text(1036, 712, 70, 34, "描述：", 19, MenuPaint.Mist);
            p.Text(1100, 712, 520, 64, e.Description, 20, MenuPaint.Ivory, TextAnchor.UpperLeft, false, true);
        }

        // 信物入栏 where no StageHud shows it (the town of Zhuojun: the stone-classic rubbing kept at Lu's house). Same place
        // and look as StageHud's memory toast: bottom right, a silver-edged panel, a cyan diamond, two seconds.
        ThreeKingdoms.Core.Story.ChapterState keepWatched;
        readonly System.Collections.Generic.Queue<string> keepToasts = new System.Collections.Generic.Queue<string>();
        string keepToast; float keepToastUntil;
        public string ShownKeepsakeToast => keepToast;

        void WatchKeepsakes(ThreeKingdoms.Core.Story.ChapterState ch)
        {
            if (ch == keepWatched) return;
            if (keepWatched != null) keepWatched.Flagged -= OnKeepsakeFlag;
            keepWatched = ch;
            if (keepWatched != null) keepWatched.Flagged += OnKeepsakeFlag;
        }

        void OnKeepsakeFlag(string flag)
        {
            var k = KeepsakeCatalog.FromFlag(flag);
            if (k != null && FindFirstObjectByType<ThreeKingdoms.UI.Story.StageHud>() == null) keepToasts.Enqueue($"信物入栏：{k.Name}");
        }

        void TickKeepsakeToast()
        {
            if (keepToast != null && Time.unscaledTime >= keepToastUntil) keepToast = null;
            if (keepToast == null && keepToasts.Count > 0) { keepToast = keepToasts.Dequeue(); keepToastUntil = Time.unscaledTime + 2f; }
        }

        void DrawKeepsakeToast()
        {
            if (keepToast == null || Current == Mode.Root) return;
            float k = Screen.height / 1080f;
            var text = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(22 * k), alignment = TextAnchor.MiddleLeft, richText = false };
            text.normal.textColor = MenuPaint.Ivory;
            float w = text.CalcSize(new GUIContent(keepToast)).x + 76 * k, h = 52 * k;
            var r = new Rect(Screen.width - 40 * k - w, Screen.height - 230 * k - h, w, h);
            GUI.depth = -10;
            var o = GUI.color;
            GUI.color = new Color(0.063f, 0.110f, 0.188f, 0.92f); GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = MenuPaint.Silver;
            float b = Mathf.Max(1f, 2 * k);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, b), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(r.x, r.yMax - b, r.width, b), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, b, r.height), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(r.xMax - b, r.y, b, r.height), Texture2D.whiteTexture);
            GUI.color = o;
            var mark = new GUIStyle(text) { fontSize = Mathf.RoundToInt(18 * k) };
            mark.normal.textColor = MenuPaint.Cyan;
            GUI.Label(new Rect(r.x + 22 * k, r.y, 24 * k, h), "◆", mark);
            GUI.Label(new Rect(r.x + 50 * k, r.y, w - 50 * k, h), keepToast, text);
        }
    }
}
