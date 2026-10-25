using UnityEngine;
using UnityEngine.Rendering;

namespace ThreeKingdoms.Gameplay
{
    // Ink-wash art trial (Bohai_InkTrial scene, user 2026-10-25): flips the trial patch between the probe's original look,
    // the ink-wash painting look so the two can be compared on the spot.
    // V key or the gamepad's left shoulder cycles; -ink 0|1|2 picks one at launch (screenshots). Trial tool, not shipping UI.
    public sealed class InkTrialSwitcher : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Look
        {
            public string Name;
            public int Renderer;   // index in the pipeline's renderer list (the ink look draws through the ink-wash renderer)
            public VolumeProfile High, Low;
            public Color Fog, Sky, Equator, Ground, Sun;
            public float FogDensity, SunIntensity, ShadowStrength;
            public Vector3 SunEuler;
        }

        public Renderer[] Renderers;
        // One material per renderer per look, flattened: [look * Renderers.Length + i].
        public Material[] Materials;
        public Look[] Looks;
        public Volume Volume;
        public Light Sun;

        public int Current { get; private set; } = -1;
        static GUIStyle style, shadow;

        void Start()
        {
            int start = Looks.Length - 1;   // the ink look first
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-ink");
            if (i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out int pick)) start = Mathf.Clamp(pick, 0, Looks.Length - 1);
            Apply(start);
        }

        void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if ((kb != null && kb.vKey.wasPressedThisFrame) || (pad != null && pad.leftShoulder.wasPressedThisFrame))
                Apply((Current + 1) % Looks.Length);
        }

        public void Apply(int look)
        {
            Current = look;
            int n = Renderers.Length;
            for (int i = 0; i < n; i++)
                if (Renderers[i]) Renderers[i].sharedMaterial = Materials[look * n + i];
            var l = Looks[look];
            bool low = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-hd2dLow") >= 0;
            if (Volume) Volume.sharedProfile = low ? l.Low : l.High;
            RenderSettings.fogColor = l.Fog;
            RenderSettings.fogDensity = l.FogDensity;
            RenderSettings.ambientSkyColor = l.Sky;
            RenderSettings.ambientEquatorColor = l.Equator;
            RenderSettings.ambientGroundColor = l.Ground;
            if (Camera.main)
            {
                Camera.main.backgroundColor = l.Fog;
                var data = Camera.main.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                if (data) data.SetRenderer(l.Renderer);
            }
            if (Sun)
            {
                Sun.color = l.Sun; Sun.intensity = l.SunIntensity; Sun.shadowStrength = l.ShadowStrength;
                Sun.transform.rotation = Quaternion.Euler(l.SunEuler);
            }
        }

        void OnGUI()
        {
            if (Current < 0) return;
            int size = Mathf.RoundToInt(22f * Screen.height / 1080f);
            if (style == null || style.fontSize != size)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = TextAnchor.LowerLeft, fontStyle = FontStyle.Bold };
                style.normal.textColor = new Color(0.97f, 0.96f, 0.92f);
                shadow = new GUIStyle(style);
                shadow.normal.textColor = new Color(0.08f, 0.08f, 0.08f, 0.9f);
            }
            string text = $"美术试验：{Looks[Current].Name}　（V 键 / LB 切换）";
            var rect = new Rect(24f, Screen.height - size * 2.4f, 1200f, size * 2f);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, shadow);
            GUI.Label(rect, text, style);
        }
    }
}
