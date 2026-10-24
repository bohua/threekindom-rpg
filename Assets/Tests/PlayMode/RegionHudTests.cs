using System.Collections;
using NUnit.Framework;
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
}
