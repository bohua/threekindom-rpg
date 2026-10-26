using System.Collections.Generic;
using UnityEngine;

namespace ThreeKingdoms.Gameplay
{
    // A wooden road sign at a fork of the post road (user 2026-10-24). Walking up to it shows a bubble, as an NPC
    // would, with one line per way: an arrow along the road and where it leads, with the distance.
    public sealed class Signpost : MonoBehaviour
    {
        public static readonly List<Signpost> All = new List<Signpost>();

        [Tooltip("Where each way leads")] public string[] Names;
        [Tooltip("A point a little way along each road, giving its direction")] public Vector3[] Toward;
        [Tooltip("Metres to each destination along the road")] public float[] Metres;
        public float Radius = 7f;
        [Tooltip("Height of the bubble's tail above the sign, metres")] public float BubbleHeight = 3.2f;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public static Signpost Nearest(Vector3 p)
        {
            Signpost best = null; float bestD = float.MaxValue;
            foreach (var s in All)
            {
                var d = s.transform.position - p; d.y = 0f;
                float m = d.magnitude;
                if (m <= s.Radius && m < bestD) { best = s; bestD = m; }
            }
            return best;
        }

        // Eight-way arrow for a ground direction; the camera looks north, so north is up the screen.
        public static string Arrow(Vector3 dir)
        {
            float a = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;   // 0 north, 90 east
            string[] arrows = { "↑", "↗", "→", "↘", "↓", "↙", "←", "↖" };
            return arrows[Mathf.RoundToInt(((a % 360f) + 360f) % 360f / 45f) % 8];
        }

        // Han measures: 1 步 ≈ 1.39 m, 1 里 = 300 步 (≈ 415 m).
        public static string Distance(float metres) =>
            metres >= 400f ? $"约 {Mathf.Max(1, Mathf.RoundToInt(metres / 415f))} 里" : $"约 {Mathf.RoundToInt(metres / 1.39f / 10f) * 10} 步";

        public IEnumerable<(string arrow, string name, string distance)> Lines()
        {
            for (int i = 0; Names != null && i < Names.Length; i++)
                yield return (Arrow(Toward[i] - transform.position), Names[i], Metres != null && i < Metres.Length ? Distance(Metres[i]) : "");
        }
    }
}
