using ThreeKingdoms.Core.Story;
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

    // A cold editor's first load of Zhuojun_Town can take over two minutes.
    [UnityTest, Timeout(480000)]
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

    // On steep ground (the abandoned beacon sits in a fold of the hills) the camera tilts up until the hillside no
    // longer stands between it and the party (user 2026-10-28).
    [UnityTest]
    public IEnumerator CameraClearsTheHillsideAtTheBeacon()
    {
        GameRun.NewGame(Catalog);
        SceneManager.LoadScene("Region_R1");
        yield return WaitScene("Region_R1");
        var spot = GameObject.Find("Spot 废弃烽燧");
        Assert.IsNotNull(spot);
        yield return WalkInto(spot.transform.position + Vector3.up * 0.5f);
        yield return new WaitForSeconds(4f);
        var cam = Object.FindFirstObjectByType<FollowCamera>().transform;
        var to = Player.position + Vector3.up * 0.8f;
        Debug.Log($"R1 beacon camera pitch {cam.eulerAngles.x:0.0} at {cam.position} player {Player.position}");
        foreach (var h in Physics.RaycastAll(cam.position, (to - cam.position).normalized, Vector3.Distance(cam.position, to) - 0.5f))
            Assert.IsFalse(h.collider is MeshCollider && h.collider.name.StartsWith("Terrain"), "hillside between camera and party: " + h.collider.name);
    }
    // Rivers are not walkable (user 2026-10-29): from the bank of the Juma, pushing straight at the river stops at
    // its edge.
    [UnityTest]
    public IEnumerator PartyCannotWalkIntoTheRiver()
    {
        GameRun.NewGame(Catalog);
        SceneManager.LoadScene("Region_R1");
        yield return WaitScene("Region_R1");
        var mid = new Vector3(106f, 0, 61.5f);
        var inward = -new Vector3(0.762f, 0, 0.647f);
        var bank = mid - inward * 16f;
        Assert.IsTrue(Physics.Raycast(bank + Vector3.up * 80f, Vector3.down, out var hit, 200f));
        yield return WalkInto(hit.point + Vector3.up * 0.3f);
        var cc = Player.GetComponent<CharacterController>();
        for (int i = 0; i < 120; i++) { cc.Move(inward * 0.25f + Vector3.down * 0.2f); yield return null; }
        Assert.Greater(Flat(Player.position, mid), 5f, "walked into the river: " + Player.position);
    }
    // People at the wild spots after the NPC design's R1.6 wiring table (user 2026-10-29): in 190 (N4) the hunter
    // stands at his cabin and his first line shows in the bubble; the old soldier is still at the beacon.
    [UnityTest]
    public IEnumerator WildSpotsHaveSomeoneToTalkTo()
    {
        GameRun.ForChapter(Catalog, ChapterStep.Gongsun, 0, 100);
        SceneManager.LoadScene("Region_R1");
        yield return WaitScene("Region_R1");
        var hunter = GameObject.Find("Talk 猎户石大");
        Assert.IsNotNull(hunter, "the hunter is placed");
        Assert.IsNotNull(GameObject.Find("Talk 郝老卒").GetComponent<RegionTalk>().Current, "the old soldier is at the beacon in 190");
        yield return WalkInto(hunter.transform.position + new Vector3(1.5f, 0.5f, -1.5f));
        yield return null; yield return null;
        var hud = Object.FindFirstObjectByType<ThreeKingdoms.UI.Region.RegionHud>();
        Assert.IsTrue(hud.BubbleLines.Count > 0 && hud.BubbleLines[0].StartsWith("猎户石大：皮子要不要"), string.Join("|", hud.BubbleLines));
    }
}
