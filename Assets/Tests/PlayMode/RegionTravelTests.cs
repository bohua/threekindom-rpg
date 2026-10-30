using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Data.Battle;
using ThreeKingdoms.Gameplay.Story;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Chapter one's region travel (RegionTravel, coordinator 2026-10-30): from the Pingyuan base in 191 the courier lists the
// regions open that year; Luoyang's story layer receives the party at its post station and saves there; its courier
// brings the party home to Pingyuan.
public sealed class RegionTravelTests
{
    string saveDir;
    readonly Queue<int> picks = new Queue<int>();

    [SetUp] public void Fresh()
    {
        GameRun.Clear();
        StagePlayer.AutoSeconds = 0f;
        Time.timeScale = 4f;
        saveDir = System.IO.Path.Combine(Application.temporaryCachePath, "travel-" + System.Guid.NewGuid().ToString("N"));
        GameRun.SaveDirectoryOverride = saveDir;
    }

    [TearDown] public void Clean()
    {
        Time.timeScale = 1f;
        GameRun.Clear();
        GameRun.SaveDirectoryOverride = null;
        if (System.IO.Directory.Exists(saveDir)) System.IO.Directory.Delete(saveDir, true);
    }

#if UNITY_EDITOR
    static BattleCatalog Catalog => UnityEditor.AssetDatabase.LoadAssetAtPath<BattleCatalog>("Assets/_Project/Data/Battle/BattleCatalog.asset");
#else
    static BattleCatalog Catalog => null;
#endif

    IEnumerator Drive(FieldStoryController c, float seconds = 60f)
    {
        var scene = SceneManager.GetActiveScene().name;
        float deadline = Time.realtimeSinceStartup + seconds;
        yield return null;
        while (c && SceneManager.GetActiveScene().name == scene && (c.Stage.Playing || c.Busy && c.Panel == null))
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "scene " + scene + " stuck");
            if (c.Choices != null) c.Stage.Choose(picks.Count > 0 ? picks.Dequeue() : 0);
            else c.Stage.Advance();
            yield return null;
        }
        if (c && c.Panel != null) c.ClosePanel();
        yield return null;
    }

    static IEnumerator WaitForScene(string name, float seconds = 90f)
    {
        float deadline = Time.realtimeSinceStartup + seconds;
        while (SceneManager.GetActiveScene().name != name || Object.FindFirstObjectByType<FieldStoryController>() == null)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "waiting for " + name);
            yield return null;
        }
        yield return null;
    }

    IEnumerator TalkTo(FieldStoryController c, string id, params int[] answers)
    {
        foreach (var a in answers) picks.Enqueue(a);
        var who = c.Find(id);
        Assert.IsNotNull(who, id);
        Assert.IsTrue(who.gameObject.activeInHierarchy, id + " shown");
        var cc = c.Player.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;
        c.Player.transform.position = who.position + new Vector3(0, 0, -1.4f);
        if (cc) cc.enabled = true;
        yield return null;
        while (c.Busy) yield return null;
        c.Interact();
        Assert.IsTrue(c.Busy, "talking to " + id);
        yield return Drive(c);
    }

    [UnityTest]
    public IEnumerator PingyuanToLuoyangAndBack()
    {
        var run = GameRun.ForChapter(Catalog, ChapterStep.PingyuanSafe, exp: 370, copper: 500);
        run.Chapter2.Set(ChapterScripts4.PingyuanGreeted);
        run.Chapter2.Set(ChapterState.Year191);
        Assert.AreEqual(ChronicleNode.N5, run.Chapter2.Node);
        Assert.IsTrue(RegionTravel.Allowed(run.Chapter2));
        CollectionAssert.IsSubsetOf(new[] { "R3", "R4", "R5a", "R5b" }, RegionTravel.Open(run.Chapter2));
        CollectionAssert.DoesNotContain(RegionTravel.Open(run.Chapter2), "R6");

        SceneManager.LoadScene(GameRun.BohaiScene);
        yield return WaitForScene(GameRun.BohaiScene);
        var py = Object.FindFirstObjectByType<FieldStoryController>();
        yield return Drive(py);
        var labels = RegionTravel.Targets(run.Chapter2).Select(t => t.label).ToList();
        int luoyang = labels.FindIndex(l => l.Contains("洛阳"));
        Assert.GreaterOrEqual(luoyang, 0, string.Join(",", labels));
        yield return TalkTo(py, FieldScriptsRegions.Courier, luoyang);

        yield return WaitForScene("Region_R5B");
        var ly = Object.FindFirstObjectByType<FieldStoryController>();
        Assert.AreEqual(FieldStoryController.Place.R5b, ly.Location);
        yield return Drive(ly);
        var post = ly.Find(FieldScriptsRegions.Courier);
        Assert.Less(Vector3.Distance(ly.Player.transform.position, post.position), 12f, "arrived at the post station");
        Assert.AreEqual("region:R5b", ly.SaveLocation);

        // Home: the map (option 0), then 回平原 (always first when away).
        yield return TalkTo(ly, FieldScriptsRegions.Courier, 0, 0);
        yield return WaitForScene(GameRun.BohaiScene);
        Assert.AreEqual(ChapterStep.PingyuanSafe, run.Chapter2.Step);
    }
}
