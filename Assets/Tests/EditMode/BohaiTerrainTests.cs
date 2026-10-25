using System.IO;
using NUnit.Framework;
using ThreeKingdoms.EditorTools;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ThreeKingdoms.Tests.EditMode
{
    // User 2026-10-25: invisible walls on open grass. Woods and hills may only block where they visibly stand, so no
    // terrain wall may sit on a cell the terrain plan marks as open plain or road, nor in a wood's 2 m edge band.
    public sealed class BohaiTerrainTests
    {
        [Test]
        public void NoTerrainWallStandsOnOpenGround()
        {
            EditorSceneManager.OpenScene(BohaiPlainBuilder.ScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(BohaiPlainBuilder.TerrainDir + "/bohai_terrain_mask.png"));
            var px = tex.GetPixels32();
            int w = tex.width, bad = 0;
            string first = null;
            bool Is(Color32 c, int r, int g, int b) => Mathf.Abs(c.r - r) + Mathf.Abs(c.g - g) + Mathf.Abs(c.b - b) < 24;
            for (int z = 0; z < tex.height; z += 3)
                for (int x = 0; x < w; x += 3)
                {
                    var c = px[z * w + x];
                    if (!Is(c, 0x7F, 0xA0, 0x5A) && !Is(c, 0xC8, 0xA0, 0x60)) continue;   // open plain or road
                    foreach (var col in Physics.OverlapBox(new Vector3(x - 560f + 0.5f, 1f, z - 330f + 0.5f), new Vector3(0.25f, 0.5f, 0.25f)))
                        if (col.transform.parent && col.transform.parent.name == "Impassable") { bad++; first ??= $"({x - 560}, {z - 330})"; }
                }
            Assert.AreEqual(0, bad, "terrain walls on open ground, first at " + first);
        }
    }
}
