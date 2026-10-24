using UnityEngine;

namespace ThreeKingdoms.Gameplay
{
    // Peach petals drifting down over the oath (user 2026-10-24): a few small camera-facing quads that fall, sway and
    // spin inside a box, and start again at the top when they reach the ground.
    public sealed class PetalFall : MonoBehaviour
    {
        public Material Material;
        public int Count = 36;
        public Vector3 Area = new Vector3(10f, 5f, 6f);
        [Tooltip("Petal size in metres")] public float Size = 0.16f;
        public float FallSpeed = 0.55f;

        Transform[] petals;
        float[] seeds;

        void OnEnable()
        {
            if (petals != null || !Material) return;
            petals = new Transform[Count];
            seeds = new float[Count];
            var rng = new System.Random(24);
            for (int i = 0; i < Count; i++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(q.GetComponent<Collider>());
                q.name = "petal";
                q.GetComponent<Renderer>().sharedMaterial = Material;
                q.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                q.transform.SetParent(transform, false);
                q.transform.localScale = Vector3.one * Size;
                seeds[i] = (float)rng.NextDouble() * 100f;
                q.transform.localPosition = new Vector3(((float)rng.NextDouble() - 0.5f) * Area.x, (float)rng.NextDouble() * Area.y, ((float)rng.NextDouble() - 0.5f) * Area.z);
                petals[i] = q.transform;
            }
        }

        void Update()
        {
            if (petals == null) return;
            var cam = UnityEngine.Camera.main;
            float t = Time.time;
            for (int i = 0; i < petals.Length; i++)
            {
                var p = petals[i];
                float s = seeds[i];
                var pos = p.localPosition;
                pos.y -= FallSpeed * (0.7f + 0.3f * Mathf.Sin(s)) * Time.deltaTime;
                pos.x += Mathf.Sin(t * 1.3f + s) * 0.4f * Time.deltaTime + 0.15f * Time.deltaTime;
                if (pos.y < 0f) { pos.y = Area.y; pos.x = (Mathf.Repeat(s * 7.3f + t, 1f) - 0.5f) * Area.x; }
                p.localPosition = pos;
                if (cam) p.rotation = cam.transform.rotation * Quaternion.Euler(0, 0, t * 90f * (0.5f + Mathf.Repeat(s, 1f)));
            }
        }
    }
}
