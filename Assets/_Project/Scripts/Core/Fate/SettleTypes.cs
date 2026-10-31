using System;
using System.Collections.Generic;

namespace ThreeKingdoms.Core.Fate
{
    public static class EventTypes
    {
        public const string Memory = "memory", Recruit = "recruit", Gift = "gift", Battle = "battle", Flag = "flag", Rescue = "rescue", Death = "death";
    }

    /// <summary>
    /// One stage-log event (spec §4.7 StageEventLog). Typed fields replace the ADR's JSON payload string because
    /// Core has no JSON parser; ThreeKingdoms.Data/Gameplay fill them.
    /// </summary>
    [Serializable]
    public sealed class StageEvent
    {
        public long Seq;
        public string Type;
        public bool Scripted;
        public MemoryEntry Memory;      // memory
        public string CharacterId;      // recruit, gift, death
        public string RecruitState;     // recruit
        public string ItemId;           // gift
        public string CaseId, Result;   // rescue: success | fail

        public static StageEvent MemoryEvent(long seq, MemoryEntry m) => new StageEvent { Seq = seq, Type = EventTypes.Memory, Memory = m };
        public static StageEvent RecruitEvent(long seq, string id, string state, bool scripted) => new StageEvent { Seq = seq, Type = EventTypes.Recruit, CharacterId = id, RecruitState = state, Scripted = scripted };
        public static StageEvent GiftEvent(long seq, string id, string itemId) => new StageEvent { Seq = seq, Type = EventTypes.Gift, CharacterId = id, ItemId = itemId };
        public static StageEvent RescueEvent(long seq, string caseId, bool success) => new StageEvent { Seq = seq, Type = EventTypes.Rescue, CaseId = caseId, Result = success ? "success" : "fail" };
        public static StageEvent DeathEvent(long seq, string id) => new StageEvent { Seq = seq, Type = EventTypes.Death, CharacterId = id, Scripted = true };
    }

    [Serializable]
    public sealed class StageEventLog
    {
        public List<StageEvent> Events = new List<StageEvent>();

        public StageEventLog Add(StageEvent e)
        {
            Events.Add(e);
            return this;
        }

        public long NextSeq(FateSaveSection s)
        {
            long max = s.world.seqCounter;
            foreach (var e in Events) if (e.Seq > max) max = e.Seq;
            return max + 1;
        }
    }

    public static class ChangeKinds
    {
        public const string Faction = "faction", Location = "location", RecruitState = "recruitState", Death = "death",
            ReturnItem = "returnItem", Post = "post", Tier = "tier";
    }

    /// <summary>A change Gameplay must apply to inventory/party in the same save transaction (spec §5.2 ⑦).</summary>
    [Serializable]
    public sealed class FateChange
    {
        public string Kind, CharacterId, ItemId, From, To;
        public override string ToString() => $"{Kind}:{CharacterId}:{ItemId}:{From}->{To}";
    }

    /// <summary>A two-step option that was announced this settle and needs its warning scene (spec §5.2 ⑤).</summary>
    [Serializable]
    public sealed class AnnouncedTransition
    {
        public string CharacterId, OptionId, Cause, AnnouncedStage, CommitStage, WarningSceneId;
    }

    /// <summary>Why a candidate did or did not reach the draw; read by the fate panel.</summary>
    [Serializable]
    public sealed class CandidateTrace
    {
        public string OptionId, Status; // scored | filtered:<reason>
        public bool IsDefault;
        public int Score, Weight;
        public string Cause;
    }

    [Serializable]
    public sealed class CharacterTrace
    {
        public string CharacterId, Tier, Proposed, Chosen, Cause, Note;
        public bool Arbitrated, Announced, Committed, Revoked;
        public List<string> Tokens = new List<string>();
        public List<CandidateTrace> Candidates = new List<CandidateTrace>();
    }

    public sealed class SettleResult
    {
        public FateSaveSection NewFateSection;
        public List<FateChange> Changes = new List<FateChange>();
        public List<AnnouncedTransition> Announced = new List<AnnouncedTransition>();
        public List<string> Errors = new List<string>();
        public List<CharacterTrace> Trace = new List<CharacterTrace>();
    }

    /// <summary>Read-only queries for Narrative/Yarn (ADR Decision 5).</summary>
    public interface IFateQuery
    {
        string Memory(string who, string key);
        int Favor(string who);
        int Trust(string who);
        int View(string who, string dim);
        string Location(string who);
        string Faction(string who);
        bool InParty(string who);
        bool Rescued(string caseId);
    }

    public sealed class FateQuery : IFateQuery
    {
        readonly FateSaveSection s;
        public FateQuery(FateSaveSection section) { s = section; }

        Relation ToProtagonist(string who) => s.Character(who)?.RelationTo(s.world.protagonistId);
        public string Memory(string who, string key) => MemoryStore.Find(s.Character(who), key)?.value;
        public int Favor(string who) => ToProtagonist(who)?.favor ?? 0;
        public int Trust(string who) => ToProtagonist(who)?.trust ?? 0;
        public int View(string who, string dim)
        {
            var r = ToProtagonist(who);
            int i = ViewDims.IndexOf(dim);
            return r?.views != null && i >= 0 ? r.views[i] : 0;
        }
        public string Location(string who) { var c = s.Character(who); return c != null && c.IsAlive ? c.location : null; }
        public string Faction(string who) { var c = s.Character(who); return c != null && c.IsAlive ? c.faction : null; }
        public bool InParty(string who) => s.Character(who)?.recruitState == RecruitStates.InParty;
        public bool Rescued(string caseId) => s.world.rescueResults.TryGetValue(caseId, out var r) && r == "success";
    }
}
