using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThreeKingdoms.Gameplay
{
    // Marks a wall, building or tree that may stand between the town camera and the player.
    public sealed class Occluder : MonoBehaviour
    {
        internal static readonly List<Occluder> All = new List<Occluder>();
        internal Renderer[] Renderers;
        internal Bounds Bounds;
        internal bool Hidden;

        void OnEnable()
        {
            Renderers = GetComponentsInChildren<Renderer>();
            Bounds = new Bounds(transform.position, Vector3.zero);
            foreach (var r in Renderers) Bounds.Encapsulate(r.bounds);
            All.Add(this);
        }

        void OnDisable() { SetHidden(false); All.Remove(this); }

        internal void SetHidden(bool hide)
        {
            if (Hidden == hide) return;
            Hidden = hide;
            // Keep the shadow so the ground does not suddenly brighten when the wall is cut away.
            foreach (var r in Renderers) if (r) r.shadowCastingMode = hide ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
        }
    }
}
