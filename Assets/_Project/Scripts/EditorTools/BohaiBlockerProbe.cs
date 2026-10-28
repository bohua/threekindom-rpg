using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ThreeKingdoms.EditorTools
{
    // Diagnostic: walks the Bohai plain on a grid and lists which colliders stand on open plain (user 2026-10-25:
    // invisible walls in the open country). Prints the top collider groups with sample positions.
    public static class BohaiBlockerProbe
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene(BohaiPlainBuilder.ScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();
            var hits = new Dictionary<string, (int count, Vector3 sample)>();
            var tex = new Texture2D(2, 2); tex.LoadImage(System.IO.File.ReadAllBytes(BohaiPlainBuilder.TerrainDir + "/bohai_terrain_mask.png"));
            var px = tex.GetPixels32(); int w = tex.width;
            bool Open(float x, float z)
            {
                int ix = Mathf.Clamp((int)(x + 560f), 0, w - 1), iz = Mathf.Clamp((int)(z + 330f), 0, tex.height - 1);
                var c = px[iz * w + ix];
                return (Mathf.Abs(c.r - 0x7F) + Mathf.Abs(c.g - 0xA0) + Mathf.Abs(c.b - 0x5A) < 24) || (Mathf.Abs(c.r - 0xC8) + Mathf.Abs(c.g - 0xA0) + Mathf.Abs(c.b - 0x60) < 24);
            }
            for (float z = -205f; z < 335f; z += 3f)
                for (float x = -555f; x < 555f; x += 3f)
                {
                    var cols = Physics.OverlapBox(new Vector3(x, 1f, z), new Vector3(0.3f, 0.6f, 0.3f));
                    if (!Open(x, z)) continue;   // only open plain or road cells
                    foreach (var c in cols)
                    {
                        string path = Path(c.transform);
                        hits[path] = hits.TryGetValue(path, out var h) ? (h.count + 1, h.sample) : (1, new Vector3(x, 0, z));
                    }
                }
            foreach (var kv in hits.OrderByDescending(k => k.Value.count).Take(40))
                Debug.Log($"BLOCKER {kv.Value.count,6}  {kv.Key}  e.g. ({kv.Value.sample.x:0}, {kv.Value.sample.z:0})");
            Debug.Log("BLOCKER_PROBE_DONE");
            EditorApplication.Exit(0);
        }

        static string Path(Transform t)
        {
            var parts = new List<string>();
            for (int i = 0; t && i < 4; i++, t = t.parent) parts.Insert(0, t.name);
            return string.Join("/", parts);
        }
    }
}
