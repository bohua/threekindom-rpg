using System;
using System.Collections.Generic;
using UnityEngine;

namespace ThreeKingdoms.UI.Story
{
    // The march arrow on the 3D 天下图 (user 2026-10-30: "用箭头和一些动画来显示这个马车或者军队正在行进"): a brush-stroke
    // ribbon laid on the land along the route, drawn out to a length and flowing forward, with its head at the tip.
    // Art: Codex art/tianxia-liu-march (march_arrow_body tiles along the road, march_arrow_head); plain colour without it.
    public sealed class TianxiaArrow : IDisposable
    {
        const float Width = 0.34f;      // metres on the diorama (a route between regions is some 6–20 m)
        const float Lift = 0.10f;       // above the ground it follows
        static readonly Color Plain = new Color(0.85f, 0.38f, 0.17f, 0.95f);

        readonly List<Vector3> points = new List<Vector3>();   // diorama metres, densified, on the ground
        readonly List<float> along = new List<float>();
        readonly Mesh mesh;
        readonly GameObject body, head;
        readonly Material bodyMat, headMat;
        readonly float tileLength;
        readonly Color bodyColor, headColor;

        public float Length => along.Count > 0 ? along[along.Count - 1] : 0f;

        public TianxiaArrow(PrologueDiorama map, IReadOnlyList<Vector3> route, float trim = 0f)
        {
            // Every 0.15 m, so the ribbon follows the rise and fall of the land.
            for (int i = 1; i < route.Count; i++)
            {
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(route[i - 1], route[i]) / 0.15f));
                for (int j = i == 1 ? 0 : 1; j <= n; j++)
                {
                    var p = Vector3.Lerp(route[i - 1], route[i], j / (float)n);
                    p.y = PrologueDiorama.GroundAt(p.x, p.z) + Lift;
                    points.Add(p);
                }
            }
            // Smooth the heights a little: the grid steps by half a metre.
            for (int pass = 0; pass < 2; pass++)
                for (int i = 1; i < points.Count - 1; i++)
                    points[i] = new Vector3(points[i].x, Mathf.Max(points[i].y, (points[i - 1].y + points[i].y + points[i + 1].y) / 3f), points[i].z);
            // Starts and ends outside the city walls (the cities stand on the land, the arrow would cross their roofs).
            if (trim > 0f && points.Count > 2)
            {
                Vector3 first = points[0], last = points[points.Count - 1];
                points.RemoveAll(q => Flat(q - first) < trim || Flat(q - last) < trim);
                if (points.Count < 2) { points.Clear(); points.Add(first); points.Add(last); }
            }
            float run = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                if (i > 0) run += Vector3.Distance(points[i - 1], points[i]);
                along.Add(run);
            }

            var bodyTex = Resources.Load<Texture2D>("Prologue/march_arrow_body");
            var headTex = Resources.Load<Texture2D>("Prologue/march_arrow_head");
            tileLength = bodyTex ? Width * bodyTex.width / bodyTex.height : Width * 4f;
            bodyMat = map.NewOverlayMaterial(bodyTex ? bodyTex : Texture2D.whiteTexture);
            headMat = map.NewOverlayMaterial(headTex ? headTex : Texture2D.whiteTexture);
            bodyColor = bodyTex ? Color.white : Plain; headColor = headTex ? Color.white : Plain;

            mesh = new Mesh { name = "march arrow" };
            mesh.MarkDynamic();
            body = Piece(map, "March arrow", mesh, bodyMat);
            head = Piece(map, "March arrow head", HeadMesh(), headMat);
            Draw(0f, 0f);
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        static GameObject Piece(PrologueDiorama map, string name, Mesh m, Material mat)
        {
            var go = new GameObject(name) { layer = PrologueDiorama.Layer };
            go.transform.SetParent(map.Root, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        // The head: a flat square on the ground pointing along +x, its back edge at the origin.
        static Mesh HeadMesh()
        {
            float s = Width * 2.3f;
            var m = new Mesh { name = "march arrow head" };
            m.vertices = new[] { new Vector3(-s * 0.35f, 0, -s / 2), new Vector3(-s * 0.35f, 0, s / 2), new Vector3(s * 0.65f, 0, s / 2), new Vector3(s * 0.65f, 0, -s / 2) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateBounds();
            return m;
        }

        // The point and heading at a distance along the arrow.
        public (Vector3 at, Vector3 dir) At(float distance)
        {
            if (points.Count < 2) return (points.Count > 0 ? points[0] : Vector3.zero, Vector3.forward);
            int i = 1;
            while (i < points.Count - 1 && along[i] < distance) i++;
            float u = Mathf.InverseLerp(along[i - 1], along[i], distance);
            var dir = points[i] - points[i - 1]; dir.y = 0f;
            return (Vector3.Lerp(points[i - 1], points[i], u), dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward);
        }

        // p 0..1: how far the arrow is drawn; flow: seconds, scrolling the brush texture forward; a: opacity.
        public void Draw(float p, float flow, float a = 1f)
        {
            float length = Length * Mathf.Clamp01(p);
            float headBack = Width * 0.8f;   // the body stops under the head
            float bodyEnd = Mathf.Max(0f, length - headBack);
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            for (int i = 0; i < points.Count; i++)
            {
                float d = Mathf.Min(along[i], bodyEnd);
                var (at, dir) = At(d);
                var side = Vector3.Cross(Vector3.up, dir) * (Width * 0.5f);
                // A narrower tail where the march sets out.
                float w = Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(d / (Width * 3f)));
                verts.Add(at - side * w); verts.Add(at + side * w);
                float u = d / tileLength;
                uvs.Add(new Vector2(u, 0f)); uvs.Add(new Vector2(u, 1f));
                if (i > 0) { int b = verts.Count - 4; tris.AddRange(new[] { b, b + 2, b + 3, b, b + 3, b + 1 }); }
                if (along[i] >= bodyEnd) break;
            }
            mesh.Clear();
            if (verts.Count >= 4) { mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0); mesh.RecalculateBounds(); }
            if (bodyMat)
            {
                bodyMat.SetTextureOffset("_BaseMap", new Vector2(-flow * 0.8f, 0f));
                var c = bodyColor; c.a *= a; bodyMat.SetColor("_BaseColor", c);
            }
            bool shown = length > Width * 0.5f && a > 0.01f;
            body.SetActive(shown); head.SetActive(shown);
            if (!shown) return;
            var (tip, heading) = At(Mathf.Max(0f, length - headBack));
            head.transform.localPosition = tip + Vector3.up * 0.01f;
            head.transform.localRotation = Quaternion.FromToRotation(Vector3.right, heading);
            if (headMat) { var c = headColor; c.a *= a; headMat.SetColor("_BaseColor", c); }
        }

        public void Dispose()
        {
            if (body) UnityEngine.Object.Destroy(body);
            if (head) UnityEngine.Object.Destroy(head);
            UnityEngine.Object.Destroy(mesh);
            if (bodyMat) UnityEngine.Object.Destroy(bodyMat);
            if (headMat) UnityEngine.Object.Destroy(headMat);
        }
    }
}
