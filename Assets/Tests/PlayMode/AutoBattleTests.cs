using System.Collections;
using NUnit.Framework;
using ThreeKingdoms.Core.Battle;
using ThreeKingdoms.Gameplay.Battle;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.UI.Battle;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Auto battle (user 2026-10-22): with 「自动」 on, the party's commands are chosen for them and the battle plays out.
public sealed class AutoBattleTests
{
    [SetUp] public void Fresh() => GameRun.Clear();
    [TearDown] public void Clean() => GameRun.Clear();

    [UnityTest] public IEnumerator AutoBattleWinsTheFirstBattle()
    {
        yield return SceneManager.LoadSceneAsync("Battle_Tutorial01");
        yield return null; yield return null;
        var b = Object.FindFirstObjectByType<BattleController>();
        var hud = Object.FindFirstObjectByType<BattleHud>();
        if (b.Current == BattleController.Phase.Preparing) b.ConfirmPreparation();
        yield return null;
        b.PlaybackSpeed = 4f; b.Fast = true;
        hud.ToggleAuto();
        Assert.IsTrue(b.Auto);
        float deadline = Time.realtimeSinceStartup + 120f;
        while (b.Current != BattleController.Phase.Finished)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline);
            yield return null;
        }
        Assert.AreEqual(BattleOutcome.Victory, b.Session.Result.Outcome, "the auto commands win the tutorial battle");
    }
}
