using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Story;
using static ThreeKingdoms.Gameplay.Story.S;
using static ThreeKingdoms.Gameplay.Story.SideQuests;

namespace ThreeKingdoms.Gameplay.Story
{
    // R4 the Bohai plain and the Pingyuan base (design/chapter1-rise/npc-sidequests/R3_R4_julu_bohai.md §6). Core layer:
    // 千丈之堤, 同簋, 阿禾 ③④⑤ (河上的人, 桥头第二排, 这回我能跟着走吗), 鼓叟 ⑤ 《白马》／《白马不还》, 高唐旧吏 (L6 ②).
    // 夜宴 is the main line's E9 already (FieldScripts4.AssassinScene, memory liu_ping.liuping_assassin read as
    // liu_ping_ke.let_go). Steps: N5 = Pingyuan / PingyuanSafe (191); N6 = Zhending, back from Jieqiao (192; E9 now waits
    // for the player at the county office, so the plain can be walked first); N7 = Beihai (193, the camp breaks).
    // 桥头第二排 is the producer's fallback (降级版, 10_REVIEW §3 ③): taking the recruits = wounded, leaving them = absent,
    // no one dies; the full "three squares" battle goal waits for its own system design.
    public static class SideQuestsR4
    {
        public static bool Mine(FieldStoryController c) => c.Location == FieldStoryController.Place.Pingyuan;

        const string Levee = "sq.r4.levee", Same = "sq.r4.same", SameAsked = "sq.r4.same_asked", AheJoined = "sq.r4.ahe_joined",
            AheBring = "sq.r4.ahe_bring", AheAsked = "sq.r4.ahe_asked", AheBack = "sq.r4.ahe_back", BookBaima = "sq.r4.book_baima",
            BookBuhuan = "sq.r4.book_buhuan", Gaotang = "sq.r4.gaotang", Farewell = "sq.r4.farewell";

        static ChapterStep Step(FieldStoryController c) => c.Run.Chapter2.Step;
        static bool N5(FieldStoryController c) => Step(c) == ChapterStep.Pingyuan || Step(c) == ChapterStep.PingyuanSafe;
        static bool N6(FieldStoryController c) => Step(c) == ChapterStep.Zhending;
        static bool N7(FieldStoryController c) => Step(c) == ChapterStep.Beihai;

        // Whether the boy went to Guangzong's well (a_he ①, R3): covered, punished, or never met.
        static string Guangzong(ChapterState ch) => Recall(ch, "a_he", "guangzong");

        public static bool? Present(FieldStoryController c, string id)
        {
            var ch = c.Run.Chapter2;
            switch (id)
            {
                case "lu_ditou": case "bian_sheng": case "fan_shen": case "gu_sou": case "wu_laoli": case "feng_widow": case "feng_kuan": return true;
                case "zhang_shiping": return N5(c) && ch.Step == ChapterStep.PingyuanSafe;
                case "qz_aide": return N5(c) || N6(c);
                // The boy: at the ferry towing until he joins (path B), then at the barracks door.
                case "a_he_tow": return N5(c) && !ch.Has(AheJoined) && Guangzong(ch) == "punished";
                case "a_he": return ch.Has(AheJoined) || N5(c) && Guangzong(ch) != "punished";
                default: return null;
            }
        }

        public static string Prompt(FieldStoryController c, string id)
        {
            var ch = c.Run.Chapter2;
            switch (id)
            {
                case "lu_ditou": return N5(c) && !ch.Has(Levee) ? "raw:[确认] 与鲁堤头交谈（修堤）" : null;
                case "bian_sheng": return N5(c) && ch.Has(ChapterScripts4.Porridge) && !ch.Has(Same) ? "raw:[确认] 在卞生身边坐下" : null;
                case "gu_sou":
                    if (N5(c) && !ch.Has(BookBaima)) return "raw:[确认] 听鼓叟说书《白马》";
                    if (N6(c) && !ch.Has(BookBuhuan)) return "raw:[确认] 听鼓叟说书《白马不还》";
                    return null;
                case "wu_laoli": return N6(c) && !ch.Has(Gaotang) ? "raw:[确认] 与高唐老吏交谈" : null;
                case "py_clerk": return N6(c) && !ch.Has(ChapterScripts4.Assassin) ? "raw:[确认] 回相府（夜里有客求见）" : null;
                case "qz_aide": return "raw:[确认] 与青州从事交谈（通行文书）";
                default: return null;
            }
        }

        public static bool Marked(FieldStoryController c, string id)
        {
            var ch = c.Run.Chapter2;
            return id switch
            {
                "lu_ditou" => N5(c) && !ch.Has(Levee),
                "fan_shen" => N5(c) && ch.Has(ChapterScripts4.Porridge) && !ch.Has(SameAsked),
                "bian_sheng" => N5(c) && ch.Has(SameAsked) && !ch.Has(Same),
                "gu_sou" => N5(c) && !ch.Has(BookBaima) || N6(c) && !ch.Has(BookBuhuan),
                "a_he_tow" => !ch.Has(AheJoined),
                "a_he" => N5(c) && !ch.Has(AheJoined) || ch.Step == ChapterStep.PingyuanSafe && ch.Has(AheJoined) && !ch.Has(AheAsked)
                          || N6(c) && ch.Has(AheJoined) && !ch.Has(AheBack) || N7(c) && ch.Has(AheJoined) && !ch.Has(Farewell),
                "wu_laoli" => N6(c) && !ch.Has(Gaotang),
                "py_clerk" => N6(c) && !ch.Has(ChapterScripts4.Assassin),
                _ => false,
            };
        }

        public static IEnumerable<StageStep> Talk(FieldStoryController c, string id) => id switch
        {
            "lu_ditou" => LuDitou(c),
            "fan_shen" => FanShen(c),
            "bian_sheng" => BianSheng(c),
            "gu_sou" => GuSou(c),
            "a_he" or "a_he_tow" => AHe(c, id),
            "zhang_shiping" => new[] { L("zhang_shiping", "dlg.lb1.zhang_shiping.N5.word") },
            "wu_laoli" => WuLaoli(c),
            "qz_aide" => QingzhouAide(c),
            "feng_widow" => new[] { L("feng_widow", c.Run.Chapter2.Has(Gaotang) ? "sq.r4.widow.after" : "sq.r4.widow.plain") },
            "feng_kuan" => new[] { L("feng_kuan", "dlg.lb1.npc.r4.gaotang.son.N5.1") },
            "py_clerk" => N6(c) && !c.Run.Chapter2.Has(ChapterScripts4.Assassin) ? FieldScripts4.Assassin(c) : null,
            _ => null,
        };

        // Arrival extras: back from Jieqiao (N6) the recruits' fate is told once (the fallback's wounded / absent).
        public static IEnumerable<StageStep> OnArrive(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (!N6(c) || !ch.Has(AheJoined) || Recall(ch, "a_he", "jieqiao") != null) yield break;
            bool brought = ch.Has(AheBring);
            string value = brought ? "wounded" : "absent";
            yield return Remember("a_he", "jieqiao", value, $"mem.a_he.jieqiao.{value}");
            yield return Mem("liu_bei", "a_he_jieqiao", value);
            yield return L("tian_yu", brought ? "dlg.lb1.tian_yu.N6.ahe_wounded" : "dlg.lb1.tian_yu.N6.ahe_stayed");
            if (brought) foreach (var s in Keepsake("a_he_banner")) yield return s;
        }

        // ---------- sq.lb1.r4.levee 千丈之堤 ----------

        // The writers' three supply trips and the rammer are told as one walk along the levee (no carrying mini game).
        static IEnumerable<StageStep> LuDitou(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (!N5(c) || ch.Has(Levee))
            {
                yield return L("lu_ditou", N6(c) ? "dlg.lb1.lu_ditou.N6.1" : N7(c) ? "dlg.lb1.lu_ditou.N7.1" : "dlg.lb1.lu_ditou.N5.lore.1");
                yield break;
            }
            yield return L("lu_ditou", "dlg.lb1.lu_ditou.N5.1");
            yield return Choice("liu_bei", "sq.r4.levee.prompt", "sq_levee_go_", new[] { "sq.r4.levee.walk", "sq.r4.levee.later" },
                LeveeWalk().ToArray(), new[] { L("lu_ditou", "dlg.lb1.lu_ditou.N5.lore.2") });
        }

        static IEnumerable<StageStep> LeveeWalk()
        {
            yield return L("liu_bei", "dlg.lb1.sq.levee.liu_bei.1");
            yield return L("lu_ditou", "dlg.lb1.sq.levee.lu.1");
            foreach (int i in new[] { 1, 2, 3 })
            {
                yield return Fade(1f, 0.3f);
                yield return Fade(0f, 0.3f);
                yield return L("lu_ditou", "dlg.lb1.sq.levee.spot." + i);
            }
            yield return L("narrator", "sq.r4.levee.supply");
            yield return L("lusan", "dlg.lb1.sq.levee.lusan.1");
            yield return L("system", "sq.r4.levee.fan");
            yield return L("lu_ditou", "sq.r4.levee.earth");
            yield return L("liu_ping", "dlg.lb1.liu_ping.N5.levee.1");
            yield return L("liu_bei", "dlg.lb1.sq.levee.liu_bei.2");
            yield return Fade(1f, 0.4f);
            yield return Fade(0f, 0.4f);
            yield return L("lu_ditou", "sq.r4.levee.ram");
            yield return Choice("liu_bei", "sq.r4.levee.song", "sq_levee_song_", new[] { "sq.r4.levee.song.liu", "sq.r4.levee.song.zhang" },
                new[]
                {
                    L("liu_bei", "dlg.lb1.sq.levee.liu_bei.A1"), L("liu_bei", "sq.r4.levee.song.lead"), L("narrator", "sq.r4.levee.song.all"),
                    L("lu_ditou", "dlg.lb1.lu_ditou.N5.song.liu"), Mem("lu_ditou", "levee", "liu_bei_sang"),
                },
                new[]
                {
                    L("zhang_fei", "dlg.lb1.sq.levee.zhang_fei.B1"), L("zhang_fei", "sq.r4.levee.song.lead"), L("narrator", "sq.r4.levee.song.all.zf"),
                    L("lu_ditou", "dlg.lb1.lu_ditou.N5.song.zhang"), Mem("lu_ditou", "levee", "zhang_fei_sang"),
                });
            yield return L("lu_ditou", "dlg.lb1.lu_ditou.N5.levee.done");
            foreach (var s in Keepsake("lu_ditou_clappers")) yield return s;
            yield return L("jian_yong", "dlg.lb1.jian_yong.N5.levee.1");
            yield return L("tian_yu", "dlg.lb1.tian_yu.N5.levee.1");
            yield return Reputation(2);
            yield return Mem("liu_bei", "pingyuan_levee", "done");
            yield return Mem("liu_ping", "levee_refused", "yes");
            yield return Flag(Levee);
        }

        // ---------- sq.lb1.r4.same_table 同簋 ----------

        static IEnumerable<StageStep> FanShen(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (N5(c) && ch.Has(ChapterScripts4.Porridge) && !ch.Has(Same))
            {
                yield return L("fan_shen", "dlg.lb1.sq.same.fan.1");
                yield return Flag(SameAsked);
                yield break;
            }
            yield return L("fan_shen", "sq.r4.fan.plain");
        }

        static IEnumerable<StageStep> BianSheng(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (ch.Has(Same) || !N5(c) || !ch.Has(ChapterScripts4.Porridge))
            {
                yield return L("bian_sheng", ch.Has(Same) ? (N7(c) ? "dlg.lb1.npc.r4.pingyuan.poor_scholar.N7.1" : N6(c) ? "dlg.lb1.npc.r4.pingyuan.poor_scholar.N6.1" : "dlg.lb1.npc.r4.pingyuan.poor_scholar.N5.1")
                    : "dlg.lb1.npc.r4.pingyuan.poor_scholar.N5.0");
                yield break;
            }
            foreach (var s in Lines("bian_sheng", "dlg.lb1.npc.r4.pingyuan.poor_scholar.N5.0", "zhang_fei", "dlg.lb1.sq.same.zhang_fei.1", "bian_sheng", "dlg.lb1.sq.same.bian.1",
                         "narrator", "sq.r4.same.sit", "liu_bei", "dlg.lb1.sq.same.liu_bei.1", "bian_sheng", "dlg.lb1.sq.same.bian.2", "liu_bei", "dlg.lb1.sq.same.liu_bei.2",
                         "bian_sheng", "dlg.lb1.sq.same.bian.3", "liu_bei", "dlg.lb1.sq.same.liu_bei.3"))
                yield return s;
            yield return Reputation(1);
            yield return Flag(Same);
            yield return Mem("liu_bei", "same_table", "yes");
        }

        // ---------- 鼓叟 ⑤ 《白马》／《白马不还》 ----------

        static IEnumerable<StageStep> GuSou(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (N5(c) && !ch.Has(BookBaima))
            {
                yield return L("gu_sou", "dlg.lb1.gu_sou.N5.0");
                for (int i = 1; i <= 6; i++) yield return L("gu_sou", "dlg.lb1.gu_sou.N5.book." + i);
                if (ch.Has(AheJoined)) { yield return L("narrator", "sq.r4.book.ahe_left"); yield return L("gu_sou", "dlg.lb1.gu_sou.N5.ahe_left"); }
                yield return Flag(BookBaima);
                yield return Mem("gu_sou", "pingyuan_audience", "yes");
                yield return L("system", "sq.book.5");
                yield break;
            }
            if (N6(c) && !ch.Has(BookBuhuan))
            {
                yield return L("gu_sou", "dlg.lb1.gu_sou.N6.0");
                for (int i = 1; i <= 4; i++) yield return L("gu_sou", "dlg.lb1.gu_sou.N6.book." + i);
                string bridge = Recall(ch, "a_he", "jieqiao");
                yield return L("gu_sou", "dlg.lb1.gu_sou.N6.book.5." + (bridge == "held" ? "alive" : bridge == "dead" ? "dead" : bridge == "wounded" ? "wounded" : "none"));
                yield return L("gu_sou", "dlg.lb1.gu_sou.N6.book.6");
                yield return L("gu_sou", "dlg.lb1.gu_sou.N6.after");
                yield return Flag(BookBuhuan);
                yield return L("system", "sq.book.5b");
                yield break;
            }
            yield return L("gu_sou", "sq.r4.gu_sou.idle");
        }

        // ---------- 阿禾 ③ 河上的人, ④ 桥头第二排 (fallback), ⑤ 这回我能跟着走吗 ----------

        static IEnumerable<StageStep> AHe(FieldStoryController c, string id)
        {
            var ch = c.Run.Chapter2;
            if (!ch.Has(AheJoined))
            {
                if (!N5(c)) yield break;
                string gz = Guangzong(ch);
                if (gz == "covered")
                {
                    yield return L("a_he", "dlg.lb1.a_he.N5.A.1");
                    if (Recall(ch, "a_he", "anxi_extra_food") == "yes") yield return L("a_he", "dlg.lb1.a_he.N5.A.anxi");
                }
                else if (gz == "punished")
                {
                    yield return L("a_he", "dlg.lb1.a_he.N5.B.1");
                    yield return L("a_he", "dlg.lb1.a_he.N5.B.2");
                    if (Recall(ch, "a_he", "anxi") != null) yield return L("a_he", "dlg.lb1.a_he.N5.B.anxi");
                    yield return L("liu_bei", "dlg.lb1.sq.a_he_3.liu_bei.B1");
                    if (ch.Has("r3.grave_visited")) { yield return L("liu_bei", "dlg.lb1.sq.a_he_3.liu_bei.B2"); yield return L("a_he", "sq.r4.ahe.zheng_hao"); }
                    yield return L("a_he", "dlg.lb1.a_he.N5.B.3");
                    yield return Mem("a_he", "secret_kept", "promised");
                }
                else
                {
                    yield return L("narrator", "sq.r4.ahe.notice");
                    yield return L("a_he", "dlg.lb1.a_he.N5.none.1");
                }
                // 定名 at the barracks door.
                foreach (var s in Lines("tian_yu", "dlg.lb1.sq.a_he_3.tian_yu.1", "a_he", "sq.r4.ahe.name", "tian_yu", "dlg.lb1.sq.a_he_3.tian_yu.2",
                             "a_he", "dlg.lb1.sq.a_he_3.a_he.2", "tian_yu", "dlg.lb1.sq.a_he_3.tian_yu.3", "a_he", "dlg.lb1.sq.a_he_3.a_he.3",
                             "tian_yu", "dlg.lb1.sq.a_he_3.tian_yu.4", "liu_bei", "dlg.lb1.sq.a_he_3.liu_bei.1"))
                    yield return s;
                yield return Mem("a_he", "pingyuan_name", "he");
                yield return Mem("tian_yu", "named_a_he", "yes");
                yield return Flag(AheJoined);
                yield return L("system", "sq.r4.ahe.joined");
                yield break;
            }
            // Before the Jieqiao campaign (fallback ④): take the recruits or not.
            if (ch.Step == ChapterStep.PingyuanSafe && !ch.Has(AheAsked))
            {
                foreach (var s in Lines("tian_yu", "dlg.lb1.tian_yu.N6pre.1", "a_he", "dlg.lb1.a_he.N6pre.1", "zhang_fei", "dlg.lb1.a_he.N6pre.zhang_fei"))
                    yield return s;
                yield return Choice("liu_bei", "sq.r4.ahe.bring.prompt", "sq_ahe_bring_", new[] { "sq.r4.ahe.bring", "sq.r4.ahe.leave" },
                    new[] { L("liu_bei", "dlg.lb1.a_he.N6pre.liu_bei"), Flag(AheBring) },
                    new[] { L("liu_bei", "sq.r4.ahe.leave.1") });
                yield return Flag(AheAsked);
                yield break;
            }
            string bridge = Recall(ch, "a_he", "jieqiao");
            if (N6(c) && !ch.Has(AheBack))
            {
                if (bridge == "absent")
                    foreach (var s in Lines("a_he", "dlg.lb1.a_he.N6.absent.1", "a_he", "dlg.lb1.a_he.N6.absent.2", "liu_bei", "dlg.lb1.a_he.N6.absent.lb")) yield return s;
                else
                    yield return L("a_he", "sq.r4.ahe.wounded");
                yield return Flag(AheBack);
                yield break;
            }
            if (N7(c) && !ch.Has(Farewell)) { foreach (var s in FarewellSteps(ch)) yield return s; yield break; }
            yield return L("a_he", ch.Has(Farewell) ? (Recall(ch, "a_he", "leave_pingyuan") == "stay" ? "dlg.lb1.a_he.N7.B" : "dlg.lb1.a_he.N7.A") : "sq.r4.ahe.idle");
        }

        // ⑤ at the camp breaking (N7): also played on the way out with Taishi Ci if the player has not talked to him.
        public static StageStep[] FarewellSteps(ChapterState ch)
        {
            if (!ch.Has(AheJoined) || ch.Has(Farewell) || Recall(ch, "a_he", "jieqiao") == "dead") return new StageStep[0];
            string bridge = Recall(ch, "a_he", "jieqiao");
            var steps = new List<StageStep> { L("a_he", "dlg.lb1.a_he.N7.1") };
            if (bridge == "wounded") steps.Add(L("a_he", "dlg.lb1.a_he.N7.wounded"));
            if (bridge == "absent") steps.Add(L("a_he", "dlg.lb1.a_he.N7.absent"));
            steps.Add(Choice("liu_bei", "sq.r4.ahe.leave.prompt", "sq_ahe_leave_", new[] { "sq.r4.ahe.follow", "sq.r4.ahe.stay" },
                new[]
                {
                    L("liu_bei", "sq.r4.ahe.follow.1"), L("a_he", Guangzong(ch) == "covered" ? "sq.r4.ahe.follow.debt" : "dlg.lb1.a_he.N7.A"),
                    Mem("a_he", "leave_pingyuan", "follow"), Mem("liu_bei", "a_he_choice", "follow"),
                },
                new[]
                {
                    L("liu_bei", "sq.r4.ahe.stay.1"), L("a_he", "dlg.lb1.a_he.N7.B"), L("lu_ditou", "dlg.lb1.lu_ditou.N7.ahe"),
                    Mem("a_he", "leave_pingyuan", "stay"), Mem("liu_bei", "a_he_choice", "stay"),
                }));
            steps.Add(Flag(Farewell));
            return steps.ToArray();
        }

        // The Qingzhou aide Bi Qian (R4 §6.5 领通行文书点): Tian Kai's passes, Ji province from 191, Yan province added in 192.
        static IEnumerable<StageStep> QingzhouAide(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (N6(c) && !ch.Has(SideQuestsR3.PassYanzhou))
            {
                yield return L("qz_aide", "dlg.lb1.npc.r4.pingyuan.qingzhou_aide.N6.1");
                yield return Flag(SideQuestsR3.PassJizhou);
                yield return Flag(SideQuestsR3.PassYanzhou);
                yield return L("system", "sq.r4.pass.yan");
                yield break;
            }
            if (!ch.Has(SideQuestsR3.PassJizhou))
            {
                yield return L("qz_aide", "dlg.lb1.npc.r4.pingyuan.qingzhou_aide.N5.1");
                yield return L("qz_aide", "dlg.lb1.npc.r4.pingyuan.qingzhou_aide.N5.2");
                yield return Flag(SideQuestsR3.PassJizhou);
                yield return L("system", "sq.r4.pass.ji");
                yield break;
            }
            yield return L("qz_aide", N6(c) ? "dlg.lb1.npc.r4.pingyuan.qingzhou_aide.N6.1" : "dlg.lb1.npc.r4.pingyuan.qingzhou_aide.N6pre.1");
        }

        // ---------- sq.lb1.L6.gaotang_2 高唐旧吏 ----------

        static IEnumerable<StageStep> WuLaoli(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (!N6(c) || ch.Has(Gaotang))
            {
                yield return L("wu_laoli", ch.Has(Gaotang) ? "dlg.lb1.npc.r4.gaotang.old_clerk.N6.after" : N7(c) ? "dlg.lb1.npc.r4.gaotang.old_clerk.N7.1" : "dlg.lb1.npc.r4.gaotang.old_clerk.N5.1");
                yield break;
            }
            foreach (var s in Lines("wu_laoli", "dlg.lb1.sq.gaotang.wu.1", "wu_laoli", "dlg.lb1.sq.gaotang.wu.2", "zhang_fei", "dlg.lb1.sq.gaotang.zhang_fei.1",
                         "liu_bei", "dlg.lb1.sq.gaotang.liu_bei.1", "feng_widow", "dlg.lb1.sq.gaotang.widow.1", "feng_kuan", "dlg.lb1.sq.gaotang.son.1"))
                yield return s;
            yield return Choice("liu_bei", "sq.r4.gaotang.prompt", "sq_gaotang_", new[] { "sq.r4.gaotang.owed", "sq.r4.gaotang.comfort" },
                new[]
                {
                    L("liu_bei", "dlg.lb1.sq.gaotang.liu_bei.A"), L("feng_kuan", "dlg.lb1.sq.gaotang.son.A"), L("liu_bei", "dlg.lb1.sq.gaotang.liu_bei.A2"),
                    Mem("liu_bei", "gaotang", "owed"),
                },
                new[]
                {
                    L("liu_bei", "dlg.lb1.sq.gaotang.liu_bei.B"), L("feng_kuan", "dlg.lb1.sq.gaotang.son.B"),
                    Mem("liu_bei", "gaotang", "comfort"),
                });
            yield return L("guan_yu", "dlg.lb1.sq.gaotang.guan_yu.1");
            yield return Reputation(2);
            yield return Flag(Gaotang);
        }
    }
}
