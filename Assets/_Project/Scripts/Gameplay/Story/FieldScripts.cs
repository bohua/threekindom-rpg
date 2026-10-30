using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Save;
using static ThreeKingdoms.Gameplay.Story.S;

namespace ThreeKingdoms.Gameplay.Story
{
    // Who says and does what in Lu Zhi's camp and Anxi (V02_HUANGJIN_PLAN §5–§7). Positions are world X / Z of the scenes
    // the ChapterFieldBuilder makes (camp: gate z −20, command tent z 12; Anxi: south gate z −25, yamen z 18, post station
    // at x 22, east gate x 35). Text keys are in ChapterText.
    public static class FieldScripts
    {
        // ---------- arrival ----------

        public static IEnumerable<StageStep> OnArrive(FieldStoryController c)
        {
            if (FieldScriptsR1.Mine(c)) return FieldScriptsR1.OnArrive(c);   // the R1 map's side quests (npc-sidequests)
            var main = OnArriveMain(c);
            var side = SideQuests.OnArrive(c).ToList();
            return side.Count == 0 ? main : (main ?? Enumerable.Empty<StageStep>()).Concat(side);
        }

        static IEnumerable<StageStep> OnArriveMain(FieldStoryController c)
        {
            if (FieldScripts5.Mine(c)) return FieldScripts5.OnArrive(c);   // 0.5
            if (FieldScripts4.Mine(c)) return FieldScripts4.OnArrive(c);
            if (FieldScripts3.Mine(c)) return FieldScripts3.OnArrive(c);   // 0.3
            var ch = c.Run.Chapter2;
            if (c.Location == FieldStoryController.Place.Camp && ch.Step == ChapterStep.Camp)
                return ch.Has(ChapterScripts.CampGreeted) ? new[] { Autosave(SaveData.CampLocation) } : CampArrival();
            if (c.Location == FieldStoryController.Place.Anxi && (ch.Step == ChapterStep.Anxi || ch.Step == ChapterStep.AnxiBandits))
                return ch.Step == ChapterStep.Anxi && !ch.Has(ChapterScripts.AnxiGreeted) ? AnxiArrival() : new[] { Autosave(SaveData.AnxiLocation) };
            return null;
        }

        // C2 拜见卢植: the brothers walk up the main road to the command tent, where Lu Zhi stands at the door.
        static IEnumerable<StageStep> CampArrival()
        {
            yield return Autosave(SaveData.CampLocation);
            yield return Move("liu_bei", 0f, 7.6f, 2.6f);
            yield return Show("guan_yu", -1.6f, 7f, "back");
            yield return Show("zhang_fei", 1.6f, 7f, "back");
            yield return Face("liu_bei", "back");
            foreach (var s in Lines("liu_bei", "c2.1", "lu_zhi", "c2.2", "liu_bei", "c2.3", "lu_zhi", "c2.4", "liu_bei", "c2.5",
                "guan_yu", "c2.6", "lu_zhi", "c2.7", "zhang_fei", "c2.8", "lu_zhi", "c2.9", "lu_zhi", "c2.10", "lu_zhi", "c2.11",
                "liu_bei", "c2.12", "lu_zhi", "c2.13"))
                yield return s;
            yield return Flag(ChapterScripts.CampGreeted);
        }

        // C10 到任安喜: in through the south gate; the clerk hurries down from the yamen to meet the new sergeant.
        static IEnumerable<StageStep> AnxiArrival()
        {
            yield return Autosave(SaveData.AnxiLocation);
            yield return Move("liu_bei", 0f, -17f, 1.6f);
            yield return Show("guan_yu", -1.6f, -18f, "back");
            yield return Show("zhang_fei", 1.6f, -18f, "back");
            yield return L("zhang_fei", "c10.1");
            yield return L("guan_yu", "c10.2");
            yield return Show("clerk", 0f, -12.5f, "front");
            yield return L("clerk", "c10.3");
            foreach (var s in Lines("liu_bei", "c10.4", "clerk", "c10.5", "clerk", "c10.6", "zhang_fei", "c10.7", "liu_bei", "c10.8", "clerk", "c10.9"))
                yield return s;
            yield return Show("clerk", 0f, 15.6f, "front");
            yield return Flag(ChapterScripts.AnxiGreeted);
        }

        static StageStep Show(string id, float x, float z, string facing) => Actor(id, null, x, z, facing);

        // ---------- prompts, marks, goals ----------

        public static string Prompt(FieldStoryController c, string id)
        {
            if (FieldScriptsR1.Mine(c)) return FieldScriptsR1.Prompt(c, id);   // the R1 map's side quests (npc-sidequests)
            var side = SideQuests.Prompt(c, id);   // chapter one's side quests (SideQuests*.cs)
            if (side != null) return side;
            if (FieldScripts5.Mine(c)) return FieldScripts5.Prompt(c, id);   // 0.5
            if (FieldScripts4.Mine(c)) return FieldScripts4.Prompt(c, id);
            if (FieldScripts3.Mine(c)) return FieldScripts3.Prompt(c, id);
            var ch = c.Run.Chapter2;
            switch (id)
            {
                case "camp_guard": return "raw:[确认] 与营门守卒交谈（存档／出发）";
                case "quartermaster": return "raw:[确认] 与军需官交谈（补给）";
                case "quiver": return ch.Has(ChapterScripts.QuiverFound) ? null : "raw:[确认] 翻看草堆";
                case "clerk": return "raw:[确认] 与县吏交谈（存档）";
                case "merchant_a": case "merchant_b": return $"raw:[确认] 与{Speaker(id)}交谈（买卖）";
                case "east_gate": return ch.Step == ChapterStep.Anxi ? "raw:[确认] 出东门剿贼" : null;
                case "timber": return ch.Has(ChapterScripts.WoodCarried) || ch.Has(ChapterState.Bridge) ? null : "raw:[确认] 查看木料";
                case "seal": return ch.Has(ChapterScripts.SealFound) ? null : "raw:[确认] 查看石碓后面";
                case "ox": return ch.Has(ChapterState.OxCase) ? null : "raw:[确认] 看看黄牛";
                default: return "talk:" + id;
            }
        }

        static string Speaker(string id) => id == "merchant_a" ? "兵器铺掌柜" : "杂货铺掌柜";

        public static bool Marked(FieldStoryController c, string id)
        {
            if (FieldScriptsR1.Mine(c)) return FieldScriptsR1.Marked(c, id);   // the R1 map's side quests (npc-sidequests)
            if (SideQuests.Marked(c, id)) return true;
            if (FieldScripts5.Mine(c)) return FieldScripts5.Marked(c, id);   // 0.5
            if (FieldScripts4.Mine(c)) return FieldScripts4.Marked(c, id);
            if (FieldScripts3.Mine(c)) return FieldScripts3.Marked(c, id);
            var ch = c.Run.Chapter2;
            bool anxi = ch.Step == ChapterStep.Anxi;
            return id switch
            {
                "camp_guard" => ch.Has(ChapterScripts.CampGreeted),
                "old_woman" => anxi && !ch.Has(ChapterState.Alms),
                "wang" => anxi && !ch.Has(ChapterState.OxCase),
                "bridge_elder" => anxi && !ch.Has(ChapterState.Bridge),
                "clerk" => anxi && ch.OpenCommissions.Count() == 0 && !ch.Has(ChapterScripts.CharmGiven),
                "east_gate" => anxi,
                "courier" => ch.Step == ChapterStep.AnxiBandits,
                _ => false,
            };
        }

        public static string Objective(FieldStoryController c)
        {
            if (FieldScriptsR1.Mine(c)) return FieldScriptsR1.Objective(c);   // the R1 map's side quests (npc-sidequests)
            if (FieldScripts5.Mine(c)) return FieldScripts5.Objective(c);   // 0.5
            if (FieldScripts4.Mine(c)) return FieldScripts4.Objective(c);
            if (FieldScripts3.Mine(c)) return FieldScripts3.Objective(c);
            var ch = c.Run.Chapter2;
            if (c.Location == FieldStoryController.Place.Camp)
                return ch.Has(ChapterScripts.CampGreeted) ? "目标：在营中整备，准备好后到营门出发" : "目标：拜见卢植";
            if (ch.Step == ChapterStep.AnxiBandits) return "目标：督邮到了驿馆，去见驿卒";
            int open = ch.OpenCommissions.Count();
            return open > 0 ? $"目标：处理县中难事（还剩{open}件），准备好后从东门出城剿贼" : "目标：从东门出城剿贼";
        }

        // ---------- talking ----------

        public static IEnumerable<StageStep> Talk(FieldStoryController c, string id)
        {
            if (FieldScriptsR1.Mine(c)) return FieldScriptsR1.Talk(c, id);   // the R1 map's side quests (npc-sidequests)
            var side = SideQuests.Talk(c, id);
            if (side != null) return side;
            if (FieldScripts5.Mine(c)) return FieldScripts5.Talk(c, id);   // 0.5
            if (FieldScripts4.Mine(c)) return FieldScripts4.Talk(c, id);
            if (FieldScripts3.Mine(c)) return FieldScripts3.Talk(c, id);
            var ch = c.Run.Chapter2;
            var inv = c.Run.Progress.Inventory;
            switch (id)
            {
                // Lu Zhi's camp
                case "lu_zhi": return new[] { L("lu_zhi", ch.Has(ChapterScripts.CampGreeted) ? "camp.lu_zhi.after" : "c2.1") };
                case "lu_guard": return new[] { L("lu_guard", "camp.lu_guard") };
                case "wounded_a": return new[] { L("wounded_a", "camp.wounded_a") };
                case "wounded_b": return new[] { L("wounded_b", "camp.wounded_b") };
                case "veteran": return new[] { L("veteran", "camp.veteran") };
                case "quartermaster": return new[] { L("quartermaster", "camp.quartermaster"), Call(() => c.pendingPanel = TownStoryController.ShopPanel) };
                case "quiver":
                    if (ch.Has(ChapterScripts.QuiverFound)) return null;
                    return new[] { L("system", "camp.quiver.found"), Item("lu_quiver"), L("system", "camp.quiver.note"), Flag(ChapterScripts.QuiverFound) };
                case "camp_guard": return CampGuard(c);

                // Anxi
                case "clerk": return Clerk(c);
                case "courier": return ch.Step == ChapterStep.AnxiBandits ? Courier(c) : new[] { L("courier", "anxi.courier.before") };
                case "old_woman": return OldWoman(c);
                case "wang": case "li": case "ox": return OxCase(c, id);
                case "bridge_elder": return Bridge(c);
                case "timber":
                    if (ch.Has(ChapterScripts.WoodCarried) || ch.Has(ChapterState.Bridge)) return null;
                    return new[] { L("system", "anxi.wood.found"), L("zhang_fei", "anxi.wood.carry"), Flag(ChapterScripts.WoodCarried), Call(c.RefreshFlags) };
                case "seal":
                    if (ch.Has(ChapterScripts.SealFound)) return null;
                    return new[] { L("system", "anxi.seal.found"), Call(() => inv.AddCopper(30)), L("system", "anxi.seal.note"), Flag(ChapterScripts.SealFound) };
                case "merchant_a": return new[] { L("merchant_a", "anxi.merchant_a"), Call(() => c.pendingPanel = TownStoryController.ShopPanel) };
                case "merchant_b": return new[] { L("merchant_b", "anxi.merchant_b"), Call(() => c.pendingPanel = TownStoryController.ShopPanel) };
                case "townsman_a": case "townsman_b":
                    return new[] { L(id, ch.Step == ChapterStep.AnxiBandits ? $"anxi.after.{id}" : $"anxi.{id}") };
                case "townsman_c": case "townsman_d": return new[] { L(id, $"anxi.{id}") };
                case "anxi_child": return new[] { L("anxi_child", "anxi.child") };
                case "east_gate": return EastGate(c);
            }
            return null;
        }

        static IEnumerable<StageStep> CampGuard(FieldStoryController c)
        {
            yield return L("camp_guard", "camp.guard.talk");
            yield return Choice("camp_guard", "camp.guard.ask", "camp_guard_", new[] { "camp.guard.opt.save", "camp.guard.opt.leave", "camp.guard.opt.cancel" },
                new[] { Call(() => c.pendingPanel = TownStoryController.SavePanel) },
                new[]
                {
                    Choice("system", "camp.leave.ask", "camp_leave_", new[] { "camp.leave.yes", "camp.leave.no" },
                        new[]
                        {
                            Fade(1f), Advance(ChapterStep.Camp, ChapterStep.Yingchuan), Load(GameRun.StageScene, ChapterScripts.ToYingchuan),
                        },
                        new StageStep[0]),
                },
                new StageStep[0]);
        }

        static IEnumerable<StageStep> Clerk(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (ch.Step == ChapterStep.Anxi && ch.OpenCommissions.Count() == 0 && !ch.Has(ChapterScripts.CharmGiven))
            {
                yield return L("clerk", "anxi.clerk.charm.1");
                yield return Item("anxi_charm");
                yield return L("system", "anxi.clerk.charm.2");
                yield return Flag(ChapterScripts.CharmGiven);
            }
            yield return Choice("clerk", ch.Step == ChapterStep.AnxiBandits ? "anxi.clerk.done" : "anxi.clerk.talk", "clerk_",
                new[] { "anxi.clerk.opt.save", "anxi.clerk.opt.cancel" },
                new[] { Call(() => c.pendingPanel = TownStoryController.SavePanel) },
                new StageStep[0]);
        }

        static IEnumerable<StageStep> OldWoman(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            var run = c.Run;
            if (ch.Has(ChapterState.Alms)) { yield return L("old_woman", "anxi.alms.done"); yield break; }
            yield return L("old_woman", "anxi.alms.1");
            yield return Choice("liu_bei", "anxi.alms.prompt", "alms_", new[] { "anxi.alms.copper", "anxi.alms.ration", "anxi.alms.none" },
                new[]
                {
                    Call(() => { if (run.Progress.Inventory.SpendCopper(20)) { ch.Set(ChapterState.Alms); run.Life.AddReputation(1); } }),
                    L("old_woman", "anxi.alms.thanks").When(ChapterState.Alms),
                    L("system", "anxi.alms.poor").When("!" + ChapterState.Alms),
                },
                new[]
                {
                    Call(() => { if (run.Progress.Inventory.Rations > 0) { run.Progress.Inventory.SpendRations(1); ch.Set(ChapterState.Alms); run.Life.AddReputation(1); } }),
                    L("old_woman", "anxi.alms.thanks").When(ChapterState.Alms),
                    L("system", "anxi.alms.noration").When("!" + ChapterState.Alms),
                },
                new[] { L("system", "anxi.alms.later") });
        }

        // 断案: who owns the ox? Letting it go shows the answer (it walks to the Wang trough); a wrong verdict can be retried.
        static IEnumerable<StageStep> OxCase(FieldStoryController c, string id)
        {
            var ch = c.Run.Chapter2;
            var run = c.Run;
            if (ch.Has(ChapterState.OxCase)) { yield return L("wang", "anxi.ox.done"); yield break; }
            yield return L("wang", "anxi.ox.1");
            yield return L("li", "anxi.ox.2");
            var verdictWang = new[]
            {
                L("liu_bei", "anxi.ox.wang.right"), L("li", "anxi.ox.li.ashamed"),
                Call(() => { ch.Set(ChapterState.OxCase); run.Life.AddReputation(1); }),
            };
            var verdictLi = new[] { L("system", "anxi.ox.wrong") };
            yield return Choice("liu_bei", "anxi.ox.prompt", "ox_", new[] { "anxi.ox.look", "anxi.ox.wang", "anxi.ox.li" },
                new[]
                {
                    Move("ox", -10f, 0.4f, 2.4f), L("system", "anxi.ox.walk"), Flag(ChapterScripts.OxLooked),
                    Choice("liu_bei", "anxi.ox.prompt", "ox_verdict_", new[] { "anxi.ox.wang", "anxi.ox.li" }, verdictWang, verdictLi),
                },
                verdictWang, verdictLi);
        }

        static IEnumerable<StageStep> Bridge(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            var run = c.Run;
            if (ch.Has(ChapterState.Bridge)) { yield return L("bridge_elder", "anxi.bridge.done"); yield break; }
            if (!ch.Has(ChapterScripts.WoodCarried))
            {
                yield return L("bridge_elder", "anxi.bridge.1");
                yield return L("bridge_elder", "anxi.bridge.2");
                yield return L("system", "anxi.bridge.need");
                yield break;
            }
            yield return L("bridge_elder", "anxi.bridge.fix.1");
            yield return Fade(1f, 0.4f);
            yield return Call(() => { ch.Set(ChapterState.Bridge); run.Life.AddReputation(1); c.RefreshFlags(); });
            yield return Fade(0f, 0.4f);
            yield return L("narrator", "anxi.bridge.fix.2");
        }

        static IEnumerable<StageStep> EastGate(FieldStoryController c)
        {
            if (c.Run.Chapter2.Step != ChapterStep.Anxi) { yield return L("system", "anxi.gate.done"); yield break; }
            yield return Choice("system", "anxi.gate.ask", "gate_", new[] { "anxi.gate.yes", "anxi.gate.no" },
                new[] { Fade(1f), Fight("battle_6") },
                new StageStep[0]);
        }

        // After the bandits: the courier warns that open commissions will close, then C11 督邮下县, C12 鞭督邮 (choice ②),
        // C13 挂印出走 at the east gate, the later-events narration, scroll ③ with the 完成 seal, and the end of 0.2.
        static IEnumerable<StageStep> Courier(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            yield return L("courier", "anxi.courier.warn");
            var open = ch.OpenCommissions.ToList();
            if (open.Count > 0)
                yield return L("system", "raw:" + string.Format(Text("anxi.courier.warn.open"), string.Join("、", open.Select(o => Text("commission." + o)))));
            yield return Choice("system", "anxi.courier.ask", "courier_", new[] { "anxi.courier.yes", "anxi.courier.no" },
                Duyou(c).ToArray(), new StageStep[0]);
        }

        // Keys are resolved by the UI; the courier's list of open commissions needs the words here.
        public static System.Func<string, string> Text = k => k;

        static IEnumerable<StageStep> Duyou(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            // 鞭督邮 as an automatic in-game scene like the peach-garden oath (user 2026-10-26: "做成游戏内Q版动画和对白，
            // 剧情结束后出水墨画卷"): letterbox, a closer shot, the brothers' and the inspector's story poses (DuyouPose, drawn
            // facing screen right; flip faces left), lines moving on by themselves (confirm moves on early), and the ink
            // scroll as soon as the seal hangs on his neck. Places: the post station's door (22, 12.4), the hitching post
            // (20, 9); bound, the inspector stands just right of it (his poses draw the post, so the real one is hidden).
            const float t = 3f, boundX = 20.46f, boundZ = 8.96f;
            yield return Fade(1f);
            yield return Letterbox(true);
            yield return Focus(21.6f, 10f, 18f);
            yield return Music("anxi_duyou");
            yield return Show("liu_bei", 21.9f, 8.4f, "back");
            yield return Show("guan_yu", 18.8f, 7.8f, "back");
            yield return Show("zhang_fei", 23.4f, 7.6f, "back");
            yield return Show("courier", 25.8f, 11.2f, "left");
            yield return Show("du_you_aide", 22f, 11.6f, "front");
            yield return Show("du_you", 23f, 13.2f, "front");
            yield return Pose("du_you", DuyouPose.DuLounge, flip: true);
            yield return Fade(0f);
            // C11: refused at the gate.
            yield return TL("du_you_aide", "c11.1", 4f);
            yield return Pose("zhang_fei", DuyouPose.ZhangAngry, flip: true);
            yield return TL("zhang_fei", "c11.2", 2.5f);
            yield return Pose("zhang_fei", -1);
            yield return Pose("liu_bei", DuyouPose.LiuRequest);
            yield return TL("liu_bei", "c11.3", t);
            yield return Pose("du_you", DuyouPose.DuWaveAway, flip: true);
            yield return TL("du_you_aide", "c11.4", 2.5f);
            yield return Hide("du_you");
            yield return Shake(0.2f);
            yield return TL("narrator", "c11.5", 2.5f);
            yield return Pose("liu_bei", -1);
            yield return TL("du_you_aide", "c11.6", 3.5f);
            yield return Pose("liu_bei", DuyouPose.LiuFist);
            yield return TL("liu_bei", "c11.7", 3.5f);
            yield return TL("du_you_aide", "c11.8", 2f);
            yield return Hide("du_you_aide");
            // Two days later the clerk brings word at night.
            yield return Fade(1f, 0.4f);
            yield return Show("clerk", 20.8f, 9.8f, "right");
            yield return Pose("guan_yu", DuyouPose.GuanStroke);
            yield return Pose("zhang_fei", DuyouPose.ZhangArmsCrossed);
            yield return Fade(0f, 0.4f);
            yield return TL("narrator", "c11.9", 5f);
            yield return Hide("clerk");
            yield return Pose("guan_yu", -1); yield return Pose("zhang_fei", -1); yield return Pose("liu_bei", -1);

            // C12: Liu Bei himself (史实) or Zhang Fei first (演义); the seal ends on the inspector's neck either way.
            yield return Choice("liu_bei", "c12.prompt", ChapterScripts.DuyouChoice, new[] { "c12.a", "c12.b" },
                new[]
                {
                    Pose("zhang_fei", DuyouPose.ZhangCharge, flip: true), MoveWith("zhang_fei", 22.6f, 10.4f, 0.5f), TL("zhang_fei", "c12.a.1", 2.5f),
                    Pose("zhang_fei", -1), Pose("liu_bei", DuyouPose.LiuStop), TL("liu_bei", "c12.a.2", 2.5f),
                    Pose("zhang_fei", DuyouPose.ZhangHeldBack, flip: true), Pose("liu_bei", -1), Move("zhang_fei", 23.4f, 7.6f, 0.4f),
                    TL("liu_bei", "c12.a.3", 2.5f),
                    // He walks in and drags the inspector out to the hitching post, pulling him along backwards.
                    Move("liu_bei", 22.4f, 12.2f, 0.8f), Hide("liu_bei"), Wait(0.6f),
                    Show("du_you", 22.6f, 12.6f, "front"), Pose("du_you", DuyouPose.DuDragged, flip: true),
                    Show("liu_bei", 21.6f, 12.4f, "left"), Pose("liu_bei", DuyouPose.LiuDrag, flip: true),
                    MoveWith("du_you", boundX + 0.8f, boundZ + 0.3f, 1.6f), Move("liu_bei", boundX - 0.2f, boundZ + 0.4f, 1.6f),
                    Hide("hitching_post"), Show("du_you", boundX, boundZ, "front"), Pose("du_you", DuyouPose.DuBound),
                    Pose("liu_bei", -1), Move("liu_bei", 21.7f, 8.8f, 0.5f),
                    TL("narrator", "c12.a.4", 3f),
                    TL("du_you", "c12.a.5", 2.5f),
                    Pose("liu_bei", DuyouPose.LiuCaneRaise, flip: true), TL("liu_bei", "c12.a.6", 3.5f),
                    Pose("liu_bei", DuyouPose.LiuCaneStrike, flip: true), Pose("du_you", DuyouPose.DuFlinch), Shake(0.25f), Wait(0.35f),
                    Pose("liu_bei", DuyouPose.LiuCaneRaise, flip: true), Pose("du_you", DuyouPose.DuBound), Wait(0.3f),
                    Pose("liu_bei", DuyouPose.LiuCaneStrike, flip: true), Pose("du_you", DuyouPose.DuFlinch), Shake(0.25f), Wait(0.35f),
                    Pose("liu_bei", DuyouPose.LiuCaneRaise, flip: true), Pose("du_you", DuyouPose.DuBeg), Wait(0.3f),
                    Pose("liu_bei", DuyouPose.LiuCaneStrike, flip: true), Pose("du_you", DuyouPose.DuFlinch), Shake(0.3f),
                    TL("narrator", "c12.a.7", 3f),
                    Pose("guan_yu", DuyouPose.GuanNod), TL("guan_yu", "c12.a.8", 3f),
                    Pose("guan_yu", -1), Pose("liu_bei", -1),
                    Remember("liu_bei", "liubei_duyou", "A_self", "mem.liubei_duyou.A"),
                    Remember("guan_yu", "guanyu_duyou", "A_self", "mem.guanyu_duyou.A"),
                },
                new[]
                {
                    Pose("zhang_fei", DuyouPose.ZhangCharge, flip: true), TL("narrator", "c12.b.1", 2f), Move("zhang_fei", 22.4f, 12.2f, 0.6f), Hide("zhang_fei"),
                    Pose("liu_bei", DuyouPose.LiuStop), TL("liu_bei", "c12.b.2", 1.8f), Pose("liu_bei", -1),
                    // Zhang Fei drags him out by the hair and ties him to the post.
                    Show("du_you", 22.6f, 12.6f, "front"), Pose("du_you", DuyouPose.DuDragged, flip: true),
                    Show("zhang_fei", 21.6f, 12.4f, "left"), Pose("zhang_fei", DuyouPose.ZhangDragHair),
                    MoveWith("du_you", boundX + 0.8f, boundZ + 0.3f, 1.4f), Move("zhang_fei", boundX - 0.2f, boundZ + 0.4f, 1.4f),
                    Hide("hitching_post"), Show("du_you", boundX, boundZ, "front"), Pose("du_you", DuyouPose.DuBound),
                    Pose("zhang_fei", -1), Move("zhang_fei", 21.7f, 8.9f, 0.4f),
                    TL("narrator", "c12.b.3", 3.5f),
                    Pose("zhang_fei", DuyouPose.ZhangWhipRaise, flip: true), TL("zhang_fei", "c12.b.4", 2.5f),
                    Pose("zhang_fei", DuyouPose.ZhangWhipStrike, flip: true), Pose("du_you", DuyouPose.DuFlinch), Shake(0.25f), Wait(0.3f),
                    Pose("zhang_fei", DuyouPose.ZhangWhipRaise, flip: true), Pose("du_you", DuyouPose.DuBound), Wait(0.25f),
                    Pose("zhang_fei", DuyouPose.ZhangWhipStrike, flip: true), Pose("du_you", DuyouPose.DuFlinch), Shake(0.25f),
                    Pose("du_you", DuyouPose.DuBeg), TL("du_you", "c12.b.5", 2f),
                    Pose("zhang_fei", DuyouPose.ZhangWhipRaise, flip: true), Move("liu_bei", 22.9f, 8.7f, 0.4f),
                    Pose("liu_bei", DuyouPose.LiuStop, flip: true), TL("liu_bei", "c12.b.6", 2.5f),
                    Pose("zhang_fei", DuyouPose.ZhangHeldBack, flip: true), TL("zhang_fei", "c12.b.7", 2.5f),
                    TL("liu_bei", "c12.b.8", 3.5f), Pose("liu_bei", -1),
                    Pose("guan_yu", DuyouPose.GuanSigh), TL("guan_yu", "c12.b.9", 3.5f), Pose("guan_yu", -1), Pose("zhang_fei", -1),
                    Move("zhang_fei", 23.4f, 7.6f, 0.4f),
                    Remember("liu_bei", "liubei_duyou", "B_zhangfei", "mem.liubei_duyou.B"),
                    Remember("guan_yu", "guanyu_duyou", "B_zhangfei", "mem.guanyu_duyou.B"),
                });
            yield return Remember("zhang_fei", "zhangfei_duyou", "held_back", "mem.zhangfei_duyou");
            // The seal hung on his neck; the three turn to go, Liu Bei looks back, and the moment freezes into the scroll.
            yield return Move("liu_bei", 21.4f, 8.9f, 0.5f);
            yield return Pose("liu_bei", DuyouPose.LiuHangSeal, flip: true);
            yield return TL("narrator", "c12.end.1", 3.5f);
            yield return Pose("du_you", DuyouPose.DuSealed);
            yield return TL("liu_bei", "c12.end.2", 2.5f);
            yield return Pose("zhang_fei", DuyouPose.ZhangLaugh);
            yield return TL("zhang_fei", "c12.end.3", 2.5f);
            yield return Pose("guan_yu", DuyouPose.GuanSalute);
            yield return TL("guan_yu", "c12.end.4", 3f);
            yield return Pose("liu_bei", -1);
            yield return Move("liu_bei", 22.6f, 8.2f, 0.6f);
            yield return Pose("liu_bei", DuyouPose.LiuLookBack);
            yield return Focus(21.6f, 9.6f, 26f);
            yield return TL("liu_bei", "c12.end.5", 3.5f);
            yield return Letterbox(false);
            yield return Music(null);
            yield return Scroll(ChapterScripts.ScrollAnxi);
            yield return Fade(1f);
            yield return Focus(0f, 0f, 0f);
            yield return Pose("liu_bei", -1); yield return Pose("guan_yu", -1); yield return Pose("zhang_fei", -1);
            yield return Hide("du_you");
            yield return Show("hitching_post", -999f, -999f, "front");

            // C13 at the east gate: who comes to see them off depends on what was done in the county.
            yield return Show("liu_bei", 31f, 0f, "left");
            yield return Show("guan_yu", 32.4f, 0.8f, "left");
            yield return Show("zhang_fei", 32.4f, -0.8f, "left");
            yield return Show("clerk", 28.6f, 0.6f, "right");
            yield return Show("anxi_child", 28.4f, -1f, "right");
            yield return Show("old_woman", 27.4f, 1.6f, "right").When(ChapterState.Alms);
            yield return Show("wang", 27.2f, -0.2f, "right").When(ChapterState.OxCase);
            yield return Show("bridge_elder", 27.6f, -1.8f, "right").When(ChapterState.Bridge);
            yield return Fade(0f);
            yield return L("clerk", "c13.1");
            yield return L("old_woman", "c13.alms").When(ChapterState.Alms);
            yield return L("wang", "c13.ox").When(ChapterState.OxCase);
            yield return L("bridge_elder", "c13.bridge").When(ChapterState.Bridge);
            yield return L("clerk", "c13.charm").When(ChapterScripts.CharmGiven);
            yield return Call(() => { if (!ch.Has(ChapterState.Alms) && !ch.Has(ChapterState.OxCase) && !ch.Has(ChapterState.Bridge)) ch.Set("none_done"); });
            yield return L("clerk", "c13.none").When("none_done");
            yield return L("anxi_child", "c13.child");
            yield return L("liu_bei", "c13.liu");
            yield return L("guan_yu", "c13.guan.a").When(ChapterScripts.DuyouChoice + "0");
            yield return L("guan_yu", "c13.guan.b").When(ChapterScripts.DuyouChoice + "1");
            yield return L("zhang_fei", "c13.zhang.a").When(ChapterScripts.DuyouChoice + "0");
            yield return L("zhang_fei", "c13.zhang.b").When(ChapterScripts.DuyouChoice + "1");
            yield return Move("liu_bei", 36f, 0f, 1.4f);
            yield return L("narrator", "c13.end");
            yield return Fade(1f);
            yield return Advance(ChapterStep.AnxiBandits, ChapterStep.Done);
            yield return Load(GameRun.StageScene, ChapterScripts3.Drift);
        }
    }
}
