using System.Collections;
using NUnit.Framework;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.UI.Story;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Speakers with an illustration show it beside the dialogue box (user 2026-10-22): none for the narrator reading the
// notice, Liu Bei at the left when he sighs, Zhang Fei at the right when he calls out; then the two-way answer.
public sealed class DialoguePortraitTests
{
    [SetUp] public void Fresh() => GameRun.Clear();
    [TearDown] public void Clean() => GameRun.Clear();

    [UnityTest] public IEnumerator SpeakersWithArtShowTheirPortrait()
    {
        yield return SceneManager.LoadSceneAsync("Zhuojun_Town");
        yield return null;
        var c = Object.FindFirstObjectByType<TownStoryController>();
        var hud = Object.FindFirstObjectByType<TownHud>();
        Assert.GreaterOrEqual(hud.Portraits.Entries.Length, 8, "the heroes, the castle steward and the Yellow Turban leaders have illustrations");

        var p = c.Player; var cc = p.GetComponent<CharacterController>();
        cc.enabled = false; p.transform.position = c.NoticeSpot.position; cc.enabled = true;
        yield return null;
        c.Interact();
        yield return null;
        Assert.AreEqual("narrator", c.Line?.Speaker);
        Assert.IsNull(hud.PortraitSpeaker, "the narrator has no portrait");

        yield return new WaitForSecondsRealtime(0.2f);
        c.Advance();
        yield return null;
        Assert.AreEqual("liu_bei", c.Line?.Speaker);
        Assert.AreEqual("liu_bei", hud.PortraitSpeaker, "Liu Bei's illustration while he sighs");
        Assert.IsNotNull(hud.PortraitTexture);

        // Zhang Fei walks in from off screen, then speaks.
        c.Advance();
        float deadline = Time.realtimeSinceStartup + 15f;
        while (c.Line?.Speaker != "zhang_fei") { Assert.Less(Time.realtimeSinceStartup, deadline); yield return null; }
        Assert.IsTrue(c.ZhangFei.gameObject.activeSelf);
        Assert.Less(Vector3.Distance(c.ZhangFei.position, p.transform.position), 3f, "he walked up to Liu Bei");
        Assert.AreEqual("zhang_fei", hud.PortraitSpeaker, "the portrait follows the speaker");

        while (c.Choices == null) { Assert.Less(Time.realtimeSinceStartup, deadline); yield return null; c.Advance(); }
        Assert.AreEqual(2, hud.ChoiceButtons.Count, "two answers for Liu Bei");
        yield return null; yield return null;
        Assert.AreSame(hud.ChoiceButtons[0], hud.ChoiceButtons[0].panel.focusController.focusedElement, "the cursor is on the first answer");
        c.Choose(0);
        while (c.Busy) { Assert.Less(Time.realtimeSinceStartup, deadline); yield return null; c.Advance(); }
        Assert.IsTrue(c.Run.Story.ZhangFeiJoined, "Zhang Fei joins");
    }
}
