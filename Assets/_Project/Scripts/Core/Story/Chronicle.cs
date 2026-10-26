using System;
using System.Collections.Generic;
using System.Linq;

namespace ThreeKingdoms.Core.Story
{
    // One main-line year node (chapter 1 script §3, §7.5): the year it is, which regions it opens or closes, which
    // places turn dangerous or safe. Years only jump at these nodes (blueprint §7).
    public sealed class ChronicleNodeDef
    {
        public string Id { get; }
        public int Year { get; }
        public string Era { get; }
        public IReadOnlyList<string> Opens { get; }
        public IReadOnlyList<string> Closes { get; }
        public IReadOnlyList<string> Danger { get; }

        public ChronicleNodeDef(string id, int year, string era, string[] opens = null, string[] closes = null, string[] danger = null)
        {
            Id = id; Year = year; Era = era;
            Opens = opens ?? Array.Empty<string>();
            Closes = closes ?? Array.Empty<string>();
            Danger = danger ?? Array.Empty<string>();
        }
    }

    // Chapter 1 "崛起" nodes and regions after CHAPTER1_RISE_SCRIPT.md §3 and §7.3–7.5 (proposal reviewed by CCGS,
    // route chosen by the user 2026-10-24). Region ids: R1..R8 (R3g = only the Guangzong area of R3 in 184,
    // yingchuan = the one-off battle map). Danger ids name a region or a town inside an open region.
    public static class Chapter1Chronicle
    {
        public const string R1 = "R1", R2 = "R2", R3Guangzong = "R3g", R3 = "R3", R4 = "R4", R5a = "R5a", R5b = "R5b",
            R6 = "R6", R7 = "R7", R8 = "R8", Yingchuan = "yingchuan", Ye = "ye", Puyang = "puyang";

        public static readonly IReadOnlyList<ChronicleNodeDef> Nodes = new[]
        {
            new ChronicleNodeDef("N1", 184, "中平元年", opens: new[] { R1, R3Guangzong, Yingchuan }),
            new ChronicleNodeDef("N2", 185, "中平二年", opens: new[] { R2 }, closes: new[] { Yingchuan }),
            new ChronicleNodeDef("N3", 189, "中平六年"),
            new ChronicleNodeDef("N4", 190, "初平元年", opens: new[] { R5a }),
            // 191: the rest of R3 opens with the Pingyuan base; Ye now belongs to Yuan Shao (pass needed).
            new ChronicleNodeDef("N5", 191, "初平二年", opens: new[] { R5b, R4, R3 }, closes: new[] { R3Guangzong }, danger: new[] { Ye }),
            new ChronicleNodeDef("N6", 192, "初平三年", opens: new[] { R6 }, danger: new[] { R6 }),
            new ChronicleNodeDef("N7", 193, "初平四年", opens: new[] { R7 }),
            new ChronicleNodeDef("N8", 194, "兴平元年", opens: new[] { R8 }, danger: new[] { Puyang }),
            new ChronicleNodeDef("epilogue", 195, "兴平二年"),
        };

        public static readonly IReadOnlyCollection<string> Regions = new[] { R1, R2, R3Guangzong, R3, R4, R5a, R5b, R6, R7, R8, Yingchuan };
        public static readonly IReadOnlyCollection<string> DangerAreas = new[] { Ye, R6, Puyang };

        public static int IndexOf(string nodeId)
        {
            for (int i = 0; i < Nodes.Count; i++) if (Nodes[i].Id == nodeId) return i;
            return -1;
        }
    }

    // Where the play-through stands in the chronicle: the current year node, the regions open to the player and the
    // places marked dangerous. Pure C# like StoryState, saved by ADR-0004 (fields chronicleNode / openRegions /
    // dangerAreas). Nodes only move forward one at a time; story beats can still open, close or calm single places.
    public sealed class ChronicleState
    {
        readonly HashSet<string> open = new HashSet<string>();
        readonly HashSet<string> danger = new HashSet<string>();

        public int NodeIndex { get; private set; }
        public ChronicleNodeDef Node => Chapter1Chronicle.Nodes[NodeIndex];
        public string NodeId => Node.Id;
        public int Year => Node.Year;
        public IReadOnlyCollection<string> OpenRegions => open;
        public IReadOnlyCollection<string> DangerAreas => danger;

        ChronicleState() { }

        // A new game: the first node applied (184, Zhuojun and the Guangzong front).
        public static ChronicleState Start()
        {
            var s = new ChronicleState();
            s.Apply(Chapter1Chronicle.Nodes[0]);
            return s;
        }

        // Development / probe entry: every node up to and including nodeId applied in order.
        public static ChronicleState At(string nodeId)
        {
            int target = Chapter1Chronicle.IndexOf(nodeId);
            if (target < 0) throw new ArgumentException($"未知年份节点 {nodeId}");
            var s = Start();
            while (s.NodeIndex < target) s.Advance();
            return s;
        }

        // A saved play-through. An empty node id is a save from before chronicles existed: it starts at the first node.
        public static ChronicleState Restore(string nodeId, IEnumerable<string> openRegions, IEnumerable<string> dangerAreas)
        {
            if (string.IsNullOrEmpty(nodeId)) return Start();
            int index = Chapter1Chronicle.IndexOf(nodeId);
            if (index < 0) throw new ArgumentException($"未知年份节点 {nodeId}");
            var s = new ChronicleState { NodeIndex = index };
            foreach (string r in openRegions ?? Enumerable.Empty<string>())
            {
                if (!Chapter1Chronicle.Regions.Contains(r)) throw new ArgumentException($"未知区域 {r}");
                s.open.Add(r);
            }
            foreach (string d in dangerAreas ?? Enumerable.Empty<string>())
            {
                if (!Chapter1Chronicle.DangerAreas.Contains(d)) throw new ArgumentException($"未知危险区域 {d}");
                s.danger.Add(d);
            }
            return s;
        }

        // Moves to the next year node and applies it. False at the last node.
        public bool Advance()
        {
            if (NodeIndex + 1 >= Chapter1Chronicle.Nodes.Count) return false;
            NodeIndex++;
            Apply(Node);
            return true;
        }

        // Only the next node can be reached (a replayed beat or an out-of-order call changes nothing).
        public bool AdvanceTo(string nodeId) => Chapter1Chronicle.IndexOf(nodeId) == NodeIndex + 1 && Advance();

        public bool IsOpen(string region) => open.Contains(region);
        public bool IsDangerous(string area) => danger.Contains(area);

        // Single story beats (a pass granted, a one-off map closed).
        public void Close(string region) => open.Remove(region);
        public void Calm(string area) => danger.Remove(area);

        void Apply(ChronicleNodeDef node)
        {
            foreach (string r in node.Opens) open.Add(r);
            foreach (string r in node.Closes) open.Remove(r);
            foreach (string d in node.Danger) danger.Add(d);
        }
    }
}
