using System;
using System.Collections.Generic;

namespace ThreeKingdoms.Core.Fate
{
    /// <summary>Raw design data for one or more volumes, as loaded from JSON. <see cref="FateDataSet"/> indexes it.</summary>
    [Serializable]
    public class FateDataBundle
    {
        public VolumeDef[] volumes = Array.Empty<VolumeDef>();
        public NodeDef[] nodes = Array.Empty<NodeDef>();
        public CharacterProfile[] profiles = Array.Empty<CharacterProfile>();
        public FateRoute[] routes = Array.Empty<FateRoute>();
        public FateOption[] genericOptions = Array.Empty<FateOption>();
        public RescueCase[] rescues = Array.Empty<RescueCase>();
        public HistoryEventDef[] historyEvents = Array.Empty<HistoryEventDef>();
        public RandomEventDef[] randomEvents = Array.Empty<RandomEventDef>();
        public WorldSlotsDef worldSlots = new WorldSlotsDef();
        public TuningTable tuning;
        public FateMigrationTable migrations = new FateMigrationTable();
        /// <summary>Known external ids (scenes, flags, items, books, factions, places, objectives) for V-01; null skips those checks.</summary>
        public SortedDictionary<string, string[]> knownIds;
    }

    /// <summary>Read-only, indexed view over a <see cref="FateDataBundle"/>. Lookups are by id; iteration is ordinal.</summary>
    public sealed class FateDataSet
    {
        public FateDataBundle Raw { get; }
        public TuningTable Tuning { get; }
        readonly SortedDictionary<string, CharacterProfile> profiles = new SortedDictionary<string, CharacterProfile>(StringComparer.Ordinal);
        readonly SortedDictionary<string, FateRoute> mainRoutes = new SortedDictionary<string, FateRoute>(StringComparer.Ordinal);
        readonly SortedDictionary<string, FateRoute> routesById = new SortedDictionary<string, FateRoute>(StringComparer.Ordinal);
        readonly SortedDictionary<string, FateOption> generic = new SortedDictionary<string, FateOption>(StringComparer.Ordinal);
        readonly SortedDictionary<string, RandomEventDef> randomEvents = new SortedDictionary<string, RandomEventDef>(StringComparer.Ordinal);
        readonly SortedDictionary<string, CityDef> cities = new SortedDictionary<string, CityDef>(StringComparer.Ordinal);
        readonly List<NodeDef> nodeOrder = new List<NodeDef>();

        public FateDataSet(FateDataBundle raw)
        {
            Raw = raw ?? throw new ArgumentNullException(nameof(raw));
            Tuning = raw.tuning ?? TuningTable.CreateDefault();
            foreach (var p in raw.profiles) if (p?.id != null) profiles[p.id] = p;
            foreach (var r in raw.routes)
            {
                if (r?.characterId == null) continue;
                if (string.IsNullOrEmpty(r.routeId)) mainRoutes[r.characterId] = r;
                else routesById[r.routeId] = r;
            }
            foreach (var g in raw.genericOptions) if (g?.id != null) generic[g.id] = g;
            foreach (var e in raw.randomEvents) if (e?.id != null) randomEvents[e.id] = e;
            foreach (var c in raw.worldSlots?.cities ?? Array.Empty<CityDef>()) if (c?.id != null) cities[c.id] = c;
            BuildNodeOrder();
        }

        /// <summary>The same data with a different tuning table (simulator EPS report, tests).</summary>
        public FateDataSet WithTuning(TuningTable tuning)
        {
            var b = new FateDataBundle
            {
                volumes = Raw.volumes, nodes = Raw.nodes, profiles = Raw.profiles, routes = Raw.routes, genericOptions = Raw.genericOptions,
                rescues = Raw.rescues, historyEvents = Raw.historyEvents, randomEvents = Raw.randomEvents, worldSlots = Raw.worldSlots,
                tuning = tuning, migrations = Raw.migrations, knownIds = Raw.knownIds
            };
            return new FateDataSet(b);
        }

        void BuildNodeOrder()
        {
            // Follow next-links from each volume's first node (the node nobody points to); unlinked nodes keep file order.
            var byId = new Dictionary<string, NodeDef>(StringComparer.Ordinal);
            var pointed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var n in Raw.nodes) { byId[n.id] = n; if (n.next != null) pointed.Add(n.next); }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var n in Raw.nodes)
            {
                if (pointed.Contains(n.id) || seen.Contains(n.id)) continue;
                for (var cur = n; cur != null && seen.Add(cur.id); cur = cur.next != null && byId.TryGetValue(cur.next, out var nx) ? nx : null)
                    nodeOrder.Add(cur);
            }
            foreach (var n in Raw.nodes) if (seen.Add(n.id)) nodeOrder.Add(n);
        }

        public IEnumerable<CharacterProfile> Profiles => profiles.Values;
        public IReadOnlyList<NodeDef> Nodes => nodeOrder;

        public CharacterProfile Profile(string id) => id != null && profiles.TryGetValue(id, out var p) ? p : null;
        public FateRoute MainRoute(string characterId) => characterId != null && mainRoutes.TryGetValue(characterId, out var r) ? r : null;
        public FateRoute RouteById(string routeId) => routeId != null && routesById.TryGetValue(routeId, out var r) ? r : null;
        public FateOption Generic(string id) => id != null && generic.TryGetValue(id, out var o) ? o : null;
        public RandomEventDef RandomEvent(string id) => id != null && randomEvents.TryGetValue(id, out var e) ? e : null;
        public CityDef City(string id) => id != null && cities.TryGetValue(id, out var c) ? c : null;

        public VolumeDef Volume(string id)
        {
            foreach (var v in Raw.volumes) if (v.id == id) return v;
            return null;
        }

        public NodeDef Node(string id)
        {
            foreach (var n in nodeOrder) if (n.id == id) return n;
            return null;
        }

        public int NodeIndex(string id)
        {
            for (int i = 0; i < nodeOrder.Count; i++) if (nodeOrder[i].id == id) return i;
            return -1;
        }

        public string NextNode(string id) => Node(id)?.next;

        /// <summary>The route that governs a character now (survivor route after a rescue, else the main route).</summary>
        public FateRoute RouteFor(CharacterState c) => (c?.routeId != null ? RouteById(c.routeId) : null) ?? MainRoute(c?.id);

        /// <summary>Find an option by id among a route's own options and the generic library.</summary>
        public FateOption Option(FateRoute route, string optionId)
        {
            if (optionId == null) return null;
            if (route != null) foreach (var o in route.fateOptions) if (o.id == optionId) return o;
            return Generic(optionId);
        }

        /// <summary>Candidates for a stage in ordinal id order: route options plus permitted generics that apply to the stage.</summary>
        public List<FateOption> OptionsFor(FateRoute route, string stage)
        {
            var map = new SortedDictionary<string, FateOption>(StringComparer.Ordinal);
            if (route != null)
            {
                foreach (var o in route.fateOptions) if (o.AppliesTo(stage)) map[o.id] = o;
                foreach (var g in route.genericOptions ?? Array.Empty<string>())
                {
                    var o = Generic(g);
                    if (o != null && o.AppliesTo(stage)) map[o.id] = o;
                }
                var def = Option(route, route.DefaultOptionFor(stage));
                if (def != null) map[def.id] = def;
            }
            return new List<FateOption>(map.Values);
        }

        public IEnumerable<RescueCase> RescuesAt(string node)
        {
            foreach (var r in Raw.rescues) if (r.node == node) yield return r;
        }

        public RescueCase Rescue(string id)
        {
            foreach (var r in Raw.rescues) if (r.id == id) return r;
            return null;
        }

        public bool IsActive(FateRoute route, string stage)
        {
            if (route == null) return false;
            if (string.IsNullOrEmpty(route.activeFrom)) return true;
            int a = NodeIndex(route.activeFrom), s = NodeIndex(stage);
            return a < 0 || s >= a;
        }

        public string[] KnownIds(string kind)
        {
            if (Raw.knownIds == null) return null;
            return Raw.knownIds.TryGetValue(kind, out var ids) ? ids : Array.Empty<string>();
        }
    }
}
