using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ThreeKingdoms.Core.Economy;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Data.Battle;
using ThreeKingdoms.Save;
using UnityEditor;

namespace ThreeKingdoms.Tests.EditMode
{
    // Chapter 1 year nodes (CHAPTER1_RISE_SCRIPT.md §3, §7.5) and their save fields (ADR-0004 amendment 2026-10-24).
    public sealed class ChronicleTests
    {
        const string CatalogPath = "Assets/_Project/Data/Battle/BattleCatalog.asset";

        [Test]
        public void ANewGameStartsIn184WithZhuojunOpen()
        {
            var c = ChronicleState.Start();
            Assert.AreEqual("N1", c.NodeId);
            Assert.AreEqual(184, c.Year);
            Assert.IsTrue(c.IsOpen(Chapter1Chronicle.R1));
            Assert.IsTrue(c.IsOpen(Chapter1Chronicle.R3Guangzong));
            Assert.IsFalse(c.IsOpen(Chapter1Chronicle.R4));
            Assert.IsEmpty(c.DangerAreas);
        }

        [Test]
        public void Node191OpensTheBohaiPlainAndMakesYeDangerous()
        {
            var c = ChronicleState.At("N5");
            Assert.AreEqual(191, c.Year);
            Assert.IsTrue(c.IsOpen(Chapter1Chronicle.R4));
            Assert.IsTrue(c.IsOpen(Chapter1Chronicle.R3));
            Assert.IsFalse(c.IsOpen(Chapter1Chronicle.R3Guangzong), "the whole of R3 replaces the Guangzong-only area");
            Assert.IsFalse(c.IsOpen(Chapter1Chronicle.Yingchuan), "the one-off Yingchuan map closed in 185");
            Assert.IsTrue(c.IsDangerous(Chapter1Chronicle.Ye));
            Assert.IsFalse(c.IsDangerous(Chapter1Chronicle.R6));
        }

        [Test]
        public void YanzhouIsDangerousTheMomentItOpens()
        {
            var c = ChronicleState.At("N6");
            Assert.IsTrue(c.IsOpen(Chapter1Chronicle.R6) && c.IsDangerous(Chapter1Chronicle.R6));
        }

        [Test]
        public void NodesOnlyMoveForwardOneAtATime()
        {
            var c = ChronicleState.Start();
            Assert.IsFalse(c.AdvanceTo("N3"), "skipping N2");
            Assert.IsFalse(c.AdvanceTo("N1"), "the current node again");
            Assert.IsTrue(c.AdvanceTo("N2"));
            Assert.AreEqual("N2", c.NodeId);
            var end = ChronicleState.At("epilogue");
            Assert.IsFalse(end.Advance());
            Assert.AreEqual("epilogue", end.NodeId);
        }

        [Test]
        public void StoryBeatsCanCloseAndCalmSinglePlaces()
        {
            var c = ChronicleState.At("N5");
            c.Calm(Chapter1Chronicle.Ye);   // a pass from Tian Kai or Kong Rong
            Assert.IsFalse(c.IsDangerous(Chapter1Chronicle.Ye));
            c.Close(Chapter1Chronicle.R5b);
            Assert.IsFalse(c.IsOpen(Chapter1Chronicle.R5b));
        }

        [Test]
        public void UnknownIdsAreRejectedOnRestore()
        {
            Assert.Throws<ArgumentException>(() => ChronicleState.Restore("N99", null, null));
            Assert.Throws<ArgumentException>(() => ChronicleState.Restore("N5", new[] { "R42" }, null));
            Assert.Throws<ArgumentException>(() => ChronicleState.Restore("N5", new[] { Chapter1Chronicle.R4 }, new[] { "atlantis" }));
        }

        [Test]
        public void ChronicleRoundTripsThroughASaveFile()
        {
            var data = AssetDatabase.LoadAssetAtPath<BattleCatalog>(CatalogPath).ToRuntime();
            string dir = Path.Combine(Path.GetTempPath(), "tk-chronicle-tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                var slots = new SaveSlots(dir, data.ManualSaveSlots);
                var chronicle = ChronicleState.At("N5");
                chronicle.Calm(Chapter1Chronicle.Ye);
                var save = SaveCodec.Capture(StoryState.AtBattle(3), new Inventory(data.Economy), data.Party.ToDictionary(g => g.Id, _ => 60),
                    SaveData.RegionPrefix + Chapter1Chronicle.R4, 4, "第一篇章　崛起", "probe", new DateTime(2026, 10, 24, 20, 0, 0), chronicle: chronicle);
                slots.Write(SaveSlots.Manual(1), save);
                Assert.AreEqual(SaveReadStatus.Ok, slots.Read(SaveSlots.Manual(1), out var read));
                Assert.IsTrue(SaveCodec.TryRestore(read, data.Economy, out var r, out string error), error);
                Assert.AreEqual("N5", r.Chronicle.NodeId);
                Assert.IsTrue(r.Chronicle.IsOpen(Chapter1Chronicle.R4));
                Assert.IsFalse(r.Chronicle.IsDangerous(Chapter1Chronicle.Ye), "a calmed place stays calm after loading");
                Assert.AreEqual("region:R4", r.Location);
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }

        [Test]
        public void ARegionSaveNeedsThatRegionOpen()
        {
            var data = AssetDatabase.LoadAssetAtPath<BattleCatalog>(CatalogPath).ToRuntime();
            var save = SaveCodec.Capture(StoryState.AtBattle(3), new Inventory(data.Economy), data.Party.ToDictionary(g => g.Id, _ => 60),
                SaveData.RegionPrefix + Chapter1Chronicle.R4, 4, "第一篇章　崛起", "probe", DateTime.Now, chronicle: ChronicleState.Start());
            Assert.IsFalse(SaveCodec.TryRestore(save, data.Economy, out _, out string error));
            StringAssert.Contains("尚未开放", error);
        }

        [Test]
        public void SavesFromBeforeChroniclesStartAtTheFirstNode()
        {
            var data = AssetDatabase.LoadAssetAtPath<BattleCatalog>(CatalogPath).ToRuntime();
            var save = SaveCodec.Capture(new StoryState(), new Inventory(data.Economy), data.Party.ToDictionary(g => g.Id, _ => 0),
                SaveData.TownLocation, 1, "第一章", "1.0", DateTime.Now);
            save.chronicleNode = null; save.openRegions = null; save.dangerAreas = null;   // what an older file reads as
            Assert.IsTrue(SaveCodec.TryRestore(save, data.Economy, out var r, out string error), error);
            Assert.AreEqual("N1", r.Chronicle.NodeId);
            Assert.IsTrue(r.Chronicle.IsOpen(Chapter1Chronicle.R1));
        }
    }
}
