using UnityEngine;

namespace ThreeKingdoms.Gameplay
{
    // A companion walking the leader's trail a fixed distance behind (Guan Yu, Zhang Fei once they join). It has no
    // collider, so it never blocks the player; PixelWalkFrames on the same object animates it from its own movement.
    [DefaultExecutionOrder(100)]   // after PartyTrail has recorded this frame's leader position
    public sealed class PartyFollower : MonoBehaviour
    {
        public PartyTrail Trail;
        [Tooltip("Metres behind the leader along the trail")] public float Distance = 1.2f;

        void LateUpdate()
        {
            if (Trail) transform.position = Trail.PointBehind(Distance);
        }

        public void Snap() { if (Trail) transform.position = Trail.PointBehind(Distance); }
    }
}
