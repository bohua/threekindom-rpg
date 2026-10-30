using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ThreeKingdoms.Core.Fate;

namespace ThreeKingdoms.Data.Fate
{
    public sealed class FateDataException : Exception
    {
        public FateDataException(string message) : base(message) { }
    }

    /// <summary>
    /// Loads fate JSON (ADR-0013 Decision 2) into the Core DTOs and wraps them as a read-only <see cref="FateDataSet"/>.
    /// Numbers must be integers: non-integer numbers are rejected instead of being read through double.
    /// </summary>
    public static class FateJsonLoader
    {
        /// <summary>File names inside a volume folder. Missing optional files load as empty.</summary>
        public static readonly string[] Files =
        {
            "volume.json", "nodes.json", "profiles.json", "routes.json", "generic_options.json", "rescues.json",
            "history_events.json", "random_events.json", "world_slots.json", "tuning.json", "fate_migrations.json", "known_ids.json"
        };

        public static JsonSerializerSettings Settings()
        {
            return new JsonSerializerSettings
            {
                FloatParseHandling = FloatParseHandling.Decimal,
                MissingMemberHandling = MissingMemberHandling.Error,
                NullValueHandling = NullValueHandling.Ignore,
                ObjectCreationHandling = ObjectCreationHandling.Auto,
                Converters = { new ConditionJsonConverter() }
            };
        }

        public static FateDataSet LoadDirectory(string dir) => new FateDataSet(LoadBundle(name =>
        {
            var path = Path.Combine(dir, name);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }));

        /// <summary>Load from any source (TextAssets, files, test strings). read(name) returns null for a missing file.</summary>
        public static FateDataBundle LoadBundle(Func<string, string> read)
        {
            var b = new FateDataBundle();
            var vol = Parse<VolumeDef>(read, "volume.json");
            if (vol != null) b.volumes = new[] { vol };
            b.nodes = Parse<NodeDef[]>(read, "nodes.json") ?? b.nodes;
            b.profiles = Parse<CharacterProfile[]>(read, "profiles.json") ?? b.profiles;
            b.routes = Parse<FateRoute[]>(read, "routes.json") ?? b.routes;
            b.genericOptions = Parse<FateOption[]>(read, "generic_options.json") ?? b.genericOptions;
            b.rescues = Parse<RescueCase[]>(read, "rescues.json") ?? b.rescues;
            b.historyEvents = Parse<HistoryEventDef[]>(read, "history_events.json") ?? b.historyEvents;
            b.randomEvents = Parse<RandomEventDef[]>(read, "random_events.json") ?? b.randomEvents;
            b.worldSlots = Parse<WorldSlotsDef>(read, "world_slots.json") ?? b.worldSlots;
            b.tuning = Parse<TuningTable>(read, "tuning.json");
            b.migrations = Parse<FateMigrationTable>(read, "fate_migrations.json") ?? b.migrations;
            b.knownIds = Parse<SortedDictionary<string, string[]>>(read, "known_ids.json");
            if (b.knownIds != null) b.knownIds = new SortedDictionary<string, string[]>(b.knownIds, StringComparer.Ordinal);
            return b;
        }

        /// <summary>Merge several volume folders (cross-volume V-01, spec §8.1).</summary>
        public static FateDataSet LoadDirectories(params string[] dirs)
        {
            var merged = new FateDataBundle();
            var volumes = new List<VolumeDef>(); var nodes = new List<NodeDef>(); var profiles = new List<CharacterProfile>();
            var routes = new List<FateRoute>(); var generic = new List<FateOption>(); var rescues = new List<RescueCase>();
            var history = new List<HistoryEventDef>(); var random = new List<RandomEventDef>();
            var posts = new List<PostDef>(); var cities = new List<CityDef>(); var floors = new List<FactionFloor>();
            foreach (var dir in dirs)
            {
                var b = LoadBundle(name => { var p = Path.Combine(dir, name); return File.Exists(p) ? File.ReadAllText(p) : null; });
                volumes.AddRange(b.volumes); nodes.AddRange(b.nodes); profiles.AddRange(b.profiles); routes.AddRange(b.routes);
                generic.AddRange(b.genericOptions); rescues.AddRange(b.rescues); history.AddRange(b.historyEvents); random.AddRange(b.randomEvents);
                posts.AddRange(b.worldSlots.posts); cities.AddRange(b.worldSlots.cities); floors.AddRange(b.worldSlots.factionFloors);
                merged.tuning = merged.tuning ?? b.tuning;
                merged.knownIds = merged.knownIds ?? b.knownIds;
            }
            merged.volumes = volumes.ToArray(); merged.nodes = nodes.ToArray(); merged.profiles = profiles.ToArray();
            merged.routes = routes.ToArray(); merged.genericOptions = generic.ToArray(); merged.rescues = rescues.ToArray();
            merged.historyEvents = history.ToArray(); merged.randomEvents = random.ToArray();
            merged.worldSlots = new WorldSlotsDef { posts = posts.ToArray(), cities = cities.ToArray(), factionFloors = floors.ToArray() };
            return new FateDataSet(merged);
        }

        static T Parse<T>(Func<string, string> read, string name) where T : class
        {
            var text = read(name);
            if (string.IsNullOrWhiteSpace(text)) return null;
            try
            {
                return JsonConvert.DeserializeObject<T>(text, Settings());
            }
            catch (JsonException e)
            {
                throw new FateDataException($"{name}: {e.Message}");
            }
        }

        public static string ToJson(object value, bool indented = true) =>
            JsonConvert.SerializeObject(value, indented ? Formatting.Indented : Formatting.None, Settings());
    }

    /// <summary>Condition language JSON: <c>{ "op": args }</c> with all/any arrays and a single not child (spec §4.4).</summary>
    public sealed class ConditionJsonConverter : JsonConverter<Condition>
    {
        public override Condition ReadJson(JsonReader reader, Type objectType, Condition existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var tok = JToken.Load(reader);
            return tok.Type == JTokenType.Null ? null : Parse(tok);
        }

        public static Condition Parse(JToken tok)
        {
            if (!(tok is JObject o) || o.Count != 1)
                throw new FateDataException($"condition must be an object with exactly one predicate: {tok.ToString(Formatting.None)}");
            JProperty prop = null;
            foreach (var p in o.Properties()) prop = p;
            var c = new Condition { op = prop.Name };
            var v = prop.Value;
            if (c.op == "all" || c.op == "any")
            {
                if (!(v is JArray arr)) throw new FateDataException($"{c.op} needs an array");
                var list = new List<Condition>();
                foreach (var ch in arr) list.Add(Parse(ch));
                c.children = list.ToArray();
            }
            else if (c.op == "not")
            {
                c.children = new[] { Parse(v) };
            }
            else if (v is JArray args)
            {
                var list = new List<string>();
                foreach (var a in args) list.Add(Arg(a));
                c.args = list.ToArray();
            }
            else
            {
                c.args = new[] { Arg(v) };
            }
            return c;
        }

        static string Arg(JToken t)
        {
            switch (t.Type)
            {
                case JTokenType.String: return (string)t;
                case JTokenType.Integer: return ((long)t).ToString(CultureInfo.InvariantCulture);
                case JTokenType.Boolean: return (bool)t ? "true" : "false";
                default: throw new FateDataException($"condition arguments must be strings or integers, got {t.Type}: {t}");
            }
        }

        public override void WriteJson(JsonWriter writer, Condition value, JsonSerializer serializer)
        {
            if (value == null) { writer.WriteNull(); return; }
            writer.WriteStartObject();
            writer.WritePropertyName(value.op);
            if (value.op == "all" || value.op == "any")
            {
                writer.WriteStartArray();
                foreach (var ch in value.children) WriteJson(writer, ch, serializer);
                writer.WriteEndArray();
            }
            else if (value.op == "not") WriteJson(writer, value.children.Length > 0 ? value.children[0] : null, serializer);
            else if (value.args.Length == 1) WriteArg(writer, value.op, 0, value.args[0]);
            else
            {
                writer.WriteStartArray();
                for (int i = 0; i < value.args.Length; i++) WriteArg(writer, value.op, i, value.args[i]);
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
        }

        static void WriteArg(JsonWriter w, string op, int i, string a)
        {
            if (Predicates.IntArgIndex(op) == i && long.TryParse(a, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) w.WriteValue(n);
            else w.WriteValue(a);
        }
    }
}
