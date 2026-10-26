using System.Linq;
using NUnit.Framework;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.Save;
using ThreeKingdoms.UI.Story;

// 0.3 讨董篇 rules (SYSTEMS_V03 §1, §6; REVIEW_V03 R11–R14).
public sealed class Chapter3RulesTests
{
    [Test]
    public void TheDrillPaysOnceAndOnlyAtTheGarrison()
    {
        var c = ChapterState.At(ChapterStep.Suanzao);
        Assert.IsFalse(c.RecordVictory("battle_7"), "not after leaving");
        c = ChapterState.At(ChapterStep.Gongsun);
        Assert.IsTrue(c.RecordVictory("battle_7"));
        Assert.AreEqual(ChapterStep.Gongsun, c.Step, "the optional battle moves no step");
        Assert.IsFalse(c.RecordVictory("battle_7"));
    }

    [Test]
    public void HulaoLeadsToLuoyangOnce()
    {
        var c = ChapterState.At(ChapterStep.Hulao);
        Assert.AreEqual("battle_8", c.NextBattleId);
        Assert.IsTrue(c.RecordVictory("battle_8"));
        Assert.AreEqual(ChapterStep.Luoyang, c.Step);
        Assert.IsFalse(c.RecordVictory("battle_8"));
    }

    [Test]
    public void YearsFollowTheChronicle()
    {
        Assert.AreEqual(189, ChapterState.At(ChapterStep.Done).Year);
        Assert.AreEqual(190, ChapterState.At(ChapterStep.Gongsun).Year);
        var c = ChapterState.At(ChapterStep.Luoyang);
        Assert.AreEqual(190, c.Year, "191 begins with the flag written in D9, not with the step");
        c.Set(ChapterState.Year191);
        Assert.AreEqual(191, c.Year);
        Assert.AreEqual(ChronicleNode.N5, c.Node);
    }

    [Test]
    public void LeftPlacesCannotBeLoaded()
    {
        Assert.IsTrue(SaveCodec.PlaceOpen(SaveData.GongsunLocation, ChapterStep.Gongsun));
        Assert.IsFalse(SaveCodec.PlaceOpen(SaveData.GongsunLocation, ChapterStep.Suanzao));
        Assert.IsTrue(SaveCodec.PlaceOpen(SaveData.SuanzaoLocation, ChapterStep.Suanzao));
        Assert.IsFalse(SaveCodec.PlaceOpen(SaveData.SuanzaoLocation, ChapterStep.Hulao));
    }

    [Test]
    public void OnlyTheLastScrollOfTheVersionIsSealed()
    {
        Assert.IsFalse(InkScrollText.Sealed("anxi"));
        Assert.IsFalse(InkScrollText.Sealed(ChapterScripts3.ScrollFenLuoyang), "0.4 moved the seal to 界橋");
        Assert.IsTrue(InkScrollText.Sealed(ChapterScripts4.ScrollJieqiao));
        foreach (var id in new[] { ChapterScripts3.ScrollWenjiu, ChapterScripts3.ScrollSanying, ChapterScripts3.ScrollFenLuoyang })
        {
            Assert.IsTrue(InkScrollText.TryGet(id, out var e), id);
            Assert.LessOrEqual(InkScrollText.Length(e.Body), 100, id);
        }
    }

    [Test]
    public void EveryKeyTheScriptsUseHasText()
    {
        var steps = new[] { ChapterScripts3.Drift, ChapterScripts3.ToSuanzao, ChapterScripts3.Huimeng, ChapterScripts3.ToHulao, ChapterScripts3.AfterHulao }
            .SelectMany(id => ChapterScripts3.Stage(id));
        bool Known(string key) => key == null || key.StartsWith("raw:") || ChapterText3.Lines.ContainsKey(key) || ChapterText.Lines.ContainsKey(key);
        foreach (var s in Flatten(steps))
        {
            if (s.Op == StageOp.Line || s.Op == StageOp.Choice) Assert.IsTrue(Known(s.B), "text for " + s);
            if (s.Op == StageOp.Card) Assert.IsTrue(Known(s.A), "card " + s.A);
            if (s.Op == StageOp.Choice) foreach (var o in s.Options) Assert.IsTrue(Known(o), "option " + o);
            if (s.Op == StageOp.Remember) Assert.IsTrue(Known(s.D), "memory " + s.D);
            if (s.Op == StageOp.Line) Assert.IsFalse(string.IsNullOrEmpty(StoryText.Speaker(s.A)) && s.A != "narrator" && s.A != "system", "speaker " + s.A);
        }
    }

    static System.Collections.Generic.IEnumerable<StageStep> Flatten(System.Collections.Generic.IEnumerable<StageStep> steps)
    {
        foreach (var s in steps)
        {
            yield return s;
            foreach (var b in s.Branches) foreach (var x in Flatten(b)) yield return x;
        }
    }
}
