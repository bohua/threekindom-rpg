using System.Collections.Generic;
using NUnit.Framework;
using ThreeKingdoms.Core.Fate;
using ThreeKingdoms.Data.Fate;
using static ThreeKingdoms.Core.Fate.Condition;

namespace ThreeKingdoms.Tests.Fate
{
    // S3: every predicate of spec §4.4, one positive and one negative case each.
    public sealed class FateConditionTests
    {
        FateDataSet data;
        FateSaveSection s;
        FakeContext ctx;

        [SetUp] public void Setup()
        {
            data = FateTestKit.TestData();
            s = FateTestKit.Start(data);
            ctx = new FakeContext();
            var proud = s.Character("t_proud");
            var rel = proud.RelationTo("t_lord");
            rel.favor = 30; rel.trust = 40; rel.views[ViewDims.IndexOf("valor")] = 55;
            proud.relations["t_rival"].favor = 80;
            MemoryStore.Put(proud, new MemoryEntry { who = "t_proud", key = "duel", value = "won", kind = MemoryKinds.Player, seq = 1 });
            proud.heldItems.Add(new HeldItem { itemId = "book_t_strategy", origin = "player_given" });
            proud.presentAt.Add("battle_t2");
        }

        bool Eval(Condition c, bool fulfillment = false) => ConditionEvaluator.Eval(c, s, ctx, data, fulfillment);

        [Test] public void AllAnyNot()
        {
            var t = Of("alive", "t_proud");
            var f = Of("alive", "nobody");
            Assert.IsTrue(Eval(All(t, t))); Assert.IsFalse(Eval(All(t, f)));
            Assert.IsTrue(Eval(Any(f, t))); Assert.IsFalse(Eval(Any(f, f)));
            Assert.IsTrue(Eval(Not(f))); Assert.IsFalse(Eval(Not(t)));
        }

        [Test] public void NodeReached()
        {
            Assert.IsTrue(Eval(Of("nodeReached", "tv.N1")));
            Assert.IsFalse(Eval(Of("nodeReached", "tv.N2")));
        }

        [Test] public void FlagAndFlagValue()
        {
            ctx.Flags.Add("t_relief_ready"); ctx.Values["route"] = "east";
            Assert.IsTrue(Eval(Of("flag", "t_relief_ready"))); Assert.IsFalse(Eval(Of("flag", "other")));
            Assert.IsTrue(Eval(Of("flagValue", "route", "east"))); Assert.IsFalse(Eval(Of("flagValue", "route", "west")));
        }

        [Test] public void MemoryWithAndWithoutValue()
        {
            Assert.IsTrue(Eval(Of("memory", "t_proud", "duel"))); Assert.IsTrue(Eval(Of("memory", "t_proud", "duel", "won")));
            Assert.IsFalse(Eval(Of("memory", "t_proud", "duel", "lost"))); Assert.IsFalse(Eval(Of("memory", "t_proud", "none")));
        }

        [Test] public void FavorAndTrustAtLeast()
        {
            Assert.IsTrue(Eval(Of("favorAtLeast", "t_proud", "30"))); Assert.IsFalse(Eval(Of("favorAtLeast", "t_proud", "31")));
            Assert.IsTrue(Eval(Of("trustAtLeast", "t_proud", "40"))); Assert.IsFalse(Eval(Of("trustAtLeast", "t_proud", "41")));
        }

        [Test] public void ViewAtLeast()
        {
            Assert.IsTrue(Eval(Of("viewAtLeast", "t_proud", "valor", "55"))); Assert.IsFalse(Eval(Of("viewAtLeast", "t_proud", "deceit", "1")));
        }

        [Test] public void RelationAtLeast()
        {
            Assert.IsTrue(Eval(Of("relationAtLeast", "t_proud", "t_rival", "80"))); Assert.IsFalse(Eval(Of("relationAtLeast", "t_proud", "t_rival", "81")));
        }

        [Test] public void AliveExcludesPendingDeath()
        {
            Assert.IsTrue(Eval(Of("alive", "t_doomed")));
            s.Character("t_doomed").pendingDeath = true;
            Assert.IsFalse(Eval(Of("alive", "t_doomed")));
            Assert.IsNull(new FateQuery(s).Location("t_doomed"));
        }

        [Test] public void FactionLocationRecruitState()
        {
            Assert.IsTrue(Eval(Of("faction", "t_proud", "t_lord_band"))); Assert.IsFalse(Eval(Of("faction", "t_proud", "t_house")));
            Assert.IsTrue(Eval(Of("location", "t_proud", "t_camp"))); Assert.IsFalse(Eval(Of("location", "t_proud", "t_city")));
            Assert.IsTrue(Eval(Of("recruitState", "t_proud", "inParty"))); Assert.IsFalse(Eval(Of("recruitState", "t_proud", "known")));
        }

        [Test] public void PresentAt()
        {
            Assert.IsTrue(Eval(Of("presentAt", "t_proud", "battle_t2"))); Assert.IsFalse(Eval(Of("presentAt", "t_proud", "battle_t_siege")));
        }

        [Test] public void FactionDestroyed()
        {
            ctx.Destroyed.Add("t_house");
            Assert.IsTrue(Eval(Of("factionDestroyed", "t_house"))); Assert.IsFalse(Eval(Of("factionDestroyed", "t_rival_band")));
        }

        [Test] public void HoldsPost()
        {
            Assert.IsTrue(Eval(Of("holdsPost", "t_doomed", "t_post_a"))); Assert.IsFalse(Eval(Of("holdsPost", "t_heir", "t_post_a")));
        }

        [Test] public void HasItemCharacterHoldsHasBook()
        {
            ctx.Items.Add("book_t_strategy");
            Assert.IsTrue(Eval(Of("hasItem", "book_t_strategy"))); Assert.IsFalse(Eval(Of("hasItem", "sword_t_proud")));
            Assert.IsTrue(Eval(Of("characterHolds", "t_proud", "sword_t_proud"))); Assert.IsFalse(Eval(Of("characterHolds", "t_loyal", "sword_t_proud")));
            Assert.IsTrue(Eval(Of("hasBook", "t_proud", "book_t_strategy"))); Assert.IsFalse(Eval(Of("hasBook", "t_loyal", "book_t_strategy")));
        }

        [Test] public void ReputationAndEventSeen()
        {
            ctx.Rep = 20; ctx.Seen.Add("h.t_war");
            Assert.IsTrue(Eval(Of("reputationAtLeast", "20"))); Assert.IsFalse(Eval(Of("reputationAtLeast", "21")));
            Assert.IsTrue(Eval(Of("eventSeen", "h.t_war"))); Assert.IsFalse(Eval(Of("eventSeen", "h.t_flood")));
        }

        [Test] public void BattleObjectiveOnlyInFulfillment()
        {
            ctx.Objectives.Add("t_breakout");
            Assert.IsFalse(Eval(Of(Predicates.BattleObjective, "t_breakout")), "outside fulfillment it never passes");
            Assert.IsTrue(Eval(Of(Predicates.BattleObjective, "t_breakout"), true));
            Assert.IsFalse(Eval(Of(Predicates.BattleObjective, "other"), true));
        }

        [Test] public void UnknownPredicateNeverPassesAndV02RejectsIt()
        {
            Assert.IsFalse(Eval(Of("isHero", "t_proud")));
            var issues = new List<ValidationIssue>();
            FateValidator.CheckCondition(Of("isHero", "t_proud"), "test", false, issues);
            Assert.AreEqual("V-02", issues[0].Code);
        }

        [Test] public void LoaderParsesConditionJsonAndRejectsFloats()
        {
            var c = ConditionJsonConverter.Parse(Newtonsoft.Json.Linq.JToken.Parse(
                "{\"all\":[{\"flag\":\"x\"},{\"not\":{\"favorAtLeast\":[\"t_proud\",50]}}]}"));
            Assert.AreEqual("all", c.op);
            Assert.AreEqual("not", c.children[1].op);
            Assert.AreEqual(new[] { "t_proud", "50" }, c.children[1].children[0].args);
            Assert.Throws<FateDataException>(() => ConditionJsonConverter.Parse(Newtonsoft.Json.Linq.JToken.Parse("{\"favorAtLeast\":[\"t_proud\",1.5]}")));
            Assert.Throws<FateDataException>(() => ConditionJsonConverter.Parse(Newtonsoft.Json.Linq.JToken.Parse("{\"flag\":\"a\",\"alive\":\"b\"}")));
            var json = FateJsonLoader.ToJson(c, false);
            Assert.AreEqual("{\"all\":[{\"flag\":\"x\"},{\"not\":{\"favorAtLeast\":[\"t_proud\",50]}}]}", json);
        }

        [Test] public void LoaderRejectsNonIntegerNumbersInDtos()
        {
            Assert.Throws<FateDataException>(() => FateJsonLoader.LoadBundle(n => n == "tuning.json" ? "{\"tau\": 0.1}" : null));
        }
    }
}
