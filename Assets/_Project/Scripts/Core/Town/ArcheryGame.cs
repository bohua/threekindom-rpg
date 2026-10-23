using System;
using System.Collections.Generic;
using System.Linq;

namespace ThreeKingdoms.Core.Town
{
    // The barracks archery range (user 2026-10-22, after 太阁立志传5): five straw targets at different heights and
    // distances; the archer sets an angle (up/down) and a draw (left/right) and the arrow flies a ballistic arc.
    // Every target the arc passes through counts, once; three arrows; when all five have been hit a fresh set of five
    // stands up. Units are metres on the range, x away from the archer, y up from the ground.
    public sealed class ArcheryGame
    {
        public const int Arrows = 3, TargetCount = 5;
        public const float MinAngle = 0f, MaxAngle = 60f, MinPower = 0.2f, MaxPower = 1f;
        public const float Gravity = 9.8f, MinSpeed = 12f, MaxSpeed = 30f, TargetRadius = 1.2f;   // doubled after playtest (user 2026-10-23)
        public const float NearestTarget = 12f, FarthestTarget = 36f, LowestTarget = 1f, HighestTarget = 9f;
        public static readonly (float x, float y) Release = (0f, 1.4f);

        public sealed class Target
        {
            public float X, Y;
            public bool Hit;
        }

        readonly Random rng;
        readonly List<Target> targets = new List<Target>();

        public IReadOnlyList<Target> Targets => targets;
        public int ArrowsLeft { get; private set; } = Arrows;
        public int Score { get; private set; }
        public int Sets { get; private set; } = 1;
        public bool Finished => ArrowsLeft == 0;

        public ArcheryGame(Random rng)
        {
            this.rng = rng;
            StandTargets();
        }

        // Five targets that can be cleared in one or two arrows (user 2026-10-23): most stand along one flyable arc,
        // the rest along a second; each at its own place along the range.
        void StandTargets()
        {
            targets.Clear();
            int onFirst = rng.Next(3, TargetCount + 1);
            PlaceAlongArc(onFirst);
            if (targets.Count < TargetCount) PlaceAlongArc(TargetCount - targets.Count);
            // Never fewer than five, even if no second arc fitted.
            while (targets.Count < TargetCount) PlaceAlongArc(1);
        }

        void PlaceAlongArc(int count)
        {
            for (int attempt = 0; attempt < 60; attempt++)
            {
                float angle = 6f + 36f * (float)rng.NextDouble(), power = 0.45f + 0.5f * (float)rng.NextDouble();
                var usable = Path(angle, power).Where(p => p.x >= NearestTarget && p.x <= FarthestTarget && p.y >= LowestTarget && p.y <= HighestTarget)
                    .Where(p => targets.All(t => Math.Abs(t.X - p.x) > TargetRadius * 2.6f)).ToList();
                if (usable.Count < count) continue;
                float span = usable[usable.Count - 1].x - usable[0].x;
                if (span < TargetRadius * 2.6f * count) continue;
                // Evenly spaced along the usable stretch, nudged a little so sets differ.
                var picked = new List<Target>();
                for (int i = 0; i < count; i++)
                {
                    float x = usable[0].x + span * (i + 0.5f) / count + (float)(rng.NextDouble() - 0.5) * span / count * 0.4f;
                    var p = usable.OrderBy(q => Math.Abs(q.x - x)).First();
                    if (picked.Concat(targets).Any(t => Math.Abs(t.X - p.x) < TargetRadius * 2.6f)) { picked = null; break; }
                    picked.Add(new Target { X = p.x, Y = p.y });
                }
                if (picked == null) continue;
                targets.AddRange(picked);
                targets.Sort((a, b) => a.X.CompareTo(b.X));
                return;
            }
        }

        public static float Speed(float power) => MinSpeed + (MaxSpeed - MinSpeed) * Clamp(power, MinPower, MaxPower);

        // The arc for an angle (degrees) and a draw (0.2–1), sampled every dt seconds until it lands or leaves the range.
        public static List<(float x, float y)> Path(float angle, float power, float dt = 0.01f)
        {
            float a = Clamp(angle, MinAngle, MaxAngle) * (float)Math.PI / 180f, v = Speed(power);
            float vx = v * (float)Math.Cos(a), vy = v * (float)Math.Sin(a);
            var points = new List<(float, float)> { Release };
            for (float t = dt; t < 10f; t += dt)
            {
                float x = Release.x + vx * t, y = Release.y + vy * t - Gravity * t * t / 2f;
                points.Add((x, y));
                if (y < 0f || x > FarthestTarget + 6f) break;
            }
            return points;
        }

        public sealed class Shot
        {
            public List<(float x, float y)> Path;
            public List<Target> Hits = new List<Target>();
            public bool Refreshed;     // all five had been hit, so a new set stood up after this arrow
        }

        public Shot Shoot(float angle, float power)
        {
            if (Finished) return null;
            var shot = new Shot { Path = Path(angle, power) };
            for (int i = 1; i < shot.Path.Count; i++)
                foreach (var t in targets)
                    if (!t.Hit && SegmentHits(shot.Path[i - 1], shot.Path[i], t))
                    {
                        t.Hit = true;
                        shot.Hits.Add(t);
                    }
            Score += shot.Hits.Count;
            ArrowsLeft--;
            if (targets.All(t => t.Hit))
            {
                StandTargets();
                Sets++;
                shot.Refreshed = true;
            }
            return shot;
        }

        // Rewards (first draft): five copper a target, and reputation for a good round.
        public int CopperReward => Score * 5;
        public int ReputationReward => Score >= 5 ? 2 : Score >= 3 ? 1 : 0;

        static bool SegmentHits((float x, float y) a, (float x, float y) b, Target t)
        {
            float dx = b.x - a.x, dy = b.y - a.y;
            float len2 = dx * dx + dy * dy;
            float k = len2 > 0f ? Clamp(((t.X - a.x) * dx + (t.Y - a.y) * dy) / len2, 0f, 1f) : 0f;
            float px = a.x + k * dx - t.X, py = a.y + k * dy - t.Y;
            return px * px + py * py <= TargetRadius * TargetRadius;
        }

        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
