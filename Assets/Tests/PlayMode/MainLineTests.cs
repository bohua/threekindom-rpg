using System.Collections;
using System.Linq;
using Jrpg;
using NUnit.Framework;
using ThreeKingdoms.Core.Battle;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Gameplay.Battle;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.UI.Battle;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

// ADR-0006 acceptance: the whole Alpha 0.1 main line runs in one play-through — recruit Guan Yu and Zhang Fei, the
// automatic peach-garden oath, the south gate, three battles with a return to town in between, and the ending.
public sealed class MainLineTests
{
    [SetUp] public void Fresh() => GameRun.Clear();
    [TearDown] public void Clean() => GameRun.Clear();

    static void Teleport(PlayerMover p, Vector3 at)
    {
        var cc = p.GetComponent<CharacterController>();
        cc.enabled = false; p.transform.position = at; cc.enabled = true;
    }

    static IEnumerator Talk(TownStoryController c, Vector3 at, string expectedPrompt)
    {
        Teleport(c.Player, at);
        yield return null;
        c.Interact();
        Assert.IsTrue(c.Busy, "dialogue started for " + expectedPrompt);
        Assert.IsFalse(c.Player.InputEnabled, "movement locked during dialogue");
        float deadline = Time.realtimeSinceStartup + 30f;
        while (c.Busy)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline);
            yield return null;
            c.Advance();
        }
        Assert.IsTrue(c.Player.InputEnabled, "control returns after the sequence");
    }

    static IEnumerator FightAndWin()
    {
        yield return null;
        var b = Object.FindFirstObjectByType<BattleController>();
        Assert.IsNotNull(b, "a battle scene loaded");
        b.PlaybackSpeed = 4f; b.Fast = true;
        float deadline = Time.realtimeSinceStartup + 120f;
        while (b.Current != BattleController.Phase.Finished)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline);
            if (b.Current == BattleController.Phase.Commanding && b.Actor != null)
            {
                var s = b.Session.State;
                var actor = b.Actor;
                var target = s.Enemies.Where(e => e.Active).OrderBy(e => e.Troops).ThenBy(e => e.Slot).First();
                var hurt = s.Players.Where(u => u.Active && u.Troops * 2 < u.Stats.MaxTroops).OrderBy(u => u.Troops).FirstOrDefault();
                var heal = actor.Skills.FirstOrDefault(k => k.HealPercent > 0 && s.CanUseSkill(actor, k));
                var strike = actor.Skills.FirstOrDefault(k => k.Damage != DamageKind.None && s.CanUseSkill(actor, k));
                if (hurt != null && heal != null) b.Choose(BattleCommand.UseSkill(actor, heal, hurt));
                else if (strike != null) b.Choose(BattleCommand.UseSkill(actor, strike, strike.IsSingleTarget ? target : null));
                else b.Choose(BattleCommand.Attack(actor, target));
            }
            yield return null;
        }
        if (b.Session.Result.Outcome != BattleOutcome.Victory)
        {
            b.StartBattle();   // retry once (defeat restores the pre-battle state)
            yield return FightAndWin();
        }
    }

    static VisualElement HudRoot() => Object.FindFirstObjectByType<BattleHud>().GetComponent<UIDocument>().rootVisualElement;

    static Button ButtonText(string text) => HudRoot().Query<Button>().ToList().FirstOrDefault(b => b.text == text);

    static void Press(Button b)
    {
        using (var e = NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); }
    }

    [UnityTest] public IEnumerator WholeMainLinePlaysThrough()
    {
        yield return SceneManager.LoadSceneAsync("Zhuojun_Town");
        yield return null;
        var c = Object.FindFirstObjectByType<TownStoryController>();
        c.FadeTime = 0.02f;
        var story = c.Run.Story;
        Assert.AreEqual(StoryPhase.FindCompanions, story.Phase);
        Assert.Less(c.Player.transform.position.z, -40f, "a new game starts outside the south gate");

        // The gate refuses with a reason before the oath.
        Teleport(c.Player, new Vector3(0, 0, -36f));
        yield return null;
        c.Interact();
        Assert.AreEqual("gate:locked", c.Line?.Line);
        while (c.Busy) { yield return null; c.Advance(); }

        // Zhang Fei first, then Guan Yu (order is free); the oath starts by itself and ends inside the west gate.
        yield return Talk(c, c.ZhangFei.position + new Vector3(0, 0, -1.2f), "talk:zhang_fei");
        Assert.IsTrue(story.ZhangFeiJoined);
        Assert.IsFalse(c.ZhangFei.gameObject.activeSelf, "a joined companion leaves their post");
        // …and walks behind Liu Bei from then on (user 2026-10-20).
        var zf = c.ZhangFeiFollower;
        Assert.IsTrue(zf.gameObject.activeSelf, "Zhang Fei follows once he joins");
        Assert.IsFalse(c.GuanYuFollower.gameObject.activeSelf, "Guan Yu does not follow before joining");
        Assert.IsNull(zf.GetComponent<Collider>(), "a follower never blocks the player");
        var zfWalk = zf.GetComponent<ThreeKingdoms.Gameplay.PixelWalkFrames>();
        Assert.IsTrue(zfWalk.HasWalkCycle);
        bool followerWalked = false;
        for (int i = 0; i < 20; i++) { c.Player.Move(Vector2.right, 0.02f); yield return null; followerWalked |= zfWalk.Walking; }
        Assert.IsTrue(followerWalked, "the follower plays the walk cycle");
        Vector3 gap = c.Player.transform.position - zf.transform.position; gap.y = 0f;
        Assert.That(gap.magnitude, Is.InRange(0.9f, 1.3f), "about 1.2 m behind");
        Assert.Greater(gap.x, 0.5f, "behind, on the side Liu Bei came from");
        yield return Talk(c, c.GuanYu.position + new Vector3(0, 0, -1.2f), "talk:guan_yu");
        Assert.AreEqual(SequenceState.Done, story.Oath);
        Assert.AreEqual(StoryPhase.LeaveTown, story.Phase);
        Assert.Less(Vector3.Distance(c.Player.transform.position, c.OathEndSpot.position), 0.5f, "control resumes inside the west gate");
        Assert.IsTrue(c.GuanYuFollower.gameObject.activeSelf && c.ZhangFeiFollower.gameObject.activeSelf, "both brothers follow after the oath");
        Assert.Less(c.GuanYuFollower.Distance, c.ZhangFeiFollower.Distance, "one behind the other");

        for (int battle = 1; battle <= 3; battle++)
        {
            c = Object.FindFirstObjectByType<TownStoryController>();
            c.FadeTime = 0.02f;
            Teleport(c.Player, new Vector3(0, 0, -36f));
            yield return null;
            c.Interact();   // gate:battle_N → fade and load the battle
            float deadline = Time.realtimeSinceStartup + 20f;
            while (SceneManager.GetActiveScene().name == "Zhuojun_Town") { Assert.Less(Time.realtimeSinceStartup, deadline); yield return null; }
            Assert.AreEqual(GameRun.BattleScenes[battle - 1], SceneManager.GetActiveScene().name);
            yield return FightAndWin();
            var b = Object.FindFirstObjectByType<BattleController>();
            Assert.AreEqual(battle, GameRun.Current.Story.BattlesDone);
            Assert.IsTrue(b.LastApplied.RewardGranted);
            yield return null;
            if (battle < 3)
            {
                Assert.IsTrue(b.CanReturnToTown);
                var back = ButtonText("回到涿郡");
                Assert.IsNotNull(back, "the result panel offers the way back to town");
                Assert.IsNull(ButtonText("再战一次"), "a won play-through battle is not replayed");
                Press(back);
                yield return null; yield return null;
                c = Object.FindFirstObjectByType<TownStoryController>();
                Assert.Less(Vector3.Distance(c.Player.transform.position, c.ReturnSpot.position), 0.5f, "back inside the south gate");
                Assert.IsFalse(c.GuanYu.gameObject.activeSelf || c.ZhangFei.gameObject.activeSelf, "companions stay in the party");
                Assert.IsTrue(c.GuanYuFollower.gameObject.activeSelf && c.ZhangFeiFollower.gameObject.activeSelf, "and follow Liu Bei back in town");
            }
            else
            {
                Assert.IsTrue(b.ReachedEnding);
                for (int i = 0; i < 3; i++)   // continue → two narration lines → test version end
                {
                    var next = ButtonText("继续");
                    Assert.IsNotNull(next, "ending step " + i);
                    Press(next);
                    yield return null;
                }
                Assert.AreEqual(SequenceState.Done, GameRun.Current.Story.Ending);
                Assert.IsTrue(HudRoot().Query<Label>().ToList().Any(l => l.text == "测试版结束"));
                Assert.IsNotNull(ButtonText("重新开始"));
            }
        }
        var levels = GameRun.Current.Data.Party.Select(g => GameRun.Current.Progress.Level(g)).ToArray();
        Assert.That(levels, Is.All.EqualTo(4), "three battles bring everyone to level 4 (V1_SPEC §7)");
        Assert.AreEqual(120 + 30 + 50 + 60, GameRun.Current.Progress.Copper);
    }
}
