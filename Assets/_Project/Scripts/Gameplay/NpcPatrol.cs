using UnityEngine;

namespace ThreeKingdoms.Gameplay
{
    // A resident walking a short fixed route (ZHUOJUN_TOWN_DESIGN.md §4: the patrol soldier, the child, the grain
    // porter): walks between waypoints, rests at each, waits while the player stands in the way and stops while a
    // conversation or panel holds the town (Paused). PixelWalkFrames on the same object animates the walk.
    public sealed class NpcPatrol : MonoBehaviour
    {
        public Vector3[] Waypoints = System.Array.Empty<Vector3>();
        public float Speed = 1.6f;
        public float Rest = 1.5f;
        [Tooltip("Stand still while the player is this close, so the resident never walks into them")] public float GiveWay = 1.4f;
        public Transform Player;
        public bool Paused;

        int next = 1;
        float restUntil;

        void Update()
        {
            if (Paused || Waypoints.Length < 2 || Time.time < restUntil) return;
            Vector3 target = Waypoints[next];
            Vector3 to = target - transform.position;
            to.y = 0f;
            if (Player)
            {
                Vector3 p = Player.position - transform.position;
                p.y = 0f;
                if (p.magnitude < GiveWay && Vector3.Dot(p, to) > 0f) return;   // the player is ahead: wait
            }
            float step = Speed * Time.deltaTime;
            if (to.magnitude <= step)
            {
                transform.position = new Vector3(target.x, transform.position.y, target.z);
                next = (next + 1) % Waypoints.Length;
                restUntil = Time.time + Rest;
                return;
            }
            transform.position += to.normalized * step;
        }
    }
}
