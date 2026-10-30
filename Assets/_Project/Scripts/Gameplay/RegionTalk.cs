using System.Collections.Generic;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Gameplay.Story;
using UnityEngine;

namespace ThreeKingdoms.Gameplay
{
    // Someone (or something) to talk to at a wild spot of an open region, after the chapter 1 NPC design
    // (design/chapter1-rise/npc-sidequests, R1_R2 §R1.6 wiring table; user 2026-10-29). Walking up shows a bubble with
    // the speaker and the first line, as a signpost does; confirm turns to the next line. What is said depends on the
    // chronicle node (N1 184 … N8 194): each entry holds from its node until the next entry's, and a node with no entry
    // leaves the spot empty (the old soldier gone from the beacon). A speaker left blank reads as narration (an
    // inspected stele, the mute tomb-keeper's gestures).
    public sealed class RegionTalk : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Entry
        {
            [Tooltip("First node this entry holds from (1–8)")] public int from = 4;
            [Tooltip("Last node it holds to (0: until the next entry)")] public int until;
            public string speaker;
            [TextArea] public string[] lines;
            [Tooltip("Dialogue keys of the lines (dlg.lb1.…), for localisation")] public string[] keys;
        }

        public static readonly List<RegionTalk> All = new List<RegionTalk>();
        // The node used when no game is running (a scene opened in the editor): R1 opens from N4 (190).
        public static int FallbackNode = 4;

        public Entry[] Entries = new Entry[0];
        [Tooltip("The sprite to hide when no entry holds at the current node")] public GameObject Body;
        public float Radius = 4.5f;
        public float BubbleHeight = 3.2f;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Start()
        {
            if (Body) Body.SetActive(Current != null);
        }

        public static int Node => GameRun.Current != null ? (int)GameRun.Current.Chapter2.Node : FallbackNode;

        // The entry holding at the current node: the latest one started at or before it, unless it has ended.
        public Entry Current
        {
            get
            {
                Entry best = null;
                int n = Node;
                foreach (var e in Entries)
                    if (e.from <= n && (e.until == 0 || n <= e.until) && (best == null || e.from >= best.from)) best = e;
                return best;
            }
        }

        public string Speaker => Current?.speaker ?? "";
        public IReadOnlyList<string> Script => Current?.lines ?? new string[0];

        public static RegionTalk Nearest(Vector3 p)
        {
            RegionTalk best = null; float bestD = float.MaxValue;
            foreach (var t in All)
            {
                if (t.Current == null) continue;
                var d = t.transform.position - p; d.y = 0f;
                float m = d.magnitude;
                if (m <= t.Radius && m < bestD) { best = t; bestD = m; }
            }
            return best;
        }
    }
}
