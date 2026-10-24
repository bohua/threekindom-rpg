// PROTOTYPE - NOT FOR PRODUCTION
// Question: 空轨式 AT 行动条＋小范围格子战斗，配合“学派回路镶嵌兵书→策略”，是否好玩、是否看得懂？
// Date: 2026-10-24
// 战斗界面按用户选定的蓝色设计稿重做（refs/battle-poc/ref5-battle-ui-target-blue.png）：
// 顶部标题栏、左侧行动顺序、中央六令转轮、右侧武技／策略面板、下方原因提示条、底部武将卡与操作提示。
// 切图来自 Codex（art/battle-poc-ui-slices-blue），缺图时用代码画的同色面板顶替。
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Prototype.BingshuTactics
{
    public sealed partial class TacticsGame
    {
        // 由编辑器构建脚本填入（名称不含扩展名）
        public Texture2D[] UiTextures = new Texture2D[0];
        public Texture2D LuBuHudBust;
        Dictionary<string, Texture2D> ui;

        static readonly Color Navy = new Color(0.05f, 0.09f, 0.16f, 0.88f), NavyDeep = new Color(0.03f, 0.05f, 0.10f, 0.94f);
        static readonly Color Cyan = new Color(0.35f, 0.85f, 1f), CyanDim = new Color(0.25f, 0.55f, 0.70f, 0.9f), Amber = new Color(1f, 0.80f, 0.35f);
        static readonly Color Grey = new Color(0.55f, 0.60f, 0.68f);

        Texture2D UI(string name)
        {
            if (ui == null) ui = UiTextures.Where(t => t).ToDictionary(t => t.name, t => t);
            return ui.TryGetValue(name, out var t) ? t : null;
        }

        // 九宫格：四角 24 像素不拉伸；缺图时画深蓝底＋青色细边
        void Frame(Rect r, string name, Color? fallbackEdge = null, float border = 24f)
        {
            var t = UI(name);
            if (t) { Rect(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), new Color(0.02f, 0.05f, 0.10f, 0.72f)); NineSlice(r, t, border); return; }
            Rect(r, name.Contains("selected") || name.Contains("active") || name.Contains("current") ? new Color(0.06f, 0.20f, 0.30f, 0.95f) : Navy);
            var edge = fallbackEdge ?? CyanDim;
            Rect(new Rect(r.x, r.y, r.width, 1.5f), edge); Rect(new Rect(r.x, r.yMax - 1.5f, r.width, 1.5f), edge);
            Rect(new Rect(r.x, r.y, 1.5f, r.height), edge); Rect(new Rect(r.xMax - 1.5f, r.y, 1.5f, r.height), edge);
        }

        // 自绘九宫格：贴图四边各取 b 像素（贴图像素），屏幕上四角按 min(b, 边长/2) 绘制，中间拉伸
        static void NineSlice(Rect r, Texture2D t, float b)
        {
            float tw = t.width, th = t.height;
            float bx = Mathf.Min(b, tw / 2 - 1), by = Mathf.Min(b, th / 2 - 1);
            float sx = Mathf.Min(bx, r.width / 2), sy = Mathf.Min(by, r.height / 2);
            float[] xs = { r.x, r.x + sx, r.xMax - sx, r.xMax }, ys = { r.y, r.y + sy, r.yMax - sy, r.yMax };
            float[] us = { 0, bx / tw, 1 - bx / tw, 1 }, vs = { 1, 1 - by / th, by / th, 0 };   // GUI y 向下，纹理 v 向上
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    var dst = UnityEngine.Rect.MinMaxRect(xs[i], ys[j], xs[i + 1], ys[j + 1]);
                    if (dst.width <= 0 || dst.height <= 0) continue;
                    var uv = UnityEngine.Rect.MinMaxRect(us[i], vs[j + 1], us[i + 1], vs[j]);
                    GUI.DrawTextureWithTexCoords(dst, t, uv);
                }
        }

        void Icon(Rect r, string name, Color fallback)
        {
            var t = UI(name);
            if (t) GUI.DrawTexture(r, t, ScaleMode.ScaleToFit);
            else Disc(r.center, r.width * 0.42f, fallback);
        }

        GUIStyle St(int size, Color c, TextAnchor a = TextAnchor.MiddleLeft, bool bold = false)
        {
            var st = new GUIStyle(text) { fontSize = size, alignment = a, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal };
            st.normal.textColor = c;
            return st;
        }

        static readonly Dictionary<string, string> CmdIcon = new Dictionary<string, string>
        { ["攻击"] = "cmd_attack", ["策略"] = "cmd_strategy", ["移动"] = "cmd_move", ["待机"] = "cmd_wait", ["防御"] = "cmd_defend", ["武技"] = "cmd_craft" };

        // 不可用的原因（提示条与置灰说明用）
        string DisabledReason(string item)
        {
            switch (item)
            {
                case "攻击": return "攻击范围内没有敌人";
                case "移动": return "本回合已移动";
                case "武技":
                    if (!active.Crafts.Any(c => active.Cp >= c.Cost)) return "气势不足";
                    return "范围内没有目标";
                case "策略":
                    if (moved) return "已移动，策略须原地咏唱";
                    var lit = StrategiesOf(active);
                    if (lit.Count == 0) return "没有点亮的策略";
                    if (!lit.Any(l => active.Ep >= l.Def.Cost)) return "策力不足";
                    return "范围内没有目标";
            }
            return "";
        }

        int RoundNo => 1 + Mathf.FloorToInt(Mathf.Max(0, now) / 100f);

        // ---------------- 顶栏 ----------------
        void DrawTopBarV(float sw)
        {
            Rect(new Rect(0, 0, sw, 46), new Color(0.03f, 0.06f, 0.11f, 0.92f));
            Rect(new Rect(0, 46, sw, 1.5f), CyanDim);
            GUI.Label(new Rect(28, 2, 300, 42), "临阵·六令", St(26, Color.white, TextAnchor.MiddleLeft, true));
            string where = stage == 1 ? "虎牢关外" : "黄巾旷野";
            string who = active != null && mode != Mode.Deploy && mode != Mode.Circuit ? $" · {active.Name}的回合" : mode == Mode.Deploy ? " · 布阵" : "";
            GUI.Label(new Rect(sw - 560, 2, 400, 42), where + who, St(16, new Color(0.85f, 0.92f, 1f), TextAnchor.MiddleRight));
            if (mode != Mode.Deploy && mode != Mode.Circuit) GUI.Label(new Rect(sw - 150, 2, 130, 42), $"第{RoundNo}回合", St(20, Amber, TextAnchor.MiddleRight, true));
            // 日志：顶栏下方一行小字
            if (!string.IsNullOrEmpty(log) && mode != Mode.Circuit)
            {
                Rect(new Rect(sw / 2 - 300, 50, 600, 24), new Color(0.03f, 0.05f, 0.10f, 0.7f));
                GUI.Label(new Rect(sw / 2 - 296, 50, 592, 24), log, St(14, new Color(0.85f, 0.92f, 1f), TextAnchor.MiddleCenter));
            }
        }

        // ---------------- 左侧行动顺序 ----------------
        void DrawAtBarV()
        {
            if (mode == Mode.Deploy || mode == Mode.Circuit) return;
            var list = units.Where(x => x.Alive).OrderBy(x => x.At).ThenBy(x => x.Team == Team.Player ? 0 : 1).Take(6).ToList();
            var panel = new Rect(14, 78, 210, 40 + list.Count * 72);
            Frame(panel, "panel_frame");
            GUI.Label(new Rect(panel.x + 14, panel.y + 6, 180, 28), "行动顺序", St(18, Color.white, TextAnchor.MiddleLeft, true));
            float y = panel.y + 40;
            foreach (var u in list)
            {
                var r = new Rect(panel.x + 8, y, panel.width - 16, 66);
                Frame(r, u == active ? "at_item_current" : u.Team == Team.Player ? "at_item_ally" : "at_item_enemy", u == active ? Cyan : (Color?)null);
                if (u != active) Rect(new Rect(r.x + 2, r.y + 8, 3, r.height - 16), u.Team == Team.Player ? new Color(0.35f, 0.65f, 1f) : new Color(0.95f, 0.35f, 0.35f));
                var b = HudPortrait(u);
                if (b) GUI.DrawTexture(new Rect(r.x + 10, r.y + 6, 54, 54), b, ScaleMode.ScaleAndCrop);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) FocusOn(u);   // 点头像对准此人
                GUI.Label(new Rect(r.x + 72, r.y + 6, 120, 26), u.Name, St(17, Color.white, TextAnchor.MiddleLeft, true));
                string sub = u == active ? "当前" : u.PendingCast != null ? "咏唱中" : $"{(u.Team == Team.Player ? "友" : "敌")} · {u.At - now:00}";
                GUI.Label(new Rect(r.x + 72, r.y + 34, 120, 24), sub, St(13, u == active ? Cyan : u.PendingCast != null ? new Color(0.85f, 0.65f, 1f) : Grey));
                y += 72;
            }
        }

        Texture2D HudPortrait(Unit u) => u.Key switch
        {
            "LiuBei" => LiuBeiBust, "GuanYu" => GuanYuBust, "ZhangFei" => ZhangFeiBust, "LuBu" => LuBuHudBust ? LuBuHudBust : LuBuBust,
            _ => UI("enemy_portrait_generic")
        };

        // ---------------- 中央转轮 ----------------
        void DrawRingV()
        {
            var c = ToGui(views[active].position + Vector3.up * 0.9f);
            float target = -ringSel * 60f;
            ringAngle = Mathf.LerpAngle(ringAngle, target, 0.25f);
            const float R = 118f;
            var track = UI("ring_track");
            if (track) { float tr = R * 480f / 456f; GUI.DrawTexture(new Rect(c.x - tr, c.y - tr, tr * 2, tr * 2), track); }   // 轨道圆半径约占图 456/480
            else
                for (int k = 0; k < 72; k++)
                {
                    float a = k * 5f * Mathf.Deg2Rad;
                    Rect(new Rect(c.x + Mathf.Cos(a) * R - 1, c.y + Mathf.Sin(a) * R - 1, 2.5f, 2.5f), new Color(Cyan.r, Cyan.g, Cyan.b, 0.55f));
                }
            // 先画非选中，再画选中（压在上面）
            foreach (bool drawSel in new[] { false, true })
                for (int i = 0; i < RingItems.Length; i++)
                {
                    bool sel = i == ringSel;
                    if (sel != drawSel) continue;
                    float ang = (i * 60f + ringAngle) * Mathf.Deg2Rad;
                    var p = c + new Vector2(Mathf.Cos(ang), -Mathf.Sin(ang)) * R;
                    string item = RingItems[i]; bool usable = RingUsable(item);
                    float size = sel ? 96 : 68;
                    var ir = new Rect(p.x - size / 2, p.y - size / 2, size, size);
                    if (sel)
                    {
                        var plate = new Rect(p.x + 24, p.y - 26, 170, 52);
                        var pt = UI("ring_nameplate");
                        if (pt) GUI.DrawTexture(plate, pt); else { Rect(plate, new Color(0.06f, 0.22f, 0.32f, 0.95f)); Rect(new Rect(plate.x, plate.y, plate.width, 2), Cyan); Rect(new Rect(plate.x, plate.yMax - 2, plate.width, 2), Cyan); }
                        GUI.Label(new Rect(plate.x + 40, plate.y, 120, plate.height), item, St(24, Color.white, TextAnchor.MiddleLeft, true));
                    }
                    Icon(ir, $"{CmdIcon[item]}_{(sel && usable ? "selected" : usable ? "normal" : "disabled")}", usable ? (sel ? Cyan : new Color(0.12f, 0.20f, 0.32f)) : new Color(0.25f, 0.27f, 0.30f));
                    if (!UI($"{CmdIcon[item]}_normal")) GUI.Label(ir, item.Substring(0, 1), St(sel ? 26 : 20, usable ? Color.white : Grey, TextAnchor.MiddleCenter, true));
                    if (!sel)
                    {
                        var lab = new Rect(p.x - 32, p.y + size / 2 - 6, 64, 24);
                        Rect(lab, new Color(0.03f, 0.06f, 0.12f, 0.9f));
                        GUI.Label(lab, item, St(15, usable ? Color.white : Grey, TextAnchor.MiddleCenter, true));
                    }
                    if (usable && GUI.Button(ir, GUIContent.none, GUIStyle.none)) { ringSel = i; ChooseRing(i); }   // 单击直接触发；置灰点不中
                }
            // 地图上的当前单位指示
            var arrow = UI("cursor_arrow");
            var ap = ToGui(views[active].position + Vector3.up * (2.2f * HeadK));
            if (arrow) GUI.DrawTexture(new Rect(ap.x - 14, ap.y - 14 + Mathf.Sin(Time.time * 5f) * 3f, 28, 28), arrow);
            DrawSidePanel(false);
            DrawHintBar();
        }

        // 下方提示条：有置灰的指令就说明原因，并给出可用数／总数
        void DrawHintBar()
        {
            float sw = Screen.width / S;
            int usable = RingItems.Count(RingUsable);
            var dis = RingItems.FirstOrDefault(x => !RingUsable(x));
            var r = new Rect(sw / 2 - 270, RefH - 214, 540, 44);
            Frame(r, "hint_bar");
            string msg = dis != null ? $"{dis}不可用 · {DisabledReason(dis)}" : $"{RingItems[ringSel]}";
            Icon(new Rect(r.x + 12, r.y + 8, 28, 28), dis != null ? $"{CmdIcon[dis]}_disabled" : $"{CmdIcon[RingItems[ringSel]]}_normal", Grey);
            GUI.Label(new Rect(r.x + 48, r.y, 380, r.height), msg, St(17, Color.white, TextAnchor.MiddleLeft, true));
            GUI.Label(new Rect(r.xMax - 90, r.y, 76, r.height), $"{usable} / {RingItems.Length}", St(17, Amber, TextAnchor.MiddleRight, true));
        }

        // ---------------- 右侧武技／策略面板 ----------------
        // focusList：在武技或策略列表里选招时为 true，列表项可点、高亮当前项
        void DrawSidePanel(bool focusList)
        {
            float sw = Screen.width / S;
            var crafts = active.Crafts.Select(s => (s, 1f, ok: active.Cp >= s.Cost && HasTargets(active, s), why: active.Cp < s.Cost ? "气势不足" : !HasTargets(active, s) ? "无目标" : "")).ToList();
            var strats = StrategiesOf(active).Select(l => (s: l.Def, l.PowerMul, ok: !moved && active.Ep >= l.Def.Cost && HasTargets(active, l.Def),
                why: moved ? "需原地" : active.Ep < l.Def.Cost ? "策力不足" : !HasTargets(active, l.Def) ? "无目标" : "")).ToList();
            float rowH = 56, h = 70 + crafts.Count * (rowH + 6) + 64 + Mathf.Max(1, strats.Count) * (rowH + 6) + 10;
            var panel = new Rect(sw - 440, 78, 424, Mathf.Min(h, RefH - 238));
            Frame(panel, "panel_frame");
            float y = panel.y + 10;
            void Header(string title, string icon, string res, int val)
            {
                Icon(new Rect(panel.x + 14, y + 6, 38, 38), icon, Cyan);
                GUI.Label(new Rect(panel.x + 60, y, 160, 50), title, St(24, Color.white, TextAnchor.MiddleLeft, true));
                GUI.Label(new Rect(panel.xMax - 220, y, 120, 50), active.Name, St(16, new Color(0.85f, 0.92f, 1f), TextAnchor.MiddleRight));
                GUI.Label(new Rect(panel.xMax - 96, y, 82, 50), $"{res} <color=#FFCC59><b>{val}</b></color>", St(16, new Color(0.85f, 0.92f, 1f), TextAnchor.MiddleRight));
                y += 52; Rect(new Rect(panel.x + 12, y, panel.width - 24, 1.5f), CyanDim); y += 8;
            }
            void Row(SkillDef s, bool ok, string why, string costIcon, string costName, int idx, bool listIsThis)
            {
                if (y + rowH > panel.yMax - 6) return;
                var r = new Rect(panel.x + 10, y, panel.width - 20, rowH);
                bool sel = focusList && listIsThis && idx == listSel;
                Frame(r, sel ? "list_item_selected" : ok ? "list_item_normal" : "list_item_disabled", sel ? Cyan : (Color?)null);
                if (sel) GUI.Label(new Rect(r.x - 16, r.y, 16, r.height), "▶", St(14, Cyan, TextAnchor.MiddleCenter));
                string ic = s.IsStrategy ? ElemIcon(s.Element) : "cmd_craft_normal";
                Icon(new Rect(r.x + 8, r.y + 8, 40, 40), ic, s.IsStrategy ? Wuxing.Tint(s.Element) : Cyan);
                var nameCol = ok ? Color.white : Grey;
                GUI.Label(new Rect(r.x + 56, r.y + 2, 200, 28), s.Name, St(19, nameCol, TextAnchor.MiddleLeft, true));
                Icon(new Rect(r.x + 58, r.y + 32, 18, 18), costIcon, s.IsStrategy ? new Color(0.35f, 0.6f, 1f) : Amber);
                GUI.Label(new Rect(r.x + 80, r.y + 28, 110, 26), $"{costName} {s.Cost}", St(14, ok ? new Color(0.85f, 0.92f, 1f) : Grey));
                if (!ok && !string.IsNullOrEmpty(why)) GUI.Label(new Rect(r.xMax - 200, r.y + 28, 92, 26), why, St(13, Grey, TextAnchor.MiddleRight));
                Icon(new Rect(r.xMax - 64, r.y + 4, 30, 30), RangeIcon(s), Amber);
                GUI.Label(new Rect(r.xMax - 100, r.y + 32, 96, 22), RangeName(s), St(12, nameCol, TextAnchor.MiddleCenter));
                if (focusList && listIsThis && ok && GUI.Button(r, GUIContent.none, GUIStyle.none)) { listSel = idx; ChooseSkill(SkillRows()[idx]); }
                y += rowH + 6;
            }
            Header("武技", "cmd_craft_normal", "气势", active.Cp);
            for (int i = 0; i < crafts.Count; i++) Row(crafts[i].s, crafts[i].ok, crafts[i].why, "icon_cp", "气势", i, showCraftList);
            y += 6;
            Header("策略", "cmd_strategy_normal", "策力", active.Ep);
            if (strats.Count == 0) { GUI.Label(new Rect(panel.x + 20, y, panel.width - 40, 30), "没有点亮的策略", St(15, Grey)); }
            for (int i = 0; i < strats.Count; i++) Row(strats[i].s, strats[i].ok, strats[i].why, "icon_ep", "策力", i, !showCraftList);
        }

        static string ElemIcon(Element e) => e switch
        {
            Element.Fire => "icon_fire", Element.Water => "icon_water", Element.Wood => "icon_wood", Element.Metal => "icon_metal", _ => "icon_earth"
        };

        static string RangeIcon(SkillDef s) => s.Area == Area.Single ? "icon_range_single" : s.Area == Area.Line ? "icon_range_line" : s.Radius == 1 ? "icon_range_cross" : "icon_range_area";

        static string RangeName(SkillDef s) => s.Area switch
        {
            Area.Single => "单体", Area.Line => $"直线{s.Length}格", Area.SelfCircle => $"周围{s.Radius}格",
            _ => s.Radius >= 99 ? "全军" : s.Radius == 1 ? "十字一格" : $"半径{s.Radius}格"
        };

        // ---------------- 底部武将卡与操作提示 ----------------
        void DrawPartyV(float sw)
        {
            var party = units.Where(u => u.Team == Team.Player).ToList();
            float w = 300, gap = 10, y = RefH - 132, x0 = 14;
            foreach (var u in party)
            {
                bool act = u == active && mode != Mode.Deploy;
                var r = new Rect(x0, y, w, 120);
                Frame(r, act ? "party_card_active" : "party_card", act ? Cyan : (Color?)null);
                var b = HudPortrait(u);
                if (b) GUI.DrawTexture(new Rect(r.x + 6, r.y + 6, 96, r.height - 12), b, ScaleMode.ScaleAndCrop);
                float bx = r.x + 110;
                GUI.Label(new Rect(bx, r.y + 4, 120, 30), u.Name, St(21, Color.white, TextAnchor.MiddleLeft, true));
                GUI.Label(new Rect(bx + 62, r.y + 6, 60, 28), "Lv.03", St(13, Amber));
                if (act) { var tag = new Rect(r.xMax - 74, r.y + 8, 64, 24); Frame(tag, "list_item_selected", Cyan, 8); GUI.Label(tag, "行动中", St(14, Color.white, TextAnchor.MiddleCenter, true)); }
                void Stat(float dy, string label, int v, int max, Color c)
                {
                    GUI.Label(new Rect(bx, r.y + dy, 40, 22), label, St(14, new Color(0.85f, 0.92f, 1f)));
                    Rect(new Rect(bx + 40, r.y + dy + 7, 108, 9), new Color(0, 0, 0, 0.6f));
                    Rect(new Rect(bx + 41, r.y + dy + 8, 106 * Mathf.Clamp01(v / (float)Mathf.Max(1, max)), 7), c);
                    GUI.Label(new Rect(bx + 146, r.y + dy, 36, 22), v.ToString(), St(15, Color.white, TextAnchor.MiddleRight));
                }
                Stat(38, "兵力", u.Hp, u.MaxHp, new Color(0.35f, 0.85f, 0.65f));
                Stat(62, "策力", u.Ep, u.MaxEp, new Color(0.30f, 0.60f, 1f));
                Stat(86, "气势", u.Cp, 100, new Color(1f, 0.75f, 0.30f));
                if (!u.Alive) Rect(r, new Color(0, 0, 0, 0.6f));
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) FocusOn(u);
                x0 += w + gap;
            }
            // 操作提示
            var hint = new Rect(x0, y, sw - x0 - 14, 120);
            if (hint.width < 120) return;
            Frame(hint, "panel_frame");
            void Key(float dy, string k, string what)
            {
                var kr = new Rect(hint.x + 18, hint.y + dy, 52, 32);
                Frame(kr, "list_item_normal", CyanDim, 8);
                GUI.Label(kr, k, St(16, Color.white, TextAnchor.MiddleCenter, true));
                GUI.Label(new Rect(kr.xMax + 14, hint.y + dy, 120, 32), what, St(16, Color.white));
            }
            Key(8, "↑↓", "选择"); Key(44, "A", "确认"); Key(80, "B", "返回");
        }
    }
}
