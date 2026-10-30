using System;

namespace ThreeKingdoms.Core.Battle
{
    // Troop classes, ranks and matchups (design/balance/BALANCE_MODEL_v1.md §3.5, §3.14; user 2026-10-27: 8 main + 6 special
    // troop types, no class change, rank-ups within a line at levels 1 / 10 / 20).
    // Matchup = 大兵种压制 +30 (horse → foot, foot → bow, bow → horse; the mind class is outside the triangle) plus each type's
    // specialties, as percent points; five-element +20 is added by the tactics battle; the total is clamped to 70–160.
    // Strategies take no troop terms (T1). The tactics prototype mirrors this table in TroopTraits (checked pair by pair in tests).
    public static class TroopRules
    {
        public const int MinPercent = 70, MaxPercent = 160;

        public static TroopClass ClassOf(TroopType t) => t switch
        {
            TroopType.Cavalry or TroopType.HorseArcher or TroopType.Elephant => TroopClass.Horse,
            TroopType.Archer or TroopType.Crossbow => TroopClass.Bow,
            TroopType.Strategist or TroopType.Dancer or TroopType.Shaman => TroopClass.Mind,
            _ => TroopClass.Foot,
        };

        public static int Rank(int level) => level >= 20 ? 3 : level >= 10 ? 2 : 1;

        // Each rank above the first: troops +10, physical defence +2.
        public static int RankTroops(int rank) => 10 * (Math.Clamp(rank, 1, 3) - 1);
        public static int RankDefense(int rank) => 2 * (Math.Clamp(rank, 1, 3) - 1);

        static readonly string[][] Names =
        {
            new[] { "步兵", "重步兵", "近卫兵" }, new[] { "骑兵", "突骑", "铁骑" }, new[] { "弓兵", "强弓兵", "神射手" },
            new[] { "贼众", "渠帅", "义军" }, new[] { "军师", "谋士", "军师祭酒" }, new[] { "枪兵", "长枪兵", "虎卫" },
            new[] { "弩兵" }, new[] { "弓骑" }, new[] { "剑姬" }, new[] { "舞姬" }, new[] { "巫祝" }, new[] { "水贼" }, new[] { "蛮兵" }, new[] { "象兵" },
        };

        // The rank names exist for the six main lines (§3.14); the others read "<name>·二阶" until named.
        public static string Name(TroopType t, int rank = 1)
        {
            var line = Names[(int)t];
            int i = Math.Clamp(rank, 1, 3) - 1;
            return i < line.Length ? line[i] : line[0] + (i == 1 ? "·二阶" : "·三阶");
        }

        public static int DefaultMove(TroopType t) => t switch
        {
            TroopType.Cavalry or TroopType.HorseArcher => 5, TroopType.Swordswoman or TroopType.Elephant => 4, _ => 3,
        };

        public static int DefaultRange(TroopType t) => t switch
        {
            TroopType.Archer => 3,
            TroopType.Crossbow or TroopType.HorseArcher or TroopType.Strategist or TroopType.Dancer or TroopType.Shaman => 2,
            _ => 1,
        };

        // Physical hits (attacks, techniques, signature skills). melee = adjacent close combat; charged = moved before striking.
        public static int MatchupPoints(TroopType a, TroopType d, bool melee, bool charged = false, int attackerRank = 1)
        {
            var ca = ClassOf(a); var cd = ClassOf(d);
            int p = 0;
            if ((ca == TroopClass.Horse && cd == TroopClass.Foot) || (ca == TroopClass.Foot && cd == TroopClass.Bow) || (ca == TroopClass.Bow && cd == TroopClass.Horse)) p += 30;
            if (a == TroopType.Spearman && cd == TroopClass.Horse) p += 30;                          // 拒马
            if (a == TroopType.Archer && cd == TroopClass.Horse) p += 10;                            // 弓→骑 1.4
            if (a == TroopType.Crossbow && cd == TroopClass.Horse) p += 30;                          // 强弩破骑, 1.6
            if (a == TroopType.Cavalry && charged) p += attackerRank == 1 ? 10 : attackerRank == 2 ? 15 : 20;   // 冲锋
            if (a == TroopType.Elephant && charged) p += 20;                                         // 冲撞
            if (d == TroopType.Spearman && ca == TroopClass.Horse) p -= 40;                          // 拒马: 骑→枪 0.9
            if (melee && ca == TroopClass.Foot && (d == TroopType.Archer || d == TroopType.Crossbow || d == TroopType.HorseArcher || d == TroopType.Strategist)) p += 20;
            if (melee && (d == TroopType.Dancer || d == TroopType.Shaman)) p += 20;
            return p;
        }

        public static int Percent(int points) => Math.Clamp(100 + points, MinPercent, MaxPercent);

        // Round battles: the line-up has no positions, so foot and horse attacks count as close combat and nobody has charged.
        public static int RoundAttackPercent(TroopType a, TroopType d)
        {
            var ca = ClassOf(a);
            return Percent(MatchupPoints(a, d, melee: ca == TroopClass.Foot || ca == TroopClass.Horse));
        }

        // Defend command: 步兵 takes −60% instead of −50%. Returned as the damage share kept (GuardPercent).
        public static int GuardPercent(TroopType t, int baseGuardPercent) => t == TroopType.Infantry ? baseGuardPercent - 10 : baseGuardPercent;
    }
}
