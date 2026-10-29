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
    // Blender models (user 2026-10-26: "让 Codex 做出一个效果图，然后直接让 Codex 把 Blender 的工作也做好"): Codex draws
    // a design image, models the piece in Blender and exports an FBX (1 unit = 1 m, origin at the bottom centre, front
    // toward Blender -Y, one material per texture key, materials.json beside it). Each delivery is a set under art/<set>;
    // Import copies it, with every texture it uses, into Environment/Blender/<set> and makes its own materials (the same
    // flat URP Lit look as the town builder, painted textures cut out at alpha 0.5). Pipeline notes: BLENDER_PIPELINE.md
    // in the shared docs. -noBlender leaves the quad-built versions in place.
    internal static class BlenderModels
    {
        const string ArtRoot = @"D:\BuildArtifacts\Codex\hd2d-jrpg\art";
        public const string Root = "Assets/_Project/Art/Environment/Blender";
        public static bool Enabled = !System.Environment.GetCommandLineArgs().Contains("-noBlender");

        sealed class Entry { public string Texture; public bool Clip, Blend, TwoSided, Tiling, Triplanar; public float Metres = 12f; }
        static readonly Dictionary<string, Dictionary<string, Entry>> sets = new Dictionary<string, Dictionary<string, Entry>>();
        static readonly Dictionary<string, Material> made = new Dictionary<string, Material>();

        static string Dir(string set) => $"{Root}/{set}";

        // Copies the set's FBX files, materials.json and textures into the project and makes its materials; false when
        // the set has not been delivered. maxTexture: the largest texture size kept (the prologue diorama keeps its 8K
        // ground; town and region sets stay at 2048).
        internal static bool Import(string set, int maxTexture = 2048)
        {
            // -blenderFrom <folder>: a revision still in review (bridge staging) instead of the approved art/<set>.
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-blenderFrom");
            string from = i >= 0 && i + 1 < args.Length ? args[i + 1] : Path.Combine(ArtRoot, set), to = Dir(set);
            if (!Directory.Exists(from) || !File.Exists(Path.Combine(from, "materials.json"))) return false;
            Directory.CreateDirectory(to + "/Textures");
            Directory.CreateDirectory(to + "/Materials");
            foreach (string f in Directory.GetFiles(from, "*.fbx")) File.Copy(f, Path.Combine(to, Path.GetFileName(f)), true);
            var entries = Read(File.ReadAllText(Path.Combine(from, "materials.json")));
            foreach (var e in entries.Values)
            {
                string src = Path.IsPathRooted(e.Texture) ? e.Texture : Path.Combine(from, e.Texture);
                if (File.Exists(src)) File.Copy(src, Path.Combine(to, "Textures", Path.GetFileName(src)), true);
                e.Texture = $"{to}/Textures/{Path.GetFileName(src)}";
            }
            sets[set] = entries;
            AssetDatabase.Refresh();
            foreach (var e in entries.Values) ConfigureTexture(e, maxTexture);
            foreach (string path in Directory.GetFiles(to, "*.fbx")) ConfigureModel(path.Replace('\\', '/'));
            foreach (var kv in entries) MakeMaterial(set, kv.Key, kv.Value);
            AssetDatabase.SaveAssets();
            return true;
        }

        // materials.json: {"zs_limestone_rock": {"texture": "...", "alpha_clip": false, "tiling": true, "two_sided": false}, ...}
        static Dictionary<string, Entry> Read(string json)
        {
            var d = new Dictionary<string, Entry>();
            foreach (Match m in Regex.Matches(json, "\"([^\"]+)\"\\s*:\\s*\\{([^}]*)\\}"))
            {
                string body = m.Groups[2].Value;
                d[m.Groups[1].Value] = new Entry
                {
                    Texture = Regex.Match(body, "\"texture\"\\s*:\\s*\"([^\"]+)\"").Groups[1].Value.Replace("\\\\", "\\"),
                    Clip = Regex.IsMatch(body, "\"alpha_clip\"\\s*:\\s*true"),
                    Blend = Regex.IsMatch(body, "\"alpha_blend\"\\s*:\\s*true"),
                    TwoSided = Regex.IsMatch(body, "\"two_sided\"\\s*:\\s*true"),
                    Tiling = Regex.IsMatch(body, "\"tiling\"\\s*:\\s*true"),
                    // Rock faces Codex projects from the world (the Taihang cliffs): "projection": "world_triplanar".
                    Triplanar = Regex.IsMatch(body, "\"projection\"\\s*:\\s*\"[^\"]*triplanar"),
                };
                var size = Regex.Match(body, "\"texture_scale_m\"\\s*:\\s*([0-9.]+)");
                if (size.Success) d[m.Groups[1].Value].Metres = float.Parse(size.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            }
            return d;
        }

        static void ConfigureTexture(Entry e, int maxTexture)
        {
            if (!(AssetImporter.GetAtPath(e.Texture) is TextureImporter ti)) return;
            ti.textureType = TextureImporterType.Default;
            ti.filterMode = FilterMode.Trilinear;
            ti.mipmapEnabled = true;
            ti.anisoLevel = 4;
            ti.maxTextureSize = maxTexture;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.sRGBTexture = true;
            ti.alphaIsTransparency = true;
            ti.wrapMode = e.Tiling ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            if (EditorUtility.IsDirty(ti)) ti.SaveAndReimport();
        }

        static void ConfigureModel(string path)
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

        // Flat painted look, as ZhuojunTownBuilder.Mat: no smoothness, highlights or reflections; cut-out alpha.
        static Material MakeMaterial(string set, string key, Entry e)
        {
            if (e.Triplanar)
            {
                var tri = new Material(Shader.Find("ThreeKingdoms/WorldTriplanar")) { name = key };
                tri.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(e.Texture));
                tri.SetFloat("_Meters", e.Metres);
                string tp = $"{Dir(set)}/Materials/{key}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(tp)) AssetDatabase.DeleteAsset(tp);
                AssetDatabase.CreateAsset(tri, tp);
                return made[set + "/" + key] = tri;
            }
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = key };
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(e.Texture));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0f);
            m.SetFloat("_SpecularHighlights", 0f); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            if (e.Clip)
            {
                m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON");
                m.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else if (e.Blend) Transparent(m);
            if (e.TwoSided) m.SetFloat("_Cull", (float)CullMode.Off);
            string path = $"{Dir(set)}/Materials/{key}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            made[set + "/" + key] = m;
            return m;
        }

        // Soft alpha (mist, the diorama's fading edges): URP transparent, no depth writes.
        internal static void Transparent(Material m)
        {
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        // Places a model of an imported set; null when it is missing or switched off, so the caller keeps its quad
        // version. COL_ children become box colliders; the whole piece is one occluder.
        // meshColliders: COL_ children keep their shape as mesh colliders (terrain) instead of becoming boxes.
        internal static Transform Place(string set, Transform parent, string model, string name, Vector3 at, float yaw = 0f, bool meshColliders = false)
        {
            if (!Enabled || string.IsNullOrEmpty(set) || !sets.ContainsKey(set)) return null;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Dir(set)}/{model}.fbx");
            if (!prefab) return null;
            var go = Object.Instantiate(prefab, parent, false);
            go.name = name;
            go.transform.localPosition = at;
            // Blender's front (-Y), exported -Z forward, arrives facing +Z (north) in Unity: turn it back to the camera.
            go.transform.localRotation = Quaternion.Euler(0, yaw + 180f, 0) * go.transform.localRotation;
            var root = go.transform;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.name.StartsWith("COL_") && meshColliders)
                {
                    r.gameObject.AddComponent<MeshCollider>().sharedMesh = r.GetComponent<MeshFilter>().sharedMesh;
                    Object.DestroyImmediate(r.GetComponent<MeshFilter>());
                    Object.DestroyImmediate(r);
                    continue;
                }
                if (r.name.StartsWith("COL_"))
                {
                    var b = LocalBounds(root, r.GetComponent<MeshFilter>());
                    ZhuojunTownBuilder.Solid(root, b.center, b.size);
                    Object.DestroyImmediate(r.GetComponent<MeshFilter>());
                    Object.DestroyImmediate(r);
                    continue;
                }
                r.sharedMaterials = r.sharedMaterials.Select(m => Swap(set, m)).ToArray();
                r.shadowCastingMode = ShadowCastingMode.On;
            }
            go.AddComponent<Occluder>();
            return root;
        }

        // Darkens or tints one material of a set (painted textures that read too bright under the midday sun).
        internal static void Tint(string set, string key, Color c)
        {
            if (made.TryGetValue(set + "/" + key, out var m) && m) { m.SetColor("_BaseColor", c); EditorUtility.SetDirty(m); }
        }

        static Material Swap(string set, Material imported)
        {
            if (!imported) return null;
            string key = Regex.Replace(imported.name, "\\.\\d+$", "");
            if (made.TryGetValue(set + "/" + key, out var m)) return m;
            // Some sets name the Blender material with a region prefix ("R6_granite") and the table without it.
            string bare = Regex.Replace(key, "^R\\d+[A-Za-z]?_", "");
            return made.TryGetValue(set + "/" + bare, out m) ? m : imported;
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

        // World bounds of everything the piece draws.
        internal static Bounds Bounds(Transform root)
        {
            var rs = root.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
