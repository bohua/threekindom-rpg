using System;
using System.Globalization;
using ThreeKingdoms.Data.Battle;
using ThreeKingdoms.Save;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThreeKingdoms.Gameplay.Story
{
    // Title screen actions (design/ux/title-menu.md): a new game plays the prologue and lands at the notice board;
    // loading reads a slot and rebuilds the play-through, landing at the tavern or the saved battle's pre-battle state
    // (save-system rule 5); continue loads whichever readable slot was saved last.
    public sealed class TitleController : MonoBehaviour
    {
        // Resources/Audio/Music/title_theme, looped; silence until the file exists (StoryMusic).
        public const string MusicCue = "title_theme";

        public BattleCatalog Catalog;

        public SaveSlots Slots => GameRun.Slots(Catalog ? Catalog.ManualSaveSlots : 3);

        void OnEnable() => StoryMusic.Play(MusicCue, loop: true, fadeIn: 1.2f);

        public void NewGame()
        {
            StoryMusic.Stop();
            GameRun.Clear();
            GameRun.PrologueDue = true;
            // The prologue ends on the young man of Lousang (user 2026-10-30): the game opens at home in the R1 map, and
            // he walks to Zhuojun for the notice. Builds without the R1 map open at the notice board as before.
            if (Application.CanStreamedLevelBeLoaded(GameRun.R1Scene))
            {
                GameRun.NewGame(Catalog);
                GameRun.LoadScene(GameRun.R1Scene);
            }
            else GameRun.LoadScene(GameRun.TownScene);
        }

        // Every slot in menu order: the rolling pre-battle save, then the manual ones.
        public string[] SlotNames
        {
            get
            {
                var slots = Slots;
                var names = new string[slots.ManualCount + 1];
                names[0] = SaveSlots.Auto;
                for (int i = 1; i <= slots.ManualCount; i++) names[i] = SaveSlots.Manual(i);
                return names;
            }
        }

        // The readable slot saved last, or null when there is none (continue is then greyed out).
        public string LatestSlot()
        {
            var slots = Slots;
            string best = null; DateTime bestAt = DateTime.MinValue;
            foreach (var slot in SlotNames)
            {
                if (slots.Read(slot, out var data) != SaveReadStatus.Ok) continue;
                DateTime.TryParse(data.savedAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var at);
                if (best == null || at > bestAt) { best = slot; bestAt = at; }
            }
            return best;
        }

        // Any slot file at all, readable or not, so a damaged save can still say why from the load list.
        public bool AnySave()
        {
            var slots = Slots;
            foreach (var slot in SlotNames) if (slots.Exists(slot)) return true;
            return false;
        }

        // Ok means the scene is loading; anything else leaves the file untouched and returns why.
        public SaveReadStatus Load(string slot, out string error)
        {
            error = null;
            var status = Slots.Read(slot, out var data);
            if (status != SaveReadStatus.Ok) return status;
            if (!GameRun.TryLoad(Catalog, data, out error)) return SaveReadStatus.Damaged;
            StoryMusic.Stop();
            return SaveReadStatus.Ok;
        }

        public void Quit() => Application.Quit();
    }
}
