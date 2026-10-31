using System.Collections;

namespace ThreeKingdoms.Gameplay.Story
{
    // The prologue 天下大势 (user 2026-10-27, design/gdd/prologue.md): a new game from the title plays it before Liu Bei
    // gets control, and lands him at the notice board facing the 募义兵 notice, where reading it starts the meeting with
    // Zhang Fei. The travel menu's 史事 page replays it wherever he stands.
    public sealed partial class TownStoryController
    {
        public const string PrologueEntry = "prologue";

        // Called from Start: true when this town load is the title's 新游戏 and the story has not begun.
        bool TakePrologue()
        {
            if (!GameRun.PrologueDue) return false;
            GameRun.PrologueDue = false;
            return !Run.Story.ZhangFeiJoined && !Run.Story.GuanYuJoined;
        }

        IEnumerator PlayPrologue(bool landAtNotice)
        {
            Busy = true;
            SetControl(false);
            Prompt = null;
            if (landAtNotice)
            {
                // Placed first, so the shot fading out of the last card shows the town already in place.
                if (NoticeSpot) Place(Player.transform, NoticeSpot);
                if (NoticeBoard) StageMoves.Face(Player.transform, NoticeBoard.position);
            }
            Cinematic = true;
            Changed?.Invoke();
            try { yield return StoryCinematics.Prologue(); }
            finally
            {
                Cinematic = false;
                Busy = false;
                SetControl(true);
                Changed?.Invoke();
            }
        }

        public void PlayChronicle(string id)
        {
            if (id != PrologueEntry) return;
            ClosePanel();
            StartCoroutine(PlayPrologue(false));
        }
    }
}
