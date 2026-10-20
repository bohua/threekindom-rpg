using System.Collections;
using System.Linq;
using NUnit.Framework;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Gameplay;
using ThreeKingdoms.Gameplay.Story;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// The 12 Zhuojun residents (ZHUOJUN_TOWN_DESIGN.md §4, ZHUOJUN_RESIDENTS.md sprites): all at their posts with their
// own atlas, three walk short routes with the drawn cycle and stop while a conversation holds the town, and talking
// gives one line that changes after the oath.
public sealed class ResidentTests
{
    [SetUp] public void Fresh() => GameRun.Clear();
    [TearDown] public void Clean() => GameRun.Clear();

    [UnityTest] public IEnumerator ResidentsStandWalkAndTalk()
    {
        yield return SceneManager.LoadSceneAsync("Zhuojun_Town");
        yield return null;
        var c = Object.FindFirstObjectByType<TownStoryController>();
        Assert.AreEqual(12, c.Residents.Length);
        Assert.That(c.Residents.Select(TownStoryController.ResidentId), Is.EqualTo(Enumerable.Range(1, 12).Select(i => $"N{i:00}")));
        Assert.AreSame(c.Resident("N04"), c.Smith, "the smith is the N04 resident");
        Assert.AreSame(c.Resident("N05"), c.Innkeeper, "the innkeeper is the N05 resident");
        foreach (var r in c.Residents)
        {
            Assert.IsNotNull(r.GetComponent<CapsuleCollider>(), r.name + " stands in the way like a person");
            var tex = r.GetComponentInChildren<MeshRenderer>().sharedMaterial.GetTexture("_BaseMap");
            Assert.IsNotNull(tex, r.name + " has its own atlas");
        }

        var walkers = c.Residents.Where(r => r.GetComponent<NpcPatrol>()).ToArray();
        Assert.That(walkers.Select(TownStoryController.ResidentId), Is.EquivalentTo(new[] { "N02", "N10", "N12" }));
        foreach (var w in walkers) Assert.IsTrue(w.GetComponent<PixelWalkFrames>().HasWalkCycle, w.name + " plays its drawn walk");
        var porter = c.Resident("N12");
        Vector3 start = porter.position;
        float deadline = Time.realtimeSinceStartup + 6f;
        while (Vector3.Distance(porter.position, start) < 1f && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.Greater(Vector3.Distance(porter.position, start), 1f, "the porter walks his route");

        // Talk to the elder by the well: one line, the porter stops while the conversation holds the town.
        var elder = c.Resident("N09");
        var cc = c.Player.GetComponent<CharacterController>();
        cc.enabled = false; c.Player.transform.position = elder.position + new Vector3(0, 0, -1.2f); cc.enabled = true;
        yield return null;
        Assert.AreEqual("npc:N09", c.Prompt);
        c.Interact();
        Assert.IsTrue(c.Busy);
        Assert.AreEqual("npc.N09", c.Line?.Line);
        yield return null;
        Vector3 held = porter.position;
        for (int i = 0; i < 20; i++) yield return null;
        Assert.AreEqual(held, porter.position, "residents stop during a conversation");
        while (c.Busy) { c.Advance(); yield return null; }

        // After the oath the gate guard has something new to say.
        c.Run.Story.Join(StoryState.GuanYu); c.Run.Story.Join(StoryState.ZhangFei);
        c.Run.Story.BeginOath(); c.Run.Story.CompleteOath();
        c.TalkToResident("N01");
        Assert.AreEqual("npc.N01.after", c.Line?.Line);
        while (c.Busy) { yield return null; c.Advance(); }
    }
}
