using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Story;
using static ThreeKingdoms.Gameplay.Story.S;

namespace ThreeKingdoms.Gameplay.Story
{
    // The 0.3 讨董篇 cutscenes on the story stage (design/v0.3-taodong/V03_TAODONG_PLAN.md §2, §5; REVIEW_V03.md). Keys
    // only; ChapterText3 holds the words. Sources per scene are in the plan's table (史实／演义／原创).
    public static class ChapterScripts3
    {
        // Stage scripts.
        public const string Drift = "drift", ToSuanzao = "to_suanzao", Huimeng = "huimeng", ToHulao = "to_hulao", AfterHulao = "after_hulao";
        // Choice flags (choice flag + option index): ③ the duel questions, ④ Hulao (battle), ⑤ the ruins.
        public const string DuelChoice = "d7_", HulaoChoice = "b8_", RuinsChoice = "d10_";
        // Field flags.
        public const string GongsunGreeted = "d4", ZhongwangLearned = "zhongwang", SuanzaoGreeted = "d6", Wenjiu = "wenjiu",
            BellFound = "bell", EdictFound = "edict", RationsShared = "share", JianYong = "jianyong";
        // Ink scrolls: ④ ⑤ ⑥; ⑥ closes 0.3 and alone carries the 完成 seal.
        public const string ScrollWenjiu = "wenjiu", ScrollSanying = "sanying", ScrollFenLuoyang = "fen_luoyang";

        public static IReadOnlyList<StageStep> Stage(string id) => id switch
        {
            Drift => DriftSteps().ToList(),
            ToSuanzao => ToSuanzaoSteps().ToList(),
            Huimeng => HuimengSteps().ToList(),
            ToHulao => ToHulaoSteps().ToList(),
            AfterHulao => AfterHulaoSteps().ToList(),
            _ => ChapterScripts4.Stage(id),
        };

        // D1 漂泊 (186–189), D2 洛阳之变 (the realm-map chronicle), D3 献刀与捉放, then north to Gongsun Zan (190).
        static IEnumerable<StageStep> DriftSteps()
        {
            yield return Background("map");
            yield return Fade(0f);
            yield return Chronicle("year.186", "y186");
            yield return L("narrator", "d1.1");
            yield return March("anxi", "gaotang", "year.186");
            yield return L("narrator", "d1.2");
            yield return L("narrator", "d1.3");
            yield return Chronicle("year.189", "y189");
            foreach (var s in Lines("narrator", "d2.1", "narrator", "d2.2", "narrator", "d2.3")) yield return s;
            yield return ChronicleEvent("luoyang", "dong", "ev.dong_luoyang");
            yield return L("narrator", "d2.4");
            yield return EndChronicle();
            yield return Fade(1f);

            // D3 献刀与捉放（演义第 4 回；吕伯奢之事裴注三说不一，不演）
            yield return Card("card.luoyang189");
            yield return Background("luoyang_palace");
            yield return Actor("dong_zhuo", "NpcDongZhuo", 0.66f, 0.34f, "left");
            yield return Actor("cao_cao", "NpcCaoCao", 0.34f, 0.3f, "right");
            yield return Fade(0f);
            foreach (var s in Lines("narrator", "d3.1", "dong_zhuo", "d3.2", "cao_cao", "d3.3", "narrator", "d3.4", "cao_cao", "d3.5", "dong_zhuo", "d3.6"))
                yield return s;
            yield return Move("cao_cao", -0.1f, 0.3f, 1.2f);
            yield return L("narrator", "d3.7");
            yield return Fade(1f);
            yield return Hide("dong_zhuo");
            yield return Background("lv_farm");
            yield return Actor("cao_cao", "NpcCaoCao", 0.38f, 0.32f, "right");
            yield return Actor("chen_gong", "NpcChenGong", 0.26f, 0.36f, "right");
            yield return Actor("lv_boshe", "NpcLvBoshe", 0.66f, 0.32f, "left");
            yield return Fade(0f);
            foreach (var s in Lines("narrator", "d3.8", "lv_boshe", "d3.9", "cao_cao", "d3.10", "narrator", "d3.11"))
                yield return s;
            yield return Hide("lv_boshe");
            yield return Face("cao_cao", "left");
            foreach (var s in Lines("cao_cao", "d3.12", "chen_gong", "d3.13", "narrator", "d3.14")) yield return s;
            yield return Fade(1f);
            yield return Hide("cao_cao");
            yield return Hide("chen_gong");

            // 190: north to Gongsun Zan.
            yield return Background("map");
            yield return Fade(0f);
            yield return March("gaotang", "ji", "year.190");
            yield return Card("card.gongsun");
            yield return Advance(ChapterStep.Done, ChapterStep.Gongsun);
            yield return Load(GameRun.GongsunScene);
        }

        // D5 has been answered at Gongsun Zan's door: south to Suanzao.
        static IEnumerable<StageStep> ToSuanzaoSteps()
        {
            yield return Background("map");
            yield return Fade(0f);
            yield return March("ji", "suanzao", "year.190");
            yield return Card("card.suanzao");
            yield return Advance(ChapterStep.Gongsun, ChapterStep.Suanzao);
            yield return Load(GameRun.SuanzaoScene);
        }

        // D6 会盟 and D7 温酒斩华雄 in the coalition leader's tent (演义第 5 回), choice ③, scroll ④, back to the camp.
        static IEnumerable<StageStep> HuimengSteps()
        {
            yield return Background("mengzhu_tent");
            yield return Actor("yuan_shao", "NpcYuanShao", 0.5f, 0.56f, "front");
            yield return Actor("yuan_shu", "NpcYuanShu", 0.32f, 0.5f, "right");
            yield return Actor("cao_cao", "NpcCaoCao", 0.68f, 0.5f, "left");
            yield return Actor("sun_jian", "NpcSunJian", 0.22f, 0.44f, "right");
            yield return Actor("kong_rong", "NpcKongRong", 0.78f, 0.44f, "left");
            yield return Actor("gongsun_zan", "NpcGongsunZan", 0.6f, 0.3f, "back");
            yield return Actor("liu_bei", "LiuBei", 0.7f, 0.22f, "back");
            yield return Actor("guan_yu", "GuanYu", 0.8f, 0.2f, "back");
            yield return Actor("zhang_fei", "ZhangFei", 0.88f, 0.22f, "back");
            yield return Fade(0f);
            foreach (var s in Lines("narrator", "d6.1", "yuan_shao", "d6.2", "yuan_shao", "d6.3", "gongsun_zan", "d6.4", "yuan_shao", "d6.5",
                         "liu_bei", "d6.6", "yuan_shao", "d6.7", "cao_cao", "d6.8"))
                yield return s;
            yield return Flag(SuanzaoGreeted);

            // D7 温酒斩华雄
            yield return Shake(0.4f);
            foreach (var s in Lines("messenger", "d7.1", "yuan_shao", "d7.2", "messenger", "d7.3", "yuan_shao", "d7.4"))
                yield return s;
            yield return Move("guan_yu", 0.62f, 0.34f, 0.8f);
            foreach (var s in Lines("guan_yu", "d7.5", "yuan_shao", "d7.6", "gongsun_zan", "d7.7", "yuan_shu", "d7.8")) yield return s;
            yield return Move("zhang_fei", 0.82f, 0.3f, 0.4f);
            foreach (var s in Lines("zhang_fei", "d7.9", "liu_bei", "d7.10")) yield return s;
            yield return Remember("zhang_fei", "zhangfei_suanzao", "held_back", "mem.zhangfei_suanzao");
            foreach (var s in Lines("cao_cao", "d7.11", "yuan_shao", "d7.12", "cao_cao", "d7.13", "guan_yu", "d7.14")) yield return s;
            yield return Move("guan_yu", 1.15f, 0.34f, 1.0f);
            yield return Hide("guan_yu");
            // 一骑讨简化版 (SYSTEMS_V03 §5): three questions, words only.
            yield return L("narrator", "d7.15");
            yield return Choice("narrator", "d7.q1", DuelChoice + "1_", new[] { "d7.q1.a", "d7.q1.b" }, new[] { L("narrator", "d7.q1.a.1") }, new[] { L("narrator", "d7.q1.b.1") });
            yield return Choice("narrator", "d7.q2", DuelChoice + "2_", new[] { "d7.q2.a", "d7.q2.b" }, new[] { L("narrator", "d7.q2.a.1") }, new[] { L("narrator", "d7.q2.b.1") });
            yield return Choice("narrator", "d7.q3", DuelChoice + "3_", new[] { "d7.q3.a", "d7.q3.b" }, new[] { L("narrator", "d7.q3.a.1") }, new[] { L("narrator", "d7.q3.b.1") });
            yield return Scroll(ScrollWenjiu);
            yield return Actor("guan_yu", "GuanYu", 1.1f, 0.34f, "left");
            yield return Move("guan_yu", 0.62f, 0.34f, 1.0f);
            foreach (var s in Lines("narrator", "d7.16", "guan_yu", "d7.17", "cao_cao", "d7.18", "yuan_shu", "d7.19", "liu_bei", "d7.20")) yield return s;
            yield return Remember("cao_cao", "caocao_wenjiu", "seen", "mem.caocao_wenjiu");
            yield return Remember("yuan_shu", "yuanshu_gongshou", "hostile", "mem.yuanshu_gongshou");
            yield return Remember("guan_yu", "guanyu_moxi", "seen", "mem.guanyu_moxi");
            yield return Flag(Wenjiu);
            yield return Fade(1f);
            yield return Call(() => { if (GameRun.Current != null) GameRun.Current.NextArrival = GameRun.Arrival.ReturnFromBattle; });   // back at the plaza
            yield return Load(GameRun.SuanzaoScene);
        }

        // Out through the camp gate to Hulao Pass: battle 8 (its opening lines play D8).
        static IEnumerable<StageStep> ToHulaoSteps()
        {
            yield return Background("hulao_gate");
            yield return Fade(0f);
            yield return Card("card.hulao");
            yield return Advance(ChapterStep.Suanzao, ChapterStep.Hulao);
            yield return Fade(1f);
            yield return Fight("battle_8");
        }

        // After battle 8: scroll ⑤, D9 Luoyang burns (the chronicle; 191 begins), D10 the ruins (choice ⑤), D11 the coalition
        // breaks up, the 后事预告 cards (not written into the story state), scroll ⑥ with the 完成 seal, back to the title.
        static IEnumerable<StageStep> AfterHulaoSteps()
        {
            yield return Scroll(ScrollSanying);
            yield return Background("map");
            yield return Fade(0f);
            yield return Chronicle("year.190", "y190");
            yield return L("narrator", "d9.1");
            yield return ChronicleEvent("luoyang", "none", "ev.luoyang_burns");
            yield return L("narrator", "d9.2");
            yield return L("narrator", "d9.3");
            yield return Flag(ChapterState.Year191);
            yield return Chronicle("year.191", "y191");
            yield return ChronicleEvent("luoyang", "sun", "ev.sun_luoyang");
            yield return L("narrator", "d9.4");
            yield return EndChronicle();
            yield return Fade(1f);

            yield return Card("card.ruins");
            yield return Background("luoyang_ruins");
            yield return Actor("liu_bei", "LiuBei", 0.4f, 0.3f, "right");
            yield return Actor("guan_yu", "GuanYu", 0.3f, 0.36f, "right");
            yield return Actor("zhang_fei", "ZhangFei", 0.3f, 0.24f, "right");
            yield return Actor("old_soldier", "NpcElderSoldier", 0.68f, 0.34f, "left");
            yield return Fade(0f);
            foreach (var s in Lines("narrator", "d10.1", "zhang_fei", "d10.2", "old_soldier", "d10.3", "old_soldier", "d10.4", "guan_yu", "d10.5"))
                yield return s;
            yield return Remember("sun_jian", "sunjian_well", "rumour", "mem.sunjian_well");
            yield return Choice("liu_bei", "d10.prompt", RuinsChoice, new[] { "d10.a", "d10.b" },
                new[]
                {
                    L("liu_bei", "d10.a.1"), L("old_soldier", "d10.a.2"), Reputation(1),
                    Remember("liu_bei", "liubei_ruins", "A_bury", "mem.liubei_ruins.A"),
                },
                new[]
                {
                    L("liu_bei", "d10.b.1"), L("guan_yu", "d10.b.2"),
                    Remember("liu_bei", "liubei_ruins", "B_report", "mem.liubei_ruins.B"),
                });
            // D11 联军瓦解
            yield return Actor("gongsun_zan", "NpcGongsunZan", 0.9f, 0.3f, "left");
            yield return Move("gongsun_zan", 0.62f, 0.3f, 1.0f);
            foreach (var s in Lines("gongsun_zan", "d11.1", "gongsun_zan", "d11.2", "liu_bei", "d11.3")) yield return s;
            yield return L("guan_yu", "d11.4");
            foreach (var s in Lines("liu_bei", "d11.5", "zhang_fei", "d11.6", "liu_bei", "d11.7")) yield return s;
            yield return Fade(1f);
            // 0.4: scroll ⑥ without the seal, then section six (the 后事预告 moved to the end of 0.4).
            yield return Scroll(ScrollFenLuoyang);
            yield return Advance(ChapterStep.Luoyang, ChapterStep.Done03);
            yield return Load(GameRun.StageScene, ChapterScripts4.HebeiOpen);
        }

        // ---------- battles ----------

        // Memories when a battle's first victory is recorded. A value "?flag|value" is written only when the flag is set
        // (the in-battle choice ④ comes back as a flag before these are written).
        public static IEnumerable<(string who, string key, string value, string text)> VictoryMemories(string encounterId) => encounterId switch
        {
            "battle_7" => new[] { ("gongsun_zan", "gongsun_drill", "seen", "mem.gongsun_drill") },
            "battle_8" => new[]
            {
                ("lu_bu", "lubu_three", "seen", "mem.lubu_three"),
                ("dong_zhuo", "dongzhuo_hulao", "seen", "mem.dongzhuo_hulao"),
                ("cao_cao", "caocao_sanying", "seen", "mem.caocao_sanying"),
                ("zhang_fei", "zhangfei_trust", "?" + HulaoChoice + "1|trusted", "mem.zhangfei_trust"),
                ("zhang_fei", "zhangfei_shield", "?" + HulaoChoice + "0|shielded", "mem.zhangfei_shield"),
            },
            _ => ChapterScripts4.VictoryMemories(encounterId),
        };

        public static (string scene, string stage) AfterVictory(string encounterId) => encounterId switch
        {
            "battle_7" => (GameRun.GongsunScene, null),
            "battle_8" => (GameRun.StageScene, AfterHulao),
            _ => ChapterScripts4.AfterVictory(encounterId),
        };
    }
}
