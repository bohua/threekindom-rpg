using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Gameplay.Battle;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.Save;
using ThreeKingdoms.UI.Menu;
using ThreeKingdoms.UI.Save;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

// design/ux/title-menu.md: continue / new game / load / settings / quit; continue and load are greyed out without
// saves, continue loads the slot saved last, and settings show the travel menu's own values.
public sealed class TitleMenuTests
{
    string dir;

    [SetUp]
    public void Fresh()
    {
        GameRun.Clear();
        dir = Path.Combine(Application.temporaryCachePath, "title-menu-" + Guid.NewGuid().ToString("N"));
        GameRun.SaveDirectoryOverride = dir;
    }

    [TearDown]
    public void Clean()
    {
        GameRun.Clear();
        GameRun.SaveDirectoryOverride = null;
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }

    static SaveData BattleSave(string location, int battlesDone, string savedAt) => new SaveData
    {
        location = location, chapter = GameRun.Chapter, guanYuJoined = true, zhangFeiJoined = true,
        oath = (int)SequenceState.Done, battlesDone = battlesDone, copper = 150, rations = 2, savedAt = savedAt,
        exp = new[] { new ExpEntry { general = "liu_bei", exp = 10 }, new ExpEntry { general = "guan_yu", exp = 10 }, new ExpEntry { general = "zhang_fei", exp = 10 } },
        leaderLevel = 2,
    };

    [UnityTest] public IEnumerator WithoutSavesContinueAndLoadAreGreyAndNewGameHasFocus()
    {
        yield return SceneManager.LoadSceneAsync("Title");
        yield return null; yield return null;
        var title = UnityEngine.Object.FindFirstObjectByType<TitleHud>();
        Assert.AreEqual(TitleHud.Mode.Main, title.Current);
        Assert.That(title.Entries.Select(b => b.text), Is.EqualTo(new[] { "继续游戏", "新的游戏", "读取存档", "设置", "退出游戏" }));
        Assert.IsFalse(title.Entries[0].enabledSelf, "continue needs a save");
        Assert.IsFalse(title.Entries[2].enabledSelf, "load needs a save");
        var focused = title.GetComponent<UIDocument>().rootVisualElement.panel.focusController.focusedElement;
        Assert.AreSame(title.Entries[1], focused, "new game has the pointer");
    }

    [UnityTest] public IEnumerator ContinueLoadsTheSlotSavedLast()
    {
        GameRun.Slots(3).Write(SaveSlots.Manual(1), BattleSave("battle_1", 0, "2026-10-26T10:00:00"));
        GameRun.Slots(3).Write(SaveSlots.Auto, BattleSave("battle_2", 1, "2026-10-26T12:00:00"));
        yield return SceneManager.LoadSceneAsync("Title");
        yield return null; yield return null;
        var title = UnityEngine.Object.FindFirstObjectByType<TitleHud>();
        Assert.IsTrue(title.Entries[0].enabledSelf);
        Assert.IsTrue(title.Entries[2].enabledSelf);
        Assert.AreEqual(SaveSlots.Auto, title.Controller.LatestSlot());
        title.Continue();
        float deadline = Time.realtimeSinceStartup + 20f;
        while (SceneManager.GetActiveScene().name != "Battle_Tutorial02") { Assert.Less(Time.realtimeSinceStartup, deadline); yield return null; }
        yield return null;
        Assert.AreEqual(BattleController.Phase.Preparing, UnityEngine.Object.FindFirstObjectByType<BattleController>().Current);
    }

    [UnityTest] public IEnumerator SettingsShowTheSharedValuesAndCancelReturns()
    {
        int music = GameSettings.Music;
        try
        {
            GameSettings.Music = 50;
            yield return SceneManager.LoadSceneAsync("Title");
            yield return null;
            var title = UnityEngine.Object.FindFirstObjectByType<TitleHud>();
            title.ShowSettings();
            yield return null;
            Assert.AreEqual(TitleHud.Mode.Settings, title.Current);
            Assert.AreEqual(5, title.Entries.Count, "four settings and restore defaults");
            Assert.AreEqual("50", title.Entries[0].Q<Label>("value").text);
            title.ShowMain();
            Assert.AreEqual(TitleHud.Mode.Main, title.Current);
            Assert.IsTrue(title.Entries.All(b => b.focusable), "the menu takes the pointer again");
        }
        finally { GameSettings.Music = music; }
    }
}
