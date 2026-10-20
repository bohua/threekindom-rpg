using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.UI.Battle;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThreeKingdoms.UI.Story
{
    // Town overlay (UI Toolkit): objective top-left, interaction prompt, dialogue box and the fade used by the oath
    // and by leaving town. Reads TownStoryController and never changes story state.
    [RequireComponent(typeof(UIDocument))]
    public sealed class TownHud : MonoBehaviour
    {
        public TownStoryController Controller;

        static readonly Color Gold = new Color(0.93f, 0.76f, 0.36f);
        static readonly Color Panel = new Color(0.07f, 0.09f, 0.14f, 0.9f);
        static readonly Color Edge = new Color(0.62f, 0.50f, 0.28f);

        VisualElement root, dialogue, fade;
        Label objective, prompt, speaker, line, advance;

        void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;
            BattleFont.ApplyTo(root);
            root.style.flexGrow = 1;

            objective = Label(root, 24, Color.white);
            Place(objective, 24, 20, -1, -1);
            Frame(objective);

            prompt = Label(root, 24, Gold);
            Place(prompt, 0, -1, 0, 60);
            prompt.style.unityTextAlign = TextAnchor.MiddleCenter;

            dialogue = new VisualElement { pickingMode = PickingMode.Ignore };
            Place(dialogue, 240, -1, 240, 40);
            Frame(dialogue);
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
            if (!Controller || Controller.Run == null) { fade.style.opacity = 0; dialogue.style.display = DisplayStyle.None; return; }
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
            fade.style.opacity = Controller.Fade;
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
