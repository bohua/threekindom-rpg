using System.Collections.Generic;
using System.IO;
using System.Linq;
using Jrpg;
using ThreeKingdoms.App;
using ThreeKingdoms.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ThreeKingdoms.EditorTools
{
    // Ink-wash trial (user 2026-10-25: the Bohai wild looks ugly and yellow; try ink-wash ground textures on a small
    // piece, leave the whole map alone). Copies the probe scene Bohai_Plain into Bohai_InkTrial, keeps only the patch
    // north of Pingyuan (north road, its bridge, the river, a wood and a hill), fences it in, swaps every texture that has an
    // ink version in BohaiPlain/Ink (same file name and size, so tiling and prop sizes stay) and replaces the warm LUT
    // grading with a neutral one. The probe scene and its materials are never written.
    public static class BohaiInkTrialBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Bohai_InkTrial.unity";
        public const string InkDir = BohaiPlainBuilder.ArtDir + "/Ink";
        const string InkMaterialDir = InkDir + "/Materials";
        public const string HighProfilePath = "Assets/_Project/Settings/HD2D_InkTrial.asset";
        public const string LowProfilePath = "Assets/_Project/Settings/HD2D_InkTrial_Low.asset";

        // The trial patch (metres, x east, z north) and how far beyond it scenery is kept for the camera.
        public const float X0 = -40f, X1 = 340f, Z0 = 10f, Z1 = 250f, Keep = 60f;
        public static readonly Vector3 PlayerStart = new Vector3(23f, 0f, 98f);   // on the north road just north of the bridge
        static readonly Vector3 VillageCentre = new Vector3(40f, 0, 200f);          // pixel houses stay out of the ink patch
        const float VillageRadius = 48f;

        [MenuItem("JRPG/Build Bohai ink-wash trial")]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(BohaiPlainBuilder.ScenePath, OpenSceneMode.Single);
            Crop();
            Fence();
            MoveParty();
            // Screenshot spots (-teleport): bridge head, wood edge, hill foot, river bank.
            foreach (var (name, at) in new[] { ("Bridge", PlayerStart), ("Wood", new Vector3(150f, 0, 128f)), ("Hill", new Vector3(250f, 0, 160f)), ("River", new Vector3(110f, 0, 40f)) })
                new GameObject("Spot Ink " + name).transform.position = at;
            int swapped = SwapTextures();
            Grade();
            EditorSceneManager.SaveScene(scene, ScenePath, saveAsCopy: true);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log($"BOHAI_INK_TRIAL_BUILT swapped={swapped}");
        }

        public static void BuildFromCommandLine()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static bool InPatch(Bounds b, float margin) =>
            b.max.x >= X0 - margin && b.min.x <= X1 + margin && b.max.z >= Z0 - margin && b.min.z <= Z1 + margin;

        static bool InVillage(Vector3 p) => (new Vector2(p.x, p.z) - new Vector2(VillageCentre.x, VillageCentre.z)).magnitude < VillageRadius;

        // Leaves outside the patch (plus a margin of scenery) and everything in the north village go; groups left empty go too.
        static void Crop()
        {
            var env = GameObject.Find("Environment").transform;
            var doomed = new List<GameObject>();
            foreach (var t in env.GetComponentsInChildren<Transform>(true))
            {
                if (t.childCount > 0 || t == env) continue;
                var r = t.GetComponent<Renderer>();
                var c = t.GetComponent<Collider>();
                if (!r && !c) continue;
                var b = r ? r.bounds : c.bounds;
                // Props: judge by the prop root (sprite and collider are its children).
                var root = t.parent != null && t.parent.GetComponent<Occluder>() ? t.parent : t;
                bool huge = b.size.x > 200f || b.size.z > 200f;   // ground, terrain meshes: keep, they run under the patch
                if (!huge && (!InPatch(b, Keep) || InVillage(b.center))) doomed.Add(t.gameObject);
                else if (!huge && root != t && InVillage(root.position)) doomed.Add(root.gameObject);
            }
            foreach (var go in doomed) if (go) Object.DestroyImmediate(go);
            // Region-edge walls of the probe stay irrelevant once fenced; empty groups are pruned bottom-up.
            bool pruned;
            do
            {
                pruned = false;
                foreach (var t in env.GetComponentsInChildren<Transform>(true))
                    if (t != env && t.childCount == 0 && t.GetComponents<Component>().Length == 1) { Object.DestroyImmediate(t.gameObject); pruned = true; break; }
            } while (pruned);

            var labels = GameObject.Find("Whitebox labels");
            if (labels) Object.DestroyImmediate(labels);
            var people = GameObject.Find("People").transform;
            foreach (var t in people.Cast<Transform>().ToArray())
            {
                if (t.GetComponent<PlayerMover>() || t.name.Contains("(follower)")) continue;
                var p = t.position;
                if (p.x < X0 || p.x > X1 || p.z < Z0 || p.z > Z1 || InVillage(p)) Object.DestroyImmediate(t.gameObject);
            }
        }

        static void Fence()
        {
            var g = new GameObject("Trial fence").transform;
            g.SetParent(GameObject.Find("Environment").transform, false);
            void Wall(Vector3 centre, Vector3 size)
            {
                var go = new GameObject("Collider");
                go.transform.SetParent(g, false);
                go.transform.localPosition = centre;
                go.AddComponent<BoxCollider>().size = size;
            }
            float cx = (X0 + X1) / 2f, cz = (Z0 + Z1) / 2f, w = X1 - X0, d = Z1 - Z0;
            Wall(new Vector3(X0 - 0.5f, 3f, cz), new Vector3(1f, 6f, d + 2f));
            Wall(new Vector3(X1 + 0.5f, 3f, cz), new Vector3(1f, 6f, d + 2f));
            Wall(new Vector3(cx, 3f, Z0 - 0.5f), new Vector3(w + 2f, 6f, 1f));
            Wall(new Vector3(cx, 3f, Z1 + 0.5f), new Vector3(w + 2f, 6f, 1f));
        }

        static void MoveParty()
        {
            var player = Object.FindFirstObjectByType<PlayerMover>().transform;
            var delta = PlayerStart - player.position;
            player.position = PlayerStart;
            foreach (var t in GameObject.Find("People").transform.Cast<Transform>())
                if (t != player && t.name.Contains("(follower)")) t.position += delta;
            var cam = UnityEngine.Camera.main;
            var follow = cam.GetComponent<FollowCamera>();
            cam.transform.position = player.position + follow.Offset;
            // The review cameras look at the trial patch instead of the whole region.
            var over = GameObject.Find("Review Overview");
            if (over) over.transform.SetPositionAndRotation(new Vector3((X0 + X1) / 2f, 260f, Z0 - 300f), Quaternion.Euler(38f, 0f, 0f));
        }

        // Ink versions live in Ink/A (paper-white, ink only) and Ink/B (ink with light colour), same file names as the probe
        // textures. Every single-material renderer whose texture has an ink version gets one cloned material per look; the
        // switcher in the scene flips between the original and the two.
        static readonly string[] Variants = { "A", "B" };
        static InkTrialSwitcher switcher;

        static int SwapTextures()
        {
            var ink = new Dictionary<string, Texture2D>[Variants.Length];
            for (int v = 0; v < Variants.Length; v++)
            {
                ink[v] = new Dictionary<string, Texture2D>();
                string dir = $"{InkDir}/{Variants[v]}";
                if (!Directory.Exists(dir)) continue;
                foreach (string path in Directory.GetFiles(dir, "*.png"))
                {
                    string p = path.Replace('\\', '/');
                    var importer = (TextureImporter)AssetImporter.GetAtPath(p);
                    if (importer == null) continue;
                    importer.textureType = TextureImporterType.Default;
                    importer.filterMode = FilterMode.Point;
                    importer.mipmapEnabled = false;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.alphaIsTransparency = true;
                    importer.SaveAndReimport();
                    ink[v][Path.GetFileNameWithoutExtension(p)] = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                }
            }
            Directory.CreateDirectory(InkMaterialDir);
            var clones = new Dictionary<(Material, int), Material>();
            Material Clone(Material m, int v)
            {
                if (clones.TryGetValue((m, v), out var c)) return c;
                c = null;
                var tex = m.GetTexture("_BaseMap") as Texture2D;
                if (tex && ink[v].TryGetValue(tex.name, out var inkTex))
                {
                    // Keep the original's tiling: wrap modes come from the probe texture.
                    var src = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex));
                    string inkPath = AssetDatabase.GetAssetPath(inkTex);
                    var dst = (TextureImporter)AssetImporter.GetAtPath(inkPath);
                    if (src && dst && (dst.wrapModeU != src.wrapModeU || dst.wrapModeV != src.wrapModeV))
                    {
                        dst.wrapModeU = src.wrapModeU; dst.wrapModeV = src.wrapModeV; dst.SaveAndReimport();
                        inkTex = AssetDatabase.LoadAssetAtPath<Texture2D>(inkPath);
                    }
                    c = new Material(m) { name = $"{m.name}_ink{Variants[v]}" };
                    c.SetTexture("_BaseMap", inkTex);
                    if (c.HasProperty("_MainTex")) c.SetTexture("_MainTex", inkTex);
                    // Water drawn as near-blank paper reads as ground in play (river looked walkable): a wash keeps it apart.
                    if (tex.name.Contains("water")) c.SetColor("_BaseColor", v == 0 ? new Color(0.70f, 0.72f, 0.74f) : new Color(0.60f, 0.75f, 0.82f));
                    string path = $"{InkMaterialDir}/{c.name}.mat";
                    if (AssetDatabase.LoadAssetAtPath<Material>(path)) AssetDatabase.DeleteAsset(path);
                    AssetDatabase.CreateAsset(c, path);
                }
                return clones[(m, v)] = c;
            }

            var renderers = new List<Renderer>();
            var originals = new List<Material>();
            var perLook = Variants.Select(_ => new List<Material>()).ToArray();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var m = r.sharedMaterial;
                if (r.sharedMaterials.Length != 1 || !m || !m.HasProperty("_BaseMap")) continue;
                var looks = Variants.Select((_, v) => Clone(m, v)).ToArray();
                if (looks.All(x => x == null)) continue;
                renderers.Add(r);
                originals.Add(m);
                for (int v = 0; v < Variants.Length; v++) perLook[v].Add(looks[v] ?? m);
            }
            switcher = new GameObject("Ink trial switcher").AddComponent<InkTrialSwitcher>();
            switcher.Renderers = renderers.ToArray();
            switcher.Materials = originals.Concat(perLook.SelectMany(x => x)).ToArray();
            return renderers.Count;
        }

        // Looks: the probe as it is (late-afternoon sun, warm LUT), and for both ink versions neutral daylight with a
        // paper-white haze and no LUT.
        static void Grade()
        {
            var sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).First(l => l.type == LightType.Directional);
            var volume = Object.FindFirstObjectByType<Volume>();
            var tier = volume.GetComponent<HD2DQualityTier>();
            var original = new InkTrialSwitcher.Look
            {
                Name = "原版（探针现状）",
                High = tier ? tier.High : volume.sharedProfile, Low = tier ? tier.Low : volume.sharedProfile,
                Fog = RenderSettings.fogColor, FogDensity = RenderSettings.fogDensity,
                Sky = RenderSettings.ambientSkyColor, Equator = RenderSettings.ambientEquatorColor, Ground = RenderSettings.ambientGroundColor,
                Sun = sun.color, SunIntensity = sun.intensity, ShadowStrength = sun.shadowStrength, SunEuler = sun.transform.eulerAngles,
            };
            InkTrialSwitcher.Look Ink(string name, string variant, float saturation) => new InkTrialSwitcher.Look
            {
                Name = name,
                High = Profile(HighProfilePath.Replace(".asset", $"_{variant}.asset"), HD2DSampleBuilder.HighProfilePath, saturation),
                Low = Profile(LowProfilePath.Replace(".asset", $"_{variant}.asset"), HD2DSampleBuilder.LowProfilePath, saturation),
                Fog = new Color(0.92f, 0.91f, 0.87f), FogDensity = 0.0030f,
                Sky = new Color(0.86f, 0.88f, 0.90f), Equator = new Color(0.80f, 0.80f, 0.78f), Ground = new Color(0.52f, 0.52f, 0.50f),
                Sun = new Color(1f, 0.99f, 0.96f), SunIntensity = 1.9f, ShadowStrength = 0.55f, SunEuler = new Vector3(50f, -28f, 0f),
            };
            if (!switcher) switcher = new GameObject("Ink trial switcher").AddComponent<InkTrialSwitcher>();
            switcher.Looks = new[] { original, Ink("水墨留白（A）", "A", -15f), Ink("水墨淡彩（B）", "B", -4f) };
            switcher.Volume = volume;
            switcher.Sun = sun;
            switcher.Renderers ??= new Renderer[0];
            switcher.Materials ??= new Material[0];
            // The quality tier would put its own profile back on Awake; the switcher owns the volume in this scene.
            if (tier) Object.DestroyImmediate(tier);
            sun.name = "Sun (set by the ink trial switcher)";
            switcher = null;
        }

        static VolumeProfile Profile(string path, string source, float saturation)
        {
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CopyAsset(source, path);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile.TryGet(out ColorLookup lookup)) lookup.active = false;
            if (profile.TryGet(out ColorAdjustments color))
            {
                color.postExposure.Override(-0.3f);   // paper-white textures clip under the old exposure
                color.contrast.Override(4f);
                color.saturation.Override(saturation);
                color.colorFilter.Override(Color.white);
            }
            if (profile.TryGet(out Vignette vignette)) vignette.intensity.Override(0.18f);
            if (profile.TryGet(out Bloom bloom)) bloom.intensity.Override(Mathf.Min(bloom.intensity.value, 0.3f));
            EditorUtility.SetDirty(profile);
            return profile;
        }
    }
}
