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
    // Blender models (user 2026-10-26 pilot; 2026-10-27 "补齐涿郡的3D建模"): Codex draws a design image, models the piece
    // in Blender and exports an FBX (1 unit = 1 m, front toward Blender -Y, one material per texture key, materials.json
    // beside it). The town builder places the models instead of assembling buildings and props from quads, with the same
    // HD textures and materials. Pipeline notes: BLENDER_PIPELINE.md in the shared docs; plan:
    // production/levels/zhuojun/blender-rebuild.md. -noBlender (or Enabled = false) falls back to the quad-built town.
    internal static class BlenderModels
    {
        const string ArtRoot = @"D:\BuildArtifacts\Codex\hd2d-jrpg\art";
        // Delivered sets, later ones winning where a model name repeats.
        static readonly string[] Sets = { "zhuojun-blender-pilot", "zhuojun-blender-b1", "zhuojun-blender-b2", "zhuojun-blender-b3", "zhuojun-blender-b4" };
        public static bool Enabled = !System.Environment.GetCommandLineArgs().Contains("-noBlender");
        // The material Codex leaves on terrain for Unity to replace with the town's blended ground.
        public const string GroundKey = "zhuojun_ground_splat";
        public static Material Ground;

        sealed class Entry { public bool Clip, TwoSided; public string Emission; }
        static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        static readonly Dictionary<string, string> models = new Dictionary<string, string>();   // model -> asset path

        static string Dir(string set) => $"{ZhuojunTownBuilder.HdRoot}/{set}";

        // Copies every delivered set (FBX files, materials.json, new textures) into the project; returns the folders to
        // search for textures, empty when nothing has been delivered.
        internal static string[] Import()
        {
            entries.Clear();
            models.Clear();
            var dirs = new List<string>();
            foreach (string set in Sets)
            {
                string from = Path.Combine(ArtRoot, set), to = Dir(set);
                if (!Directory.Exists(from) || !Directory.GetFiles(from, "*.fbx").Any()) continue;
                Directory.CreateDirectory(to);
                foreach (string f in Directory.GetFiles(from, "*.fbx")) File.Copy(f, Path.Combine(to, Path.GetFileName(f)), true);
                string json = Path.Combine(from, "materials.json");
                if (File.Exists(json)) File.Copy(json, Path.Combine(to, "materials.json"), true);
                if (Directory.Exists(Path.Combine(from, "textures")))
                    foreach (string f in Directory.GetFiles(Path.Combine(from, "textures"), "*.png"))
                    {
                        // A set may carry its own copy of a town texture (roof_eave, roof_ridge…): the town's own wins,
                        // or the copy, searched first, would re-skin every building.
                        if (TownTexture(Path.GetFileNameWithoutExtension(f))) continue;
                        File.Copy(f, Path.Combine(to, Path.GetFileName(f)), true);
                    }
                dirs.Insert(0, to);
            }
            AssetDatabase.Refresh();
            foreach (string set in Sets)
            {
                string to = Dir(set);
                if (!Directory.Exists(to)) continue;
                ReadMaterials(Path.Combine(to, "materials.json"));
                foreach (string path in Directory.GetFiles(to, "*.fbx"))
                {
                    string p = path.Replace('\\', '/');
                    Configure(p);
                    models[Path.GetFileNameWithoutExtension(p)] = p;
                }
            }
            return dirs.ToArray();
        }

        static readonly string[] TownArt = { "zhuojun-hd-ground", "zhuojun-hd-buildings", "zhuojun-hd-props", "zhuojun-hd-greenery" };
        static bool TownTexture(string key) => TownArt.Any(d => File.Exists(Path.Combine(ArtRoot, d, key + ".png")))
            || File.Exists($"{ZhuojunTownBuilder.TownDir}/{key}.png") || File.Exists($"{ZhuojunTownBuilder.MapDir}/{key}.png");

        public static bool Has(string model) => Enabled && models.ContainsKey(model);

        // materials.json: {"house_front": {"texture": "...", "alpha_clip": true, "tiling": false, "two_sided": false, "emission_mask": "..."}, ...}
        static void ReadMaterials(string path)
        {
            if (!File.Exists(path)) return;
            foreach (Match m in Regex.Matches(File.ReadAllText(path), "\"([^\"]+)\"\\s*:\\s*\\{([^}]*)\\}"))
            {
                string body = m.Groups[2].Value;
                var em = Regex.Match(body, "\"emission_mask\"\\s*:\\s*\"([^\"]+)\"");
                entries[m.Groups[1].Value] = new Entry
                {
                    Clip = Regex.IsMatch(body, "\"alpha_clip\"\\s*:\\s*true"),
                    TwoSided = Regex.IsMatch(body, "\"two_sided\"\\s*:\\s*true"),
                    Emission = em.Success ? Path.GetFileNameWithoutExtension(em.Groups[1].Value) : null,
                };
            }
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
            mi.isReadable = true;   // terrain needs it for its mesh collider
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            if (EditorUtility.IsDirty(mi)) mi.SaveAndReimport();
        }

        // Places a delivered model with the town's materials; null when it is missing or switched off, so the caller
        // builds the quad version instead. COL_ children become box colliders; without them the "Walls" (or
        // "GateBlock") bounds do. terrain: mesh colliders on everything, no occluder (the camera never cuts the ground).
        internal static Transform Place(Transform parent, string model, string name, Vector3 at, float yaw = 0f, bool terrain = false)
        {
            if (!Has(model)) return null;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(models[model]);
            if (!prefab) return null;
            var go = Object.Instantiate(prefab, parent, false);
            go.name = name;
            go.transform.localPosition = at;
            // Blender's front (-Y), exported -Z forward, arrives facing +Z (north) in Unity: turn it back to the camera.
            // After the turn Blender X is Unity x and Blender Y is Unity z, so world-placed sets (terrain) line up.
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
                if (terrain)
                {
                    r.gameObject.AddComponent<MeshCollider>().sharedMesh = r.GetComponent<MeshFilter>().sharedMesh;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
            if (terrain) return root;
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
            if (key == GroundKey && Ground) return Ground;
            entries.TryGetValue(key, out var e);
            if (!ZhuojunTownBuilder.Tex(key, optional: true)) return imported;
            return ZhuojunTownBuilder.Mat(key, clip: e?.Clip ?? false, twoSided: e?.TwoSided ?? false,
                emissionMask: e?.Emission != null && ZhuojunTownBuilder.Tex(e.Emission, optional: true) ? e.Emission : null);
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
