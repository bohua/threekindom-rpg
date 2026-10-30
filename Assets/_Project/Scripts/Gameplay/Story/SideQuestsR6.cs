using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Story;
using static ThreeKingdoms.Gameplay.Story.S;
using static ThreeKingdoms.Gameplay.Story.SideQuests;

namespace ThreeKingdoms.Gameplay.Story
{
    // R6 Yan province (design/chapter1-rise/npc-sidequests/R6_R7_yanzhou_qingzhou.md §R6), a danger area from 192 (the
    // Yan pass from the Qingzhou aide at Pingyuan). Core layer, both after Cao Song's death (N7, the writers' x14_seen):
    // 鼓叟说书·缟素 (L1 ⑥, Puyang's market) and 华费之间的新坟 (three tellings of one death, none judged). World X / Z of
    // Region_R6: Puyang (230, 235), the grave between Hua and Fei at the Taishan road (435, 100).
    public static class SideQuestsR6
    {
        public static bool Mine(FieldStoryController c) => c.Location == FieldStoryController.Place.R6;

        const string Book = "sq.r6.gaosu", Tales = "sq.r6.tales", Told = "sq.r6.told.";
        static bool N7(FieldStoryController c) => c.Run.Chapter2.Node >= ChronicleNode.N7;
        static readonly string[] Tellers = { "huafei_headman", "huafei_peddler", "taishan_woodcutter" };

        public static bool? Present(FieldStoryController c, string id) => id switch
        {
            "gu_sou" => N7(c) && !c.Run.Chapter2.Has(Book),
            "huafei_headman" or "huafei_peddler" or "taishan_woodcutter" => true,
            "puyang_patrol" => false,
            _ => null,
        };

        public static string Prompt(FieldStoryController c, string id) => id switch
        {
            "gu_sou" => "raw:[确认] 听鼓叟说书",
            _ => null,
        };

        public static bool Marked(FieldStoryController c, string id)
        {
            var ch = c.Run.Chapter2;
            if (id == "gu_sou") return N7(c) && !ch.Has(Book);
            if (Tellers.Contains(id)) return N7(c) && !ch.Has(Tales) && !ch.Has(Told + id);
            return false;
        }

        public static IEnumerable<StageStep> Talk(FieldStoryController c, string id)
        {
            if (id == "gu_sou") return GuSou(c);
            if (Tellers.Contains(id)) return Teller(c, id);
            return null;
        }

        // ---------- sq.lb1.L1.gaosu 鼓叟说书·缟素 ----------

        static IEnumerable<StageStep> GuSou(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (ch.Has(Book)) yield break;
            for (int i = 1; i <= 3; i++) yield return L("gu_sou", "dlg.lb1.gu_sou.gaosu." + i);
            yield return Actor("puyang_patrol", null, 233f, 240f, "front");
            yield return L("puyang_patrol", "dlg.lb1.npc.r6.puyang.soldier.gaosu");
            yield return L("narrator", "sq.r6.gaosu.scatter");
            yield return Choice("liu_bei", "sq.r6.gaosu.prompt", "sq_gaosu_", new[] { "sq.r6.gaosu.A", "sq.r6.gaosu.B" },
                new[]
                {
                    L("liu_bei", "dlg.lb1.npc.r6.puyang.soldier.A"), L("narrator", "sq.r6.gaosu.pass"), Hide("puyang_patrol"),
                    L("narrator", "sq.r6.gaosu.gate"), L("gu_sou", "dlg.lb1.npc.r6.puyang.soldier.4"), L("gu_sou", "sq.r6.gaosu.A2"),
                    Mem("gu_sou", "gaosu", "shielded"),
                },
                new[]
                {
                    Hide("puyang_patrol"), L("narrator", "sq.r6.gaosu.gate"), L("gu_sou", "dlg.lb1.npc.r6.puyang.soldier.4"),
                    L("gu_sou", "dlg.lb1.npc.r6.puyang.soldier.B"), Mem("gu_sou", "gaosu", "stood_by"),
                });
            yield return Flag(Book);
            yield return Hide("gu_sou");
            yield return L("system", "sq.book.6");
        }

        // ---------- sq.lb1.r6.huafei_grave 华费之间的新坟 ----------

        static IEnumerable<StageStep> Teller(FieldStoryController c, string id)
        {
            var ch = c.Run.Chapter2;
            int n = (int)ch.Node;
            switch (id)
            {
                case "huafei_headman":
                    if (n < 7) { yield return L(id, "sq.r6.headman.n6"); yield break; }
                    if (n >= 8 && ch.Has(Tales)) { yield return L(id, "dlg.lb1.npc.r6.huafei.headman.N8.1"); yield break; }
                    yield return L(id, "dlg.lb1.npc.r6.huafei.headman.N7.1");
                    yield return L(id, "dlg.lb1.npc.r6.huafei.headman.N7.2");
                    break;
                case "huafei_peddler":
                    if (n < 7) { yield return L(id, "dlg.lb1.npc.r6.huafei.peddler.N6.1"); yield break; }
                    if (n >= 8 && ch.Has(Tales)) { yield return L(id, "dlg.lb1.npc.r6.huafei.peddler.N8.1"); yield break; }
                    yield return L(id, "dlg.lb1.npc.r6.huafei.peddler.N7.1");
                    break;
                default:
                    if (n < 7) { yield return L(id, "sq.r6.woodcutter.n6"); yield break; }
                    yield return L(id, "dlg.lb1.npc.r6.taishan.woodcutter.N7.1");
                    yield return L(id, "dlg.lb1.npc.r6.taishan.woodcutter.N7.2");
                    break;
            }
            if (ch.Has(Tales)) yield break;
            yield return Flag(Told + id);
            // The three heard: Jian Yong asks how to write it down.
            if (!Tellers.All(t => t == id || ch.Has(Told + t))) { yield return L("jian_yong", "sq.r6.tales.more"); yield break; }
            yield return L("jian_yong", "dlg.lb1.jian_yong.r6.grave.1");
            yield return Choice("liu_bei", "sq.r6.tales.prompt", "sq_tales_", new[] { "sq.r6.tales.all", "sq.r6.tales.official" },
                new[] { L("liu_bei", "dlg.lb1.jian_yong.r6.grave.A"), L("jian_yong", "sq.r6.tales.A2"), Mem("jian_yong", "caosong_three_tales", "all_three") },
                new[] { L("liu_bei", "dlg.lb1.jian_yong.r6.grave.B"), L("jian_yong", "sq.r6.tales.B2"), Mem("jian_yong", "caosong_three_tales", "official_only") });
            yield return Choice("liu_bei", "sq.r6.toast.prompt", "sq_toast_", new[] { "sq.r6.toast.yes", "sq.r6.toast.no" },
                new[] { L("liu_bei", "dlg.lb1.jian_yong.r6.grave.C"), L("zhang_fei", "sq.r6.toast.zf"), Mem("liu_bei", "toast_at_cao_grave", "yes") },
                new StageStep[0]);
            yield return Flag(Tales);
            yield return L("system", "sq.r6.tales.book");
        }
    }
}
