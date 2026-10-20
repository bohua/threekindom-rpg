using System.Collections;
using System.Linq;
using NUnit.Framework;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.UI.Menu;
using ThreeKingdoms.UI.Shop;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Town main menu (game-ui answer 10): 武将 shows each joined general's level, exp, attributes, skills and gear and
// leads to the outfit panel; 物品 shows copper, rations and the bag; 设置 returns to the title.
public sealed class MainMenuTests
{
    [SetUp] public void Fresh() => GameRun.Clear();
    [TearDown] public void Clean() => GameRun.Clear();

    [UnityTest] public IEnumerator MenuShowsTheGeneralsTheBagAndReturnsToTitle()
    {
        yield return SceneManager.LoadSceneAsync("Zhuojun_Town");
        yield return null;
        var c = Object.FindFirstObjectByType<TownStoryController>();
        var menu = Object.FindFirstObjectByType<MainMenuHud>();
        c.Run.Story.Join(StoryState.ZhangFei);
        c.Run.Progress.Inventory.Buy(c.Run.Progress.Inventory.Data.Find("leather_bracers"));

        c.OpenPanel(TownStoryController.MenuPanel);
        Assert.AreEqual(MainMenuHud.Mode.Root, menu.Current);
        Assert.IsFalse(c.Player.InputEnabled, "movement pauses while the menu is open");
        Assert.That(menu.Entries.Select(b => b.text), Is.EqualTo(new[] { "武将", "物品", "设置", "关闭菜单" }));

        menu.Go(MainMenuHud.Mode.Generals);
        Assert.That(menu.Entries.Select(b => b.text), Is.EqualTo(new[] { "刘备", "张飞", "更换装备" }), "only joined generals");
        menu.ShowGeneral(c.Run.Progress.Party.First(g => g.Id == "liu_bei"));
        StringAssert.Contains("Lv.1", menu.Detail);
        StringAssert.Contains("升级需 10", menu.Detail);
        StringAssert.Contains("武力 65", menu.Detail);
        StringAssert.Contains("整队", menu.Detail);
        StringAssert.Contains("火矢（3 级习得）", menu.Detail);
        StringAssert.Contains("猎弓", menu.Detail);

        menu.Go(MainMenuHud.Mode.Items);
        StringAssert.Contains("铜钱 80", menu.Detail);
        StringAssert.Contains("皮护腕", menu.Detail);

        // 配装 hands over to the outfit panel; leaving it returns to free movement.
        menu.Go(MainMenuHud.Mode.Generals);
        c.SwitchPanel(TownStoryController.OutfitPanel);
        var outfit = Object.FindFirstObjectByType<ShopHud>();
        Assert.AreEqual(MainMenuHud.Mode.Closed, menu.Current);
        Assert.AreEqual(ShopHud.Mode.PickGeneral, outfit.Current);
        outfit.Back();
        Assert.IsNull(c.Panel);
        Assert.IsTrue(c.Player.InputEnabled);

        c.OpenPanel(TownStoryController.MenuPanel);
        menu.Back();
        Assert.IsNull(c.Panel, "cancel at the top closes the menu");

        c.OpenPanel(TownStoryController.MenuPanel);
        menu.Go(MainMenuHud.Mode.Settings);
        c.ReturnToTitle();
        yield return null; yield return null;
        Assert.AreEqual("Title", SceneManager.GetActiveScene().name);
        Assert.IsNull(GameRun.Current, "returning to the title ends the play-through");
    }
}
