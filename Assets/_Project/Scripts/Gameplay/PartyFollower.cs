using UnityEngine;

namespace ThreeKingdoms.Gameplay
{
    // A companion walking the leader's trail (Guan Yu, Zhang Fei once they join). It runs after the leader rather
    // than being pinned to a fixed gap: it reacts late when Liu Bei sets off, so the line stretches, catches up on
    // the move, and closes in to a tighter gap after he stops (user 2026-10-20). Later slots react later still.
    // No collider, so it never blocks the player; PixelWalkFrames on the same object animates it from its movement.
    [DefaultExecutionOrder(100)]   // after PartyTrail has recorded this frame's leader position
    public sealed class PartyFollower : MonoBehaviour
    {
        public PartyTrail Trail;
        [Tooltip("Place in the line: 1 right behind the leader, 2 behind that")] public int Slot = 1;
        [Tooltip("Gap per slot while the leader walks, metres")] public float WalkGap = 1.9f;
        [Tooltip("Gap per slot once the leader stands still, metres")] public float StopGap = 1.3f;
        [Tooltip("How hard the follower closes the gap, per second")] public float CatchUp = 4f;
        [Tooltip("Acceleration in m/s², divided by the slot: lower means a later start")] public float Acceleration = 22f;
        [Tooltip("Top speed in m/s, above the leader's so it can catch up")] public float MaxSpeed = 13f;

        Vector3 lastLeader;

        public float Distance { get; private set; }   // metres behind the leader along the trail, right now
        public float Speed { get; private set; }
        public bool LeaderWalking { get; private set; }
        public float TargetGap => (LeaderWalking ? WalkGap : StopGap) * Slot;

        void LateUpdate()
        {
            if (!Trail) return;
            float dt = Time.deltaTime;
            Vector3 leader = Trail.transform.position;
            Vector3 moved = leader - lastLeader;
            moved.y = 0f;
            lastLeader = leader;
            float step = moved.magnitude;
            if (step > 2f) step = 0f;   // a teleport is handled by Snap, not chased
            float leaderSpeed = dt > 0f ? step / dt : 0f;
            LeaderWalking = leaderSpeed > 0.3f;

            Distance += step;   // standing still, the follower falls behind by what the leader walked
            if (dt > 0f)
            {
                float wanted = Mathf.Clamp(leaderSpeed + (Distance - TargetGap) * CatchUp, 0f, MaxSpeed);
                Speed = Mathf.MoveTowards(Speed, wanted, Acceleration / Mathf.Max(1, Slot) * dt);
                Distance -= Speed * dt;
            }
            Distance = Mathf.Clamp(Distance, 0.6f * Slot, Trail.Length - 0.5f);
            transform.position = Trail.PointBehind(Distance);
        }

        // Stand at the resting gap (after a fade, a scene load or a join).
        public void Snap()
        {
            Speed = 0f;
            LeaderWalking = false;
            Distance = StopGap * Slot;
            if (!Trail) return;
            lastLeader = Trail.transform.position;
            transform.position = Trail.PointBehind(Distance);
        }
    }
}
