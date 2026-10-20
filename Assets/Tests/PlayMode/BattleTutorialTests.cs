using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ThreeKingdoms.Core.Battle;
using ThreeKingdoms.Gameplay;
using ThreeKingdoms.Gameplay.Battle;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.UI.Battle;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

// ADR-0002 acceptance: the playable battle-1 scene (Liu / Guan / Zhang vs Cheng Yuanzhi + 2 headmen) runs end to
// end through the controller and HUD, grants the reward once, and can be fought again.
public sealed class BattleTutorialTests
{
    [SetUp] public void Fresh() => GameRun.Clear();
    [TearDown] public void Clean() => GameRun.Clear();

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

    [UnityTest] public IEnumerator ThreeBattlesChainWithProgressCarriedOver()
    {
        yield return SceneManager.LoadSceneAsync("Battle_Tutorial01");
        yield return null;
        var c = Object.FindFirstObjectByType<BattleController>();
        c.PlaybackSpeed = 4f; c.Fast = true;
        yield return PlayToEnd(c);
        Assert.IsTrue(c.HasNextBattle);
        c.GoToNextBattle();
        yield return null; yield return null;

        c = Object.FindFirstObjectByType<BattleController>();
        Assert.AreEqual("battle_2", c.Session.Encounter.Id);
        Assert.That(c.Session.State.Enemies.Select(u => u.Id), Is.EqualTo(new[] { "zhang_liang", "yt_officer", "yt_officer", "yt_officer" }));
        Assert.IsTrue(c.Session.State.Players.All(p => p.Level == 2), "levels carried from battle 1");
        Assert.AreEqual(150, c.Progress.Copper);
        Assert.IsTrue(c.EnemyViews.All(v => v.gameObject.activeSelf), "4 enemies shown");
        var stage = Object.FindFirstObjectByType<BattleStage>();
        Assert.AreEqual("ZhuojunOutskirts", stage.Backdrop.name, "battles 2-3 are fought outside the walls");
        Assert.AreEqual(5, stage.Backdrop.Layers.Length);
        Assert.IsNotNull(Object.FindFirstObjectByType<UIDocument>().panelSettings, "battle 2 has its HUD");

        GameRun.Clear();   // open battle 3 on its own: a fresh development run at its expected state
        yield return SceneManager.LoadSceneAsync("Battle_Tutorial03");
        yield return null;
        c = Object.FindFirstObjectByType<BattleController>();
        Assert.AreEqual("battle_3", c.Session.Encounter.Id);
        Assert.That(c.Session.State.Enemies.Select(u => u.Id), Is.EqualTo(new[] { "zhang_jiao", "zhang_bao", "zhang_liang" }));
        Assert.IsTrue(c.Session.State.Players.All(p => p.Level == 3), "opened directly: battle 3 starts at level 3");
        Assert.IsTrue(c.Session.State.Players.All(p => p.Skills.Count == 2), "level-3 skills available in battle 3");
        Assert.IsFalse(string.IsNullOrEmpty(c.EnemyViews[0].name) || c.EnemyViews[0].name == "Yellow Turban 1", "Zhang Jiao has his own sprite");
    }

    // Gamepad works from the first frame: the command menu holds the EventSystem selection without a mouse.
    [UnityTest] public IEnumerator CommandMenuIsSelectedForGamepadOnStart()
    {
        yield return SceneManager.LoadSceneAsync("Battle_Tutorial01");
        for (int i = 0; i < 5; i++) yield return null;
        var root = Object.FindFirstObjectByType<BattleHud>().GetComponent<UIDocument>().rootVisualElement;
        Assert.IsNotNull(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject, "EventSystem has the HUD panel selected");
        var focused = root.panel.focusController.focusedElement as Button;
        Assert.IsNotNull(focused, "a menu button has focus");
        Assert.AreEqual("攻击", focused.text);
    }

    // Bars follow the playback hit by hit instead of jumping to the round's final values.
    [UnityTest] public IEnumerator CardsShowEachHitAsItIsPlayed()
    {
        yield return SceneManager.LoadSceneAsync("Battle_Tutorial01");
        yield return null;
        var c = Object.FindFirstObjectByType<BattleController>();
        var root = Object.FindFirstObjectByType<BattleHud>().GetComponent<UIDocument>().rootVisualElement;
        var hits = new List<BattleEvent>();
        c.EventPlayed += e => { if (e.Kind == BattleEventKind.Damage && e.Target.Side == Side.Player) hits.Add(e); };
        foreach (var p in c.Session.State.Players.ToList()) c.Choose(BattleCommand.Defend(p));
        while (hits.Count == 0) yield return null;
        var first = hits[0];
        yield return null;
        string shown = $"{first.TargetTroopsAfter}/{first.Target.Stats.MaxTroops}";
        Assert.IsTrue(root.Query<Label>().ToList().Any(l => l.text == shown), "card shows " + shown + " right after the first hit");
        Assert.Less(first.Target.Troops, first.TargetTroopsAfter, "the round's later hits are already resolved in the core");
        Assert.IsFalse(root.Query<Label>().ToList().Any(l => l.text == $"{first.Target.Troops}/{first.Target.Stats.MaxTroops}"),
            "the final value is not shown before its hit is played");
    }
}
