using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Jrpg;
using NUnit.Framework;
using Prototype.BingshuTactics;
using ThreeKingdoms.Core.Battle;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Data.Battle;
using ThreeKingdoms.Gameplay.Battle;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.Save;
using ThreeKingdoms.UI.Battle;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

// 0.2 黄巾篇 acceptance (V02_HUANGJIN_PLAN §8 step 7 / 9, SYSTEMS_V02 §6): one play-through from Lu Zhi's camp to the
// end of Anxi — C2, the camp gate, the march, C3 and battle 4 with its events, C4, choice ①, battle 5 with the escort,
// C8, the Wancheng detour, C9 and scroll ②, Anxi's commissions, battle 6, the courier, choice ②, C13 and scroll ③.
public sealed class ChapterFlowTests
{
    string saveDir;
    readonly Queue<int> picks = new Queue<int>();
    readonly HashSet<string> comics = new HashSet<string>();

    [SetUp] public void Fresh()
    {
        GameRun.Clear();
        StagePlayer.AutoSeconds = 0f;
        ChapterTactics.AutoPlay = true;
        Time.timeScale = 6f;
        saveDir = System.IO.Path.Combine(Application.temporaryCachePath, "chapter-" + System.Guid.NewGuid().ToString("N"));
        GameRun.SaveDirectoryOverride = saveDir;
    }

    [TearDown] public void Clean()
    {
        ChapterTactics.AutoPlay = false;
        Time.timeScale = 1f;
        TacticsGame.Chapter = null;
        GameRun.Clear();
        GameRun.SaveDirectoryOverride = null;
        if (System.IO.Directory.Exists(saveDir)) System.IO.Directory.Delete(saveDir, true);
    }

#if UNITY_EDITOR
    static BattleCatalog Catalog => UnityEditor.AssetDatabase.LoadAssetAtPath<BattleCatalog>("Assets/_Project/Data/Battle/BattleCatalog.asset");
#else
    static BattleCatalog Catalog => null;
#endif

    // Moves through lines, cards and marches, answering choices from the queue (0 when it runs dry), until the screen is
    // no longer busy or the scene changes.
    IEnumerator Drive(IStoryScreen screen, StagePlayer player, float seconds = 120f)
    {
        var scene = SceneManager.GetActiveScene().name;
        float deadline = Time.realtimeSinceStartup + seconds;
        yield return null;
        while (true)
        {
            var mono = screen as MonoBehaviour;
            bool alive = mono != null;
            bool same = SceneManager.GetActiveScene().name == scene;
            bool playing = player != null && player.Playing;
            bool busy = alive && screen.Busy;
            bool panel = alive && screen is FieldStoryController fc && fc.Panel != null;
            if (!(alive && same && (playing || (busy && !panel))))
            {
                Debug.Log($"CHAPTER_TEST drive end in {scene}: alive={alive} same={same} playing={playing} busy={busy} panel={panel}");
                break;
            }
            Assert.Less(Time.realtimeSinceStartup, deadline, "scene " + scene + " stuck");
            var hud = Object.FindFirstObjectByType<ThreeKingdoms.UI.Story.StageHud>();
            if (hud && hud.ShownComic != null) comics.Add(hud.ShownComic);
            if (screen.Choices != null) player.Choose(picks.Count > 0 ? picks.Dequeue() : 0);
            else player.Advance();
            yield return null;
        }
        yield return null;
    }

    static IEnumerator WaitFor<T>(float seconds = 30f) where T : Object
    {
        float deadline = Time.realtimeSinceStartup + seconds;
        while (Object.FindFirstObjectByType<T>() == null) { Assert.Less(Time.realtimeSinceStartup, deadline, "waiting for " + typeof(T).Name); yield return null; }
        yield return null;
    }

    static void Teleport(Transform who, Vector3 at)
    {
        var cc = who.GetComponent<CharacterController>();
        if (cc) cc.enabled = false; who.position = at; if (cc) cc.enabled = true;
    }

    IEnumerator TalkTo(FieldStoryController c, string id, params int[] answers)
    {
        foreach (var a in answers) picks.Enqueue(a);
        var who = c.Find(id);
        Assert.IsNotNull(who, id);
        Teleport(c.Player.transform, who.position + new Vector3(0, 0, -1.4f));
        yield return null;
        float wait = Time.realtimeSinceStartup + 10f;
        while (c.Busy) { Assert.Less(Time.realtimeSinceStartup, wait, $"still busy before talking to {id}: panel={c.Panel} playing={c.Stage.Playing}"); yield return null; }
        Debug.Log($"CHAPTER_TEST talk {id}: prompt={c.Prompt} at {c.Player.transform.position} playing={c.Stage.Playing}");
        c.Interact();
        Assert.IsTrue(c.Busy, "talking to " + id);
        yield return Drive(c, c.Stage);
        if (c && c.Panel != null) c.ClosePanel();
        yield return null;
    }

    // Battles 4–6 are fought on the tactics map (ADR-0010): the AI plays our side, lines advance by themselves.
    IEnumerator WinBattle(string expected)
    {
        yield return WaitFor<TacticsGame>();
        var bridge = Object.FindFirstObjectByType<ChapterTactics>();
        Assert.AreEqual(expected, bridge.EncounterId);
        var game = Object.FindFirstObjectByType<TacticsGame>();
        string scene = SceneManager.GetActiveScene().name;
        float deadline = Time.realtimeSinceStartup + 400f;
        while (!game.ChapterFinished)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "battle " + expected);
            Assert.IsFalse(game.Lost, expected + " lost");
            yield return null;
        }
        Debug.Log($"CHAPTER_TEST {expected} won in round {game.Rounds}");
        Assert.IsTrue(bridge.LastApplied.RewardGranted);
        while (SceneManager.GetActiveScene().name == scene) yield return null;
        yield return null;
    }

    [UnityTest]
    public IEnumerator TheWholeChapterPlaysThrough()
    {
        // Lu Zhi's camp: arrival autosave and C2.
        var run = GameRun.ForChapter(Catalog, ChapterStep.Camp, exp: 60, copper: 260);
        SceneManager.LoadScene(GameRun.CampScene);
        yield return WaitFor<FieldStoryController>();
        var camp = Object.FindFirstObjectByType<FieldStoryController>();
        Assert.IsTrue(camp.Busy, "C2 plays on arrival");
        yield return Drive(camp, camp.Stage);
        Assert.IsTrue(run.Chapter2.Has(ChapterScripts.CampGreeted));
        Assert.AreEqual(SaveReadStatus.Ok, run.Slots().Read(SaveSlots.Auto, out var auto));
        Assert.AreEqual(SaveData.CampLocation, auto.location, "到达大营时写段落自动档");
        yield return TalkTo(camp, "quiver");
        Assert.AreEqual(1, run.Progress.Inventory.Count(run.Data.Economy.Find("lu_quiver")), "flags: " + string.Join(",", run.Chapter2.Flags));
        // The gate guard: leave (option 2), then confirm.
        int rations = run.Progress.Inventory.Rations;
        yield return TalkTo(camp, "camp_guard", 1, 0);

        // The story stage: march, C3 (two rations), battle 4.
        yield return WaitFor<StageController>();
        var stage = Object.FindFirstObjectByType<StageController>();
        yield return Drive(stage, stage.Player);
        Assert.AreEqual(rations + 2, run.Progress.Inventory.Rations, "校尉赠军粮×2");
        yield return WinBattle("battle_4");
        Assert.AreEqual("seen", run.Chapter2.Recall("cao_cao", "caocao_yingchuan"));

        // C6 (choice ① B: escort to the post pavilion), C7, battle 5, C8.
        yield return WaitFor<StageController>();
        stage = Object.FindFirstObjectByType<StageController>();
        picks.Enqueue(1);
        yield return Drive(stage, stage.Player);
        Assert.AreEqual("B_escort", run.Chapter2.Recall("lu_zhi", "luzhi_cart"));
        yield return WinBattle("battle_5");
        Assert.AreEqual("seen", run.Chapter2.Recall("dong_zhuo", "dongzhuo_baishen"));

        // The Wancheng detour (yes), C9, scroll ②, 185, Anxi.
        yield return WaitFor<StageController>();
        stage = Object.FindFirstObjectByType<StageController>();
        picks.Enqueue(0);
        yield return Drive(stage, stage.Player);
        Assert.AreEqual(StoryCinematics.LastScroll, ChapterScripts.ScrollZhuanzhan);
        Assert.AreEqual("seen", run.Chapter2.Recall("sun_jian", "sunjian_wancheng"));

        yield return WaitFor<FieldStoryController>();
        var anxi = Object.FindFirstObjectByType<FieldStoryController>();
        Assert.AreEqual(ChronicleNode.N2, run.Chapter2.Node);
        yield return Drive(anxi, anxi.Stage);   // C10
        Assert.IsTrue(run.Chapter2.Has(ChapterScripts.AnxiGreeted));
        // The three commissions.
        int rep = run.Life.Reputation;
        yield return TalkTo(anxi, "old_woman", 0);
        yield return TalkTo(anxi, "wang", 1);      // judged for Wang straight away
        if (!run.Chapter2.Has(ChapterState.OxCase)) yield return TalkTo(anxi, "wang", 0, 0);
        yield return TalkTo(anxi, "bridge_elder");
        yield return TalkTo(anxi, "timber");
        yield return TalkTo(anxi, "bridge_elder");
        Assert.AreEqual(0, run.Chapter2.OpenCommissions.Count());
        Assert.AreEqual(rep + 3, run.Life.Reputation);
        yield return TalkTo(anxi, "clerk", 1);
        Assert.IsTrue(run.Chapter2.Has(ChapterScripts.CharmGiven));
        // Battle 6 through the east gate, back to Anxi.
        yield return TalkTo(anxi, "east_gate", 0);
        yield return WinBattle("battle_6");
        yield return WaitFor<FieldStoryController>();
        anxi = Object.FindFirstObjectByType<FieldStoryController>();
        Assert.AreEqual(ChapterStep.AnxiBandits, run.Chapter2.Step);
        // The courier, C11, choice ② A (Liu Bei himself), C13, the after-story and scroll ③.
        picks.Clear();
        yield return TalkTo(anxi, "courier", 0, 0);
        float deadline = Time.realtimeSinceStartup + 60f;
        while (SceneManager.GetActiveScene().name != GameRun.TitleScene) { Assert.Less(Time.realtimeSinceStartup, deadline); StoryCinematics.Advance(); yield return null; }
        Assert.AreEqual(ChapterScripts.ScrollAnxi, StoryCinematics.LastScroll);
        Assert.AreEqual(ChapterStep.Done, run.Chapter2.Step);
        Assert.AreEqual("A_self", run.Chapter2.Recall("liu_bei", "liubei_duyou"));
        // The 鞭督邮 story plays as a 连环画 (user 2026-10-25): choice A shows the bind and cane panels, not Zhang Fei's.
        Assert.That(new[] { "P1_refused", "P2_report", "P3A_bind", "P4A_cane", "P5_seal", "P6_leave" }, Is.SubsetOf(comics), string.Join(",", comics));
        Assert.IsFalse(comics.Contains("P3B_drag"));
        Assert.AreEqual(10, run.Chapter2.Memories.Count, "必做 9 条＋宛城 1 条");
        Assert.That(run.Data.Party.Select(g => run.Progress.Level(g)), Is.All.EqualTo(7));
    }
}
