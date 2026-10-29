using System.IO;
using ThreeKingdoms.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThreeKingdoms.EditorTools
{
    // The prologue's world map as a 3D 青绿山水 diorama (user 2026-10-28: "大地图这个山水画，它能否做成3D的建模和贴图……
    // 把镜头压低，从当中穿过"). Codex designed it (art/prologue-3d-map-concept) and built it in Blender
    // (art/prologue-3d-map-blender: terrain, water, peaks, cities, trees, mist; 120 × 67.5 m, origin at the south-west
    // corner, 1 m = 16 pixels of the flat map). This makes Resources/Prologue/TianxiaDiorama.prefab: the six models, a
    // sun and a camera for the prologue view, and overlay copies of the terrain whose UV is the flat map's, so the
    // story layers (the spreading rising, the cinnabar ring, the arrows, the glow, the notices) lie on the ground.
    public static class PrologueDioramaBuilder
    {
        // The refined second pass (art/prologue-3d-map-blender-v2) once delivered, else the first.
        public static readonly string Set = Directory.Exists(@"D:\BuildArtifacts\Codex\hd2d-jrpg\art\prologue-3d-map-blender-v2")
            ? "prologue-3d-map-blender-v2" : "prologue-3d-map-blender";
        const string PrefabPath = "Assets/_Project/Resources/Prologue/TianxiaDiorama.prefab";
        public const int Layer = 30;   // the diorama renders to its own camera only
        static readonly string[] Models = { "tianxia_terrain", "tianxia_water", "tianxia_peaks", "tianxia_cities", "tianxia_trees", "tianxia_mist" };
        static readonly (string model, string set)[] Refinements = { ("tianxia_trees", "prologue-3d-map-trees-v3"), ("tianxia_cities", "prologue-3d-map-cities-v3"), ("tianxia_peaks", "prologue-3d-map-peaks-v3") };
        // Overlay order, bottom to top; PrologueDiorama finds them by these names.
        public static readonly string[] Overlays = { "map_spread", "spread", "map_julu", "map_recruit", "arrows", "ring" };

        [MenuItem("JRPG/Build prologue diorama (3D world map)")]
        public static void Build()
        {
            if (!BlenderModels.Import(Set, 8192)) { Debug.LogError("Prologue diorama: art/" + Set + " not delivered."); return; }
            // Finer passes of single models (user 2026-10-29: "这个大地图在游戏中会一直使用和出现，务必做得最精良") replace
            // the base set's model of the same name once delivered.
            var sources = new System.Collections.Generic.Dictionary<string, string>();
            foreach (var (model, set) in Refinements)
                if (BlenderModels.Import(set, 4096)) sources[model] = set;
            var root = new GameObject("TianxiaDiorama");
            try
            {
                Transform terrain = null;
                foreach (var model in Models)
                {
                    var t = BlenderModels.Place(sources.TryGetValue(model, out var refined) ? refined : Set, root.transform, model, model, Vector3.zero);
                    if (!t) { Debug.LogWarning("Prologue diorama: missing " + model); continue; }
                    foreach (var o in t.GetComponentsInChildren<Occluder>(true)) Object.DestroyImmediate(o);
                    foreach (var r in t.GetComponentsInChildren<MeshRenderer>(true))
                        if (model == "tianxia_mist") r.shadowCastingMode = ShadowCastingMode.Off;
                    if (model == "tianxia_terrain") terrain = t;
                }
                // Check the axes against the anchors: Luo Yang stands at (51.1, 43.1) m on the map.
                var luoyang = FindDeep(root.transform, "luoyang");
                if (luoyang && luoyang.GetComponent<Renderer>())
                    Debug.Log($"Prologue diorama: luoyang at {luoyang.GetComponent<Renderer>().bounds.center} (expected x 51.1, z 43.1)");

                if (terrain) AddOverlays(root.transform, terrain);
                WriteRoutes();
                WriteHeights(root.transform);

                var sun = new GameObject("Diorama Sun").AddComponent<Light>();
                sun.transform.SetParent(root.transform, false);
                sun.type = LightType.Directional;
                sun.color = new Color(1f, 0.98f, 0.94f);
                sun.intensity = 1.25f;
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(52f, 150f, 0f);   // from the south-east, high
                sun.cullingMask = 1 << Layer;

                var cam = new GameObject("Diorama Camera").AddComponent<Camera>();
                cam.transform.SetParent(root.transform, false);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.94f, 0.93f, 0.89f);   // the paper the edges fade into
                cam.cullingMask = 1 << Layer;
                cam.nearClipPlane = 0.08f; cam.farClipPlane = 400f;
                cam.fieldOfView = 45f;
                cam.enabled = false;

                SetLayer(root.transform, Layer);
                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("Prologue diorama: saved " + PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
        }

        public static void BuildFromCommandLine()
        {
            Build();
            EditorApplication.Exit(0);
        }

        // The camera routes of anchors.json (Blender metres, Z up) as Resources/Prologue/tianxia_routes.json in Unity's
        // axes (x east, y up, z north) for PrologueDiorama.
        static void WriteRoutes()
        {
            string src = @"D:\BuildArtifacts\Codex\hd2d-jrpg\art\" + Set + @"\_checks\anchors.json";
            if (!File.Exists(src)) return;
            string json = File.ReadAllText(src);
            var outRoutes = new System.Collections.Generic.List<string>();
            foreach (var id in new[] { "A", "B", "C" })
            {
                int at = json.IndexOf("\"" + id + "\": {", json.IndexOf("\"routes\""));
                if (at < 0) continue;
                // "points" holds nested arrays: take the block up to its own closing bracket.
                int depth = 0, open = json.IndexOf('[', json.IndexOf("\"points\"", at)), end;
                for (end = open; end < json.Length; end++) { if (json[end] == '[') depth++; else if (json[end] == ']' && --depth == 0) break; }
                string block = json.Substring(open, end - open);
                var fovMatch = System.Text.RegularExpressions.Regex.Match(json.Substring(at, open - at), @"""fov_vertical_deg""\s*:\s*([0-9.]+)");
                var pos = Vectors(block, "position_m");
                var look = Vectors(block, "look_at_m");
                outRoutes.Add($"{{\"id\":\"{id}\",\"fov\":{(fovMatch.Success ? fovMatch.Groups[1].Value : "50")},\"positions\":[{string.Join(",", pos)}],\"looks\":[{string.Join(",", look)}]}}");
            }
            File.WriteAllText("Assets/_Project/Resources/Prologue/tianxia_routes.json", "{\"routes\":[" + string.Join(",", outRoutes) + "]}");
            AssetDatabase.ImportAsset("Assets/_Project/Resources/Prologue/tianxia_routes.json");
        }

        // The top of the land and the peaks on a half-metre grid (Resources/Prologue/tianxia_height.bytes: int width,
        // int depth, then floats row by row from the south-west), so flights over the map can clear the mountains
        // without colliders on the shipped meshes (PrologueDiorama.GroundAt).
        public const float HeightStep = 0.5f;
        static void WriteHeights(Transform root)
        {
            var added = new System.Collections.Generic.List<MeshCollider>();
            foreach (var f in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var t = f.transform;
                while (t && t != root && t.name != "tianxia_terrain" && t.name != "tianxia_peaks") t = t.parent;
                if (t && t != root && f.sharedMesh) added.Add(f.gameObject.AddComponent<MeshCollider>());
            }
            Physics.SyncTransforms();
            int w = Mathf.CeilToInt(120f / HeightStep) + 1, d = Mathf.CeilToInt(67.5f / HeightStep) + 1;
            using (var bw = new BinaryWriter(File.Create("Assets/_Project/Resources/Prologue/tianxia_height.bytes")))
            {
                bw.Write(w); bw.Write(d);
                for (int z = 0; z < d; z++)
                    for (int x = 0; x < w; x++)
                    {
                        float h = 0f;
                        foreach (var hit in Physics.RaycastAll(new Vector3(x * HeightStep, 80f, z * HeightStep), Vector3.down, 160f))
                            if (added.Contains(hit.collider as MeshCollider)) h = Mathf.Max(h, hit.point.y);
                        bw.Write(h);
                    }
            }
            foreach (var c in added) Object.DestroyImmediate(c);
            AssetDatabase.ImportAsset("Assets/_Project/Resources/Prologue/tianxia_height.bytes");
            Debug.Log($"Prologue diorama: heights {w}×{d}");
        }

        static System.Collections.Generic.List<string> Vectors(string block, string key)
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(block, "\"" + key + @"""\s*:\s*\[\s*(-?[0-9.eE+-]+)\s*,\s*(-?[0-9.eE+-]+)\s*,\s*(-?[0-9.eE+-]+)"))
                list.Add($"{{\"x\":{m.Groups[1].Value},\"y\":{m.Groups[3].Value},\"z\":{m.Groups[2].Value}}}");
            return list;
        }

        // Copies of the terrain mesh lifted a little, each with a transparent unlit material whose texture the prologue
        // sets at run time (UV 0..1 = the 1920×1080 flat map).
        static void AddOverlays(Transform root, Transform terrain)
        {
            var source = terrain.GetComponentInChildren<MeshFilter>();
            if (!source) return;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            string dir = BlenderModels.Root + "/" + Set + "/Materials";
            for (int i = 0; i < Overlays.Length; i++)
            {
                var go = new GameObject("Overlay " + Overlays[i]);
                go.transform.SetParent(root, false);
                go.transform.position = source.transform.position + Vector3.up * (0.02f + 0.012f * i);
                go.transform.rotation = source.transform.rotation;
                go.transform.localScale = source.transform.lossyScale;
                go.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                var r = go.AddComponent<MeshRenderer>();
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                var m = new Material(shader) { name = "overlay_" + Overlays[i] };
                BlenderModels.Transparent(m);
                m.renderQueue = (int)RenderQueue.Transparent + 10 + i;
                m.SetColor("_BaseColor", Color.white);
                string path = $"{dir}/overlay_{Overlays[i]}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(path)) AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(m, path);
                r.sharedMaterial = m;
                go.SetActive(false);
            }
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var f = FindDeep(c, name); if (f) return f; }
            return null;
        }

        static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform c in t) SetLayer(c, layer);
        }
    }
}
