using System;
using System.Collections.Generic;
using System.IO;
using ThreeKingdoms.Core.Fate;
using ThreeKingdoms.Data.Fate;

namespace ThreeKingdoms.Tests.Fate
{
    /// <summary>Shared fixtures for the fate tests: the synthetic dataset (Assets/_Project/Data/Fate/test) and helpers.</summary>
    public static class FateTestKit
    {
        public const string TestDir = "Assets/_Project/Data/Fate/test";
        public const ulong Seed = 0x9E3779B97F4A7C15UL;

        public static FateDataBundle Bundle() => FateJsonLoader.LoadBundle(name =>
        {
            var p = Path.Combine(TestDir, name);
            return File.Exists(p) ? File.ReadAllText(p) : null;
        });

        public static FateDataSet TestData() => new FateDataSet(Bundle());

        /// <summary>Load fresh data and let the test break it (fault samples for the validator and simulator).</summary>
        public static FateDataSet TestData(Action<FateDataBundle> mutate)
        {
            var b = Bundle();
            mutate(b);
            return new FateDataSet(b);
        }

        public static FateDataSet DataWithTuning(Action<TuningTable> tweak)
        {
            var d = TestData();
            var t = d.Tuning.Clone();
            tweak(t);
            return d.WithTuning(t);
        }

        public static FateSaveSection Start(FateDataSet d, ulong seed = Seed) => FateEngine.CreateInitialState(d, "tv", seed);

        public static FateRoute Route(FateDataBundle b, string characterId)
        {
            foreach (var r in b.routes) if (r.characterId == characterId && string.IsNullOrEmpty(r.routeId)) return r;
            throw new ArgumentException(characterId);
        }

        public static FateOption Option(FateDataBundle b, string optionId)
        {
            foreach (var r in b.routes) foreach (var o in r.fateOptions) if (o.id == optionId) return o;
            foreach (var g in b.genericOptions) if (g.id == optionId) return g;
            throw new ArgumentException(optionId);
        }

        public static MemoryEntry Player(string who, string key, int intensity, int valence, int importance, params string[] tags) => new MemoryEntry
        {
            who = who, key = key, value = "v", kind = MemoryKinds.Player, tags = tags, intensity = intensity, valence = valence,
            importance = importance, actors = new[] { "t_lord", who }, text = key
        };

        public static StageEventLog Log(FateSaveSection s, params MemoryEntry[] memories)
        {
            var log = new StageEventLog();
            long seq = s.world.seqCounter;
            foreach (var m in memories) log.Add(StageEvent.MemoryEvent(++seq, m));
            return log;
        }

        public static SettleResult Settle(FateDataSet d, FateSaveSection s, StageEventLog log, string node, IConditionContext ctx = null) =>
            FateEngine.Settle(d, s, log ?? new StageEventLog(), node, ctx);

        public static string Json(object o) => FateJsonLoader.ToJson(o, false);

        public static CharacterTrace Trace(SettleResult r, string id) => r.Trace.Find(t => t.CharacterId == id);

        public static CandidateTrace Candidate(SettleResult r, string id, string option) =>
            Trace(r, id)?.Candidates.Find(c => c.OptionId == option);
    }

    /// <summary>A configurable condition context.</summary>
    public sealed class FakeContext : IConditionContext
    {
        public readonly HashSet<string> Flags = new HashSet<string>();
        public readonly Dictionary<string, string> Values = new Dictionary<string, string>();
        public readonly HashSet<string> Objectives = new HashSet<string>();
        public readonly HashSet<string> Items = new HashSet<string>();
        public readonly HashSet<string> Destroyed = new HashSet<string>();
        public readonly HashSet<string> Seen = new HashSet<string>();
        public int Rep;
        public bool Flag(string name) => Flags.Contains(name);
        public string FlagValue(string name) => Values.TryGetValue(name, out var v) ? v : null;
        public bool BattleObjective(string id) => Objectives.Contains(id);
        public bool HasItem(string itemId) => Items.Contains(itemId);
        public int Reputation => Rep;
        public bool FactionDestroyed(string factionId) => Destroyed.Contains(factionId);
        public bool EventSeen(string eventId) => Seen.Contains(eventId);
    }
}
