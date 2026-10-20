using System.Collections;
using System.Linq;
using NUnit.Framework;
using ThreeKingdoms.Core.Battle;
using ThreeKingdoms.Gameplay.Battle;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.UI.Battle;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

// Teaching hints (story-events answer 8, game-ui rule 25): each appears at its moment, once, without blocking any
// legal command, and every seen hint can be read again from the 「提示」 menu.
public sealed class TutorialHintTests
{
    [SetUp] public void Fresh() => GameRun.Clear();
    [TearDown] public void Clean() => GameRun.Clear();

    static IEnumerator Load(string scene)
    {
        yield return SceneManager.LoadSceneAsync(scene);
        yield return null; yield return null;
    }

    [UnityTest] public IEnumerator FirstBattleTeachesOrdersTargetsOrderRetreatAndRecovery()
    {
        yield return Load("Battle_Tutorial01");
        var b = Object.FindFirstObjectByType<BattleController>();
        var tutor = Object.FindFirstObjectByType<BattleTutor>();
        var hud = Object.FindFirstObjectByType<BattleHud>();
        Assert.IsNotNull(tutor, "battle scenes carry the tutor");
        Assert.That(tutor.Seen, Is.EqualTo(new[] { "b1.command" }), "the first order prompt appears as commanding starts");
        StringAssert.Contains("下令", hud.CurrentHint);

        var s = b.Session.State;
        var enemy = s.Enemies.First();
        b.PreviewTarget(enemy);
        Assert.Contains("b1.target", tutor.Seen.ToList());
        // A hint never restricts the choice: defending is accepted while the target hint is up.
        var first = b.Actor;
        b.Choose(BattleCommand.Defend(first));
        Assert.IsTrue(b.Commands.ContainsKey(first));

        b.PlaybackSpeed = 4f; b.Fast = true;
        float deadline = Time.realtimeSinceStartup + 120f;
        while (b.Current != BattleController.Phase.Finished)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline);
            if (b.Current == BattleController.Phase.Commanding && b.Actor != null)
                b.Choose(BattleCommand.Attack(b.Actor, s.Enemies.Where(e => e.Active).OrderBy(e => e.Troops).First()));
            yield return null;
        }
        Assert.AreEqual(BattleOutcome.Victory, b.Session.Result.Outcome);
        Assert.That(tutor.Seen, Is.EquivalentTo(new[] { "b1.command", "b1.target", "b1.order", "b1.retreat", "b1.recover" }));
        Assert.AreEqual(tutor.Seen.Count, tutor.Seen.Distinct().Count(), "each hint appears once");
    }

    [UnityTest] public IEnumerator SecondBattleTeachesTechAdvantageAndFocus()
    {
        yield return Load("Battle_Tutorial02");
        var b = Object.FindFirstObjectByType<BattleController>();
        var tutor = Object.FindFirstObjectByType<BattleTutor>();
        Assert.Contains("b2.tech", tutor.Seen.ToList());
        var s = b.Session.State;
        var guan = s.Players.First(u => u.Id == "guan_yu");
        var officer = s.Enemies.First(e => e.Troop == TroopType.YellowTurban);
        Assert.IsTrue(s.HasAdvantage(guan, officer), "cavalry beats the Yellow Turbans");

        // Liu Bei and Guan Yu both strike the same officer: advantage when Guan Yu aims, focus when both are set.
        b.Choose(BattleCommand.Attack(b.Actor, officer));
        Assert.AreSame(guan, b.Actor);
        b.PreviewTarget(officer);
        Assert.Contains("b2.advantage", tutor.Seen.ToList());
        b.Choose(BattleCommand.Attack(guan, officer));
        Assert.Contains("b2.focus", tutor.Seen.ToList());
    }

    [UnityTest] public IEnumerator SeenHintsCanBeReadAgainFromTheMenu()
    {
        yield return Load("Battle_Tutorial03");
        var tutor = Object.FindFirstObjectByType<BattleTutor>();
        var hud = Object.FindFirstObjectByType<BattleHud>();
        Assert.Contains("b3.skills", tutor.Seen.ToList());
        var menu = hud.GetComponent<UnityEngine.UIElements.UIDocument>().rootVisualElement.Query<UnityEngine.UIElements.Button>().ToList();
        var hints = menu.FirstOrDefault(x => x.text == "提示");
        Assert.IsNotNull(hints, "the command menu offers 提示 once a hint was shown");
        using (var e = UnityEngine.UIElements.NavigationSubmitEvent.GetPooled()) { e.target = hints; hints.SendEvent(e); }
        yield return null;
        var entries = hud.GetComponent<UnityEngine.UIElements.UIDocument>().rootVisualElement.Query<UnityEngine.UIElements.Button>().ToList();
        Assert.IsTrue(entries.Any(x => x.text == "三级技能与目标优先"));
    }
}
