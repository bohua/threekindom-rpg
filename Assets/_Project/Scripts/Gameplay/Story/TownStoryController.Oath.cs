using System;
using System.Collections;
using Jrpg;
using UnityEngine;

namespace ThreeKingdoms.Gameplay.Story
{
    // The peach-garden oath as an automatic scene of about 30 seconds (user 2026-10-24, design/gdd/story-cinematics.md
    // §3.1): the three walk up to the altar, talk, light incense and kneel, speak the oath (演义 第1回), rise and share
    // wine, Guan Yu and Zhang Fei salute their elder brother, the camera pulls back, and the moment freezes into the
    // ink scroll. Lines advance by time; confirm moves on to the next beat early, but the scene cannot be skipped
    // (story-events answer 9). Triggering, the replay after a reload and the end state are unchanged (V1_SPEC §2.1).
    public sealed partial class TownStoryController
    {
        // Story poses drawn after the walk cycle in each brother's atlas (PixelWalkFrames.StoryPoseStart + pose), all
        // facing the camera with the altar between them and the viewer. Without them the brothers keep standing.
        public enum OathPose { Salute = 0, Incense = 1, KneelIncense = 2, Kowtow = 3, Toast = 4, BowToElder = 5 }

        public enum OathAct { WalkIn, FaceEachOther, Laugh, Salute, Kneel, Recite, Kowtow, Toast, BowToElder, PullBack }

        public readonly struct OathBeat
        {
            public readonly string Speaker, Line;
            public readonly float Seconds;
            public readonly OathAct Act;
            public OathBeat(string speaker, string line, float seconds, OathAct act) { Speaker = speaker; Line = line; Seconds = seconds; Act = act; }
        }

        // About 32 seconds in all; the lines are in StoryText (oath.1–oath.10).
        public static readonly OathBeat[] OathBeats =
        {
            new OathBeat("narrator", "oath.1", 4f, OathAct.WalkIn),
            new OathBeat("liu_bei", "oath.2", 3f, OathAct.FaceEachOther),
            new OathBeat("zhang_fei", "oath.3", 2.5f, OathAct.Laugh),
            new OathBeat("guan_yu", "oath.4", 3f, OathAct.Salute),
            new OathBeat("narrator", "oath.5", 3f, OathAct.Kneel),
            new OathBeat("brothers", "oath.6", 5f, OathAct.Recite),
            new OathBeat("brothers", "oath.7", 4.5f, OathAct.Kowtow),
            new OathBeat("liu_bei", "oath.8", 2.5f, OathAct.Toast),
            new OathBeat("zhang_fei", "oath.9", 2.5f, OathAct.BowToElder),
            new OathBeat("liu_bei", "oath.10", 2.5f, OathAct.PullBack),
        };

        [Header("Peach-garden oath (user 2026-10-24)")]
        [Tooltip("Close shot on the altar while the oath plays; switched on only then")] public CameraZone OathShot;
        [Tooltip("Where the three come from, walking up the grove path to their places")] public Vector3 OathEntrance = new Vector3(-58f, 0, -3f);
        [Tooltip("The altar, incense and wine in front of the three; shown during the oath")] public GameObject OathAltar;
        [Tooltip("Falling peach petals during the oath")] public GameObject OathPetals;
        [Tooltip("Distance the camera pulls back to in the last beat")] public float PullBackDistance = 30f;
        [Tooltip("Shortest time a line stays up before confirm can move on")] public float MinBeatSeconds = 0.4f;

        // The beat now playing (-1 outside the oath).
        public int OathBeatIndex { get; private set; } = -1;
        // True while a full-screen cinematic (the ink scroll) holds the town.
        public bool Cinematic { get; private set; }
        // While the oath plays, dialogue shows as subtitles over a letterboxed picture (TownHud).
        public bool Letterbox { get; private set; }

        IEnumerator PlayOath()
        {
            if (!Run.Story.BeginOath()) yield break;
            Busy = true;
            SetControl(false);
            Prompt = null;
            Changed?.Invoke();
            float shotDistance = OathShot ? OathShot.Distance : 0f;
            try
            {
                yield return FadeTo(1f);
                SetFollowers(false);
                var liu = Player.transform;
                Vector3 liuSpot = GroveSpots.Length > 0 ? GroveSpots[0].position : GroveFocus.position;
                Vector3 guanSpot = GroveSpots.Length > 1 ? GroveSpots[1].position : liuSpot + Vector3.left * 1.8f;
                Vector3 zhangSpot = GroveSpots.Length > 2 ? GroveSpots[2].position : liuSpot + Vector3.right * 1.8f;
                // In single file up the grove path, Liu Bei in front.
                StagePlace(liu, OathEntrance);
                if (GuanYu) { StagePlace(GuanYu, OathEntrance + new Vector3(1.4f, 0, -0.4f)); GuanYu.gameObject.SetActive(true); }
                if (ZhangFei) { StagePlace(ZhangFei, OathEntrance + new Vector3(2.8f, 0, -0.8f)); ZhangFei.gameObject.SetActive(true); }
                if (OathAltar) OathAltar.SetActive(true);
                if (OathPetals) OathPetals.SetActive(true);
                if (OathShot) OathShot.gameObject.SetActive(true);
                if (Camera && GroveFocus) { Camera.Target = GroveFocus; Camera.Snap(); }
                var seeThrough = Camera ? Camera.GetComponent<SeeThroughOccluders>() : null;
                if (seeThrough) { if (GuanYu) seeThrough.AlsoVisible.Add(GuanYu); if (ZhangFei) seeThrough.AlsoVisible.Add(ZhangFei); }
                Letterbox = true;
                StoryMusic.Play(StoryMusic.OathCue);
                yield return FadeTo(0f);

                for (int i = 0; i < OathBeats.Length; i++)
                {
                    var beat = OathBeats[i];
                    OathBeatIndex = i;
                    Line = new DialogueLine(beat.Speaker, beat.Line);
                    advance = false;
                    Changed?.Invoke();
                    var act = StartCoroutine(Act(beat.Act, liuSpot, guanSpot, zhangSpot));
                    float started = Time.time;
                    yield return null;
                    while (Time.time - started < beat.Seconds && !(advance && Time.time - started >= MinBeatSeconds)) yield return null;
                    if (act != null) StopCoroutine(act);   // null when the act finished at once
                    foreach (var w in stageWalks) if (w != null) StopCoroutine(w);
                    stageWalks.Clear();
                    // A stopped walk never reaches its finally: switch the player's controller back on here.
                    if (Player.TryGetComponent<CharacterController>(out var cc)) cc.enabled = true;
                    if (beat.Act == OathAct.WalkIn) { StagePlace(liu, liuSpot); StagePlace(GuanYu, guanSpot); StagePlace(ZhangFei, zhangSpot); FaceCamera(); }
                }
                OathBeatIndex = -1;
                Line = null;
                Letterbox = false;
                Changed?.Invoke();

                // Bullet time: the last frame of the oath freezes into the ink scroll (design §3.3).
                StoryMusic.Stop(0.3f);
                Cinematic = true;
                Changed?.Invoke();
                yield return StoryCinematics.Scroll(StoryCinematics.OathScroll);
                Cinematic = false;

                yield return FadeTo(1f);
                Pose(liu, -1); Pose(GuanYu, -1); Pose(ZhangFei, -1);
                if (GuanYu) GuanYu.gameObject.SetActive(false);
                if (ZhangFei) ZhangFei.gameObject.SetActive(false);
                if (OathAltar) OathAltar.SetActive(false);
                if (OathPetals) OathPetals.SetActive(false);
                if (Camera && Camera.TryGetComponent<SeeThroughOccluders>(out var see)) see.AlsoVisible.Clear();
                if (OathShot) { OathShot.Distance = shotDistance; OathShot.gameObject.SetActive(false); }
                Place(Player.transform, OathEndSpot);
                if (Camera) { Camera.Target = Player.transform; Camera.Snap(); }
                ShowFollowers(Vector3.left);   // control resumes inside the west gate, the brothers behind
                Run.Story.CompleteOath();
                ShowBlockers();
                yield return FadeTo(0f);
            }
            finally
            {
                OathBeatIndex = -1;
                Line = null;
                Letterbox = false;
                Cinematic = false;
                Fade = 0f;
                Pose(Player ? Player.transform : null, -1); Pose(GuanYu, -1); Pose(ZhangFei, -1);
                if (OathShot) { OathShot.Distance = shotDistance; OathShot.gameObject.SetActive(false); }
                if (Camera) Camera.Target = Player.transform;
                Busy = false;
                SetControl(true);
                Changed?.Invoke();
            }
        }

        // What the three do during one beat.
        IEnumerator Act(OathAct act, Vector3 liuSpot, Vector3 guanSpot, Vector3 zhangSpot)
        {
            var liu = Player.transform;
            switch (act)
            {
                case OathAct.WalkIn:
                    // Liu Bei first, the others a step behind, each to his place behind the altar.
                    stageWalks.Add(StartCoroutine(StageMoves.Walk(GuanYu, new[] { OathEntrance + new Vector3(-0.6f, 0, 0), guanSpot }, 2.6f)));
                    stageWalks.Add(StartCoroutine(StageMoves.Walk(ZhangFei, new[] { OathEntrance + new Vector3(0.4f, 0, -0.4f), zhangSpot }, 3.0f)));
                    yield return WalkPlayer(liu, liuSpot, 2.6f);
                    FaceCamera();
                    break;
                case OathAct.FaceEachOther:
                    StageMoves.Face(liu, liu.position + Vector3.left); yield return Wait(0.9f);
                    StageMoves.Face(liu, liu.position + Vector3.right); yield return Wait(0.9f);
                    FaceCamera();
                    break;
                case OathAct.Laugh:
                    StageMoves.Face(ZhangFei, ZhangFei ? ZhangFei.position + Vector3.left : Vector3.zero);
                    yield return Hop(ZhangFei, 2, 0.18f);
                    FaceCamera();
                    break;
                case OathAct.Salute:
                    Pose(GuanYu, (int)OathPose.Salute);
                    yield return Wait(0.8f);
                    Pose(liu, (int)OathPose.Salute); Pose(ZhangFei, (int)OathPose.Salute);
                    break;
                case OathAct.Kneel:
                    AllPose(OathPose.Incense);
                    yield return Wait(1.2f);
                    AllPose(OathPose.KneelIncense);
                    break;
                case OathAct.Recite:
                    AllPose(OathPose.KneelIncense);
                    break;
                case OathAct.Kowtow:
                    for (int i = 0; i < 2; i++)
                    {
                        AllPose(OathPose.Kowtow); yield return Wait(1.0f);
                        AllPose(OathPose.KneelIncense); yield return Wait(0.6f);
                    }
                    AllPose(OathPose.Kowtow);
                    break;
                case OathAct.Toast:
                    AllPose(OathPose.Toast);
                    yield return Hop(liu, 1, 0.08f);
                    break;
                case OathAct.BowToElder:
                    Pose(liu, (int)OathPose.Toast);
                    Pose(GuanYu, (int)OathPose.BowToElder); Pose(ZhangFei, (int)OathPose.BowToElder);
                    break;
                case OathAct.PullBack:
                    AllPose(OathPose.Salute);
                    if (!OathShot) break;
                    float from = OathShot.Distance;
                    for (float t = 0; t < 2.4f; t += Time.deltaTime)
                    {
                        OathShot.Distance = Mathf.Lerp(from, PullBackDistance, Mathf.SmoothStep(0, 1, t / 2.4f));
                        yield return null;
                    }
                    break;
            }
        }

        readonly System.Collections.Generic.List<Coroutine> stageWalks = new System.Collections.Generic.List<Coroutine>();

        void AllPose(OathPose pose) { Pose(Player.transform, (int)pose); Pose(GuanYu, (int)pose); Pose(ZhangFei, (int)pose); }

        // The pose a brother shows, or -1 to walk and stand normally.
        static void Pose(Transform who, int pose)
        {
            if (!who) return;
            var walk = who.GetComponent<PixelWalkFrames>();
            if (walk) { walk.PoseOverride = pose; walk.StageLift = 0f; }
        }

        public static int ShownPose(Transform who) => who && who.GetComponent<PixelWalkFrames>() is PixelWalkFrames w ? w.PoseOverride : -1;

        void FaceCamera()
        {
            foreach (var who in new[] { Player ? Player.transform : null, GuanYu, ZhangFei })
                if (who) StageMoves.Face(who, who.position + Vector3.back);
        }

        // The player's character walks a stage path: its controller is off meanwhile so the move is not overridden.
        static IEnumerator WalkPlayer(Transform who, Vector3 to, float speed)
        {
            var controller = who.GetComponent<CharacterController>();
            if (controller) controller.enabled = false;
            try { yield return StageMoves.Walk(who, new[] { to }, speed); }
            finally { if (controller) controller.enabled = true; }
        }

        static void StagePlace(Transform who, Vector3 at) { if (who) StageMoves.Place(who, at); }

        static IEnumerator Wait(float seconds)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime) yield return null;
        }

        // A small jump or two in place (laughing, raising the bowls).
        static IEnumerator Hop(Transform who, int times, float height)
        {
            var walk = who ? who.GetComponent<PixelWalkFrames>() : null;
            if (!walk) yield break;
            for (int i = 0; i < times; i++)
                for (float t = 0; t < 0.28f; t += Time.deltaTime)
                {
                    walk.StageLift = Mathf.Sin(t / 0.28f * Mathf.PI) * height;
                    yield return null;
                }
            walk.StageLift = 0f;
        }
    }
}
