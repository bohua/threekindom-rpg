using System.Collections.Generic;
using ThreeKingdoms.Core.Story;
using static ThreeKingdoms.Gameplay.Story.S;

namespace ThreeKingdoms.Gameplay.Story
{
    // The R5a open region (Suanzao, Hulao, the Bian river), reached by region travel. Core layer here: 汴水夜话, the
    // boatman Lao Hang who saw Cao Hong give Cao Cao his horse (opens after the Bian river, N5; CD M4: not before Hulao).
    // World X / Z of Region_R5A: the Bian river ford (288, 127), the fisher's house beside it (351, 124).
    public static class SideQuestsR5aWild
    {
        public static bool Mine(FieldStoryController c) => c.Location == FieldStoryController.Place.R5a;

        const string Heard = "lb1.r5a.bian_horse_heard";

        public static bool? Present(FieldStoryController c, string id) => id == "lao_hang" ? true : null;

        public static string Prompt(FieldStoryController c, string id) =>
            id == "lao_hang" && c.Run.Chapter2.Node >= ChronicleNode.N5 && !c.Run.Chapter2.Has(Heard) ? "raw:[确认] 与汴水船夫交谈" : null;

        public static bool Marked(FieldStoryController c, string id) =>
            id == "lao_hang" && c.Run.Chapter2.Node >= ChronicleNode.N5 && !c.Run.Chapter2.Has(Heard);

        public static IEnumerable<StageStep> Talk(FieldStoryController c, string id) => id == "lao_hang" ? LaoHang(c) : null;

        static IEnumerable<StageStep> LaoHang(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (ch.Node < ChronicleNode.N5) { yield return L("lao_hang", "dlg.lb1.npc.r5a.bianshui.boatman.N4.1"); yield break; }
            if (ch.Has(Heard)) { yield return L("lao_hang", "dlg.lb1.npc.r5a.bianshui.boatman.N5.2"); yield break; }
            foreach (var s in Lines("lao_hang", "sq.r5a.bian.0", "lao_hang", "sq.r5a.bian.1", "lao_hang", "sq.r5a.bian.2", "lao_hang", "sq.r5a.bian.3", "lao_hang", "sq.r5a.bian.4"))
                yield return s;
            yield return Choice("liu_bei", "sq.r5a.bian.prompt", "sq_bian_", new[] { "sq.r5a.bian.believe", "sq.r5a.bian.guan" },
                new[] { L("liu_bei", "sq.r5a.bian.believe.1") },
                new[] { L("guan_yu", "sq.r5a.bian.guan.1") });
            yield return L("lao_hang", "dlg.lb1.npc.r5a.bianshui.boatman.N5.1");
            yield return Flag(Heard);
            yield return L("system", "sq.r5a.bian.card");
        }
    }
}
