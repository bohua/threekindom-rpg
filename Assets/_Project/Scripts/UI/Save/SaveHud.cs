using System;
using System.Collections.Generic;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.Save;
using ThreeKingdoms.UI.Battle;
using ThreeKingdoms.UI.Common;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThreeKingdoms.UI.Save
{
    // Manual save at the tavern keeper N05 (save-system GDD): the three slots with their summaries, a confirmation
    // before overwriting (answer 2), and a clear message when a slot cannot be read. Cancel closes the panel.
    [RequireComponent(typeof(UIDocument))]
    public sealed class SaveHud : MonoBehaviour
    {
        [Tooltip("Pixel skin (BATTLE_UI_SKIN.md); empty keeps the flat style")] public ThreeKingdoms.UI.Battle.BattleUiSkin Skin;
        [Tooltip("TownStoryController or FieldStoryController (0.2): any IFieldHost")] public MonoBehaviour Host;
        ThreeKingdoms.Gameplay.Story.IFieldHost Controller => Host as ThreeKingdoms.Gameplay.Story.IFieldHost;

        public enum Mode { Closed, Slots, Confirm }

        VisualElement root, panel, list;
        Label message;
        int openedFrame, pendingSlot, lastIndex;

        public Mode Current { get; private set; } = Mode.Closed;
        public string Message => message?.text;
        public IReadOnlyList<Button> Entries => list.Query<Button>().ToList();

        void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            MenuKit.Skin = Skin;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;
            BattleFont.ApplyTo(root);
            root.style.flexGrow = 1;

            panel = new VisualElement();
            panel.style.position = Position.Absolute;
            panel.style.left = 360; panel.style.right = 360; panel.style.top = 120; panel.style.bottom = 120;
            MenuKit.Frame(panel);
            root.Add(panel);
            MenuKit.Text(panel, SaveText.SaveTitle, 34, MenuKit.Gold);
            list = new VisualElement();
            list.style.flexGrow = 1; list.style.marginTop = 16;
            panel.Add(list);
            var footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row; footer.style.justifyContent = Justify.SpaceBetween;
            panel.Add(footer);
            message = MenuKit.Text(footer, "", 24, MenuKit.Gold);
            message.style.flexShrink = 1;
            MenuKit.Text(footer, SaveText.Back, 20, MenuKit.Muted);

            root.RegisterCallback<NavigationCancelEvent>(e => { e.StopPropagation(); Back(); }, TrickleDown.TrickleDown);
            if (Controller != null) { Controller.PanelOpened += OnOpened; Controller.PanelClosed += OnClosed; }
            Hide();
        }

        void OnDisable()
        {
            if (Controller != null) { Controller.PanelOpened -= OnOpened; Controller.PanelClosed -= OnClosed; }
        }

        void OnOpened(string p) { if (p == TownStoryController.SavePanel) Open(); }
        void OnClosed(string p) { if (p == TownStoryController.SavePanel) Hide(); }

        void Open()
        {
            panel.style.display = DisplayStyle.Flex;
            openedFrame = Time.frameCount;
            lastIndex = 0;
            message.text = SaveText.Greeting;
            ShowSlots();
        }

        void Hide()
        {
            Current = Mode.Closed;
            panel.style.display = DisplayStyle.None;
            list.Clear();
        }

        public void Back()
        {
            if (Current == Mode.Confirm) ShowSlots();
            else if (Current == Mode.Slots) Controller.ClosePanel();
        }

        void ShowSlots()
        {
            Current = Mode.Slots;
            list.Clear();
            var slots = Controller.Run.Slots();
            for (int i = 1; i <= slots.ManualCount; i++)
            {
                int index = i;
                var status = slots.Read(SaveSlots.Manual(index), out var data);
                string body = status == SaveReadStatus.Ok ? SaveText.Summary(data) : SaveText.Status(status);
                Entry($"{SaveText.Slot(index)}\n{body}", () => Choose(index), index - 1);
            }
            MenuKit.FocusFirst(list, lastIndex);
        }

        // An occupied slot (even an unreadable one) asks before it is replaced.
        public void Choose(int index)
        {
            if (Controller.Run.Slots().Exists(SaveSlots.Manual(index))) { pendingSlot = index; ShowConfirm(); }
            else Write(index);
        }

        void ShowConfirm()
        {
            Current = Mode.Confirm;
            list.Clear();
            message.text = SaveText.ConfirmOverwrite(pendingSlot);
            Entry(SaveText.Overwrite, () => Write(pendingSlot), -1);
            Entry(SaveText.Cancel, ShowSlots, -1);
            MenuKit.FocusFirst(list);
        }

        public void ConfirmOverwrite() { if (Current == Mode.Confirm) Write(pendingSlot); }

        void Write(int index)
        {
            try
            {
                Controller.Run.SaveTo(SaveSlots.Manual(index), Controller.SaveLocation);
                message.text = SaveText.Saved(index);
            }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException)
            {
                message.text = SaveText.SaveFailed(e.Message);
            }
            lastIndex = index - 1;
            ShowSlots();
        }

        void Entry(string text, Action onClick, int index)
        {
            MenuKit.Button(list, text, () => { if (Time.frameCount > openedFrame) onClick(); }, () => { if (index >= 0) lastIndex = index; }, height: 72);
        }

        void Update()
        {
            if (Current != Mode.Closed) MenuKit.KeepFocus(root, list);
        }
    }
}
