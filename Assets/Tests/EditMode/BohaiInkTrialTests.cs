using System.IO;
using System.Linq;
using Jrpg;
using NUnit.Framework;
using ThreeKingdoms.EditorTools;
using ThreeKingdoms.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ThreeKingdoms.Tests.EditMode
{
    // Ink-wash trial (production/levels/bohai-plain/ink-trial.md): the trial scene can flip between the original and the
    // ink look, the painted textures are drop-in replacements, and the probe's own scene and renderer are left as they were.
    public sealed class BohaiInkTrialTests
    {
        [Test]
        public void TrialSceneSwitchesBetweenOriginalAndInk()
        {
            EditorSceneManager.OpenScene(BohaiInkTrialBuilder.ScenePath, OpenSceneMode.Single);
            var s = Object.FindFirstObjectByType<InkTrialSwitcher>();
            Assert.IsNotNull(s, "switcher");
            Assert.AreEqual(2, s.Looks.Length);
            Assert.AreEqual(0, s.Looks[0].Renderer, "the original look keeps the default renderer");
            Assert.Greater(s.Looks[1].Renderer, 0, "the ink look draws through the ink-wash renderer");
            Assert.Greater(s.Renderers.Length, 100, "ink-swapped renderers");
            Assert.AreEqual(s.Renderers.Length * 2, s.Materials.Length);
            Assert.IsTrue(s.Materials.All(m => m), "every look has a material for every renderer");
            Assert.IsTrue(s.Looks.All(l => l.High && l.Low), "every look has both quality profiles");
            var p = Object.FindFirstObjectByType<PlayerMover>().transform.position;
            Assert.That(p.x, Is.InRange(BohaiInkTrialBuilder.X0, BohaiInkTrialBuilder.X1));
            Assert.That(p.z, Is.InRange(BohaiInkTrialBuilder.Z0, BohaiInkTrialBuilder.Z1));
            Assert.IsTrue(EditorBuildSettings.scenes.Any(x => x.path == BohaiInkTrialBuilder.ScenePath && x.enabled));
        }

        [Test]
        public void InkTexturesMatchTheOriginalsTheyReplace()
        {
            foreach (string v in new[] { "Painted" })
                foreach (string path in Directory.GetFiles($"{BohaiInkTrialBuilder.InkDir}/{v}", "*.png"))
                {
                    var ink = AssetDatabase.LoadAssetAtPath<Texture2D>(path.Replace('\\', '/'));
                    string key = Path.GetFileNameWithoutExtension(path);
                    var original = AssetDatabase.FindAssets(key + " t:Texture2D", new[] { "Assets/_Project/Art/Environment" })
                        .Select(AssetDatabase.GUIDToAssetPath)
                        .Where(x => Path.GetFileNameWithoutExtension(x) == key && !x.Contains("/Ink/"))
                        .Select(AssetDatabase.LoadAssetAtPath<Texture2D>).FirstOrDefault();
                    Assert.IsNotNull(original, $"{v}/{key} replaces nothing");
                    Assert.AreEqual(new Vector2Int(original.width * 4, original.height * 4), new Vector2Int(ink.width, ink.height), $"{v}/{key} is 4x its original");
                }
        }

        [Test]
        public void ProbeSceneKeepsItsMaterialsAndRenderer()
        {
            EditorSceneManager.OpenScene(BohaiPlainBuilder.ScenePath, OpenSceneMode.Single);
            Assert.IsFalse(Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .SelectMany(r => r.sharedMaterials).Any(m => m && m.name.Contains("_ink")), "ink material in Bohai_Plain");
            Assert.IsNull(Object.FindFirstObjectByType<InkTrialSwitcher>());
            var defaultRenderer = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>("Assets/_Project/Settings/HD2D_Renderer.asset");
            Assert.IsEmpty(defaultRenderer.rendererFeatures, "the default renderer has no ink pass");
        }
    }
}
