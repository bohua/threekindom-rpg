using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Prototype.BingshuTactics;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Data.Battle;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.Save;
using ThreeKingdoms.UI.Battle;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// 0.4 河北篇 acceptance (V04_HEBEI_PLAN, SYSTEMS_V04 §7): one play-through of the Pingyuan base and section seven — E3,
// rumours and training, the porridge and the canal, battle 9, the ferry, 192, battle 10 with Zhao Yun, choice ⑥, battle 11
// with the way out, Zhending, back to Pingyuan for the assassin (choice ⑦), scroll ⑧ and on into 0.5.
public sealed class Chapter4FlowTests
{
    string saveDir;
    readonly Queue<int> picks = new Queue<int>();

    [SetUp] public void Fresh()
    {
        GameRun.Clear();
        StagePlayer.AutoSeconds = 0f;
        ChapterTactics.AutoPlay = true;
        Time.timeScale = 6f;
        saveDir = System.IO.Path.Combine(Application.temporaryCachePath, "chapter4-" + System.Guid.NewGuid().ToString("N"));
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

    IEnumerator Drive(IStoryScreen screen, StagePlayer player, float seconds = 180f)
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
            if (!(alive && same && (playing || (busy && !panel)))) break;
            Assert.Less(Time.realtimeSinceStartup, deadline, "scene " + scene + " stuck");
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
        while (c.Busy) { Assert.Less(Time.realtimeSinceStartup, wait, "still busy before talking to " + id); yield return null; }
        c.Interact();
        Assert.IsTrue(c.Busy, "talking to " + id);
        yield return Drive(c, c.Stage);
        if (c && c.Panel != null) c.ClosePanel();
        yield return null;
    }

    IEnumerator WinBattle(string expected)
    {
        yield return WaitFor<TacticsGame>();
        var bridge = Object.FindFirstObjectByType<ChapterTactics>();
        Assert.AreEqual(expected, bridge.EncounterId);
        var game = Object.FindFirstObjectByType<TacticsGame>();
        string scene = SceneManager.GetActiveScene().name;
        float deadline = Time.realtimeSinceStartup + 500f;
        while (!game.ChapterFinished)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "battle " + expected);
            Assert.IsFalse(game.Lost, expected + " lost");
            yield return null;
        }
        Debug.Log($"CHAPTER4_TEST {expected} won in round {game.Rounds}");
        Assert.IsTrue(bridge.LastApplied.RewardGranted);
        while (SceneManager.GetActiveScene().name == scene) yield return null;
        yield return null;
    }

    IEnumerator PlayStage()
    {
        yield return WaitFor<StageController>();
        var stage = Object.FindFirstObjectByType<StageController>();
        yield return Drive(stage, stage.Player);
    }

    [UnityTest]
    public IEnumerator TheHebeiChapterPlaysThrough()
    {
        var run = GameRun.ForChapter(Catalog, ChapterStep.Pingyuan, exp: 370, copper: 500);
        SceneManager.LoadScene(GameRun.BohaiScene);
        yield return WaitFor<FieldStoryController>(90f);
        var py = Object.FindFirstObjectByType<FieldStoryController>();
        Assert.IsTrue(py.Busy, "E3 plays on arrival");
        yield return Drive(py, py.Stage);
        Assert.IsTrue(run.Chapter2.Has(ChapterScripts4.PingyuanGreeted));
        Assert.AreEqual(SaveReadStatus.Ok, run.Slots().Read(SaveSlots.Auto, out var auto));
        Assert.AreEqual("region:R4", auto.location);

        // The base: a rumour, a day's training, the porridge, the canal (look first, then judge).
        yield return TalkTo(py, "jian_yong");
        Assert.IsTrue(run.Chapter2.Has(ChapterScripts4.RumourX11));
        int exp = run.Progress.Exp(run.Data.Party[0]);
        yield return TalkTo(py, "tian_yu", 0);
        Assert.AreEqual(exp + 30, run.Progress.Exp(run.Data.Party[0]));
        int rep = run.Life.Reputation;
        yield return TalkTo(py, "py_elder", 0);
        yield return TalkTo(py, "canal");
        yield return TalkTo(py, "py_farmer_a", 0);
        Assert.AreEqual(rep + 2, run.Life.Reputation);

        // Battle 9 from the south gate, back to Pingyuan.
        yield return TalkTo(py, "py_courier", 0);
        yield return WinBattle("battle_9");
        yield return WaitFor<FieldStoryController>(90f);
        py = Object.FindFirstObjectByType<FieldStoryController>();
        Assert.AreEqual(ChapterStep.PingyuanSafe, run.Chapter2.Step);

        // The ferry: 192, the Panhe (battle 10), Zhao Yun (choice ⑥ A), Jieqiao (battle 11).
        yield return TalkTo(py, "py_ferry", 0);
        yield return PlayStage();
        Assert.AreEqual(ChronicleNode.N6, run.Chapter2.Node);
        yield return WinBattle("battle_10");
        yield return PlayStage();
        Assert.IsTrue(run.Chapter2.Has(ChapterState.ZhaoYunGuest));
        Assert.AreEqual("A_invited", run.Chapter2.Recall("zhao_yun", "zhaoyun_liubei"));
        yield return WinBattle("battle_11");
        Assert.AreEqual(ChapterStep.Zhending, run.Chapter2.Step);

        // Zhending, back to Pingyuan: the assassin (choice ⑦ A), scroll ⑧ (no seal since 0.5), then 193 opens in Pingyuan.
        yield return PlayStage();
        yield return WaitFor<FieldStoryController>(90f);
        py = Object.FindFirstObjectByType<FieldStoryController>();
        yield return Drive(py, py.Stage);
        yield return PlayStage();
        yield return WaitFor<FieldStoryController>(90f);
        Assert.AreEqual(GameRun.BohaiScene, SceneManager.GetActiveScene().name);
        Assert.AreEqual(ChapterScripts4.ScrollJieqiao, StoryCinematics.LastScroll);
        Assert.AreEqual(ChapterStep.Beihai, run.Chapter2.Step);
        Assert.AreEqual(ChronicleNode.N7, run.Chapter2.Node);
        Assert.AreEqual("A_free", run.Chapter2.Recall("liu_ping", "liuping_assassin"));
        Assert.That(run.Data.Party.Select(g => run.Progress.Level(g)), Is.All.GreaterThanOrEqualTo(11));
    }
}
