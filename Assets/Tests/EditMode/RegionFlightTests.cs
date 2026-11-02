using NUnit.Framework;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.UI.Story;

// The courier's travel flies over the 3D 天下图 (RegionTravel → March → TianxiaFlight): every region and home has a
// place on the map, so no trip falls back to a bare fade.
public sealed class RegionFlightTests
{
    [Test]
    public void EveryRegionAndHomeIsOnTheMap()
    {
        foreach (var r in RegionTravel.Regions)
            Assert.IsTrue(TianxiaFlight.Places.ContainsKey(RegionTravel.PlaceOf(r.Scene) ?? ""), r.Name);
        foreach (var home in new[] { GameRun.GongsunScene, GameRun.SuanzaoScene, GameRun.BohaiScene, GameRun.XiapiScene })
            Assert.IsTrue(TianxiaFlight.Places.ContainsKey(RegionTravel.PlaceOf(home) ?? ""), home);
    }

    [Test]
    public void TheFlightShowsTheRunsYear()
    {
        var ch = new ChapterState();
        ch.Set(ChapterState.Year191);
        Assert.AreEqual("year.191", RegionTravel.YearKey(ch));
    }
}
