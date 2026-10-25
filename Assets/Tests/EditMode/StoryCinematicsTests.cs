using System.Linq;
using NUnit.Framework;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.UI.Story;

// The ink scroll text and the oath timing (user 2026-10-24, design/gdd/story-cinematics.md).
public sealed class StoryCinematicsTests
{
    [Test]
    public void EveryScrollHasATitleAndAtMostAHundredCharacters()
    {
        foreach (var id in new[] { StoryCinematics.OathScroll, StoryCinematics.YellowTurbanScroll })
        {
            Assert.IsTrue(InkScrollText.TryGet(id, out var e), id);
            Assert.IsNotEmpty(e.Title);
            Assert.LessOrEqual(InkScrollText.Length(e.Body), 100, id);
            StringAssert.Contains("——", e.Body, "the source follows the story");
        }
    }

    [Test]
    public void ScrollTextIsTraditional()
    {
        // Simplified forms that must not appear (their traditional forms are used in the scroll text).
        const string simplified = "汉黄刘关张飞园结义备愿协图国战败为与";
        foreach (var id in InkScrollText.Ids)
        {
            InkScrollText.TryGet(id, out var e);
            foreach (char c in e.Title + e.Body)
                Assert.IsFalse(simplified.Contains(c), $"{id}: simplified character {c}");
        }
    }

    [Test]
    public void ColumnsReadRightToLeftWithTheSourceApart()
    {
        InkScrollText.TryGet(StoryCinematics.OathScroll, out var e);
        var columns = InkScrollText.Columns(e.Body, 13);
        Assert.IsTrue(columns.All(c => c.Length <= 13));
        Assert.AreEqual(e.Body, string.Concat(columns));
        Assert.IsTrue(columns.Last().StartsWith("——") || columns.Any(c => c.StartsWith("——")), "the source starts its own column");
        Assert.AreEqual("︱︱典出︽三國演義︾", InkScrollText.ToVertical("——典出《三國演義》"));
    }

    [Test]
    public void TheOathLastsAboutThirtySeconds()
    {
        float total = TownStoryController.OathBeats.Sum(b => b.Seconds);
        Assert.That(total, Is.InRange(28f, 36f));
        Assert.IsTrue(TownStoryController.OathBeats.Any(b => b.Act == TownStoryController.OathAct.Kowtow), "they kneel and bow");
        Assert.IsTrue(TownStoryController.OathBeats.Any(b => b.Act == TownStoryController.OathAct.Toast), "they share wine");
    }
}
