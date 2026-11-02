using System.Linq;
using NUnit.Framework;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Gameplay.Story;

// The keepsake shelf (design/gdd/keepsake-shelf.md): the catalogue is whole, flags map to entries, R1's two older
// keepsakes count from their own flag and memory.
public sealed class KeepsakeTests
{
    [Test]
    public void CatalogueIsWhole()
    {
        var all = KeepsakeCatalog.All;
        Assert.AreEqual(all.Length, all.Select(e => e.Id).Distinct().Count(), "ids unique");
        Assert.AreEqual(22, all.Length, "bible §6 has 22 keepsakes");
        Assert.AreEqual(10, KeepsakeCatalog.Shown.Count(), "ten can be had in this build");
        foreach (var e in KeepsakeCatalog.Shown)
        {
            Assert.IsNotEmpty(e.Name, e.Id); Assert.IsNotEmpty(e.Who, e.Id); Assert.IsNotEmpty(e.WhoName, e.Id);
            Assert.IsNotEmpty(e.Place, e.Id); Assert.IsNotEmpty(e.Year, e.Id);
            Assert.IsNotEmpty(e.Origin, e.Id); Assert.IsNotEmpty(e.Description, e.Id);
        }
    }

    [Test]
    public void FlagsMapToTheShelf()
    {
        var ch = new ChapterState();
        string seen = null;
        ch.Flagged += f => seen = f;
        Assert.AreEqual(0, KeepsakeCatalog.HeldCount(ch));
        ch.Set(SideQuests.KeepsakePrefix + "zang_hong_draft");
        Assert.AreEqual("臧洪盟辞底稿", KeepsakeCatalog.FromFlag(seen)?.Name);
        Assert.AreEqual(1, KeepsakeCatalog.HeldCount(ch));
        seen = null;
        ch.Set(SideQuests.KeepsakePrefix + "zang_hong_draft");
        Assert.IsNull(seen, "a flag already up raises nothing");
        Assert.IsNull(KeepsakeCatalog.FromFlag(SideQuests.KeepsakePrefix + "mang_sunzi"), "the Beimang book is a 兵书, not a keepsake");
        Assert.IsNull(KeepsakeCatalog.FromFlag(SideQuests.KeepsakePrefix + "fengsu_copy"), "not in this build");
    }

    [Test]
    public void OlderR1KeepsakesCount()
    {
        var ch = new ChapterState();
        ch.Set(FieldScriptsR1.HuMarket);
        ch.Remember("lu_yu", "rubbing", "kept_rubbing", "mem.lu_yu.rubbing.kept_rubbing");
        Assert.IsTrue(KeepsakeCatalog.Held(ch, KeepsakeCatalog.Find("hu_horse_tally")));
        Assert.IsTrue(KeepsakeCatalog.Held(ch, KeepsakeCatalog.Find("shijing_copy")));
        Assert.AreEqual(2, KeepsakeCatalog.HeldCount(ch));
    }
}
