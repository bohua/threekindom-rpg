using System.Collections.Generic;
using System.Linq;
using Prototype.BingshuTactics;
using ThreeKingdoms.Core.Battle;
using UnityEngine;

namespace ThreeKingdoms.UI.Menu
{
    // 兵书: the military-book screen of the half-real-time tactics prototype (PR #14, TacticsGame.Circuit.cs, after
    // DESTINY_LIBRARY_UI.png and the UI_STYLE_GUIDE 命盘 rules) inside the travel menu: 武将 roster, 命盘 (the
    // school's bagua plate with sockets, cords and line totals), 藏书阁 (two columns of books, a pearl = socketed by
    // this general; books another general holds are hidden), 策略一览 (emblem, name, brief, range grid; lit rows are
    // the strategies the circuit lights). Same mock-up coordinates as the prototype, scaled to fit between the tab row
    // and the hint bar. Only the joined generals are listed; their intellect (智力) sets how far a cord can run.
    public sealed partial class MainMenuHud
    {
        int plateSlot, bingshuFocus, bookSel, libScroll, stratScroll, stratSel;
        public int BingshuFocus => bingshuFocus;
        public int PlateSlot => plateSlot;

        BingshuShelf Shelf => BingshuShelf.For(Controller.Run);
        int Intellect(GeneralSpec g) => Progress.Stats(g, Inv.Bonus(g.Id)).Intellect;

        static string ElemKey(Element e) => e switch
        {
            Element.Metal => "metal", Element.Wood => "wood", Element.Water => "water", Element.Fire => "fire", Element.Earth => "earth", _ => "metal"
        };

        static string BookTitle(Book b)
        {
            var n = b.Name;
            if (n.EndsWith("残卷")) n = n.Substring(0, n.Length - 2).Trim('《', '》') + "·残卷";
            return n.Trim('《', '》');
        }

        static readonly Dictionary<string, string> StratBrief = new Dictionary<string, string>
        {
            ["引火"] = "单体燃烧", ["火烧营寨"] = "点燃周边敌军", ["火海"] = "大范围持续燃烧",
            ["截流"] = "推迟敌方行动", ["水攻"] = "冲击直线敌军", ["决堤"] = "洪流冲击大片敌军",
            ["抚军"] = "恢复单体兵力", ["鼓舞"] = "恢复兵力并鼓舞", ["收拢溃卒"] = "恢复全军兵力",
            ["坚壁"] = "减少所受伤害", ["八阵"] = "护佑周边友军", ["箭雨"] = "箭雨覆盖周边", ["万箭齐发"] = "密集箭雨覆盖",
        };

        // ---------- input ----------

        void MoveBingshu(int dx, int dy)
        {
            var g = General; if (g == null) return;
            var c = Shelf.Circuit(g.Id);
            if (bingshuFocus == 0)
            {
                int step = dy != 0 ? dy : dx;
                plateSlot = Wrap(plateSlot + step, c.Slots.Length);
            }
            else if (bingshuFocus == 1)
            {
                var shelf = Shelf.ShelfFor(g.Id);
                if (dy != 0) bookSel = Mathf.Clamp(bookSel + dy * 2, 0, Mathf.Max(0, shelf.Count - 1));
                if (dx != 0) bookSel = Mathf.Clamp(bookSel + dx, 0, Mathf.Max(0, shelf.Count - 1));
            }
            else if (dy != 0) stratSel = Mathf.Clamp(stratSel + dy, 0, Strategies.All.Length - 1);
        }

        void ConfirmBingshu()
        {
            var g = General; if (g == null) return;
            var c = Shelf.Circuit(g.Id);
            if (bingshuFocus == 0)
            {
                bingshuFocus = 1;
                var shelf = Shelf.ShelfFor(g.Id);
                bookSel = Mathf.Max(0, shelf.FindIndex(b => CircuitMath.Fits(c, plateSlot, b) && Shelf.Owner(b) == null));
            }
            else if (bingshuFocus == 1)
            {
                var b = Shelf.ShelfFor(g.Id).ElementAtOrDefault(bookSel);
                if (b != null) SocketBook(g, b);
            }
        }

        public bool SocketBook(GeneralSpec g, Book b)
        {
            bool ok = Shelf.Toggle(g.Id, b, ref plateSlot);
            Message = ok ? "" : $"{BookTitle(b)}放不进{g.DisplayName}的命盘：没有合适的空槽";
            return ok;
        }

        void RemoveBook()
        {
            var g = General; if (g == null) return;
            var slots = Shelf.Slots(g.Id);
            if (bingshuFocus == 0) { if (plateSlot < slots.Length) slots[plateSlot] = null; return; }
            var b = Shelf.ShelfFor(g.Id).ElementAtOrDefault(bookSel);
            int at = b == null ? -1 : System.Array.IndexOf(slots, b);
            if (at >= 0) slots[at] = null;
        }

        // ---------- drawing ----------

        void DrawBingshu()
        {
            var p = paint;
            // Map the prototype's mock-up rows 92–852 into 158–848, centred.
            float k0 = p.K, ox0 = p.Ox, oy0 = p.Oy;
            float s = 690f / 760f;
            p.K = k0 * s; p.Ox = ox0 + MenuPaint.MockW * k0 * (1 - s) / 2f; p.Oy = oy0 + 158 * k0 - 92 * p.K;
            try
            {
                var roster = Joined;
                if (roster.Count == 0) return;
                generalIndex = Mathf.Clamp(generalIndex, 0, roster.Count - 1);
                var g = roster[generalIndex];
                var c = Shelf.Circuit(g.Id);
                DrawRosterPanel(roster, g);
                DrawPlatePanel(g, c);
                DrawLibraryPanel(g, c);
                DrawStrategyPanel(g, c);
            }
            finally { p.K = k0; p.Ox = ox0; p.Oy = oy0; }
        }

        void Gem(Rect r, Element e)
        {
            var p = paint;
            var t = p.Art("gem_" + ElemKey(e));
            if (t) { var gr = new Rect(r.center.x - r.width * 0.95f, r.center.y - r.height * 0.95f, r.width * 1.9f, r.height * 1.9f); GUI.DrawTexture(gr, t, ScaleMode.ScaleToFit); return; }
            p.Disc(r.center, r.width * 0.42f, Wuxing.Tint(e));
            p.Disc(r.center + new Vector2(-r.width * 0.12f, -r.height * 0.12f), r.width * 0.12f, new Color(1, 1, 1, 0.5f));
        }

        void DrawRosterPanel(List<GeneralSpec> roster, GeneralSpec u)
        {
            var p = paint;
            p.PanelBox(24, 92, 292, 760, "武将");
            for (int k = 0; k < roster.Count && k < 6; k++)
            {
                var hu = roster[k]; bool sel = hu == u;
                var r = p.R(36, 150 + k * 118.5f, 252, 110);
                p.Frame(r, sel ? "card_selected" : "card", sel ? MenuPaint.Cyan : (Color?)null);
                var bust = p.Art("bust_" + hu.Id.Replace("_", "")) ?? Bust(hu.Id);
                if (bust) GUI.DrawTexture(new Rect(r.x + 4, r.y + 4, r.width - 8, r.height - 8), bust, ScaleMode.ScaleAndCrop);
                p.Fill(new Rect(r.x + r.width * 0.62f, r.y + 3, r.width * 0.38f - 3, r.height - 6), new Color(0.02f, 0.05f, 0.10f, 0.35f));
                p.Outline(new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6), sel ? new Color(0.45f, 0.90f, 1f, 0.95f) : new Color(0.55f, 0.65f, 0.78f, 0.45f), sel ? 2f : 1f);
                GUI.Label(new Rect(r.x + r.width * 0.64f, r.y, r.width * 0.36f, r.height), hu.DisplayName, p.St(26, sel ? Color.white : MenuPaint.Ivory, TextAnchor.MiddleCenter, true));
                if (sel) Pointer(14, 150 + k * 118.5f + 30);
                int idx = k;
                if (GUI.Button(r, GUIContent.none, GUIStyle.none) && !sel) { generalIndex = idx; plateSlot = 0; bingshuFocus = 0; libScroll = 0; }
            }
            ScrollBar(296, 160, 828, 0, 6, roster.Count);
        }

        Vector2 SocketPos(Vector2 hub, CircuitDef c, int i, float r) => hub + new Vector2(c.Slots[i].Pos.x, -c.Slots[i].Pos.y) * r;

        static Color PlateLineColor(int l) => l switch
        {
            0 => new Color(0.92f, 0.36f, 0.38f), 1 => new Color(0.30f, 0.70f, 0.95f), 2 => new Color(0.95f, 0.78f, 0.30f), _ => new Color(0.45f, 0.85f, 0.55f)
        };

        // Bagua.All order: 乾 兑 离 震 巽 坎 艮 坤.
        static readonly string[] GuaKeys = { "qian", "dui", "li", "zhen", "xun", "kan", "gen", "kun" };

        // UI_STYLE_GUIDE element colours (金 银白, 木 青绿, 水 天蓝, 火 朱红, 土 赭金).
        static Color ElementColor(Element e) => e switch
        {
            Element.Metal => MenuPaint.Hex(0xD5DDE4), Element.Wood => MenuPaint.Hex(0x61BCA0), Element.Water => MenuPaint.Hex(0x5BAFE3),
            Element.Fire => MenuPaint.Hex(0xD86B70), Element.Earth => MenuPaint.Hex(0xC4A36D), _ => new Color(0.5f, 0.55f, 0.62f),
        };

        // The colour of a half cord next to socket `near`: its book's element, else the far end's, else dim grey.
        static Color CordColor(Book near, Book far)
        {
            var b = near ?? far;
            return b == null ? new Color(0.42f, 0.47f, 0.55f, 0.85f) : ElementColor(b.Element);
        }

        void DrawPlatePanel(GeneralSpec g, CircuitDef c)
        {
            var p = paint;
            var slots = Shelf.Slots(g.Id);
            int intel = Intellect(g);
            p.PanelBox(336, 92, 532, 760, "命盘", $"{c.Name}　智力 {intel}");
            var hub = p.P(603, 438); float half = 272 * p.K, R = 150 * p.K;
            if (!p.Icon(new Rect(hub.x - half, hub.y - half, half * 2, half * 2), "destiny_plate"))
            {
                p.Disc(hub, half * 0.92f, new Color(0.10f, 0.16f, 0.26f, 0.9f));
                p.Disc(hub, half * 0.86f, new Color(0.04f, 0.07f, 0.13f, 0.95f));
            }

            // Bagua directions whose element matches the book socketed there light up (user 2026-10-24): the
            // direction's trapezoid is swapped for its lit form (Codex art/bingshu-sector-highlights, drawn on the
            // plate's own canvas); without that art a glow is drawn instead.
            var plateRect = new Rect(hub.x - half, hub.y - half, half * 2, half * 2);
            // Directions with no book socketed sit dark and inactive, the opposite of lit (user 2026-10-26; Codex
            // art/bingshu-vacant-dim, on the plate's own canvas).
            for (int gua = 0; gua < GuaKeys.Length; gua++)
            {
                bool filled = false;
                for (int i = 0; i < c.Slots.Length; i++) if (c.Slots[i].Gua == gua && slots[i] != null) filled = true;
                if (!filled) p.Icon(plateRect, "sector_dim_" + GuaKeys[gua], ScaleMode.StretchToFill);
            }
            for (int i = 0; i < c.Slots.Length; i++)
            {
                int gua = c.Slots[i].Gua; var book = slots[i];
                if (gua < 0 || book == null || Bagua.All[gua].Element != book.Element) continue;
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 3f);
                if (p.Icon(plateRect, "sector_lit_" + GuaKeys[gua], ScaleMode.StretchToFill, new Color(1, 1, 1, 0.85f + 0.15f * pulse))) continue;
                var d = new Vector2(Bagua.All[gua].Dir.x, -Bagua.All[gua].Dir.y);
                var col = ElementColor(book.Element);
                p.Line(hub + d * 196 * p.K, hub + d * 262 * p.K, 120 * p.K, new Color(col.r, col.g, col.b, 0.30f * pulse));
                p.Line(hub + d * 206 * p.K, hub + d * 254 * p.K, 84 * p.K, new Color(col.r, col.g, col.b, 0.35f * pulse));
                p.Line(hub + d * 256 * p.K + new Vector2(-d.y, d.x) * 60 * p.K, hub + d * 256 * p.K - new Vector2(-d.y, d.x) * 60 * p.K, 5 * p.K, new Color(1, 1, 1, 0.75f * pulse));
            }

            // Cords: thick, coloured by the element of the books they join (user 2026-10-24: "链路的颜色取决于连接到的镶嵌位的颜色").
            var segs = CircuitMath.Segments(c, intel, out var edges);
            foreach (var ed in edges)
            {
                Vector2 p0 = SocketPos(hub, c, ed.A, R), p1 = SocketPos(hub, c, ed.B, R);
                if (ed.Line < 0)
                {
                    var mid = (p0 + p1) / 2f; var dir = (p1 - p0).normalized * 10f * p.K;
                    p.Line(p0, mid - dir, 5 * p.K, new Color(0.5f, 0.55f, 0.62f, 0.6f)); p.Line(mid + dir, p1, 5 * p.K, new Color(0.5f, 0.55f, 0.62f, 0.6f));
                    continue;
                }
                // Each half of the cord takes the colour of the socket at its end (the other end's when that one is empty).
                p.Line(p0, p1, 24 * p.K, new Color(0.02f, 0.04f, 0.08f, 0.9f));
                var m = (p0 + p1) / 2f;
                foreach (var (from, to, col) in new[] { (p0, m, CordColor(slots[ed.A], slots[ed.B])), (m, p1, CordColor(slots[ed.B], slots[ed.A])) })
                {
                    p.Line(from, to, 19 * p.K, new Color(col.r, col.g, col.b, 0.40f));
                    p.Line(from, to, 11 * p.K, col);
                    p.Line(from, to, 3 * p.K, Color.Lerp(col, Color.white, 0.55f));
                }
            }

            float sock = 80 * p.K;
            for (int i = 0; i < c.Slots.Length; i++)
            {
                var pos = SocketPos(hub, c, i, R); var book = slots[i]; bool sel = i == plateSlot && bingshuFocus <= 1;
                var rr = new Rect(pos.x - sock / 2, pos.y - sock / 2, sock, sock);
                if (c.Slots[i].Gua < 0)
                {
                    if (sel) p.Disc(pos, 58 * p.K, new Color(0.35f, 0.85f, 1f, 0.35f + 0.15f * Mathf.Sin(Time.unscaledTime * 5f)));
                    // The hub takes its school's element: a ring in that colour and the taiji tinted with it.
                    var hc = ElementColor(c.Affinity);
                    p.Disc(pos, 50 * p.K, new Color(hc.r, hc.g, hc.b, 0.35f));
                    p.Disc(pos, 46 * p.K, hc);
                    p.Disc(pos, 40 * p.K, MenuPaint.Base);
                    var tr = new Rect(pos.x - 78 * p.K, pos.y - 78 * p.K, 156 * p.K, 156 * p.K);
                    if (!p.Icon(tr, "taiji_hub", tint: Color.Lerp(hc, Color.white, 0.25f))) p.Disc(pos, 34 * p.K, hc);
                    var badge = p.R(556, 494, 94, 38);
                    p.Frame(badge, "count_badge", SilverA(0.6f), new Color(0.03f, 0.07f, 0.14f, 0.92f));
                    var e = book != null ? book.Element : c.Slots[i].Locked;
                    Gem(p.R(568, 498, 30, 30), e);
                    p.Text(600, 494, 50, 38, $"×{(book != null ? CircuitMath.Value(c, book) : 0)}", 22, book != null ? MenuPaint.Ivory : MenuPaint.Grey);
                }
                else
                {
                    if (sel) p.Disc(pos, sock * 0.62f, new Color(0.35f, 0.85f, 1f, 0.30f + 0.15f * Mathf.Sin(Time.unscaledTime * 5f)));
                    // An empty socket is a hollow pearl seat (user 2026-10-26: the square placeholder was jarring).
                    string ring = book != null ? (sel ? "socket_ring_selected" : "socket_ring")
                        : p.Has("socket_vacant") ? (sel ? "socket_vacant_selected" : "socket_vacant") : (sel ? "socket_plus" : "socket_empty");
                    float grow = ring == "socket_ring_selected" ? 1.17f : 1.43f;
                    var gr = new Rect(rr.center.x - rr.width * grow / 2, rr.center.y - rr.height * grow / 2, rr.width * grow, rr.height * grow);
                    // A filled socket's ring takes its book's element colour, the same as the cords into it.
                    Color? ringTint = book != null ? Color.Lerp(ElementColor(book.Element), Color.white, 0.15f) : (Color?)null;
                    if (book != null) p.Disc(pos, sock * 0.56f, new Color(ringTint.Value.r, ringTint.Value.g, ringTint.Value.b, 0.45f));
                    if (!p.Icon(gr, ring, tint: ringTint))
                    {
                        p.Disc(pos, sock * 0.5f, sel ? MenuPaint.Cyan : new Color(0.62f, 0.70f, 0.80f)); p.Disc(pos, sock * 0.44f, new Color(0.04f, 0.07f, 0.14f));
                        if (book == null) GUI.Label(rr, sel ? "+" : "□", p.St(30, new Color(0.75f, 0.82f, 0.9f), TextAnchor.MiddleCenter));
                    }
                    if (book != null)
                    {
                        Gem(new Rect(pos.x - 17 * p.K, pos.y - 27 * p.K, 34 * p.K, 34 * p.K), book.Element);
                        GUI.Label(new Rect(pos.x - 20 * p.K, pos.y + 4 * p.K, 40 * p.K, 26 * p.K), CircuitMath.Value(c, book).ToString(), p.St(22, MenuPaint.Ivory, TextAnchor.MiddleCenter, true));
                    }
                    else if (c.Slots[i].Locked != Element.None)
                    {
                        var o = GUI.color; GUI.color = new Color(1, 1, 1, 0.45f);
                        Gem(new Rect(pos.x + 12 * p.K, pos.y + 12 * p.K, 22 * p.K, 22 * p.K), c.Slots[i].Locked);
                        GUI.color = o;
                    }
                }
                int idx = i;
                if (GUI.Button(rr, GUIContent.none, GUIStyle.none))
                {
                    if (Event.current.button == 1 || (plateSlot == idx && book != null)) slots[idx] = null;
                    plateSlot = idx; bingshuFocus = 0;
                }
            }

            // Line totals.
            var lg = p.R(340, 740, 526, 110);
            p.Fill(new Rect(lg.x + 6, lg.y, lg.width - 12, lg.height - 6), new Color(0.03f, 0.06f, 0.12f, 0.55f));
            p.Fill(new Rect(lg.x + 14, lg.y + 52 * p.K, lg.width - 28, 1), new Color(0.5f, 0.62f, 0.78f, 0.35f));
            for (int l = 0; l < segs.Length && l < 3; l++)
            {
                float y = 754 + l * 42 - (segs.Length > 2 ? 10 : 0);
                var t = CircuitMath.LineTotals(c, slots, segs[l]);
                p.Fill(p.R(360, y + 14, 32, 5), PlateLineColor(l));
                p.Text(398, y, 40, 34, (l + 1).ToString(), 22, MenuPaint.Ivory);
                int k = 0;
                foreach (var e in Wuxing.All)
                {
                    float x = 450 + k * 83;
                    Gem(p.R(x, y + 3, 26, 26), e);
                    p.Text(x + 30, y, 50, 34, $"×{t[e]}", 20, t[e] > 0 ? MenuPaint.Ivory : MenuPaint.Grey);
                    k++;
                }
            }
        }

        void DrawLibraryPanel(GeneralSpec g, CircuitDef c)
        {
            var p = paint;
            p.PanelBox(892, 92, 436, 760, "藏书阁");
            var shelf = Shelf.ShelfFor(g.Id);
            const int cols = 2, rowsVisible = 5;
            if (bingshuFocus == 1) bookSel = Mathf.Clamp(bookSel, 0, Mathf.Max(0, shelf.Count - 1));
            int selRow = bookSel / cols, maxFirst = Mathf.Max(0, (shelf.Count + 1) / 2 - rowsVisible);
            if (bingshuFocus == 1) libScroll = Mathf.Clamp(libScroll, Mathf.Max(0, selRow - rowsVisible + 1), Mathf.Min(selRow, maxFirst));
            libScroll = Mathf.Clamp(libScroll, 0, maxFirst);
            bool lockedSlot = plateSlot >= 0 && plateSlot < c.Slots.Length && c.Slots[plateSlot].Locked != Element.None && bingshuFocus == 1;
            for (int idx = libScroll * cols; idx < shelf.Count && idx < (libScroll + rowsVisible + 1) * cols; idx++)
            {
                var b = shelf[idx]; int row = idx / cols - libScroll, col = idx % cols;
                bool partial = row >= rowsVisible;
                var r = p.R(900 + col * 202, 154 + row * 124.5f, 194, partial ? 40 : 108);
                bool sel = bingshuFocus == 1 && idx == bookSel;
                bool mine = Shelf.Owner(b) == g.Id;
                bool dim = lockedSlot && !CircuitMath.Fits(c, plateSlot, b) && !mine;
                var o = GUI.color; if (partial) GUI.color = new Color(1, 1, 1, 0.35f); else if (dim) GUI.color = new Color(1, 1, 1, 0.45f);
                p.Frame(r, sel ? "card_selected" : "card", sel ? MenuPaint.Cyan : (Color?)null);
                if (sel && !p.Has("card_selected")) p.Outline(r, MenuPaint.Cyan, 2);
                if (!partial)
                {
                    var sr = p.R(908 + col * 202, 164 + row * 124.5f, 52, 82);
                    var sc = p.Art("scroll_" + ElemKey(b.Element));
                    if (sc) GUI.DrawTexture(new Rect(sr.center.x - sr.width * 0.75f, sr.center.y - sr.height * 0.75f, sr.width * 1.5f, sr.height * 1.5f), sc, ScaleMode.ScaleToFit);
                    else p.Fill(new Rect(sr.x + sr.width * 0.2f, sr.y, sr.width * 0.6f, sr.height), Wuxing.Tint(b.Element) * 0.8f);
                    var title = BookTitle(b);
                    p.Text(966 + col * 202, 166 + row * 124.5f, 130, 34, title, title.Length > 5 ? 19 : 21, MenuPaint.Ivory, TextAnchor.MiddleLeft, true);
                    Gem(p.R(968 + col * 202, 212 + row * 124.5f, 26, 26), b.Element);
                    p.Text(998 + col * 202, 208 + row * 124.5f, 50, 34, $"×{b.Grade}", 21, MenuPaint.Ivory);
                    var pr = p.R(1040 + col * 202, 196 + row * 124.5f, 46, 46);
                    if (!p.Icon(pr, mine ? "pearl_filled" : "pearl_empty"))
                    {
                        p.Disc(pr.center, pr.width * 0.46f, new Color(0.45f, 0.55f, 0.68f));
                        p.Disc(pr.center, pr.width * 0.40f, mine ? new Color(0.92f, 0.90f, 0.82f) : new Color(0.04f, 0.06f, 0.10f));
                    }
                    int i = idx;
                    if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { bookSel = i; bingshuFocus = 1; SocketBook(g, b); }
                }
                GUI.color = o;
            }
            ScrollBar(1310, 162, 828, libScroll, rowsVisible, (shelf.Count + 1) / 2);
        }

        void DrawStrategyPanel(GeneralSpec g, CircuitDef c)
        {
            var p = paint;
            p.PanelBox(1350, 92, 298, 760, "策略一览");
            var lit = CircuitMath.Lit(c, Shelf.Slots(g.Id), Intellect(g));
            var all = Strategies.All;
            const int visible = 13;
            if (bingshuFocus == 2) stratScroll = Mathf.Clamp(stratScroll, Mathf.Max(0, stratSel - visible + 1), Mathf.Min(stratSel, Mathf.Max(0, all.Length - visible)));
            stratScroll = Mathf.Clamp(stratScroll, 0, Mathf.Max(0, all.Length - visible));
            for (int k = 0; k < visible && stratScroll + k < all.Length; k++)
            {
                var s = all[stratScroll + k]; bool on = lit.Any(x => x.Def == s);
                bool sel = bingshuFocus == 2 && stratSel == stratScroll + k;
                var r = p.R(1362, 146 + k * 53.6f, 276, 50);
                p.Frame(r, on || sel ? "row_lit" : "row", on ? MenuPaint.Cyan : (Color?)null);
                if (sel) p.Outline(r, MenuPaint.Cyan, 2);
                var er = p.R(1372, 152 + k * 53.6f, 38, 38);
                var em = p.Art("emblem_" + ElemKey(s.Main));
                if (em) GUI.DrawTexture(new Rect(er.center.x - er.width * 0.85f, er.center.y - er.height * 0.85f, er.width * 1.7f, er.height * 1.7f), em, ScaleMode.ScaleToFit);
                else { p.Disc(er.center, er.width * 0.48f, Wuxing.Tint(s.Main)); p.Disc(er.center, er.width * 0.40f, new Color(0.04f, 0.07f, 0.14f)); Gem(new Rect(er.x + er.width * 0.2f, er.y + er.height * 0.2f, er.width * 0.6f, er.height * 0.6f), s.Main); }
                var o = GUI.color; if (!on) GUI.color = new Color(1, 1, 1, 0.7f);
                p.Text(1424, 146 + k * 53.6f, 150, 28, s.Name, 21, MenuPaint.Ivory, TextAnchor.MiddleLeft, true);
                p.Text(1424, 171 + k * 53.6f, 150, 22, StratBrief.TryGetValue(s.Name, out var bf) ? bf : s.Desc, 15, new Color(0.75f, 0.80f, 0.88f));
                GUI.color = o;
                DrawRangeGrid(p.R(1566, 150 + k * 53.6f, 64, 42), s);
            }
        }

        void DrawRangeGrid(Rect box, SkillDef s)
        {
            var p = paint;
            int[,] pat;
            if (s.Area == Area.Line) pat = s.Width >= 3 ? new[,] { { 1, 1, 1, 1 }, { 1, 1, 1, 1 } } : new[,] { { 1, 1, 1, 1 } };
            else if (s.Area == Area.Circle && s.Radius >= 99) pat = new[,] { { 1, 1, 1, 1, 1 }, { 1, 1, 1, 1, 1 }, { 1, 1, 1, 1, 1 } };
            else if (s.Area == Area.Circle && s.Radius >= 2) pat = new[,] { { 0, 0, 1, 0, 0 }, { 0, 1, 1, 1, 0 }, { 1, 1, 1, 1, 1 }, { 0, 1, 1, 1, 0 }, { 0, 0, 1, 0, 0 } };
            else if (s.Area == Area.Circle) pat = new[,] { { 0, 1, 0 }, { 1, 1, 1 }, { 0, 1, 0 } };
            else pat = new[,] { { 0, 0, 0 }, { 0, 1, 0 }, { 0, 0, 0 } };
            int rows = pat.GetLength(0), cols = pat.GetLength(1);
            float cell = Mathf.Min(box.width / cols, box.height / rows, 11 * p.K), gap = Mathf.Max(1f, cell * 0.14f);
            float w = cols * cell, h = rows * cell; float x0 = box.center.x - w / 2, y0 = box.center.y - h / 2;
            var tint = Wuxing.Tint(s.Main); tint = Color.Lerp(tint, Color.white, s.Main == Element.Water ? 0.35f : 0.15f);
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                    p.Fill(new Rect(x0 + x * cell + gap / 2, y0 + y * cell + gap / 2, cell - gap, cell - gap), pat[y, x] == 1 ? tint : new Color(0.25f, 0.32f, 0.42f, 0.45f));
        }
    }
}
