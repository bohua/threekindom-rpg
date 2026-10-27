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
    // 0.5 徐州篇 battles on the tactics map (V05_XUZHOU_PLAN §4, SYSTEMS_V05 §2–§3, REVIEW_V05 R9–R12, R27–R28): battle 12
    // Duchang (reserves, Taishi Ci), 13 east of Xiapi (waves, Cao Bao, the way back in), 14 the walls of Xiapi (waves,
    // Danyang troops), 15 the optional Xiaopei patrol. Up to three on the field, the rest in reserve. First row = north.
    public static class Stages5
    {
        public static bool Handles(string id) => id == "battle_12" || id == "battle_13" || id == "battle_14" || id == "battle_15";

        public static readonly string[] Duchang =
        {
            "WWWWWWWGGWWWWWWW",
            "^^^...,::,...^^^",
            "^^..,,.::.,,..^^",
            "..,,...::...,,..",
            "t......::......t",
            "..x....::....x..",
            "t.,,...::...,,.t",
            "...,,..::..,,...",
            "t......::......t",
            "tt.....::.....tt",
            "t......::......t",
            "tt.....::.....tt",
        };

        public static readonly string[] XiapiEast =
        {
            "WWWWWWWWGGWWWWWWWW",
            "........::........",
            "..^^....::....^^..",
            "..^^....::....^^..",
            "t.......::.......t",
            "...~~~=.::.=~~~...",
            "........::........",
            "..,,....::....,,..",
            "t.,,....::....,,.t",
            "........::........",
            "tt......::......tt",
            "t.......::.......t",
        };

        public static readonly string[] XiapiWalls =
        {
            "................",
            "..,,...::...,,..",
            "................",
            "AAAAAAA::AAAAAAA",
            "AAAAAAA::AAAAAAA",
            "~~~~~~=::=~~~~~~",
            "......,::,......",
            "..,,...::...,,..",
            "t......::......t",
            "..x....::....x..",
            "tt.....::.....tt",
            "t......::......t",
        };

        static Vector2Int At(string[] rows, int x, int row) => new Vector2Int(x, rows.Length - 1 - row);

        // Guests and story units (their levels follow the brothers', REVIEW_V04 R7).
        // GENERALS_ROSTER v0.3: fixed attributes, roster troop types (太史慈 弓骑, 于禁 步兵, 曹军步卒 枪兵).
        static readonly GeneralSpec ZhaoYun = new GeneralSpec("zhao_yun", "赵云", TroopType.Cavalry, 93, 86, 74, element: UnitElement.Fire);
        static readonly GeneralSpec TaishiCi = new GeneralSpec("taishi_ci", "太史慈", TroopType.HorseArcher, 93, 82, 60, element: UnitElement.Fire);
        static readonly GeneralSpec CaoBao = new GeneralSpec("cao_bao", "曹豹", TroopType.Infantry, 70, 66, 40);
        static readonly GeneralSpec Danyang = new GeneralSpec("danyang", "丹杨兵", TroopType.Infantry, 72, 70, 30);
        static readonly GeneralSpec YuJin = new GeneralSpec("yu_jin", "于禁", TroopType.Infantry, 76, 86, 68, element: UnitElement.Earth);
        static readonly GeneralSpec CaoInf = new GeneralSpec("cao_infantry", "曹军步卒", TroopType.Spearman, 70, 64, 30, element: UnitElement.Fire);
        static readonly GeneralSpec CaoCav = new GeneralSpec("cao_cavalry", "曹军骑兵", TroopType.Cavalry, 72, 60, 28, element: UnitElement.Fire);

        // Battle 12's line-up comes from the formation choice before it (REVIEW_V05 R11): Liu Bei always goes.
        public const string Formation = "f12_";

        public static TacticsStageDef Build(string id, EncounterSpec encounter, GameRun run, BattleData data)
        {
            var def = new TacticsStageDef { Id = id, Title = encounter.DisplayName, Seed = 12 + GameRun.BattleIndex(id) };
            var ch = run.Chapter2;
            var party = data.Party.Where(g => run.Story.HasJoined(g.Id)).ToList();
            int partyLevel = party.Min(g => run.Progress.Level(g));
            TacticsUnitDef Hero(string gid, Vector2Int pos)
            {
                var g = party.First(x => x.Id == gid);
                var s = run.Progress.Stats(g, run.Progress.Inventory.Bonus(g.Id));
                var u = ChapterTactics.Stages.Unit(g, run.Progress.Level(g), s, Team.Player, pos);
                if (gid == "liu_bei" && !u.Crafts.Contains("众望")) u.Crafts = u.Crafts.Append("众望").ToArray();
                return u;
            }
            TacticsUnitDef Guest(GeneralSpec g, string key, Vector2Int pos, params string[] crafts)
            {
                var u = ChapterTactics.Stages.Unit(g, partyLevel, BattleRules.Compute(g, partyLevel, default, data.Troops, data.Config), Team.Player, pos);
                u.Key = key; u.Crafts = crafts;
                return u;
            }
            TacticsUnitDef Zhao(Vector2Int pos) => Guest(ZhaoYun, "ZhaoYun", pos, "援护", "连击");
            TacticsUnitDef Taishi(Vector2Int pos) => Guest(TaishiCi, "TaishiCi", pos, "火矢");
            TacticsUnitDef Ally(GeneralSpec g, string key, int level, Vector2Int pos)
            {
                var u = ChapterTactics.Stages.Unit(g, level, BattleRules.Compute(g, level, default, data.Troops, data.Config), Team.Player, pos);
                u.Ai = true; u.Key = key;
                return u;
            }
            TacticsUnitDef Foe(GeneralSpec g, int level, Vector2Int pos, string key)
            {
                var u = ChapterTactics.Stages.Unit(g, level, BattleRules.Compute(g, level, default, data.Troops, data.Config), Team.Enemy, pos);
                u.Key = key;
                return u;
            }
            string KeyOf(string gid) => gid switch
            {
                "guan_hai" => "GuanHai", "yt_remnant" => "YtRemnant", "cao_infantry" => "CaoInfantry", "cao_cavalry" => "CaoCavalry", "cao_archer" => "CaoInfantry",
                "cao_ren" => "CaoRen", "deserter" => "Bandit", "liukou" => "Bandit", _ => "Rebel",
            };
            // The line-up after battle 12: the brothers on the field, Zhao Yun in reserve.
            void Brothers(string[] rows, Vector2Int a, Vector2Int b, Vector2Int c, Vector2Int reserveAt)
            {
                def.Units.Add(Hero("liu_bei", a)); def.Units.Add(Hero("guan_yu", b)); def.Units.Add(Hero("zhang_fei", c));
                if (ch.Has(ChapterState.ZhaoYunGuest)) def.Reserves.Add(Zhao(reserveAt));
            }
            void Enemies(Vector2Int[] spots, string[] names = null)
            {
                for (int i = 0; i < encounter.Enemies.Count && i < spots.Length; i++)
                {
                    var e = encounter.Enemies[i];
                    var u = Foe(e.General, e.Level, spots[i], KeyOf(e.General.Id));
                    if (names != null && i < names.Length && names[i] != null) u.Name = names[i];
                    def.Units.Add(u);
                }
            }
            string[] rows;
            switch (id)
            {
                case "battle_12":
                {
                    rows = Duchang; def.Where = "北海 · 都昌城外"; def.Deploy = new RectInt(3, 0, 10, 3);
                    var slots = new[] { At(rows, 7, 10), At(rows, 5, 11), At(rows, 10, 11) };
                    var pick = ch.Has(Formation + "1") ? new[] { "zhao", "taishi" } : ch.Has(Formation + "2") ? new[] { "guan_yu", "taishi" } : new[] { "guan_yu", "zhang_fei" };
                    def.Units.Add(Hero("liu_bei", slots[0]));
                    var all = new[] { "guan_yu", "zhang_fei", "zhao", "taishi" };
                    TacticsUnitDef Make(string k, Vector2Int p) => k == "zhao" ? Zhao(p) : k == "taishi" ? Taishi(p) : Hero(k, p);
                    for (int i = 0; i < 2; i++) def.Units.Add(Make(pick[i], slots[i + 1]));
                    foreach (var k in all.Where(k => !pick.Contains(k))) def.Reserves.Add(Make(k, At(rows, 7, 11)));
                    Enemies(new[] { At(rows, 7, 4), At(rows, 4, 5), At(rows, 11, 5), At(rows, 2, 1), At(rows, 13, 1), At(rows, 7, 2) },
                        new[] { null, "黄巾余部甲", "黄巾余部乙", "黄巾弓手甲", "黄巾弓手乙", "黄巾余部丙" });
                    def.Units[0 + 3 + 0].Leader = true;
                    foreach (var u in def.Units.Where(x => x.Team == Team.Enemy && x.Name.Contains("弓手"))) { u.Move = 0; u.Range = 3; }
                    def.Objective = "击溃管亥，解都昌之围";
                    def.Events.Add(new TacticsEvent { Round = 2, Actions = new[] { Line("taishi_ci", "b12.hint.1"), Line("liu_bei", "b12.hint.2") } });
                    def.OpeningLines = Lines("guan_hai", "b12.open.1", "taishi_ci", "b12.open.2", "liu_bei", "b12.open.3");
                    def.VictoryLines = Lines("narrator", "b12.win.1", "taishi_ci", "b12.win.2");
                    break;
                }
                case "battle_13":
                {
                    rows = XiapiEast; def.Where = "下邳 · 城东"; def.Deploy = new RectInt(4, 7, 10, 3);
                    def.ExitZone = new RectInt(7, 10, 4, 1);
                    Brothers(rows, At(rows, 8, 3), At(rows, 10, 3), At(rows, 6, 4), At(rows, 9, 2));
                    var cb = Ally(CaoBao, "CaoBao", 12, At(rows, 12, 3)); cb.Leader = true;
                    def.Units.Add(cb);
                    Enemies(new[] { At(rows, 7, 9), At(rows, 10, 9), At(rows, 9, 11), At(rows, 6, 11) });
                    def.Objective = "迎击曹军前锋（三波）";
                    var yj = Foe(YuJin, 13, At(rows, 9, 11), "YuJin"); yj.Leader = true; yj.Floor = 0.45f;
                    def.Events.Add(new TacticsEvent { Round = 2, Actions = new[] { Goal("曹军第二波将至（于禁）") } });
                    def.Events.Add(new TacticsEvent
                    {
                        Round = 3,
                        Actions = new[]
                        {
                            Banner("第二波 · 于禁出阵"),
                            new TacticsAction { Kind = TacticsActionKind.Spawn, Units = new[] { yj, Foe(CaoInf, 11, At(rows, 5, 11), "CaoInfantry"), Foe(CaoInf, 11, At(rows, 12, 11), "CaoInfantry") } },   // BALANCE_MODEL_v1 §3.11: 杂兵 = our level − 1
                            Line("yu_jin", "b13.yujin.1"), Goal("把于禁的兵力压到一半以下，或坚持到第 6 回合"),
                        },
                    });
                    def.Events.Add(new TacticsEvent
                    {
                        Round = 5,
                        Actions = new[]
                        {
                            Banner("第三波 · 曹军骑兵"),
                            new TacticsAction { Kind = TacticsActionKind.Spawn, Units = new[] { Foe(CaoCav, 11, At(rows, 3, 11), "CaoCavalry"), Foe(CaoCav, 11, At(rows, 14, 11), "CaoCavalry") } },
                        },
                    });
                    // The way back in opens when Yu Jin falls below half, or at the latest at round 6 (the AI play-through
                    // showed the front could not be held longer); whichever comes first cancels the other.
                    TacticsAction[] Fallback(string other) => new[]
                    {
                        new TacticsAction { Kind = TacticsActionKind.Cancel, Unit = other },
                        Line("cao_bao", "b13.caobao.1"), new TacticsAction { Kind = TacticsActionKind.Retreat, Unit = "曹豹" },
                        Line("liu_bei", "b13.exit.1"), new TacticsAction { Kind = TacticsActionKind.OpenExit }, Goal("全军撤入下邳城门（青蓝格）"),
                    };
                    def.Events.Add(new TacticsEvent { Id = "fallback_hp", HpBelow = "于禁", Ratio = 0.5f, Actions = Fallback("fallback_round") });
                    def.Events.Add(new TacticsEvent { Id = "fallback_round", Round = 6, Actions = Fallback("fallback_hp") });
                    def.OpeningLines = Lines("narrator", "b13.open.1", "cao_bao", "b13.open.2", "liu_bei", "b13.open.3");
                    def.VictoryLines = Lines("guan_yu", "b13.win.1", "liu_bei", "b13.win.2");
                    break;
                }
                case "battle_14":
                {
                    rows = XiapiWalls; def.Where = "下邳 · 城头"; def.Deploy = new RectInt(2, 7, 12, 3);
                    Brothers(rows, At(rows, 5, 3), At(rows, 8, 2), At(rows, 7, 4), At(rows, 8, 1));
                    def.Units.Add(Ally(Danyang, "Danyang", 13, At(rows, 3, 3)));
                    def.Units.Add(Ally(Danyang, "Danyang", 13, At(rows, 12, 3)));
                    def.Units[def.Units.Count - 2].Name = "丹杨兵甲"; def.Units[def.Units.Count - 1].Name = "丹杨兵乙";
                    Enemies(new[] { At(rows, 8, 10), At(rows, 6, 11), At(rows, 10, 11), At(rows, 5, 10), At(rows, 11, 10) });
                    var cr = def.Units.First(u => u.Name == "曹仁"); cr.Leader = true; cr.Floor = 0.45f;
                    def.Objective = "守住下邳城门，迫退曹仁（或坚持到第 7 回合）";
                    def.Events.Add(new TacticsEvent
                    {
                        Round = 3,
                        Actions = new[] { Banner("第二波 · 曹军步卒"), new TacticsAction { Kind = TacticsActionKind.Spawn, Units = new[] { Foe(CaoInf, 12, At(rows, 4, 11), "CaoInfantry"), Foe(CaoInf, 12, At(rows, 12, 11), "CaoInfantry") } } },
                    });
                    def.Events.Add(new TacticsEvent { Round = 4, Actions = new[] { Banner("曹军阵中骚动"), Line("narrator", "b14.stir.1") } });   // REVIEW_V05 R3
                    def.Events.Add(new TacticsEvent
                    {
                        Round = 5,
                        Actions = new[] { Banner("第三波 · 曹军骑兵"), new TacticsAction { Kind = TacticsActionKind.Spawn, Units = new[] { Foe(CaoCav, 12, At(rows, 2, 11), "CaoCavalry"), Foe(CaoCav, 12, At(rows, 13, 11), "CaoCavalry") } } },
                    });
                    // Cao Ren sounds the retreat when he falls below half, or when word of Yanzhou reaches him (round 7, the
                    // betrayal the round-4 stir foreshadows; REVIEW_V05 R3).
                    TacticsAction[] Recall(string other) => new[]
                    {
                        new TacticsAction { Kind = TacticsActionKind.Cancel, Unit = other },
                        Line("cao_ren", "b14.caoren.1"), Banner("曹军鸣金"), new TacticsAction { Kind = TacticsActionKind.RetreatAll, Team = Team.Enemy },
                    };
                    def.Events.Add(new TacticsEvent { Id = "recall_hp", HpBelow = "曹仁", Ratio = 0.5f, Actions = Recall("recall_round") });
                    def.Events.Add(new TacticsEvent { Id = "recall_round", Round = 7, Actions = Recall("recall_hp") });
                    def.OpeningLines = Lines("narrator", "b14.open.1", "guan_yu", "b14.open.2", "liu_bei", "b14.open.3");
                    def.VictoryLines = Lines("zhang_fei", "b14.win.1", "liu_bei", "b14.win.2");
                    break;
                }
                default:
                {
                    rows = Stages4.Pingyuan; def.Where = "沛国 · 小沛郊外"; def.Deploy = new RectInt(2, 8, 12, 3);
                    Brothers(rows, At(rows, 7, 1), At(rows, 5, 3), At(rows, 10, 3), At(rows, 8, 2));
                    Enemies(new[] { At(rows, 7, 10), At(rows, 3, 9), At(rows, 12, 9), At(rows, 5, 11), At(rows, 10, 11), At(rows, 8, 11) },
                        new[] { "散兵头目", "散兵甲", "流寇甲", "流寇乙", "散兵乙", "散兵丙" });
                    def.Units.First(u => u.Name == "散兵头目").Leader = true;
                    def.Objective = "肃清小沛郊外的散兵流寇";
                    def.OpeningLines = Lines("liu_bei", "b15.open.1", "zhang_fei", "b15.open.2");
                    def.VictoryLines = Lines("liu_bei", "b15.win.1");
                    break;
                }
            }
            def.Rows = rows;
            return def;
        }

        static List<(string, string)> Lines(params string[] pairs)
        {
            var l = new List<(string, string)>();
            for (int i = 0; i + 1 < pairs.Length; i += 2) l.Add((StoryText.Speaker(pairs[i]), StoryText.Line(pairs[i + 1])));
            return l;
        }
        static TacticsAction Line(string speaker, string key) => new TacticsAction { Kind = TacticsActionKind.Line, Speaker = StoryText.Speaker(speaker), Text = StoryText.Line(key) };
        static TacticsAction Goal(string text) => new TacticsAction { Kind = TacticsActionKind.Objective, Text = text };
        static TacticsAction Banner(string text) => new TacticsAction { Kind = TacticsActionKind.Banner, Text = text };
    }
}
