using UnityEngine;

namespace ThreeKingdoms.Gameplay
{
    // Walk animation from the single-pose AI sheets (front idle, left walk, right walk, back walk): until real
    // walk cycles are drawn, a step alternates the pose with its mirror image (the legs swap) and adds a small
    // bounce; standing still shows the last facing without bounce.
    public sealed class PixelWalkFrames : MonoBehaviour
    {
        public SpriteFrameAnimator Animator;
        public int Front = 0, Left = 1, Right = 2, Back = 3;
        [Tooltip("Metres per step: the cycle follows distance walked, so steps match speed and feet do not slide")] public float StepLength = 0.5f;
        [Tooltip("Bounce height per step in world units (about two art pixels)")] public float Bounce = 0.06f;

        Vector3 last;
        int facing;
        float phase;
        PixelBillboard billboard;

        public bool Walking { get; private set; }
        public bool Mirrored => billboard && billboard.Flip;

        void Start()
        {
            last = transform.position;
            facing = Front;
            if (Animator) billboard = Animator.GetComponent<PixelBillboard>();
        }

        void LateUpdate()
        {
            Vector3 delta = transform.position - last;
            last = transform.position;
            delta.y = 0f;
            if (!Animator) return;
            float speed = Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f;
            Walking = speed > 0.3f;
            if (Walking)
                facing = Mathf.Abs(delta.x) > Mathf.Abs(delta.z)
                    ? (delta.x < 0f ? Left : Right)
                    : (delta.z > 0f ? Back : Front);

            int frame = facing;
            bool flip = false;
            float lift = 0f;
            if (Walking)
            {
                phase += delta.magnitude / Mathf.Max(0.05f, StepLength);
                bool second = Mathf.FloorToInt(phase) % 2 == 1;
                float step = phase - Mathf.Floor(phase);
                lift = Mathf.Sin(step * Mathf.PI) * Bounce;
                if (second)
                {
                    // The mirrored opposite pose faces the same way with the other leg forward.
                    if (facing == Left) { frame = Right; flip = true; }
                    else if (facing == Right) { frame = Left; flip = true; }
                    else flip = true;
                }
            }
            else phase = 0f;

            if (Animator.Frame != frame) Animator.Frame = frame;
            if (billboard) { billboard.Flip = flip; billboard.Lift = lift; }
        }
    }
}
