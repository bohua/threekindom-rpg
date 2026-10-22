using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.UI.Battle;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThreeKingdoms.UI.Story
{
    // Town overlay (UI Toolkit): objective top-left, interaction prompt, dialogue box, the fade used by the oath and by
    // leaving town, and the bubbles over heads (user 2026-10-21): residents near Liu Bei say their line, story
    // characters with a pending event and event spots show "!". Reads TownStoryController and never changes story state.
    [RequireComponent(typeof(UIDocument))]
    public sealed class TownHud : MonoBehaviour
    {
        public TownStoryController Controller;
        [Tooltip("Pixel skin (BATTLE_UI_SKIN.md); empty keeps the flat style")] public ThreeKingdoms.UI.Battle.BattleUiSkin Skin;
        [Tooltip("Illustrations shown beside the dialogue box for speakers that have one")] public DialoguePortrait[] Portraits = new DialoguePortrait[0];

        // A speaker's full-body illustration (user 2026-10-22): Liu Bei stands at the left facing in, everyone he talks
        // to at the right. Crop is the shown fraction from the top; Axis the figure's centre line (0..1 of the width).
        [System.Serializable]
        public struct DialoguePortrait
        {
            public string Speaker;
            public Texture2D Texture;
            public bool Right, Flip;
            [Range(0.3f, 1f)] public float Crop;
            [Range(0f, 1f)] public float Axis;
        }

        const float PortraitHeight = 700f, PortraitBottom = 150f, PortraitInset = 20f, PortraitIn = 0.22f;
        WindPortrait portrait;
        string portraitSpeaker;
        float portraitShownAt;
        bool portraitRight;

        // The speaker whose illustration is up, or null.
        public string PortraitSpeaker => portrait != null && portrait.style.display == DisplayStyle.Flex ? portraitSpeaker : null;
        public Texture2D PortraitTexture => PortraitSpeaker != null ? portrait.Texture : null;

        static readonly Color Gold = new Color(0.93f, 0.76f, 0.36f);
        static readonly Color Panel = new Color(0.07f, 0.09f, 0.14f, 0.9f);
        static readonly Color Edge = new Color(0.62f, 0.50f, 0.28f);

        VisualElement root, dialogue, fade, bubbleLayer, markLayer;
        readonly Dictionary<Transform, VisualElement> speech = new Dictionary<Transform, VisualElement>();
        readonly Dictionary<Transform, VisualElement> marks = new Dictionary<Transform, VisualElement>();

        static readonly Color BubbleFill = new Color(0.97f, 0.93f, 0.84f, 0.97f);
        static readonly Color BubbleEdge = new Color(0.35f, 0.24f, 0.14f);
        static readonly Color BubbleInk = new Color(0.18f, 0.12f, 0.08f);
        static readonly Color Alert = new Color(0.78f, 0.16f, 0.10f);
        Label objective, prompt, speaker, line, advance;

        void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;
            BattleFont.ApplyTo(root);
            root.style.flexGrow = 1;

            bubbleLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            Place(bubbleLayer, 0, 0, 0, 0);
            root.Add(bubbleLayer);
            // "!" marks draw above speech bubbles so a nearby line never hides one.
            markLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            Place(markLayer, 0, 0, 0, 0);
            root.Add(markLayer);

            objective = Label(root, 24, Color.white);
            Place(objective, 24, 20, -1, -1);
            Frame(objective);
            if (Skin) { Skin.Plaque(objective); objective.style.paddingLeft = objective.style.paddingRight = 28; objective.style.paddingTop = objective.style.paddingBottom = 14; }

            prompt = Label(root, 24, Gold);
            Place(prompt, 0, -1, 0, 60);
            prompt.style.unityTextAlign = TextAnchor.MiddleCenter;

            // Under the dialogue box so the box covers the figure's waist.
            portrait = new WindPortrait();
            portrait.style.position = Position.Absolute;
            portrait.style.bottom = PortraitBottom;
            portrait.style.height = PortraitHeight;
            portrait.style.display = DisplayStyle.None;
            root.Add(portrait);

            dialogue = new VisualElement { pickingMode = PickingMode.Ignore };
            Place(dialogue, 240, -1, 240, 40);
            Frame(dialogue);
            if (Skin) { Skin.Panel(dialogue); dialogue.style.paddingLeft = dialogue.style.paddingRight = 44; dialogue.style.paddingTop = dialogue.style.paddingBottom = 36; }
            dialogue.style.minHeight = 170;
            speaker = Label(dialogue, 26, Gold);
            line = Label(dialogue, 28, Color.white);
            line.style.whiteSpace = WhiteSpace.Normal; line.style.marginTop = 10;
            advance = Label(dialogue, 18, new Color(0.8f, 0.8f, 0.8f));
            advance.text = StoryText.Advance;
            Place(advance, -1, -1, 20, 10);
            root.Add(dialogue);

            fade = new VisualElement { pickingMode = PickingMode.Ignore };
            Place(fade, 0, 0, 0, 0);
            fade.style.backgroundColor = Color.black;
            root.Add(fade);

            if (Controller) Controller.Changed += Refresh;
            Refresh();
        }

        void OnDisable() { if (Controller) Controller.Changed -= Refresh; }

        void Refresh()
        {
            if (!Controller || Controller.Run == null) { fade.style.opacity = 0; dialogue.style.display = DisplayStyle.None; ShowPortrait(null); return; }
            objective.text = StoryText.Objective(Controller.Run.Story);
            prompt.text = Controller.Busy ? "" : StoryText.Prompt(Controller.Prompt);
            prompt.style.display = string.IsNullOrEmpty(prompt.text) ? DisplayStyle.None : DisplayStyle.Flex;
            var l = Controller.Line;
            dialogue.style.display = l.HasValue ? DisplayStyle.Flex : DisplayStyle.None;
            if (l.HasValue)
            {
                string who = StoryText.Speaker(l.Value.Speaker);
                speaker.text = who;
                speaker.style.display = string.IsNullOrEmpty(who) ? DisplayStyle.None : DisplayStyle.Flex;
                line.text = StoryText.Line(l.Value.Line);
            }
            ShowPortrait(l.HasValue ? l.Value.Speaker : null);
            fade.style.opacity = Controller.Fade;
        }

        // ---------- portrait ----------

        void ShowPortrait(string speaker)
        {
            int i = speaker == null ? -1 : System.Array.FindIndex(Portraits, p => p.Speaker == speaker && p.Texture);
            if (i < 0) { portrait.style.display = DisplayStyle.None; portraitSpeaker = null; return; }
            if (speaker == portraitSpeaker && portrait.style.display == DisplayStyle.Flex) return;
            var p = Portraits[i];
            float crop = p.Crop > 0 ? p.Crop : 0.6f, axis = p.Axis > 0 ? p.Axis : 0.5f;
            portrait.Show(p.Texture, p.Flip, crop, axis);
            portrait.style.width = PortraitHeight * p.Texture.width / (p.Texture.height * crop);
            portraitRight = p.Right;
            if (p.Right) { portrait.style.left = StyleKeyword.Auto; portrait.style.right = PortraitInset; }
            else { portrait.style.right = StyleKeyword.Auto; portrait.style.left = PortraitInset; }
            portrait.style.display = DisplayStyle.Flex;
            portraitSpeaker = speaker;
            portraitShownAt = Time.unscaledTime;
        }

        // Slides in from its side on a new speaker, then sways and breathes.
        void Update()
        {
            if (portrait == null || portrait.style.display != DisplayStyle.Flex) return;
            float k = Mathf.Clamp01((Time.unscaledTime - portraitShownAt) / PortraitIn);
            k = 1f - (1f - k) * (1f - k);
            portrait.style.opacity = k;
            float slide = (1f - k) * 60f * (portraitRight ? 1f : -1f);
            portrait.style.translate = new Translate(slide, Mathf.Sin(Time.unscaledTime * 1.4f) * 4f);
            portrait.Tick(Time.unscaledTime);
        }

        // ---------- bubbles ----------

        public IReadOnlyCollection<Transform> SpeechBubbles => speech.Keys;
        public IReadOnlyCollection<Transform> MarkBubbles => marks.Keys;

        void LateUpdate()
        {
            var cam = Camera.main;
            if (!Controller || Controller.Run == null || root?.panel == null || !cam) return;
            bool visible = Controller.Fade < 0.01f;
            Sync(speech, bubbleLayer, visible ? Controller.Speaking : Enumerable.Empty<Transform>(), t => SpeechBubble(StoryText.Line(Controller.ResidentLineKey(TownStoryController.ResidentId(t)))));
            var alerts = Controller.StoryMarks.ToList();
            if (Controller.PlayerAlert) alerts.Add(Controller.Player.transform);
            Sync(marks, markLayer, visible ? alerts : Enumerable.Empty<Transform>(), _ => MarkBubble());
            float bob = Mathf.Sin(Time.unscaledTime * 4f) * 4f;
            // The pixel "!" glints for a moment once per second.
            var alertFrame = Skin && Skin.BubbleAlertGlint && Mathf.Repeat(Time.unscaledTime, 1f) < 0.18f ? Skin.BubbleAlertGlint : Skin ? Skin.BubbleAlert : null;
            if (alertFrame) foreach (var e in marks.Values) if (e.Q(className: "alert-frame") is VisualElement f) f.style.backgroundImage = new StyleBackground(alertFrame);
            foreach (var pair in speech) Anchor(pair.Value, pair.Key, cam, 0f);
            foreach (var pair in marks) Anchor(pair.Value, pair.Key, cam, bob);
        }

        void Sync(Dictionary<Transform, VisualElement> shown, VisualElement layer, IEnumerable<Transform> wanted, System.Func<Transform, VisualElement> make)
        {
            var want = new HashSet<Transform>(wanted.Where(t => t));
            foreach (var gone in shown.Keys.Where(t => !want.Contains(t)).ToList()) { shown[gone].RemoveFromHierarchy(); shown.Remove(gone); }
            foreach (var t in want)
                if (!shown.ContainsKey(t)) { var e = make(t); layer.Add(e); shown[t] = e; }
        }

        // Bottom-centre of the bubble sits just above the head.
        void Anchor(VisualElement e, Transform t, Camera cam, float lift)
        {
            var p = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, t.position + Vector3.up * 2.05f, cam);
            e.style.left = p.x; e.style.top = p.y - lift;
        }

        // Bubble root: positioned by its bottom-centre, children stacked and centred.
        static VisualElement Anchored()
        {
            var b = new VisualElement { pickingMode = PickingMode.Ignore };
            b.style.position = Position.Absolute;
            b.style.alignItems = Align.Center;
            b.style.translate = new Translate(new Length(-50, LengthUnit.Percent), new Length(-100, LengthUnit.Percent));
            return b;
        }

        // Codex pixel speech bubble: nine-sliced cream board plus the separate tail, which overlaps the board's
        // bottom 2 native px (manifest: joins at h-2).
        VisualElement PixelSpeech(string text)
        {
            var b = Anchored();
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            Skin.Speech(box);
            box.style.paddingLeft = box.style.paddingRight = 18; box.style.paddingTop = box.style.paddingBottom = 12;
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.style.fontSize = 20; l.style.color = BubbleInk; l.style.whiteSpace = WhiteSpace.Normal; l.style.maxWidth = 380;
            l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
            l.style.paddingLeft = l.style.paddingRight = l.style.paddingTop = l.style.paddingBottom = 0;
            box.Add(l);
            b.Add(box);
            var tail = new VisualElement { pickingMode = PickingMode.Ignore };
            ThreeKingdoms.UI.Battle.BattleUiSkin.Image(tail, Skin.BubbleTail);
            tail.style.marginTop = -2 * ThreeKingdoms.UI.Battle.BattleUiSkin.Scale;
            b.Add(tail);
            return b;
        }

        static VisualElement Bubble(VisualElement content, float radius)
        {
            var b = Anchored();
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.style.backgroundColor = BubbleFill;
            box.style.borderTopWidth = box.style.borderBottomWidth = box.style.borderLeftWidth = box.style.borderRightWidth = 2;
            box.style.borderTopColor = box.style.borderBottomColor = box.style.borderLeftColor = box.style.borderRightColor = BubbleEdge;
            box.style.borderTopLeftRadius = box.style.borderTopRightRadius = box.style.borderBottomLeftRadius = box.style.borderBottomRightRadius = radius;
            box.Add(content);
            b.Add(box);
            // Tail: a small turned square tucked under the box.
            var tail = new VisualElement { pickingMode = PickingMode.Ignore };
            tail.style.width = tail.style.height = 12; tail.style.marginTop = -7;
            tail.style.backgroundColor = BubbleFill;
            tail.style.borderBottomWidth = tail.style.borderRightWidth = 2;
            tail.style.borderBottomColor = tail.style.borderRightColor = BubbleEdge;
            tail.style.rotate = new Rotate(45);
            b.Add(tail);
            return b;
        }

        VisualElement SpeechBubble(string text)
        {
            if (Skin && Skin.HasBubbles) return PixelSpeech(text);
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.style.fontSize = 20; l.style.color = BubbleInk; l.style.whiteSpace = WhiteSpace.Normal; l.style.maxWidth = 380;
            l.style.paddingLeft = l.style.paddingRight = 14; l.style.paddingTop = l.style.paddingBottom = 8;
            l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
            return Bubble(l, 10);
        }

        VisualElement MarkBubble()
        {
            if (Skin && Skin.HasBubbles)
            {
                var m = Anchored();
                var img = new VisualElement { pickingMode = PickingMode.Ignore };
                img.AddToClassList("alert-frame");
                ThreeKingdoms.UI.Battle.BattleUiSkin.Image(img, Skin.BubbleAlert);
                m.Add(img);
                return m;
            }
            var l = new Label("!") { pickingMode = PickingMode.Ignore };
            l.style.fontSize = 30; l.style.color = Alert; l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.width = 40; l.style.height = 40; l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
            return Bubble(l, 20);
        }

        static Label Label(VisualElement parent, int size, Color color)
        {
            var l = new Label { pickingMode = PickingMode.Ignore };
            l.style.fontSize = size; l.style.color = color;
            l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
            parent.Add(l);
            return l;
        }

        static void Frame(VisualElement e)
        {
            e.style.backgroundColor = Panel;
            e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = 2;
            e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = Edge;
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = 4;
            e.style.paddingLeft = e.style.paddingRight = 22; e.style.paddingTop = e.style.paddingBottom = 12;
        }

        static void Place(VisualElement e, float left, float top, float right, float bottom)
        {
            e.style.position = Position.Absolute;
            if (left >= 0) e.style.left = left;
            if (top >= 0) e.style.top = top;
            if (right >= 0) e.style.right = right;
            if (bottom >= 0) e.style.bottom = bottom;
        }
    }
}
