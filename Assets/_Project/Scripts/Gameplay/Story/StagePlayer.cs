using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Story;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ThreeKingdoms.Gameplay.Story
{
    // How a scene shows the actors of a cutscene: sprites on the story stage, characters standing in a walkable scene.
    public interface IStageActors
    {
        void Background(string id);
        void Show(string id, string sprite, float x, float y, string facing);
        IEnumerator Move(string id, float x, float y, float seconds);
        void Face(string id, string facing);
        void Hide(string id);
        void Shake(float seconds);
        void Flash();
        void Pose(string id, int pose, bool flip) { }
        void Focus(float x, float z, float distance) { }
    }

    // What the dialogue overlay reads (StageHud): the same in the story stage and the walkable 0.2 scenes.
    public interface IStoryScreen
    {
        GameRun Run { get; }
        DialogueLine? Line { get; }
        IReadOnlyList<string> Choices { get; }
        void Choose(int index);
        // A full-screen card (place and year, or narration), or null.
        string Card { get; }
        // Letterbox and subtitles while an automatic scene plays (the oath look).
        bool Letterbox { get; }
        // The comic panel now on screen, or null.
        string Comic { get; }
        float Fade { get; }
        bool Cinematic { get; }
        // The march on the realm map now playing, or null.
        StagePlayer.MarchShot March { get; }
        string Objective { get; }
        string Prompt { get; }
        bool Busy { get; }
        event Action Changed;
    }

    // Plays 0.2 cutscene scripts (StageScript): lines wait for confirm, choices wait for an answer and set a flag,
    // cards and marches time out or skip on confirm (SYSTEMS_V02 §5: a skipped march still stops at a via event).
    // Story effects (memories, flags, gifts, progress) are written as the steps pass, so a reload never loses one that
    // was shown. The host scene supplies the actors.
    public sealed class StagePlayer : MonoBehaviour
    {
        public sealed class MarchShot
        {
            public string From, To, Year;
            public float T;     // 0 → 1 along the road
        }

        public InputActionReference Confirm;
        [Tooltip("Seconds a march takes when nobody skips it (SYSTEMS_V02 §5: 5–8 s)")] public float MarchSeconds = 6f;

        public IStageActors Actors { get; set; }
        // Development / soak runs: lines and cards move on by themselves after this many seconds, choices take option 1.
        public static float AutoSeconds;
        float waitingSince;
        public GameRun Run => GameRun.Current;
        public bool Playing { get; private set; }
        public DialogueLine? Line { get; private set; }
        public IReadOnlyList<string> Choices { get; private set; }
        public string Card { get; private set; }
        public string Comic { get; private set; }
        public bool Letterbox { get; private set; }
        [Tooltip("Shortest time a timed line stays up before confirm can move on")] public float MinTimedSeconds = 0.4f;
        public float Fade { get; private set; }
        public bool Cinematic { get; private set; }
        public MarchShot March { get; private set; }
        // Set when a Load / Battle step left the scene: nothing after it plays.
        public bool Left { get; private set; }

        public event Action Changed;

        bool advance;
        int? chosen;
        int pressedFrame = -1;

        void OnEnable() { if (Confirm) Confirm.action.Enable(); }

        void Update()
        {
            if (Playing && AutoSeconds > 0f)
            {
                if (Choices != null && chosen == null && Time.time - waitingSince > AutoSeconds) chosen = 0;
                else if (Choices == null && Time.time - waitingSince > AutoSeconds) { waitingSince = Time.time; Advance(); }
            }
            if (!Playing || Choices != null) return;
            if (Confirm && Confirm.action.WasPressedThisFrame()) Advance();
        }

        public void Advance()
        {
            if (Time.frameCount == pressedFrame) return;
            pressedFrame = Time.frameCount;
            if (Cinematic) StoryCinematics.Advance();
            else advance = true;
        }

        public void Choose(int index) { if (Choices != null && index >= 0 && index < Choices.Count) chosen = index; }

        public IEnumerator Play(IEnumerable<StageStep> steps)
        {
            Playing = true;
            Changed?.Invoke();
            try { yield return Steps(steps); }
            finally
            {
                Line = null; Choices = null; Card = null; Comic = null; March = null; Cinematic = false; Letterbox = false;
                Playing = false;
                Changed?.Invoke();
            }
        }

        IEnumerator Steps(IEnumerable<StageStep> steps)
        {
            foreach (var step in steps)
            {
                if (Left) yield break;
                if (!Passes(step.If)) continue;
                yield return Do(step);
            }
        }

        bool Passes(string flag)
        {
            if (string.IsNullOrEmpty(flag)) return true;
            bool not = flag[0] == '!';
            bool has = Run != null && Run.Chapter2.Has(not ? flag.Substring(1) : flag);
            return not ? !has : has;
        }

        IEnumerator Do(StageStep s)
        {
            var run = Run;
            switch (s.Op)
            {
                case StageOp.Line:
                    Line = new DialogueLine(s.A, s.B);
                    if (s.Seconds > 0f) yield return WaitTimed(s.Seconds);
                    else yield return WaitConfirm();
                    Line = null;
                    break;
                case StageOp.Choice:
                    Line = new DialogueLine(s.A, s.B);
                    Choices = s.Options;
                    chosen = null;
                    waitingSince = Time.time;
                    Changed?.Invoke();
                    while (chosen == null) yield return null;
                    int pick = chosen.Value;
                    Choices = null; Line = null;
                    run?.Chapter2.Set(s.C + pick);
                    Changed?.Invoke();
                    if (pick < s.Branches.Length) yield return Steps(s.Branches[pick]);
                    break;
                case StageOp.Card:
                    Card = s.A;
                    yield return WaitConfirmOr(s.Seconds);
                    Card = null;
                    break;
                case StageOp.Comic: Comic = s.A; Changed?.Invoke(); break;
                case StageOp.Pose: Actors?.Pose(s.A, (int)s.X, s.C == "flip"); break;
                case StageOp.Letterbox: Letterbox = s.A != null; Changed?.Invoke(); break;
                case StageOp.Focus: Actors?.Focus(s.X, s.Y, s.Seconds); break;
                case StageOp.Background: Actors?.Background(s.A); break;
                case StageOp.Actor: Actors?.Show(s.A, s.B, s.X, s.Y, s.C); break;
                case StageOp.Move:
                    if (Actors == null) break;
                    if (s.D == "async") StartCoroutine(Actors.Move(s.A, s.X, s.Y, s.Seconds));
                    else yield return Actors.Move(s.A, s.X, s.Y, s.Seconds);
                    break;
                case StageOp.Face: Actors?.Face(s.A, s.C); break;
                case StageOp.Hide: Actors?.Hide(s.A); break;
                case StageOp.Shake: Actors?.Shake(s.Seconds); break;
                case StageOp.Flash: Actors?.Flash(); break;
                case StageOp.Remember: run?.Chapter2.Remember(s.A, s.B, s.C, s.D); break;
                case StageOp.Flag: run?.Chapter2.Set(s.A); break;
                case StageOp.Scroll:
                    Cinematic = true;
                    Changed?.Invoke();
                    yield return StoryCinematics.Scroll(s.A);
                    Cinematic = false;
                    break;
                case StageOp.March:
                    March = new MarchShot { From = s.A, To = s.B, Year = s.C };
                    advance = false;
                    for (float t = 0; t < MarchSeconds && !advance; t += Time.deltaTime)
                    {
                        March.T = t / MarchSeconds;
                        Changed?.Invoke();
                        yield return null;
                    }
                    March.T = 1f;
                    Changed?.Invoke();
                    yield return WaitSeconds(0.3f);
                    March = null;
                    break;
                case StageOp.Fade:
                    float from = Fade;
                    for (float t = 0; t < s.Seconds; t += Time.deltaTime) { Fade = Mathf.Lerp(from, s.X, t / s.Seconds); Changed?.Invoke(); yield return null; }
                    Fade = s.X;
                    break;
                case StageOp.Rations: run?.Progress.Inventory.AddRations((int)s.X); break;
                case StageOp.Reputation: run?.Life.AddReputation((int)s.X); break;
                case StageOp.Item:
                    if (run != null) run.Progress.Inventory.AddItem(run.Data.Economy.Find(s.A));
                    break;
                case StageOp.Advance:
                    run?.Chapter2.Advance((ChapterStep)int.Parse(s.A), (ChapterStep)int.Parse(s.B));
                    break;
                case StageOp.Autosave:
                    if (run != null && !run.IsDevelopment)
                    {
                        try { run.SaveTo(Save.SaveSlots.Auto, s.A); }
                        catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException) { Debug.LogWarning("Autosave failed: " + e.Message); }
                    }
                    break;
                case StageOp.Music:
                    if (string.IsNullOrEmpty(s.A)) StoryMusic.Stop(); else StoryMusic.Play(s.A, loop: true);
                    break;
                case StageOp.Wait: yield return WaitSeconds(s.Seconds); break;
                case StageOp.Call: s.Callback?.Invoke(); break;
                case StageOp.Load:
                    Left = true;
                    if (run != null) run.PendingStage = s.B;
                    SceneManager.LoadScene(s.A);
                    break;
                case StageOp.Battle:
                    Left = true;
                    if (run != null) run.NextArrival = GameRun.Arrival.ReturnFromBattle;
                    SceneManager.LoadScene(GameRun.SceneFor(s.A));
                    break;
            }
            Changed?.Invoke();
        }

        IEnumerator WaitConfirm()
        {
            waitingSince = Time.time;
            advance = false;
            Changed?.Invoke();
            yield return null;   // the press that started the line must not skip it
            advance = false;
            while (!advance) yield return null;
        }

        // A timed line: on by itself after seconds (development auto-play makes it shorter), or on confirm after a moment.
        IEnumerator WaitTimed(float seconds)
        {
            advance = false;
            Changed?.Invoke();
            float started = Time.time;
            if (AutoSeconds > 0f) seconds = Mathf.Min(seconds, AutoSeconds);
            yield return null;
            advance = false;
            while (Time.time - started < seconds && !(advance && Time.time - started >= MinTimedSeconds)) yield return null;
        }

        IEnumerator WaitConfirmOr(float seconds)
        {
            advance = false;
            Changed?.Invoke();
            yield return null;
            advance = false;
            for (float t = 0; t < seconds && !advance; t += Time.deltaTime) yield return null;
        }

        static IEnumerator WaitSeconds(float seconds)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime) yield return null;
        }
    }
}
