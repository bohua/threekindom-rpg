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

        public static string Trail() => "十年足迹：" + string.Join(" → ", TrailNodes().Select(n => Names.TryGetValue(n, out var s) ? s : n));

        public static string Ladder() =>
            "【名分】白身 → 安喜尉 → 平原令 → 平原相 → 豫州刺史 → 徐州牧\n织席贩履之人，第一次被一州军民托付。";

        public static string Battles()
        {
            int optional = (Ch != null && Ch.Has(ChapterState.Drill) ? 1 : 0) + (Ch != null && Ch.Has(ChapterState.Patrol15) ? 1 : 0);
            return $"【征战】主线 13 战全部告捷；选做 {optional}/2（校场演武、小沛巡防）。";
        }

        public static string People()
        {
            int n = Ch?.Memories.Select(m => m.Who).Distinct().Count() ?? 0;
            int echoes = Ch?.Flags.Count(f => f.StartsWith("echo:")) ?? 0;
            return $"【人心】记住刘备的人：{n} 位；下邳城中听过的回声：{echoes} 条。";
        }

        public static string Choices()
        {
            var parts = new List<string>();
            if (Ch == null) return "【抉择】";
            parts.Add("槛车：" + (Ch.Recall("lu_zhi", "luzhi_cart") == "B_escort" ? "护送恩师一程" : "恩师清白，自有公论"));
            parts.Add("督邮：" + (Ch.Recall("liu_bei", "liubei_duyou") == "A_self" ? "亲手一杖" : "止住三弟"));
            parts.Add("虎牢关：" + (Ch.Has(ChapterScripts3.HulaoChoice + "0") ? "以众望护三弟" : "信三弟，待二弟"));
            parts.Add("刺客：" + (Ch.Recall("liu_ping", "liuping_assassin") == "A_free" ? "放他归去" : "问明缘由"));
            parts.Add($"声望：{Run?.Life.Reputation ?? 0}");
            return "【抉择】" + string.Join("；", parts) + "。";
        }
    }
}
