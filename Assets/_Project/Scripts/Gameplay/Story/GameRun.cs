using System;
using System.IO;
using ThreeKingdoms.Core.Economy;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Core.Town;
using ThreeKingdoms.Data.Battle;
using ThreeKingdoms.Gameplay.Battle;
using ThreeKingdoms.Save;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThreeKingdoms.Gameplay.Story
{
    // One play-through held across scene loads (ADR-0006 §2): story flags, progression and the inventory. The save
    // system (ADR-0004) captures it into a SaveData and rebuilds it from one.
    public sealed class GameRun
    {
        public const string TitleScene = "Title";
        public const string TownScene = "Zhuojun_Town";
        public const string TavernScene = "Zhuojun_Tavern";   // indoor scene where Guan Yu joins (user 2026-10-22)
        public static readonly string[] BattleScenes = { "Battle_Tutorial01", "Battle_Tutorial02", "Battle_Tutorial03" };
        public const string Chapter = "第一章　涿郡起兵";
        // Walkable open regions (chapter 1 art probe, 2026-10-24): region id → scene.
        public static readonly System.Collections.Generic.IReadOnlyDictionary<string, string> RegionScenes =
            new System.Collections.Generic.Dictionary<string, string> { [Chapter1Chronicle.R4] = "Bohai_Plain" };

        public enum Arrival { NewGame, ReturnFromBattle, AtTavern, FromTavernInside }

        public static GameRun Current { get; private set; }

        public StoryState Story { get; }
        public BattleData Data { get; }
        public DevPartyProgress Progress { get; }
        // Reputation and the like (user 2026-10-22).
        public TownLife Life { get; private set; } = new TownLife();
        // Year node, open regions and dangerous places (chapter 1 chronicle).
        public ChronicleState Chronicle { get; set; } = ChronicleState.Start();
        // One random source for town games, so tests can seed it.
        public System.Random Random { get; set; } = new System.Random();
        // Teaching hints already shown this play-through (each appears once; story-events answer 8).
        public System.Collections.Generic.List<string> SeenHints { get; } = new System.Collections.Generic.List<string>();
        // Where the next town load puts the player.
        public Arrival NextArrival { get; set; } = Arrival.NewGame;

        // True for a run made by opening a battle scene directly (development); victory then offers the next battle
        // instead of returning to town, and nothing is autosaved.
        public bool IsDevelopment { get; }

        GameRun(BattleData data, StoryState story, DevPartyProgress progress, bool development)
        {
            Data = data; Story = story; Progress = progress; IsDevelopment = development;
        }

        public static GameRun NewGame(BattleCatalog catalog)
        {
            var data = catalog.ToRuntime();
            return Current = new GameRun(data, new StoryState(), new DevPartyProgress(data, new Inventory(data.Economy)), false);   // 120 copper, 2 rations, starting gear (V1_SPEC §8)
        }

        // A battle scene opened on its own: the expected state before that battle (lv 1/2/3, copper 120/150/200).
        public static GameRun ForBattle(BattleCatalog catalog, int battleIndex, int startExp, int startCopper, int rations)
        {
            var data = catalog.ToRuntime();
            return Current = new GameRun(data, StoryState.AtBattle(battleIndex), new DevPartyProgress(data, new Inventory(data.Economy, startCopper, rations), startExp), true);
        }

        public static void Clear() => Current = null;

        // Static state survives "enter play mode" without a domain reload; start every run clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = null; SaveDirectoryOverride = null; }

        public static int BattleIndex(string encounterId) => Array.IndexOf(new[] { "battle_1", "battle_2", "battle_3" }, encounterId);

        public static string SceneFor(string encounterId) => encounterId switch
        {
            "battle_1" => BattleScenes[0],
            "battle_2" => BattleScenes[1],
            "battle_3" => BattleScenes[2],
            _ => null,
        };

        // ---------- saving (ADR-0004) ----------

        // Tests point this at a temporary folder so they never touch the player's saves.
        public static string SaveDirectoryOverride { get; set; }

        public static SaveSlots Slots(int manualCount) =>
            new SaveSlots(SaveDirectoryOverride ?? Path.Combine(Application.persistentDataPath, "saves"), manualCount);

        public SaveSlots Slots() => Slots(Data.ManualSaveSlots);

        // location: SaveData.TownLocation (the tavern) or the battle id whose pre-battle state this is.
        public SaveData Capture(string location)
        {
            var leader = Progress.Party.Count > 0 ? Progress.Party[0] : null;
            return SaveCodec.Capture(Story, Progress.Inventory, Progress.ExpByGeneral, location, leader != null ? Progress.Level(leader) : 1,
                Chapter, Application.version, DateTime.Now, Life, Chronicle);
        }

        public void SaveTo(string slot, string location) => Slots().Write(slot, Capture(location));

        // Rebuilds a play-through from a save and loads its scene: the tavern, or the saved battle's pre-battle state.
        public static bool TryLoad(BattleCatalog catalog, SaveData save, out string error)
        {
            var data = catalog.ToRuntime();
            if (!SaveCodec.TryRestore(save, data.Economy, out var restored, out error)) return false;
            var progress = new DevPartyProgress(data, restored.Inventory);
            progress.RestoreExp(restored.Exp);
            Current = new GameRun(data, restored.Story, progress, false) { Life = restored.Life, Chronicle = restored.Chronicle };
            if (restored.Location == SaveData.TownLocation)
            {
                Current.NextArrival = Arrival.AtTavern;
                SceneManager.LoadScene(TownScene);
            }
            else if (restored.Location.StartsWith(SaveData.RegionPrefix))
            {
                if (!RegionScenes.TryGetValue(restored.Location.Substring(SaveData.RegionPrefix.Length), out string scene))
                { error = $"区域 {restored.Location} 还没有场景"; Current = null; return false; }
                SceneManager.LoadScene(scene);
            }
            else SceneManager.LoadScene(SceneFor(restored.Location));
            return true;
        }
    }
}
