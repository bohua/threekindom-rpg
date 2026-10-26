using System.Collections;
using System.Linq;
using Jrpg;
using NUnit.Framework;
using ThreeKingdoms.Gameplay;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Zhuojun town (ZHUOJUN_TOWN_DESIGN.md) built from the native texture delivery: all 12 locations exist, the player
// walks in through the south gate, walls block, and walls between camera and player are cut away (design §6).
public sealed class ZhuojunTownTests
{
    [SetUp] public void Fresh() => ThreeKingdoms.Gameplay.Story.GameRun.Clear();

    static IEnumerator Walk(PlayerMover mover, Vector2 input, float seconds)
    {
        for (float t = 0; t < seconds; t += 0.02f) { mover.Move(input, 0.02f); yield return null; }
    }

    [UnityTest] public IEnumerator TownHasAllTwelveLocationsAndIsWalkable()
    {
        yield return SceneManager.LoadSceneAsync("Zhuojun_Town");
        yield return null;
        string[] ids = { "01 ", "02 ", "03 ", "04 ", "05 ", "06 ", "07 ", "08 ", "09 ", "10 ", "11 ", "12 " };
        var names = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Select(t => t.name).ToList();
        foreach (var id in ids) Assert.IsTrue(names.Any(n => n.StartsWith(id)), "location " + id.Trim() + " is built");

        var mover = Object.FindFirstObjectByType<PlayerMover>();
        Assert.Less(mover.transform.position.z, -38f, "the player starts outside the south gate (design §5)");

        // Walk north through the gate passage into the main axis.
        yield return Walk(mover, Vector2.up, 2.5f);   // 7 m/s: from z -46 to about -28.5
        Assert.Greater(mover.transform.position.z, -30f, "the south gate passage is open");
        Assert.AreEqual(0f, mover.transform.position.x, 0.3f);

        // The south wall now stands between the camera and the player and must be cut away.
        yield return null;
        var southWall = Object.FindObjectsByType<Occluder>(FindObjectsSortMode.None).Single(o => o.name == "10 South gate" && o.isActiveAndEnabled);
        var r = southWall.GetComponentInChildren<MeshRenderer>();
        Assert.AreEqual(ShadowCastingMode.ShadowsOnly, r.shadowCastingMode, "the gate block hides while it covers the player");

        // Walk west along the south loop street into the west city wall: it blocks.
        yield return Walk(mover, Vector2.up, 0.6f);
        float z = mover.transform.position.z;
        yield return Walk(mover, Vector2.left, 9f);
        Assert.Greater(mover.transform.position.x, -46.5f, "the west city wall blocks");
        Assert.AreEqual(z, mover.transform.position.z, 1.5f);
    }

    [UnityTest] public IEnumerator EveryRendererUsesPointFilteredNativeTextures()
    {
        yield return SceneManager.LoadSceneAsync("Zhuojun_Town");
        yield return null;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            var tex = r.sharedMaterial && r.sharedMaterial.HasProperty("_BaseMap") ? r.sharedMaterial.GetTexture("_BaseMap") : null;   // the blended ground has its own layers
            if (tex == null || r.name == "Pixel sprite") continue;
            // Pixel textures stay point-filtered; the HD renovation's painted textures (2026-10-26) are mipmapped instead.
            Assert.IsTrue(tex.filterMode == FilterMode.Point || tex.mipmapCount > 1, r.name + " " + tex.name + " " + tex.filterMode);
        }
        var smithy = GameObject.Find("02 Weapons shop").transform.Find("Front").GetComponent<MeshRenderer>().sharedMaterial;
        Assert.IsTrue(smithy.IsKeywordEnabled("_EMISSION"), "forge glow kept (RealtimeEmissive)");
        Assert.Greater(Object.FindObjectsByType<PixelBillboard>(FindObjectsSortMode.None).Length, 80, "props are lit pixel billboards");
    }

    [UnityTest] public IEnumerator BuildingsDoNotOverlap()
    {
        yield return SceneManager.LoadSceneAsync("Zhuojun_Town");
        yield return null;
        var boxes = Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None)
            .Where(b => b.transform.parent != null && b.transform.parent.Find("Front") != null)
            .Select(b => (name: b.transform.parent.name, bounds: b.bounds)).ToList();
        Assert.GreaterOrEqual(boxes.Count, 12, "the 13 buildings of the concept map (user 2026-10-23)");
        for (int i = 0; i < boxes.Count; i++)
        for (int j = i + 1; j < boxes.Count; j++)
        {
            var a = boxes[i].bounds; a.Expand(-0.1f);
            Assert.IsFalse(a.Intersects(boxes[j].bounds), boxes[i].name + " overlaps " + boxes[j].name);
        }
    }

    // The commandery stands on a 2.1 m platform (concept map, user 2026-10-23): the six platform steps and the three
    // hall steps are solid and climbable all the way from the square (user 2026-10-20: the party used to walk through them).
    [UnityTest] public IEnumerator PlayerClimbsTheHallSteps()
    {
        yield return SceneManager.LoadSceneAsync("Zhuojun_Town");
        yield return null;
        var mover = Object.FindFirstObjectByType<PlayerMover>();
        var cc = mover.GetComponent<CharacterController>();
        cc.enabled = false; mover.transform.position = new Vector3(0, 0, 13.5f); cc.enabled = true;
        yield return null;
        for (float t = 0; t < 2.5f; t += 0.02f) { mover.Move(Vector2.up, 0.02f); yield return null; }
        Assert.Greater(mover.transform.position.y, 3.0f, "up on the hall landing, not inside the platform");
        Assert.Greater(mover.transform.position.z, 22.4f, "up at the door");
        Assert.Less(mover.transform.position.z, 24f, "the hall wall still stops the player");
        // And back down onto the platform court.
        for (float t = 0; t < 0.25f; t += 0.02f) { mover.Move(Vector2.down, 0.02f); yield return null; }
        Assert.That(mover.transform.position.y, Is.InRange(1.95f, 2.3f), "standing on the platform");
    }

    // Each district has its own shot (user 2026-10-24): the camera eases low and close at the commandery steps and
    // high and wide over the square, turns toward the side streets, and keeps looking at the player throughout.
    [UnityTest] public IEnumerator CameraEasesIntoDistrictShots()
    {
        yield return SceneManager.LoadSceneAsync("Zhuojun_Town");
        yield return null;
        var mover = Object.FindFirstObjectByType<PlayerMover>();
        var cc = mover.GetComponent<CharacterController>();
        var follow = Object.FindFirstObjectByType<FollowCamera>();
        Assert.IsTrue(follow.UseRig);
        IEnumerator Stand(Vector3 at) { cc.enabled = false; mover.transform.position = at; cc.enabled = true; yield return new WaitForSeconds(2.5f); }

        yield return Stand(new Vector3(0, 0, 12.5f));
        Assert.AreEqual("Commandery steps", follow.ActiveZone.name);
        Assert.AreEqual(17f, Mathf.DeltaAngle(0, follow.transform.eulerAngles.x), 1f, "low angle up at the hall");
        float stepsDistance = Vector3.Distance(follow.transform.position, mover.transform.position);

        yield return Stand(new Vector3(0, 0, -15f));
        Assert.AreEqual("Main axis", follow.ActiveZone.name);
        Assert.AreEqual(32f, Mathf.DeltaAngle(0, follow.transform.eulerAngles.x), 1f, "high over the main axis");
        Assert.Greater(Vector3.Distance(follow.transform.position, mover.transform.position), stepsDistance + 8f, "pulled back");

        yield return Stand(new Vector3(30f, 0, -8f));
        Assert.AreEqual(12f, Mathf.DeltaAngle(0, follow.transform.eulerAngles.y), 1f, "turned toward the smithy street");
        var toPlayer = (mover.transform.position + Vector3.up - follow.transform.position).normalized;
        Assert.Greater(Vector3.Dot(follow.transform.forward, toPlayer), 0.99f, "the player stays in the middle of the shot");

        // On the market square the camera sinks slowly until the hills show over the commandery (user 2026-10-26).
        cc.enabled = false; mover.transform.position = new Vector3(0, 0, 2f); cc.enabled = true;
        yield return new WaitForSeconds(1f);
        float midway = Mathf.DeltaAngle(0, follow.transform.eulerAngles.x);
        Assert.Greater(midway, 12f, "it lowers gradually, not at once");
        yield return new WaitForSeconds(6f);
        Assert.AreEqual("Market square (low, the hills behind)", follow.ActiveZone.name);
        Assert.AreEqual(8f, Mathf.DeltaAngle(0, follow.transform.eulerAngles.x), 1f, "low over the square");
    }

    // Speed doubled (user 2026-10-20) and walking plays the drawn 4-frame cycle for the facing direction.
    [UnityTest] public IEnumerator PlayerWalksFastWithTheDrawnCycle()
    {
        yield return SceneManager.LoadSceneAsync("Zhuojun_Town");
        yield return null;
        var mover = Object.FindFirstObjectByType<PlayerMover>();
        var walk = mover.GetComponent<PixelWalkFrames>();
        Assert.AreEqual(9f, mover.Speed, 0.01f);
        Assert.IsTrue(walk.HasWalkCycle, "Liu Bei's atlas carries the 16 walk frames");
        float z0 = mover.transform.position.z;
        var seen = new System.Collections.Generic.HashSet<int>();
        for (float t = 0; t < 1f; t += 0.02f)
        {
            mover.Move(Vector2.up, 0.02f);
            yield return null;
            if (walk.Walking) seen.Add(walk.CurrentFrame);
        }
        Assert.Greater(mover.transform.position.z - z0, 8f, "about 9 m in a second");
        int upRow = PixelWalkFrames.WalkStart + 3 * PixelWalkFrames.WalkFramesPerRow;
        Assert.That(seen, Is.EquivalentTo(new[] { upRow, upRow + 1, upRow + 2, upRow + 3 }), "all four back-view frames play");
        Assert.IsFalse(walk.Mirrored, "the drawn cycle needs no mirroring");
        for (int i = 0; i < 3; i++) yield return null;
        Assert.IsFalse(walk.Walking, "standing still stops the cycle");
        Assert.AreEqual(upRow + 1, walk.CurrentFrame, "idle stands on the passing frame of the last direction");
    }
}
