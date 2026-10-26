using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Story;
using static ThreeKingdoms.Gameplay.Story.S;

namespace ThreeKingdoms.Gameplay.Story
{
    // The 0.2 cutscenes as data (V02_HUANGJIN_PLAN §2, §5). Keys only; ChapterText holds the words. Stage ids name what the
    // story stage scene plays next (GameRun.PendingStage); field scripts are played by the walkable scenes.
    public static class ChapterScripts
    {
        // Stage scripts for Story_Stage.
        public const string ToCamp = "to_camp", ToYingchuan = "to_yingchuan", ToGuangzong = "to_guangzong", AfterGuangzong = "after_guangzong";
        // Choice flags: the choice's flag plus the option index ("c6_0", "c12_1").
        public const string CartChoice = "c6_", DuyouChoice = "c12_", DetourChoice = "c5_";
        // Field flags.
        public const string CampGreeted = "c2", AnxiGreeted = "c10", QuiverFound = "quiver", SealFound = "seal", WoodCarried = "wood",
            OxLooked = "ox_looked", CharmGiven = "charm", BanditsAccepted = "bandits";
        // Scrolls (InkScrollText): ① huangjin exists; ② and ③ are 0.2's.
        public const string ScrollZhuanzhan = "zhuanzhan", ScrollAnxi = "anxi";

        public static IReadOnlyList<StageStep> Stage(string id) => id switch
        {
            ToCamp => ToCampSteps().ToList(),
            ToYingchuan => ToYingchuanSteps().ToList(),
            ToGuangzong => ToGuangzongSteps().ToList(),
            AfterGuangzong => AfterGuangzongSteps().ToList(),
            _ => new List<StageStep>(),
        };

        // Zhuojun south gate → march → Lu Zhi's camp (the confirm box was answered in town).
        static IEnumerable<StageStep> ToCampSteps()
        {
            yield return Advance(ChapterStep.ZhuojunFree, ChapterStep.Camp);
            yield return March("zhuojun", "guangzong", "year.184");
            yield return Card("card.guangzong");
            yield return Load(GameRun.CampScene);
        }

        // Camp left → march south → C3 in the officers' tent → battle 4.
        static IEnumerable<StageStep> ToYingchuanSteps()
        {
            yield return March("guangzong", "yingchuan", "year.184");
            yield return Card("card.yingchuan");
            yield return Background("tent");
            yield return Actor("officer", "HanOfficer", 0.62f, 0.32f, "left");
            yield return Actor("liu_bei", "LiuBei", 0.34f, 0.3f, "right");
            yield return Actor("guan_yu", "GuanYu", 0.24f, 0.36f, "right");
            yield return Actor("zhang_fei", "ZhangFei", 0.24f, 0.24f, "right");
            yield return Fade(0f);
            foreach (var s in Lines("han_officer", "c3.1", "han_officer", "c3.2", "han_officer", "c3.3", "guan_yu", "c3.4", "zhang_fei", "c3.5", "han_officer", "c3.6"))
                yield return s;
            yield return Rations(2);
            yield return L("system", "c3.7");
            yield return Fade(1f);
            yield return Fight("battle_4");
        }

        // After battle 4 (and C4 in the battle): back north; C6 the prison cart (choice ①), C7, battle 5.
        static IEnumerable<StageStep> ToGuangzongSteps()
        {
            yield return March("yingchuan", "guangzong", "year.184");
            yield return Card("card.road");
            yield return Background("road");
            yield return Actor("liu_bei", "LiuBei", 0.3f, 0.3f, "right");
            yield return Actor("guan_yu", "GuanYu", 0.22f, 0.36f, "right");
            yield return Actor("zhang_fei", "ZhangFei", 0.22f, 0.24f, "right");
            yield return Actor("cart", "PrisonCart", 1.1f, 0.3f, "left");
            yield return Actor("escort", "EscortOfficer", 1.22f, 0.36f, "left");
            yield return Fade(0f);
            yield return L("narrator", "c6.1");
            yield return Move("cart", 0.62f, 0.3f, 2.2f);
            yield return Move("escort", 0.74f, 0.36f, 0.6f);
            foreach (var s in Lines("liu_bei", "c6.2", "lu_zhi", "c6.3", "liu_bei", "c6.4", "lu_zhi", "c6.5", "zhang_fei", "c6.6"))
                yield return s;
            yield return Move("zhang_fei", 0.5f, 0.24f, 0.5f);
            foreach (var s in Lines("liu_bei", "c6.7", "zhang_fei", "c6.8", "liu_bei", "c6.9", "lu_zhi", "c6.10"))
                yield return s;
            yield return Remember("zhang_fei", "zhangfei_cart", "held_back", "mem.zhangfei_cart");
            yield return Choice("liu_bei", "c6.prompt", CartChoice, new[] { "c6.a", "c6.b" },
                new[]
                {
                    L("liu_bei", "c6.a.1"), L("lu_zhi", "c6.a.2"),
                    Remember("lu_zhi", "luzhi_cart", "A_trust", "mem.luzhi_cart.A"),
                },
                new[]
                {
                    L("liu_bei", "c6.b.1"), L("escort", "c6.b.2"),
                    Move("cart", 0.86f, 0.3f, 1.6f), L("narrator", "c6.b.3"), L("lu_zhi", "c6.b.4"), L("liu_bei", "c6.b.5"),
                    Remember("lu_zhi", "luzhi_cart", "B_escort", "mem.luzhi_cart.B"),
                });
            yield return Move("cart", 1.2f, 0.3f, 2.4f);
            yield return Hide("cart");
            yield return Hide("escort");
            yield return L("narrator", "c6.11");
            yield return Shake(0.5f);
            foreach (var s in Lines("narrator", "c7.1", "zhang_fei", "c7.2", "liu_bei", "c7.3", "guan_yu", "c7.4", "liu_bei", "c7.5"))
                yield return s;
            yield return Fade(1f);
            yield return Fight("battle_5");
        }

        // After battle 5 (C8 in the battle): the optional Wancheng detour (C5, adaptation), C9 on the realm map, scroll ②,
        // the year turns to 185, march to Anxi.
        static IEnumerable<StageStep> AfterGuangzongSteps()
        {
            yield return Background("road");
            yield return Fade(0f);
            yield return Choice("liu_bei", "c5.prompt", DetourChoice, new[] { "c5.yes", "c5.no" },
                new[]
                {
                    March("guangzong", "wancheng", "year.184"), Card("card.wancheng"), Background("wancheng"),
                    L("narrator", "c5.1"), L("zhang_fei", "c5.2"), L("guan_yu", "c5.3"), L("narrator", "c5.4"),
                    L("sun_jian", "c5.5"), L("liu_bei", "c5.6"), L("sun_jian", "c5.7"),
                    Remember("sun_jian", "sunjian_wancheng", "seen", "mem.sunjian_wancheng"),
                },
                new StageStep[0]);
            yield return Background("map");
            yield return L("narrator", "c9.1");
            yield return L("narrator", "c9.2");
            yield return L("narrator", "c9.3");
            yield return Scroll(ScrollZhuanzhan);
            yield return Advance(ChapterStep.ToAnxi, ChapterStep.Anxi);
            yield return March("guangzong", "anxi", "year.185");
            yield return Card("card.anxi");
            yield return Load(GameRun.AnxiScene);
        }

        // ---------- battles ----------

        // Short lines a battle event plays (SYSTEMS_V02 §1.2 dialogue(id)).
        public static IReadOnlyList<DialogueLine> BattleDialogue(string id) => id switch
        {
            "b4_fire" => new[] { new DialogueLine("guan_yu", "b4_fire.1"), new DialogueLine("liu_bei", "b4_fire.2") },
            "b4_cavalry" => new[] { new DialogueLine("zhang_fei", "b4_cavalry.1"), new DialogueLine("zhang_liang", "b4_cavalry.2") },
            "b5_zhangjiao_flee" => new[] { new DialogueLine("zhang_jiao", "b5_zhangjiao_flee.1") },
            _ => new DialogueLine[0],
        };

        // Memories written when a battle's first victory is recorded (C4, C8 play as the battle's aftermath).
        public static IEnumerable<(string who, string key, string value, string text)> VictoryMemories(string encounterId) => encounterId switch
        {
            "battle_4" => new[]
            {
                ("cao_cao", "caocao_yingchuan", "seen", "mem.caocao_yingchuan"),
                ("guan_yu", "guanyu_caocao", "seen", "mem.guanyu_caocao"),
            },
            "battle_5" => new[]
            {
                ("dong_zhuo", "dongzhuo_baishen", "seen", "mem.dongzhuo_baishen"),
                ("zhang_fei", "zhangfei_dongzhuo", "held_back", "mem.zhangfei_dongzhuo"),
            },
            _ => new (string, string, string, string)[0],
        };

        // Where "continue" leads after a won battle of the chapter: the story stage, or Anxi after the bandits.
        public static (string scene, string stage) AfterVictory(string encounterId) => encounterId switch
        {
            "battle_4" => (GameRun.StageScene, ToGuangzong),
            "battle_5" => (GameRun.StageScene, AfterGuangzong),
            "battle_6" => (GameRun.AnxiScene, null),
            _ => (null, null),
        };
    }
}
