using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ThreeKingdoms.Data.Battle;
using ThreeKingdoms.Gameplay.Story;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Chapter 1 side quests in Zhuojun after the NPC design (npc-sidequests R1_R2, core layer; user 2026-10-29): the
// Zhongshan horse merchants after battle 1 and the smith's three weapons, and Gu Sou's tale after battle 3.
public sealed class ZhuojunSidequestTests
{
#if UNITY_EDITOR
    static BattleCatalog Catalog => UnityEditor.AssetDatabase.LoadAssetAtPath<BattleCatalog>("Assets/_Project/Data/Battle/BattleCatalog.asset");
#else
    static BattleCatalog Catalog => null;
#endif

    [SetUp] public void Fresh() => GameRun.Clear();
    [TearDown] public void Clean() => GameRun.Clear();

    static void Teleport(TownStoryController c, Vector3 at)
    {
        var cc = c.Player.GetComponent<CharacterController>();
        cc.enabled = false; c.Player.transform.position = at; cc.enabled = true;
    }

    static IEnumerator Talk(TownStoryController c, Transform who, Queue<int> answers, List<string> heard)
    {
        Teleport(c, who.position + new Vector3(0, 0, -1.3f));
        yield return null;
        c.Interact();
        Assert.IsTrue(c.Busy, "talking to " + who.name);
        float deadline = Time.realtimeSinceStartup + 20f;
        while (c.Busy && c.Panel == null)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline);
            yield return null;
            if (c.Line.HasValue) heard.Add(c.Line.Value.Line);
            if (c.Choices != null) c.Choose(answers.Count > 0 ? answers.Dequeue() : 0);
            else c.Advance();
        }
    }

    [UnityTest, Timeout(480000)] public IEnumerator HorseMerchantsPayAndTheSmithForgesTheThreeWeapons()
    {
        GameRun.ForBattle(Catalog, 1, 10, 150, 2);
        yield return SceneManager.LoadSceneAsync("Zhuojun_Town");
        yield return null;
        var c = Object.FindFirstObjectByType<TownStoryController>();
        var su = GameObject.Find("su_shuang 苏双");
        Assert.IsNotNull(su, "the merchants wait at the post station after battle 1");
        var heard = new List<string>();
        yield return Talk(c, su.transform, new Queue<int>(new[] { 1 }), heard);
        Assert.Contains("dlg.lb1.su_shuang.N1.repay.1", heard);
        Assert.IsTrue(c.Run.Chapter2.Has(TownStoryController.ZhuoGift));
        Assert.AreEqual("accepted", c.Run.Chapter2.Recall("zhang_shiping", "zhuo_gift"));
        Assert.AreEqual("accepted", c.Run.Chapter2.Recall("su_shuang", "zhuo_gift"));

        heard.Clear();
        yield return Talk(c, c.Smith, new Queue<int>(), heard);
        Assert.Contains("dlg.lb1.npc.r1.zhuo.smith.N1.gift.3", heard);
        Assert.IsTrue(c.Run.Chapter2.Has(TownStoryController.ZhuoWeapons));
    }

    [UnityTest, Timeout(480000)] public IEnumerator GuSouTellsOfZhangJueAfterTheThirdBattle()
    {
        GameRun.ForChapter(Catalog, ThreeKingdoms.Core.Story.ChapterStep.ZhuojunFree, 60, 200);   // battle 3 won, Zou Jing heard
        yield return SceneManager.LoadSceneAsync("Zhuojun_Town");
        yield return null;
        var c = Object.FindFirstObjectByType<TownStoryController>();
        var gu = GameObject.Find("gu_sou 鼓叟");
        Assert.IsNotNull(gu, "Gu Sou is at the tavern after battle 3");
        var heard = new List<string>();
        yield return Talk(c, gu.transform, new Queue<int>(new[] { 0 }), heard);
        Assert.Contains("dlg.lb1.gu_sou.N1.6", heard);
        Assert.Contains("dlg.lb1.gu_sou.N1.introduced.3", heard);
        Assert.AreEqual("introduced", c.Run.Chapter2.Recall("gu_sou", "met_liu_bei"));
    }
    // 卢门：灵位前的字 (sq.lb1.L4.zhuo): back in Zhuojun in 192 by the realm map, the guard's news, then Lu Yu at his
    // family's gate: three rations and the stone-classic copy (with Lu Zhi's slip) left by the tablet.
    [UnityTest, Timeout(480000)] public IEnumerator LuYuAtTheMemorialTablet()
    {
        var run = GameRun.ForChapter(Catalog, ThreeKingdoms.Core.Story.ChapterStep.Pingyuan, 400, 300, rations: 4);
        run.Chapter2.Set(ThreeKingdoms.Core.Story.ChapterState.Year192);
        run.Chapter2.Set(TownStoryController.RubbingGot);
        run.Chapter2.Set(TownStoryController.LuzhiSlip);
        yield return SceneManager.LoadSceneAsync("Zhuojun_Town");
        yield return null; yield return null;
        var c = Object.FindFirstObjectByType<TownStoryController>();
        float deadline = Time.realtimeSinceStartup + 20f;
        while (c.Busy) { Assert.Less(Time.realtimeSinceStartup, deadline); c.Advance(); yield return null; }
        Assert.IsTrue(c.Run.Chapter2.Has(TownStoryController.LuMenHeard), "the guard's news");
        var lu = GameObject.Find("lu_yu 卢毓");
        Assert.IsNotNull(lu, "Lu Yu at the gate in 192");
        var heard = new List<string>();
        yield return Talk(c, lu.transform, new Queue<int>(new[] { 0, 0 }), heard);
        Assert.Contains("dlg.lb1.lu_yu.N6.rub.1", heard);
        Assert.AreEqual("given", c.Run.Chapter2.Recall("lu_yu", "rice"));
        Assert.AreEqual("gave_rubbing", c.Run.Chapter2.Recall("lu_yu", "rubbing"));
        Assert.AreEqual(1, c.Run.Progress.Inventory.Rations);
        Assert.IsTrue(c.Run.Chapter2.Has(TownStoryController.LuMen));
    }
}
