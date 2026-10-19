using UnityEngine;

namespace ThreeKingdoms.Gameplay
{
    // One layered 2D battle background (ADR-0005 §8): 640×360 pixel layers from far to near, plus the light
    // direction painted into them so the lit character quads in front match the picture.
    [CreateAssetMenu(menuName = "ThreeKingdoms/Battle Backdrop")]
    public sealed class BattleBackdrop : ScriptableObject
    {
        public const int Width = 640, Height = 360;

        public string DisplayName;
        [Tooltip("Far → near. Each layer is Width×Height, point-filtered; all but the first may contain transparency.")]
        public Texture2D[] Layers;
        [Tooltip("Euler angles (pitch, yaw) of the key light as seen from the battle camera.")]
        public Vector2 LightPitchYaw = new Vector2(30f, 50f);
        public Color LightColor = Color.white;
        public float LightIntensity = 2f;

        public Quaternion LightRotation => Quaternion.Euler(LightPitchYaw.x, LightPitchYaw.y, 0f);
    }
}
