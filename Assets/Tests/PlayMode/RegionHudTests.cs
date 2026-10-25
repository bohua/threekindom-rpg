using System.Collections;
using NUnit.Framework;
using ThreeKingdoms.Gameplay;
using ThreeKingdoms.UI.Region;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Bohai plain field HUD (user 2026-10-24): the signboard names town · spot inside a town and region · road "A ↔ B"
// outside; the mini map is there.
public sealed class RegionHudTests
{
    static IEnumerator At(Transform player, Vector3 at)
    {
        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false; player.position = at; cc.enabled = true;
        yield return null; yield return null;
    }

    [UnityTest] public IEnumerator SignboardFollowsThePlayer()
    {
        yield return SceneManager.LoadSceneAsync("Bohai_Plain");
        yield return null;
        var hud = Object.FindFirstObjectByType<RegionHud>();
        var player = Object.FindFirstObjectByType<Jrpg.PlayerMover>().transform;
        Assert.IsNotNull(hud.Map, "the mini map is baked");

        yield return At(player, new Vector3(-60f, 0, -151f));
        Assert.AreEqual("平原 · 县衙", hud.SignText);
        Assert.AreEqual("坐标  x -60  ·  z -151", hud.CoordsText, "the position readout under the sign");
        yield return At(player, new Vector3(-60f, 0.35f, -255f));
        Assert.AreEqual("平原 · 码头", hud.SignText);
        yield return At(player, new Vector3(-60f, 0, -200f));
        Assert.AreEqual("平原 · 南城门", hud.SignText);
        yield return At(player, new Vector3(100f, 0, -165f));
        Assert.AreEqual("渤海 · 平原东 ↔ 高唐", hud.SignText);
        yield return At(player, new Vector3(150f, 0, -170f));
        Assert.AreEqual("渤海 · 平原东 ↔ 高唐", hud.SignText, "the main road wins over a spur at the fork");
        yield return At(player, new Vector3(-10f, 0, 50f));
        Assert.AreEqual("渤海 · 平原北 ↔ 北村", hud.SignText);
        yield return At(player, new Vector3(200f, 0, 150f));
        Assert.AreEqual("渤海 · 野外", hud.SignText);
    }

    // Out on the roads the party mounts and moves faster; back in a town it dismounts and walks (user 2026-10-24).
    [UnityTest] public IEnumerator PartyRidesOutsideAndWalksInTown()
    {
        yield return SceneManager.LoadSceneAsync("Bohai_Plain");
        yield return null;
        var ride = Object.FindFirstObjectByType<RideController>();
        var player = ride.Mover.transform;
        Assert.AreEqual(MountRider.State.OnFoot, ride.Leader.Current, "walking in the county office square");
        Assert.AreEqual(ride.WalkSpeed, ride.Mover.Speed);

        yield return At(player, new Vector3(100f, 0, -165f));
        yield return new WaitForSeconds(ride.Settle + ride.Leader.SequenceTime + 0.3f);
        Assert.AreEqual(MountRider.State.Riding, ride.Leader.Current, "on the east road");
        Assert.AreEqual(ride.RideSpeed, ride.Mover.Speed);
        yield return new WaitForSeconds(ride.CompanionDelay * 2f + ride.Leader.SequenceTime);
        foreach (var c in ride.Companions) Assert.AreEqual(MountRider.State.Riding, c.Current, c.name + " rides too");
        // Galloping east along the road, the horses keep at least a horse length (about 3 m) apart (user 2026-10-24).
        for (float t = 0; t < 2.5f; t += Time.deltaTime) { ride.Mover.Move(new Vector2(1f, 0f), Time.deltaTime); yield return null; }
        var line = new System.Collections.Generic.List<Transform> { player };
        foreach (var c in ride.Companions) line.Add(c.transform);
        for (int i = 1; i < line.Count; i++)
            Assert.GreaterOrEqual(Vector3.Distance(line[i].position, line[i - 1].position), 5.5f, line[i].name + " keeps a horse length behind");

        yield return At(player, new Vector3(-60f, 0, -151f));
        yield return new WaitForSeconds(ride.Settle + ride.Leader.SequenceTime + 0.3f);
        Assert.AreEqual(MountRider.State.OnFoot, ride.Leader.Current, "back in Pingyuan");
        Assert.AreEqual(ride.WalkSpeed, ride.Mover.Speed);
        Assert.IsTrue(ride.Mover.InputEnabled);
    }

    // A signpost at a fork shows a bubble with each way and its place (user 2026-10-24); woods, hills and rivers
    // block the way, bridges do not.
    [UnityTest] public IEnumerator SignpostsAndImpassableTerrain()
    {
        yield return SceneManager.LoadSceneAsync("Bohai_Plain");
        yield return null;
        var hud = Object.FindFirstObjectByType<RegionHud>();
        var player = Object.FindFirstObjectByType<Jrpg.PlayerMover>().transform;
        yield return At(player, GameObject.Find("Spot Sign west").transform.position);
        yield return null;
        Assert.AreEqual(4, hud.BubbleLines.Count, string.Join(" / ", hud.BubbleLines));
        Assert.IsTrue(hud.BubbleLines.Exists(l => l.Contains("西村")) && hud.BubbleLines.Exists(l => l.Contains("平原城")));
        yield return At(player, new Vector3(200f, 0, 150f));
        Assert.AreEqual(0, hud.BubbleLines.Count, "no bubble away from the signs");

        bool Blocked(Vector3 at) => Physics.CheckBox(at + Vector3.up * 1f, new Vector3(0.3f, 0.6f, 0.3f));
        Assert.IsTrue(Blocked(new Vector3(-180f, 0, 0f)), "the 曲陌 wood is impassable");
        Assert.IsTrue(Blocked(new Vector3(-135f, 0, 95f)), "the 笃马河 is impassable");
        Assert.IsTrue(Blocked(new Vector3(-150f, 0, 320f)), "the northern hills are impassable");
        Assert.IsFalse(Blocked(new Vector3(-2.9f, 0, 59.9f)), "the road bridge is open");
        Assert.IsTrue(Blocked(new Vector3(285f, 0, -62f)), "the reed pond is impassable over its middle too");
        Assert.IsNotNull(GameObject.Find("Pond water"), "the reed pond shows its water (user 2026-10-25: it looked like grass)");
    }
}
