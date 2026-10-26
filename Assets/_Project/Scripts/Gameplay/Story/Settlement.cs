using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Story;

namespace ThreeKingdoms.Gameplay.Story
{
    // The chapter-one settlement (SYSTEMS_V05 §5, REVIEW_V05 R16): read from the play-through, written into nothing.
    // The trail follows the chapter's steps; the ladder of names is the one the chapter climbed.
    public static class Settlement
    {
        static GameRun Run => GameRun.Current;
        static ChapterState Ch => Run?.Chapter2;

        // City nodes Liu Bei passed, in order (the realm-map trail; optional places only when their flags say so).
        public static IReadOnlyList<string> TrailNodes()
        {
            var l = new List<string> { "zhuojun", "guangzong", "yingchuan", "guangzong" };
            if (Ch != null && Ch.Has(ChapterScripts.DetourChoice + "0")) l.Add("wancheng");
            l.AddRange(new[] { "anxi", "gaotang", "ji", "suanzao", "hulao", "luoyang", "ji", "pingyuan", "jieqiao", "zhending", "pingyuan", "duchang", "pingyuan", "xuzhou" });
            return l;
        }

        static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            ["zhuojun"] = "涿郡", ["guangzong"] = "广宗", ["yingchuan"] = "颍川", ["wancheng"] = "宛城", ["anxi"] = "安喜", ["gaotang"] = "高唐",
            ["ji"] = "蓟", ["suanzao"] = "酸枣", ["hulao"] = "虎牢关", ["luoyang"] = "洛阳", ["pingyuan"] = "平原", ["jieqiao"] = "界桥",
            ["zhending"] = "真定", ["duchang"] = "都昌", ["xuzhou"] = "下邳",
        };

        // The trail's stations in first-visit order, numbered (the design shows each city once).
        public static IReadOnlyList<(string node, string name)> Stations() =>
            TrailNodes().Distinct().Select(n => (n, Names.TryGetValue(n, out var s) ? s : n)).ToList();

        public static readonly string[] LadderSteps = { "徐州牧", "豫州刺史", "平原相", "平原令", "安喜尉", "白身" };
        public static int OptionalDone => (Ch != null && Ch.Has(ChapterState.Drill) ? 1 : 0) + (Ch != null && Ch.Has(ChapterState.Patrol15) ? 1 : 0);
        public static int Remembering => Ch?.Memories.Select(m => m.Who).Distinct().Count() ?? 0;
        public static int EchoesHeard => Ch == null ? 0 : FieldScripts5.EchoesHeard(Ch);
        public static int EchoCount => FieldScripts5.Echoes.Length;
        public static int Reputation => Run?.Life.Reputation ?? 0;

        // The key choices as (topic, what Liu Bei did).
        public static IReadOnlyList<(string topic, string choice)> ChoiceList()
        {
            var l = new List<(string, string)>();
            if (Ch == null) return l;
            l.Add(("槛车", Ch.Recall("lu_zhi", "luzhi_cart") == "B_escort" ? "护送恩师" : "公道自在"));
            l.Add(("督邮", Ch.Recall("liu_bei", "liubei_duyou") == "A_self" ? "亲手一杖" : "止住三弟"));
            l.Add(("众望", Ch.Has(ChapterScripts3.HulaoChoice + "0") ? "护住三弟" : "等候二弟"));
            l.Add(("刺客", Ch.Recall("liu_ping", "liuping_assassin") == "A_free" ? "放他归去" : "问明缘由"));
            l.Add(("吕布", Ch.Recall("lu_bu", "lubu_xuzhou") == "B_cautious" ? "屯于城外" : "开城相迎"));
            return l;
        }

        public static string Trail() => "十年足迹：" + string.Join(" → ", TrailNodes().Select(n => Names.TryGetValue(n, out var s) ? s : n));
    }
}
