using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Story;
using static ThreeKingdoms.Gameplay.Story.S;

namespace ThreeKingdoms.Gameplay.Story
{
    // The 0.4 河北篇 cutscenes on the story stage (design/v0.4-hebei/V04_HEBEI_PLAN.md §2, §5; REVIEW_V04.md). Keys only;
    // ChapterText4 holds the words.
    public static class ChapterScripts4
    {
        public const string HebeiOpen = "hebei_open", ToPanhe = "to_panhe", AfterPanhe = "after_panhe", AfterJieqiao = "after_jieqiao", End04 = "end04";
        // Choice flags: ⑥ how Liu Bei answers Zhao Yun, ⑦ the assassin.
        public const string ZhaoChoice = "e7_", AssassinChoice = "e9_";
        // Field flags.
        public const string PingyuanGreeted = "e3", Porridge = "porridge", Canal = "canal", CanalLooked = "canal_looked", Assassin = "e9",
            RumourX11 = "rumor_x11", RumourX12 = "rumor_x12";
        public const string ScrollPanhe = "panhe", ScrollJieqiao = "jieqiao";

        public static IReadOnlyList<StageStep> Stage(string id) => id switch
        {
            HebeiOpen => HebeiOpenSteps().ToList(),
            ToPanhe => ToPanheSteps().ToList(),
            AfterPanhe => AfterPanheSteps().ToList(),
            AfterJieqiao => AfterJieqiaoSteps().ToList(),
            End04 => End04Steps().ToList(),
            _ => ChapterScripts5.Stage(id),
        };

        // E1 袁绍夺冀州 (the chronicle), E2 平原令 in Gongsun Zan's hall, the march to Pingyuan (191).
        static IEnumerable<StageStep> HebeiOpenSteps()
        {
            yield return Background("map");
            yield return Fade(0f);
            yield return Chronicle("year.191", "y191b");
            foreach (var s in Lines("narrator", "e1.1", "narrator", "e1.2")) yield return s;
            yield return ChronicleEvent("ye", "yuan_shao", "ev.yuan_ji");
            foreach (var s in Lines("narrator", "e1.3", "narrator", "e1.4")) yield return s;
            yield return EndChronicle();
            yield return Fade(1f);
            yield return Background("gongsun_hall");
            yield return Actor("gongsun_zan", "NpcGongsunZan", 0.5f, 0.5f, "front");
            yield return Actor("tian_kai", "NpcTianKai", 0.66f, 0.44f, "left");
            yield return Actor("liu_bei", "LiuBei", 0.44f, 0.28f, "back");
            yield return Actor("guan_yu", "GuanYu", 0.34f, 0.24f, "back");
            yield return Actor("zhang_fei", "ZhangFei", 0.54f, 0.22f, "back");
            yield return Fade(0f);
            foreach (var s in Lines("gongsun_zan", "e2.1", "gongsun_zan", "e2.2", "tian_kai", "e2.3", "liu_bei", "e2.4", "gongsun_zan", "e2.5"))
                yield return s;
            // REVIEW_V04 R17: Tian Yu goes with Liu Bei to Pingyuan even if 0.3 skipped him.
            yield return L("gongsun_zan", "e2.tianyu").When("!" + ChapterState.TianYu);
            yield return Flag(ChapterState.TianYu);
            yield return L("zhang_fei", "e2.6");
            yield return Fade(1f);
            yield return Background("map");
            yield return Fade(0f);
            yield return March("ji", "pingyuan", "year.191");
            yield return Card("card.pingyuan");
            yield return Advance(ChapterStep.Done03, ChapterStep.Pingyuan);
            yield return Load(GameRun.BohaiScene);
        }

        // From the Pingyuan ferry: E5 the chronicle (192), E6 at the Panhe, battle 10.
        static IEnumerable<StageStep> ToPanheSteps()
        {
            yield return Background("map");
            yield return Fade(0f);
            yield return Chronicle("year.192", "y192");
            yield return L("narrator", "e5.1");
            yield return L("narrator", "e5.2");
            yield return Flag(ChapterState.Year192);
            yield return L("narrator", "e5.3");
            yield return EndChronicle();
            yield return Advance(ChapterStep.PingyuanSafe, ChapterStep.Panhe);
            yield return March("pingyuan", "jieqiao", "year.192");
            yield return Card("card.panhe");
            yield return Fade(1f);
            yield return Fight("battle_10");
        }

        // After battle 10: scroll ⑦, E7 (choice ⑥), Zhao Yun rides with Liu Bei; battle 11 at Jieqiao.
        static IEnumerable<StageStep> AfterPanheSteps()
        {
            yield return Scroll(ScrollPanhe);
            yield return Background("panhe_bank");
            yield return Actor("gongsun_zan", "NpcGongsunZan", 0.36f, 0.34f, "right");
            yield return Actor("zhao_yun", "NpcZhaoYun", 0.56f, 0.34f, "left");
            yield return Actor("liu_bei", "LiuBei", 0.74f, 0.3f, "left");
            yield return Actor("guan_yu", "GuanYu", 0.84f, 0.34f, "left");
            yield return Actor("zhang_fei", "ZhangFei", 0.84f, 0.24f, "left");
            yield return Fade(0f);
            foreach (var s in Lines("gongsun_zan", "e7.1", "zhao_yun", "e7.2", "gongsun_zan", "e7.3", "zhao_yun", "e7.4", "narrator", "e7.5"))
                yield return s;
            yield return Choice("liu_bei", "e7.prompt", ZhaoChoice, new[] { "e7.a", "e7.b" },
                new[] { L("liu_bei", "e7.a.1"), L("zhao_yun", "e7.a.2"), Remember("zhao_yun", "zhaoyun_liubei", "A_invited", "mem.zhaoyun_liubei.A") },
                new[] { L("liu_bei", "e7.b.1"), L("zhao_yun", "e7.b.2"), Remember("zhao_yun", "zhaoyun_liubei", "B_loyal", "mem.zhaoyun_liubei.B") });
            foreach (var s in Lines("gongsun_zan", "e7.6", "guan_yu", "e7.7", "zhang_fei", "e7.8")) yield return s;
            yield return Flag(ChapterState.ZhaoYunGuest);
            yield return L("system", "e7.guest");
            yield return Fade(1f);
            yield return Card("card.jieqiao");
            yield return Fight("battle_11");
        }

        // After battle 11: the defeat (严纲阵斩), E8 Zhending, back to Pingyuan where E9 plays.
        static IEnumerable<StageStep> AfterJieqiaoSteps()
        {
            yield return Background("jieqiao_after");
            yield return Actor("liu_bei", "LiuBei", 0.5f, 0.3f, "back");
            yield return Actor("zhao_yun", "NpcZhaoYun", 0.62f, 0.32f, "left");
            yield return Actor("guan_yu", "GuanYu", 0.38f, 0.34f, "right");
            yield return Actor("zhang_fei", "ZhangFei", 0.4f, 0.24f, "right");
            yield return Fade(0f);
            foreach (var s in Lines("narrator", "e8.0", "zhang_fei", "e8.1", "zhao_yun", "e8.2", "liu_bei", "e8.3")) yield return s;
            yield return Fade(1f);
            yield return Background("zhending_home");
            yield return Actor("zhao_brother", "NpcZhaoBrother", 0.5f, 0.42f, "front");
            yield return Fade(0f);
            foreach (var s in Lines("narrator", "e8.4", "zhao_brother", "e8.5", "zhao_yun", "e8.6", "zhao_brother", "e8.7", "liu_bei", "e8.8", "zhao_brother", "e8.9"))
                yield return s;
            yield return Remember("zhao_yun", "zhaoyun_zhending", "entrusted", "mem.zhaoyun_zhending");
            yield return Fade(1f);
            yield return Background("map");
            yield return Fade(0f);
            yield return March("zhending", "pingyuan", "year.192");
            yield return Card("card.pingyuan_xiang");
            yield return Load(GameRun.BohaiScene);
        }

        // After E9 in Pingyuan: scroll ⑧ (no seal since 0.5), then straight on into 193 (V05_XUZHOU_PLAN §2): Taishi Ci
        // reaches Pingyuan (F1, in the field).
        static IEnumerable<StageStep> End04Steps()
        {
            yield return Background("map");
            yield return Fade(1f);
            yield return Scroll(ScrollJieqiao);
            yield return Advance(ChapterStep.Zhending, ChapterStep.Done04);
            yield return Advance(ChapterStep.Done04, ChapterStep.Beihai);
            yield return Flag(ChapterState.Year193);
            yield return Card("card.beihai_news", 3.5f);
            yield return Load(GameRun.BohaiScene);
        }

        // ---------- battles ----------

        public static IEnumerable<(string who, string key, string value, string text)> VictoryMemories(string encounterId) => encounterId switch
        {
            "battle_9" => new[] { ("pingyuan_folk", "pingyuan_defended", "seen", "mem.pingyuan_defended") },
            "battle_10" => new[] { ("wen_chou", "wenchou_panhe", "seen", "mem.wenchou_panhe") },
            "battle_11" => new[] { ("gongsun_zan", "gongsun_jieqiao", "rearguard", "mem.gongsun_jieqiao") },
            _ => ChapterScripts5.VictoryMemories(encounterId),
        };

        public static (string scene, string stage) AfterVictory(string encounterId) => encounterId switch
        {
            "battle_9" => (GameRun.BohaiScene, null),
            "battle_10" => (GameRun.StageScene, AfterPanhe),
            "battle_11" => (GameRun.StageScene, AfterJieqiao),
            _ => ChapterScripts5.AfterVictory(encounterId),
        };
    }
}
