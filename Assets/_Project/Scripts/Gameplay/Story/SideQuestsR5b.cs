using System.Collections.Generic;
using ThreeKingdoms.Core.Story;
using static ThreeKingdoms.Gameplay.Story.S;
using static ThreeKingdoms.Gameplay.Story.SideQuests;

namespace ThreeKingdoms.Gameplay.Story
{
    // R5b Luoyang (design/chapter1-rise/npc-sidequests/R5_suanzao_luoyang.md §6–§8), reached by region travel from 191
    // (N5). Core layer: 鼓叟·一把火 (L1 ④), 卢门·石经摹本 (L4 ③, the fallback: the old scholar hands over the copy, no
    // stele-rubbing steps), 北邙兵书 (must-do, §10.1 #5), 井里只有水 (the fallback: no hidden niche). The writers' small
    // carrying and well-winding interactions are told in lines. World X / Z of Region_R5B: the South Palace (248, 185), the
    // Zhenguan well (298, 143), the Beimang slope above the ruins (250, 278), the tombs' west slope (100–190, 260).
    public static class SideQuestsR5b
    {
        public static bool Mine(FieldStoryController c) => c.Location == FieldStoryController.Place.R5b;

        const string Book = "sq.r5b.book4", Copy = "lb1.L4.rubbing_got", Slip = "lb1.L4.luzhi_slip", NoBook = "lb1.L4.no_book",
            Graves = "sq.r5b.graves", GravesAsked = "sq.r5b.graves_asked", Well = "sq.r5b.well";

        static bool N6(FieldStoryController c) => c.Run.Chapter2.Node >= ChronicleNode.N6;

        public static bool? Present(FieldStoryController c, string id) => id switch
        {
            "gu_sou" or "guan_hu" or "jiang_sanniang" => !N6(c),
            "qi_gong" or "yan_gu" or "fan_wu" => true,
            "mang_digger" => c.Run.Chapter2.Has(GravesAsked) && !c.Run.Chapter2.Has(Graves),
            "yuan_agent" => false,
            _ => null,
        };

        public static string Prompt(FieldStoryController c, string id)
        {
            var ch = c.Run.Chapter2;
            return id switch
            {
                "gu_sou" => ch.Has(Book) ? null : "raw:[确认] 在火边坐下，听鼓叟说书",
                "qi_gong" => ch.Has(Copy) || ch.Has(NoBook) ? null : "raw:[确认] 与太学老博士交谈",
                "yan_gu" => ch.Has(Graves) ? null : "raw:[确认] 与守陵老吏交谈",
                "mang_digger" => "raw:[确认] 与挖冢的饥民交谈",
                "fan_wu" => ch.Has(Well) || N6(c) ? null : "raw:[确认] 与守井老卒交谈",
                _ => null,
            };
        }

        public static bool Marked(FieldStoryController c, string id)
        {
            var ch = c.Run.Chapter2;
            return id switch
            {
                "gu_sou" => !ch.Has(Book),
                "qi_gong" => !ch.Has(Copy) && !ch.Has(NoBook),
                "yan_gu" => !ch.Has(Graves) && !ch.Has(GravesAsked),
                "mang_digger" => true,
                "fan_wu" => !ch.Has(Well) && !N6(c),
                _ => false,
            };
        }

        public static IEnumerable<StageStep> Talk(FieldStoryController c, string id) => id switch
        {
            "gu_sou" => GuSou(c),
            "guan_hu" => new[] { L("guan_hu", "dlg.lb1.npc.r5b.nangong.old_guard.N5." + (c.Run.Chapter2.Has(Book) ? "3" : "1")) },
            "jiang_sanniang" => new[] { L("jiang_sanniang", "dlg.lb1.npc.r5b.nangong.returnee.N5.2") },
            "qi_gong" => QiGong(c),
            "yan_gu" => YanGu(c),
            "mang_digger" => Digger(c),
            "fan_wu" => FanWu(c),
            _ => null,
        };

        // ---------- sq.lb1.L1.yibahuo 鼓叟·一把火 ----------

        static IEnumerable<StageStep> GuSou(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (ch.Has(Book)) { yield return L("gu_sou", "sq.r5b.gu_sou.after"); yield break; }
            for (int i = 1; i <= 5; i++) yield return L("gu_sou", "dlg.lb1.gu_sou.L1.4." + i);
            yield return L("narrator", "sq.r5b.book.cry");
            yield return Choice("liu_bei", "sq.r5b.book.prompt", "sq_book4_", new[] { "sq.r5b.book.stay", "sq.r5b.book.comfort" },
                new[] { L("narrator", "sq.r5b.book.stay.1"), Mem("gu_sou", "heard_luoyang", "stay") },
                new[] { L("liu_bei", "dlg.lb1.liu_bei.L1.4.comfort"), Reputation(1), Mem("gu_sou", "heard_luoyang", "comfort") });
            foreach (var s in Lines("gu_sou", "dlg.lb1.liu_bei.L1.4.6", "gu_sou", "dlg.lb1.liu_bei.L1.4.7", "narrator", "sq.r5b.book.laugh",
                         "gu_sou", "dlg.lb1.liu_bei.L1.4.8", "zhang_fei", "dlg.lb1.zhang_fei.L1.4.1"))
                yield return s;
            yield return Flag(Book);
            yield return L("system", "sq.book.4");
        }

        // ---------- sq.lb1.L4.shijing 卢门·石经摹本 ----------

        static IEnumerable<StageStep> QiGong(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (ch.Has(Copy) || ch.Has(NoBook))
            {
                yield return L("qi_gong", N6(c) ? "dlg.lb1.npc.r5b.nangong.boshi.N6.1" : "dlg.lb1.npc.r5b.nangong.boshi.N5.3");
                yield break;
            }
            yield return L("qi_gong", "dlg.lb1.npc.r5b.nangong.boshi.N5.1");
            yield return L("qi_gong", "dlg.lb1.npc.r5b.nangong.boshi.N5.2");
            if (Recall(ch, "lu_zhi", "cart") != "escort")
            {
                yield return L("liu_bei", "sq.r5b.qi.lu");
                yield return L("qi_gong", "dlg.lb1.npc.r5b.nangong.boshi.N5.nobook");
                yield return Flag(NoBook);
                yield break;
            }
            yield return L("narrator", "sq.r5b.qi.show");
            foreach (var s in Lines("qi_gong", "dlg.lb1.npc.r5b.nangong.boshi.L4.1", "qi_gong", "sq.r5b.qi.2", "qi_gong", "sq.r5b.qi.3", "qi_gong", "sq.r5b.qi.4",
                         "liu_bei", "sq.r5b.qi.ask", "qi_gong", "dlg.lb1.npc.r5b.nangong.boshi.L4.5", "qi_gong", "sq.r5b.qi.copies"))
                yield return s;
            yield return Choice("liu_bei", "sq.r5b.qi.prompt", "sq_qi_", new[] { "sq.r5b.qi.pangeng", "sq.r5b.qi.wuyi" },
                new[] { L("qi_gong", "dlg.lb1.npc.r5b.nangong.boshi.L4.6a"), Mem("liu_bei", "L4_rubbing", "pangeng") },
                new[] { L("qi_gong", "dlg.lb1.npc.r5b.nangong.boshi.L4.6b"), Mem("liu_bei", "L4_rubbing", "wuyi") });
            yield return L("qi_gong", "dlg.lb1.npc.r5b.nangong.boshi.L4.7");
            yield return Flag(Copy);
            yield return Flag(Slip);
            yield return L("system", "sq.r5b.qi.got");
        }

        // ---------- sq.lb1.r5b.mang_bingshu 北邙兵书 ----------

        static IEnumerable<StageStep> YanGu(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (ch.Has(Graves)) { yield return L("yan_gu", N6(c) ? "dlg.lb1.yan_gu.N6.1" : Recall(ch, "yan_gu", "diggers") == "feed" ? "dlg.lb1.yan_gu.done.feed" : "dlg.lb1.yan_gu.done.drive"); yield break; }
            if (ch.Has(GravesAsked))
            {
                if (!ch.Has("sq.r5b.filled")) { yield return L("yan_gu", "dlg.lb1.yan_gu.bs.2"); yield break; }
                // Back at the gate tower: the warden's book of the tomb office.
                foreach (var s in Lines("yan_gu", "dlg.lb1.yan_gu.bs.3", "yan_gu", "sq.r5b.yan.4", "yan_gu", "sq.r5b.yan.5", "liu_bei", "dlg.lb1.liu_bei.mang.name"))
                    yield return s;
                yield return L("yan_gu", Recall(ch, "yan_gu", "diggers") == "feed" ? "sq.r5b.yan.write.feed" : "sq.r5b.yan.write");
                yield return Flag(Graves);
                yield return Mem("yan_gu", "filled_graves", "yes");
                yield return Mem("liu_bei", "mang_name_in_book", "yes");
                foreach (var s in Keepsake("mang_sunzi")) yield return s;
                yield break;
            }
            foreach (var s in Lines("yan_gu", "dlg.lb1.yan_gu.N5.1", "narrator", "sq.r5b.yan.bow", "yan_gu", "dlg.lb1.yan_gu.N5.2", "yan_gu", "dlg.lb1.yan_gu.N5.3",
                         "yan_gu", "dlg.lb1.yan_gu.N5.4", "yan_gu", "dlg.lb1.yan_gu.N5.5", "yan_gu", "dlg.lb1.yan_gu.bs.1", "yan_gu", "dlg.lb1.yan_gu.bs.2"))
                yield return s;
            yield return Flag(GravesAsked);
            yield return L("system", "sq.r5b.yan.goal");
        }

        // The west slope: two holes filled past the scattered grave-robbers, the third with the hungry family.
        static IEnumerable<StageStep> Digger(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            var run = c.Run;
            if (ch.Has("sq.r5b.filled")) yield break;
            yield return L("narrator", "sq.r5b.slope");
            yield return L("mang_digger", "dlg.lb1.npc.r5b.mang.digger.1");
            yield return Choice("liu_bei", "sq.r5b.dig.prompt", "sq_dig_", new[] { "sq.r5b.dig.drive", "sq.r5b.dig.feed" },
                new[] { L("liu_bei", "dlg.lb1.liu_bei.mang.drive"), L("narrator", "sq.r5b.dig.drive.1"), Mem("yan_gu", "diggers", "drive") },
                new[]
                {
                    L("liu_bei", "dlg.lb1.liu_bei.mang.feed"), Call(() => { if (run.Progress.Inventory.Rations > 0) run.Progress.Inventory.SpendRations(1); }),
                    L("narrator", "sq.r5b.dig.feed.1"), Mem("yan_gu", "diggers", "feed"),
                });
            yield return Fade(1f, 0.5f);
            yield return Fade(0f, 0.5f);
            yield return L("narrator", ch.Recall("yan_gu", "diggers") == "feed" ? "sq.r5b.filled.feed" : "sq.r5b.filled");
            yield return Flag("sq.r5b.filled");
            yield return Hide("mang_digger");
            yield return L("system", "sq.r5b.back");
        }

        // ---------- sq.lb1.r5b.zhenguan_well 井里只有水 ----------

        static IEnumerable<StageStep> FanWu(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (ch.Has(Well) || N6(c)) { yield return L("fan_wu", N6(c) ? "dlg.lb1.npc.r5b.zhenguan.well_keeper.N6.1" : "dlg.lb1.npc.r5b.zhenguan.well_keeper.N5.3"); yield break; }
            foreach (var s in Lines("fan_wu", "dlg.lb1.npc.r5b.zhenguan.well_keeper.N5.1", "liu_bei", "sq.r5b.well.water", "fan_wu", "dlg.lb1.npc.r5b.zhenguan.well_keeper.N5.2",
                         "fan_wu", "sq.r5b.well.mud", "zhang_fei", "dlg.lb1.zhang_fei.well.1"))
                yield return s;
            yield return Actor("yuan_agent", null, 300f, 137f, "back");
            yield return L("yuan_agent", "sq.r5b.well.agent");
            yield return L("yuan_agent", "dlg.lb1.npc.r5b.zhenguan.yuan_agent.1");
            yield return Choice("liu_bei", "sq.r5b.well.prompt", "sq_well_", new[] { "sq.r5b.well.truth", "sq.r5b.well.shield" },
                new[] { L("liu_bei", "sq.r5b.well.truth.1"), L("fan_wu", "sq.r5b.well.truth.2"), Flag("lb1.r5b.well_choice.truth") },
                new[] { L("liu_bei", "sq.r5b.well.shield.1"), L("fan_wu", "sq.r5b.well.shield.2"), Flag("lb1.r5b.well_choice.shield") });
            yield return Hide("yuan_agent");
            yield return L("fan_wu", "dlg.lb1.npc.r5b.zhenguan.well_keeper.N5.3");
            yield return L("fan_wu", "dlg.lb1.npc.r5b.zhenguan.well_keeper.done.1");
            yield return Flag(Well);
            yield return L("system", "sq.r5b.well.book");
        }
    }
}
