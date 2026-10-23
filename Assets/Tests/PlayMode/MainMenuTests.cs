using System.Collections;
using System.Linq;
using NUnit.Framework;
using ThreeKingdoms.Core.Economy;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.UI.Menu;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// The pause menu (user 2026-10-22, reference image 1): tabs 物品 / 装备 / 技能 / 武将 / 系统, the general's card at the
// left, the list in the middle and its detail at the right; 武将 opens the general's page (reference image 2).
public sealed class MainMenuTests
{
    [SetUp] public void Fresh() => GameRun.Clear();
    [TearDown] public void Clean() => GameRun.Clear();

    [UnityTest] public IEnumerator PauseMenuTabsEquipmentGeneralPageAndTitle()
    {
        yield return SceneManager.LoadSceneAsync("Zhuojun_Town");
        yield return null;
        var c = Object.FindFirstObjectByType<TownStoryController>();
        var menu = Object.FindFirstObjectByType<MainMenuHud>();
        c.Run.Story.Join(StoryState.ZhangFei);
        var inv = c.Run.Progress.Inventory;
        inv.Buy(inv.Data.Find("leather_bracers"));

        c.OpenPanel(TownStoryController.MenuPanel);
        Assert.AreEqual(MainMenuHud.Mode.Root, menu.Current);
        Assert.IsFalse(c.Player.InputEnabled, "movement pauses while the menu is open");
        Assert.AreEqual(MainMenuHud.Tab.Generals, menu.CurrentTab);
        Assert.That(menu.Entries.Select(b => b.text.Split('　')[0]), Is.EqualTo(new[] { "刘备", "张飞" }), "only joined generals");
        StringAssert.Contains("武力", menu.Detail);
        StringAssert.Contains("声望 0", menu.Detail);

        menu.Show(MainMenuHud.Tab.Items);
        StringAssert.Contains("皮护腕", menu.Detail);
        menu.Show(MainMenuHud.Tab.Skills);
        StringAssert.Contains("整队", menu.Detail);
        StringAssert.Contains("火矢（3 级习得）", menu.Detail);

        // 装备: the accessory slot takes the bracers from the bag.
        menu.Show(MainMenuHud.Tab.Equip);
        Assert.AreEqual(3, menu.Entries.Count, "weapon, armour, accessory");
        menu.PickFor(EquipSlot.Accessory);
        var bracers = menu.SideEntries.First(b => b.text.StartsWith("皮护腕"));
        using (var e = UnityEngine.UIElements.NavigationSubmitEvent.GetPooled()) { e.target = bracers; bracers.SendEvent(e); }
        Assert.AreEqual("leather_bracers", inv.Worn("liu_bei", EquipSlot.Accessory)?.Id, "equipped from the menu");

        menu.SelectGeneral(1);
        Assert.AreEqual("zhang_fei", menu.General.Id, "◀ ▶ change general");

        // The general's page: name with courtesy name, radar values, 列传.
        menu.ShowInfo(menu.General);
        Assert.AreEqual(MainMenuHud.Mode.Info, menu.Current);
        StringAssert.Contains("张飞（翼德）", menu.Detail);
        menu.ShowInfoTab(MainMenuHud.InfoTab.Biography);
        StringAssert.Contains("丈八蛇矛", menu.Detail);
        menu.Back();
        Assert.AreEqual(MainMenuHud.Mode.Root, menu.Current);
        menu.Back();
        Assert.IsNull(c.Panel, "cancel at the top closes the menu");
        Assert.IsTrue(c.Player.InputEnabled);

        c.OpenPanel(TownStoryController.MenuPanel);
        menu.Show(MainMenuHud.Tab.System);
        c.ReturnToTitle();
        yield return null; yield return null;
        Assert.AreEqual("Title", SceneManager.GetActiveScene().name);
        Assert.IsNull(GameRun.Current, "returning to the title ends the play-through");
    }
}
