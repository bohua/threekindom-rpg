using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Story;
using static ThreeKingdoms.Gameplay.Story.S;
using static ThreeKingdoms.Gameplay.Story.SideQuests;

namespace ThreeKingdoms.Gameplay.Story
{
    // R8 Xiapi (design/chapter1-rise/npc-sidequests/R8_xuzhou_epilogue.md §3–§4). Core layer: 织席郎 (L1 ⑧), 背进城的孩子
    // (L2 ⑥), 使君的账 (L5), 城头 (L6), 什伍名籍, 布席于路. The writers' N8b–N8c is the Xiapi step after Tao Qian's
    // greeting (F6); N8d the Echo step; N8e the seal (a story stage). Xiapi as built (ChapterFieldBuilder5) is one walled
    // square: the refugee mouth, barracks, wharf and shrine road of the writers' map are corners of it.
    public static class SideQuestsR8
    {
        public static bool Mine(FieldStoryController c) => c.Location == FieldStoryController.Place.Xiapi;

        const string BookShort = "sq.r8.book_short", Book = "sq.r8.book", Wall = "sq.r8.wall", Shop = "sq.r8.shop",
            AheMet = "sq.r8.ahe", AheChild = "sq.r8.ahe_child", Roll = "sq.r8.roll", RollDone = "sq.r8.roll_done",
            MatsMet = "sq.r8.mats", MatsRice = "sq.r8.mats_rice", MatsDone = "sq.r8.mats_done";
        public const string FoundPrefix = "sq.r8.found.";
        public static readonly string[] Shi = { "dy_p2", "dy_p3", "dy_p4", "dy_p5", "dy_p6", "dy_p7", "dy_p8", "dy_p9", "dy_p10" };

        static bool Open(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            return ch.Step == ChapterStep.Xiapi && ch.Has(ChapterScripts5.XiapiGreeted) || ch.Step == ChapterStep.Echo;
        }
        static bool Echo(FieldStoryController c) => c.Run.Chapter2.Step == ChapterStep.Echo;

        // a_he's state at Xiapi (the card's 状态判定): null when the boy was never met.
        static string AheState(ChapterState ch)
        {
            string bridge = Recall(ch, "a_he", "jieqiao");
            if (bridge == null) return Recall(ch, "a_he", "pingyuan_name") != null ? (Recall(ch, "a_he", "leave_pingyuan") == "stay" ? "stay" : "follow") : null;
            if (bridge == "dead") return "dead";
            return Recall(ch, "a_he", "leave_pingyuan") == "stay" ? "stay" : "follow";
        }

        public static bool? Present(FieldStoryController c, string id)
        {
            var ch = c.Run.Chapter2;
            switch (id)
            {
                case "gu_sou": case "zhang_shiping": case "su_shuang": case "chu_weng": case "pan_daqiao": return Open(c);
                case "a_he": return Open(c) && AheState(ch) == "follow";
                case "dy_p2": case "dy_p3": case "dy_p4": case "dy_p5": case "dy_p6": case "dy_p7": case "dy_p8": case "dy_p9": case "dy_p10":
                    return Open(c);
                case "yao_heng": return Open(c) && ch.Has(MatsRice) && !ch.Has(MatsDone);
                case "wall_stairs": return Echo(c);
                default: return null;
            }
        }

        public static string Prompt(FieldStoryController c, string id)
        {
            var ch = c.Run.Chapter2;
            if (!Open(c)) return null;
            switch (id)
            {
                case "gu_sou": return ch.Has(Book) ? null : "raw:[确认] 坐下听鼓叟说书《织席郎》";
                case "wall_stairs": return ch.Has(Wall) ? null : "raw:[确认] 登上城头";
                case "zhang_shiping": case "su_shuang": return ch.Has(Shop) ? null : "raw:[确认] 与中山马商叙旧";
                case "pan_daqiao": return ch.Has(RollDone) ? null : "raw:[确认] 与丹杨兵什长交谈";
                case "chu_weng": return ch.Has(MatsDone) ? null : "raw:[确认] 与施粥的老人交谈";
                case "mi_zhu": return ch.Has(MatsMet) && !ch.Has(MatsRice) ? "raw:[确认] 向糜竺讨一车米（施粥）" : null;
                default: return null;
            }
        }

        public static bool Marked(FieldStoryController c, string id)
        {
            var ch = c.Run.Chapter2;
            if (!Open(c)) return false;
            return id switch
            {
                "gu_sou" => !ch.Has(Book) && (Echo(c) || !ch.Has(BookShort)),
                "wall_stairs" => false,   // the player's own choice (ND S12): no mark, no prompt to go
                "zhang_shiping" => !ch.Has(Shop),
                "a_he" => !ch.Has(AheMet) || ch.Has(AheMet) && !ch.Has(AheChild),
                "pan_daqiao" => !ch.Has(Roll) || Shi.All(p => ch.Has(FoundPrefix + p)) && !ch.Has(RollDone),
                "chu_weng" => !ch.Has(MatsMet) || ch.Has(MatsRice) && !ch.Has(MatsDone),
                "mi_zhu" => ch.Has(MatsMet) && !ch.Has(MatsRice),
                _ => Shi.Contains(id) && ch.Has(Roll) && !ch.Has(FoundPrefix + id),
            };
        }

        public static IEnumerable<StageStep> Talk(FieldStoryController c, string id)
        {
            var ch = c.Run.Chapter2;
            if (!Open(c)) return null;
            switch (id)
            {
                case "gu_sou": return GuSou(c);
                case "wall_stairs": return ch.Has(Wall) ? null : CityWall(c);
                case "zhang_shiping": case "su_shuang": return MiShop(c);
                case "a_he": return AHe(c);
                case "pan_daqiao": return PanDaqiao(c);
                case "chu_weng": return ChuWeng(c);
                case "mi_zhu": return ch.Has(MatsMet) && !ch.Has(MatsRice) ? MiRice(c) : null;
            }
            if (Shi.Contains(id)) return OneOfTen(c, id);
            return null;
        }

        // ---------- sq.lb1.L1.zhixilang 织席郎 ----------

        // One book episode: its line, the year node (one per node), the importance of the memory it comes from.
        static IEnumerable<(string key, int node, int imp)> Episodes(ChapterState ch)
        {
            string cart = Recall(ch, "lu_zhi", "cart");
            if (cart != null) yield return (cart == "escort" ? "ev.cart_escort" : "ev.cart_wait", 1, 3);
            string tale = Recall(ch, "gu_sou", "anxi_tale");
            if (tale == "truth") yield return (Recall(ch, "liu_bei", "duyou") == "B" ? "ev.anxi_truth_b" : "ev.anxi_truth", 2, 3);
            else if (tale == "let_be") yield return ("ev.anxi_let", 2, 3);
            yield return ("ev.hulao", 4, 3);
            if (Recall(ch, "gu_sou", "suanzao_cart") != null) yield return ("ev.suanzao", 4, 3);
            if (Recall(ch, "gu_sou", "heard_luoyang") != null) yield return ("ev.luoyang", 5, 3);
            if (Recall(ch, "liu_bei", "pingyuan_levee") == "done") yield return ("ev.pingyuan", 5, 3);
            string lp = Recall(ch, "liu_ping_ke", "let_go");
            if (lp == "yes") yield return ("ev.lp_go", 5, 3); else if (lp == "asked") yield return ("ev.lp_asked", 5, 2);
            switch (Recall(ch, "a_he", "jieqiao"))
            {
                case "held": yield return ("ev.ahe_held", 6, 4); break;
                case "wounded": yield return ("ev.ahe_wounded", 6, 4); break;
                case "absent": yield return ("ev.ahe_absent", 6, 2); break;
                case "dead": yield return ("ev.ahe_dead", 6, 5); break;
            }
            if (Recall(ch, "lu_yu", "rubbing") == "gave_rubbing" || Recall(ch, "lu_yu", "rice") == "given") yield return ("ev.luyu", 6, 4);
            if (Recall(ch, "lu_zhi", "old_camp_grave") != null) yield return ("ev.luying", 6, 3);
            yield return ("ev.beihai", 7, 3);
            string why = Recall(ch, "gongsun_zan", "why_help");
            if (why != null) yield return (why == "people" ? "ev.gsz_people" : "ev.gsz_promise", 7, 3);
            if (Recall(ch, "zhang_shiping", "jinan_road") != null) yield return ("ev.horse", 7, 3);
            if (HasKeepsake(ch, "fengsu_copy")) yield return ("ev.fengsu", 7, 2);
            if (Recall(ch, "liu_bei", "L6_lousang") != null && Recall(ch, "liu_bei", "gaotang") != null && Recall(ch, "liu_bei", "L6_xiami") != null)
                yield return ("ev.oldland", 8, 3);
        }

        // The card's rule: by importance (ties: earlier year), one per year node, at most five, then told in year order.
        public static List<string> PickEpisodes(ChapterState ch)
        {
            var picked = new List<(string key, int node, int imp)>();
            foreach (var e in Episodes(ch).Select((e, i) => (e, i)).OrderByDescending(x => x.e.imp).ThenBy(x => x.e.node).ThenBy(x => x.i).Select(x => x.e))
            {
                if (picked.Count >= 5) break;
                if (picked.Any(p => p.node == e.node)) continue;
                picked.Add(e);
            }
            return picked.OrderBy(p => p.node).Select(p => "dlg.lb1.gu_sou.L1." + p.key).ToList();
        }

        static IEnumerable<StageStep> GuSou(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (ch.Has(Book)) { yield return L("gu_sou", "sq.r8.gu_sou.after"); yield break; }
            foreach (var s in Lines("gu_sou", "dlg.lb1.gu_sou.L1.open.1", "gu_sou", "dlg.lb1.gu_sou.L1.open.2", "gu_sou", "dlg.lb1.gu_sou.L1.ch1")) yield return s;
            if (!Echo(c))
            {
                yield return L("gu_sou", "sq.r8.gu_sou.short");
                yield return Flag(BookShort);
                yield break;
            }
            foreach (var key in PickEpisodes(ch)) yield return L("gu_sou", key);
            foreach (var s in Lines("gu_sou", "dlg.lb1.gu_sou.L1.close.1", "gu_sou", "dlg.lb1.gu_sou.L1.close.2", "gu_sou", "dlg.lb1.gu_sou.L1.ask")) yield return s;
            bool anxiLet = Recall(ch, "gu_sou", "anxi_tale") == "let_be" && Recall(ch, "liu_bei", "duyou") != "B";
            yield return Choice("liu_bei", "sq.r8.book.prompt", "sq_book_", new[] { "sq.r8.book.too_much", "sq.r8.book.too_little", "sq.r8.book.right" },
                new[] { L("gu_sou", "dlg.lb1.gu_sou.L1.ans.too_much"), Mem("gu_sou", "zhixilang_answer", "too_much") },
                new[] { L("gu_sou", "sq.r8.ans.too_little"), Mem("gu_sou", "zhixilang_answer", "too_little") },
                new[]
                {
                    L("gu_sou", "dlg.lb1.gu_sou.L1.ans.right"),
                    anxiLet ? L("gu_sou", "dlg.lb1.gu_sou.L1.ans.right.anxi") : L("narrator", "sq.r8.book.right.end"),
                    Mem("gu_sou", "zhixilang_answer", "right"),
                });
            yield return L("gu_sou", "dlg.lb1.gu_sou.L1.gift");
            yield return Flag(Book);
            yield return Mem("liu_bei", "heard_zhixilang", "yes");
            foreach (var s in Keepsake("gu_sou_drumsticks")) yield return s;
        }

        // ---------- sq.lb1.L2.a_he_6 背进城的孩子 ----------

        static IEnumerable<StageStep> AHe(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            string bridge = Recall(ch, "a_he", "jieqiao");
            if (Echo(c))
            {
                yield return L("a_he", "dlg.lb1.a_he.N8d.echo");
                if (Recall(ch, "tian_yu", "ahe_trust") == "trusted") yield return L("tian_yu", "dlg.lb1.tian_yu.N8d.ahe_trusted");
                if (!ch.Has(AheChild)) foreach (var s in KeepChild(c)) yield return s;
                yield break;
            }
            if (ch.Has(AheMet)) { foreach (var s in KeepChild(c)) yield return s; yield break; }
            yield return L("a_he", "dlg.lb1.a_he.N8b.1");
            if (bridge == "held" || bridge == "wounded" || bridge == "absent") yield return L("a_he", "dlg.lb1.a_he.N8b." + bridge);
            string gz = Recall(ch, "a_he", "guangzong");
            if (gz == "covered" || gz == "punished") yield return L("a_he", "dlg.lb1.a_he.N8b." + gz);
            yield return Flag(AheMet);
            foreach (var s in KeepChild(c)) yield return s;
        }

        static IEnumerable<StageStep> KeepChild(FieldStoryController c)
        {
            if (c.Run.Chapter2.Has(AheChild)) { yield return L("a_he", "dlg.lb1.a_he.N8c.1"); yield break; }
            yield return L("a_he", "sq.r8.ahe.ask");
            yield return Choice("liu_bei", "sq.r8.ahe.prompt", "sq_ahe_", new[] { "sq.r8.ahe.kept", "sq.r8.ahe.camp" },
                new[] { L("liu_bei", "sq.r8.ahe.kept.1"), Mem("a_he", "xiapi_child", "kept") },
                new[] { L("liu_bei", "sq.r8.ahe.camp.1"), Mem("a_he", "xiapi_child", "camp") });
            yield return Flag(AheChild);
            yield return L("a_he", "dlg.lb1.a_he.N8c.1");
        }

        // The other two states play where the card puts them: a stayed boy's word through Jian Yong, a dead boy's through
        // Tian Yu; both are echoes and go with those two people's own echoes (EchoExtras).
        public static IEnumerable<(string speaker, string key)> EchoExtras(ChapterState ch, string id)
        {
            string state = AheState(ch), bridge = Recall(ch, "a_he", "jieqiao");
            if (id == "jian_yong" && state == "stay")
            {
                yield return ("jian_yong", "dlg.lb1.jian_yong.N8d.ahe_stay.1");
                yield return ("jian_yong", "dlg.lb1.jian_yong.N8d.ahe_stay.2");
                if (bridge == "absent") yield return ("jian_yong", "dlg.lb1.jian_yong.N8d.ahe_stay.absent");
            }
            if (id == "tian_yu" && state == "dead")
                for (int i = 1; i <= 4; i++) yield return ("tian_yu", "dlg.lb1.tian_yu.N8d.ahe_dead." + i);
        }

        // ---------- sq.lb1.L5.mi_shop 使君的账 ----------

        static IEnumerable<StageStep> MiShop(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (ch.Has(Shop)) { yield return L("zhang_shiping", "dlg.lb1.zhang_shiping.N8b.2"); yield break; }
            yield return L("zhang_shiping", "dlg.lb1.zhang_shiping.N8b.1");
            string road = Recall(ch, "zhang_shiping", "jinan_road");
            if (road == "escorted") yield return L("su_shuang", "dlg.lb1.su_shuang.N8b.escort");
            else
            {
                // Refused at Jinan, or the escort never played (an extension-layer quest): the ledger is given here.
                if (road == null) yield return L("su_shuang", "dlg.lb1.su_shuang.N8b.base");
                yield return L("zhang_shiping", "dlg.lb1.zhang_shiping.N8b.ledger");
                yield return L("liu_bei", "dlg.lb1.liu_bei.L5.ledger");
                foreach (var s in Keepsake("zhang_shiping_ledger")) yield return s;
            }
            bool clean = Recall(ch, "su_shuang", "anxi_wine") == "declined" || Recall(ch, "zhang_shiping", "horse_deal") == "paid";
            yield return L("mi_zhu", clean ? "dlg.lb1.mi_zhu.L5.alt" : "dlg.lb1.mi_zhu.L5.1");
            yield return L("liu_bei", "dlg.lb1.liu_bei.L5.1");
            yield return L("mi_zhu", "dlg.lb1.mi_zhu.L5.2");
            if (Recall(ch, "lu_yu", "rubbing") == "gave_rubbing" || Recall(ch, "lu_yu", "rice") == "given")
            {
                yield return L("zhang_shiping", "dlg.lb1.zhang_shiping.N8b.luyu");
                yield return L("jian_yong", "dlg.lb1.echo.lu_yu.1");
            }
            yield return L("zhang_shiping", "dlg.lb1.zhang_shiping.N8b.2");
            yield return Reputation(1);
            yield return Mem("mi_zhu", "horse_merchants", "yes");
            yield return Mem("zhang_shiping", "xiapi", "yes");
            yield return Flag(Shop);
        }

        // ---------- sq.lb1.L6.city_wall 城头 ----------

        static IEnumerable<StageStep> CityWall(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            bool lousang = Recall(ch, "liu_bei", "L6_lousang") != null, gaotang = Recall(ch, "liu_bei", "gaotang") != null, xiami = Recall(ch, "liu_bei", "L6_xiami") != null;
            int n = (lousang ? 1 : 0) + (gaotang ? 1 : 0) + (xiami ? 1 : 0);
            yield return Flag(Wall);
            yield return Fade(1f, 0.4f);
            yield return Card("sq.r8.wall.card", 1.6f);
            yield return Fade(0f, 0.4f);
            if (n == 0) { yield return Wait(2.5f); yield return L("narrator", "sq.r8.wall.wind"); yield break; }
            if (n == 3)
            {
                foreach (var k in new[] { "v3.1", "v3.2", "v3.3", "v3.4" }) yield return L("liu_bei", "dlg.lb1.liu_bei.L6." + k);
                yield return Mem("liu_bei", "wall_monologue", "full");
            }
            else
            {
                if (lousang) yield return L("liu_bei", "dlg.lb1.liu_bei.L6.v3.1");
                if (gaotang) yield return L("liu_bei", "dlg.lb1.liu_bei.L6.v3.2");
                if (xiami) yield return L("liu_bei", "sq.r8.wall.xiami_short");
                yield return L("liu_bei", "dlg.lb1.liu_bei.L6.v2.a");
                yield return L("liu_bei", "dlg.lb1.liu_bei.L6.v2.b");
                yield return Mem("liu_bei", "wall_monologue", "short");
            }
            yield return L("narrator", "sq.r8.wall.end");
        }

        // ---------- sq.lb1.r8.danyang_names 什伍名籍 ----------

        static IEnumerable<StageStep> PanDaqiao(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (ch.Has(RollDone))
            {
                yield return L("pan_daqiao", Echo(c) ? (Recall(ch, "pan_daqiao", "names_asked") == "by_name" ? "dlg.lb1.pan_daqiao.N8d.by_name.1" : "dlg.lb1.pan_daqiao.N8d.1") : "dlg.lb1.pan_daqiao.N8c.1");
                yield break;
            }
            if (!ch.Has(Roll))
            {
                foreach (var s in Lines("pan_daqiao", "dlg.lb1.pan_daqiao.N8b.1", "pan_daqiao", "dlg.lb1.pan_daqiao.N8b.2", "pan_daqiao", "dlg.lb1.pan_daqiao.N8b.3",
                             "narrator", "sq.r8.roll.book", "tian_yu", "sq.r8.roll.tianyu"))
                    yield return s;
                yield return Flag(Roll);
                yield return L("system", "sq.r8.roll.goal");
                yield break;
            }
            int found = Shi.Count(p => ch.Has(FoundPrefix + p));
            if (found < Shi.Length) { yield return L("pan_daqiao", "sq.r8.roll.left"); yield return L("system", $"raw:还没认到的：{Shi.Length - found} 人"); yield break; }
            yield return L("pan_daqiao", "sq.r8.roll.all");
            yield return Choice("liu_bei", "sq.r8.roll.prompt", "sq_roll_", new[] { "sq.r8.roll.by_name", "sq.r8.roll.by_count" },
                new[]
                {
                    L("tian_yu", "sq.r8.roll.by_name.tianyu"), L("pan_daqiao", "dlg.lb1.pan_daqiao.N8b.by_name.1"),
                    Mem("pan_daqiao", "names_asked", "by_name"), Mem("tian_yu", "danyang_roll", "by_name"), Mem("tao_qian", "danyang_names", "yes"),
                    Flag(KeepsakePrefix + "pan_daqiao_tags"), L("system", "sq.keepsake.pan_daqiao_tags"),
                },
                new[]
                {
                    L("tian_yu", "sq.r8.roll.by_count.tianyu"), L("pan_daqiao", "dlg.lb1.pan_daqiao.N8b.by_count.1"),
                    Mem("pan_daqiao", "names_asked", "by_count"), Mem("tian_yu", "danyang_roll", "by_count"), Reputation(1),
                });
            yield return Flag(RollDone);
        }

        static IEnumerable<StageStep> OneOfTen(FieldStoryController c, string id)
        {
            var ch = c.Run.Chapter2;
            yield return L(id, "dlg.lb1.r8.danyang." + id.Substring(3));
            if (ch.Has(Roll) && !ch.Has(FoundPrefix + id) && !ch.Has(RollDone))
            {
                yield return Flag(FoundPrefix + id);
                yield return L("system", "sq.r8.roll.found." + id.Substring(3));
            }
        }

        // ---------- sq.lb1.r8.spread_mats 布席于路 ----------

        static IEnumerable<StageStep> ChuWeng(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (ch.Has(MatsDone))
            {
                string v = Recall(ch, "chu_weng", "spread_mats");
                yield return L("chu_weng", v == "shrine" ? "dlg.lb1.chu_weng.N8c.shrine.1" : "dlg.lb1.chu_weng.N8c.official.1");
                yield break;
            }
            if (!ch.Has(MatsMet))
            {
                for (int i = 1; i <= 5; i++) yield return L("chu_weng", "dlg.lb1.chu_weng.N8b." + i);
                yield return L("chu_weng", "sq.r8.mats.rice");
                yield return Flag(MatsMet);
                yield return L("system", "sq.r8.mats.goal");
                yield break;
            }
            if (!ch.Has(MatsRice)) { yield return L("chu_weng", "sq.r8.mats.wait"); yield break; }
            // The rice is here; Chen Deng's clerk comes to seal the shrine.
            yield return L("chu_weng", "sq.r8.mats.thanks");
            yield return L("yao_heng", "sq.r8.mats.seal");
            yield return Choice("liu_bei", "sq.r8.mats.prompt", "sq_mats_", new[] { "sq.r8.mats.official", "sq.r8.mats.shrine" },
                new[]
                {
                    L("liu_bei", "sq.r8.mats.official.1"), L("yao_heng", "sq.r8.mats.official.2"), L("chen_deng", "sq.r8.mats.official.chen"),
                    L("chu_weng", "dlg.lb1.chu_weng.N8c.official.1"),
                    Mem("chu_weng", "spread_mats", "official"), Mem("chen_deng", "futu_shrine", "by_rule"),
                },
                new[]
                {
                    L("liu_bei", "sq.r8.mats.shrine.1"), L("yao_heng", "sq.r8.mats.shrine.2"), L("chen_deng", "sq.r8.mats.shrine.chen"),
                    L("chu_weng", "dlg.lb1.chu_weng.N8c.shrine.1"),
                    Mem("chu_weng", "spread_mats", "shrine"), Mem("chen_deng", "futu_shrine", "soft"),
                });
            yield return L("chu_weng", "sq.r8.mats.gift");
            yield return L("liu_bei", "sq.r8.mats.feel");
            yield return L("chu_weng", "sq.r8.mats.feel.2");
            yield return Flag(MatsDone);
            foreach (var s in Keepsake("chu_weng_mat")) yield return s;
        }

        static IEnumerable<StageStep> MiRice(FieldStoryController c)
        {
            yield return L("liu_bei", "sq.r8.mats.ask");
            yield return L("mi_fang", "sq.r8.mats.mifang");
            yield return L("mi_zhu", "sq.r8.mats.mizhu");
            yield return Flag(MatsRice);
            yield return L("system", "sq.r8.mats.goal2");
        }
    }
}
