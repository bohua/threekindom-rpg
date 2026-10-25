using System.Collections.Generic;
using System.Linq;
using Prototype.BingshuTactics;
using ThreeKingdoms.Core.Battle;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Data.Battle;
using ThreeKingdoms.Gameplay.Battle;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.UI.Story;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThreeKingdoms.UI.Battle
{
    // 0.2 battles 4–6 on the tactics map (user 2026-10-25, ADR-0010). Sits in the battle scene before TacticsGame starts:
    // builds the stage from the play-through (the party's levels and gear through the combat formulas, the encounter's
    // enemies, allies and events), writes the pre-battle autosave, and on victory hands the result back exactly as the
    // round-based battle did — the reward-once gate, exp and copper, the battle's memories — then continues the story.
    // A defeat changes nothing, so retrying is reloading the scene.
    [DefaultExecutionOrder(-500)]
    public sealed class ChapterTactics : MonoBehaviour
    {
        public BattleCatalog Catalog;
        public string EncounterId = "battle_4";
        public DialoguePortraitSet Portraits;   // the same bust set the story dialogue uses

        public static bool AutoPlay;   // tests and screenshots: the party is played by the AI, lines advance by themselves

        public GameRun Run { get; private set; }
        public DevPartyProgress.Applied LastApplied { get; private set; }

        void Awake()
        {
            Run = GameRun.Current ?? GameRun.ForChapter(Catalog, StepFor(EncounterId), ExpFor(EncounterId), 260, development: true);
            var data = Run.Data;
            var encounter = data.Encounter(EncounterId);
            var def = Stages.Build(EncounterId, encounter, Run, data);
            if (Portraits)
                foreach (var e in Portraits.Entries)
                {
                    string name = StoryText.Speaker(e.Speaker);
                    if (e.Texture && !string.IsNullOrEmpty(name) && !def.Portraits.ContainsKey(name))
                        def.Portraits[name] = new TacticsPortrait { Texture = e.Texture, Crop = e.Crop, Axis = e.Axis, Flip = e.Flip };
                }
            def.AutoPlay = AutoPlay || System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-chapterShots") >= 0;
            def.Finished = OnFinished;
            TacticsGame.Chapter = def;
            if (!Run.IsDevelopment && Run.NextBattleId == EncounterId)
            {
                try { Run.SaveTo(ThreeKingdoms.Save.SaveSlots.Auto, EncounterId); }
                catch (System.Exception e) when (e is System.IO.IOException || e is System.UnauthorizedAccessException) { Debug.LogWarning("Autosave failed: " + e.Message); }
            }
        }

        // -chapterShots <dir>: the AI plays the battle and a frame is saved at deployment and every few seconds after.
        System.Collections.IEnumerator Start()
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-chapterShots");
            if (i < 0 || i + 1 >= args.Length) yield break;
            string dir = args[i + 1];
            System.IO.Directory.CreateDirectory(dir);
            foreach (var t in new[] { 0.7f, 0.5f })   // the opening lines
            {
                yield return new WaitForSecondsRealtime(t);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"{EncounterId}-line{t:0.0}.png"));
            }
            for (int k = 0; k < 8; k++)
            {
                yield return new WaitForSecondsRealtime(k == 0 ? 2.5f : 7f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"{EncounterId}-{k}.png"));
            }
        }

        void OnDestroy() { if (TacticsGame.Chapter != null && TacticsGame.Chapter.Id == EncounterId) TacticsGame.Chapter = null; }

        static ChapterStep StepFor(string id) => id == "battle_4" ? ChapterStep.Yingchuan : id == "battle_5" ? ChapterStep.Guangzong : ChapterStep.Anxi;
        static int ExpFor(string id) => id == "battle_4" ? 60 : id == "battle_5" ? 100 : 150;

        void OnFinished(TacticsOutcome outcome)
        {
            if (!outcome.Victory) return;
            var encounter = Run.Data.Encounter(EncounterId);
            bool firstClear = Run.RecordVictory(EncounterId);
            LastApplied = Run.Progress.Apply(BattleResult.Victory(encounter, outcome.Rounds), firstClear);
            if (firstClear)
                foreach (var (who, key, value, text) in ChapterScripts.VictoryMemories(EncounterId))
                    Run.Chapter2.Remember(who, key, value, text);
            TacticsGame.Chapter = null;
            var (scene, stage) = ChapterScripts.AfterVictory(EncounterId);
            if (Run.IsDevelopment || scene == null) { SceneManager.LoadScene(GameRun.TitleScene); return; }
            Run.PendingStage = stage;
            Run.NextArrival = GameRun.Arrival.ReturnFromBattle;
            SceneManager.LoadScene(scene);
        }

        // The three maps (Codex concept art/v02-tactics-concept). First row is the north edge. Legend: TacticsGame.Legend
        // and ChapterLegend — '.' grass, ',' dry grass, 'm' marsh, '~' creek, '=' ford, 'b' bridge, ':' road, 'c'/'C'
        // camp, 't'/'T' woods, 'r'/'R'/'B' rock, '^'/'A' one/two levels up, 'v'/'V' road one/two levels up.
        public static class Stages
        {
            public static readonly string[] Yingchuan =
            {
                "tt^^cccccccc^^tt",
                "t^^.cccccccc.^^t",
                "t^,,,..::..,,,^t",
                ".,,,,..::..,,,,.",
                "..,,..~::~..,,..",
                "...~~~.::.~~~...",
                "t..~~..::..~~..t",
                "tt.....::.....tt",
                "t...,..::..,...t",
                "....r..::..r....",
                "................",
                "tt............tt",
            };

            public static readonly string[] Guangzong =
            {
                "AA^^....:.....^^AA",
                "A^^.....:......^^A",
                "^^......:...tt...^",
                "^...tt..:...tt....",
                "....tt..::.......,",
                "..^^.....::....,,,",
                ".^^^^.....::...,,,",
                ".^^^^......::.....",
                "..^^........::....",
                "t............::..t",
                "tt............::tt",
                "ttt............:tt",
            };

            public static readonly string[] Anxi =
            {
                "TTAAACCCCCCAAATT",
                "TAAAACCCCCCAAAAT",
                "TAAAAAAVVAAAAAAT",
                "T^^^^AAVVAA^^^^T",
                "t^^^^^^vv^^^^^^t",
                "t^^R^^^vv^^^R^^t",
                "~~~=~~~bb~~~=~~~",
                "t......::......t",
                "tt.....::.....tt",
                "t..r...::...r..t",
                "................",
                "tt............tt",
            };

            static Vector2Int At(string[] rows, int x, int row) => new Vector2Int(x, rows.Length - 1 - row);

            public static TacticsStageDef Build(string id, EncounterSpec encounter, GameRun run, BattleData data)
            {
                var def = new TacticsStageDef { Id = id, Title = encounter.DisplayName };
                var party = data.Party.Where(g => run.Story.HasJoined(g.Id)).ToList();
                string[] rows; Vector2Int[] partyAt, enemyAt; int extra; string extraName;
                switch (id)
                {
                    case "battle_4":
                        rows = Yingchuan; def.Night = true; def.Where = "颍川 · 草泽夜营";
                        def.Deploy = new RectInt(3, 0, 10, 3);
                        partyAt = new[] { At(rows, 7, 11), At(rows, 6, 10), At(rows, 9, 10) };
                        enemyAt = new[] { At(rows, 6, 1), At(rows, 9, 1), At(rows, 5, 3), At(rows, 10, 3) };
                        extra = 2; extraName = "黄巾兵";
                        break;
                    case "battle_5":
                        rows = Guangzong; def.Where = "广宗 · 城外官道";
                        def.Deploy = new RectInt(0, 0, 7, 3);
                        partyAt = new[] { At(rows, 2, 11), At(rows, 4, 10), At(rows, 5, 11) };
                        enemyAt = new[] { At(rows, 16, 6), At(rows, 14, 5), At(rows, 14, 7), At(rows, 15, 4) };
                        extra = 2; extraName = "黄巾兵";
                        break;
                    default:
                        rows = Anxi; def.Where = "安喜 · 山麓山寨";
                        def.Deploy = new RectInt(3, 0, 10, 3);
                        partyAt = new[] { At(rows, 7, 11), At(rows, 6, 10), At(rows, 9, 10) };
                        enemyAt = new[] { At(rows, 7, 1), At(rows, 5, 3), At(rows, 10, 3), At(rows, 7, 4) };
                        extra = 2; extraName = "山贼喽啰";
                        break;
                }
                def.Rows = rows;

                for (int i = 0; i < party.Count && i < 3; i++)
                {
                    var g = party[i];
                    var s = run.Progress.Stats(g, run.Progress.Inventory.Bonus(g.Id));
                    def.Units.Add(Unit(g, run.Progress.Level(g), s, Team.Player, partyAt[i]));
                }
                for (int i = 0; i < encounter.Enemies.Count; i++)
                {
                    var e = encounter.Enemies[i];
                    var s = BattleRules.Compute(e.General, e.Level, default, data.Troops, data.Config);
                    var u = Unit(e.General, e.Level, s, Team.Enemy, enemyAt[i]);
                    u.Leader = i == 0;
                    if (e.FocusEscort) u.Focus = "董卓";
                    def.Units.Add(u);
                }
                // The map has room for more than the round battle's four: two common soldiers at the last slot's level - 1.
                var last = encounter.Enemies[encounter.Enemies.Count - 1];
                var spots = id == "battle_4" ? new[] { At(rows, 3, 4), At(rows, 12, 4) }
                          : id == "battle_5" ? new[] { At(rows, 16, 8), At(rows, 13, 3) }
                          : new[] { At(rows, 3, 5), At(rows, 12, 5) };
                for (int i = 0; i < extra; i++)
                {
                    var lv = Mathf.Max(1, last.Level - 1);
                    var u = Unit(last.General, lv, BattleRules.Compute(last.General, lv, default, data.Troops, data.Config), Team.Enemy, spots[i]);
                    u.Name = extraName + "甲乙"[i];
                    if (last.FocusEscort) u.Focus = "董卓";
                    def.Units.Add(u);
                }
                foreach (var a in encounter.Allies)
                {
                    var s = BattleRules.Compute(a.General, a.Level, default, data.Troops, data.Config);
                    var u = Unit(a.General, a.Level, s, Team.Player, At(rows, 2, 6));
                    u.Name = "董卓"; u.Ai = true; u.Escort = a.Escort; u.Move = 0;
                    def.Units.Add(u);
                }

                Events(id, def, rows);
                def.OpeningLines = Lines(BattleScenes.BeforeBattle(id));
                def.VictoryLines = Lines(BattleScenes.AfterVictory(id));   // C4 / C8 play here, as the battle's aftermath
                return def;
            }

            static List<(string, string)> Lines(IEnumerable<DialogueLine> lines) =>
                lines.Select(l => (StoryText.Speaker(l.Speaker), StoryText.Line(l.Line))).ToList();

            static TacticsAction Line(string speaker, string key) => new TacticsAction { Kind = TacticsActionKind.Line, Speaker = StoryText.Speaker(speaker), Text = StoryText.Line(key) };
            static TacticsAction Goal(string text) => new TacticsAction { Kind = TacticsActionKind.Objective, Text = text };
            static TacticsAction Banner(string text) => new TacticsAction { Kind = TacticsActionKind.Banner, Text = text };

            // The round battles' scripted events, re-timed for a map where both sides have to close in first.
            static void Events(string id, TacticsStageDef def, string[] rows)
            {
                switch (id)
                {
                    case "battle_4":
                        def.Objective = "扛住黄巾的攻势";
                        var dry = new List<Vector2Int>();
                        for (int r = 0; r < rows.Length; r++)
                            for (int x = 0; x < rows[r].Length; x++)
                                if (rows[r][x] == ',') dry.Add(At(rows, x, r));
                        def.Events.Add(new TacticsEvent { Round = 2, Actions = new[] { Goal("坚守！火攻将于下回合发动") } });
                        def.Events.Add(new TacticsEvent
                        {
                            Round = 3,
                            Actions = new[]
                            {
                                new TacticsAction { Kind = TacticsActionKind.Fire, Tiles = dry.ToArray() }, Banner("火起！"),
                                Line("guan_yu", "b4_fire.1"), Line("liu_bei", "b4_fire.2"),
                                new TacticsAction { Kind = TacticsActionKind.Chaos, Team = Team.Enemy, Turns = 4 },   // 剧情强制混乱 4 次行动，之后转普通混乱
                                new TacticsAction { Kind = TacticsActionKind.Damage, Team = Team.Enemy, Value = 0.10f },
                                Goal("援军将至：坚持到第 6 回合"),
                            },
                        });
                        def.Events.Add(new TacticsEvent { Round = 5, Actions = new[] { Goal("红旗骑兵将于下回合杀到") } });
                        def.Events.Add(new TacticsEvent
                        {
                            Round = 6,
                            Actions = new[]
                            {
                                Banner("援军杀到！"), Line("zhang_fei", "b4_cavalry.1"), Line("zhang_liang", "b4_cavalry.2"),
                                new TacticsAction { Kind = TacticsActionKind.RetreatAll, Team = Team.Enemy },
                            },
                        });
                        break;
                    case "battle_5":
                        def.Objective = "保护董卓本阵（坚持到第 6 回合）";
                        for (int r = 2; r <= 5; r++)
                            def.Events.Add(new TacticsEvent { Round = r, Actions = new[] { Goal($"保护董卓本阵（剩 {6 - r} 回合）") } });
                        def.Events.Add(new TacticsEvent
                        {
                            Round = 6,
                            Actions = new[] { Banner("张角兵败退走"), new TacticsAction { Kind = TacticsActionKind.RetreatAll, Team = Team.Enemy } },
                        });
                        def.Events.Add(new TacticsEvent
                        {
                            HpBelow = "张角", Ratio = 0.3f,
                            Actions = new[] { Line("zhang_jiao", "b5_zhangjiao_flee.1"), new TacticsAction { Kind = TacticsActionKind.RetreatAll, Team = Team.Enemy } },
                        });
                        break;
                    default:
                        def.Objective = "剿灭山贼";
                        break;
                }
            }

            static TacticsUnitDef Unit(GeneralSpec g, int level, UnitStats s, Team team, Vector2Int pos)
            {
                var troop = g.Troop switch
                {
                    TroopType.Infantry => Troop.Infantry, TroopType.Cavalry => Troop.Cavalry, TroopType.Archer => Troop.Archer,
                    TroopType.YellowTurban => Troop.Rebel, _ => Troop.Strategist,
                };
                var u = new TacticsUnitDef
                {
                    Name = g.DisplayName, Key = KeyOf(g.Id), Team = team, Troop = troop, Level = level,
                    Hp = s.MaxTroops, Atk = s.PhysicalAttack, Def = s.PhysicalDefense, Mat = s.StrategyAttack, Mdef = s.StrategyDefense,
                    Speed = s.Speed, Move = troop == Troop.Cavalry ? 5 : troop == Troop.Strategist ? 2 : 3,
                    Range = troop == Troop.Archer ? 3 : troop == Troop.Strategist ? 2 : 1,
                    Ep = s.MaxTech * 4, Int = s.Intellect, Wu = s.Force, Pos = pos,
                    Crafts = g.SkillsAt(level).Select(k => k.DisplayName).ToArray(),
                };
                u.School = g.Id switch { "liu_bei" => School.Ru, "zhang_fei" => School.Fa, "zhang_jiao" => School.Dao, _ => troop == Troop.Rebel ? School.Fa : School.Bing };
                if (troop == Troop.Rebel || g.Id == "zhang_jiao") u.Element = Element.Earth;
                return u;
            }

            static string KeyOf(string id) => id switch
            {
                "liu_bei" => "LiuBei", "guan_yu" => "GuanYu", "zhang_fei" => "ZhangFei", "zhang_jiao" => "ZhangJiao",
                "zhang_bao" => "ZhangBao", "zhang_liang" => "ZhangLiang", "yt_guard" => "YellowGuard", "dong_zhuo" => "DongZhuo",
                "bandit" => "Bandit", "bandit_chief" => "BanditChief", _ => "Rebel",
            };
        }
    }
}
