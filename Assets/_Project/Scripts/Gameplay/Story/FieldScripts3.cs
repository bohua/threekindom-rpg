using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Save;
using static ThreeKingdoms.Gameplay.Story.S;

namespace ThreeKingdoms.Gameplay.Story
{
    // Who says and does what in Gongsun Zan's garrison and the Suanzao camp (V03_TAODONG_PLAN §3.1, §5–§7; REVIEW_V03
    // R15–R16, R27–R29). World X / Z of the scenes ChapterFieldBuilder3 makes:
    //   garrison: south gate z −22, the command hall's steps z 15.5 (square z 6–14), drill ground west (x −24…−10),
    //             stable east (x 14…26), quartermaster x 9 z −6, Liu Bei's quarters x −9 z −8.
    //   Suanzao: the camp gate z −28, main road x −6…6, tents at x ±14 (z −12, 2, 14), altar z 22–28, leader's tent z 34.
    public static class FieldScripts3
    {
        public static bool Mine(FieldStoryController c) =>
            c.Location == FieldStoryController.Place.Gongsun || c.Location == FieldStoryController.Place.Suanzao;

        static bool Gongsun(FieldStoryController c) => c.Location == FieldStoryController.Place.Gongsun;

        // ---------- arrival ----------

        public static IEnumerable<StageStep> OnArrive(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (Gongsun(c) && ch.Step == ChapterStep.Gongsun)
                return ch.Has(ChapterScripts3.GongsunGreeted) ? new[] { Autosave(SaveData.GongsunLocation) } : GongsunArrival();
            if (!Gongsun(c) && ch.Step == ChapterStep.Suanzao)
                return ch.Has(ChapterScripts3.Wenjiu) || ch.Has("sz_arrived") ? new[] { Autosave(SaveData.SuanzaoLocation) } : SuanzaoArrival();
            return null;
        }

        // D4 同窗相见: Gongsun Zan comes down the steps; Jian Yong comes out of Liu Bei's quarters.
        static IEnumerable<StageStep> GongsunArrival()
        {
            yield return Autosave(SaveData.GongsunLocation);
            yield return Move("liu_bei", 0f, 8f, 3.2f);
            yield return Show("guan_yu", -1.6f, 7.2f, "back");
            yield return Show("zhang_fei", 1.6f, 7.2f, "back");
            yield return Face("liu_bei", "back");
            yield return Move("gongsun_zan", 0f, 11f, 1.0f);
            foreach (var s in Lines("gongsun_zan", "d4.1", "liu_bei", "d4.2", "gongsun_zan", "d4.3", "guan_yu", "d4.4", "gongsun_zan", "d4.5",
                         "zhang_fei", "d4.6", "gongsun_zan", "d4.7", "liu_bei", "d4.8", "gongsun_zan", "d4.9"))
                yield return s;
            yield return Remember("gongsun_zan", "gongsun_tongchuang", "seen", "mem.gongsun_tongchuang");
            yield return L("narrator", "d4.10");
            yield return Flag(ChapterScripts3.ZhongwangLearned);
            yield return L("system", "d4.zhongwang");
            yield return Show("jian_yong", 0f, 4.5f, "back");
            foreach (var s in Lines("jian_yong", "d4.11", "liu_bei", "d4.12", "jian_yong", "d4.13")) yield return s;
            yield return Flag(ChapterScripts3.JianYong);
            yield return Show("jian_yong", -9f, -8f, "front");
            yield return Show("gongsun_zan", 0f, 15.5f, "front");
            yield return Flag(ChapterScripts3.GongsunGreeted);
        }

        // Arrival at Suanzao: the gate, the banners, Gongsun Zan's men by the white-flag camp.
        static IEnumerable<StageStep> SuanzaoArrival()
        {
            yield return Autosave(SaveData.SuanzaoLocation);
            yield return Move("liu_bei", 0f, -20f, 2.0f);
            yield return Show("guan_yu", -1.6f, -21f, "back");
            yield return Show("zhang_fei", 1.6f, -21f, "back");
            foreach (var s in Lines("narrator", "sz.arrive.1", "zhang_fei", "sz.arrive.2", "guan_yu", "sz.arrive.3", "liu_bei", "sz.arrive.4",
                         "sz_gs_soldier", "sz.arrive.5"))
                yield return s;
            yield return Flag("sz_arrived");
        }

        static StageStep Show(string id, float x, float z, string facing) => Actor(id, null, x, z, facing);

        // ---------- prompts, marks, goals ----------

        public static string Prompt(FieldStoryController c, string id)
        {
            var ch = c.Run.Chapter2;
            switch (id)
            {
                case "gs_quartermaster": return "raw:[确认] 与军需官交谈（补给／存档）";
                case "sz_gs_soldier": return "raw:[确认] 与公孙瓒营士卒交谈（存档／出发）";
                case "sz_merchant": return "raw:[确认] 与随军商贩交谈（买卖）";
                case "bell": return ch.Has(ChapterScripts3.BellFound) ? null : "raw:[确认] 翻看草料堆";
                case "edict": return ch.Has(ChapterScripts3.EdictFound) ? null : "raw:[确认] 拾起地上的绢帛";
                case "gongsun_zan": return ch.Has(ChapterScripts3.GongsunGreeted) ? "raw:[确认] 与公孙瓒交谈（随军南下）" : "talk:" + id;
                case "baima_rider": return ch.DrillOpen ? "raw:[确认] 与白马义从交谈（校场演武）" : "talk:" + id;
                case "yuan_shao": return ch.Has(ChapterScripts3.Wenjiu) ? "talk:" + id : "raw:[确认] 拜见盟主袁绍（入帐会盟）";
                default: return "talk:" + id;
            }
        }

        public static bool Marked(FieldStoryController c, string id)
        {
            var ch = c.Run.Chapter2;
            return id switch
            {
                "gongsun_zan" => Gongsun(c) && ch.Has(ChapterScripts3.GongsunGreeted),
                "baima_rider" => ch.DrillOpen,
                "tian_yu" => Gongsun(c) && !ch.Has(ChapterState.TianYu) && (!ch.Has(RollStarted) || ch.Has(RollFound)),
                "roll_head_1" or "roll_head_2" or "roll_head_3" or "roll_head_4" => Gongsun(c) && ch.Has(RollStarted) && !ch.Has("roll_h" + id[id.Length - 1]),
                "roll_sick" => Gongsun(c) && ch.Has("roll_h3") && !ch.Has(RollFound),
                "yuan_shao" => !ch.Has(ChapterScripts3.Wenjiu),
                "sz_soldier_a" => !ch.Has(ChapterScripts3.RationsShared),
                "sz_gs_soldier" => ch.Has(ChapterScripts3.Wenjiu),
                _ => false,
            };
        }

        public static string Objective(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (Gongsun(c))
                return !ch.Has(ChapterScripts3.GongsunGreeted) ? "目标：拜见公孙瓒"
                    : ch.Has(RollStarted) && !ch.Has(ChapterState.TianYu) ? (ch.Has(RollFound) ? "目标：回校场告诉田豫" : ch.Has("roll_h3") ? "目标：去马厩找第三队少的那个人" : "目标：陪田豫到校场，挨个问四队的队头")
                    : ch.DrillOpen ? "目标：营中整备（可到校场与白马义从演武），准备好后向公孙瓒请命南下" : "目标：营中整备，准备好后向公孙瓒请命南下";
            return ch.Has(ChapterScripts3.Wenjiu) ? "目标：整备后到辕门边公孙瓒营，随军出战虎牢关" : "目标：到会盟坛前拜见盟主袁绍";
        }

        // ---------- talking ----------

        public static IEnumerable<StageStep> Talk(FieldStoryController c, string id)
        {
            var ch = c.Run.Chapter2;
            var run = c.Run;
            var inv = run.Progress.Inventory;
            switch (id)
            {
                // Gongsun Zan's garrison
                case "gongsun_zan": return ch.Has(ChapterScripts3.GongsunGreeted) ? GongsunZan(c) : null;
                case "baima_rider": return ch.DrillOpen ? Drill(c) : new[] { L("baima_rider", ch.Has(ChapterState.Drill) ? "gs.rider.after" : "gs.rider.talk") };
                case "groom": return new[] { L("groom", "gs.groom") };
                case "jian_yong": return new[] { L("jian_yong", "gs.jianyong") };
                case "tian_yu": return TianYu(c);
                case "roll_head_1": case "roll_head_2": case "roll_head_3": case "roll_head_4": return RollHead(c, id[id.Length - 1] - '0');
                case "roll_sick": return RollSick(c);
                case "gs_soldier_a": return new[] { L("gs_soldier_a", "gs.soldier_a") };
                case "gs_soldier_b": return new[] { L("gs_soldier_b", "gs.soldier_b") };
                case "gs_guard": return new[] { L("gs_guard", "gs.guard") };
                case "gs_quartermaster":
                    return new[]
                    {
                        L("gs_quartermaster", "gs.quartermaster"),
                        Choice("gs_quartermaster", "gs.quartermaster.ask", "gsq_", new[] { "gs.opt.shop", "gs.opt.save", "gs.opt.cancel" },
                            new[] { Call(() => c.pendingPanel = TownStoryController.ShopPanel) },
                            new[] { Call(() => c.pendingPanel = TownStoryController.SavePanel) },
                            new StageStep[0]),
                    };
                case "bell":
                    if (ch.Has(ChapterScripts3.BellFound)) return null;
                    return new[] { L("system", "gs.bell.found"), Item("baima_bell"), L("system", "gs.bell.note"), Flag(ChapterScripts3.BellFound) };

                // Suanzao
                case "yuan_shao":
                    if (!ch.Has(ChapterScripts3.Wenjiu))
                        return new[] { L("yuan_shao", "sz.yuanshao.before"), Fade(1f), Load(GameRun.StageScene, ChapterScripts3.Huimeng) };
                    return new[] { L("yuan_shao", "sz.yuanshao.after") };
                case "cao_cao": return new[] { L("cao_cao", ch.Has(ChapterScripts3.Wenjiu) ? "sz.cao.after" : "sz.cao.before") };
                case "sun_jian": return new[] { L("sun_jian", "sz.sun") };
                case "yuan_shu": return new[] { L("yuan_shu", ch.Has(ChapterScripts3.Wenjiu) ? "sz.yuanshu.after" : "sz.yuanshu.before") };
                case "kong_rong": return new[] { L("kong_rong", "sz.kong") };
                case "tao_qian": return new[] { L("tao_qian", "sz.tao") };
                case "sz_envoy": return new[] { L("sz_envoy", "sz.envoy") };
                case "sz_soldier_b": return new[] { L("sz_soldier_b", "sz.soldier_b") };
                case "sz_soldier_a": return ShareRations(c);
                case "sz_merchant": return new[] { L("sz_merchant", "sz.merchant"), Call(() => c.pendingPanel = TownStoryController.ShopPanel) };
                case "sz_gs_soldier": return CampGate(c);
                case "edict":
                    if (ch.Has(ChapterScripts3.EdictFound)) return null;
                    return new[] { L("system", "sz.edict.found"), Call(() => inv.AddCopper(40)), L("system", "sz.edict.note"), Flag(ChapterScripts3.EdictFound) };
            }
            return null;
        }

        // D5 讨董檄文 and the departure (REVIEW_V03 R29: only when the player asks; the box lists what is left undone).
        static IEnumerable<StageStep> GongsunZan(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            yield return L("gongsun_zan", ch.Has(ChapterState.Drill) ? "gs.zan.after_drill" : "gs.zan.talk");
            // 田豫点兵 is a must before the call to arms (npc-sidequests sq.lb1.r1.tian_yu_roll).
            if (!ch.Has(ChapterState.TianYu)) { yield return L("gongsun_zan", "gs.zan.roll_first"); yield break; }
            var left = new List<string>();
            if (ch.DrillOpen) left.Add("校场演武");
            string ask = "raw:随公孙瓒南下会盟。离开后不能返回" + (left.Count > 0 ? "（尚未完成：" + string.Join("、", left) + "）" : "") + "。确定出发吗？";
            yield return Choice("system", ask, "gs_leave_", new[] { "gs.leave.yes", "gs.leave.no" },
                new[]
                {
                    L("gongsun_zan", "d5.1"), L("gongsun_zan", "d5.2"), L("liu_bei", "d5.3"),
                    L("gongsun_zan", "d5.4").When(ChapterState.Drill),
                    L("tian_yu", "d5.5").When(ChapterState.TianYu), L("liu_bei", "d5.6").When(ChapterState.TianYu),
                    Remember("tian_yu", "tianyu_follow", "follow", "mem.tianyu_follow").When(ChapterState.TianYu),
                    L("gongsun_zan", "d5.7"),
                    Fade(1f), Load(GameRun.StageScene, ChapterScripts3.ToSuanzao),
                },
                new StageStep[0]);
        }

        static IEnumerable<StageStep> Drill(FieldStoryController c)
        {
            yield return L("baima_rider", "gs.rider.ask");
            yield return Choice("liu_bei", "gs.drill.ask", "drill_", new[] { "gs.drill.yes", "gs.drill.no" },
                new[] { L("baima_rider", "gs.drill.go"), Fade(1f), Fight("battle_7") },
                new[] { L("baima_rider", "gs.drill.later") });
        }

        // 田豫点兵 (npc-sidequests R1_R2 sq.lb1.r1.tian_yu_roll, core layer; lines dlg.lb1.tian_yu.N4.* and
        // dlg.lb1.tian_yu_roll.*): Tian Yu offers to count the men; Liu Bei walks him round the four squads on the drill
        // ground; the third is a man short ("ran off"), Tian Yu will not write "fled" before he has seen him, and he is found
        // sick behind the hay. Tian Yu writes "sick" before the squad; done, he joins (the existing TianYu flag).
        public const string RollStarted = "roll_started", RollFound = "roll_found";

        static IEnumerable<StageStep> TianYu(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (ch.Has(ChapterState.TianYu)) { yield return L("tian_yu", "gs.tianyu.after"); yield break; }
            if (!ch.Has(RollStarted))
            {
                foreach (var s in Lines("tian_yu", "gs.tianyu.1", "zhang_fei", "gs.tianyu.zf", "tian_yu", "gs.tianyu.2"))
                    yield return s;
                yield return Flag(RollStarted);
                yield break;
            }
            if (!ch.Has(RollFound)) { yield return L("tian_yu", "gs.roll.wait"); yield break; }
            foreach (var s in Lines("tian_yu", "gs.roll.s4", "tian_yu", "gs.roll.s5"))
                yield return s;
            yield return Flag(ChapterState.TianYu);
            yield return Remember("tian_yu", "first_roll", "fair", "mem.tianyu_roll");
        }

        static IEnumerable<StageStep> RollHead(FieldStoryController c, int n)
        {
            var ch = c.Run.Chapter2;
            if (!ch.Has(RollStarted)) { yield return L("roll_head", "gs.roll.idle"); yield break; }
            if (n == 3)
            {
                yield return L("roll_head", "gs.roll.s2.1");
                yield return L("tian_yu", "gs.roll.s2.2");
            }
            else yield return L("roll_head", "gs.roll.head" + n);
            yield return Flag("roll_h" + n);
            yield return Call(c.RefreshFlags);
        }

        static IEnumerable<StageStep> RollSick(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (ch.Has(RollFound)) { yield return L("roll_sick", "gs.roll.sick.after"); yield break; }
            foreach (var s in Lines("roll_sick", "gs.roll.s3.1", "liu_bei", "gs.roll.s3.lb"))
                yield return s;
            yield return Flag(RollFound);
        }

        // R15: the soldiers short of grain; Liu Bei shares his own (a ration or 20 copper), reputation +1.
        static IEnumerable<StageStep> ShareRations(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            var run = c.Run;
            if (ch.Has(ChapterScripts3.RationsShared)) { yield return L("sz_soldier_a", "sz.share.done"); yield break; }
            yield return L("sz_soldier_a", "sz.share.1");
            yield return Choice("liu_bei", "sz.share.prompt", "share_", new[] { "sz.share.ration", "sz.share.copper", "sz.share.none" },
                new[]
                {
                    Call(() => { if (run.Progress.Inventory.Rations > 0) { run.Progress.Inventory.SpendRations(1); ch.Set(ChapterScripts3.RationsShared); run.Life.AddReputation(1); } }),
                    L("sz_soldier_a", "sz.share.thanks").When(ChapterScripts3.RationsShared),
                    L("system", "sz.share.noration").When("!" + ChapterScripts3.RationsShared),
                },
                new[]
                {
                    Call(() => { if (run.Progress.Inventory.SpendCopper(20)) { ch.Set(ChapterScripts3.RationsShared); run.Life.AddReputation(1); } }),
                    L("sz_soldier_a", "sz.share.thanks").When(ChapterScripts3.RationsShared),
                    L("system", "sz.share.poor").When("!" + ChapterScripts3.RationsShared),
                },
                new[] { L("sz_soldier_a", "sz.share.later") });
        }

        static IEnumerable<StageStep> CampGate(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (!ch.Has(ChapterScripts3.Wenjiu))
            {
                yield return L("sz_gs_soldier", "sz.gate.before");
                yield return Choice("sz_gs_soldier", "sz.gate.ask", "szg_", new[] { "gs.opt.save", "gs.opt.cancel" },
                    new[] { Call(() => c.pendingPanel = TownStoryController.SavePanel) }, new StageStep[0]);
                yield break;
            }
            yield return L("sz_gs_soldier", "sz.gate.talk");
            yield return Choice("sz_gs_soldier", "sz.gate.ask", "szg_", new[] { "gs.opt.save", "sz.gate.opt.leave", "gs.opt.cancel" },
                new[] { Call(() => c.pendingPanel = TownStoryController.SavePanel) },
                new[]
                {
                    Choice("system", "sz.leave.ask", "sz_leave_", new[] { "sz.leave.yes", "gs.leave.no" },
                        new[] { Fade(1f), Load(GameRun.StageScene, ChapterScripts3.ToHulao) },
                        new StageStep[0]),
                },
                new StageStep[0]);
        }
    }
}
