using UnityEngine;

namespace ThreeKingdoms.Gameplay
{
    // A floating name over a whitebox landmark (chapter 1 art probe): which grey box is the levee, the ferry, the post
    // station. Drawn with IMGUI so it needs no font asset; hidden with -noLabels or the L key. Not shipping UI.
    public sealed class WhiteboxLabel : MonoBehaviour
    {
        public string Text;
        public float Height = 6f;
        public float MaxDistance = 220f;

        public static bool Hidden;
        static GUIStyle style, shadow;
        static int lastToggleFrame = -1;

        void Awake() { if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-noLabels") >= 0) Hidden = true; }

        void Update()
        {
            if (lastToggleFrame == Time.frameCount) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.lKey.wasPressedThisFrame) { Hidden = !Hidden; lastToggleFrame = Time.frameCount; }
        }

        void OnGUI()
        {
            if (Hidden || string.IsNullOrEmpty(Text)) return;
            var cam = Camera.main;
            if (!cam) return;
            Vector3 sp = cam.WorldToScreenPoint(transform.position + Vector3.up * Height);
            if (sp.z <= 0f || sp.z > MaxDistance) return;
            int size = Mathf.RoundToInt(22f * Screen.height / 1080f);
            if (style == null || style.fontSize != size)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                style.normal.textColor = new Color(1f, 0.97f, 0.88f);
                shadow = new GUIStyle(style);
                shadow.normal.textColor = new Color(0.1f, 0.08f, 0.06f, 0.9f);
            }
            var rect = new Rect(sp.x - 200f, Screen.height - sp.y - size, 400f, size * 2f);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), Text, shadow);
            GUI.Label(rect, Text, style);
        }
    }
}
