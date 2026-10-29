using System;
using System.Collections;
using ThreeKingdoms.Gameplay.Story;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ThreeKingdoms.UI.Save
{
    // The clouds the title goes through into the game (user 2026-10-28: new game or load, a cloud opens and the game
    // begins; any loading gets an animation). Codex design art/title-cloud-transition: four cloud curtains roll in from
    // the edges (600 ms), a full cloud ground covers the screen with the ink sun turning while the scene loads in the
    // background (at least 500 ms), then the curtains part from the middle (800 ms) over the new scene. It lives in its
    // own UI document above every panel and survives the scene change; nothing on it takes input.
    public sealed class CloudCurtain : MonoBehaviour
    {
        const float CloseSeconds = 0.6f, MinHold = 0.5f, OpenSeconds = 0.8f, SunFps = 8f;

        // Curtain rects in the design's 1920×1080 pixels: off screen and closed (DESIGN_NOTES, top-left pivots).
        struct Curtain { public VisualElement e; public Vector2 off, on; public Vector2 size; public float offScale, onScale; }

        static CloudCurtain current;
        static readonly System.Collections.Generic.Dictionary<string, Texture2D> art = new System.Collections.Generic.Dictionary<string, Texture2D>();
        static readonly string[] Names = { "cloud_left", "cloud_right", "cloud_top", "cloud_bottom", "cloud_fill", "loading_sheet" };
        readonly Curtain[] curtains = new Curtain[4];
        VisualElement root, fill, sun;
        Texture2D sunSheet;
        AsyncOperation pending;
        string requested;
        float opening, closing;
        bool holding;

        public static bool Busy => current;

        // Rolls the clouds in over the title, runs `then` under them (it starts the scene load), then waits for the
        // new scene and parts the clouds. When `then` loads nothing (a refused save), the clouds part over the title.
        // The cloud art is large; the title loads it when it opens so the clouds start moving at once.
        public static void Preload()
        {
            foreach (var n in Names) Art(n);
        }

        static Texture2D Art(string name)
        {
            if (!art.TryGetValue(name, out var t) || !t) art[name] = t = Resources.Load<Texture2D>("TitleMenu/Clouds/" + name);
            return t;
        }

        public static void Play(PanelSettings panel, Action then)
        {
            if (current) Destroy(current.gameObject);
            var go = new GameObject("Cloud curtain") { hideFlags = HideFlags.DontSave };
            go.SetActive(false);
            DontDestroyOnLoad(go);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            doc.sortingOrder = 1000;
            current = go.AddComponent<CloudCurtain>();
            go.SetActive(true);
            current.StartCoroutine(current.Run(then));
        }

        void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            root.style.position = Position.Absolute;
            root.style.left = root.style.top = 0; root.style.width = 1920; root.style.height = 1080;
            curtains[0] = Make("cloud_left", new Vector2(-1480, -110), new Vector2(0, -338), 1f, 1.35f);
            curtains[1] = Make("cloud_right", new Vector2(2000, -110), new Vector2(30, -338), 1f, 1.35f);
            curtains[2] = Make("cloud_top", new Vector2(-240, -760), new Vector2(-240, 0), 1f, 1f);
            curtains[3] = Make("cloud_bottom", new Vector2(-240, 1140), new Vector2(-240, 380), 1f, 1f);
            fill = Layer("cloud_fill", 0, 0, 1920, 1080);
            fill.style.opacity = 0;
            sunSheet = Art("loading_sheet");
            sun = new VisualElement { pickingMode = PickingMode.Ignore };
            sun.style.position = Position.Absolute; sun.style.left = 888; sun.style.top = 468; sun.style.width = 144; sun.style.height = 144;
            sun.style.overflow = Overflow.Hidden; sun.style.opacity = 0;
            if (sunSheet)
            {
                var strip = new VisualElement { name = "strip", pickingMode = PickingMode.Ignore };
                strip.style.position = Position.Absolute; strip.style.top = 0; strip.style.height = 144; strip.style.width = 144 * 8;
                strip.style.backgroundImage = sunSheet;
                sun.Add(strip);
            }
            root.Add(sun);
            Set(0f);
        }

        Curtain Make(string art, Vector2 off, Vector2 on, float offScale, float onScale)
        {
            var tex = Art(art);
            var size = tex ? new Vector2(tex.width, tex.height) : new Vector2(1400, 1300);
            var e = Layer(art, off.x, off.y, size.x, size.y);
            return new Curtain { e = e, off = off, on = on, size = size, offScale = offScale, onScale = onScale };
        }

        VisualElement Layer(string art, float x, float y, float w, float h)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute;
            e.style.left = x; e.style.top = y; e.style.width = w; e.style.height = h;
            var tex = Art(art);
            if (tex) e.style.backgroundImage = tex;
            else e.style.backgroundColor = new Color(0.97f, 0.98f, 1f);
            root.Add(e);
            return e;
        }

        // 0 = curtains off screen, 1 = closed.
        void Set(float k)
        {
            foreach (var c in curtains)
            {
                var p = Vector2.Lerp(c.off, c.on, k);
                float s = Mathf.Lerp(c.offScale, c.onScale, k);
                c.e.style.left = p.x; c.e.style.top = p.y;
                c.e.style.width = c.size.x * s; c.e.style.height = c.size.y * s;
            }
        }

        static float Ease(float t) => (1f - Mathf.Cos(Mathf.PI * Mathf.Clamp01(t))) * 0.5f;   // easeInOutSine

        IEnumerator Run(Action then)
        {
            // Close: the curtains roll in; the cloud ground fades in over the last 120 ms.
            for (float t = 0; t < CloseSeconds; t += Step())
            {
                closing = t / CloseSeconds;
                Set(Ease(t / CloseSeconds));
                fill.style.opacity = Mathf.Clamp01((t - (CloseSeconds - 0.12f)) / 0.12f);
                yield return null;
            }
            Set(1f); fill.style.opacity = 1f; closing = 1f; holding = true;

            // Under full cover: start the load in the background and turn the ink sun until it is ready.
            var priority = Application.backgroundLoadingPriority;
            Application.backgroundLoadingPriority = ThreadPriority.High;   // the clouds cover the screen: load at full speed
            // In the editor an asynchronous load of a big scene crawls (20 s and more for Zhuojun in the PlayMode tests), so
            // there it loads at once under the clouds; a player build loads in the background while the sun turns.
            GameRun.SceneLoadOverride = scene =>
            {
                requested = scene;
                if (Application.isEditor) SceneManager.LoadScene(scene);
                else pending = SceneManager.LoadSceneAsync(scene);
            };
            try { then?.Invoke(); }
            finally { GameRun.SceneLoadOverride = null; }

            float held = 0, sunT = 0;
            while (held < MinHold || (pending != null && !pending.isDone))
            {
                float dt = Step();
                held += dt; sunT += dt;
                sun.style.opacity = Mathf.Clamp01(held / 0.15f);
                var strip = sun.Q("strip");
                if (strip != null) strip.style.left = -144 * (Mathf.FloorToInt(sunT * SunFps) % 8);
                yield return null;
            }
            Application.backgroundLoadingPriority = priority;
            holding = false;
            if (requested != null) { Debug.Log($"CloudCurtain: {requested} ready after {held:0.00} s under the clouds"); yield return null; yield return null; }   // let the new scene draw its first frames

            // Open: the sun goes first, then the cloud ground, the curtains part along their way in.
            for (float t = 0; t < OpenSeconds; t += Step())
            {
                opening = t / OpenSeconds;
                Set(1f - Ease(t / OpenSeconds));
                sun.style.opacity = 1f - Mathf.Clamp01(t / 0.08f);
                fill.style.opacity = 1f - Mathf.Clamp01(t / 0.16f);
                root.style.opacity = 1f - Mathf.Clamp01((t - (OpenSeconds - 0.12f)) / 0.12f);
                yield return null;
            }
            current = null;
            Destroy(gameObject);
        }

        static float Step() => Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        // The scene the curtain is loading, or null; how far the clouds have parted (0–1). Tests and the screenshot run.
        public static string Loading => current ? current.requested : null;
        public static float Opening => current ? current.opening : 1f;
        public static float Closing => current ? current.closing : 0f;
        public static bool Holding => current && current.holding;

        // Removes a curtain still on screen (tests that end before the clouds have parted).
        public static void Clear()
        {
            if (current) Destroy(current.gameObject);
            current = null;
            GameRun.SceneLoadOverride = null;
        }

        // Runs a coroutine that must outlive the scene that started it (the title's screenshot run).
        public static void Persist(IEnumerator routine)
        {
            var go = new GameObject("Curtain runner") { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<Runner>().StartCoroutine(routine);
        }

        sealed class Runner : MonoBehaviour { }
    }
}
