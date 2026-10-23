using System.Collections;
using UnityEngine;

namespace ThreeKingdoms.Gameplay.Story
{
    // Scripted movement for story scenes: a character walks a path at a steady pace (PixelWalkFrames plays the
    // walk from the motion) and can be turned to face a point when it stops.
    public static class StageMoves
    {
        public const float WalkSpeed = 3.6f;

        public static IEnumerator Walk(Transform who, Vector3[] path, float speed = WalkSpeed)
        {
            if (!who) yield break;
            foreach (var to in path)
            {
                var target = new Vector3(to.x, who.position.y, to.z);
                while ((who.position - target).sqrMagnitude > 0.0004f)
                {
                    who.position = Vector3.MoveTowards(who.position, target, speed * Time.deltaTime);
                    yield return null;
                }
            }
            yield return null;   // one still frame so the walk settles on the standing pose
        }

        public static void Face(Transform who, Vector3 point)
        {
            if (!who) return;
            var walk = who.GetComponent<PixelWalkFrames>();
            if (walk) walk.Face(point - who.position);
        }

        public static void Place(Transform who, Vector3 at)
        {
            if (!who) return;
            var controller = who.GetComponent<CharacterController>();
            if (controller) controller.enabled = false;
            who.position = at;
            if (controller) controller.enabled = true;
        }
    }
}
