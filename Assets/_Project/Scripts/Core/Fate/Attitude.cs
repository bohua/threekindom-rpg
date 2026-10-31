using System;
using System.Collections.Generic;

namespace ThreeKingdoms.Core.Fate
{
    /// <summary>Change to one character's attitude toward the protagonist (spec §5.1). Integers after rounding.</summary>
    public sealed class AttitudeDelta
    {
        public int Favor, Trust;
        public readonly int[] Views = new int[6];
        /// <summary>Unrounded Δfavor in Q16.16, for tests and the fate panel.</summary>
        public long FavorQ16;
    }

    public static class Attitude
    {
        public const int FavorMin = -100, FavorMax = 100, TrustMin = 0, TrustMax = 100, ViewMin = 0, ViewMax = 100;

        /// <summary>react(t) in Q16.16: Σ_k (personality_k − 50)/50 × REACT[t][k], one division at the end.</summary>
        public static long React(Personality p, string tag, TuningTable t, bool treasureAffinity = true)
        {
            if (p == null || !t.react.TryGetValue(tag, out var row)) return 0;
            var pv = p.ToArray();
            long sum = 0;
            for (int k = 0; k < 8 && k < row.Length; k++)
            {
                int coeff = row[k];
                if (k == 7 && tag == "gift_treasure" && !treasureAffinity) coeff = 0; // spec §4.10: pride bonus only with affinity
                sum += (long)(pv[k] - 50) * coeff;
            }
            return sum * FixedMath.One / 50;
        }

        public static AttitudeDelta Compute(CharacterProfile profile, MemoryEntry m, TuningTable t)
        {
            var d = new AttitudeDelta();
            if (m == null) return d;
            int intensity = m.intensity;
            long favorQ = 0;
            long trust = 0;
            bool favoredBook = false;
            bool affinity = m.Param("affinity") == "1";
            foreach (var tag in m.tags ?? Array.Empty<string>())
            {
                favorQ += intensity * React(profile?.personality, tag, t, affinity);
                if (t.trustDelta.TryGetValue(tag, out var td)) trust += (long)td * intensity;
                if (t.tagToView.TryGetValue(tag, out var vrow))
                    for (int i = 0; i < 6 && i < vrow.Length; i++) d.Views[i] += vrow[i] * intensity;
                if (tag == "gift_book" && profile?.favoredBooks != null && Array.IndexOf(profile.favoredBooks, m.Param("itemId")) >= 0)
                    favoredBook = true;
            }
            favorQ += FixedMath.FromInt((long)m.valence * intensity * 2);
            if (favoredBook) favorQ = FixedMath.Mul(favorQ, t.favoredBookMultiplier);
            d.FavorQ16 = favorQ;
            d.Favor = FixedMath.RoundToInt(favorQ);
            d.Trust = (int)trust;
            return d;
        }

        /// <summary>
        /// Attitude change for one PLAYER memory. Pure: returns the delta and leaves the inputs alone.
        /// </summary>
        public static AttitudeDelta ApplyInteraction(CharacterProfile profile, Relation current, MemoryEntry m, TuningTable t) => Compute(profile, m, t);

        /// <summary>Apply a delta to a relation with the spec's clamps (favor −100..100, trust/views 0..100).</summary>
        public static void Apply(Relation r, AttitudeDelta d)
        {
            r.favor = FixedMath.Clamp(r.favor + d.Favor, FavorMin, FavorMax);
            r.trust = FixedMath.Clamp(r.trust + d.Trust, TrustMin, TrustMax);
            if (r.views == null) r.views = new int[6];
            for (int i = 0; i < 6; i++) r.views[i] = FixedMath.Clamp(r.views[i] + d.Views[i], ViewMin, ViewMax);
        }
    }

    /// <summary>Memory storage rules (spec §4.6, §5.6): who+key overwrite, seq order, compression.</summary>
    public static class MemoryStore
    {
        public static MemoryEntry Find(CharacterState c, string key)
        {
            if (c == null || key == null) return null;
            foreach (var m in c.memories) if (m.key == key) return m;
            return null;
        }

        /// <summary>Insert or overwrite by key (the 0.2 semantics). Returns the stored entry.</summary>
        public static MemoryEntry Put(CharacterState c, MemoryEntry m)
        {
            for (int i = 0; i < c.memories.Count; i++)
            {
                if (c.memories[i].key == m.key)
                {
                    c.memories[i] = m;
                    return m;
                }
            }
            c.memories.Add(m);
            return m;
        }

        /// <summary>
        /// Compress per §5.6: when uncompressed entries below importance 5 exceed the cap, the oldest (by seq)
        /// importance ≤3 entries — and importance 4 entries at least 3 stages old — are compressed until the cap holds.
        /// Compressed entries keep key/value (memory() still answers) and lose their text; their tags feed the stage summary.
        /// </summary>
        public static int Compress(CharacterState c, int cap, Func<string, int> stageIndex, int currentStageIndex)
        {
            int live = 0;
            foreach (var m in c.memories) if (!m.compressed && m.importance < 5) live++;
            if (live <= cap) return 0;
            var candidates = new List<MemoryEntry>();
            foreach (var m in c.memories)
            {
                if (m.compressed || m.importance >= 5) continue;
                if (m.importance == 4 && currentStageIndex - stageIndex(m.node) < 3) continue;
                candidates.Add(m);
            }
            candidates.Sort((a, b) => a.seq.CompareTo(b.seq));
            int compressedNow = 0;
            var touchedStages = new SortedDictionary<string, bool>(StringComparer.Ordinal);
            foreach (var m in candidates)
            {
                if (live <= cap) break;
                m.compressed = true;
                m.text = null;
                live--;
                compressedNow++;
                if (m.node != null) touchedStages[m.node] = true;
            }
            foreach (var stage in touchedStages.Keys) c.stageSummaries[stage] = BuildSummary(c, stage);
            return compressedNow;
        }

        /// <summary>sum.&lt;tier&gt;.&lt;tagA&gt;.&lt;tagB&gt;.&lt;tagC&gt; from the most frequent tags among the stage's compressed memories.</summary>
        public static StageSummary BuildSummary(CharacterState c, string stage)
        {
            var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (var m in c.memories)
            {
                if (!m.compressed || m.node != stage) continue;
                foreach (var t in m.tags ?? Array.Empty<string>()) counts[t] = (counts.TryGetValue(t, out var n) ? n : 0) + 1;
            }
            var list = new List<KeyValuePair<string, int>>(counts);
            list.Sort((a, b) => a.Value != b.Value ? b.Value.CompareTo(a.Value) : string.CompareOrdinal(a.Key, b.Key));
            var key = "sum." + (c.tier ?? "T3");
            for (int i = 0; i < 3; i++) key += "." + (i < list.Count ? list[i].Key : "none");
            return new StageSummary { textKey = key };
        }
    }
}
