using System;
using System.Collections.Generic;
using System.Text;

namespace ThreeKingdoms.Core.Fate
{
    public enum SimPolicy
    {
        /// <summary>The player never interacts; rescue conditions are never met.</summary>
        NoInteraction,
        /// <summary>Every active character gets a strong positive interaction each stage; flags and objectives are met.</summary>
        MaxInteraction
    }

    /// <summary>Gameplay facts the simulator pretends exist.</summary>
    public sealed class SimConditionContext : IConditionContext
    {
        readonly bool all;
        public SimConditionContext(SimPolicy policy) { all = policy == SimPolicy.MaxInteraction; }
        public bool Flag(string name) => all;
        public string FlagValue(string name) => null;
        public bool BattleObjective(string id) => all;
        public bool HasItem(string itemId) => all;
        public int Reputation => all ? 100 : 0;
        public bool FactionDestroyed(string factionId) => false;
        public bool EventSeen(string eventId) => all;
    }

    [Serializable]
    public sealed class DeviationStat
    {
        public string CharacterId, Stage;
        public int Runs, Deviations;
        public int PerMille => Runs == 0 ? 0 : (int)((long)Deviations * 1000 / Runs);
    }

    [Serializable]
    public sealed class SimPolicyReport
    {
        public SimPolicy Policy;
        public int Runs, Settles, AnchorViolations, BudgetOverruns, HistoricalMismatches, EngineErrors;
        public List<DeviationStat> Deviations = new List<DeviationStat>();
        public SortedDictionary<string, int> OptionCounts = new SortedDictionary<string, int>(StringComparer.Ordinal); // "char|stage|option"
        public SortedDictionary<string, int> ReachedAvailable = new SortedDictionary<string, int>(StringComparer.Ordinal);
        public SortedDictionary<string, int> RescueSuccesses = new SortedDictionary<string, int>(StringComparer.Ordinal);
        public List<string> SampleErrors = new List<string>();
        public long ElapsedMs;

        /// <summary>Max over (character, stage) of the deviation rate, per mille.</summary>
        public int MaxDeviationPerMille
        {
            get
            {
                int m = 0;
                foreach (var d in Deviations) if (d.PerMille > m) m = d.PerMille;
                return m;
            }
        }
    }

    [Serializable]
    public sealed class SimReport
    {
        public string Volume;
        public List<SimPolicyReport> Policies = new List<SimPolicyReport>();
        public List<string> ReachabilityGaps = new List<string>();
        /// <summary>Mean and max total-variation distance (per mille) of the option distribution with EPS on vs off.</summary>
        public int EpsMeanTvPerMille, EpsMaxTvPerMille;

        public SimPolicyReport For(SimPolicy p) => Policies.Find(r => r.Policy == p);

        public int TotalAnchorViolations
        {
            get { int n = 0; foreach (var p in Policies) n += p.AnchorViolations; return n; }
        }

        public string ToText()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# 命运模拟报告 · {Volume}");
            foreach (var p in Policies)
            {
                sb.AppendLine($"## {p.Policy}：{p.Runs} 次 × {p.Settles / Math.Max(1, p.Runs)} 次结算，{p.ElapsedMs} ms");
                sb.AppendLine($"- HARD 锚点违反 {p.AnchorViolations}；预算超额 {p.BudgetOverruns}；historical 不一致 {p.HistoricalMismatches}；引擎错误 {p.EngineErrors}");
                sb.AppendLine($"- 最大偏离率（人物×阶段）{Pct(p.MaxDeviationPerMille)}");
                foreach (var d in p.Deviations)
                    if (d.Deviations > 0) sb.AppendLine($"  - {d.CharacterId} @ {d.Stage}：{Pct(d.PerMille)}");
                foreach (var kv in p.RescueSuccesses) sb.AppendLine($"- 救人 {kv.Key} 成功 {kv.Value}/{p.Runs}");
                foreach (var e in p.SampleErrors) sb.AppendLine($"- 错误样例：{e}");
            }
            sb.AppendLine($"## EPS 影响（最大交往，EPS 开 vs 关）：平均 TV {Pct(EpsMeanTvPerMille)}，最大 {Pct(EpsMaxTvPerMille)}");
            sb.AppendLine(ReachabilityGaps.Count == 0 ? "## 可达性：无缺口" : "## 可达性缺口");
            foreach (var g in ReachabilityGaps) sb.AppendLine("- " + g);
            return sb.ToString();
        }

        static string Pct(int perMille) => (perMille / 10) + "." + (perMille % 10) + "%";
    }

    /// <summary>Headless whole-volume simulation and reachability report (spec §8.2, §8.3). No I/O, no saves.</summary>
    public static class FateSimulator
    {
        public static SimReport Run(FateDataSet data, string volume, int runs, ulong baseSeed = 0x5EEDUL, bool epsReport = true)
        {
            var report = new SimReport { Volume = volume };
            report.Policies.Add(RunPolicy(data, volume, runs, SimPolicy.NoInteraction, baseSeed));
            var max = RunPolicy(data, volume, runs, SimPolicy.MaxInteraction, baseSeed);
            report.Policies.Add(max);
            if (epsReport)
            {
                var t = data.Tuning.Clone();
                t.eps = 0;
                var noEps = RunPolicy(data.WithTuning(t), volume, runs, SimPolicy.MaxInteraction, baseSeed);
                EpsImpact(max, noEps, out report.EpsMeanTvPerMille, out report.EpsMaxTvPerMille);
            }
            report.ReachabilityGaps.AddRange(Reachability(data, volume, max));
            return report;
        }

        public static SimPolicyReport RunPolicy(FateDataSet data, string volume, int runs, SimPolicy policy, ulong baseSeed)
        {
            var rep = new SimPolicyReport { Policy = policy, Runs = runs };
            var ctx = new SimConditionContext(policy);
            var dev = new SortedDictionary<string, DeviationStat>(StringComparer.Ordinal);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int run = 0; run < runs; run++)
            {
                ulong seed = Hash64.SplitMix64(baseSeed + (ulong)run);
                var s = FateEngine.CreateInitialState(data, volume, seed);
                TrackAvailability(s, rep);
                for (string node = data.NextNode(s.world.stageId); node != null; node = data.NextNode(node))
                {
                    var log = PlayStage(data, s, policy, ctx, rep);
                    string prev = s.world.stageId;
                    var res = FateEngine.Settle(data, s, log, node, ctx);
                    s = res.NewFateSection;
                    rep.Settles++;
                    rep.EngineErrors += res.Errors.Count;
                    foreach (var e in res.Errors)
                    {
                        if (e.StartsWith("post ")) rep.HistoricalMismatches++;
                        if (rep.SampleErrors.Count < 5 && !rep.SampleErrors.Contains(e)) rep.SampleErrors.Add(e);
                    }
                    if (s.world.deviationBudget.used > s.world.deviationBudget.max) rep.BudgetOverruns++;
                    rep.AnchorViolations += CountAnchorViolations(data, s, prev, node, ctx);
                    rep.HistoricalMismatches += CountPostMismatches(data, s, node);
                    foreach (var c in s.characters.Values)
                    {
                        foreach (var h in c.fateHistory)
                        {
                            if (h.stage != node) continue;
                            string key = c.id + "|" + node;
                            if (!dev.TryGetValue(key, out var st)) dev[key] = st = new DeviationStat { CharacterId = c.id, Stage = node };
                            st.Runs++;
                            if (!h.@default) st.Deviations++;
                            string ok = key + "|" + h.optionId;
                            rep.OptionCounts[ok] = (rep.OptionCounts.TryGetValue(ok, out var n) ? n : 0) + 1;
                        }
                    }
                    TrackAvailability(s, rep);
                }
            }
            watch.Stop();
            rep.ElapsedMs = watch.ElapsedMilliseconds;
            rep.Deviations.AddRange(dev.Values);
            return rep;
        }

        /// <summary>What happens during a stage: interactions by policy, and the node's rescue checks.</summary>
        static StageEventLog PlayStage(FateDataSet data, FateSaveSection s, SimPolicy policy, IConditionContext ctx, SimPolicyReport rep)
        {
            var log = new StageEventLog();
            long seq = s.world.seqCounter;
            string stage = s.world.stageId;
            if (policy == SimPolicy.MaxInteraction)
            {
                foreach (var c in s.characters.Values)
                {
                    if (c.id == s.world.protagonistId || !c.IsAlive) continue;
                    var route = data.RouteFor(c);
                    if (route == null || !data.IsActive(route, stage)) continue;
                    var likes = data.Profile(c.id)?.values?.likes;
                    var tags = likes != null && likes.Length > 0 ? (string[])likes.Clone() : new[] { "courtesy" };
                    log.Add(StageEvent.MemoryEvent(++seq, new MemoryEntry
                    {
                        who = c.id, key = "sim." + stage, value = "max", kind = MemoryKinds.Player, node = stage,
                        actors = new[] { s.world.protagonistId, c.id }, tags = tags, intensity = 3, valence = 2, importance = 3,
                        templateId = "mem.sim", text = ""
                    }));
                }
            }
            foreach (var rc in data.RescuesAt(stage))
            {
                bool eligible = ConditionEvaluator.Eval(rc.eligibility, s, ctx, data);
                if (!eligible) continue;
                bool success = ConditionEvaluator.Eval(rc.fulfillment, s, ctx, data, true);
                log.Add(StageEvent.RescueEvent(++seq, rc.id, success));
                if (success) rep.RescueSuccesses[rc.id] = (rep.RescueSuccesses.TryGetValue(rc.id, out var n) ? n : 0) + 1;
            }
            return log;
        }

        static void TrackAvailability(FateSaveSection s, SimPolicyReport rep)
        {
            foreach (var c in s.characters.Values)
                if (c.recruitState == RecruitStates.Available || c.recruitState == RecruitStates.InParty)
                    rep.ReachedAvailable[c.id] = 1;
        }

        static int CountAnchorViolations(FateDataSet data, FateSaveSection s, string prev, string node, IConditionContext ctx)
        {
            int n = 0;
            foreach (var c in s.characters.Values)
            {
                var main = data.MainRoute(c.id);
                if (main == null) continue;
                bool rescued = c.tier == Tiers.T1S;
                foreach (var a in main.anchors)
                {
                    if (!a.IsHard) continue;
                    if (a.IsDies)
                    {
                        if (a.stage == prev && c.alive && !rescued) n++;
                        continue;
                    }
                    if (a.stage != node || rescued || !c.IsAlive) continue;
                    if (!ConditionEvaluator.Eval(a.state, s, ctx, data)) n++;
                }
            }
            return n;
        }

        static int CountPostMismatches(FateDataSet data, FateSaveSection s, string node)
        {
            int n = 0;
            foreach (var p in data.Raw.worldSlots.posts)
            {
                if (p.historical == null || !p.historical.TryGetValue(node, out var want)) continue;
                s.world.slots.posts.TryGetValue(p.id, out var have);
                if (have != want) n++;
            }
            return n;
        }

        static void EpsImpact(SimPolicyReport withEps, SimPolicyReport noEps, out int meanTv, out int maxTv)
        {
            // Group "char|stage|option" counts by "char|stage" and compare distributions.
            var groups = new SortedDictionary<string, bool>(StringComparer.Ordinal);
            foreach (var k in withEps.OptionCounts.Keys) groups[Group(k)] = true;
            foreach (var k in noEps.OptionCounts.Keys) groups[Group(k)] = true;
            long sum = 0;
            maxTv = 0;
            foreach (var g in groups.Keys)
            {
                var keys = new SortedDictionary<string, bool>(StringComparer.Ordinal);
                foreach (var k in withEps.OptionCounts.Keys) if (Group(k) == g) keys[k] = true;
                foreach (var k in noEps.OptionCounts.Keys) if (Group(k) == g) keys[k] = true;
                long a = 0, b = 0;
                foreach (var k in keys.Keys) { a += Get(withEps, k); b += Get(noEps, k); }
                long diff = 0;
                foreach (var k in keys.Keys)
                {
                    long pa = a == 0 ? 0 : Get(withEps, k) * 1000 / a;
                    long pb = b == 0 ? 0 : Get(noEps, k) * 1000 / b;
                    diff += Math.Abs(pa - pb);
                }
                int tv = (int)(diff / 2);
                sum += tv;
                if (tv > maxTv) maxTv = tv;
            }
            meanTv = groups.Count == 0 ? 0 : (int)(sum / groups.Count);
        }

        static string Group(string key) => key.Substring(0, key.LastIndexOf('|'));
        static long Get(SimPolicyReport r, string k) => r.OptionCounts.TryGetValue(k, out var n) ? n : 0;

        /// <summary>
        /// Gaps: recruitable characters never available/in party under max interaction (with the likely cause),
        /// and rescue cases whose eligibility ∧ fulfillment never held.
        /// </summary>
        public static List<string> Reachability(FateDataSet data, string volume, SimPolicyReport max)
        {
            var gaps = new List<string>();
            var vol = data.Volume(volume);
            foreach (var p in data.Profiles)
            {
                if (Array.IndexOf(p.recruitableIn, volume) < 0) continue;
                var route = data.MainRoute(p.id);
                foreach (var a in route?.anchors ?? Array.Empty<Anchor>())
                    if (a.IsHard && !a.IsDies && FateValidator.ConflictsWithParty(a.state, p.id, vol?.protagonistFaction) && string.IsNullOrEmpty(a.departureSceneId))
                        gaps.Add($"{p.id}：锚点 {a.id} 与在队冲突，缺 departureSceneId（离队剧情）");
                if (max.ReachedAvailable.ContainsKey(p.id)) continue;
                bool hasPath = false;
                if (route != null)
                    foreach (var g in route.genericOptions)
                        if (data.Generic(g)?.effect?.recruitState == RecruitStates.Available) hasPath = true;
                if (route != null)
                    foreach (var o in route.fateOptions)
                        if (o.effect?.recruitState == RecruitStates.Available) hasPath = true;
                gaps.Add(hasPath
                    ? $"{p.id}：最大交往下从未到达 available／在队（候选存在但从未胜出或被过滤）"
                    : $"{p.id}：最大交往下从未到达 available／在队，缺把 recruitState 设为 available 的候选或剧情招募点");
            }
            foreach (var rc in data.Raw.rescues)
            {
                if (rc.volume != null && rc.volume != volume) continue;
                if (!max.RescueSuccesses.ContainsKey(rc.id))
                    gaps.Add($"救人 {rc.id}：eligibility ∧ fulfillment 从未同时成立");
            }
            return gaps;
        }
    }
}
