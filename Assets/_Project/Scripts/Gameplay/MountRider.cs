using UnityEngine;

namespace ThreeKingdoms.Gameplay
{
    // One character on foot or on horseback (user 2026-10-24): outside towns the party mounts and gallops, and
    // dismounts on reaching the next place. The walking sprite and the mounted sprite are two quads; this swaps them,
    // plays the mount sequence (and backwards to dismount), and runs the gallop cycle from distance travelled.
    // Mounted atlas (Codex art/mounted-heroes): 20 frames, rows down/left/right/up × 4 gallop frames, then 4 mount
    // frames facing right. Without a mounted atlas only the speed changes.
    public sealed class MountRider : MonoBehaviour
    {
        public enum State { OnFoot, Mounting, Riding, Dismounting }
        public const int MountStart = 16, Frames = 4;

        public GameObject WalkSprite;
        public SpriteFrameAnimator RideAnimator;
        public PixelWalkFrames Walk;
        [Tooltip("Seconds for the whole mount (or dismount) sequence")] public float SequenceTime = 0.8f;
        [Tooltip("Metres galloped per drawn frame")] public float FrameLength = 1.4f;

        public State Current { get; private set; } = State.OnFoot;
        public bool Busy => Current == State.Mounting || Current == State.Dismounting;
        public bool HasHorseArt => RideAnimator;

        float timer, phase;
        int row = 2;
        Vector3 last;
        PixelBillboard rideBillboard;

        void Awake()
        {
            if (RideAnimator) { rideBillboard = RideAnimator.GetComponent<PixelBillboard>(); RideAnimator.gameObject.SetActive(false); }
            last = transform.position;
        }

        public void Mount()
        {
            if (Current != State.OnFoot) return;
            Current = State.Mounting; timer = 0f;
            ShowHorse(true);
        }

        public void Dismount()
        {
            if (Current != State.Riding) return;
            Current = State.Dismounting; timer = 0f;
        }

        void ShowHorse(bool on)
        {
            if (!RideAnimator) return;
            RideAnimator.gameObject.SetActive(on);
            if (WalkSprite) WalkSprite.SetActive(!on);
            if (Walk) Walk.enabled = !on;
        }

        void LateUpdate()
        {
            Vector3 delta = transform.position - last;
            last = transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > 25f) delta = Vector3.zero;
            if (delta.sqrMagnitude > 0.0001f)
                row = Mathf.Abs(delta.x) > Mathf.Abs(delta.z) ? (delta.x < 0f ? 1 : 2) : (delta.z > 0f ? 3 : 0);

            switch (Current)
            {
                case State.Mounting:
                case State.Dismounting:
                    timer += Time.deltaTime;
                    float t = Mathf.Clamp01(timer / Mathf.Max(0.05f, SequenceTime));
                    int step = Mathf.Min(Frames - 1, Mathf.FloorToInt(t * Frames));
                    if (Current == State.Dismounting) step = Frames - 1 - step;
                    SetFrame(MountStart + step, row == 1);
                    if (t >= 1f)
                    {
                        if (Current == State.Mounting) Current = State.Riding;
                        else { Current = State.OnFoot; ShowHorse(false); }
                    }
                    break;
                case State.Riding:
                    float speed = Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f;
                    if (speed > 0.3f) phase += delta.magnitude / Mathf.Max(0.1f, FrameLength);
                    else phase = 0f;
                    SetFrame(row * Frames + Mathf.FloorToInt(phase) % Frames, false);
                    break;
            }
        }

        void SetFrame(int frame, bool flip)
        {
            if (!RideAnimator) return;
            if (RideAnimator.Frame != frame) RideAnimator.Frame = frame;
            if (rideBillboard) rideBillboard.Flip = flip;
        }
    }
}
