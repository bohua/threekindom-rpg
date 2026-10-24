// PROTOTYPE - NOT FOR PRODUCTION
// Question: 空轨式 AT 行动条＋小范围格子战斗，配合“学派回路镶嵌兵书→策略”，是否好玩、是否看得懂？
// Date: 2026-10-23
// 单挑（用户 2026-10-23）：刘关张任一人行动结束站定时与吕布相邻、双方都在场，就在地图上触发一次单挑。
// 三回合，每回合出“攻击／架挡／绝招”；吕布出手前会露出破绽提示（七成准）。三兄弟都单挑过后触发“三英战吕布”。
// 对白参考《三国演义》第 5 回的情节自行撰写，“三英战吕布”为演义情节，非史实。
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Prototype.BingshuTactics
{
    public sealed partial class TacticsGame
    {
        // 由编辑器构建脚本填入：对白立绘
        public Texture2D LiuBeiPortrait, GuanYuPortrait, ZhangFeiPortrait, LuBuPortrait;
        // 单挑动作帧（Codex 交图后填入；没有时用待机帧加程序动作）
        public Texture2D LiuBeiDuel, GuanYuDuel, ZhangFeiDuel, LuBuDuel;

        readonly HashSet<string> dueled = new HashSet<string>();
        bool trioDone;

        sealed class DuelState
        {
            public Unit Hero, LuBu; public int HpH = DuelRules.Hp, HpL = DuelRules.Hp, Round;
            public string Speaker = "", Line = ""; public bool Waiting, Choosing; public int Choice;
            public DuelMove LastLuBu = DuelMove.Guard; public string Tell = "";
        }

        DuelState duel;
        Vector3? camFocus; float camZoom;

        static readonly Dictionary<string, (string who, string line)[]> Intro = new Dictionary<string, (string, string)[]>
        {
            ["张飞"] = new[] { ("张飞", "三姓家奴休走！燕人张飞在此！"), ("吕布", "环眼贼，也敢挡我去路？"), ("张飞", "少说废话，吃俺一矛！") },
            ["关羽"] = new[] { ("关羽", "吕布匹夫，关某来会你！"), ("吕布", "红面长髯……倒有几分架势。"), ("关羽", "看刀！") },
            ["刘备"] = new[] { ("刘备", "二弟、三弟，备来助阵！"), ("吕布", "织席贩履之辈，也配与我交手？"), ("刘备", "今日便叫你见识仁者之剑！") },
        };

        static readonly Dictionary<string, string> WinLine = new Dictionary<string, string>
        { ["张飞"] = "哈哈！吕布也不过如此！", ["关羽"] = "今日且饶你一命。", ["刘备"] = "承让了。" };
        static readonly Dictionary<string, string> LoseLine = new Dictionary<string, string>
        { ["张飞"] = "俺不服！改日再来！", ["关羽"] = "好厉害的戟法……", ["刘备"] = "果然名不虚传……" };

        static string TellOf(DuelMove m) => m switch
        {
            DuelMove.Attack => "吕布挺戟直进，戟锋直指咽喉！",
            DuelMove.Guard => "吕布横戟当胸，稳如山岳。",
            _ => "吕布双臂一沉，画戟嗡嗡作响——",
        };

        Texture2D Portrait(Unit u) => u.Key switch
        {
            "LiuBei" => LiuBeiPortrait, "GuanYu" => GuanYuPortrait, "ZhangFei" => ZhangFeiPortrait, "LuBu" => LuBuPortrait, _ => null
        };

        // 行动结束后检查：与吕布相邻、双方都在场、此人未单挑过
        IEnumerator MaybeDuel(Unit u)
        {
            if (stage != 1 || u.Team != Team.Player || !u.Alive || dueled.Contains(u.Name) || !Intro.ContainsKey(u.Name)) yield break;
            var lubu = units.FirstOrDefault(x => x.Name == "吕布");
            if (lubu == null || !lubu.Alive || Grid.Dist(u.Pos, lubu.Pos) != 1) yield break;
            yield return Duel(u, lubu);
        }

        IEnumerator Say(string who, string line)
        {
            duel.Speaker = who; duel.Line = line; duel.Waiting = true;
            float t0 = Time.time;
            while (duel.Waiting)
            {
                if (autoPlay && Time.time - t0 > 0.6f) duel.Waiting = false;
                yield return null;
            }
        }

        IEnumerator Duel(Unit hero, Unit lubu)
        {
            dueled.Add(hero.Name);
            var prevMode = mode;
            mode = Mode.Duel;
            duel = new DuelState { Hero = hero, LuBu = lubu };
            Face(hero, Tile(lubu.Pos) - Tile(hero.Pos)); Face(lubu, Tile(hero.Pos) - Tile(lubu.Pos));
            camFocus = (Tile(hero.Pos) + Tile(lubu.Pos)) / 2f;
            ClearDuelPose(hero); ClearDuelPose(lubu);
            Log($"单挑：{hero.Name} 对 吕布");
            yield return new WaitForSeconds(0.5f);
            foreach (var (who, line) in Intro[hero.Name]) yield return Say(who, line);

            for (duel.Round = 1; duel.Round <= DuelRules.Rounds && duel.HpH > 0 && duel.HpL > 0; duel.Round++)
            {
                // 吕布先定招，再露出破绽提示（七成准）；不连用两次绝招
                var lubuMove = (DuelMove)Random.Range(0, 3);
                if (lubuMove == DuelMove.Special && duel.LastLuBu == DuelMove.Special) lubuMove = DuelMove.Attack;
                var tell = Random.value < 0.7f ? lubuMove : (DuelMove)(((int)lubuMove + Random.Range(1, 3)) % 3);
                duel.Tell = TellOf(tell);
                duel.Speaker = ""; duel.Line = $"第 {duel.Round} 回合　{duel.Tell}";
                // 玩家出招
                duel.Choosing = true; duel.Choice = 0;
                float t0 = Time.time;
                while (duel.Choosing)
                {
                    if (autoPlay && Time.time - t0 > 0.5f)
                    {
                        var counter = tell == DuelMove.Attack ? DuelMove.Guard : tell == DuelMove.Guard ? DuelMove.Special : DuelMove.Attack;
                        duel.Choice = (int)counter; duel.Choosing = false;
                    }
                    yield return null;
                }
                var heroMove = (DuelMove)duel.Choice;
                if (heroMove == DuelMove.Special && hero.Cp < DuelRules.SpecialCp) heroMove = DuelMove.Attack;
                if (heroMove == DuelMove.Special) hero.Cp -= DuelRules.SpecialCp;
                duel.LastLuBu = lubuMove;

                var (toH, toL, key) = DuelRules.Resolve(heroMove, lubuMove, hero.Wu, lubu.Wu);
                yield return DuelExchange(hero, lubu, heroMove, lubuMove, toH, toL);
                duel.HpH = Mathf.Max(0, duel.HpH - toH); duel.HpL = Mathf.Max(0, duel.HpL - toL);
                string text = key switch
                {
                    "相持" => "两人对峙，谁也不敢先动。",
                    "互搏" => "兵刃相交，火星四溅！",
                    "对拼" => "两股劲力正面相撞，各退三步！",
                    "挡下" => $"{(DuelRules.Beats(heroMove, lubuMove) ? hero.Name : "吕布")}架住来势，反手一击！",
                    "破防" => $"{(DuelRules.Beats(heroMove, lubuMove) ? hero.Name : "吕布")}一招破开对方的架势！",
                    _ => $"{(DuelRules.Beats(heroMove, lubuMove) ? hero.Name : "吕布")}抢先出手，打断了对方的蓄势！",
                };
                yield return Say("", $"{hero.Name}{DuelRules.Name(heroMove)}，吕布{DuelRules.Name(lubuMove)}——{text}");
            }

            // 结算：体力低者落败，损兵四分之一（不致死）；吕布落败则动摇、行动推后
            // 胜者亮相
            var winner = duel.HpH > duel.HpL ? hero : duel.HpH < duel.HpL ? lubu : null;
            if (winner != null && rigs[winner].DuelTex) rigs[winner].DuelCol = 9;
            if (duel.HpH > duel.HpL)
            {
                yield return Say(hero.Name, WinLine[hero.Name]);
                yield return Say("吕布", "可恶……");
                lubu.Hp = Mathf.Max(1, lubu.Hp - lubu.MaxHp / 4);
                lubu.At += 40; lubu.AddStatus(new Status { Name = "动摇", AtkMul = 0.9f, Turns = 4 });
                hero.Cp = Mathf.Min(100, hero.Cp + 20);
                // 得胜：立即再行动一次，进入“昂扬”攻防 +50%，3 回合（本回合结束时的结算会先减 1，故记 4）（用户 2026-10-23）
                hero.AddStatus(new Status { Name = "昂扬", AtkMul = 1.5f, DefMul = 1.5f, Turns = 4 });
                hero.At = now - 0.01f;
                popups.Add((views[hero].position + Vector3.up * 2.4f, "昂扬！再行动", new Color(1f, 0.85f, 0.3f), 0f));
                yield return Say("", $"{hero.Name}士气昂扬，立即再次行动！（攻防 +50%，3 回合）");
                popups.Add((views[lubu].position + Vector3.up * 2.4f, "动摇", new Color(0.8f, 0.7f, 1f), 0f));
            }
            else if (duel.HpH < duel.HpL)
            {
                yield return Say("吕布", "哼，就这点本事？");
                yield return Say(hero.Name, LoseLine[hero.Name]);
                hero.Hp = Mathf.Max(1, hero.Hp - hero.MaxHp / 4);
                lubu.Cp = Mathf.Min(100, lubu.Cp + 20);
            }
            else yield return Say("吕布", "……有点意思。");

            if (!trioDone && new[] { "刘备", "关羽", "张飞" }.All(dueled.Contains))
            {
                trioDone = true;
                yield return Say("张飞", "大哥、二哥，一起上！");
                yield return Say("关羽", "三弟，左右夹击！");
                yield return Say("刘备", "吕布，休想走脱！");
                yield return Say("吕布", "……三个打一个，算什么英雄！");
                yield return Say("", "三英战吕布：吕布左支右绌，渐渐抵挡不住。（吕布物攻、物防下降）");
                lubu.AddStatus(new Status { Name = "三英", AtkMul = 0.85f, DefMul = 0.8f, Turns = 99 });
                lubu.At += 60;
            }

            camFocus = null; duel = null;
            rigs[hero].DuelCol = -1; rigs[lubu].DuelCol = -1;
            mode = prevMode == Mode.Duel ? Mode.Busy : prevMode;
        }

        // 一次交锋的动作：双方按出招踏步、架挡后撤、绝招先蓄力；伤害数字与闪白
        IEnumerator DuelExchange(Unit hero, Unit lubu, DuelMove hm, DuelMove lm, int toH, int toL)
        {
            SetDuelPose(hero, hm, 0); SetDuelPose(lubu, lm, 0);
            if (hm == DuelMove.Special || lm == DuelMove.Special)
            {
                if (hm == DuelMove.Special) popups.Add((views[hero].position + Vector3.up * 2.6f, "绝招！", new Color(1f, 0.85f, 0.3f), 0f));
                if (lm == DuelMove.Special) popups.Add((views[lubu].position + Vector3.up * 2.6f, "绝招！", new Color(1f, 0.4f, 0.3f), 0f));
                yield return new WaitForSeconds(0.35f);
            }
            SetDuelPose(hero, hm, 1); SetDuelPose(lubu, lm, 1);
            var a = StartCoroutine(hm == DuelMove.Guard ? Brace(hero, lubu) : Lunge(hero, lubu.Pos));
            yield return lm == DuelMove.Guard ? Brace(lubu, hero) : Lunge(lubu, hero.Pos);
            if (toL > 0) { HitFx(hero, lubu); popups.Add((views[lubu].position + Vector3.up * 1.9f, toL.ToString(), new Color(1f, 0.95f, 0.8f), 0f)); SetDuelPose(lubu, lm, 2); }
            if (toH > 0) { HitFx(lubu, hero); popups.Add((views[hero].position + Vector3.up * 1.9f, toH.ToString(), new Color(1f, 0.95f, 0.8f), 0f)); SetDuelPose(hero, hm, 2); }
            if (toH == 0 && toL == 0) popups.Add(((views[hero].position + views[lubu].position) / 2 + Vector3.up * 2.2f, "叮！", Color.white, 0f));
            yield return new WaitForSeconds(0.45f);
            ClearDuelPose(hero); ClearDuelPose(lubu);
        }

        // 架挡：身体压低后撤半步
        IEnumerator Brace(Unit u, Unit foe)
        {
            var rig = rigs[u];
            var dir = Tile(u.Pos) - Tile(foe.Pos); dir.y = 0; dir = dir.normalized * 0.18f;
            rig.StepHold = 0.35f;
            for (float t = 0; t < 0.15f; t += Time.deltaTime) { rig.Step = Vector3.Lerp(Vector3.zero, dir, t / 0.15f); yield return null; }
        }

        // 单挑姿势：phase 0 起手／蓄力，1 出招，2 受击
        void SetDuelPose(Unit u, DuelMove m, int phase)
        {
            if (!rigs.TryGetValue(u, out var rig) || rig.DuelTex == null) return;
            rig.DuelCol = phase == 2 ? 6 : m == DuelMove.Guard ? 5 : m == DuelMove.Special ? (phase == 0 ? 7 : 8) : (phase == 0 ? 2 : 3);
        }

        // 单挑进行中回到对峙架势（0/1 两帧交替），结束后恢复普通帧
        void ClearDuelPose(Unit u) { if (rigs.TryGetValue(u, out var rig)) rig.DuelCol = duel != null && rig.DuelTex ? (int)(Time.time * 3f) % 2 : -1; }

        // ---------------- 界面与输入 ----------------
        void DuelInput(Vector2Int pulse, bool ok)
        {
            if (duel == null) return;
            if (duel.Choosing)
            {
                if (pulse.x != 0) duel.Choice = (duel.Choice + pulse.x + 3) % 3;
                if (ok && (duel.Choice != (int)DuelMove.Special || duel.Hero.Cp >= DuelRules.SpecialCp)) duel.Choosing = false;
            }
            else if (duel.Waiting && ok) duel.Waiting = false;
        }

        void DrawDuel(float sw)
        {
            if (duel == null) return;
            var hero = duel.Hero;
            float py = RefH - 250;
            Rect(new Rect(0, py, sw, 250), new Color(0.03f, 0.03f, 0.05f, 0.88f));
            // 立绘：我方左、吕布右（水平翻转面向对手）
            var hp = Portrait(hero); var lp = Portrait(duel.LuBu);
            if (hp) GUI.DrawTexture(new Rect(10, py - 140, 260, 390), hp, ScaleMode.ScaleToFit);
            if (lp) GUI.DrawTextureWithTexCoords(new Rect(sw - 270, py - 140, 260, 390), lp, new Rect(1, 0, -1, 1));
            // 体力与回合
            float cx = sw / 2;
            GUI.Label(new Rect(cx - 200, py + 8, 400, 26), $"单挑　{hero.Name} VS 吕布　第 {Mathf.Clamp(duel.Round, 1, DuelRules.Rounds)}/{DuelRules.Rounds} 回合", new GUIStyle(big) { fontSize = 20, alignment = TextAnchor.MiddleCenter });
            GUI.Label(new Rect(290, py + 40, 80, 20), hero.Name, text);
            Bar(new Rect(350, py + 44, 240, 14), duel.HpH / (float)DuelRules.Hp, new Color(0.4f, 0.85f, 0.4f));
            GUI.Label(new Rect(sw - 360, py + 40, 80, 20), "吕布", text);
            Bar(new Rect(sw - 600, py + 44, 240, 14), duel.HpL / (float)DuelRules.Hp, new Color(0.95f, 0.35f, 0.3f));
            // 对白框
            var box = new Rect(290, py + 70, sw - 580, 90);
            Rect(box, new Color(0.10f, 0.09f, 0.08f, 0.95f));
            Rect(new Rect(box.x, box.y, box.width, 2), new Color(0.8f, 0.64f, 0.3f));
            if (!string.IsNullOrEmpty(duel.Speaker)) GUI.Label(new Rect(box.x + 12, box.y + 8, 200, 22), $"<color=#e8c56a><b>{duel.Speaker}</b></color>", text);
            GUI.Label(new Rect(box.x + 12, box.y + 34, box.width - 24, 50), duel.Line, new GUIStyle(text) { fontSize = 18, wordWrap = true });
            if (duel.Waiting && GUI.Button(box, GUIContent.none, GUIStyle.none)) duel.Waiting = false;
            if (duel.Waiting) GUI.Label(new Rect(box.xMax - 160, box.yMax - 22, 150, 20), "▼ 回车／A／点击继续", small);
            // 出招
            if (duel.Choosing)
            {
                for (int i = 0; i < 3; i++)
                {
                    var m = (DuelMove)i;
                    bool can = m != DuelMove.Special || hero.Cp >= DuelRules.SpecialCp;
                    var r = new Rect(cx - 225 + i * 155, py + 172, 140, 40);
                    if (i == duel.Choice) Rect(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), new Color(1f, 0.85f, 0.3f, 0.95f));
                    GUI.enabled = can;
                    string label = m == DuelMove.Special ? $"绝招（气势{DuelRules.SpecialCp}）" : DuelRules.Name(m);
                    if (GUI.Button(r, label)) { duel.Choice = i; duel.Choosing = false; }
                    GUI.enabled = true;
                }
                GUI.Label(new Rect(cx - 300, py + 220, 600, 20), "架挡克攻击，绝招破架挡，攻击打断绝招　·　←→ 选择，回车／A 出招", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
            }
        }
    }
}
