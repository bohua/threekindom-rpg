using System.Collections;
using NUnit.Framework;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.UI.Menu;
using ThreeKingdoms.UI.Story;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// The prologue 天下大势 (design/gdd/prologue.md): the title's 新游戏 plays it, then Liu Bei stands at the notice board
// with control; it can be skipped at any time; only a new game plays it; the travel menu's 史事 page replays it.
public sealed class PrologueTests
{
    [SetUp] public void Fresh() { GameRun.Clear(); GameRun.PrologueDue = false; StoryCinematics.SkipPrologue = false; }
    [TearDown] public void Clean() { GameRun.Clear(); GameRun.PrologueDue = false; StoryCinematics.SkipPrologue = false; }

    static IEnumerator OpenTown()
    {
        yield return SceneManager.LoadSceneAsync("Zhuojun_Town");
        yield return null;
    }

    static IEnumerator WaitFor(System.Func<bool> done, float seconds, string what)
    {
        float deadline = Time.realtimeSinceStartup + seconds;
        while (!done())
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, what);
            yield return null;
        }
    }

    [UnityTest] public IEnumerator NewGamePlaysAllNineShotsThenLandsAtTheNoticeBoard()
    {
        GameRun.PrologueDue = true;
        int before = StoryCinematics.PrologueCount;
        yield return OpenTown();
        var c = Object.FindFirstObjectByType<TownStoryController>();
        var view = Object.FindFirstObjectByType<StoryCinematicsView>();
        Assert.IsNotNull(view, "the town has the cinematics layer");
        view.PrologueSpeed = 40f;
        Assert.IsFalse(GameRun.PrologueDue, "taken once");
        Assert.AreEqual(before + 1, StoryCinematics.PrologueCount);
        Assert.IsTrue(c.Busy && c.Cinematic, "the town waits under the prologue");
        Assert.IsFalse(c.Player.InputEnabled);

        int highest = -1;
        yield return WaitFor(() => { highest = Mathf.Max(highest, view.PrologueShot); return !view.Playing; }, 60f, "prologue ends");
        Assert.AreEqual(PrologueText.Shots.Length - 1, highest, "every shot played");
        yield return WaitFor(() => !c.Busy, 5f, "control returns");
        Assert.IsTrue(c.Player.InputEnabled);
        float d = Vector3.Distance(c.Player.transform.position, c.NoticeSpot.position);
        Assert.Less(d, 0.3f, "Liu Bei stands at the notice board");
        yield return null;
        Assert.AreEqual("notice", c.Prompt, "reading the notice is the next step");
    }

    // User 2026-10-30: a new game opens in Lousang (the R1 map); after the prologue Liu Bei talks with his mother, then
    // the goal sends him to Zhuojun for the notice.
    [UnityTest] public IEnumerator NewGameOpensInLousangWithHisMother()
    {
#if UNITY_EDITOR
        var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<ThreeKingdoms.Data.Battle.BattleCatalog>("Assets/_Project/Data/Battle/BattleCatalog.asset");
#else
        ThreeKingdoms.Data.Battle.BattleCatalog catalog = null;
#endif
        GameRun.NewGame(catalog);
        GameRun.PrologueDue = true;
        yield return SceneManager.LoadSceneAsync(GameRun.R1Scene);
        yield return null;
        var c = Object.FindFirstObjectByType<FieldStoryController>();
        Assert.IsNotNull(c, "the R1 map has its story layer");
        Assert.IsFalse(GameRun.PrologueDue, "taken once");
        var mother = c.Find("ls_mother");
        Assert.IsTrue(mother && mother.gameObject.activeInHierarchy, "his mother is at home");
        Assert.Less(Vector3.Distance(c.Player.transform.position, mother.position), 2.5f, "Liu Bei starts beside her");
        yield return WaitFor(() => Object.FindFirstObjectByType<StoryCinematicsView>().PrologueShot >= 0, 5f, "prologue starts");
        StoryCinematics.SkipPrologue = true;
        // The talk with his mother follows; move it along until control returns.
        float deadline = Time.realtimeSinceStartup + 30f;
        bool talked = false;
        while (c.Busy)
        {
            if (c.Line.HasValue) { talked |= c.Line.Value.Speaker == "liu_bei_mu"; c.Stage.Advance(); }
            Assert.Less(Time.realtimeSinceStartup, deadline, "the opening ends");
            yield return null;
        }
        Assert.IsTrue(talked, "his mother speaks");
        Assert.IsTrue(c.Player.InputEnabled);
        StringAssert.Contains("涿郡", c.Objective, "the goal is the notice in Zhuojun");
    }

    [UnityTest] public IEnumerator SkippingEndsItAtOnce()
    {
        GameRun.PrologueDue = true;
        yield return OpenTown();
        var c = Object.FindFirstObjectByType<TownStoryController>();
        var view = Object.FindFirstObjectByType<StoryCinematicsView>();
        yield return WaitFor(() => view.PrologueShot >= 0, 5f, "prologue starts");
        StoryCinematics.SkipPrologue = true;
        yield return WaitFor(() => !c.Busy, 5f, "skip returns control");
        Assert.IsTrue(c.Player.InputEnabled);
        Assert.Less(Vector3.Distance(c.Player.transform.position, c.NoticeSpot.position), 0.3f);
    }

    [Test] public void BothNarrationsHaveTheSameLinesAndAVoiceForEach()
    {
        for (int s = 0; s < PrologueText.Shots.Length; s++)
        {
            Assert.AreEqual(PrologueText.Shots[s].Lines.Length, PrologueText.ClassicLines[s].Length, $"shot {s + 1}: 演义体 line count matches the pictures' timing");
            for (int l = 0; l < PrologueText.Shots[s].Lines.Length; l++)
            {
                Assert.IsNotNull(Resources.Load<AudioClip>($"Audio/Voice/Prologue/prologue_{s + 1}_{l + 1}"), $"modern voice {s + 1}-{l + 1}");
                Assert.IsNotNull(Resources.Load<AudioClip>($"Audio/Voice/Prologue/prologue_c_{s + 1}_{l + 1}"), $"classic voice {s + 1}-{l + 1}");
            }
        }
    }

    [UnityTest] public IEnumerator OnlyANewGamePlaysIt()
    {
        int before = StoryCinematics.PrologueCount;
        yield return OpenTown();
        var c = Object.FindFirstObjectByType<TownStoryController>();
        Assert.AreEqual(before, StoryCinematics.PrologueCount, "a town opened without 新游戏 (a load, a return) has no prologue");
        Assert.IsFalse(c.Busy);
    }

    [UnityTest] public IEnumerator ChronicleTabReplaysIt()
    {
        yield return OpenTown();
        var c = Object.FindFirstObjectByType<TownStoryController>();
        var menu = Object.FindFirstObjectByType<MainMenuHud>();
        var view = Object.FindFirstObjectByType<StoryCinematicsView>();
        var at = c.Player.transform.position;
        c.OpenPanel(TownStoryController.MenuPanel);
        menu.Show(MainMenuHud.Tab.Chronicle);
        Assert.AreEqual("史事", MainMenuHud.TabName(MainMenuHud.Tab.Chronicle));
        menu.Confirm();
        Assert.IsNull(c.Panel, "the menu closes for the replay");
        yield return WaitFor(() => view.PrologueShot >= 0, 5f, "replay starts");
        Assert.IsTrue(c.Busy);
        StoryCinematics.SkipPrologue = true;
        yield return WaitFor(() => !c.Busy, 5f, "replay ends");
        Assert.Less(Vector3.Distance(c.Player.transform.position, at), 0.3f, "a replay leaves Liu Bei where he was");
        Assert.IsTrue(c.Player.InputEnabled);
    }
}
