using System;
using System.Globalization;

namespace ThreeKingdoms.Core.Fate
{
    /// <summary>
    /// One predicate of the condition language (spec §4.4). JSON form: <c>{ "op": args }</c>;
    /// all/any carry <see cref="children"/>, not carries one child, the rest carry string args (integers as invariant text).
    /// </summary>
    [Serializable]
    public class Condition
    {
        public string op;
        public string[] args = Array.Empty<string>();
        public Condition[] children = Array.Empty<Condition>();

        public static Condition Of(string op, params string[] args) => new Condition { op = op, args = args };
        public static Condition All(params Condition[] c) => new Condition { op = "all", children = c };
        public static Condition Any(params Condition[] c) => new Condition { op = "any", children = c };
        public static Condition Not(Condition c) => new Condition { op = "not", children = new[] { c } };

        public string Arg(int i) => args != null && i < args.Length ? args[i] : null;

        public int IntArg(int i) => int.Parse(Arg(i), NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    /// <summary>Arity of every allowed predicate; used by V-02 and the evaluator. Unknown ops return -1.</summary>
    public static class Predicates
    {
        public const string BattleObjective = "battleObjective";

        public static readonly string[] All =
        {
            "all", "any", "not", "nodeReached", "flag", "flagValue", "memory", "favorAtLeast", "trustAtLeast",
            "viewAtLeast", "relationAtLeast", "alive", "faction", "location", "recruitState", "presentAt",
            "factionDestroyed", "holdsPost", "hasItem", "characterHolds", "hasBook", "reputationAtLeast",
            "eventSeen", BattleObjective
        };

        /// <summary>Minimum and maximum arg count; children ops return (0,0).</summary>
        public static bool TryArity(string op, out int min, out int max)
        {
            switch (op)
            {
                case "all": case "any": case "not": min = max = 0; return true;
                case "nodeReached": case "flag": case "alive": case "factionDestroyed": case "hasItem":
                case "reputationAtLeast": case "eventSeen": case BattleObjective:
                    min = max = 1; return true;
                case "flagValue": case "favorAtLeast": case "trustAtLeast": case "faction": case "location":
                case "recruitState": case "presentAt": case "holdsPost": case "characterHolds": case "hasBook":
                    min = max = 2; return true;
                case "memory": min = 2; max = 3; return true;
                case "viewAtLeast": case "relationAtLeast": min = max = 3; return true;
                default: min = max = -1; return false;
            }
        }

        /// <summary>Index of the integer argument for numeric predicates, else -1.</summary>
        public static int IntArgIndex(string op)
        {
            switch (op)
            {
                case "favorAtLeast": case "trustAtLeast": return 1;
                case "viewAtLeast": case "relationAtLeast": return 2;
                case "reputationAtLeast": return 0;
                default: return -1;
            }
        }

        /// <summary>Predicates that are evaluated against the protagonist (forbidden in protagonist rescue cases, V-06).</summary>
        public static bool IsProtagonistAttitude(string op) => op == "favorAtLeast" || op == "trustAtLeast" || op == "viewAtLeast";
    }

    /// <summary>World facts owned by Gameplay (flags, inventory, reputation, battle objectives). Assembled per settle.</summary>
    public interface IConditionContext
    {
        bool Flag(string name);
        string FlagValue(string name);
        bool BattleObjective(string id); // fulfillment only
        bool HasItem(string itemId);
        int Reputation { get; }
        bool FactionDestroyed(string factionId);
        bool EventSeen(string eventId);
    }

    /// <summary>A context with nothing set; useful for "no interaction" runs and tests.</summary>
    public sealed class EmptyConditionContext : IConditionContext
    {
        public static readonly EmptyConditionContext Instance = new EmptyConditionContext();
        public bool Flag(string name) => false;
        public string FlagValue(string name) => null;
        public bool BattleObjective(string id) => false;
        public bool HasItem(string itemId) => false;
        public int Reputation => 0;
        public bool FactionDestroyed(string factionId) => false;
        public bool EventSeen(string eventId) => false;
    }

    public static class ConditionEvaluator
    {
        /// <summary>Evaluate against a fate section. A null condition is true. battleObjective is false unless allowFulfillment.</summary>
        public static bool Eval(Condition c, FateSaveSection s, IConditionContext ctx, FateDataSet data = null, bool allowFulfillment = false)
        {
            if (c == null) return true;
            ctx = ctx ?? EmptyConditionContext.Instance;
            switch (c.op)
            {
                case "all":
                    foreach (var ch in c.children) if (!Eval(ch, s, ctx, data, allowFulfillment)) return false;
                    return true;
                case "any":
                    foreach (var ch in c.children) if (Eval(ch, s, ctx, data, allowFulfillment)) return true;
                    return false;
                case "not":
                    return c.children.Length == 1 && !Eval(c.children[0], s, ctx, data, allowFulfillment);
                case "nodeReached":
                {
                    if (data == null) return s.world.stageId == c.Arg(0);
                    int cur = data.NodeIndex(s.world.stageId), want = data.NodeIndex(c.Arg(0));
                    return want >= 0 && cur >= want;
                }
                case "flag": return ctx.Flag(c.Arg(0));
                case "flagValue": return ctx.FlagValue(c.Arg(0)) == c.Arg(1);
                case "memory":
                {
                    var m = MemoryStore.Find(s.Character(c.Arg(0)), c.Arg(1));
                    if (m == null) return false;
                    return c.args.Length < 3 || m.value == c.Arg(2);
                }
                case "favorAtLeast":
                {
                    var r = s.Character(c.Arg(0))?.RelationTo(s.world.protagonistId);
                    return r != null && r.favor >= c.IntArg(1);
                }
                case "trustAtLeast":
                {
                    var r = s.Character(c.Arg(0))?.RelationTo(s.world.protagonistId);
                    return r != null && r.trust >= c.IntArg(1);
                }
                case "viewAtLeast":
                {
                    var r = s.Character(c.Arg(0))?.RelationTo(s.world.protagonistId);
                    int d = ViewDims.IndexOf(c.Arg(1));
                    return r?.views != null && d >= 0 && r.views[d] >= c.IntArg(2);
                }
                case "relationAtLeast":
                {
                    var r = s.Character(c.Arg(0))?.RelationTo(c.Arg(1));
                    return r != null && r.favor >= c.IntArg(2);
                }
                case "alive": return s.Character(c.Arg(0))?.IsAlive == true;
                case "faction": return s.Character(c.Arg(0))?.faction == c.Arg(1);
                case "location": return s.Character(c.Arg(0))?.location == c.Arg(1);
                case "recruitState": return s.Character(c.Arg(0))?.recruitState == c.Arg(1);
                case "presentAt":
                {
                    var ch = s.Character(c.Arg(0));
                    return ch != null && ch.IsAlive && ch.presentAt.Contains(c.Arg(1));
                }
                case "factionDestroyed": return ctx.FactionDestroyed(c.Arg(0));
                case "holdsPost": return s.world.slots.posts.TryGetValue(c.Arg(1), out var holder) && holder == c.Arg(0);
                case "hasItem": return ctx.HasItem(c.Arg(0));
                case "characterHolds":
                case "hasBook":
                {
                    var ch = s.Character(c.Arg(0));
                    if (ch == null) return false;
                    foreach (var h in ch.heldItems) if (h.itemId == c.Arg(1)) return true;
                    return false;
                }
                case "reputationAtLeast": return ctx.Reputation >= c.IntArg(0);
                case "eventSeen": return ctx.EventSeen(c.Arg(0));
                case Predicates.BattleObjective: return allowFulfillment && ctx.BattleObjective(c.Arg(0));
                default: return false; // unknown predicates never pass; V-02 rejects them at build time
            }
        }

        /// <summary>True when the condition tree contains the op anywhere.</summary>
        public static bool Contains(Condition c, string op)
        {
            if (c == null) return false;
            if (c.op == op) return true;
            foreach (var ch in c.children ?? Array.Empty<Condition>()) if (Contains(ch, op)) return true;
            return false;
        }
    }
}
