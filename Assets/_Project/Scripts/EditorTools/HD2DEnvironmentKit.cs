using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThreeKingdoms.EditorTools
{
    // Modular low-poly building kit for the HD-2D sample (house, city wall, gate tower, ground, props).
    // Meshes carry world-scale UVs so every surface shows the same texel density as the characters
    // (ADR-0005: 30 px per unit); textures come from HD2DTexturePainter and are point-filtered.
    public static class HD2DEnvironmentKit
    {
        const string Root = "Assets/_Project/Art/Environment";
        const string TextureDir = Root + "/Textures";
        const string MaterialDir = Root + "/Materials";
        public const string MeshAssetPath = Root + "/HD2D_EnvMeshes.asset";

        public sealed class Palette
        {
            public Material Plaster, RammedEarth, Timber, Roof, Thatch, Paving, Dirt, Door, Lattice, Leaves, Blossom, Lantern, Banner, Sign;
        }

        static Mesh meshContainer;
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

        public static Palette CreatePalette()
        {
            Directory.CreateDirectory(TextureDir);
            Directory.CreateDirectory(MaterialDir);
            BeginMeshes();
            return new Palette
            {
                Plaster = Mat("Plaster", HD2DTexturePainter.Plaster(), tiled: true),
                RammedEarth = Mat("RammedEarth", HD2DTexturePainter.RammedEarth(), tiled: true),
                Timber = Mat("Timber", HD2DTexturePainter.Timber(), tiled: true),
                Roof = Mat("RoofTile", HD2DTexturePainter.RoofTile(), tiled: true),
                Thatch = Mat("Thatch", HD2DTexturePainter.Thatch(), tiled: true),
                Paving = Mat("Paving", HD2DTexturePainter.Paving(), tiled: true),
                Dirt = Mat("Dirt", HD2DTexturePainter.Dirt(), tiled: true),
                Door = Mat("DoorPlanks", HD2DTexturePainter.DoorPlanks(), tiled: false),
                Lattice = Mat("Lattice", HD2DTexturePainter.Lattice(), tiled: false),
                Leaves = Mat("Leaves", HD2DTexturePainter.Leaves(), tiled: true),
                Blossom = Mat("PeachBlossom", HD2DTexturePainter.Blossom(), tiled: true),
                Lantern = Mat("Lantern", HD2DTexturePainter.Lantern(), tiled: false, emission: 0.9f),
                Banner = Mat("WineBanner", HD2DTexturePainter.WineBanner(), tiled: false, alphaClip: true),
                Sign = Mat("Signboard", HD2DTexturePainter.Signboard(), tiled: false),
            };
        }

        public static void EndMeshes() => AssetDatabase.SaveAssets();

        // ---------- textures & materials ----------

        static Material Mat(string name, HD2DTexturePainter.Canvas canvas, bool tiled, float emission = 0f, bool alphaClip = false)
        {
            string texturePath = $"{TextureDir}/{name}.png";
            var tex = new Texture2D(canvas.Width, canvas.Height, TextureFormat.RGBA32, false);
            tex.SetPixels32(canvas.Pixels);
            tex.Apply();
            File.WriteAllBytes(texturePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = tiled;                 // tiled surfaces recede into the distance; mips stop shimmering
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = tiled ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.alphaIsTransparency = alphaClip;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);

            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            m.SetTexture("_BaseMap", texture);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.05f);
            m.SetFloat("_SpecularHighlights", 0f); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            // World-scale UVs advance 1 per TileWorldSize units; scale so one texture pixel = 1/30 unit.
            if (tiled)
                m.SetTextureScale("_BaseMap", new Vector2(HD2DTexturePainter.TilePixels / (float)canvas.Width, HD2DTexturePainter.TilePixels / (float)canvas.Height));
            if (emission > 0f)
            {
                m.SetTexture("_EmissionMap", texture);
                m.SetColor("_EmissionColor", Color.white * emission);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            if (alphaClip)
            {
                m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Cull", (float)CullMode.Off);
                m.renderQueue = (int)RenderQueue.AlphaTest;
            }
            string path = $"{MaterialDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing)
            {
                existing.shader = m.shader;
                existing.CopyPropertiesFromMaterial(m);
                existing.shaderKeywords = m.shaderKeywords;
                existing.renderQueue = m.renderQueue;
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(m);
                return existing;
            }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // ---------- meshes ----------

        static void BeginMeshes()
        {
            meshes.Clear();
            if (AssetDatabase.LoadAssetAtPath<Mesh>(MeshAssetPath)) AssetDatabase.DeleteAsset(MeshAssetPath);
            meshContainer = new Mesh { name = "HD2D_EnvMeshes" };
            AssetDatabase.CreateAsset(meshContainer, MeshAssetPath);
        }

        static Mesh Store(string key, System.Func<Mesh> build)
        {
            if (meshes.TryGetValue(key, out var mesh)) return mesh;
            mesh = build();
            mesh.name = key;
            mesh.RecalculateBounds();
            AssetDatabase.AddObjectToAsset(mesh, meshContainer);
            meshes[key] = mesh;
            return mesh;
        }

        // Axis-aligned box centred on the origin. World-scale UVs (tiled) or 0–1 per face (fit).
        public static Mesh BoxMesh(Vector3 size, bool fit)
        {
            string key = $"Box_{size.x:0.###}x{size.y:0.###}x{size.z:0.###}{(fit ? "_fit" : "")}";
            return Store(key, () =>
            {
                Vector3 h = size / 2f;
                var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uvs = new List<Vector2>(); var triangles = new List<int>();
                // (normal, u axis, v axis) per face; v is "up" on walls so texture rows stay horizontal.
                var faces = new[]
                {
                    (Vector3.forward, Vector3.left, Vector3.up), (Vector3.back, Vector3.right, Vector3.up),
                    (Vector3.right, Vector3.forward, Vector3.up), (Vector3.left, Vector3.back, Vector3.up),
                    (Vector3.up, Vector3.right, Vector3.forward), (Vector3.down, Vector3.right, Vector3.back),
                };
                foreach (var (n, u, v) in faces)
                {
                    int start = vertices.Count;
                    float uLen = Mathf.Abs(Vector3.Dot(size, u)), vLen = Mathf.Abs(Vector3.Dot(size, v));
                    Vector3 c = Vector3.Scale(n, h);
                    for (int i = 0; i < 4; i++)
                    {
                        float su = i == 1 || i == 2 ? 1 : 0, sv = i >= 2 ? 1 : 0;
                        vertices.Add(c + u * (su - 0.5f) * uLen + v * (sv - 0.5f) * vLen);
                        normals.Add(n);
                        uvs.Add(fit ? new Vector2(su, sv) : new Vector2(su * uLen, sv * vLen) / HD2DTexturePainter.TileWorldSize);
                    }
                    triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
                }
                var mesh = new Mesh();
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uvs); mesh.SetTriangles(triangles, 0);
                mesh.RecalculateTangents();
                return mesh;
            });
        }

        // Triangular prism for gable ends: base along Z (width), apex up (height), thickness along X.
        static Mesh GableMesh(float width, float height, float thickness)
        {
            string key = $"Gable_{width:0.###}x{height:0.###}x{thickness:0.###}";
            return Store(key, () =>
            {
                float w = width / 2f, t = thickness / 2f;
                var p = new[] { new Vector3(0, 0, -w), new Vector3(0, 0, w), new Vector3(0, height, 0) };
                var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
                void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 n)
                {
                    int s = vertices.Count;
                    foreach (var q in new[] { a, b, c }) { vertices.Add(q); normals.Add(n); uvs.Add(new Vector2(q.z, q.y) / HD2DTexturePainter.TileWorldSize); }
                    tris.AddRange(new[] { s, s + 1, s + 2 });
                }
                Vector3 ox = new Vector3(t, 0, 0);
                Tri(p[0] + ox, p[2] + ox, p[1] + ox, Vector3.right);
                Tri(p[0] - ox, p[1] - ox, p[2] - ox, Vector3.left);
                var mesh = new Mesh();
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
                return mesh;
            });
        }

        public static GameObject Piece(Transform parent, string name, Vector3 center, Vector3 size, Material material, Vector3 euler = default, bool fit = false, bool collider = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size, fit);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.On;
            if (collider) go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        static Transform Group(Transform parent, string name, Vector3 position, float yaw)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = position;
            t.localRotation = Quaternion.Euler(0, yaw, 0);
            return t;
        }

        // ---------- buildings ----------

        public enum HouseStyle { Tiled, Thatched, Tavern }

        // Front faces local -Z; yaw turns the front toward the street.
        public static Transform House(Transform parent, string name, Vector3 position, float yaw, float width, HouseStyle style, Palette p)
        {
            var house = Group(parent, name, position, yaw);
            const float depth = 4.2f, wallH = 2.4f, plinth = 0.3f, eave = 0.6f, slope = 27f;
            float top = plinth + wallH;
            Piece(house, "Plinth", new Vector3(0, plinth / 2, 0), new Vector3(width + 0.3f, plinth, depth + 0.3f), p.Paving);
            Piece(house, "Walls", new Vector3(0, plinth + wallH / 2, 0), new Vector3(width, wallH, depth), style == HouseStyle.Thatched ? p.RammedEarth : p.Plaster);

            // Timber frame: corner and intermediate posts on the front and back, a beam band under the eaves.
            int bays = Mathf.Max(2, Mathf.RoundToInt(width / 2f));
            for (int i = 0; i <= bays; i++)
            {
                float x = -width / 2 + width * i / bays;
                Piece(house, "Post", new Vector3(x, plinth + wallH / 2, -depth / 2 - 0.06f), new Vector3(0.22f, wallH, 0.14f), p.Timber, collider: false);
                Piece(house, "Post", new Vector3(x, plinth + wallH / 2, depth / 2 + 0.06f), new Vector3(0.22f, wallH, 0.14f), p.Timber, collider: false);
            }
            foreach (float z in new[] { -depth / 2 - 0.08f, depth / 2 + 0.08f })
                Piece(house, "Beam", new Vector3(0, top - 0.1f, z), new Vector3(width + 0.2f, 0.2f, 0.18f), p.Timber, collider: false);

            Piece(house, "Door", new Vector3(0, plinth + 0.95f, -depth / 2 - 0.05f), new Vector3(1.1f, 1.9f, 0.08f), p.Door, fit: true, collider: false);
            Piece(house, "Lintel", new Vector3(0, plinth + 1.98f, -depth / 2 - 0.1f), new Vector3(1.5f, 0.16f, 0.14f), p.Timber, collider: false);
            float windowX = width / 4f + 0.35f;
            if (width >= 4.5f)
                foreach (float x in new[] { -windowX, windowX })
                    Piece(house, "Window", new Vector3(x, plinth + 1.45f, -depth / 2 - 0.04f), new Vector3(0.9f, 0.8f, 0.06f), p.Lattice, fit: true, collider: false);

            // Gable roof: ridge along local X, eaves overhang on all sides, gable triangles close the ends.
            float rad = slope * Mathf.Deg2Rad, halfRun = depth / 2 + eave, rise = Mathf.Tan(rad) * (depth / 2);
            float ridgeY = top + rise, slab = halfRun / Mathf.Cos(rad), roofW = width + 2 * eave;
            var roofMat = style == HouseStyle.Thatched ? p.Thatch : p.Roof;
            float thickness = style == HouseStyle.Thatched ? 0.3f : 0.16f;
            for (int side = -1; side <= 1; side += 2)
            {
                float cz = side * halfRun / 2, cy = ridgeY - Mathf.Tan(rad) * halfRun / 2 + thickness / 2;
                Piece(house, side < 0 ? "Roof front" : "Roof back", new Vector3(0, cy, cz), new Vector3(roofW, thickness, slab), roofMat, new Vector3(side * slope, 0, 0), collider: false);
            }
            foreach (float x in new[] { -width / 2, width / 2 })
            {
                var gable = new GameObject("Gable");
                gable.transform.SetParent(house, false);
                gable.transform.localPosition = new Vector3(x, top, 0);
                gable.AddComponent<MeshFilter>().sharedMesh = GableMesh(depth, rise, 0.2f);
                gable.AddComponent<MeshRenderer>().sharedMaterial = style == HouseStyle.Thatched ? p.RammedEarth : p.Plaster;
            }
            if (style != HouseStyle.Thatched)
            {
                Piece(house, "Ridge", new Vector3(0, ridgeY + 0.14f, 0), new Vector3(roofW + 0.2f, 0.24f, 0.34f), p.Roof, collider: false);
                // Ridge-end ornaments and upturned eave corners give the silhouette its Han-dynasty read.
                foreach (int s in new[] { -1, 1 })
                {
                    Piece(house, "Ridge ornament", new Vector3(s * (roofW / 2 + 0.05f), ridgeY + 0.38f, 0), new Vector3(0.2f, 0.5f, 0.24f), p.Roof, new Vector3(0, 0, -s * 22f), collider: false);
                    foreach (int f in new[] { -1, 1 })
                        Piece(house, "Eave corner", new Vector3(s * (roofW / 2 - 0.25f), top - Mathf.Tan(rad) * eave + 0.12f, f * (halfRun - 0.15f)),
                            new Vector3(0.8f, 0.12f, 0.5f), p.Roof, new Vector3(f * slope * 0.5f, s * 35f * f, s * 16f), collider: false);
                }
            }
            if (style == HouseStyle.Tavern)
            {
                Piece(house, "Signboard", new Vector3(0, top + 0.05f, -depth / 2 - 0.2f), new Vector3(1.8f, 0.6f, 0.08f), p.Sign, fit: true, collider: false);
                Piece(house, "Banner pole", new Vector3(width / 2 + 0.6f, 1.6f, -depth / 2 - 0.6f), new Vector3(0.1f, 3.2f, 0.1f), p.Timber, collider: false);
                Piece(house, "Banner arm", new Vector3(width / 2 + 0.6f, 3.1f, -depth / 2 - 0.95f), new Vector3(0.08f, 0.08f, 0.8f), p.Timber, collider: false);
                Piece(house, "Wine banner", new Vector3(width / 2 + 0.6f, 2.45f, -depth / 2 - 1.05f), new Vector3(0.02f, 1.2f, 0.6f), p.Banner, fit: true, collider: false);
            }
            return house;
        }

        // City wall along local X with crenellations on the camera side (local -Z).
        public static Transform CityWall(Transform parent, string name, Vector3 position, float length, Palette p)
        {
            var wall = Group(parent, name, position, 0);
            const float height = 3.6f, thickness = 2.4f;
            Piece(wall, "Rammed earth", new Vector3(0, height / 2, 0), new Vector3(length, height, thickness), p.RammedEarth);
            Piece(wall, "Walkway", new Vector3(0, height + 0.03f, 0), new Vector3(length, 0.06f, thickness - 0.4f), p.Paving, collider: false);
            for (float x = -length / 2 + 0.5f; x <= length / 2 - 0.4f; x += 1.4f)
                Piece(wall, "Merlon", new Vector3(x, height + 0.35f, -thickness / 2 + 0.2f), new Vector3(0.8f, 0.7f, 0.4f), p.RammedEarth, collider: false);
            Piece(wall, "Parapet", new Vector3(0, height + 0.2f, thickness / 2 - 0.15f), new Vector3(length, 0.4f, 0.3f), p.RammedEarth, collider: false);
            return wall;
        }

        // Gate tower: two rammed-earth piers, an open timber gate, a platform and a timber-framed pavilion.
        public static Transform GateTower(Transform parent, Vector3 position, Palette p)
        {
            var gate = Group(parent, "Gate tower", position, 0);
            const float pierH = 4.2f, depth = 3.2f, opening = 3.6f, pierW = 2.6f;
            foreach (int s in new[] { -1, 1 })
            {
                Piece(gate, "Pier", new Vector3(s * (opening / 2 + pierW / 2), pierH / 2, 0), new Vector3(pierW, pierH, depth), p.RammedEarth);
                Piece(gate, "Gate leaf", new Vector3(s * (opening / 2 - 0.15f), 1.6f, depth / 2 - 0.9f), new Vector3(1.6f, 3.2f, 0.12f), p.Door, new Vector3(0, s * 70f, 0), fit: true, collider: false);
            }
            Piece(gate, "Gate beam", new Vector3(0, pierH - 0.3f, 0), new Vector3(opening + 0.4f, 0.6f, depth), p.Timber, collider: false);
            float deck = pierH;
            Piece(gate, "Platform", new Vector3(0, deck + 0.15f, 0), new Vector3(opening + 2 * pierW, 0.3f, depth + 0.2f), p.Paving, collider: false);

            var pavilion = Group(gate, "Pavilion", new Vector3(0, deck + 0.3f, 0.2f), 0);
            const float pw = 6.4f, pd = 2.6f, ph = 2.0f, eave = 0.9f, slope = 26f;
            Piece(pavilion, "Walls", new Vector3(0, ph / 2, 0), new Vector3(pw, ph, pd), p.Plaster, collider: false);
            for (int i = 0; i <= 4; i++)
                Piece(pavilion, "Post", new Vector3(-pw / 2 + pw * i / 4, ph / 2, -pd / 2 - 0.06f), new Vector3(0.24f, ph, 0.14f), p.Timber, collider: false);
            for (int i = 0; i < 4; i++)
                Piece(pavilion, "Window", new Vector3(-pw / 2 + pw * (i + 0.5f) / 4, ph * 0.55f, -pd / 2 - 0.04f), new Vector3(1.1f, 0.8f, 0.06f), p.Lattice, fit: true, collider: false);
            Piece(pavilion, "Beam", new Vector3(0, ph - 0.1f, -pd / 2 - 0.08f), new Vector3(pw + 0.2f, 0.2f, 0.18f), p.Timber, collider: false);
            float rad = slope * Mathf.Deg2Rad, halfRun = pd / 2 + eave, rise = Mathf.Tan(rad) * (pd / 2), ridgeY = ph + rise, roofW = pw + 2 * eave;
            for (int side = -1; side <= 1; side += 2)
                Piece(pavilion, "Roof", new Vector3(0, ridgeY - Mathf.Tan(rad) * halfRun / 2 + 0.08f, side * halfRun / 2), new Vector3(roofW, 0.16f, halfRun / Mathf.Cos(rad)), p.Roof, new Vector3(side * slope, 0, 0), collider: false);
            foreach (float x in new[] { -pw / 2, pw / 2 })
            {
                var g = new GameObject("Gable");
                g.transform.SetParent(pavilion, false);
                g.transform.localPosition = new Vector3(x, ph, 0);
                g.AddComponent<MeshFilter>().sharedMesh = GableMesh(pd, rise, 0.2f);
                g.AddComponent<MeshRenderer>().sharedMaterial = p.Plaster;
            }
            Piece(pavilion, "Ridge", new Vector3(0, ridgeY + 0.14f, 0), new Vector3(roofW + 0.2f, 0.26f, 0.36f), p.Roof, collider: false);
            foreach (int s in new[] { -1, 1 })
            {
                Piece(pavilion, "Ridge ornament", new Vector3(s * (roofW / 2 + 0.05f), ridgeY + 0.42f, 0), new Vector3(0.22f, 0.6f, 0.26f), p.Roof, new Vector3(0, 0, -s * 22f), collider: false);
                foreach (int f in new[] { -1, 1 })
                    Piece(pavilion, "Eave corner", new Vector3(s * (roofW / 2 - 0.3f), ph - Mathf.Tan(rad) * eave + 0.14f, f * (halfRun - 0.2f)),
                        new Vector3(1.0f, 0.14f, 0.6f), p.Roof, new Vector3(f * slope * 0.5f, s * 35f * f, s * 18f), collider: false);
            }
            return gate;
        }

        // ---------- ground & props ----------

        public static void Ground(Transform parent, Palette p)
        {
            Piece(parent, "Ground", new Vector3(0, -0.05f, 20), new Vector3(160, 0.1f, 160), p.Dirt);
            Piece(parent, "Main street", new Vector3(0, 0.02f, 10), new Vector3(5, 0.04f, 52), p.Paving, collider: false);
        }

        public static void Lantern(Transform parent, Vector3 at, Palette p)
        {
            Piece(parent, "Lantern post", at + new Vector3(0, 1.1f, 0), new Vector3(0.12f, 2.2f, 0.12f), p.Timber, collider: false);
            Piece(parent, "Lantern arm", at + new Vector3(0, 2.25f, -0.2f), new Vector3(0.08f, 0.08f, 0.5f), p.Timber, collider: false);
            Piece(parent, "Lantern", at + new Vector3(0, 1.95f, -0.4f), new Vector3(0.34f, 0.44f, 0.34f), p.Lantern, fit: true, collider: false);
            var light = new GameObject("Lantern light").AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.localPosition = at + new Vector3(0, 1.95f, -0.4f);
            light.type = LightType.Point;
            light.range = 6f;
            light.intensity = 2.2f;
            light.color = new Color(1f, 0.58f, 0.28f);
            light.shadows = LightShadows.None;
        }

        public static void Stall(Transform parent, Vector3 at, float yaw, Palette p)
        {
            var stall = Group(parent, "Market stall", at, yaw);
            Piece(stall, "Counter", new Vector3(0, 0.45f, 0), new Vector3(1.8f, 0.9f, 1.0f), p.Timber);
            for (int i = 0; i < 4; i++)
                Piece(stall, "Post", new Vector3(i % 2 == 0 ? -0.85f : 0.85f, 1.1f, i < 2 ? -0.45f : 0.45f), new Vector3(0.08f, 2.2f, 0.08f), p.Timber, collider: false);
            Piece(stall, "Awning", new Vector3(0, 2.25f, 0), new Vector3(2.3f, 0.1f, 1.5f), p.Thatch, new Vector3(-10f, 0, 0), collider: false);
        }

        public static void Well(Transform parent, Vector3 at, Palette p)
        {
            var well = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            well.name = "Well"; well.transform.SetParent(parent, false);
            well.transform.localPosition = at + new Vector3(0, 0.4f, 0);
            well.transform.localScale = new Vector3(1.4f, 0.4f, 1.4f);
            well.GetComponent<Renderer>().sharedMaterial = p.Paving;
            foreach (int s in new[] { -1, 1 })
                Piece(parent, "Well post", at + new Vector3(s * 0.75f, 1.0f, 0), new Vector3(0.12f, 1.2f, 0.12f), p.Timber, collider: false);
            Piece(parent, "Well beam", at + new Vector3(0, 1.6f, 0), new Vector3(1.7f, 0.12f, 0.12f), p.Timber, collider: false);
        }

        public static void Tree(Transform parent, Vector3 at, Palette p, Material crownOverride = null)
        {
            Piece(parent, "Tree trunk", at + new Vector3(0, 1.2f, 0), new Vector3(0.36f, 2.4f, 0.36f), p.Timber);
            var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.name = "Tree crown"; crown.transform.SetParent(parent, false);
            crown.transform.localPosition = at + new Vector3(0, 3.0f, 0);
            crown.transform.localScale = new Vector3(2.6f, 2.2f, 2.6f);
            Object.DestroyImmediate(crown.GetComponent<Collider>());
            crown.GetComponent<Renderer>().sharedMaterial = crownOverride ? crownOverride : p.Leaves;
        }
    }
}
