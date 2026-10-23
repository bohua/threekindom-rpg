using UnityEngine;
using UnityEngine.UIElements;

namespace ThreeKingdoms.UI.Story
{
    // A full-body illustration shown beside the dialogue box (user 2026-10-22), drawn as a subdivided mesh whose
    // vertices drift in a wind: the further a point lies from the figure's axis, the more it sways (sleeves, ribbons,
    // hair ends), while the face and body near the axis stay still. The lower edge fades out behind the dialogue box.
    public sealed class WindPortrait : VisualElement
    {
        const int Cols = 16, Rows = 24;

        Texture2D texture;
        bool flip;
        float cropBottom = 0.6f, axis = 0.5f;

        // Sway as a fraction of the element's width at full weight; 0 freezes the figure.
        public float Amplitude { get; set; } = 0.016f;
        public float Time { get; set; }
        public Texture2D Texture => texture;

        public WindPortrait()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        // cropBottom: how much of the picture, from the top, is shown (waist-up); axis: the figure's centre line in u.
        public void Show(Texture2D tex, bool mirrored, float crop, float figureAxis)
        {
            texture = tex; flip = mirrored; cropBottom = crop; axis = mirrored ? 1f - figureAxis : figureAxis;
            MarkDirtyRepaint();
        }

        public void Tick(float time) { Time = time; MarkDirtyRepaint(); }

        void Draw(MeshGenerationContext mgc)
        {
            var r = contentRect;
            if (!texture || r.width < 1 || r.height < 1) return;
            var mesh = mgc.Allocate((Cols + 1) * (Rows + 1), Cols * Rows * 6, texture);
            var region = mesh.uvRegion;
            float t = Time, amp = Amplitude * r.width;
            // A slow gust under two faster flutters travelling down the cloth.
            float gust = 0.55f + 0.45f * Mathf.Sin(t * 0.7f);
            for (int y = 0; y <= Rows; y++)
            {
                float v = (float)y / Rows;                       // 0 top .. 1 bottom of the shown part
                for (int x = 0; x <= Cols; x++)
                {
                    float u = (float)x / Cols;
                    float w = Smooth(0.07f, 0.42f, Mathf.Abs(u - axis));
                    float side = Mathf.Sign(u - axis);
                    float dx = amp * w * (gust * (0.6f * Mathf.Sin(t * 2.1f + v * 6f + u * 3f) + 0.4f * Mathf.Sin(t * 3.7f + v * 11f)) + 0.35f * gust * side);
                    float dy = amp * 0.35f * w * Mathf.Sin(t * 2.7f + u * 5f + v * 4f);
                    float su = flip ? 1f - u : u;
                    float sv = 1f - v * cropBottom;
                    float alpha = 1f - Smooth(0.82f, 1f, v);
                    mesh.SetNextVertex(new Vertex
                    {
                        position = new Vector3(r.x + u * r.width + dx, r.y + v * r.height + dy, Vertex.nearZ),
                        tint = new Color(1f, 1f, 1f, alpha),
                        uv = new Vector2(region.x + su * region.width, region.y + sv * region.height),
                    });
                }
            }
            for (int y = 0; y < Rows; y++)
                for (int x = 0; x < Cols; x++)
                {
                    ushort a = (ushort)(y * (Cols + 1) + x), b = (ushort)(a + 1), c = (ushort)(a + Cols + 1), d = (ushort)(c + 1);
                    mesh.SetNextIndex(a); mesh.SetNextIndex(b); mesh.SetNextIndex(c);
                    mesh.SetNextIndex(b); mesh.SetNextIndex(d); mesh.SetNextIndex(c);
                }
        }

        static float Smooth(float a, float b, float x)
        {
            float k = Mathf.Clamp01((x - a) / (b - a));
            return k * k * (3f - 2f * k);
        }
    }
}
