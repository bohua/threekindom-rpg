using UnityEngine;
using UnityEngine.Rendering;

namespace ThreeKingdoms.App
{
    // ADR-0005 quality tiers: High uses the full post-processing profile, Low drops depth of field
    // and softens bloom. Until the settings UI exists, Low is selected with the -hd2dLow launch switch.
    public sealed class HD2DQualityTier : MonoBehaviour
    {
        public Volume Volume;
        public VolumeProfile High;
        public VolumeProfile Low;

        public bool IsLow { get; private set; }

        void Awake() => Apply(LaunchArgs.Has("-hd2dLow"));

        public void Apply(bool low)
        {
            IsLow = low;
            if (Volume) Volume.sharedProfile = low ? Low : High;
        }
    }
}
