using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.UI.Battle;
using ThreeKingdoms.UI.Common;
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
        [Tooltip("Illustrations shown beside the dialogue box for speakers that have one")] public DialoguePortraitSet Portraits;

        DialogueView dialogue;
        // Liu Bei's answer to Zhang Fei (user 2026-10-22): the options stand above the dialogue box.
        VisualElement choiceList;
        // Three dice on a mat while a bet is on (user 2026-10-22); pips drawn in code until the Codex dice art lands.
        VisualElement diceMat;
        readonly VisualElement[] dieFaces = new VisualElement[3];
        public int[] ShownDice { get; private set; }
        [Header("Dice art (Codex art/zhuojun-casino); empty draws the pips in code")]
        public Texture2D[] DiceFaces = new Texture2D[0];
        public Texture2D DiceMat, DiceCup, DiceCupLifted;
        VisualElement cup;
        public IReadOnlyList<Button> ChoiceButtons => choiceList.Query<Button>().ToList();
        // The speaker whose illustration is up, or null.
        public string PortraitSpeaker => dialogue?.PortraitSpeaker;
        public Texture2D PortraitTexture => dialogue?.PortraitTexture;

        static readonly Color Gold = new Color(0.93f, 0.76f, 0.36f);
        static readonly Color Panel = new Color(0.07f, 0.09f, 0.14f, 0.9f);
        static readonly Color Edge = new Color(0.62f, 0.50f, 0.28f);

        VisualElement root, fade, bubbleLayer, markLayer;
        readonly Dictionary<Transform, VisualElement> speech = new Dictionary<Transform, VisualElement>();
        readonly Dictionary<Transform, VisualElement> marks = new Dictionary<Transform, VisualElement>();
        readonly Dictionary<Transform, VisualElement> games = new Dictionary<Transform, VisualElement>();

        static readonly Color BubbleFill = new Color(0.97f, 0.93f, 0.84f, 0.97f);
        static readonly Color BubbleEdge = new Color(0.35f, 0.24f, 0.14f);
        static readonly Color BubbleInk = new Color(0.18f, 0.12f, 0.08f);
        static readonly Color Alert = new Color(0.78f, 0.16f, 0.10f);
        Label objective, prompt;

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

            dialogue = new DialogueView(root, Skin, Portraits);
            diceMat = new VisualElement { pickingMode = PickingMode.Ignore };
            Place(diceMat, -1, 300, -1, -1);
            diceMat.style.left = new Length(50, LengthUnit.Percent);
            diceMat.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
            diceMat.style.flexDirection = FlexDirection.Row;
            diceMat.style.paddingLeft = diceMat.style.paddingRight = 36; diceMat.style.paddingTop = diceMat.style.paddingBottom = 28;
            diceMat.style.backgroundColor = new Color(0.12f, 0.26f, 0.18f, 0.96f);
            diceMat.style.borderTopWidth = diceMat.style.borderBottomWidth = diceMat.style.borderLeftWidth = diceMat.style.borderRightWidth = 6;
            diceMat.style.borderTopColor = diceMat.style.borderBottomColor = diceMat.style.borderLeftColor = diceMat.style.borderRightColor = new Color(0.36f, 0.22f, 0.12f);
            for (int i = 0; i < 3; i++)
            {
                var die = new VisualElement { pickingMode = PickingMode.Ignore };
                die.style.width = die.style.height = 96;
                die.style.marginLeft = die.style.marginRight = 14;
                die.style.backgroundColor = new Color(0.96f, 0.93f, 0.85f);
                die.style.borderTopLeftRadius = die.style.borderTopRightRadius = die.style.borderBottomLeftRadius = die.style.borderBottomRightRadius = 12;
                diceMat.Add(die);
                dieFaces[i] = die;
            }
            if (DiceMat)
            {
                BattleUiSkin.Apply(diceMat, DiceMat, 8, 8, 8, 8);
                diceMat.style.unitySliceScale = 4f;
                diceMat.style.paddingLeft = diceMat.style.paddingRight = 48; diceMat.style.paddingTop = diceMat.style.paddingBottom = 40;
            }
            if (DiceCup)
            {
                // The cup covers the dice while they shake.
                cup = new VisualElement { pickingMode = PickingMode.Ignore };
                cup.style.position = Position.Absolute;
                cup.style.width = DiceCup.width * 4; cup.style.height = DiceCup.height * 4;
                cup.style.left = new Length(50, LengthUnit.Percent); cup.style.top = 10;
                cup.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
                cup.style.backgroundImage = new StyleBackground(DiceCup);
                diceMat.Add(cup);
            }
            diceMat.style.display = DisplayStyle.None;
            root.Add(diceMat);

            choiceList = new VisualElement();
            Place(choiceList, -1, -1, 300, 230);
            choiceList.style.width = 520;
            choiceList.style.display = DisplayStyle.None;
            root.Add(choiceList);

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
            if (!Controller || Controller.Run == null) { fade.style.opacity = 0; dialogue.Hide(); return; }
            objective.text = StoryText.Objective(Controller.Run.Story);
            prompt.text = Controller.Busy ? "" : StoryText.Prompt(Controller.Prompt);
            prompt.style.display = string.IsNullOrEmpty(prompt.text) ? DisplayStyle.None : DisplayStyle.Flex;
            var l = Controller.Line;
            if (l.HasValue) dialogue.Show(l.Value.Speaker, l.Value.Line);
            else dialogue.Hide();
            ShowChoices(Controller.Choices);
            ShowDice(Controller.Dice);
            fade.style.opacity = Controller.Fade;
        }

        static readonly (float x, float y)[][] Pips =
        {
            new[] { (0.5f, 0.5f) },
            new[] { (0.25f, 0.25f), (0.75f, 0.75f) },
            new[] { (0.25f, 0.25f), (0.5f, 0.5f), (0.75f, 0.75f) },
            new[] { (0.25f, 0.25f), (0.75f, 0.25f), (0.25f, 0.75f), (0.75f, 0.75f) },
            new[] { (0.25f, 0.25f), (0.75f, 0.25f), (0.5f, 0.5f), (0.25f, 0.75f), (0.75f, 0.75f) },
            new[] { (0.25f, 0.22f), (0.75f, 0.22f), (0.25f, 0.5f), (0.75f, 0.5f), (0.25f, 0.78f), (0.75f, 0.78f) },
        };

        void ShowDice(int[] dice)
        {
            ShownDice = dice;
            diceMat.style.display = dice == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (dice == null) return;
            bool rolling = Controller && Controller.DiceRolling;
            if (cup != null)
            {
                cup.style.display = rolling ? DisplayStyle.Flex : DisplayStyle.None;
                foreach (var d in dieFaces) d.style.visibility = rolling ? Visibility.Hidden : Visibility.Visible;
            }
            for (int i = 0; i < 3; i++)
            {
                var die = dieFaces[i];
                die.Clear();
                if (DiceFaces.Length == 6 && DiceFaces[Mathf.Clamp(dice[i], 1, 6) - 1])
                {
                    // 24 px pixel dice at 4×.
                    die.style.backgroundImage = new StyleBackground(DiceFaces[Mathf.Clamp(dice[i], 1, 6) - 1]);
                    die.style.backgroundColor = Color.clear;
                    continue;
                }
                int v = Mathf.Clamp(dice[i], 1, 6);
                foreach (var (x, y) in Pips[v - 1])
                {
                    var pip = new VisualElement { pickingMode = PickingMode.Ignore };
                    float size = v == 1 ? 26 : 18;
                    pip.style.position = Position.Absolute;
                    pip.style.width = pip.style.height = size;
                    pip.style.left = x * 96 - size / 2; pip.style.top = y * 96 - size / 2;
                    pip.style.borderTopLeftRadius = pip.style.borderTopRightRadius = pip.style.borderBottomLeftRadius = pip.style.borderBottomRightRadius = size / 2;
                    // One and four are red, as on Chinese dice.
                    pip.style.backgroundColor = v == 1 || v == 4 ? new Color(0.78f, 0.12f, 0.10f) : new Color(0.12f, 0.10f, 0.08f);
                    die.Add(pip);
                }
            }
        }

        void ShowChoices(IReadOnlyList<string> keys)
        {
            if (keys == null) { choiceList.Clear(); choiceList.style.display = DisplayStyle.None; return; }
            if (choiceList.childCount == keys.Count) return;
            choiceList.Clear();
            MenuKit.Skin = Skin;
            for (int i = 0; i < keys.Count; i++)
            {
                int index = i;
                MenuKit.Button(choiceList, StoryText.Line(keys[i]), () => Controller.Choose(index), size: 26);   // one line: the pixel option bar with its cursor
            }
            choiceList.style.display = DisplayStyle.Flex;
            MenuKit.FocusFirst(choiceList);
        }

        void Update()
        {
            dialogue?.Tick();
            // The town's panels share one UI panel: keep the focus on an answer, not on a hidden menu's button.
            if (choiceList != null && choiceList.style.display == DisplayStyle.Flex && root.panel != null
                && !(root.panel.focusController.focusedElement is Button b && b.parent == choiceList))
            {
                // Re-sent like MenuKit.KeepFocus: a focus given before the panel's handler takes it is lost.
                var first = choiceList.Query<Button>().First();
                first?.Blur();
                first?.Focus();
            }
        }

        // ---------- bubbles ----------

        public IReadOnlyCollection<Transform> SpeechBubbles => speech.Keys;
        public IReadOnlyCollection<Transform> MarkBubbles => marks.Keys;
        public IReadOnlyCollection<Transform> GameBubbles => games.Keys;

        void LateUpdate()
        {
            var cam = Camera.main;
            if (!Controller || Controller.Run == null || root?.panel == null || !cam) return;
            bool visible = Controller.Fade < 0.01f;
            Sync(speech, bubbleLayer, visible ? Controller.Speaking : Enumerable.Empty<Transform>(), t => SpeechBubble(StoryText.Line(Controller.ResidentLineKey(TownStoryController.ResidentId(t)))));
            var alerts = Controller.StoryMarks.ToList();
            if (Controller.PlayerAlert) alerts.Add(Controller.Player.transform);
            Sync(marks, markLayer, visible ? alerts : Enumerable.Empty<Transform>(), _ => MarkBubble());
            // "?" over townsfolk with a mini-game, unless a "!" or their own speech bubble is showing.
            var gameMarks = visible ? Controller.GameMarks.Where(t => !marks.ContainsKey(t) && !speech.ContainsKey(t)) : Enumerable.Empty<Transform>();
            Sync(games, markLayer, gameMarks, _ => QuestionBubble());
            float bob = Mathf.Sin(Time.unscaledTime * 4f) * 4f;
            // The pixel "!" glints for a moment once per second.
            var alertFrame = Skin && Skin.BubbleAlertGlint && Mathf.Repeat(Time.unscaledTime, 1f) < 0.18f ? Skin.BubbleAlertGlint : Skin ? Skin.BubbleAlert : null;
            if (alertFrame) foreach (var e in marks.Values) if (e.Q(className: "alert-frame") is VisualElement f) f.style.backgroundImage = new StyleBackground(alertFrame);
            var questionFrame = Skin && Skin.BubbleQuestionGlint && Mathf.Repeat(Time.unscaledTime + 0.5f, 1f) < 0.18f ? Skin.BubbleQuestionGlint : Skin ? Skin.BubbleQuestion : null;
            if (questionFrame) foreach (var e in games.Values) if (e.Q(className: "alert-frame") is VisualElement f) f.style.backgroundImage = new StyleBackground(questionFrame);
            foreach (var pair in speech) Anchor(pair.Value, pair.Key, cam, 0f);
            foreach (var pair in marks) Anchor(pair.Value, pair.Key, cam, bob);
            foreach (var pair in games) Anchor(pair.Value, pair.Key, cam, bob * 0.5f);
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

        VisualElement QuestionBubble()
        {
            if (Skin && Skin.BubbleQuestion)
            {
                var m = Anchored();
                var img = new VisualElement { pickingMode = PickingMode.Ignore };
                img.AddToClassList("alert-frame");
                ThreeKingdoms.UI.Battle.BattleUiSkin.Image(img, Skin.BubbleQuestion);
                m.Add(img);
                return m;
            }
            var l = new Label("?") { pickingMode = PickingMode.Ignore };
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
