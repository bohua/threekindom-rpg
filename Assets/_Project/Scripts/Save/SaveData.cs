using System;

namespace ThreeKingdoms.Save
{
    // One save file (ADR-0004), written as JSON. Plain fields only so JsonUtility can read and write it; every field
    // V1_SPEC §9 lists, nothing about accounts or licences.
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentFormat = 1;
        public const string TownLocation = "town";
        // A post station in an open region (chapter 1 art probe): "region:R4".
        public const string RegionPrefix = "region:";

        public int formatVersion = CurrentFormat;
        public string gameVersion = "";
        public string savedAt = "";           // ISO 8601, local time
        public string chapter = "";
        // Safe stage (save-system GDD answer 6): "town" (the tavern), a battle id = that battle's pre-battle state, or
        // RegionPrefix + region id = that region's post station.
        public string location = TownLocation;

        // Story flags (story-events GDD)
        public bool guanYuJoined;
        public bool zhangFeiJoined;
        public int oath;
        public int battlesDone;
        public int ending;

        // Progression
        public ExpEntry[] exp = Array.Empty<ExpEntry>();

        // Equipment & items
        public int copper;
        public int rations;
        public BagEntry[] bag = Array.Empty<BagEntry>();
        // Town life (user 2026-10-22); older saves read 0.
        public int reputation;
        public int quizTakenAt = -1;
        public int archeryRewardedAt = -1;
        public WornEntry[] worn = Array.Empty<WornEntry>();
        // Chronicle (chapter 1 year nodes, ADR-0004 amendment 2026-10-24); older saves read "" and start at the first node.
        public string chronicleNode = "";
        public string[] openRegions = Array.Empty<string>();
        public string[] dangerAreas = Array.Empty<string>();

        // Slot summary only (answer 4: chapter, place, Liu Bei's level, copper, time); not read back into the game.
        public int leaderLevel;
    }

    [Serializable] public struct ExpEntry { public string general; public int exp; }
    [Serializable] public struct BagEntry { public string item; public int count; }
    [Serializable] public struct WornEntry { public string general; public string item; }
}
