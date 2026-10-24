using UnityEngine;

namespace Jrpg
{
    // A region of a town with its own shot (user 2026-10-24: closer, swinging, other angles at different places).
    // While the player stands inside, FollowCamera eases toward this zone's pitch, distance, yaw, field of view and
    // look-at offset; outside every zone it eases back to its own defaults. Yaw stays within ±20° of the scene's
    // facing so the south-facing façades never show their flat edges (ADR-0005 amendment 2026-10-24).
    public sealed class CameraZone : MonoBehaviour
    {
        public static readonly System.Collections.Generic.List<CameraZone> All = new System.Collections.Generic.List<CameraZone>();

        [Tooltip("World-space extent on the ground (y ignored).")] public Vector2 Size = new Vector2(10f, 10f);
        [Range(5f, 60f)] public float Pitch = 25f;
        [Range(10f, 60f)] public float Distance = 34f;
        [Range(-20f, 20f)] public float Yaw;
        [Range(10f, 40f)] public float Fov = 20f;
        [Tooltip("Where on the player the camera looks, in metres (y = up).")] public Vector3 LookOffset = new Vector3(0, 0.8f, 0);
        [Tooltip("Slow side-to-side swing of the yaw, degrees (0 = still).")] public float Sway;
        [Tooltip("Higher wins where zones overlap.")] public int Priority;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public bool Contains(Vector3 p)
        {
            Vector3 c = transform.position;
            return Mathf.Abs(p.x - c.x) <= Size.x / 2f && Mathf.Abs(p.z - c.z) <= Size.y / 2f;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.35f);
            Gizmos.DrawCube(transform.position, new Vector3(Size.x, 0.1f, Size.y));
        }
    }
}
