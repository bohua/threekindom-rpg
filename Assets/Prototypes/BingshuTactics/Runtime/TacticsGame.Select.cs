// PROTOTYPE - NOT FOR PRODUCTION
// 出阵选将（用户 2026-10-30：每场战斗有自己的上阵上限与必上人物，一场上 5–10 人很正常；界面按 Codex 设计图
// art/deploy-select-ui-concept-v2，苍蓝银辉统一风格，卡片带战场 Q 版，上阵标识用红色）。
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Prototype.BingshuTactics
{
    public sealed partial class TacticsGame
    {
        // 统一风格色（UI_STYLE_GUIDE 苍蓝银辉）；上阵标识按用户要求用红色
        static readonly Color SelBase = new Color(0.063f, 0.11f, 0.19f, 0.97f), SelPanel = new Color(0.125f, 0.208f, 0.29f, 0.96f),
            SelGroove = new Color(0.043f, 0.078f, 0.137f, 1f), SelMist = new Color(0.71f, 0.784f, 0.835f), SelSilver = new Color(0.55f, 0.655f, 0.73f),
            SelCyan = new Color(0.384f, 0.843f, 0.91f), SelSelBg = new Color(0.086f, 0.294f, 0.369f, 1f), SelGold = new Color(0.84f, 0.73f, 0.47f),
            SelRed = new Color(0.86f, 0.24f, 0.22f), SelJade = new Color(0.33f, 0.80f, 0.47f), SelSky = new Color(0.30f, 0.52f, 0.95f);

        readonly List<Unit> selectRoster = new List<Unit>();
        int selCursor, selTab;   // 页签：0 全部，1 步，2 骑，3 弓，4 军
        string selMessage;

        int DeployCap => Chapter != null && Chapter.MaxDeploy > 0 ? Chapter.MaxDeploy : selectRoster.Count;
        bool IsRequired(Unit u) => Chapter != null && Chapter.Required.Contains(u.Name);
        IEnumerable<Unit> Fielded => selectRoster.Where(u => !u.Benched);

        // 战前：可操作武将不少于两人时先进选将界面（电脑代打与测试跳过）
        bool EnterSelect()
        {
            selectRoster.Clear();
            selectRoster.AddRange(units.Where(u => u.Team == Team.Player && !u.Ai && !u.Benched));
            selectRoster.AddRange(reserveBench.Where(u => !selectRoster.Contains(u)));
            if (selectRoster.Count < 2) return false;
            selCursor = 0; selTab = 0; selMessage = null;
            mode = Mode.Select;
            return true;
        }

        // 页签 1–4 依次对应 步／骑／弓／军 四个大系
        List<Unit> SelectTabList() => selectRoster.Where(u => selTab == 0 || (int)TroopTraits.ClassOf(u.Troop) == selTab - 1).ToList();

        void ToggleDeploy(Unit u)
        {
            if (!u.Benched)
            {
                if (IsRequired(u)) { selMessage = $"{u.Name}为本场必上武将，无法撤下"; return; }
                if (Fielded.Count() <= 1) { selMessage = "至少要有一人上阵"; return; }
                Bench(u);
                selMessage = null;
            }
            else
            {
                if (Fielded.Count() >= DeployCap) { selMessage = $"已满 {DeployCap} 人，先撤下一人"; return; }
                Unbench(u);
                selMessage = null;
            }
            RefreshSwapCrafts();
        }

        // 从后备回到地图：放在布阵区内最近的空格
        void Unbench(Unit u)
        {
            reserveBench.Remove(u);
            u.Benched = false;
            var spots = new List<Vector2Int>();
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    var p = new Vector2Int(x, y);
                    if (InDeploy(p) && !Grid.Impassable(grid.TerrainAt(p)) && At(p) == null) spots.Add(p);
                }
            var anchor = Fielded.Where(f => f != u).Select(f => f.Pos).DefaultIfEmpty(new Vector2Int(W / 2, 1)).First();
            var at = spots.Count > 0 ? spots.OrderBy(p => Grid.Dist(p, anchor)).First() : NearestFree(anchor);
            u.Pos = at; views[u].position = Tile(at);
            if (rigs.TryGetValue(u, out var rig)) { rig.DeadT = 0f; rig.R.enabled = true; rig.Shadow.gameObject.SetActive(true); }
        }

        // 自动编成：必上者先上，其余按兵力与等级补满
        void AutoFill()
        {
            foreach (var u in selectRoster.Where(u => u.Benched && IsRequired(u)).ToList()) if (Fielded.Count() < DeployCap) Unbench(u);
            foreach (var u in selectRoster.Where(u => u.Benched).OrderByDescending(u => u.Level).ThenByDescending(u => u.MaxHp).ToList())
                if (Fielded.Count() < DeployCap) Unbench(u);
            RefreshSwapCrafts();
            selMessage = "已按等级与兵力自动编成";
        }

        string DeployBlocker()
        {
            var missing = selectRoster.Where(u => IsRequired(u) && u.Benched).Select(u => u.Name).ToList();
            if (missing.Count > 0) return "缺少必上武将：" + string.Join("、", missing);
            return Fielded.Any() ? null : "至少要有一人上阵";
        }

        void ConfirmSelect()
        {
            var block = DeployBlocker();
            if (block != null) { selMessage = block; return; }
            mode = Mode.Deploy;
            Log("布阵：点选我方武将，再点蓝色格子调整站位。可先打开「兵书回路」配书。");
        }

        void SelectInput(Vector2Int pulse, bool ok, bool cancel, bool lb, bool rb)
        {
            var list = SelectTabList();
            if (lb) { selTab = (selTab + 4) % 5; selCursor = 0; }
            if (rb) { selTab = (selTab + 1) % 5; selCursor = 0; }
            list = SelectTabList();
            if (list.Count == 0) return;
            if (pulse.x != 0) selCursor = Mathf.Clamp(selCursor + pulse.x, 0, list.Count - 1);
            if (pulse.y != 0) selCursor = Mathf.Clamp(selCursor - pulse.y * 3, 0, list.Count - 1);
            selCursor = Mathf.Clamp(selCursor, 0, list.Count - 1);
            if (ok) ToggleDeploy(list[selCursor]);
            if (Pressed(Kb?.fKey, Pad?.startButton)) ConfirmSelect();
            if (Pressed(Kb?.gKey, Pad?.buttonWest)) AutoFill();
        }

        // ---------- 绘制 ----------
        // ---------- 切图（Codex art/deploy-select-slices，1920×1080 基准；本界面按 720 高绘制，边距缩 2/3） ----------
        readonly Dictionary<string, Texture2D> selTex = new Dictionary<string, Texture2D>();
        Texture2D STex(string name)
        {
            if (!selTex.TryGetValue(name, out var t)) selTex[name] = t = Resources.Load<Texture2D>("DeploySelect/" + name);
            return t;
        }

        // 九宫格：四角按原尺寸的 2/3 绘制，边与中心拉伸
        bool Nine(Rect r, string name, int border, float alpha = 1f)
        {
            var t = STex(name);
            if (!t) return false;
            float b = border * (RefH / 1080f), bu = border / (float)t.width, bv = border / (float)t.height;
            b = Mathf.Min(b, r.width / 2f, r.height / 2f);
            var o = GUI.color; GUI.color = new Color(1, 1, 1, alpha);
            float[] xs = { r.x, r.x + b, r.xMax - b, r.xMax }, ys = { r.y, r.y + b, r.yMax - b, r.yMax };
            float[] us = { 0, bu, 1 - bu, 1 }, vs = { 1, 1 - bv, bv, 0 };
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    var dst = new Rect(xs[i], ys[j], xs[i + 1] - xs[i], ys[j + 1] - ys[j]);
                    if (dst.width <= 0 || dst.height <= 0) continue;
                    GUI.DrawTextureWithTexCoords(dst, t, new Rect(us[i], vs[j + 1], us[i + 1] - us[i], vs[j] - vs[j + 1]));
                }
            GUI.color = o;
            return true;
        }

        bool Img(Rect r, string name, float alpha = 1f)
        {
            var t = STex(name);
            if (!t) return false;
            var o = GUI.color; GUI.color = new Color(alpha, alpha, alpha, o.a);
            GUI.DrawTexture(r, t, ScaleMode.StretchToFill);
            GUI.color = o;
            return true;
        }

        void SkinBar(Rect r, float v, string fill)
        {
            if (!Img(r, "ui/bar_track")) Rect(r, SelGroove);
            var f = new Rect(r.x, r.y, r.width * Mathf.Clamp01(v), r.height);
            var t = STex(fill);
            if (t) GUI.DrawTextureWithTexCoords(f, t, new Rect(0, 0, Mathf.Clamp01(v), 1));
        }

        static readonly Dictionary<string, string> BustKeys = new Dictionary<string, string>
        {
            ["刘备"] = "LIU_BEI", ["关羽"] = "GUAN_YU", ["张飞"] = "ZHANG_FEI", ["赵云"] = "ZHAO_YUN", ["太史慈"] = "TAISHI_CI",
            ["简雍"] = "JIAN_YONG", ["孙乾"] = "SUN_QIAN", ["糜竺"] = "MI_ZHU", ["糜芳"] = "MI_FANG",
        };

        // 统一构图的卡片头像（双眼在 32% 高处）；按目标框比例裁两侧
        bool CardBust(Rect r, Unit u, float shade)
        {
            if (!BustKeys.TryGetValue(u.Name, out var key)) return false;
            var t = STex("busts/" + key + "_CARD");
            if (!t) return false;
            float aspect = r.width / r.height, src = t.width / (float)t.height;
            var uv = aspect < src ? new Rect((1 - aspect / src) / 2f, 0, aspect / src, 1) : new Rect(0, 0, 1, 1);
            var o = GUI.color; GUI.color = new Color(shade, shade, shade, 1f);
            GUI.DrawTextureWithTexCoords(r, t, uv);
            GUI.color = o;
            return true;
        }

        void SelBox(Rect r, Color fill, Color edge, float w = 1.2f)
        {
            Rect(r, fill);
            Rect(new Rect(r.x, r.y, r.width, w), edge); Rect(new Rect(r.x, r.yMax - w, r.width, w), edge);
            Rect(new Rect(r.x, r.y, w, r.height), edge); Rect(new Rect(r.xMax - w, r.y, w, r.height), edge);
        }

        // 战场 Q 版的正面待机一帧（与地图上同一张图）
        void DrawQ(Rect r, Unit u, float alpha = 1f)
        {
            if (!quads.TryGetValue(u, out var q) || !q.sharedMaterial) return;
            var tex = q.sharedMaterial.GetTexture("_BaseMap") ?? q.sharedMaterial.mainTexture;
            if (!tex || !rigs.TryGetValue(u, out var rig)) return;
            Rect uv = rig.Kind == SheetKind.Eight ? new Rect(0f, 1f - 1f / rig.Rows, 1f / rig.Cols, 1f / rig.Rows)
                : rig.Kind == SheetKind.Legacy25 ? new Rect(10f / rig.Cols, 0f, 1f / rig.Cols, 1f) : new Rect(0f, 0f, 1f / rig.Cols, 1f);
            var o = GUI.color; GUI.color = new Color(1, 1, 1, alpha);
            float side = Mathf.Min(r.width, r.height);
            GUI.DrawTextureWithTexCoords(new Rect(r.center.x - side / 2, r.yMax - side, side, side), tex, uv);
            GUI.color = o;
        }

        void DrawBust(Rect r, Unit u, float alpha = 1f)
        {
            if (Chapter == null || !Chapter.Portraits.TryGetValue(u.Name, out var pt) || !pt.Texture)
            {
                Rect(r, new Color(0.09f, 0.14f, 0.22f, 1f));
                GUI.Label(r, u.Name.Substring(0, 1), St(30, SelMist, TextAnchor.MiddleCenter, true));
                return;
            }
            float crop = pt.Crop > 0 ? pt.Crop * 0.62f : 0.38f, axis = pt.Axis > 0 ? pt.Axis : 0.5f;
            float uh = crop, uw = Mathf.Min(1f, crop * pt.Texture.height / (float)pt.Texture.width * r.width / r.height);
            float ux = Mathf.Clamp(axis - uw / 2f, 0f, 1f - uw);
            var uv = pt.Flip ? new Rect(ux + uw, 1f - uh, -uw, uh) : new Rect(ux, 1f - uh, uw, uh);
            var o = GUI.color; GUI.color = new Color(alpha, alpha, alpha, 1f);
            GUI.DrawTextureWithTexCoords(r, pt.Texture, uv);
            GUI.color = o;
        }

        void DrawSelect(float sw)
        {
            Rect(new Rect(0, 0, sw, RefH), SelBase);
            if (STex("ui/bg_deploy")) GUI.DrawTexture(new Rect(0, 0, sw, RefH), STex("ui/bg_deploy"), ScaleMode.ScaleAndCrop);
            Img(new Rect(0, 82, sw, 4), "ui/header_rule");
            // 页眉
            GUI.Label(new Rect(24, 12, 220, 40), "出阵选将", St(30, Ivory, TextAnchor.MiddleLeft, true));
            GUI.Label(new Rect(26, 50, 260, 20), "战前整备 / 选择武将 → 调整布阵", St(13, SelMist, TextAnchor.MiddleLeft));
            Rect(new Rect(250, 16, 1, 52), SelSilver);
            GUI.Label(new Rect(268, 10, 600, 32), Chapter?.Title ?? "", St(24, Ivory, TextAnchor.MiddleLeft, true));
            GUI.Label(new Rect(268, 40, 600, 20), Chapter?.Where ?? "", St(15, SelMist, TextAnchor.MiddleLeft));
            GUI.Label(new Rect(268, 58, 700, 20), "◆ 胜利条件：" + (Chapter?.Objective ?? ""), St(14, Ivory, TextAnchor.MiddleLeft));
            var cnt = new Rect(sw - 270, 10, 250, 64);
            if (!Img(cnt, "ui/count_plate")) SelBox(cnt, SelPanel, SelSilver);
            GUI.Label(new Rect(cnt.x + 16, cnt.y + 6, 80, 30), "上阵", St(20, Ivory, TextAnchor.MiddleLeft, true));
            GUI.Label(new Rect(cnt.x + 100, cnt.y + 2, 134, 40), $"{Fielded.Count()} / {DeployCap}", St(32, SelGold, TextAnchor.MiddleRight, true));
            int req = selectRoster.Count(IsRequired), reqIn = selectRoster.Count(u => IsRequired(u) && !u.Benched);
            GUI.Label(new Rect(cnt.x + 16, cnt.y + 40, 220, 20), req > 0 ? $"必上 {reqIn} / {req}" : "本场无必上武将", St(13, SelMist, TextAnchor.MiddleLeft));

            // 左：可用武将
            var left = new Rect(20, 86, sw * 0.68f - 30, RefH - 86 - 70);
            if (!Nine(left, "ui/panel_frame_9slice", 48)) SelBox(left, SelPanel, SelSilver);
            GUI.Label(new Rect(left.x + 14, left.y + 6, 200, 28), "可用武将", St(20, Ivory, TextAnchor.MiddleLeft, true));
            GUI.Label(new Rect(left.xMax - 300, left.y + 6, 286, 28), $"可上阵 {DeployCap} 人 · 后备 {selectRoster.Count(u => u.Benched)} 人", St(14, SelMist, TextAnchor.MiddleRight));
            string[] tabs = { "全部", "步", "骑", "弓", "军师" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int n = i == 0 ? selectRoster.Count : selectRoster.Count(u => (int)TroopTraits.ClassOf(u.Troop) == i - 1);
                var tr = new Rect(left.x + 14 + i * 92, left.y + 38, 86, 26);
                bool on = i == selTab;
                if (!Nine(tr, on ? "ui/tab_selected" : "ui/tab", 20)) SelBox(tr, on ? SelSelBg : SelGroove, on ? SelCyan : SelSilver * 0.8f);
                if (GUI.Button(tr, GUIContent.none, GUIStyle.none)) { selTab = i; selCursor = 0; }
                GUI.Label(tr, $"{tabs[i]} {n}", St(15, on ? Ivory : SelMist, TextAnchor.MiddleCenter));
            }
            var list = SelectTabList();
            float cw = (left.width - 14 * 2 - 10 * 2) / 3f, ch = 118f;
            for (int i = 0; i < list.Count; i++)
            {
                var u = list[i];
                var r = new Rect(left.x + 14 + (i % 3) * (cw + 10), left.y + 74 + (i / 3) * (ch + 10), cw, ch);
                if (r.yMax > left.yMax - 22) break;
                DrawSelectCard(r, u, i == selCursor && padMode);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { selCursor = i; ToggleDeploy(u); }
            }
            GUI.Label(new Rect(left.x + 14, left.yMax - 22, left.width - 28, 18),
                "<color=#dc3d38>■</color> 已上阵　 🔒 必上不可撤下　 ◆ 当前光标　　未上阵者留作后备，战中可替换", St(12, SelMist, TextAnchor.MiddleLeft));

            // 右：布阵预览、敌情、出场顺序
            float rx = left.xMax + 12, rw = sw - 20 - rx;
            DrawSelectMap(new Rect(rx, 86, rw, 220));
            DrawSelectEnemy(new Rect(rx, 314, rw, 150));
            DrawSelectOrder(new Rect(rx, 472, rw, left.yMax - 472));

            // 底栏
            var bar = new Rect(0, RefH - 62, sw, 62);
            Rect(bar, SelGroove);
            Rect(new Rect(0, bar.y, sw, 1), SelSilver);
            var block = DeployBlocker();
            string state = selMessage ?? block ?? $"准备就绪　{(Chapter != null && Chapter.MaxDeploy > 0 ? $"最多 {DeployCap} 人出阵" : "全员可出阵")}" +
                (req > 0 ? $" · {string.Join("、", selectRoster.Where(IsRequired).Select(u => u.Name))}必须参战" : "") + " · 后备可在战中替换";
            GUI.Label(new Rect(24, bar.y + 4, sw - 520, 26), state, St(16, selMessage != null || block != null ? SelGold : Ivory, TextAnchor.MiddleLeft, true));
            GUI.Label(new Rect(24, bar.y + 32, sw - 520, 22), "A/Enter 上阵/下阵　LB/RB·Q/E 切换页签　X/G 自动编成　Start/F 出阵　方向键 选择", St(13, SelMist, TextAnchor.MiddleLeft));
            var auto = new Rect(sw - 420, bar.y + 10, 180, 42);
            if (!Nine(auto, "ui/button", 20)) SelBox(auto, SelPanel, SelSilver);
            GUI.Label(auto, "自动编成", St(19, Ivory, TextAnchor.MiddleCenter, true));
            if (GUI.Button(auto, GUIContent.none, GUIStyle.none)) AutoFill();
            var go = new Rect(sw - 228, bar.y + 10, 208, 42);
            if (!Nine(go, block == null ? "ui/button_primary" : "ui/button_disabled", 20)) SelBox(go, block == null ? SelSelBg : SelGroove, block == null ? SelCyan : SelSilver * 0.6f, 1.6f);
            GUI.Label(go, "出阵 ›", St(21, block == null ? Ivory : SelMist, TextAnchor.MiddleCenter, true));
            if (GUI.Button(go, GUIContent.none, GUIStyle.none)) ConfirmSelect();
        }

        void DrawSelectCard(Rect r, Unit u, bool cursor)
        {
            bool on = !u.Benched, req = IsRequired(u);
            bool skinned = Nine(r, cursor ? "ui/card_cursor_9slice" : on ? "ui/card_deployed_9slice" : "ui/card_normal_9slice", 40);
            if (!skinned) SelBox(r, on ? SelSelBg : SelGroove, cursor ? SelCyan : on ? SelRed : SelSilver * 0.7f, cursor || on ? 2f : 1.2f);
            var bust = new Rect(r.x + 3, r.y + 3, Mathf.Round(r.height * 0.68f), r.height - 6);
            if (!CardBust(bust, u, 1f)) DrawBust(bust, u);
            // 上阵：红色“阵”旗（用户 2026-10-30）；必上：锁签
            if (on)
            {
                var flag = new Rect(bust.x + 3, bust.y + 2, 22, 25);
                if (!Img(flag, "ui/mark_deployed")) { Rect(flag, SelRed); GUI.Label(flag, "阵", St(14, Ivory, TextAnchor.MiddleCenter, true)); }
            }
            if (req)
            {
                var lk = new Rect(bust.x + 3, bust.yMax - 20, bust.width - 6, 17);
                if (!Img(lk, "ui/mark_required_lock")) { Rect(lk, new Color(0.04f, 0.06f, 0.1f, 0.82f)); GUI.Label(lk, "🔒 必上", St(13, SelGold, TextAnchor.MiddleCenter, true)); }
            }
            float tx = bust.xMax + 8, qs = Mathf.Min(r.height - 20, 70f);
            var stand = new Rect(r.xMax - qs - 8, r.y + 8, qs, r.height - 16);
            Rect(stand, new Color(0.10f, 0.17f, 0.25f, 0.85f));
            Img(new Rect(stand.x, stand.yMax - stand.width * 1.25f * 0.42f, stand.width, stand.width * 1.25f * 0.42f), "ui/q_stand");
            DrawQ(new Rect(stand.x - 6, stand.y - 4, stand.width + 12, stand.height - 2), u);
            float tw = stand.x - tx - 6;
            GUI.Label(new Rect(tx, r.y + 6, tw - 20, 26), u.Name, St(u.Name.Length > 2 ? 17 : 20, Ivory, TextAnchor.MiddleLeft, true));   // 三字名缩一号，不压五行珠
            GUI.Label(new Rect(tx, r.y + 32, tw, 20), $"Lv.{u.Level}　{TroopTraits.Name(u.Troop, u.Rank)}", St(13, SelMist, TextAnchor.MiddleLeft));
            // 五行小徽：色珠里写五行字（同设计图）
            var ec = new Vector2(tx + tw - 9, r.y + 19);
            Disc(ec, 9f, new Color(0.85f, 0.88f, 0.9f));
            Disc(ec, 7.5f, Wuxing.Tint(u.Element));
            GUI.Label(new Rect(ec.x - 9, ec.y - 9, 18, 18), Wuxing.Name(u.Element), St(11, u.Element == Element.Metal ? new Color(0.15f, 0.2f, 0.3f) : Ivory, TextAnchor.MiddleCenter, true));
            GUI.Label(new Rect(tx, r.y + 60, 40, 18), "兵力", St(12, SelMist, TextAnchor.MiddleLeft));
            GUI.Label(new Rect(tx, r.y + 60, tw, 18), u.MaxHp.ToString(), St(12, Ivory, TextAnchor.MiddleRight));
            SkinBar(new Rect(tx, r.y + 78, tw, 5), 1f, "ui/bar_troops");
            GUI.Label(new Rect(tx, r.y + 84, 40, 18), "策力", St(12, SelMist, TextAnchor.MiddleLeft));
            GUI.Label(new Rect(tx, r.y + 84, tw, 18), u.MaxEp.ToString(), St(12, Ivory, TextAnchor.MiddleRight));
            SkinBar(new Rect(tx, r.y + 102, tw, 5), 1f, "ui/bar_strategy");
            if (cursor)
            {
                if (!Img(new Rect(r.x - 14, r.center.y - 8, 16, 16), "ui/cursor_diamond")) GUI.Label(new Rect(r.x - 12, r.center.y - 9, 14, 18), "◆", St(14, SelCyan, TextAnchor.MiddleCenter));
            }
        }

        void DrawSelectMap(Rect r)
        {
            if (!Nine(r, "ui/panel_frame_9slice", 48)) SelBox(r, SelPanel, SelSilver);
            GUI.Label(new Rect(r.x + 12, r.y + 4, 200, 26), "本场布阵预览", St(18, Ivory, TextAnchor.MiddleLeft, true));
            GUI.Label(new Rect(r.xMax - 212, r.y + 4, 200, 26), Chapter?.Where ?? "", St(13, SelMist, TextAnchor.MiddleRight));
            var area = new Rect(r.x + 12, r.y + 34, r.width - 24, r.height - 46);
            float cell = Mathf.Min(area.width / W, area.height / H);
            var o = new Vector2(area.x + (area.width - cell * W) / 2f, area.y + (area.height - cell * H) / 2f);
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    var p = new Vector2Int(x, y);
                    var c = Grid.Impassable(grid.TerrainAt(p)) ? new Color(0.05f, 0.08f, 0.12f) : InDeploy(p) ? new Color(0.12f, 0.36f, 0.45f) : new Color(0.10f, 0.17f, 0.24f);
                    Rect(new Rect(o.x + x * cell + 0.5f, o.y + (H - 1 - y) * cell + 0.5f, cell - 1f, cell - 1f), c);
                }
            Img(new Rect(o.x - 8, o.y - 8, cell * W + 16, cell * H + 16), "ui/minimap_frame");
            foreach (var u in units.Where(u => u.Alive))
            {
                var c = new Vector2(o.x + (u.Pos.x + 0.5f) * cell, o.y + (H - u.Pos.y - 0.5f) * cell);
                if (u.Team == Team.Player && !u.Ai) DrawQ(new Rect(c.x - cell * 0.8f, c.y - cell * 1.1f, cell * 1.6f, cell * 1.6f), u);
                else Disc(c, cell * 0.28f, u.Team == Team.Player ? SelCyan : SelRed);
            }
        }

        void DrawSelectEnemy(Rect r)
        {
            if (!Nine(r, "ui/panel_frame_9slice", 48)) SelBox(r, SelPanel, SelSilver);
            var foes = units.Where(u => u.Alive && u.Team == Team.Enemy).ToList();
            GUI.Label(new Rect(r.x + 12, r.y + 4, 200, 26), "敌情摘要", St(18, Ivory, TextAnchor.MiddleLeft, true));
            GUI.Label(new Rect(r.xMax - 152, r.y + 4, 140, 26), $"敌方 {foes.Count} 人", St(16, Ivory, TextAnchor.MiddleRight, true));
            string[] cls = { "步", "骑", "弓", "军" };
            var parts = cls.Select((n, i) => (n, c: foes.Count(f => (int)TroopTraits.ClassOf(f.Troop) == i))).Where(x => x.c > 0).Select(x => $"{x.n} {x.c}");
            GUI.Label(new Rect(r.x + 14, r.y + 34, r.width - 120, 24), string.Join("　", parts), St(16, Ivory, TextAnchor.MiddleLeft));
            var els = foes.Select(f => f.Element).Where(e => e != Element.None).Distinct().ToList();
            for (int i = 0; i < els.Count; i++) Disc(new Vector2(r.xMax - 20 - i * 22, r.y + 46), 8f, Wuxing.Tint(els[i]));
            // “克”：我方已上阵者能克敌方五行的伤害招式
            var counters = new List<string>();
            foreach (var u in Fielded)
                foreach (var s in StrategiesOf(u).Select(l => l.Def).Concat(u.Crafts).Where(s => s.Effect == Effect.Damage && s.Element != Element.None))
                    if (els.Contains(Wuxing.Overcomes(s.Element))) { counters.Add($"{u.Name}·{s.Name} 克{Wuxing.Name(Wuxing.Overcomes(s.Element))}"); break; }
            var kr = new Rect(r.x + 12, r.y + 66, 30, 30);
            if (!Img(kr, "ui/counter_badge")) { SelBox(kr, SelSelBg, SelCyan); GUI.Label(kr, "克", St(17, Ivory, TextAnchor.MiddleCenter, true)); }
            GUI.Label(new Rect(kr.xMax + 8, r.y + 62, r.width - 60, r.height - 70),
                counters.Count > 0 ? string.Join(" / ", counters.Take(4)) : "已上阵者暂无能克制敌方五行的招式",
                new GUIStyle(St(14, Ivory, TextAnchor.UpperLeft)) { wordWrap = true });
        }

        void DrawSelectOrder(Rect r)
        {
            if (!Nine(r, "ui/panel_frame_9slice", 48)) SelBox(r, SelPanel, SelSilver);
            GUI.Label(new Rect(r.x + 12, r.y + 4, 220, 26), "已上阵 · 出场顺序", St(18, Ivory, TextAnchor.MiddleLeft, true));
            var on = Fielded.ToList();
            int slots = Mathf.Max(on.Count, Mathf.Min(DeployCap, 10));
            float s = Mathf.Min(58f, (r.width - 24 - (slots - 1) * 6) / Mathf.Max(1, slots));
            for (int i = 0; i < slots; i++)
            {
                var b = new Rect(r.x + 12 + i * (s + 6), r.y + 38, s, s);
                if (!Img(b, i < on.Count ? "ui/order_slot_filled" : "ui/order_slot")) SelBox(b, SelGroove, i < on.Count ? SelRed : SelSilver * 0.6f);
                if (i < on.Count)
                {
                    DrawQ(new Rect(b.x - 4, b.y - 6, b.width + 8, b.height + 4), on[i]);
                    GUI.Label(new Rect(b.x + 2, b.yMax - 16, 16, 16), (i + 1).ToString(), St(12, Ivory, TextAnchor.MiddleLeft, true));
                }
                else if (!STex("ui/order_slot")) GUI.Label(b, "+", St(22, SelMist, TextAnchor.MiddleCenter));
            }
            var mix = on.GroupBy(u => TroopTraits.ClassOf(u.Troop)).Select(g => $"{(g.Key == TroopClass.Foot ? "步" : g.Key == TroopClass.Horse ? "骑" : g.Key == TroopClass.Bow ? "弓" : "军")} {g.Count()}");
            GUI.Label(new Rect(r.x + 12, r.y + 44 + s, r.width - 24, 22), "兵种分布：" + string.Join("　", mix), St(14, SelMist, TextAnchor.MiddleLeft));
        }
    }
}
