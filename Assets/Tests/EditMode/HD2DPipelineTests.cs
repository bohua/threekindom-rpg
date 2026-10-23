using System.Linq;
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

    [Test] public void ProjectUsesLinearColourSpaceIncludingReleaseProfile()
    {
        Assert.AreEqual(ColorSpace.Linear, PlayerSettings.colorSpace);
        string release = System.IO.File.ReadAllText("Assets/_Project/Settings/BuildProfiles/Win64-Release.asset");
        StringAssert.Contains("m_ActiveColorSpace: 1", release, "the release build profile carries its own player-settings copy");
    }

    [Test] public void SharedLutPassesUrpValidationInEveryProfile()
    {
        var lut = AssetDatabase.LoadAssetAtPath<Texture2D>(HD2DLutBuilder.LutPath);
        Assert.IsNotNull(lut);
        Assert.AreEqual(1024, lut.width);
        Assert.AreEqual(32, lut.height);
        Assert.IsFalse(((TextureImporter)AssetImporter.GetAtPath(HD2DLutBuilder.LutPath)).sRGBTexture, "LUTs must be imported as linear data");
        foreach (string path in new[] { HD2DSampleBuilder.HighProfilePath, HD2DSampleBuilder.LowProfilePath })
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            Assert.IsTrue(profile.TryGet(out ColorLookup lookup), path);
            Assert.AreSame(lut, lookup.texture.value, path);
            Assert.AreEqual(1f, lookup.contribution.value, path);
            Assert.IsTrue(lookup.ValidateLUT(), path + ": URP silently disables an invalid LUT");
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
            Assert.AreEqual((WalkCycleImporter.PoseFrames + WalkCycleImporter.WalkFrames) * HD2DSampleBuilder.FrameSize, texture.width, key + ": 9 poses + 16 walk frames");
            Assert.AreEqual(HD2DSampleBuilder.FrameSize, texture.height, key);
        }
    }

    [Test] public void EnvironmentSurfacesMatchCharacterTexelDensity()
    {
        // A 3-unit-wide wall face must span 3 × 30 = 90 texture pixels, the same density as the 48 px / 1.6 unit characters.
        var plaster = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/Materials/Plaster.mat");
        Assert.IsNotNull(plaster);
        var texture = (Texture2D)plaster.GetTexture("_BaseMap");
        Assert.AreEqual(FilterMode.Point, texture.filterMode);
        Vector2 tiling = plaster.GetTextureScale("_BaseMap");

        var mesh = AssetDatabase.LoadAllAssetsAtPath(HD2DEnvironmentKit.MeshAssetPath).OfType<Mesh>()
            .First(m => m.name.StartsWith("Box_") && !m.name.EndsWith("_fit"));
        Vector3 size = mesh.bounds.size;
        float uSpan = 0f;
        var uvs = mesh.uv; var normals = mesh.normals;
        for (int i = 0; i < uvs.Length; i++) if (normals[i] == Vector3.back) uSpan = Mathf.Max(uSpan, uvs[i].x);
        Assert.AreEqual(size.x * HD2DSampleBuilder.PixelsPerUnit, uSpan * tiling.x * texture.width, 0.01f);
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
