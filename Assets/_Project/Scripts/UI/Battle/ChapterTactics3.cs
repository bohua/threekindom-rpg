using System.Collections.Generic;
using System.Linq;
using Prototype.BingshuTactics;
using ThreeKingdoms.Core.Battle;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Data.Battle;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.UI.Story;
using UnityEngine;

namespace ThreeKingdoms.UI.Battle
{
    // 0.3 讨董篇 battles on the tactics map (V03_TAODONG_PLAN §4, SYSTEMS_V03 §3, REVIEW_V03 R7–R11, R17–R18, R30–R31):
    // battle 7 the optional drill with the White Horse riders, battle 8 Hulao Pass. First row of a map is the north edge.
    public static class Stages3
    {
        public static bool Handles(string id) => id == "battle_7" || id == "battle_8";

        // Battle 7: the drill ground of Gongsun Zan's garrison. A raised reviewing stand in the south-west for Liu Bei's bows,
        // two lines of barriers where Zhang Fei can stand against riders, the riders in from the north.
        public static readonly string[] Drill =
        {
            "tt............tt",
            "t..,,......,,..t",
            "...,,,,..,,,,...",
            "..x...,,,,...x..",
            "....,,,,,,,,....",
            "...,,,,,,,,,,...",
            "...,,,,,,,,,,...",
            ".AA.,,,,,,,,....",
            ".AA.x..,,..x....",
            ".^^............t",
            "t......::.....tt",
            "tt.....::....ttt",
        };

        // Battle 8: Hulao Pass. The gate in the north wall, a two-tile road between barriers before it (Lü Bu holds it), the
        // pass slope down to the creek and its bridge, the coalition's ground in the south.
        public static readonly string[] Hulao =
        {
            "WWWWWWWGWWWWWWWW",
            "cccxxxx::xxxxccc",
            "ccc.xxx::xxx.ccc",
            "^^.,..,::,..,.^^",
            "A^.,,..::..,,.^A",
            "A^..,,.::.,,..^A",
            "^^...,.::.,...^^",
            "~~~=~~~bb~~~=~~~",
            "...,...::...,...",
            "..r....::....r..",
            "t......::......t",
            "tt.....::.....tt",
            "t......::......t",
            "tt.....::.....tt",
        };

        static Vector2Int At(string[] rows, int x, int row) => new Vector2Int(x, rows.Length - 1 - row);

        public static TacticsStageDef Build(string id, EncounterSpec encounter, GameRun run, BattleData data)
        {
            var def = new TacticsStageDef { Id = id, Title = encounter.DisplayName, Seed = id == "battle_8" ? 8 : 7 };
            var party = data.Party.Where(g => run.Story.HasJoined(g.Id)).ToList();
            bool zhongwang = run.Chapter2.Step >= ChapterStep.Suanzao || run.Chapter2.Has(ChapterScripts3.ZhongwangLearned);
            TacticsUnitDef Hero(GeneralSpec g, Vector2Int pos)
            {
                var s = run.Progress.Stats(g, run.Progress.Inventory.Bonus(g.Id));
                var u = ChapterTactics.Stages.Unit(g, run.Progress.Level(g), s, Team.Player, pos);
                if (g.Id == "liu_bei" && zhongwang && !u.Crafts.Contains("众望")) u.Crafts = u.Crafts.Append("众望").ToArray();   // REVIEW_V03 R16
                if (id == "battle_8") u.Crafts = u.Crafts.Append("三英").ToArray();
                return u;
            }
            TacticsUnitDef Foe(EnemySlot e, Vector2Int pos)
            {
                var s = BattleRules.Compute(e.General, e.Level, default, data.Troops, data.Config);
                return ChapterTactics.Stages.Unit(e.General, e.Level, s, Team.Enemy, pos);
            }
            GeneralSpec G(string gid) => party.First(g => g.Id == gid);

            if (id == "battle_7")
            {
                var rows = Drill;
                def.Rows = rows; def.Where = "公孙瓒营 · 校场"; def.Deploy = new RectInt(1, 0, 14, 3);
                def.Units.Add(Hero(G("liu_bei"), At(rows, 2, 8)));
                def.Units.Add(Hero(G("guan_yu"), At(rows, 7, 10)));
                def.Units.Add(Hero(G("zhang_fei"), At(rows, 5, 9)));
                var spots = new[] { At(rows, 7, 1), At(rows, 4, 2), At(rows, 11, 2) };
                for (int i = 0; i < encounter.Enemies.Count; i++)
                {
                    var u = Foe(encounter.Enemies[i], spots[i]);
                    u.Name = "白马义从" + "甲乙丙"[i]; u.Leader = i == 0; u.Key = "BaimaRider";
                    def.Units.Add(u);
                }
                def.Objective = "演武：让三名白马骑将认输（兵力归零即下场）";
                def.OpeningLines = new List<(string, string)>
                {
                    (S("gongsun_zan"), T("b7.open.1")), (S("baima_rider"), T("b7.open.2")), (S("liu_bei"), T("b7.open.3")), (S("zhang_fei"), T("b7.open.4")),
                };
                def.VictoryLines = new List<(string, string)> { (S("baima_rider"), T("b7.win.1")), (S("gongsun_zan"), T("b7.win.2")) };
                return def;
            }

            // ---------- battle 8: Hulao Pass ----------
            var map = Hulao;
            def.Rows = map; def.Where = "虎牢关 · 关前"; def.Deploy = new RectInt(5, 4, 6, 2);
            var zhang = Hero(G("zhang_fei"), At(map, 7, 9));
            def.Units.Add(zhang);
            for (int i = 0; i < encounter.Enemies.Count; i++)
            {
                var e = encounter.Enemies[i];
                var pos = i == 0 ? At(map, 7, 1) : i == 1 ? At(map, 5, 3) : At(map, 10, 3);
                var u = Foe(e, pos);
                if (i == 0)
                {
                    u.Leader = true; u.Floor = 0.5f; u.Hp = Mathf.RoundToInt(u.Hp * 1.6f); u.Key = "LuBu"; u.Move = 0;   // REVIEW_V03 R7: Lü Bu holds the gate
                }
                else { u.Name = "并州骑" + "甲乙"[i - 1]; u.Focus = zhang.Name; }
                def.Units.Add(u);
            }
            var guan = Hero(G("guan_yu"), At(map, 7, 13));
            var liu = Hero(G("liu_bei"), At(map, 8, 13));
            def.Objective = "张飞单骑接战：坚持到援军赶到（张飞撤退即败）";
            def.Events.Add(new TacticsEvent
            {
                Id = "choice", Round = 2,
                Actions = new[]
                {
                    Line("liu_bei", "b8.choice.0"),
                    new TacticsAction
                    {
                        Kind = TacticsActionKind.Choice, Speaker = S("liu_bei"), Text = T("b8.choice.prompt"), Unit = ChapterScripts3.HulaoChoice,
                        Options = new[] { T("b8.choice.a"), T("b8.choice.b") },
                        Branches = new[]
                        {
                            new[]
                            {
                                Banner("刘备提前出阵 · 众望"),
                                new TacticsAction { Kind = TacticsActionKind.Spawn, Units = new[] { liu }, Near = zhang.Name },
                                new TacticsAction { Kind = TacticsActionKind.Guard, Speaker = liu.Name, Unit = zhang.Name },
                                Line("liu_bei", "b8.choice.a.1"), Line("zhang_fei", "b8.choice.a.2"),
                                new TacticsAction { Kind = TacticsActionKind.Cancel, Unit = "liu_enters" },
                            },
                            new[] { Line("liu_bei", "b8.choice.b.1"), Line("zhang_fei", "b8.choice.b.2") },
                        },
                    },
                },
            });
            def.Events.Add(new TacticsEvent
            {
                Id = "guan_enters", Round = 4,
                Actions = new[]
                {
                    Banner("关羽赶到！"), new TacticsAction { Kind = TacticsActionKind.Spawn, Units = new[] { guan } },
                    Line("guan_yu", "b8.guan.1"), Goal("合力迫退吕布（关张近身、刘备两格内可用合体技“三英”）"),
                },
            });
            def.Events.Add(new TacticsEvent
            {
                Id = "liu_enters", Round = 5,
                Actions = new[] { Banner("刘备赶到！"), new TacticsAction { Kind = TacticsActionKind.Spawn, Units = new[] { liu } }, Line("liu_bei", "b8.liu.1") },
            });
            def.Events.Add(new TacticsEvent
            {
                HpBelow = "吕布", Ratio = 0.6f,
                Actions = new[]
                {
                    Line("lu_bu", "b8.lubu.flee.1"), Banner("吕布拨马退入关中"),
                    new TacticsAction { Kind = TacticsActionKind.RetreatAll, Team = Team.Enemy },
                },
            });
            def.OpeningLines = new List<(string, string)>
            {
                (S("narrator"), T("b8.open.1")), (S("gongsun_zan"), T("b8.open.2")), (S("lu_bu"), T("b8.open.3")),
                (S("narrator"), T("b8.open.4")), (S("zhang_fei"), T("b8.open.5")), (S("lu_bu"), T("b8.open.6")),
            };
            def.VictoryLines = new List<(string, string)>
            {
                (S("zhang_fei"), T("b8.win.1")), (S("guan_yu"), T("b8.win.2")), (S("liu_bei"), T("b8.win.3")), (S("cao_cao"), T("b8.win.4")),
            };
            return def;
        }

        static string S(string speaker) => StoryText.Speaker(speaker);
        static string T(string key) => StoryText.Line(key);
        static TacticsAction Line(string speaker, string key) => new TacticsAction { Kind = TacticsActionKind.Line, Speaker = S(speaker), Text = T(key) };
        static TacticsAction Goal(string text) => new TacticsAction { Kind = TacticsActionKind.Objective, Text = text };
        static TacticsAction Banner(string text) => new TacticsAction { Kind = TacticsActionKind.Banner, Text = text };
    }
}
