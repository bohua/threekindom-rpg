using System.Collections.Generic;
using ThreeKingdoms.Core.Story;
using static ThreeKingdoms.Gameplay.Story.S;
using static ThreeKingdoms.Gameplay.Story.SideQuests;

namespace ThreeKingdoms.Gameplay.Story
{
    // R5a Suanzao, the allied camp (design/chapter1-rise/npc-sidequests/R5_suanzao_luoyang.md §2–§4). Core layer here:
    // 鼓叟·会盟 (L1 ③), 坛上盟辞, 鲍家兄弟的箭. The writers' "N4·入营" is before the oath (no Wenjiu yet), "会盟后" and
    // "温酒后" both after it: the game plays the oath and Hua Xiong in one stage. The camp is never seen again after Hulao,
    // so the arrows' ending (after the Bian river, 汴水后) plays in the story stage after battle 8 (AfterBianshui).
    // People added to the Suanzao scene (ChapterFieldBuilder3): gu_sou by the gate, zang_hong at the altar's east steps,
    // bao_xin and bao_tao at the east tent.
    public static class SideQuestsR5a
    {
        public static bool Mine(FieldStoryController c) => c.Location == FieldStoryController.Place.Suanzao;

        const string Book = "sq.r5a.huimeng", AltarBuilt = "sq.r5a.altar_built", AltarGift = "sq.r5a.altar_gift",
            ArrowGiven = "sq.r5a.arrow_given", ArrowAsked = "sq.r5a.arrow_asked";

        static bool After(FieldStoryController c) => c.Run.Chapter2.Has(ChapterScripts3.Wenjiu);

        public static bool? Present(FieldStoryController c, string id) => id switch
        {
            "gu_sou" or "zang_hong" or "bao_xin" or "bao_tao" => true,
            _ => null,
        };

        public static string Prompt(FieldStoryController c, string id)
        {
            var ch = c.Run.Chapter2;
            switch (id)
            {
                case "gu_sou": return After(c) && !ch.Has(Book) ? "raw:[确认] 听鼓叟说书《会盟》" : null;
                case "zang_hong": return !After(c) && !ch.Has(AltarBuilt) ? "raw:[确认] 与臧洪交谈（坛下）" : null;
                case "bao_tao": return After(c) && !ch.Has(ArrowGiven) ? "raw:[确认] 与鲍韬比箭" : null;
                default: return null;
            }
        }

        public static bool Marked(FieldStoryController c, string id)
        {
            var ch = c.Run.Chapter2;
            return id switch
            {
                "gu_sou" => After(c) && !ch.Has(Book),
                "zang_hong" => !After(c) ? !ch.Has(AltarBuilt) : ch.Has(AltarBuilt) && !ch.Has(AltarGift),
                "bao_tao" => After(c) && !ch.Has(ArrowGiven),
                "bao_xin" => ch.Has(ArrowGiven) && !ch.Has(ArrowAsked),
                _ => false,
            };
        }

        public static IEnumerable<StageStep> Talk(FieldStoryController c, string id) => id switch
        {
            "gu_sou" => GuSou(c),
            "zang_hong" => ZangHong(c),
            "bao_tao" => BaoTao(c),
            "bao_xin" => BaoXin(c),
            _ => null,
        };

        // ---------- sq.lb1.L1.huimeng 鼓叟·会盟 ----------

        static IEnumerable<StageStep> GuSou(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            string tale = Recall(c, "gu_sou", "anxi_tale");
            if (!After(c))
            {
                yield return L("gu_sou", tale == "truth" ? "dlg.lb1.gu_sou.N4in.truth" : tale == "let_be" ? "dlg.lb1.gu_sou.N4in.letbe" : "dlg.lb1.gu_sou.N4in.1");
                yield break;
            }
            if (ch.Has(Book)) { yield return L("gu_sou", "sq.r5a.gu_sou.again"); yield break; }
            yield return L("gu_sou", "dlg.lb1.gu_sou.N4hm.1");
            foreach (var s in Lines("gu_sou", "dlg.lb1.gu_sou.L1.3.1", "gu_sou", "dlg.lb1.gu_sou.L1.3.2", "gu_sou", "dlg.lb1.gu_sou.L1.3.3")) yield return s;
            if (tale == "truth") yield return L("gu_sou", "dlg.lb1.gu_sou.L1.3.4t");
            else
            {
                yield return L("gu_sou", "dlg.lb1.gu_sou.L1.3.4l");
                yield return L("zhang_fei", "dlg.lb1.zhang_fei.L1.3.1");
            }
            yield return L("gu_sou", "dlg.lb1.gu_sou.L1.3.6");
            if (ch.Has(ChapterScripts3.JianYong)) yield return L("jian_yong", "dlg.lb1.jian_yong.L1.3.1");
            yield return Flag(Book);
            yield return Mem("gu_sou", "heard_suanzao", "yes");
            yield return L("system", "sq.book.3");
        }

        // ---------- sq.lb1.r5a.altar_oath 坛上盟辞 ----------

        static IEnumerable<StageStep> ZangHong(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            var run = c.Run;
            if (After(c))
            {
                if (!ch.Has(AltarBuilt)) { yield return L("zang_hong", "dlg.lb1.zang_hong.N4hm.1"); yield break; }
                if (ch.Has(AltarGift)) { yield return L("zang_hong", "dlg.lb1.zang_hong.N4hm.1"); yield break; }
                string answer = Recall(c, "zang_hong", "altar_answer");
                yield return L("zang_hong", answer == "below" ? "dlg.lb1.zang_hong.N4hm.below.1" : "dlg.lb1.zang_hong.N4hm.hard.1");
                yield return L("zang_hong", "dlg.lb1.zang_hong.altar.gift.1");
                yield return Flag(AltarGift);
                yield return Mem("zang_hong", "altar_oath_done", "yes");
                foreach (var s in Keepsake("zang_hong_draft")) yield return s;
                yield break;
            }
            if (ch.Has(AltarBuilt)) { yield return L("zang_hong", "sq.r5a.zang_hong.wait"); yield break; }
            foreach (var s in Lines("zang_hong", "dlg.lb1.zang_hong.N4in.1", "zang_hong", "dlg.lb1.zang_hong.N4in.2", "zang_hong", "sq.r5a.altar.1"))
                yield return s;
            bool tianYu = ch.Has(ChapterState.TianYu);
            yield return Choice("liu_bei", "sq.r5a.altar.prompt", "sq_altar_", new[] { "sq.r5a.altar.host", "sq.r5a.altar.own", "sq.r5a.altar.carry", "sq.r5a.altar.later" },
                new[]
                {
                    L("system", "sq.r5a.altar.host.1"), Reputation(1),
                    Mem("zuo_mancang", "altar_meal", "host_grain"), Flag(AltarBuilt),
                },
                new[]
                {
                    tianYu ? L("tian_yu", "dlg.lb1.tian_yu.altar.own_grain.1") : L("liu_bei", "sq.r5a.altar.own.1"),
                    Call(() => { if (run.Progress.Inventory.Rations > 0) run.Progress.Inventory.SpendRations(1); }),
                    L("system", "sq.r5a.altar.own.2"), Reputation(1),
                    Mem("zuo_mancang", "altar_meal", "own_grain"), Flag(AltarBuilt),
                },
                new[]
                {
                    L("zhang_fei", "dlg.lb1.zhang_fei.altar.carry_earth.1"), L("system", "sq.r5a.altar.carry.1"), Reputation(2),
                    Mem("zuo_mancang", "altar_meal", "carry_earth"), Flag(AltarBuilt),
                },
                new[] { L("zang_hong", "sq.r5a.altar.later.1") });
            if (!ch.Has(AltarBuilt)) yield break;
            // The altar done; that night he reads it once below the altar and asks for the truth.
            yield return Fade(1f, 0.5f);
            yield return Fade(0f, 0.5f);
            foreach (var s in Lines("narrator", "sq.r5a.altar.night", "zang_hong", "sq.r5a.altar.ask",
                         "zang_hong", "dlg.lb1.zang_hong.altar.read.1", "zang_hong", "dlg.lb1.zang_hong.altar.read.2"))
                yield return s;
            yield return Choice("liu_bei", "sq.r5a.altar.answer", "sq_altar_ans_", new[] { "sq.r5a.altar.hard", "sq.r5a.altar.below" },
                new[] { L("liu_bei", "dlg.lb1.liu_bei.altar.hard"), Mem("zang_hong", "altar_answer", "hard") },
                new[] { L("liu_bei", "dlg.lb1.liu_bei.altar.below"), Mem("zang_hong", "altar_answer", "below") });
            yield return L("zang_hong", "sq.r5a.altar.thanks");
        }

        // ---------- sq.lb1.r5a.bao_arrows 鲍家兄弟的箭 ----------

        // The writers' match is the archery mini game; the Suanzao field has no range, so the three arrows are told and
        // Liu Bei chooses to shoot to win or to let the boy win (both go on, as the card says).
        static IEnumerable<StageStep> BaoTao(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (!After(c)) { yield return L("bao_tao", "dlg.lb1.bao_tao.N4in.1"); yield break; }
            if (ch.Has(ArrowGiven))
            {
                string word = Recall(c, "bao_xin", "brother_word");
                yield return L("bao_tao", word == "let_go" ? "dlg.lb1.bao_tao.N4hl.a.1" : word == "stay_close" ? "dlg.lb1.bao_tao.N4hl.b.1" : "sq.r5a.bao_tao.ask");
                yield break;
            }
            foreach (var s in Lines("bao_tao", "dlg.lb1.bao_tao.N4hm.1", "bao_tao", "dlg.lb1.bao_tao.N4in.1")) yield return s;
            yield return Choice("liu_bei", "sq.r5a.arrow.prompt", "sq_arrow_", new[] { "sq.r5a.arrow.win", "sq.r5a.arrow.yield" },
                new[] { L("narrator", "sq.r5a.arrow.win.1"), L("bao_tao", "sq.r5a.arrow.win.2") },
                new[] { L("narrator", "sq.r5a.arrow.lose.1"), L("zhang_fei", "dlg.lb1.zhang_fei.arrow.lose") });
            foreach (var s in Lines("bao_tao", "sq.r5a.arrow.give", "bao_tao", "sq.r5a.arrow.ask")) yield return s;
            yield return Flag(ArrowGiven);
            yield return L("system", "sq.r5a.arrow.goal");
        }

        static IEnumerable<StageStep> BaoXin(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (!After(c)) { foreach (var s in Lines("bao_xin", "dlg.lb1.bao_xin.N4in.1", "bao_xin", "dlg.lb1.bao_xin.N4in.2")) yield return s; yield break; }
            if (!ch.Has(ArrowGiven) || ch.Has(ArrowAsked))
            {
                yield return L("bao_xin", ch.Has(ArrowAsked) ? "dlg.lb1.bao_xin.N4hl.1" : "dlg.lb1.bao_xin.N4hm.1");
                yield break;
            }
            yield return L("bao_xin", "dlg.lb1.bao_xin.arrow.1");
            yield return Choice("liu_bei", "sq.r5a.arrow.word", "sq_arrow_word_", new[] { "sq.r5a.arrow.let_go", "sq.r5a.arrow.stay_close" },
                new[] { L("liu_bei", "dlg.lb1.liu_bei.arrow.a"), Mem("bao_xin", "brother_word", "let_go") },
                new[] { L("liu_bei", "dlg.lb1.liu_bei.arrow.b"), Mem("bao_xin", "brother_word", "stay_close") });
            yield return L("bao_xin", "sq.r5a.bao_xin.after");
            yield return Flag(ArrowAsked);
        }

        // Played in the story stage after battle 8, once Luoyang has burned (X6, the Bian river): Bao Xin wounded at his
        // tent's door. Bao Tao dies at the Bian river whatever was said (the card: 不改生死).
        public static IEnumerable<StageStep> AfterBianshui(ChapterState ch)
        {
            string word = Recall(ch, "bao_xin", "brother_word");
            if (word == null) yield break;
            yield return Card("sq.r5a.bianshui.card");
            yield return Background("mengzhu_tent");
            yield return Actor("bao_xin", "NpcBaoXin", 0.6f, 0.34f, "left");
            yield return Actor("liu_bei", "LiuBei", 0.38f, 0.3f, "right");
            yield return Fade(0f);
            yield return L("narrator", "sq.r5a.bianshui.1");
            yield return L("bao_xin", word == "let_go" ? "dlg.lb1.bao_xin.arrow.end.a" : "dlg.lb1.bao_xin.arrow.end.b");
            yield return L("liu_bei", "dlg.lb1.liu_bei.arrow.end");
            yield return Remember("bao_xin", "liu_kept_arrow", "yes", "mem.bao_xin.liu_kept_arrow.yes");
            yield return Flag(KeepsakePrefix + "bao_tao_arrow");
            yield return L("system", "sq.keepsake.bao_tao_arrow");
            yield return Fade(1f);
        }
    }
}
