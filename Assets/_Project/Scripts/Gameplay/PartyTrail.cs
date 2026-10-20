using System.Collections.Generic;
using UnityEngine;

namespace ThreeKingdoms.Gameplay
{
    // Breadcrumb trail behind the party leader: the leader's recent positions, newest first, so followers walk the
    // same path the player took (round corners and doorways) instead of cutting straight across.
    public sealed class PartyTrail : MonoBehaviour
    {
        [Tooltip("Metres between recorded points")] public float Spacing = 0.12f;
        [Tooltip("Trail length kept, in metres")] public float Length = 6f;

        readonly List<Vector3> points = new List<Vector3>();

        void LateUpdate()
        {
            Vector3 p = transform.position;
            if (points.Count == 0 || (p - points[0]).sqrMagnitude >= Spacing * Spacing) points.Insert(0, p);
            int keep = Mathf.CeilToInt(Length / Mathf.Max(0.01f, Spacing)) + 2;
            if (points.Count > keep) points.RemoveRange(keep, points.Count - keep);
        }

        // Restart the trail at the leader, laid out straight towards `behind` (after a fade, a scene load or a join).
        public void Reset(Vector3 behind)
        {
            points.Clear();
            behind.y = 0f;
            behind = behind.sqrMagnitude > 0.0001f ? behind.normalized : Vector3.back;
            Vector3 p = transform.position;
            for (float d = 0f; d <= Length; d += Spacing) points.Add(p + behind * d);
        }

        // The point `distance` metres back along the path from the leader's current position.
        public Vector3 PointBehind(float distance)
        {
            Vector3 prev = transform.position;
            foreach (var next in points)
            {
                float step = Vector3.Distance(prev, next);
                if (step >= distance) return step > 0f ? Vector3.Lerp(prev, next, distance / step) : next;
                distance -= step;
                prev = next;
            }
            return prev;
        }
    }
}
