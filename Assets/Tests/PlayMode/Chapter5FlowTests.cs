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

// 0.5 徐州篇 acceptance (V05_XUZHOU_PLAN, SYSTEMS_V05 §7): Taishi Ci at Pingyuan (choice ⑧ B, then the gate), the
// formation and battle 12 with reserves, Kong Rong's hall, 194, battle 13's waves and the way back in, Xiapi (F6), battle
// 14, the echoes, the optional patrol (battle 15), the third offer and the seal, Lü Bu, the settlement, the title.
public sealed class Chapter5FlowTests
{
    string saveDir;
    readonly Queue<int> picks = new Queue<int>();

    [SetUp] public void Fresh()
    {
        GameRun.Clear();
        StagePlayer.AutoSeconds = 0f;
        ChapterTactics.AutoPlay = true;
        Time.timeScale = 6f;
        saveDir = System.IO.Path.Combine(Application.temporaryCachePath, "chapter5-" + System.Guid.NewGuid().ToString("N"));
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
        Debug.Log($"CHAPTER5_TEST {expected} won in round {game.Rounds}");
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
    public IEnumerator TheXuzhouChapterPlaysThrough()
    {
        var run = GameRun.ForChapter(Catalog, ChapterStep.Beihai, exp: 620, copper: 600);
        foreach (var f in new[] { ChapterState.ZhaoYunGuest, ChapterState.Year193, ChapterState.TianYu }) run.Chapter2.Set(f);
        SceneManager.LoadScene(GameRun.BohaiScene);
        yield return WaitFor<FieldStoryController>(90f);
        var py = Object.FindFirstObjectByType<FieldStoryController>();
        Assert.IsTrue(py.Busy, "F1 plays on arrival");
        picks.Enqueue(1);   // ⑧ B: ask first, he waits at the gate
        yield return Drive(py, py.Stage);
        Assert.IsTrue(run.Chapter2.Has(ChapterScripts5.TaishiArrived));
        Assert.AreEqual("B_asked", run.Chapter2.Recall("taishi_ci", "taishici_plea"));

        // The south gate, the march, the formation (Zhao Yun and Taishi Ci), battle 12.
        yield return TalkTo(py, "taishi_ci", 0);
        picks.Enqueue(1);
        yield return PlayStage();
        yield return WinBattle("battle_12");
        Assert.AreEqual(ChapterStep.XuzhouRoad, run.Chapter2.Step);

        // Scroll ⑨, X14, Kong Rong's hall, Taishi Ci leaves (no keepsake after ⑧ B), 194, Cao Bao, battle 13.
        yield return PlayStage();
        Assert.AreEqual(ChapterScripts5.ScrollBeihai, StoryCinematics.LastScroll);
        Assert.IsFalse(run.Chapter2.Has(ChapterScripts5.TaishiToken));
        Assert.AreEqual(ChronicleNode.N8, run.Chapter2.Node);
        Assert.AreEqual("held_himself", run.Chapter2.Recall("zhang_fei", "zhangfei_caobao"));
        yield return WinBattle("battle_13");
        Assert.AreEqual(ChapterStep.Xiapi, run.Chapter2.Step);

        // Xiapi: F6–F7 on arrival, the autosave, the east gate, battle 14.
        yield return WaitFor<FieldStoryController>(90f);
        var xp = Object.FindFirstObjectByType<FieldStoryController>();
        Assert.AreEqual(GameRun.XiapiScene, SceneManager.GetActiveScene().name);
        yield return Drive(xp, xp.Stage);
        Assert.IsTrue(run.Chapter2.Has(ChapterScripts5.XiapiGreeted));
        Assert.AreEqual(SaveReadStatus.Ok, run.Slots().Read(SaveSlots.Auto, out var auto));
        Assert.AreEqual(SaveData.XiapiLocation, auto.location);
        yield return TalkTo(xp, "xp_officer", 0);
        yield return WinBattle("battle_14");

        // F8–F9, back in Xiapi for the echoes; the optional patrol from Sun Qian's post station.
        yield return PlayStage();
        yield return WaitFor<FieldStoryController>(90f);
        xp = Object.FindFirstObjectByType<FieldStoryController>();
        yield return Drive(xp, xp.Stage);
        Assert.AreEqual(ChapterStep.Echo, run.Chapter2.Step);
        Assert.IsTrue(run.Chapter2.PatrolOpen);
        yield return TalkTo(xp, "jian_yong");
        yield return TalkTo(xp, "xp_old_soldier");
        Assert.AreEqual(2, FieldScripts5.EchoesHeard(run.Chapter2));
        yield return TalkTo(xp, "sun_qian", 1);
        yield return WinBattle("battle_15");
        Assert.IsTrue(run.Chapter2.Has(ChapterState.Patrol15));
        Assert.IsFalse(run.RecordVictory("battle_15"), "the patrol pays once");
        yield return WaitFor<FieldStoryController>(90f);
        xp = Object.FindFirstObjectByType<FieldStoryController>();
        yield return Drive(xp, xp.Stage);

        // The third offer (受印), scroll ⑩ with the seal, Zhao Yun, Lü Bu (⑨ B), 196, the settlement, the title.
        yield return TalkTo(xp, "xp_clerk", 0);
        picks.Enqueue(0); picks.Enqueue(1);
        yield return WaitFor<StageController>();
        var stage = Object.FindFirstObjectByType<StageController>();
        yield return Drive(stage, stage.Player, 300f);
        float deadline = Time.realtimeSinceStartup + 60f;
        while (SceneManager.GetActiveScene().name != GameRun.TitleScene) { Assert.Less(Time.realtimeSinceStartup, deadline); StoryCinematics.Advance(); yield return null; }
        Assert.AreEqual(ChapterScripts5.ScrollSanrang, StoryCinematics.LastScroll);
        Assert.AreEqual(ChapterStep.Done05, run.Chapter2.Step);
        Assert.AreEqual("entrusted", run.Chapter2.Recall("tao_qian", "taoqian_seal"));
        Assert.AreEqual("B_cautious", run.Chapter2.Recall("lu_bu", "lubu_xuzhou"));
        Assert.AreEqual(SaveReadStatus.Ok, run.Slots().Read(SaveSlots.Auto, out auto));
        Assert.AreEqual(SaveData.ChapterDoneLocation, auto.location);
        Assert.That(run.Data.Party.Select(g => run.Progress.Level(g)), Is.All.GreaterThanOrEqualTo(14));
    }
}
