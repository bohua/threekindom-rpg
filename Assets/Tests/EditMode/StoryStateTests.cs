using NUnit.Framework;
using ThreeKingdoms.Core.Story;

namespace ThreeKingdoms.Tests.EditMode
{
    // story-events GDD rules and 2026-10-19 answers, through StoryState (ADR-0006).
    public sealed class StoryStateTests
    {
        [TestCase(StoryState.GuanYu, StoryState.ZhangFei)]
        [TestCase(StoryState.ZhangFei, StoryState.GuanYu)]
        public void EitherOrderAndTheLastJoinStartsTheOath(string first, string second)
        {
            var s = new StoryState();
            Assert.AreEqual(StoryPhase.FindCompanions, s.Phase);
            Assert.IsFalse(s.Join(first));
            Assert.IsTrue(s.Join(second), "the last join triggers the oath (rule 7)");
            Assert.AreEqual(StoryPhase.Oath, s.Phase);
        }

        [Test]
        public void RepeatedJoinDoesNotRetriggerAfterTheOath()
        {
            var s = new StoryState();
            s.Join(StoryState.GuanYu); s.Join(StoryState.ZhangFei);
            Assert.IsTrue(s.BeginOath());
            Assert.IsFalse(s.BeginOath(), "the trigger is claimed once (rule 9)");
            Assert.IsFalse(s.Join(StoryState.GuanYu));
            s.CompleteOath();
            Assert.IsFalse(s.Join(StoryState.ZhangFei));
            Assert.IsFalse(s.BeginOath(), "never replays once done");
            Assert.AreEqual(StoryPhase.LeaveTown, s.Phase);
        }

        [Test]
        public void OathNeedsBothCompanions()
        {
            var s = new StoryState();
            s.Join(StoryState.GuanYu);
            Assert.IsFalse(s.BeginOath());
            Assert.Throws<System.InvalidOperationException>(() => s.CompleteOath());
            Assert.Throws<System.ArgumentException>(() => s.Join("liu_bei"));
        }

        [Test]
        public void InterruptedOathReplaysFromTheStart()
        {
            var s = new StoryState();
            s.Join(StoryState.GuanYu); s.Join(StoryState.ZhangFei);
            s.BeginOath();
            s.ToSafeState();   // quit / reload mid-oath (rule 11)
            Assert.AreEqual(SequenceState.NotStarted, s.Oath);
            Assert.IsTrue(s.BeginOath());
        }

        [Test]
        public void SouthGateGivesReasonsAndBattlesGoInOrder()
        {
            var s = new StoryState();
            Assert.IsFalse(s.CanLeaveTown(out var reason));
            Assert.AreEqual(StoryState.ReasonNeedCompanions, reason, "a reason, not an invisible wall (rule 12)");
            s.Join(StoryState.GuanYu); s.Join(StoryState.ZhangFei); s.BeginOath(); s.CompleteOath();
            Assert.IsTrue(s.CanLeaveTown(out reason));
            Assert.IsNull(reason);
            Assert.AreEqual("battle_1", s.NextBattleId);

            Assert.IsFalse(s.RecordVictory("battle_2"), "out of order records nothing");
            Assert.IsTrue(s.RecordVictory("battle_1"));
            Assert.IsFalse(s.RecordVictory("battle_1"), "a replayed win is not a second first clear");
            Assert.AreEqual("battle_2", s.NextBattleId);
            Assert.IsTrue(s.RecordVictory("battle_2"));
            Assert.IsTrue(s.RecordVictory("battle_3"));
            Assert.IsNull(s.NextBattleId);
            Assert.AreEqual(StoryPhase.Ending, s.Phase);
            Assert.IsFalse(s.CanLeaveTown(out reason));
            Assert.AreEqual(StoryState.ReasonAllDone, reason);
        }

        [Test]
        public void EndingPlaysOnceAndSurvivesInterruption()
        {
            var s = StoryState.AtBattle(2);
            Assert.IsFalse(s.BeginEnding(), "not before the third battle");
            s.RecordVictory("battle_3");
            Assert.IsTrue(s.BeginEnding());
            s.ToSafeState();
            Assert.IsTrue(s.BeginEnding());
            s.CompleteEnding();
            Assert.AreEqual(SequenceState.Done, s.Ending);
            Assert.IsFalse(s.BeginEnding());
        }

        [Test]
        public void DevelopmentEntryStartsAtTheGivenBattle()
        {
            var s = StoryState.AtBattle(1);
            Assert.IsTrue(s.GuanYuJoined && s.ZhangFeiJoined);
            Assert.AreEqual(SequenceState.Done, s.Oath);
            Assert.AreEqual("battle_2", s.NextBattleId);
            Assert.IsTrue(s.HasJoined("liu_bei"));
        }
    }
}
