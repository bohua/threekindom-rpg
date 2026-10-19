using NUnit.Framework;
using ThreeKingdoms.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// ADR-0005: rendering settings and the sprite conversion pipeline.
public sealed class HD2DPipelineTests
{
    [Test] public void RendererUsesForwardPlusWithPostProcessing()
    {
        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(HD2DSampleBuilder.RendererPath);
        Assert.IsNotNull(data);
        Assert.AreEqual(RenderingMode.ForwardPlus, data.renderingMode);
        Assert.IsNotNull(data.postProcessData);
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(HD2DSampleBuilder.PipelinePath);
        Assert.IsTrue(pipeline.supportsHDR);
        Assert.AreEqual(1, pipeline.msaaSampleCount);
        Assert.AreSame(pipeline, GraphicsSettings.defaultRenderPipeline);
    }

    [Test] public void LowTierDropsDepthOfFieldHighTierUsesBokeh()
    {
        var high = AssetDatabase.LoadAssetAtPath<VolumeProfile>(HD2DSampleBuilder.HighProfilePath);
        var low = AssetDatabase.LoadAssetAtPath<VolumeProfile>(HD2DSampleBuilder.LowProfilePath);
        Assert.IsTrue(high.TryGet(out DepthOfField highDof) && highDof.active);
        Assert.AreEqual(DepthOfFieldMode.Bokeh, highDof.mode.value);
        Assert.IsTrue(!low.TryGet(out DepthOfField lowDof) || !lowDof.active || lowDof.mode.value == DepthOfFieldMode.Off);
        foreach (var profile in new[] { high, low })
        {
            Assert.IsTrue(profile.Has<Bloom>() && profile.Has<Tonemapping>() && profile.Has<Vignette>() && profile.Has<ColorAdjustments>());
            profile.TryGet(out Tonemapping tonemapping);
            Assert.AreEqual(TonemappingMode.ACES, tonemapping.mode.value);
        }
    }

    [Test] public void CharacterAtlasesArePointFilteredWithoutMipmaps()
    {
        foreach (string key in new[] { "LiuBei", "GuanYu", "ZhangFei", "YellowTurban" })
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(HD2DSampleBuilder.AtlasPath(key));
            Assert.IsNotNull(texture, key);
            Assert.AreEqual(FilterMode.Point, texture.filterMode, key);
            Assert.AreEqual(1, texture.mipmapCount, key);
            Assert.AreEqual(9 * HD2DSampleBuilder.FrameSize, texture.width, key);
            Assert.AreEqual(HD2DSampleBuilder.FrameSize, texture.height, key);
        }
    }

    [Test] public void ConverterProducesBinaryAlphaAndScalesFigureToTargetHeight()
    {
        // 3×3 sheet of 90×90 cells; each cell holds a 30×60 figure with a soft (half-alpha) outer ring.
        const int cell = 90, size = cell * 3;
        var pixels = new Color32[size * size];
        for (int f = 0; f < 9; f++)
        {
            int cx = f % 3 * cell, cy = size - (f / 3 + 1) * cell;
            for (int y = 10; y < 70; y++)
            for (int x = 30; x < 60; x++)
            {
                bool edge = x == 30 || x == 59 || y == 10 || y == 69;
                pixels[(cy + y) * size + cx + x] = edge ? new Color32(200, 40, 40, 100) : new Color32(250, 120, 30, 255);
            }
        }
        var settings = PixelSpriteConverter.Settings.Default;
        settings.FrameSize = 24; settings.FigureHeight = 20;
        Color32[] atlas = PixelSpriteConverter.BuildAtlas(pixels, size, size, settings, out int width, out int height);

        Assert.AreEqual(9 * 24, width);
        Assert.AreEqual(24, height);
        int opaqueRows = 0;
        for (int y = 0; y < height; y++)
        {
            bool any = false;
            for (int x = 0; x < 24; x++)
            {
                byte a = atlas[y * width + x].a;
                Assert.IsTrue(a == 0 || a == 255, "alpha must be binary");
                any |= a == 255;
            }
            if (any) opaqueRows++;
        }
        Assert.That(opaqueRows, Is.InRange(19, 21));
        Assert.AreEqual(0, atlas[0].a, "bottom row is the transparent margin under the feet");
        Color32 body = atlas[10 * width + 12];
        Assert.AreEqual(255, body.a);
        Assert.Greater(body.r, body.b, "block colour comes from the opaque body, quantised");
    }
}
