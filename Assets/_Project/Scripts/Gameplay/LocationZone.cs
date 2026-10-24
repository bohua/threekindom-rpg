using System.Collections.Generic;
using UnityEngine;

namespace ThreeKingdoms.Gameplay
{
    // A named place for the location signboard (user 2026-10-24): "平原 · 南城门", or on a road "渤海 · 平原东 ↔ 高唐".
    // Major is the town (inside a town) or the region (outside); Minor the spot or the road, "A ↔ B" for a road.
    // A zone is a ground box, or a road segment A–B with a half width. Higher priority wins where zones overlap.
    public sealed class LocationZone : MonoBehaviour
    {
        public static readonly List<LocationZone> All = new List<LocationZone>();

        public string Major;
        public string Minor;
        public int Priority;
        [Tooltip("Ground box size (x, z) around the transform; ignored for a road segment.")] public Vector2 Size = new Vector2(10f, 10f);
        public bool IsSegment;
        public Vector3 A, B;
        public float HalfWidth = 8f;
        [Tooltip("Roads and open country: the party rides here (user 2026-10-24)")] public bool Ride;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public bool Contains(Vector3 p)
        {
            if (!IsSegment)
            {
                Vector3 c = transform.position;
                return Mathf.Abs(p.x - c.x) <= Size.x / 2f && Mathf.Abs(p.z - c.z) <= Size.y / 2f;
            }
            var a = new Vector2(A.x, A.z); var b = new Vector2(B.x, B.z); var q = new Vector2(p.x, p.z);
            float t = Mathf.Clamp01(Vector2.Dot(q - a, b - a) / Mathf.Max(0.001f, (b - a).sqrMagnitude));
            return (a + t * (b - a) - q).sqrMagnitude <= HalfWidth * HalfWidth;
        }

        // The highest-priority zone containing p (the first one added wins a tie); null outside every zone.
        public static LocationZone Find(Vector3 p)
        {
            LocationZone best = null;
            foreach (var z in All)
                if (z.Contains(p) && (best == null || z.Priority > best.Priority)) best = z;
            return best;
        }
    }
}
