using System;
using System.Collections.Generic;

namespace ThreeKingdoms.Core.Fate
{
    /// <summary>
    /// Deterministic fate settlement (spec v5.1 §5, ADR-0013). Pure: inputs are cloned, never mutated;
    /// integer Q16.16 only; all iteration is ordinal.
    /// </summary>
    public static class FateEngine
    {
        public const string DefaultCause = "DEFAULT";

        // ───────────────────────── volume start ─────────────────────────

        /// <summary>The state at the first node of a volume, before any settle: profiles placed, defaults applied.</summary>
        public static FateSaveSection CreateInitialState(FateDataSet data, string volumeId, ulong fateSeed)
        {
            var vol = data.Volume(volumeId) ?? throw new ArgumentException("unknown volume " + volumeId);
            string first = null;
            foreach (var n in data.Nodes) if (n.volume == volumeId) { first = n.id; break; }
            var s = new FateSaveSection { fateDataVersion = vol.fateDataVersion };
            s.world.volume = volumeId;
            s.world.protagonistId = vol.protagonistId;
            s.world.stageId = first;
            s.world.fateSeed = FateStreams.FormatSeed(fateSeed);
            s.world.fateDataVersion = vol.fateDataVersion;

            foreach (var p in data.Profiles)
            {
                var route = data.MainRoute(p.id);
                if (p.id != vol.protagonistId && (route == null || route.volume != volumeId)) continue;
                var c = new CharacterState
                {
                    id = p.id, tier = p.tier, faction = p.initialFaction, location = p.initialLocation,
                    recruitState = p.initialRecruitState ?? RecruitStates.Unknown, role = p.initialRole,
                    scriptedParty = p.initialRecruitState == RecruitStates.InParty
                };
                foreach (var it in p.ownedItems ?? Array.Empty<HeldItem>()) c.heldItems.Add(it.Clone());
                if (route != null)
                {
                    foreach (var ir in route.initialRelations)
                    {
                        c.relations[ir.target] = new Relation
                        {
                            favor = ir.favor, trust = ir.trust, tags = new List<string>(ir.tags ?? Array.Empty<string>()),
                            baseline = new RelationBaseline { favor = ir.favor, trust = ir.trust }
                        };
                    }
                }
                if (p.id != vol.protagonistId)
                {
                    if (!c.relations.TryGetValue(vol.protagonistId, out var toP))
                        c.relations[vol.protagonistId] = toP = new Relation();
                    if (toP.views == null) toP.views = new int[6];
                }
                s.characters[p.id] = c;
            }

            foreach (var c in s.characters.Values)
            {
                var route = data.RouteFor(c);
                if (route == null || !data.IsActive(route, first)) continue;
                var def = data.Option(route, route.DefaultOptionFor(first));
                if (def == null) continue;
                ApplyEffect(s, c, def, null);
                c.fateHistory.Add(new FateHistoryEntry { stage = first, optionId = def.id, cause = DefaultCause, @default = true });
            }
            ApplyHistoricalPosts(data, s, first);
            DistributeHistory(data, s, first, new SortedDictionary<string, SortedDictionary<string, Token>>(StringComparer.Ordinal));
            RecomputeGarrison(data, s);
            s.fateCommitted = first;
            return s;
        }

        // ───────────────────────── settle ─────────────────────────

        sealed class Token
        {
            public string Name, Label;
            public int Importance;
            public long Seq;
        }

        sealed class Proposal
        {
            public CharacterState Character;
            public FateOption Option, Default;
            public int Score;
            public string Cause;
            public bool IsDefault, Arbitrated, FromCommit;
            public CharacterTrace Trace;
        }

        /// <summary>
        /// Settle on entering <paramref name="nodeId"/> (spec §5.2 steps ①–⑦). Step ⑧ (text) is P1.
        /// The section's stageId is the stage that just ended; the log holds that stage's events.
        /// </summary>
        public static SettleResult Settle(FateDataSet data, FateSaveSection state, StageEventLog log, string nodeId, IConditionContext ctx)
        {
            ctx = ctx ?? EmptyConditionContext.Instance;
            var r = new SettleResult();
            var s = state.Clone();
            var t = data.Tuning;
            string prev = s.world.stageId;
            ulong settleSeed = FateStreams.SettleSeed(FateStreams.ParseSeed(s.world.fateSeed), nodeId);
            var tokens = new SortedDictionary<string, SortedDictionary<string, Token>>(StringComparer.Ordinal);

            // ① stage settlement
            ReplayLog(data, s, log, r);
            MergeDeathsAndRescues(data, s, prev, nodeId, r);
            ApplyHistoricalPosts(data, s, nodeId);
            DistributeHistory(data, s, nodeId, tokens);
            DrawRandom(data, s, nodeId, settleSeed, tokens);

            // ② memory upkeep
            int curIndex = data.NodeIndex(nodeId);
            foreach (var c in s.characters.Values)
                MemoryStore.Compress(c, t.memoryCap, data.NodeIndex, curIndex);

            s.world.stageId = nodeId;
            s.world.pendingRecompute = false;
            s.world.pendingTargetNode = null;
            foreach (var c in s.characters.Values) c.presentAt.Clear();
            CollectPlayerAndRelationTokens(s, t, tokens);

            // ③–⑤ candidates, scoring, two-step handling
            var proposals = new List<Proposal>();
            ExpireCooldowns(data, s, nodeId);
            // Snapshot: anchor checks swap hypothetical states into the map while we iterate.
            foreach (var c in new List<CharacterState>(s.characters.Values))
            {
                if (c.id == s.world.protagonistId || !c.IsAlive) continue;
                var route = data.RouteFor(c);
                if (route == null || !data.IsActive(route, nodeId)) continue;
                var trace = new CharacterTrace { CharacterId = c.id, Tier = c.tier };
                r.Trace.Add(trace);
                var def = data.Option(route, route.DefaultOptionFor(nodeId));
                if (def == null)
                {
                    r.Errors.Add($"{c.id}: no default option for {nodeId}");
                    trace.Note = "no default";
                    continue;
                }
                var charTokens = tokens.TryGetValue(c.id, out var tk) ? tk : new SortedDictionary<string, Token>(StringComparer.Ordinal);
                trace.Tokens.AddRange(charTokens.Keys);

                var pending = FindPending(s, c.id, nodeId);
                if (pending != null)
                {
                    proposals.Add(HandleCommit(data, s, c, route, def, pending, nodeId, ctx, trace, r));
                    continue;
                }
                proposals.Add(ScoreAndDraw(data, s, c, route, def, nodeId, ctx, settleSeed, charTokens, trace, r));
            }

            // ⑥ arbitration
            Arbitrate(data, s, proposals, nodeId);

            // ⑦ commit
            Commit(data, s, proposals, nodeId, ctx, r);
            r.NewFateSection = s;
            s.fateCommitted = nodeId;
            return r;
        }

        // ───────────── ① helpers ─────────────

        static void ReplayLog(FateDataSet data, FateSaveSection s, StageEventLog log, SettleResult r)
        {
            if (log == null) return;
            var events = new List<StageEvent>(log.Events);
            events.Sort((a, b) => a.Seq.CompareTo(b.Seq));
            foreach (var e in events)
            {
                if (e.Seq > s.world.seqCounter) s.world.seqCounter = e.Seq;
                switch (e.Type)
                {
                    case EventTypes.Memory:
                    {
                        var m = e.Memory?.Clone();
                        var who = s.Character(m?.who);
                        if (who == null) { r.Errors.Add("memory for unknown character " + m?.who); break; }
                        m.seq = e.Seq;
                        if (string.IsNullOrEmpty(m.id)) m.id = MemoryId(e.Seq);
                        if (string.IsNullOrEmpty(m.node)) m.node = s.world.stageId;
                        MemoryStore.Put(who, m);
                        if (m.kind == MemoryKinds.Player)
                        {
                            var rel = EnsureRelation(who, s.world.protagonistId);
                            Attitude.Apply(rel, Attitude.ApplyInteraction(data.Profile(who.id), rel, m, data.Tuning));
                        }
                        break;
                    }
                    case EventTypes.Recruit:
                    {
                        var c = s.Character(e.CharacterId);
                        if (c == null) { r.Errors.Add("recruit for unknown character " + e.CharacterId); break; }
                        c.recruitState = e.RecruitState;
                        c.scriptedParty = e.Scripted && e.RecruitState == RecruitStates.InParty;
                        break;
                    }
                    case EventTypes.Gift:
                    {
                        var c = s.Character(e.CharacterId);
                        if (c == null) { r.Errors.Add("gift for unknown character " + e.CharacterId); break; }
                        c.heldItems.Add(new HeldItem { itemId = e.ItemId, origin = "player_given" });
                        break;
                    }
                    case EventTypes.Rescue:
                        if (e.CaseId != null) s.world.rescueResults[e.CaseId] = e.Result;
                        break;
                    case EventTypes.Death:
                    {
                        var c = s.Character(e.CharacterId);
                        if (c != null) c.pendingDeath = true;
                        break;
                    }
                }
            }
        }

        static void MergeDeathsAndRescues(FateDataSet data, FateSaveSection s, string prev, string nodeId, SettleResult r)
        {
            // Rescue cases of the stage that just ended.
            var rescuedIds = new SortedDictionary<string, RescueCase>(StringComparer.Ordinal);
            foreach (var rc in data.RescuesAt(prev))
            {
                var c = s.Character(rc.characterId);
                if (c == null) continue;
                bool success = s.world.rescueResults.TryGetValue(rc.id, out var res) && res == "success";
                c.rescues[rc.id] = success ? "success" : "fail";
                if (!success)
                {
                    s.world.rescueResults[rc.id] = "fail";
                    foreach (var b in rc.onFail.memoryBroadcast)
                    {
                        var who = s.Character(b.who);
                        if (who == null || !who.IsAlive) continue;
                        MemoryStore.Put(who, NewMemory(s, b.who, b.key, MemoryKinds.History, prev, b.importance, "mem." + b.key));
                    }
                    continue;
                }
                rescuedIds[c.id] = rc;
                string fromTier = c.tier;
                c.tier = rc.onSuccess.tierAfter ?? Tiers.T1S;
                c.routeId = rc.onSuccess.afterRouteId;
                c.pendingDeath = false;
                r.Changes.Add(new FateChange { Kind = ChangeKinds.Tier, CharacterId = c.id, From = fromTier, To = c.tier });
            }

            // dies anchors of the stage that just ended, plus deaths the story flagged (pendingDeath).
            foreach (var c in s.characters.Values)
            {
                if (!c.alive) continue;
                bool dies = c.pendingDeath;
                var route = data.MainRoute(c.id);
                if (route != null && !rescuedIds.ContainsKey(c.id))
                    foreach (var a in route.anchors)
                        if (a.IsDies && a.stage == prev) dies = true;
                if (!dies) continue;
                Kill(s, c, r);
            }

            // Power transfers of earlier successful rescues fall due at their atNode.
            foreach (var rc in data.Raw.rescues)
            {
                var pt = rc.onSuccess?.powerTransfer;
                if (pt == null || pt.atNode != nodeId) continue;
                if (!(s.world.rescueResults.TryGetValue(rc.id, out var res) && res == "success")) continue;
                SetPost(s, pt.position, pt.successor, r);
            }
        }

        static void Kill(FateSaveSection s, CharacterState c, SettleResult r)
        {
            c.alive = false;
            c.pendingDeath = false;
            c.presentAt.Clear();
            r.Changes.Add(new FateChange { Kind = ChangeKinds.Death, CharacterId = c.id });
            var vacated = new List<string>();
            foreach (var kv in s.world.slots.posts) if (kv.Value == c.id) vacated.Add(kv.Key);
            foreach (var p in vacated) s.world.slots.posts.Remove(p);
            ReturnGivenItems(c, r);
        }

        static void SetPost(FateSaveSection s, string post, string holder, SettleResult r)
        {
            s.world.slots.posts.TryGetValue(post, out var old);
            if (old == holder) return;
            s.world.slots.posts[post] = holder;
            r?.Changes.Add(new FateChange { Kind = ChangeKinds.Post, CharacterId = holder, ItemId = post, From = old, To = holder });
        }

        internal static void ApplyHistoricalPosts(FateDataSet data, FateSaveSection s, string nodeId)
        {
            foreach (var p in data.Raw.worldSlots?.posts ?? Array.Empty<PostDef>())
                if (p.historical != null && p.historical.TryGetValue(nodeId, out var holder))
                    s.world.slots.posts[p.id] = holder;
        }

        static void DistributeHistory(FateDataSet data, FateSaveSection s, string nodeId, SortedDictionary<string, SortedDictionary<string, Token>> tokens)
        {
            int target = data.NodeIndex(nodeId);
            var events = new List<HistoryEventDef>(data.Raw.historyEvents);
            events.Sort((a, b) =>
            {
                int c = data.NodeIndex(a.stage).CompareTo(data.NodeIndex(b.stage));
                return c != 0 ? c : string.CompareOrdinal(a.id, b.id);
            });
            foreach (var e in events)
            {
                int due = data.NodeIndex(e.stage);
                if (due < 0 || due + e.delayStages > target || s.world.HistoryAlreadyDelivered(e.id)) continue;
                foreach (var c in s.characters.Values)
                {
                    if (c.id == s.world.protagonistId || !c.IsAlive) continue;
                    var route = data.RouteFor(c);
                    if (route == null || !data.IsActive(route, nodeId) || !InScope(data, c, e.scope)) continue;
                    var spec = e.memory ?? new EventMemorySpec();
                    var m = NewMemory(s, c.id, spec.key ?? "h." + e.id, MemoryKinds.History, e.stage, spec.importance, spec.templateId ?? "mem.h." + e.id);
                    MemoryStore.Put(c, m);
                    if (e.causesDeviation) AddToken(tokens, c.id, "HISTORY:" + e.id, "HISTORY:" + m.id, m.importance, m.seq);
                }
                if (!s.world.historyDelivered.TryGetValue(nodeId, out var list)) s.world.historyDelivered[nodeId] = list = new List<string>();
                list.Add(e.id);
            }
        }

        static bool InScope(FateDataSet data, CharacterState c, string scope)
        {
            if (string.IsNullOrEmpty(scope) || scope == "world") return true;
            if (scope.StartsWith("region:")) return data.City(c.location)?.region == scope.Substring(7);
            if (scope.StartsWith("faction:")) return c.faction == scope.Substring(8);
            if (scope.StartsWith("explicit:")) return Array.IndexOf(scope.Substring(9).Split(','), c.id) >= 0;
            return false;
        }

        static void DrawRandom(FateDataSet data, FateSaveSection s, string nodeId, ulong settleSeed, SortedDictionary<string, SortedDictionary<string, Token>> tokens)
        {
            foreach (var c in s.characters.Values)
            {
                if (c.id == s.world.protagonistId || !c.IsAlive) continue;
                var route = data.RouteFor(c);
                if (route == null || !data.IsActive(route, nodeId) || route.randomEvents == null || route.randomEvents.Length == 0) continue;
                var rng = FateStreams.Stream(settleSeed, "random", c.id);
                long roll = rng.NextBelow(FixedMath.One);
                if (roll >= data.Tuning.RandomRateFor(c.tier)) continue;
                var eligible = new List<RandomEventDef>();
                var ids = (string[])route.randomEvents.Clone();
                Array.Sort(ids, StringComparer.Ordinal);
                foreach (var id in ids)
                {
                    var def = data.RandomEvent(id);
                    if (def != null && (def.stages == null || def.stages.Length == 0 || Array.IndexOf(def.stages, nodeId) >= 0)) eligible.Add(def);
                }
                if (eligible.Count == 0) continue;
                var ev = eligible[(int)rng.NextBelow(eligible.Count)];
                var spec = ev.memory ?? new EventMemorySpec();
                var m = NewMemory(s, c.id, spec.key ?? "r." + ev.id, MemoryKinds.Random, nodeId, spec.importance, spec.templateId ?? "mem.r." + ev.id);
                MemoryStore.Put(c, m);
                if (ev.causesDeviation) AddToken(tokens, c.id, "RANDOM:" + ev.id, "RANDOM:" + m.id, m.importance, m.seq);
            }
        }

        static void CollectPlayerAndRelationTokens(FateSaveSection s, TuningTable t, SortedDictionary<string, SortedDictionary<string, Token>> tokens)
        {
            foreach (var c in s.characters.Values)
            {
                foreach (var m in c.memories)
                {
                    if (m.kind != MemoryKinds.Player || m.importance < 3) continue;
                    string label = "PLAYER:" + m.id;
                    foreach (var tag in m.tags ?? Array.Empty<string>()) AddToken(tokens, c.id, "PLAYER:" + tag, label, m.importance, m.seq);
                    if (m.valence > 0) AddToken(tokens, c.id, "PLAYER:+", label, m.importance, m.seq);
                    if (m.valence < 0) AddToken(tokens, c.id, "PLAYER:-", label, m.importance, m.seq);
                }
                foreach (var kv in c.relations)
                {
                    int rise = kv.Value.favor - (kv.Value.baseline?.favor ?? 0);
                    if (rise >= t.relationCauseThreshold) AddToken(tokens, c.id, "RELATION:" + kv.Key, "RELATION:" + kv.Key, 0, 0);
                }
            }
        }

        static void AddToken(SortedDictionary<string, SortedDictionary<string, Token>> tokens, string who, string name, string label, int importance, long seq)
        {
            if (!tokens.TryGetValue(who, out var map)) tokens[who] = map = new SortedDictionary<string, Token>(StringComparer.Ordinal);
            if (map.TryGetValue(name, out var old) && (old.Importance > importance || (old.Importance == importance && old.Seq >= seq))) return;
            map[name] = new Token { Name = name, Label = label, Importance = importance, Seq = seq };
        }

        // ───────────── ③–⑤ ─────────────

        static void ExpireCooldowns(FateDataSet data, FateSaveSection s, string nodeId)
        {
            int cur = data.NodeIndex(nodeId);
            s.world.pendingTransitions.RemoveAll(p => p.revoked && data.NodeIndex(p.cooldownUntil) < cur);
        }

        static PendingTransition FindPending(FateSaveSection s, string id, string nodeId)
        {
            foreach (var p in s.world.pendingTransitions)
                if (!p.revoked && p.characterId == id && p.commitStage == nodeId) return p;
            return null;
        }

        static bool InCooldown(FateDataSet data, FateSaveSection s, string charId, string optionId, string nodeId)
        {
            foreach (var p in s.world.pendingTransitions)
                if (p.revoked && p.characterId == charId && p.optionId == optionId && data.NodeIndex(nodeId) <= data.NodeIndex(p.cooldownUntil))
                    return true;
            return false;
        }

        static Proposal HandleCommit(FateDataSet data, FateSaveSection s, CharacterState c, FateRoute route, FateOption def,
            PendingTransition p, string nodeId, IConditionContext ctx, CharacterTrace trace, SettleResult r)
        {
            s.world.pendingTransitions.Remove(p);
            var opt = data.Option(route, p.optionId);
            bool ok = opt != null
                      && ConditionEvaluator.Eval(opt.preconditions, s, ctx, data)
                      && PassesHardAnchors(data, s, c, route, opt, nodeId, ctx);
            trace.Proposed = p.optionId;
            if (ok)
            {
                trace.Committed = true;
                trace.Cause = p.cause;
                return new Proposal { Character = c, Option = opt, Default = def, Cause = p.cause, FromCommit = true, Trace = trace };
            }
            // Revoke: "gave up the idea", cooldown through the stage after the commit stage.
            trace.Revoked = true;
            p.revoked = true;
            p.cooldownUntil = data.NextNode(nodeId) ?? nodeId;
            s.world.pendingTransitions.Add(p);
            MemoryStore.Put(c, NewMemory(s, c.id, "r.revoke." + p.optionId, MemoryKinds.Random, nodeId, 2, "mem.revoke"));
            return new Proposal { Character = c, Option = def, Default = def, Cause = DefaultCause, IsDefault = true, Trace = trace };
        }

        static Proposal ScoreAndDraw(FateDataSet data, FateSaveSection s, CharacterState c, FateRoute route, FateOption def, string nodeId,
            IConditionContext ctx, ulong settleSeed, SortedDictionary<string, Token> charTokens, CharacterTrace trace, SettleResult r)
        {
            var t = data.Tuning;
            var profile = data.Profile(c.id);
            var candidates = new List<FateOption>();
            var causes = new SortedDictionary<string, string>(StringComparer.Ordinal);
            bool inPartyScope = c.recruitState == RecruitStates.InParty && c.scriptedParty;
            foreach (var o in data.OptionsFor(route, nodeId))
            {
                bool isDefault = o.id == def.id;
                var ct = new CandidateTrace { OptionId = o.id, IsDefault = isDefault };
                trace.Candidates.Add(ct);
                if (c.tier == Tiers.T0 && !isDefault) { ct.Status = "filtered:T0"; continue; }
                if (!isDefault && inPartyScope && !o.twoStep) { ct.Status = "filtered:inParty"; continue; }
                if (!PassesHardAnchors(data, s, c, route, o, nodeId, ctx)) { ct.Status = "filtered:anchor"; if (!isDefault) continue; r.Errors.Add($"{c.id}: default {o.id} breaks a HARD anchor at {nodeId}"); }
                if (!isDefault && !ConditionEvaluator.Eval(o.preconditions, s, ctx, data)) { ct.Status = "filtered:preconditions"; continue; }
                if (!isDefault)
                {
                    var hit = MatchCause(o, charTokens);
                    if (hit == null) { ct.Status = "filtered:cause"; continue; }
                    if (o.twoStep && (InCooldown(data, s, c.id, o.id, nodeId) || !o.AppliesTo(data.NextNode(nodeId) ?? "")))
                    { ct.Status = "filtered:twoStep"; continue; }
                    causes[o.id] = hit.Label;
                    ct.Cause = hit.Label;
                }
                if (ct.Status == null) ct.Status = "scored";
                candidates.Add(o);
            }
            if (!candidates.Contains(def)) candidates.Insert(0, def);

            // ④ scoring and draw
            var scores = new int[candidates.Count];
            var eps = FateStreams.Stream(settleSeed, "eps", c.id);
            for (int i = 0; i < candidates.Count; i++)
            {
                scores[i] = c.tier == Tiers.T0 ? 0 : Score(data, s, c, profile, candidates[i], candidates[i].id == def.id, eps);
                Find(trace, candidates[i].id).Score = scores[i];
            }
            int pick = 0;
            if (candidates.Count > 1)
            {
                var weights = SoftmaxWeights(scores, t.tau);
                long total = 0;
                foreach (var w in weights) total += w;
                for (int i = 0; i < candidates.Count; i++) Find(trace, candidates[i].id).Weight = weights[i];
                var draw = FateStreams.Stream(settleSeed, "draw", c.id);
                long roll = draw.NextBelow(total);
                long acc = 0;
                for (int i = 0; i < weights.Length; i++)
                {
                    acc += weights[i];
                    if (roll < acc) { pick = i; break; }
                }
            }
            var chosen = candidates[pick];
            trace.Proposed = chosen.id;
            var prop = new Proposal { Character = c, Default = def, Trace = trace };

            // ⑤ two-step: the first win only announces; this stage follows the default.
            if (chosen.id != def.id && chosen.twoStep)
            {
                var pt = new PendingTransition
                {
                    characterId = c.id, optionId = chosen.id, announcedStage = nodeId, commitStage = data.NextNode(nodeId),
                    cause = causes[chosen.id], warningSceneId = chosen.warningSceneId
                };
                s.world.pendingTransitions.Add(pt);
                r.Announced.Add(new AnnouncedTransition
                {
                    CharacterId = c.id, OptionId = chosen.id, Cause = pt.cause, AnnouncedStage = nodeId,
                    CommitStage = pt.commitStage, WarningSceneId = chosen.warningSceneId
                });
                trace.Announced = true;
                chosen = def;
            }
            prop.Option = chosen;
            prop.IsDefault = chosen.id == def.id;
            prop.Score = scores[candidates.IndexOf(chosen)];
            prop.Cause = prop.IsDefault ? DefaultCause : causes[chosen.id];
            return prop;
        }

        static CandidateTrace Find(CharacterTrace trace, string id)
        {
            foreach (var ct in trace.Candidates) if (ct.OptionId == id) return ct;
            var n = new CandidateTrace { OptionId = id, Status = "scored" };
            trace.Candidates.Add(n);
            return n;
        }

        static Token MatchCause(FateOption o, SortedDictionary<string, Token> charTokens)
        {
            Token best = null;
            foreach (var need in o.requiresCause ?? Array.Empty<string>())
            {
                if (!charTokens.TryGetValue(need, out var tk)) continue;
                if (best == null || tk.Importance > best.Importance || (tk.Importance == best.Importance && tk.Seq > best.Seq)) best = tk;
            }
            return best;
        }

        /// <summary>score = wP·fitP + wA·fitA + wR·pull + wI·prox·infl + H·[default] + ε (spec §5.3), Q16.16.</summary>
        public static int Score(FateDataSet data, FateSaveSection s, CharacterState c, CharacterProfile profile, FateOption o, bool isDefault, Pcg32 eps)
        {
            var t = data.Tuning;
            long fitP = FitP(profile?.personality, o.tags);
            long fitA = 0;
            if (profile?.ambitions != null && o.ambitionsServed != null)
                foreach (var a in profile.ambitions)
                    if (Array.IndexOf(o.ambitionsServed, a.id) >= 0) fitA += (long)a.weight * FixedMath.One / 1000;
            long pull = Pull(s, c, o);
            var toP = c.RelationTo(s.world.protagonistId);
            long infl = toP == null ? 0 : FixedMath.Clamp((long)(toP.favor + toP.trust) * FixedMath.One / 200, -FixedMath.One, FixedMath.One);
            long score = FixedMath.Mul(t.wP, fitP) + FixedMath.Mul(t.wA, fitA) + FixedMath.Mul(t.wR, pull)
                         + FixedMath.Mul(t.wI, o.playerProximity * infl);
            if (isDefault) score += t.HFor(c.tier);
            if (eps != null && t.eps > 0) score += (long)(eps.NextBelow(2L * t.eps + 1)) - t.eps;
            return (int)score;
        }

        public static long FitP(Personality personality, Personality tags)
        {
            if (personality == null || tags == null) return FixedMath.Half;
            var p = personality.ToArray();
            var v = tags.ToArray();
            long dot = 0, tt = 0, pp = 0;
            for (int k = 0; k < 8; k++)
            {
                long pk = (long)(p[k] - 50) * FixedMath.One / 50;
                dot += v[k] * pk;
                tt += (long)v[k] * v[k];
                pp += pk * pk;
            }
            long denom = FixedMath.Isqrt(tt) * FixedMath.Isqrt(pp);
            long cos = denom == 0 ? 0 : dot * FixedMath.One / denom;
            return (cos + FixedMath.One) >> 1;
        }

        static long Pull(FateSaveSection s, CharacterState c, FateOption o)
        {
            string faction = o.effect?.faction, location = o.effect?.location;
            if (faction == null && location == null) faction = c.faction;
            int best = int.MinValue;
            foreach (var other in s.characters.Values)
            {
                if (other.id == c.id || !other.IsAlive) continue;
                bool there = (faction != null && other.faction == faction) || (location != null && other.location == location);
                if (!there) continue;
                var rel = c.RelationTo(other.id);
                if (rel != null && rel.favor > best) best = rel.favor;
            }
            return best == int.MinValue ? 0 : (long)best * FixedMath.One / 100;
        }

        /// <summary>Fixed-point softmax weights via the 4097-entry EXP table with linear interpolation (spec §5.3).</summary>
        public static int[] SoftmaxWeights(int[] scores, int tau)
        {
            int max = int.MinValue;
            foreach (var sc in scores) if (sc > max) max = sc;
            var w = new int[scores.Length];
            var table = ExpTable.Values;
            for (int i = 0; i < scores.Length; i++)
            {
                long d = (long)scores[i] - max; // ≤ 0
                long x = (-d) * 256L * FixedMath.One / tau; // Q16.16 index
                long idx = x >> 16;
                if (idx >= ExpTable.Size - 1) { w[i] = table[ExpTable.Size - 1]; continue; }
                long frac = x & 0xFFFF;
                long a = table[idx], b = table[idx + 1];
                w[i] = (int)(a + (((b - a) * frac) >> 16));
            }
            return w;
        }

        // ───────────── ⑥ ─────────────

        static void Arbitrate(FateDataSet data, FateSaveSection s, List<Proposal> proposals, string nodeId)
        {
            var vol = data.Volume(s.world.volume);
            int present = 0;
            foreach (var c in s.characters.Values)
            {
                if (!c.IsAlive || !Budgeted(c)) continue;
                if (vol != null && vol.openRegions.Length > 0 && Array.IndexOf(vol.openRegions, data.City(c.location)?.region) < 0) continue;
                present++;
            }
            int max = FixedMath.CeilMulRatio(present, data.Tuning.deviationBudgetRatio);
            int used = 0;
            var takenPosts = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var cityCount = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (var c in s.characters.Values)
                if (c.IsAlive && c.location != null) cityCount[c.location] = (cityCount.TryGetValue(c.location, out var n) ? n : 0) + 1;
            var factionMoves = new List<KeyValuePair<string, string>>(); // from → to of committed deviations

            // Commits announced last stage are honoured first and only consume budget.
            foreach (var p in proposals)
                if (p.FromCommit && !Allowed(data, s, p, nodeId, takenPosts, cityCount, factionMoves, out _)) Revert(p, "constraint");
            foreach (var p in proposals)
                if (p.FromCommit && !p.IsDefault) { if (Budgeted(p.Character)) used++; Reserve(s, p, takenPosts, cityCount, factionMoves); }

            var order = proposals.FindAll(p => !p.FromCommit && !p.IsDefault);
            order.Sort((a, b) =>
            {
                int c = Tiers.Weight(b.Character.tier).CompareTo(Tiers.Weight(a.Character.tier));
                if (c != 0) return c;
                c = b.Score.CompareTo(a.Score);
                return c != 0 ? c : string.CompareOrdinal(a.Character.id, b.Character.id);
            });
            foreach (var p in order)
            {
                bool budgeted = Budgeted(p.Character);
                if (budgeted && used >= max) { Revert(p, "budget"); continue; }
                if (!Allowed(data, s, p, nodeId, takenPosts, cityCount, factionMoves, out var why)) { Revert(p, why); continue; }
                if (budgeted) used++;
                Reserve(s, p, takenPosts, cityCount, factionMoves);
            }
            s.world.deviationBudget = new DeviationBudget { stage = nodeId, max = max, used = used };
        }

        /// <summary>Only T1/T2 (and T1S survivors) deviations count against the budget; T3 originals are governed by data.</summary>
        static bool Budgeted(CharacterState c) => c.tier == Tiers.T1 || c.tier == Tiers.T2 || c.tier == Tiers.T1S;

        static void Revert(Proposal p, string why)
        {
            p.Option = p.Default;
            p.IsDefault = true;
            p.Arbitrated = true;
            p.Cause = DefaultCause;
            p.FromCommit = false;
            p.Trace.Arbitrated = true;
            p.Trace.Note = why;
        }

        static void Reserve(FateSaveSection s, Proposal p, SortedDictionary<string, string> takenPosts, SortedDictionary<string, int> cityCount, List<KeyValuePair<string, string>> moves)
        {
            var e = p.Option.effect;
            if (e == null) return;
            if (e.post != null) takenPosts[e.post] = p.Character.id;
            if (e.location != null && e.location != p.Character.location)
            {
                cityCount[e.location] = (cityCount.TryGetValue(e.location, out var n) ? n : 0) + 1;
                if (p.Character.location != null && cityCount.TryGetValue(p.Character.location, out var m)) cityCount[p.Character.location] = m - 1;
            }
            if (e.faction != null && e.faction != p.Character.faction) moves.Add(new KeyValuePair<string, string>(p.Character.faction, e.faction));
        }

        static bool Allowed(FateDataSet data, FateSaveSection s, Proposal p, string nodeId, SortedDictionary<string, string> takenPosts,
            SortedDictionary<string, int> cityCount, List<KeyValuePair<string, string>> moves, out string why)
        {
            var c = p.Character;
            var e = p.Option.effect ?? new FateEffect();
            why = null;
            // unique posts
            if (e.post != null)
            {
                bool unique = true;
                foreach (var pd in data.Raw.worldSlots.posts) if (pd.id == e.post) unique = pd.unique;
                if (unique && (takenPosts.ContainsKey(e.post) || (s.world.slots.posts.TryGetValue(e.post, out var h) && h != c.id))) { why = "post"; return false; }
            }
            // garrison caps
            if (e.location != null && e.location != c.location)
            {
                var city = data.City(e.location);
                int there = cityCount.TryGetValue(e.location, out var n) ? n : 0;
                if (city != null && city.garrisonCap > 0 && there + 1 > city.garrisonCap) { why = "garrison"; return false; }
            }
            // faction floors
            bool leavesFaction = e.dies || (e.faction != null && e.faction != c.faction) || e.recruitState == RecruitStates.Departed;
            foreach (var f in data.Raw.worldSlots.factionFloors)
            {
                if (Array.IndexOf(f.mustKeep, c.id) < 0 || !leavesFaction) continue;
                if (data.NodeIndex(nodeId) <= data.NodeIndex(f.until)) { why = "factionFloor"; return false; }
            }
            // survivor constraints (T1S)
            if (c.tier == Tiers.T1S)
            {
                foreach (var rc in data.Raw.rescues)
                {
                    if (rc.characterId != c.id) continue;
                    var sc = rc.onSuccess.survivorConstraints;
                    if (sc == null) continue;
                    if (e.faction != null && sc.factionsAllowed.Length > 0 && Array.IndexOf(sc.factionsAllowed, e.faction) < 0) { why = "survivor"; return false; }
                    if (e.post != null && Array.IndexOf(sc.cannotHoldPosts, e.post) >= 0) { why = "survivor"; return false; }
                    foreach (var scene in e.presentAt) if (Array.IndexOf(sc.mustNotBePresentAt, scene) >= 0) { why = "survivor"; return false; }
                }
            }
            // mutual defection: an earlier commit went to.from where this one goes from.to
            if (e.faction != null && e.faction != c.faction)
                foreach (var mv in moves)
                    if (mv.Key == e.faction && mv.Value == c.faction) { why = "mutual"; return false; }
            return true;
        }

        // ───────────── ⑦ ─────────────

        static void Commit(FateDataSet data, FateSaveSection s, List<Proposal> proposals, string nodeId, IConditionContext ctx, SettleResult r)
        {
            var snapshots = new SortedDictionary<string, CharacterState>(StringComparer.Ordinal);
            foreach (var p in proposals)
            {
                snapshots[p.Character.id] = p.Character.Clone();
                ApplyEffect(s, p.Character, p.Option, r);
            }
            // Re-verify HARD anchors on the committed state; fall back to the default on failure (data error).
            foreach (var p in proposals)
            {
                var route = data.RouteFor(p.Character);
                if (AnchorsHold(data, s, route, nodeId, ctx) || p.IsDefault) continue;
                r.Errors.Add($"{p.Character.id}: {p.Option.id} failed HARD anchor re-check at {nodeId}; reverted to default");
                var restored = snapshots[p.Character.id];
                s.characters[p.Character.id] = restored;
                p.Character = restored;
                p.Option = p.Default;
                p.IsDefault = true;
                p.Cause = DefaultCause;
                ApplyEffect(s, restored, p.Default, r);
            }
            foreach (var p in proposals)
            {
                p.Trace.Chosen = p.Option.id;
                if (p.Trace.Cause == null) p.Trace.Cause = p.Cause;
                p.Character.fateHistory.Add(new FateHistoryEntry
                {
                    stage = nodeId, optionId = p.Option.id, cause = p.Cause ?? DefaultCause, score = p.Score,
                    @default = p.IsDefault, arbitrated = p.Arbitrated
                });
            }
            RecomputeGarrison(data, s);
            foreach (var pd in data.Raw.worldSlots?.posts ?? Array.Empty<PostDef>())
            {
                if (pd.historical == null || !pd.historical.TryGetValue(nodeId, out var want)) continue;
                s.world.slots.posts.TryGetValue(pd.id, out var have);
                if (have != want) r.Errors.Add($"post {pd.id} held by {have ?? "nobody"} at {nodeId}; history says {want}");
            }
        }

        internal static bool AnchorsHold(FateDataSet data, FateSaveSection s, FateRoute route, string nodeId, IConditionContext ctx)
        {
            if (route == null) return true;
            foreach (var a in route.anchors)
                if (a.IsHard && !a.IsDies && a.stage == nodeId && !ConditionEvaluator.Eval(a.state, s, ctx, data))
                    return false;
            return true;
        }

        static bool PassesHardAnchors(FateDataSet data, FateSaveSection s, CharacterState c, FateRoute route, FateOption o, string nodeId, IConditionContext ctx)
        {
            bool any = false;
            foreach (var a in route.anchors) if (a.IsHard && !a.IsDies && a.stage == nodeId) any = true;
            if (!any) return true;
            var hypothetical = c.Clone();
            ApplyEffect(s, hypothetical, o, null);
            s.characters[c.id] = hypothetical;
            try { return AnchorsHold(data, s, route, nodeId, ctx); }
            finally { s.characters[c.id] = c; }
        }

        /// <summary>Apply an option's effect; null fields mean "unchanged". Changes are reported when r is given.</summary>
        internal static void ApplyEffect(FateSaveSection s, CharacterState c, FateOption o, SettleResult r)
        {
            var e = o?.effect;
            if (e == null) return;
            if (e.faction != null && e.faction != c.faction) { r?.Changes.Add(new FateChange { Kind = ChangeKinds.Faction, CharacterId = c.id, From = c.faction, To = e.faction }); c.faction = e.faction; }
            if (e.location != null && e.location != c.location) { r?.Changes.Add(new FateChange { Kind = ChangeKinds.Location, CharacterId = c.id, From = c.location, To = e.location }); c.location = e.location; }
            if (e.recruitState != null && e.recruitState != c.recruitState)
            {
                r?.Changes.Add(new FateChange { Kind = ChangeKinds.RecruitState, CharacterId = c.id, From = c.recruitState, To = e.recruitState });
                c.recruitState = e.recruitState;
                if (e.recruitState != RecruitStates.InParty) c.scriptedParty = false;
                if (e.recruitState == RecruitStates.Departed && r != null) ReturnGivenItems(c, r);
            }
            if (e.role != null) c.role = e.role;
            c.presentAt.Clear();
            if (e.presentAt != null) c.presentAt.AddRange(e.presentAt);
            if (e.relationTags != null)
                foreach (var kv in e.relationTags)
                {
                    var rel = EnsureRelation(c, kv.Key);
                    foreach (var tag in kv.Value) if (!rel.tags.Contains(tag)) rel.tags.Add(tag);
                }
            if (e.post != null) SetPost(s, e.post, c.id, r);
            if (e.dies)
            {
                if (r != null) Kill(s, c, r);
                else { c.alive = false; c.presentAt.Clear(); }
            }
        }

        static void ReturnGivenItems(CharacterState c, SettleResult r)
        {
            var keep = new List<HeldItem>();
            foreach (var h in c.heldItems)
            {
                if (h.origin == "player_given") r.Changes.Add(new FateChange { Kind = ChangeKinds.ReturnItem, CharacterId = c.id, ItemId = h.itemId });
                else keep.Add(h);
            }
            c.heldItems = keep;
        }

        static void RecomputeGarrison(FateDataSet data, FateSaveSection s)
        {
            s.world.slots.garrison.Clear();
            foreach (var city in data.Raw.worldSlots?.cities ?? Array.Empty<CityDef>())
            {
                var list = new List<string>();
                foreach (var c in s.characters.Values) if (c.IsAlive && c.location == city.id) list.Add(c.id);
                s.world.slots.garrison[city.id] = list;
            }
        }

        // ───────────── misc ─────────────

        static Relation EnsureRelation(CharacterState c, string target)
        {
            if (!c.relations.TryGetValue(target, out var rel)) c.relations[target] = rel = new Relation();
            return rel;
        }

        public static string MemoryId(long seq) => "m_" + seq.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);

        static MemoryEntry NewMemory(FateSaveSection s, string who, string key, string kind, string node, int importance, string templateId)
        {
            long seq = ++s.world.seqCounter;
            return new MemoryEntry
            {
                id = MemoryId(seq), who = who, key = key, kind = kind, node = node, seq = seq,
                importance = importance, intensity = 1, templateId = templateId, text = ""
            };
        }

        /// <summary>The proposal for a character as the panel/simulator see it, after settle.</summary>
        public static string ChosenOption(SettleResult r, string characterId)
        {
            foreach (var t in r.Trace) if (t.CharacterId == characterId) return t.Chosen;
            return null;
        }
    }
}
