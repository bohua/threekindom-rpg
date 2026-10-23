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
    string saveDir;

    [SetUp] public void Fresh()
    {
        GameRun.Clear();
        saveDir = System.IO.Path.Combine(Application.temporaryCachePath, "main-line-" + System.Guid.NewGuid().ToString("N"));
        GameRun.SaveDirectoryOverride = saveDir;   // never touch the player's saves
    }

    [TearDown] public void Clean()
    {
        GameRun.Clear();
        GameRun.SaveDirectoryOverride = null;
        if (System.IO.Directory.Exists(saveDir)) System.IO.Directory.Delete(saveDir, true);
    }

    static void Teleport(PlayerMover p, Vector3 at)
    {
        var cc = p.GetComponent<CharacterController>();
        cc.enabled = false; p.transform.position = at; cc.enabled = true;
    }

    static IEnumerator Talk(TownStoryController c, Vector3 at, string expectedPrompt, int choice = 0)
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
            if (c.Choices != null) c.Choose(choice);
            else c.Advance();
        }
        Assert.IsTrue(c.Player.InputEnabled, "control returns after the sequence");
    }

    static IEnumerator FightAndWin()
    {
        yield return null;
        var b = Object.FindFirstObjectByType<BattleController>();
        Assert.IsNotNull(b, "a battle scene loaded");
        if (b.Current == BattleController.Phase.Preparing)
        {
            Assert.That(b.Selected.Select(g => g.Id), Is.EqualTo(new[] { "liu_bei", "guan_yu", "zhang_fei" }), "everyone joined goes by default");
            b.ConfirmPreparation();
        }
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
        Assert.IsNull(c.BlockerLeader, "no Yellow Turbans at the gate before the oath");
        while (c.Busy) { yield return null; c.Advance(); }

        // The notice board on the square (user 2026-10-22): Liu Bei sighs, Zhang Fei walks up and scolds him, Liu Bei
        // answers (either answer works) and Zhang Fei joins.
        Assert.That(c.StoryMarks, Has.Member(c.NoticeBoard), "the notice board wears the \"!\"");
        yield return Talk(c, c.NoticeSpot.position, "notice", choice: 1);
        Assert.IsTrue(story.ZhangFeiJoined);
        Assert.IsFalse(c.ZhangFei.gameObject.activeSelf, "a joined companion leaves their post");
        // …and walks behind Liu Bei from then on (user 2026-10-20).
        var zf = c.ZhangFeiFollower;
        Assert.IsTrue(zf.gameObject.activeSelf, "Zhang Fei follows once he joins");
        Assert.IsFalse(c.GuanYuFollower.gameObject.activeSelf, "Guan Yu does not follow before joining");
        Assert.IsNull(zf.GetComponent<Collider>(), "a follower never blocks the player");
        var zfWalk = zf.GetComponent<ThreeKingdoms.Gameplay.PixelWalkFrames>();
        Assert.IsTrue(zfWalk.HasWalkCycle);
        // It runs after Liu Bei: the gap stretches when he sets off, then closes in after he stops (user 2026-10-20).
        bool followerWalked = false;
        float widest = 0f;
        for (float t = 0f; t < 0.4f; t += Time.deltaTime)
        {
            c.Player.Move(Vector2.right, Time.deltaTime);
            yield return null;
            followerWalked |= zfWalk.Walking;
            widest = Mathf.Max(widest, zf.Distance);
        }
        Assert.IsTrue(followerWalked, "the follower plays the walk cycle");
        Assert.Greater(widest, zf.StopGap + 0.4f, "the line stretches as Liu Bei sets off");
        for (float t = 0f; t < 2f && (zf.Speed > 0.01f || zfWalk.Walking); t += Time.deltaTime) yield return null;
        Assert.AreEqual(zf.StopGap, zf.Distance, 0.3f, "and closes in to the resting gap after he stops");
        Vector3 gap = c.Player.transform.position - zf.transform.position; gap.y = 0f;
        Assert.Greater(gap.x, 0.5f, "behind, on the side Liu Bei came from");
        // Then into the tavern with him: the indoor scene, where Guan Yu comes in and joins; back in town the oath
        // plays by itself and ends inside the west gate.
        Assert.IsFalse(story.GuanYuJoined);
        Assert.That(c.StoryMarks, Has.Member(c.TavernDoor), "the tavern door wears the \"!\" now");
        Teleport(c.Player, c.TavernDoor.position + new Vector3(1.2f, 0, -1.0f));   // beside the tavern keeper
        yield return null;
        c.Interact();
        float tavernDeadline = Time.realtimeSinceStartup + 60f;
        while (SceneManager.GetActiveScene().name != "Zhuojun_Tavern") { Assert.Less(Time.realtimeSinceStartup, tavernDeadline); yield return null; }
        var tavern = Object.FindFirstObjectByType<TavernStage>();
        tavern.FadeTime = 0.02f;
        bool guanSpoke = false;
        while (SceneManager.GetActiveScene().name == "Zhuojun_Tavern")
        {
            Assert.Less(Time.realtimeSinceStartup, tavernDeadline);
            if (tavern && tavern.Line.HasValue) { guanSpoke |= tavern.Line.Value.Speaker == "guan_yu"; tavern.Advance(); }
            yield return null;
        }
        Assert.IsTrue(guanSpoke, "Guan Yu speaks in the tavern");
        Assert.IsTrue(story.GuanYuJoined);
        yield return null;
        c = Object.FindFirstObjectByType<TownStoryController>();
        c.FadeTime = 0.02f;
        while (c.Busy) { Assert.Less(Time.realtimeSinceStartup, tavernDeadline); yield return null; c.Advance(); }
        Assert.AreEqual(SequenceState.Done, story.Oath);
        Assert.AreEqual(StoryPhase.LeaveTown, story.Phase);
        Assert.Less(Vector3.Distance(c.Player.transform.position, c.OathEndSpot.position), 0.5f, "control resumes inside the west gate");
        Assert.IsTrue(c.GuanYuFollower.gameObject.activeSelf && c.ZhangFeiFollower.gameObject.activeSelf, "both brothers follow after the oath");
        Assert.AreEqual(1, c.GuanYuFollower.Slot, "Guan Yu walks right behind Liu Bei");
        Assert.AreEqual(2, c.ZhangFeiFollower.Slot, "Zhang Fei behind him");
        Assert.Less(c.GuanYuFollower.Distance, c.ZhangFeiFollower.Distance, "one behind the other");

        for (int battle = 1; battle <= 3; battle++)
        {
            c = Object.FindFirstObjectByType<TownStoryController>();
            c.FadeTime = 0.02f;
            // The next group of Yellow Turbans blocks the south gate (user 2026-10-22); their leader wears the "!".
            string[] leaders = { "程远志", "张梁", "张角" }, leaderKeys = { "cheng_yuanzhi", "zhang_liang", "zhang_jiao" };
            Assert.IsNotNull(c.BlockerLeader, "Yellow Turbans block the gate before battle " + battle);
            Assert.AreEqual(leaders[battle - 1], c.BlockerLeader.name);
            Assert.That(c.StoryMarks, Has.Member(c.BlockerLeader));
            Assert.IsTrue(c.GateWall.enabled, "they cannot be walked past");
            Teleport(c.Player, new Vector3(0, 0, -36f));
            yield return null;
            c.Interact();   // gate:battle_N → the leader's challenge, then fade and load the battle
            Assert.AreEqual(leaderKeys[battle - 1], c.Line?.Speaker, "the leader speaks first");
            float deadline = Time.realtimeSinceStartup + 20f;
            while (SceneManager.GetActiveScene().name == "Zhuojun_Town")
            {
                Assert.Less(Time.realtimeSinceStartup, deadline);
                yield return null;
                if (c != null && c.Line.HasValue) c.Advance();
            }
            Assert.AreEqual(GameRun.BattleScenes[battle - 1], SceneManager.GetActiveScene().name);
            yield return null;
            var prep = Object.FindFirstObjectByType<BattleController>();
            Assert.AreEqual(BattleController.Phase.Preparing, prep.Current, "a play-through battle opens on preparation");
            if (battle == 1)
            {
                // The cursor starts on 「确认出战」; Start asks before ending the preparation (user 2026-10-22).
                yield return null; yield return null;
                Assert.AreEqual("确认出战", (HudRoot().panel.focusController.focusedElement as Button)?.text);
                var prepHud = Object.FindFirstObjectByType<BattleHud>();
                prepHud.AskSortie();
                Assert.IsTrue(prepHud.SortieConfirmOpen);
                Press(ButtonText("继续整备"));
                Assert.IsFalse(prepHud.SortieConfirmOpen, "keep preparing closes the question");
                Assert.AreEqual(BattleController.Phase.Preparing, prep.Current);
            }
            prep.ConfirmPreparation();
            // Confirming writes the rolling pre-battle save (save-system rule 2): one slot, overwritten each battle.
            Assert.AreEqual(ThreeKingdoms.Save.SaveReadStatus.Ok, GameRun.Current.Slots().Read(ThreeKingdoms.Save.SaveSlots.Auto, out var auto));
            Assert.AreEqual($"battle_{battle}", auto.location);
            Assert.AreEqual(battle - 1, auto.battlesDone);
            yield return FightAndWin();
            var b = Object.FindFirstObjectByType<BattleController>();
            Assert.AreEqual(battle, GameRun.Current.Story.BattlesDone);
            Assert.IsTrue(b.LastApplied.RewardGranted);
            yield return null;
            // The leader withdraws with a parting line before the results (user 2026-10-22; nobody dies, V1_SPEC §2.2).
            var hud = Object.FindFirstObjectByType<BattleHud>();
            Assert.IsTrue(hud.DialoguePlaying, "lines after the victory");
            Assert.AreEqual(leaderKeys[battle - 1], hud.DialogueSpeaker);
            Assert.AreEqual(leaderKeys[battle - 1], hud.PortraitSpeaker, "with the leader's illustration");
            Assert.IsNull(ButtonText("回到涿郡"), "results wait for the lines");
            while (hud.DialoguePlaying) { hud.AdvanceDialogue(); yield return null; }
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
                Assert.IsNotNull(ButtonText("返回标题"));
            }
        }
        var levels = GameRun.Current.Data.Party.Select(g => GameRun.Current.Progress.Level(g)).ToArray();
        Assert.That(levels, Is.All.EqualTo(4), "three battles bring everyone to level 4 (V1_SPEC §7)");
        Assert.AreEqual(120 + 30 + 50 + 60, GameRun.Current.Progress.Copper);
    }
}
