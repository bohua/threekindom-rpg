using System.Collections;
using NUnit.Framework;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Gameplay.Story;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// City gates (user 2026-10-22): two guards at each gate let Liu Bei in but not out; once Guan Yu and Zhang Fei have
// joined the south guards are gone and a Yellow Turban squad blocks the gate, whose general challenges him on approach.
public sealed class GateGuardTests
{
    [SetUp] public void Fresh() => GameRun.Clear();
    [TearDown] public void Clean() => GameRun.Clear();

    static void Teleport(TownStoryController c, Vector3 at)
    {
        var cc = c.Player.GetComponent<CharacterController>();
        cc.enabled = false; c.Player.transform.position = at; cc.enabled = true;
    }

    static IEnumerator Finish(TownStoryController c)
    {
        float deadline = Time.realtimeSinceStartup + 10f;
        while (c.Busy) { Assert.Less(Time.realtimeSinceStartup, deadline); yield return null; c.Advance(); }
    }

    [UnityTest] public IEnumerator GuardsLetLiuBeiInButNotOut()
    {
        yield return SceneManager.LoadSceneAsync("Zhuojun_Town");
        yield return null;
        var c = Object.FindFirstObjectByType<TownStoryController>();
        c.FadeTime = 0.02f;
        Assert.AreEqual(2, c.Gates.Length);
        foreach (var g in c.Gates) Assert.AreEqual(2, g.Guards.childCount, g.Id + " gate has two guards");

        // Coming in from outside through the south gate is fine.
        Teleport(c, new Vector3(0, 0, -36f));
        yield return null; yield return null;
        Assert.IsFalse(c.Busy, "walking in is allowed");
        Teleport(c, new Vector3(0, 0, -28f));
        yield return null;
        Assert.IsTrue(c.WasInside);

        // Going back out is not: the guards stop him and send him back in.
        foreach (var gate in c.Gates)
        {
            Teleport(c, gate.Center - Vector3.up);
            yield return null; yield return null;
            Assert.IsTrue(c.Busy, "the " + gate.Id + " guards stop him");
            Assert.AreEqual("guard", c.Line?.Speaker);
            yield return Finish(c);
            Assert.IsTrue(c.TownInside.Contains(c.Player.transform.position), "sent back inside the " + gate.Id + " gate");
        }

        // Both brothers joined (and the oath done): the south guards stand down, the Yellow Turbans arrive.
        var story = c.Run.Story;
        story.Join(StoryState.GuanYu); story.Join(StoryState.ZhangFei);
        Assert.IsTrue(story.BeginOath()); story.CompleteOath();
        yield return SceneManager.LoadSceneAsync("Zhuojun_Town");
        yield return null;
        c = Object.FindFirstObjectByType<TownStoryController>();
        Assert.IsFalse(System.Array.Find(c.Gates, g => g.Id == "south").Guards.gameObject.activeSelf, "no guards at the south gate");
        Assert.IsTrue(System.Array.Find(c.Gates, g => g.Id == "west").Guards.gameObject.activeSelf, "the west gate stays guarded");
        Assert.IsNotNull(c.BlockerLeader);
        Assert.AreEqual("程远志", c.BlockerLeader.name);

        // Walking through the south passage is allowed now; near the general, the challenge starts by itself.
        Teleport(c, new Vector3(0, 0, -36f));
        yield return null; yield return null;
        Assert.IsFalse(c.Busy, "not yet in range");
        Teleport(c, new Vector3(0, 0, -39.6f));
        yield return null; yield return null;
        Assert.IsTrue(c.Busy, "the general challenges Liu Bei");
        Assert.AreEqual("cheng_yuanzhi", c.Line?.Speaker);
    }
}
