using UnityEngine;
using UnityEngine.UIElements;

namespace ThreeKingdoms.UI.Battle
{
    // Pixel skin for the battle HUD (BATTLE_UI_SKIN.md, Codex delivery art/battle-ui): nine-slice frames drawn at an
    // integer 2× with tiled edges and fixed corners, bar fills tiled along x. Slice sizes are the manifest's, in native
    // pixels. Without a skin the HUD keeps its flat colours.
    [CreateAssetMenu(menuName = "ThreeKingdoms/UI/Battle UI Skin")]
    public sealed class BattleUiSkin : ScriptableObject
    {
        public const float Scale = 2f;

        public Texture2D CommandPanel, PlaquePanel, HintPanel;
        public Texture2D ItemNormal, ItemSelected, ItemDisabled;
        public Texture2D CardNormal, CardActive, CardActiveGlow, CardWounded, CardRouted, RoutedSeal;
        public Texture2D PortraitFrame, PortraitFrameActive, UnitBadge;
        public Texture2D BarFrame, BarTroops, BarTroopsLow, BarSkill;
        [Header("Enemy plates (BATTLE_UI_ENEMY_SKIN.md)")]
        public Texture2D EnemyPlate, EnemyPlateBoss, EnemyPlateTarget, EnemyBarFrame, EnemyBarFill, EnemyBarFillLow, EnemyRouted;

        public bool HasEnemyPlates => EnemyPlate && EnemyBarFrame && EnemyBarFill;

        // Nine-slice a texture onto an element: corners fixed, edges and centre tiled, drawn at Scale.
        public static void Apply(VisualElement e, Texture2D tex, int left, int right, int top, int bottom)
        {
            if (!tex) return;
            e.style.backgroundImage = new StyleBackground(tex);
            e.style.unitySliceLeft = left; e.style.unitySliceRight = right;
            e.style.unitySliceTop = top; e.style.unitySliceBottom = bottom;
            e.style.unitySliceScale = Scale;
            e.style.unitySliceType = SliceType.Tiled;
            e.style.backgroundColor = Color.clear;
            e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = 0;
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = 0;
        }

        public void Panel(VisualElement e) => Apply(e, CommandPanel, 20, 20, 20, 20);
        public void Plaque(VisualElement e) => Apply(e, PlaquePanel, 10, 10, 10, 10);
        public void Hint(VisualElement e) => Apply(e, HintPanel, 10, 10, 10, 10);
        public void Item(VisualElement e, Texture2D state) => Apply(e, state, 14, 6, 6, 6);
        public void Card(VisualElement e, Texture2D state) => Apply(e, state, 24, 24, 24, 24);
        public void Portrait(VisualElement e, bool active) => Apply(e, active ? PortraitFrameActive : PortraitFrame, 8, 8, 8, 8);
        public void Bar(VisualElement e) => Apply(e, BarFrame, 4, 4, 2, 2);
        public void EnemyName(VisualElement e, Texture2D state) => Apply(e, state, 6, 6, 4, 4);
        public void EnemyBar(VisualElement e) => Apply(e, EnemyBarFrame, 3, 3, 2, 2);

        // A plain image at Scale (badge, seal) or a fill tiled along x at Scale.
        public static void Image(VisualElement e, Texture2D tex)
        {
            if (!tex) return;
            e.style.backgroundImage = new StyleBackground(tex);
            e.style.width = tex.width * Scale; e.style.height = tex.height * Scale;
        }

        public static void Fill(VisualElement e, Texture2D tex)
        {
            if (!tex) return;
            e.style.backgroundImage = new StyleBackground(tex);
            e.style.backgroundRepeat = new BackgroundRepeat(Repeat.Repeat, Repeat.NoRepeat);
            e.style.backgroundSize = new BackgroundSize(tex.width * Scale, tex.height * Scale);
            e.style.backgroundColor = Color.clear;
        }
    }
}
