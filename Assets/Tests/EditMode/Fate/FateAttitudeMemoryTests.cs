using System.Collections.Generic;
using NUnit.Framework;
using ThreeKingdoms.Core.Fate;

namespace ThreeKingdoms.Tests.Fate
{
    // S4 attitude rules (spec §5.1) and S5 memory store / compression (spec §5.6).
    public sealed class FateAttitudeTests
    {
        static CharacterProfile Profile(int l, int r, int b, int c, int cu, int f, int p, int pr) => new CharacterProfile
        {
            id = "x",
            personality = new Personality { loyalty = l, righteousness = r, benevolence = b, courage = c, cunning = cu, fame = f, profit = p, pride = pr }
        };

        static MemoryEntry Deceit() => new MemoryEntry { kind = MemoryKinds.Player, tags = new[] { "deceit" }, intensity = 2, valence = -1, importance = 3 };

        [Test] public void ZhaoYunDeceitExampleMatchesSpec()
        {
            var zhao = Profile(85, 90, 80, 85, 45, 30, 15, 35);
            var t = TuningTable.CreateDefault();
            Assert.AreEqual(-753664, Attitude.React(zhao.personality, "deceit", t)); // −11.5
            var d = Attitude.ApplyInteraction(zhao, new Relation(), Deceit(), t);
            Assert.AreEqual(-27 * 65536, d.FavorQ16);
            Assert.AreEqual(-27, d.Favor);
            Assert.AreEqual(-12, d.Trust);
            Assert.AreEqual(16, d.Views[ViewDims.IndexOf("deceit")]);
            Assert.AreEqual(-8, d.Views[ViewDims.IndexOf("faith")]);
            Assert.AreEqual(4, d.Views[ViewDims.IndexOf("wisdom")]);
            Assert.AreEqual(0, d.Views[ViewDims.IndexOf("valor")]);
        }

        [Test] public void DeceitHurtsXunYuTypeAndPleasesJiaXuType()
        {
            var t = TuningTable.CreateDefault();
            var xunyu = Profile(95, 85, 70, 50, 90, 60, 50, 50);
            var jiaxu = Profile(50, 40, 50, 50, 95, 20, 60, 50);
            Assert.Less(Attitude.React(xunyu.personality, "deceit", t), -5 * 65536);
            Assert.Greater(Attitude.React(jiaxu.personality, "deceit", t), 5 * 65536);
        }

        [Test] public void FavoredBookMultipliesFavorByOneAndAHalf()
        {
            var t = TuningTable.CreateDefault();
            var p = Profile(50, 60, 50, 50, 80, 50, 50, 50);
            var m = new MemoryEntry { kind = MemoryKinds.Player, tags = new[] { "gift_book" }, intensity = 2, valence = 1, importance = 3 };
            m.@params["itemId"] = "book_x";
            int plain = Attitude.ApplyInteraction(p, new Relation(), m, t).Favor;
            p.favoredBooks = new[] { "book_x" };
            var d = Attitude.ApplyInteraction(p, new Relation(), m, t);
            Assert.AreEqual(FixedMath.RoundToInt(FixedMath.Mul(Attitude.Compute(Profile(50, 60, 50, 50, 80, 50, 50, 50), m, t).FavorQ16, 98304)), d.Favor);
            Assert.Greater(d.Favor, plain);
        }

        [Test] public void TreasurePrideBonusNeedsAffinity()
        {
            var t = TuningTable.CreateDefault();
            var proud = Profile(50, 50, 50, 50, 50, 50, 50, 100);
            long without = Attitude.React(proud.personality, "gift_treasure", t, false);
            long with = Attitude.React(proud.personality, "gift_treasure", t, true);
            Assert.AreEqual(0, without);
            Assert.AreEqual(3 * 65536, with);
        }

        [Test] public void ApplyClampsAndInteractionDoesNotMutateInputs()
        {
            var rel = new Relation { favor = 95, trust = 98, views = new int[6] };
            var m = new MemoryEntry { kind = MemoryKinds.Player, tags = new[] { "keep_promise" }, intensity = 3, valence = 2, importance = 3 };
            var p = Profile(90, 90, 50, 50, 50, 50, 50, 50);
            var d = Attitude.ApplyInteraction(p, rel, m, TuningTable.CreateDefault());
            Assert.AreEqual(95, rel.favor, "ApplyInteraction must not touch the relation");
            Attitude.Apply(rel, d);
            Assert.AreEqual(100, rel.favor);
            Assert.AreEqual(100, rel.trust);
            Attitude.Apply(rel, new AttitudeDelta { Favor = -500, Trust = -500 });
            Assert.AreEqual(-100, rel.favor);
            Assert.AreEqual(0, rel.trust);
        }

        [Test] public void TuningJsonEqualsSpecDefaults()
        {
            var fromJson = FateTestKit.TestData().Tuning;
            var def = TuningTable.CreateDefault();
            Assert.AreEqual(FateTestKit.Json(def), FateTestKit.Json(fromJson));
        }
    }

    public sealed class FateMemoryTests
    {
        static CharacterState Char() => new CharacterState { id = "c", tier = Tiers.T2 };

        static MemoryEntry M(int seq, int importance, string node = "tv.N1", params string[] tags) => new MemoryEntry
        {
            id = "m" + seq, who = "c", key = "k" + seq, value = "v" + seq, seq = seq, importance = importance, node = node,
            kind = MemoryKinds.Player, tags = tags.Length == 0 ? new[] { "courtesy" } : tags, text = "t"
        };

        static int Index(string node) => node == null ? 0 : node[node.Length - 1] - '1';

        [Test] public void SameKeyOverwrites()
        {
            var c = Char();
            MemoryStore.Put(c, new MemoryEntry { key = "luzhi_cart", value = "A" });
            MemoryStore.Put(c, new MemoryEntry { key = "luzhi_cart", value = "B_escort" });
            Assert.AreEqual(1, c.memories.Count);
            Assert.AreEqual("B_escort", MemoryStore.Find(c, "luzhi_cart").value);
        }

        [Test] public void FortyOneEntriesCompressTheOldest()
        {
            var c = Char();
            for (int i = 1; i <= 41; i++) MemoryStore.Put(c, M(i, 3));
            Assert.AreEqual(1, MemoryStore.Compress(c, 40, Index, 0));
            Assert.IsTrue(c.memories[0].compressed);
            Assert.IsNull(c.memories[0].text);
            Assert.IsFalse(c.memories[1].compressed);
            Assert.AreEqual("v1", MemoryStore.Find(c, "k1").value, "memory() still answers after compression");
            Assert.IsTrue(c.stageSummaries.ContainsKey("tv.N1"));
            Assert.AreEqual("sum.T2.courtesy.none.none", c.stageSummaries["tv.N1"].textKey);
        }

        [Test] public void FortyEntriesDoNotCompress()
        {
            var c = Char();
            for (int i = 1; i <= 40; i++) MemoryStore.Put(c, M(i, 3));
            Assert.AreEqual(0, MemoryStore.Compress(c, 40, Index, 0));
        }

        [Test] public void ImportanceFiveIsNeverCompressed()
        {
            var c = Char();
            for (int i = 1; i <= 60; i++) MemoryStore.Put(c, M(i, 5));
            Assert.AreEqual(0, MemoryStore.Compress(c, 40, Index, 4));
            Assert.IsTrue(c.memories.TrueForAll(m => !m.compressed));
        }

        [Test] public void ImportanceFourWaitsThreeStages()
        {
            var c = Char();
            for (int i = 1; i <= 41; i++) MemoryStore.Put(c, M(i, 4, "tv.N1"));
            Assert.AreEqual(0, MemoryStore.Compress(c, 40, Index, 2), "only two stages old");
            Assert.AreEqual(1, MemoryStore.Compress(c, 40, Index, 3));
        }

        [Test] public void SummaryUsesTopThreeTagsByFrequency()
        {
            var c = Char();
            c.memories.Add(new MemoryEntry { key = "a", node = "tv.N2", compressed = true, tags = new[] { "valor", "gift" } });
            c.memories.Add(new MemoryEntry { key = "b", node = "tv.N2", compressed = true, tags = new[] { "valor", "wisdom" } });
            c.memories.Add(new MemoryEntry { key = "c", node = "tv.N2", compressed = true, tags = new[] { "benevolence" } });
            Assert.AreEqual("sum.T2.valor.benevolence.gift", MemoryStore.BuildSummary(c, "tv.N2").textKey);
        }
    }
}
