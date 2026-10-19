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
            Assert.That(data.Encounters.Select(e => e.Id), Is.EqualTo(new[] { "battle_1", "battle_2", "battle_3" }));
            var b1 = data.Encounter("battle_1");
            Assert.That(b1.Enemies.Select(e => (e.General.Id, e.Level)), Is.EqualTo(new[] { ("cheng_yuanzhi", 2), ("yt_headman", 1), ("yt_headman", 1) }));
            Assert.AreEqual(EnemyAiKind.HighestTroops, b1.Ai);
            Assert.AreEqual(4, data.Encounter("battle_2").Enemies.Count);
            Assert.AreEqual(TroopType.Strategist, data.Encounter("battle_3").Enemies[0].General.Troop);
            Assert.That(data.Encounters.Select(e => e.ExpReward), Is.EqualTo(new[] { 10, 20, 30 }));
            Assert.That(data.Encounters.Select(e => e.CopperReward), Is.EqualTo(new[] { 30, 50, 60 }));
            Assert.AreEqual(30, data.Ration.HealPercent);
            Assert.IsFalse(data.Troops.Get(TroopType.YellowTurban).PlayerUsable);
        }
    }
}
