using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ThreeKingdoms.Core.Battle;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Save;

namespace ThreeKingdoms.Tests.EditMode
{
    // 0.2 黄巾篇 rules (SYSTEMS_V02 §6 acceptance): battle events, allies and escort targets, chaos, the level cap, the
    // chapter steps, memories and the format-2 save with the Alpha 0.1 migration.
    public sealed class ChapterRulesTests
    {
        static BattleConfig Config7 => new BattleConfig(140, 50, 20, 100, new[] { 0, 10, 30, 60, 100, 150, 210 });

        static EncounterSpec Yingchuan(EnemyAiKind ai = EnemyAiKind.FacingSlot) => new EncounterSpec("battle_4", "颍川夜战",
            new[] { new EnemySlot(GddData.ZhangLiang, 5), new EnemySlot(GddData.Headman, 3) }, ai, 3, 40, 60, null,
            new BattleScript(new[]
            {
                new ScriptEvent("fire_preview", ScriptTrigger.TurnStart(2), new[] { ScriptAction.Objective("坚守！火攻将于下回合发动") }),
                new ScriptEvent("fire", ScriptTrigger.TurnStart(3), new[]
                {
                    ScriptAction.Fx("fire_wall"), ScriptAction.Banner("火起！"), ScriptAction.ApplyStatus(BattleScript.EnemyAll, "chaos", 2),
                    ScriptAction.DamageRatio(BattleScript.EnemyAll, 10), ScriptAction.Objective("援军将至：坚持到第 5 回合"),
                }),
                new ScriptEvent("cavalry", ScriptTrigger.TurnStart(5), new[] { ScriptAction.Retreat(BattleScript.EnemyAll) }),
            }));

        static IEnumerable<CombatantInput> Brothers(int level = 4) =>
            new[] { GddData.At(GddData.LiuBei, level), GddData.At(GddData.GuanYu, level), GddData.At(GddData.ZhangFei, level) };

        static List<BattleCommand> AllDefend(BattleSession s) => s.State.Players.Where(u => u.Active).Select(BattleCommand.Defend).ToList();

        [Test]
        public void FireComesOnRoundThreeAndTheCavalryEndsRoundFive()
        {
            var s = new BattleSession(Yingchuan(), Brothers().ToList(), GddData.Ration, 2, Config7, GddData.Troops, new PassiveAi());
            s.StartRound();
            s.PlayRound(AllDefend(s));
            var two = s.StartRound();
            Assert.AreEqual("坚守！火攻将于下回合发动", s.State.Objective);
            s.PlayRound(AllDefend(s));
            int before = s.State.Enemies[0].Troops;
            var three = s.StartRound();
            var zl = s.State.Enemies[0];
            Assert.IsTrue(zl.Statuses.ContainsKey("chaos"));
            Assert.AreEqual(70, zl.ModifierPercent(BattleStat.PhysicalAttack));
            Assert.AreEqual(80, zl.ModifierPercent(BattleStat.PhysicalDefense));
            Assert.AreEqual(before - BattleRules.PercentOf(zl.Stats.MaxTroops, 10), zl.Troops);
            Assert.IsTrue(three.Any(e => e.Kind == BattleEventKind.Banner && e.Text == "火起！"));
            s.PlayRound(AllDefend(s));
            s.PlayRound(AllDefend(s));
            s.StartRound();
            Assert.AreEqual(BattleOutcome.Victory, s.Result?.Outcome, "援军杀到：敌方全体溃走即胜利");
            Assert.IsTrue(s.State.Enemies.All(e => e.Routed && e.RetreatReason == RetreatReason.Routed));
        }

        [Test]
        public void ARetryStartsTheEventsOver()
        {
            var first = new BattleSession(Yingchuan(), Brothers().ToList(), GddData.Ration, 2, Config7, GddData.Troops, new PassiveAi());
            for (int i = 0; i < 3; i++) { first.StartRound(); first.PlayRound(AllDefend(first)); }
            Assert.IsTrue(first.State.FiredEvents.Contains("fire"));
            var retry = new BattleSession(Yingchuan(), Brothers().ToList(), GddData.Ration, 2, Config7, GddData.Troops, new PassiveAi());
            Assert.AreEqual(0, retry.State.FiredEvents.Count);
        }

        [Test]
        public void EventDamageNeverRetreatsAUnit()
        {
            var enc = new EncounterSpec("t", "t", new[] { new EnemySlot(GddData.Headman, 1) }, EnemyAiKind.FacingSlot, 3, 0, 0, null,
                new BattleScript(new[] { new ScriptEvent("burn", ScriptTrigger.TurnStart(1), new[] { ScriptAction.DamageRatio(BattleScript.EnemyAll, 100) }) }));
            var s = new BattleSession(enc, Brothers().ToList(), GddData.Ration, 2, Config7, GddData.Troops, new PassiveAi());
            s.StartRound();
            Assert.AreEqual(1, s.State.Enemies[0].Troops);
            Assert.IsTrue(s.State.Enemies[0].Active);
        }

        // Battle 5: Dong Zhuo's army as an escort ally; the guards focus it; it can be healed; losing it loses the battle.
        static EncounterSpec Guangzong(bool earlyWin = true, bool hold = true)
        {
            var dong = new GeneralSpec("dong_zhuo", "董卓本阵", TroopType.Infantry, 70, 40, 50);
            var events = new List<ScriptEvent>();
            if (hold) events.Add(new ScriptEvent("hold_end", ScriptTrigger.TurnStart(5), new[] { ScriptAction.Retreat(BattleScript.EnemyAll) }));
            if (earlyWin) events.Add(new ScriptEvent("early", ScriptTrigger.HpBelow("zhang_jiao", 30), new[] { ScriptAction.Retreat(BattleScript.EnemyAll) }));
            return new EncounterSpec("battle_5", "广宗救董卓",
                new[] { new EnemySlot(GddData.ZhangJiao, 6), new EnemySlot(GddData.Headman, 4, focusEscort: true) },
                EnemyAiKind.FacingSlot, 3, 50, 70, new[] { new AllySlot(dong, 7, escort: true) }, new BattleScript(events));
        }

        [Test]
        public void TheAllyActsOnItsOwnAndCanBeHealed()
        {
            var s = new BattleSession(Guangzong(), Brothers(5).ToList(), GddData.Ration, 2, Config7, GddData.Troops, new PassiveAi());
            var dong = s.State.AllyUnits.Single();
            Assert.AreEqual(Side.Ally, dong.Side);
            Assert.AreEqual(3, dong.Slot, "友军排在我方三人之后");
            Assert.IsTrue(s.State.Escorts.Contains(dong));
            var liu = s.State.Players[0];
            Assert.IsFalse(s.State.LegalTargets(liu, CommandKind.Skill, GddData.Regroup).Contains(dong), "满兵力时整队选不到");
            // Our three defend; Dong Zhuo attacks by himself (half troops or more).
            var log = s.PlayRound(AllDefend(s));
            Assert.IsTrue(log.Any(e => e.Kind == BattleEventKind.ActionStarted && e.Actor == dong && e.Command == CommandKind.Attack));
        }

        [Test]
        public void FocusEscortGuardsGoForTheEscort()
        {
            var s = new BattleSession(Guangzong(), Brothers(5).ToList(), GddData.Ration, 2, Config7, GddData.Troops);
            var dong = s.State.AllyUnits.Single();
            var log = s.PlayRound(AllDefend(s));
            var guardHits = log.Where(e => e.Kind == BattleEventKind.Damage && e.Actor == s.State.Enemies[1]).ToList();
            Assert.IsTrue(guardHits.Count > 0 && guardHits.All(e => e.Target == dong));
        }

        [Test]
        public void LosingTheEscortLosesTheBattleAtOnce()
        {
            var s = new BattleSession(Guangzong(hold: false), Brothers(5).ToList(), GddData.Ration, 2, Config7, GddData.Troops,
                new ScriptedAi(v => v.Enemies.Where(e => e.Active).Select(e => BattleCommand.Attack(e, v.Allies[0])).ToList()));
            for (int round = 0; round < 30 && s.Result == null; round++) s.PlayRound(AllDefend(s));
            Assert.AreEqual(BattleOutcome.Defeat, s.Result.Outcome);
            Assert.IsFalse(s.State.AllyUnits[0].Active);
            Assert.IsTrue(s.State.Players.All(p => p.Active), "我方全员仍在，失败来自护卫目标");
        }

        [Test]
        public void ZhangJiaoBelowThirtyPercentEndsItEarly()
        {
            var s = new BattleSession(Guangzong(hold: false), Brothers(7).ToList(), GddData.Ration, 2, Config7, GddData.Troops, new PassiveAi());
            var jiao = s.State.Enemies[0];
            for (int round = 0; round < 10 && s.Result == null; round++)
            {
                var cmds = s.State.Players.Where(u => u.Active).Select(u => BattleCommand.Attack(u, jiao)).ToList();
                s.PlayRound(cmds);
            }
            Assert.AreEqual(BattleOutcome.Victory, s.Result.Outcome);
            Assert.IsTrue(s.State.Enemies.Any(e => e.Routed), "张角退走（溃走），不是被击破");
        }

        [Test]
        public void ChaosAndIntimidateKeepTheStronger()
        {
            var s = new BattleSession(Yingchuan(), Brothers().ToList(), GddData.Ration, 2, Config7, GddData.Troops, new PassiveAi());
            for (int i = 0; i < 2; i++) { s.StartRound(); s.PlayRound(AllDefend(s)); }
            s.StartRound();   // fire: chaos −30% attack
            var zhang = s.State.Players[2];
            var cmds = AllDefend(s);
            cmds[2] = BattleCommand.UseSkill(zhang, GddData.Intimidate);   // −20% attack: weaker, chaos stays
            s.PlayRound(cmds);
            Assert.AreEqual(70, s.State.Enemies[0].ModifierPercent(BattleStat.PhysicalAttack));
        }

        [Test]
        public void TheScriptRejectsUnknownUnitsAndActionsAfterEndBattle()
        {
            Assert.Throws<System.ArgumentException>(() => new EncounterSpec("x", "x", new[] { new EnemySlot(GddData.Headman, 1) }, EnemyAiKind.FacingSlot, 3, 0, 0, null,
                new BattleScript(new[] { new ScriptEvent("e", ScriptTrigger.HpBelow("nobody", 30), new ScriptAction[0]) })));
            Assert.Throws<System.ArgumentException>(() => new EncounterSpec("x", "x", new[] { new EnemySlot(GddData.Headman, 1) }, EnemyAiKind.FacingSlot, 3, 0, 0, null,
                new BattleScript(new[] { new ScriptEvent("e", ScriptTrigger.TurnStart(1), new[] { ScriptAction.EndBattle(BattleOutcome.Victory), ScriptAction.Banner("x") }) })));
        }

        [Test]
        public void LevelSevenAtTwoHundredTen()
        {
            Assert.AreEqual(5, BattleRules.LevelForExp(100, Config7));
            Assert.AreEqual(6, BattleRules.LevelForExp(150, Config7));
            Assert.AreEqual(7, BattleRules.LevelForExp(210, Config7));
            Assert.AreEqual(7, BattleRules.LevelForExp(999, Config7));
        }

        // ---------- chapter and memories ----------

        [Test]
        public void TheChapterOpensAfterBattleThreeAndMovesOneWay()
        {
            var c = new ChapterState();
            c.OpenAfterZhuojun(false);
            Assert.AreEqual(ChapterStep.Zhuojun, c.Step);
            c.OpenAfterZhuojun(true);
            Assert.AreEqual(ChapterStep.ZouJingOrders, c.Step);
            Assert.IsFalse(c.Advance(ChapterStep.Camp, ChapterStep.Yingchuan), "不能跳步");
            Assert.IsTrue(c.Advance(ChapterStep.ZouJingOrders, ChapterStep.ZhuojunFree));
            Assert.IsTrue(c.Advance(ChapterStep.ZhuojunFree, ChapterStep.Camp));
            Assert.IsTrue(c.Advance(ChapterStep.Camp, ChapterStep.Yingchuan));
            Assert.AreEqual("battle_4", c.NextBattleId);
            Assert.IsFalse(c.RecordVictory("battle_5"));
            Assert.IsTrue(c.RecordVictory("battle_4"));
            Assert.IsFalse(c.RecordVictory("battle_4"), "同一场只发一次奖");
            Assert.AreEqual(ChronicleNode.N1, c.Node);
        }

        [Test]
        public void TheSameMemoryIsOverwrittenNotDuplicated()
        {
            var c = new ChapterState();
            var heard = new List<string>();
            c.Remembered += m => heard.Add(m.Who);
            c.Remember("lu_zhi", "luzhi_cart", "A_trust", "mem.luzhi_cart.A");
            c.Remember("lu_zhi", "luzhi_cart", "B_escort", "mem.luzhi_cart.B");
            Assert.AreEqual(1, c.Memories.Count);
            Assert.AreEqual("B_escort", c.Recall("lu_zhi", "luzhi_cart"));
            Assert.AreEqual(2, heard.Count, "每次写入都提示");
        }

        [Test]
        public void ChapterStateSurvivesASave()
        {
            var story = StoryState.Restore(true, true, SequenceState.Done, 3, SequenceState.Done);
            var inv = new Core.Economy.Inventory(new Core.Economy.EconomyData(new Core.Economy.EconomyConfig(120, 2, 10, 50), new Core.Economy.EquipmentSpec[0], null));
            var c = new ChapterState();
            c.OpenAfterZhuojun(true);
            c.Advance(ChapterStep.ZouJingOrders, ChapterStep.ZhuojunFree);
            c.Advance(ChapterStep.ZhuojunFree, ChapterStep.Camp);
            c.Set("c2");
            c.Remember("lu_zhi", "luzhi_cart", "A_trust", "mem.luzhi_cart.A");
            var data = SaveCodec.Capture(story, inv, new Dictionary<string, int>(), SaveData.CampLocation, 4, "x", "0.2", System.DateTime.Now, null, c);
            Assert.AreEqual(2, data.formatVersion);
            Assert.IsTrue(SaveCodec.TryRestore(data, inv.Data, out var r, out var error), error);
            Assert.AreEqual(ChapterStep.Camp, r.Chapter.Step);
            Assert.IsTrue(r.Chapter.Has("c2"));
            Assert.AreEqual("A_trust", r.Chapter.Recall("lu_zhi", "luzhi_cart"));
        }

        [Test]
        public void AnAlphaSaveContinuesWithZouJingAndKeepsItsLevels()
        {
            var data = new SaveData
            {
                formatVersion = 1, location = SaveData.TownLocation, guanYuJoined = true, zhangFeiJoined = true,
                oath = (int)SequenceState.Done, battlesDone = 3, ending = (int)SequenceState.NotStarted, copper = 260, rations = 2,
                exp = new[] { new ExpEntry { general = "liu_bei", exp = 60 } },
            };
            var economy = new Core.Economy.EconomyData(new Core.Economy.EconomyConfig(120, 2, 10, 50), new Core.Economy.EquipmentSpec[0], null);
            Assert.IsTrue(SaveCodec.TryRestore(data, economy, out var r, out var error), error);
            Assert.AreEqual(ChapterStep.ZouJingOrders, r.Chapter.Step);
            Assert.AreEqual(0, r.Chapter.Memories.Count);
            Assert.AreEqual(60, r.Exp.Single().exp);
        }

        [Test]
        public void APlaceLeftBehindIsNeverLoaded()
        {
            Assert.IsFalse(SaveCodec.PlaceOpen(SaveData.CampLocation, ChapterStep.Yingchuan));
            Assert.IsFalse(SaveCodec.PlaceOpen(SaveData.TownLocation, ChapterStep.Camp));
            Assert.IsTrue(SaveCodec.PlaceOpen(SaveData.AnxiLocation, ChapterStep.AnxiBandits));
            Assert.IsFalse(SaveCodec.PlaceOpen(SaveData.AnxiLocation, ChapterStep.Done));
        }
    }
}
