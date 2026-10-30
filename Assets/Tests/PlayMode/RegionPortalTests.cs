using System.Collections;
using Jrpg;
using NUnit.Framework;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Data.Battle;
using ThreeKingdoms.Gameplay.Story;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// R2 中山 (user 2026-10-26): out of Anxi's south gate into the Zhongshan region, over the Hutuo by the ferry and back,
// and in again through the south gate.
public sealed class RegionPortalTests
{
    [SetUp] public void Fresh() { GameRun.Clear(); StagePlayer.AutoSeconds = 0f; }
    [TearDown] public void Clean() => GameRun.Clear();

#if UNITY_EDITOR
    static BattleCatalog Catalog => UnityEditor.AssetDatabase.LoadAssetAtPath<BattleCatalog>("Assets/_Project/Data/Battle/BattleCatalog.asset");
#else
    static BattleCatalog Catalog => null;
#endif

    static IEnumerator Settle(FieldStoryController c)
    {
        float deadline = Time.realtimeSinceStartup + 60f;
        yield return null;
        while (c && (c.Busy || c.Stage.Playing))
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "arrival scene stuck");
            if (c.Choices != null) c.Stage.Choose(0); else c.Stage.Advance();
            yield return null;
        }
    }

    static IEnumerator WaitScene(string name)
    {
        float deadline = Time.realtimeSinceStartup + 30f;
        while (SceneManager.GetActiveScene().name != name) { Assert.Less(Time.realtimeSinceStartup, deadline, "waiting for " + name); yield return null; }
        yield return null; yield return null;
    }

    static Transform Player => Object.FindFirstObjectByType<PlayerMover>().transform;

    static IEnumerator WalkInto(Vector3 at)
    {
        var p = Player;
        var cc = p.GetComponent<CharacterController>();
        if (cc) cc.enabled = false; p.position = at; if (cc) cc.enabled = true;
        yield return null; yield return null;
    }

    static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

    [UnityTest]
    public IEnumerator AnxiSouthGateLeadsIntoZhongshanAndBack()
    {
        GameRun.ForChapter(Catalog, ChapterStep.Anxi, exp: 150, copper: 260);
        SceneManager.LoadScene(GameRun.AnxiScene);
        yield return WaitScene(GameRun.AnxiScene);
        yield return Settle(Object.FindFirstObjectByType<FieldStoryController>());

        // Out through the south gate.
        yield return WalkInto(new Vector3(0, 0, -30f));
        yield return WaitScene("Zhongshan_Region");
        Assert.Less(Flat(Player.position, new Vector3(278f, 0, 124f)), 2f, "outside Anxi's south gate");

        // Across the Hutuo and back by the ferry.
        yield return WalkInto(new Vector3(271f, 0, 80.8f));
        Assert.Less(Player.position.z, 52f, "ferried to the south bank");
        yield return WalkInto(new Vector3(269f, 0, 54.2f));
        Assert.Greater(Player.position.z, 84f, "ferried back to the north bank");

        // In again at the south gate.
        yield return WalkInto(new Vector3(278f, 0, 128.4f));
        yield return WaitScene(GameRun.AnxiScene);
        var c = Object.FindFirstObjectByType<FieldStoryController>();
        Assert.IsNotNull(c);
        Assert.Less(Flat(c.Player.transform.position, new Vector3(0, 0, -21f)), 2f, "inside Anxi's south gate");
        Assert.AreEqual(ChapterStep.Anxi, GameRun.Current.Chapter2.Step, "the story step is untouched");
    }
}
