using System.Collections;
using System.Linq;
using NUnit.Framework;
using ThreeKingdoms.Gameplay;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// ADR-0005 §8 battle backdrop validation criteria that can be checked automatically.
public sealed class BattleBackdropTests
{
    [UnityTest] public IEnumerator BattleSceneUsesLayeredBackdropAndOrthographicCamera()
    {
        yield return SceneManager.LoadSceneAsync("Battle_ZhuojunMarket");
        yield return null;
        var cam = Camera.main;
        Assert.IsTrue(cam.orthographic);
        Assert.AreEqual(6f, cam.orthographicSize, 0.001f, "360 px / 2 / 30 px per unit");

        var stage = Object.FindFirstObjectByType<BattleStage>();
        Assert.IsNotNull(stage);
        Assert.GreaterOrEqual(stage.Backdrop.Layers.Length, 3);
        foreach (var layer in stage.Backdrop.Layers)
        {
            Assert.AreEqual(BattleBackdrop.Width, layer.width);
            Assert.AreEqual(BattleBackdrop.Height, layer.height);
            Assert.AreEqual(FilterMode.Point, layer.filterMode);
        }
        Assert.AreEqual(0f, Quaternion.Angle(stage.Backdrop.LightRotation, stage.KeyLight.transform.rotation), 0.01f, "key light comes from the backdrop metadata");

        var volume = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None).Single(v => v.isGlobal);
        Assert.IsFalse(volume.sharedProfile.TryGet(out DepthOfField dof) && dof.active && dof.mode.value != DepthOfFieldMode.Off, "no depth of field with an orthographic camera");
        Assert.IsTrue(volume.sharedProfile.TryGet(out Bloom bloom) && bloom.threshold.value > 1f, "painted highlights must not bloom");

        var units = Object.FindObjectsByType<SpriteFrameAnimator>(FindObjectsSortMode.None);
        Assert.AreEqual(7, units.Length, "3 party members against the largest V1 enemy line-up of 4");
        var bounds = new Rect(-21.333f / 2, -6f, 21.333f, 12f);
        foreach (var unit in units) Assert.IsTrue(bounds.Contains(unit.transform.position), unit.name + " stays on screen");
    }

    [UnityTest] public IEnumerator TownAndBattleShareTheSameLut()
    {
        yield return SceneManager.LoadSceneAsync("HD2D_Sample");
        yield return null;
        var townLut = Lut();
        yield return SceneManager.LoadSceneAsync("Battle_ZhuojunMarket");
        yield return null;
        var battleLut = Lut();
        Assert.IsNotNull(townLut);
        Assert.AreSame(townLut, battleLut);
    }

    static Texture Lut()
    {
        var volume = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None).Single(v => v.isGlobal);
        return volume.sharedProfile.TryGet(out ColorLookup lookup) ? lookup.texture.value : null;
    }
}
