using System;
using System.Linq;
using NUnit.Framework;
using ThreeKingdoms.Core.Economy;
using ThreeKingdoms.Core.Town;
using ThreeKingdoms.Save;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Data.Battle;
using UnityEditor;

static class EconomyFixture
{
    public static EconomyData Data() =>
        AssetDatabase.LoadAssetAtPath<BattleCatalog>("Assets/_Project/Data/Battle/BattleCatalog.asset").ToRuntime().Economy;
}

// Town life (user 2026-10-22): reputation from alms, the scholar's quiz and the archery range; the dice game.
public sealed class TownLifeTests
{
    static Inventory Purse(int copper) => new Inventory(EconomyFixture.Data(), copper);

    [Test] public void AlmsCostCopperAndRaiseReputation()
    {
        var inv = Purse(30); var life = new TownLife();
        Assert.AreEqual(Charity.Result.Given, Charity.Give(inv, life, 1));
        Assert.AreEqual(10, inv.Copper);
        Assert.AreEqual(3, life.Reputation);
        Assert.AreEqual(Charity.Result.NotEnoughCopper, Charity.Give(inv, life, 2), "50 copper is more than he has");
        Assert.AreEqual(10, inv.Copper);
        Assert.AreEqual(Charity.Result.Declined, Charity.Give(inv, life, -1));
        Assert.AreEqual(3, life.Reputation);
    }

    [Test] public void QuizScoresRightAnswersAndPunishesAllWrong()
    {
        Assert.AreEqual(-2, ScholarQuiz.ReputationFor(0));
        Assert.AreEqual(1, ScholarQuiz.ReputationFor(1));
        Assert.AreEqual(3, ScholarQuiz.ReputationFor(3));
        var life = new TownLife(1);
        life.AddReputation(ScholarQuiz.ReputationFor(0));
        Assert.AreEqual(0, life.Reputation, "reputation never goes below zero");

        var picked = ScholarQuiz.Pick(QuizBank.All.Length, new Random(7));
        Assert.AreEqual(3, picked.Distinct().Count(), "three different questions");
        Assert.GreaterOrEqual(QuizBank.All.Length, 30);
        Assert.IsTrue(QuizBank.All.All(q => q.Options.Length == 3 && q.Options.Distinct().Count() == 3));
        var (text, options, answer) = QuizBank.Ask(1, new Random(3));
        Assert.AreEqual(QuizBank.All[1].Options[0], options[answer], "the shuffled answer index points at the right option");
    }

    [Test] public void DiceBigSmallAndTriples()
    {
        Assert.IsTrue(DiceGame.IsBig(new[] { 6, 5, 1 }));
        Assert.IsTrue(DiceGame.IsSmall(new[] { 1, 2, 3 }));
        Assert.IsFalse(DiceGame.IsBig(new[] { 5, 5, 5 }) || DiceGame.IsSmall(new[] { 2, 2, 2 }), "a triple is neither: the house wins");
        var inv = Purse(30);
        Assert.AreEqual(DiceGame.Outcome.Won, DiceGame.Bet(inv, 20, true, new[] { 6, 5, 4 }));
        Assert.AreEqual(50, inv.Copper, "even money");
        Assert.AreEqual(DiceGame.Outcome.Lost, DiceGame.Bet(inv, 20, false, new[] { 3, 3, 3 }));
        Assert.AreEqual(30, inv.Copper);
        Assert.AreEqual(DiceGame.Outcome.NotEnoughCopper, DiceGame.Bet(inv, 50, true, new[] { 6, 6, 5 }));
        Assert.AreEqual(30, inv.Copper);
    }

    // Finds an angle and draw whose arc passes through the given target.
    static (float angle, float power) AimAt(ArcheryGame.Target t)
    {
        for (float a = ArcheryGame.MinAngle; a <= ArcheryGame.MaxAngle; a += 0.5f)
        for (float p = ArcheryGame.MinPower; p <= ArcheryGame.MaxPower; p += 0.01f)
            if (ArcheryGame.Path(a, p).Any(pt => (pt.x - t.X) * (pt.x - t.X) + (pt.y - t.Y) * (pt.y - t.Y) < 0.2f)) return (a, p);
        Assert.Fail("no arc reaches the target");
        return default;
    }

    [Test] public void ArcheryCountsEachTargetOnceAndRestocks()
    {
        var game = new ArcheryGame(new Random(11));
        Assert.AreEqual(5, game.Targets.Count);
        Assert.IsTrue(game.Targets.Zip(game.Targets.Skip(1), (l, r) => r.X - l.X).All(d => d > ArcheryGame.TargetRadius * 2), "five targets apart along the range");
        // Laid out along one or two flyable arcs (user 2026-10-23): some single arrow passes through three or more.
        int best = 0;
        for (float ang = ArcheryGame.MinAngle; ang <= ArcheryGame.MaxAngle; ang += 1f)
        for (float pw = ArcheryGame.MinPower; pw <= ArcheryGame.MaxPower; pw += 0.02f)
        {
            var path = ArcheryGame.Path(ang, pw);
            best = System.Math.Max(best, game.Targets.Count(t => path.Any(q => (q.x - t.X) * (q.x - t.X) + (q.y - t.Y) * (q.y - t.Y) <= ArcheryGame.TargetRadius * ArcheryGame.TargetRadius)));
        }
        Assert.GreaterOrEqual(best, 3, "one arrow can take at least three");
        var first = game.Targets[0];
        var (a, p) = AimAt(first);
        var shot = game.Shoot(a, p);
        Assert.Contains(first, shot.Hits);
        int score = game.Score;
        var again = game.Shoot(a, p);
        Assert.IsFalse(again.Hits.Contains(first), "a target already hit scores nothing");
        Assert.AreEqual(score + again.Hits.Count, game.Score);
        Assert.AreEqual(1, game.ArrowsLeft);
        game.Shoot(10f, 0.5f);
        Assert.IsTrue(game.Finished);
        Assert.IsNull(game.Shoot(10f, 0.5f), "three arrows only");
    }

    [Test] public void ArcheryRestocksWhenAllFiveAreHit()
    {
        var game = new ArcheryGame(new Random(5));
        foreach (var t in game.Targets) t.Hit = true;
        game.Targets[4].Hit = false;
        var (a, p) = AimAt(game.Targets[4]);
        var shot = game.Shoot(a, p);
        Assert.IsTrue(shot.Refreshed, "the last of the five falls and a new set stands up");
        Assert.IsTrue(game.Targets.All(t => !t.Hit));
        Assert.AreEqual(2, game.Sets);
    }

    [Test] public void ReputationIsSavedAndLoaded()
    {
        var data = EconomyFixture.Data();
        var save = SaveCodec.Capture(new StoryState(), new Inventory(data), new System.Collections.Generic.Dictionary<string, int>(),
            SaveData.TownLocation, 1, "第一章", "1.0", new DateTime(2026, 10, 22), new TownLife(9));
        Assert.AreEqual(9, save.reputation);
        Assert.IsTrue(SaveCodec.TryRestore(save, data, out var r, out string error), error);
        Assert.AreEqual(9, r.Life.Reputation);
    }
}
