using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ThreeKingdoms.Core.Battle;
using ThreeKingdoms.Data.Battle;
using UnityEditor;

namespace ThreeKingdoms.Tests.EditMode
{
    // The shipped battle assets convert and validate, and hold the combat GDD's battle-1 line-up.
    public sealed class BattleDataAssetTests
    {
        const string CatalogPath = "Assets/_Project/Data/Battle/BattleCatalog.asset";

        [Test]
        public void EveryBattleAssetConverts()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:BattleCatalog"))
                Assert.DoesNotThrow(() => AssetDatabase.LoadAssetAtPath<BattleCatalog>(AssetDatabase.GUIDToAssetPath(guid)).ToRuntime());
        }

        [Test]
        public void CatalogHoldsPartyAndThreeEncounters()
        {
            var data = AssetDatabase.LoadAssetAtPath<BattleCatalog>(CatalogPath).ToRuntime();
            Assert.That(data.Party.Select(g => g.Id), Is.EqualTo(new[] { "liu_bei", "guan_yu", "zhang_fei" }));
            Assert.That(data.Encounters.Select(e => e.Id), Is.EqualTo(new[] { "battle_1", "battle_2", "battle_3", "battle_4", "battle_5", "battle_6", "battle_7", "battle_8", "battle_9", "battle_10", "battle_11" }));   // 0.2 adds 4–6, 0.3 adds 7–8, 0.4 adds 9–11
            var b1 = data.Encounter("battle_1");
            Assert.That(b1.Enemies.Select(e => (e.General.Id, e.Level)), Is.EqualTo(new[] { ("cheng_yuanzhi", 2), ("yt_headman", 1), ("yt_headman", 1) }));
            Assert.AreEqual(EnemyAiKind.HighestTroops, b1.Ai);
            Assert.AreEqual(4, data.Encounter("battle_2").Enemies.Count);
            Assert.AreEqual(TroopType.Strategist, data.Encounter("battle_3").Enemies[0].General.Troop);
            Assert.That(data.Encounters.Select(e => e.ExpReward), Is.EqualTo(new[] { 10, 20, 30, 40, 50, 60, 70, 90, 90, 110, 130 }));
            Assert.That(data.Encounters.Select(e => e.CopperReward), Is.EqualTo(new[] { 30, 50, 60, 60, 70, 80, 60, 120, 100, 120, 150 }));
            Assert.AreEqual(30, data.Ration.HealPercent);
            Assert.IsFalse(data.Troops.Get(TroopType.YellowTurban).PlayerUsable);
        }
    
        // A plain scripted player (no look-ahead): Liu Bei regroups whoever is under half, else fire arrows; Guan Yu
        // break-formations / charges the weakest; Zhang Fei sweeps when two or more enemies stand, else attacks.
        static IEnumerable<BattleCommand> Script(BattleState s)
        {
            var target = s.Enemies.Where(e => e.Active).OrderBy(e => e.Troops).ThenBy(e => e.Slot).First();
            var hurt = s.Players.Where(u => u.Active && u.Troops * 2 < u.Stats.MaxTroops).OrderBy(u => u.Troops).FirstOrDefault();
            foreach (var p in s.Players.Where(u => u.Active))
            {
                SkillSpec Skill(string id) => p.Skills.FirstOrDefault(k => k.Id == id && s.CanUseSkill(p, k));
                if (p.Id == "liu_bei" && hurt != null && Skill("regroup") != null) yield return BattleCommand.UseSkill(p, Skill("regroup"), hurt);
                else if (p.Id == "liu_bei" && Skill("fire_arrows") != null) yield return BattleCommand.UseSkill(p, Skill("fire_arrows"), target);
                else if (p.Id == "guan_yu" && Skill("break_formation") != null && target.Modifiers.Count == 0) yield return BattleCommand.UseSkill(p, Skill("break_formation"), target);
                else if (p.Id == "guan_yu" && Skill("charge") != null) yield return BattleCommand.UseSkill(p, Skill("charge"), target);
                else if (p.Id == "zhang_fei" && s.Enemies.Count(e => e.Active) >= 2 && Skill("sweep") != null) yield return BattleCommand.UseSkill(p, Skill("sweep"));
                else if (hurt != null && s.CanUseRation(p) && hurt.Troops * 3 < hurt.Stats.MaxTroops) yield return BattleCommand.Ration(p, hurt);
                else yield return BattleCommand.Attack(p, target);
            }
        }

        [TestCase("battle_1", 1)]
        [TestCase("battle_2", 2)]
        [TestCase("battle_3", 3)]
        public void EachBattleIsWinnableAtItsExpectedLevel(string encounterId, int level)
        {
            var data = AssetDatabase.LoadAssetAtPath<BattleCatalog>(CatalogPath).ToRuntime();
            var lineup = data.Party.Select(g => CombatantInput.AtLevel(g, level)).ToList();
            var session = new BattleSession(data.Encounter(encounterId), lineup, data.Ration, 2, data.Config, data.Troops);
            for (int i = 0; session.Result == null && i < 40; i++) session.PlayRound(Script(session.State).ToList());
            Assert.IsNotNull(session.Result, "finishes within 40 rounds");
            TestContext.WriteLine($"{encounterId}: {session.Result.Outcome} in {session.Result.Rounds} rounds, " +
                string.Join(", ", session.State.Players.Select(p => $"{p.Name} {p.Troops}/{p.Stats.MaxTroops}")));
            Assert.AreEqual(BattleOutcome.Victory, session.Result.Outcome);
        }
    }
}
