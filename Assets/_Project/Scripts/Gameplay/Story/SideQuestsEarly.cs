using System.Collections.Generic;
using ThreeKingdoms.Core.Story;
using static ThreeKingdoms.Gameplay.Story.S;
using static ThreeKingdoms.Gameplay.Story.SideQuests;

namespace ThreeKingdoms.Gameplay.Story
{
    // The 0.2 scenes' side-quest lines (the bible's "0.2 回填，并入 0.3 第一个冲刺，只加对白"): Lu Zhi's camp (R3,
    // 阿禾 ① 黄天下的饼) and Anxi (R2, 阿禾 ② 婆婆的孙子, 中山马商 ② 中山的酒). Both scenes close behind the player, so
    // a missed quest leaves its memory unwritten and later scenes take their "never met" lines.
    public static class SideQuestsEarly
    {
        public static bool Mine(FieldStoryController c) =>
            c.Location == FieldStoryController.Place.Camp || c.Location == FieldStoryController.Place.Anxi;

        static bool Camp(FieldStoryController c) => c.Location == FieldStoryController.Place.Camp;
        const string Caught = "sq.r3.ahe", Hidden = "sq.r2.ahe", ExtraFood = "sq.r2.ahe_food", Wine = "sq.r2.wine";

        public static bool? Present(FieldStoryController c, string id)
        {
            var ch = c.Run.Chapter2;
            switch (id)
            {
                case "a_he": return Camp(c) ? ch.Has(ChapterScripts.CampGreeted) && !ch.Has(Caught) : ch.Step == ChapterStep.Anxi && ch.Has(ChapterState.Alms);
                case "zheng_hao": return Camp(c);
                case "su_shuang": case "zhang_shiping": return !Camp(c) && ch.Step == ChapterStep.Anxi;
                default: return null;
            }
        }

        public static string Prompt(FieldStoryController c, string id)
        {
            var ch = c.Run.Chapter2;
            if (id == "a_he" && Camp(c)) return "raw:[确认] 帐后有动静";
            if (id == "su_shuang" && !ch.Has(Wine)) return "raw:[确认] 与中山马商交谈";
            return null;
        }

        public static bool Marked(FieldStoryController c, string id)
        {
            var ch = c.Run.Chapter2;
            return id switch
            {
                "a_he" => Camp(c) ? !ch.Has(Caught) : !ch.Has(Hidden),
                "su_shuang" => !ch.Has(Wine),
                _ => false,
            };
        }

        public static IEnumerable<StageStep> Talk(FieldStoryController c, string id) => id switch
        {
            "a_he" => Camp(c) ? Bread(c) : Granny(c),
            "zheng_hao" => ZhengHao(c),
            "su_shuang" or "zhang_shiping" => Merchants(c),
            _ => null,
        };

        // ---------- sq.lb1.L2.a_he_1 黄天下的饼 ----------

        static IEnumerable<StageStep> Bread(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            var run = c.Run;
            if (ch.Has(Caught)) yield break;
            yield return L("narrator", "sq.r3.ahe.scene");
            foreach (var s in Lines("quartermaster", "dlg.lb1.npc.r3.lzcamp.quartermaster.N1.caught.1", "a_he", "dlg.lb1.a_he.N1.caught.1",
                         "wounded_a", "dlg.lb1.npc.r3.lzcamp.wounded_hungry.N1.caught.1", "narrator", "sq.r3.ahe.kneel",
                         "liu_bei", "dlg.lb1.sq.a_he_1.liu_bei.1", "a_he", "dlg.lb1.sq.a_he_1.a_he.1", "liu_bei", "dlg.lb1.sq.a_he_1.liu_bei.2",
                         "a_he", "dlg.lb1.sq.a_he_1.a_he.2", "quartermaster", "dlg.lb1.sq.a_he_1.qm.1"))
                yield return s;
            yield return Choice("liu_bei", "sq.r3.ahe.prompt", "sq_ahe_bread_", new[] { "sq.r3.ahe.cover", "sq.r3.ahe.law" },
                new[]
                {
                    L("liu_bei", "dlg.lb1.sq.a_he_1.liu_bei.A1"), L("zhang_fei", "dlg.lb1.sq.a_he_1.zhang_fei.A1"),
                    L("quartermaster", "dlg.lb1.npc.r3.lzcamp.quartermaster.N1.covered.1"),
                    Call(() => run.Progress.Inventory.SpendCopper(20)), Reputation(1),
                    L("a_he", "dlg.lb1.a_he.N1.covered.1"), L("a_he", "dlg.lb1.a_he.N1.covered.2"),
                    Mem("a_he", "guangzong", "covered"), Mem("liu_bei", "a_he_guangzong", "covered"),
                },
                new[]
                {
                    L("liu_bei", "dlg.lb1.sq.a_he_1.liu_bei.B1"), L("zhang_fei", "dlg.lb1.sq.a_he_1.zhang_fei.B1"), L("guan_yu", "dlg.lb1.sq.a_he_1.guan_yu.B1"),
                    L("quartermaster", "dlg.lb1.npc.r3.lzcamp.quartermaster.N1.punished.1"),
                    L("zheng_hao", "dlg.lb1.zheng_hao.N1.punished.1"), L("a_he", "dlg.lb1.a_he.N1.punished.2"),
                    Mem("a_he", "guangzong", "punished"), Mem("zheng_hao", "released_a_he", "yes"), Mem("liu_bei", "a_he_guangzong", "punished"),
                });
            yield return Flag(Caught);
            yield return Hide("a_he");
            yield return L("system", "sq.r3.ahe.book");
        }

        // 郑蒿, the old soldier of Lu Zhi's camp (R3 §3; his grave is the 卢营旧冢 of 192).
        static IEnumerable<StageStep> ZhengHao(FieldStoryController c)
        {
            string gz = Recall(c, "a_he", "guangzong");
            if (gz == "covered") { yield return L("zheng_hao", "dlg.lb1.zheng_hao.N1.covered.1"); yield break; }
            if (gz == "punished") { yield return L("zheng_hao", "dlg.lb1.zheng_hao.N1.punished.2"); yield break; }
            foreach (var s in Lines("zheng_hao", "dlg.lb1.zheng_hao.N1.1", "zheng_hao", "dlg.lb1.zheng_hao.N1.3")) yield return s;
        }

        // ---------- sq.lb1.L2.a_he_2 婆婆的孙子 ----------

        static IEnumerable<StageStep> Granny(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            var run = c.Run;
            string gz = Recall(c, "a_he", "guangzong");
            if (ch.Has(Hidden))
            {
                yield return L("a_he", gz == "covered" ? "dlg.lb1.a_he.N2.recognized.2" : gz == "punished" ? "dlg.lb1.a_he.N2.hid.2" : "dlg.lb1.a_he.N2.stranger.1");
                yield break;
            }
            yield return L("old_woman", "dlg.lb1.a_he.N2.alms.0");
            if (gz == "covered")
            {
                foreach (var s in Lines("a_he", "dlg.lb1.a_he.N2.recognized.1", "a_he", "dlg.lb1.a_he.N2.recognized.2", "liu_bei", "dlg.lb1.a_he.N2.recognized.lb",
                             "a_he", "dlg.lb1.a_he.N2.recognized.3"))
                    yield return s;
                yield return Mem("a_he", "anxi", "recognized");
            }
            else if (gz == "punished")
            {
                foreach (var s in Lines("old_woman", "dlg.lb1.a_he.N2.hid.1", "a_he", "dlg.lb1.a_he.N2.hid.2")) yield return s;
                yield return Mem("a_he", "anxi", "hid");
            }
            else yield return L("a_he", "dlg.lb1.a_he.N2.stranger.1");
            yield return L("old_woman", "dlg.lb1.a_he.N2.miao.1");
            yield return Flag(Hidden);
            if (run.Progress.Inventory.Rations <= 0) yield break;
            yield return Choice("liu_bei", "sq.r2.ahe.food", "sq_ahe_food_", new[] { "sq.r2.ahe.food.give", "sq.r2.ahe.food.no" },
                new[]
                {
                    Call(() => run.Progress.Inventory.SpendRations(1)),
                    gz == "punished" ? L("old_woman", "sq.r2.ahe.food.granny") : L("a_he", "sq.r2.ahe.food.take"),
                    Mem("a_he", "anxi_extra_food", "yes"),
                },
                new StageStep[0]);
        }

        // ---------- sq.lb1.L5.anxi_horse 中山马商（二）：中山的酒 ----------

        static IEnumerable<StageStep> Merchants(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (ch.Has(Wine)) { yield return L("zhang_shiping", "dlg.lb1.zhang_shiping.N2.1"); yield break; }
            yield return L("zhang_shiping", "dlg.lb1.zhang_shiping.N2.1");
            yield return L("su_shuang", "dlg.lb1.su_shuang.N2.1");
            yield return Choice("liu_bei", "sq.r2.wine.prompt", "sq_wine_", new[] { "sq.r2.wine.drink", "sq.r2.wine.decline" },
                new[]
                {
                    L("liu_bei", "sq.r2.wine.drink.1"), L("su_shuang", "dlg.lb1.su_shuang.N2.drank.1"),
                    L("zhang_shiping", "dlg.lb1.zhang_shiping.N2.drank.1"), L("zhang_shiping", "dlg.lb1.zhang_shiping.N2.drank.2"),
                    Mem("su_shuang", "anxi_wine", "drank"), L("system", "sq.r2.wine.dates"),
                },
                new[]
                {
                    L("liu_bei", "sq.r2.wine.decline.1"), L("su_shuang", "dlg.lb1.su_shuang.N2.declined.1"), L("zhang_shiping", "dlg.lb1.zhang_shiping.N2.declined.1"),
                    Mem("su_shuang", "anxi_wine", "declined"),
                });
            yield return Flag(Wine);
        }
    }
}
