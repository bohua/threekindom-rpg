using System.Collections;
using System.Linq;
using Jrpg;
using NUnit.Framework;
using ThreeKingdoms.Core.Economy;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.UI.Shop;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// The Zhuojun weapons & equipment shop at the smith N04 (equipment-items GDD): open it in town, buy, refuse to sell
// what is worn, confirm a sale, change a slot, close it and get control back; the new gear reaches the battle line-up.
public sealed class ShopTests
{
    [SetUp] public void Fresh() => GameRun.Clear();
    [TearDown] public void Clean() => GameRun.Clear();

    [UnityTest] public IEnumerator BuySellAndOutfitAtTheSmith()
    {
        yield return SceneManager.LoadSceneAsync("Zhuojun_Town");
        yield return null;
        var c = Object.FindFirstObjectByType<TownStoryController>();
        var hud = Object.FindFirstObjectByType<ShopHud>();
        Assert.IsNotNull(c.Smith, "N04 is wired");
        Assert.IsNotNull(hud, "the town has a shop panel");
        Assert.IsNotNull(Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>(), "menus can be navigated with a gamepad");
        c.Run.Story.Join(StoryState.GuanYu);
        var inv = c.Run.Progress.Inventory;
        var guan = c.Run.Progress.Party.First(g => g.Id == StoryState.GuanYu);
        var shuo = inv.Data.Find("iron_shuo");
        var bracers = inv.Data.Find("leather_bracers");

        // Walk up to the smith and talk.
        var cc = c.Player.GetComponent<CharacterController>();
        cc.enabled = false; c.Player.transform.position = c.Smith.position + new Vector3(0, 0, -1.2f); cc.enabled = true;
        yield return null;
        Assert.AreEqual("shop", c.Prompt);
        c.Interact();
        Assert.IsTrue(c.ShopOpen);
        Assert.IsFalse(c.Player.InputEnabled, "movement pauses while the shop is open");
        Assert.AreEqual(ShopHud.Mode.Tabs, hud.Current);
        yield return null; yield return null;

        hud.Go(ShopHud.Mode.Buy);
        Assert.IsTrue(hud.Entries.Any(b => b.text == "精铁马槊"), "the improved weapons are on sale");
        Assert.IsFalse(hud.Entries.Any(b => b.text == "布甲"), "starting gear is not sold");
        hud.BuyItem(shuo);
        hud.BuyItem(bracers);
        Assert.AreEqual(120 - 60 - 40, inv.Copper);

        // Worn items are refused with a reason; a bag item asks first, then sells for half.
        hud.Go(ShopHud.Mode.Sell);
        hud.AskSell(inv.Data.Find("hunting_bow"));
        StringAssert.Contains("卸下", hud.Message);
        Assert.AreEqual(ShopHud.Mode.Sell, hud.Current);
        hud.AskSell(bracers);
        Assert.AreEqual(ShopHud.Mode.ConfirmSell, hud.Current, "selling is confirmed first");
        Assert.AreEqual(20, inv.Copper, "nothing sold before confirming");
        hud.ConfirmSell();
        Assert.AreEqual(40, inv.Copper);
        Assert.AreEqual(0, inv.Count(bracers));

        // Outfit Guan Yu: the lance goes back to the bag.
        hud.SelectGeneral(guan, EquipSlot.Weapon);
        Assert.IsTrue(hud.Entries.Any(b => b.text.StartsWith("精铁马槊") && b.enabledSelf));
        hud.EquipItem(shuo);
        Assert.AreEqual(shuo, inv.Worn(StoryState.GuanYu, EquipSlot.Weapon));
        Assert.AreEqual(1, inv.Count(inv.Data.Find("cavalry_lance")));

        // Cancel steps back level by level and closes the shop from the top.
        for (int i = 0; i < 5 && c.ShopOpen; i++) hud.Back();
        Assert.IsFalse(c.ShopOpen);
        Assert.AreEqual(ShopHud.Mode.Closed, hud.Current);
        Assert.IsTrue(c.Player.InputEnabled, "control returns after leaving");

        // The battle line-up carries the new weapon: 59 → 64 physical attack at level 1.
        var lineup = c.Run.Progress.Lineup(c.Run.Story.HasJoined);
        var guanIn = lineup.First(l => l.General.Id == StoryState.GuanYu);
        Assert.AreEqual(9, guanIn.Equipment.PhysicalAttack);
        Assert.AreEqual(3, guanIn.Equipment.PhysicalDefense);
    }
}
