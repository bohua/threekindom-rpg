using System.Collections;
using System.Linq;
using NUnit.Framework;
using ThreeKingdoms.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Texture-driven Zhuojun corner (native 30 px/m pixel textures on simple geometry).
public sealed class ZhuojunCornerTests
{
    [UnityTest] public IEnumerator CornerUsesNativePixelTexturesAndForgeGlow()
    {
        yield return SceneManager.LoadSceneAsync("Zhuojun_Corner");
        yield return null;
        var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        Assert.Greater(renderers.Length, 40);
        foreach (var r in renderers)
        {
            var tex = r.sharedMaterial.GetTexture("_BaseMap") as Texture2D;
            Assert.IsNotNull(tex, r.name);
            Assert.AreEqual(FilterMode.Point, tex.filterMode, r.name + " must stay pixel-crisp");
        }
        var smithyFront = renderers.Single(r => r.name == "Front" && r.transform.parent.name == "Smithy");
        Assert.IsTrue(smithyFront.sharedMaterial.IsKeywordEnabled("_EMISSION"), "forge glow comes from the emission mask");
        Assert.IsNotNull(smithyFront.sharedMaterial.GetTexture("_EmissionMap"));
        Assert.Greater(Object.FindObjectsByType<PixelBillboard>(FindObjectsSortMode.None).Length, 15, "props are lit pixel billboards");
    }
}