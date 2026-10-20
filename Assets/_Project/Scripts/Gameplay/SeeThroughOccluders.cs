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
        [Tooltip("Height on the target the camera must be able to see")] public float TargetHeight = 0.9f;

        void LateUpdate()
        {
            if (!Target) return;
            Vector3 from = transform.position, to = Target.position + Vector3.up * TargetHeight;
            var ray = new Ray(from, to - from);
            float length = Vector3.Distance(from, to) - 0.3f;
            foreach (var o in Occluder.All)
            {
                bool blocks = o.Bounds.IntersectRay(ray, out float hit) && hit < length && !o.Bounds.Contains(to);
                o.SetHidden(blocks);
            }
        }
    }
}
