using System.Collections;
using NUnit.Framework;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.UI.Story;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Speakers with an illustration show it beside the dialogue box (user 2026-10-22): Guan Yu at the right while he
// talks, Liu Bei at the left on his reply, none for the system line; the figure keeps swaying while it is up.
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
        Assert.GreaterOrEqual(hud.Portraits.Length, 4, "the three heroes and the castle steward have illustrations");

        var p = c.Player; var cc = p.GetComponent<CharacterController>();
        cc.enabled = false; p.transform.position = c.GuanYu.position + new Vector3(0, 0, -1.2f); cc.enabled = true;
        yield return null;
        c.Interact();
        yield return null;
        Assert.AreEqual("guan_yu", c.Line?.Speaker);
        Assert.AreEqual("guan_yu", hud.PortraitSpeaker, "Guan Yu's illustration is up while he talks");
        Assert.IsNotNull(hud.PortraitTexture);

        yield return new WaitForSecondsRealtime(0.3f);
        c.Advance();
        yield return null;
        Assert.AreEqual("liu_bei", c.Line?.Speaker);
        Assert.AreEqual("liu_bei", hud.PortraitSpeaker, "the portrait follows the speaker");

        float deadline = Time.realtimeSinceStartup + 10f;
        while (c.Busy && c.Line?.Speaker != "system") { Assert.Less(Time.realtimeSinceStartup, deadline); yield return null; c.Advance(); }
        if (c.Busy) Assert.IsNull(hud.PortraitSpeaker, "system lines show no portrait");
    }
}
