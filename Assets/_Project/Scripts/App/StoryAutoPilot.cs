using System.Collections;
using Jrpg;
using ThreeKingdoms.Gameplay.Battle;
using ThreeKingdoms.Gameplay.Story;
using UnityEngine;

namespace ThreeKingdoms.App
{
    // Development only (-storyAuto): plays the main line by itself — reads the notice (Zhang Fei), goes into the tavern
    // (Guan Yu), watches the oath,
    // leaves by the south gate, and after each won battle returns to town. Battles are commanded by BattleAutoPilot.
    public sealed class StoryAutoPilot : MonoBehaviour
    {
        public float LineSeconds = 1.2f;
        float nextAdvance;

        void Update()
        {
            var town = FindAnyObjectByType<TownStoryController>();
            if (town && town.Run != null) { DriveTown(town); return; }
            var tavern = FindAnyObjectByType<TavernStage>();
            if (tavern) { if (Time.time >= nextAdvance) { nextAdvance = Time.time + LineSeconds; tavern.Advance(); } return; }
            var battle = FindAnyObjectByType<BattleController>();
            if (battle && battle.Current == BattleController.Phase.Finished && battle.CanReturnToTown && Time.time > nextAdvance + 2f)
                battle.ReturnToTown();
            else if (battle && battle.Current != BattleController.Phase.Finished) nextAdvance = Time.time;
        }

        void DriveTown(TownStoryController c)
        {
            if (Time.time < nextAdvance) return;
            nextAdvance = Time.time + LineSeconds;
            if (c.Choices != null) { c.Choose(0); return; }
            if (c.Busy) { c.Advance(); return; }
            var s = c.Run.Story;
            if (!s.ZhangFeiJoined) Go(c, c.NoticeSpot.position);
            else if (!s.GuanYuJoined) Go(c, c.TavernDoor.position + new Vector3(1.2f, 0, -1.0f));
            else if (s.NextBattleId != null) Go(c, new Vector3(0, 0, -36f));
        }

        static void Go(TownStoryController c, Vector3 at)
        {
            var cc = c.Player.GetComponent<CharacterController>();
            cc.enabled = false; c.Player.transform.position = at; cc.enabled = true;
            c.Interact();
        }
    }
}
