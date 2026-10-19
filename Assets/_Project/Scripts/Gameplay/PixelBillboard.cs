using UnityEngine;

namespace ThreeKingdoms.Gameplay
{
    // ADR-0005: pixel characters are upright quads that only yaw toward the camera, so they never tilt
    // with the camera pitch and their shadows keep a stable shape. Seen from a pitched camera an upright
    // quad is foreshortened by cos(pitch); SquarePixels stretches it by 1/cos(pitch) so each art pixel
    // stays square on screen. The quad's pivot is its centre, so it is lifted to keep the feet grounded.
    [ExecuteAlways]
    public sealed class PixelBillboard : MonoBehaviour
    {
        public Camera Target;
        public bool SquarePixels = true;
        [Tooltip("Unstretched quad size in world units (frame pixels / pixels-per-unit).")]
        public Vector2 BaseSize = new Vector2(1.6f, 1.6f);

        void LateUpdate()
        {
            Camera cam = Target ? Target : Camera.main;
            if (!cam) return;
            Vector3 euler = cam.transform.eulerAngles;
            transform.rotation = Quaternion.Euler(0f, euler.y, 0f);

            float pitch = Mathf.DeltaAngle(0f, euler.x);
            float height = BaseSize.y;
            if (SquarePixels && Mathf.Abs(pitch) < 80f) height /= Mathf.Cos(pitch * Mathf.Deg2Rad);
            var scale = new Vector3(BaseSize.x, height, 1f);
            if (transform.localScale != scale) transform.localScale = scale;
            var position = new Vector3(transform.localPosition.x, height / 2f, transform.localPosition.z);
            if (transform.localPosition != position) transform.localPosition = position;
        }
    }
}
