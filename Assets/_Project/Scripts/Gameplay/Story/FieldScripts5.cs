using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Save;
using static ThreeKingdoms.Gameplay.Story.S;

namespace ThreeKingdoms.Gameplay.Story
{
    // Xiapi, seat of Xu province (V05_XUZHOU_PLAN §3, SYSTEMS_V05 §4, REVIEW_V05 R6, R13–R14, R17, R25–R26). World X / Z
    // of ChapterFieldBuilder5's scene: the east gate at z −40 (the brothers come in there), the market z −30…−10 with the
    // Mi family shop (x −22) and the post station (x 22) at its sides, the hall square z 4…21, the hall steps at z 22;
    // the Chen house (x −22) and the guest house (x 22) on the square's sides. Tao Qian stands at the steps until he falls
    // ill (F9); after that a clerk keeps the door and the post station is where to save.
    public static class FieldScripts5
    {
        public static bool Mine(FieldStoryController c) => c.Location == FieldStoryController.Place.Xiapi;

        // The echoes (SYSTEMS_V05 §4): who can be talked to in the Echo step, each heard once for the count.
        public static readonly string[] Echoes = { "mi_zhu", "chen_deng", "sun_qian", "kong_rong", "xp_refugee", "xp_old_soldier", "xp_scholar", "xp_merchant", "jian_yong", "tian_yu", "zhao_yun" };
        public const string EchoPrefix = "echo:";

        // ---------- arrival ----------

        public static IEnumerable<StageStep> OnArrive(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (ch.Step == ChapterStep.Xiapi && !ch.Has(ChapterScripts5.XiapiGreeted)) return Arrival();
            if (ch.Step == ChapterStep.Echo) return EchoArrival(c);
            return new[] { Autosave(SaveData.XiapiLocation) };
        }

        // F6 陶谦正堂相迎（丹杨兵四千，“去楷归谦”；一让）, F7 致曹操书.
        static IEnumerable<StageStep> Arrival()
        {
            yield return Autosave(SaveData.XiapiLocation);
            yield return Move("liu_bei", 0f, 15f, 3.2f);
            yield return Show("guan_yu", -1.8f, 14f, "back");
            yield return Show("zhang_fei", 1.8f, 14f, "back");
            yield return Show("zhao_yun", 3.4f, 13.4f, "back");
            yield return Face("liu_bei", "back");
            foreach (var s in Lines("tao_qian", "f6.1", "liu_bei", "f6.2", "tao_qian", "f6.3", "narrator", "f6.4", "guan_yu", "f6.5", "tao_qian", "f6.6",
                "liu_bei", "f6.7", "mi_zhu", "f6.8", "tao_qian", "f6.9"))
                yield return s;
            yield return Remember("tao_qian", "taoqian_first", "declined", "mem.taoqian_first");
            foreach (var s in Lines("liu_bei", "f7.1", "chen_deng", "f7.2", "narrator", "f7.3")) yield return s;
            yield return Flag(ChapterScripts5.XiapiGreeted);
            yield return Show("zhao_yun", -8f, 14f, "front");
            yield return L("system", "f6.goal");
        }

        // After F9: Tao Qian is ill, the clerk keeps the hall door; the first time, a word on the echoes.
        static IEnumerable<StageStep> EchoArrival(FieldStoryController c)
        {
            yield return Autosave(SaveData.XiapiLocation);
            yield return Show("tao_qian", 0f, -300f, "front");
            yield return Show("xp_clerk", 0f, 20.6f, "front");
            if (!c.Run.Chapter2.Has("f10"))
            {
                yield return L("narrator", "f10.1");
                yield return L("liu_bei", "f10.2");
                yield return Flag("f10");
            }
        }

        static StageStep Show(string id, float x, float z, string facing) => Actor(id, null, x, z, facing);

        // ---------- prompts, marks, goals ----------

        public static string Prompt(FieldStoryController c, string id)
        {
            var ch = c.Run.Chapter2;
            switch (id)
            {
                case "xp_officer": return ch.Step == ChapterStep.Xiapi && ch.Has(ChapterScripts5.XiapiGreeted) ? "raw:[确认] 登城迎战曹仁" : "talk:" + id;
                case "sun_qian": return "raw:[确认] 与孙乾交谈（存档" + (ch.PatrolOpen ? "／小沛巡防" : "") + "）";
                case "mi_zhu": return "raw:[确认] 与糜竺交谈（糜家商号·买卖）";
                case "xp_clerk": return ch.Step == ChapterStep.Echo ? "raw:[确认] 入州府正堂" : "talk:" + id;
                default: return "talk:" + id;
            }
        }

        public static bool Marked(FieldStoryController c, string id)
        {
            var ch = c.Run.Chapter2;
            if (ch.Step == ChapterStep.Xiapi) return id == "xp_officer" && ch.Has(ChapterScripts5.XiapiGreeted);
            if (ch.Step != ChapterStep.Echo) return false;
            if (id == "xp_clerk") return true;
            if (id == "sun_qian" && ch.PatrolOpen) return true;
            return Echoes.Contains(id) && !ch.Has(EchoPrefix + id);
        }

        public static int EchoesHeard(ChapterState ch) => Echoes.Count(e => ch.Has(EchoPrefix + e));

        public static string Objective(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (ch.Step == ChapterStep.Xiapi)
                return ch.Has(ChapterScripts5.XiapiGreeted) ? "目标：曹仁兵临城下——整备后到东门找守门军官，登城迎战" : "目标：拜见陶谦";
            if (ch.Step == ChapterStep.Echo)
                return $"目标：在下邳城中走走（回声 {EchoesHeard(ch)}/{Echoes.Length}）；准备好后到州府门前找州府吏"
                    + (ch.PatrolOpen ? "；孙乾处可接小沛巡防（选做）" : "");
            return "目标：回州府";
        }

        // ---------- talking ----------

        public static IEnumerable<StageStep> Talk(FieldStoryController c, string id)
        {
            var ch = c.Run.Chapter2;
            switch (id)
            {
                case "tao_qian": return new[] { L("tao_qian", ch.Has(ChapterScripts5.XiapiGreeted) ? "xp.tao.after" : "f6.1") };
                case "xp_officer":
                    if (ch.Step != ChapterStep.Xiapi || !ch.Has(ChapterScripts5.XiapiGreeted)) return new[] { L("xp_officer", "xp.officer.after") };
                    return new[]
                    {
                        L("xp_officer", "xp.officer.1"),
                        Choice("system", "xp.officer.ask", "xpo_", new[] { "xp.officer.yes", "gs.leave.no" }, new[] { Fade(1f), Fight("battle_14") }, new StageStep[0]),
                    };
                case "xp_clerk":
                    if (ch.Step != ChapterStep.Echo) return new[] { L("xp_clerk", "xp.clerk.before") };
                    return new[]
                    {
                        L("xp_clerk", "xp.clerk.1"),
                        Choice("system", "xp.clerk.ask", "xpc_", new[] { "xp.clerk.yes", "gs.leave.no" },
                            new[] { Fade(1f), Load(GameRun.StageScene, ChapterScripts5.Sanrang) }, new StageStep[0]),
                    };
                case "sun_qian": return SunQian(c);
                case "mi_zhu":
                    return Echo(c, id).Append(Call(() => c.pendingPanel = TownStoryController.ShopPanel));
            }
            if (Echoes.Contains(id)) return Echo(c, id);
            return new[] { L(id, "xp." + id) };
        }

        // 孙乾 at the post station: save; in the Echo step the optional Xiaopei patrol (battle 15, REVIEW_V05 R8).
        static IEnumerable<StageStep> SunQian(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            foreach (var s in Echo(c, "sun_qian")) yield return s;
            var save = new[] { Call(() => c.pendingPanel = TownStoryController.SavePanel) };
            if (ch.PatrolOpen)
                yield return Choice("sun_qian", "xp.sunqian.ask", "xps_", new[] { "gs.opt.save", "xp.sunqian.patrol", "gs.opt.cancel" },
                    save, new[] { L("sun_qian", "xp.sunqian.patrol.1"), Fade(1f), Fight("battle_15") }, new StageStep[0]);
            else
                yield return Choice("sun_qian", "xp.sunqian.ask", "xps_", new[] { "gs.opt.save", "gs.opt.cancel" }, save, new StageStep[0]);
        }

        // One person's echo: the first variant whose condition holds (REVIEW_V05 R14: each has a default). Before the Echo
        // step they say their ordinary line.
        static IEnumerable<StageStep> Echo(FieldStoryController c, string id)
        {
            var ch = c.Run.Chapter2;
            if (ch.Step != ChapterStep.Echo) { yield return L(id, "xp." + id); yield break; }
            foreach (var (speaker, key) in EchoLines(c.Run, id)) yield return L(speaker, key);
            yield return Flag(EchoPrefix + id);
        }

        public static IEnumerable<(string speaker, string key)> EchoLines(GameRun run, string id)
        {
            var ch = run.Chapter2;
            string v = id switch
            {
                "xp_old_soldier" => ch.Recall("dong_zhuo", "dongzhuo_baishen") != null ? "baishen" : "plain",
                "xp_scholar" => ch.Recall("lu_zhi", "luzhi_cart") is string cart ? (cart == "B_escort" ? "b" : "a") : "plain",
                "jian_yong" => ch.Recall("liu_ping", "liuping_assassin") is string lp ? (lp == "A_free" ? "a" : "b") : "plain",
                "xp_merchant" => ChapterState.AnxiCommissions.Count(ch.Has) switch { 3 => "all", 0 => "none", _ => "some" },
                "zhao_yun" => ch.Recall("zhao_yun", "zhaoyun_zhending") != null ? "home" : "plain",
                "kong_rong" => ch.Recall("kong_rong", "kongrong_saved") != null ? "saved" : "plain",
                _ => "plain",
            };
            string speaker = id;
            yield return (speaker, $"echo.{id}.{v}");
            yield return (speaker, $"echo.{id}.{v}.2");
            foreach (var extra in SideQuestsR8.EchoExtras(ch, id)) yield return extra;
        }
    }
}
