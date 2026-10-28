using System.Text;

namespace ThreeKingdoms.Core.Fate
{
    /// <summary>FNV-1a 64 over UTF-8 bytes and the SplitMix64 finalizer (spec §5.3).</summary>
    public static class Hash64
    {
        public const ulong FnvOffset = 14695981039346656037UL;
        public const ulong FnvPrime = 1099511628211UL;

        public static ulong Fnv1a(string s)
        {
            ulong h = FnvOffset;
            if (string.IsNullOrEmpty(s)) return h;
            foreach (byte b in Encoding.UTF8.GetBytes(s))
            {
                h ^= b;
                h *= FnvPrime;
            }
            return h;
        }

        public static ulong SplitMix64(ulong x)
        {
            ulong z = x + 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    /// <summary>PCG32 (XSH-RR) with the standard multiplier and pcg32_srandom seeding.</summary>
    public sealed class Pcg32
    {
        const ulong Multiplier = 6364136223846793005UL;
        ulong state;
        readonly ulong inc;

        public Pcg32(ulong initState, ulong initSeq)
        {
            state = 0;
            inc = (initSeq << 1) | 1UL;
            Next();
            state += initState;
            Next();
        }

        public uint Next()
        {
            ulong old = state;
            state = unchecked(old * Multiplier + inc);
            uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
        }

        /// <summary>Uniform value in [0, bound) by modulo (spec: r = draw.Next() % Σw). bound must be > 0.</summary>
        public long NextBelow(long bound) => Next() % bound;
    }

    /// <summary>Per-settle random streams (spec §5.3 stream table).</summary>
    public static class FateStreams
    {
        public static ulong ParseSeed(string seed)
        {
            if (string.IsNullOrEmpty(seed)) return 0;
            if (seed.StartsWith("0x") || seed.StartsWith("0X"))
                return ulong.Parse(seed.Substring(2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
            return ulong.Parse(seed, System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string FormatSeed(ulong seed) => "0x" + seed.ToString("X16", System.Globalization.CultureInfo.InvariantCulture);

        public static ulong SettleSeed(ulong fateSeed, string nodeId) => Hash64.SplitMix64(fateSeed ^ Hash64.Fnv1a(nodeId));

        public static Pcg32 Stream(ulong settleSeed, string streamName, string characterId)
        {
            return new Pcg32(Hash64.SplitMix64(settleSeed ^ Hash64.Fnv1a(streamName)), Hash64.Fnv1a(characterId));
        }
    }
}
