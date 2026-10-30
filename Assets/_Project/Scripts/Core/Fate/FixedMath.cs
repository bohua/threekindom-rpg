using System;

namespace ThreeKingdoms.Core.Fate
{
    /// <summary>Q16.16 fixed-point helpers (spec v5.1 §5.3). All products and quotients use long intermediates.</summary>
    public static class FixedMath
    {
        public const int One = 65536;
        public const int Half = 32768;

        public static long FromInt(long v) => v << 16;

        /// <summary>(a*b)>>16, arithmetic shift = floor.</summary>
        public static long Mul(long a, long b) => (a * b) >> 16;

        /// <summary>(a<<16)/b, truncating toward zero like C# integer division. b must not be 0.</summary>
        public static long Div(long a, long b) => (a << 16) / b;

        /// <summary>Round a Q16.16 value to an integer, halves away from zero (spec §5.1 final deltas).</summary>
        public static int RoundToInt(long q)
        {
            return q >= 0 ? (int)((q + Half) >> 16) : -(int)((-q + Half) >> 16);
        }

        public static long Clamp(long v, long min, long max) => v < min ? min : (v > max ? max : v);
        public static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);

        /// <summary>Integer square root, floor(sqrt(n)) for n >= 0.</summary>
        public static long Isqrt(long n)
        {
            if (n < 0) throw new ArgumentOutOfRangeException(nameof(n));
            if (n < 2) return n;
            ulong u = (ulong)n;
            ulong x = u;
            ulong y = (x + 1) >> 1;
            while (y < x)
            {
                x = y;
                y = (x + u / x) >> 1;
            }
            return (long)x;
        }

        /// <summary>ceil(count × ratioQ16) as an integer.</summary>
        public static int CeilMulRatio(int count, int ratioQ16) => (int)(((long)count * ratioQ16 + (One - 1)) >> 16);
    }

    /// <summary>
    /// exp(-x) for x in [0,16] in steps of 1/256, Q16.16, 4097 entries (spec §5.3).
    /// Built with integer arithmetic only (Q0.32 Taylor constant + repeated products) so every platform builds the same table.
    /// </summary>
    public static class ExpTable
    {
        public const int Size = 4097;
        public const int StepsPerUnit = 256;
        static int[] table;

        public static int[] Values
        {
            get
            {
                if (table == null) table = Build();
                return table;
            }
        }

        public static int Get(int index) => Values[index];

        static int[] Build()
        {
            // c = exp(-1/256) in Q0.32 via Taylor series: sum (-x)^n / n!, x = 2^24 (1/256 in Q0.32)
            const ulong scale = 1UL << 32;
            ulong x = 1UL << 24;
            ulong term = scale;
            long c = (long)scale;
            for (int n = 1; n < 12; n++)
            {
                term = (term * x >> 32) / (ulong)n;
                if ((n & 1) == 1) c -= (long)term; else c += (long)term;
            }
            var result = new int[Size];
            ulong acc = scale; // exp(0) in Q0.32
            for (int i = 0; i < Size; i++)
            {
                result[i] = (int)((acc + (1UL << 15)) >> 16);
                acc = (acc * (ulong)c) >> 32;
            }
            result[0] = FixedMath.One;
            return result;
        }

        /// <summary>FNV-1a 64 over the table's little-endian bytes; used by tests (and V-10 later) to lock the table.</summary>
        public static ulong Hash()
        {
            ulong h = Hash64.FnvOffset;
            foreach (int v in Values)
            {
                for (int b = 0; b < 4; b++)
                {
                    h ^= (byte)(v >> (8 * b));
                    h *= Hash64.FnvPrime;
                }
            }
            return h;
        }
    }
}
