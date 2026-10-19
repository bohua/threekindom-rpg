using UnityEngine;

namespace ThreeKingdoms.Gameplay
{
    // Applies a backdrop's light metadata to the battle key light, so the lit character quads always match
    // the light painted into the 2D layers (ADR-0005 §8.1).
    public sealed class BattleStage : MonoBehaviour
    {
        public BattleBackdrop Backdrop;
        public Light KeyLight;

        void Awake() => Apply();

        public void Apply()
        {
            if (!Backdrop || !KeyLight) return;
            KeyLight.type = LightType.Directional;
            KeyLight.transform.rotation = Backdrop.LightRotation;
            KeyLight.color = Backdrop.LightColor;
            KeyLight.intensity = Backdrop.LightIntensity;
        }
    }
}
