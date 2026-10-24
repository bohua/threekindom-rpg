using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ThreeKingdoms.Core.Town;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.UI.Story;
using ThreeKingdoms.UI.Town;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Town life in play (user 2026-10-22): alms for the beggar, the scholar's quiz, big-or-small at the gambling house
// and the archery range, plus the new townsfolk.
public sealed class TownLifePlayTests
{
    [SetUp] public void Fresh() => GameRun.Clear();
    [TearDown] public void Clean() => GameRun.Clear();

    static void Teleport(TownStoryController c, Vector3 at)
    {
        var cc = c.Player.GetComponent<CharacterController>();
        cc.enabled = false; c.Player.transform.position = at; cc.enabled = true;
    }

    // Walks up to someone, talks, answers each choice in turn and advances every line until the town is free again.
    static IEnumerator Talk(TownStoryController c, Transform who, string prompt, Queue<int> answers, List<string> heard = null)
    {
        Teleport(c, who.position + new Vector3(0, 0, -1.3f));
        yield return null;
        c.Interact();
        Assert.IsTrue(c.Busy, "talking to " + who.name);
        float deadline = Time.realtimeSinceStartup + 20f;
        while (c.Busy && c.Panel == null)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline);
            yield return null;
            if (c.Line.HasValue) heard?.Add(c.Line.Value.Line);
            if (c.Choices != null) c.Choose(answers.Count > 0 ? answers.Dequeue() : 0);
            else c.Advance();
        }
    }

    // Townsfolk with a mini-game wear a "?" (user 2026-10-24): the scholar, the ruffian and the archer, not the beggar;
    // it goes away while a mini-game panel is open.
    [UnityTest] public IEnumerator MiniGameTownsfolkWearQuestionMarks()
    {
        yield return SceneManager.LoadSceneAsync("Zhuojun_Town");
        yield return null;
        var c = Object.FindFirstObjectByType<TownStoryController>();
        var hud = Object.FindFirstObjectByType<TownHud>();
        Teleport(c, new Vector3(0, 0, -29f));
        float deadline = Time.realtimeSinceStartup + 10f;
        while (hud.GameBubbles.Count < 3 && Time.realtimeSinceStartup < deadline) yield return null;
        CollectionAssert.AreEquivalent(new[] { c.Scholar, c.Ruffian, c.Archer }, hud.GameBubbles.ToArray());
        Assert.IsFalse(hud.GameBubbles.Contains(c.Beggar));

        c.OpenArchery();
        yield return null; yield return null;
        Assert.AreEqual(0, hud.GameBubbles.Count, "no marks over an open mini-game");
    }

    [UnityTest] public IEnumerator BeggarScholarCasinoAndRange()
    {
        yield return SceneManager.LoadSceneAsync("Zhuojun_Town");
        yield return null;
        var c = Object.FindFirstObjectByType<TownStoryController>();
        c.Run.Random = new System.Random(4);
        var inv = c.Run.Progress.Inventory;
        var life = c.Run.Life;
        Assert.AreEqual(24, c.Ambient.Length, "twenty kinds of townsfolk, three drilling soldiers and three recruits among them");
        Assert.IsTrue(c.Ambient.Any(t => t.GetComponent<ThreeKingdoms.Gameplay.PoseCycle>()), "some are busy at something");

        // Alms: twenty copper, three reputation.
        int copper = inv.Copper;
        yield return Talk(c, c.Beggar, "beggar", new Queue<int>(new[] { 1 }));
        Assert.AreEqual(copper - 20, inv.Copper);
        Assert.AreEqual(3, life.Reputation);

        // The scholar asks three questions, once between battles.
        var heard = new List<string>();
        int rep = life.Reputation;
        yield return Talk(c, c.Scholar, "scholar", new Queue<int>(new[] { 0 }), heard);
        Assert.AreEqual(3, heard.Distinct().Count(l => l.StartsWith("raw:（第")), "three questions");
        Assert.AreEqual(0, life.QuizTakenAt);
        Assert.AreNotEqual(rep, life.Reputation, "the quiz changes reputation either way");
        heard.Clear();
        yield return Talk(c, c.Scholar, "scholar", new Queue<int>(), heard);
        Assert.Contains("scholar.later", heard, "only once until the next battle");

        // Big or small: the stake is won or lost, the dice are shown.
        copper = inv.Copper;
        bool sawDice = false;
        Teleport(c, c.Ruffian.position + new Vector3(0, 0, -1.3f));
        yield return null;
        c.Interact();
        var answers = new Queue<int>(new[] { 0, 0, 2 });   // big, ten copper, then leave
        float deadline = Time.realtimeSinceStartup + 20f;
        while (c.Busy)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline);
            yield return null;
            sawDice |= c.Dice != null;
            if (c.Choices != null) c.Choose(answers.Count > 0 ? answers.Dequeue() : 2);
            else c.Advance();
        }
        Assert.IsTrue(sawDice, "the dice are on the table");
        Assert.AreEqual(10, Mathf.Abs(inv.Copper - copper), "ten copper won or lost");

        // The range: three arrows, then back to the archer with a reward.
        yield return Talk(c, c.Archer, "archery", new Queue<int>(new[] { 0 }));
        Assert.AreEqual(TownStoryController.ArcheryPanel, c.Panel);
        var hud = Object.FindFirstObjectByType<ArcheryHud>();
        Assert.IsTrue(hud.Open);
        var game = c.Archery;
        for (int arrow = 0; arrow < ArcheryGame.Arrows; arrow++)
        {
            var target = game.Targets.First(t => !t.Hit);
            var (a, p) = AimAt(target);
            hud.SetAim(a, p);
            hud.Submit();
            while (hud.Flying) yield return null;
        }
        Assert.IsTrue(game.Finished);
        Assert.GreaterOrEqual(game.Score, 3, "aimed arrows hit");
        copper = inv.Copper;
        hud.Submit();   // back to the archer
        yield return null;
        Assert.IsNull(c.Panel);
        while (c.Busy) { yield return null; c.Advance(); }
        Assert.AreEqual(copper + game.CopperReward, inv.Copper, "the range pays");
    }

    static (float angle, float power) AimAt(ArcheryGame.Target t)
    {
        for (float a = ArcheryGame.MinAngle; a <= ArcheryGame.MaxAngle; a += 0.5f)
        for (float p = ArcheryGame.MinPower; p <= ArcheryGame.MaxPower; p += 0.01f)
            if (ArcheryGame.Path(a, p).Any(pt => (pt.x - t.X) * (pt.x - t.X) + (pt.y - t.Y) * (pt.y - t.Y) < 0.2f)) return (a, p);
        return (20f, 0.6f);
    }
}
