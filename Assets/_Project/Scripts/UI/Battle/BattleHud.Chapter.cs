using System.Collections.Generic;
using ThreeKingdoms.Core.Battle;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThreeKingdoms.UI.Battle
{
    // 0.2 additions to the battle HUD (SYSTEMS_V02 §1–§2, art/v02-ui): the turn-goal bar top left, the event banner top
    // centre, the lines a battle event plays, and the ally card with the escort mark. Colours follow the unified
    // 苍蓝银辉 UI style (UI_STYLE_GUIDE.md): midnight blue, silver hairline, ivory text, cyan for the escort.
    public sealed partial class BattleHud
    {
        static readonly Color StyleBase = new Color(0.063f, 0.110f, 0.188f, 0.92f);     // #101C30
        static readonly Color StyleSilver = new Color(0.549f, 0.655f, 0.729f);          // #8CA7BA
        static readonly Color StyleIvory = new Color(0.957f, 0.945f, 0.910f);           // #F4F1E8
        static readonly Color StyleCyan = new Color(0.384f, 0.843f, 0.910f);            // #62D7E8
        static readonly Color StyleGold = new Color(0.839f, 0.729f, 0.471f);            // #D6BA78
        const float AutoLineSeconds = 1.6f;

        Label objectiveLabel, bannerLabel;
        VisualElement objectiveBox, bannerBand;
        float bannerUntil;
        bool eventDialogue;
        float autoAdvanceAt;

        public string ShownObjective => objectiveBox != null && objectiveBox.style.display == DisplayStyle.Flex ? objectiveLabel.text : null;
        public string ShownBanner => bannerBand != null && bannerBand.style.display == DisplayStyle.Flex ? bannerLabel.text : null;
        public bool EventDialoguePlaying => eventDialogue && DialoguePlaying;

        void BuildChapterLayout()
        {
            objectiveBox = new VisualElement { pickingMode = PickingMode.Ignore };
            Absolute(objectiveBox, 24, 120, -1, -1);
            Hairline(objectiveBox, StyleSilver);
            objectiveBox.style.backgroundColor = StyleBase;
            objectiveBox.style.paddingLeft = objectiveBox.style.paddingRight = 20;
            objectiveBox.style.paddingTop = objectiveBox.style.paddingBottom = 10;
            objectiveBox.style.flexDirection = FlexDirection.Row;
            var diamond = Text("◆", 18, StyleCyan); diamond.style.marginRight = 10; diamond.style.unityTextAlign = TextAnchor.MiddleLeft;
            objectiveBox.Add(diamond);
            objectiveLabel = Text("", 22, StyleIvory);
            objectiveBox.Add(objectiveLabel);
            objectiveBox.style.display = DisplayStyle.None;
            root.Add(objectiveBox);

            bannerBand = new VisualElement { pickingMode = PickingMode.Ignore };
            Absolute(bannerBand, 0, 210, 0, -1);
            bannerBand.style.height = 110;
            bannerBand.style.justifyContent = Justify.Center;
            bannerBand.style.backgroundColor = new Color(StyleBase.r, StyleBase.g, StyleBase.b, 0.82f);
            bannerBand.style.borderTopWidth = bannerBand.style.borderBottomWidth = 2;
            bannerBand.style.borderTopColor = bannerBand.style.borderBottomColor = StyleSilver;
            bannerLabel = Text("", 60, StyleIvory);
            bannerLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            bannerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            bannerLabel.style.unityTextOutlineWidth = 2; bannerLabel.style.unityTextOutlineColor = new Color(0, 0, 0, 0.8f);
            bannerBand.Add(bannerLabel);
            bannerBand.style.display = DisplayStyle.None;
            root.Add(bannerBand);
        }

        static void Hairline(VisualElement e, Color c)
        {
            e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = 2;
            e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = c;
        }

        void RefreshChapter(BattleState state)
        {
            bool show = !string.IsNullOrEmpty(state.Objective) && Controller.Current != Gameplay.Battle.BattleController.Phase.Finished;
            objectiveBox.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (show) objectiveLabel.text = state.Objective;
            // Enemy plates name a status (混乱) beside the commander's name.
            foreach (var pair in plateOf)
            {
                if (pair.Value.Label == null) continue;
                string mark = "";
                foreach (var kv in pair.Key.Statuses) mark += $"〔{BattleText.StatusName(kv.Key)}〕";
                pair.Value.Name.text = pair.Value.Label + mark;
            }
        }

        void ShowBanner(string text)
        {
            bannerLabel.text = text;
            bannerBand.style.display = DisplayStyle.Flex;
            bannerUntil = Time.unscaledTime + 1.5f / Mathf.Max(1f, Controller.PlaybackRate);
        }

        // A battle event's short lines (SYSTEMS_V02 §1.2 dialogue(id)): the battle waits until they are read; with auto
        // battle on they move on by themselves.
        void PlayEventDialogue(IReadOnlyList<Gameplay.Story.DialogueLine> lines)
        {
            aftermath = lines;
            aftermathStep = 0;
            eventDialogue = true;
            dialogueCatcher.style.display = DisplayStyle.Flex;
            dialogue.Show(lines[0].Speaker, lines[0].Line);
            dialogueCatcher.Focus();
            autoAdvanceAt = Time.unscaledTime + AutoLineSeconds;
        }

        void TickChapter()
        {
            if (bannerBand != null && bannerBand.style.display == DisplayStyle.Flex && Time.unscaledTime > bannerUntil)
                bannerBand.style.display = DisplayStyle.None;
            if (eventDialogue && Controller.Auto && DialoguePlaying && Time.unscaledTime > autoAdvanceAt) AdvanceDialogue();
        }

        // The ally's card: the same layout as ours, an escort frame and the 护 mark, no tech worth showing.
        Card MakeAllyCard(BattleUnit u)
        {
            var c = MakeCard(u);
            bool escort = false;
            foreach (var e in Controller.Session.State.Escorts) if (e == u) escort = true;
            if (escort)
            {
                Hairline(c.Root, StyleCyan);
                var mark = Text(BattleText.EscortMark, 24, StyleBase);
                mark.style.position = Position.Absolute;
                mark.style.left = 6; mark.style.top = 6;
                mark.style.width = 34; mark.style.height = 34;
                mark.style.unityTextAlign = TextAnchor.MiddleCenter;
                mark.style.backgroundColor = StyleCyan;
                mark.style.borderTopLeftRadius = mark.style.borderTopRightRadius = mark.style.borderBottomLeftRadius = mark.style.borderBottomRightRadius = 4;
                mark.pickingMode = PickingMode.Ignore;
                c.Root.Add(mark);
            }
            c.Name.text = $"{u.Name}   {BattleText.AllyCaption}";
            return c;
        }
    }
}
