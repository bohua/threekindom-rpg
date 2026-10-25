using UnityEngine;

namespace ThreeKingdoms.Gameplay
{
    // Walk animation. With a walk-cycle atlas (CHARACTER_WALK_CYCLES.md: frames 9–24 = rows down/left/right/up ×
    // left contact, passing, right contact, passing) it plays the drawn 4-frame cycle for the facing direction and
    // stands on the passing frame when idle. Older atlases with only the nine poses fall back to alternating a pose
    // with its mirror plus a small bounce. Either way the cycle follows distance walked, so steps match speed.
    public sealed class PixelWalkFrames : MonoBehaviour
    {
        public const int WalkStart = 9, WalkFramesPerRow = 4;

        public SpriteFrameAnimator Animator;
        public int Front = 0, Left = 1, Right = 2, Back = 3;
        [Tooltip("Metres walked per drawn frame (walk-cycle atlas)")] public float FrameLength = 0.6f;
        [Tooltip("Metres per step for the mirrored fallback")] public float StepLength = 0.5f;
        [Tooltip("Fallback bounce height per step in world units (about two art pixels)")] public float Bounce = 0.06f;

        Vector3 last;
        int row;          // 0 down, 1 left, 2 right, 3 up
        float phase;
        PixelBillboard billboard;

        public bool Walking { get; private set; }
        public bool Mirrored => billboard && billboard.Flip;
        public bool HasWalkCycle => Animator && Animator.Columns >= WalkStart + 4 * WalkFramesPerRow;
        // Story poses after the walk cycle (user 2026-10-24, the peach-garden oath): frames 25 onward.
        public const int StoryPoseStart = WalkStart + 4 * WalkFramesPerRow;
        // A story pose shown instead of the walk while set (-1 = none); see OathPose.
        public int PoseOverride { get; set; } = -1;
        // Extra height for a hop in a story scene (laughing, raising a bowl), in world units.
        public float StageLift { get; set; }
        public bool HasStoryPose(int pose) => Animator && pose >= 0 && Animator.Columns > StoryPoseStart + pose;
        public int CurrentFrame => Animator ? Animator.Frame : -1;

        // Awake, not Start: a follower switched on mid-frame must already know its atlas has walk frames.
        void Awake()
        {
            if (Animator) { billboard = Animator.GetComponent<PixelBillboard>(); Animator.Apply(); }
        }

        void OnEnable() => last = transform.position;

        // Turn to face a direction while standing (story scenes): the next frame shows that row's standing pose.
        public void Face(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            row = Mathf.Abs(direction.x) > Mathf.Abs(direction.z) ? (direction.x < 0f ? 1 : 2) : (direction.z > 0f ? 3 : 0);
        }

        void LateUpdate()
        {
            Vector3 delta = transform.position - last;
            last = transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > 4f) delta = Vector3.zero;   // a teleport (fade, spawn, follower snap) is not a step
            if (!Animator) return;
            if (PoseOverride >= 0 && HasStoryPose(PoseOverride))
            {
                if (Animator.Frame != StoryPoseStart + PoseOverride) Animator.Frame = StoryPoseStart + PoseOverride;
                if (billboard) { billboard.Flip = false; billboard.Lift = StageLift; }
                return;
            }
            float speed = Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f;
            Walking = speed > 0.3f;
            if (Walking)
                row = Mathf.Abs(delta.x) > Mathf.Abs(delta.z) ? (delta.x < 0f ? 1 : 2) : (delta.z > 0f ? 3 : 0);

            int frame;
            bool flip = false;
            float lift = 0f;
            if (HasWalkCycle)
            {
                if (Walking)
                {
                    phase += delta.magnitude / Mathf.Max(0.05f, FrameLength);
                    frame = WalkStart + row * WalkFramesPerRow + Mathf.FloorToInt(phase) % WalkFramesPerRow;
                }
                else { phase = 0f; frame = WalkStart + row * WalkFramesPerRow + 1; }   // passing pose = standing
            }
            else
            {
                frame = row == 1 ? Left : row == 2 ? Right : row == 3 ? Back : Front;
                if (Walking)
                {
                    phase += delta.magnitude / Mathf.Max(0.05f, StepLength);
                    float step = phase - Mathf.Floor(phase);
                    lift = Mathf.Sin(step * Mathf.PI) * Bounce;
                    if (Mathf.FloorToInt(phase) % 2 == 1)
                    {
                        // The mirrored opposite pose faces the same way with the other leg forward.
                        if (row == 1) frame = Right;
                        else if (row == 2) frame = Left;
                        flip = true;
                    }
                }
                else phase = 0f;
            }

            if (Animator.Frame != frame) Animator.Frame = frame;
            if (billboard) { billboard.Flip = flip; billboard.Lift = lift + StageLift; }
        }
    }
}
