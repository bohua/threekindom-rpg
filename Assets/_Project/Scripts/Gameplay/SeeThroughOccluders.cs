using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThreeKingdoms.Gameplay
{
    // ZHUOJUN_TOWN_DESIGN §6: when the player walks behind a high wall or building, the occluder is cut away.
    // Each frame, occluders whose bounds cross the camera → player segment are switched to shadows-only.
    public sealed class SeeThroughOccluders : MonoBehaviour
    {
        public Transform Target;
        // Also kept in view, e.g. Guan Yu and Zhang Fei during the peach-garden oath (user 2026-10-26: a peach tree hid
        // Zhang Fei the whole time).
        public readonly List<Transform> AlsoVisible = new List<Transform>();
        [Tooltip("Height on the target the camera must be able to see")] public float TargetHeight = 0.9f;

        void LateUpdate()
        {
            if (!Target) return;
            Vector3 from = transform.position;
            foreach (var o in Occluder.All)
            {
                bool blocks = Blocks(o, from, Target);
                foreach (var t in AlsoVisible) blocks |= t && t.gameObject.activeInHierarchy && Blocks(o, from, t);
                o.SetHidden(blocks);
            }
        }

        bool Blocks(Occluder o, Vector3 from, Transform target)
        {
            Vector3 to = target.position + Vector3.up * TargetHeight;
            var ray = new Ray(from, to - from);
            float length = Vector3.Distance(from, to) - 0.3f;
            return o.Bounds.IntersectRay(ray, out float hit) && hit < length && !o.Bounds.Contains(to);
        }
    }
}
