using System;

namespace ThreeKingdoms.Gameplay.Story
{
    // Something that can open a full-screen party panel (shop, outfit, save): the town and the battle's
    // pre-battle preparation. The panel UI listens for its name and calls ClosePanel when done.
    public interface IPanelHost
    {
        GameRun Run { get; }
        string Panel { get; }
        void ClosePanel();
        event Action<string> PanelOpened;
        event Action<string> PanelClosed;
    }

    // A walkable scene with the main menu, the save panel and the shop (Zhuojun; 0.2 Lu Zhi's camp and Anxi).
    public interface IFieldHost : IPanelHost
    {
        ThreeKingdoms.Data.Battle.BattleCatalog Catalog { get; }
        // Where a manual save made here puts the player back (SaveData location).
        string SaveLocation { get; }
        void SwitchPanel(string panel);
        void ReturnToTitle();
        // 史事簿 (travel menu 史事): close the menu and replay a chronicle entry here, then hand control back.
        void PlayChronicle(string id);
    }

    // A host whose shop carries more than the first tier of equipment (0.2: the Anxi market).
    public interface IShopTier
    {
        int ShopTier { get; }
    }
}
