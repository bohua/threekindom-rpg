using UnityEngine;

namespace ThreeKingdoms.UI.Save
{
    // The white that the title's start flash ends on, lifted in the next scene (600 ms easeOutCubic). IMGUI draws it
    // over every UI so it also covers the new scene's own menus while they appear.
    public sealed class TitleWhiteFade : MonoBehaviour
    {
        static TitleWhiteFade current;
        static Texture2D white;
        float hold = 0.1f, fade, seconds = 0.6f;

        public static void Begin(float fadeSeconds)
        {
            if (current) Destroy(current.gameObject);
            var go = new GameObject("Title white fade") { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(go);
            current = go.AddComponent<TitleWhiteFade>();
            current.seconds = fadeSeconds;
        }

        public static void Cancel() { if (current) Destroy(current.gameObject); }

        void Update()
        {
            if (hold > 0f) { hold -= Mathf.Min(Time.unscaledDeltaTime, 0.05f); return; }   // the first frames of the new scene
            fade += Mathf.Min(Time.unscaledDeltaTime, 0.05f) / seconds;
            if (fade >= 1f) Destroy(gameObject);
        }

        void OnGUI()
        {
            if (!white) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
            GUI.depth = -1000;
            var c = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 1f - (1f - Mathf.Pow(1f - Mathf.Clamp01(fade), 3f)));
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white);
            GUI.color = c;
        }
    }
}
