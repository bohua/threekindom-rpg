using System.Collections;
using Jrpg;
using NUnit.Framework;
using ThreeKingdoms.Data.Battle;
using ThreeKingdoms.Gameplay;
using ThreeKingdoms.Gameplay.Story;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// R1 涿蓟 (user 2026-10-27: the big map round Zhuojun): from the region in at Zhuojun's south gate, and out again at the
// end of the south road. R1 (x, z) = town (x + 360, z + 72).
public sealed class R1RegionTests
{
    bool intro;
    [SetUp] public void Fresh() { GameRun.Clear(); intro = TownStoryController.PlayArrivalIntro; TownStoryController.PlayArrivalIntro = false; }
    [TearDown] public void Clean() { GameRun.Clear(); TownStoryController.PlayArrivalIntro = intro; }

#if UNITY_EDITOR
    static BattleCatalog Catalog => UnityEditor.AssetDatabase.LoadAssetAtPath<BattleCatalog>("Assets/_Project/Data/Battle/BattleCatalog.asset");
#else
    static BattleCatalog Catalog => null;
#endif

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
    public IEnumerator RegionLeadsIntoZhuojunAndBack()
    {
        GameRun.NewGame(Catalog).NextArrival = GameRun.Arrival.ReturnFromBattle;
        SceneManager.LoadScene("Region_R1");
        yield return WaitScene("Region_R1");

        // In at the south gate.
        yield return WalkInto(new Vector3(360f, 2f, 33f));
        yield return WaitScene(GameRun.TownScene);
        Assert.Less(Flat(Player.position, new Vector3(0, 0, -42f)), 2f, "just outside Zhuojun's south gate, in town");

        // Out at the end of the south road.
        yield return WalkInto(new Vector3(0, 0.5f, -60f));
        yield return WalkInto(new Vector3(0, 0.5f, -76f));
        yield return WaitScene("Region_R1");
        Assert.Less(Flat(Player.position, new Vector3(360f, 0, 24f)), 2f, "on the R1 road south of the gate");
    }
}
