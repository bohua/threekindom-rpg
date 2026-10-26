using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Save;
using static ThreeKingdoms.Gameplay.Story.S;

namespace ThreeKingdoms.Gameplay.Story
{
    // The Pingyuan base in the Bohai plain (V04_HEBEI_PLAN §3.1, SYSTEMS_V04 §5, REVIEW_V04 R11–R12, R14, R19–R20).
    // World X / Z of the probe's Bohai_Plain scene (BohaiPlainBuilder): the county office steps at (−60, −140), its square
    // to z −158, the barracks door near (−28, −147), the market west of the square, the south gate (−60, −195), the
    // Pingyuan ferry beyond the levee, the west road sign at (−390, 24).
    public static class FieldScripts4
    {
        public static bool Mine(FieldStoryController c) => c.Location == FieldStoryController.Place.Pingyuan;
        public const string RegionLocation = SaveData.RegionPrefix + "R4";

        // ---------- arrival ----------

        public static IEnumerable<StageStep> OnArrive(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            if (ch.Step == ChapterStep.Pingyuan && !ch.Has(ChapterScripts4.PingyuanGreeted)) return Arrival();
            if (ch.Step == ChapterStep.Zhending && !ch.Has(ChapterScripts4.Assassin)) return AssassinScene(c);
            if (ch.Step == ChapterStep.Pingyuan || ch.Step == ChapterStep.PingyuanSafe || ch.Step == ChapterStep.Zhending)
                return new[] { Autosave(RegionLocation) };
            return null;
        }

        // E3 赴任：the townsfolk gather before the county office; Liu Bei takes the steps (R11 回收“地还没有”).
        static IEnumerable<StageStep> Arrival()
        {
            yield return Autosave(RegionLocation);
            yield return Show("liu_bei", -60f, -156f, "back");
            yield return Show("guan_yu", -62f, -158f, "back");
            yield return Show("zhang_fei", -58f, -158f, "back");
            foreach (var s in Lines("py_clerk", "e3.1", "py_man", "e3.2", "py_woman", "e3.3")) yield return s;
            yield return Move("liu_bei", -60f, -146f, 1.6f);
            yield return Face("liu_bei", "front");
            foreach (var s in Lines("liu_bei", "e3.4", "py_elder", "e3.5", "liu_bei", "e3.6", "guan_yu", "e3.7", "liu_bei", "e3.8")) yield return s;
            yield return Remember("pingyuan_folk", "pingyuan_arrival", "seen", "mem.pingyuan_arrival");
            foreach (var s in Lines("jian_yong", "e3.9", "tian_yu", "e3.10")) yield return s;
            yield return Flag(ChapterScripts4.PingyuanGreeted);
            yield return L("py_courier", "e4.1");
            yield return L("liu_bei", "e4.2");
        }

        // E9 平原相与刺客刘平（史实·先主传；“待客甚厚”裴注引《魏书》）; choice ⑦; Zhang Fei held back (R12).
        static IEnumerable<StageStep> AssassinScene(FieldStoryController c)
        {
            yield return Autosave(RegionLocation);
            yield return Show("liu_bei", -60f, -150f, "front");
            yield return Show("guan_yu", -63f, -152f, "right");
            yield return Show("zhang_fei", -57f, -152f, "left");
            foreach (var s in Lines("narrator", "e9.1", "py_clerk", "e9.2", "liu_bei", "e9.3")) yield return s;
            yield return Show("assassin", -60f, -156f, "back");
            foreach (var s in Lines("assassin", "e9.4", "liu_bei", "e9.5", "narrator", "e9.6", "assassin", "e9.7", "zhang_fei", "e9.8", "liu_bei", "e9.9"))
                yield return s;
            yield return Remember("zhang_fei", "zhangfei_assassin", "held_back", "mem.zhangfei_assassin");
            yield return Choice("liu_bei", "e9.prompt", ChapterScripts4.AssassinChoice, new[] { "e9.a", "e9.b" },
                new[] { L("liu_bei", "e9.a.1"), L("assassin", "e9.a.2"), Reputation(1), Remember("liu_ping", "liuping_assassin", "A_free", "mem.liuping.A") },
                new[] { L("liu_bei", "e9.b.1"), L("assassin", "e9.b.2"), L("guan_yu", "e9.b.3"), Remember("liu_ping", "liuping_assassin", "B_asked", "mem.liuping.B") });
            yield return Hide("assassin");
            foreach (var s in Lines("narrator", "e9.10", "jian_yong", "e9.11", "liu_bei", "e9.12")) yield return s;
            yield return Flag(ChapterScripts4.Assassin);
            yield return Fade(1f);
            yield return Load(GameRun.StageScene, ChapterScripts4.End04);
        }

        static StageStep Show(string id, float x, float z, string facing) => Actor(id, null, x, z, facing);

        // ---------- prompts, marks, goals ----------

        public static string Prompt(FieldStoryController c, string id)
        {
            var ch = c.Run.Chapter2;
            switch (id)
            {
                case "py_clerk": return "raw:[确认] 与县吏交谈（存档）";
                case "jian_yong": return "raw:[确认] 与简雍交谈（情报）";
                case "tian_yu": return "raw:[确认] 与田豫交谈（兵营训练）";
                case "py_courier": return ch.Step == ChapterStep.Pingyuan ? "raw:[确认] 出城迎击流寇" : "talk:" + id;
                case "py_ferry": return ch.Step == ChapterStep.PingyuanSafe ? "raw:[确认] 渡河北上（随公孙瓒拒袁绍）" : "talk:" + id;
                case "canal": return ch.Has(ChapterScripts4.Canal) ? null : "raw:[确认] 看看水渠";
                case "py_merchant": return "raw:[确认] 与商贩交谈（买卖）";
                default: return "talk:" + id;
            }
        }

        public static bool Marked(FieldStoryController c, string id)
        {
            var ch = c.Run.Chapter2;
            return id switch
            {
                "py_courier" => ch.Step == ChapterStep.Pingyuan,
                "py_ferry" => ch.Step == ChapterStep.PingyuanSafe,
                "jian_yong" => !ch.Has(RumourFlag(ch)),
                "tian_yu" => !ch.Has(TrainFlag(ch)),
                "py_elder" => !ch.Has(ChapterScripts4.Porridge),
                "py_farmer_a" => !ch.Has(ChapterScripts4.Canal),
                _ => false,
            };
        }

        public static string Objective(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            return ch.Step switch
            {
                ChapterStep.Pingyuan => "目标：流寇犯境——到南门找驿卒，出城迎击",
                ChapterStep.PingyuanSafe => "目标：整顿平原，准备好后到南门外平原津渡河北上",
                _ => "目标：回县衙",
            };
        }

        static string RumourFlag(ChapterState ch) => ch.Has(ChapterState.Year192) ? ChapterScripts4.RumourX12 : ChapterScripts4.RumourX11;
        static string TrainFlag(ChapterState ch) => "train_" + (int)ch.Step;

        // ---------- talking ----------

        public static IEnumerable<StageStep> Talk(FieldStoryController c, string id)
        {
            var ch = c.Run.Chapter2;
            var run = c.Run;
            switch (id)
            {
                case "py_clerk":
                    return new[]
                    {
                        L("py_clerk", "py.clerk"),
                        Choice("py_clerk", "py.clerk.ask", "pyc_", new[] { "gs.opt.save", "gs.opt.cancel" },
                            new[] { Call(() => c.pendingPanel = TownStoryController.SavePanel) }, new StageStep[0]),
                    };
                case "jian_yong":
                {
                    string f = RumourFlag(ch);
                    if (ch.Has(f)) return new[] { L("jian_yong", "py.jianyong.done") };
                    string key = f == ChapterScripts4.RumourX12 ? "rumor.x12" : "rumor.x11";
                    return new[] { L("jian_yong", "py.jianyong.1"), L("narrator", key + ".1"), L("narrator", key + ".2"), L("jian_yong", key + ".3"), Flag(f) };
                }
                case "tian_yu":
                {
                    string f = TrainFlag(ch);
                    if (ch.Has(f)) return new[] { L("tian_yu", "py.tianyu.done") };
                    return new[]
                    {
                        L("tian_yu", "py.tianyu.1"),
                        Choice("liu_bei", "py.tianyu.ask", "pyt_", new[] { "py.tianyu.yes", "gs.opt.cancel" },
                            new[]
                            {
                                Fade(1f, 0.4f),
                                Call(() => run.Progress.RestoreExp(run.Data.Party.Select(g => (g.Id, run.Progress.Exp(g) + 30)))),
                                Flag(f), Fade(0f, 0.4f), L("system", "py.tianyu.result"),
                            },
                            new StageStep[0]),
                    };
                }
                case "py_elder": return Porridge(c);
                case "py_farmer_a": case "py_farmer_b": case "canal": return CanalCase(c, id);
                case "py_courier":
                    if (ch.Step != ChapterStep.Pingyuan) return new[] { L("py_courier", "py.courier.after") };
                    return new[]
                    {
                        L("py_courier", "py.courier.1"),
                        Choice("system", "py.courier.ask", "pyw_", new[] { "py.courier.yes", "gs.leave.no" }, new[] { Fade(1f), Fight("battle_9") }, new StageStep[0]),
                    };
                case "py_ferry":
                    if (ch.Step != ChapterStep.PingyuanSafe) return new[] { L("py_ferry", "py.ferry.before") };
                    return new[]
                    {
                        L("py_ferry", "py.ferry.1"),
                        Choice("system", "py.ferry.ask", "pyf_", new[] { "py.ferry.yes", "gs.leave.no" },
                            new[] { Fade(1f), Load(GameRun.StageScene, ChapterScripts4.ToPanhe) }, new StageStep[0]),
                    };
                case "py_post_guard": return new[] { L("py_post_guard", "py.postguard") };
                case "py_merchant": return new[] { L("py_merchant", "py.merchant"), Call(() => c.pendingPanel = TownStoryController.ShopPanel) };
                case "py_man": case "py_woman": case "py_child":
                    return new[] { L(id, ch.Step >= ChapterStep.PingyuanSafe ? $"py.{id}.after" : $"py.{id}") };
            }
            return null;
        }

        // 施粥 (REVIEW_V04 R19: the porridge stall stands off the square).
        static IEnumerable<StageStep> Porridge(FieldStoryController c)
        {
            var ch = c.Run.Chapter2;
            var run = c.Run;
            if (ch.Has(ChapterScripts4.Porridge)) { yield return L("py_elder", "py.porridge.done"); yield break; }
            yield return L("py_elder", "py.porridge.1");
            yield return Choice("liu_bei", "py.porridge.prompt", "pyp_", new[] { "py.porridge.copper", "py.porridge.no" },
                new[]
                {
                    Call(() => { if (run.Progress.Inventory.SpendCopper(30)) { ch.Set(ChapterScripts4.Porridge); run.Life.AddReputation(1); } }),
                    L("py_elder", "py.porridge.thanks").When(ChapterScripts4.Porridge),
                    L("system", "py.porridge.poor").When("!" + ChapterScripts4.Porridge),
                },
                new[] { L("py_elder", "py.porridge.later") });
        }

        // 争渠: two villages quarrel over the canal; the water's run shows whose field it was dug for. A wrong verdict can be retried.
        static IEnumerable<StageStep> CanalCase(FieldStoryController c, string id)
        {
            var ch = c.Run.Chapter2;
            var run = c.Run;
            if (ch.Has(ChapterScripts4.Canal)) { yield return L("py_farmer_a", "py.canal.done"); yield break; }
            if (id == "canal")
            {
                yield return L("system", "py.canal.look");
                yield return Flag(ChapterScripts4.CanalLooked);
                yield break;
            }
            yield return L("py_farmer_a", "py.canal.1");
            yield return L("py_farmer_b", "py.canal.2");
            var right = new[] { L("liu_bei", "py.canal.right"), L("py_farmer_b", "py.canal.ashamed"), Call(() => { ch.Set(ChapterScripts4.Canal); run.Life.AddReputation(1); }) };
            var wrong = new[] { L("system", "py.canal.wrong") };
            yield return Choice("liu_bei", "py.canal.prompt", "pyq_", new[] { "py.canal.a", "py.canal.b" }, right, wrong);
        }
    }
}
