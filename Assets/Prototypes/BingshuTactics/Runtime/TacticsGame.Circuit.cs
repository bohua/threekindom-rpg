// PROTOTYPE - NOT FOR PRODUCTION
// Question: 空轨式 AT 行动条＋小范围格子战斗，配合“学派回路镶嵌兵书→策略”，是否好玩、是否看得懂？
// Date: 2026-10-24
// 兵书回路界面按用户指定的效果稿 1:1 重做（用户 2026-10-24：“兵书系统按这个1:1 制作……还原度90%以上为合格”；
// 效果稿 destiny-library-ui-20261024-v1/DESTINY_LIBRARY_UI.png，1672×941）：
// 顶栏“兵书回路 / 兵书配置 / 布阵前准备”；四栏：武将（胸像卡名单）、命盘（八卦盘＋槽位＋连线＋线路合计）、
// 藏书阁（两列兵书卡，右侧珠槽＝本将装备；他人装备的书不显示）、策略一览（徽记、名称、短说明、范围小格，点亮的行高亮）；
// 底部按键提示与“回到布阵”。切图来自 Codex（art/battle-poc-destiny-ui-slices），缺图时用代码画的同色元素顶替。
// 坐标直接取效果稿像素，按 720/941 缩放并水平居中。
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Prototype.BingshuTactics
{
    public sealed partial class TacticsGame
    {
        public Texture2D[] DestinyTextures = new Texture2D[0];
        Dictionary<string, Texture2D> destiny;

        // 名单里不出战的武将（效果稿展示名单可向下扩展；不改变本关出战阵容）
        readonly List<Unit> bench = new List<Unit>();

        const float MockW = 1672f, MockH = 941f;
        float dk, dox;   // 效果稿像素 → 界面坐标

        Texture2D DT(string name)
        {
            if (destiny == null) destiny = DestinyTextures.Where(t => t).ToDictionary(t => t.name, t => t);
            return destiny.TryGetValue(name, out var t) ? t : null;
        }

        Rect MR(float x, float y, float w, float h) => new Rect(dox + x * dk, y * dk, w * dk, h * dk);
        Vector2 MP(float x, float y) => new Vector2(dox + x * dk, y * dk);
        int MF(float mockPx) => Mathf.Max(8, Mathf.RoundToInt(mockPx * dk));

        // 九宫格边距（贴图像素，左右上下，取自 Codex manifest），屏幕上按效果稿比例缩放
        static readonly Dictionary<string, Vector4> DBorder = new Dictionary<string, Vector4>
        {
            ["panel_frame"] = new Vector4(34, 34, 34, 34), ["panel_header"] = new Vector4(36, 36, 30, 30), ["title_bar"] = new Vector4(112, 56, 78, 16),
            ["card"] = new Vector4(14, 14, 14, 14), ["card_selected"] = new Vector4(16, 16, 16, 16), ["row"] = new Vector4(11, 11, 11, 11), ["row_lit"] = new Vector4(11, 11, 11, 11),
            ["keycap"] = new Vector4(8, 8, 8, 8), ["button_return"] = new Vector4(22, 58, 30, 30), ["count_badge"] = new Vector4(9, 9, 9, 9),
            ["scroll_track"] = new Vector4(4, 4, 5, 5), ["scroll_thumb"] = new Vector4(3, 3, 6, 6),
        };

        bool DFrame(Rect r, string name)
        {
            var t = DT(name);
            if (!t) return false;
            NineSliceScaled(r, t, DBorder.TryGetValue(name, out var v) ? v : new Vector4(16, 16, 16, 16), dk);
            return true;
        }

        // b = (左, 右, 上, 下)
        static void NineSliceScaled(Rect r, Texture2D t, Vector4 b, float scale)
        {
            float tw = t.width, th = t.height;
            float l = b.x * scale, rr = b.y * scale, tp = b.z * scale, bt = b.w * scale;
            float kx = Mathf.Min(1f, r.width / Mathf.Max(1f, l + rr)), ky = Mathf.Min(1f, r.height / Mathf.Max(1f, tp + bt));
            l *= kx; rr *= kx; tp *= ky; bt *= ky;
            float[] xs = { r.x, r.x + l, r.xMax - rr, r.xMax }, ys = { r.y, r.y + tp, r.yMax - bt, r.yMax };
            float[] us = { 0, b.x / tw, 1 - b.y / tw, 1 }, vs = { 1, 1 - b.z / th, b.w / th, 0 };
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    var dst = UnityEngine.Rect.MinMaxRect(xs[i], ys[j], xs[i + 1], ys[j + 1]);
                    if (dst.width <= 0 || dst.height <= 0) continue;
                    GUI.DrawTextureWithTexCoords(dst, t, UnityEngine.Rect.MinMaxRect(us[i], vs[j + 1], us[i + 1], vs[j]));
                }
        }

        // 图标切图四周留有透明边，按内容占比放大绘制，使可见部分落在 r 里
        static Rect Grow(Rect r, float k) => new Rect(r.center.x - r.width * k / 2, r.center.y - r.height * k / 2, r.width * k, r.height * k);

        void Outline(Rect r, Color edge, float w)
        {
            Rect(new Rect(r.x, r.y, r.width, w), edge); Rect(new Rect(r.x, r.yMax - w, r.width, w), edge);
            Rect(new Rect(r.x, r.y, w, r.height), edge); Rect(new Rect(r.xMax - w, r.y, w, r.height), edge);
        }

        void DTex(Rect r, string name, Color fallback, bool disc = false)
        {
            var t = DT(name);
            if (t) { GUI.DrawTexture(r, t, ScaleMode.ScaleToFit); return; }
            if (disc) Disc(r.center, r.width * 0.42f, fallback); else Rect(r, fallback);
        }

        static string ElemKey(Element e) => e switch
        {
            Element.Metal => "metal", Element.Wood => "wood", Element.Water => "water", Element.Fire => "fire", Element.Earth => "earth", _ => "metal"
        };

        void Gem(Rect r, Element e) { var g = DT("gem_" + ElemKey(e)); var t = g ?? UI("icon_" + ElemKey(e)); if (t) GUI.DrawTexture(g ? Grow(r, 1.9f) : r, t, ScaleMode.ScaleToFit); else Disc(r.center, r.width * 0.4f, Wuxing.Tint(e)); }

        // 书名按效果稿写法：去书名号，残卷写成“×·残卷”
        static string BookTitle(Book b)
        {
            var n = b.Name;
            if (n.EndsWith("残卷")) n = n.Substring(0, n.Length - 2).Trim('《', '》') + "·残卷";
            return n.Trim('《', '》');
        }

        // 策略一览的一句话说明（效果稿用语）
        static readonly Dictionary<string, string> StratBrief = new Dictionary<string, string>
        {
            ["引火"] = "单体燃烧", ["火烧营寨"] = "点燃周边敌军", ["火海"] = "大范围持续燃烧",
            ["截流"] = "推迟敌方行动", ["水攻"] = "冲击直线敌军", ["决堤"] = "洪流冲击大片敌军",
            ["抚军"] = "恢复单体兵力", ["鼓舞"] = "恢复兵力并鼓舞", ["收拢溃卒"] = "恢复全军兵力",
            ["坚壁"] = "减少所受伤害", ["八阵"] = "护佑周边友军", ["箭雨"] = "箭雨覆盖周边", ["万箭齐发"] = "密集箭雨覆盖",
        };

        List<Unit> CircuitRoster() => units.Where(x => x.Team == Team.Player).Concat(bench).ToList();

        void BuildBench()
        {
            bench.Clear();
            foreach (var (name, key, school, intel) in new[] { ("赵云", "ZhaoYun", School.Bing, 76), ("曹操", "CaoCao", School.Fa, 91), ("孙权", "SunQuan", School.Dao, 80) })
                bench.Add(new Unit { Name = name, Key = key, Team = Team.Player, School = school, Element = Circuits.Of(school).Affinity, Int = intel });
        }

        // 藏书阁：本将已装备的在前（有珠），其余按五行、品级；他人装备的书不显示
        List<Book> ShelfFor(Unit u) => library.Where(b => { var o = Owner(b); return o == null || o == u; })
            .OrderBy(b => Owner(b) == u ? 0 : 1).ThenBy(b => b.Element).ThenByDescending(b => b.Grade).ToList();

        void EquipBook(Unit u, Book b)
        {
            var c = u.Circuit;
            if (u.Slots.Contains(b)) { u.Slots[System.Array.IndexOf(u.Slots, b)] = null; return; }   // 再点已装备的书：卸下
            int slot = circuitSlot >= 0 && circuitSlot < c.Slots.Length && CircuitMath.Fits(c, circuitSlot, b) ? circuitSlot
                : Enumerable.Range(0, c.Slots.Length).Where(i => u.Slots[i] == null && CircuitMath.Fits(c, i, b)).DefaultIfEmpty(-1).First();
            if (slot < 0) { Log($"{BookTitle(b)}放不进{u.Name}的命盘：没有合适的空槽"); return; }
            u.Slots[slot] = b; circuitSlot = slot;
        }

        int libScroll, stratScroll, rosterScroll;
        Vector2 SocketPos(Vector2 hub, CircuitDef c, int i, float r) => hub + new Vector2(c.Slots[i].Pos.x, -c.Slots[i].Pos.y) * r;

        void DrawCircuit(float sw)
        {
            dk = RefH / MockH; dox = (sw - MockW * dk) / 2f;
            var roster = CircuitRoster();
            circuitUnit = Mathf.Clamp(circuitUnit, 0, roster.Count - 1);
            var u = roster[circuitUnit]; var c = u.Circuit;
            var ivory = new Color(0.93f, 0.91f, 0.84f); var silver = new Color(0.72f, 0.80f, 0.90f);

            // 背景：夜营，压暗
            var bg = DT("bg_night_camp");
            if (bg) GUI.DrawTexture(new Rect(0, 0, sw, RefH), bg, ScaleMode.ScaleAndCrop);
            Rect(new Rect(0, 0, sw, RefH), bg ? new Color(0.02f, 0.04f, 0.09f, 0.35f) : new Color(0.03f, 0.05f, 0.10f, 0.9f));

            // 顶栏
            var top = new Rect(0, 0, sw, 80 * dk);
            if (!DFrame(top, "title_bar")) { Rect(top, new Color(0.03f, 0.06f, 0.12f, 0.92f)); Rect(new Rect(0, top.yMax - 1.5f, sw, 1.5f), CyanDim); }
            GUI.Label(MR(80, 6, 300, 60), "兵书回路", St(MF(46), ivory, TextAnchor.MiddleLeft, true));
            GUI.Label(MR(290, 22, 400, 44), "/  兵书配置  /  布阵前准备", St(MF(20), silver));
            GUI.Label(MR(1250, 16, 390, 50), $"{u.Name} <size={MF(18)}>·</size> <size={MF(20)}>当前配置</size>", St(MF(26), ivory, TextAnchor.MiddleRight, true));

            DrawRosterPanel(roster, u, ivory);
            DrawPlatePanel(u, c, ivory);
            DrawLibraryPanel(u, c, ivory);
            DrawStrategyPanel(u, c, ivory);
            DrawCircuitFooter(sw, ivory);
        }

        void PanelV(Rect r, string title, Color ivory)
        {
            if (!DFrame(r, "panel_frame")) Frame(r, "panel_frame");
            var hr = new Rect(r.x + 6 * dk, r.y + 4 * dk, r.width - 12 * dk, 46 * dk);
            if (!DFrame(hr, "panel_header")) Rect(new Rect(hr.x + 8, hr.yMax - 1, hr.width - 16, 1), CyanDim);
            GUI.Label(new Rect(r.x + 32 * dk, r.y + 4 * dk, r.width, 46 * dk), title, St(MF(30), ivory, TextAnchor.MiddleLeft, true));
        }

        void ScrollBar(float x, float y0, float y1, int first, int visible, int total)
        {
            var track = MR(x, y0, 10, y1 - y0);
            if (!DFrame(track, "scroll_track")) Rect(new Rect(track.center.x - 1, track.y, 2, track.height), new Color(0.4f, 0.55f, 0.7f, 0.5f));
            float frac = total <= visible ? 1f : visible / (float)total, pos = total <= visible ? 0f : first / (float)(total - visible);
            float th = Mathf.Max(20, track.height * frac), ty = track.y + (track.height - th) * pos;
            var thumb = new Rect(track.x, ty, track.width, th);
            var tt = DT("scroll_thumb"); if (tt) GUI.DrawTexture(thumb, tt, ScaleMode.StretchToFill); else Rect(new Rect(thumb.x + 2, thumb.y, thumb.width - 4, thumb.height), new Color(0.55f, 0.70f, 0.85f, 0.85f));
            var ar = DT("scroll_arrow_up");
            if (ar)
            {
                GUI.DrawTexture(MR(x - 3, y0 - 14, 16, 10), ar, ScaleMode.ScaleToFit);
                var m = GUI.matrix; var a = MR(x - 3, y1 + 4, 16, 10);
                GUI.matrix = m * Matrix4x4.TRS(a.center, Quaternion.identity, new Vector3(1, -1, 1)) * Matrix4x4.TRS(-(Vector3)a.center, Quaternion.identity, Vector3.one); GUI.DrawTexture(a, ar, ScaleMode.ScaleToFit); GUI.matrix = m;
            }
        }

        // ---------------- 武将 ----------------
        void DrawRosterPanel(List<Unit> roster, Unit u, Color ivory)
        {
            PanelV(MR(24, 92, 292, 760), "武将", ivory);
            const int visible = 6;
            rosterScroll = Mathf.Clamp(rosterScroll, Mathf.Max(0, circuitUnit - visible + 1), Mathf.Min(circuitUnit, Mathf.Max(0, roster.Count - visible)));
            for (int k = 0; k < visible && rosterScroll + k < roster.Count; k++)
            {
                int i = rosterScroll + k; var hu = roster[i]; bool sel = i == circuitUnit;
                var r = MR(36, 150 + k * 118.5f, 252, 110);
                var bust = DT("bust_" + hu.Key.ToLowerInvariant()) ?? Bust(hu);
                if (!DFrame(r, sel ? "card_selected" : "card")) Rect(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), new Color(0.03f, 0.06f, 0.12f, 0.9f));
                if (bust) GUI.DrawTexture(new Rect(r.x + 4, r.y + 4, r.width - 8, r.height - 8), bust, ScaleMode.ScaleAndCrop);
                // 名字落在卡片右侧的暗底上
                Rect(new Rect(r.x + r.width * 0.62f, r.y + 3, r.width * 0.38f - 3, r.height - 6), new Color(0.02f, 0.05f, 0.10f, 0.35f));
                Outline(new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6), sel ? new Color(0.45f, 0.90f, 1f, 0.95f) : new Color(0.55f, 0.65f, 0.78f, 0.45f), sel ? 2f : 1f);
                GUI.Label(new Rect(r.x + r.width * 0.64f, r.y, r.width * 0.36f, r.height), hu.Name, St(MF(26), sel ? Color.white : ivory, TextAnchor.MiddleCenter, true));
                if (sel) DTex(MR(14, 150 + k * 118.5f + 38, 22, 34), "select_arrow", Cyan);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none) && i != circuitUnit) { circuitUnit = i; circuitSlot = -1; circuitFocus = 0; libScroll = 0; }
            }
            ScrollBar(296, 160, 828, rosterScroll, visible, roster.Count);
        }

        // ---------------- 命盘 ----------------
        void DrawPlatePanel(Unit u, CircuitDef c, Color ivory)
        {
            PanelV(MR(336, 92, 532, 760), "命盘", ivory);
            var hub = MP(603, 438); float half = 272 * dk, R = 150 * dk;
            var plateTex = DT("destiny_plate");
            if (plateTex) GUI.DrawTexture(new Rect(hub.x - half, hub.y - half, half * 2, half * 2), plateTex);
            else { if (!plate) plate = MakeBaguaPlate(400); GUI.DrawTexture(new Rect(hub.x - half * 0.95f, hub.y - half * 0.95f, half * 1.9f, half * 1.9f), plate); }

            // 连线：深色描边＋线路色（线1朱红、线2青蓝……）
            var segs = CircuitMath.Segments(c, u.Int, out var edges);
            foreach (var ed in edges)
            {
                Vector2 p0 = SocketPos(hub, c, ed.A, R), p1 = SocketPos(hub, c, ed.B, R);
                if (ed.Line < 0)
                {
                    var mid = (p0 + p1) / 2f; var dir = (p1 - p0).normalized * 8f;
                    Line(p0, mid - dir, 2, new Color(0.5f, 0.55f, 0.62f, 0.6f)); Line(mid + dir, p1, 2, new Color(0.5f, 0.55f, 0.62f, 0.6f));
                    continue;
                }
                var col = PlateLineColor(ed.Line);
                Line(p0, p1, 9 * dk, new Color(0.02f, 0.04f, 0.08f, 0.9f));
                Line(p0, p1, 6 * dk, new Color(col.r, col.g, col.b, 0.35f));
                Line(p0, p1, 3.2f * dk, col);
            }

            // 槽位：中枢是太极，下方小牌写中枢兵书的五行与数值；外圈槽位环内放宝石图标与数值
            float sock = 80 * dk;
            for (int i = 0; i < c.Slots.Length; i++)
            {
                var p = SocketPos(hub, c, i, R); var book = u.Slots[i]; bool sel = i == circuitSlot && circuitFocus <= 1;
                var rr = new Rect(p.x - sock / 2, p.y - sock / 2, sock, sock);
                if (c.Slots[i].Gua < 0)
                {
                    var tr = new Rect(p.x - 52 * dk, p.y - 52 * dk, 104 * dk, 104 * dk);
                    if (sel) Disc(p, 58 * dk, new Color(0.35f, 0.85f, 1f, 0.35f + 0.15f * Mathf.Sin(Time.time * 5f)));
                    DTex(Grow(tr, 1.5f), "taiji_hub", new Color(0.85f, 0.85f, 0.8f), true);
                    var badge = MR(556, 494, 94, 38);
                    if (!DFrame(badge, "count_badge")) { Rect(badge, new Color(0.03f, 0.07f, 0.14f, 0.92f)); }
                    if (book != null) { Gem(MR(568, 498, 30, 30), book.Element); GUI.Label(MR(600, 494, 50, 38), $"×{CircuitMath.Value(c, book)}", St(MF(22), ivory, TextAnchor.MiddleLeft)); }
                    else { Gem(MR(568, 498, 30, 30), c.Slots[i].Locked); GUI.Label(MR(600, 494, 50, 38), "×0", St(MF(22), Grey, TextAnchor.MiddleLeft)); }
                }
                else
                {
                    if (sel) Disc(p, sock * 0.62f, new Color(0.35f, 0.85f, 1f, 0.30f + 0.15f * Mathf.Sin(Time.time * 5f)));
                    string ring = book != null ? (sel ? "socket_ring_selected" : "socket_ring") : (sel ? "socket_plus" : "socket_empty");
                    if (!DT(ring))
                    {
                        Disc(p, sock * 0.5f, sel ? Cyan : new Color(0.62f, 0.70f, 0.80f)); Disc(p, sock * 0.44f, new Color(0.04f, 0.07f, 0.14f));
                        if (book == null) GUI.Label(rr, sel ? "+" : "□", St(MF(30), new Color(0.75f, 0.82f, 0.9f), TextAnchor.MiddleCenter));
                    }
                    else GUI.DrawTexture(Grow(rr, ring == "socket_ring_selected" ? 1.17f : 1.43f), DT(ring));
                    if (book != null)
                    {
                        Gem(new Rect(p.x - 17 * dk, p.y - 27 * dk, 34 * dk, 34 * dk), book.Element);
                        GUI.Label(new Rect(p.x - 20 * dk, p.y + 4 * dk, 40 * dk, 26 * dk), CircuitMath.Value(c, book).ToString(), St(MF(22), ivory, TextAnchor.MiddleCenter, true));
                    }
                    else if (c.Slots[i].Locked != Element.None)
                    {
                        var o = GUI.color; GUI.color = new Color(1, 1, 1, 0.45f);
                        Gem(new Rect(p.x + 12 * dk, p.y + 12 * dk, 22 * dk, 22 * dk), c.Slots[i].Locked);
                        GUI.color = o;
                    }
                }
                if (GUI.Button(rr, GUIContent.none, GUIStyle.none))
                {
                    if (Event.current.button == 1 || (circuitSlot == i && book != null)) u.Slots[i] = null;
                    circuitSlot = i; circuitFocus = 0;
                }
            }

            // 线路合计
            var lg = MR(340, 740, 526, 110);
            Rect(new Rect(lg.x + 6, lg.y, lg.width - 12, lg.height - 6), new Color(0.03f, 0.06f, 0.12f, 0.55f));
            Rect(new Rect(lg.x + 14, lg.y + 52 * dk, lg.width - 28, 1), new Color(0.5f, 0.62f, 0.78f, 0.35f));
            for (int l = 0; l < segs.Length && l < 3; l++)
            {
                float y = 754 + l * 42 - (segs.Length > 2 ? 10 : 0);
                var t = CircuitMath.LineTotals(c, u.Slots, segs[l]);
                Rect(MR(360, y + 14, 32, 5), PlateLineColor(l));
                GUI.Label(MR(398, y, 40, 34), (l + 1).ToString(), St(MF(22), ivory, TextAnchor.MiddleLeft));
                int k = 0;
                foreach (var e in Wuxing.All)
                {
                    float x = 450 + k * 83;
                    Gem(MR(x, y + 3, 26, 26), e);
                    GUI.Label(MR(x + 30, y, 50, 34), $"×{t[e]}", St(MF(20), t[e] > 0 ? ivory : Grey, TextAnchor.MiddleLeft));
                    k++;
                }
            }
        }

        static Color PlateLineColor(int l) => l switch
        {
            0 => new Color(0.92f, 0.36f, 0.38f), 1 => new Color(0.30f, 0.70f, 0.95f), 2 => new Color(0.95f, 0.78f, 0.30f), _ => new Color(0.45f, 0.85f, 0.55f)
        };

        // ---------------- 藏书阁 ----------------
        void DrawLibraryPanel(Unit u, CircuitDef c, Color ivory)
        {
            PanelV(MR(892, 92, 436, 760), "藏书阁", ivory);
            var shelf = ShelfFor(u);
            const int cols = 2, rowsVisible = 5;
            if (circuitFocus == 1) bookSel = Mathf.Clamp(bookSel, 0, Mathf.Max(0, shelf.Count - 1));
            int selRow = bookSel / cols, maxFirst = Mathf.Max(0, (shelf.Count + 1) / 2 - rowsVisible);
            if (circuitFocus == 1) libScroll = Mathf.Clamp(libScroll, Mathf.Max(0, selRow - rowsVisible + 1), Mathf.Min(selRow, maxFirst));
            libScroll = Mathf.Clamp(libScroll, 0, maxFirst);
            bool lockedSlot = circuitSlot >= 0 && circuitSlot < c.Slots.Length && c.Slots[circuitSlot].Locked != Element.None && circuitFocus == 1;   // 只在挑书时把放不进去的书压暗
            for (int idx = libScroll * cols; idx < shelf.Count && idx < (libScroll + rowsVisible + 1) * cols; idx++)
            {
                var b = shelf[idx]; int row = idx / cols - libScroll, col = idx % cols;
                bool partial = row >= rowsVisible;   // 底部露出半张，表示还有
                var r = MR(900 + col * 202, 154 + row * 124.5f, 194, partial ? 40 : 108);
                bool sel = (circuitFocus == 1 && idx == bookSel);
                bool mine = Owner(b) == u;
                bool dim = lockedSlot && !CircuitMath.Fits(c, circuitSlot, b) && !mine;
                var o = GUI.color; if (partial) GUI.color = new Color(1, 1, 1, 0.35f); else if (dim) GUI.color = new Color(1, 1, 1, 0.45f);
                if (!DFrame(r, sel ? "card_selected" : "card")) Frame(r, sel ? "party_card_active" : "party_card", sel ? Cyan : (Color?)null);
                if (!partial)
                {
                    var sc = DT("scroll_" + ElemKey(b.Element));
                    var sr = MR(908 + col * 202, 164 + row * 124.5f, 52, 82);
                    if (sc) GUI.DrawTexture(Grow(sr, 1.5f), sc, ScaleMode.ScaleToFit); else { Rect(new Rect(sr.x + sr.width * 0.2f, sr.y, sr.width * 0.6f, sr.height), Wuxing.Tint(b.Element) * 0.8f); }
                    GUI.Label(MR(966 + col * 202, 166 + row * 124.5f, 130, 34), BookTitle(b), St(MF(BookTitle(b).Length > 5 ? 19 : 21), ivory, TextAnchor.MiddleLeft, true));
                    Gem(MR(968 + col * 202, 212 + row * 124.5f, 26, 26), b.Element);
                    GUI.Label(MR(998 + col * 202, 208 + row * 124.5f, 50, 34), $"×{b.Grade}", St(MF(21), ivory, TextAnchor.MiddleLeft));
                    var pr = MR(1040 + col * 202, 196 + row * 124.5f, 46, 46);
                    var pt = DT(mine ? "pearl_filled" : "pearl_empty");
                    if (pt) GUI.DrawTexture(pr, pt, ScaleMode.ScaleToFit);
                    else { Disc(pr.center, pr.width * 0.46f, new Color(0.45f, 0.55f, 0.68f)); Disc(pr.center, pr.width * 0.40f, mine ? new Color(0.92f, 0.90f, 0.82f) : new Color(0.04f, 0.06f, 0.10f)); }
                    if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { bookSel = idx; EquipBook(u, b); }
                }
                GUI.color = o;
            }
            ScrollBar(1310, 162, 828, libScroll, rowsVisible, (shelf.Count + 1) / 2);
        }

        // ---------------- 策略一览 ----------------
        void DrawStrategyPanel(Unit u, CircuitDef c, Color ivory)
        {
            PanelV(MR(1350, 92, 298, 760), "策略一览", ivory);
            var lit = CircuitMath.Lit(c, u.Slots, u.Int);
            var all = Strategies.All;
            const int visible = 13;
            stratScroll = Mathf.Clamp(stratScroll, 0, Mathf.Max(0, all.Length - visible));
            for (int k = 0; k < visible && stratScroll + k < all.Length; k++)
            {
                var s = all[stratScroll + k]; bool on = lit.Any(x => x.Def == s);
                bool sel = circuitFocus == 2 && listSel == stratScroll + k;
                var r = MR(1362, 146 + k * 53.6f, 276, 50);
                if (!DFrame(r, on || sel ? "row_lit" : "row")) Frame(r, on || sel ? "list_item_selected" : "list_item_normal", on ? Cyan : (Color?)null);
                var em = DT("emblem_" + ElemKey(s.Main));
                var er = MR(1372, 152 + k * 53.6f, 38, 38);
                if (em) GUI.DrawTexture(Grow(er, 1.7f), em, ScaleMode.ScaleToFit); else { Disc(er.center, er.width * 0.48f, Wuxing.Tint(s.Main)); Disc(er.center, er.width * 0.40f, new Color(0.04f, 0.07f, 0.14f)); Gem(new Rect(er.x + er.width * 0.2f, er.y + er.height * 0.2f, er.width * 0.6f, er.height * 0.6f), s.Main); }
                GUI.Label(MR(1424, 146 + k * 53.6f, 150, 28), s.Name, St(MF(21), ivory, TextAnchor.MiddleLeft, true));
                GUI.Label(MR(1424, 171 + k * 53.6f, 150, 22), StratBrief.TryGetValue(s.Name, out var bf) ? bf : s.Desc, St(MF(15), new Color(0.75f, 0.80f, 0.88f), TextAnchor.MiddleLeft));
                DrawRangeGrid(MR(1566, 150 + k * 53.6f, 64, 42), s);
            }
        }

        // 范围小格：按策略的施放范围画几格亮块（视觉摘要，精确范围以规则为准）
        void DrawRangeGrid(Rect box, SkillDef s)
        {
            int[,] pat;
            if (s.Area == Area.Line) pat = s.Width >= 3 ? new[,] { { 1, 1, 1, 1 }, { 1, 1, 1, 1 } } : new[,] { { 1, 1, 1, 1 } };
            else if (s.Area == Area.Circle && s.Radius >= 99) pat = new[,] { { 1, 1, 1, 1, 1 }, { 1, 1, 1, 1, 1 }, { 1, 1, 1, 1, 1 } };
            else if (s.Area == Area.Circle && s.Radius >= 2) pat = new[,] { { 0, 0, 1, 0, 0 }, { 0, 1, 1, 1, 0 }, { 1, 1, 1, 1, 1 }, { 0, 1, 1, 1, 0 }, { 0, 0, 1, 0, 0 } };
            else if (s.Area == Area.Circle) pat = new[,] { { 0, 1, 0 }, { 1, 1, 1 }, { 0, 1, 0 } };
            else pat = new[,] { { 0, 0, 0 }, { 0, 1, 0 }, { 0, 0, 0 } };
            int rows = pat.GetLength(0), cols = pat.GetLength(1);
            float cell = Mathf.Min(box.width / cols, box.height / rows, 11 * dk), gap = Mathf.Max(1f, cell * 0.14f);
            float w = cols * cell, h = rows * cell; float x0 = box.center.x - w / 2, y0 = box.center.y - h / 2;
            var tint = Wuxing.Tint(s.Main); tint = Color.Lerp(tint, Color.white, s.Main == Element.Water ? 0.35f : 0.15f);
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                {
                    var cr = new Rect(x0 + x * cell + gap / 2, y0 + y * cell + gap / 2, cell - gap, cell - gap);
                    Rect(cr, pat[y, x] == 1 ? tint : new Color(0.25f, 0.32f, 0.42f, 0.45f));
                }
        }

        // ---------------- 底栏 ----------------
        void DrawCircuitFooter(float sw, Color ivory)
        {
            Rect(new Rect(0, 870 * dk, sw, RefH - 870 * dk), new Color(0.02f, 0.04f, 0.08f, 0.6f));
            Rect(new Rect(0, 870 * dk, sw, 1), new Color(0.45f, 0.6f, 0.78f, 0.45f));
            var hints = padMode
                ? new[] { ("LB/RB", "切换武将"), ("Y", "切换列表"), ("↑↓", "选择"), ("A", "镶嵌兵书"), ("X", "卸下兵书"), ("B", "返回") }
                : new[] { ("Q/E", "切换武将"), ("Tab", "切换列表"), ("↑↓", "选择"), ("Enter", "镶嵌兵书"), ("C", "卸下兵书"), ("Esc", "返回") };
            float x = 40;
            foreach (var (key, label) in hints)
            {
                float kw = Mathf.Max(52, key.Length * 14 + 22);
                var kr = MR(x, 894, kw, 30);
                if (!DFrame(kr, "keycap")) { Rect(kr, new Color(0.04f, 0.08f, 0.15f, 0.9f)); Frame(kr, "hint_bar"); }
                GUI.Label(kr, key, St(MF(17), ivory, TextAnchor.MiddleCenter));
                GUI.Label(MR(x + kw + 10, 890, 120, 38), label, St(MF(18), ivory));
                x += kw + 10 + label.Length * 18 + 24;
            }
            var br = MR(1414, 880, 234, 52);
            if (!DFrame(br, "button_return")) Frame(br, "ring_nameplate", Cyan);
            GUI.Label(new Rect(br.x, br.y, br.width * 0.78f, br.height), "回到布阵", St(MF(24), ivory, TextAnchor.MiddleCenter, true));
            if (!DT("button_return")) GUI.Label(new Rect(br.x + br.width * 0.75f, br.y, br.width * 0.2f, br.height), "»", St(MF(26), Cyan, TextAnchor.MiddleCenter, true));
            if (GUI.Button(br, GUIContent.none, GUIStyle.none)) { mode = Mode.Deploy; circuitSlot = -1; }
        }

        // 兵书回路的键鼠／手柄：Q/E（LB/RB）换武将；Tab（Y）在命盘、藏书阁、策略一览之间切换；
        // 命盘里方向键选槽、确认进藏书阁；藏书阁里方向键选书、确认镶嵌（再确认已装备的书则卸下）；C（X）卸下；Esc（B）返回
        // 原型对比用的隐藏键：F1 换学派，F2／F3 智力 −10／+10
        void CircuitInput(Vector2Int pulse, bool ok, bool cancel, bool lb, bool rb)
        {
            var roster = CircuitRoster();
            if (lb || rb) { circuitUnit = (circuitUnit + (rb ? 1 : roster.Count - 1)) % roster.Count; circuitSlot = 0; circuitFocus = 0; libScroll = 0; }
            var u = roster[Mathf.Clamp(circuitUnit, 0, roster.Count - 1)]; var c = u.Circuit;
            if (Pressed(Kb?.f1Key))
            {
                int si = System.Array.FindIndex(Circuits.All, x => x.School == u.School);
                SetSchool(u, Circuits.All[(si + 1) % Circuits.All.Length].School);
                Log($"{u.Name}改学{u.Circuit.Name}（原型对比）");
                circuitSlot = Mathf.Clamp(circuitSlot, 0, u.Circuit.Slots.Length - 1);
                return;
            }
            if (Pressed(Kb?.f2Key)) u.Int = Mathf.Max(10, u.Int - 10);
            if (Pressed(Kb?.f3Key)) u.Int = Mathf.Min(100, u.Int + 10);
            if (Pressed(Kb?.tabKey, Pad?.buttonNorth)) { circuitFocus = (circuitFocus + 1) % 3; if (circuitFocus == 0 && circuitSlot < 0) circuitSlot = 0; return; }
            if (Pressed(Kb?.fKey, Pad?.startButton)) { mode = Mode.Deploy; return; }
            bool unequip = Pressed(Kb?.deleteKey, Kb?.cKey, Pad?.buttonWest);
            if (circuitFocus == 0)
            {
                if (circuitSlot < 0) circuitSlot = 0;
                int step = pulse.y != 0 ? -pulse.y : pulse.x;
                if (step != 0) circuitSlot = (circuitSlot + step + c.Slots.Length) % c.Slots.Length;
                if (unequip) u.Slots[circuitSlot] = null;
                if (ok)
                {
                    circuitFocus = 1;
                    var shelf = ShelfFor(u);
                    bookSel = Mathf.Max(0, shelf.FindIndex(b => CircuitMath.Fits(c, circuitSlot, b) && Owner(b) == null));
                }
                if (cancel) { mode = Mode.Deploy; circuitSlot = -1; }
            }
            else if (circuitFocus == 1)
            {
                var shelf = ShelfFor(u);
                if (pulse.y != 0) bookSel = Mathf.Clamp(bookSel - pulse.y * 2, 0, shelf.Count - 1);
                if (pulse.x != 0) bookSel = Mathf.Clamp(bookSel + pulse.x, 0, shelf.Count - 1);
                if (ok && shelf.Count > 0) { EquipBook(u, shelf[bookSel]); }
                if (unequip && shelf.Count > 0 && u.Slots.Contains(shelf[bookSel])) u.Slots[System.Array.IndexOf(u.Slots, shelf[bookSel])] = null;
                if (cancel) circuitFocus = 0;
            }
            else
            {
                if (pulse.y != 0) listSel = Mathf.Clamp(listSel - pulse.y, 0, Strategies.All.Length - 1);
                if (cancel) circuitFocus = 0;
            }
        }
    }
}
