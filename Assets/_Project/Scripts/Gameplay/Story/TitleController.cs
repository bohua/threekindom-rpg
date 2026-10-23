using ThreeKingdoms.Data.Battle;
using ThreeKingdoms.Save;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThreeKingdoms.Gameplay.Story
{
    // Title screen actions (game-ui GDD): a new game starts outside the south gate; loading reads a slot and rebuilds
    // the play-through, landing at the tavern or the saved battle's pre-battle state (save-system rule 5).
    public sealed class TitleController : MonoBehaviour
    {
        public BattleCatalog Catalog;

        public SaveSlots Slots => GameRun.Slots(Catalog ? Catalog.ManualSaveSlots : 3);

        public void NewGame()
        {
            GameRun.Clear();
            SceneManager.LoadScene(GameRun.TownScene);
        }

        // Ok means the scene is loading; anything else leaves the file untouched and returns why.
        public SaveReadStatus Load(string slot, out string error)
        {
            error = null;
            var status = Slots.Read(slot, out var data);
            if (status != SaveReadStatus.Ok) return status;
            return GameRun.TryLoad(Catalog, data, out error) ? SaveReadStatus.Ok : SaveReadStatus.Damaged;
        }

        public void Quit() => Application.Quit();
    }
}
