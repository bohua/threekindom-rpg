using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Gameplay.Battle;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.Save;
using ThreeKingdoms.UI.Save;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// save-system GDD in the real scenes: save at the tavern keeper (with an overwrite confirmation), load it from the
// title into the tavern with the party intact, refuse a damaged slot without touching it, and load a pre-battle
// save straight into that battle.
public sealed class SaveFlowTests
{
    string dir;

    [SetUp]
    public void Fresh()
    {
        GameRun.Clear();
        dir = Path.Combine(Application.temporaryCachePath, "save-flow-" + Guid.NewGuid().ToString("N"));
        GameRun.SaveDirectoryOverride = dir;
    }

    [TearDown]
    public void Clean()
    {
        GameRun.Clear();
        GameRun.SaveDirectoryOverride = null;
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }

    static IEnumerator WaitForScene(string name)
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        while (SceneManager.GetActiveScene().name != name) { Assert.Less(Time.realtimeSinceStartup, deadline, "loading " + name); yield return null; }
        yield return null;
    }

    [UnityTest] public IEnumerator SaveAtTheTavernAndLoadFromTheTitle()
    {
        yield return SceneManager.LoadSceneAsync("Zhuojun_Town");
        yield return null;
        var c = UnityEngine.Object.FindFirstObjectByType<TownStoryController>();
        var hud = UnityEngine.Object.FindFirstObjectByType<SaveHud>();
        Assert.IsNotNull(c.Innkeeper, "N05 is wired");
        c.Run.Story.Join(StoryState.GuanYu);
        c.Run.Progress.Inventory.BuyRation();   // 110 copper, 3 rations

        var cc = c.Player.GetComponent<CharacterController>();
        cc.enabled = false; c.Player.transform.position = c.Innkeeper.position + new Vector3(0, 0, -1.2f); cc.enabled = true;
        yield return null;
        Assert.AreEqual(TownStoryController.SavePanel, c.Prompt);
        c.Interact();
        Assert.AreEqual(SaveHud.Mode.Slots, hud.Current);
        Assert.AreEqual(3, hud.Entries.Count, "three manual slots");
        Assert.IsFalse(c.Player.InputEnabled);
        yield return null; yield return null;

        hud.Choose(1);
        var slots = c.Run.Slots();
        Assert.IsTrue(slots.Exists(SaveSlots.Manual(1)));
        hud.Choose(1);
        Assert.AreEqual(SaveHud.Mode.Confirm, hud.Current, "overwriting asks first");
        hud.ConfirmOverwrite();
        StringAssert.Contains("存档 1", hud.Message);
        hud.Back();
        Assert.IsNull(c.Panel);
        Assert.IsTrue(c.Player.InputEnabled);

        // Damage slot 2 by hand: the title reports it and leaves the file as it was.
        File.WriteAllText(slots.PathOf(SaveSlots.Manual(2)), "garbage");
        var damaged = File.ReadAllBytes(slots.PathOf(SaveSlots.Manual(2)));

        GameRun.Clear();
        yield return SceneManager.LoadSceneAsync("Title");
        yield return null;
        var title = UnityEngine.Object.FindFirstObjectByType<TitleHud>();
        title.ShowLoad();
        Assert.AreEqual(4, title.Entries.Count, "the pre-battle slot and three manual slots");
        Assert.IsFalse(title.Entries[0].enabledSelf, "no autosave yet");
        title.Load(SaveSlots.Manual(2));
        StringAssert.Contains("损坏", title.Message);
        Assert.That(File.ReadAllBytes(slots.PathOf(SaveSlots.Manual(2))), Is.EqualTo(damaged));
        Assert.AreEqual("Title", SceneManager.GetActiveScene().name);

        title.Load(SaveSlots.Manual(1));
        yield return WaitForScene("Zhuojun_Town");
        c = UnityEngine.Object.FindFirstObjectByType<TownStoryController>();
        Assert.IsTrue(c.Run.Story.GuanYuJoined);
        Assert.IsFalse(c.Run.Story.ZhangFeiJoined);
        Assert.AreEqual(110, c.Run.Progress.Copper);
        Assert.AreEqual(3, c.Run.Progress.Rations);
        Assert.Less(Vector3.Distance(c.Player.transform.position, c.TavernSpot.position), 0.5f, "back before the tavern keeper");
        Assert.IsTrue(c.GuanYuFollower.gameObject.activeSelf, "Guan Yu still follows");
        Assert.IsTrue(c.Player.InputEnabled, "no leftover control lock");
    }

    [UnityTest] public IEnumerator APreBattleSaveLoadsIntoThatBattle()
    {
        var save = new SaveData
        {
            location = "battle_2", chapter = GameRun.Chapter, guanYuJoined = true, zhangFeiJoined = true,
            oath = (int)SequenceState.Done, battlesDone = 1, copper = 150, rations = 2,
            exp = new[] { new ExpEntry { general = "liu_bei", exp = 10 }, new ExpEntry { general = "guan_yu", exp = 10 }, new ExpEntry { general = "zhang_fei", exp = 10 } },
            worn = new[] { new WornEntry { general = "guan_yu", item = "cavalry_lance" } },
            leaderLevel = 2,
        };
        GameRun.Slots(3).Write(SaveSlots.Auto, save);

        yield return SceneManager.LoadSceneAsync("Title");
        yield return null;
        var title = UnityEngine.Object.FindFirstObjectByType<TitleHud>();
        title.ShowLoad();
        Assert.IsTrue(title.Entries[0].enabledSelf);
        StringAssert.Contains("第2战", title.Entries[0].text);
        title.Load(SaveSlots.Auto);
        yield return WaitForScene("Battle_Tutorial02");
        var b = UnityEngine.Object.FindFirstObjectByType<BattleController>();
        Assert.IsFalse(b.Run.IsDevelopment, "a loaded save is a real play-through");
        Assert.AreEqual(150, b.Progress.Copper);
        Assert.That(b.Session.State.Players.Select(u => u.Level), Is.All.EqualTo(2));
        Assert.IsTrue(b.AutoSaved, "starting the battle rewrites the rolling pre-battle save");
    }
}
