using NUnit.Framework;
using ThreeKingdoms.Core.Fate;

namespace ThreeKingdoms.Tests.Fate
{
    // S2: fixed point, hashes, PCG32 and the EXP table (spec §5.3).
    public sealed class FateMathTests
    {
        [Test] public void Pcg32MatchesTheReferenceDemoVector()
        {
            // pcg32-demo: pcg32_srandom(42u, 54u), first six pcg32_random() outputs
            var rng = new Pcg32(42UL, 54UL);
            uint[] expected = { 0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e };
            foreach (var e in expected) Assert.AreEqual(e, rng.Next());
        }

        [Test] public void Fnv1a64KnownVectors()
        {
            Assert.AreEqual(0xcbf29ce484222325UL, Hash64.Fnv1a(""));
            Assert.AreEqual(0xaf63dc4c8601ec8cUL, Hash64.Fnv1a("a"));
            Assert.AreEqual(0x85944171f73967e8UL, Hash64.Fnv1a("foobar"));
        }

        [Test] public void Fnv1a64HashesUtf8Bytes()
        {
            // "赵" is E8 B5 B5 in UTF-8; hashing the three bytes by hand must match.
            ulong h = Hash64.FnvOffset;
            foreach (byte b in new byte[] { 0xE8, 0xB5, 0xB5 }) { h ^= b; h *= Hash64.FnvPrime; }
            Assert.AreEqual(h, Hash64.Fnv1a("赵"));
        }

        [Test] public void SplitMix64FinalizerKnownValue()
        {
            // First SplitMix64 output for seed 0.
            Assert.AreEqual(0xE220A8397B1DCDAFUL, Hash64.SplitMix64(0));
        }

        [Test] public void StreamsAreIndependentPerCharacterAndRepeatable()
        {
            ulong seed = FateStreams.SettleSeed(1234, "tv.N2");
            var a1 = FateStreams.Stream(seed, "draw", "t_loyal");
            var a2 = FateStreams.Stream(seed, "draw", "t_loyal");
            var b = FateStreams.Stream(seed, "draw", "t_proud");
            uint x = a1.Next();
            Assert.AreEqual(x, a2.Next());
            Assert.AreNotEqual(x, b.Next());
            Assert.AreNotEqual(FateStreams.SettleSeed(1234, "tv.N2"), FateStreams.SettleSeed(1234, "tv.N3"));
        }

        [Test] public void SeedTextRoundTrips()
        {
            Assert.AreEqual(0x9E3779B97F4A7C15UL, FateStreams.ParseSeed("0x9E3779B97F4A7C15"));
            Assert.AreEqual("0x9E3779B97F4A7C15", FateStreams.FormatSeed(0x9E3779B97F4A7C15UL));
        }

        [Test] public void ExpTableEndsAndShape()
        {
            var t = ExpTable.Values;
            Assert.AreEqual(4097, t.Length);
            Assert.AreEqual(65536, t[0]);
            Assert.AreEqual(0, t[4096]);                 // exp(-16)·65536 ≈ 0.007
            Assert.That(t[256], Is.InRange(24108, 24110)); // exp(-1)·65536 ≈ 24109.3
            Assert.That(t[512], Is.InRange(8868, 8870));   // exp(-2)·65536 ≈ 8869.3
            for (int i = 1; i < t.Length; i++) Assert.LessOrEqual(t[i], t[i - 1]);
        }

        [Test] public void ExpTableHashIsLocked()
        {
            // V-10 precursor: the table is built with integer arithmetic only, so this hash is the same on every platform.
            Assert.AreEqual(ExpTableHashLock, ExpTable.Hash(), "EXP_TABLE changed; update the lock only on purpose");
        }

        public const ulong ExpTableHashLock = 18332033061095747207UL;

        [Test] public void IsqrtFloors()
        {
            Assert.AreEqual(0, FixedMath.Isqrt(0));
            Assert.AreEqual(1, FixedMath.Isqrt(3));
            Assert.AreEqual(2, FixedMath.Isqrt(4));
            Assert.AreEqual(65536, FixedMath.Isqrt(65536L * 65536L));
            Assert.AreEqual(3037000499, FixedMath.Isqrt(long.MaxValue));
        }

        [Test] public void RoundingIsHalfAwayFromZero()
        {
            Assert.AreEqual(3, FixedMath.RoundToInt(2 * 65536 + 32768));
            Assert.AreEqual(-3, FixedMath.RoundToInt(-(2 * 65536 + 32768)));
            Assert.AreEqual(2, FixedMath.RoundToInt(2 * 65536 + 32767));
            Assert.AreEqual(-27, FixedMath.RoundToInt(-1769472));
        }

        [Test] public void MulDivAndBudgetCeil()
        {
            Assert.AreEqual(32768, FixedMath.Mul(65536, 32768));
            Assert.AreEqual(98304, FixedMath.Div(3, 2));
            Assert.AreEqual(1, FixedMath.CeilMulRatio(4, 9830)); // ceil(0.6)
            Assert.AreEqual(2, FixedMath.CeilMulRatio(7, 9830)); // ceil(1.05)
            Assert.AreEqual(0, FixedMath.CeilMulRatio(0, 9830));
        }

        [Test] public void SoftmaxWeightsUseTheTableWithoutOverflow()
        {
            var w = FateEngine.SoftmaxWeights(new[] { 65536, 65536 }, 6554);
            Assert.AreEqual(65536, w[0]);
            Assert.AreEqual(65536, w[1]);
            // one τ apart → exp(-1)
            w = FateEngine.SoftmaxWeights(new[] { 0, -6554 }, 6554);
            Assert.That(w[1], Is.InRange(24090, 24130));
            // extreme gap clamps to the table end, never overflows
            w = FateEngine.SoftmaxWeights(new[] { int.MaxValue / 2, int.MinValue / 2 }, 6554);
            Assert.AreEqual(65536, w[0]);
            Assert.AreEqual(0, w[1]);
        }
    }
}
