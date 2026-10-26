using System.Linq;
using ThreeKingdoms.Core.Economy;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.Save;
using ThreeKingdoms.UI.Shop;
using UnityEngine;

namespace ThreeKingdoms.UI.Menu
{
    // 武将 (04_GENERALS_MENU) and 系统 (05_SYSTEM_MENU).
    public sealed partial class MainMenuHud
    {
        // ================= 武将 =================

        void DrawGeneral()
        {
            var p = paint;
            var g = General;
            if (g == null) return;
            int level = Progress.Level(g);
            var s = Progress.Stats(g, Inv.Bonus(g.Id));

            // The large illustration in its own frame, clipped to it.
            p.Frame(462, 158, 570, 712, "panel_frame");
            var clip = p.R(468, 164, 558, 700);
            GUI.BeginGroup(clip);
            if (Portraits && Portraits.TryGet(g.Id, out var art))
            {
                float h = 780 * p.K, w = h * art.Texture.width / art.Texture.height;
                float cx = clip.width * 0.56f;
                GUI.DrawTexture(new Rect(cx - w / 2, -10 * p.K, w, h), art.Texture, ScaleMode.StretchToFill);
            }
            GUI.EndGroup();
            p.Text(490, 170, 140, 70, g.DisplayName, 52, MenuPaint.Ivory, TextAnchor.MiddleLeft, true);
            p.Text(490 + g.DisplayName.Length * 52 + 18, 186, 160, 48, "/  " + MenuText.CourtesyName(g.Id), 28, MenuPaint.Ivory);
            p.HLine(490, 238, 200, SilverA(0.5f));
            p.Text(490, 242, 120, 44, $"Lv.{level}", 30, MenuPaint.Gold);

            // 武将详情.
            p.PanelBox(1038, 158, 604, 712, "武将详情");
            TroopMark(1514, 172, 34, g.Troop);
            p.Text(1556, 166, 80, 46, BattleTroop(g.Troop), 22, MenuPaint.Ivory);
            BarRow(1074, 228, "兵力", s.MaxTroops, s.MaxTroops, MenuPaint.Jade, 222, 22, " / ");
            BarRow(1074, 262, "技力", s.MaxTech, s.MaxTech, MenuPaint.Sky, 222, 22, " / ");

            p.SubBox(1052, 310, 576, 128, "基础能力");
            void Core(float cx, string icon, string name, int value)
            {
                if (!p.Icon(cx - 62, 362, 30, 30, icon)) p.Text(cx - 62, 360, 30, 34, "◆", 16, MenuPaint.Mist, TextAnchor.MiddleCenter);
                p.Text(cx - 24, 356, 90, 40, name, 21, MenuPaint.Ivory);
                p.Text(cx - 60, 392, 120, 44, value.ToString(), 32, MenuPaint.Ivory, TextAnchor.MiddleCenter);
            }
            Core(1176, "stat_force", "武力", s.Force);
            p.Fill(1266, 364, 1, 64, SilverA(0.35f));
            Core(1357, "stat_command", "统率", s.Command);
            p.Fill(1448, 364, 1, 64, SilverA(0.35f));
            Core(1540, "stat_intellect", "智力", s.Intellect);

            p.SubBox(1052, 450, 576, 168, "战斗属性");
            StatCell(1092, 500, "stat_patk", "物攻", s.PhysicalAttack, 1296);
            StatCell(1092, 542, "stat_pdef", "物防", s.PhysicalDefense, 1296);
            p.Fill(1330, 500, 1, 108, SilverA(0.35f));
            StatCell(1356, 500, "stat_satk", "计攻", s.StrategyAttack, 1590);
            StatCell(1356, 538, "stat_sdef", "计防", s.StrategyDefense, 1590);
            StatCell(1356, 576, "stat_speed", "速度", s.Speed, 1590);

            p.SubBox(1052, 630, 576, 110, "成长");
            int exp = Progress.Exp(g);
            var next = Progress.ExpForNextLevel(g);
            p.Text(1086, 672, 110, 34, "累计经验", 20, MenuPaint.Ivory);
            p.Bar(1196, 682, 286, 14, next is int n0 && n0 > 0 ? exp / (float)n0 : 1f, MenuPaint.Amber);
            p.Text(1490, 672, 110, 34, next is int n1 ? $"{exp} / {n1}" : $"{exp}（上限）", 20, MenuPaint.Ivory, TextAnchor.MiddleRight);
            p.Text(1086, 704, 110, 34, "距下一级", 20, MenuPaint.Ivory);
            p.Text(1490, 704, 110, 34, next is int n2 ? (n2 - exp).ToString() : "—", 20, MenuPaint.Ivory, TextAnchor.MiddleRight);

            p.SubBox(1052, 750, 576, 112, "装备");
            for (int i = 0; i < 3; i++)
            {
                var slot = (EquipSlot)i; var worn = Inv.Worn(g.Id, slot);
                float x = 1060 + i * 190;
                p.Frame(x, 788, 182, 66, "slot_row", SilverA(0.35f));
                GearPicture(x + 6, 791, 60, worn, slot);
                p.Text(x + 74, 790, 100, 30, ShopText.Slot(slot), 18, MenuPaint.Ivory);
                p.Text(x + 74, 818, 108, 32, worn != null ? worn.DisplayName : "未装备", 19, worn != null ? MenuPaint.Ivory : MenuPaint.Mist);
                int idx = i;
                if (GUI.Button(p.R(x, 788, 182, 66), GUIContent.none, GUIStyle.none)) { Show(Tab.Equip); slotIndex = idx; }
            }
        }

        // ================= 系统 =================

        // Left column rows: 背景音乐, 音效, 文字速度, 全屏显示, 恢复默认, 返回标题, 退出游戏. Right column: 3 manual + auto.
        int sysIndex, saveIndex;
        bool confirmLoad;
        public int SaveIndex => saveIndex;
        public bool ConfirmingLoad => confirmLoad;

        void MoveSystem(int dx, int dy)
        {
            if (confirmLoad) return;
            if (column == 2)
            {
                if (dy != 0) saveIndex = Wrap(saveIndex + dy, 4);
                if (dx < 0) column = 1;
                return;
            }
            if (dy != 0) { sysIndex = Wrap(sysIndex + dy, 7); return; }
            switch (sysIndex)
            {
                case 0: GameSettings.Music += dx * 10; break;
                case 1: GameSettings.Effects += dx * 10; break;
                case 2: GameSettings.TextSpeed += dx; break;
                case 3: GameSettings.FullScreen = !GameSettings.FullScreen; break;
                case 5: if (dx > 0) sysIndex = 6; break;
                case 6: if (dx > 0) column = 2; else sysIndex = 5; break;
                default: if (dx > 0) column = 2; break;
            }
        }

        void ConfirmSystem()
        {
            if (column == 2) { if (confirmLoad) LoadSelected(); else TryAskLoad(); return; }
            switch (sysIndex)
            {
                case 2: GameSettings.TextSpeed += 1; break;
                case 3: GameSettings.FullScreen = !GameSettings.FullScreen; break;
                case 4: GameSettings.Restore(); Message = "已恢复默认设置。"; break;
                case 5: Controller.ReturnToTitle(); break;
                case 6: Application.Quit(); break;
            }
        }

        string SlotName(int i) => i < 3 ? SaveSlots.Manual(i + 1) : SaveSlots.Auto;

        void TryAskLoad()
        {
            var status = Controller.Run.Slots().Read(SlotName(saveIndex), out _);
            if (status != SaveReadStatus.Ok) { Message = ThreeKingdoms.UI.Save.SaveText.Status(status); return; }
            confirmLoad = true;
        }

        void LoadSelected()
        {
            confirmLoad = false;
            var status = Controller.Run.Slots().Read(SlotName(saveIndex), out var data);
            if (status != SaveReadStatus.Ok) { Message = ThreeKingdoms.UI.Save.SaveText.Status(status); return; }
            if (!GameRun.TryLoad(Controller.Catalog, data, out string error)) Message = ThreeKingdoms.UI.Save.SaveText.LoadFailed(error);
        }

        void SettingFocus(float y, float h, int index)
        {
            if (column != 1 || sysIndex != index) return;
            paint.Fill(70, y, 588, h, new Color(MenuPaint.SelectBg.r, MenuPaint.SelectBg.g, MenuPaint.SelectBg.b, 0.55f));
            Pointer(52, y + h / 2 - 26);
        }

        void Slider(float y, int index, int value, System.Action<int> set)
        {
            var p = paint;
            SettingFocus(y - 22, 60, index);
            p.Text(82, y - 22, 140, 60, index == 0 ? "背景音乐" : "音效", 23, MenuPaint.Ivory);
            float x0 = 232, w = 338;
            var track = p.R(x0, y - 4, w, 14);
            if (!p.Icon(track, "slider_track", ScaleMode.StretchToFill)) { p.Fill(track, new Color(0.02f, 0.04f, 0.08f, 0.9f)); p.Outline(track, SilverA(0.4f), 1); }
            var fill = p.R(x0, y - 4, w * value / 100f, 14);
            if (!p.Icon(fill, "slider_fill", ScaleMode.StretchToFill)) p.Fill(fill, MenuPaint.Cyan);
            var knob = p.R(x0 + w * value / 100f - 18, y - 15, 36, 36);
            if (!p.Icon(knob, "slider_knob")) { p.Disc(knob.center, 13 * p.K, MenuPaint.Cyan); p.Disc(knob.center, 7 * p.K, MenuPaint.Ivory); }
            p.Text(586, y - 22, 54, 60, value.ToString(), 28, MenuPaint.Gold, TextAnchor.MiddleRight);
            var hit = p.R(x0 - 10, y - 22, w + 20, 44);
            var e = Event.current;
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && hit.Contains(e.mousePosition))
            {
                set(Mathf.RoundToInt(Mathf.Clamp01((e.mousePosition.x - track.x) / track.width) * 10) * 10);
                column = 1; sysIndex = index; e.Use();
            }
        }

        void DrawSystem()
        {
            var p = paint;
            p.PanelBox(30, 158, 668, 690, "设置");
            p.SubBox(58, 230, 610, 218, "声音", "icon_sound");
            Slider(330, 0, GameSettings.Music, v => GameSettings.Music = v);
            p.HLine(70, 366, 586, SilverA(0.18f));
            Slider(402, 1, GameSettings.Effects, v => GameSettings.Effects = v);

            p.SubBox(58, 464, 610, 214, "显示与操作", "icon_display");
            SettingFocus(538, 58, 2);
            p.Text(82, 538, 160, 58, "文字速度", 23, MenuPaint.Ivory);
            if (!p.Icon(376, 552, 28, 30, "icon_chevron_left")) p.Text(376, 538, 28, 58, "‹", 34, MenuPaint.Ivory, TextAnchor.MiddleCenter);
            p.Frame(424, 544, 168, 44, "subpanel", SilverA(0.45f), new Color(0.02f, 0.05f, 0.10f, 0.7f));
            p.Text(424, 544, 168, 44, GameSettings.TextSpeeds[GameSettings.TextSpeed], 23, MenuPaint.Ivory, TextAnchor.MiddleCenter);
            if (!p.Icon(612, 552, 28, 30, "icon_chevron_right")) p.Text(612, 538, 28, 58, "›", 34, MenuPaint.Ivory, TextAnchor.MiddleCenter);
            if (GUI.Button(p.R(370, 540, 44, 54), GUIContent.none, GUIStyle.none)) { GameSettings.TextSpeed -= 1; column = 1; sysIndex = 2; }
            if (GUI.Button(p.R(604, 540, 44, 54), GUIContent.none, GUIStyle.none)) { GameSettings.TextSpeed += 1; column = 1; sysIndex = 2; }
            p.HLine(70, 600, 586, SilverA(0.18f));
            SettingFocus(606, 60, 3);
            p.Text(82, 606, 160, 60, "全屏显示", 23, MenuPaint.Ivory);
            bool full = GameSettings.FullScreen;
            var tg = p.R(378, 616, 84, 40);
            if (!p.Icon(tg, full ? "toggle_on" : "toggle_off"))
            {
                p.Fill(tg, full ? new Color(0.10f, 0.45f, 0.55f) : new Color(0.25f, 0.28f, 0.32f)); p.Outline(tg, SilverA(0.6f), 1);
                p.Disc(p.P(full ? 440 : 400, 636), 15 * p.K, full ? MenuPaint.Cyan : MenuPaint.Grey);
            }
            p.Text(484, 606, 120, 60, full ? "开启" : "关闭", 23, MenuPaint.Ivory);
            if (GUI.Button(tg, GUIContent.none, GUIStyle.none)) { GameSettings.FullScreen = !full; column = 1; sysIndex = 3; }

            Button(198, 696, 326, 60, "恢复默认", ButtonKind.Normal, () => { GameSettings.Restore(); Message = "已恢复默认设置。"; }, focused: column == 1 && sysIndex == 4);
            Button(82, 774, 270, 58, "返回标题", ButtonKind.Normal, Controller.ReturnToTitle, "icon_home", 24, column == 1 && sysIndex == 5);
            Button(370, 774, 272, 58, "退出游戏", ButtonKind.Normal, Application.Quit, "icon_exit", 24, column == 1 && sysIndex == 6);

            // 存档管理.
            var slots = Controller.Run.Slots();
            p.PanelBox(718, 158, 924, 690, "存档管理", $"手动 <color=#D6BA78><size={p.F(26)}>{slots.ManualCount}</size></color> 槽 · 自动 <color=#D6BA78><size={p.F(26)}>1</size></color> 槽");
            for (int i = 0; i < 4; i++)
            {
                float y = 234 + i * 124;
                var status = slots.Read(SlotName(i), out var data);
                bool sel = column == 2 && saveIndex == i;
                Row(742, y, 876, 110, sel || (column != 2 && saveIndex == i));
                if (sel) Pointer(724, y + 28);
                bool ok = status == SaveReadStatus.Ok;
                string thumb = !ok ? "thumb_empty" : data.location == SaveData.TownLocation ? "thumb_tavern" : "thumb_southgate";
                var tr = p.R(770, y + 6, 210, 98);
                if (!p.Icon(tr, thumb, ScaleMode.ScaleAndCrop)) p.Fill(tr, ok ? new Color(0.20f, 0.16f, 0.12f) : new Color(0.16f, 0.17f, 0.19f));
                p.Outline(tr, SilverA(0.45f), 1);
                if (!p.Icon(1004, y + 34, 40, 42, i < 3 ? "icon_save" : "icon_autosave")) p.Text(1004, y + 34, 40, 42, i < 3 ? "▤" : "↻", 28, MenuPaint.Mist, TextAnchor.MiddleCenter);
                p.Text(1068, y + 14, 200, 46, i < 3 ? $"手动存档 {i + 1:00}" : "自动存档", 28, MenuPaint.Ivory, TextAnchor.MiddleLeft, true);
                p.Text(1068, y + 58, 200, 40, ok ? MenuText.SavePlace(data.location) : status == SaveReadStatus.Empty ? "空存档" : ThreeKingdoms.UI.Save.SaveText.Status(status).Split('，')[0], 21, ok ? MenuPaint.Ivory : MenuPaint.Mist);
                if (ok)
                {
                    var bust = Bust("liu_bei");
                    var br = p.R(1256, y + 8, 118, 94);
                    if (bust) GUI.DrawTexture(br, bust, ScaleMode.ScaleAndCrop);
                    p.Outline(br, SilverA(0.45f), 1);
                    p.Text(1406, y + 14, 90, 46, "刘备", 26, MenuPaint.Ivory, TextAnchor.MiddleLeft, true);
                    p.Text(1490, y + 14, 110, 46, $"Lv.{data.leaderLevel}", 24, MenuPaint.Gold);
                    if (!p.Icon(1408, y + 64, 28, 28, "icon_clock")) p.Text(1408, y + 60, 28, 34, "◷", 22, MenuPaint.Mist, TextAnchor.MiddleCenter);
                    p.Text(1446, y + 58, 170, 40, MenuText.SavedAt(data.savedAt), 21, MenuPaint.Ivory);
                }
                else
                {
                    p.Text(1406, y + 14, 160, 46, "—", 24, MenuPaint.Mist, TextAnchor.MiddleCenter);
                    p.HLine(1430, y + 55, 110, SilverA(0.2f));
                    p.Text(1406, y + 58, 160, 40, "—", 24, MenuPaint.Mist, TextAnchor.MiddleCenter);
                }
                int idx = i;
                if (GUI.Button(p.R(742, y, 876, 110), GUIContent.none, GUIStyle.none)) { column = 2; saveIndex = idx; }
            }
            p.HLine(742, 740, 876, SilverA(0.3f));
            if (!p.Icon(742, 756, 50, 62, "icon_lantern")) p.Text(742, 756, 50, 62, "🏮", 30, MenuPaint.Gold, TextAnchor.MiddleCenter);
            p.Text(808, 760, 300, 58, "手动存档需在酒肆掌柜处进行", 21, MenuPaint.Ivory);
            Button(1112, 756, 230, 66, "保存", ButtonKind.Disabled, icon: "icon_lock");
            bool loadable = slots.Read(SlotName(saveIndex), out _) == SaveReadStatus.Ok;
            Button(1356, 756, 264, 66, "读取所选存档", loadable ? ButtonKind.Primary : ButtonKind.Disabled, () => { column = 2; TryAskLoad(); }, size: 26);

            if (confirmLoad)
            {
                p.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, 0.45f));
                p.Frame(560, 360, 552, 230, "panel_frame");
                p.Text(560, 386, 552, 90, $"读取{(saveIndex < 3 ? $"手动存档 {saveIndex + 1:00}" : "自动存档")}？\n当前未存档的进度将会丢失。", 24, MenuPaint.Ivory, TextAnchor.MiddleCenter, false, true);
                Button(616, 498, 200, 60, "读取", ButtonKind.Primary, LoadSelected);
                Button(856, 498, 200, 60, "取消", ButtonKind.Normal, () => confirmLoad = false);
            }
        }
    }
}
