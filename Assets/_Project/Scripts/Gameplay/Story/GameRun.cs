using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Data.Battle;
using ThreeKingdoms.Gameplay.Battle;
using UnityEngine;

namespace ThreeKingdoms.Gameplay.Story
{
    // One play-through held across scene loads (ADR-0006 §2): story flags plus the stand-in party progress. The save
    // system (ADR-0004) will read and write these; until then they live only in memory.
    public sealed class GameRun
    {
        public const string TownScene = "Zhuojun_Town";
        public static readonly string[] BattleScenes = { "Battle_Tutorial01", "Battle_Tutorial02", "Battle_Tutorial03" };

        public enum Arrival { NewGame, ReturnFromBattle }

        public static GameRun Current { get; private set; }

        public StoryState Story { get; }
        public BattleData Data { get; }
        public DevPartyProgress Progress { get; }
        // Where the next town load puts the player.
        public Arrival NextArrival { get; set; } = Arrival.NewGame;

        // True for a run made by opening a battle scene directly (development); victory then offers the next battle
        // instead of returning to town.
        public bool IsDevelopment { get; }

        GameRun(BattleData data, StoryState story, DevPartyProgress progress, bool development)
        {
            Data = data; Story = story; Progress = progress; IsDevelopment = development;
        }

        public static GameRun NewGame(BattleCatalog catalog)
        {
            var data = catalog.ToRuntime();
            return Current = new GameRun(data, new StoryState(), new DevPartyProgress(data, rations: 2, copper: 120), false);   // V1_SPEC §8
        }

        // A battle scene opened on its own: the expected state before that battle (lv 1/2/3, copper 120/150/200).
        public static GameRun ForBattle(BattleCatalog catalog, int battleIndex, int startExp, int startCopper, int rations)
        {
            var data = catalog.ToRuntime();
            return Current = new GameRun(data, StoryState.AtBattle(battleIndex), new DevPartyProgress(data, rations, startCopper, startExp), true);
        }

        public static void Clear() => Current = null;

        // Static state survives "enter play mode" without a domain reload; start every run clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;

        public static int BattleIndex(string encounterId) => System.Array.IndexOf(new[] { "battle_1", "battle_2", "battle_3" }, encounterId);

        public static string SceneFor(string encounterId) => encounterId switch
        {
            "battle_1" => BattleScenes[0],
            "battle_2" => BattleScenes[1],
            "battle_3" => BattleScenes[2],
            _ => null,
        };
    }
}
