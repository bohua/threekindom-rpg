using UnityEngine;

namespace ThreeKingdoms.Gameplay
{
    // A townsperson busy at something (user 2026-10-22): steps through a few pose frames of their 3×3 sheet in a loop
    // (drilling with the spear, juggling, scrubbing laundry…). Pauses on the first frame while the town is held.
    public sealed class PoseCycle : MonoBehaviour
    {
        public SpriteFrameAnimator Animator;
        public int[] Frames = { 0 };
        [Tooltip("Seconds per pose")] public float Interval = 0.5f;
        [Tooltip("Offsets this one so a row of drilling soldiers is not in lockstep")] public float Phase;
        public bool Paused { get; set; }

        int shown = -1;

        void Update()
        {
            if (!Animator || Frames == null || Frames.Length == 0) return;
            int i = Paused ? 0 : Mathf.FloorToInt((Time.time + Phase) / Mathf.Max(0.05f, Interval)) % Frames.Length;
            int frame = Frames[i];
            if (frame == shown) return;
            shown = frame;
            Animator.Frame = frame;
            Animator.Apply();
        }
    }
}
