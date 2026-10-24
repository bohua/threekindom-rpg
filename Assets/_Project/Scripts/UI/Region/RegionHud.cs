using ThreeKingdoms.Gameplay;
using ThreeKingdoms.UI.Battle;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThreeKingdoms.UI.Region
{
    // Field HUD for open regions (user 2026-10-24): a signboard top left naming where the player is — town · spot, or
    // region · road "A ↔ B" — and the region's mini map top right with the player's marker. Unified UI style
    // (UI_STYLE_GUIDE.md): midnight-indigo panels, thin silver frame, ivory text, cyan for the separator, arrow and marker.
    [RequireComponent(typeof(UIDocument))]
    public sealed class RegionHud : MonoBehaviour
    {
        public Texture2D Map;
        public string RegionName = "渤海平原";
        [Tooltip("World bounds the map image covers: x min, z min, x max, z max")] public Vector4 MapBounds;
        public Transform Player;
        [Tooltip("Names laid over the map at these world positions")] public string[] PlaceNames;
        public Vector3[] PlacePositions;

        static readonly Color Base = new Color32(0x10, 0x1C, 0x30, 0xE6), Inset = new Color32(0x0B, 0x14, 0x23, 0xFF),
            Ivory = new Color32(0xF4, 0xF1, 0xE8, 0xFF), Mist = new Color32(0xB5, 0xC8, 0xD5, 0xFF),
            Silver = new Color32(0x8C, 0xA7, 0xBA, 0xFF), Cyan = new Color32(0x62, 0xD7, 0xE8, 0xFF);

        VisualElement sign, minorRow, marker;
        Label major;
        LocationZone current;
        float fade;

        public string SignText { get; private set; }

        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            BattleFont.ApplyTo(root);
            root.pickingMode = PickingMode.Ignore;
            root.style.flexGrow = 1;

            sign = Panel(root);
            sign.style.position = Position.Absolute; sign.style.left = 36; sign.style.top = 30;
            sign.style.flexDirection = FlexDirection.Row; sign.style.alignItems = Align.Center;
            sign.style.paddingLeft = 22; sign.style.paddingRight = 26; sign.style.paddingTop = 10; sign.style.paddingBottom = 10;
            major = Text(sign, "", 30, Ivory);
            major.style.unityFontStyleAndWeight = FontStyle.Bold;
            var dot = new VisualElement();
            dot.style.width = 8; dot.style.height = 8; dot.style.marginLeft = 14; dot.style.marginRight = 14;
            dot.style.backgroundColor = Cyan;
            SetRadius(dot, 4);
            sign.Add(dot);
            minorRow = new VisualElement();
            minorRow.style.flexDirection = FlexDirection.Row; minorRow.style.alignItems = Align.Center;
            sign.Add(minorRow);
            // Small cloud-corner ticks on the frame's left end (restrained, after the approved style).
            foreach (float top in new[] { -1f, 1f })
            {
                var tick = new VisualElement();
                tick.style.position = Position.Absolute; tick.style.left = 6; tick.style.width = 8; tick.style.height = 8;
                if (top < 0) tick.style.top = 6; else tick.style.bottom = 6;
                tick.style.borderLeftWidth = 2; tick.style.borderLeftColor = Silver;
                if (top < 0) { tick.style.borderTopWidth = 2; tick.style.borderTopColor = Silver; }
                else { tick.style.borderBottomWidth = 2; tick.style.borderBottomColor = Silver; }
                sign.Add(tick);
            }

            if (Map)
            {
                var frame = Panel(root);
                frame.style.position = Position.Absolute; frame.style.right = 36; frame.style.top = 30;
                frame.style.paddingLeft = 8; frame.style.paddingRight = 8; frame.style.paddingTop = 6; frame.style.paddingBottom = 8;
                var caption = Text(frame, RegionName, 18, Mist);
                caption.style.marginBottom = 4;
                float w = 400f, h = w * Map.height / Map.width;
                var map = new VisualElement();
                map.style.width = w; map.style.height = h;
                map.style.backgroundImage = new StyleBackground(Map);
                map.style.backgroundColor = Inset;
                map.style.overflow = Overflow.Hidden;
                map.style.borderTopWidth = map.style.borderBottomWidth = map.style.borderLeftWidth = map.style.borderRightWidth = 1;
                map.style.borderTopColor = map.style.borderBottomColor = map.style.borderLeftColor = map.style.borderRightColor = new Color(Silver.r, Silver.g, Silver.b, 0.6f);
                frame.Add(map);
                for (int i = 0; PlaceNames != null && i < PlaceNames.Length && i < PlacePositions.Length; i++)
                {
                    var name = Text(map, PlaceNames[i], 15, Mist);
                    name.style.position = Position.Absolute;
                    name.style.unityTextOutlineWidth = 0.6f; name.style.unityTextOutlineColor = Inset;
                    float pu = Mathf.InverseLerp(MapBounds.x, MapBounds.z, PlacePositions[i].x), pv = Mathf.InverseLerp(MapBounds.y, MapBounds.w, PlacePositions[i].z);
                    name.style.left = pu * w; name.style.top = (1f - pv) * h;
                    name.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
                }
                marker = new VisualElement();
                marker.style.position = Position.Absolute; marker.style.width = 12; marker.style.height = 12;
                marker.style.backgroundColor = Cyan;
                marker.style.borderTopWidth = marker.style.borderBottomWidth = marker.style.borderLeftWidth = marker.style.borderRightWidth = 2;
                marker.style.borderTopColor = marker.style.borderBottomColor = marker.style.borderLeftColor = marker.style.borderRightColor = Ivory;
                marker.style.rotate = new Rotate(45f);   // a diamond pointer, as in the unified style
                map.Add(marker);
            }
            current = null; SignText = null;
        }

        void Update()
        {
            if (!Player) { var mover = FindFirstObjectByType<Jrpg.PlayerMover>(); if (mover) Player = mover.transform; }
            if (!Player) return;
            var zone = LocationZone.Find(Player.position);
            if (zone != current) { current = zone; Show(zone); fade = 0f; }
            if (fade < 1f) { fade = Mathf.Min(1f, fade + Time.unscaledDeltaTime * 4f); sign.style.opacity = fade; }
            if (marker != null)
            {
                float u = Mathf.InverseLerp(MapBounds.x, MapBounds.z, Player.position.x), v = Mathf.InverseLerp(MapBounds.y, MapBounds.w, Player.position.z);
                var map = marker.parent;
                marker.style.left = u * map.resolvedStyle.width - 6f;
                marker.style.top = (1f - v) * map.resolvedStyle.height - 6f;
            }
        }

        void Show(LocationZone zone)
        {
            major.text = zone ? zone.Major : RegionName;
            minorRow.Clear();
            string minor = zone ? zone.Minor : "野外";
            // A road "A ↔ B": both ends in ivory, the double arrow in cyan.
            var parts = minor.Split('↔');
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0) { var arrow = Text(minorRow, "↔", 28, Cyan); arrow.style.marginLeft = 10; arrow.style.marginRight = 10; }
                Text(minorRow, parts[i].Trim(), 26, Ivory);
            }
            SignText = $"{major.text} · {minor}";
        }

        static VisualElement Panel(VisualElement parent)
        {
            var p = new VisualElement();
            p.pickingMode = PickingMode.Ignore;
            p.style.backgroundColor = Base;
            p.style.borderTopWidth = p.style.borderBottomWidth = p.style.borderLeftWidth = p.style.borderRightWidth = 1;
            p.style.borderTopColor = p.style.borderBottomColor = p.style.borderLeftColor = p.style.borderRightColor = Silver;
            SetRadius(p, 2);
            parent.Add(p);
            return p;
        }

        static Label Text(VisualElement parent, string text, int size, Color color)
        {
            var l = new Label(text);
            l.pickingMode = PickingMode.Ignore;
            l.style.fontSize = size; l.style.color = color;
            l.style.marginLeft = l.style.marginRight = l.style.paddingLeft = l.style.paddingRight = 0;
            parent.Add(l);
            return l;
        }

        static void SetRadius(VisualElement e, float r) =>
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;
    }
}
