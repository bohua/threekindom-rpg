using System;
using System.Collections;
using System.Collections.Generic;
using ThreeKingdoms.Gameplay.Story;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ThreeKingdoms.UI.Story
{
    // The prologue 天下大势 (user 2026-10-27, design/gdd/prologue.md), 0.2 static version: three ink scrolls panned right
    // to left, the world map with its layers faded in one by one (Ju Lu's light, the preaching routes, eight provinces
    // under yellow banners, Luo Yang's alert ring, three arrows, the recruitment notices), pins of who stood where in
    // 184, a push in to Zhuo commandery and the three heroes' cards, then the notice board at Zhuo's gate fading into
    // the live town. Subtitles carry the narration. Confirm or cancel asks "跳过序章？" at any time.
    // Art is read from Resources/Prologue (Codex art/prologue-art); a missing picture is drawn as a plain panel, so the
    // flow works before the art lands. Time runs unscaled and the town keeps running underneath (it is held Busy).
    public sealed partial class StoryCinematicsView
    {
        const string PrologueArt = "Prologue/";
        // Narration, one clip per subtitle line: Resources/Audio/Voice/Prologue/prologue_<shot>_<line> (1-based), and
        // prologue_c_<shot>_<line> for the 演义体 lines. A line
        // stays up at least as long as its clip; a missing clip leaves that line silent at reading pace.
        const string PrologueVoice = "Audio/Voice/Prologue/prologue_";
        const float VoiceTail = 0.25f;   // the music stays at full level under the narration (user 2026-10-28)
        AudioSource voice;
        const float CharsPerSecond = 11f, MinLine = 1.6f, LineGap = 0.12f, ShotLead = 0.6f, ShotTail = 0.4f;   // brisk (user 2026-10-28: far too slow)
        static readonly Color Night = new Color(0.063f, 0.11f, 0.19f);       // #101C30
        static readonly Color PanelBlue = new Color(0.125f, 0.208f, 0.29f);      // #20354A
        static readonly Color Ivory = new Color(0.957f, 0.945f, 0.91f);      // #F4F1E8
        static readonly Color Mist = new Color(0.71f, 0.784f, 0.835f);       // #B5C8D5
        static readonly Color SilverEdge = new Color(0.549f, 0.655f, 0.729f); // #8CA7BA
        static readonly Color SelectBg = new Color(0.086f, 0.294f, 0.369f);  // #164B5E
        static readonly Color Cyan = new Color(0.384f, 0.843f, 0.91f);       // #62D7E8

        // The map layers in the order they come up.
        static readonly string[] MapLayers = { "map_julu", "map_spread", "map_recruit" };   // the held land, ring and arrows are drawn by PrologueMapFx

        VisualElement proRoot, proScroll, proMap, proMapInner, proScene, proSubBar, proSkip, proBlack;
        Label proSub, proHeader, proHint;
        Label[] skipButtons;
        readonly Dictionary<string, VisualElement> proLayers = new Dictionary<string, VisualElement>();
        readonly List<VisualElement> proYellowPins = new List<VisualElement>(), proHeroPins = new List<VisualElement>();
        PrologueMapFx mapFx;
        PrologueDiorama diorama;
        VisualElement veil;
        Texture2D flatMap;
        readonly Dictionary<VisualElement, (Vector2 at, float dx, float dy)> pinPlaces = new Dictionary<VisualElement, (Vector2, float, float)>();
        Dictionary<string, Vector2> anchors;
        Texture2D[] scrollArt;
        int skipChoice;   // 0 是, 1 否
        bool skipByPointer;

        // Tests play it fast; 1 is the real pace.
        [NonSerialized] public float PrologueSpeed = 1f;
        // Read by tests: the shot now playing (0-based, -1 outside), and whether the skip question is up.
        public int PrologueShot { get; private set; } = -1;
        public bool SkipAsked => proSkip != null && proSkip.style.display == DisplayStyle.Flex;

        public IEnumerator PlayPrologue()
        {
            Stage = "prologue";
            PrologueShot = -1;
            skipByPointer = false;
            bool classic = ThreeKingdoms.UI.Menu.GameSettings.ClassicNarration;
            BuildPrologue();
            UseDiorama(PrologueDiorama.TryCreate());
            StoryMusic.Play(StoryMusic.PrologueCue, fadeIn: 1.2f);
            bool skipped = false;
            try
            {
                proRoot.style.display = DisplayStyle.Flex;
                SetOpacity(proRoot, 1);
                var shots = PrologueText.Shots;
                for (int s = 0; s < shots.Length && !skipped; s++)
                {
                    PrologueShot = s;
                    proHeader.text = $"{PrologueText.Title}   │   {s + 1} / {shots.Length}";
                    var lines = PrologueText.LinesOf(s, classic);
                    var starts = new float[lines.Length];
                    var spans = new float[lines.Length];
                    var clips = new AudioClip[lines.Length];
                    float t0 = ShotLead;
                    for (int i = 0; i < lines.Length; i++)
                    {
                        clips[i] = Resources.Load<AudioClip>($"{PrologueVoice}{(classic ? "c_" : "")}{s + 1}_{i + 1}");
                        spans[i] = Mathf.Max(LineTime(lines[i]), clips[i] ? clips[i].length + VoiceTail : 0f);
                        starts[i] = t0; t0 += spans[i] + LineGap;
                    }
                    int spoken = -1;
                    starts1 = starts.Length > 1 ? starts[1] : 0f;
                    float length = t0 + ShotTail;
                    for (float t = 0; t < length; )
                    {
                        if (StoryCinematics.SkipPrologue || skipByPointer) { skipped = true; break; }
                        if (SkipAsked)
                        {
                            int answer = ReadSkipInput();
                            if (answer == 0) { skipped = true; break; }
                            if (answer == 1) { proSkip.style.display = DisplayStyle.None; if (voice) voice.UnPause(); }
                        }
                        else
                        {
                            if (ConfirmPressed() || CancelPressed()) { AskSkip(); if (voice) voice.Pause(); }
                            else t += Time.unscaledDeltaTime * PrologueSpeed;
                        }
                        int line = -1;
                        for (int i = 0; i < starts.Length; i++) if (t >= starts[i]) line = i;
                        if (line != spoken) { spoken = line; Speak(line >= 0 ? clips[line] : null); }
                        ApplyShot(s, Mathf.Min(t, length), length, line, line >= 0 ? t - starts[line] : 0f);
                        ShowLine(line >= 0 ? lines[line] : null, line >= 0 ? t - starts[line] : 0f, line >= 0 ? spans[line] : 1f);
                        if (line >= 0 && t - starts[line] > spans[line] * 0.6f) ShotForReview($"prologue-{s + 1}-{line + 1}");
                        yield return null;
                    }
                }
                // Into the town: the scene picture fades away over the live street (also after a skip).
                proSkip.style.display = DisplayStyle.None;
                proSubBar.style.display = DisplayStyle.None;
                Speak(null);
                // Zhuo carries the piece on as its own music; elsewhere it fades out.
                if (StoryMusic.Background == StoryMusic.PrologueCue) StoryMusic.Loop(); else StoryMusic.Stop(1.2f);
                float from = proRoot.resolvedStyle.opacity;
                yield return Tween(skipped ? 0.6f : 1.4f, k => SetOpacity(proRoot, from * (1f - k)));
                if (ReviewDir() != null) { proRoot.style.display = DisplayStyle.None; yield return new WaitForSecondsRealtime(1f); ShotForReview("prologue-landed"); }
            }
            finally
            {
                if (StoryMusic.Current == StoryMusic.PrologueCue && StoryMusic.Background != StoryMusic.PrologueCue) StoryMusic.Stop(0.4f);
                if (voice) voice.Stop();
                UseDiorama(null);
                if (proRoot != null) proRoot.style.display = DisplayStyle.None;
                PrologueShot = -1;
                Stage = null;
            }
        }

        // -prologueShots <dir>: one frame per subtitle line (and the skip question once) for review.
        readonly HashSet<string> reviewShots = new HashSet<string>();

        void ShotForReview(string key)
        {
            string dir = ReviewDir();
            if (dir == null || !reviewShots.Add(key)) return;
            System.IO.Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, key + ".png"));
            if (key == "prologue-4-1") StartCoroutine(SkipQuestionShot(dir));
        }

        IEnumerator SkipQuestionShot(string dir)
        {
            AskSkip();
            yield return new WaitForSecondsRealtime(0.3f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "prologue-skip.png"));
            yield return new WaitForSecondsRealtime(0.3f);
            proSkip.style.display = DisplayStyle.None;
        }

        static string ReviewDir()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-prologueShots");
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        // Plays one line's narration (null stops it). Tests at a faster pace stay silent.
        void Speak(AudioClip clip)
        {
            if (!voice)
            {
                voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.ignoreListenerPause = true;
            }
            voice.Stop();
            if (!clip || PrologueSpeed > 1.01f) return;
            voice.clip = clip;
            voice.Play();
        }

        static float LineTime(string line) => Mathf.Max(MinLine, line.Length / CharsPerSecond + 1.0f);

        // ---------- what each shot shows at time t ----------

        float starts1;   // when the current shot's second line begins (shot 4 starts the spreading there)

        void ApplyShot(int s, float t, float length, int line, float lineT)
        {
            float k = length > 0 ? t / length : 1f;
            switch (s)
            {
                case 0:
                    // Black, then the scroll unrolls from the right edge and drifts right to left.
                    SetOpacity(proBlack, 1f - Mathf.Clamp01(t / 1.2f));
                    ShowScroll(0, k, Mathf.Clamp01(t / 2.6f));
                    break;
                case 1:
                    ShowScroll(1, k, 1f);
                    SetOpacity(proScroll, Mathf.Clamp01(t / 0.8f));
                    break;
                case 2:
                    ShowScroll(2, k, 1f);
                    // The paper turns into the map in the last three seconds.
                    float m = Mathf.Clamp01((t - (length - 3f)) / 3f);
                    SetOpacity(proScroll, 1f - m * 0.85f);
                    Visible(proMap, m > 0f); SetOpacity(proMap, m);
                    break;
                case 3:
                    // Ju Lu lights up, the three Zhang appear, and the preaching spreads out in the last lines.
                    MapOnly();
                    Layer("map_julu", Mathf.Clamp01(t / 1.5f) * (0.85f + 0.15f * Mathf.Sin(t * 3f)));
                    for (int i = 0; i < proYellowPins.Count; i++) Pin(proYellowPins[i], Mathf.Clamp01((t - 1.8f - i * 0.9f) / 0.5f));
                    Layer("map_spread", Mathf.Clamp01(line - 1 + lineT / 2f));
                    mapFx.Spread(line >= 1 ? 0.28f * Mathf.Clamp01((t - starts1) / Mathf.Max(1f, length - starts1)) : 0f, t);
                    break;
                case 4:
                    // The rising: banners go up and the held land creeps out, deepening and burning at its edge; then
                    // one stroke of cinnabar rings Luo Yang.
                    MapOnly();
                    Layer("map_julu", 1f); Layer("map_spread", 1f - 0.5f * Mathf.Clamp01(k / 0.3f));
                    foreach (var p in proYellowPins) Pin(p, 1f - Mathf.Clamp01(t / 0.8f));
                    mapFx.Spread(Mathf.Lerp(0.28f, 1.15f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(1f, length * 0.8f)))), t);
                    mapFx.Ring(line >= 1 ? Mathf.Clamp01(lineT / 1.4f) : 0f, t);
                    break;
                case 5:
                    // Three thick arrows draw themselves out of Luo Yang; the recruitment notices come up after.
                    MapOnly();
                    Layer("map_julu", 1f); Layer("map_spread", 0.5f);
                    mapFx.Spread(1.2f, t); mapFx.Ring(1f, t);
                    // On the 3D map the arrows wait until the camera has flown out of the pass and risen over Luo Yang.
                    float arrowsFrom = diorama != null ? length * 0.5f : 0.6f;
                    float march = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - arrowsFrom) / 3.2f));
                    mapFx.Arrows(march, t);
                    // The three armies march behind the drawing arrows, their generals riding above (user 2026-10-29).
                    mapFx.Armies(Mathf.Clamp01((t - arrowsFrom) / Mathf.Max(3.6f, length - arrowsFrom - 0.4f)), t, 1f);
                    Layer("map_recruit", line >= 1 ? Mathf.Clamp01(lineT / 1.5f) : 0f);
                    break;
                case 6:
                    MapOnly();
                    Layer("map_julu", 1f); Layer("map_spread", 0.5f);
                    mapFx.Spread(1.2f, t); mapFx.Ring(1f, t); mapFx.Arrows(1f, t);
                    mapFx.Armies(1f, t, 1f - Mathf.Clamp01(t / 0.8f));
                    // The armies and notices step back while the pins speak.
                    float back = 1f - 0.55f * Mathf.Clamp01(t / 1.2f);
                    mapFx.SetOpacity(back, back, back);
                    Layer("map_recruit", back);
                    float per = Mathf.Max(0.6f, (length - ShotLead - ShotTail) / (proHeroPins.Count + 1));
                    for (int i = 0; i < proHeroPins.Count; i++) Pin(proHeroPins[i], Mathf.Clamp01((t - ShotLead * 0.5f - i * per) / 0.5f));
                    break;
                case 7:
                    // "And our story begins with …": the map pushes north-east from Luo Yang to Zhuo.
                    Visible(proScroll, false);
                    Visible(proMap, true);
                    foreach (var p in proHeroPins) Pin(p, 1f - Mathf.Clamp01(t / 0.6f));
                    mapFx.Spread(1.2f, t);
                    if (diorama == null) ZoomMap(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(2f, length * 0.85f))));
                    break;
                case 8:
                    // The notice board at the gate fades in and slowly closes on the young man's back.
                    Visible(proScene, true);
                    float sc = Mathf.Clamp01((t - 0.2f) / 1.4f);
                    SetOpacity(proScene, sc);
                    SetOpacity(proMap, 1f - sc);
                    float zoom = 1f + 0.07f * Ease(k);
                    proScene.style.scale = new Scale(new Vector3(zoom, zoom, 1));
                    if (sc >= 1f) Visible(proMap, false);
                    break;
            }
            if (diorama != null && s >= 2)
            {
                var pose = DioramaPose(s, t, length, k);
                diorama.View(pose.Position, pose.LookAt, pose.Fov);
                foreach (var pin in proYellowPins) FollowPin(pin);
                foreach (var pin in proHeroPins) FollowPin(pin);
                mapFx.Sync();
            }
        }

        // ---------- the 3D map ----------

        // On (a diorama) or off (null): the map panel shows the diorama's camera instead of the flat map.
        void UseDiorama(PrologueDiorama d)
        {
            if (diorama != null && diorama != d) diorama.Dispose();
            diorama = d;
            mapFx.Diorama = d;
            proMapInner.style.backgroundImage = d != null ? new StyleBackground(Background.FromRenderTexture((RenderTexture)d.Image)) : new StyleBackground(flatMap);
            veil.style.display = d != null ? DisplayStyle.None : DisplayStyle.Flex;
        }

        // A pin stands where its city falls on screen, shifted by its offset; hidden while the city is out of view.
        void FollowPin(VisualElement pin)
        {
            if (!pinPlaces.TryGetValue(pin, out var place)) return;
            var at = diorama.Project(place.at, 1.2f);
            if (at == null) { pin.style.visibility = Visibility.Hidden; return; }
            pin.style.visibility = Visibility.Visible;
            pin.style.left = Length.Percent(at.Value.x * 100f + place.dx / 19.2f);
            pin.style.top = Length.Percent(at.Value.y * 100f + place.dy / 10.8f);
        }

        // The camera over the diorama for each map shot: the whole land, down to Ju Lu, up over the rising, the
        // Taihang pass toward Luo Yang (route A) while the armies march, the whole land for the people, then north
        // to Zhuo (route C).
        PrologueDiorama.Pose DioramaPose(int s, float t, float length, float k)
        {
            var overview = new PrologueDiorama.Pose(new Vector3(60f, 64f, -24f), new Vector3(60f, 0f, 35f), 45f);
            var julu = PrologueDiorama.ToWorld(Anchor("julu"), 0.6f);
            var juluClose = new PrologueDiorama.Pose(julu + new Vector3(0f, 14f, -18f), julu, 45f);
            var east = new PrologueDiorama.Pose(new Vector3(64f, 36f, 6f), new Vector3(62f, 0f, 42f), 45f);
            var a = PrologueDiorama.RouteOf("A");
            var c = PrologueDiorama.RouteOf("C");
            var aStart = a != null ? PrologueDiorama.Along(a, 0f) : east;
            var aEnd = a != null ? PrologueDiorama.Along(a, 1f) : east;
            var luoyangAt = PrologueDiorama.ToWorld(Anchor("luoyang"), 0.6f);
            var luoyangHigh = new PrologueDiorama.Pose(luoyangAt + new Vector3(-6f, 24f, -20f), luoyangAt + new Vector3(6f, 0f, 2f), 45f);
            var cStart = c != null ? PrologueDiorama.Along(c, 0f) : overview;
            float sk = Mathf.SmoothStep(0f, 1f, k);
            switch (s)
            {
                case 2: return overview;
                case 3: return PrologueDiorama.Pose.Lerp(overview, juluClose, sk);
                case 4: return PrologueDiorama.Pose.Lerp(juluClose, east, sk);
                case 5:
                    // Low out of the Taihang pass toward Luo Yang (route A), then up over the city to watch the arrows.
                    if (k < 0.12f) return PrologueDiorama.Pose.Lerp(east, aStart, Mathf.SmoothStep(0f, 1f, k / 0.12f));
                    if (k < 0.45f) return a != null ? PrologueDiorama.Along(a, (k - 0.12f) / 0.33f) : east;
                    return PrologueDiorama.Pose.Lerp(aEnd, luoyangHigh, Mathf.SmoothStep(0f, 1f, (k - 0.45f) / 0.2f));
                case 6: return PrologueDiorama.Pose.Lerp(luoyangHigh, overview, Mathf.SmoothStep(0f, 1f, t / 1.6f));
                case 7:
                    if (k < 0.15f) return PrologueDiorama.Pose.Lerp(overview, cStart, Mathf.SmoothStep(0f, 1f, k / 0.15f));
                    return c != null ? PrologueDiorama.Along(c, (k - 0.15f) / 0.85f) : overview;
                default: return c != null ? PrologueDiorama.Along(c, 1f) : overview;
            }
        }

        void ShowScroll(int index, float k, float unrolled)
        {
            Visible(proScroll, true);
            var tex = scrollArt[index];
            proScroll.style.backgroundImage = tex;
            proScroll.style.backgroundColor = tex ? Color.clear : new Color(0.86f, 0.82f, 0.72f);
            // The painting is twice the screen wide (3840×1080): it starts on its right half and drifts to its left.
            float aspect = tex ? (float)tex.width / tex.height : 32f / 9f;
            float widthPct = 100f * aspect / (16f / 9f);
            proScroll.style.width = new Length(widthPct, LengthUnit.Percent);
            float travel = widthPct - 100f;
            proScroll.style.left = new Length(-travel * (1f - Mathf.SmoothStep(0f, 1f, k)), LengthUnit.Percent);   // an even drift, slow at both ends
            // Unrolling from the right: a black curtain over what is not yet open.
            proBlack.style.display = DisplayStyle.Flex;
            proBlack.style.right = new Length(100f * unrolled, LengthUnit.Percent);
            if (unrolled >= 1f && index > 0) proBlack.style.display = DisplayStyle.None;
        }

        void MapOnly()
        {
            Visible(proScroll, false);
            proBlack.style.display = DisplayStyle.None;
            Visible(proMap, true);
            SetOpacity(proMap, 1f);
        }

        void Layer(string id, float a)
        {
            if (!proLayers.TryGetValue(id, out var v)) return;
            a = Mathf.Clamp01(a);
            if (diorama != null) { diorama.Overlay(id, (Texture)v.userData, a); a = 0f; }
            v.style.display = a > 0f ? DisplayStyle.Flex : DisplayStyle.None;
            v.style.opacity = a;
        }

        static void Pin(VisualElement pin, float a)
        {
            a = Mathf.Clamp01(a);
            pin.style.display = a > 0f ? DisplayStyle.Flex : DisplayStyle.None;
            pin.style.opacity = a;
            float s = 0.7f + 0.3f * Ease(a);
            pin.style.scale = new Scale(new Vector3(s, s, 1));
        }

        // The map scaled about Zhuo so it ends near the middle of the screen.
        void ZoomMap(float k)
        {
            var at = Anchor("zhuojun");
            float s = Mathf.Lerp(1f, 2.6f, k);
            proMapInner.style.transformOrigin = new TransformOrigin(Length.Percent(at.x * 100f), Length.Percent(at.y * 100f));
            proMapInner.style.scale = new Scale(new Vector3(s, s, 1));
            proMapInner.style.translate = new Translate(Length.Percent((0.5f - at.x) * 100f * k), Length.Percent((0.42f - at.y) * 100f * k));
        }

        void ShowLine(string text, float t, float span)
        {
            if (string.IsNullOrEmpty(text)) { SetOpacity(proSub, 0); return; }
            proSubBar.style.display = DisplayStyle.Flex;
            if (proSub.text != text) proSub.text = text;
            SetOpacity(proSub, Mathf.Clamp01(t / 0.3f) * Mathf.Clamp01((span - t) / 0.3f + 0.15f));
        }

        static void Visible(VisualElement v, bool on) => v.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;

        // ---------- skip ----------

        void AskSkip()
        {
            skipChoice = 1;   // 否 by default
            proSkip.style.display = DisplayStyle.Flex;
            PaintSkip();
        }

        // 0 skip, 1 go on, -1 nothing yet. Left / right (or up / down) move between 是 and 否.
        int ReadSkipInput()
        {
            var k = Keyboard.current;
            var g = Gamepad.current;
            bool move = (k != null && (k.leftArrowKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame
                        || k.aKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame))
                     || (g != null && (g.dpad.left.wasPressedThisFrame || g.dpad.right.wasPressedThisFrame || g.dpad.up.wasPressedThisFrame || g.dpad.down.wasPressedThisFrame));
            if (move) { skipChoice = 1 - skipChoice; PaintSkip(); }
            if (CancelPressed()) return 1;
            if (ConfirmPressed()) return skipChoice;
            return -1;
        }

        void PaintSkip()
        {
            for (int i = 0; i < skipButtons.Length; i++)
            {
                bool sel = i == skipChoice;
                var b = skipButtons[i];
                b.style.backgroundColor = sel ? SelectBg : new Color(Night.r, Night.g, Night.b, 0.9f);
                Border(b, sel ? Cyan : SilverEdge, sel ? 2f : 1f);
                b.text = (sel ? "◆ " : "") + (i == 0 ? PrologueText.SkipYes : PrologueText.SkipNo);
            }
        }

        // Chosen from the skip question by a pointer.
        void ChooseSkip(int choice)
        {
            skipChoice = choice;
            if (choice == 0) skipByPointer = true;
            else proSkip.style.display = DisplayStyle.None;
        }

        // ---------- layout (built on first use) ----------

        void BuildPrologue()
        {
            if (proRoot != null) { ResetPrologue(); return; }
            proRoot = Layer(root);
            proRoot.style.backgroundColor = Color.black;
            proRoot.style.overflow = Overflow.Hidden;

            scrollArt = new[] { Art("scroll_1"), Art("scroll_2"), Art("scroll_3") };
            proScroll = new VisualElement { pickingMode = PickingMode.Ignore };
            proScroll.style.position = Position.Absolute;
            proScroll.style.top = 0; proScroll.style.bottom = 0;
            proScroll.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
            proRoot.Add(proScroll);

            proMap = Layer(proRoot);
            proMapInner = Layer(proMap);
            var baseMap = Art("map_base");
            flatMap = baseMap;
            if (baseMap) proMapInner.style.backgroundImage = baseMap;
            else proMapInner.style.backgroundColor = Night;
            proMapInner.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
            anchors = ReadAnchors();
            // A light veil over the map so the moving marks stand out (user 2026-10-28: the marks were easy to miss).
            veil = Layer(proMapInner);
            veil.style.backgroundColor = new Color(0.03f, 0.05f, 0.09f, 0.22f);
            foreach (var id in MapLayers)
            {
                var tex = Art(id);
                if (!tex) continue;
                var v = Layer(proMapInner);
                v.style.backgroundImage = tex;
                v.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
                v.userData = tex;
                proLayers[id] = v;
            }
            mapFx = new PrologueMapFx(proMapInner, Anchor);
            foreach (var p in PrologueText.YellowTurbans) proYellowPins.Add(MakePin(p, true));
            foreach (var p in PrologueText.Heroes) proHeroPins.Add(MakePin(p, false));


            proScene = Layer(proRoot);
            var scene = Art("zhuojun_notice");
            if (scene) proScene.style.backgroundImage = scene;
            else proScene.style.backgroundColor = new Color(0.55f, 0.6f, 0.62f);
            proScene.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(60));

            proBlack = Layer(proRoot);
            proBlack.style.backgroundColor = Color.black;

            BuildSubtitles();
            BuildSkip();
            ResetPrologue();
        }

        void ResetPrologue()
        {
            proRoot.BringToFront();
            Visible(proScroll, false); Visible(proMap, false); Visible(proScene, false);
            proBlack.style.display = DisplayStyle.Flex; proBlack.style.right = 0; SetOpacity(proBlack, 1);
            SetOpacity(proMap, 0); SetOpacity(proScene, 0); SetOpacity(proScroll, 1);
            proScene.style.scale = new Scale(Vector3.one);
            proMapInner.style.scale = new Scale(Vector3.one);
            proMapInner.style.translate = new Translate(0, 0);
            foreach (var id in MapLayers) Layer(id, 0f);
            foreach (var p in proYellowPins) Pin(p, 0f);
            foreach (var p in proHeroPins) Pin(p, 0f);
            mapFx.Reset(); mapFx.SetOpacity(1f, 1f, 1f);
            proSub.text = ""; SetOpacity(proSub, 0);
            proSubBar.style.display = DisplayStyle.Flex;
            proSkip.style.display = DisplayStyle.None;
        }

        void BuildSubtitles()
        {
            // A soft dark band at the bottom, no heavy frame (art/prologue-ui-concept).
            proSubBar = new VisualElement { pickingMode = PickingMode.Ignore };
            proSubBar.style.position = Position.Absolute;
            proSubBar.style.left = 0; proSubBar.style.right = 0; proSubBar.style.bottom = 0; proSubBar.style.height = 258;
            var band = Art("ui_subtitle_band");
            if (band) { proSubBar.style.backgroundImage = band; proSubBar.style.unityBackgroundScaleMode = ScaleMode.StretchToFill; }
            else proSubBar.style.backgroundColor = new Color(Night.r * 0.5f, Night.g * 0.5f, Night.b * 0.5f, 0.62f);
            proSubBar.style.justifyContent = Justify.Center;
            proSubBar.style.alignItems = Align.Center;
            proRoot.Add(proSubBar);
            proSub = new Label { pickingMode = PickingMode.Ignore };
            proSub.style.fontSize = 36;
            proSub.style.color = Ivory;
            proSub.style.whiteSpace = WhiteSpace.Normal;
            proSub.style.maxWidth = 1500;
            proSub.style.unityTextAlign = TextAnchor.MiddleCenter;
            proSub.style.marginTop = 10;
            proSubBar.Add(proSub);

            proHeader = new Label { pickingMode = PickingMode.Ignore };
            proHeader.style.position = Position.Absolute; proHeader.style.left = 37; proHeader.style.top = 26;
            proHeader.style.fontSize = 22; proHeader.style.color = Mist;
            proHeader.style.backgroundColor = new Color(Night.r, Night.g, Night.b, 0.82f);
            proHeader.style.paddingLeft = proHeader.style.paddingRight = 22; proHeader.style.paddingTop = proHeader.style.paddingBottom = 10;
            proRoot.Add(proHeader);
            proHint = new Label(PrologueText.SkipHint) { pickingMode = PickingMode.Ignore };
            proHint.style.position = Position.Absolute; proHint.style.right = 56; proHint.style.bottom = 30;
            proHint.style.fontSize = 20; proHint.style.color = Mist;
            proRoot.Add(proHint);
        }

        void BuildSkip()
        {
            proSkip = new VisualElement();
            Centre(proSkip, 560, 260);
            proSkip.style.backgroundColor = new Color(PanelBlue.r, PanelBlue.g, PanelBlue.b, 0.96f);
            Border(proSkip, SilverEdge, 1.5f);
            proSkip.style.alignItems = Align.Center;
            proSkip.style.paddingTop = 34;
            var q = new Label(PrologueText.SkipQuestion) { pickingMode = PickingMode.Ignore };
            q.style.fontSize = 36; q.style.color = Ivory;
            proSkip.Add(q);
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row; row.style.marginTop = 30;
            proSkip.Add(row);
            skipButtons = new Label[2];
            for (int i = 0; i < 2; i++)
            {
                var b = new Label();
                b.style.width = 170; b.style.height = 58; b.style.marginLeft = b.style.marginRight = 18;
                b.style.fontSize = 28; b.style.color = Ivory; b.style.unityTextAlign = TextAnchor.MiddleCenter;
                int choice = i;
                b.RegisterCallback<ClickEvent>(_ => ChooseSkip(choice));
                b.RegisterCallback<PointerEnterEvent>(_ => { skipChoice = choice; PaintSkip(); });
                skipButtons[i] = b;
                row.Add(b);
            }
            var note = new Label(PrologueText.SkipNote) { pickingMode = PickingMode.Ignore };
            note.style.fontSize = 19; note.style.color = Mist; note.style.marginTop = 22;
            proSkip.Add(note);
            proRoot.Add(proSkip);
        }

        VisualElement MakePin(PrologueText.Pin pin, bool yellow)
        {
            var at = Anchor(pin.Anchor);
            var v = new VisualElement { pickingMode = PickingMode.Ignore };
            pinPlaces[v] = (at, pin.Dx, pin.Dy);
            v.style.position = Position.Absolute;
            v.style.left = Length.Percent(at.x * 100f + pin.Dx / 19.2f);
            v.style.top = Length.Percent(at.y * 100f + pin.Dy / 10.8f);
            v.style.width = 170; v.style.marginLeft = -85; v.style.marginTop = -66;
            v.style.alignItems = Align.Center;
            if (pin.Dx != 0 || pin.Dy != 0)
            {
                // The pin stands off to one side; a dot (with a faint line) marks the city it belongs to.
                var dot = new VisualElement { pickingMode = PickingMode.Ignore };
                dot.style.position = Position.Absolute;
                dot.style.width = dot.style.height = 14;
                dot.style.left = 85 - pin.Dx - 7; dot.style.top = 66 - pin.Dy - 7;
                dot.style.backgroundColor = yellow ? new Color(0.84f, 0.73f, 0.47f) : Ivory;
                Border(dot, Night, 2f);
                Round(dot, 7);
                v.Add(dot);
            }
            var face = new VisualElement { pickingMode = PickingMode.Ignore };
            face.style.width = 116; face.style.height = 116;
            var tex = Art("tag_" + pin.Id);
            if (tex) face.style.backgroundImage = tex;
            else
            {
                face.style.backgroundColor = yellow ? new Color(0.62f, 0.5f, 0.16f) : PanelBlue;
                Border(face, yellow ? new Color(0.84f, 0.73f, 0.47f) : SilverEdge, 2f);
                Round(face, 58);
                var ch = new Label(pin.Name.Substring(0, 1)) { pickingMode = PickingMode.Ignore };
                ch.style.fontSize = 40; ch.style.color = Ivory; ch.style.unityTextAlign = TextAnchor.MiddleCenter; ch.style.flexGrow = 1;
                face.Add(ch);
            }
            v.Add(face);
            var plate = new VisualElement { pickingMode = PickingMode.Ignore };
            plate.style.backgroundColor = new Color(Night.r, Night.g, Night.b, 0.86f);
            Border(plate, yellow ? new Color(0.84f, 0.73f, 0.47f) : SilverEdge, 1f);
            plate.style.paddingLeft = plate.style.paddingRight = 10; plate.style.paddingTop = 2; plate.style.paddingBottom = 4;
            plate.style.marginTop = 4; plate.style.alignItems = Align.Center;
            var name = new Label(pin.Name) { pickingMode = PickingMode.Ignore };
            name.style.fontSize = 24; name.style.color = Ivory;
            var note = new Label(pin.Note) { pickingMode = PickingMode.Ignore };
            note.style.fontSize = 16; note.style.color = Mist;
            plate.Add(name); plate.Add(note);
            v.Add(plate);
            proMapInner.Add(v);
            return v;
        }

        static void Border(VisualElement v, Color c, float w)
        {
            v.style.borderLeftColor = v.style.borderRightColor = v.style.borderTopColor = v.style.borderBottomColor = c;
            v.style.borderLeftWidth = v.style.borderRightWidth = v.style.borderTopWidth = v.style.borderBottomWidth = w;
        }

        static Texture2D Art(string name) => Resources.Load<Texture2D>(PrologueArt + name);

        // Map anchors as fractions of the map (0..1, from the top left), from map_anchors.json (Codex) when present.
        // The fallbacks are read off the approved world map concept (art/world-map-chapter1, 1920×1080).
        static readonly Dictionary<string, Vector2> FallbackAnchors = new Dictionary<string, Vector2>
        {
            { "julu", new Vector2(955, 328) }, { "luoyang", new Vector2(752, 428) }, { "xiapi", new Vector2(1112, 492) },
            { "hedong", new Vector2(690, 392) }, { "shanyang", new Vector2(1000, 432) }, { "youzhou", new Vector2(1060, 238) },
            { "zhuojun", new Vector2(1012, 268) }, { "guangzong", new Vector2(948, 340) }, { "yingchuan", new Vector2(842, 470) },
        };

        [Serializable] class AnchorFile { public AnchorEntry[] anchors = Array.Empty<AnchorEntry>(); public float width = 1920, height = 1080; }
        [Serializable] class AnchorEntry { public string id; public float x, y; }

        static Dictionary<string, Vector2> ReadAnchors()
        {
            var result = new Dictionary<string, Vector2>();
            foreach (var kv in FallbackAnchors) result[kv.Key] = new Vector2(kv.Value.x / 1920f, kv.Value.y / 1080f);
            var file = Resources.Load<TextAsset>(PrologueArt + "map_anchors");
            if (!file) return result;
            try
            {
                var data = JsonUtility.FromJson<AnchorFile>(file.text);
                foreach (var a in data.anchors)
                    if (!string.IsNullOrEmpty(a.id)) result[a.id] = new Vector2(a.x / data.width, a.y / data.height);
            }
            catch (ArgumentException e) { Debug.LogWarning("map_anchors.json unreadable, using defaults: " + e.Message); }
            return result;
        }

        Vector2 Anchor(string id) => anchors != null && anchors.TryGetValue(id, out var v) ? v : new Vector2(0.5f, 0.5f);
    }
}
