using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ThreeKingdoms.Core.Economy;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Data.Battle;
using ThreeKingdoms.Save;
using UnityEditor;
using UnityEngine;

namespace ThreeKingdoms.Tests.EditMode
{
    // save-system GDD acceptance criteria that need no scene: every V1_SPEC §9 field round-trips, damaged and
    // unsupported files are reported and left untouched, sequences never load mid-play, rewards come once per run.
    public sealed class SaveTests
    {
        const string CatalogPath = "Assets/_Project/Data/Battle/BattleCatalog.asset";

        BattleData data;
        string dir;
        SaveSlots slots;

        [SetUp]
        public void Prepare()
        {
            data = AssetDatabase.LoadAssetAtPath<BattleCatalog>(CatalogPath).ToRuntime();
            dir = Path.Combine(Path.GetTempPath(), "tk-save-tests-" + Guid.NewGuid().ToString("N"));
            slots = new SaveSlots(dir, data.ManualSaveSlots);
        }

        [TearDown]
        public void Remove() { if (Directory.Exists(dir)) Directory.Delete(dir, true); }

        static StoryState AfterOath(int battlesDone) => StoryState.Restore(true, true, SequenceState.Done, battlesDone, SequenceState.NotStarted);

        [Test]
        public void ThreeManualSlotsComeFromTheCatalog() => Assert.AreEqual(3, slots.ManualCount);

        [Test]
        public void EveryFieldRoundTrips()
        {
            var eco = data.Economy;
            var inv = new Inventory(eco, copper: 150, rations: 1);
            inv.Buy(eco.Find("iron_shuo"));
            inv.Equip(data.Party[1], eco.Find("iron_shuo"));
            inv.Buy(eco.Find("leather_bracers"));
            var exp = data.Party.ToDictionary(g => g.Id, _ => 10);
            var save = SaveCodec.Capture(AfterOath(1), inv, exp, "battle_2", 2, "第一章", "1.0", new DateTime(2026, 10, 20, 18, 30, 0));
            slots.Write(SaveSlots.Auto, save);

            Assert.AreEqual(SaveReadStatus.Ok, slots.Read(SaveSlots.Auto, out var read));
            Assert.IsTrue(SaveCodec.TryRestore(read, eco, out var r, out string error), error);
            Assert.AreEqual("battle_2", r.Location);
            Assert.IsTrue(r.Story.GuanYuJoined && r.Story.ZhangFeiJoined);
            Assert.AreEqual(SequenceState.Done, r.Story.Oath);
            Assert.AreEqual(1, r.Story.BattlesDone);
            Assert.AreEqual(50, r.Inventory.Copper);   // 150 − 60 − 40
            Assert.AreEqual(1, r.Inventory.Rations);
            Assert.AreEqual("iron_shuo", r.Inventory.Worn("guan_yu", EquipSlot.Weapon).Id);
            Assert.AreEqual(1, r.Inventory.Count(eco.Find("cavalry_lance")));
            Assert.AreEqual(1, r.Inventory.Count(eco.Find("leather_bracers")));
            Assert.AreEqual("hunting_bow", r.Inventory.Worn("liu_bei", EquipSlot.Weapon).Id);
            Assert.That(r.Exp, Is.EquivalentTo(exp.Select(kv => (kv.Key, kv.Value))));
            Assert.AreEqual(2, read.leaderLevel);
            Assert.AreEqual("第一章", read.chapter);
            Assert.AreEqual("2026-10-20T18:30:00", read.savedAt);
        }

        [Test]
        public void ADamagedFileIsReportedAndLeftAlone()
        {
            Directory.CreateDirectory(dir);
            string path = slots.PathOf(SaveSlots.Manual(1));
            File.WriteAllText(path, "{ not json");
            var before = File.ReadAllBytes(path);
            Assert.AreEqual(SaveReadStatus.Damaged, slots.Read(SaveSlots.Manual(1), out var d));
            Assert.IsNull(d);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
        }

        [Test]
        public void AnotherFormatVersionIsUnsupportedAndLeftAlone()
        {
            var save = SaveCodec.Capture(new StoryState(), new Inventory(data.Economy), data.Party.ToDictionary(g => g.Id, _ => 0),
                SaveData.TownLocation, 1, "c", "1.0", DateTime.Now);
            save.formatVersion = 99;
            slots.Write(SaveSlots.Manual(2), save);
            var before = File.ReadAllBytes(slots.PathOf(SaveSlots.Manual(2)));
            Assert.AreEqual(SaveReadStatus.Unsupported, slots.Read(SaveSlots.Manual(2), out _));
            Assert.That(File.ReadAllBytes(slots.PathOf(SaveSlots.Manual(2))), Is.EqualTo(before));
        }

        [Test]
        public void EmptySlotsAreEmptyAndWritesLeaveNoTemporaryFile()
        {
            Assert.AreEqual(SaveReadStatus.Empty, slots.Read(SaveSlots.Manual(3), out _));
            var save = SaveCodec.Capture(new StoryState(), new Inventory(data.Economy), data.Party.ToDictionary(g => g.Id, _ => 0),
                SaveData.TownLocation, 1, "c", "1.0", DateTime.Now);
            slots.Write(SaveSlots.Manual(3), save);
            save.copper = 7;
            slots.Write(SaveSlots.Manual(3), save);   // overwrite replaces the slot
            Assert.AreEqual(SaveReadStatus.Ok, slots.Read(SaveSlots.Manual(3), out var d));
            Assert.AreEqual(7, d.copper);
            Assert.IsEmpty(Directory.GetFiles(dir, "*.tmp"));
        }

        [Test]
        public void AnOathCutOffMidPlayReplaysFromTheStart()
        {
            var story = StoryState.Restore(true, true, SequenceState.NotStarted, 0, SequenceState.NotStarted);
            Assert.IsTrue(story.BeginOath());
            var save = SaveCodec.Capture(story, new Inventory(data.Economy), data.Party.ToDictionary(g => g.Id, _ => 0),
                SaveData.TownLocation, 1, "c", "1.0", DateTime.Now);
            Assert.AreEqual((int)SequenceState.NotStarted, save.oath, "never saved mid-sequence");
            Assert.IsTrue(SaveCodec.TryRestore(save, data.Economy, out var r, out _));
            Assert.AreEqual(StoryPhase.Oath, r.Story.Phase, "the oath plays again from its start");
        }

        [Test]
        public void AnEarlierSaveCanWinTheSameBattleOnceMore()
        {
            var save = SaveCodec.Capture(AfterOath(0), new Inventory(data.Economy), data.Party.ToDictionary(g => g.Id, _ => 0),
                "battle_1", 1, "c", "1.0", DateTime.Now);
            Assert.IsTrue(SaveCodec.TryRestore(save, data.Economy, out var r, out _));
            Assert.IsTrue(r.Story.RecordVictory("battle_1"), "the reward is granted again in this run");
            Assert.IsFalse(r.Story.RecordVictory("battle_1"), "but only once");
        }

        [Test]
        public void SavesThatPointNowhereAreRejected()
        {
            var save = SaveCodec.Capture(AfterOath(0), new Inventory(data.Economy), data.Party.ToDictionary(g => g.Id, _ => 0),
                "battle_2", 1, "c", "1.0", DateTime.Now);
            Assert.IsFalse(SaveCodec.TryRestore(save, data.Economy, out _, out _), "battle 2 before battle 1 is won");
            save.location = "xuzhou";
            Assert.IsFalse(SaveCodec.TryRestore(save, data.Economy, out _, out _), "no playable Xuzhou");
            save.location = SaveData.TownLocation;
            save.bag = new[] { new BagEntry { item = "magic_sword", count = 1 } };
            Assert.IsFalse(SaveCodec.TryRestore(save, data.Economy, out _, out _), "unknown items mean a damaged save");
        }

        [Test]
        public void SavesHoldNoAccountData()
        {
            var save = SaveCodec.Capture(new StoryState(), new Inventory(data.Economy), data.Party.ToDictionary(g => g.Id, _ => 0),
                SaveData.TownLocation, 1, "c", "1.0", DateTime.Now);
            string json = JsonUtility.ToJson(save);
            foreach (var word in new[] { "account", "license", "licence", "token", "password", "user" })
                StringAssert.DoesNotContain(word, json.ToLowerInvariant());
        }
    }
}
