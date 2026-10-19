using System.Collections;
using System.Linq;
using NUnit.Framework;
using ThreeKingdoms.Core.Battle;
using ThreeKingdoms.Gameplay.Battle;
using ThreeKingdoms.UI.Battle;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

// ADR-0002 acceptance: the playable battle-1 scene (Liu / Guan / Zhang vs Cheng Yuanzhi + 2 headmen) runs end to
// end through the controller and HUD, grants the reward once, and can be fought again.
public sealed class BattleTutorialTests
{
    static void AutoCommand(BattleController c)
    {
        var state = c.Session.State;
        var actor = c.Actor;
        var target = state.Enemies.Where(e => e.Active).OrderBy(e => e.Troops).ThenBy(e => e.Slot).First();
        var strike = actor.Skills.FirstOrDefault(s => s.Damage != DamageKind.None && state.CanUseSkill(actor, s));
        c.Choose(strike != null ? BattleCommand.UseSkill(actor, strike, strike.IsSingleTarget ? target : null) : BattleCommand.Attack(actor, target));
    }

    static IEnumerator PlayToEnd(BattleController c)
    {
        float deadline = Time.realtimeSinceStartup + 120f;
        while (c.Current != BattleController.Phase.Finished)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "battle did not finish");
            if (c.Current == BattleController.Phase.Commanding && c.Actor != null) AutoCommand(c);
            yield return null;
        }
    }

    [UnityTest] public IEnumerator Battle1IsPlayableEndToEndAndRewardsOnce()
    {
        yield return SceneManager.LoadSceneAsync("Battle_Tutorial01");
        yield return null;
        var c = Object.FindFirstObjectByType<BattleController>();
        Assert.IsNotNull(c);
        c.PlaybackSpeed = 4f; c.Fast = true;
        Assert.AreEqual(BattleController.Phase.Commanding, c.Current);
        Assert.AreEqual("battle_1", c.Session.Encounter.Id);
        Assert.That(c.Session.State.Players.Select(u => u.Id), Is.EqualTo(new[] { "liu_bei", "guan_yu", "zhang_fei" }));
        Assert.That(c.Session.State.Enemies.Select(u => u.Id), Is.EqualTo(new[] { "cheng_yuanzhi", "yt_headman", "yt_headman" }));
        Assert.IsFalse(c.EnemyViews[3].gameObject.activeSelf, "the fourth enemy slot is hidden in a 3v3");

        var hud = Object.FindFirstObjectByType<BattleHud>();
        var root = hud.GetComponent<UIDocument>().rootVisualElement;
        Assert.IsTrue(root.Query<Label>().ToList().Any(l => l.text.Contains("刘备")), "cards show simplified-Chinese names");
        Assert.IsTrue(root.Query<Button>().ToList().Any(b => b.text == "攻击"), "command menu is up");

        yield return PlayToEnd(c);
        Assert.AreEqual(BattleOutcome.Victory, c.Session.Result.Outcome);
        Assert.IsTrue(c.LastApplied.RewardGranted);
        Assert.AreEqual(150, c.Progress.Copper);
        Assert.IsTrue(c.Session.State.Players.All(p => c.Progress.Level(p.General) == 2), "equal exp: everyone reaches level 2");
        Assert.IsTrue(root.Query<Label>().ToList().Any(l => l.text == "胜利"));

        c.StartBattle();   // fight again: levels carry over, reward is not granted twice
        Assert.IsTrue(c.Session.State.Players.All(p => p.Level == 2));
        yield return PlayToEnd(c);
        Assert.AreEqual(BattleOutcome.Victory, c.Session.Result.Outcome);
        Assert.IsFalse(c.LastApplied.RewardGranted);
        Assert.AreEqual(150, c.Progress.Copper);
    }

    [UnityTest] public IEnumerator CancelStepsBackToPreviousGeneralAndTargetsAreMarked()
    {
        yield return SceneManager.LoadSceneAsync("Battle_Tutorial01");
        yield return null;
        var c = Object.FindFirstObjectByType<BattleController>();
        var root = Object.FindFirstObjectByType<BattleHud>().GetComponent<UIDocument>().rootVisualElement;
        var liu = c.Actor;
        Assert.AreEqual("liu_bei", liu.Id);
        c.Choose(BattleCommand.Defend(liu));
        Assert.AreEqual("guan_yu", c.Actor.Id);

        using (var e = NavigationCancelEvent.GetPooled()) { e.target = root; root.SendEvent(e); }
        yield return null;
        Assert.AreSame(liu, c.Actor, "cancel on the command menu returns to the previous general");
        Assert.AreEqual(0, c.Commands.Count);

        var target = c.Session.State.Enemies[0];
        c.PreviewTarget(target);
        Assert.IsTrue(c.ViewOf(target).TargetMarker.activeSelf);
        c.PreviewTarget(null);
        Assert.IsFalse(c.ViewOf(target).TargetMarker.activeSelf);
        Assert.IsNotNull(c.QuitAction, "Start quits the battle scene");
    }
}
