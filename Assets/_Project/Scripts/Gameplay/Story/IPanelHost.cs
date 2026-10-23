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
}
