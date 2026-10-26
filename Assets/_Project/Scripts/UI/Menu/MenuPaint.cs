using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ThreeKingdoms.UI.Menu
{
    // IMGUI drawing for the travel menu (行旅, GAME_MENU_UI_REDESIGN.md): every rect is given in the mock-up's own
    // pixels (1672×941) and scaled to the screen height, centred horizontally, so positions can be read straight off
    // the five design PNGs. Art is looked up by file name: the menu's own Codex slices (art/travel-menu-ui-slices)
    // first, then the military-book screen's slices (Prototypes/BingshuTactics/Art/Destiny) for the shared frames
    // (panel_frame, panel_header, card, row, keycap…); anything missing falls back to flat shapes in the same colours.
    public sealed class MenuPaint
    {
        public const float MockW = 1672f, MockH = 941f;

        // UI_STYLE_GUIDE.md colour tokens.
        public static readonly Color Base = Hex(0x101C30), Panel = Hex(0x20354A), Groove = Hex(0x0B1423);
        public static readonly Color Ivory = Hex(0xF4F1E8), Mist = Hex(0xB5C8D5), Silver = Hex(0x8CA7BA);
        public static readonly Color SelectBg = Hex(0x164B5E), Cyan = Hex(0x62D7E8), Gold = Hex(0xD6BA78);
        public static readonly Color Jade = new Color(0.33f, 0.80f, 0.47f), Sky = new Color(0.30f, 0.52f, 0.95f), Amber = new Color(0.93f, 0.72f, 0.32f);
        public static readonly Color Grey = new Color(0.55f, 0.60f, 0.68f), Green = new Color(0.36f, 0.86f, 0.42f), Red = Hex(0xD86B70);

        public float K = 1f, Ox, Oy;   // mock pixel → screen
        readonly Dictionary<string, Texture2D> art = new Dictionary<string, Texture2D>();
        Texture2D white, disc;
        Font font;
        readonly Dictionary<(int, Color, TextAnchor, bool, bool), GUIStyle> styles = new Dictionary<(int, Color, TextAnchor, bool, bool), GUIStyle>();

        // Nine-slice borders (left, right, top, bottom) in texture pixels; Destiny values from its Codex manifest.
        static readonly Dictionary<string, Vector4> Borders = new Dictionary<string, Vector4>
        {
            ["panel_frame"] = new Vector4(34, 34, 34, 34), ["panel_header"] = new Vector4(36, 36, 30, 30), ["title_bar"] = new Vector4(112, 56, 78, 16),
            ["card"] = new Vector4(14, 14, 14, 14), ["card_selected"] = new Vector4(16, 16, 16, 16), ["row"] = new Vector4(11, 11, 11, 11), ["row_lit"] = new Vector4(11, 11, 11, 11),
            ["keycap"] = new Vector4(8, 8, 8, 8), ["button_return"] = new Vector4(22, 58, 30, 30), ["count_badge"] = new Vector4(9, 9, 9, 9),
            ["scroll_track"] = new Vector4(4, 4, 5, 5), ["scroll_thumb"] = new Vector4(3, 3, 6, 6),
        };

        public MenuPaint(IEnumerable<Texture2D> menuArt, IEnumerable<Texture2D> sharedArt)
        {
            foreach (var t in (sharedArt ?? Enumerable.Empty<Texture2D>()).Concat(menuArt ?? Enumerable.Empty<Texture2D>()))
                if (t) art[t.name] = t;   // the menu's own slices win over shared ones of the same name
        }

        // Nine-slice borders for the menu's own slices come from their manifest (filled in by the importer).
        public void SetBorder(string name, Vector4 border) => Borders[name] = border;

        public void Begin()
        {
            K = Screen.height / MockH;
            Ox = (Screen.width - MockW * K) / 2f; Oy = 0;
            if (!white) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
            if (!disc) disc = MakeDisc(64);
            if (!font) font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Microsoft YaHei UI", "SimHei", "SimSun", "DengXian" }, 32);
        }

        public Texture2D Art(string name) => name != null && art.TryGetValue(name, out var t) ? t : null;
        public bool Has(string name) => Art(name);

        public Rect R(float x, float y, float w, float h) => new Rect(Ox + x * K, Oy + y * K, w * K, h * K);
        public Vector2 P(float x, float y) => new Vector2(Ox + x * K, Oy + y * K);
        public int F(float mockPx) => Mathf.Max(8, Mathf.RoundToInt(mockPx * K));

        public GUIStyle St(float size, Color c, TextAnchor a = TextAnchor.MiddleLeft, bool bold = false, bool wrap = false)
        {
            int px = F(size);
            if (styles.TryGetValue((px, c, a, bold, wrap), out var s)) return s;
            s = new GUIStyle(GUI.skin.label) { font = font, fontSize = px, alignment = a, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal, wordWrap = wrap, richText = true, clipping = TextClipping.Overflow, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) };
            s.normal.textColor = c;
            styles[(px, c, a, bold, wrap)] = s;
            return s;
        }

        public void Text(float x, float y, float w, float h, string text, float size, Color c, TextAnchor a = TextAnchor.MiddleLeft, bool bold = false, bool wrap = false) =>
            GUI.Label(R(x, y, w, h), text, St(size, c, a, bold, wrap));

        public void Fill(Rect r, Color c) { var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = o; }
        public void Fill(float x, float y, float w, float h, Color c) => Fill(R(x, y, w, h), c);

        public void Disc(Vector2 centre, float radius, Color c)
        {
            var o = GUI.color; GUI.color = c;
            GUI.DrawTexture(new Rect(centre.x - radius, centre.y - radius, radius * 2, radius * 2), disc);
            GUI.color = o;
        }

        public void Outline(Rect r, Color c, float w)
        {
            Fill(new Rect(r.x, r.y, r.width, w), c); Fill(new Rect(r.x, r.yMax - w, r.width, w), c);
            Fill(new Rect(r.x, r.y, w, r.height), c); Fill(new Rect(r.xMax - w, r.y, w, r.height), c);
        }

        public void HLine(float x, float y, float w, Color c, float thick = 1f) => Fill(new Rect(Ox + x * K, Oy + y * K, w * K, Mathf.Max(1f, thick * K)), c);

        public void Line(Vector2 a, Vector2 b, float w, Color col)
        {
            var m = GUI.matrix; var o = GUI.color; GUI.color = col;
            float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            GUI.matrix = m * Matrix4x4.TRS(a, Quaternion.Euler(0, 0, ang), Vector3.one) * Matrix4x4.TRS(-(Vector3)a, Quaternion.identity, Vector3.one);
            GUI.DrawTexture(new Rect(a.x, a.y - w / 2, Vector2.Distance(a, b), w), white);
            GUI.matrix = m; GUI.color = o;
        }

        // A framed box: the named nine-slice if it exists, else a flat groove with a thin edge.
        public void Frame(Rect r, string name, Color? edge = null, Color? fill = null)
        {
            var t = Art(name);
            if (t) { NineSlice(r, t, Borders.TryGetValue(name, out var b) ? b : new Vector4(16, 16, 16, 16)); return; }
            Fill(r, fill ?? new Color(Groove.r, Groove.g, Groove.b, 0.88f));
            Outline(r, edge ?? new Color(Silver.r, Silver.g, Silver.b, 0.55f), Mathf.Max(1f, K * 1.2f));
        }

        public void Frame(float x, float y, float w, float h, string name, Color? edge = null, Color? fill = null) => Frame(R(x, y, w, h), name, edge, fill);

        void NineSlice(Rect r, Texture2D t, Vector4 b)
        {
            float tw = t.width, th = t.height, s = K;
            float l = b.x * s, rr = b.y * s, tp = b.z * s, bt = b.w * s;
            float kx = Mathf.Min(1f, r.width / Mathf.Max(1f, l + rr)), ky = Mathf.Min(1f, r.height / Mathf.Max(1f, tp + bt));
            l *= kx; rr *= kx; tp *= ky; bt *= ky;
            float[] xs = { r.x, r.x + l, r.xMax - rr, r.xMax }, ys = { r.y, r.y + tp, r.yMax - bt, r.yMax };
            float[] us = { 0, b.x / tw, 1 - b.y / tw, 1 }, vs = { 1, 1 - b.z / th, b.w / th, 0 };
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    var dst = Rect.MinMaxRect(xs[i], ys[j], xs[i + 1], ys[j + 1]);
                    if (dst.width <= 0 || dst.height <= 0) continue;
                    GUI.DrawTextureWithTexCoords(dst, t, Rect.MinMaxRect(us[i], vs[j + 1], us[i + 1], vs[j]));
                }
        }

        // An icon or picture by name; returns false when missing so the caller can draw its own stand-in.
        public bool Icon(Rect r, string name, ScaleMode mode = ScaleMode.ScaleToFit, Color? tint = null)
        {
            var t = Art(name);
            if (!t) return false;
            // The 64-px glyph icons fill under half their canvas: draw them grown so the glyph fills the rect.
            if (name.StartsWith("icon_") || name.StartsWith("stat_") || name.StartsWith("cat_") || name.StartsWith("troop_"))
                r = new Rect(r.center.x - r.width, r.center.y - r.height, r.width * 2f, r.height * 2f);
            var o = GUI.color; if (tint != null) GUI.color = tint.Value;
            GUI.DrawTexture(r, t, mode);
            GUI.color = o;
            return true;
        }

        public bool Icon(float x, float y, float w, float h, string name, ScaleMode mode = ScaleMode.ScaleToFit, Color? tint = null) => Icon(R(x, y, w, h), name, mode, tint);

        // A sub-rectangle of a texture (portrait crops); uv in 0–1 with y up.
        public void Crop(Rect r, Texture t, Rect uv, bool flip = false)
        {
            if (!t) return;
            if (flip) uv = new Rect(uv.xMax, uv.y, -uv.width, uv.height);
            GUI.DrawTextureWithTexCoords(r, t, uv);
        }

        public void Bar(float x, float y, float w, float h, float value, Color c)
        {
            var r = R(x, y, w, h);
            Fill(r, new Color(0.02f, 0.04f, 0.08f, 0.85f));
            Fill(new Rect(r.x + 1, r.y + 1, (r.width - 2) * Mathf.Clamp01(value), r.height - 2), c);
        }

        // Panel with a header strip and its title, like the mock-ups' 队伍 / 随行物资 / 物品详情.
        public void PanelBox(float x, float y, float w, float h, string title, string right = null)
        {
            Frame(x, y, w, h, "panel_frame");
            if (title == null) return;
            var hr = R(x + 6, y + 4, w - 12, 52);
            var ht = Art("panel_header");
            if (ht) NineSlice(hr, ht, Borders["panel_header"]);
            else HLine(x + 14, y + 56, w - 28, new Color(Silver.r, Silver.g, Silver.b, 0.45f));
            Text(x + 38, y + 6, w - 60, 50, title, 30, Ivory, TextAnchor.MiddleLeft, true);
            if (right != null) Text(x + 20, y + 6, w - 48, 50, right, 22, Ivory, TextAnchor.MiddleRight);
        }

        // A group box inside a panel (使用对象, 作用范围, 习得进度…).
        public void SubBox(float x, float y, float w, float h, string title = null, string icon = null)
        {
            Frame(x, y, w, h, "subpanel", new Color(Silver.r, Silver.g, Silver.b, 0.35f), new Color(Groove.r, Groove.g, Groove.b, 0.55f));
            if (title == null) return;
            float tx = x + 26;
            if (icon != null && Icon(x + 22, y + 10, 40, 40, icon)) tx = x + 76;
            Text(tx, y + 8, w - 40, 46, title, icon != null ? 30 : 24, Ivory, TextAnchor.MiddleLeft, true);
            HLine(x + 14, y + 56, w - 28, new Color(Silver.r, Silver.g, Silver.b, 0.30f));
        }

        public void Keycap(float x, float y, float w, string key)
        {
            var r = R(x, y, w, 34);
            if (!Has("keycap")) { Fill(r, new Color(0.04f, 0.08f, 0.15f, 0.9f)); Outline(r, Silver, Mathf.Max(1, K)); }
            else Frame(r, "keycap");
            GUI.Label(r, key, St(18, Ivory, TextAnchor.MiddleCenter, true));
        }

        public static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

        static Texture2D MakeDisc(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float c = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(c - d + 0.5f)));
                }
            t.Apply();
            return t;
        }
    }
}
