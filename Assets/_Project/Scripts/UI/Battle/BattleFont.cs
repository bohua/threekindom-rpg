using UnityEngine;
using UnityEngine.UIElements;

namespace ThreeKingdoms.UI.Battle
{
    // TEMPORARY CJK font until ADR-0007 ships a bundled font: a dynamic font from Windows system fonts.
    // Development / validation builds only (ADR-0002 §8).
    public static class BattleFont
    {
        static readonly string[] Families = { "Microsoft YaHei", "Microsoft YaHei UI", "SimHei", "SimSun", "DengXian" };
        static Font font;

        public static void ApplyTo(VisualElement root)
        {
            if (!font)
            {
                font = Font.CreateDynamicFontFromOSFont(Families, 32);
                if (!font) Debug.LogError("BattleFont: no CJK system font found (" + string.Join(", ", Families) + ")");
            }
            if (font) root.style.unityFontDefinition = FontDefinition.FromFont(font);
        }
    }
}
