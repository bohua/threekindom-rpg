using System;
using System.Collections.Generic;

namespace ThreeKingdoms.Core.Fate
{
    // Save-side state (spec v5.1 §4.6, §4.7, §7.1). Maps are SortedDictionary with ordinal comparers so every
    // enumeration is deterministic. Every class has an explicit deep Clone because Settle never mutates its inputs.

    [Serializable]
    public class MemoryEntry
    {
        public string id, who, key, value, kind, node, location, templateId, text;
        public long seq;
        public string[] actors = Array.Empty<string>();
        public string[] tags = Array.Empty<string>();
        public int intensity = 1, valence, importance = 3;
        public SortedDictionary<string, string> @params = new SortedDictionary<string, string>(StringComparer.Ordinal);
        public bool compressed;

        public MemoryEntry Clone()
        {
            var m = (MemoryEntry)MemberwiseClone();
            m.actors = (string[])actors?.Clone();
            m.tags = (string[])tags?.Clone();
            m.@params = @params == null ? null : new SortedDictionary<string, string>(@params, StringComparer.Ordinal);
            return m;
        }

        public string Param(string name) => @params != null && @params.TryGetValue(name, out var v) ? v : null;
    }

    [Serializable]
    public class RelationBaseline
    {
        public int favor, trust;
    }

    [Serializable]
    public class Relation
    {
        public int favor, trust;
        public List<string> tags = new List<string>();
        public RelationBaseline baseline = new RelationBaseline();
        public int[] views; // six views, only toward the protagonist (ViewDims order)

        public Relation Clone()
        {
            return new Relation
            {
                favor = favor, trust = trust,
                tags = new List<string>(tags ?? new List<string>()),
                baseline = new RelationBaseline { favor = baseline?.favor ?? 0, trust = baseline?.trust ?? 0 },
                views = (int[])views?.Clone()
            };
        }
    }

    [Serializable]
    public class StageSummary
    {
        public string textKey;
        public SortedDictionary<string, string> @params = new SortedDictionary<string, string>(StringComparer.Ordinal);
    }

    [Serializable]
    public class FateHistoryEntry
    {
        public string stage, optionId, cause;
        public int score;
        public bool @default, arbitrated;

        public FateHistoryEntry Clone() => (FateHistoryEntry)MemberwiseClone();
    }

    [Serializable]
    public class CharacterState
    {
        public string id, tier, location, faction, role, recruitState, routeId;
        public bool alive = true, pendingDeath, scriptedParty;
        public List<string> presentAt = new List<string>();
        public SortedDictionary<string, Relation> relations = new SortedDictionary<string, Relation>(StringComparer.Ordinal);
        public List<HeldItem> heldItems = new List<HeldItem>();
        public List<MemoryEntry> memories = new List<MemoryEntry>();
        public SortedDictionary<string, StageSummary> stageSummaries = new SortedDictionary<string, StageSummary>(StringComparer.Ordinal);
        public List<FateHistoryEntry> fateHistory = new List<FateHistoryEntry>();
        public SortedDictionary<string, string> rescues = new SortedDictionary<string, string>(StringComparer.Ordinal);

        public bool IsAlive => alive && !pendingDeath;

        public Relation RelationTo(string target)
        {
            return target != null && relations.TryGetValue(target, out var r) ? r : null;
        }

        public CharacterState Clone()
        {
            var c = (CharacterState)MemberwiseClone();
            c.presentAt = new List<string>(presentAt);
            c.relations = new SortedDictionary<string, Relation>(StringComparer.Ordinal);
            foreach (var kv in relations) c.relations[kv.Key] = kv.Value.Clone();
            c.heldItems = heldItems.ConvertAll(h => h.Clone());
            c.memories = memories.ConvertAll(m => m.Clone());
            c.stageSummaries = new SortedDictionary<string, StageSummary>(StringComparer.Ordinal);
            foreach (var kv in stageSummaries)
                c.stageSummaries[kv.Key] = new StageSummary { textKey = kv.Value.textKey, @params = new SortedDictionary<string, string>(kv.Value.@params, StringComparer.Ordinal) };
            c.fateHistory = fateHistory.ConvertAll(f => f.Clone());
            c.rescues = new SortedDictionary<string, string>(rescues, StringComparer.Ordinal);
            return c;
        }
    }

    [Serializable]
    public class PendingTransition
    {
        public string characterId, optionId, announcedStage, commitStage, cause, warningSceneId, cooldownUntil;
        public bool warningShown, revoked;

        public PendingTransition Clone() => (PendingTransition)MemberwiseClone();
    }

    [Serializable]
    public class DeviationBudget
    {
        public string stage;
        public int max, used;
    }

    [Serializable]
    public class WorldSlotsState
    {
        public SortedDictionary<string, string> posts = new SortedDictionary<string, string>(StringComparer.Ordinal);
        public SortedDictionary<string, List<string>> garrison = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);

        public WorldSlotsState Clone()
        {
            var s = new WorldSlotsState { posts = new SortedDictionary<string, string>(posts, StringComparer.Ordinal) };
            foreach (var kv in garrison) s.garrison[kv.Key] = new List<string>(kv.Value);
            return s;
        }
    }

    [Serializable]
    public class WorldFateState
    {
        public string volume, protagonistId, stageId, fateSeed, pendingTargetNode;
        public int fateDataVersion;
        public long seqCounter;
        public bool pendingRecompute;
        public List<PendingTransition> pendingTransitions = new List<PendingTransition>();
        public DeviationBudget deviationBudget = new DeviationBudget();
        public WorldSlotsState slots = new WorldSlotsState();
        public SortedDictionary<string, string> rescueResults = new SortedDictionary<string, string>(StringComparer.Ordinal);
        public SortedDictionary<string, List<string>> historyDelivered = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);

        public WorldFateState Clone()
        {
            var w = (WorldFateState)MemberwiseClone();
            w.pendingTransitions = pendingTransitions.ConvertAll(p => p.Clone());
            w.deviationBudget = new DeviationBudget { stage = deviationBudget?.stage, max = deviationBudget?.max ?? 0, used = deviationBudget?.used ?? 0 };
            w.slots = slots.Clone();
            w.rescueResults = new SortedDictionary<string, string>(rescueResults, StringComparer.Ordinal);
            w.historyDelivered = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var kv in historyDelivered) w.historyDelivered[kv.Key] = new List<string>(kv.Value);
            return w;
        }

        public bool HistoryAlreadyDelivered(string eventId)
        {
            foreach (var kv in historyDelivered)
                if (kv.Value.Contains(eventId)) return true;
            return false;
        }
    }

    /// <summary>The fate section of the save (spec §7.1). The whole SaveDocument stays in ThreeKingdoms.Save (P0b).</summary>
    [Serializable]
    public class FateSaveSection
    {
        public int schemaVersion = 1;
        public int fateDataVersion;
        public WorldFateState world = new WorldFateState();
        public SortedDictionary<string, CharacterState> characters = new SortedDictionary<string, CharacterState>(StringComparer.Ordinal);
        public string fateCommitted;

        public CharacterState Character(string id) => id != null && characters.TryGetValue(id, out var c) ? c : null;

        public FateSaveSection Clone()
        {
            var s = new FateSaveSection { schemaVersion = schemaVersion, fateDataVersion = fateDataVersion, world = world.Clone(), fateCommitted = fateCommitted };
            foreach (var kv in characters) s.characters[kv.Key] = kv.Value.Clone();
            return s;
        }
    }
}
