using UnityEngine;
using UnityEngine.UIElements;

namespace ThreeKingdoms.UI.Story
{
    // The dialogue box with the speaker's illustration beside it (user 2026-10-22), used in town and after a battle:
    // the illustration sits under the box, slides in from its side on a new speaker, then sways in the wind and
    // breathes. Speaker and line arrive as keys and are turned into text by StoryText.
    public sealed class DialogueView
    {
        const float PortraitHeight = 700f, PortraitBottom = 150f, PortraitInset = 20f, PortraitIn = 0.22f;
        static readonly Color Gold = new Color(0.93f, 0.76f, 0.36f);
        static readonly Color PanelColor = new Color(0.07f, 0.09f, 0.14f, 0.9f);
        static readonly Color Edge = new Color(0.62f, 0.50f, 0.28f);

        readonly DialoguePortraitSet portraits;
        readonly WindPortrait portrait;
        readonly VisualElement box;
        readonly Label speaker, line;
        string portraitSpeaker;
        float portraitShownAt;
        bool portraitRight;

        public VisualElement Box => box;
        public bool Visible => box.style.display == DisplayStyle.Flex;
        // The speaker whose illustration is up, or null.
        public string PortraitSpeaker => portrait.style.display == DisplayStyle.Flex ? portraitSpeaker : null;
        public Texture2D PortraitTexture => PortraitSpeaker != null ? portrait.Texture : null;

        public DialogueView(VisualElement parent, ThreeKingdoms.UI.Battle.BattleUiSkin skin, DialoguePortraitSet portraits)
        {
            this.portraits = portraits;
            // Under the dialogue box so the box covers the figure's waist.
            portrait = new WindPortrait();
            portrait.style.position = Position.Absolute;
            portrait.style.bottom = PortraitBottom;
            portrait.style.height = PortraitHeight;
            portrait.style.display = DisplayStyle.None;
            parent.Add(portrait);

            box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.style.position = Position.Absolute;
            box.style.left = 240; box.style.right = 240; box.style.bottom = 40;
            box.style.backgroundColor = PanelColor;
            box.style.borderTopWidth = box.style.borderBottomWidth = box.style.borderLeftWidth = box.style.borderRightWidth = 2;
            box.style.borderTopColor = box.style.borderBottomColor = box.style.borderLeftColor = box.style.borderRightColor = Edge;
            box.style.borderTopLeftRadius = box.style.borderTopRightRadius = box.style.borderBottomLeftRadius = box.style.borderBottomRightRadius = 4;
            box.style.paddingLeft = box.style.paddingRight = 22; box.style.paddingTop = box.style.paddingBottom = 12;
            if (skin) { skin.Panel(box); box.style.paddingLeft = box.style.paddingRight = 44; box.style.paddingTop = box.style.paddingBottom = 36; }
            box.style.minHeight = 170;
            speaker = Text(box, 26, Gold);
            line = Text(box, 28, Color.white);
            line.style.whiteSpace = WhiteSpace.Normal; line.style.marginTop = 10;
            var advance = Text(box, 18, new Color(0.8f, 0.8f, 0.8f));
            advance.text = StoryText.Advance;
            advance.style.position = Position.Absolute; advance.style.right = 44; advance.style.bottom = 16;
            parent.Add(box);
            Hide();
        }

        public void Show(string speakerKey, string lineKey)
        {
            string who = StoryText.Speaker(speakerKey);
            speaker.text = who;
            speaker.style.display = string.IsNullOrEmpty(who) ? DisplayStyle.None : DisplayStyle.Flex;
            line.text = StoryText.Line(lineKey);
            box.style.display = DisplayStyle.Flex;
            ShowPortrait(speakerKey);
        }

        public void Hide()
        {
            box.style.display = DisplayStyle.None;
            ShowPortrait(null);
        }

        void ShowPortrait(string key)
        {
            if (key == null || !portraits || !portraits.TryGet(key, out var p)) { portrait.style.display = DisplayStyle.None; portraitSpeaker = null; return; }
            if (key == portraitSpeaker && portrait.style.display == DisplayStyle.Flex) return;
            float crop = p.Crop > 0 ? p.Crop : 0.6f, axis = p.Axis > 0 ? p.Axis : 0.5f;
            portrait.Show(p.Texture, p.Flip, crop, axis);
            portrait.style.width = PortraitHeight * p.Texture.width / (p.Texture.height * crop);
            portraitRight = p.Right;
            if (p.Right) { portrait.style.left = StyleKeyword.Auto; portrait.style.right = PortraitInset; }
            else { portrait.style.right = StyleKeyword.Auto; portrait.style.left = PortraitInset; }
            portrait.style.display = DisplayStyle.Flex;
            portraitSpeaker = key;
            portraitShownAt = Time.unscaledTime;
        }

        // Call every frame: the slide-in, the breathing bob and the wind.
        public void Tick()
        {
            if (portrait.style.display != DisplayStyle.Flex) return;
            float k = Mathf.Clamp01((Time.unscaledTime - portraitShownAt) / PortraitIn);
            k = 1f - (1f - k) * (1f - k);
            portrait.style.opacity = k;
            float slide = (1f - k) * 60f * (portraitRight ? 1f : -1f);
            portrait.style.translate = new Translate(slide, Mathf.Sin(Time.unscaledTime * 1.4f) * 4f);
            portrait.Tick(Time.unscaledTime);
        }

        static Label Text(VisualElement parent, int size, Color color)
        {
            var l = new Label { pickingMode = PickingMode.Ignore };
            l.style.fontSize = size; l.style.color = color;
            l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
            parent.Add(l);
            return l;
        }
    }
}
