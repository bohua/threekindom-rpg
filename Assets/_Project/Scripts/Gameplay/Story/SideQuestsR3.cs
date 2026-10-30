using System.Collections.Generic;
using ThreeKingdoms.Core.Story;
using static ThreeKingdoms.Gameplay.Story.S;
using static ThreeKingdoms.Gameplay.Story.SideQuests;

namespace ThreeKingdoms.Gameplay.Story
{
    // R3 Julu and Wei (design/chapter1-rise/npc-sidequests/R3_R4_julu_bohai.md §5), reached by region travel from 191.
    // Core layer: 卢营旧冢 (the old camp's ruin, Zheng Hao's grave) and 一张过所 (the must-do pass lesson at Ye's south gate;
    // the pass itself comes from the Qingzhou aide at Pingyuan, SideQuestsR4). World X / Z of Region_R3: Lu Zhi's old
    // camp (365, 224), Ye (150, 135) with its south gate at z ≈ 112.
    public static class SideQuestsR3
    {
        public static bool Mine(FieldStoryController c) => c.Location == FieldStoryController.Place.R3;

        public const string PassJizhou = "r4.pass_jizhou", PassYanzhou = "r4.pass_yanzhou";
        const string Grave = "r3.grave_visited", GraveMsg = "sq.r3.grave_msg", YeAsked = "sq.r3.ye_asked", YePass = "r3.ye_pass";

        public static bool? Present(FieldStoryController c, string id) => id switch
        {
            "zheng_grave" or "ge_niang" or "ye_captain" => true,
            _ => null,
        };

        public static string Prompt(FieldStoryController c, string id)
        {
            var ch = c.Run.Chapter2;
            return id switch
            {
                "zheng_grave" => ch.Has(Grave) ? null : ch.Has(GraveMsg) ? "raw:[确认] 添土，重写木牌" : "raw:[确认] 看看木牌",
                "ye_captain" => ch.Has(YePass) ? null : "raw:[确认] 进邺城（守门队率）",
                _ => null,
            };
        }

        public static bool Marked(FieldStoryController c, string id)
        {
            var ch = c.Run.Chapter2;
            return id switch
            {
                "zheng_grave" => !ch.Has(Grave),
                "ge_niang" => ch.Has("sq.r3.grave_read") && !ch.Has(GraveMsg),
                "ye_captain" => !ch.Has(YePass) && (!ch.Has(YeAsked) || ch.Has(PassJizhou)),
                _ => false,
            };
        }

        public static IEnumerable<StageStep> Talk(FieldStoryController c, string id) => id switch
        {
            "zheng_grave" => GraveSteps(c),
            "ge_niang" => GeNiang(c),
            "ye_captain" => YeGate(c),
            _ => null,
        };

        // ---------- sq.lb1.r3.old_camp_grave 卢营旧冢 ----------

        static IEnumerable<StageStep> GraveSteps(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (ch.Has(Grave)) { yield return L("narrator", "sq.r3.grave.new"); yield break; }
            if (!ch.Has("sq.r3.grave_read"))
            {
                foreach (var s in Lines("narrator", "dlg.lb1.sq.grave.ruins.1", "narrator", "dlg.lb1.sq.grave.ruins.2", "zhang_fei", "dlg.lb1.sq.grave.ruins.3",
                             "narrator", "dlg.lb1.zheng_hao.N5.grave.1", "liu_bei", "dlg.lb1.sq.grave.liu_bei.1"))
                    yield return s;
                yield return Flag("sq.r3.grave_read");
                if (!ch.Has(GraveMsg)) { yield return L("system", "sq.r3.grave.goal"); yield break; }
            }
            if (!ch.Has(GraveMsg)) { yield return L("narrator", "sq.r3.grave.wait"); yield break; }
            yield return Choice("liu_bei", "sq.r3.grave.prompt", "sq_grave_", new[] { "sq.r3.grave.add", "gs.opt.cancel" },
                new[]
                {
                    Fade(1f, 0.4f), Fade(0f, 0.4f), L("narrator", "sq.r3.grave.board"), Reputation(1),
                    Flag(Grave), Mem("liu_bei", "zheng_hao_grave", "visited"),
                },
                new StageStep[0]);
        }

        static IEnumerable<StageStep> GeNiang(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (ch.Has("sq.r3.grave_read") && !ch.Has(GraveMsg))
            {
                yield return L("ge_niang", "dlg.lb1.npc.r3.liupo.ge_niang.N5.2");
                string gz = Recall(ch, "a_he", "guangzong");
                yield return L("ge_niang", gz == "covered" ? "dlg.lb1.sq.grave.msg.covered" : gz == "punished" ? "dlg.lb1.sq.grave.msg.punished" : "dlg.lb1.sq.grave.msg.none");
                yield return Flag(GraveMsg);
                yield break;
            }
            int n = (int)ch.Node;
            yield return L("ge_niang", n >= 7 ? "dlg.lb1.npc.r3.liupo.ge_niang.N7.1" : n >= 6 ? "dlg.lb1.npc.r3.liupo.ge_niang.N6.1" : "dlg.lb1.npc.r3.liupo.ge_niang.N5.none.1");
        }

        // ---------- sq.lb1.r3.ye_pass 一张过所 ----------

        static IEnumerable<StageStep> YeGate(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            int n = (int)ch.Node;
            if (ch.Has(YePass)) { yield return L("ye_captain", n >= 7 ? "dlg.lb1.npc.r3.ye.gate_captain.N7.1" : n >= 6 ? "dlg.lb1.npc.r3.ye.gate_captain.N6.1" : "dlg.lb1.npc.r3.ye.gate_captain.N5.pass.2"); yield break; }
            if (!ch.Has(PassJizhou))
            {
                foreach (var s in Lines("ye_captain", "dlg.lb1.npc.r3.ye.gate_captain.N5.nopass.1", "ye_captain", "dlg.lb1.npc.r3.ye.gate_captain.N5.nopass.2",
                             "jian_yong", "dlg.lb1.sq.ye_pass.jian_yong.1", "jian_yong", "dlg.lb1.sq.ye_pass.jian_yong.2"))
                    yield return s;
                yield return Flag(YeAsked);
                yield return L("system", "sq.r3.ye.goal");
                yield break;
            }
            yield return L("ye_captain", "dlg.lb1.npc.r3.ye.gate_captain.N5.pass.1");
            yield return L("system", "sq.r3.ye.danger");
            yield return Flag(YePass);
        }
    }
}
