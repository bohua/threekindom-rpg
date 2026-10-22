using System;
using System.Collections;
using System.Collections.Generic;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Data.Battle;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ThreeKingdoms.Gameplay.Story
{
    // Inside the tavern (user 2026-10-22, after 演义 第1回): Liu Bei and Zhang Fei talk over wine, Guan Yu comes in and
    // calls the waiter for a drink before going to enlist, the two invite him over, and he joins. Played as a scene the
    // player watches (lines advance on confirm); afterwards the party walks out and the town plays the peach-garden
    // oath. Only staged while Zhang Fei has joined and Guan Yu has not; otherwise it returns to town at once.
    public sealed class TavernStage : MonoBehaviour
    {
        public BattleCatalog Catalog;
        public Transform LiuBei, ZhangFei, GuanYu, Waiter;
        [Tooltip("Liu Bei's and Zhang Fei's places at the table, and Guan Yu's after he is called over")]
        public Transform LiuSeat, ZhangSeat, GuanSeat;
        [Tooltip("The waiter's place behind the counter and beside the table")] public Transform WaiterPost, WaiterAtTable;
        [Tooltip("Guan Yu's way in from the door, to the table he first takes")] public Vector3[] GuanYuEntrance = Array.Empty<Vector3>();
        public InputActionReference InteractAction;
        [Tooltip("Seconds for a fade to black / back")] public float FadeTime = 0.4f;

        public GameRun Run { get; private set; }
        public DialogueLine? Line { get; private set; }
        public float Fade { get; private set; } = 1f;
        public bool Playing { get; private set; }
        public event Action Changed;

        bool advance;

        static readonly DialogueLine[] Drinking =
        {
            new DialogueLine("waiter", "tavern.1"), new DialogueLine("zhang_fei", "tavern.2"), new DialogueLine("liu_bei", "tavern.3"),
        };
        static readonly DialogueLine[] GuanYuArrives =
        {
            new DialogueLine("guan_yu", "tavern.4"), new DialogueLine("waiter", "tavern.5"),
            new DialogueLine("liu_bei", "tavern.6"), new DialogueLine("zhang_fei", "tavern.7"),
        };
        static readonly DialogueLine[] AtTheTable =
        {
            new DialogueLine("liu_bei", "tavern.8"), new DialogueLine("guan_yu", "tavern.9"),
            new DialogueLine("liu_bei", "tavern.10"), new DialogueLine("guan_yu", "tavern.11"),
            new DialogueLine("system", "guan.joined"), new DialogueLine("zhang_fei", "tavern.12"),
        };

        void OnEnable() { if (InteractAction) InteractAction.action.Enable(); }
        void OnDisable() { if (InteractAction) InteractAction.action.Disable(); }

        void Start()
        {
            Run = GameRun.Current ?? GameRun.NewGame(Catalog);
            if (!GameRunHasMeeting()) { ReturnToTown(); return; }
            StartCoroutine(Play());
        }

        bool GameRunHasMeeting() => Run.Story.ZhangFeiJoined && !Run.Story.GuanYuJoined;

        void Update()
        {
            if (Playing && InteractAction && InteractAction.action.WasPressedThisFrame()) Advance();
        }

        public void Advance() { if (Line.HasValue) advance = true; }

        IEnumerator Play()
        {
            Playing = true;
            Changed?.Invoke();
            StageMoves.Place(LiuBei, LiuSeat.position);
            StageMoves.Place(ZhangFei, ZhangSeat.position);
            StageMoves.Face(LiuBei, ZhangFei.position);
            StageMoves.Face(ZhangFei, LiuBei.position);
            StageMoves.Place(Waiter, WaiterPost.position);
            if (GuanYu) GuanYu.gameObject.SetActive(false);
            yield return FadeTo(0f);

            // The waiter brings the wine, then goes back behind the counter.
            yield return StageMoves.Walk(Waiter, new[] { WaiterAtTable.position });
            StageMoves.Face(Waiter, LiuBei.position);
            yield return Lines(Drinking, 0, 1);
            StartCoroutine(StageMoves.Walk(Waiter, new[] { WaiterPost.position }));
            yield return Lines(Drinking, 1, Drinking.Length);

            // Guan Yu comes in from the street and calls for wine.
            if (GuanYu && GuanYuEntrance.Length > 0)
            {
                StageMoves.Place(GuanYu, GuanYuEntrance[0]);
                GuanYu.gameObject.SetActive(true);
                yield return StageMoves.Walk(GuanYu, GuanYuEntrance);
                StageMoves.Face(GuanYu, Waiter.position);
                StageMoves.Face(LiuBei, GuanYu.position);
                StageMoves.Face(ZhangFei, GuanYu.position);
            }
            yield return Lines(GuanYuArrives, 0, GuanYuArrives.Length);

            // Called over, he sits with them.
            yield return StageMoves.Walk(GuanYu, new[] { GuanSeat.position });
            StageMoves.Face(GuanYu, LiuBei.position);
            StageMoves.Face(LiuBei, GuanYu.position);
            yield return Lines(AtTheTable, 0, 4);
            Run.Story.Join(StoryState.GuanYu);
            yield return Lines(AtTheTable, 4, AtTheTable.Length);

            Playing = false;
            yield return FadeTo(1f);
            ReturnToTown();
        }

        IEnumerator Lines(IReadOnlyList<DialogueLine> lines, int from, int to)
        {
            for (int i = from; i < to; i++)
            {
                Line = lines[i]; advance = false;
                Changed?.Invoke();
                yield return null;
                while (!advance) yield return null;
            }
            Line = null;
            Changed?.Invoke();
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

        // Back out the tavern door; with both brothers in, the town plays the oath (StoryPhase.Oath).
        void ReturnToTown()
        {
            Run.NextArrival = GameRun.Arrival.FromTavernInside;
            SceneManager.LoadScene(GameRun.TownScene);
        }
    }
}
