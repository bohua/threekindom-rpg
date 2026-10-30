using System.Collections.Generic;
using System.IO;
using System.Linq;
using Jrpg;
using ThreeKingdoms.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ThreeKingdoms.EditorTools
{
    // Chapter 1 open regions, hub first (user 2026-10-27, "好的开始": the hubs are built properly from Codex Blender
    // models, the wild stays a greybox for now, but its landform, the places and their heights are fixed beforehand by
    // a Codex design — "野外可以先灰模处理，但是地形地貌以及具体地址的位置和高度，必须先通过codex设计图敲定").
    //
    // Each region has a Codex concept set (art/<region>-concept: a painted top-down layout, a 16-bit heightmap at 1 px
    // per metre and terrain_layout.json). tools/regions/prepare_region.py turns that into greybox/region.json (places,
    // roads, water in one schema), heights.bin (float metres, row 0 = south) with every place's plot flattened, and
    // ground_tint.png (the painted layout with its contour lines filtered out). This builder raises the heightmap as a
    // walkable mesh coloured by that tint, lays the roads and water on it, stands a grey block for every place (a
    // Blender model where its hub set has been delivered) and labels it, so the greybox already sits where the final
    // places will. Metres, origin south-west, x east, z north, camera looks north, like the Bohai and Zhongshan regions.
    public static class RegionGreyboxBuilder
    {
        const string CodexArt = @"D:\BuildArtifacts\Codex\hd2d-jrpg\art";
        const string Root = "Assets/_Project/Art/Environment/Regions";

        [System.Serializable] public sealed class RegionData
        {
            public string id, name, scene;
            public float width, height;
            public RegionPlace[] places = new RegionPlace[0];
            public RegionRoad[] roads = new RegionRoad[0];
            public RegionWater[] waters = new RegionWater[0];
            public float spawnX, spawnZ;
            public float[] openZone = new float[0];   // x, z pairs: the part open in the earliest year, outlined on the ground
            public string wildSet, wildLayout;        // refinement: the region's Codex wild set and its placements in region metres
            public string detailSet, detailLayout;    // second refinement: ground clutter and finer vegetation
            public string[] dropPads = new string[0];  // materials of flat apron decals the baked earth replaces
            public string[] wildTint = new string[0]; // "material:r,g,b": painted textures that read too bright at noon
            public string[] wildSkip = new string[0]; // wild pieces left out (the Taihang cliff shells fought the heightmap's own mountains)
        }

        [System.Serializable] public sealed class RegionPlace
        {
            public string id, name, kind;   // town, gate_town, camp, village, post_station, explore, barrier, ferry, bridge, battlefield, exit
            public float x, z, y, w, d, yaw;
            public int year;
            public string model, set;       // a Blender model for it, when its hub set exists
            public string wallModel;        // walled places: <wallModel>_wall_8m/_4m of the same set, the model as the gate
            public string layout;           // a hub set's own layout (Codex _checks/layout_*.json): every piece where Codex placed it
            public bool skip;               // a sub-place drawn by its hub's layout: label only
            public float layoutX, layoutZ;  // where the layout's origin sits relative to the place centre (e.g. its gate)
            public float layoutY;           // added to every piece's height (layouts written in absolute regional heights)
            public string portalScene, portalArrival;
            public int[] portalSteps = new int[0];   // ChapterStep values during which the doorway is open (empty: always)
            public string arrivalName;      // the spot in front of the place, named so a portal from the hub scene finds it
        }

        [System.Serializable] public sealed class RegionRoad { public string id; public float width = 5f; public float[] pts = new float[0]; }   // x, z pairs
        [System.Serializable] public sealed class RegionWater { public string id; public float level; public float width; public float[] line = new float[0]; public float[] poly = new float[0]; public float[] levels = new float[0]; }   // levels: per line point, for rivers that fall

        // The regions built so far; their concept folder under art/.
        static readonly (string id, string concept)[] Regions =
        {
            ("R3", "r3-julu-concept"), ("R5A", "r5a-suanzao-concept"), ("R5B", "r5b-luoyang-concept"),
            ("R7", "r7-qingzhou-concept"), ("R8", "r8-xuzhou-concept"), ("R6", "r6-yanzhou-concept"),
        };

        static RegionData data;
        static float[,] heights;   // [x, z] at 1 m
        static string dir, matDir;

        [MenuItem("JRPG/Build Region Greyboxes (R3, R5a, R5b, R7, R8, R6)")]
        public static void BuildAll()
        {
            foreach (var r in Regions)
                if (File.Exists(Path.Combine(CodexArt, r.concept, "greybox", "region.json"))) Build(r.id);
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                var args = System.Environment.GetCommandLineArgs();
                int i = System.Array.IndexOf(args, "-region");
                if (i >= 0) Build(args[i + 1]); else BuildAll();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static string ScenePathOf(string id) => $"Assets/_Project/Scenes/Region_{id}.unity";

        public static void Build(string id)
        {
            string concept = Regions.First(r => r.id == id).concept;
            string src = Path.Combine(CodexArt, concept, "greybox");
            data = JsonUtility.FromJson<RegionData>(File.ReadAllText(Path.Combine(src, "region.json")));
            dir = $"{Root}/{id}";
            matDir = dir + "/Materials";
            Directory.CreateDirectory(matDir);
            mats.Clear();
            painted = false;
            LoadHeights(Path.Combine(src, "heights.bin"));
            foreach (var f in new[] { "ground_tint.png", "map.png", "map_labeled.png" })
                if (File.Exists(Path.Combine(src, f))) File.Copy(Path.Combine(src, f), $"{dir}/{f}", true);
            AssetDatabase.Refresh();
            foreach (var set in data.places.Where(p => !string.IsNullOrEmpty(p.set)).Select(p => p.set).Distinct()) BlenderModels.Import(set);

            MountedImporter.Import();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.86f, 0.93f, 1.06f);
            RenderSettings.ambientEquatorColor = new Color(0.78f, 0.80f, 0.78f);
            RenderSettings.ambientGroundColor = new Color(0.44f, 0.42f, 0.36f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.0016f;
            RenderSettings.fogColor = new Color(0.80f, 0.88f, 0.97f);

            var env = new GameObject("Environment").transform;
            var labels = new GameObject("Whitebox labels").transform;
            Terrain(env);
            Waters(env);
            Roads(env);
            OpenZone(env);
            var spots = new GameObject("Spots").transform;
            foreach (var p in data.places) Place(env, labels, spots, p);
            Wild(env);
            DropPads(env);
            Edges(env);

            var sun = new GameObject("Midday sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.98f, 0.94f);
            sun.intensity = 2.3f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.65f;
            sun.transform.rotation = Quaternion.Euler(56f, -24f, 0f);

            Locations();
            var spawn = new Vector3(data.spawnX, H(data.spawnX, data.spawnZ), data.spawnZ);
            var people = new GameObject("People").transform;
            var player = HD2DSampleBuilder.Character("Liu Bei", "LiuBei", spawn, 0, isPlayer: true);
            player.transform.SetParent(people, true);
            var trail = player.AddComponent<PartyTrail>();
            trail.Length = 24f;
            int slot = 1;
            var companions = new List<MountRider>();
            foreach (var (name, key) in new[] { ("Guan Yu (follower)", "GuanYu"), ("Zhang Fei (follower)", "ZhangFei") })
            {
                var go = HD2DSampleBuilder.Character(name, key, spawn + new Vector3(0, 0, -1.5f * slot), 0, isPlayer: false);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.AddComponent<PixelWalkFrames>().Animator = go.GetComponentInChildren<SpriteFrameAnimator>();
                var f = go.AddComponent<PartyFollower>();
                f.Trail = trail; f.Slot = slot++;
                go.transform.SetParent(people, true);
                companions.Add(ZhongshanBuilder.AddRider(go, key));
            }
            var ride = player.AddComponent<RideController>();
            ride.Mover = player.GetComponent<PlayerMover>();
            ride.Leader = ZhongshanBuilder.AddRider(player, "LiuBei");
            ride.Companions = companions.ToArray();

            Camera(player.transform);
            Hud(player.transform);
            string scenePath = ScenePathOf(id);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), scenePath);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == scenePath)) scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log($"REGION_GREYBOX_BUILT {id}");
        }

        // ---------- heights ----------

        static void LoadHeights(string path)
        {
            var bytes = File.ReadAllBytes(path);
            int w = Mathf.RoundToInt(data.width) + 1, h = Mathf.RoundToInt(data.height) + 1;
            heights = new float[w, h];
            for (int z = 0; z < h; z++)
                for (int x = 0; x < w; x++)
                    heights[x, z] = System.BitConverter.ToSingle(bytes, (z * w + x) * 4);
        }

        // Ground height at a point (bilinear between the 1 m samples).
        public static float H(float x, float z)
        {
            int w = heights.GetLength(0), h = heights.GetLength(1);
            x = Mathf.Clamp(x, 0, w - 1.001f); z = Mathf.Clamp(z, 0, h - 1.001f);
            int x0 = (int)x, z0 = (int)z;
            float fx = x - x0, fz = z - z0;
            return Mathf.Lerp(Mathf.Lerp(heights[x0, z0], heights[x0 + 1, z0], fx), Mathf.Lerp(heights[x0, z0 + 1], heights[x0 + 1, z0 + 1], fx), fz);
        }

        // ---------- materials ----------

        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

        static Material Mat(string name, Color color, Texture2D tex = null, bool transparent = false)
        {
            if (mats.TryGetValue(name, out var m)) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            m.SetColor("_BaseColor", color);
            if (tex) m.SetTexture("_BaseMap", tex);
            m.SetFloat("_Smoothness", transparent ? 0.6f : 0f);
            m.SetFloat("_SpecularHighlights", 0f); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            if (transparent)
            {
                m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            string path = $"{matDir}/{name}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            return mats[name] = m;
        }

        static Material Grey(float v) => Mat($"grey_{v:0.00}", new Color(v, v * 0.99f, v * 0.96f));

        static Texture2D Tint()
        {
            string path = $"{dir}/ground_tint.png";
            if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) return null;
            ti.textureType = TextureImporterType.Default; ti.mipmapEnabled = true; ti.filterMode = FilterMode.Trilinear;
            ti.wrapMode = TextureWrapMode.Clamp; ti.maxTextureSize = 4096; ti.npotScale = TextureImporterNPOTScale.None;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---------- refinement: the painted ground and the wild pieces ----------

        // The region's own painted grounds blended by the baked index map (bake_ground.py writes greybox/ground_index.png,
        // greybox/layers/NN_<name>.png at 512 px per 8 m tile and ground_layers.json); null while the region is still a greybox.
        static Material GroundArray()
        {
            string src = Path.Combine(CodexArt, Regions.First(r => r.id == data.id).concept, "greybox");
            string idx = Path.Combine(src, "ground_index.png"), atlas = Path.Combine(src, "ground_layers.png"), grid = Path.Combine(src, "ground_layers_grid.json");
            if (!File.Exists(idx) || !File.Exists(atlas) || !File.Exists(grid)) return null;
            // The grounds at full detail (1024 px per 8 m tile) as one compressed texture array, imported from the grid.
            var g = JsonUtility.FromJson<LayerGrid>(File.ReadAllText(grid));
            string arrPath = $"{dir}/ground_layers.png";
            File.Copy(atlas, arrPath, true);
            AssetDatabase.ImportAsset(arrPath);
            var ai = (TextureImporter)AssetImporter.GetAtPath(arrPath);
            ai.textureShape = TextureImporterShape.Texture2DArray;
            var settings = new TextureImporterSettings();
            ai.ReadTextureSettings(settings);
            settings.flipbookColumns = g.columns; settings.flipbookRows = g.rows;
            ai.SetTextureSettings(settings);
            ai.sRGBTexture = true; ai.mipmapEnabled = true; ai.npotScale = TextureImporterNPOTScale.None; ai.maxTextureSize = 8192;
            ai.textureCompression = TextureImporterCompression.CompressedHQ; ai.filterMode = FilterMode.Trilinear; ai.anisoLevel = 8;
            ai.wrapMode = TextureWrapMode.Repeat;
            ai.SaveAndReimport();
            var arr = AssetDatabase.LoadAssetAtPath<Texture2DArray>(arrPath);
            if (!arr) throw new System.InvalidOperationException("ground layers did not import as a texture array: " + arrPath);
            File.Copy(idx, $"{dir}/ground_index.png", true);
            AssetDatabase.ImportAsset($"{dir}/ground_index.png");
            var imp = (TextureImporter)AssetImporter.GetAtPath($"{dir}/ground_index.png");
            imp.sRGBTexture = false; imp.mipmapEnabled = false; imp.npotScale = TextureImporterNPOTScale.None; imp.maxTextureSize = 4096;
            imp.textureCompression = TextureImporterCompression.Uncompressed; imp.filterMode = FilterMode.Point; imp.wrapMode = TextureWrapMode.Clamp;
            imp.alphaIsTransparency = false;
            imp.SaveAndReimport();
            var m = new Material(Shader.Find("ThreeKingdoms/GroundArraySplat")) { name = "ground_painted" };
            m.SetTexture("_Index", AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/ground_index.png"));
            m.SetTexture("_Layers", arr);
            m.SetFloat("_TileMetres", 8f);   // Codex ground tiles are 960 px = 8 m
            m.SetVector("_Bounds", new Vector4(0, 0, data.width, data.height));
            m.SetColor("_Tint", new Color(1.18f, 1.16f, 1.08f));   // the painted grounds read dark under the region's sun
            // The region's rock ground also covers every steep slope: a cliff first, then bare rock, then scree.
            var keys = File.Exists(Path.Combine(src, "ground_layers.json"))
                ? System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(Path.Combine(src, "ground_layers.json")), "\"([^\"]+)\"").Cast<System.Text.RegularExpressions.Match>().Select(x => x.Groups[1].Value).ToList()
                : new List<string>();
            int rock = -1;
            foreach (var words in new[] { new[] { "cliff" }, new[] { "rock", "granite" }, new[] { "limestone", "stone", "scree" } })
                if (rock < 0) rock = keys.FindIndex(k => words.Any(k.Contains));
            m.SetFloat("_RockLayer", rock);
            // The region's most common ground carries on past its edge.
            m.SetFloat("_OuterLayer", Mathf.Max(0, keys.FindIndex(k => k.StartsWith("grass") || k.Contains("_grass"))));
            if (File.Exists(Path.Combine(src, "ground_rock.png")))
            {
                File.Copy(Path.Combine(src, "ground_rock.png"), $"{dir}/ground_rock.png", true);
                AssetDatabase.ImportAsset($"{dir}/ground_rock.png");
                var ri = (TextureImporter)AssetImporter.GetAtPath($"{dir}/ground_rock.png");
                ri.sRGBTexture = false; ri.mipmapEnabled = false; ri.npotScale = TextureImporterNPOTScale.None; ri.maxTextureSize = 1024;
                ri.textureCompression = TextureImporterCompression.Uncompressed; ri.filterMode = FilterMode.Bilinear; ri.wrapMode = TextureWrapMode.Clamp;
                ri.SaveAndReimport();
                m.SetTexture("_Rock", AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/ground_rock.png"));
            }
            string mp = $"{matDir}/ground_painted.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(mp)) AssetDatabase.DeleteAsset(mp);
            AssetDatabase.CreateAsset(m, mp);
            painted = true;
            return m;
        }
        static bool painted;
        [System.Serializable] sealed class LayerGrid { public int columns, rows, count; }

        // Trees, reeds, villages, bridges, cliffs ... where Codex placed them, each set on the ground under it (models
        // whose origin is the region origin, like the Taihang cliffs, keep their own heights).
        static void Wild(Transform env)
        {
            if (string.IsNullOrEmpty(data.wildSet) || !BlenderModels.Import(data.wildSet)) return;
            foreach (var t in data.wildTint)
            {
                var kv = t.Split(':'); var c = kv[1].Split(',').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                BlenderModels.Tint(data.wildSet, kv[0], new Color(c[0], c[1], c[2]));
            }
            // The second refinement pass (user 2026-10-28 "野外……不够精细"): ground clutter by the thousand, and finer
            // trees and reeds that replace the first set's pieces of the same name.
            bool detail = !string.IsNullOrEmpty(data.detailSet) && BlenderModels.Import(data.detailSet);
            // Leaves, reeds and grass cards are painted for a softer light and glare at noon (user 2026-10-30): one
            // shade darker everywhere unless the hubs file names its own tint.
            foreach (string set in detail ? new[] { data.wildSet, data.detailSet } : new[] { data.wildSet })
                foreach (string key in BlenderModels.Keys(set))
                    if (System.Text.RegularExpressions.Regex.IsMatch(key, "leaf|needle|foliage|reed|rush|sedge|cattail|grass|flower|wormwood|artemisia|lotus|moss|sapling|seedling|jujube_(low|sucker)")
                        && !data.wildTint.Any(t => t.StartsWith(key + ":")))
                        BlenderModels.Tint(set, key, new Color(0.7f, 0.74f, 0.66f));
            // The ground clutter's stones are near white in the sun; a grey closer to the painted ground.
            if (detail)
                foreach (string key in BlenderModels.Keys(data.detailSet))
                    if (System.Text.RegularExpressions.Regex.IsMatch(key, "stone|rock|granite|pebble|chip|limestone") && !data.wildTint.Any(t => t.StartsWith(key + ":")))
                        BlenderModels.Tint(data.detailSet, key, new Color(0.74f, 0.73f, 0.7f));
            Scatter(env, "Wild", data.wildSet, data.wildLayout, detail ? data.detailSet : null, combine: false);
            // densify_detail.py's thicker copy of the Codex clutter, when baked.
            string concept = Regions.First(r => r.id == data.id).concept;
            string dense = File.Exists(Path.Combine(CodexArt, concept, "greybox", "detail_dense.json")) ? $"../{concept}/greybox/detail_dense.json" : data.detailLayout;
            if (detail) Scatter(env, "Ground detail", data.detailSet, dense, null, combine: true);
        }

        // The models' flat earth aprons sit on the ground as a lighter, hard-edged patch; the baked ground already blends
        // earth into grass under every place (bake_ground.py), so in a painted region the aprons go (user 2026-10-28).
        static void DropPads(Transform env)
        {
            if (!painted || data.dropPads.Length == 0) return;
            foreach (var r in env.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!r || r.sharedMaterials.Length == 0 || r.bounds.size.y > 0.35f) continue;
                if (r.sharedMaterials.All(m => m && data.dropPads.Contains(System.Text.RegularExpressions.Regex.Replace(m.name, @"\.\d+$", ""))))
                    Object.DestroyImmediate(r.gameObject);
            }
        }

        static void Scatter(Transform env, string name, string set, string layout, string preferSet, bool combine)
        {
            string path = Path.Combine(CodexArt, set, layout.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) return;
            var g = new GameObject(name).transform;
            g.SetParent(env, false);
            var file = JsonUtility.FromJson<LayoutFile>(File.ReadAllText(path));
            foreach (var piece in file.placements)
            {
                if (data.wildSkip.Contains(piece.asset)) continue;
                var pos = piece.position_m != null && piece.position_m.Length >= 2 ? piece.position_m : piece.position;
                // Codex roles: whole-region geometry (cliffs, the Fukou pass) keeps its own heights; bridges sit at their
                // deck height; everything else stands on our ground (smoothed, so not Codex's sampled height).
                float y = piece.placement_role == "world_geometry" ? (pos.Length > 2 ? pos[2] : 0f) + piece.elevation_offset_m
                        : piece.placement_role == "bridge_surface" ? piece.deck_world_height_m
                        : H(pos[0], pos[1]);
                var at = new Vector3(pos[0], y, pos[1]);
                float yaw = -(piece.rotation_z_deg + piece.rotation_z);
                var t = (preferSet != null ? BlenderModels.Place(preferSet, g, piece.asset, piece.asset, at, yaw) : null)
                        ?? BlenderModels.Place(set, g, piece.asset, piece.asset, at, yaw);
                if (t && piece.hide_objects != null)
                    foreach (var c in t.GetComponentsInChildren<Transform>(true).Where(c => piece.hide_objects.Contains(c.name)).ToArray())
                        if (c) Object.DestroyImmediate(c.gameObject);
                if (t && piece.scale > 0f) t.localScale *= piece.scale;
            }
            if (combine) CombineByCell(g, 48f);
        }

        // Thousands of small pieces would be thousands of objects: merge them per 48 m cell and material into a few
        // meshes (no colliders, no occluders: grass and pebbles never block the party or the camera).
        static void CombineByCell(Transform g, float cell)
        {
            foreach (var c in g.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var o in g.GetComponentsInChildren<Occluder>(true)) Object.DestroyImmediate(o);
            var groups = new Dictionary<(int, int, Material), List<CombineInstance>>();
            foreach (var r in g.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mf = r.GetComponent<MeshFilter>();
                if (!mf || !mf.sharedMesh) continue;
                var p = r.transform.position;
                for (int i = 0; i < mf.sharedMesh.subMeshCount && i < r.sharedMaterials.Length; i++)
                {
                    var key = ((int)(p.x / cell), (int)(p.z / cell), r.sharedMaterials[i]);
                    if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<CombineInstance>();
                    list.Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = i, transform = r.transform.localToWorldMatrix });
                }
            }
            for (int i = g.childCount - 1; i >= 0; i--) Object.DestroyImmediate(g.GetChild(i).gameObject);
            string meshes = $"{dir}/{data.id}_Terrain.asset";
            int n = 0;
            foreach (var kv in groups)
            {
                if (!kv.Key.Item3) continue;
                var mesh = new Mesh { name = $"{g.name} {n++}", indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                AssetDatabase.AddObjectToAsset(mesh, meshes);
                var go = new GameObject(mesh.name);
                go.transform.SetParent(g, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = kv.Key.Item3;
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        // ---------- terrain: the heightmap as 64 m chunks, walkable, coloured by the painted layout ----------

        static void Terrain(Transform env)
        {
            var g = new GameObject("Terrain").transform;
            g.SetParent(env, false);
            var mat = GroundArray() ?? Mat("ground", Color.white, Tint());
            var meshes = $"{dir}/{data.id}_Terrain.asset";
            AssetDatabase.DeleteAsset(meshes);
            bool first = true;
            int W = heights.GetLength(0) - 1, Hh = heights.GetLength(1) - 1;
            const int C = 64;
            for (int cz = 0; cz < Hh; cz += C)
                for (int cx = 0; cx < W; cx += C)
                {
                    int nx = Mathf.Min(C, W - cx), nz = Mathf.Min(C, Hh - cz);
                    var verts = new Vector3[(nx + 1) * (nz + 1)];
                    var uvs = new Vector2[verts.Length];
                    for (int z = 0; z <= nz; z++)
                        for (int x = 0; x <= nx; x++)
                        {
                            int gx = cx + x, gz = cz + z;
                            verts[z * (nx + 1) + x] = new Vector3(gx, heights[gx, gz], gz);
                            uvs[z * (nx + 1) + x] = new Vector2(gx / data.width, gz / data.height);
                        }
                    var tris = new List<int>(nx * nz * 6);
                    for (int z = 0; z < nz; z++)
                        for (int x = 0; x < nx; x++)
                        {
                            int a = z * (nx + 1) + x, b = a + 1, c = a + nx + 1, d = c + 1;
                            tris.AddRange(new[] { a, c, b, b, c, d });
                        }
                    var mesh = new Mesh { name = $"Terrain {cx},{cz}", vertices = verts, uv = uvs, triangles = tris.ToArray() };
                    mesh.RecalculateNormals(); mesh.RecalculateBounds();
                    if (first) { AssetDatabase.CreateAsset(mesh, meshes); first = false; } else AssetDatabase.AddObjectToAsset(mesh, meshes);
                    var go = new GameObject(mesh.name);
                    go.transform.SetParent(g, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = mat;
                    r.shadowCastingMode = ShadowCastingMode.On;
                    go.AddComponent<MeshCollider>().sharedMesh = mesh;
                }
            // A skirt of the same ground a long way out, so the region's edge is not a cliff into the void.
            var skirt = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Object.DestroyImmediate(skirt.GetComponent<Collider>());
            skirt.name = "Outer ground";
            skirt.transform.SetParent(g, false);
            skirt.transform.position = new Vector3(data.width / 2f, -6f, data.height / 2f);   // below every river bed
            skirt.transform.localScale = new Vector3(data.width / 10f * 4f, 1, data.height / 10f * 4f);
            skirt.GetComponent<MeshRenderer>().sharedMaterial = Mat("outer ground", new Color(0.56f, 0.62f, 0.44f));
            if (painted) Beyond(g, mat);
        }

        // The country past the region's edge (user 2026-10-30: the edges showed an empty plain): the edge ground carries
        // on outward, then rises into low hills and far mountains, so every view out ends in land, never the skirt.
        static void Beyond(Transform g, Material mat)
        {
            int W = heights.GetLength(0) - 1, D = heights.GetLength(1) - 1;
            const int R = 720, S = 8;
            System.Func<int, int, List<int>> axis = (n, step) =>
            {
                var l = new List<int>();
                for (int v = -R; v < 0; v += step) l.Add(v);
                for (int v = 0; v < n; v += step) l.Add(v);
                l.Add(n);
                for (int v = n + step; v <= n + R; v += step) l.Add(v);
                return l;
            };
            var xs = axis(W, S); var zs = axis(D, S);
            var verts = new List<Vector3>(xs.Count * zs.Count);
            foreach (int z in zs)
                foreach (int x in xs)
                {
                    float dx = Mathf.Max(0, -x, x - W), dz = Mathf.Max(0, -z, z - D), d = Mathf.Sqrt(dx * dx + dz * dz);
                    float range = 22f + 70f * Mathf.PerlinNoise(x / 260f + 3.1f, z / 260f + 7.7f);
                    float ridge = 1f - Mathf.Abs(Mathf.PerlinNoise(x / 90f + 11f, z / 90f + 5f) * 2f - 1f);
                    float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(40f, 520f, d)) * range * (0.65f + 0.35f * ridge)
                               + Mathf.InverseLerp(0f, 60f, d) * 1.5f * Mathf.PerlinNoise(x / 25f, z / 25f);
                    verts.Add(new Vector3(x, H(x, z) + rise - (d > 0 ? 0f : 0.04f), z));
                }
            var tris = new List<int>();
            int nx = xs.Count;
            for (int j = 0; j < zs.Count - 1; j++)
                for (int i = 0; i < nx - 1; i++)
                {
                    // Cells inside the region belong to the terrain itself.
                    if (xs[i] >= 0 && xs[i + 1] <= W && zs[j] >= 0 && zs[j + 1] <= D) continue;
                    int a = j * nx + i, b = a + 1, c = a + nx, e = c + 1;
                    tris.AddRange(new[] { a, c, b, b, c, e });
                }
            Save(g, "Beyond the region", verts, tris, mat);
        }

        // ---------- water, roads ----------

        static void Waters(Transform env)
        {
            var g = new GameObject("Water").transform;
            g.SetParent(env, false);
            var mat = Mat("water", new Color(0.30f, 0.52f, 0.56f, 0.72f), null, transparent: true);
            foreach (var w in data.waters)
            {
                if (w.poly.Length >= 6) PolyMesh(g, w.id, Pts(w.poly), w.level, mat);
                else if (w.line.Length >= 4)
                {
                    var drawn = Pts(w.line);
                    var line = Smooth(drawn, 2f);
                    // A falling river takes the level of its nearest drawn point.
                    System.Func<Vector3, float> level = w.levels.Length == drawn.Count
                        ? q => w.levels[Enumerable.Range(0, drawn.Count).OrderBy(i => (drawn[i] - new Vector3(q.x, 0, q.z)).sqrMagnitude).First()]
                        : _ => w.level;
                    Ribbon(g, w.id, line, w.width, mat, level, 0f, false);
                }
            }
        }

        static void Roads(Transform env)
        {
            var g = new GameObject("Roads").transform;
            g.SetParent(env, false);
            var mat = Mat("road", new Color(0.80f, 0.70f, 0.52f));
            if (painted) return;   // the painted ground already draws the roads, with their own edges
            // Inside a modelled hub the hub's own ground and streets take over: a road stops at its walls.
            var hubs = data.places.Where(p => !string.IsNullOrEmpty(p.layout) || !string.IsNullOrEmpty(p.wallModel)).ToArray();
            bool InHub(Vector3 q) => hubs.Any(p =>
            {
                var l = Quaternion.Euler(0, -p.yaw, 0) * (q - new Vector3(p.x, 0, p.z));
                return Mathf.Abs(l.x) < p.w / 2f - 1f && Mathf.Abs(l.z) < p.d / 2f - 1f;
            });
            foreach (var r in data.roads)
            {
                if (r.pts.Length < 4) continue;
                var run = new List<Vector3>();
                int part = 0;
                foreach (var q in Smooth(Pts(r.pts), 2f).Append(new Vector3(float.NaN, 0, 0)))
                {
                    if (!float.IsNaN(q.x) && !InHub(q)) { run.Add(q); continue; }
                    if (run.Count >= 2) Ribbon(g, $"{r.id} {part++}", run, r.width, mat, p => H(p.x, p.z), 0.06f, true);
                    run = new List<Vector3>();
                }
            }
        }

        static List<Vector3> Pts(float[] xz)
        {
            var l = new List<Vector3>();
            for (int i = 0; i + 1 < xz.Length; i += 2) l.Add(new Vector3(xz[i], 0, xz[i + 1]));
            return l;
        }

        // A polyline resampled every step metres with Catmull-Rom curves, so corners come out round.
        static List<Vector3> Smooth(List<Vector3> pts, float step)
        {
            if (pts.Count < 3) return pts;
            var o = new List<Vector3>();
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                Vector3 p0 = pts[Mathf.Max(0, i - 1)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Mathf.Min(pts.Count - 1, i + 2)];
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(p1, p2) / step));
                for (int k = 0; k < n; k++)
                {
                    float t = k / (float)n;
                    o.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t));
                }
            }
            o.Add(pts[pts.Count - 1]);
            return o;
        }

        // A band along a line, following the ground (roads) or flat at a level (rivers); ends taper (no square ends).
        static void Ribbon(Transform parent, string name, List<Vector3> pts, float width, Material mat, System.Func<Vector3, float> y, float lift, bool taper)
        {
            if (pts.Count < 2) return;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < pts.Count; i++)
            {
                var dir = (pts[Mathf.Min(i + 1, pts.Count - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
                var side = new Vector3(dir.z, 0, -dir.x);
                float k = taper ? Mathf.Clamp01(Mathf.Min(i, pts.Count - 1 - i) / 3f) * 0.6f + 0.4f : 1f;
                foreach (float s in new[] { -1f, 1f })
                {
                    var p = pts[i] + side * s * width * 0.5f * k;
                    p.y = y(p) + lift;
                    verts.Add(p);
                }
                if (i > 0) { int a = (i - 1) * 2; tris.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 }); }
            }
            Save(parent, name, verts, tris, mat);
        }

        static void PolyMesh(Transform parent, string name, List<Vector3> poly, float level, Material mat)
        {
            // Fan from the centroid: the water outlines Codex draws are close to convex lakes and pools.
            var c = poly.Aggregate(Vector3.zero, (s, p) => s + p) / poly.Count;
            var verts = new List<Vector3> { new Vector3(c.x, level, c.z) };
            verts.AddRange(poly.Select(p => new Vector3(p.x, level, p.z)));
            var tris = new List<int>();
            for (int i = 1; i <= poly.Count; i++) tris.AddRange(new[] { 0, i, i % poly.Count + 1 });
            Save(parent, name, verts, tris, mat, doubleSided: true);
        }

        static void Save(Transform parent, string name, List<Vector3> verts, List<int> tris, Material mat, bool doubleSided = false)
        {
            if (doubleSided) { int n = tris.Count; for (int i = 0; i < n; i += 3) tris.AddRange(new[] { tris[i], tris[i + 2], tris[i + 1] }); }
            var mesh = new Mesh { name = name, vertices = verts.ToArray(), triangles = tris.ToArray() };
            if (verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            string meshes = $"{dir}/{data.id}_Terrain.asset";
            AssetDatabase.AddObjectToAsset(mesh, meshes);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        // The part open in the earliest year (R3: Guangzong in 184), outlined by a row of low stakes for review.
        static void OpenZone(Transform env)
        {
            if (data.openZone.Length < 6) return;
            var g = new GameObject("Open zone outline (review)").transform;
            g.SetParent(env, false);
            var pts = Pts(data.openZone);
            var mat = Mat("open zone", new Color(0.95f, 0.78f, 0.25f));
            for (int i = 0; i < pts.Count; i++)
            {
                var a = pts[i]; var b = pts[(i + 1) % pts.Count];
                int n = Mathf.CeilToInt(Vector3.Distance(a, b) / 6f);
                for (int k = 0; k < n; k++)
                {
                    var p = Vector3.Lerp(a, b, k / (float)n);
                    var stake = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Object.DestroyImmediate(stake.GetComponent<Collider>());
                    stake.transform.SetParent(g, false);
                    stake.transform.position = new Vector3(p.x, H(p.x, p.z) + 0.3f, p.z);
                    stake.transform.localScale = new Vector3(0.25f, 0.6f, 0.25f);
                    stake.GetComponent<MeshRenderer>().sharedMaterial = mat;
                }
            }
        }

        // ---------- places ----------

        static GameObject Box(Transform parent, string name, Vector3 baseCentre, Vector3 size, float shade, float yaw = 0f, bool solid = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = baseCentre + new Vector3(0, size.y / 2f, 0);
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = Grey(shade);
            if (size.y > 3f) go.AddComponent<Occluder>();
            return go;
        }

        static void Place(Transform env, Transform labels, Transform spots, RegionPlace p)
        {
            var g = new GameObject($"{p.kind} {p.name}").transform;
            g.SetParent(env, false);
            float y = H(p.x, p.z);
            g.position = new Vector3(p.x, y, p.z);
            g.rotation = Quaternion.Euler(0, p.yaw, 0);
            float w = Mathf.Max(p.w, 4f), d = Mathf.Max(p.d, 4f);
            bool modelled = p.skip
                || (!string.IsNullOrEmpty(p.layout) && ModelLayout(g, p))
                || (!string.IsNullOrEmpty(p.wallModel) && ModelWalls(g, p, w, d))
                || (string.IsNullOrEmpty(p.wallModel) && !string.IsNullOrEmpty(p.model) && BlenderModels.Place(p.set, g, p.model, p.name, Vector3.zero) != null);
            if (!modelled)
                switch (p.kind)
                {
                    case "town":
                    case "gate_town":
                        Walls(g, w, d, p.kind == "town" ? 6f : 5f, gateOpen: p.year <= 0 || !string.IsNullOrEmpty(p.portalScene));
                        if (p.kind == "town") { Box(g, "Hall", new Vector3(0, 0, d * 0.25f), new Vector3(14f, 5f, 9f), 0.6f); for (int i = -1; i <= 1; i += 2) Box(g, "Block", new Vector3(i * w * 0.25f, 0, 0), new Vector3(10f, 4f, 8f), 0.64f); }
                        break;
                    case "camp":
                        Walls(g, w, d, 3f, gateOpen: true, shade: 0.45f);
                        Box(g, "Command tent", new Vector3(0, 0, d * 0.32f), new Vector3(12f, 5f, 8f), 0.78f);
                        for (int i = -1; i <= 1; i += 2)
                            for (int k = 0; k < 3; k++)
                                Box(g, "Tent", new Vector3(i * w * 0.3f, 0, -d * 0.25f + k * 9f), new Vector3(5f, 2.6f, 4f), 0.74f);
                        break;
                    case "village":
                        for (int i = 0; i < 5; i++)
                        {
                            float a = i * 72f * Mathf.Deg2Rad;
                            var at = new Vector3(Mathf.Cos(a) * w * 0.3f, 0, Mathf.Sin(a) * d * 0.3f);
                            Box(g, "House", at, new Vector3(6f, 3.4f, 5f), 0.66f, i * 17f);
                        }
                        break;
                    case "post_station":
                        Box(g, "Post station", new Vector3(0, 0, 3f), new Vector3(8f, 3.6f, 6f), 0.7f);
                        Box(g, "Stable", new Vector3(9f, 0, 3f), new Vector3(7f, 3f, 5f), 0.6f);
                        break;
                    case "barrier":
                        Box(g, "Barrier", Vector3.zero, new Vector3(w, 4f, 2f), 0.35f);
                        Box(g, "Guard post", new Vector3(w / 2f + 3f, 0, -2f), new Vector3(3f, 6f, 3f), 0.42f);
                        break;
                    case "bridge":
                        Box(g, "Deck", new Vector3(0, -0.2f, 0), new Vector3(Mathf.Max(5f, p.w), 0.4f, Mathf.Max(5f, p.d)), 0.55f, 0f, solid: false);
                        break;
                    case "ferry":
                        Box(g, "Landing", new Vector3(0, -0.3f, 0), new Vector3(8f, 0.3f, 6f), 0.55f, 0f, solid: false);
                        Box(g, "Ferry hut", new Vector3(-7f, 0, 4f), new Vector3(5f, 3f, 4f), 0.62f);
                        break;
                    case "battlefield":
                    case "explore":
                    default:
                        // A low pad the size of the place and a marker post, so the spot reads without blocking it. In a
                        // painted region the ground and the wild set already show the place: no grey pad on the earth.
                        if (painted) break;
                        Box(g, "Pad", new Vector3(0, -0.1f, 0), new Vector3(Mathf.Min(w, 16f), 0.3f, Mathf.Min(d, 16f)), 0.62f, 0f, solid: false);
                        Box(g, "Post", Vector3.zero, new Vector3(0.4f, 3f, 0.4f), 0.4f);
                        break;
                }
            Label(labels, p.name, g.position + new Vector3(0, Mathf.Max(8f, d * 0.12f), 0));
            var spot = new GameObject(string.IsNullOrEmpty(p.arrivalName) ? "Spot " + p.name : p.arrivalName);
            spot.transform.SetParent(spots, false);
            // In front of the place (south of it in its own frame), where the party stands on arrival.
            var front = g.position + g.rotation * new Vector3(0, 0, -(d / 2f + 4f));
            spot.transform.position = new Vector3(front.x, H(front.x, front.z), front.z);
            if (!string.IsNullOrEmpty(p.portalScene))
            {
                var portal = new GameObject("Portal " + p.name).AddComponent<RegionPortal>();
                portal.transform.SetParent(g, false);
                portal.transform.localPosition = new Vector3(0, 0, -d / 2f);
                portal.Size = new Vector2(6f, 2.5f); portal.TargetScene = p.portalScene; portal.ArrivalSpot = p.portalArrival;
                portal.Facing = Vector3.forward;
                portal.Steps = p.portalSteps ?? new int[0];
            }
        }

        [System.Serializable] sealed class LayoutFile { public LayoutPiece[] placements = new LayoutPiece[0]; }
        [System.Serializable] sealed class LayoutPiece
        {
            public string asset, child, placement_role;
            public float[] position_m, position;
            public float rotation_z_deg, rotation_z, scale, deck_world_height_m, elevation_offset_m;
            public string[] hide_objects;
        }

        // A hub laid out piece by piece as Codex assembled it in Blender (+Y north = +z, rotations anticlockwise).
        // A piece with a child keeps only that object of a prop group, moved to the piece's origin.
        static bool ModelLayout(Transform g, RegionPlace p)
        {
            string path = Path.Combine(CodexArt, p.set, p.layout.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) return false;
            var file = JsonUtility.FromJson<LayoutFile>(File.ReadAllText(path));
            int n = 0;
            foreach (var piece in file.placements)
            {
                // Codex names the fields differently per set (position_m / rotation_z_deg, position / rotation_z).
                var pos = piece.position_m != null && piece.position_m.Length >= 2 ? piece.position_m : piece.position;
                var at = new Vector3(pos[0] + p.layoutX, (pos.Length > 2 ? pos[2] : 0f) + p.layoutY, pos[1] + p.layoutZ);
                var t = BlenderModels.Place(p.set, g, piece.asset, string.IsNullOrEmpty(piece.child) ? piece.asset : piece.child, at, -(piece.rotation_z_deg + piece.rotation_z));
                if (!t) continue;
                n++;
                if (string.IsNullOrEmpty(piece.child)) continue;
                var keep = t.GetComponentsInChildren<Transform>(true).FirstOrDefault(c => c.name == piece.child);
                if (!keep) { Object.DestroyImmediate(t.gameObject); continue; }
                foreach (var r in t.GetComponentsInChildren<MeshRenderer>(true).Where(r => !r.transform.IsChildOf(keep)).ToArray())
                    Object.DestroyImmediate(r.gameObject);
                keep.localPosition = new Vector3(0, keep.localPosition.y, 0);
            }
            return n > 0;
        }

        // A walled place from its set's modules: 8 m and 4 m runs (stretched a little to fit), corners, the gate model in
        // the middle of the front (south in the place's own frame). False when the set is missing.
        static bool ModelWalls(Transform g, RegionPlace p, float w, float d)
        {
            var gate = BlenderModels.Place(p.set, g, p.model, "Gate", new Vector3(0, 0, -d / 2f));
            if (!gate) return false;
            void Run(Vector3 a, Vector3 b)
            {
                float len = Vector3.Distance(a, b);
                if (len < 1f) return;
                float yaw = Mathf.Atan2(b.x - a.x, b.z - a.z) * Mathf.Rad2Deg - 90f;
                int n8 = Mathf.FloorToInt(len / 8f);
                var pieces = Enumerable.Repeat(8f, n8).ToList();
                float rem = len - n8 * 8f;
                if (rem > 2f) pieces.Add(rem); else if (n8 > 0) for (int i = 0; i < n8; i++) pieces[i] = len / n8; else pieces.Add(len);
                float at = 0f;
                foreach (float piece in pieces)
                {
                    bool small = piece < 6f;
                    var t = BlenderModels.Place(p.set, g, $"{p.wallModel}_wall_{(small ? 4 : 8)}m", "Wall", Vector3.Lerp(a, b, (at + piece / 2f) / len), yaw);
                    if (t) t.localScale = new Vector3(piece / (small ? 4f : 8f), 1f, 1f);
                    at += piece;
                }
            }
            float hw = w / 2f, hd = d / 2f, gap = 6.5f;
            Run(new Vector3(-hw, 0, -hd), new Vector3(-gap, 0, -hd));
            Run(new Vector3(gap, 0, -hd), new Vector3(hw, 0, -hd));
            Run(new Vector3(hw, 0, -hd), new Vector3(hw, 0, hd));
            Run(new Vector3(hw, 0, hd), new Vector3(-hw, 0, hd));
            Run(new Vector3(-hw, 0, hd), new Vector3(-hw, 0, -hd));
            return true;
        }

        // A rectangle of wall round a place with a 7 m gap in the middle of its south side.
        static void Walls(Transform g, float w, float d, float h, bool gateOpen, float shade = 0.52f)
        {
            float t = 2f, gap = 7f;
            Box(g, "Wall N", new Vector3(0, 0, d / 2f), new Vector3(w, h, t), shade);
            Box(g, "Wall W", new Vector3(-w / 2f, 0, 0), new Vector3(t, h, d), shade);
            Box(g, "Wall E", new Vector3(w / 2f, 0, 0), new Vector3(t, h, d), shade);
            float side = (w - gap) / 2f;
            Box(g, "Wall SW", new Vector3(-(gap + side) / 2f, 0, -d / 2f), new Vector3(side, h, t), shade);
            Box(g, "Wall SE", new Vector3((gap + side) / 2f, 0, -d / 2f), new Vector3(side, h, t), shade);
            Box(g, "Gate tower", new Vector3(0, h, -d / 2f), new Vector3(gap + 2f, 2.5f, t + 1f), shade * 0.9f, 0f, solid: false);
            if (!gateOpen) Box(g, "Gate (shut)", new Vector3(0, 0, -d / 2f), new Vector3(gap, h, 0.6f), 0.3f);
        }

        static void Label(Transform labels, string text, Vector3 at)
        {
            var go = new GameObject("Label " + text);
            go.transform.SetParent(labels, false);
            go.transform.position = at;
            var l = go.AddComponent<WhiteboxLabel>();
            l.Text = text; l.Height = 0f;
        }

        // Invisible walls round the region (the exits are places of kind "exit" with their own labels).
        static void Edges(Transform env)
        {
            var g = new GameObject("Region edge").transform;
            g.SetParent(env, false);
            void Wall(Vector3 c, Vector3 s) { var go = new GameObject("Edge"); go.transform.SetParent(g, false); go.transform.position = c; go.AddComponent<BoxCollider>().size = s; }
            float W = data.width, D = data.height;
            Wall(new Vector3(W / 2f, 20f, -1f), new Vector3(W + 4f, 80f, 2f));
            Wall(new Vector3(W / 2f, 20f, D + 1f), new Vector3(W + 4f, 80f, 2f));
            Wall(new Vector3(-1f, 20f, D / 2f), new Vector3(2f, 80f, D + 4f));
            Wall(new Vector3(W + 1f, 20f, D / 2f), new Vector3(2f, 80f, D + 4f));
        }

        static void Locations()
        {
            var g = new GameObject("Location zones").transform;
            var z0 = new GameObject($"{data.name} · 野外").AddComponent<LocationZone>();
            z0.transform.SetParent(g, false);
            z0.transform.position = new Vector3(data.width / 2f, 0, data.height / 2f);
            z0.Major = data.name; z0.Minor = "野外"; z0.Size = new Vector2(data.width + 100f, data.height + 100f); z0.Ride = true;
            foreach (var p in data.places)
            {
                var z = new GameObject($"{data.name} · {p.name}").AddComponent<LocationZone>();
                z.transform.SetParent(g, false);
                z.transform.position = new Vector3(p.x, 0, p.z);
                z.Major = data.name; z.Minor = p.name.Split('（')[0]; z.Priority = 3;
                z.Size = new Vector2(Mathf.Max(p.w, 16f) + 10f, Mathf.Max(p.d, 16f) + 10f);
            }
        }

        static void Camera(Transform player)
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<UnityEngine.Camera>();
            cam.fieldOfView = HD2DSampleBuilder.CameraFov;
            cam.nearClipPlane = 1f; cam.farClipPlane = 900f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.None;
            Quaternion pitch = Quaternion.Euler(HD2DSampleBuilder.CameraPitch, 0f, 0f);
            camGo.transform.rotation = pitch;
            var follow = camGo.AddComponent<FollowCamera>();
            follow.Target = player;
            follow.Offset = new Vector3(0, 0.8f, 0) + pitch * new Vector3(0, 0, -HD2DSampleBuilder.CameraDistance);
            camGo.transform.position = player.position + follow.Offset;
            follow.UseRig = true;
            follow.Pitch = HD2DSampleBuilder.CameraPitch; follow.Distance = HD2DSampleBuilder.CameraDistance; follow.Fov = HD2DSampleBuilder.CameraFov;
            camGo.AddComponent<SeeThroughOccluders>().Target = player;

            var volumeGo = new GameObject("Global Volume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ZhongshanBuilder.ArtDir + "/Zhongshan_Daylight_High.asset")
                ?? AssetDatabase.LoadAssetAtPath<VolumeProfile>(HD2DSampleBuilder.HighProfilePath);
            follow.FocusVolume = volume;

            var zone = new GameObject("Open country").AddComponent<CameraZone>();
            zone.transform.position = new Vector3(data.width / 2f, 0, data.height / 2f);
            zone.Size = new Vector2(data.width, data.height);
            zone.Pitch = 32f; zone.Distance = 40f; zone.Fov = 21f; zone.LookOffset = new Vector3(0, 0.8f, 0);

            var over = new GameObject("Review Overview").AddComponent<UnityEngine.Camera>();
            over.enabled = false;
            over.fieldOfView = 34f; over.farClipPlane = 3000f;
            over.transform.SetPositionAndRotation(new Vector3(data.width / 2f, data.width * 0.7f, -data.height * 0.75f), Quaternion.Euler(42f, 0f, 0f));
        }

        static void Hud(Transform player)
        {
            var go = new GameObject("Region HUD");
            var doc = go.AddComponent<UnityEngine.UIElements.UIDocument>();
            doc.panelSettings = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>("Assets/_Project/Settings/BattlePanelSettings.asset");
            var hud = go.AddComponent<ThreeKingdoms.UI.Region.RegionHud>();
            Texture2D Painted(string file)
            {
                string path = $"{dir}/{file}";
                if (!(AssetImporter.GetAtPath(path) is TextureImporter imp)) return null;
                imp.textureType = TextureImporterType.Default; imp.mipmapEnabled = true; imp.npotScale = TextureImporterNPOTScale.None;
                imp.textureCompression = TextureImporterCompression.Uncompressed; imp.filterMode = FilterMode.Trilinear; imp.wrapMode = TextureWrapMode.Clamp;
                imp.maxTextureSize = 4096;
                imp.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            var map = Painted("map.png");
            hud.Map = map; hud.FullMap = Painted("map_labeled.png") ?? map;
            hud.Player = player; hud.RegionName = data.name;
            hud.MapBounds = new Vector4(0, 0, data.width, data.height);
            var named = data.places.Where(p => p.kind != "exit").ToArray();
            hud.PlaceNames = named.Select(p => p.name.Split('（')[0]).ToArray();
            hud.PlacePositions = named.Select(p => new Vector3(p.x, 0, p.z)).ToArray();
        }
    }
}
