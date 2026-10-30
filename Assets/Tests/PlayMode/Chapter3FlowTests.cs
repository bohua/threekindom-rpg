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

// 0.3 讨董篇 acceptance (V03_TAODONG_PLAN §8 step 7, SYSTEMS_V03 §6): one play-through from Gongsun Zan's garrison to the
// end of 0.3 — D4, the optional drill (battle 7), Tian Yu, D5, Suanzao, sharing the grain, D6–D7 with scroll ④, the
// camp gate, battle 8 at Hulao Pass with choice ④, scroll ⑤, the chronicle into 191, the ruins (choice ⑤), D11, scroll ⑥.
public sealed class Chapter3FlowTests
{
    string saveDir;
    readonly Queue<int> picks = new Queue<int>();

    [SetUp] public void Fresh()
    {
        GameRun.Clear();
        StagePlayer.AutoSeconds = 0f;
        ChapterTactics.AutoPlay = true;
        Time.timeScale = 6f;
        saveDir = System.IO.Path.Combine(Application.temporaryCachePath, "chapter3-" + System.Guid.NewGuid().ToString("N"));
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
        Debug.Log($"CHAPTER3_TEST {expected} won in round {game.Rounds}");
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

    [UnityTest, Timeout(900000)]   // the whole chapter on a busy machine runs past the default 180 s
    public IEnumerator TheTaodongChapterPlaysThrough()
    {
        // Gongsun Zan's garrison: arrival autosave, D4, 众望 learned.
        var run = GameRun.ForChapter(Catalog, ChapterStep.Gongsun, exp: 210, copper: 400);
        SceneManager.LoadScene(GameRun.GongsunScene);
        yield return WaitFor<FieldStoryController>();
        var gs = Object.FindFirstObjectByType<FieldStoryController>();
        Assert.IsTrue(gs.Busy, "D4 plays on arrival");
        yield return Drive(gs, gs.Stage);
        Assert.IsTrue(run.Chapter2.Has(ChapterScripts3.GongsunGreeted));
        Assert.IsTrue(run.Chapter2.Has(ChapterScripts3.ZhongwangLearned));
        Assert.AreEqual(SaveReadStatus.Ok, run.Slots().Read(SaveSlots.Auto, out var auto));
        Assert.AreEqual(SaveData.GongsunLocation, auto.location);
        Assert.AreEqual(ChronicleNode.N4, run.Chapter2.Node);
        yield return TalkTo(gs, "bell");
        Assert.AreEqual(1, run.Progress.Inventory.Count(run.Data.Economy.Find("baima_bell")));
        // 田豫点兵 (npc-sidequests sq.lb1.r1.tian_yu_roll): offer, the four squads, the sick man behind the hay, back to Tian Yu.
        yield return TalkTo(gs, "tian_yu");
        Assert.IsTrue(run.Chapter2.Has(FieldScripts3.RollStarted));
        for (int n = 1; n <= 4; n++) yield return TalkTo(gs, "roll_head_" + n);
        yield return TalkTo(gs, "roll_sick");
        Assert.IsTrue(run.Chapter2.Has(FieldScripts3.RollFound));
        yield return TalkTo(gs, "tian_yu");
        Assert.IsTrue(run.Chapter2.Has(ChapterState.TianYu));
        Assert.AreEqual("fair", run.Chapter2.Recall("tian_yu", "first_roll"));

        // The optional drill (battle 7): once, back to the garrison, no step change.
        yield return TalkTo(gs, "baima_rider", 0);
        yield return WinBattle("battle_7");
        yield return WaitFor<FieldStoryController>();
        gs = Object.FindFirstObjectByType<FieldStoryController>();
        Assert.AreEqual(ChapterStep.Gongsun, run.Chapter2.Step);
        Assert.IsTrue(run.Chapter2.Has(ChapterState.Drill));
        Assert.IsFalse(run.Chapter2.DrillOpen);
        Assert.IsFalse(run.RecordVictory("battle_7"), "the drill pays once");

        // 简雍打听 in Ji is played on the R1 map (R1SidequestTests); here it is taken as done before D5.
        run.Chapter2.Set(FieldScriptsR1.JianYongEar);
        // D5 and the departure (confirm), the march, Suanzao.
        yield return TalkTo(gs, "gongsun_zan", 0);
        yield return PlayStage();
        yield return WaitFor<FieldStoryController>();
        var sz = Object.FindFirstObjectByType<FieldStoryController>();
        Assert.AreEqual(GameRun.SuanzaoScene, SceneManager.GetActiveScene().name);
        Assert.AreEqual("follow", run.Chapter2.Recall("tian_yu", "tianyu_follow"));
        yield return Drive(sz, sz.Stage);   // arrival
        int rep = run.Life.Reputation;
        yield return TalkTo(sz, "sz_soldier_a", 0);   // a ration for the hungry soldiers
        Assert.AreEqual(rep + 1, run.Life.Reputation);

        // D6 and D7 in the leader's tent, scroll ④, back to the plaza.
        yield return TalkTo(sz, "yuan_shao");
        yield return PlayStage();
        Assert.AreEqual(ChapterScripts3.ScrollWenjiu, StoryCinematics.LastScroll);
        Assert.AreEqual("seen", run.Chapter2.Recall("cao_cao", "caocao_wenjiu"));
        Assert.AreEqual("held_back", run.Chapter2.Recall("zhang_fei", "zhangfei_suanzao"));
        yield return WaitFor<FieldStoryController>();
        sz = Object.FindFirstObjectByType<FieldStoryController>();
        Assert.IsTrue(run.Chapter2.Has(ChapterScripts3.Wenjiu));

        // Out of the camp gate (leave, confirm) to Hulao Pass; battle 8 (the AI takes choice ④ A).
        yield return TalkTo(sz, "sz_gs_soldier", 1, 0);
        yield return PlayStage();
        yield return WinBattle("battle_8");
        Assert.IsTrue(run.Chapter2.Has(ChapterScripts3.HulaoChoice + "0"), "choice ④ comes back as a flag");
        Assert.AreEqual("shielded", run.Chapter2.Recall("zhang_fei", "zhangfei_shield"));
        Assert.AreEqual("seen", run.Chapter2.Recall("lu_bu", "lubu_three"));

        // Scroll ⑤, the chronicle into 191, the ruins (choice ⑤ A), D11, the after-story, scroll ⑥, the title.
        yield return WaitFor<StageController>();
        var stage = Object.FindFirstObjectByType<StageController>();
        Assert.AreEqual(ChapterScripts3.AfterHulao, stage.StageId);
        rep = run.Life.Reputation;
        yield return Drive(stage, stage.Player, 240f);
        // 0.4: scroll ⑥ (no seal), then section six opens on the story stage and leads to the Bohai plain.
        yield return WaitFor<StageController>();
        var hebei = Object.FindFirstObjectByType<StageController>();
        Assert.AreEqual(ChapterScripts3.ScrollFenLuoyang, StoryCinematics.LastScroll);
        Assert.AreEqual(ChapterScripts4.HebeiOpen, hebei.StageId);
        yield return Drive(hebei, hebei.Player);
        yield return WaitFor<FieldStoryController>(60f);
        Assert.AreEqual(GameRun.BohaiScene, SceneManager.GetActiveScene().name);
        Assert.AreEqual(ChapterStep.Pingyuan, run.Chapter2.Step);
        Assert.AreEqual(ChronicleNode.N5, run.Chapter2.Node);
        Assert.AreEqual(rep + 1, run.Life.Reputation, "choice ⑤ A");
        Assert.AreEqual("A_bury", run.Chapter2.Recall("liu_bei", "liubei_ruins"));
        Assert.That(run.Data.Party.Select(g => run.Progress.Level(g)), Is.All.EqualTo(9));
    }
}
