using UnityEngine;

namespace ThreeKingdoms.Gameplay
{
    // Mounts the party on roads and open country and dismounts it in towns and at places (user 2026-10-24), from the
    // LocationZone the leader stands in. The leader stops for the mount/dismount sequence, then moves at RideSpeed;
    // companions follow a beat later and keep a longer gap on horseback.
    [DefaultExecutionOrder(-10)]
    public sealed class RideController : MonoBehaviour
    {
        public Jrpg.PlayerMover Mover;
        public MountRider Leader;
        public MountRider[] Companions;
        public float WalkSpeed = 9f, RideSpeed = 20f;
        [Tooltip("Seconds the leader must stay in riding (or walking) ground before switching, so borders do not flicker")]
        public float Settle = 0.25f;
        [Tooltip("Extra delay per companion slot")] public float CompanionDelay = 0.25f;

        float pending;
        bool wantRide;
        readonly System.Collections.Generic.List<(MountRider rider, float at, bool mount)> queue = new System.Collections.Generic.List<(MountRider, float, bool)>();

        public bool Riding => Leader && Leader.Current == MountRider.State.Riding;

        void Update()
        {
            if (!Mover || !Leader) return;
            var zone = LocationZone.Find(Mover.transform.position);
            bool ride = zone && zone.Ride;
            if (ride != wantRide) { pending += Time.deltaTime; if (pending >= Settle) { wantRide = ride; pending = 0f; Switch(ride); } }
            else pending = 0f;

            // The leader stands still while getting on or off the horse.
            Mover.InputEnabled = !Leader.Busy;
            Mover.Speed = Leader.Current == MountRider.State.Riding ? RideSpeed : WalkSpeed;

            for (int i = queue.Count - 1; i >= 0; i--)
                if (Time.time >= queue[i].at)
                {
                    var (rider, _, mount) = queue[i];
                    if (mount) rider.Mount(); else rider.Dismount();
                    queue.RemoveAt(i);
                }
        }

        void Switch(bool ride)
        {
            if (ride) Leader.Mount(); else Leader.Dismount();
            queue.Clear();
            for (int i = 0; Companions != null && i < Companions.Length; i++)
            {
                var c = Companions[i];
                if (!c) continue;
                queue.Add((c, Time.time + CompanionDelay * (i + 1), ride));
                var f = c.GetComponent<PartyFollower>();
                if (f)
                {
                    f.MaxSpeed = ride ? RideSpeed * 1.4f : 13f;
                    f.Acceleration = ride ? 50f : 22f;
                    f.WalkGap = ride ? 3.4f : 1.9f;
                    f.StopGap = ride ? 2.6f : 1.3f;
                }
            }
        }
    }
}
