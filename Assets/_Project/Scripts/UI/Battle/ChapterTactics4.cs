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
    // 0.4 河北篇 battles on the tactics map (V04_HEBEI_PLAN §4, SYSTEMS_V04, REVIEW_V04 R7–R9, R21–R23): battle 9 the
    // raiders before Pingyuan, battle 10 the Panhe (Zhao Yun arrives, Gongsun Zan to protect), battle 11 the rearguard at
    // Jieqiao (crossbows, the threshold and the way out). First row of a map is the north edge.
    public static class Stages4
    {
        public static bool Handles(string id) => id == "battle_9" || id == "battle_10" || id == "battle_11";

        public static readonly string[] Pingyuan =
        {
            "WWWWWWWGGWWWWWWW",
            "..^^^^.::.^^^^..",
            "t......::......t",
            "..,,...::...,,..",
            "..,,.x.::.x.,,..",
            "t......::......t",
            "...~~~=::=~~~...",
            "..,,...::...,,..",
            "t.,,...::...,,.t",
            "tt.....::.....tt",
            "t......::......t",
            "tt.....::.....tt",
        };

        public static readonly string[] Panhe =
        {
            "..,,....::....,,..",
            "..,,....::.....,..",
            "....,,..::..,,....",
            "..tt....::....tt..",
            "........==........",
            "~~~~=~~~==~~~=~~~~",
            "~~~~=~~~==~~~=~~~~",
            "..tt....::....tt..",
            ".ttt....::....ttt.",
            "...,,...::...,,...",
            "........::........",
            "t.......::.......t",
        };

        public static readonly string[] Jieqiao =
        {
            "AAAA^^^::^^^AAAA",
            "A^^^^^.::.^^^^^A",
            "^^,,...::...,,^^",
            "..,,...::...,,..",
            "tt.....::.....tt",
            "t......::......t",
            "~~~~~~~bb~~~~~~~",
            "~~~~~~~bb~~~~~~~",
            "tt.....::.....tt",
            "t..,...::...,..t",
            "...,,..::..,,...",
            "t......::......t",
            "................",
            "tt............tt",
        };

        static Vector2Int At(string[] rows, int x, int row) => new Vector2Int(x, rows.Length - 1 - row);

        // Zhao Yun rides as a guest general (REVIEW_V04 R7): his level is the lowest of the brothers', fixed gear.
        static readonly GeneralSpec ZhaoYun = new GeneralSpec("zhao_yun", "赵云", TroopType.Cavalry, 88, 85, 70, 2, 2, 2);
        static readonly GeneralSpec GongsunZan = new GeneralSpec("gongsun_zan", "公孙瓒", TroopType.Cavalry, 80, 82, 62);

        public static TacticsStageDef Build(string id, EncounterSpec encounter, GameRun run, BattleData data)
        {
            var def = new TacticsStageDef { Id = id, Title = encounter.DisplayName, Seed = 9 + GameRun.BattleIndex(id) };
            var party = data.Party.Where(g => run.Story.HasJoined(g.Id)).ToList();
            int partyLevel = party.Min(g => run.Progress.Level(g));
            TacticsUnitDef Hero(GeneralSpec g, Vector2Int pos)
            {
                var s = run.Progress.Stats(g, run.Progress.Inventory.Bonus(g.Id));
                var u = ChapterTactics.Stages.Unit(g, run.Progress.Level(g), s, Team.Player, pos);
                if (g.Id == "liu_bei" && !u.Crafts.Contains("众望")) u.Crafts = u.Crafts.Append("众望").ToArray();
                return u;
            }
            TacticsUnitDef Zhao(Vector2Int pos)
            {
                var s = BattleRules.Compute(ZhaoYun, partyLevel, default, data.Troops, data.Config);
                var u = ChapterTactics.Stages.Unit(ZhaoYun, partyLevel, s, Team.Player, pos);
                u.Key = "ZhaoYun"; u.Crafts = new[] { "援护", "连击" };
                return u;
            }
            TacticsUnitDef Foe(EnemySlot e, Vector2Int pos)
            {
                var s = BattleRules.Compute(e.General, e.Level, default, data.Troops, data.Config);
                var u = ChapterTactics.Stages.Unit(e.General, e.Level, s, Team.Enemy, pos);
                u.Key = e.General.Id switch
                {
                    "wen_chou" => "WenChou", "yuan_cavalry" => "Xiliang", "qu_yi" => "QuYi", "xiandeng" => "Xiandeng", "qiangnu" => "Crossbow",
                    "liukou_chief" => "BanditChief", "liukou" => "Bandit", _ => u.Key,
                };
                return u;
            }
            GeneralSpec G(string gid) => party.First(g => g.Id == gid);
            string[] rows;
            switch (id)
            {
                case "battle_9":
                {
                    rows = Pingyuan; def.Where = "平原 · 城外"; def.Deploy = new RectInt(2, 8, 12, 3);
                    def.Units.Add(Hero(G("liu_bei"), At(rows, 7, 1)));
                    def.Units.Add(Hero(G("guan_yu"), At(rows, 5, 3)));
                    def.Units.Add(Hero(G("zhang_fei"), At(rows, 10, 3)));
                    var spots = new[] { At(rows, 7, 10), At(rows, 3, 9), At(rows, 12, 9), At(rows, 5, 11), At(rows, 10, 11) };
                    for (int i = 0; i < encounter.Enemies.Count; i++)
                    {
                        var u = Foe(encounter.Enemies[i], spots[i]);
                        if (i > 0) u.Name = "流寇" + "甲乙丙丁"[i - 1]; else u.Leader = true;
                        def.Units.Add(u);
                    }
                    def.Objective = "击退犯境的流寇";
                    def.OpeningLines = Lines("raider_chief", "b9.open.1", "liu_bei", "b9.open.2", "zhang_fei", "b9.open.3");
                    def.VictoryLines = Lines("liu_bei", "b9.win.1", "jian_yong", "b9.win.2");
                    break;
                }
                case "battle_10":
                {
                    rows = Panhe; def.Where = "磐河 · 河滩"; def.Deploy = new RectInt(4, 0, 10, 3);
                    def.Units.Add(Hero(G("liu_bei"), At(rows, 8, 10)));
                    def.Units.Add(Hero(G("guan_yu"), At(rows, 7, 11)));
                    def.Units.Add(Hero(G("zhang_fei"), At(rows, 10, 11)));
                    var gz = ChapterTactics.Stages.Unit(GongsunZan, 11, BattleRules.Compute(GongsunZan, 11, default, data.Troops, data.Config), Team.Player, At(rows, 2, 8));
                    gz.Ai = true; gz.Escort = true; gz.Move = 0; gz.Key = "GongsunZan"; gz.Hp = gz.Hp * 3 / 2;
                    def.Units.Add(gz);
                    var spots = new[] { At(rows, 8, 1), At(rows, 5, 2), At(rows, 12, 2), At(rows, 9, 0) };
                    for (int i = 0; i < encounter.Enemies.Count; i++)
                    {
                        var u = Foe(encounter.Enemies[i], spots[i]);
                        if (i == 0) { u.Leader = true; u.Floor = 0.45f; }
                        else { u.Name = "袁军骑" + "甲乙丙"[i - 1]; u.Focus = "公孙瓒"; }
                        def.Units.Add(u);
                    }
                    def.Objective = "保护公孙瓒，击退文丑（公孙瓒倒下即败）";
                    def.Events.Add(new TacticsEvent
                    {
                        Round = 2,
                        Actions = new[]
                        {
                            Banner("白马银枪，杀入阵来！"), new TacticsAction { Kind = TacticsActionKind.Spawn, Units = new[] { Zhao(At(rows, 17, 7)) } },
                            Line("zhao_yun", "b10.zhao.1"), Line("liu_bei", "b10.zhao.2"),
                        },
                    });
                    def.Events.Add(new TacticsEvent
                    {
                        HpBelow = "文丑", Ratio = 0.5f,
                        Actions = new[] { Line("wen_chou", "b10.flee.1"), Banner("文丑拨马退去"), new TacticsAction { Kind = TacticsActionKind.RetreatAll, Team = Team.Enemy } },
                    });
                    def.OpeningLines = Lines("narrator", "b10.open.1", "wen_chou", "b10.open.2", "gongsun_zan", "b10.open.3", "liu_bei", "b10.open.4");
                    def.VictoryLines = Lines("gongsun_zan", "b10.win.1", "zhao_yun", "b10.win.2");
                    break;
                }
                default:
                {
                    rows = Jieqiao; def.Where = "界桥 · 南岸"; def.Deploy = new RectInt(3, 3, 10, 3);
                    def.ExitZone = new RectInt(2, 0, 12, 2);
                    def.Units.Add(Hero(G("liu_bei"), At(rows, 8, 10)));
                    def.Units.Add(Hero(G("guan_yu"), At(rows, 6, 9)));
                    def.Units.Add(Hero(G("zhang_fei"), At(rows, 7, 8)));
                    def.Units.Add(Zhao(At(rows, 9, 9)));
                    var spots = new[] { At(rows, 7, 4), At(rows, 6, 5), At(rows, 9, 5), At(rows, 5, 1), At(rows, 10, 1) };
                    for (int i = 0; i < encounter.Enemies.Count; i++)
                    {
                        var u = Foe(encounter.Enemies[i], spots[i]);
                        if (i == 0) { u.Leader = true; u.Floor = 0.45f; }
                        else if (i < 3) u.Name = "先登死士" + "甲乙"[i - 1];
                        else { u.Name = "强弩" + "甲乙"[i - 3]; u.Move = 0; u.Range = 4; }   // REVIEW_V04 R9: the crossbows hold the high bank
                        def.Units.Add(u);
                    }
                    def.Objective = "殿后：把麴义的兵力压到一半以下";
                    def.Events.Add(new TacticsEvent { Round = 1, Actions = new[] { Line("zhang_fei", "b11.hint.1"), Line("liu_bei", "b11.hint.2") } });
                    def.Events.Add(new TacticsEvent
                    {
                        HpBelow = "麴义", Ratio = 0.5f,
                        Actions = new[]
                        {
                            Line("qu_yi", "b11.qu.1"), Banner("麴义攻势受挫——撤！"), Line("liu_bei", "b11.exit.1"),
                            new TacticsAction { Kind = TacticsActionKind.OpenExit }, Goal("全军撤到南岸撤离区（青蓝格）"),
                        },
                    });
                    def.OpeningLines = Lines("narrator", "b11.open.1", "narrator", "b11.open.2", "gongsun_zan", "b11.open.3", "liu_bei", "b11.open.4");
                    def.VictoryLines = Lines("zhao_yun", "b11.win.1", "liu_bei", "b11.win.2");
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
