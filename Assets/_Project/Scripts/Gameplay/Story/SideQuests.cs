using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Story;
using static ThreeKingdoms.Gameplay.Story.S;

namespace ThreeKingdoms.Gameplay.Story
{
    // Chapter one's people and side quests, core layer (design/chapter1-rise/npc-sidequests: the bible §8, 10_REVIEW §3–§4;
    // user 2026-10-29 "只做核心层"). Each walkable scene asks here first: who stands where at this step, what a person
    // says, which side quest they start. The lines are the writers' own, keyed dlg.lb1.… (tools/npc/extract_lines.py);
    // memory keys follow 10_REVIEW §4 exactly. A region's quests live in SideQuests<Region>.cs.
    public static class SideQuests
    {
        // A keepsake (信物, bible §6) is a flag plus a line; the keepsake shelf comes with its own system design.
        public const string KeepsakePrefix = "keepsake.";
        public static bool HasKeepsake(ChapterState ch, string id) => ch.Has(KeepsakePrefix + id);
        public static IEnumerable<StageStep> Keepsake(string id)
        {
            yield return Flag(KeepsakePrefix + id);
            yield return L("system", "sq.keepsake." + id);
        }

        // A memory written the 10_REVIEW §4 way (who.key = value); the text key is mem.<who>.<key>.<value>.
        public static StageStep Mem(string who, string key, string value) => Remember(who, key, value, $"mem.{who}.{key}.{value}");

        public static string Recall(FieldStoryController c, string who, string key) => Recall(c.Run.Chapter2, who, key);

        // 10_REVIEW §4 names five main-line memories differently from the 0.2–0.5 build; reads go through here so either
        // spelling counts (the main line keeps writing its own keys, saves need no migration).
        public static string Recall(ChapterState ch, string who, string key)
        {
            string v = ch.Recall(who, key);
            if (v != null) return v;
            switch (who + "." + key)
            {
                case "lu_zhi.cart": return ch.Recall("lu_zhi", "luzhi_cart") switch { "B_escort" => "escort", "A_trust" => "wait", _ => null };
                case "liu_ping_ke.let_go": return ch.Recall("liu_ping", "liuping_assassin") switch { "A_free" => "yes", "B_asked" => "asked", _ => null };
                case "yuan_shu.gongshou": return ch.Recall("yuan_shu", "yuanshu_gongshou") != null ? "yes" : null;
                case "sun_jian.wancheng_met": return ch.Recall("sun_jian", "sunjian_wancheng") != null ? "yes" : null;
                case "lu_bu.hulao_three": return ch.Recall("lu_bu", "lubu_three") != null ? "yes" : null;
                // Not a review key: which way the 鞭督邮 went (the script's flag c12), A Liu Bei, B Zhang Fei.
                case "liu_bei.duyou": return ch.Recall("liu_bei", "liubei_duyou") is string d ? d.Substring(0, 1) : null;
            }
            return null;
        }

        // The realm-map courier at each walkable home (RegionTravel); in the regions he is FieldScriptsRegions'.
        static bool HomeCourier(FieldStoryController c, string id) => id == FieldScriptsRegions.Courier && !FieldScriptsRegions.Mine(c);

        // Null when the person is not a side-quest person here (the scene's own scripts decide).
        public static bool? Present(FieldStoryController c, string id) =>
            HomeCourier(c, id) ? RegionTravel.Allowed(c.Run.Chapter2)
            : SideQuestsEarly.Mine(c) ? SideQuestsEarly.Present(c, id)
            : SideQuestsR5a.Mine(c) ? SideQuestsR5a.Present(c, id)
            : SideQuestsR4.Mine(c) ? SideQuestsR4.Present(c, id)
            : SideQuestsR8.Mine(c) ? SideQuestsR8.Present(c, id)
            : SideQuestsR5b.Mine(c) ? SideQuestsR5b.Present(c, id)
            : SideQuestsR3.Mine(c) ? SideQuestsR3.Present(c, id)
            : SideQuestsR6.Mine(c) ? SideQuestsR6.Present(c, id)
            : SideQuestsR7.Mine(c) ? SideQuestsR7.Present(c, id)
            : SideQuestsR2.Mine(c) ? SideQuestsR2.Present(c, id)
            : SideQuestsR5aWild.Mine(c) ? SideQuestsR5aWild.Present(c, id) : null;

        public static string Prompt(FieldStoryController c, string id) =>
            HomeCourier(c, id) ? "raw:[确认] 与驿卒交谈（天下图）"
            : SideQuestsEarly.Mine(c) ? SideQuestsEarly.Prompt(c, id)
            : SideQuestsR5a.Mine(c) ? SideQuestsR5a.Prompt(c, id)
            : SideQuestsR4.Mine(c) ? SideQuestsR4.Prompt(c, id)
            : SideQuestsR8.Mine(c) ? SideQuestsR8.Prompt(c, id)
            : SideQuestsR5b.Mine(c) ? SideQuestsR5b.Prompt(c, id)
            : SideQuestsR3.Mine(c) ? SideQuestsR3.Prompt(c, id)
            : SideQuestsR6.Mine(c) ? SideQuestsR6.Prompt(c, id)
            : SideQuestsR7.Mine(c) ? SideQuestsR7.Prompt(c, id)
            : SideQuestsR2.Mine(c) ? SideQuestsR2.Prompt(c, id)
            : SideQuestsR5aWild.Mine(c) ? SideQuestsR5aWild.Prompt(c, id) : null;

        public static bool Marked(FieldStoryController c, string id) =>
            SideQuestsEarly.Mine(c) ? SideQuestsEarly.Marked(c, id)
            : SideQuestsR5a.Mine(c) ? SideQuestsR5a.Marked(c, id)
            : SideQuestsR4.Mine(c) ? SideQuestsR4.Marked(c, id)
            : SideQuestsR8.Mine(c) ? SideQuestsR8.Marked(c, id)
            : SideQuestsR5b.Mine(c) ? SideQuestsR5b.Marked(c, id)
            : SideQuestsR3.Mine(c) ? SideQuestsR3.Marked(c, id)
            : SideQuestsR6.Mine(c) ? SideQuestsR6.Marked(c, id)
            : SideQuestsR7.Mine(c) ? SideQuestsR7.Marked(c, id)
            : SideQuestsR2.Mine(c) ? SideQuestsR2.Marked(c, id)
            : SideQuestsR5aWild.Mine(c) && SideQuestsR5aWild.Marked(c, id);

        public static IEnumerable<StageStep> Talk(FieldStoryController c, string id) =>
            HomeCourier(c, id) ? RegionTravel.Courier(c, id)
            : SideQuestsEarly.Mine(c) ? SideQuestsEarly.Talk(c, id)
            : SideQuestsR5a.Mine(c) ? SideQuestsR5a.Talk(c, id)
            : SideQuestsR4.Mine(c) ? SideQuestsR4.Talk(c, id)
            : SideQuestsR8.Mine(c) ? SideQuestsR8.Talk(c, id)
            : SideQuestsR5b.Mine(c) ? SideQuestsR5b.Talk(c, id)
            : SideQuestsR3.Mine(c) ? SideQuestsR3.Talk(c, id)
            : SideQuestsR6.Mine(c) ? SideQuestsR6.Talk(c, id)
            : SideQuestsR7.Mine(c) ? SideQuestsR7.Talk(c, id)
            : SideQuestsR2.Mine(c) ? SideQuestsR2.Talk(c, id)
            : SideQuestsR5aWild.Mine(c) ? SideQuestsR5aWild.Talk(c, id) : null;

        // Played after the scene's own arrival steps.
        public static IEnumerable<StageStep> OnArrive(FieldStoryController c) =>
            SideQuestsR4.Mine(c) ? SideQuestsR4.OnArrive(c) : Enumerable.Empty<StageStep>();

        // Lines a story stage adds for side quests (the stage id and the run's state decide).
        public static IEnumerable<StageStep> StageExtras(string stage, ChapterState ch) => stage switch
        {
            "after_bianshui" => SideQuestsR5a.AfterBianshui(ch),
            _ => Enumerable.Empty<StageStep>(),
        };
    }
}
