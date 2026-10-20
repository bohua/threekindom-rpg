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
            var tex = r.sharedMaterial ? r.sharedMaterial.GetTexture("_BaseMap") : null;
            if (tex == null || r.name == "Pixel sprite") continue;
            Assert.AreEqual(FilterMode.Point, tex.filterMode, r.name + " " + tex.name);
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
        Assert.Greater(boxes.Count, 30, "the town is lined with buildings");
        for (int i = 0; i < boxes.Count; i++)
        for (int j = i + 1; j < boxes.Count; j++)
        {
            var a = boxes[i].bounds; a.Expand(-0.1f);
            Assert.IsFalse(a.Intersects(boxes[j].bounds), boxes[i].name + " overlaps " + boxes[j].name);
        }
    }

    // Speed doubled (user 2026-10-20) and walking plays the drawn 4-frame cycle for the facing direction.
    [UnityTest] public IEnumerator PlayerWalksFastWithTheDrawnCycle()
    {
        yield return SceneManager.LoadSceneAsync("Zhuojun_Town");
        yield return null;
        var mover = Object.FindFirstObjectByType<PlayerMover>();
        var walk = mover.GetComponent<PixelWalkFrames>();
        Assert.AreEqual(7f, mover.Speed, 0.01f);
        Assert.IsTrue(walk.HasWalkCycle, "Liu Bei's atlas carries the 16 walk frames");
        float z0 = mover.transform.position.z;
        var seen = new System.Collections.Generic.HashSet<int>();
        for (float t = 0; t < 1f; t += 0.02f)
        {
            mover.Move(Vector2.up, 0.02f);
            yield return null;
            if (walk.Walking) seen.Add(walk.CurrentFrame);
        }
        Assert.Greater(mover.transform.position.z - z0, 6f, "about 7 m in a second");
        int upRow = PixelWalkFrames.WalkStart + 3 * PixelWalkFrames.WalkFramesPerRow;
        Assert.That(seen, Is.EquivalentTo(new[] { upRow, upRow + 1, upRow + 2, upRow + 3 }), "all four back-view frames play");
        Assert.IsFalse(walk.Mirrored, "the drawn cycle needs no mirroring");
        for (int i = 0; i < 3; i++) yield return null;
        Assert.IsFalse(walk.Walking, "standing still stops the cycle");
        Assert.AreEqual(upRow + 1, walk.CurrentFrame, "idle stands on the passing frame of the last direction");
    }
}
