using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Story;
using UnityEngine;
using static ThreeKingdoms.Gameplay.Story.S;

namespace ThreeKingdoms.Gameplay.Story
{
    // Travel between chapter one's open regions (coordinator 2026-10-30: the regions open by year after the chapter
    // script v1.1, Chapter1Chronicle; nobody else builds the travel). The main line always has a home: the walkable
    // scene of its current step (Pingyuan in 191–193, Xiapi in 194 …). From home or from any open region, a post-station
    // courier opens the realm map's list of the regions open this year; going back home returns to the step's scene.
    // Steps without a walkable home (story stages, battles) have no travel. The flight over the 3D 天下图 (PR #37,
    // TianxiaFlight) is to play between the two scenes once that branch is merged; until then a fade.
    public static class RegionTravel
    {
        public sealed class Region
        {
            public readonly string Id, Name, Scene;
            public Region(string id, string name, string scene) { Id = id; Name = name; Scene = scene; }
        }

        public static readonly IReadOnlyList<Region> Regions = new[]
        {
            new Region(Chapter1Chronicle.R1, "涿蓟", "Region_R1"),
            new Region(Chapter1Chronicle.R2, "中山常山", "Zhongshan_Region"),
            new Region(Chapter1Chronicle.R3, "巨鹿魏郡", "Region_R3"),
            new Region(Chapter1Chronicle.R4, "渤海平原", GameRun.BohaiScene),
            new Region(Chapter1Chronicle.R5a, "酸枣虎牢", "Region_R5A"),
            new Region(Chapter1Chronicle.R5b, "洛阳", "Region_R5B"),
            new Region(Chapter1Chronicle.R6, "兖州", "Region_R6"),
            new Region(Chapter1Chronicle.R7, "青州", "Region_R7"),
            new Region(Chapter1Chronicle.R8, "徐州", "Region_R8"),
        };

        // Every region scene has this spot at its post station (ChapterFieldBuilderRegions).
        public const string PostArrival = "Arrival (post station)";

        public static Region ById(string id) => Regions.FirstOrDefault(r => r.Id == id);
        public static Region ByScene(string scene) => Regions.FirstOrDefault(r => r.Scene == scene);

        // The regions open at the run's year node (the node table's opens and closes up to it).
        public static List<string> Open(ChapterState ch)
        {
            var open = new HashSet<string>();
            int upto = (int)ch.Node - 1;
            for (int i = 0; i <= upto && i < Chapter1Chronicle.Nodes.Count; i++)
            {
                var n = Chapter1Chronicle.Nodes[i];
                foreach (var id in n.Opens) open.Add(id);
                foreach (var id in n.Closes) open.Remove(id);
            }
            // The 184 Guangzong corner of R3 is the camp's own map; R3 as a travel target opens with the rest in 191.
            open.Remove(Chapter1Chronicle.R3Guangzong);
            open.Remove(Chapter1Chronicle.Yingchuan);
            return Regions.Where(r => open.Contains(r.Id)).Select(r => r.Id).ToList();
        }

        public static bool Danger(ChapterState ch, string id)
        {
            for (int i = 0; i < (int)ch.Node && i < Chapter1Chronicle.Nodes.Count; i++)
                if (Chapter1Chronicle.Nodes[i].Danger.Contains(id)) return true;
            return false;
        }

        // The main line's walkable scene at this step, or null (no travel from a story stage).
        public static string Home(ChapterState ch) => ch.Step switch
        {
            ChapterStep.Gongsun => GameRun.GongsunScene,
            ChapterStep.Suanzao => GameRun.SuanzaoScene,
            ChapterStep.Pingyuan or ChapterStep.PingyuanSafe or ChapterStep.Zhending or ChapterStep.Beihai => GameRun.BohaiScene,
            ChapterStep.Xiapi or ChapterStep.Echo => GameRun.XiapiScene,
            _ => null,
        };

        // Travel opens from 190 on (before that the chapter is one road: the camp, Anxi); the first walk of a new
        // place (its greeting) must be over, so a step's arrival scene always plays at home.
        public static bool Allowed(ChapterState ch) => Home(ch) != null && ch.Node >= ChronicleNode.N4 && Greeted(ch);

        static bool Greeted(ChapterState ch) => ch.Step switch
        {
            ChapterStep.Gongsun => ch.Has(ChapterScripts3.GongsunGreeted),
            ChapterStep.Suanzao => ch.Has(ChapterScripts3.Wenjiu),
            ChapterStep.Pingyuan => ch.Has(ChapterScripts4.PingyuanGreeted),
            ChapterStep.Beihai => ch.Has(ChapterScripts5.TaishiArrived),
            ChapterStep.Xiapi => ch.Has(ChapterScripts5.XiapiGreeted),
            _ => true,
        };

        // The courier's choice: home first (when away), then the other open regions whose scene is in this build.
        public static List<(string label, StageStep[] go)> Targets(ChapterState ch)
        {
            string here = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, home = Home(ch);
            var targets = new List<(string label, StageStep[] go)>();
            if (here != home) targets.Add(("raw:回" + HomeName(ch), GoHome(home)));
            foreach (var id in Open(ch))
            {
                var r = ById(id);
                if (r.Scene == here || r.Scene == home || !Application.CanStreamedLevelBeLoaded(r.Scene)) continue;
                targets.Add(("raw:前往" + r.Name + (Danger(ch, id) ? "（危险区域）" : ""), Go(r)));
            }
            return targets;
        }

        public static IEnumerable<StageStep> Courier(FieldStoryController c, string courier)
        {
            var ch = c.Run.Chapter2;
            if (!Allowed(ch)) { yield return L(courier, "sq.travel.closed"); yield break; }
            var targets = Targets(ch);
            if (targets.Count == 0) { yield return L(courier, "sq.travel.none"); yield break; }
            targets.Add(("gs.opt.cancel", new StageStep[0]));
            yield return L(courier, "sq.travel.ask");
            yield return Choice("system", "sq.travel.prompt", "sq_travel_", targets.Select(t => t.label).ToArray(), targets.Select(t => t.go).ToArray());
        }

        static string HomeName(ChapterState ch) => Home(ch) switch
        {
            GameRun.GongsunScene => "公孙瓒营", GameRun.SuanzaoScene => "酸枣大营", GameRun.BohaiScene => "平原", GameRun.XiapiScene => "下邳", _ => "主线所在地",
        };

        static StageStep[] Go(Region r) => new[]
        {
            Call(() => RegionPortal.SetArrival(PostArrival, Vector3.forward)),
            Fade(1f), Load(r.Scene),
        };

        static StageStep[] GoHome(string home) => new[]
        {
            Call(() => { if (GameRun.Current != null) GameRun.Current.NextArrival = GameRun.Arrival.ReturnFromBattle; }),
            Fade(1f), Load(home),
        };
    }
}
