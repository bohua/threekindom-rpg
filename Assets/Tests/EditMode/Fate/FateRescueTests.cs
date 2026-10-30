using NUnit.Framework;
using ThreeKingdoms.Core.Fate;
using static ThreeKingdoms.Tests.Fate.FateTestKit;

namespace ThreeKingdoms.Tests.Fate
{
    // S7: rescue merge (spec §4.5, §5.4). AC 6 on the synthetic case rescue.t_doomed.tv.N2.
    public sealed class FateRescueTests
    {
        const string CaseId = "rescue.t_doomed.tv.N2";
        FateDataSet data;
        RescueCase rescue;

        [SetUp] public void Setup()
        {
            data = TestData();
            rescue = data.Rescue(CaseId);
        }

        /// <summary>Into N2 with the given N1 log; returns the N2 state and the eligibility answer the story would see.</summary>
        FateSaveSection IntoN2(bool befriend, FakeContext ctx, out bool eligible)
        {
            var s = Start(data);
            var log = befriend ? Log(s, Player("t_doomed", "stood_together", 3, 2, 3, "valor", "fought_alongside")) : null;
            var s2 = Settle(data, s, log, "tv.N2", ctx).NewFateSection;
            eligible = ConditionEvaluator.Eval(rescue.eligibility, s2, ctx, data);
            return s2;
        }

        SettleResult IntoN3(FateSaveSection s2, bool eligible, FakeContext ctx)
        {
            var log = new StageEventLog();
            if (eligible)
            {
                bool success = ConditionEvaluator.Eval(rescue.fulfillment, s2, ctx, data, true);
                log.Add(StageEvent.RescueEvent(s2.world.seqCounter + 1, CaseId, success));
            }
            return Settle(data, s2, log, "tv.N3", ctx);
        }

        static FakeContext Ready(bool breakout)
        {
            var ctx = new FakeContext();
            ctx.Flags.Add("t_relief_ready");
            if (breakout) ctx.Objectives.Add("t_breakout");
            return ctx;
        }

        [Test] public void AC6_NotEligibleWithoutTheFlagOrFavor()
        {
            IntoN2(true, new FakeContext(), out bool noFlag);
            Assert.IsFalse(noFlag, "flag missing");
            IntoN2(false, Ready(true), out bool noFavor);
            Assert.IsFalse(noFavor, "favor below 50");
            IntoN2(true, Ready(true), out bool both);
            Assert.IsTrue(both);
            Assert.IsFalse(ConditionEvaluator.Eval(rescue.eligibility, Start(data), Ready(true), data), "favor starts at 0");
        }

        [Test] public void AC6_SuccessfulRescueMakesASurvivor()
        {
            var ctx = Ready(true);
            var s2 = IntoN2(true, ctx, out bool eligible);
            Assert.IsTrue(eligible);
            var r3 = IntoN3(s2, eligible, ctx);
            var s3 = r3.NewFateSection;
            var doomed = s3.Character("t_doomed");
            Assert.IsTrue(doomed.alive);
            Assert.AreEqual(Tiers.T1S, doomed.tier);
            Assert.AreEqual("t_doomed.tv.survivor", doomed.routeId);
            Assert.AreEqual("t_doomed.tv.retire", doomed.fateHistory.Find(h => h.stage == "tv.N3").optionId);
            Assert.AreEqual("wanderer", doomed.faction);
            Assert.AreEqual("success", doomed.rescues[CaseId]);
            Assert.IsTrue(new FateQuery(s3).Rescued(CaseId));
            Assert.IsFalse(s3.Character("t_squire").alive, "unchangedDeaths still die");
            Assert.AreEqual("t_heir", s3.world.slots.posts["t_post_a"], "powerTransfer at its atNode");
            Assert.IsEmpty(r3.Errors);
        }

        [Test] public void AC6_FailedRescueFollowsHistoryAndBroadcasts()
        {
            var ctx = Ready(false);
            var s2 = IntoN2(true, ctx, out bool eligible);
            Assert.IsTrue(eligible);
            var s3 = IntoN3(s2, eligible, ctx).NewFateSection;
            Assert.IsFalse(s3.Character("t_doomed").alive);
            Assert.AreEqual("fail", s3.world.rescueResults[CaseId]);
            var told = MemoryStore.Find(s3.Character("t_heir"), "t_doomed_death");
            Assert.IsNotNull(told);
            Assert.AreEqual(5, told.importance);
        }

        [Test] public void AC6_HistoricalSlotsAreIdenticalOnBothPaths()
        {
            var ok = IntoN3(IntoN2(true, Ready(true), out var e1), e1, Ready(true)).NewFateSection;
            var fail = IntoN3(IntoN2(true, Ready(false), out var e2), e2, Ready(false)).NewFateSection;
            var never = IntoN3(IntoN2(false, new FakeContext(), out var e3), e3, new FakeContext()).NewFateSection;
            Assert.AreEqual(Json(fail.world.slots.posts), Json(ok.world.slots.posts));
            Assert.AreEqual(Json(never.world.slots.posts), Json(ok.world.slots.posts));
            Assert.AreEqual(ok.Character("t_squire").alive, fail.Character("t_squire").alive);
        }

        [Test] public void AC6_AffectedNodeVariantIsDeclared()
        {
            Assert.AreEqual("tv.N4", rescue.onSuccess.affectedNodes[0].node);
            CollectionAssert.Contains(data.KnownIds("dialogues"), rescue.onSuccess.affectedNodes[0].dialogueVariant);
            Assert.AreEqual("battle_t_siege_rescue", rescue.sceneVariant.eligible);
        }

        [Test] public void SurvivorConstraintsBlockForbiddenOptions()
        {
            // Give the survivor route an option that takes the forbidden post; arbitration must refuse it.
            var d = TestData(b =>
            {
                foreach (var r in b.routes)
                    if (r.routeId == "t_doomed.tv.survivor")
                    {
                        var grab = new FateOption
                        {
                            id = "t_doomed.tv.retake_post", stages = new[] { "tv.N4" }, playerProximity = 1, requiresCause = new[] { "PLAYER:+" },
                            effect = new FateEffect { post = "t_post_a", faction = "t_house" }
                        };
                        r.fateOptions = new[] { r.fateOptions[0], grab };
                    }
            });
            data = d;
            rescue = d.Rescue(CaseId);
            var ctx = Ready(true);
            var s3 = IntoN3(IntoN2(true, ctx, out var e), e, ctx).NewFateSection;
            for (ulong seed = 0; seed < 10; seed++)
            {
                s3.world.fateSeed = FateStreams.FormatSeed(seed);
                var r4 = Settle(d, s3, Log(s3, Player("t_doomed", "visit" + seed, 3, 2, 3, "valor")), "tv.N4", ctx);
                Assert.AreEqual("t_heir", r4.NewFateSection.world.slots.posts["t_post_a"]);
                Assert.AreNotEqual("t_doomed.tv.retake_post", Trace(r4, "t_doomed").Chosen);
            }
        }

        [Test] public void StoryFlaggedDeathRemovesTheCharacterAtTheNextSettle()
        {
            var s = Start(data);
            var log = new StageEventLog().Add(StageEvent.DeathEvent(1, "t_rival"));
            var r = Settle(data, s, log, "tv.N2");
            Assert.IsFalse(r.NewFateSection.Character("t_rival").alive);
            Assert.IsNull(Trace(r, "t_rival"));
        }
    }
}
