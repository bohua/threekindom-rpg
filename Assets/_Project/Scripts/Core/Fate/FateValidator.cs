using System;
using System.Collections.Generic;
using System.Globalization;

namespace ThreeKingdoms.Core.Fate
{
    [Serializable]
    public sealed class ValidationIssue
    {
        public string Code, Message;
        public override string ToString() => Code + " " + Message;
    }

    /// <summary>Build-time data checks V-01…V-06 (static part) and V-08 (spec §8.1). Pure; returns every issue found.</summary>
    public static class FateValidator
    {
        static readonly string[] NegativeTags = { "break_promise", "deceit", "insult" };
        static readonly string[] AllTags =
        {
            "benevolence", "valor", "wisdom", "keep_promise", "break_promise", "authority", "deceit", "courtesy", "insult",
            "gift", "gift_book", "gift_treasure", "fought_alongside", "fought_against", "spared", "captured"
        };

        public static List<ValidationIssue> ValidateAll(FateDataSet data)
        {
            var issues = new List<ValidationIssue>();
            V01References(data, issues);
            V02Conditions(data, issues);
            V03Defaults(data, issues);
            V04Anchors(data, issues);
            V05Causes(data, issues);
            V06Rescues(data, issues);
            V08History(data, issues);
            return issues;
        }

        static void Add(List<ValidationIssue> list, string code, string msg) => list.Add(new ValidationIssue { Code = code, Message = msg });

        static IEnumerable<FateRoute> Routes(FateDataSet d) => d.Raw.routes;

        static IEnumerable<string> VolumeStages(FateDataSet d, string volume)
        {
            foreach (var n in d.Nodes) if (n.volume == volume) yield return n.id;
        }

        static bool IsDefaultSomewhere(FateDataSet d, FateRoute route, string optionId)
        {
            foreach (var s in route.defaultRoute) if (s.optionId == optionId) return true;
            return false;
        }

        // ───────── V-01 ─────────
        public static void V01References(FateDataSet d, List<ValidationIssue> issues)
        {
            void Node(string id, string where) { if (id != null && d.Node(id) == null) Add(issues, "V-01", $"{where}: unknown node {id}"); }
            void Char(string id, string where) { if (id != null && d.Profile(id) == null) Add(issues, "V-01", $"{where}: unknown character {id}"); }
            void External(string kind, string id, string where)
            {
                var known = d.KnownIds(kind);
                if (id == null || known == null) return;
                if (Array.IndexOf(known, id) < 0) Add(issues, "V-01", $"{where}: unknown {kind} {id}");
            }

            foreach (var v in d.Raw.volumes) Char(v.protagonistId, "volume " + v.id);
            foreach (var route in Routes(d))
            {
                string w = "route " + (route.routeId ?? route.characterId);
                Char(route.characterId, w);
                Node(route.activeFrom, w);
                foreach (var st in route.defaultRoute)
                {
                    Node(st.stage, w);
                    if (d.Option(route, st.optionId) == null) Add(issues, "V-01", $"{w}: default option {st.optionId} not found");
                }
                foreach (var g in route.genericOptions) if (d.Generic(g) == null) Add(issues, "V-01", $"{w}: unknown generic option {g}");
                foreach (var e in route.randomEvents) if (d.RandomEvent(e) == null) Add(issues, "V-01", $"{w}: unknown random event {e}");
                foreach (var ir in route.initialRelations) Char(ir.target, w);
                foreach (var a in route.anchors)
                {
                    Node(a.stage, w + " anchor " + a.id);
                    External("scenes", a.departureSceneId, w);
                    External("scenes", a.compensationSceneId, w);
                    CheckConditionRefs(d, a.state, w + " anchor " + a.id, issues);
                }
                foreach (var o in route.fateOptions) CheckOptionRefs(d, o, w, issues);
            }
            foreach (var g in d.Raw.genericOptions) CheckOptionRefs(d, g, "generic", issues);
            foreach (var rc in d.Raw.rescues)
            {
                string w = "rescue " + rc.id;
                Char(rc.characterId, w);
                Node(rc.node, w);
                if (rc.hint != null) Node(rc.hint.fromStage, w);
                if (rc.onSuccess.afterRouteId != null && d.RouteById(rc.onSuccess.afterRouteId) == null)
                    Add(issues, "V-01", $"{w}: afterRouteId {rc.onSuccess.afterRouteId} not found");
                foreach (var u in rc.onSuccess.unchangedDeaths) Char(u, w);
                var pt = rc.onSuccess.powerTransfer;
                if (pt != null)
                {
                    Char(pt.successor, w);
                    Node(pt.atNode, w);
                    if (!PostExists(d, pt.position)) Add(issues, "V-01", $"{w}: unknown post {pt.position}");
                }
                foreach (var an in rc.onSuccess.affectedNodes) Node(an.node, w);
                foreach (var b in rc.onFail.memoryBroadcast) Char(b.who, w);
                if (rc.sceneVariant != null)
                {
                    External("scenes", rc.sceneVariant.@default, w);
                    External("scenes", rc.sceneVariant.eligible, w);
                }
                CheckConditionRefs(d, rc.eligibility, w, issues);
                CheckConditionRefs(d, rc.fulfillment, w, issues);
                if (rc.hint != null) CheckConditionRefs(d, rc.hint.condition, w, issues);
            }
            foreach (var h in d.Raw.historyEvents)
            {
                if (h.scope != null && h.scope.StartsWith("explicit:"))
                    foreach (var id in h.scope.Substring(9).Split(',')) Char(id, "history " + h.id);
                if (h.scope != null && h.scope.StartsWith("region:") && !RegionExists(d, h.scope.Substring(7)))
                    Add(issues, "V-01", $"history {h.id}: unknown region {h.scope.Substring(7)}");
            }
            foreach (var p in d.Raw.worldSlots.posts)
            {
                if (p.city != null && d.City(p.city) == null) Add(issues, "V-01", $"post {p.id}: unknown city {p.city}");
                foreach (var kv in p.historical) { Node(kv.Key, "post " + p.id); Char(kv.Value, "post " + p.id); }
            }
            foreach (var f in d.Raw.worldSlots.factionFloors)
            {
                Node(f.until, "factionFloor " + f.faction);
                foreach (var m in f.mustKeep) Char(m, "factionFloor " + f.faction);
            }
            foreach (var p in d.Profiles)
            {
                if (p.initialLocation != null && d.City(p.initialLocation) == null && d.KnownIds("places") != null)
                    External("places", p.initialLocation, "profile " + p.id);
                foreach (var post in p.powerPositions) if (!PostExists(d, post)) Add(issues, "V-01", $"profile {p.id}: unknown post {post}");
            }
        }

        static bool PostExists(FateDataSet d, string id)
        {
            foreach (var p in d.Raw.worldSlots.posts) if (p.id == id) return true;
            return false;
        }

        static bool RegionExists(FateDataSet d, string region)
        {
            foreach (var c in d.Raw.worldSlots.cities) if (c.region == region) return true;
            return false;
        }

        static void CheckOptionRefs(FateDataSet d, FateOption o, string where, List<ValidationIssue> issues)
        {
            string w = where + " option " + o.id;
            foreach (var st in o.stages) if (d.Node(st) == null) Add(issues, "V-01", $"{w}: unknown stage {st}");
            var e = o.effect;
            if (e != null)
            {
                if (e.location != null && d.City(e.location) == null && d.KnownIds("places") != null && Array.IndexOf(d.KnownIds("places"), e.location) < 0)
                    Add(issues, "V-01", $"{w}: unknown place {e.location}");
                if (e.post != null && !PostExists(d, e.post)) Add(issues, "V-01", $"{w}: unknown post {e.post}");
                if (e.recruitState != null && (!RecruitStates.IsKnown(e.recruitState) || e.recruitState == RecruitStates.InParty))
                    Add(issues, "V-01", $"{w}: effect.recruitState {e.recruitState} not allowed");
                if (e.relationTags != null) foreach (var k in e.relationTags.Keys) if (d.Profile(k) == null) Add(issues, "V-01", $"{w}: unknown character {k}");
                var scenes = d.KnownIds("scenes");
                if (scenes != null) foreach (var sc in e.presentAt) if (Array.IndexOf(scenes, sc) < 0) Add(issues, "V-01", $"{w}: unknown scene {sc}");
            }
            if (o.warningSceneId != null && d.KnownIds("scenes") != null && Array.IndexOf(d.KnownIds("scenes"), o.warningSceneId) < 0)
                Add(issues, "V-01", $"{w}: unknown scene {o.warningSceneId}");
            CheckConditionRefs(d, o.preconditions, w, issues);
        }

        static void CheckConditionRefs(FateDataSet d, Condition c, string where, List<ValidationIssue> issues)
        {
            if (c == null) return;
            switch (c.op)
            {
                case "memory": case "favorAtLeast": case "trustAtLeast": case "viewAtLeast": case "alive":
                case "faction": case "location": case "recruitState": case "presentAt": case "characterHolds": case "hasBook":
                    if (c.Arg(0) != null && d.Profile(c.Arg(0)) == null) Add(issues, "V-01", $"{where}: {c.op} names unknown character {c.Arg(0)}");
                    break;
                case "relationAtLeast":
                    foreach (var i in new[] { 0, 1 }) if (c.Arg(i) != null && d.Profile(c.Arg(i)) == null) Add(issues, "V-01", $"{where}: relationAtLeast names unknown character {c.Arg(i)}");
                    break;
                case "nodeReached":
                    if (d.Node(c.Arg(0)) == null) Add(issues, "V-01", $"{where}: nodeReached unknown node {c.Arg(0)}");
                    break;
                case "holdsPost":
                    if (d.Profile(c.Arg(0)) == null) Add(issues, "V-01", $"{where}: holdsPost unknown character {c.Arg(0)}");
                    if (!PostExists(d, c.Arg(1))) Add(issues, "V-01", $"{where}: holdsPost unknown post {c.Arg(1)}");
                    break;
            }
            foreach (var kind in new[] { ("flag", "flags"), ("flagValue", "flags"), ("hasItem", "items"), ("eventSeen", "events"), ("factionDestroyed", "factions"), (Predicates.BattleObjective, "objectives") })
                if (c.op == kind.Item1)
                {
                    var known = d.KnownIds(kind.Item2);
                    if (known != null && Array.IndexOf(known, c.Arg(0)) < 0) Add(issues, "V-01", $"{where}: unknown {kind.Item2} {c.Arg(0)}");
                }
            if (c.op == "presentAt" && d.KnownIds("scenes") != null && Array.IndexOf(d.KnownIds("scenes"), c.Arg(1)) < 0)
                Add(issues, "V-01", $"{where}: unknown scene {c.Arg(1)}");
            foreach (var ch in c.children ?? Array.Empty<Condition>()) CheckConditionRefs(d, ch, where, issues);
        }

        // ───────── V-02 ─────────
        public static void V02Conditions(FateDataSet d, List<ValidationIssue> issues)
        {
            foreach (var route in Routes(d))
            {
                string w = "route " + (route.routeId ?? route.characterId);
                foreach (var a in route.anchors) CheckCondition(a.state, w + " anchor " + a.id, false, issues);
                foreach (var o in route.fateOptions) CheckCondition(o.preconditions, w + " option " + o.id, false, issues);
            }
            foreach (var g in d.Raw.genericOptions) CheckCondition(g.preconditions, "generic " + g.id, false, issues);
            foreach (var rc in d.Raw.rescues)
            {
                CheckCondition(rc.eligibility, "rescue " + rc.id + " eligibility", false, issues);
                CheckCondition(rc.fulfillment, "rescue " + rc.id + " fulfillment", true, issues);
                if (rc.hint != null) CheckCondition(rc.hint.condition, "rescue " + rc.id + " hint", false, issues);
            }
        }

        public static void CheckCondition(Condition c, string where, bool fulfillment, List<ValidationIssue> issues)
        {
            if (c == null) return;
            if (!Predicates.TryArity(c.op, out int min, out int max))
            {
                Add(issues, "V-02", $"{where}: unknown predicate '{c.op}'");
                return;
            }
            if (c.op == Predicates.BattleObjective && !fulfillment)
                Add(issues, "V-02", $"{where}: battleObjective is only allowed in fulfillment");
            if (c.op == "all" || c.op == "any")
            {
                if (c.children == null || c.children.Length == 0) Add(issues, "V-02", $"{where}: {c.op} needs children");
            }
            else if (c.op == "not")
            {
                if (c.children == null || c.children.Length != 1) Add(issues, "V-02", $"{where}: not needs exactly one child");
            }
            else
            {
                int n = c.args?.Length ?? 0;
                if (n < min || n > max) Add(issues, "V-02", $"{where}: {c.op} takes {min}–{max} args, got {n}");
                int ii = Predicates.IntArgIndex(c.op);
                if (ii >= 0 && ii < n && !int.TryParse(c.args[ii], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                    Add(issues, "V-02", $"{where}: {c.op} arg {ii} must be an integer, got {c.args[ii]}");
                if (c.op == "viewAtLeast" && n >= 2 && ViewDims.IndexOf(c.args[1]) < 0) Add(issues, "V-02", $"{where}: unknown view {c.args[1]}");
                if (c.op == "recruitState" && n >= 2 && !RecruitStates.IsKnown(c.args[1])) Add(issues, "V-02", $"{where}: unknown recruitState {c.args[1]}");
            }
            foreach (var ch in c.children ?? Array.Empty<Condition>()) CheckCondition(ch, where, fulfillment, issues);
        }

        // ───────── V-03 ─────────
        public static void V03Defaults(FateDataSet d, List<ValidationIssue> issues)
        {
            foreach (var route in Routes(d))
            {
                string w = "route " + (route.routeId ?? route.characterId);
                int deathIndex = int.MaxValue; // no defaults are needed after the stage of a dies anchor
                foreach (var a in route.anchors) if (a.IsDies) deathIndex = Math.Min(deathIndex, d.NodeIndex(a.stage));
                foreach (var stage in VolumeStages(d, route.volume))
                {
                    if (!d.IsActive(route, stage) || d.NodeIndex(stage) > deathIndex) continue;
                    int count = 0;
                    foreach (var st in route.defaultRoute) if (st.stage == stage) count++;
                    if (count != 1) { Add(issues, "V-03", $"{w}: {count} default options at {stage} (need exactly 1)"); continue; }
                    var def = d.Option(route, route.DefaultOptionFor(stage));
                    if (def == null) continue;
                    if (def.preconditions != null) Add(issues, "V-03", $"{w}: default {def.id} has preconditions");
                }
            }
            // Defaults must satisfy HARD anchors: walk every volume on default routes only.
            foreach (var v in d.Raw.volumes)
                DefaultWalk(d, v.id, (stage, s) =>
                {
                    foreach (var c in s.characters.Values)
                    {
                        var route = d.RouteFor(c);
                        if (route == null || !c.IsAlive || !d.IsActive(route, stage)) continue;
                        foreach (var a in route.anchors)
                            if (a.IsHard && !a.IsDies && a.stage == stage && !ConditionEvaluator.Eval(a.state, s, EmptyConditionContext.Instance, d))
                                Add(issues, "V-03", $"route {c.id}: default route breaks HARD anchor {a.id} at {stage}");
                    }
                });
        }

        /// <summary>Walk a volume applying only default options (no scoring); calls back with the state of each stage.</summary>
        public static void DefaultWalk(FateDataSet d, string volume, Action<string, FateSaveSection> visit)
        {
            FateSaveSection s;
            try { s = FateEngine.CreateInitialState(d, volume, 0); }
            catch (ArgumentException) { return; }
            if (s.world.stageId == null) return;
            visit(s.world.stageId, s);
            string prev = s.world.stageId;
            for (string node = d.NextNode(prev); node != null; prev = node, node = d.NextNode(node))
            {
                foreach (var c in s.characters.Values)
                {
                    var main = d.MainRoute(c.id);
                    if (main == null || !c.alive) continue;
                    foreach (var a in main.anchors) if (a.IsDies && a.stage == prev) { c.alive = false; c.presentAt.Clear(); }
                }
                FateEngine.ApplyHistoricalPosts(d, s, node);
                s.world.stageId = node;
                foreach (var c in s.characters.Values)
                {
                    c.presentAt.Clear();
                    var route = d.RouteFor(c);
                    if (route == null || !c.IsAlive || !d.IsActive(route, node)) continue;
                    FateEngine.ApplyEffect(s, c, d.Option(route, route.DefaultOptionFor(node)), null);
                }
                visit(node, s);
            }
        }

        // ───────── V-04 ─────────
        public static void V04Anchors(FateDataSet d, List<ValidationIssue> issues)
        {
            foreach (var v in d.Raw.volumes)
            {
                DefaultWalk(d, v.id, (stage, s) =>
                {
                    foreach (var c in new List<CharacterState>(s.characters.Values))
                    {
                        var route = d.RouteFor(c);
                        if (route == null || !c.IsAlive || !d.IsActive(route, stage)) continue;
                        foreach (var a in route.anchors)
                        {
                            if (!a.IsHard || a.IsDies || a.stage != stage) continue;
                            bool satisfiable = false;
                            foreach (var o in d.OptionsFor(route, stage))
                            {
                                bool ok = Hypothetical(d, s, c, o, a);
                                if (ok) satisfiable = true;
                                else if (ConditionEvaluator.Contains(a.state, "presentAt"))
                                    Add(issues, "V-04", $"route {c.id}: option {o.id} conflicts with presentAt anchor {a.id} at {stage}");
                            }
                            if (!satisfiable) Add(issues, "V-04", $"route {c.id}: no candidate satisfies HARD anchor {a.id} at {stage}");
                        }
                    }
                });
                // anchors that keep a recruitable character away from the protagonist's party need a departure scene
                foreach (var route in Routes(d))
                {
                    if (route.volume != v.id) continue;
                    var p = d.Profile(route.characterId);
                    if (p == null || Array.IndexOf(p.recruitableIn, v.id) < 0) continue;
                    foreach (var a in route.anchors)
                        if (a.IsHard && !a.IsDies && ConflictsWithParty(a.state, route.characterId, v.protagonistFaction) && string.IsNullOrEmpty(a.departureSceneId))
                            Add(issues, "V-04", $"route {route.characterId}: anchor {a.id} conflicts with inParty and has no departureSceneId");
                }
            }
        }

        static bool Hypothetical(FateDataSet d, FateSaveSection s, CharacterState c, FateOption o, Anchor a)
        {
            var h = c.Clone();
            FateEngine.ApplyEffect(s, h, o, null);
            s.characters[c.id] = h;
            try { return ConditionEvaluator.Eval(a.state, s, EmptyConditionContext.Instance, d); }
            finally { s.characters[c.id] = c; }
        }

        /// <summary>An anchor that pins the character to a non-protagonist faction or a non-party recruit state.</summary>
        public static bool ConflictsWithParty(Condition c, string self, string protagonistFaction)
        {
            if (c == null) return false;
            if (c.op == "faction" && c.Arg(0) == self && c.Arg(1) != protagonistFaction) return true;
            if (c.op == "recruitState" && c.Arg(0) == self && c.Arg(1) != RecruitStates.InParty && c.Arg(1) != RecruitStates.Reserve) return true;
            if (c.op == "not") return false;
            foreach (var ch in c.children ?? Array.Empty<Condition>()) if (ConflictsWithParty(ch, self, protagonistFaction)) return true;
            return false;
        }

        // ───────── V-05 ─────────
        public static void V05Causes(FateDataSet d, List<ValidationIssue> issues)
        {
            foreach (var route in Routes(d))
            {
                var p = d.Profile(route.characterId);
                var vol = d.Volume(route.volume);
                foreach (var o in route.fateOptions)
                    if (!IsDefaultSomewhere(d, route, o.id)) CheckCauses(d, o, p, vol, "route " + route.characterId, issues);
                foreach (var g in route.genericOptions)
                {
                    var o = d.Generic(g);
                    if (o != null && !IsDefaultSomewhere(d, route, o.id)) CheckCauses(d, o, p, vol, "route " + route.characterId, issues);
                }
            }
        }

        public static void CheckCauses(FateDataSet d, FateOption o, CharacterProfile p, VolumeDef vol, string where, List<ValidationIssue> issues)
        {
            string w = where + " option " + o.id;
            if (o.requiresCause == null || o.requiresCause.Length == 0) Add(issues, "V-05", $"{w}: non-default option needs requiresCause");
            foreach (var tok in o.requiresCause ?? Array.Empty<string>())
            {
                if (!WellFormed(tok)) { Add(issues, "V-05", $"{w}: malformed cause token {tok}"); continue; }
                int dir = Direction(d, tok, vol);
                if (o.playerProximity > 0 && dir <= 0) Add(issues, "V-05", $"{w}: proximity +1 needs a positive cause, got {tok}");
                if (o.playerProximity < 0 && dir >= 0) Add(issues, "V-05", $"{w}: proximity −1 needs a negative cause, got {tok}");
            }
            var e = o.effect ?? new FateEffect();
            bool leaving = e.dies || e.recruitState == RecruitStates.Departed;
            if (leaving && !o.twoStep) Add(issues, "V-05", $"{w}: leaving/death options must be twoStep");
            if (o.twoStep)
            {
                if (string.IsNullOrEmpty(o.warningSceneId)) Add(issues, "V-05", $"{w}: twoStep option needs warningSceneId");
                bool hasNext = false;
                foreach (var st in o.stages)
                {
                    var next = d.NextNode(st);
                    if (next != null && Array.IndexOf(o.stages, next) >= 0) hasNext = true;
                }
                if (!hasNext) Add(issues, "V-05", $"{w}: twoStep stages must include the stage right after an announcing stage");
            }
            if (e.dies && p?.tier == Tiers.T3)
            {
                bool eventCause = false;
                foreach (var tok in o.requiresCause ?? Array.Empty<string>())
                    if (tok.StartsWith("RANDOM:") || tok.StartsWith("HISTORY:")) eventCause = true;
                if (!eventCause) Add(issues, "V-05", $"{w}: T3 death needs a RANDOM: or HISTORY: cause");
            }
        }

        static bool WellFormed(string tok)
        {
            if (tok == "DEFAULT" || tok == "PLAYER:+" || tok == "PLAYER:-") return true;
            if (tok.StartsWith("PLAYER:")) return Array.IndexOf(AllTags, tok.Substring(7)) >= 0;
            return (tok.StartsWith("RANDOM:") && tok.Length > 7) || (tok.StartsWith("HISTORY:") && tok.Length > 8) || (tok.StartsWith("RELATION:") && tok.Length > 9);
        }

        /// <summary>+1 positive, −1 negative, 0 neutral (RANDOM/HISTORY/other RELATION).</summary>
        static int Direction(FateDataSet d, string tok, VolumeDef vol)
        {
            if (tok == "PLAYER:+") return 1;
            if (tok == "PLAYER:-") return -1;
            if (tok.StartsWith("PLAYER:")) return Array.IndexOf(NegativeTags, tok.Substring(7)) >= 0 ? -1 : 1;
            if (tok.StartsWith("RELATION:"))
            {
                var who = tok.Substring(9);
                if (vol != null && (who == vol.protagonistId || d.Profile(who)?.initialFaction == vol.protagonistFaction)) return 1;
            }
            return 0;
        }

        // ───────── V-06 (static) ─────────
        public static void V06Rescues(FateDataSet d, List<ValidationIssue> issues)
        {
            foreach (var rc in d.Raw.rescues)
            {
                string w = "rescue " + rc.id;
                var p = d.Profile(rc.characterId);
                if (p != null && p.powerPositions.Length > 0 && rc.onSuccess.powerTransfer == null)
                    Add(issues, "V-06", $"{w}: {p.id} holds power positions; powerTransfer is required");
                if (rc.subject == "protagonist")
                    foreach (var c in new[] { rc.eligibility, rc.hint?.condition })
                        foreach (var op in new[] { "favorAtLeast", "trustAtLeast", "viewAtLeast" })
                            if (ConditionEvaluator.Contains(c, op)) Add(issues, "V-06", $"{w}: protagonist case may not use {op}");
                if (!HasDiesAnchor(d, rc.characterId, rc.node)) Add(issues, "V-06", $"{w}: {rc.characterId} has no dies anchor at {rc.node}");
                foreach (var u in rc.onSuccess.unchangedDeaths)
                    if (!HasDiesAnchor(d, u, rc.node)) Add(issues, "V-06", $"{w}: unchangedDeaths {u} has no dies anchor at {rc.node}");
                var dialogues = d.KnownIds("dialogues");
                if (dialogues != null)
                    foreach (var an in rc.onSuccess.affectedNodes)
                        if (Array.IndexOf(dialogues, an.dialogueVariant) < 0) Add(issues, "V-06", $"{w}: dialogue variant {an.dialogueVariant} missing");
                if (rc.onSuccess.tierAfter != Tiers.T1S) Add(issues, "V-06", $"{w}: tierAfter must be T1S");
            }
            foreach (var f in d.Raw.worldSlots.factionFloors)
            {
                int until = d.NodeIndex(f.until);
                foreach (var m in f.mustKeep)
                {
                    var route = d.MainRoute(m);
                    if (route == null) continue;
                    foreach (var a in route.anchors)
                        if (a.IsDies && d.NodeIndex(a.stage) <= until)
                            Add(issues, "V-06", $"factionFloor {f.faction}: mustKeep {m} dies at {a.stage} before {f.until}");
                }
            }
        }

        static bool HasDiesAnchor(FateDataSet d, string characterId, string node)
        {
            var route = d.MainRoute(characterId);
            if (route == null) return false;
            foreach (var a in route.anchors) if (a.IsDies && a.stage == node) return true;
            return false;
        }

        // ───────── V-08 ─────────
        public static void V08History(FateDataSet d, List<ValidationIssue> issues)
        {
            int last = -1;
            string vol = null;
            foreach (var h in d.Raw.historyEvents)
            {
                var node = d.Node(h.stage);
                if (node == null) { Add(issues, "V-08", $"history {h.id}: stage {h.stage} is not a node of a loaded volume"); continue; }
                if (vol != null && node.volume != vol) { last = -1; }
                vol = node.volume;
                int idx = d.NodeIndex(h.stage);
                if (idx < last) Add(issues, "V-08", $"history {h.id}: stage {h.stage} out of order");
                last = Math.Max(last, idx);
                if (h.delayStages < 0) Add(issues, "V-08", $"history {h.id}: negative delayStages");
            }
        }
    }
}
