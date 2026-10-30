using System;
using System.Collections.Generic;

namespace ThreeKingdoms.Core.Fate
{
    // Read-only design data (spec v5.1 §4.1–4.5, §4.7). Public-field [Serializable] DTOs per ADR-0001;
    // ThreeKingdoms.Data.Fate fills them from JSON. All numbers that feed scoring are integers (Q16.16 or per-mille).

    public static class Tiers
    {
        public const string T0 = "T0", T1 = "T1", T1S = "T1S", T2 = "T2", T3 = "T3";

        public static int Weight(string tier)
        {
            switch (tier)
            {
                case T0: return 4;
                case T1: case T1S: return 3;
                case T2: return 2;
                default: return 1;
            }
        }

        public static bool IsKnown(string tier) => tier == T0 || tier == T1 || tier == T1S || tier == T2 || tier == T3;
    }

    public static class RecruitStates
    {
        public const string Locked = "locked", Unknown = "unknown", Known = "known", Available = "available",
            InParty = "inParty", Reserve = "reserve", Departed = "departed";

        public static bool IsKnown(string s) => s == Locked || s == Unknown || s == Known || s == Available || s == InParty || s == Reserve || s == Departed;
    }

    public static class MemoryKinds
    {
        public const string Player = "PLAYER", Witness = "WITNESS", History = "HISTORY", Random = "RANDOM", Rumor = "RUMOR";
    }

    public static class ViewDims
    {
        public static readonly string[] All = { "benevolence", "valor", "wisdom", "faith", "authority", "deceit" };

        public static int IndexOf(string dim) => Array.IndexOf(All, dim);
    }

    [Serializable]
    public class NodeDef
    {
        public string id;
        public string volume;
        public int year;
        public string eraName;
        public string title;
        public string next;
    }

    [Serializable]
    public class Personality
    {
        public int loyalty, righteousness, benevolence, courage, cunning, fame, profit, pride;

        public int[] ToArray() => new[] { loyalty, righteousness, benevolence, courage, cunning, fame, profit, pride };
    }

    [Serializable]
    public class Ambition
    {
        public string id;
        public int weight; // per-mille, sum 1000
    }

    [Serializable]
    public class ValueLikes
    {
        public string[] likes = Array.Empty<string>();
        public string[] dislikes = Array.Empty<string>();
    }

    [Serializable]
    public class Voice
    {
        public string register;
        public string selfRef;
        public string[] sampleLines = Array.Empty<string>();
        public string[] taboo = Array.Empty<string>();
    }

    [Serializable]
    public class HeldItem
    {
        public string itemId;
        public string origin; // own | player_given

        public HeldItem Clone() => new HeldItem { itemId = itemId, origin = origin };
    }

    [Serializable]
    public class CharacterProfile
    {
        public string id, name, courtesyName, tier, ownerLine, biography, growthCurveId;
        public string[] recruitableIn = Array.Empty<string>();
        public string[] factionsEligible = Array.Empty<string>();
        public Personality personality = new Personality();
        public Ambition[] ambitions = Array.Empty<Ambition>();
        public ValueLikes values = new ValueLikes();
        public Voice voice;
        public SortedDictionary<string, string> wuxingAptitude = new SortedDictionary<string, string>(StringComparer.Ordinal);
        public string[] favoredBooks = Array.Empty<string>();
        public HeldItem[] ownedItems = Array.Empty<HeldItem>();
        public string[] powerPositions = Array.Empty<string>();
        public string[] sources = Array.Empty<string>();
        // Starting world placement (P0a addition so a volume can be initialised without a save).
        public string initialFaction, initialLocation, initialRecruitState, initialRole;
    }

    [Serializable]
    public class RouteStep
    {
        public string stage;
        public string optionId;
    }

    public static class AnchorKinds
    {
        public const string State = "state", Dies = "dies";
    }

    [Serializable]
    public class Anchor
    {
        public string id, stage, strength, source, canonLevel, departureSceneId, compensationSceneId;
        public string kind = AnchorKinds.State; // "state" (condition on the stage's state) or "dies" (dies during this node)
        public Condition state;

        public bool IsHard => strength == "HARD";
        public bool IsDies => kind == AnchorKinds.Dies;
    }

    [Serializable]
    public class FateEffect
    {
        public string faction, location, recruitState, role, post;
        public bool dies;
        public string[] presentAt = Array.Empty<string>(); // P0a addition: scenes this option puts the character in (for presentAt anchors)
        public SortedDictionary<string, string[]> relationTags = new SortedDictionary<string, string[]>(StringComparer.Ordinal);
        public string aptitudeChange; // field only in P0 (spec §10)
        public string itemTransfer;   // field only in P0 (spec §10)
    }

    [Serializable]
    public class FateOption
    {
        public string id;
        public string[] stages = Array.Empty<string>(); // empty = any stage (generic options)
        public FateEffect effect = new FateEffect();
        public int playerProximity;
        public string[] requiresCause = Array.Empty<string>();
        public bool twoStep;
        public string warningSceneId;
        public Condition preconditions;
        public Personality tags; // personality-direction vector for fitP (−100..100 per dimension)
        public string[] ambitionsServed = Array.Empty<string>();
        public string summaryTemplate, canonLevel, source;

        public bool AppliesTo(string stage) => stages == null || stages.Length == 0 || Array.IndexOf(stages, stage) >= 0;
    }

    [Serializable]
    public class InitialRelation
    {
        public string target;
        public int favor, trust;
        public string[] tags = Array.Empty<string>();
    }

    [Serializable]
    public class FateRoute
    {
        public string characterId;
        public string routeId; // optional; survivor routes are referenced by RescueCase.onSuccess.afterRouteId
        public string volume;
        public string activeFrom;
        public RouteStep[] defaultRoute = Array.Empty<RouteStep>();
        public Anchor[] anchors = Array.Empty<Anchor>();
        public FateOption[] fateOptions = Array.Empty<FateOption>();
        public string[] genericOptions = Array.Empty<string>(); // ids from the generic library this character may use
        public InitialRelation[] initialRelations = Array.Empty<InitialRelation>();
        public string[] randomEvents = Array.Empty<string>();

        public string DefaultOptionFor(string stage)
        {
            foreach (var s in defaultRoute) if (s.stage == stage) return s.optionId;
            return null;
        }
    }

    [Serializable]
    public class RescueHint
    {
        public string fromStage, level, textKey;
        public Condition condition;
    }

    [Serializable]
    public class SceneVariant
    {
        public string kind;
        public string @default;
        public string eligible;
    }

    [Serializable]
    public class SurvivorConstraints
    {
        public string[] factionsAllowed = Array.Empty<string>();
        public string[] cannotHoldPosts = Array.Empty<string>();
        public string[] mustNotBePresentAt = Array.Empty<string>();
    }

    [Serializable]
    public class PowerTransfer
    {
        public string position, successor, mode, atNode;
    }

    [Serializable]
    public class AffectedNode
    {
        public string node, dialogueVariant;
    }

    [Serializable]
    public class RescueSuccess
    {
        public string liftAnchor = AnchorKinds.Dies;
        public string tierAfter = Tiers.T1S;
        public string afterRouteId;
        public string[] unchangedDeaths = Array.Empty<string>();
        public SurvivorConstraints survivorConstraints = new SurvivorConstraints();
        public PowerTransfer powerTransfer;
        public AffectedNode[] affectedNodes = Array.Empty<AffectedNode>();
        public string epilogueVariantId;
    }

    [Serializable]
    public class MemoryBroadcast
    {
        public string who, key;
        public int importance;
    }

    [Serializable]
    public class RescueFail
    {
        public bool historical = true;
        public MemoryBroadcast[] memoryBroadcast = Array.Empty<MemoryBroadcast>();
    }

    [Serializable]
    public class RescueCase
    {
        public string id, characterId, subject, volume, node, historicalFate, source, canonLevel;
        public RescueHint hint;
        public Condition eligibility;
        public Condition fulfillment;
        public SceneVariant sceneVariant;
        public RescueSuccess onSuccess = new RescueSuccess();
        public RescueFail onFail = new RescueFail();
    }

    [Serializable]
    public class EventMemorySpec
    {
        public string key, templateId;
        public int importance = 3;
    }

    [Serializable]
    public class HistoryEventDef
    {
        public string id, stage;
        public string scope = "world"; // world | region:<r> | faction:<f> | explicit:<id,id>
        public int delayStages;
        public bool causesDeviation, splitsTextGroup;
        public EventMemorySpec memory;
    }

    [Serializable]
    public class RandomEventDef
    {
        public string id;
        public string[] stages = Array.Empty<string>(); // empty = any stage
        public bool causesDeviation;
        public EventMemorySpec memory;
    }

    [Serializable]
    public class PostDef
    {
        public string id, city;
        public bool unique = true;
        public SortedDictionary<string, string> historical = new SortedDictionary<string, string>(StringComparer.Ordinal);
    }

    [Serializable]
    public class CityDef
    {
        public string id, region;
        public int garrisonCap;
    }

    [Serializable]
    public class FactionFloor
    {
        public string faction, until;
        public string[] mustKeep = Array.Empty<string>();
    }

    [Serializable]
    public class WorldSlotsDef
    {
        public PostDef[] posts = Array.Empty<PostDef>();
        public CityDef[] cities = Array.Empty<CityDef>();
        public FactionFloor[] factionFloors = Array.Empty<FactionFloor>();
    }

    [Serializable]
    public class VolumeDef
    {
        public string id;
        public string protagonistId;
        public string protagonistFaction;
        public int fateDataVersion = 1;
        public string[] openRegions = Array.Empty<string>();
    }

    [Serializable]
    public class FateMigration
    {
        public string from, to; // optionId rename
    }

    [Serializable]
    public class FateMigrationTable
    {
        public int fateDataVersion;
        public FateMigration[] optionRenames = Array.Empty<FateMigration>();
    }
}
