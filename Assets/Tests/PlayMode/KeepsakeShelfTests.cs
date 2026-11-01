using System.Collections;
using NUnit.Framework;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.UI.Menu;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// The keepsake shelf (design/gdd/keepsake-shelf.md): 物品 ⇄ 信物 on Y / Tab, two columns of cards, held count from the save.
// With KEEPSAKE_SHOTS=<dir> it also saves a screenshot of the page for review.
public sealed class KeepsakeShelfTests
{
    [SetUp] public void Fresh() => GameRun.Clear();
    [TearDown] public void Clean() => GameRun.Clear();

    [UnityTest, Timeout(600000)] public IEnumerator ItemsPageTurnsToKeepsakes()
    {
        yield return SceneManager.LoadSceneAsync("Zhuojun_Town");
        yield return null;
        var c = Object.FindFirstObjectByType<TownStoryController>();
        var menu = Object.FindFirstObjectByType<MainMenuHud>();
        foreach (var id in new[] { "gu_sou_drumsticks", "zang_hong_draft", "bao_tao_arrow", "lu_ditou_clappers", "zhang_shiping_ledger", "pan_daqiao_tags", "chu_weng_mat" })
            c.Run.Chapter2.Set(SideQuests.KeepsakePrefix + id);
        Assert.AreEqual(7, KeepsakeCatalog.HeldCount(c.Run.Chapter2));

        c.OpenPanel(TownStoryController.MenuPanel);
        Assert.AreEqual(MainMenuHud.Tab.Items, menu.CurrentTab);
        Assert.IsFalse(menu.KeepsakePage, "opens on the goods");
        menu.Alternate();
        Assert.IsTrue(menu.KeepsakePage, "Y / Tab turns to 信物");
        menu.Move(1, 0);
        Assert.AreEqual(1, menu.KeepsakeIndex, "right moves along the row");
        menu.Move(0, 1);
        Assert.AreEqual(3, menu.KeepsakeIndex, "down moves a row");
        menu.Move(0, -1);
        menu.Confirm();
        StringAssert.Contains("信物", menu.Message, "keepsakes cannot be used");

        var dir = System.Environment.GetEnvironmentVariable("KEEPSAKE_SHOTS");
        if (!string.IsNullOrEmpty(dir))
        {
            System.IO.Directory.CreateDirectory(dir);
            menu.Move(-1, 0); menu.Move(0, 1);   // 臧洪盟辞底稿
            for (int i = 0; i < 20; i++) yield return null;
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "keepsake-shelf.png"));
            for (int i = 0; i < 5; i++) yield return null;
            menu.Move(0, -1);   // 胡市马契, not yet
            for (int i = 0; i < 10; i++) yield return null;
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "keepsake-shelf-unheld.png"));
            for (int i = 0; i < 5; i++) yield return null;
        }

        menu.Alternate();
        Assert.IsFalse(menu.KeepsakePage, "and back to 物品");
        menu.Back();
        Assert.AreEqual(MainMenuHud.Mode.Closed, menu.Current);
    }
}
