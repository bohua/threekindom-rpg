using System;
using System.Collections.Generic;
using System.Linq;

namespace ThreeKingdoms.Core.Story
{
    // Where the 0.2 Yellow Turban chapter stands (V02_HUANGJIN_PLAN §2, SYSTEMS_V02 §5). One step after another; a place
    // left behind is never loaded again (the "no return" rule is a progress step, not a disabled scene).
    public enum ChapterStep
    {
        Zhuojun = 0,        // Alpha 0.1 still in progress
        ZouJingOrders = 1,  // battle 3 won: C1 at the south gate, then scroll ①
        ZhuojunFree = 2,    // free in Zhuojun; the south gate leads out (confirm)
        Camp = 3,           // Lu Zhi's camp: C2, then free
        Yingchuan = 4,      // camp left: C3, battle 4, C4
        Guangzong = 5,      // battle 4 won: C6, C7, battle 5, C8
        ToAnxi = 6,         // battle 5 won: optional Wancheng (C5), C9, scroll ②
        Anxi = 7,           // 185, Anxi county: C10, commissions, battle 6
        AnxiBandits = 8,    // battle 6 won: the courier leads to C11–C13
        Done = 9,           // Anxi left: scroll ③, the end of 0.2
    }

    // Main-line node (蓝图 §4.2): 184 until the Yellow Turbans are put down (C9), then 185.
    public enum ChronicleNode { N1 = 1, N2 = 2 }

    // One thing a character remembers (SYSTEMS_V02 §3): flags only, no attitude numbers.
    public readonly struct Memory
    {
        public readonly string Who, Key, Value, Text;
        public readonly ChronicleNode Node;
        public Memory(string who, string key, string value, ChronicleNode node, string text) { Who = who; Key = key; Value = value; Node = node; Text = text; }
    }

    // The 0.2 chapter's own state, beside StoryState (which keeps Alpha 0.1's Zhuojun rules unchanged). Pure C#, saved
    // with the play-through (ADR-0004) in the same write as the story flags.
    public sealed class ChapterState
    {
        public const string Wancheng = "wancheng";
        // Commissions in Anxi (V02_HUANGJIN_PLAN §6.2): alms (must-do layer), the ox case, the bridge.
        public const string Alms = "alms", OxCase = "ox", Bridge = "bridge";
        public static readonly string[] AnxiCommissions = { Alms, OxCase, Bridge };

        readonly HashSet<string> flags = new HashSet<string>();
        readonly List<Memory> memories = new List<Memory>();

        public ChapterStep Step { get; private set; }
        public IReadOnlyCollection<string> Flags => flags;
        public IReadOnlyList<Memory> Memories => memories;
        public ChronicleNode Node => Step >= ChapterStep.Anxi ? ChronicleNode.N2 : ChronicleNode.N1;
        public int Year => Node == ChronicleNode.N2 ? 185 : 184;

        public event Action<Memory> Remembered;

        public bool Has(string flag) => flags.Contains(flag);
        public void Set(string flag) { if (!string.IsNullOrEmpty(flag)) flags.Add(flag); }

        // Battle 3 won (Alpha 0.1's last battle): the chapter opens with Zou Jing's orders.
        public void OpenAfterZhuojun(bool zhuojunWon)
        {
            if (zhuojunWon && Step == ChapterStep.Zhuojun) Step = ChapterStep.ZouJingOrders;
        }

        // Moves forward only, one step at a time from the expected step; returns false otherwise (a replayed beat).
        public bool Advance(ChapterStep from, ChapterStep to)
        {
            if (Step != from || to <= from) return false;
            Step = to;
            return true;
        }

        // The next main battle the chapter expects, or null.
        public string NextBattleId => Step switch
        {
            ChapterStep.Yingchuan => "battle_4",
            ChapterStep.Guangzong => "battle_5",
            ChapterStep.Anxi => "battle_6",
            _ => null,
        };

        // The single reward-once gate for battles 4–6 (as StoryState.RecordVictory is for 1–3).
        public bool RecordVictory(string encounterId)
        {
            if (encounterId == null || encounterId != NextBattleId) return false;
            Step = Step switch
            {
                ChapterStep.Yingchuan => ChapterStep.Guangzong,
                ChapterStep.Guangzong => ChapterStep.ToAnxi,
                _ => ChapterStep.AnxiBandits,
            };
            return true;
        }

        // Same who + key keeps only the last write (replaying a beat overwrites, never duplicates).
        public void Remember(string who, string key, string value, string text)
        {
            int i = memories.FindIndex(m => m.Who == who && m.Key == key);
            var m = new Memory(who, key, value, Node, text);
            if (i >= 0) memories[i] = m; else memories.Add(m);
            Remembered?.Invoke(m);
        }

        public string Recall(string who, string key) => memories.FirstOrDefault(m => m.Who == who && m.Key == key).Value;

        public IEnumerable<Memory> MemoriesOf(string who) => memories.Where(m => m.Who == who);

        public IEnumerable<string> OpenCommissions => AnxiCommissions.Where(c => !Has(c));

        // Development / test entry straight into battle 4, 5 or 6 (battleIndex 3–5).
        public static ChapterState AtBattle(int battleIndex) => new ChapterState
        {
            Step = battleIndex switch { 3 => ChapterStep.Yingchuan, 4 => ChapterStep.Guangzong, 5 => ChapterStep.Anxi, _ => ChapterStep.Zhuojun },
        };

        // A saved play-through (ADR-0004).
        public static ChapterState Restore(ChapterStep step, IEnumerable<string> flags, IEnumerable<Memory> memories)
        {
            if (!Enum.IsDefined(typeof(ChapterStep), step)) throw new ArgumentException($"未知进度 {(int)step}");
            var s = new ChapterState { Step = step };
            foreach (var f in flags ?? Enumerable.Empty<string>()) s.Set(f);
            foreach (var m in memories ?? Enumerable.Empty<Memory>()) s.memories.Add(m);
            return s;
        }
    }
}
