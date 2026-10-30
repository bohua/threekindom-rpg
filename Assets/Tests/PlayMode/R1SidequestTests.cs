using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Data.Battle;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.UI.Story;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// The R1 涿蓟 map's side quests after the NPC design (npc-sidequests R1_R2, core layer; user 2026-10-29), in 190 while
// Gongsun Zan's garrison is the base: 简雍打听 in Ji with the first 天下传闻, Kumo's horses (the province's way), and
// Lousang under the mulberry.
public sealed class R1SidequestTests
{
    readonly Queue<int> picks = new Queue<int>();

#if UNITY_EDITOR
    static BattleCatalog Catalog => UnityEditor.AssetDatabase.LoadAssetAtPath<BattleCatalog>("Assets/_Project/Data/Battle/BattleCatalog.asset");
#else
    static BattleCatalog Catalog => null;
#endif

    [SetUp] public void Fresh() { GameRun.Clear(); StagePlayer.AutoSeconds = 0f; }
    [TearDown] public void Clean() => GameRun.Clear();

    static void Teleport(Transform who, Vector3 at)
    {
        var cc = who.GetComponent<CharacterController>();
        if (cc) cc.enabled = false; who.position = at; if (cc) cc.enabled = true;
    }

    IEnumerator TalkTo(FieldStoryController c, string id, params int[] answers)
    {
        foreach (var a in answers) picks.Enqueue(a);
        var who = c.Find(id);
        Assert.IsNotNull(who, id);
        Assert.IsTrue(who.gameObject.activeInHierarchy, id + " is there");
        Teleport(c.Player.transform, who.position + new Vector3(0, 0.3f, -1.4f));
        yield return null; yield return null;
        c.Interact();
        Assert.IsTrue(c.Busy, "talking to " + id);
        float deadline = Time.realtimeSinceStartup + 30f;
        yield return null;
        while (c.Stage.Playing || c.Busy && c.Panel == null)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "stuck talking to " + id);
            if (c.Choices != null) c.Stage.Choose(picks.Count > 0 ? picks.Dequeue() : 0);
            else c.Stage.Advance();
            yield return null;
        }
        yield return null;
    }

    IEnumerator Arrive()
    {
        GameRun.ForChapter(Catalog, ChapterStep.Gongsun, 200, 300);
        SceneManager.LoadScene("Region_R1");
        float deadline = Time.realtimeSinceStartup + 300f;
        while (Object.FindFirstObjectByType<FieldStoryController>() == null || SceneManager.GetActiveScene().name != "Region_R1")
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "loading Region_R1");
            yield return null;
        }
        var c = Object.FindFirstObjectByType<FieldStoryController>();
        while (c.Busy) yield return null;
        yield return null;
    }

    [UnityTest, Timeout(480000)] public IEnumerator JianYongListensRoundJiAndTheRumoursOpen()
    {
        yield return Arrive();
        var c = Object.FindFirstObjectByType<FieldStoryController>();
        Assert.IsFalse(c.Find("ji_jian_yong_hall").gameObject.activeSelf, "Jian Yong waits at the hall only once the city is heard");
        yield return TalkTo(c, "ji_jian_yong");
        Assert.IsTrue(c.Run.Chapter2.Has(FieldScriptsR1.EarStarted));
        yield return TalkTo(c, "ji_guest_camp");
        yield return TalkTo(c, "ji_refugee");
        yield return TalkTo(c, "ji_east_gate");
        yield return TalkTo(c, "ji_jian_yong_hall");
        Assert.IsTrue(c.Run.Chapter2.Has(FieldScriptsR1.JianYongEar));
        Assert.AreEqual("done", c.Run.Chapter2.Recall("jian_yong", "first_rumor"));
        Assert.AreEqual(FieldScriptsR1.RumourPanel, c.Panel, "the 天下传闻 panel opens");
        var hud = Object.FindFirstObjectByType<RumorHud>();
        Assert.IsTrue(hud.IsOpen);
        Assert.AreEqual(2, hud.Heard.Count);
        c.ClosePanel();
    }

    [UnityTest, Timeout(480000)] public IEnumerator KumoGetsHisHorsesByLiuYusWrit()
    {
        yield return Arrive();
        var c = Object.FindFirstObjectByType<FieldStoryController>();
        yield return TalkTo(c, "kumo");
        Assert.IsTrue(c.Run.Chapter2.Has(FieldScriptsR1.HuStarted));
        c.Run.Chapter2.Set("hu_choice_1");   // at the garrison the quartermaster's choice (FieldScripts3): the province's way
        yield return TalkTo(c, "liu_yu");
        Assert.IsTrue(c.Run.Chapter2.Has(FieldScriptsR1.HuLiuYu));
        Assert.AreEqual("yes", c.Run.Chapter2.Recall("liu_yu", "sent_writ"));
        yield return TalkTo(c, "kumo");
        Assert.IsTrue(c.Run.Chapter2.Has(FieldScriptsR1.HuMarket));
        Assert.IsTrue(c.Run.Chapter2.Has(FieldScriptsR1.HuMarket + "=liu_yu"));
    }

    [UnityTest, Timeout(480000)] public IEnumerator LousangUnderTheMulberry()
    {
        yield return Arrive();
        var c = Object.FindFirstObjectByType<FieldStoryController>();
        yield return TalkTo(c, "ls_mother");
        yield return TalkTo(c, "ls_zijing", 1);
        Assert.AreEqual("never_forgot", c.Run.Chapter2.Recall("liu_zijing", "n4_answer"));
        yield return TalkTo(c, "ls_yuanqi");
        yield return TalkTo(c, "ls_deran");
        yield return TalkTo(c, "ls_tree");
        Assert.IsTrue(c.Run.Chapter2.Has(FieldScriptsR1.Lousang));
        Assert.AreEqual("visited", c.Run.Chapter2.Recall("liu_bei", "L6_lousang"));
    }
}
