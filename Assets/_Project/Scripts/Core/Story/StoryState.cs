using System;

namespace ThreeKingdoms.Core.Story
{
    // Three-state scripted sequence (oath, ending): only NotStarted and Done are safe to save.
    public enum SequenceState { NotStarted, Playing, Done }

    public enum StoryPhase { FindCompanions, Oath, LeaveTown, Ending }

    // Main-line flags and phase rules from story-events GDD (ADR-0006 §1). Pure C#: no engine objects, so the save
    // system (ADR-0004) can serialise it and every rule is EditMode-testable.
    public sealed class StoryState
    {
        public const string GuanYu = "guan_yu", ZhangFei = "zhang_fei";
        public const int BattleCount = 3;
        public const string ReasonNeedCompanions = "need_companions", ReasonAllDone = "all_done";

        public bool GuanYuJoined { get; private set; }
        public bool ZhangFeiJoined { get; private set; }
        public SequenceState Oath { get; private set; }
        public int BattlesDone { get; private set; }
        public SequenceState Ending { get; private set; }

        public StoryPhase Phase =>
            BattlesDone >= BattleCount ? StoryPhase.Ending :
            Oath == SequenceState.Done ? StoryPhase.LeaveTown :
            GuanYuJoined && ZhangFeiJoined ? StoryPhase.Oath : StoryPhase.FindCompanions;

        // Development / test entry straight into a battle: both companions joined, oath done, battlesDone won.
        public static StoryState AtBattle(int battlesDone) => new StoryState
        {
            GuanYuJoined = true, ZhangFeiJoined = true, Oath = SequenceState.Done,
            BattlesDone = System.Math.Clamp(battlesDone, 0, BattleCount),
        };

        // A saved play-through (ADR-0004). Sequences cut off mid-play come back as not started (rule 11).
        public static StoryState Restore(bool guanYuJoined, bool zhangFeiJoined, SequenceState oath, int battlesDone, SequenceState ending)
        {
            var s = new StoryState
            {
                GuanYuJoined = guanYuJoined, ZhangFeiJoined = zhangFeiJoined, Oath = oath,
                BattlesDone = System.Math.Clamp(battlesDone, 0, BattleCount), Ending = ending,
            };
            if (s.Oath != SequenceState.NotStarted && !(guanYuJoined && zhangFeiJoined)) throw new ArgumentException("桃园结义需要关张都已入队。");
            s.ToSafeState();
            return s;
        }

        // Encounter IDs match the battle data (ADR-0002); null once all three are won.
        public string NextBattleId => BattlesDone < BattleCount ? $"battle_{BattlesDone + 1}" : null;

        public bool HasJoined(string generalId) =>
            generalId == GuanYu ? GuanYuJoined : generalId == ZhangFei ? ZhangFeiJoined : generalId == "liu_bei";

        // Written when the meeting dialogue ends (GDD: quitting mid-dialogue leaves them unjoined). Order is free
        // (GDD answer 1). Returns true when this join completes the pair and the oath should start now (rule 7).
        public bool Join(string generalId)
        {
            if (generalId == GuanYu) GuanYuJoined = true;
            else if (generalId == ZhangFei) ZhangFeiJoined = true;
            else throw new ArgumentException($"{generalId} 不是可入队的同伴。");
            return GuanYuJoined && ZhangFeiJoined && Oath == SequenceState.NotStarted;
        }

        // Claims the trigger so the oath cannot start twice (rule 9).
        public bool BeginOath()
        {
            if (!(GuanYuJoined && ZhangFeiJoined) || Oath != SequenceState.NotStarted) return false;
            Oath = SequenceState.Playing;
            return true;
        }

        // Only marks the oath done: no recruiting, no starting gear, no duplicated members (rule 9).
        public void CompleteOath()
        {
            if (Oath != SequenceState.Playing) throw new InvalidOperationException("桃园结义没有在播放。");
            Oath = SequenceState.Done;
        }

        // A sequence cut off by a reload or quit replays from its start (rule 11). Called on every town load and by
        // the save system before writing (ADR-0004).
        public void ToSafeState()
        {
            if (Oath == SequenceState.Playing) Oath = SequenceState.NotStarted;
            if (Ending == SequenceState.Playing) Ending = SequenceState.NotStarted;
        }

        // The south gate gives a clear reason instead of an invisible wall (rule 12).
        public bool CanLeaveTown(out string reasonKey)
        {
            reasonKey = Oath != SequenceState.Done ? ReasonNeedCompanions : NextBattleId == null ? ReasonAllDone : null;
            return reasonKey == null;
        }

        // Battles unlock in order (rule 14); only the expected encounter advances. Returns true when the main line
        // moved on (a replayed or out-of-order win records nothing).
        public bool RecordVictory(string encounterId)
        {
            if (encounterId == null || encounterId != NextBattleId) return false;
            BattlesDone++;
            return true;
        }

        public bool BeginEnding()
        {
            if (Phase != StoryPhase.Ending || Ending != SequenceState.NotStarted) return false;
            Ending = SequenceState.Playing;
            return true;
        }

        public void CompleteEnding()
        {
            if (Ending != SequenceState.Playing) throw new InvalidOperationException("结尾没有在播放。");
            Ending = SequenceState.Done;
        }
    }
}
