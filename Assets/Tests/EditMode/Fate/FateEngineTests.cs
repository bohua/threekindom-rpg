using System.IO;
using NUnit.Framework;
using ThreeKingdoms.Core.Fate;
using static ThreeKingdoms.Tests.Fate.FateTestKit;

namespace ThreeKingdoms.Tests.Fate
{
    // S6: the eight-step settle (spec §5.2) on the synthetic dataset. AC 2, 3, 5.
    public sealed class FateEngineTests
    {
        FateDataSet data;

        [SetUp] public void Setup() => data = TestData();

        [Test] public void InitialStateAppliesDefaultsAndHistory()
        {
            var s = Start(data);
            Assert.AreEqual("tv.N1", s.world.stageId);
            Assert.AreEqual("t_doomed", s.world.slots.posts["t_post_a"]);
            Assert.AreEqual("t_house", s.Character("t_loyal").faction);
            Assert.AreEqual("t_loyal.tv.serve", s.Character("t_loyal").fateHistory[0].optionId);
            Assert.AreEqual(60, s.Character("t_rival").RelationTo("t_lord").baseline.favor);
            Assert.IsNotNull(s.Character("t_proud").RelationTo("t_lord").views);
        }

        [Test] public void AC2_SameSeedSameLogGivesIdenticalState()
        {
            var s = Start(data);
            var log = Log(s, Player("t_loyal", "aid", 2, 1, 3, "benevolence"), Player("t_proud", "slight", 3, -2, 3, "insult"));
            var a = Settle(data, s, log, "tv.N2");
            var b = Settle(data, s, log, "tv.N2");
            Assert.AreEqual(Json(a.NewFateSection), Json(b.NewFateSection));
            Assert.AreEqual(Json(a.Changes), Json(b.Changes));
            Assert.AreEqual(Json(a.Announced), Json(b.Announced));
            // a reload (state round-tripped through JSON) settles the same
            var reloaded = Newtonsoft.Json.JsonConvert.DeserializeObject<FateSaveSection>(Json(s), ThreeKingdoms.Data.Fate.FateJsonLoader.Settings());
            Assert.AreEqual(Json(a.NewFateSection), Json(Settle(data, reloaded, log, "tv.N2").NewFateSection));
        }

        [Test] public void AC2_WholeVolumeReplayIsIdentical()
        {
            string Run()
            {
                var s = Start(data, 77);
                var sb = new System.Text.StringBuilder();
                foreach (var node in new[] { "tv.N2", "tv.N3", "tv.N4", "tv.N5" })
                {
                    var r = Settle(data, s, Log(s, Player("t_rival", "talk." + node, 2, 1, 3, "courtesy")), node, new FakeContext());
                    s = r.NewFateSection;
                    sb.Append(Json(r.Trace));
                }
                return sb + Json(s);
            }
            Assert.AreEqual(Run(), Run());
        }

        [Test] public void SettleDoesNotMutateItsInputs()
        {
            var s = Start(data);
            string before = Json(s);
            var log = Log(s, Player("t_proud", "slight", 3, -2, 3, "insult"));
            Settle(data, s, log, "tv.N2");
            Assert.AreEqual(before, Json(s));
        }

        [Test] public void DifferentSeedsCanDiffer()
        {
            // the eps stream depends on the seed, so scores differ even when choices do not
            var s1 = Start(data, 1); var s2 = Start(data, 2);
            var r1 = Settle(data, s1, null, "tv.N2"); var r2 = Settle(data, s2, null, "tv.N2");
            Assert.AreNotEqual(Json(r1.Trace), Json(r2.Trace));
        }

        [Test] public void AC3_NoInteractionMeansT1HasOnlyTheDefault()
        {
            var s = Start(data);
            s = Settle(data, s, null, "tv.N2").NewFateSection;
            var r = Settle(data, s, null, "tv.N3");
            var wander = Candidate(r, "t_loyal", "t_loyal.tv.wander");
            Assert.AreEqual("filtered:cause", wander.Status);
            Assert.AreEqual("t_loyal.tv.serve", Trace(r, "t_loyal").Chosen);
            Assert.IsEmpty(Trace(r, "t_loyal").Tokens);
        }

        [Test] public void AC3_ImportantInteractionOpensASameDirectionCandidate()
        {
            var s = Start(data);
            s = Settle(data, s, Log(s, Player("t_loyal", "fed_refugees", 2, 1, 3, "benevolence")), "tv.N2").NewFateSection;
            var r = Settle(data, s, null, "tv.N3");
            Assert.AreEqual("scored", Candidate(r, "t_loyal", "t_loyal.tv.wander").Status);
            StringAssert.StartsWith("PLAYER:m_", Candidate(r, "t_loyal", "t_loyal.tv.wander").Cause);
            CollectionAssert.Contains(Trace(r, "t_loyal").Tokens, "PLAYER:benevolence");
        }

        [Test] public void AC3_LowImportanceInteractionIsNotACause()
        {
            var s = Start(data);
            s = Settle(data, s, Log(s, Player("t_loyal", "nod", 1, 1, 2, "benevolence")), "tv.N2").NewFateSection;
            var r = Settle(data, s, null, "tv.N3");
            Assert.AreEqual("filtered:cause", Candidate(r, "t_loyal", "t_loyal.tv.wander").Status);
        }

        [Test] public void RelationTokenUsesRiseOverBaselineNotAbsoluteFavor()
        {
            var s = Start(data);
            var r = Settle(data, s, null, "tv.N2");
            Assert.AreEqual(60, r.NewFateSection.Character("t_rival").RelationTo("t_lord").favor);
            CollectionAssert.DoesNotContain(Trace(r, "t_rival").Tokens, "RELATION:t_lord");
            s = Start(data);
            s.Character("t_rival").RelationTo("t_lord").favor = 100; // +40 over its baseline of 60
            r = Settle(data, s, null, "tv.N2");
            CollectionAssert.Contains(Trace(r, "t_rival").Tokens, "RELATION:t_lord");
            Assert.AreEqual("scored", Candidate(r, "t_rival", "generic.join_protagonist").Status);
        }

        [Test] public void HardAnchorFiltersCandidates()
        {
            var d = TestData(b => Option(b, "t_loyal.tv.wander").stages = new[] { "tv.N2", "tv.N3", "tv.N4" });
            var s = Start(d);
            var r = Settle(d, s, Log(s, Player("t_loyal", "fed", 3, 2, 3, "benevolence")), "tv.N2");
            Assert.AreEqual("filtered:anchor", Candidate(r, "t_loyal", "t_loyal.tv.wander").Status);
            CollectionAssert.Contains(r.NewFateSection.Character("t_loyal").presentAt, "battle_t2");
        }

        [Test] public void HistoryIsDeliveredByScopeAndDelayOnlyOnce()
        {
            var s = Start(data);
            Assert.IsNotNull(MemoryStore.Find(s.Character("t_loyal"), "h.h.t_war"), "world event of the first node reaches everyone at once");
            Assert.IsNull(MemoryStore.Find(s.Character("t_rival"), "h.h.t_flood"), "region event has a one-stage delay");
            Assert.IsNull(MemoryStore.Find(s.Character("t_heir"), "h.h.t_purge"), "never a future event");
            var r2 = Settle(data, s, null, "tv.N2");
            var s2 = r2.NewFateSection;
            Assert.IsNotNull(MemoryStore.Find(s2.Character("t_rival"), "h.h.t_flood"), "t_town is in r_south");
            Assert.IsNull(MemoryStore.Find(s2.Character("t_proud"), "h.h.t_flood"), "t_camp is in r_north");
            Assert.IsNotNull(MemoryStore.Find(s2.Character("t_heir"), "h.h.t_purge"), "explicit recipient");
            Assert.IsNull(MemoryStore.Find(s2.Character("t_loyal"), "h.h.t_purge"));
            CollectionAssert.Contains(Trace(r2, "t_heir").Tokens, "HISTORY:h.t_purge");
            Assert.AreEqual(new[] { "h.t_war" }, s2.world.historyDelivered["tv.N1"].ToArray());
            Assert.AreEqual(new[] { "h.t_flood", "h.t_purge" }, s2.world.historyDelivered["tv.N2"].ToArray());
            var s3 = Settle(data, s2, null, "tv.N3").NewFateSection;
            Assert.IsFalse(s3.world.historyDelivered.ContainsKey("tv.N3"), "nothing is delivered twice");
        }

        [Test] public void HistoryCauseOpensACandidate()
        {
            var s = Start(data);
            var r = Settle(data, s, null, "tv.N2");
            Assert.AreEqual("scored", Candidate(r, "t_heir", "t_heir.tv.hide").Status);
            StringAssert.StartsWith("HISTORY:", Candidate(r, "t_heir", "t_heir.tv.hide").Cause);
        }

        [Test] public void RandomEventIsDrawnFromTheRandomStream()
        {
            // re.t_plague is limited to stage tv.N3
            var forced = DataWithTuning(t => t.randomRateT3 = 65536);
            var early = Settle(forced, Start(forced), null, "tv.N2");
            Assert.IsNull(MemoryStore.Find(early.NewFateSection.Character("t_wild"), "r.re.t_plague"), "not eligible at tv.N2");
            var r = Settle(forced, early.NewFateSection, null, "tv.N3");
            Assert.IsNotNull(MemoryStore.Find(r.NewFateSection.Character("t_wild"), "r.re.t_plague"));
            CollectionAssert.Contains(Trace(r, "t_wild").Tokens, "RANDOM:re.t_plague");
            var never = DataWithTuning(t => t.randomRateT3 = 0);
            var r0 = Settle(never, Settle(never, Start(never), null, "tv.N2").NewFateSection, null, "tv.N3");
            Assert.IsNull(MemoryStore.Find(r0.NewFateSection.Character("t_wild"), "r.re.t_plague"));
            Assert.AreEqual("filtered:cause", Candidate(r0, "t_wild", "t_wild.tv.plague_death").Status);
        }

        [Test] public void T3DeathGoesThroughTwoSteps()
        {
            // Force the plague and make the death option the clear favourite; it must announce first and kill only at the next node.
            var d = TestData(b => { b.tuning = b.tuning ?? TuningTable.CreateDefault(); b.tuning.randomRateT3 = 65536; b.tuning.tau = 655; b.tuning.eps = 0; Option(b, "t_wild.tv.plague_death").tags = null; Option(b, "t_wild.tv.plague_death").ambitionsServed = new[] { "live_quietly" }; });
            var s2 = Settle(d, Start(d), null, "tv.N2").NewFateSection;
            var r3 = Settle(d, s2, null, "tv.N3");
            var announced = r3.Announced.Find(a => a.CharacterId == "t_wild");
            Assert.IsNotNull(announced, Json(Trace(r3, "t_wild")));
            Assert.AreEqual("scene_t_wild_sick", announced.WarningSceneId);
            Assert.AreEqual("tv.N4", announced.CommitStage);
            Assert.IsTrue(r3.NewFateSection.Character("t_wild").alive);
            var r4 = Settle(d, r3.NewFateSection, null, "tv.N4");
            Assert.IsFalse(r4.NewFateSection.Character("t_wild").alive);
            Assert.IsTrue(r4.Changes.Exists(c => c.Kind == ChangeKinds.Death && c.CharacterId == "t_wild"));
            StringAssert.StartsWith("RANDOM:m_", r4.NewFateSection.Character("t_wild").fateHistory.Find(h => h.stage == "tv.N4").cause);
        }

        /// <summary>
        /// Insult t_proud during N1 and settle into N2. The leave option is favoured (~90%) but the draw is real,
        /// so take the first fixed seed whose draw picks it; the result for that seed is fully deterministic.
        /// </summary>
        static FateSaveSection AfterInsult(FateDataSet d, out SettleResult r2, bool withGift = true)
        {
            for (ulong seed = 1; seed <= 32; seed++)
            {
                var s = Start(d, seed);
                var log = Log(s, Player("t_proud", "slight", 3, -2, 3, "insult"));
                if (withGift) log.Add(StageEvent.GiftEvent(log.NextSeq(s), "t_proud", "book_t_strategy"));
                r2 = Settle(d, s, log, "tv.N2");
                if (r2.Announced.Exists(a => a.CharacterId == "t_proud")) return r2.NewFateSection;
            }
            Assert.Fail("no seed in 1..32 drew the leave option");
            r2 = null;
            return null;
        }

        [Test] public void AC5_LeaveCandidateFirstWinOnlyAnnounces()
        {
            var s2 = AfterInsult(data, out var r2);
            Assert.Less(s2.Character("t_proud").RelationTo("t_lord").favor, 0);
            var a = r2.Announced.Find(x => x.CharacterId == "t_proud");
            Assert.IsNotNull(a, Json(Trace(r2, "t_proud")));
            Assert.AreEqual("t_proud.tv.leave_party", a.OptionId);
            Assert.AreEqual("tv.N3", a.CommitStage);
            Assert.AreEqual("scene_t_proud_warning", a.WarningSceneId);
            StringAssert.StartsWith("PLAYER:m_", a.Cause);
            Assert.AreEqual("inParty", s2.Character("t_proud").recruitState, "the announcing stage follows the default");
            Assert.AreEqual("t_proud.tv.follow", s2.Character("t_proud").fateHistory.Find(h => h.stage == "tv.N2").optionId);
        }

        [Test] public void AC5_CommitWhenThresholdStillHoldsReturnsGivenItems()
        {
            var s2 = AfterInsult(data, out _);
            var r3 = Settle(data, s2, null, "tv.N3");
            var proud = r3.NewFateSection.Character("t_proud");
            Assert.AreEqual("departed", proud.recruitState);
            Assert.AreEqual("t_rival_band", proud.faction);
            Assert.IsTrue(Trace(r3, "t_proud").Committed);
            Assert.IsTrue(r3.Changes.Exists(c => c.Kind == ChangeKinds.ReturnItem && c.ItemId == "book_t_strategy"));
            Assert.IsFalse(r3.Changes.Exists(c => c.Kind == ChangeKinds.ReturnItem && c.ItemId == "sword_t_proud"), "own items leave with the character");
            Assert.IsTrue(proud.heldItems.Exists(h => h.itemId == "sword_t_proud"));
            var h3 = proud.fateHistory.Find(h => h.stage == "tv.N3");
            Assert.IsFalse(h3.@default);
            StringAssert.StartsWith("PLAYER:m_", h3.cause);
            Assert.IsEmpty(r3.NewFateSection.world.pendingTransitions);
        }

        [Test] public void AC5_WinningBackFavorRevokesAndCoolsDown()
        {
            var s2 = AfterInsult(data, out _);
            var log2 = Log(s2, Player("t_proud", "apology", 3, 2, 3, "courtesy"), Player("t_proud", "praise", 3, 2, 3, "courtesy"));
            var r3 = Settle(data, s2, log2, "tv.N3");
            var s3 = r3.NewFateSection;
            Assert.GreaterOrEqual(s3.Character("t_proud").RelationTo("t_lord").favor, 0);
            Assert.AreEqual("inParty", s3.Character("t_proud").recruitState);
            Assert.IsTrue(Trace(r3, "t_proud").Revoked);
            Assert.IsNotNull(MemoryStore.Find(s3.Character("t_proud"), "r.revoke.t_proud.tv.leave_party"));
            var cooled = s3.world.pendingTransitions.Find(p => p.characterId == "t_proud");
            Assert.IsTrue(cooled.revoked);
            Assert.AreEqual("tv.N4", cooled.cooldownUntil);

            // insulted again: threshold and cause hold, but the cooldown blocks a new announcement at N4
            var r4 = Settle(data, s3, Log(s3, Player("t_proud", "slight2", 3, -2, 3, "insult"), Player("t_proud", "slight3", 3, -2, 3, "insult")), "tv.N4");
            Assert.Less(r4.NewFateSection.Character("t_proud").RelationTo("t_lord").favor, 0);
            Assert.AreEqual("filtered:twoStep", Candidate(r4, "t_proud", "t_proud.tv.leave_party").Status);
            Assert.IsEmpty(r4.Announced);
            // the cooldown entry expires afterwards
            var r5 = Settle(data, r4.NewFateSection, null, "tv.N5");
            Assert.IsFalse(r5.NewFateSection.world.pendingTransitions.Exists(p => p.revoked));
        }

        [Test] public void InPartyCharactersOnlyGetDefaultAndTwoStepOptions()
        {
            var d = TestData(b =>
            {
                var r = Route(b, "t_proud");
                r.genericOptions = new[] { "generic.wander" };
            });
            var s = Start(d);
            var r2 = Settle(d, s, Log(s, Player("t_proud", "slight", 3, -2, 3, "insult")), "tv.N2");
            Assert.AreEqual("filtered:inParty", Candidate(r2, "t_proud", "generic.wander").Status);
            Assert.AreNotEqual("filtered:inParty", Candidate(r2, "t_proud", "t_proud.tv.leave_party").Status);
        }

        [Test] public void DeviationBudgetIsNeverExceeded()
        {
            // Everyone gets a strong cause; the budget (ceil(4×0.15)=1) still allows at most one T1/T2 deviation.
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var s = Start(data, seed);
                s.Character("t_rival").RelationTo("t_lord").favor = 100;
                var log = Log(s, Player("t_loyal", "a", 3, 2, 3, "benevolence"), Player("t_rival", "b", 3, 2, 3, "gift"));
                var s2 = Settle(data, s, log, "tv.N2").NewFateSection;
                var r3 = Settle(data, s2, null, "tv.N3");
                var b = r3.NewFateSection.world.deviationBudget;
                Assert.AreEqual(1, b.max);
                Assert.LessOrEqual(b.used, b.max);
            }
        }

        [Test] public void ProtagonistIsNotSettled()
        {
            var r = Settle(data, Start(data), null, "tv.N2");
            Assert.IsNull(Trace(r, "t_lord"));
        }

        [Test] public void FateQueryReadsState()
        {
            var s = Start(data);
            var s2 = Settle(data, s, Log(s, Player("t_loyal", "fed", 2, 1, 3, "benevolence")), "tv.N2").NewFateSection;
            var q = new FateQuery(s2);
            Assert.AreEqual("v", q.Memory("t_loyal", "fed"));
            Assert.Greater(q.Favor("t_loyal"), 0);
            Assert.Greater(q.View("t_loyal", "benevolence"), 0);
            Assert.IsTrue(q.InParty("t_proud"));
            Assert.AreEqual("t_house", q.Faction("t_loyal"));
            Assert.IsFalse(q.Rescued("rescue.t_doomed.tv.N2"));
        }

        [Test] public void CoreFateUsesNoFloatsEngineOrSystemRandom()
        {
            foreach (var file in Directory.GetFiles("Assets/_Project/Scripts/Core/Fate", "*.cs"))
            {
                var text = File.ReadAllText(file);
                foreach (var banned in new[] { "float ", "double ", "System.Random", "UnityEngine", "Math.Exp", "Math.Pow", "Math.Sqrt" })
                    StringAssert.DoesNotContain(banned, text, $"{Path.GetFileName(file)} uses {banned}");
            }
            StringAssert.Contains("\"noEngineReferences\": true", File.ReadAllText("Assets/_Project/Scripts/Core/ThreeKingdoms.Core.asmdef"));
        }
    }
}
