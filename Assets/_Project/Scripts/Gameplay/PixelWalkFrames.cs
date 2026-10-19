using UnityEngine;

namespace ThreeKingdoms.Gameplay
{
    // Picks a facing frame from the movement direction. The AI sheets only have single poses
    // (front idle, left, right, back), so this stands in for walk cycles until they are drawn.
    public sealed class PixelWalkFrames : MonoBehaviour
    {
        public SpriteFrameAnimator Animator;
        public int Front = 0, Left = 1, Right = 2, Back = 3;
        Vector3 last;

        void Start() => last = transform.position;

        void LateUpdate()
        {
            Vector3 delta = transform.position - last;
            last = transform.position;
            delta.y = 0f;
            if (!Animator || delta.sqrMagnitude < 1e-6f) return;
            Animator.Frame = Mathf.Abs(delta.x) > Mathf.Abs(delta.z)
                ? (delta.x < 0f ? Left : Right)
                : (delta.z > 0f ? Back : Front);
        }
    }
}
