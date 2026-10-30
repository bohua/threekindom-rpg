using System;
using System.Collections.Generic;

namespace ThreeKingdoms.Core.Fate
{
    /// <summary>Tuning knobs (spec §11) and attitude tables (spec §5.1). Weights are Q16.16 integers.</summary>
    [Serializable]
    public class TuningTable
    {
        public int fateDataVersion = 1;
        public int wP = 16384, wA = 13107, wR = 13107, wI = 22938;   // 0.25 / 0.20 / 0.20 / 0.35
        public int tau = 6554;                                       // 0.10
        public int hT1 = 22938, hT2 = 16384, hT3 = 0, hT1S = 0;      // 0.35 / 0.25 / 0 / 0
        public int eps = 3277;                                       // 0.05
        public int deviationBudgetRatio = 9830;                      // 0.15
        public int randomRateT1 = 0, randomRateT2 = 1966, randomRateT3 = 5243; // 0 / 0.03 / 0.08
        public int relationCauseThreshold = 40;
        public int memoryCap = 40;
        public int favorBandClose = 40, favorBandDistant = -20;
        public int favoredBookMultiplier = 98304;                    // ×1.5

        // personality column order: loyalty, righteousness, benevolence, courage, cunning, fame, profit, pride
        public SortedDictionary<string, int[]> react = new SortedDictionary<string, int[]>(StringComparer.Ordinal);
        // view column order: benevolence, valor, wisdom, faith, authority, deceit
        public SortedDictionary<string, int[]> tagToView = new SortedDictionary<string, int[]>(StringComparer.Ordinal);
        public SortedDictionary<string, int> trustDelta = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public int HFor(string tier)
        {
            switch (tier)
            {
                case Tiers.T1: return hT1;
                case Tiers.T2: return hT2;
                case Tiers.T1S: return hT1S;
                default: return hT3;
            }
        }

        public int RandomRateFor(string tier)
        {
            switch (tier)
            {
                case Tiers.T1: return randomRateT1;
                case Tiers.T2: return randomRateT2;
                case Tiers.T3: return randomRateT3;
                default: return 0;
            }
        }

        public string FavorBand(int favor) => favor >= favorBandClose ? "close" : (favor < favorBandDistant ? "distant" : "neutral");

        public TuningTable Clone()
        {
            var t = (TuningTable)MemberwiseClone();
            t.react = new SortedDictionary<string, int[]>(react, StringComparer.Ordinal);
            t.tagToView = new SortedDictionary<string, int[]>(tagToView, StringComparer.Ordinal);
            t.trustDelta = new SortedDictionary<string, int>(trustDelta, StringComparer.Ordinal);
            return t;
        }

        /// <summary>Spec v5.1 §5.1 / §11 initial values.</summary>
        public static TuningTable CreateDefault()
        {
            var t = new TuningTable();
            void R(string tag, params int[] v) => t.react[tag] = v;
            R("benevolence", 0, 3, 8, 0, 0, 0, -2, 0);
            R("valor", 0, 2, 0, 8, -1, 2, 0, 1);
            R("wisdom", 0, 0, 0, -1, 8, 1, 0, 1);
            R("keep_promise", 4, 8, 2, 0, 0, 0, 0, 0);
            R("break_promise", -6, -10, -3, 0, 0, 0, 0, 0);
            R("authority", 3, 0, -2, 2, 0, 2, 0, -2);
            R("deceit", -3, -8, -4, 0, 3, -2, 2, -1);
            R("courtesy", 2, 3, 2, 0, 2, 3, 0, 6);
            R("insult", -2, -3, -1, 1, -2, -4, 0, -10);
            R("gift", 0, 1, 0, 0, 0, 2, 6, 0);
            R("gift_book", 0, 1, 0, 0, 5, 2, 1, 2);
            R("gift_treasure", 0, 1, 0, 2, 0, 4, 5, 3);
            R("fought_alongside", 4, 6, 0, 4, 0, 1, 0, 0);
            R("fought_against", -2, -2, 0, 2, 0, 0, 0, 1);
            R("spared", 0, 6, 5, 0, 0, 0, 0, -3);
            R("captured", -2, -3, 0, 0, 0, 0, 0, -6);

            void V(string tag, params int[] v) => t.tagToView[tag] = v;
            V("benevolence", 6, 0, 0, 1, 0, 0);
            V("valor", 0, 6, 0, 0, 2, 0);
            V("wisdom", 0, 0, 6, 0, 0, 1);
            V("keep_promise", 0, 0, 0, 6, 0, 0);
            V("break_promise", 0, 0, 0, -10, 0, 4);
            V("authority", 0, 1, 0, 0, 6, 0);
            V("deceit", 0, 0, 2, -4, 0, 8);
            V("courtesy", 0, 0, 0, 1, 0, 0);
            V("insult", 0, 0, 0, -1, 0, 0);
            V("gift", 1, 0, 0, 0, 0, 0);
            V("gift_book", 1, 0, 0, 0, 0, 0);
            V("gift_treasure", 1, 0, 0, 0, 0, 0);
            V("fought_alongside", 0, 2, 0, 2, 0, 0);
            V("fought_against", 0, 1, 0, 0, 0, 0);
            V("captured", 0, 1, 0, 0, 2, 0);
            V("spared", 0, 0, 0, 2, 0, 0);

            t.trustDelta["keep_promise"] = 6;
            t.trustDelta["fought_alongside"] = 4;
            t.trustDelta["spared"] = 3;
            t.trustDelta["courtesy"] = 1;
            t.trustDelta["gift"] = 1;
            t.trustDelta["gift_book"] = 1;
            t.trustDelta["gift_treasure"] = 1;
            t.trustDelta["break_promise"] = -10;
            t.trustDelta["deceit"] = -6;
            t.trustDelta["insult"] = -2;
            t.trustDelta["captured"] = -3;
            return t;
        }
    }
}
