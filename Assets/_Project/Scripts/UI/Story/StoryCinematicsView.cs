using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ThreeKingdoms.Gameplay.Story;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using UnityEngine.Video;

namespace ThreeKingdoms.UI.Story
{
    // Full-screen cinematics over the town and battle HUDs (user 2026-10-24).
    // The ink scroll: at the end of a main story beat everything stops ("bullet time"): the frame freezes, turns grey
    // and dark, an ink drop spreads, a xuan-paper scroll unrolls from the middle with the episode painted in the
    // lianhuanhua manner, the story types itself in columns right to left, top to bottom, a red 完成 seal presses down,
    // and the player confirms to go on. The opening film plays after the Yellow Turbans are beaten.
    // Layout and timing follow the approved design sheets in art/story-cinematics-concept. Time runs unscaled: the
    // scroll sets timeScale to 0 while it is up.
    [RequireComponent(typeof(UIDocument))]
    public sealed class StoryCinematicsView : MonoBehaviour, StoryCinematics.IView
    {
        [Serializable]
        public struct Painting
        {
            public string Id;
            public Texture2D Texture;
        }

        [Header("Ink scroll art (Codex art/story-cinematics); empty parts are drawn plain")]
        public Texture2D Paper;
        public Texture2D Roller;
        public Texture2D Seal;
        public Texture2D InkDrop;
        public Painting[] Paintings = new Painting[0];
        [Header("Timing (seconds)")]
        public float FreezeTime = 0.5f, UnrollTime = 3.0f, SealTime = 0.22f;
        [Tooltip("Seconds for the painting to rise from faint to full once the scroll is open (user 2026-10-26)")]
        public float PaintingFadeTime = 3.0f;
        [Tooltip("Characters typed per second")] public float TypeSpeed = 45f;
        [Tooltip("Characters per column")] public int PerColumn = 13;
        [Header("Opening film")]
        [Tooltip("Under StreamingAssets; StoryFlowBuilder copies it from the opening delivery")]
        public string OpeningFile = "Video/opening.mp4";

        const float ScrollWidth = 1560f, ScrollHeight = 780f, RollerWidth = 54f;
        static readonly Color PaperColor = new Color(0.93f, 0.90f, 0.82f);
        static readonly Color Ink = new Color(0.10f, 0.09f, 0.08f);
        static readonly Color SealRed = new Color(0.72f, 0.12f, 0.10f);
        static readonly string[] KaiFamilies = { "KaiTi", "STKaiti", "MingLiU", "SimSun", "Microsoft YaHei" };
        static Font kai;

        VisualElement root, freezeColor, freezeGrey, dim, inkDrop, scroll, clip, content, rollerLeft, rollerRight, painting, titleColumn, textColumns, seal, black, film;
        Label hint;
        readonly List<Label> glyphs = new List<Label>();
        Texture2D shot, grey;
        bool advance;

        // "freeze", "unroll" (the story writes itself as the paper opens), "paint", "seal", "wait", "out", "opening", or null.
        public string Stage { get; private set; }
        public bool Playing => Stage != null;
        public string ShownId { get; private set; }
        public int TypedCount { get; private set; }
        public int GlyphCount => glyphs.Count;
        public string Title => titleColumn?.userData as string;

        public void Advance() => advance = true;

        void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            root = doc.rootVisualElement;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;
            root.style.flexGrow = 1;
            Build();
            StoryCinematics.View = this;
        }

        void OnDisable()
        {
            if (StoryCinematics.View == (StoryCinematics.IView)this) StoryCinematics.View = null;
            if (Playing) Time.timeScale = 1f;
            Release();
        }

        void Release()
        {
            if (shot) Destroy(shot);
            if (grey) Destroy(grey);
            shot = grey = null;
        }

        // ---------- layout ----------

        void Build()
        {
            if (!kai) kai = Font.CreateDynamicFontFromOSFont(KaiFamilies, 40);
            if (kai) root.style.unityFontDefinition = FontDefinition.FromFont(kai);

            freezeColor = Layer(root);
            freezeGrey = Layer(root);
            dim = Layer(root);
            dim.style.backgroundColor = new Color(0.02f, 0.03f, 0.05f, 1f);
            inkDrop = new VisualElement { pickingMode = PickingMode.Ignore };
            Centre(inkDrop, 900, 900);
            if (InkDrop) inkDrop.style.backgroundImage = InkDrop;
            else { inkDrop.style.backgroundColor = new Color(0, 0, 0, 0.55f); Round(inkDrop, 450); }
            root.Add(inkDrop);

            scroll = new VisualElement { pickingMode = PickingMode.Ignore };
            Centre(scroll, ScrollWidth + RollerWidth * 2, ScrollHeight + 40);
            root.Add(scroll);
            clip = new VisualElement { pickingMode = PickingMode.Ignore };
            clip.style.position = Position.Absolute;
            clip.style.top = 20; clip.style.height = ScrollHeight;
            clip.style.overflow = Overflow.Hidden;
            scroll.Add(clip);
            content = new VisualElement { pickingMode = PickingMode.Ignore };
            content.style.position = Position.Absolute;
            content.style.top = 0; content.style.width = ScrollWidth; content.style.height = ScrollHeight;
            if (Paper) { content.style.backgroundImage = Paper; content.style.unityBackgroundImageTintColor = Color.white; }
            else content.style.backgroundColor = PaperColor;
            clip.Add(content);

            // Each painting is a whole sheet of paper: the scene on the left, blank paper on the right where the story
            // stands in columns (design sheet 03).
            painting = new VisualElement { pickingMode = PickingMode.Ignore };
            painting.style.position = Position.Absolute;
            painting.style.left = 0; painting.style.top = 0; painting.style.right = 0; painting.style.bottom = 0;
            painting.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
            content.Add(painting);

            textColumns = new VisualElement { pickingMode = PickingMode.Ignore };
            textColumns.style.position = Position.Absolute;
            textColumns.style.right = 170; textColumns.style.top = 80; textColumns.style.bottom = 70;
            textColumns.style.flexDirection = FlexDirection.RowReverse;
            content.Add(textColumns);

            titleColumn = new VisualElement { pickingMode = PickingMode.Ignore };
            titleColumn.style.position = Position.Absolute;
            titleColumn.style.right = 60; titleColumn.style.top = 70;
            titleColumn.style.flexDirection = FlexDirection.Column;
            content.Add(titleColumn);

            seal = new VisualElement { pickingMode = PickingMode.Ignore };
            seal.style.position = Position.Absolute;
            seal.style.width = 150; seal.style.height = 150;
            seal.style.right = 200; seal.style.bottom = 60;
            if (Seal) seal.style.backgroundImage = Seal;
            else
            {
                seal.style.backgroundColor = new Color(SealRed.r, SealRed.g, SealRed.b, 0.92f);
                Round(seal, 8);
                seal.style.alignItems = Align.Center; seal.style.justifyContent = Justify.Center;
                var word = new Label(InkScrollText.Seal) { pickingMode = PickingMode.Ignore };
                word.style.fontSize = 54; word.style.color = PaperColor; word.style.unityFontStyleAndWeight = FontStyle.Bold;
                seal.Add(word);
            }
            content.Add(seal);

            rollerLeft = MakeRoller(scroll);
            rollerRight = MakeRoller(scroll);

            hint = new Label(InkScrollText.Continue) { pickingMode = PickingMode.Ignore };
            hint.style.position = Position.Absolute; hint.style.right = 80; hint.style.bottom = 40;
            hint.style.fontSize = 24; hint.style.color = new Color(0.95f, 0.92f, 0.85f);
            root.Add(hint);

            film = Layer(root);
            film.style.backgroundColor = Color.black;
            film.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            black = Layer(root);
            black.style.backgroundColor = Color.black;
            Hide();
        }

        VisualElement MakeRoller(VisualElement parent)
        {
            var r = new VisualElement { pickingMode = PickingMode.Ignore };
            r.style.position = Position.Absolute;
            r.style.top = 0; r.style.width = RollerWidth; r.style.height = ScrollHeight + 40;
            if (Roller) r.style.backgroundImage = Roller;
            else
            {
                r.style.backgroundColor = new Color(0.24f, 0.15f, 0.09f);
                Round(r, 10);
            }
            parent.Add(r);
            return r;
        }

        static VisualElement Layer(VisualElement parent)
        {
            var v = new VisualElement { pickingMode = PickingMode.Ignore };
            v.style.position = Position.Absolute;
            v.style.left = v.style.right = v.style.top = v.style.bottom = 0;
            v.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
            parent.Add(v);
            return v;
        }

        static void Centre(VisualElement v, float w, float h)
        {
            v.style.position = Position.Absolute;
            v.style.width = w; v.style.height = h;
            v.style.left = new Length(50, LengthUnit.Percent); v.style.top = new Length(50, LengthUnit.Percent);
            v.style.marginLeft = -w / 2f; v.style.marginTop = -h / 2f;
        }

        static void Round(VisualElement v, float r) =>
            v.style.borderTopLeftRadius = v.style.borderTopRightRadius = v.style.borderBottomLeftRadius = v.style.borderBottomRightRadius = r;

        void Hide()
        {
            foreach (var v in new[] { freezeColor, freezeGrey, dim, inkDrop, scroll, hint, film, black })
                v.style.display = DisplayStyle.None;
        }

        // ---------- the scroll ----------

        public IEnumerator PlayScroll(string id)
        {
            if (!InkScrollText.TryGet(id, out var entry)) yield break;
            Stage = "freeze";
            ShownId = id;
            advance = false;
            float timeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            // The frame is grabbed once rendered; batch mode (tests) never reaches end of frame and has no picture.
            if (Application.isBatchMode) yield return null;
            else { yield return new WaitForEndOfFrame(); Capture(); }
            Time.timeScale = 0f;
            StoryMusic.Play(StoryMusic.ScrollCue, fadeIn: 0.3f);
            StoryMusic.Sfx(StoryMusic.InkSfx);
            try
            {
                Fill(entry, id);
                Show(freezeColor, freezeGrey, dim, inkDrop);
                SetOpacity(freezeGrey, 0); SetOpacity(dim, 0); SetOpacity(inkDrop, 0);
                yield return Tween(FreezeTime, k =>
                {
                    SetOpacity(freezeGrey, k);
                    SetOpacity(dim, 0.45f * k);
                    float s = Mathf.Lerp(0.15f, 1.5f, Ease(k));
                    inkDrop.style.scale = new Scale(new Vector3(s, s, 1));
                    SetOpacity(inkDrop, 0.6f * Mathf.Sin(k * Mathf.PI));
                });
                inkDrop.style.display = DisplayStyle.None;

                // User 2026-10-26: the scroll opens slowly from right to left, the story writing itself column by
                // column as the paper passes; the painting side stays blank paper until the scroll is fully open, then
                // the painting rises from faint to full with the music, and the seal comes down last with a bang.
                Stage = "unroll";
                StoryMusic.Sfx(StoryMusic.UnrollSfx);
                Show(scroll);
                SetOpacity(painting, 0); SetOpacity(seal, 0);
                foreach (var t in titleGlyphs) SetOpacity(t, 0);
                float started = Time.unscaledTime, typedAt = started;
                int titleShown = 0;
                float open = 0f;
                while (open < ScrollWidth || TypedCount < glyphs.Count)
                {
                    bool rush = advance;   // confirm opens and writes the rest at once
                    advance = false;
                    float k = Mathf.Clamp01((Time.unscaledTime - started) / Mathf.Max(0.05f, UnrollTime));
                    open = rush ? ScrollWidth : Mathf.Max(open, ScrollWidth * Mathf.SmoothStep(0f, 1f, k));
                    OpenFromRight(open);
                    // The title, then each glyph once the opened paper has reached its column, at writing speed.
                    while (titleShown < titleGlyphs.Count && (rush || open >= TitleReach)) SetOpacity(titleGlyphs[titleShown++], 1);
                    while (TypedCount < glyphs.Count && (rush || (open >= Reach(TypedCount) && Time.unscaledTime >= typedAt)))
                    {
                        SetOpacity(glyphs[TypedCount++], 1);
                        typedAt = Mathf.Max(typedAt, Time.unscaledTime) + 1f / Mathf.Max(1f, TypeSpeed);
                    }
                    if (rush) break;
                    yield return null;
                }
                OpenFromRight(ScrollWidth);
                foreach (var t in titleGlyphs) SetOpacity(t, 1);
                while (TypedCount < glyphs.Count) SetOpacity(glyphs[TypedCount++], 1);

                Stage = "paint";
                float paintFrom = Time.unscaledTime;
                advance = false;
                while (Time.unscaledTime - paintFrom < PaintingFadeTime && !advance)
                {
                    float k = (Time.unscaledTime - paintFrom) / PaintingFadeTime;
                    SetOpacity(painting, k * k * (3f - 2f * k));   // from faint to deep, slow at both ends
                    yield return null;
                }
                SetOpacity(painting, 1);

                // 0.2: the 完成 seal only on the version's last scroll (V02_HUANGJIN_PLAN §1); the others end unsealed.
                if (InkScrollText.Sealed(id))
                {
                    Stage = "seal";
                    yield return WaitUnscaled(0.25f);
                    StoryMusic.Sfx(StoryMusic.SealSfx);
                    yield return Tween(SealTime, k =>
                    {
                        float sc = Mathf.Lerp(3f, 1f, k * k);
                        seal.style.scale = new Scale(new Vector3(sc, sc, 1));
                        SetOpacity(seal, k);
                    });
                    yield return Tween(0.28f, k =>
                    {
                        float shake = (1f - k) * 10f * Mathf.Sin(k * 46f);
                        scroll.style.translate = new Translate(shake, -shake * 0.6f);
                    });
                    scroll.style.translate = new Translate(0, 0);
                }

                Stage = "wait";
                advance = false;
                yield return WaitUnscaled(0.3f);
                Show(hint);
                while (!advance && !ConfirmPressed()) yield return null;

                Stage = "out";
                hint.style.display = DisplayStyle.None;
                Show(black);
                yield return Tween(0.4f, k => SetOpacity(black, k));
                foreach (var v in new[] { freezeColor, freezeGrey, dim, scroll }) v.style.display = DisplayStyle.None;
                Time.timeScale = timeScale;
                StoryMusic.Stop(0.6f);
                yield return Tween(0.4f, k => SetOpacity(black, 1f - k));
            }
            finally
            {
                Time.timeScale = timeScale;
                Hide();
                Release();
                Stage = null;
            }
        }

        // The scroll opened from its right end by `open` pixels: the right roller stays, the left one travels left.
        void OpenFromRight(float open)
        {
            float shut = ScrollWidth - open;
            clip.style.left = RollerWidth + shut; clip.style.width = open;
            content.style.left = -shut;
            rollerLeft.style.left = shut;
            rollerRight.style.left = RollerWidth + ScrollWidth;
        }

        // How far the paper must be open (from the right) before a column of the story can be written.
        const float TextRight = 170f, ColumnStep = 36 + 6 + 14, TitleReach = 150f;
        readonly List<int> glyphColumn = new List<int>();
        readonly List<Label> titleGlyphs = new List<Label>();
        float Reach(int glyph) => TextRight + (glyphColumn[glyph] + 1) * ColumnStep;

        // Puts the painting, the title and the story (hidden, typed later) on the paper.
        void Fill(InkScrollText.Entry entry, string id)
        {
            Texture2D art = null;
            foreach (var p in Paintings) if (p.Id == id && p.Texture) art = p.Texture;
            painting.style.backgroundImage = art;

            titleColumn.Clear();
            titleGlyphs.Clear();
            titleColumn.userData = entry.Title;
            foreach (char c in entry.Title)
            {
                var l = Glyph(titleColumn, c.ToString(), 76, Ink);
                titleGlyphs.Add(l);
                l.style.unityFontStyleAndWeight = FontStyle.Bold;
                l.style.marginBottom = 6;
            }

            textColumns.Clear();
            glyphs.Clear();
            glyphColumn.Clear();
            TypedCount = 0;
            var columns = InkScrollText.Columns(entry.Body, PerColumn);
            // The seal sits at the foot of the story, just left of its last column (never over the text).
            seal.style.right = 170 + columns.Count * (36 + 6 + 14) + 10;
            seal.style.bottom = 70;
            int columnIndex = -1;
            foreach (var column in columns)
            {
                columnIndex++;
                var col = new VisualElement { pickingMode = PickingMode.Ignore };
                col.style.flexDirection = FlexDirection.Column;
                col.style.marginLeft = 14;
                textColumns.Add(col);
                bool source = column.StartsWith("——", StringComparison.Ordinal);
                foreach (char c in InkScrollText.ToVertical(column))
                {
                    var g = Glyph(col, c.ToString(), source ? 30 : 36, source ? new Color(0.30f, 0.27f, 0.24f) : Ink);
                    SetOpacity(g, 0);
                    glyphs.Add(g);
                    glyphColumn.Add(columnIndex);
                }
            }
        }

        static Label Glyph(VisualElement parent, string text, int size, Color color)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.style.fontSize = size; l.style.color = color;
            l.style.width = size + 6; l.style.height = size + 8;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
            l.style.paddingLeft = l.style.paddingRight = l.style.paddingTop = l.style.paddingBottom = 0;
            parent.Add(l);
            return l;
        }

        // The frame as it was when the story stopped, and its grey, darkened copy.
        void Capture()
        {
            Release();
            try { shot = ScreenCapture.CaptureScreenshotAsTexture(); }
            catch (Exception) { shot = null; }
            if (!shot || shot.width < 8) { freezeColor.style.backgroundImage = null; freezeGrey.style.backgroundImage = null; return; }
            var px = shot.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                // Ink-wash grey: luminance, a touch of cool blue, a soft lift so the darkest parts do not crush.
                float l = (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
                l = 0.08f + l * 0.85f;
                px[i] = new Color32((byte)(l * 238), (byte)(l * 242), (byte)(l * 250), 255);
            }
            grey = new Texture2D(shot.width, shot.height, TextureFormat.RGBA32, false);
            grey.SetPixels32(px);
            grey.Apply(false, true);
            freezeColor.style.backgroundImage = shot;
            freezeGrey.style.backgroundImage = grey;
        }

        // ---------- the opening film ----------

        public IEnumerator PlayOpening()
        {
            string path = Path.Combine(Application.streamingAssetsPath, OpeningFile);
            if (!File.Exists(path)) { Debug.Log($"Opening film not found at {path}; skipping it."); yield break; }
            Stage = "opening";
            StoryMusic.Stop(0.4f);
            var rt = new RenderTexture(1280, 720, 0);
            var player = gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.source = VideoSource.Url;
            player.url = path;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = rt;
            player.audioOutputMode = VideoAudioOutputMode.Direct;
            player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            bool ended = false;
            player.loopPointReached += _ => ended = true;
            try
            {
                Show(black);
                yield return Tween(0.4f, k => SetOpacity(black, k));
                player.Prepare();
                float wait = Time.unscaledTime + 8f;
                while (!player.isPrepared && Time.unscaledTime < wait) yield return null;
                if (!player.isPrepared) yield break;
                film.style.backgroundImage = Background.FromRenderTexture(rt);
                Show(film, hint);
                hint.text = InkScrollText.SkipOpening;
                hint.BringToFront();
                player.Play();
                black.BringToFront();
                yield return Tween(0.3f, k => SetOpacity(black, 1f - k));
                advance = false;
                while (!ended && !advance && !CancelPressed()) yield return null;
                player.Stop();
                yield return Tween(0.3f, k => SetOpacity(black, k));
            }
            finally
            {
                if (player) Destroy(player);
                rt.Release();
                Destroy(rt);
                film.style.backgroundImage = null;
                hint.text = InkScrollText.Continue;
                Hide();
                Stage = null;
            }
        }

        // ---------- helpers ----------

        static void Show(params VisualElement[] vs)
        {
            foreach (var v in vs) { v.style.display = DisplayStyle.Flex; v.style.opacity = 1; }
        }

        static void SetOpacity(VisualElement v, float a) => v.style.opacity = Mathf.Clamp01(a);

        static float Ease(float k) => 1f - (1f - k) * (1f - k) * (1f - k);

        static IEnumerator Tween(float seconds, Action<float> step)
        {
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime)
            {
                step(Mathf.Clamp01(t / seconds));
                yield return null;
            }
            step(1f);
        }

        static IEnumerator WaitUnscaled(float seconds)
        {
            float until = Time.unscaledTime + seconds;
            while (Time.unscaledTime < until) yield return null;
        }

        static bool ConfirmPressed()
        {
            var k = Keyboard.current;
            var g = Gamepad.current;
            return (k != null && (k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame || k.zKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame))
                || (g != null && g.buttonSouth.wasPressedThisFrame)
                || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
        }

        static bool CancelPressed()
        {
            var k = Keyboard.current;
            var g = Gamepad.current;
            return (k != null && (k.escapeKey.wasPressedThisFrame || k.xKey.wasPressedThisFrame || k.backspaceKey.wasPressedThisFrame))
                || (g != null && g.buttonEast.wasPressedThisFrame);
        }
    }
}
