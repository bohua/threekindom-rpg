using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ThreeKingdoms.UI.Story
{
    // The 3D 青绿山水 world map of the prologue (user 2026-10-28: make the map a modelled, finely painted diorama and fly
    // the camera low through it while the story is told). Resources/Prologue/TianxiaDiorama (PrologueDioramaBuilder)
    // is placed far from the town and drawn by its own camera into a texture the prologue shows full screen. Map
    // coordinates are fractions of the 1920×1080 flat map (0..1 from the top left); the diorama is 120 × 67.5 m with
    // its origin at the south-west corner, so a map point (u, v) sits at (120u, h, 67.5(1 − v)). The story layers are
    // textures in the flat map's space, laid on the ground through overlay copies of the terrain.
    public sealed class PrologueDiorama : IDisposable
    {
        public const float Width = 120f, Depth = 67.5f;
        const int LayerIndex = 30;
        static readonly Vector3 Offset = new Vector3(6000f, 0f, 6000f);

        readonly GameObject root;
        readonly Camera cam;
        readonly RenderTexture target;
        readonly Dictionary<string, Material> overlays = new Dictionary<string, Material>();
        readonly Dictionary<string, Renderer> overlayRenderers = new Dictionary<string, Renderer>();
        readonly List<(Light light, bool on)> otherLights = new List<(Light, bool)>();
        readonly Light previousSun;

        public Texture Image => target;

        public static PrologueDiorama TryCreate()
        {
            var prefab = Resources.Load<GameObject>("Prologue/TianxiaDiorama");
            return prefab ? new PrologueDiorama(prefab) : null;
        }

        PrologueDiorama(GameObject prefab)
        {
            root = UnityEngine.Object.Instantiate(prefab, Offset, Quaternion.identity);
            root.name = "Prologue diorama";
            cam = root.GetComponentInChildren<Camera>(true);
            target = new RenderTexture(1920, 1080, 24) { name = "Prologue diorama", antiAliasing = 4 };
            cam.targetTexture = target;
            cam.enabled = true;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = true;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.name.StartsWith("Overlay ")) continue;
                string id = r.name.Substring(8);
                overlays[id] = r.material;   // an instance per run
                overlayRenderers[id] = r;
            }
            // The town's lights stay off while the diorama is up (the town is hidden under the prologue anyway), so the
            // diorama's own sun is the main light and casts its shadows.
            var sun = root.GetComponentInChildren<Light>(true);
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l != sun) { otherLights.Add((l, l.enabled)); l.enabled = false; }
            previousSun = RenderSettings.sun;
            RenderSettings.sun = sun;
        }

        // The camera at a place in diorama metres (x east, y up, z north), looking at another.
        public void View(Vector3 position, Vector3 lookAt, float fov)
        {
            cam.transform.position = Offset + position;
            cam.transform.rotation = Quaternion.LookRotation(lookAt - position, Vector3.up);
            cam.fieldOfView = fov;
        }

        // A map point (fractions from the top left) at a height, as a screen fraction from the top left, or null when
        // it is behind the camera or off screen.
        public Vector2? Project(Vector2 map, float height = 0.9f)
        {
            var p = cam.WorldToViewportPoint(Offset + ToWorld(map, height));
            if (p.z <= 0f || p.x < -0.1f || p.x > 1.1f || p.y < -0.1f || p.y > 1.1f) return null;
            return new Vector2(p.x, 1f - p.y);
        }

        // The height of the land or the peaks under a point (diorama metres), for flights that must clear the mountains
        // (TianxiaFlight): the half-metre grid PrologueDioramaBuilder samples, 0 where it is missing.
        public static float GroundAt(float x, float z)
        {
            if (heights == null)
            {
                var file = Resources.Load<TextAsset>("Prologue/tianxia_height");
                if (!file) { heights = new float[0]; return 0f; }
                var bytes = file.bytes;
                heightW = BitConverter.ToInt32(bytes, 0); heightD = BitConverter.ToInt32(bytes, 4);
                heights = new float[heightW * heightD];
                Buffer.BlockCopy(bytes, 8, heights, 0, heights.Length * 4);
            }
            if (heights.Length == 0) return 0f;
            float fx = Mathf.Clamp(x / 0.5f, 0f, heightW - 1.001f), fz = Mathf.Clamp(z / 0.5f, 0f, heightD - 1.001f);
            int ix = (int)fx, iz = (int)fz; float u = fx - ix, v = fz - iz;
            float H(int i, int j) => heights[j * heightW + i];
            return Mathf.Lerp(Mathf.Lerp(H(ix, iz), H(ix + 1, iz), u), Mathf.Lerp(H(ix, iz + 1), H(ix + 1, iz + 1), u), v);
        }

        static float[] heights;
        static int heightW, heightD;

        public static Vector3 ToWorld(Vector2 map, float height) => new Vector3(map.x * Width, height, (1f - map.y) * Depth);

        // A story layer laid on the ground (null texture or alpha 0 hides it).
        public void Overlay(string id, Texture texture, float alpha)
        {
            if (!overlays.TryGetValue(id, out var m)) return;
            bool on = texture && alpha > 0.001f;
            overlayRenderers[id].gameObject.SetActive(on);
            if (!on) return;
            if (m.GetTexture("_BaseMap") != texture) m.SetTexture("_BaseMap", texture);
            m.SetColor("_BaseColor", new Color(1f, 1f, 1f, Mathf.Clamp01(alpha)));
        }

        public void Dispose()
        {
            foreach (var (l, on) in otherLights) if (l) l.enabled = on;
            RenderSettings.sun = previousSun;
            if (cam) cam.targetTexture = null;
            target.Release();
            UnityEngine.Object.Destroy(target);
            foreach (var m in overlays.Values) UnityEngine.Object.Destroy(m);
            UnityEngine.Object.Destroy(root);
        }

        // ---------- camera routes (Codex art/prologue-3d-map-blender _checks/anchors.json, converted at import) ----------

        public readonly struct Pose
        {
            public readonly Vector3 Position, LookAt; public readonly float Fov;
            public Pose(Vector3 position, Vector3 lookAt, float fov) { Position = position; LookAt = lookAt; Fov = fov; }
            public static Pose Lerp(Pose a, Pose b, float k) =>
                new Pose(Vector3.Lerp(a.Position, b.Position, k), Vector3.Lerp(a.LookAt, b.LookAt, k), Mathf.Lerp(a.Fov, b.Fov, k));
        }

        [Serializable] class RouteFile { public Route[] routes = Array.Empty<Route>(); }
        [Serializable] public class Route { public string id; public float fov = 50f; public Vector3[] positions = Array.Empty<Vector3>(), looks = Array.Empty<Vector3>(); }

        static Dictionary<string, Route> routes;

        public static Route RouteOf(string id)
        {
            if (routes == null)
            {
                routes = new Dictionary<string, Route>();
                var file = Resources.Load<TextAsset>("Prologue/tianxia_routes");
                if (file)
                    foreach (var r in JsonUtility.FromJson<RouteFile>(file.text).routes)
                        if (r.positions.Length > 1 && r.positions.Length == r.looks.Length) routes[r.id] = r;
            }
            return routes.TryGetValue(id, out var route) ? route : null;
        }

        // Along a route at k (0..1), smoothed through its points (Catmull-Rom).
        public static Pose Along(Route r, float k)
        {
            float f = Mathf.Clamp01(k) * (r.positions.Length - 1);
            int i = Mathf.Min((int)f, r.positions.Length - 2);
            float u = f - i;
            return new Pose(CatmullRom(r.positions, i, u), CatmullRom(r.looks, i, u), r.fov);
        }

        static Vector3 CatmullRom(Vector3[] p, int i, float u)
        {
            Vector3 p0 = p[Mathf.Max(i - 1, 0)], p1 = p[i], p2 = p[i + 1], p3 = p[Mathf.Min(i + 2, p.Length - 1)];
            float u2 = u * u, u3 = u2 * u;
            return 0.5f * (2f * p1 + (p2 - p0) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (3f * p1 - p0 - 3f * p2 + p3) * u3);
        }
    }
}
