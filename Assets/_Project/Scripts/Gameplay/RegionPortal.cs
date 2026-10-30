using System.Linq;
using Jrpg;
using ThreeKingdoms.Gameplay.Story;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThreeKingdoms.Gameplay
{
    // A doorway out of a place (R2 中山: Anxi's south gate ↔ the Zhongshan region; the Hutuo ferry between its two
    // landings). Walking into the box loads TargetScene, or moves the party within this scene when it is empty, and
    // the player appears at the object named ArrivalSpot facing Facing. A portal arms only once the player has been
    // outside it, so arriving on top of the way back does not bounce straight out again.
    public sealed class RegionPortal : MonoBehaviour
    {
        [Tooltip("Box on the ground, metres (x, z), centred on this transform")] public Vector2 Size = new Vector2(6f, 3f);
        [Tooltip("Scene to load; empty moves the party within this scene")] public string TargetScene;
        [Tooltip("Name of the object to stand at in the target scene")] public string ArrivalSpot;
        public Vector3 Facing = Vector3.forward;
        [Tooltip("Chapter steps (ChapterStep values) during which the way is open; empty = always")] public int[] Steps = new int[0];

        static string pendingArrival;
        static Vector3 pendingFacing;
        Transform player;
        bool armed;

        void Start() => player = FindFirstObjectByType<PlayerMover>()?.transform;

        void Update()
        {
            if (!player) return;
            var mover = player.GetComponent<PlayerMover>();
            Vector3 d = transform.InverseTransformPoint(player.position);
            bool inside = Mathf.Abs(d.x) <= Size.x / 2f && Mathf.Abs(d.z) <= Size.y / 2f;
            if (!inside) { armed = true; return; }
            if (!armed || (mover && !mover.InputEnabled) || !Open()) return;
            armed = false;
            if (string.IsNullOrEmpty(TargetScene))
            {
                var spot = GameObject.Find(ArrivalSpot);
                if (spot) Teleport(player, spot.transform.position, Facing);
                return;
            }
            pendingArrival = ArrivalSpot; pendingFacing = Facing;
            SceneManager.LoadScene(TargetScene);
        }

        bool Open()
        {
            if (Steps.Length == 0) return true;
            var run = GameRun.Current;
            return run == null || Steps.Contains((int)run.Chapter2.Step);
        }

        // The arrival spot a portal asked for, once (the place's own start-up uses it in place of its default).
        public static Transform TakeArrival(out Vector3 facing)
        {
            facing = pendingFacing;
            if (pendingArrival == null) return null;
            var spot = GameObject.Find(pendingArrival);
            pendingArrival = null;
            return spot ? spot.transform : null;
        }

        // Scenes without a FieldStoryController (the open regions) take the arrival here, after Awake and before Start.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook() => SceneManager.sceneLoaded += (_, _) =>
        {
            if (pendingArrival == null || FindFirstObjectByType<FieldStoryController>()) return;
            var player = FindFirstObjectByType<PlayerMover>();
            var spot = TakeArrival(out var facing);
            if (player && spot) Teleport(player.transform, spot.position, facing);
        };

        public static void Teleport(Transform player, Vector3 at, Vector3 facing)
        {
            var controller = player.GetComponent<CharacterController>();
            if (controller) controller.enabled = false;
            player.position = at;
            if (controller) controller.enabled = true;
            var trail = player.GetComponent<PartyTrail>();
            if (trail) trail.Reset(-facing);
            foreach (var f in FindObjectsByType<PartyFollower>(FindObjectsSortMode.None))
                if (f.Trail == trail) f.Snap();
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.5f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(new Vector3(0, 1f, 0), new Vector3(Size.x, 2f, Size.y));
        }
    }
}
