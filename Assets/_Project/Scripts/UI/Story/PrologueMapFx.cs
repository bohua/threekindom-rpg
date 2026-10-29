using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThreeKingdoms.UI.Story
{
    // The moving marks on the prologue's world map (user 2026-10-28: the marks were too quiet; Yellow Turban banners
    // should wave and creep out to every corner, the held land deepen in colour and burn at its edge, Luo Yang be ringed
    // with one stroke of cinnabar, and the armies' routes drawn as thick arrows). Art from Codex art/prologue-map-fx
    // and art/prologue-art: the held-land layer (map_states), a waving banner strip, a fire strip, the cinnabar ring and
    // the thick arrows. The spreading, the ring and the arrows are drawn into small textures each frame from a
    // per-pixel "when does this pixel arrive" map built once; missing art falls back to plain shapes.
    public sealed class PrologueMapFx
    {
        // Working resolution of the layer being built: the held land at half the 1920×1080 map (a soft fill), the ring and
        // the arrows at full size so they stay crisp when the 3D camera flies low over them (user 2026-10-28).
        static int W = 960, H = 540;
        const int BannerFrames = 8, FireFrames = 8;
        const float FrameRate = 10f;

        static readonly Color32 Held = new Color32(214, 168, 52, 255), Deep = new Color32(150, 78, 24, 255);
        static readonly Color32 Ember = new Color32(255, 150, 40, 255), Flame = new Color32(220, 52, 22, 255);

        readonly Func<string, Vector2> anchor;
        Reveal spread, ring, arrows;
        readonly List<(VisualElement v, float at, Vector2 pos)> banners = new List<(VisualElement, float, Vector2)>();
        readonly List<(VisualElement v, float at, Vector2 pos)> fires = new List<(VisualElement, float, Vector2)>();

        // The 3D map (PrologueDiorama): the drawn layers go onto its ground instead of the flat map, and the banners
        // and fires follow the camera. Null on the flat map.
        public PrologueDiorama Diorama;
        Sprite[] bannerFrames, fireFrames;

        public PrologueMapFx(VisualElement mapInner, Func<string, Vector2> anchor)
        {
            this.anchor = anchor;
            var states = Load("map_states");
            spread = new Reveal(mapInner, states ? Sample(states) : Blob(anchor("julu"), 0.33f));
            spread.Order = SpreadOrder(spread.Source, anchor("julu"));

            W = 1920; H = 1080;
            var ringArt = Load("cinnabar_circle");
            var c = anchor("luoyang");
            ring = new Reveal(mapInner, ringArt ? Sample(ringArt) : RingStroke(c, 75f / 1920f, 7));
            var ringPath = CentreLines("cinnabar_circle");   // follows the stroke, overshoot included
            ring.Order = ringPath.Count > 0 ? PathOrder(ring.Source, ringPath) : AngleOrder(ring.Source, c);

            var arrowArt = Load("march_arrows");
            var paths = CentreLines("march_arrows");
            if (paths.Count == 0) paths = DefaultArrows();
            arrows = new Reveal(mapInner, arrowArt ? Sample(arrowArt) : ArrowStrokes(paths));
            arrows.Order = PathOrder(arrows.Source, paths);
            W = 960; H = 540;

            armyPaths = paths;
            marchFrames = Slice(Load("han_army_march"), 8);
            dustFrames = Slice(Load("han_army_dust"), 6);
            for (int i = 0; i < Generals.Length && i < paths.Count; i++)
            {
                var pin = GeneralPin(mapInner, Generals[i]);
                // 皇甫嵩 and 朱儁 meet at 颍川: 朱儁 (the southern road) carries his medal beside his column.
                if (i == 2) { pin.style.marginLeft = 50; pin.style.marginTop = -120; }
                armies.Add((Mark(mapInner, paths[i][0], 120, 96, false), Mark(mapInner, paths[i][0], 96, 72, false), pin));
            }

            bannerFrames = Slice(Load("banner_wave"), BannerFrames);
            fireFrames = Slice(Load("fire_loop"), FireFrames);
            var spots = Spots(spread, 16);
            foreach (var (pos, at) in spots) banners.Add((Mark(mapInner, pos, 64, 80, true), at, pos));
            for (int i = 0; i < spots.Count; i += 2)
            {
                var pos = spots[i].pos + new Vector2(0.012f, 0.012f);
                fires.Add((Mark(mapInner, pos, 52, 70, false), spots[i].at, pos));
            }
            Reset();
        }

        public void Reset()
        {
            spread.Draw(0f, 0f, Paint.Spread); ring.Draw(0f, 0f, Paint.Plain); arrows.Draw(0f, 0f, Paint.Plain);
            foreach (var (v, _, _) in banners) v.style.display = DisplayStyle.None;
            foreach (var (v, _, _) in fires) v.style.display = DisplayStyle.None;
            Armies(0f, 0f, 0f);
        }

        // p 0..1: how far the rising has spread; t: seconds, for flicker and waving.
        public void Spread(float p, float t)
        {
            spread.Draw(p, t, Paint.Spread);
            int frame = (int)(t * FrameRate);
            for (int i = 0; i < banners.Count; i++)
            {
                var (v, at, pos) = banners[i];
                float a = Mathf.Clamp01((p - at) / 0.05f);
                if (!Follow(v, pos)) a = 0f;
                v.style.display = a > 0f ? DisplayStyle.Flex : DisplayStyle.None;
                if (a <= 0f) continue;
                v.style.opacity = a * marksAlpha;
                float pop = 0.6f + 0.4f * Mathf.Clamp01(a * 1.4f);
                v.style.scale = new Scale(new Vector3(pop, pop, 1));
                SetFrame(v, bannerFrames, frame + i * 3, t, i);
            }
            for (int i = 0; i < fires.Count; i++)
            {
                var (v, at, pos) = fires[i];
                // A fire burns for a while where the rising arrives, then dies to embers.
                float age = p - at;
                float a = age <= 0f ? 0f : Mathf.Clamp01(age / 0.04f) * Mathf.Clamp01((0.45f - age) / 0.15f);
                if (!Follow(v, pos)) a = 0f;
                v.style.display = a > 0f ? DisplayStyle.Flex : DisplayStyle.None;
                if (a <= 0f) continue;
                v.style.opacity = a * marksAlpha;
                SetFrame(v, fireFrames, frame + i * 5, t, i);
            }
        }

        public void Ring(float p, float t) => ring.Draw(p, t, Paint.Plain);
        public void Arrows(float p, float t) => arrows.Draw(p, t, Paint.Plain);

        // ---------- the three imperial armies (user 2026-10-29: "把皇甫嵩等三路兵马的效果以及武将头像也做了") ----------

        // Along the arrows' centre lines, in march_arrows.json order: 卢植 to 广宗, 皇甫嵩 and 朱儁 to 颍川.
        static readonly (string id, string name, string title)[] Generals =
        {
            ("lu_zhi", "卢植", "北中郎将"), ("huangfu_song", "皇甫嵩", "左中郎将"), ("zhu_jun", "朱儁", "右中郎将"),
        };

        List<Vector2[]> armyPaths;
        Sprite[] marchFrames, dustFrames;
        readonly List<(VisualElement troop, VisualElement dust, VisualElement pin)> armies = new List<(VisualElement, VisualElement, VisualElement)>();

        // p 0..1: how far along its road each army has marched (the arrows draw ahead of them); a: their opacity.
        public void Armies(float p, float t, float a)
        {
            int frame = (int)(t * FrameRate);
            for (int i = 0; i < armies.Count; i++)
            {
                var (troop, dust, pin) = armies[i];
                // The three set out a little apart.
                float k = Mathf.Clamp01((p - i * 0.06f) / (1f - 0.12f));
                var (at, heading) = PointAlong(armyPaths[i], Mathf.SmoothStep(0f, 1f, k));
                bool shown = a > 0.001f && p > i * 0.06f;
                foreach (var v in new[] { troop, dust, pin })
                {
                    v.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                    if (!shown) continue;
                    if (!Follow(v, at)) v.style.display = DisplayStyle.None;
                    v.style.opacity = a;
                }
                // All three leave 洛阳 from one gate: a medal shows only once its column is on its own road.
                pin.style.opacity = a * Mathf.Clamp01((k - 0.1f) / 0.1f);
                if (!shown) continue;
                bool marching = k < 0.999f;
                // The column faces the way it goes (the strip walks to the right).
                troop.style.scale = new Scale(new Vector3(heading.x < 0f ? -1f : 1f, 1f, 1f));
                SetFrame(troop, marchFrames, marching ? frame + i * 3 : 0, t, i);
                dust.style.display = marching && dust.style.display == DisplayStyle.Flex ? DisplayStyle.Flex : DisplayStyle.None;
                dust.style.translate = new Translate(heading.x < 0f ? 30f : -30f, 6f);
                SetFrame(dust, dustFrames, frame + i * 2, t, i);
            }
        }

        static (Vector2 at, Vector2 heading) PointAlong(Vector2[] path, float k)
        {
            float total = 0f;
            for (int i = 1; i < path.Length; i++) total += Vector2.Distance(path[i - 1], path[i]);
            float want = k * total, run = 0f;
            for (int i = 1; i < path.Length; i++)
            {
                float len = Vector2.Distance(path[i - 1], path[i]);
                if (run + len >= want || i == path.Length - 1)
                {
                    float u = len > 0f ? Mathf.Clamp01((want - run) / len) : 1f;
                    return (Vector2.Lerp(path[i - 1], path[i], u), path[i] - path[i - 1]);
                }
                run += len;
            }
            return (path[path.Length - 1], Vector2.right);
        }

        // A general's head medal (Codex art/prologue-three-armies) with name and office, riding above his column.
        static VisualElement GeneralPin(VisualElement parent, (string id, string name, string title) g)
        {
            var v = new VisualElement { pickingMode = PickingMode.Ignore };
            v.style.position = Position.Absolute;
            v.style.width = 150; v.style.marginLeft = -75; v.style.marginTop = -190;
            v.style.alignItems = Align.Center;
            var face = new VisualElement { pickingMode = PickingMode.Ignore };
            face.style.width = 92; face.style.height = 92;
            var tex = Load("tag_" + g.id);
            if (tex) face.style.backgroundImage = tex;
            else face.style.backgroundColor = new Color(0.62f, 0.16f, 0.12f);
            v.Add(face);
            var plate = new VisualElement { pickingMode = PickingMode.Ignore };
            plate.style.backgroundColor = new Color(0.06f, 0.11f, 0.19f, 0.88f);
            plate.style.borderLeftColor = plate.style.borderRightColor = plate.style.borderTopColor = plate.style.borderBottomColor = new Color(0.78f, 0.2f, 0.12f);
            plate.style.borderLeftWidth = plate.style.borderRightWidth = plate.style.borderTopWidth = plate.style.borderBottomWidth = 1f;
            plate.style.paddingLeft = plate.style.paddingRight = 10; plate.style.paddingTop = 2; plate.style.paddingBottom = 4;
            plate.style.marginTop = 3; plate.style.alignItems = Align.Center;
            var name = new Label(g.name) { pickingMode = PickingMode.Ignore };
            name.style.fontSize = 22; name.style.color = new Color(0.96f, 0.95f, 0.91f);
            var title = new Label(g.title) { pickingMode = PickingMode.Ignore };
            title.style.fontSize = 15; title.style.color = new Color(0.71f, 0.78f, 0.84f);
            plate.Add(name); plate.Add(title);
            v.Add(plate);
            parent.Add(v);
            return v;
        }

        // On the 3D map: hands the drawn layers to the diorama's ground (call after the frame's Spread / Ring / Arrows).
        public void Sync()
        {
            if (Diorama == null) return;
            foreach (var (r, id) in new[] { (spread, "spread"), (ring, "ring"), (arrows, "arrows") })
            {
                bool shown = r.View.style.display == DisplayStyle.Flex;
                float a = r.View.style.opacity.keyword == StyleKeyword.Undefined ? r.View.style.opacity.value : 1f;
                r.View.style.visibility = Visibility.Hidden;
                Diorama.Overlay(id, r.Texture, shown ? a : 0f);
            }
        }

        // A mark stands where its map point falls on screen (the 3D map); false when that point is out of view.
        bool Follow(VisualElement v, Vector2 pos)
        {
            if (Diorama == null) return true;
            var at = Diorama.Project(pos, 1.0f);
            if (at == null) return false;
            v.style.left = Length.Percent(at.Value.x * 100f); v.style.top = Length.Percent(at.Value.y * 100f);
            return true;
        }
        // Dims the drawn layers (and the banners with the held land) while later shots speak over them.
        public void SetOpacity(float spreadA, float ringA, float arrowsA)
        {
            spread.View.style.opacity = spreadA; ring.View.style.opacity = ringA; arrows.View.style.opacity = arrowsA;
            marksAlpha = spreadA;
        }

        float marksAlpha = 1f;

        // ---------- marks (banners, fires) ----------

        static VisualElement Mark(VisualElement parent, Vector2 at, float w, float h, bool banner)
        {
            var v = new VisualElement { pickingMode = PickingMode.Ignore };
            v.style.position = Position.Absolute;
            v.style.left = Length.Percent(at.x * 100f); v.style.top = Length.Percent(at.y * 100f);
            v.style.width = w; v.style.height = h; v.style.marginLeft = -w * (banner ? 0.2f : 0.5f); v.style.marginTop = -h;
            v.style.transformOrigin = new TransformOrigin(Length.Percent(banner ? 20 : 50), Length.Percent(100));
            parent.Add(v);
            return v;
        }

        static void SetFrame(VisualElement v, Sprite[] frames, int frame, float t, int seed)
        {
            if (frames != null) { v.style.backgroundImage = new StyleBackground(frames[frame % frames.Length]); return; }
            // No art: a plain yellow pennant that sways, or an orange flicker.
            if (v.style.backgroundColor.value.a == 0f) v.style.backgroundColor = v.resolvedStyle.width > 45 ? new Color(0.86f, 0.68f, 0.18f) : new Color(1f, 0.45f, 0.1f, 0.85f);
            v.style.rotate = new Rotate(Angle.Degrees(6f * Mathf.Sin(t * 5f + seed)));
        }

        static Sprite[] Slice(Texture2D sheet, int n)
        {
            if (!sheet) return null;
            int fw = sheet.width / n;
            var frames = new Sprite[n];
            for (int i = 0; i < n; i++) frames[i] = Sprite.Create(sheet, new Rect(i * fw, 0, fw, sheet.height), new Vector2(0.5f, 0f), 100f);
            return frames;
        }

        // Places for banners: spread over the held land, each with the moment the rising reaches it.
        static List<(Vector2 pos, float at)> Spots(Reveal r, int count)
        {
            var spots = new List<(Vector2, float)>();
            var rng = new System.Random(184);
            for (int tries = 0; tries < 4000 && spots.Count < count; tries++)
            {
                int i = rng.Next(r.Source.Length);
                if (r.Source[i].a < 60) continue;
                var pos = new Vector2((i % W + 0.5f) / W, 1f - (i / W + 0.5f) / H);
                bool close = false;
                foreach (var (p, _) in spots) if ((p - pos).sqrMagnitude < 0.0045f) { close = true; break; }
                if (!close) spots.Add((pos, r.Order[i]));
            }
            spots.Sort((a, b) => a.Item2.CompareTo(b.Item2));
            return spots;
        }

        // ---------- the drawn layers ----------

        enum Paint { Plain, Spread }

        sealed class Reveal
        {
            public readonly VisualElement View;
            public readonly Color32[] Source;
            public Texture2D Texture => tex;
            public float[] Order;
            readonly Texture2D tex;
            readonly Color32[] buffer;
            readonly int[] lit;
            float drawn = -1f;

            public Reveal(VisualElement parent, Color32[] source)
            {
                Source = source;
                tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                buffer = new Color32[W * H];
                var on = new List<int>();
                for (int i = 0; i < source.Length; i++) if (source[i].a > 0) on.Add(i);
                lit = on.ToArray();
                View = new VisualElement { pickingMode = PickingMode.Ignore };
                View.style.position = Position.Absolute;
                View.style.left = View.style.right = View.style.top = View.style.bottom = 0;
                View.style.backgroundImage = tex;
                View.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
                parent.Add(View);
            }

            public void Draw(float p, float t, Paint paint)
            {
                View.style.display = p > 0f ? DisplayStyle.Flex : DisplayStyle.None;
                if (p <= 0f) { drawn = -1f; return; }
                // Settled layers are not redrawn: a plain stroke once complete, the held land once its fire front is out.
                float settled = paint == Paint.Plain ? 1f : 1.1f;
                if (p >= settled && drawn >= settled) return;
                drawn = p;
                const float edge = 0.035f;
                foreach (int i in lit)
                {
                    float age = p - Order[i];
                    var s = Source[i];
                    if (age <= 0f) { buffer[i].a = 0; continue; }
                    float a = Mathf.Clamp01(age / edge);
                    if (paint == Paint.Plain) { buffer[i] = new Color32(s.r, s.g, s.b, (byte)(s.a * a)); continue; }
                    // Held land: gold at first, deepening to burnt ochre as time passes; a flickering fire front.
                    var c = Color32.Lerp(Held, Deep, Mathf.Clamp01(age * 2.2f));
                    float front = Mathf.Clamp01(1f - age / (edge * 2.2f));
                    if (front > 0f)
                    {
                        float flick = 0.6f + 0.4f * Mathf.Sin(t * 13f + i * 0.37f);
                        c = Color32.Lerp(c, Color32.Lerp(Ember, Flame, flick), front);
                    }
                    // map_states is a faint wash (alpha ≈ 69); its shape is the mask, the strength is ours.
                    float mask = Mathf.Min(1f, s.a / 60f), deep = Mathf.Clamp01(age * 2.2f);
                    byte alpha = (byte)(Mathf.Min(255f, mask * (105f + 55f * deep) + 90f * front * mask) * a);
                    buffer[i] = new Color32(c.r, c.g, c.b, alpha);
                }
                tex.SetPixels32(buffer);
                tex.Apply(false);
            }
        }

        // ---------- arrival maps ----------

        // Out from Ju Lu along a noisy front, so the rising reaches places unevenly.
        static float[] SpreadOrder(Color32[] src, Vector2 from)
        {
            var o = new float[src.Length];
            float max = 0.0001f;
            Vector2 f = new Vector2(from.x * W, (1f - from.y) * H);
            for (int i = 0; i < src.Length; i++)
            {
                if (src[i].a == 0) continue;
                float x = i % W, y = i / W;
                float d = Vector2.Distance(new Vector2(x, y), f) + 90f * (Mathf.PerlinNoise(x * 0.012f, y * 0.012f) - 0.5f) + 30f * (Mathf.PerlinNoise(x * 0.05f + 7f, y * 0.05f) - 0.5f);
                o[i] = Mathf.Max(0f, d); max = Mathf.Max(max, o[i]);
            }
            for (int i = 0; i < o.Length; i++) o[i] /= max * 1.02f;
            return o;
        }

        // Clockwise from straight up around the centre: one stroke of the brush.
        static float[] AngleOrder(Color32[] src, Vector2 centre)
        {
            var o = new float[src.Length];
            Vector2 c = new Vector2(centre.x * W, (1f - centre.y) * H);
            for (int i = 0; i < src.Length; i++)
            {
                if (src[i].a == 0) continue;
                Vector2 d = new Vector2(i % W, i / W) - c;
                float ang = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;   // 0 up, clockwise positive (texture rows go up)
                o[i] = Mathf.Repeat(ang, 360f) / 380f;
            }
            return o;
        }

        // Along each arrow from tail to head; all three are drawn together.
        static float[] PathOrder(Color32[] src, List<Vector2[]> paths)
        {
            var o = new float[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                if (src[i].a == 0) continue;
                var q = new Vector2((i % W + 0.5f) / W, 1f - (i / W + 0.5f) / H);
                float best = float.MaxValue, at = 1f;
                foreach (var path in paths)
                {
                    float total = 0f; for (int k = 1; k < path.Length; k++) total += Vector2.Distance(path[k - 1], path[k]);
                    float run = 0f;
                    for (int k = 1; k < path.Length; k++)
                    {
                        Vector2 a = path[k - 1], b = path[k];
                        float len = Vector2.Distance(a, b);
                        float u = len > 0f ? Mathf.Clamp01(Vector2.Dot(q - a, b - a) / (len * len)) : 0f;
                        float d = Vector2.Distance(q, a + (b - a) * u);
                        if (d < best) { best = d; at = (run + u * len) / Mathf.Max(total, 1e-5f); }
                        run += len;
                    }
                }
                o[i] = at * 0.96f;
            }
            return o;
        }

        // ---------- sources ----------

        static Texture2D Load(string name) => Resources.Load<Texture2D>("Prologue/" + name);

        // The art at half size. Art must be imported readable (CinematicsArtImporter sets this for the map layers).
        static Color32[] Sample(Texture2D art)
        {
            var o = new Color32[W * H];
            if (!art.isReadable) { Debug.LogWarning(art.name + " is not readable; the prologue map draws a plain stand-in."); return o; }
            var px = art.GetPixels32();
            int aw = art.width, ah = art.height;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    o[y * W + x] = px[(y * ah / H) * aw + x * aw / W];
            return o;
        }

        static Color32[] Blob(Vector2 at, float r)
        {
            var o = new Color32[W * H];
            Vector2 c = new Vector2(at.x * W, (1f - at.y) * H);
            for (int i = 0; i < o.Length; i++)
                if (Vector2.Distance(new Vector2(i % W, i / W), c) < r * W) o[i] = new Color32(214, 168, 52, 150);
            return o;
        }

        static Color32[] RingStroke(Vector2 at, float r, float width)
        {
            var o = new Color32[W * H];
            Vector2 c = new Vector2(at.x * W, (1f - at.y) * H);
            float rr = r * W;
            for (int i = 0; i < o.Length; i++)
                if (Mathf.Abs(Vector2.Distance(new Vector2(i % W, i / W), c) - rr) < width * 0.5f) o[i] = new Color32(200, 50, 30, 235);
            return o;
        }

        static Color32[] ArrowStrokes(List<Vector2[]> paths)
        {
            var o = new Color32[W * H];
            foreach (var path in paths)
                for (int k = 1; k < path.Length; k++)
                    for (float u = 0; u <= 1f; u += 0.004f)
                    {
                        var p = Vector2.Lerp(path[k - 1], path[k], u);
                        int cx = (int)(p.x * W), cy = (int)((1f - p.y) * H);
                        for (int dy = -5; dy <= 5; dy++) for (int dx = -5; dx <= 5; dx++)
                        {
                            int x = cx + dx, y = cy + dy;
                            if (x >= 0 && x < W && y >= 0 && y < H && dx * dx + dy * dy <= 25) o[y * W + x] = new Color32(196, 46, 28, 240);
                        }
                    }
            return o;
        }

        // Centre lines as map fractions from Resources/Prologue/<name>.json (converted by CinematicsArtImporter).
        static List<Vector2[]> CentreLines(string name)
        {
            var list = new List<Vector2[]>();
            var file = Resources.Load<TextAsset>("Prologue/" + name);
            if (file)
            {
                try
                {
                    var data = JsonUtility.FromJson<ArrowFile>(file.text);
                    foreach (var a in data.arrows)
                    {
                        var pts = new Vector2[a.points.Length];
                        for (int i = 0; i < pts.Length; i++) pts[i] = new Vector2(a.points[i].x / data.width, a.points[i].y / data.height);
                        if (pts.Length > 1) list.Add(pts);
                    }
                }
                catch (ArgumentException e) { Debug.LogWarning(name + ".json unreadable: " + e.Message); }
            }
            return list;
        }

        List<Vector2[]> DefaultArrows()
        {
            var list = new List<Vector2[]>();
            Vector2 lo = anchor("luoyang"), gz = anchor("guangzong"), yc = anchor("yingchuan");
            list.Add(new[] { lo, Vector2.Lerp(lo, gz, 0.5f) + new Vector2(0f, -0.03f), gz });
            list.Add(new[] { lo + new Vector2(0.005f, 0.01f), Vector2.Lerp(lo, yc, 0.5f) + new Vector2(0f, -0.035f), yc + new Vector2(0f, -0.012f) });
            list.Add(new[] { lo + new Vector2(0.005f, 0.02f), Vector2.Lerp(lo, yc, 0.5f) + new Vector2(0f, 0.035f), yc + new Vector2(0f, 0.012f) });
            return list;
        }

        [Serializable] class ArrowFile { public float width = 1920, height = 1080; public ArrowEntry[] arrows = Array.Empty<ArrowEntry>(); }
        [Serializable] class ArrowEntry { public string id; public Point[] points = Array.Empty<Point>(); }
        [Serializable] class Point { public float x, y; }
    }
}
