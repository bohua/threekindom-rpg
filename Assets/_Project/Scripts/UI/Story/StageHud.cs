using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.UI.Battle;
using ThreeKingdoms.UI.Common;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThreeKingdoms.UI.Story
{
    // The 0.2 overlay for the story stage and the walkable camp and Anxi (V02_HUANGJIN_PLAN §5, art/v02-ui): dialogue with
    // the speaker's illustration, choices, place-and-year cards, the march on the realm map, the goal and the prompt, and
    // the "××记住了这件事" toast (SYSTEMS_V02 §3). Reads an IStoryScreen; the 苍蓝银辉 colours of UI_STYLE_GUIDE.md.
    [RequireComponent(typeof(UIDocument))]
    public sealed class StageHud : MonoBehaviour
    {
        [Tooltip("StageController or FieldStoryController (an IStoryScreen)")] public MonoBehaviour Screen;
        public ThreeKingdoms.UI.Battle.BattleUiSkin Skin;
        public DialoguePortraitSet Portraits;
        [Tooltip("The realm map the marches play on (art/world-map-chapter1)")] public Texture2D Map;
        public Texture2D[] MarchPiece = new Texture2D[0];
        [Tooltip("Comic (连环画) panels by name (art/v02-duyou-comic)")] public Texture2D[] ComicPanels = new Texture2D[0];

        static readonly Color Base = new Color(0.063f, 0.110f, 0.188f, 0.94f);
        static readonly Color Silver = new Color(0.549f, 0.655f, 0.729f);
        static readonly Color Ivory = new Color(0.957f, 0.945f, 0.910f);
        static readonly Color Mist = new Color(0.710f, 0.784f, 0.835f);
        static readonly Color Cyan = new Color(0.384f, 0.843f, 0.910f);
        static readonly Color Gold = new Color(0.839f, 0.729f, 0.471f);
        static readonly Color Route = new Color(0.93f, 0.52f, 0.18f);

        // Node positions on the chapter map (normalised, 0–1 from the top left), so a new map only changes this table
        // (SYSTEMS_V02 §5).
        public static readonly Dictionary<string, Vector2> Nodes = new Dictionary<string, Vector2>
        {
            ["zhuojun"] = new Vector2(0.369f, 0.306f), ["guangzong"] = new Vector2(0.323f, 0.503f), ["yingchuan"] = new Vector2(0.229f, 0.741f),
            ["anxi"] = new Vector2(0.314f, 0.390f), ["wancheng"] = new Vector2(0.156f, 0.903f),
            // 0.3
            ["gaotang"] = new Vector2(0.384f, 0.520f), ["ji"] = new Vector2(0.392f, 0.270f), ["suanzao"] = new Vector2(0.271f, 0.667f),
            ["hulao"] = new Vector2(0.204f, 0.690f), ["luoyang"] = new Vector2(0.172f, 0.694f), ["pingyuan"] = new Vector2(0.411f, 0.424f),
            ["jieqiao"] = new Vector2(0.334f, 0.463f), ["zhending"] = new Vector2(0.290f, 0.460f),
            // 0.5
            ["duchang"] = new Vector2(0.5625f, 0.511f), ["xuzhou"] = new Vector2(0.483f, 0.727f),
        };

        IStoryScreen S => Screen as IStoryScreen;
        VisualElement comic, comicFrame, comicImage;
        readonly VisualElement[] letterbox = new VisualElement[2];
        public bool LetterboxShown => letterbox[0] != null && letterbox[0].style.display == DisplayStyle.Flex;
        string shownComic; float comicT;
        VisualElement root, fade, card, march, mapImage, piece, routeLayer, choiceList, toastBox, cinemaHide;
        Label cardText, objective, prompt, yearLabel, fromLabel, toLabel, toastText;
        DialogueView dialogue;
        readonly Queue<string> toasts = new Queue<string>();
        readonly Dictionary<Transform, Label> marks = new Dictionary<Transform, Label>();
        VisualElement markLayer;
        public int ShownMarks => marks.Count;
        float toastUntil;
        ChapterState watched;
        string drawnRoute;
        TianxiaFlight flight;
        string flightTried;

        public bool CardShown => card != null && card.style.display == DisplayStyle.Flex;
        public string ShownCard => CardShown ? cardText.text : null;
        public bool MarchShown => march != null && march.style.display == DisplayStyle.Flex;
        public string ShownToast => toastBox != null && toastBox.style.display == DisplayStyle.Flex ? toastText.text : null;
        public IReadOnlyList<Button> ChoiceButtons => choiceList.Query<Button>().ToList();
        public string PortraitSpeaker => dialogue?.PortraitSpeaker;

        void OnEnable()
        {
            FieldScripts.Text = StoryText.Line;   // the courier's list of open commissions
            root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;
            BattleFont.ApplyTo(root);
            root.style.flexGrow = 1;

            cinemaHide = new VisualElement { pickingMode = PickingMode.Ignore };
            Place(cinemaHide, 0, 0, 0, 0);
            root.Add(cinemaHide);
            markLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            Place(markLayer, 0, 0, 0, 0);
            cinemaHide.Add(markLayer);

            objective = new Label { pickingMode = PickingMode.Ignore };
            Panel(objective);
            objective.style.fontSize = 24; objective.style.color = Ivory;
            Place(objective, 24, 20, -1, -1);
            cinemaHide.Add(objective);

            prompt = new Label { pickingMode = PickingMode.Ignore };
            prompt.style.fontSize = 24; prompt.style.color = Gold;
            prompt.style.unityTextAlign = TextAnchor.MiddleCenter;
            Place(prompt, 0, -1, 0, 60);
            cinemaHide.Add(prompt);

            BuildMarch();
            // The 天下图 the marches fly over, read ahead on the story stage only (the walkable places hold no marches).
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == GameRun.StageScene) PrologueDiorama.Preload();
            // Comic panel (user 2026-10-25: the 鞭督邮 story as a 连环画): a paper-framed picture over a dimmed scene, the
            // dialogue box below it. Under everything else so lines, choices and fades stay on top.
            comic = new VisualElement { pickingMode = PickingMode.Ignore };
            Place(comic, 0, 0, 0, 0);
            comic.style.backgroundColor = new Color(0.04f, 0.035f, 0.03f, 0.88f);
            comic.style.alignItems = Align.Center;
            comicFrame = new VisualElement { pickingMode = PickingMode.Ignore };
            comicFrame.style.position = Position.Absolute;
            comicFrame.style.top = Length.Percent(4);
            comicFrame.style.height = Length.Percent(62);
            comicFrame.style.width = Length.Percent(62 * 4f / 3f * 9f / 16f);
            comicFrame.style.backgroundColor = new Color(0.90f, 0.85f, 0.74f);
            foreach (var side in new System.Action<float>[] { v => comicFrame.style.paddingTop = v, v => comicFrame.style.paddingBottom = v, v => comicFrame.style.paddingLeft = v, v => comicFrame.style.paddingRight = v }) side(10);
            comicImage = new VisualElement { pickingMode = PickingMode.Ignore };
            comicImage.style.flexGrow = 1;
            comicImage.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            comicFrame.Add(comicImage);
            comic.Add(comicFrame);
            comic.style.display = DisplayStyle.None;
            root.Insert(0, comic);
            dialogue = new DialogueView(root, Skin, Portraits);
            // Letterbox bars while an automatic scene plays (鞭督邮, user 2026-10-26: like the oath), under the dialogue box.
            for (int i = 0; i < 2; i++)
            {
                var bar = new VisualElement { pickingMode = PickingMode.Ignore };
                bar.style.position = Position.Absolute;
                bar.style.left = 0; bar.style.right = 0; bar.style.height = 96;
                if (i == 0) bar.style.top = 0; else bar.style.bottom = 0;
                bar.style.backgroundColor = Color.black;
                bar.style.display = DisplayStyle.None;
                root.Insert(root.IndexOf(dialogue.Box), bar);
                letterbox[i] = bar;
            }

            choiceList = new VisualElement();
            Place(choiceList, -1, -1, 300, 230);
            choiceList.style.width = 560;
            choiceList.style.display = DisplayStyle.None;
            root.Add(choiceList);

            card = new VisualElement { pickingMode = PickingMode.Ignore };
            Place(card, 0, 0, 0, 0);
            card.style.backgroundColor = Color.black;
            card.style.justifyContent = Justify.Center;
            cardText = new Label { pickingMode = PickingMode.Ignore };
            cardText.style.fontSize = 40; cardText.style.color = Ivory;
            cardText.style.unityTextAlign = TextAnchor.MiddleCenter;
            cardText.style.whiteSpace = WhiteSpace.Normal;
            cardText.style.marginLeft = cardText.style.marginRight = 200;
            card.Add(cardText);
            card.style.display = DisplayStyle.None;
            root.Add(card);

            toastBox = new VisualElement { pickingMode = PickingMode.Ignore };
            Panel(toastBox);
            Place(toastBox, -1, -1, 40, 230);
            toastBox.style.flexDirection = FlexDirection.Row;
            var mark = new Label("◆") { pickingMode = PickingMode.Ignore }; mark.style.color = Cyan; mark.style.fontSize = 18; mark.style.marginRight = 10;
            toastBox.Add(mark);
            toastText = new Label { pickingMode = PickingMode.Ignore }; toastText.style.color = Ivory; toastText.style.fontSize = 22;
            toastBox.Add(toastText);
            toastBox.style.display = DisplayStyle.None;
            root.Add(toastBox);

            fade = new VisualElement { pickingMode = PickingMode.Ignore };
            Place(fade, 0, 0, 0, 0);
            fade.style.backgroundColor = Color.black;
            root.Add(fade);
        }

        void BuildMarch()
        {
            march = new VisualElement { pickingMode = PickingMode.Ignore };
            Place(march, 0, 0, 0, 0);
            march.style.backgroundColor = new Color(0.04f, 0.06f, 0.10f);
            mapImage = new VisualElement { pickingMode = PickingMode.Ignore };
            Place(mapImage, 0, 0, 0, 0);
            if (Map) mapImage.style.backgroundImage = new StyleBackground(Map);
            mapImage.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            march.Add(mapImage);
            routeLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            Place(routeLayer, 0, 0, 0, 0);
            march.Add(routeLayer);
            piece = new VisualElement { pickingMode = PickingMode.Ignore };
            piece.style.position = Position.Absolute;
            piece.style.width = piece.style.height = 64;
            if (MarchPiece.Length > 0 && MarchPiece[0]) piece.style.backgroundImage = new StyleBackground(MarchPiece[0]);
            else
            {
                piece.style.backgroundColor = Route;
                piece.style.borderTopLeftRadius = piece.style.borderTopRightRadius = piece.style.borderBottomLeftRadius = piece.style.borderBottomRightRadius = 32;
                piece.style.width = piece.style.height = 28;
                Outline(piece, Ivory, 3);
            }
            march.Add(piece);
            var header = new VisualElement { pickingMode = PickingMode.Ignore };
            Panel(header);
            Place(header, -1, 24, -1, -1);
            header.style.left = new Length(50, LengthUnit.Percent);
            header.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
            yearLabel = new Label { pickingMode = PickingMode.Ignore }; yearLabel.style.fontSize = 34; yearLabel.style.color = Ivory;
            header.Add(yearLabel);
            march.Add(header);
            fromLabel = NodeLabel(); toLabel = NodeLabel();
            march.Add(fromLabel); march.Add(toLabel);
            var skip = new Label("［确认］跳过") { pickingMode = PickingMode.Ignore };
            skip.style.fontSize = 20; skip.style.color = Mist;
            Place(skip, -1, -1, 40, 30);
            march.Add(skip);
            march.style.display = DisplayStyle.None;
            root.Add(march);
        }

        Label NodeLabel()
        {
            var l = new Label { pickingMode = PickingMode.Ignore };
            Panel(l);
            l.style.fontSize = 24; l.style.color = Ivory;
            l.style.position = Position.Absolute;
            return l;
        }

        void Update()
        {
            dialogue?.Tick();
            var s = S;
            if (s == null) return;
            WatchMemories(s.Run?.Chapter2);
            bool cinema = s.Cinematic;
            cinemaHide.style.display = cinema || s.March != null ? DisplayStyle.None : DisplayStyle.Flex;
            objective.text = s.Objective ?? "";
            objective.style.display = string.IsNullOrEmpty(s.Objective) || s.Busy ? DisplayStyle.None : DisplayStyle.Flex;
            prompt.text = s.Busy ? "" : StoryText.Prompt(s.Prompt);
            if (s.Prompt != null && s.Prompt.StartsWith("raw:")) prompt.text = s.Busy ? "" : s.Prompt.Substring(4);
            foreach (var bar in letterbox) if (bar != null) bar.style.display = s.Letterbox && !cinema ? DisplayStyle.Flex : DisplayStyle.None;
            dialogue.PortraitsEnabled = true;   // user 2026-10-26: lines in the automatic scenes keep their portraits too
            if (s.Letterbox || s.Line.HasValue) SceneShot();   // -sceneShots only
            var line = s.Line;
            if (line.HasValue && !cinema && s.Card == null) dialogue.Show(line.Value.Speaker, line.Value.Line);
            else dialogue.Hide();
            ShowChoices(s.Choices);
            ShowComic(s.Comic);
            card.style.display = s.Card != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (s.Card != null) cardText.text = StoryText.Line(s.Card);
            fade.style.opacity = s.Fade;
            ShowMarch(s.March);
            TickToast();
            SyncMarks(Screen as IFieldMarks, s.Fade < 0.01f && !s.Busy);
            if (choiceList.style.display == DisplayStyle.Flex && root.panel != null
                && !(root.panel.focusController.focusedElement is Button b && b.parent == choiceList))
            {
                var first = choiceList.Query<Button>().First();
                first?.Blur();
                first?.Focus();
            }
        }

        public string ShownComic => comic != null && comic.style.display == DisplayStyle.Flex ? shownComic : null;

        void ShowComic(string key)
        {
            Texture2D tex = null;
            if (key != null) foreach (var t in ComicPanels) if (t && t.name == key) { tex = t; break; }
            if (key != shownComic)
            {
                shownComic = key; comicT = 0f;
                ComicShot(key);
                comicImage.style.backgroundImage = tex ? new StyleBackground(tex) : new StyleBackground(StyleKeyword.None);
            }
            comic.style.display = key != null ? DisplayStyle.Flex : DisplayStyle.None;
            comicT += Time.unscaledDeltaTime;
            comicFrame.style.opacity = Mathf.Clamp01(comicT / 0.35f);   // each new panel fades in
        }

        // -sceneShots <dir>: while an automatic scene plays, save a frame every 1.5 s (screenshots for review).
        float nextSceneShot; int sceneShots;

        void SceneShot()
        {
            if (Time.unscaledTime < nextSceneShot || sceneShots >= 40) return;
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-sceneShots");
            if (i < 0 || i + 1 >= args.Length) return;
            nextSceneShot = Time.unscaledTime + 1.5f;
            System.IO.Directory.CreateDirectory(args[i + 1]);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(args[i + 1], $"scene-{sceneShots++:00}.png"));
        }

        // -comicShots <dir>: save each comic panel as it appears (screenshots for review).
        void ComicShot(string key)
        {
            if (key == null) return;
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-comicShots");
            if (i < 0 || i + 1 >= args.Length) return;
            StartCoroutine(Shoot(System.IO.Path.Combine(args[i + 1], key + ".png")));
        }

        static System.Collections.IEnumerator Shoot(string path)
        {
            yield return new WaitForSecondsRealtime(0.7f);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            ScreenCapture.CaptureScreenshot(path);
        }

        void ShowChoices(IReadOnlyList<string> keys)
        {
            if (keys == null) { if (choiceList.childCount > 0) choiceList.Clear(); choiceList.style.display = DisplayStyle.None; return; }
            if (choiceList.childCount == keys.Count) return;
            choiceList.Clear();
            MenuKit.Skin = Skin;
            for (int i = 0; i < keys.Count; i++)
            {
                int index = i;
                MenuKit.Button(choiceList, StoryText.Line(keys[i]), () => S?.Choose(index), size: 26);
            }
            choiceList.style.display = DisplayStyle.Flex;
            MenuKit.FocusFirst(choiceList);
        }

        void ShowMarch(StagePlayer.MarchShot shot)
        {
            march.style.display = shot != null ? DisplayStyle.Flex : DisplayStyle.None;
            // On the 3D 天下图 when it is built (TianxiaFlight, user 2026-10-29); the flat chapter map otherwise.
            if (flight != null && (shot == null || flight.From != shot.From || flight.To != shot.To)) { flight.Dispose(); flight = null; }
            if (shot == null) { drawnRoute = null; flightTried = null; return; }
            if (flight == null && flightTried != shot.From + shot.To) { flightTried = shot.From + shot.To; flight = TianxiaFlight.TryCreate(march, shot.From, shot.To, shot.Year); }
            if (flight != null) { flight.Update(shot.T); MarchShots(shot); return; }
            var rect = MapRect();
            if (rect.width <= 0) return;
            Vector2 a = Point(shot.From, rect), b = Point(shot.To, rect);
            string key = shot.From + shot.To + rect;
            if (drawnRoute != key)
            {
                drawnRoute = key;
                routeLayer.Clear();
                float len = Vector2.Distance(a, b);
                int dashes = Mathf.Max(1, Mathf.FloorToInt(len / 22f));
                for (int i = 0; i < dashes; i++)
                {
                    var d = new VisualElement { pickingMode = PickingMode.Ignore };
                    d.style.position = Position.Absolute;
                    var p = Vector2.Lerp(a, b, (i + 0.5f) / dashes);
                    d.style.left = p.x - 6; d.style.top = p.y - 3; d.style.width = 12; d.style.height = 6;
                    d.style.backgroundColor = Route;
                    d.style.rotate = new Rotate(Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
                    routeLayer.Add(d);
                }
                yearLabel.text = StoryText.Line(shot.Year);
                fromLabel.text = StoryText.Line("node." + shot.From);
                toLabel.text = StoryText.Line("node." + shot.To);
                fromLabel.style.left = a.x + 18; fromLabel.style.top = a.y - 20;
                toLabel.style.left = b.x + 18; toLabel.style.top = b.y - 20;
            }
            var at = Vector2.Lerp(a, b, Mathf.SmoothStep(0, 1, shot.T));
            float size = piece.resolvedStyle.width > 0 ? piece.resolvedStyle.width : 28;
            piece.style.left = at.x - size / 2; piece.style.top = at.y - size / 2 - Mathf.Abs(Mathf.Sin(Time.unscaledTime * 8f)) * 4f;
            if (MarchPiece.Length > 1)
            {
                int frame = Mathf.FloorToInt(Time.unscaledTime * 8f) % MarchPiece.Length;
                if (MarchPiece[frame]) piece.style.backgroundImage = new StyleBackground(MarchPiece[frame]);
            }
        }

        // Where the map image sits inside the screen (scaled to fit, centred).
        Rect MapRect()
        {
            float w = march.resolvedStyle.width, h = march.resolvedStyle.height;
            if (float.IsNaN(w) || w <= 0 || !Map) return new Rect(0, 0, w, h);
            float scale = Mathf.Min(w / Map.width, h / Map.height);
            float mw = Map.width * scale, mh = Map.height * scale;
            return new Rect((w - mw) / 2f, (h - mh) / 2f, mw, mh);
        }

        static Vector2 Point(string node, Rect r)
        {
            var n = Nodes.TryGetValue(node ?? "", out var v) ? v : new Vector2(0.5f, 0.5f);
            return new Vector2(r.x + n.x * r.width, r.y + n.y * r.height);
        }

        // A "!" over people with something pending (a commission, the courier), like the Zhuojun story marks.
        void SyncMarks(IFieldMarks f, bool visible)
        {
            var want = f != null && visible && f.MarkCamera ? new HashSet<Transform>(f.Marks) : new HashSet<Transform>();
            foreach (var gone in marks.Keys.Where(t => !want.Contains(t)).ToList()) { marks[gone].RemoveFromHierarchy(); marks.Remove(gone); }
            foreach (var t in want)
            {
                if (!marks.TryGetValue(t, out var label))
                {
                    label = new Label("！") { pickingMode = PickingMode.Ignore };
                    label.style.position = Position.Absolute;
                    label.style.fontSize = 34; label.style.color = Gold;
                    label.style.unityFontStyleAndWeight = FontStyle.Bold;
                    label.style.unityTextOutlineWidth = 2; label.style.unityTextOutlineColor = new Color(0.05f, 0.08f, 0.14f);
                    markLayer.Add(label);
                    marks[t] = label;
                }
                if (root.panel == null) continue;
                var p = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, t.position + Vector3.up * 2.4f, f.MarkCamera);
                label.style.left = p.x - 10; label.style.top = p.y - 40 + Mathf.Sin(Time.unscaledTime * 4f) * 4f;
            }
        }

        void WatchMemories(ChapterState chapter)
        {
            if (chapter == watched) return;
            if (watched != null) watched.Remembered -= OnRemembered;
            watched = chapter;
            if (watched != null) watched.Remembered += OnRemembered;
        }

        // -marchShots <dir> (with -stage): frames of the 天下图 flight for review.
        int marchShot;
        static readonly float[] MarchShotAt = { 0.05f, 0.14f, 0.3f, 0.55f, 0.72f, 0.82f, 0.93f };
        void MarchShots(StagePlayer.MarchShot shot)
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-marchShots");
            if (i < 0 || i + 1 >= args.Length || marchShot >= MarchShotAt.Length || shot.T < MarchShotAt[marchShot]) return;
            System.IO.Directory.CreateDirectory(args[i + 1]);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(args[i + 1], $"march-{shot.From}-{shot.To}-{marchShot++}.png"));
        }

        void OnDisable() { flight?.Dispose(); flight = null; PrologueDiorama.ReleasePreload(); if (watched != null) watched.Remembered -= OnRemembered; watched = null; }

        void OnRemembered(Memory m) => toasts.Enqueue(ChapterText.Remembered(m.Who));

        void TickToast()
        {
            if (toastBox.style.display == DisplayStyle.Flex && Time.unscaledTime < toastUntil) return;
            if (toasts.Count == 0) { toastBox.style.display = DisplayStyle.None; return; }
            toastText.text = toasts.Dequeue();
            toastBox.style.display = DisplayStyle.Flex;
            toastUntil = Time.unscaledTime + 2f;   // SYSTEMS_V02 §3.2: two seconds
        }

        static void Panel(VisualElement e)
        {
            e.style.backgroundColor = Base;
            Outline(e, Silver, 2);
            e.style.paddingLeft = e.style.paddingRight = 22; e.style.paddingTop = e.style.paddingBottom = 10;
        }

        static void Outline(VisualElement e, Color c, float w)
        {
            e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = w;
            e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = c;
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
