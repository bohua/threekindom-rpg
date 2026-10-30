using System.Collections.Generic;
using ThreeKingdoms.Core.Story;
using static ThreeKingdoms.Gameplay.Story.S;

namespace ThreeKingdoms.Gameplay.Story
{
    // The story layer over chapter one's open regions R2–R8 when the player travels there (RegionTravel): a post-station
    // courier for the realm map, the arrival autosave, and the regions' side quests (SideQuests, phase B of the NPC
    // implementation plan). R4 is the Pingyuan base itself (FieldScripts4); R1 has its own layer (FieldScriptsR1).
    public static class FieldScriptsRegions
    {
        public const string Courier = "post_courier";

        public static bool IsRegion(FieldStoryController.Place p) => p >= FieldStoryController.Place.R2;
        public static bool Mine(FieldStoryController c) => IsRegion(c.Location);

        public static string RegionId(FieldStoryController.Place p) => p switch
        {
            FieldStoryController.Place.R2 => Chapter1Chronicle.R2, FieldStoryController.Place.R3 => Chapter1Chronicle.R3,
            FieldStoryController.Place.R5a => Chapter1Chronicle.R5a, FieldStoryController.Place.R5b => Chapter1Chronicle.R5b,
            FieldStoryController.Place.R6 => Chapter1Chronicle.R6, FieldStoryController.Place.R7 => Chapter1Chronicle.R7,
            FieldStoryController.Place.R8 => Chapter1Chronicle.R8, _ => null,
        };

        static string Name(FieldStoryController c) => RegionTravel.ById(RegionId(c.Location))?.Name ?? "";

        public static IEnumerable<StageStep> OnArrive(FieldStoryController c)
        {
            yield return Autosave(c.SaveLocation);
            string first = "sq.travel.seen." + RegionId(c.Location);
            if (!c.Run.Chapter2.Has(first))
            {
                yield return Flag(first);
                yield return L("system", "raw:" + Name(c) + "　" + c.Run.Chapter2.Year + "年");
            }
        }

        public static string Prompt(FieldStoryController c, string id) =>
            id == Courier ? "raw:[确认] 与驿卒交谈（天下图·存档）" : "talk:" + id;

        public static bool Marked(FieldStoryController c, string id) => false;

        public static string Objective(FieldStoryController c) =>
            $"目标：在{Name(c)}四处走走；到驿站找驿卒，可经天下图回主线所在地或前往别处";

        public static IEnumerable<StageStep> Talk(FieldStoryController c, string id)
        {
            if (id != Courier) return null;
            return CourierTalk(c);
        }

        static IEnumerable<StageStep> CourierTalk(FieldStoryController c)
        {
            yield return Choice(Courier, "sq.travel.post", "sq_post_", new[] { "sq.travel.opt.map", "gs.opt.save", "gs.opt.cancel" },
                System.Linq.Enumerable.ToArray(RegionTravel.Courier(c, Courier)),
                new[] { Call(() => c.pendingPanel = TownStoryController.SavePanel) },
                new StageStep[0]);
        }
    }
}
