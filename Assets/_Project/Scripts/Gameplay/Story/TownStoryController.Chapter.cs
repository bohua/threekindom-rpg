using System.Collections;
using ThreeKingdoms.Core.Story;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThreeKingdoms.Gameplay.Story
{
    // 0.2 in Zhuojun (V02_HUANGJIN_PLAN §3.4): after the third victory the brothers come back in through the south gate,
    // Zou Jing brings the orders (C1, 史实·先主传), scroll ① closes the section without its seal, and the town is free
    // again until the south gate's confirm box sends them south for good.
    public sealed partial class TownStoryController
    {
        [Header("0.2 Zou Jing's orders (C1)")]
        [Tooltip("Zou Jing, hidden until C1; he walks in from the gate")] public Transform ZouJing;
        public Vector3[] ZouJingEntrance = { new Vector3(0f, 0, -38f), new Vector3(0f, 0, -31.5f) };

        static readonly DialogueLine[] ZouJingLines =
        {
            new DialogueLine("zou_jing", "c1.1"), new DialogueLine("liu_bei", "c1.2"), new DialogueLine("zou_jing", "c1.3"),
            new DialogueLine("liu_bei", "c1.4"), new DialogueLine("zou_jing", "c1.5"), new DialogueLine("liu_bei", "c1.6"),
            new DialogueLine("zou_jing", "c1.7"), new DialogueLine("zhang_fei", "c1.8"), new DialogueLine("guan_yu", "c1.9"),
        };
        public static readonly string[] LeaveChoices = { "leave.zhuojun.yes", "leave.zhuojun.no" };

        bool ChapterOpen => Run != null && Run.Chapter2.Step >= ChapterStep.ZouJingOrders;

        // Zou Jing waits by the gate once he has spoken.
        void ShowZouJing()
        {
            if (!ZouJing) return;
            bool show = Run.Chapter2.Step == ChapterStep.ZhuojunFree;
            ZouJing.gameObject.SetActive(show);
            if (show && ZouJingEntrance.Length > 0) StageMoves.Place(ZouJing, ZouJingEntrance[0] + new Vector3(3f, 0, 2f));
        }

        IEnumerator PlayZouJingOrders()
        {
            if (Run.Chapter2.Step != ChapterStep.ZouJingOrders) yield break;
            Busy = true;
            SetControl(false);
            Prompt = null;
            Changed?.Invoke();
            try
            {
                if (ZouJing && ZouJingEntrance.Length > 0)
                {
                    StageMoves.Place(ZouJing, ZouJingEntrance[0]);
                    ZouJing.gameObject.SetActive(true);
                    yield return StageMoves.Walk(ZouJing, ZouJingEntrance);
                    StageMoves.Face(Player.transform, ZouJing.position);
                }
                yield return Lines(ZouJingLines);
                Cinematic = true;
                Changed?.Invoke();
                yield return StoryCinematics.Scroll(StoryCinematics.YellowTurbanScroll);   // scroll ①
                Cinematic = false;
                Run.Chapter2.Advance(ChapterStep.ZouJingOrders, ChapterStep.ZhuojunFree);
                ShowZouJing();
            }
            finally
            {
                Line = null;
                Cinematic = false;
                Busy = false;
                SetControl(true);
                Changed?.Invoke();
            }
        }

        // The south gate once Zou Jing has spoken: leaving is final (V02_HUANGJIN_PLAN §2 "不能回访").
        IEnumerator LeaveZhuojun()
        {
            Busy = true;
            SetControl(false);
            Prompt = null;
            try
            {
                Line = new DialogueLine("system", "leave.zhuojun.ask");
                Choices = LeaveChoices;
                chosen = null;
                Changed?.Invoke();
                while (chosen == null) yield return null;
                int answer = chosen.Value;
                Choices = null;
                Line = null;
                if (answer != 0)
                {
                    var back = new GameObject("step back").transform;
                    back.position = new Vector3(GateCenter.x, 0, GateCenter.z + GateSize.z / 2f + 1.6f);
                    Place(Player.transform, back);
                    Destroy(back.gameObject);
                    ShowFollowers(Vector3.back);
                    yield break;
                }
                yield return FadeTo(1f);
                Run.PendingStage = ChapterScripts.ToCamp;
                SceneManager.LoadScene(GameRun.StageScene);
            }
            finally
            {
                Line = null;
                Choices = null;
                Busy = false;
                SetControl(true);
                Changed?.Invoke();
            }
        }
    }
}
