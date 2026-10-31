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
        public const string R1Scene = "Region_R1";   // 涿蓟: a new game opens in Lousang (user 2026-10-30)
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
        // 0.4 河北篇 (design/v0.4-hebei): the Bohai plain (RegionScenes R4) and battles 9–11 on the tactics map.
        public static readonly string[] Chapter4BattleScenes = { "Battle_Pingyuan09", "Battle_Panhe10", "Battle_Jieqiao11" };
        public const string BohaiScene = "Bohai_Plain";
        // 0.5 徐州篇 (design/v0.5-xuzhou): Xiapi to walk in, battles 12–15 on the tactics map.
        public const string XiapiScene = "Xiapi_City";
        public static readonly string[] Chapter5BattleScenes = { "Battle_Duchang12", "Battle_XiapiEast13", "Battle_XiapiWalls14", "Battle_Xiaopei15" };
        public const string Chapter = "第一章　涿郡起兵";
        // Walkable open regions (chapter 1 art probe, 2026-10-24): region id → scene.
        public static readonly System.Collections.Generic.IReadOnlyDictionary<string, string> RegionScenes =
            new System.Collections.Generic.Dictionary<string, string>
            {
                [Chapter1Chronicle.R4] = "Bohai_Plain",
                // Chapter one's other open regions (RegionTravel): a save there resumes at the post station.
                [Chapter1Chronicle.R1] = "Region_R1", [Chapter1Chronicle.R2] = "Zhongshan_Region", [Chapter1Chronicle.R3] = "Region_R3",
                [Chapter1Chronicle.R5a] = "Region_R5A", [Chapter1Chronicle.R5b] = "Region_R5B", [Chapter1Chronicle.R6] = "Region_R6",
                [Chapter1Chronicle.R7] = "Region_R7", [Chapter1Chronicle.R8] = "Region_R8",
            };

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

        // The probe's region chronicle follows the chapter's year (REVIEW_V04 R5): it only ever moves forward.
        public void SyncChronicle()
        {
            while (Chronicle.Year < Chapter2.Year && Chronicle.Advance()) { }
        }

        public static void Clear() => Current = null;

        // Set by the title's 新游戏: the town plays the prologue before the player gets control (design/gdd/prologue.md).
        // Loading a save, returning from battle and opening a scene directly never play it.
        public static bool PrologueDue { get; set; }

        // Static state survives "enter play mode" without a domain reload; start every run clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = null; SaveDirectoryOverride = null; PrologueDue = false; }

        public static int BattleIndex(string encounterId) => Array.IndexOf(new[] { "battle_1", "battle_2", "battle_3", "battle_4", "battle_5", "battle_6", "battle_7", "battle_8", "battle_9", "battle_10", "battle_11", "battle_12", "battle_13", "battle_14", "battle_15" }, encounterId);

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
            "battle_9" => Chapter4BattleScenes[0],
            "battle_10" => Chapter4BattleScenes[1],
            "battle_11" => Chapter4BattleScenes[2],
            "battle_12" => Chapter5BattleScenes[0],
            "battle_13" => Chapter5BattleScenes[1],
            "battle_14" => Chapter5BattleScenes[2],
            "battle_15" => Chapter5BattleScenes[3],
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
        public string ChapterTitle => Chapter2.Step >= ChapterStep.Done04 ? "第一篇章　徐州"
            : Chapter2.Step >= ChapterStep.Done03 ? "第一篇章　河北"
            : Chapter2.Step >= ChapterStep.Done ? "第一篇章　讨董"
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
            SyncChronicle();
            var leader = Progress.Party.Count > 0 ? Progress.Party[0] : null;
            return SaveCodec.Capture(Story, Progress.Inventory, Progress.ExpByGeneral, location, leader != null ? Progress.Level(leader) : 1,
                ChapterTitle, Application.version, DateTime.Now, Life, Chapter2, Chronicle);
        }

        public void SaveTo(string slot, string location) => Slots().Write(slot, Capture(location));

        // Rebuilds a play-through from a save and loads its scene: the tavern, or the saved battle's pre-battle state.
        public static bool TryLoad(BattleCatalog catalog, SaveData save, out string error)
        {
            var data = catalog.ToRuntime();
            if (!SaveCodec.TryRestore(save, data.Economy, out var restored, out error)) return false;
            var progress = new DevPartyProgress(data, restored.Inventory);
            progress.RestoreExp(restored.Exp);
            Current = new GameRun(data, restored.Story, progress, false) { Life = restored.Life, Chapter2 = restored.Chapter ?? new ChapterState(), Chronicle = restored.Chronicle };
            if (restored.Location == SaveData.TownLocation)
            {
                Current.NextArrival = Arrival.AtTavern;
                SceneManager.LoadScene(TownScene);
            }
            else if (restored.Location == SaveData.CampLocation) SceneManager.LoadScene(CampScene);
            else if (restored.Location == SaveData.AnxiLocation) SceneManager.LoadScene(AnxiScene);
            else if (restored.Location == SaveData.GongsunLocation) SceneManager.LoadScene(GongsunScene);
            else if (restored.Location == SaveData.SuanzaoLocation) SceneManager.LoadScene(SuanzaoScene);
            else if (restored.Location == SaveData.XiapiLocation) SceneManager.LoadScene(XiapiScene);
            else if (restored.Location == SaveData.ChapterDoneLocation) { Current = null; error = "第一篇章已完成，第二篇章尚未开放。"; return false; }
            else if (restored.Location.StartsWith(SaveData.RegionPrefix))
            {
                if (!RegionScenes.TryGetValue(restored.Location.Substring(SaveData.RegionPrefix.Length), out string scene))
                { error = $"区域 {restored.Location} 还没有场景"; Current = null; return false; }
                if (scene != BohaiScene) RegionPortal.SetArrival(RegionTravel.PostArrival, Vector3.forward);
                SceneManager.LoadScene(scene);
            }
            else SceneManager.LoadScene(SceneFor(restored.Location));
            return true;
        }
    }
}
