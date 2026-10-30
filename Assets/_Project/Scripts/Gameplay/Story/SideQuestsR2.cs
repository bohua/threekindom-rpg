using System.Collections.Generic;
using ThreeKingdoms.Core.Story;
using static ThreeKingdoms.Gameplay.Story.S;
using static ThreeKingdoms.Gameplay.Story.SideQuests;

namespace ThreeKingdoms.Gameplay.Story
{
    // R2 Zhongshan and Changshan (design/chapter1-rise/npc-sidequests/R1_R2_zhuoji_zhongshan.md §R2), reached by region
    // travel from 190 (N4, from Gongsun Zan's garrison or the Suanzao camp). Core layer here: 县尉打督邮 (L1 ②, at the
    // Jingxing crossroads post station — Anxi is not revisited, 10_REVIEW §6 ③), the horse pen of 中山的酒 (L5 ②, N4
    // part), 滹沱头钓 (must-do: lines and the fishing-spot marker only; fishing comes with the cooking system). 真定·兄长的
    // 托付 is the main line's Zhending scene (memory zhao_yun.zhaoyun_zhending); 甄家谷筹 needs a walkable Zhending and
    // waits for it. World X / Z of Zhongshan_Region: the Jingxing post station (140, 134), the Hutuo ferry (271, 84).
    public static class SideQuestsR2
    {
        public static bool Mine(FieldStoryController c) => c.Location == FieldStoryController.Place.R2;

        const string Book = "sq.r2.book2", Pen = "sq.r2.pen", Fish = "sq.r2.fish";

        // Before the Suanzao camp breaks: the writers close the tale when Gu Sou leaves for Suanzao.
        static bool TaleOpen(ChapterState ch) => ch.Node == ChronicleNode.N4;

        public static bool? Present(FieldStoryController c, string id) => id switch
        {
            "gu_sou" => TaleOpen(c.Run.Chapter2) && !c.Run.Chapter2.Has(Book),
            "zhang_shiping" or "su_shuang" or "niu_lao" => true,
            _ => null,
        };

        public static string Prompt(FieldStoryController c, string id)
        {
            var ch = c.Run.Chapter2;
            return id switch
            {
                "gu_sou" => "raw:[确认] 听鼓叟说书",
                "niu_lao" => ch.Has(Fish) ? "raw:[确认] 与牛老交谈（钓鱼点：钓鱼玩法随料理系统开放）" : "raw:[确认] 与钓叟交谈",
                _ => null,
            };
        }

        public static bool Marked(FieldStoryController c, string id)
        {
            var ch = c.Run.Chapter2;
            return id switch
            {
                "gu_sou" => true,
                "zhang_shiping" => !ch.Has(Pen),
                "niu_lao" => !ch.Has(Fish),
                _ => false,
            };
        }

        public static IEnumerable<StageStep> Talk(FieldStoryController c, string id) => id switch
        {
            "gu_sou" => GuSou(c),
            "zhang_shiping" or "su_shuang" => HorsePen(c),
            "niu_lao" => NiuLao(c),
            _ => null,
        };

        // ---------- sq.lb1.L1.gu_sou_2 县尉打督邮 ----------

        static IEnumerable<StageStep> GuSou(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (ch.Has(Book)) yield break;
            foreach (var s in Lines("gu_sou", "dlg.lb1.gu_sou.N2.0", "gu_sou", "dlg.lb1.gu_sou.N2.1", "gu_sou", "dlg.lb1.gu_sou.N2.2", "gu_sou", "dlg.lb1.gu_sou.N2.3",
                         "narrator", "dlg.lb1.gu_sou.N2.crowd", "gu_sou", "dlg.lb1.gu_sou.N2.end"))
                yield return s;
            // The 鞭督邮 choice: A Liu Bei beat him himself (history), B Zhang Fei burst in first (the novel).
            bool b = Recall(ch, "liu_bei", "duyou") == "B";
            yield return Choice("liu_bei", "sq.r2.tale.prompt", "sq_tale_", new[] { b ? "sq.r2.tale.truth.b" : "sq.r2.tale.truth.a", "sq.r2.tale.let" },
                b ? new[]
                    {
                        L("liu_bei", "sq.r2.tale.truth.b.1"), L("gu_sou", "dlg.lb1.gu_sou.N2.zhang_fei.truth.1"), L("gu_sou", "dlg.lb1.gu_sou.N2.zhang_fei.truth.2"),
                        Mem("gu_sou", "anxi_tale", "truth"),
                    }
                  : new[]
                    {
                        L("liu_bei", "sq.r2.tale.truth.a.1"), L("gu_sou", "dlg.lb1.gu_sou.N2.self.truth.1"), L("gu_sou", "dlg.lb1.gu_sou.N2.self.truth.2"),
                        L("liu_bei", "dlg.lb1.gu_sou.N2.self.truth.lb"), L("gu_sou", "dlg.lb1.gu_sou.N2.self.truth.3"), Mem("gu_sou", "anxi_tale", "truth"),
                    },
                b ? new[] { L("zhang_fei", "dlg.lb1.gu_sou.N2.zhang_fei.let.zf"), Mem("gu_sou", "anxi_tale", "let_be") }
                  : new[]
                    {
                        L("zhang_fei", "dlg.lb1.gu_sou.N2.self.let.zf"), L("liu_bei", "dlg.lb1.gu_sou.N2.self.let.lb"), L("guan_yu", "dlg.lb1.gu_sou.N2.self.let.gy"),
                        Mem("gu_sou", "anxi_tale", "let_be"), Mem("zhang_fei", "anxi_tale", "credited"),
                    });
            yield return Flag(Book);
            yield return Hide("gu_sou");
            yield return L("system", "sq.book.2");
        }

        // ---------- sq.lb1.L5.anxi_horse 中山马商（二）, the Jingxing horse pen (N4) ----------

        static IEnumerable<StageStep> HorsePen(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            yield return L("zhang_shiping", "dlg.lb1.zhang_shiping.N4.1");
            if (Recall(ch, "su_shuang", "anxi_wine") == "drank") yield return L("su_shuang", "dlg.lb1.su_shuang.N4.drank");
            else if (Recall(ch, "su_shuang", "anxi_wine") == null && !ch.Has(Pen)) yield return Mem("su_shuang", "anxi_wine", "missed");
            yield return Flag(Pen);
        }

        // ---------- sq.lb1.r2.hutuo_fish 滹沱头钓（对白与钓鱼点预留） ----------

        static IEnumerable<StageStep> NiuLao(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (ch.Has(Fish)) { yield return L("niu_lao", "dlg.lb1.hutuo_fish.s1.2"); yield break; }
            foreach (var s in Lines("niu_lao", "dlg.lb1.hutuo_fish.s1.1", "niu_lao", "dlg.lb1.hutuo_fish.s1.2", "narrator", "sq.r2.fish.catch",
                         "jian_yong", "dlg.lb1.hutuo_fish.s3.jy", "niu_lao", "dlg.lb1.hutuo_fish.s4.1", "jian_yong", "dlg.lb1.hutuo_fish.s4.jy"))
                yield return s;
            yield return Mem("jian_yong", "first_fish", "hutuo");
            yield return Flag(Fish);
            yield return L("system", "sq.r2.fish.note");
        }
    }
}
