using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Economy;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Core.Town;

namespace ThreeKingdoms.Save
{
    // Converts the play-through's Core state to and from a SaveData (ADR-0004). The save system only copies state;
    // each owner (StoryState, Inventory, progression) rebuilds itself through its own Restore and validates there.
    public static class SaveCodec
    {
        public static SaveData Capture(StoryState story, Inventory inventory, IReadOnlyDictionary<string, int> exp,
            string location, int leaderLevel, string chapter, string gameVersion, DateTime now, TownLife life = null,
            ChronicleState chronicle = null)
        {
            story.ToSafeState();   // never save the middle of a sequence (rule 5)
            return new SaveData
            {
                gameVersion = gameVersion,
                savedAt = now.ToString("yyyy-MM-ddTHH:mm:ss"),
                chapter = chapter,
                location = location,
                guanYuJoined = story.GuanYuJoined,
                zhangFeiJoined = story.ZhangFeiJoined,
                oath = (int)story.Oath,
                battlesDone = story.BattlesDone,
                ending = (int)story.Ending,
                exp = exp.Select(kv => new ExpEntry { general = kv.Key, exp = kv.Value }).ToArray(),
                copper = inventory.Copper,
                rations = inventory.Rations,
                bag = inventory.Bag.Select(b => new BagEntry { item = b.item.Id, count = b.count }).ToArray(),
                worn = inventory.WornItems.Select(w => new WornEntry { general = w.generalId, item = w.item.Id }).ToArray(),
                leaderLevel = leaderLevel,
                reputation = life?.Reputation ?? 0,
                quizTakenAt = life?.QuizTakenAt ?? -1,
                archeryRewardedAt = life?.ArcheryRewardedAt ?? -1,
                chronicleNode = chronicle?.NodeId ?? "",
                openRegions = chronicle?.OpenRegions.OrderBy(r => r).ToArray() ?? Array.Empty<string>(),
                dangerAreas = chronicle?.DangerAreas.OrderBy(d => d).ToArray() ?? Array.Empty<string>(),
            };
        }

        public sealed class Restored
        {
            public StoryState Story;
            public Inventory Inventory;
            public IReadOnlyList<(string general, int exp)> Exp;
            public string Location;
            public TownLife Life;
            public ChronicleState Chronicle;
        }

        // False (with a reason) for anything the current game cannot use; the file itself is never touched.
        public static bool TryRestore(SaveData data, EconomyData economy, out Restored restored, out string error)
        {
            restored = null; error = null;
            try
            {
                bool inRegion = data.location.StartsWith(SaveData.RegionPrefix);
                if (data.location != SaveData.TownLocation && !data.location.StartsWith("battle_") && !inRegion)
                    throw new ArgumentException($"未知落点 {data.location}");
                var story = StoryState.Restore(data.guanYuJoined, data.zhangFeiJoined, (SequenceState)data.oath, data.battlesDone, (SequenceState)data.ending);
                var chronicle = ChronicleState.Restore(data.chronicleNode, data.openRegions, data.dangerAreas);
                // A post-station save must be in a region the chronicle has open.
                if (inRegion && !chronicle.IsOpen(data.location.Substring(SaveData.RegionPrefix.Length)))
                    throw new ArgumentException($"区域落点 {data.location} 尚未开放");
                // A pre-battle save must be for the battle the story expects next (no Xuzhou or skipped battles, rule 8).
                if (data.location != SaveData.TownLocation && !inRegion && data.location != story.NextBattleId)
                    throw new ArgumentException($"战前落点 {data.location} 与进度不符");
                var inventory = Inventory.Restore(economy, data.copper, data.rations,
                    (data.bag ?? Array.Empty<BagEntry>()).Select(b => (b.item, b.count)),
                    (data.worn ?? Array.Empty<WornEntry>()).Select(w => (w.general, w.item)));
                restored = new Restored
                {
                    Story = story,
                    Inventory = inventory,
                    Exp = (data.exp ?? Array.Empty<ExpEntry>()).Select(e => (e.general, e.exp)).ToList(),
                    Location = data.location,
                    Life = new TownLife(data.reputation, data.quizTakenAt, data.archeryRewardedAt),
                    Chronicle = chronicle,
                };
                return true;
            }
            catch (Exception e) when (e is ArgumentException || e is KeyNotFoundException || e is InvalidCastException || e is NullReferenceException)
            {
                error = e.Message;
                return false;
            }
        }
    }
}
