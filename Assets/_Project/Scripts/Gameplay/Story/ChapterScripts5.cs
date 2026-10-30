using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Story;
using static ThreeKingdoms.Gameplay.Story.S;

namespace ThreeKingdoms.Gameplay.Story
{
    // The 0.5 徐州篇 cutscenes on the story stage (design/v0.5-xuzhou/V05_XUZHOU_PLAN.md §2, §5; REVIEW_V05.md). Keys only;
    // ChapterText5 holds the words. The settlement cards are written from the play-through when the script starts.
    public static class ChapterScripts5
    {
        public const string ToDuchang = "to_duchang", AfterDuchang = "after_duchang", AfterXiapi14 = "after_xiapi14", Sanrang = "sanrang", SettlementPreview = "chapter_settlement";
        // Choice flags: ⑧ Taishi Ci's plea, ⑨ how Liu Bei meets Lü Bu, the battle-12 formation, the seal.
        public const string PleaChoice = "f1_", LuBuChoice = "f13_", SealChoice = "f11_";
        public const string Formation = "f12_";
        // Field flags.
        public const string TaishiArrived = "f1", XiapiGreeted = "f6", TaishiToken = "taishi_token";
        public const string ScrollBeihai = "beihai", ScrollSanrang = "sanrang";

        public static IReadOnlyList<StageStep> Stage(string id) => id switch
        {
            ToDuchang => ToDuchangSteps().ToList(),
            AfterDuchang => AfterDuchangSteps().ToList(),
            AfterXiapi14 => AfterXiapi14Steps().ToList(),
            Sanrang => SanrangSteps().ToList(),
            SettlementPreview => SettlementSteps().ToList(),
            _ => null,
        };

        static GameRun Run => GameRun.Current;
        static bool Has(string f) => Run != null && Run.Chapter2.Has(f);

        // From Pingyuan (F1 played in the field): the march to Duchang, the formation, battle 12.
        static IEnumerable<StageStep> ToDuchangSteps()
        {
            yield return Background("map");
            yield return Fade(0f);
            yield return March("pingyuan", "duchang", "year.193");
            yield return Card("card.duchang");
            yield return Background("duchang_outside");
            yield return Choice("liu_bei", "f12.prompt", Formation, new[] { "f12.a", "f12.b", "f12.c" },
                new[] { L("liu_bei", "f12.a.1") }, new[] { L("liu_bei", "f12.b.1") }, new[] { L("liu_bei", "f12.c.1") });
            yield return Fade(1f);
            yield return Fight("battle_12");
        }

        // After battle 12: scroll ⑨, X14 (two accounts), F2 Kong Rong's feast, F3 Taishi Ci leaves, F4 the decision; 194:
        // F5 the refugee road, F5b Cao Bao (Zhang Fei holds himself back), battle 13.
        static IEnumerable<StageStep> AfterDuchangSteps()
        {
            yield return Scroll(ScrollBeihai);
            yield return Background("map");
            yield return Fade(0f);
            yield return Chronicle("year.193", "y193");
            yield return L("narrator", "f2x.1");
            yield return ChronicleEvent("xuzhou", "tao", "ev.caosong");
            yield return L("narrator", "f2x.2");
            yield return L("narrator", "f2x.3");
            yield return EndChronicle();
            yield return Fade(1f);
            yield return Background("beihai_hall");
            yield return Actor("kong_rong", "NpcKongRong", 0.5f, 0.52f, "front");
            yield return Actor("mi_zhu", "NpcMiZhu", 0.66f, 0.44f, "left");
            yield return Actor("taishi_ci", "NpcTaishiCi", 0.3f, 0.44f, "right");
            yield return Actor("liu_bei", "LiuBei", 0.46f, 0.26f, "back");
            yield return Actor("guan_yu", "GuanYu", 0.36f, 0.22f, "back");
            yield return Actor("zhang_fei", "ZhangFei", 0.56f, 0.22f, "back");
            yield return Fade(0f);
            foreach (var s in Lines("kong_rong", "f2.1", "liu_bei", "f2.2", "mi_zhu", "f2.3", "mi_zhu", "f2.4", "kong_rong", "f2.5")) yield return s;
            yield return Remember("kong_rong", "kongrong_beihai", "seen", "mem.kongrong_beihai");
            // F3 太史慈辞别（史实）; the keepsake for the immediate answer ⑧A when he fought at Duchang (REVIEW_V05 R19).
            foreach (var s in Lines("taishi_ci", "f3.1", "liu_bei", "f3.2", "taishi_ci", "f3.3")) yield return s;
            yield return Call(() =>
            {
                var ch = Run?.Chapter2;
                if (ch != null && ch.Has(PleaChoice + "0") && !ch.Has(Formation + "0")) ch.Set(TaishiToken);
            });
            yield return L("taishi_ci", "f3.token").When(TaishiToken);
            yield return Remember("taishi_ci", "taishici_pingyuan", "seen", "mem.taishici_pingyuan");
            yield return Hide("taishi_ci");
            yield return Fade(1f);
            // F4 决断（公孙瓒处；借兵、借赵云为演义）
            yield return Background("gongsun_hall");
            yield return Actor("gongsun_zan", "NpcGongsunZan", 0.5f, 0.5f, "front");
            yield return Actor("tian_kai", "NpcTianKai", 0.66f, 0.44f, "left");
            yield return Actor("zhao_yun", "NpcZhaoYun", 0.32f, 0.44f, "right");
            yield return Actor("liu_bei", "LiuBei", 0.46f, 0.26f, "back");
            yield return Fade(0f);
            foreach (var s in Lines("gongsun_zan", "f4.1", "liu_bei", "f4.2", "gongsun_zan", "f4.3", "zhao_yun", "f4.4", "tian_kai", "f4.5", "guan_yu", "f4.6", "liu_bei", "f4.7"))
                yield return s;
            yield return Fade(1f);
            // 194
            yield return Flag(ChapterState.Year194);
            yield return Advance(ChapterStep.Beihai, ChapterStep.XuzhouRoad);
            yield return Background("map");
            yield return Fade(0f);
            yield return March("pingyuan", "xuzhou", "year.194");
            yield return Card("card.xuzhou");
            yield return Background("refugee_road");
            yield return Actor("liu_bei", "LiuBei", 0.5f, 0.3f, "right");
            yield return Actor("guan_yu", "GuanYu", 0.4f, 0.34f, "right");
            yield return Actor("zhang_fei", "ZhangFei", 0.4f, 0.24f, "right");
            yield return Actor("refugee", "NpcRefugee", 0.72f, 0.32f, "left");
            yield return Fade(0f);
            foreach (var s in Lines("narrator", "f5.1", "refugee", "f5.2", "liu_bei", "f5.3", "narrator", "f5.4")) yield return s;
            yield return Hide("refugee");
            // F5b 曹豹（REVIEW_V05 R1：张飞第一次自己忍住）
            yield return Actor("cao_bao", "NpcCaoBao", 0.74f, 0.34f, "left");
            foreach (var s in Lines("cao_bao", "f5b.1", "zhang_fei", "f5b.2", "narrator", "f5b.3", "zhang_fei", "f5b.4", "liu_bei", "f5b.5")) yield return s;
            yield return Remember("zhang_fei", "zhangfei_caobao", "held_himself", "mem.zhangfei_caobao");
            yield return Fade(1f);
            yield return Fight("battle_13");
        }

        // After battle 14: F8 the betrayal (chronicle), F9 Xiaopei and the second offer; the echoes in Xiapi.
        static IEnumerable<StageStep> AfterXiapi14Steps()
        {
            yield return Background("map");
            yield return Fade(0f);
            yield return Chronicle("year.194", "y194");
            yield return L("narrator", "f8.1");
            yield return ChronicleEvent("puyang", "lubu", "ev.lubu_yan");
            yield return L("narrator", "f8.2");
            yield return L("narrator", "f8.3");
            yield return EndChronicle();
            yield return Fade(1f);
            yield return Background("xiapi_hall");
            yield return Actor("tao_qian", "NpcTaoQian", 0.5f, 0.52f, "front");
            yield return Actor("liu_bei", "LiuBei", 0.5f, 0.28f, "back");
            yield return Actor("guan_yu", "GuanYu", 0.4f, 0.24f, "back");
            yield return Actor("zhang_fei", "ZhangFei", 0.6f, 0.24f, "back");
            yield return Fade(0f);
            foreach (var s in Lines("tao_qian", "f9.1", "liu_bei", "f9.2", "narrator", "f9.3", "tao_qian", "f9.4", "liu_bei", "f9.5", "tao_qian", "f9.6"))
                yield return s;
            yield return Advance(ChapterStep.Xiapi, ChapterStep.Echo);
            yield return Fade(1f);
            yield return Call(() => { if (Run != null) Run.NextArrival = GameRun.Arrival.ReturnFromBattle; });
            yield return Load(GameRun.XiapiScene);
        }

        // F11 三让与受印 (the player presses 受印), scroll ⑩, F12 Zhao Yun leaves, F13 Lü Bu comes (195, choice ⑨), F14 the
        // chronicle (196), the chapter settlement, the title. The settlement is written from the play-through.
        static IEnumerable<StageStep> SanrangSteps()
        {
            int rep = Run?.Life.Reputation ?? 0;
            string tier = rep >= 10 ? "high" : rep >= 5 ? "mid" : "low";
            yield return Background("xiapi_hall");
            yield return Actor("tao_qian", "NpcTaoQian", 0.5f, 0.52f, "front");
            yield return Actor("chen_deng", "NpcChenDeng", 0.68f, 0.46f, "left");
            yield return Actor("mi_zhu", "NpcMiZhu", 0.32f, 0.46f, "right");
            yield return Actor("liu_bei", "LiuBei", 0.5f, 0.28f, "back");
            yield return Actor("guan_yu", "GuanYu", 0.38f, 0.24f, "back");
            yield return Actor("zhang_fei", "ZhangFei", 0.62f, 0.24f, "back");
            yield return Actor("zhao_yun", "NpcZhaoYun", 0.74f, 0.26f, "back");
            yield return Fade(0f);
            foreach (var s in Lines("narrator", "f11.1", "tao_qian", "f11.2." + tier, "tao_qian", "f11.3", "liu_bei", "f11.4", "chen_deng", "f11.5", "mi_zhu", "f11.6"))
                yield return s;
            yield return L("narrator", "f11.crowd." + tier);
            yield return Choice("tao_qian", "f11.prompt", SealChoice, new[] { "f11.seal" }, new[] { L("narrator", "f11.sealed"), Shake(0.3f) });
            yield return Remember("tao_qian", "taoqian_seal", "entrusted", "mem.taoqian_seal");
            foreach (var s in Lines("liu_bei", "f11.7", "guan_yu", "f11.8", "zhang_fei", "f11.9")) yield return s;
            yield return Scroll(ScrollSanrang);
            // F12 赵云辞别（史实·裴注；演义时点），按选择⑥
            yield return Background("xiapi_hall");
            yield return Actor("zhao_yun", "NpcZhaoYun", 0.6f, 0.32f, "left");
            yield return Actor("liu_bei", "LiuBei", 0.44f, 0.3f, "right");
            yield return Fade(0f);
            yield return L("zhao_yun", "f12.1");
            yield return L("liu_bei", "f12.2.a").When(ChapterScripts4.ZhaoChoice + "0");
            yield return L("liu_bei", "f12.2.b").When("!" + ChapterScripts4.ZhaoChoice + "0");
            yield return L("narrator", "f12.3");
            yield return L("zhao_yun", "f12.4");
            yield return Hide("zhao_yun");
            yield return Fade(1f);
            // F13 吕布来投（195，下邳城外；陈宫沉默）
            yield return Card("card.lubu");
            yield return Background("refugee_road");
            yield return Actor("liu_bei", "LiuBei", 0.36f, 0.3f, "right");
            yield return Actor("guan_yu", "GuanYu", 0.26f, 0.34f, "right");
            yield return Actor("zhang_fei", "ZhangFei", 0.26f, 0.24f, "right");
            yield return Actor("lu_bu", "NpcLuBu", 0.66f, 0.32f, "left");
            yield return Actor("chen_gong", "NpcChenGong", 0.76f, 0.38f, "left");
            yield return Fade(0f);
            foreach (var s in Lines("narrator", "f13.1", "lu_bu", "f13.2", "zhang_fei", "f13.3", "narrator", "f13.4")) yield return s;
            yield return Choice("liu_bei", "f13.prompt", LuBuChoice, new[] { "f13.a", "f13.b" },
                new[] { L("liu_bei", "f13.a.1"), L("lu_bu", "f13.a.2"), Remember("lu_bu", "lubu_xuzhou", "A_welcomed", "mem.lubu.A") },
                new[] { L("liu_bei", "f13.b.1"), L("lu_bu", "f13.b.2"), Remember("lu_bu", "lubu_xuzhou", "B_cautious", "mem.lubu.B") });
            yield return L("chen_gong", "f13.5");
            yield return L("narrator", "f13.6");
            yield return Fade(1f);
            // F14 奉天子（编年）
            yield return Background("map");
            yield return Fade(0f);
            yield return Chronicle("year.196", "y196");
            foreach (var s in Lines("narrator", "f14.1", "narrator", "f14.2")) yield return s;
            yield return ChronicleEvent("xu", "cao", "ev.xudu");
            yield return L("narrator", "f14.3");
            yield return EndChronicle();
            yield return Advance(ChapterStep.Echo, ChapterStep.Done05);
            // 篇章结算 (SYSTEMS_V05 §5, design art/v05-ui-concept/ui_chapter_settlement): one page drawn by ChronicleHud
            // from the play-through; the narrator's lines under it carry the reading.
            foreach (var x in SettlementSteps()) yield return x;
            yield return Fade(1f);
            yield return Autosave(Save.SaveData.ChapterDoneLocation);
            yield return Card("end5.title", 4f);
            yield return Card("end5.next", 4f);
            yield return Call(GameRun.Clear);
            yield return Load(GameRun.TitleScene);
        }

        // The settlement page alone (also the dev entry "-stage chapter_settlement" for screenshots).
        static IEnumerable<StageStep> SettlementSteps()
        {
            yield return Chronicle("year.196", "settlement");
            yield return L("narrator", "settle.1");
            foreach (var x in Dyn(() => Settlement.Trail())) yield return x;
            yield return L("narrator", "settle.2");
            yield return EndChronicle();
        }

        // A line or card whose text is written when it is reached (the settlement): a call that fills the next step.
        static StageStep[] Dyn(Func<string> text, bool card = false)
        {
            var step = card ? Card("raw:", 5f) : L("narrator", "raw:");
            return new[] { Call(() => { if (card) step.A = "raw:" + text(); else step.B = "raw:" + text(); }), step };
        }

        // ---------- battles ----------

        public static IEnumerable<(string who, string key, string value, string text)> VictoryMemories(string encounterId) => encounterId switch
        {
            "battle_12" => new[] { ("kong_rong", "kongrong_saved", "seen", "mem.kongrong_saved") },
            "battle_13" => new[] { ("cao_cao", "caocao_xiapi", "seen", "mem.caocao_xiapi") },
            "battle_14" => new[] { ("xuzhou_folk", "xiapi_defended", "seen", "mem.xiapi_defended") },
            _ => null,
        };

        public static (string scene, string stage) AfterVictory(string encounterId) => encounterId switch
        {
            "battle_12" => (GameRun.StageScene, AfterDuchang),
            "battle_13" => (GameRun.XiapiScene, null),
            "battle_14" => (GameRun.StageScene, AfterXiapi14),
            "battle_15" => (GameRun.XiapiScene, null),
            _ => (null, null),
        };
    }
}
