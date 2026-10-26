using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ThreeKingdoms.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThreeKingdoms.EditorTools
{
    // Blender pilot (user 2026-10-26): Codex draws a design image, then models the building in Blender and exports an
    // FBX (1 unit = 1 m, origin at the bottom centre, front toward -Z, one material per texture key). The town builder
    // places the model instead of assembling the building from quads, with the same HD textures and materials.
    // Pipeline notes: BLENDER_PIPELINE.md in the shared docs; Codex delivery art/zhuojun-blender-pilot.
    // -noBlender (or Enabled = false) falls back to the quad-built buildings, for side-by-side screenshots.
    internal static class BlenderModels
    {
        const string Source = @"D:\BuildArtifacts\Codex\hd2d-jrpg\art\zhuojun-blender-pilot";
        public const string Dir = ZhuojunTownBuilder.HdRoot + "/zhuojun-blender-pilot";
        public static bool Enabled = !System.Environment.GetCommandLineArgs().Contains("-noBlender");

        sealed class Entry { public bool Clip, TwoSided; }
        static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();

        // Copies the delivered FBX files, materials.json and any new textures into the project; returns the folder to
        // search for textures (new ones live there), or null when nothing has been delivered.
        internal static string Import()
        {
            entries.Clear();
            if (!Directory.Exists(Source)) return null;
            Directory.CreateDirectory(Dir);
            foreach (string f in Directory.GetFiles(Source, "*.fbx")) File.Copy(f, Path.Combine(Dir, Path.GetFileName(f)), true);
            string json = Path.Combine(Source, "materials.json");
            if (File.Exists(json)) File.Copy(json, Path.Combine(Dir, "materials.json"), true);
            if (Directory.Exists(Path.Combine(Source, "textures")))
                foreach (string f in Directory.GetFiles(Path.Combine(Source, "textures"), "*.png")) File.Copy(f, Path.Combine(Dir, Path.GetFileName(f)), true);
            AssetDatabase.Refresh();
            ReadMaterials();
            foreach (string path in Directory.GetFiles(Dir, "*.fbx")) Configure(path.Replace('\\', '/'));
            return Dir;
        }

        // materials.json: {"house_front": {"texture": "...", "alpha_clip": true, "tiling": false, "two_sided": false}, ...}
        static void ReadMaterials()
        {
            string path = Path.Combine(Dir, "materials.json");
            if (!File.Exists(path)) return;
            foreach (Match m in Regex.Matches(File.ReadAllText(path), "\"([^\"]+)\"\\s*:\\s*\\{([^}]*)\\}"))
                entries[m.Groups[1].Value] = new Entry
                {
                    Clip = Regex.IsMatch(m.Groups[2].Value, "\"alpha_clip\"\\s*:\\s*true"),
                    TwoSided = Regex.IsMatch(m.Groups[2].Value, "\"two_sided\"\\s*:\\s*true"),
                };
        }

        static void Configure(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter mi)) return;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.importAnimation = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importBlendShapes = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.importNormals = ModelImporterNormals.Import;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.addCollider = false;
            mi.isReadable = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            if (EditorUtility.IsDirty(mi)) mi.SaveAndReimport();
        }

        // Places a delivered model with the town's materials; null when it is missing or the pilot is switched off,
        // so the caller builds the quad version instead. COL_ children become box colliders; without them the "Walls"
        // (or "GateBlock") bounds do.
        internal static Transform Place(Transform parent, string model, string name, Vector3 at, float yaw = 0f)
        {
            if (!Enabled) return null;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Dir}/{model}.fbx");
            if (!prefab) return null;
            var go = Object.Instantiate(prefab, parent, false);
            go.name = name;
            go.transform.localPosition = at;
            // Blender's front (-Y), exported -Z forward, arrives facing +Z (north) in Unity: turn it back to the camera.
            go.transform.localRotation = Quaternion.Euler(0, yaw + 180f, 0) * go.transform.localRotation;
            var root = go.transform;
            bool colliders = false;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.name.StartsWith("COL_"))
                {
                    var b = LocalBounds(root, r.GetComponent<MeshFilter>());
                    ZhuojunTownBuilder.Solid(root, b.center, b.size);
                    Object.DestroyImmediate(r.GetComponent<MeshFilter>());
                    Object.DestroyImmediate(r);
                    colliders = true;
                    continue;
                }
                r.sharedMaterials = r.sharedMaterials.Select(Swap).ToArray();
                r.shadowCastingMode = ShadowCastingMode.On;
            }
            if (!colliders)
            {
                var body = root.GetComponentsInChildren<MeshFilter>().FirstOrDefault(f => f.name == "Walls" || f.name == "GateBlock");
                if (body) { var b = LocalBounds(root, body); ZhuojunTownBuilder.Solid(root, b.center, b.size); }
            }
            go.AddComponent<Occluder>();
            return root;
        }

        static Material Swap(Material imported)
        {
            if (!imported) return null;
            string key = Regex.Replace(imported.name, "\\.\\d+$", "");
            entries.TryGetValue(key, out var e);
            return ZhuojunTownBuilder.Mat(key, clip: e?.Clip ?? false, twoSided: e?.TwoSided ?? false);
        }

        static Bounds LocalBounds(Transform root, MeshFilter f)
        {
            var mb = f.sharedMesh.bounds;
            var b = new Bounds(root.InverseTransformPoint(f.transform.TransformPoint(mb.center)), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                b.Encapsulate(root.InverseTransformPoint(f.transform.TransformPoint(corner)));
            }
            return b;
        }
    }
}
