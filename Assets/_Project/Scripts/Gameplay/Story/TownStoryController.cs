using System;
using System.Collections;
using System.Collections.Generic;
using Jrpg;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Data.Battle;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ThreeKingdoms.Gameplay.Story
{
    // One line of dialogue or narration; the UI turns the keys into text (StoryText), so this layer stays text-free.
    public readonly struct DialogueLine
    {
        public readonly string Speaker, Line;
        public DialogueLine(string speaker, string line) { Speaker = speaker; Line = line; }
    }

    // Drives the Zhuojun main line (ADR-0006 §3, story-events GDD): talk to Guan Yu and Zhang Fei to recruit them,
    // the automatic peach-garden oath once both have joined, and leaving by the south gate for the next battle.
    // Rules come from StoryState; this component only stages them.
    public sealed class TownStoryController : MonoBehaviour
    {
        public BattleCatalog Catalog;
        public PlayerMover Player;
        public FollowCamera Camera;
        public Transform GuanYu, ZhangFei;
        public InputActionReference InteractAction, MenuAction;
        [Header("Spots")]
        public Transform NewGameSpot, ReturnSpot, OathEndSpot, GroveFocus;
        public Transform[] GroveSpots = Array.Empty<Transform>();   // Liu, Guan, Zhang
        [Header("South gate passage")]
        public Vector3 GateCenter = new Vector3(0, 1, -36f);
        public Vector3 GateSize = new Vector3(4f, 3f, 5f);
        public float TalkRange = 2.2f;
        [Tooltip("Seconds for a fade to black / back")] public float FadeTime = 0.4f;

        public GameRun Run { get; private set; }
        public bool Busy { get; private set; }
        public DialogueLine? Line { get; private set; }
        // "talk:guan_yu", "gate:locked", "gate:battle_2" … or null.
        public string Prompt { get; private set; }
        public float Fade { get; private set; }
        public bool InGatePassage { get; private set; }

        public event Action Changed;

        bool advance;
        string pendingGateScene;

        static readonly DialogueLine[] GuanYuMeeting =
        {
            new DialogueLine("guan_yu", "guan.1"), new DialogueLine("liu_bei", "guan.2"),
            new DialogueLine("guan_yu", "guan.3"), new DialogueLine("system", "guan.joined"),
        };
        static readonly DialogueLine[] ZhangFeiMeeting =
        {
            new DialogueLine("zhang_fei", "zhang.1"), new DialogueLine("liu_bei", "zhang.2"),
            new DialogueLine("zhang_fei", "zhang.3"), new DialogueLine("system", "zhang.joined"),
        };
        static readonly DialogueLine[] OathScene =
        {
            new DialogueLine("narrator", "oath.1"), new DialogueLine("zhang_fei", "oath.2"),
            new DialogueLine("liu_bei", "oath.3"), new DialogueLine("guan_yu", "oath.4"),
            new DialogueLine("narrator", "oath.5"),
        };

        void OnEnable()
        {
            if (InteractAction) InteractAction.action.Enable();
            if (MenuAction) MenuAction.action.Enable();
        }

        void OnDisable()
        {
            if (InteractAction) InteractAction.action.Disable();
            if (MenuAction) MenuAction.action.Disable();
        }

        void Start()
        {
            Run = GameRun.Current ?? GameRun.NewGame(Catalog);
            var story = Run.Story;
            Place(Player.transform, Run.NextArrival == GameRun.Arrival.ReturnFromBattle ? ReturnSpot : NewGameSpot);
            Run.NextArrival = GameRun.Arrival.ReturnFromBattle;   // any later town load is a return
            if (GuanYu) GuanYu.gameObject.SetActive(!story.GuanYuJoined);
            if (ZhangFei) ZhangFei.gameObject.SetActive(!story.ZhangFeiJoined);
            SetControl(true);
            // An oath interrupted by a reload replays from the start (GDD rule 11).
            story.ToSafeState();
            if (story.Phase == StoryPhase.Oath) StartCoroutine(PlayOath());
            Changed?.Invoke();
        }

        void Update()
        {
            if (MenuAction && MenuAction.action.WasPressedThisFrame()) Application.Quit();   // system menu stand-in (GDD answer 9)
            bool pressed = InteractAction && InteractAction.action.WasPressedThisFrame();
            if (Busy) { if (pressed) Advance(); return; }
            UpdatePrompt();
            if (pressed) Interact();
        }

        // ---------- public entry points (input and tests) ----------

        public void Advance() { if (Busy) advance = true; }

        public void Interact()
        {
            if (Busy) return;
            UpdatePrompt();
            if (Prompt == null) return;
            if (Prompt == "talk:" + StoryState.GuanYu) StartCoroutine(Meet(StoryState.GuanYu, GuanYu, GuanYuMeeting));
            else if (Prompt == "talk:" + StoryState.ZhangFei) StartCoroutine(Meet(StoryState.ZhangFei, ZhangFei, ZhangFeiMeeting));
            else if (Prompt == "gate:locked" || Prompt == "gate:done") StartCoroutine(Say(new[] { new DialogueLine("system", Prompt) }));
            else if (Prompt.StartsWith("gate:battle_")) StartCoroutine(LeaveTown());
        }

        // ---------- prompts ----------

        void UpdatePrompt()
        {
            string prompt = null;
            var story = Run.Story;
            var p = Player.transform.position;
            InGatePassage = new Bounds(GateCenter, GateSize).Contains(p + Vector3.up);
            if (InGatePassage)
                prompt = story.CanLeaveTown(out string reason) ? "gate:" + story.NextBattleId
                    : reason == StoryState.ReasonAllDone ? "gate:done" : "gate:locked";
            else if (Near(GuanYu, p) && !story.GuanYuJoined) prompt = "talk:" + StoryState.GuanYu;
            else if (Near(ZhangFei, p) && !story.ZhangFeiJoined) prompt = "talk:" + StoryState.ZhangFei;
            if (prompt == Prompt) return;
            Prompt = prompt;
            Changed?.Invoke();
        }

        bool Near(Transform t, Vector3 p) => t && t.gameObject.activeInHierarchy && Vector3.Distance(Flat(t.position), Flat(p)) <= TalkRange;
        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

        // ---------- sequences ----------

        IEnumerator Say(IReadOnlyList<DialogueLine> lines)
        {
            Busy = true;
            SetControl(false);
            Prompt = null;
            try
            {
                foreach (var line in lines)
                {
                    Line = line; advance = false;
                    Changed?.Invoke();
                    yield return null;                  // the press that opened the dialogue must not skip line one
                    while (!advance) yield return null;
                }
            }
            finally
            {
                Line = null;
                Busy = false;
                SetControl(true);
                Changed?.Invoke();
            }
        }

        IEnumerator Meet(string id, Transform npc, DialogueLine[] lines)
        {
            yield return Say(lines);
            // Joined only once the dialogue has ended (GDD edge case); the companion leaves their post.
            bool startOath = Run.Story.Join(id);
            if (npc) npc.gameObject.SetActive(false);
            Changed?.Invoke();
            if (startOath) yield return PlayOath();
        }

        // Simplified peach-garden oath (V1_SPEC §2.1): the game moves the three to the grove outside the west gate,
        // plays a few lines that cannot be skipped (GDD answer 9), and returns control inside the west gate (answer 7).
        IEnumerator PlayOath()
        {
            if (!Run.Story.BeginOath()) yield break;
            Busy = true;
            SetControl(false);
            Prompt = null;
            Changed?.Invoke();
            try
            {
                yield return FadeTo(1f);
                Place(Player.transform, GroveSpots.Length > 0 ? GroveSpots[0] : GroveFocus);
                if (GuanYu && GroveSpots.Length > 1) { Place(GuanYu, GroveSpots[1]); GuanYu.gameObject.SetActive(true); }
                if (ZhangFei && GroveSpots.Length > 2) { Place(ZhangFei, GroveSpots[2]); ZhangFei.gameObject.SetActive(true); }
                if (Camera && GroveFocus) Camera.Target = GroveFocus;
                yield return FadeTo(0f);
                foreach (var line in OathScene)
                {
                    Line = line; advance = false;
                    Changed?.Invoke();
                    yield return null;
                    while (!advance) yield return null;
                }
                Line = null;
                yield return FadeTo(1f);
                if (GuanYu) GuanYu.gameObject.SetActive(false);
                if (ZhangFei) ZhangFei.gameObject.SetActive(false);
                Place(Player.transform, OathEndSpot);
                if (Camera) Camera.Target = Player.transform;
                Run.Story.CompleteOath();
                yield return FadeTo(0f);
            }
            finally
            {
                Line = null;
                Fade = 0f;
                if (Camera) Camera.Target = Player.transform;
                Busy = false;
                SetControl(true);
                Changed?.Invoke();
            }
        }

        IEnumerator LeaveTown()
        {
            Busy = true;
            SetControl(false);
            pendingGateScene = GameRun.SceneFor(Run.Story.NextBattleId);
            Prompt = null;
            Changed?.Invoke();
            yield return FadeTo(1f);
            Run.NextArrival = GameRun.Arrival.ReturnFromBattle;
            SceneManager.LoadScene(pendingGateScene);
        }

        IEnumerator FadeTo(float target)
        {
            float start = Fade;
            for (float t = 0; t < FadeTime; t += Time.deltaTime)
            {
                Fade = Mathf.Lerp(start, target, t / FadeTime);
                Changed?.Invoke();
                yield return null;
            }
            Fade = target;
            Changed?.Invoke();
        }

        void SetControl(bool on) { if (Player) Player.InputEnabled = on; }

        static void Place(Transform who, Transform spot)
        {
            if (!who || !spot) return;
            var controller = who.GetComponent<CharacterController>();
            if (controller) controller.enabled = false;
            who.position = spot.position;
            if (controller) controller.enabled = true;
        }
    }
}
