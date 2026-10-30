using System.Collections.Generic;
using ThreeKingdoms.Core.Story;
using static ThreeKingdoms.Gameplay.Story.S;
using static ThreeKingdoms.Gameplay.Story.SideQuests;

namespace ThreeKingdoms.Gameplay.Story
{
    // R7 the Qingzhou corridor (design/chapter1-rise/npc-sidequests/R6_R7_yanzhou_qingzhou.md §R7), open from 193 (N7).
    // Core layer: 稷门问答 (must-do: three questions, Jian Yong's intelligence up one level; the optional fourth, the
    // fisher's water, is the backfill layer and left out), 景王祠, 下密的草鞋 (L6 ③), 鼓叟说书·孔北海 (L1 ⑦, after
    // battle 12 — only reachable from Xiapi in 194, the chapter having no walkable home between), and the fallback of
    // 济南道马队 (L5: no escort; the horses are settled at the Jinan post station, the old ledger then comes at Xiapi).
    // World X / Z of Region_R7: Linzi (265, 211) with the Jixia ruins (298, 231), Jinan (80, 205), Duchang (465, 205);
    // Xiami has no town on the map, so its post station stands by the road east of Linzi (395, 200).
    public static class SideQuestsR7
    {
        public static bool Mine(FieldStoryController c) => c.Location == FieldStoryController.Place.R7;

        const string Stele = "sq.r7.stele", Jixia = "sq.r7.jixia", Q1 = "sq.r7.q1", Q2 = "sq.r7.q2", Shrine = "sq.r7.shrine_seen",
            ShrineDone = "sq.r7.shrine", Sandals = "sq.r7.sandals", Book = "sq.r7.kbh", Horses = "sq.r7.horses";

        static bool Beihai(ChapterState ch) => ch.Recall("kong_rong", "kongrong_beihai") != null;

        public static bool? Present(FieldStoryController c, string id) => id switch
        {
            "chunyu_weng" or "jixia_stele" or "yan_bo" or "bu_heng" or "jingwang_shrine" => true,
            "gu_sou" => Beihai(c.Run.Chapter2) && !c.Run.Chapter2.Has(Book),
            "zhang_shiping" or "su_shuang" => true,
            _ => null,
        };

        public static string Prompt(FieldStoryController c, string id)
        {
            var ch = c.Run.Chapter2;
            return id switch
            {
                "jixia_stele" => "raw:[确认] 读残碑",
                "chunyu_weng" => ch.Has(Jixia) ? null : "raw:[确认] 与稷下老儒交谈",
                "jingwang_shrine" => ch.Has(Shrine) && !ch.Has(ShrineDone) ? "raw:[确认] 查看景王祠" : null,
                "bu_heng" => ch.Has(Sandals) ? null : "raw:[确认] 进下密驿",
                "gu_sou" => "raw:[确认] 听鼓叟说书",
                "zhang_shiping" or "su_shuang" => ch.Has(Horses) ? null : "raw:[确认] 与中山马商交谈（马群）",
                _ => null,
            };
        }

        public static bool Marked(FieldStoryController c, string id)
        {
            var ch = c.Run.Chapter2;
            return id switch
            {
                "chunyu_weng" => !ch.Has(Jixia),
                "jixia_stele" => ch.Has("sq.r7.jixia_met") && !ch.Has(Stele),
                "yan_bo" => !ch.Has(Shrine),
                "jingwang_shrine" => ch.Has(Shrine) && !ch.Has(ShrineDone),
                "bu_heng" => !ch.Has(Sandals),
                "gu_sou" => true,
                "zhang_shiping" => !ch.Has(Horses),
                _ => false,
            };
        }

        public static IEnumerable<StageStep> Talk(FieldStoryController c, string id) => id switch
        {
            "jixia_stele" => new[] { L("narrator", "sq.r7.stele"), Flag(Stele) },
            "chunyu_weng" => Chunyu(c),
            "yan_bo" => YanBo(c),
            "jingwang_shrine" => ShrineSteps(c),
            "bu_heng" => BuHeng(c),
            "gu_sou" => GuSou(c),
            "zhang_shiping" or "su_shuang" => HorseMerchants(c),
            _ => null,
        };

        // ---------- sq.lb1.r7.jixia_questions 稷门问答 ----------

        static IEnumerable<StageStep> Chunyu(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (ch.Has(Jixia))
            {
                yield return L("chunyu_weng", ch.Node >= ChronicleNode.N8 ? (Recall(ch, "chunyu_weng", "last_answer") == "people" ? "dlg.lb1.chunyu_weng.N8.people" : "dlg.lb1.chunyu_weng.N8.1") : "sq.r7.chunyu.after");
                yield break;
            }
            if (!ch.Has("sq.r7.jixia_met"))
                foreach (var s in Lines("chunyu_weng", "dlg.lb1.chunyu_weng.N7.1", "chunyu_weng", "dlg.lb1.chunyu_weng.N7.2", "jian_yong", "dlg.lb1.chunyu_weng.N7.3",
                             "chunyu_weng", "dlg.lb1.chunyu_weng.N7.4"))
                    yield return s;
            yield return Flag("sq.r7.jixia_met");
            if (!ch.Has(Q1))
            {
                yield return Choice("chunyu_weng", "sq.r7.q1", "sq_q1_", new[] { "sq.r7.q1.a", "sq.r7.q1.b", "sq.r7.q1.c" },
                    new[] { L("chunyu_weng", "sq.r7.q.wrong1") },
                    new[] { L("chunyu_weng", ch.Has(Stele) ? "dlg.lb1.chunyu_weng.q1.ok" : "sq.r7.q1.lucky"), Flag(Q1) },
                    new[] { L("chunyu_weng", "sq.r7.q.wrong2") });
                if (!ch.Has(Q1)) { yield return L("system", "sq.r7.q1.hint"); yield break; }
            }
            if (!ch.Has(Q2))
            {
                yield return L("chunyu_weng", "sq.r7.q2");
                yield return Choice("chunyu_weng", "sq.r7.q2.prompt", "sq_q2_", new[] { "sq.r7.q2.a", "sq.r7.q2.b", "sq.r7.q2.c" },
                    new[] { L("chunyu_weng", "sq.r7.q.wrong3") },
                    new[] { L("chunyu_weng", "sq.r7.q2.ok"), Flag(Q2) },
                    new[] { L("chunyu_weng", "sq.r7.q.wrong1") });
                if (!ch.Has(Q2)) yield break;
            }
            yield return L("chunyu_weng", "sq.r7.q3");
            yield return Choice("liu_bei", "sq.r7.q3.prompt", "sq_q3_", new[] { "sq.r7.q3.people", "sq.r7.q3.arms", "sq.r7.q3.unknown" },
                new[] { L("chunyu_weng", "sq.r7.q3.people.1"), Mem("chunyu_weng", "last_answer", "people") },
                new[] { L("chunyu_weng", "sq.r7.q3.arms.1"), Mem("chunyu_weng", "last_answer", "arms") },
                new[] { L("chunyu_weng", "sq.r7.q3.unknown.1"), Mem("chunyu_weng", "last_answer", "unknown") });
            foreach (var s in Lines("chunyu_weng", "dlg.lb1.chunyu_weng.teach.1", "chunyu_weng", "dlg.lb1.chunyu_weng.teach.2", "jian_yong", "dlg.lb1.chunyu_weng.teach.3"))
                yield return s;
            yield return Mem("jian_yong", "three_questions", "learned");
            yield return Flag(Jixia);
            yield return L("system", "sq.r7.jixia.up");
        }

        // ---------- sq.lb1.r7.jingwang_shrine 景王祠 ----------

        static IEnumerable<StageStep> YanBo(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (ch.Has(ShrineDone) || ch.Has(Shrine))
            {
                yield return L("yan_bo", ch.Node >= ChronicleNode.N8 ? "dlg.lb1.npc.r7.jinan.old_clerk.N8.1" : "dlg.lb1.npc.r7.jinan.old_clerk.N7.6");
                yield break;
            }
            for (int i = 1; i <= 6; i++) yield return L("yan_bo", "dlg.lb1.npc.r7.jinan.old_clerk.N7." + i);
            yield return Flag(Shrine);
            yield return L("system", "sq.r7.shrine.goal");
        }

        static IEnumerable<StageStep> ShrineSteps(FieldStoryController c)
        {
            foreach (var s in Lines("narrator", "sq.r7.shrine.1", "narrator", "sq.r7.shrine.2", "narrator", "sq.r7.shrine.3", "narrator", "sq.r7.shrine.4"))
                yield return s;
            yield return Choice("liu_bei", "sq.r7.shrine.prompt", "sq_shrine_", new[] { "sq.r7.shrine.A", "sq.r7.shrine.B", "sq.r7.shrine.C" },
                new[] { L("narrator", "sq.r7.shrine.A1"), L("yan_bo", "dlg.lb1.npc.r7.jinan.old_clerk.shrine.A"), Reputation(1), Flag("jingwang.demolish"), Mem("liu_bei", "jingwang", "demolish") },
                new[] { L("narrator", "sq.r7.shrine.B1"), L("yan_bo", "dlg.lb1.npc.r7.jinan.old_clerk.shrine.B"), Reputation(1), Flag("jingwang.expose"), Mem("liu_bei", "jingwang", "expose") },
                new[] { L("yan_bo", "dlg.lb1.npc.r7.jinan.old_clerk.shrine.C"), Flag("jingwang.ignore"), Mem("liu_bei", "jingwang", "ignore") });
            yield return Flag(ShrineDone);
        }

        // ---------- sq.lb1.L6.xiami_sandals 下密的草鞋 ----------

        static IEnumerable<StageStep> BuHeng(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (ch.Has(Sandals)) { yield return L("bu_heng", ch.Node >= ChronicleNode.N8 ? "dlg.lb1.bu_heng.N8.1" : "sq.r7.buheng.after"); yield break; }
            foreach (var s in Lines("bu_heng", "dlg.lb1.bu_heng.N7.1", "bu_heng", "dlg.lb1.bu_heng.N7.2", "bu_heng", "dlg.lb1.bu_heng.N7.3",
                         "narrator", "sq.r7.sandals.beam", "liu_bei", "dlg.lb1.bu_heng.N7.4", "guan_yu", "dlg.lb1.guan_yu.L6.xiami", "zhang_fei", "dlg.lb1.zhang_fei.L6.xiami"))
                yield return s;
            yield return Choice("liu_bei", "sq.r7.sandals.prompt", "sq_sandals_", new[] { "sq.r7.sandals.A", "sq.r7.sandals.B", "sq.r7.sandals.C" },
                new[] { L("narrator", "sq.r7.sandals.A1"), L("bu_heng", "dlg.lb1.bu_heng.L6.A"), Mem("bu_heng", "sandals", "wore") },
                new[] { L("liu_bei", "dlg.lb1.bu_heng.L6.B"), L("bu_heng", "sq.r7.sandals.B2"), Mem("bu_heng", "sandals", "left") },
                new[] { L("narrator", "sq.r7.sandals.C1"), L("bu_heng", "dlg.lb1.bu_heng.L6.C"), Mem("bu_heng", "sandals", "wove_new") });
            yield return Mem("liu_bei", "L6_xiami", "visited");
            yield return Flag(Sandals);
        }

        // ---------- sq.lb1.L1.kong_beihai 鼓叟说书·孔北海 ----------

        static IEnumerable<StageStep> GuSou(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            string tale = Recall(ch, "gu_sou", "anxi_tale");
            yield return L("gu_sou", tale == "truth" ? "dlg.lb1.gu_sou.kbh.open.truth" : "sq.r7.kbh.open.letbe");
            if (Recall(ch, "gu_sou", "gaosu") == "shielded") yield return L("gu_sou", "sq.r7.kbh.open.shield");
            foreach (var s in Lines("gu_sou", "dlg.lb1.gu_sou.kbh.open.1", "gu_sou", "dlg.lb1.gu_sou.kbh.open.2", "gu_sou", "dlg.lb1.gu_sou.kbh.open.3", "gu_sou", "sq.r7.kbh.4"))
                yield return s;
            yield return Choice("liu_bei", "sq.r7.kbh.prompt", "sq_kbh_", new[] { "sq.r7.kbh.A", "sq.r7.kbh.B" },
                new[] { L("liu_bei", "sq.r7.kbh.A1"), L("gu_sou", "sq.r7.kbh.A2"), Mem("gu_sou", "kong_beihai", "surprised") },
                new[] { L("narrator", "sq.r7.kbh.B1"), L("gu_sou", "sq.r7.kbh.B2"), Mem("gu_sou", "kong_beihai", "silent") });
            yield return Flag(Book);
            yield return Hide("gu_sou");
            yield return L("system", "sq.book.7");
        }

        // ---------- sq.lb1.L5.jinan_road 济南道马队（降级版） ----------

        static IEnumerable<StageStep> HorseMerchants(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (ch.Has(Horses)) { yield return L("zhang_shiping", "sq.r7.horses.after"); yield break; }
            yield return L("zhang_shiping", "sq.r7.horses.road");
            yield return L("zhang_shiping", "dlg.lb1.zhang_shiping.L5.horses.1");
            string wine = Recall(ch, "su_shuang", "anxi_wine");
            yield return L("su_shuang", wine == "drank" ? "dlg.lb1.su_shuang.L5.wine_yes" : wine == "declined" ? "dlg.lb1.su_shuang.L5.wine_no" : "dlg.lb1.su_shuang.L5.wine_missed");
            yield return L("zhang_shiping", "dlg.lb1.zhang_shiping.L5.horses.2");
            var run = c.Run;
            var favor = new List<StageStep>
            {
                L("liu_bei", "dlg.lb1.liu_bei.L5.favor"), L("zhang_shiping", "dlg.lb1.zhang_shiping.L5.favor"), Mem("zhang_shiping", "horse_deal", "favor"),
            };
            var paid = new List<StageStep>
            {
                L("liu_bei", "dlg.lb1.liu_bei.L5.paid"), Call(() => run.Progress.Inventory.SpendCopper(100)), L("zhang_shiping", "dlg.lb1.zhang_shiping.L5.paid"),
                Mem("zhang_shiping", "horse_deal", "paid"),
            };
            var refused = new List<StageStep>
            {
                L("liu_bei", "dlg.lb1.liu_bei.L5.refused"), L("su_shuang", "dlg.lb1.su_shuang.L5.refused"), Reputation(1), Mem("zhang_shiping", "horse_deal", "refused"),
            };
            if (wine == "declined")
                yield return Choice("liu_bei", "sq.r7.horses.prompt", "sq_horses_", new[] { "sq.r7.horses.paid", "sq.r7.horses.refused" }, paid.ToArray(), refused.ToArray());
            else
                yield return Choice("liu_bei", "sq.r7.horses.prompt", "sq_horses_", new[] { "sq.r7.horses.favor", "sq.r7.horses.paid", "sq.r7.horses.refused" },
                    favor.ToArray(), paid.ToArray(), refused.ToArray());
            yield return Mem("zhang_shiping", "jinan_road", "refused");
            yield return Flag(Horses);
            yield return L("system", "sq.r7.horses.note");
        }
    }
}
