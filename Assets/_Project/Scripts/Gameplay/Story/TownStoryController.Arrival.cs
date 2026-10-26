using System.Collections;
using Jrpg;
using UnityEngine;

namespace ThreeKingdoms.Gameplay.Story
{
    // The first arrival in Zhuojun (user 2026-10-26: "第一次来到涿郡时，可以做一个第一视角从南门穿入，一直来到广场，
    // 可以看到远处的山，然后把重要建筑都播放一遍"). A new game opens with an eye-level walk in through the south gate to
    // the market square, the camera lifting to the Yan hills over the commandery, then one short shot of each landmark
    // with a subtitle. Confirm moves on to the next shot. Saves, returns from battle and the tavern never replay it.
    public sealed partial class TownStoryController
    {
        // Off for automated tests and screenshot launches that place the player themselves.
        public static bool PlayArrivalIntro = true;

        public bool ArrivalPlaying { get; private set; }
        public int ArrivalShotIndex { get; private set; } = -1;

        // A landmark shot: the camera eases in along its line of sight toward `look`, from `pitch` degrees above and
        // `distance` metres back (a little further at the start, so it drifts closer).
        readonly struct LandmarkShot
        {
            public readonly string Line; public readonly Vector3 Look; public readonly float Pitch, Distance, Yaw;
            public LandmarkShot(string line, Vector3 look, float pitch, float distance, float yaw = 0f) { Line = line; Look = look; Pitch = pitch; Distance = distance; Yaw = yaw; }
        }

        // Positions follow production/levels/zhuojun/layout.md (fronts face south, the camera stays inside the walls).
        static readonly LandmarkShot[] Landmarks =
        {
            new LandmarkShot("arrival.office", new Vector3(0f, 4.5f, 24f), 12f, 20f),
            new LandmarkShot("arrival.tavern", new Vector3(-21f, 1.8f, -4f), 14f, 13f, -8f),
            new LandmarkShot("arrival.smithy", new Vector3(27f, 1.8f, -4f), 14f, 12f, 8f),
            new LandmarkShot("arrival.casino", new Vector3(38.5f, 1.8f, -4f), 14f, 12f, 10f),
            new LandmarkShot("arrival.clinic", new Vector3(27f, 1.6f, -18.5f), 16f, 11f, 6f),
            new LandmarkShot("arrival.post", new Vector3(33f, 1.6f, -29.5f), 52f, 9f, 4f),
            new LandmarkShot("arrival.barracks", new Vector3(31f, 1.4f, 24f), 30f, 22f, 6f),
            new LandmarkShot("arrival.granary", new Vector3(-32f, 2.6f, 19f), 16f, 15f, -6f),
            new LandmarkShot("arrival.grove", new Vector3(-66f, 1.8f, 0f), 18f, 22f, -12f),
        };

        public const float WalkInSeconds = 11f, HillsSeconds = 4.5f, LandmarkSeconds = 3.8f;

        IEnumerator PlayArrival()
        {
            Busy = true;
            ArrivalPlaying = true;
            SetControl(false);
            Prompt = null;
            var cam = Camera ? Camera.transform : null;
            try
            {
                if (cam == null) yield break;
                Camera.Paused = true;
                Letterbox = true;
                Changed?.Invoke();

                // Eye level through the gate passage and up the stone axis, slowing into the square; the gaze lifts
                // to the hills over the commandery at the end.
                ArrivalShotIndex = 0;
                Subtitle("arrival.1");
                Vector3 from = new Vector3(0f, 1.7f, -62f), to = new Vector3(0f, 2.4f, -11f);   // stops at the square's south edge, where the hills clear the hall roof
                yield return Shot(WalkInSeconds, t =>
                {
                    float e = Mathf.SmoothStep(0f, 1f, t);
                    var pos = Vector3.Lerp(from, to, e) + Vector3.up * Mathf.Sin(t * Mathf.PI * 14f) * 0.03f * (1f - e);   // a light step bob
                    var ahead = pos + Vector3.forward * 30f;
                    var lookAt = Vector3.Lerp(ahead, new Vector3(0f, 22f, 120f), Mathf.SmoothStep(0.55f, 1f, t));
                    cam.SetPositionAndRotation(pos, Quaternion.LookRotation(lookAt - pos));
                    Camera.FocusAt(Mathf.Lerp(20f, 110f, t));
                });
                ArrivalShotIndex = 1;
                Subtitle("arrival.2");
                yield return Shot(HillsSeconds, t =>
                {
                    // A crane up over the commandery roof until the hills stand clear behind it.
                    float e = Mathf.SmoothStep(0f, 1f, t);
                    var pos = to + new Vector3(0f, 8f * e, -3f * e);
                    cam.SetPositionAndRotation(pos, Quaternion.LookRotation(new Vector3(0f, 22f - 4f * e, 120f) - pos));
                    Camera.FocusAt(110f);
                });

                for (int i = 0; i < Landmarks.Length; i++)
                {
                    var s = Landmarks[i];
                    ArrivalShotIndex = 2 + i;
                    yield return FadeTo(1f);
                    Subtitle(s.Line);
                    var rot = Quaternion.Euler(s.Pitch, s.Yaw, 0f);
                    void Place(float t)
                    {
                        float d = Mathf.Lerp(s.Distance + 3f, s.Distance, Mathf.SmoothStep(0f, 1f, t));
                        cam.SetPositionAndRotation(s.Look + rot * new Vector3(0, 0, -d), rot);
                        Camera.FocusAt(d);
                    }
                    Place(0f);
                    yield return FadeTo(0f);
                    yield return Shot(LandmarkSeconds, Place);
                }

                ArrivalShotIndex = -1;
                Line = null;
                yield return FadeTo(1f);
            }
            finally
            {
                ArrivalShotIndex = -1;
                ArrivalPlaying = false;
                Line = null;
                Letterbox = false;
                if (Camera) { Camera.Paused = false; Camera.Target = Player.transform; Camera.Snap(); }
                Busy = false;
                SetControl(true);
                Changed?.Invoke();
            }
            yield return FadeTo(0f);
        }

        void Subtitle(string key)
        {
            Line = new DialogueLine("narrator", key);
            Changed?.Invoke();
        }

        // Runs `frame` with t from 0 to 1 over `seconds`; Confirm (after a short moment) ends the shot early.
        IEnumerator Shot(float seconds, System.Action<float> frame)
        {
            advance = false;
            float started = Time.time;
            frame(0f);
            yield return null;
            while (Time.time - started < seconds)
            {
                if (advance && Time.time - started >= MinBeatSeconds) break;
                frame(Mathf.Clamp01((Time.time - started) / seconds));
                yield return null;
            }
            frame(1f);
            advance = false;
        }
    }
}
