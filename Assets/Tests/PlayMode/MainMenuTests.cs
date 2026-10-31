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

// The travel menu 行旅 (user 2026-10-24, GAME_MENU_UI_REDESIGN.md): tabs 物品 / 装备 / 技能 / 兵书 / 武将 / 系统 with
// the party column; 技能 is each general's own level-up skills; 兵书 is the military-book screen's four panels.
public sealed class MainMenuTests
{
    [SetUp] public void Fresh() => GameRun.Clear();
    [TearDown] public void Clean() => GameRun.Clear();

    [UnityTest] public IEnumerator TravelMenuTabsItemsEquipmentSkillsBooksAndTitle()
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
        Assert.AreEqual(MainMenuHud.Tab.Items, menu.CurrentTab, "opens on 物品 like the first mock-up");
        Assert.AreEqual("liu_bei", menu.General.Id);

        // 物品: rations first, then each kind of gear carried or worn; rations cannot be used on full troops.
        var rows = menu.ItemRows();
        Assert.IsTrue(rows[0].IsRation);
        Assert.IsTrue(rows.Any(r => r.Name == "皮护腕" && r.Count == 1), "bag item listed");
        Assert.IsTrue(rows.Any(r => !r.IsRation && r.Worn > 0), "worn gear counted");
        menu.Confirm();
        Assert.AreEqual(MenuText.TroopsFull, menu.Message);
        menu.ItemFilter = 1;
        Assert.IsTrue(menu.ItemRows().All(r => r.IsRation), "消耗品 filter");
        menu.ItemFilter = 0;

        // 技能: Liu Bei's own skills, 流矢 (v1.3 name) locked until Lv.3, nothing usable out of battle.
        menu.Show(MainMenuHud.Tab.Skills);
        var skills = menu.SkillRows();
        Assert.That(skills.Take(2).Select(s => s.Skill.DisplayName), Is.EqualTo(new[] { "整队", "流矢" }));
        Assert.IsTrue(menu.Learned(skills[0]));
        Assert.IsFalse(menu.Learned(skills[1]));
        Assert.AreEqual("Lv.3 解锁", menu.SkillUseBlocked(skills[1]));
        Assert.AreEqual(MenuText.TroopsFullSkill, menu.SkillUseBlocked(skills[0]));

        // 装备: the accessory slot takes the bracers from the bag.
        menu.Show(MainMenuHud.Tab.Equip);
        menu.PickFor(EquipSlot.Accessory);
        Assert.IsTrue(menu.Picking);
        var cands = menu.Candidates();
        Assert.AreEqual("leather_bracers", cands[0].item.Id);
        menu.Confirm();
        Assert.AreEqual("leather_bracers", inv.Worn("liu_bei", EquipSlot.Accessory)?.Id, "equipped from the menu");
        Assert.IsFalse(menu.Picking);
        // Choosing the worn item takes it off again.
        menu.PickFor(EquipSlot.Accessory);
        Assert.IsTrue(menu.Candidates().Last().worn);
        menu.Move(0, menu.Candidates().Count - 1);
        menu.Confirm();
        Assert.IsNull(inv.Worn("liu_bei", EquipSlot.Accessory), "unequipped from the menu");

        menu.SelectGeneral(1);
        Assert.AreEqual("zhang_fei", menu.General.Id, "only joined generals, in party order");
        menu.SelectGeneral(1);
        Assert.AreEqual("liu_bei", menu.General.Id);

        // 兵书: Liu Bei's 儒家 plate holds his opening books; confirming moves to 藏书阁, Y cycles the panels.
        menu.Show(MainMenuHud.Tab.Bingshu);
        var shelf = BingshuShelf.For(c.Run);
        Assert.AreEqual("儒家", shelf.Circuit("liu_bei").Name);
        Assert.AreEqual(5, shelf.Slots("liu_bei").Count(b => b != null), "four 策略书 on the hub line, one 功法书 outside");
        Assert.IsTrue(shelf.Techniques("liu_bei", 77).Any(), "the 功法书 gives a technique");
        // Saved with the play-through and read back the same.
        var text = c.Run.Capture(ThreeKingdoms.Save.SaveData.TownLocation).bingshu;
        Assert.AreEqual(shelf.Serialize(), text);
        var copy = new BingshuShelf();
        copy.Restore(text);
        CollectionAssert.AreEqual(shelf.Slots("liu_bei").Select(b => b?.Name), copy.Slots("liu_bei").Select(b => b?.Name));
        Assert.IsTrue(shelf.ShelfFor("liu_bei").All(b => shelf.Owner(b) == null || shelf.Owner(b) == "liu_bei"), "other generals' books hidden");
        Assert.IsFalse(shelf.ShelfFor("zhang_fei").Any(b => b.Name == "《李将军射法》"), "Liu Bei's 功法书 is not offered to Zhang Fei");
        menu.Confirm();
        Assert.AreEqual(1, menu.BingshuFocus);
        var free = shelf.ShelfFor("liu_bei").First(b => shelf.Owner(b) == null);
        Assert.IsTrue(menu.SocketBook(menu.General, free));
        Assert.AreEqual("liu_bei", shelf.Owner(free));
        Assert.IsTrue(menu.SocketBook(menu.General, free), "socketing it again takes it out");
        Assert.IsNull(shelf.Owner(free));
        menu.Alternate();
        Assert.AreEqual(2, menu.BingshuFocus);
        menu.Back();
        Assert.AreEqual(0, menu.BingshuFocus);

        // Tabs wrap with LB / RB.
        menu.Show(MainMenuHud.Tab.Items);
        menu.NextTab(-1);
        Assert.AreEqual(MainMenuHud.Tab.System, menu.CurrentTab);
        menu.NextTab(1);
        Assert.AreEqual(MainMenuHud.Tab.Items, menu.CurrentTab);

        menu.Back();
        Assert.IsNull(c.Panel, "cancel at the top closes the menu");
        Assert.IsTrue(c.Player.InputEnabled);
        Assert.AreEqual(MainMenuHud.Mode.Closed, menu.Current);

        c.OpenPanel(TownStoryController.MenuPanel);
        menu.Show(MainMenuHud.Tab.System);
        c.ReturnToTitle();
        yield return null; yield return null;
        Assert.AreEqual("Title", SceneManager.GetActiveScene().name);
        Assert.IsNull(GameRun.Current, "returning to the title ends the play-through");
    }

    [Test] public void QuestLineReadsLikeTheMockups()
    {
        var s = StoryState.AtBattle(1);
        Assert.AreEqual("从南门出城迎敌 · 第二战 张梁", MenuText.Quest(s));
        Assert.AreEqual("南门整备 · 第二战", MenuText.SavePlace("battle_2"));
    }
}
