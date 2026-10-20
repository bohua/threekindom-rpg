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
    // Title screen: new game, load (the rolling pre-battle save plus the manual slots, each with its summary), quit.
    // An unreadable save shows why and stays on screen; the file is never changed (save-system rule 7).
    [RequireComponent(typeof(UIDocument))]
    public sealed class TitleHud : MonoBehaviour
    {
        public TitleController Controller;

        public enum Mode { Main, Load }

        VisualElement root, list;
        Label message;
        int openedFrame;

        public Mode Current { get; private set; }
        public string Message => message?.text;
        public IReadOnlyList<Button> Entries => list.Query<Button>().ToList();

        void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            BattleFont.ApplyTo(root);
            root.style.flexGrow = 1;
            root.style.backgroundColor = new Color(0.04f, 0.05f, 0.08f);
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;

            var title = MenuKit.Text(root, SaveText.GameTitle, 96, MenuKit.Gold);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            var sub = MenuKit.Text(root, SaveText.Subtitle, 26, MenuKit.Muted);
            sub.style.marginTop = 8; sub.style.marginBottom = 48;

            list = new VisualElement();
            list.style.width = 720;
            root.Add(list);
            message = MenuKit.Text(root, "", 24, MenuKit.Warn);
            message.style.marginTop = 20; message.style.maxWidth = 900; message.style.unityTextAlign = TextAnchor.MiddleCenter;

            root.RegisterCallback<NavigationCancelEvent>(e => { e.StopPropagation(); if (Current == Mode.Load) ShowMain(); }, TrickleDown.TrickleDown);
            openedFrame = Time.frameCount;
            ShowMain();
        }

        public void ShowMain()
        {
            Current = Mode.Main;
            list.Clear();
            Entry(SaveText.NewGame, Controller.NewGame);
            Entry(SaveText.Load, ShowLoad);
            Entry(SaveText.Quit, Controller.Quit);
            MenuKit.FocusFirst(list);
        }

        public void ShowLoad()
        {
            Current = Mode.Load;
            list.Clear();
            message.text = "";
            var slots = Controller.Slots;
            bool any = false;
            any |= SlotEntry(SaveText.Auto, SaveSlots.Auto, slots);
            for (int i = 1; i <= slots.ManualCount; i++) any |= SlotEntry(SaveText.Slot(i), SaveSlots.Manual(i), slots);
            if (!any) message.text = SaveText.NoSaves;
            MenuKit.FocusFirst(list);
        }

        bool SlotEntry(string name, string slot, SaveSlots slots)
        {
            var status = slots.Read(slot, out var data);
            string body = status == SaveReadStatus.Ok ? SaveText.Summary(data) : SaveText.Status(status);
            var b = MenuKit.Button(list, $"{name}\n{body}", () => { if (Time.frameCount > openedFrame) Load(slot); },
                enabled: status != SaveReadStatus.Empty, height: 72);
            if (status == SaveReadStatus.Damaged || status == SaveReadStatus.Unsupported) b.style.color = MenuKit.Warn;
            return status != SaveReadStatus.Empty;
        }

        public void Load(string slot)
        {
            var status = Controller.Load(slot, out string error);
            if (status == SaveReadStatus.Ok) return;   // the saved scene is loading
            message.text = error != null ? SaveText.LoadFailed(error) : SaveText.Status(status);
        }

        void Entry(string text, Action onClick) =>
            MenuKit.Button(list, text, () => { if (Time.frameCount > openedFrame) onClick(); }, size: 30, height: 60);

        void Update() => MenuKit.KeepFocus(root, list);
    }
}
