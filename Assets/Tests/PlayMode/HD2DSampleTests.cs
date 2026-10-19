using System.Collections;
using System.Linq;
using NUnit.Framework;
using ThreeKingdoms.Gameplay;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

// ADR-0005 Validation Criteria that can be checked without a human looking at the screen.
public sealed class HD2DSampleTests
{
    [UnityTest] public IEnumerator SampleSceneHasHD2DWiring()
    {
        yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("HD2D_Sample");
        yield return null;
        var camera = Camera.main;
        Assert.IsNotNull(camera);
        Assert.IsFalse(camera.orthographic);
        Assert.AreEqual(20f, camera.fieldOfView, 0.01f);
        Assert.AreEqual(25f, camera.transform.eulerAngles.x, 0.01f);
        Assert.IsTrue(camera.GetUniversalAdditionalCameraData().renderPostProcessing);

        var sprites = Object.FindObjectsByType<PixelBillboard>(FindObjectsSortMode.None);
        Assert.GreaterOrEqual(sprites.Length, 4);
        foreach (var sprite in sprites)
        {
            var renderer = sprite.GetComponent<MeshRenderer>();
            var material = renderer.sharedMaterial;
            Assert.AreEqual("Universal Render Pipeline/Lit", material.shader.name);
            Assert.AreEqual(1f, material.GetFloat("_AlphaClip"));
            Assert.IsTrue(material.IsKeywordEnabled("_ALPHATEST_ON"));
            Assert.AreEqual(ShadowCastingMode.On, renderer.shadowCastingMode);
            var texture = (Texture2D)material.GetTexture("_BaseMap");
            Assert.AreEqual(FilterMode.Point, texture.filterMode);
            Assert.AreEqual(0f, sprite.transform.eulerAngles.x, 0.01f, "billboard must stay upright");
            Assert.AreEqual(1.6f, sprite.transform.lossyScale.x, 0.001f, "48 px at 30 px/unit");
            Assert.AreEqual(1.6f / Mathf.Cos(25f * Mathf.Deg2Rad), sprite.transform.lossyScale.y, 0.001f, "stretched so pixels stay square under the 25° pitch");
            Assert.AreEqual(sprite.transform.lossyScale.y / 2f, sprite.transform.localPosition.y, 0.001f, "feet stay on the ground");
        }

        var volume = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None).Single(v => v.isGlobal);
        Assert.IsTrue(volume.sharedProfile.TryGet(out DepthOfField dof) && dof.mode.value == DepthOfFieldMode.Bokeh);
        var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        Assert.IsTrue(lights.Any(l => l.type == LightType.Directional && l.shadows != LightShadows.None));
        Assert.GreaterOrEqual(lights.Count(l => l.type == LightType.Point), 9, "more lanterns than Forward's per-object limit");

        var empty = UnityEngine.SceneManagement.SceneManager.CreateScene("HD2DCleanup");
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(empty);
        yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync("HD2D_Sample");
    }

    [UnityTest] public IEnumerator BillboardOnlyFollowsCameraYaw()
    {
        var camGo = new GameObject("Test camera");
        var cam = camGo.AddComponent<Camera>();
        camGo.transform.rotation = Quaternion.Euler(30f, 40f, 0f);
        var quad = new GameObject("Test sprite");
        var billboard = quad.AddComponent<PixelBillboard>();
        billboard.Target = cam;
        try
        {
            yield return null;
            Vector3 e = quad.transform.eulerAngles;
            Assert.AreEqual(0f, e.x, 0.01f);
            Assert.AreEqual(40f, e.y, 0.01f);
            Assert.AreEqual(0f, e.z, 0.01f);
        }
        finally { Object.Destroy(quad); Object.Destroy(camGo); }
    }
}
