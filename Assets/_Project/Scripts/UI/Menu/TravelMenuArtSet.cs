using UnityEngine;

namespace ThreeKingdoms.UI.Menu
{
    // The travel menu's art as one asset under Resources (Resources/TravelMenuArt), so new slices reach every scene with
    // the menu without re-saving those scenes (their layouts belong to other work). Fields the scene sets still count;
    // this set adds to them and wins on equal names.
    public sealed class TravelMenuArtSet : ScriptableObject
    {
        public const string ResourceName = "TravelMenuArt";
        public Texture2D[] Art = new Texture2D[0];
        public Texture2D[] BingshuArt = new Texture2D[0];
        public string[] Borders = new string[0];
    }
}
