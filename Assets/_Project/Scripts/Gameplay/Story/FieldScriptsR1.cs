using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Save;
using static ThreeKingdoms.Gameplay.Story.S;

namespace ThreeKingdoms.Gameplay.Story
{
    // The R1 涿蓟 map's side quests (design/chapter1-rise/npc-sidequests R1_R2 §R1.2/§R1.4, core layer; user 2026-10-29),
    // played by a FieldStoryController laid over the region scene (ChapterFieldBuilderR1, as the 0.4 Pingyuan base over
    // the Bohai plain). World X / Z of Region_R1: Ji's centre (368, 219), south gate z 184, prefecture steps z ≈ 222,
    // the 燕台 inn by the west street (356, 217), the porridge stall in the west market (342, 201), the east gate x ≈ 407;
    // Lousang's centre (170, 64), the great mulberry (182, 80).
    //   · sq.lb1.r1.jian_yong_ear 简雍打听 (must-do, 190): Jian Yong sends Liu Bei round the inn, the porridge stall and the
    //     east gate; at the prefecture steps he sums up and the 天下传闻 panel opens for the first time with two rumours.
    //   · sq.lb1.r1.hu_market 胡市马契 (core): the Wuhuan trader Kumo's two horses held by the camp; the quartermaster at
    //     Gongsun Zan's garrison (FieldScripts3) sends Liu Bei either to Guan Jing (pays 60 copper) or to Liu Yu (his writ).
    //   · sq.lb1.L6.lousang 旧地（一）：桑下 (core): Liu Bei's mother, his uncle Zijing under the mulberry (a choice), Liu
    //     Yuanqi and his wife, Liu Deran at the village school, then the mulberry and the childhood memory.
    public static class FieldScriptsR1
    {
        public const string RegionLocation = SaveData.RegionPrefix + "R1";

        public const string JianYongEar = "lb1.r1.jian_yong_ear", EarStarted = "jy_ear_started",
            EarInn = "jy_ear_inn", EarPorridge = "jy_ear_porridge", EarEastGate = "jy_ear_east";
        public const string RumourJiKind = "rumor_ji_liu_yu", RumourJiWar = "rumor_ji_gongsun";
        public const string HuMarket = "lb1.r1.hu_market", HuStarted = "hu_started", HuCampRule = "hu_camp_rule", HuLiuYu = "hu_liu_yu";
        public const string Lousang = "lb1.L6.lousang", LsZijing = "ls_zijing", LsYuanqi = "ls_yuanqi", LsDeran = "ls_deran";
        public const string LiuYuMet = "liu_yu_met";
        public const string RumourPanel = "rumors";

        public static bool Mine(FieldStoryController c) => c.Location == FieldStoryController.Place.R1;

        static int Node(FieldStoryController c) => (int)c.Run.Chapter2.Node;
        static bool Has(FieldStoryController c, string f) => c.Run.Chapter2.Has(f);

        // Who stands on the map at the current node and stage of each quest.
        static bool Present(FieldStoryController c, string id)
        {
            int n = Node(c);
            bool ear = c.Run.Chapter2.Step == ChapterStep.Gongsun && !Has(c, JianYongEar);
            return id switch
            {
                "ji_jian_yong" => ear,
                "ji_jian_yong_hall" => ear && Has(c, EarInn) && Has(c, EarPorridge) && Has(c, EarEastGate),
                "liu_yu" or "kumo" => n >= 4 && n <= 6,
                _ when id.StartsWith("ji_") || id.StartsWith("ls_") => n >= 4,
                _ => true,
            };
        }

        static StageStep Refresh(FieldStoryController c) => Call(() =>
        {
            foreach (var t in c.People) if (t) t.gameObject.SetActive(Present(c, FieldStoryController.IdOf(t)));
        });

        // Came by the realm map (RegionTravel, from 191): the arrival autosave and the first-visit card, as in the other
        // regions; from Gongsun Zan's garrison in 190 the garrison is still the save point.
        public static IEnumerable<StageStep> OnArrive(FieldStoryController c)
        {
            yield return Refresh(c);
            if (c.Run.Chapter2.Step == ChapterStep.Gongsun) yield break;
            foreach (var s in FieldScriptsRegions.OnArrive(c)) yield return s;
        }

        public static string Prompt(FieldStoryController c, string id) => id switch
        {
            FieldScriptsRegions.Courier => FieldScriptsRegions.Prompt(c, id),
            "ls_tree" => Has(c, Lousang) || !(Has(c, LsZijing) && Has(c, LsYuanqi) && Has(c, LsDeran)) ? null : "raw:[确认] 在大桑树下站一会儿",
            _ => "talk:" + id,
        };

        public static bool Marked(FieldStoryController c, string id)
        {
            bool ear = !Has(c, JianYongEar) && Has(c, EarStarted);
            return id switch
            {
                "ji_jian_yong" => !Has(c, EarStarted),
                "ji_jian_yong_hall" => true,
                "ji_guest_camp" => ear && !Has(c, EarInn),
                "ji_refugee" => ear && !Has(c, EarPorridge),
                "ji_east_gate" => ear && !Has(c, EarEastGate),
                "liu_yu" => !Has(c, LiuYuMet) || Has(c, HuStarted) && !Has(c, HuMarket) && !Has(c, HuCampRule) && !Has(c, HuLiuYu) && Has(c, "hu_choice_1"),
                "kumo" => !Has(c, HuMarket) && (!Has(c, HuStarted) || Has(c, HuCampRule) || Has(c, HuLiuYu)),
                "ls_zijing" => !Has(c, LsZijing),
                "ls_yuanqi" => !Has(c, LsYuanqi),
                "ls_deran" => !Has(c, LsDeran),
                "ls_tree" => Prompt(c, id) != null,
                _ => false,
            };
        }

        public static string Objective(FieldStoryController c)
        {
            if (c.Run.Chapter2.Step == ChapterStep.Gongsun && !Has(c, JianYongEar))
                return !Has(c, EarStarted) ? "目标：在蓟城南门内找简雍"
                    : Present(c, "ji_jian_yong_hall") ? "目标：到州府台阶下与简雍会合" : "目标：到燕台酒楼、市集粥棚、东便门听听城里的话";
            return null;
        }

        public static IEnumerable<StageStep> Talk(FieldStoryController c, string id)
        {
            switch (id)
            {
                case FieldScriptsRegions.Courier: return FieldScriptsRegions.Talk(c, id);   // the realm map and saving

                // ---------- Ji ----------
                case "ji_jian_yong": return JianYongStart(c);
                case "ji_jian_yong_hall": return JianYongSummary(c);
                case "liu_yu": return LiuYu(c);
                case "ji_gate_clerk": return new[] { L("ji_gate_clerk", "dlg.lb1.npc.r1.ji.gate_clerk.N4.1") };
                case "ji_inn_keeper": return new[] { L("ji_inn_keeper", "dlg.lb1.npc.r1.ji.inn_keeper.N4.1") };
                case "ji_guest_camp": return Overheard(c, EarInn, "ji_guest_camp", "dlg.lb1.jian_yong_ear.s1.camp", "ji_guest_state", "dlg.lb1.jian_yong_ear.s1.state");
                case "ji_guest_state": return Overheard(c, EarInn, "ji_guest_camp", "dlg.lb1.jian_yong_ear.s1.camp", "ji_guest_state", "dlg.lb1.jian_yong_ear.s1.state");
                case "ji_refugee": return Overheard(c, EarPorridge, "ji_refugee", "dlg.lb1.npc.r1.ji.refugee.N4", null, null);
                case "ji_east_gate": return Overheard(c, EarEastGate, "ji_east_gate", "dlg.lb1.npc.r1.ji.east_gate.N4", "ji_east_gate", "dlg.lb1.npc.r1.ji.east_gate.N5");
                case "kumo": return Kumo(c);

                // ---------- Lousang ----------
                case "ls_mother": return Has(c, "ls_mother")
                    ? new[] { L("liu_bei_mu", "dlg.lb1.liu_bei_mu.N4.3") }
                    : new[] { L("liu_bei_mu", "dlg.lb1.liu_bei_mu.N4.1"), L("liu_bei_mu", "dlg.lb1.liu_bei_mu.N4.2"), L("liu_bei_mu", "dlg.lb1.liu_bei_mu.N4.3"), Flag("ls_mother") };
                case "ls_zijing": return Zijing(c);
                case "ls_yuanqi": case "ls_yuanqi_wife": return Yuanqi(c);
                case "ls_deran": return Has(c, LsDeran)
                    ? new[] { L("liu_deran", "dlg.lb1.liu_deran.N4.3") }
                    : new[] { L("liu_deran", "dlg.lb1.liu_deran.N4.1"), L("liu_deran", "dlg.lb1.liu_deran.N4.2"), L("liu_deran", "dlg.lb1.liu_deran.N4.3"), Flag(LsDeran) };
                case "ls_tree": return Has(c, Lousang) || !(Has(c, LsZijing) && Has(c, LsYuanqi) && Has(c, LsDeran)) ? null : Mulberry(c);
            }
            return null;
        }

        // ---------- 简雍打听 ----------

        static IEnumerable<StageStep> JianYongStart(FieldStoryController c)
        {
            if (Has(c, EarStarted)) { yield return L("jian_yong", "dlg.lb1.jian_yong_ear.wait"); yield break; }
            foreach (var s in Lines("jian_yong", "dlg.lb1.jian_yong.N4.1", "jian_yong", "dlg.lb1.jian_yong_ear.s0.1")) yield return s;
            yield return Flag(EarStarted);
        }

        // One stop of the errand: what is said there, heard once; the hall summary appears after the third.
        static IEnumerable<StageStep> Overheard(FieldStoryController c, string flag, string who1, string key1, string who2, string key2)
        {
            yield return L(who1, key1);
            if (who2 != null) yield return L(who2, key2);
            if (!Has(c, EarStarted) || Has(c, flag)) yield break;
            yield return Flag(flag);
            yield return Refresh(c);
        }

        static IEnumerable<StageStep> JianYongSummary(FieldStoryController c)
        {
            foreach (var s in Lines("jian_yong", "dlg.lb1.jian_yong_ear.s4.1", "liu_bei", "dlg.lb1.jian_yong_ear.s4.lb", "jian_yong", "dlg.lb1.jian_yong_ear.s4.2"))
                yield return s;
            yield return Flag(RumourJiKind);
            yield return Flag(RumourJiWar);
            yield return Flag(JianYongEar);
            yield return Remember("jian_yong", "first_rumor", "done", "mem.jian_yong_first_rumor");
            yield return L("system", "dlg.lb1.jian_yong_ear.unlocked");
            yield return Refresh(c);
            yield return Call(() => c.pendingPanel = RumourPanel);
        }

        // ---------- 刘虞 (剧本第四节 5) and his half of 胡市马契 ----------

        static IEnumerable<StageStep> LiuYu(FieldStoryController c)
        {
            if (Has(c, HuStarted) && Has(c, "hu_choice_1") && !Has(c, HuLiuYu) && !Has(c, HuCampRule))
            {
                yield return L("liu_yu", "dlg.lb1.hu_market.liu_yu.1");
                yield return Flag(HuLiuYu);
                yield return Remember("liu_yu", "sent_writ", "yes", "mem.liu_yu_sent_writ");
                yield return L("system", "dlg.lb1.hu_market.writ");
                yield break;
            }
            if (Has(c, LiuYuMet)) { yield return L("liu_yu", "dlg.lb1.liu_yu.N4.idle"); yield break; }
            foreach (var s in Lines("liu_yu", "dlg.lb1.liu_yu.N4.1", "liu_bei", "dlg.lb1.liu_yu.N4.ask", "liu_yu", "dlg.lb1.liu_yu.N4.2",
                         "liu_yu", "dlg.lb1.liu_yu.N4.3", "liu_yu", "dlg.lb1.liu_yu.N4.4"))
                yield return s;
            yield return Flag(LiuYuMet);
            yield return Remember("liu_yu", "asked_people", "yes", "mem.liu_yu_asked_people");
        }

        // ---------- 胡市马契 (Kumo at Ji's east gate) ----------

        static IEnumerable<StageStep> Kumo(FieldStoryController c)
        {
            if (Has(c, HuMarket)) { yield return L("kumo", "dlg.lb1.npc.r1.ji.wuhuan_trader.N4.1"); yield break; }
            if (Has(c, HuCampRule) || Has(c, HuLiuYu))
            {
                yield return L("kumo", "dlg.lb1.hu_market.end.1");
                yield return Flag(HuMarket);
                yield return Flag(HuMarket + (Has(c, HuCampRule) ? "=camp_rule" : "=liu_yu"));
                yield return Reputation(1);
                yield return L("system", "dlg.lb1.hu_market.keepsake");
                yield break;
            }
            if (Has(c, HuStarted)) { yield return L("kumo", "dlg.lb1.npc.r1.ji.wuhuan_trader.N4.2"); yield break; }
            foreach (var s in Lines("kumo", "dlg.lb1.npc.r1.ji.wuhuan_trader.N4.1", "kumo", "dlg.lb1.hu_market.s1.1", "kumo", "dlg.lb1.npc.r1.ji.wuhuan_trader.N4.2"))
                yield return s;
            yield return Flag(HuStarted);
            yield return L("system", "dlg.lb1.hu_market.goal");
        }

        // ---------- 旧地（一）：桑下 ----------

        static IEnumerable<StageStep> Zijing(FieldStoryController c)
        {
            if (Has(c, LsZijing)) { yield return L("liu_zijing", "dlg.lb1.liu_zijing.N4.2b"); yield break; }
            foreach (var s in Lines("liu_zijing", "dlg.lb1.liu_zijing.N4.1", "liu_zijing", "dlg.lb1.liu_zijing.N4.2")) yield return s;
            yield return Choice("liu_bei", "dlg.lb1.L6.ask", "ls_answer_", new[] { "dlg.lb1.L6.was_wrong", "dlg.lb1.L6.never_forgot" },
                new[] { L("liu_zijing", "dlg.lb1.liu_zijing.N4.was_wrong.1"), Remember("liu_zijing", "n4_answer", "was_wrong", "mem.liu_zijing_was_wrong") },
                new[] { L("liu_zijing", "dlg.lb1.liu_zijing.N4.never_forgot.1"), Remember("liu_zijing", "n4_answer", "never_forgot", "mem.liu_zijing_never_forgot") });
            yield return L("liu_zijing", "dlg.lb1.liu_zijing.N4.2b");
            yield return Flag(LsZijing);
        }

        static IEnumerable<StageStep> Yuanqi(FieldStoryController c)
        {
            if (Has(c, LsYuanqi)) { yield return L("yuanqi_qi", "dlg.lb1.yuanqi_qi.N4.2"); yield break; }
            foreach (var s in Lines("liu_yuanqi", "dlg.lb1.liu_yuanqi.N4.1", "liu_yuanqi", "dlg.lb1.liu_yuanqi.N4.2",
                         "yuanqi_qi", "dlg.lb1.yuanqi_qi.N4.1", "liu_bei", "dlg.lb1.yuanqi_qi.N4.lb", "yuanqi_qi", "dlg.lb1.yuanqi_qi.N4.2"))
                yield return s;
            yield return Flag(LsYuanqi);
        }

        static IEnumerable<StageStep> Mulberry(FieldStoryController c)
        {
            foreach (var s in Lines("liu_bei", "dlg.lb1.L6.sight", "narrator", "dlg.lb1.L6.flash.0", "young_liu_bei", "dlg.lb1.L6.flash.1",
                         "young_liu_zijing", "dlg.lb1.L6.flash.2", "narrator", "dlg.lb1.L6.flash.3"))
                yield return s;
            yield return Flag(Lousang);
            yield return Remember("liu_bei", "L6_lousang", "visited", "mem.liu_bei_L6_lousang");
        }
    }
}
