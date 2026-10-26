using System.Collections;
using Jrpg;
using NUnit.Framework;
using ThreeKingdoms.Gameplay.Story;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Every other play-mode test starts a new game in Zhuojun and expects control at once, so the first-arrival tour is off
// for the whole assembly and switched on only by ArrivalIntroTests.
[SetUpFixture]
public sealed class NoArrivalIntroForTests
{
    [OneTimeSetUp] public void Off() => TownStoryController.PlayArrivalIntro = false;
    [OneTimeTearDown] public void On() => TownStoryController.PlayArrivalIntro = true;
}

// The first look at Zhuojun (user 2026-10-26): eye level in through the south gate to the square and the hills, then
// each landmark; confirm moves on; control returns outside the south gate; a return to town never replays it.
public sealed class ArrivalIntroTests
{
    [SetUp] public void Fresh() { GameRun.Clear(); TownStoryController.PlayArrivalIntro = true; }
    [TearDown] public void Off() { TownStoryController.PlayArrivalIntro = false; GameRun.Clear(); }

    [UnityTest] public IEnumerator NewGameTourThenControl()
    {
        yield return SceneManager.LoadSceneAsync("Zhuojun_Town");
        yield return null;
        var c = Object.FindFirstObjectByType<TownStoryController>();
        c.FadeTime = 0.02f;
        Assert.IsTrue(c.ArrivalPlaying, "a new game opens on the tour");
        Assert.IsTrue(c.Busy && !c.Player.InputEnabled, "no control during the tour");
        Assert.IsTrue(c.Letterbox);
        var cam = c.Camera.transform;

        // Halfway through the walk-in the camera is at eye level inside the gate, looking north.
        yield return new WaitForSeconds(TownStoryController.WalkInSeconds * 0.5f);
        Assert.AreEqual(0, c.ArrivalShotIndex);
        Assert.Less(cam.position.y, 2.2f, "eye level");
        Assert.Greater(cam.position.z, -36f, "through the gate");
        Assert.Greater(cam.forward.z, 0.9f, "looking up the axis");

        // The walk ends looking over the commandery at the hills.
        float deadline = Time.realtimeSinceStartup + 20f;
        while (c.ArrivalShotIndex < 1) { Assert.Less(Time.realtimeSinceStartup, deadline); yield return null; }
        yield return new WaitForSeconds(0.5f);
        Assert.Greater(cam.forward.y, 0.05f, "the gaze lifts toward the hills");

        // Confirm skips through the landmarks.
        int seen = 1;
        while (c.ArrivalPlaying)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline + 60f);
            yield return new WaitForSeconds(0.5f);
            seen = Mathf.Max(seen, c.ArrivalShotIndex);
            c.Advance();
        }
        Assert.GreaterOrEqual(seen, 6, "the landmarks play one after another");
        yield return new WaitForSeconds(0.3f);
        Assert.IsFalse(c.Busy);
        Assert.IsTrue(c.Player.InputEnabled, "control returns after the tour");
        Assert.IsFalse(c.Camera.Paused);
        Assert.Less(c.Player.transform.position.z, -40f, "the game starts outside the south gate");
        Assert.IsFalse(c.Letterbox);
    }

    [UnityTest] public IEnumerator ReturningToTownDoesNotReplay()
    {
        TownStoryController.PlayArrivalIntro = false;   // a game already under way
        yield return SceneManager.LoadSceneAsync("Zhuojun_Town");
        yield return null;
        TownStoryController.PlayArrivalIntro = true;
        GameRun.Current.NextArrival = GameRun.Arrival.ReturnFromBattle;
        yield return SceneManager.LoadSceneAsync("Zhuojun_Town");
        yield return null;
        var c = Object.FindFirstObjectByType<TownStoryController>();
        Assert.IsFalse(c.ArrivalPlaying);
        Assert.IsFalse(c.Busy);
    }
}
