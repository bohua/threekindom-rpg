// 0.3 讨董篇的战棋扩展（design/v0.3-taodong/SYSTEMS_V03.md §3，修订见 REVIEW_V03.md）：
// 战中选择、分批入场（Spawn 贴着某单位落位、取消事件）、不可击败的首领（兵力下限）、刘备招牌技“众望”、合体技“三英”。
// 主线不开放原型的单挑（REVIEW_V03 R7–R8）；吕布沿用原型的守关行为。
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Prototype.BingshuTactics
{
    public sealed partial class TacticsGame
    {
        readonly List<string> chapterFlags = new List<string>();
        bool comboUsed;
        TacticsAction choice; int choiceSel; int? choicePicked;

        public IReadOnlyList<string> ChapterFlags => chapterFlags;

        // ---------------- 武技 ----------------

        void AddChapter3Crafts(Unit u, TacticsUnitDef d, Dictionary<string, SkillDef> crafts)
        {
            crafts["众望"] = Craft("众望", "替相邻友军承受下一次攻击，并提高其物防（随声望增强）2回合", Effect.Guard, Area.Single, 1, 0, 0f, 25, turns: 2);
            crafts["三英"] = Craft("三英", "合体技：关张与吕布相邻、刘备在两格内时，三人合击吕布（每场一次）", Effect.Damage, Area.Single, 2, 0, 1.2f, 0, might: true);
            if (d.Key == "LuBu")
            {
                var wushuang = Craft("天下无双", "方天画戟直劈：正前方2格物理1.3倍", Effect.Damage, Area.Line, 1, 0, 1.3f, 0, might: true);
                wushuang.Length = 2;
                u.Crafts.Add(wushuang);
            }
        }

        int FloorHp(Unit t) => t.Floor > 0f ? Mathf.CeilToInt(t.MaxHp * t.Floor) : 0;

        // 扣兵力的统一下限：不可击败的首领停在下限（REVIEW_V03 R8）
        int Floored(Unit t, int dmg)
        {
            int floor = FloorHp(t);
            return floor > 0 ? Mathf.Min(dmg, Mathf.Max(0, t.Hp - floor)) : dmg;
        }

        // “众望”：伤害计算之前换目标；一次攻击最多代受一次，代受后失效（REVIEW_V03 R10）
        Unit Cover(Unit a, Unit t, HashSet<Unit> covered = null)
        {
            var g = t.Guardian;
            if (g == null || g == t || !g.Alive || g.Team != t.Team || Grid.Dist(g.Pos, t.Pos) > 2) return t;
            if (covered != null && !covered.Add(g)) return t;
            t.Guardian = null;
            popups.Add((views[g].position + Vector3.up * (2.5f * HeadK), "众望·代受", new Color(0.55f, 0.9f, 1f), 0f));
            Log($"{g.Name} 挺身替 {t.Name} 挡下这一击！");
            return g;
        }

        void ApplyZhongwang(Unit liu, Unit t)
        {
            if (t == null || t == liu) return;
            float bonus = Mathf.Min(0.30f, 0.15f + 0.01f * Mathf.Max(0, Chapter?.Reputation ?? 0));
            t.Guardian = liu;
            t.AddStatus(new Status { Name = "众望", DefMul = 1f + bonus, Turns = 3 });   // 本回合结束会先减 1，计 2 回合
            popups.Add((views[t].position + Vector3.up * (2.2f * HeadK), "众望", new Color(0.55f, 0.9f, 1f), 0f));
            Log($"{liu.Name} 护住 {t.Name}（物防 +{bonus * 100f:0}%）");
        }

        // 三英：关羽、张飞与吕布相邻，刘备与吕布距离 ≤ 2（REVIEW_V03 R18）
        Unit ComboTarget()
        {
            if (comboUsed) return null;
            var lubu = units.FirstOrDefault(x => x.Alive && x.Key == "LuBu" && x.Team == Team.Enemy);
            if (lubu == null) return null;
            Unit B(string key) => units.FirstOrDefault(x => x.Alive && x.Key == key && x.Team == Team.Player);
            var liu = B("LiuBei"); var guan = B("GuanYu"); var zhang = B("ZhangFei");
            if (liu == null || guan == null || zhang == null) return null;
            if (Grid.Dist(guan.Pos, lubu.Pos) != 1 || Grid.Dist(zhang.Pos, lubu.Pos) != 1 || Grid.Dist(liu.Pos, lubu.Pos) > 2) return null;
            return lubu;
        }

        bool ComboReady(Unit u) => (u.Key == "LiuBei" || u.Key == "GuanYu" || u.Key == "ZhangFei") && ComboTarget() != null;

        IEnumerator SanYing(Unit u)
        {
            var lubu = ComboTarget();
            if (lubu == null) yield break;
            comboUsed = true;
            bannerText = "合体技 · 三英"; bannerT = 0f;
            yield return new WaitForSeconds(0.9f);
            yield return ShowLine("张飞", "大哥、二哥，一起上！");
            yield return ShowLine("关羽", "三弟，左右夹击！");
            yield return ShowLine("刘备", "吕布，休想走脱！");
            int total = 0;
            foreach (var key in new[] { "ZhangFei", "GuanYu", "LiuBei" })
            {
                var b = units.First(x => x.Alive && x.Key == key && x.Team == Team.Player);
                yield return Lunge(b, lubu.Pos);
                total += Combat.Damage(b, lubu, 1.2f, true, Element.None, HMul(b, lubu), InCamp(lubu));
            }
            Hit(u, lubu, total);
            lubu.AddStatus(new Status { Name = "三英", AtkMul = 0.85f, DefMul = 0.8f, Turns = 99 });
            lubu.At += 40;
            yield return ShowLine("吕布", "……三个打一个，算什么英雄！");
            Log("三英战吕布：吕布左支右绌，渐渐抵挡不住（物攻、物防下降）");
        }

        // ---------------- 战中事件 ----------------

        // Spawn 落位：Near 指名时贴着那名单位，否则用单位自己的格
        Vector2Int SpawnSpot(TacticsAction a, TacticsUnitDef d)
        {
            if (string.IsNullOrEmpty(a.Near)) return d.Pos;
            var near = units.FirstOrDefault(x => x.Alive && x.Name == a.Near);
            if (near == null) return d.Pos;
            // 离那名单位最近、且尽量靠我方一侧（南）的空格
            return grid.Diamond(near.Pos, 1).Where(p => p != near.Pos && grid.In(p) && !Grid.Impassable(grid.TerrainAt(p)) && At(p) == null)
                .OrderBy(p => p.y).DefaultIfEmpty(d.Pos).First();
        }

        IEnumerator RunAction3(TacticsAction a)
        {
            switch (a.Kind)
            {
                case TacticsActionKind.Choice:
                    choice = a; choiceSel = 0; choicePicked = null;
                    if (Chapter.AutoPlay || autoPlay) { yield return new WaitForSeconds(0.6f); choicePicked = 0; }
                    while (choicePicked == null) { ChoiceInput(); yield return null; }
                    int pick = choicePicked.Value;
                    choice = null; choicePicked = null;
                    yield return null;
                    if (!string.IsNullOrEmpty(a.Unit)) chapterFlags.Add(a.Unit + pick);
                    if (pick < a.Branches.Length) yield return RunActions(a.Branches[pick]);
                    break;
                case TacticsActionKind.Guard:
                    var liu = units.FirstOrDefault(x => x.Alive && x.Name == a.Speaker);
                    var t = units.FirstOrDefault(x => x.Alive && x.Name == a.Unit);
                    if (liu != null && t != null) ApplyZhongwang(liu, t);
                    yield return new WaitForSeconds(0.4f);
                    break;
                case TacticsActionKind.Cancel:
                    foreach (var e in Chapter.Events.Where(e => e.Id == a.Unit)) e.Fired = true;
                    break;
            }
        }

        void ChoiceInput()
        {
            if (choice == null) return;
            var kb = Keyboard.current;
            int n = choice.Options.Length;
            bool up = (kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)) || (Gamepad.current != null && Gamepad.current.dpad.up.wasPressedThisFrame);
            bool down = (kb != null && (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)) || (Gamepad.current != null && Gamepad.current.dpad.down.wasPressedThisFrame);
            if (up) choiceSel = (choiceSel + n - 1) % n;
            if (down) choiceSel = (choiceSel + 1) % n;
            if (ConfirmPressed) choicePicked = choiceSel;
        }

        void DrawChoice(float sw)
        {
            if (choice == null) return;
            var box = new Rect(sw / 2 - 360, RefH - 330, 720, 60 + 52 * choice.Options.Length);
            Rect(box, new Color(Ink.r, Ink.g, Ink.b, 0.95f));
            Rect(new Rect(box.x, box.y, box.width, 1.5f), Silver); Rect(new Rect(box.x, box.yMax - 1.5f, box.width, 1.5f), Silver);
            Rect(new Rect(box.x, box.y, 1.5f, box.height), Silver); Rect(new Rect(box.xMax - 1.5f, box.y, 1.5f, box.height), Silver);
            GUI.Label(new Rect(box.x + 22, box.y + 10, box.width - 44, 40), (string.IsNullOrEmpty(choice.Speaker) ? "" : choice.Speaker + "：") + choice.Text,
                new GUIStyle(St(19, Ivory, TextAnchor.MiddleLeft)) { wordWrap = true });
            for (int i = 0; i < choice.Options.Length; i++)
            {
                var r = new Rect(box.x + 30, box.y + 56 + 52 * i, box.width - 60, 44);
                bool sel = i == choiceSel;
                Rect(r, sel ? new Color(0.086f, 0.294f, 0.369f, 0.95f) : new Color(0.125f, 0.208f, 0.29f, 0.9f));
                if (sel) { Rect(new Rect(r.x, r.y, 3, r.height), Teal); GUI.Label(new Rect(r.x - 22, r.y, 20, r.height), "◆", St(14, Teal, TextAnchor.MiddleCenter)); }
                GUI.Label(new Rect(r.x + 16, r.y, r.width - 20, r.height), choice.Options[i], St(18, sel ? Ivory : new Color(0.71f, 0.78f, 0.84f), TextAnchor.MiddleLeft));
                if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition)) { choiceSel = i; choicePicked = i; }
            }
        }

        // 自动代打（测试、截图）时我方武将用合体技
        bool AutoCombo(Unit u, out IEnumerator act)
        {
            act = null;
            if (!IsChapter || u.Team != Team.Player || !ComboReady(u)) return false;
            act = SanYing(u);
            return true;
        }
    }
}
