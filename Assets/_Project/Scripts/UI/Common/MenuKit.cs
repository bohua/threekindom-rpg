using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace ThreeKingdoms.UI.Common
{
    // Shared look and gamepad behaviour for the out-of-battle menus (save, title): framed panels, menu buttons with a
    // visible focus that the mouse also moves, and the focus re-send the EventSystem needs on the first frames.
    public static class MenuKit
    {
        // The pixel skin out-of-battle menus draw with (BATTLE_UI_SKIN.md); null keeps the flat colours. Each menu HUD
        // sets it from its own Skin field when it is enabled.
        public static ThreeKingdoms.UI.Battle.BattleUiSkin Skin { get; set; }
        public static readonly Color Gold = new Color(0.93f, 0.76f, 0.36f);
        public static readonly Color PanelColor = new Color(0.07f, 0.09f, 0.14f, 0.94f);
        public static readonly Color Edge = new Color(0.62f, 0.50f, 0.28f);
        public static readonly Color Idle = new Color(0.12f, 0.15f, 0.22f, 0.95f);
        public static readonly Color Dim = new Color(0.35f, 0.3f, 0.2f);
        public static readonly Color Muted = new Color(0.62f, 0.62f, 0.62f);
        public static readonly Color Warn = new Color(0.9f, 0.5f, 0.42f);

        public static Button Button(VisualElement parent, string text, Action onClick, Action onFocus = null, bool enabled = true, int size = 26, float height = 48)
        {
            var b = new Button(onClick) { text = text };
            b.style.fontSize = size;
            b.style.color = enabled ? Color.white : Muted;
            b.style.backgroundColor = Idle;
            b.style.borderTopColor = b.style.borderBottomColor = b.style.borderLeftColor = b.style.borderRightColor = Dim;
            b.style.unityTextAlign = TextAnchor.MiddleLeft;
            b.style.whiteSpace = WhiteSpace.Normal;
            b.style.paddingLeft = 16; b.style.minHeight = height; b.style.marginTop = 4;
            b.SetEnabled(enabled);
            var skin = Skin;
            if (skin)
            {
                // Pixel option frames (normal / selected / disabled), same as the battle menu's text options.
                skin.Item(b, enabled ? skin.ItemNormal : skin.ItemDisabled);
                b.style.paddingLeft = 34; b.style.minHeight = Mathf.Max(48, height);
                b.RegisterCallback<FocusInEvent>(_ => { skin.Item(b, skin.ItemSelected); onFocus?.Invoke(); });
                b.RegisterCallback<FocusOutEvent>(_ => skin.Item(b, b.enabledSelf ? skin.ItemNormal : skin.ItemDisabled));
            }
            else
            {
                b.RegisterCallback<FocusInEvent>(_ =>
                {
                    b.style.backgroundColor = new Color(0.45f, 0.33f, 0.10f, 0.98f);
                    b.style.borderTopColor = b.style.borderBottomColor = b.style.borderLeftColor = b.style.borderRightColor = Gold;
                    b.style.borderLeftWidth = 6;
                    onFocus?.Invoke();
                });
                b.RegisterCallback<FocusOutEvent>(_ =>
                {
                    b.style.backgroundColor = Idle;
                    b.style.borderTopColor = b.style.borderBottomColor = b.style.borderLeftColor = b.style.borderRightColor = Dim;
                    b.style.borderLeftWidth = 1;
                });
            }
            b.RegisterCallback<PointerEnterEvent>(_ => { if (b.enabledSelf) b.Focus(); });
            parent.Add(b);
            return b;
        }

        public static Label Text(VisualElement parent, string text, int size, Color color)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.style.fontSize = size; l.style.color = color;
            l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
            l.style.whiteSpace = WhiteSpace.Normal;
            parent.Add(l);
            return l;
        }

        public static void Frame(VisualElement e)
        {
            e.style.backgroundColor = PanelColor;
            e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = 2;
            e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = Edge;
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = 4;
            e.style.paddingLeft = e.style.paddingRight = 22; e.style.paddingTop = e.style.paddingBottom = 14;
            if (Skin)
            {
                // Content starts inside the 40-px border art so options are never covered by it.
                Skin.Panel(e);
                e.style.paddingLeft = e.style.paddingRight = 40; e.style.paddingTop = e.style.paddingBottom = 36;
            }
        }

        public static void FocusFirst(VisualElement list, int index = 0)
        {
            var buttons = list.Query<Button>().Where(b => b.enabledSelf).ToList();
            if (buttons.Count > 0) buttons[Mathf.Clamp(index, 0, buttons.Count - 1)].Focus();
        }

        // Gamepad / keyboard navigation goes through the EventSystem's selection; a focus set before the panel's
        // event handler exists (first frames after a scene load or opening) is lost, so re-send it until it sticks.
        public static void KeepFocus(VisualElement root, VisualElement list)
        {
            if (root?.panel == null) return;
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject != null && root.panel.focusController.focusedElement != null) return;
            var button = list.Query<Button>().Where(b => b.enabledSelf).First();
            if (button == null) return;
            button.Blur();
            button.Focus();
        }
    }
}
