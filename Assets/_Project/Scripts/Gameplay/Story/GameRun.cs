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
        // 0.2 黄巾篇 (V02_HUANGJIN_PLAN §3): Lu Zhi's camp and Anxi county to walk in, the story stage for scenes that are
        // only played, and battles 4–6.
        public const string CampScene = "Camp_LuZhi", AnxiScene = "Anxi_Town", StageScene = "Story_Stage";
        public static readonly string[] ChapterBattleScenes = { "Battle_Yingchuan04", "Battle_Guangzong05", "Battle_Anxi06" };
        // 0.3 讨董篇 (design/v0.3-taodong): Gongsun Zan's garrison and the allied camp at Suanzao to walk in; battle 7 (the
        // optional drill) and battle 8 (Hulao Pass) on the tactics map.
        public const string GongsunScene = "Camp_Gongsun", SuanzaoScene = "Camp_Suanzao";
        public static readonly string[] Chapter3BattleScenes = { "Battle_Drill07", "Battle_Hulao08" };
        public const string Chapter = "第一章　涿郡起兵";

        public enum Arrival { NewGame, ReturnFromBattle, AtTavern, FromTavernInside, AtSouthGate }

        public static GameRun Current { get; private set; }

        public StoryState Story { get; }
        // 0.2 chapter progress, choices and memories (SYSTEMS_V02 §3, §5).
        public ChapterState Chapter2 { get; private set; } = new ChapterState();
        // The story-stage script the next Story_Stage load plays (StageScripts id).
        public string PendingStage { get; set; }
        public BattleData Data { get; }
        public DevPartyProgress Progress { get; }
        // Reputation and the like (user 2026-10-22).
        public TownLife Life { get; private set; } = new TownLife();
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
            return Current = new GameRun(data, StoryState.AtBattle(battleIndex), new DevPartyProgress(data, new Inventory(data.Economy, startCopper, rations), startExp), true)
            { Chapter2 = ChapterState.AtBattle(battleIndex) };
        }

        // A play-through at a 0.2 chapter step (tests, development): Zhuojun done, the party at the given exp and copper.
        public static GameRun ForChapter(BattleCatalog catalog, ChapterStep step, int exp, int copper, int rations = 2, bool development = false)
        {
            var data = catalog.ToRuntime();
            return Current = new GameRun(data, StoryState.Restore(true, true, SequenceState.Done, StoryState.BattleCount, SequenceState.Done),
                new DevPartyProgress(data, new Inventory(data.Economy, copper, rations), exp), development) { Chapter2 = ChapterState.At(step) };
        }

        public static void Clear() => Current = null;

        // Static state survives "enter play mode" without a domain reload; start every run clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = null; SaveDirectoryOverride = null; }

        public static int BattleIndex(string encounterId) => Array.IndexOf(new[] { "battle_1", "battle_2", "battle_3", "battle_4", "battle_5", "battle_6", "battle_7", "battle_8" }, encounterId);

        public static string SceneFor(string encounterId) => encounterId switch
        {
            "battle_1" => BattleScenes[0],
            "battle_2" => BattleScenes[1],
            "battle_3" => BattleScenes[2],
            "battle_4" => ChapterBattleScenes[0],
            "battle_5" => ChapterBattleScenes[1],
            "battle_6" => ChapterBattleScenes[2],
            "battle_7" => Chapter3BattleScenes[0],
            "battle_8" => Chapter3BattleScenes[1],
            _ => null,
        };

        // The next main battle in either chapter, or null.
        public string NextBattleId => Story.NextBattleId ?? Chapter2.NextBattleId;

        // The reward-once gate for every main battle: Alpha 0.1's three through StoryState, 4–6 through the chapter.
        public bool RecordVictory(string encounterId)
        {
            if (Story.RecordVictory(encounterId))
            {
                Chapter2.OpenAfterZhuojun(Story.NextBattleId == null);
                return true;
            }
            return Chapter2.RecordVictory(encounterId);
        }

        // Header of a save slot: the chapter part the play-through is in.
        public string ChapterTitle => Chapter2.Step >= ChapterStep.Done ? "第一篇章　讨董"
            : Chapter2.Step >= ChapterStep.Anxi ? "第一篇章　安喜县尉"
            : Chapter2.Step >= ChapterStep.Camp ? "第一篇章　黄巾转战" : Chapter;

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
                ChapterTitle, Application.version, DateTime.Now, Life, Chapter2);
        }

        public void SaveTo(string slot, string location) => Slots().Write(slot, Capture(location));

        // Rebuilds a play-through from a save and loads its scene: the tavern, or the saved battle's pre-battle state.
        public static bool TryLoad(BattleCatalog catalog, SaveData save, out string error)
        {
            var data = catalog.ToRuntime();
            if (!SaveCodec.TryRestore(save, data.Economy, out var restored, out error)) return false;
            var progress = new DevPartyProgress(data, restored.Inventory);
            progress.RestoreExp(restored.Exp);
            Current = new GameRun(data, restored.Story, progress, false) { Life = restored.Life, Chapter2 = restored.Chapter ?? new ChapterState() };
            if (restored.Location == SaveData.TownLocation)
            {
                Current.NextArrival = Arrival.AtTavern;
                SceneManager.LoadScene(TownScene);
            }
            else if (restored.Location == SaveData.CampLocation) SceneManager.LoadScene(CampScene);
            else if (restored.Location == SaveData.AnxiLocation) SceneManager.LoadScene(AnxiScene);
            else if (restored.Location == SaveData.GongsunLocation) SceneManager.LoadScene(GongsunScene);
            else if (restored.Location == SaveData.SuanzaoLocation) SceneManager.LoadScene(SuanzaoScene);
            else SceneManager.LoadScene(SceneFor(restored.Location));
            return true;
        }
    }
}
