using System;
using System.Collections.Generic;
using NUnit.Framework;
using ThreeKingdoms.Core.Fate;
using static ThreeKingdoms.Tests.Fate.FateTestKit;

namespace ThreeKingdoms.Tests.Fate
{
    // S8: validator — clean data passes; every fault sample of the brief §5 is caught by the right check.
    public sealed class FateValidatorTests
    {
        static List<ValidationIssue> Validate(Action<FateDataBundle> mutate) => FateValidator.ValidateAll(TestData(mutate));

        static void AssertCaught(string code, string fragment, Action<FateDataBundle> mutate)
        {
            var issues = Validate(mutate);
            Assert.IsTrue(issues.Exists(i => i.Code == code && i.Message.Contains(fragment)),
                $"expected {code} containing '{fragment}', got:\n{string.Join("\n", issues)}");
        }

        [Test] public void CleanSyntheticDataPasses()
        {
            var issues = FateValidator.ValidateAll(TestData());
            Assert.IsEmpty(issues, string.Join("\n", issues));
        }

        [Test] public void V01_UnknownReference() =>
            AssertCaught("V-01", "unknown character t_ghost", b => Route(b, "t_loyal").initialRelations[0].target = "t_ghost");

        [Test] public void V01_UnknownScene() =>
            AssertCaught("V-01", "unknown scene battle_nowhere", b => Option(b, "t_loyal.tv.serve_battle").effect.presentAt = new[] { "battle_nowhere" });

        [Test] public void V02_UnknownPredicate() =>
            AssertCaught("V-02", "unknown predicate 'isHero'", b => Option(b, "t_proud.tv.leave_party").preconditions = Condition.Of("isHero", "t_proud"));

        [Test] public void V02_BattleObjectiveInEligibility() =>
            AssertCaught("V-02", "battleObjective is only allowed in fulfillment",
                b => b.rescues[0].eligibility = Condition.All(Condition.Of("flag", "t_relief_ready"), Condition.Of(Predicates.BattleObjective, "t_breakout")));

        [Test] public void V03_StageWithoutDefault() =>
            AssertCaught("V-03", "0 default options at tv.N3", b =>
            {
                var r = Route(b, "t_rival");
                r.defaultRoute = Array.FindAll(r.defaultRoute, st => st.stage != "tv.N3");
            });

        [Test] public void V03_DefaultWithPreconditions() =>
            AssertCaught("V-03", "default t_wild.tv.live has preconditions", b => Option(b, "t_wild.tv.live").preconditions = Condition.Of("flag", "t_relief_ready"));

        [Test] public void V03_DefaultBreaksHardAnchor() =>
            AssertCaught("V-03", "default route breaks HARD anchor t_loyal.anchor.tv.N2.battle", b => Option(b, "t_loyal.tv.serve_battle").effect.presentAt = new string[0]);

        [Test] public void V04_AnchorNoCandidateCanSatisfy() =>
            AssertCaught("V-04", "no candidate satisfies HARD anchor", b =>
                Route(b, "t_loyal").anchors[1].state = Condition.Of("presentAt", "t_loyal", "battle_t_siege"));

        [Test] public void V04_InPartyCandidateConflictsWithPresentAt() =>
            AssertCaught("V-04", "option t_proud.tv.leave_party conflicts with presentAt anchor", b =>
            {
                var r = Route(b, "t_proud");
                Option(b, "t_proud.tv.follow").effect.presentAt = new[] { "battle_t2" };
                r.anchors = new[] { new Anchor { id = "t_proud.anchor.tv.N3.battle", stage = "tv.N3", strength = "HARD", state = Condition.Of("presentAt", "t_proud", "battle_t2") } };
            });

        [Test] public void V04_PartyConflictWithoutDepartureScene() =>
            AssertCaught("V-04", "has no departureSceneId", b => Route(b, "t_loyal").anchors[0].departureSceneId = null);

        [Test] public void V05_TwoStepWithoutNextStage() =>
            AssertCaught("V-05", "twoStep stages must include the stage right after", b => Option(b, "t_proud.tv.leave_party").stages = new[] { "tv.N2" });

        [Test] public void V05_CauseAgainstProximity() =>
            AssertCaught("V-05", "proximity +1 needs a positive cause", b => Option(b, "t_loyal.tv.wander").requiresCause = new[] { "PLAYER:insult" });

        [Test] public void V05_NonDefaultWithoutCause() =>
            AssertCaught("V-05", "needs requiresCause", b => Option(b, "t_heir.tv.hide").requiresCause = new string[0]);

        [Test] public void V05_T3DeathNeedsEventCause() =>
            AssertCaught("V-05", "T3 death needs a RANDOM: or HISTORY: cause", b => Option(b, "t_wild.tv.plague_death").requiresCause = new[] { "PLAYER:-" });

        [Test] public void V06_UnchangedDeathWithoutDiesAnchor() =>
            AssertCaught("V-06", "unchangedDeaths t_squire has no dies anchor", b => Route(b, "t_squire").anchors = new Anchor[0]);

        [Test] public void V06_MustKeepDiesBeforeUntil() =>
            AssertCaught("V-06", "mustKeep t_doomed dies", b => b.worldSlots.factionFloors[0].mustKeep = new[] { "t_loyal", "t_doomed" });

        [Test] public void V06_PowerPositionNeedsTransfer() =>
            AssertCaught("V-06", "powerTransfer is required", b => b.rescues[0].onSuccess.powerTransfer = null);

        [Test] public void V06_ProtagonistCaseCannotUseAttitudeToProtagonist() =>
            AssertCaught("V-06", "protagonist case may not use favorAtLeast", b => b.rescues[0].subject = "protagonist");

        [Test] public void V08_HistoryStageOutOfRange() =>
            AssertCaught("V-08", "stage tv.N9 is not a node", b => b.historyEvents[2].stage = "tv.N9");

        [Test] public void V08_HistoryOutOfOrder() =>
            AssertCaught("V-08", "out of order", b => b.historyEvents[1].stage = "tv.N3");
    }

    // S9: whole-volume simulation and reachability report (spec §8.2, §8.3).
    public sealed class FateSimulatorTests
    {
        static SimReport report;

        static SimReport Report()
        {
            if (report == null) report = FateSimulator.Run(TestData(), "tv", 1000);
            return report;
        }

        [Test] public void ThousandRunsTwoPoliciesHaveNoHardAnchorViolations()
        {
            var r = Report();
            Assert.AreEqual(2, r.Policies.Count);
            foreach (var p in r.Policies)
            {
                Assert.AreEqual(1000, p.Runs);
                Assert.AreEqual(4000, p.Settles, "four settles per run reach the end of the volume");
                Assert.AreEqual(0, p.AnchorViolations, p.Policy.ToString());
                Assert.AreEqual(0, p.BudgetOverruns, p.Policy.ToString());
                Assert.AreEqual(0, p.HistoricalMismatches, p.Policy.ToString());
                Assert.AreEqual(0, p.EngineErrors, string.Join("\n", p.SampleErrors));
            }
            UnityEngine.Debug.Log(r.ToText());
        }

        [Test] public void NoInteractionDeviatesAtMostFivePercentPerCharacterStage()
        {
            var none = Report().For(SimPolicy.NoInteraction);
            foreach (var d in none.Deviations)
                Assert.LessOrEqual(d.PerMille, 50, $"{d.CharacterId} @ {d.Stage}");
        }

        [Test] public void MaxInteractionReachesRecruitsAndRescue()
        {
            var r = Report();
            var max = r.For(SimPolicy.MaxInteraction);
            Assert.IsTrue(max.ReachedAvailable.ContainsKey("t_loyal"));
            Assert.IsTrue(max.ReachedAvailable.ContainsKey("t_rival"));
            Assert.AreEqual(1000, max.RescueSuccesses["rescue.t_doomed.tv.N2"]);
            Assert.IsFalse(r.For(SimPolicy.NoInteraction).RescueSuccesses.ContainsKey("rescue.t_doomed.tv.N2"));
            Assert.IsEmpty(r.ReachabilityGaps, string.Join("\n", r.ReachabilityGaps));
        }

        [Test] public void ReportsEpsImpactAndRunsWithinBudget()
        {
            var r = Report();
            Assert.GreaterOrEqual(r.EpsMaxTvPerMille, r.EpsMeanTvPerMille);
            Assert.LessOrEqual(r.EpsMaxTvPerMille, 1000);
            long ms = 0;
            foreach (var p in r.Policies) ms += p.ElapsedMs;
            Assert.Less(ms, 180000, "1,000 runs × 2 policies must finish within 3 minutes");
            StringAssert.Contains("EPS", r.ToText());
        }

        [Test] public void MissingDepartureSceneShowsAsAGap()
        {
            var d = TestData(b => Route(b, "t_loyal").anchors[0].departureSceneId = null);
            var r = FateSimulator.Run(d, "tv", 50, epsReport: false);
            Assert.IsTrue(r.ReachabilityGaps.Exists(g => g.Contains("t_loyal") && g.Contains("departureSceneId")), string.Join("\n", r.ReachabilityGaps));
        }

        [Test] public void UnreachableRecruitAndRescueAreGaps()
        {
            var d = TestData(b =>
            {
                Route(b, "t_rival").genericOptions = new[] { "generic.stay" };
                b.rescues[0].eligibility = Condition.Of("favorAtLeast", "t_doomed", "101");
                b.knownIds = null;
            });
            var r = FateSimulator.Run(d, "tv", 50, epsReport: false);
            Assert.IsTrue(r.ReachabilityGaps.Exists(g => g.StartsWith("t_rival")), string.Join("\n", r.ReachabilityGaps));
            Assert.IsTrue(r.ReachabilityGaps.Exists(g => g.Contains("rescue.t_doomed.tv.N2")));
        }
    }
}
