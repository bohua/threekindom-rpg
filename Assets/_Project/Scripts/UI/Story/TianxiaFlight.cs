using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ThreeKingdoms.Core.Story;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThreeKingdoms.UI.Story
{
    // A march on the 3D 天下图 (user 2026-10-29: the prologue's diorama is the game's world map; the open regions R1–R8
    // are marked on it and a march between places flies over it). Design: Codex art/tianxia-regions-ui
    // (TIANXIA_REGIONS_UI.png, TIANXIA_TRAVEL_STORYBOARD.png, tianxia_regions.json). Two levels, not a seamless zoom:
    // the region scenes are walkable maps hundreds of metres across, so only the directions agree.
    // The flight, over t 0..1: overview of both ends with the route (0–0.15), low along the route behind Liu Bei's
    // column (0.15–0.75), down over the destination with its name (0.75–0.9), mist closing in (0.9–1).
    public sealed class TianxiaFlight : IDisposable
    {
        // Story places on map_base (1920×1080 px, top left): city anchors of the diorama where it has them, else the
        // region hubs of tianxia_regions.json, else fitted from the old flat chapter map (StageHud.Nodes).
        public static readonly Dictionary<string, Vector2> Places = new Dictionary<string, Vector2>
        {
            ["zhuojun"] = new Vector2(1073, 219), ["ji"] = new Vector2(1089, 190), ["guangzong"] = new Vector2(1044, 319),
            ["yingchuan"] = new Vector2(931, 439), ["luoyang"] = new Vector2(818, 391), ["xuzhou"] = new Vector2(1194, 421),
            ["anxi"] = new Vector2(1009, 255), ["wancheng"] = new Vector2(826, 507), ["gaotang"] = new Vector2(1089, 321),
            ["suanzao"] = new Vector2(970, 380), ["hulao"] = new Vector2(888, 398), ["pingyuan"] = new Vector2(1110, 284),
            ["jieqiao"] = new Vector2(1032, 292), ["zhending"] = new Vector2(1001, 248), ["duchang"] = new Vector2(1268, 330),
        };

        public enum State { Locked, Open, Danger, Current, Destination }

        sealed class Region
        {
            public string Id, Name; public Vector2 Label; public Vector2[] Polygon;
            public VisualElement Plate; public State State;
        }

        static readonly Color Ivory = new Color(0.957f, 0.945f, 0.910f), Mist = new Color(0.710f, 0.784f, 0.835f);
        static readonly Color Cyan = new Color(0.384f, 0.843f, 0.910f), Gold = new Color(0.839f, 0.729f, 0.471f);
        static readonly Color Red = new Color(0.847f, 0.420f, 0.439f), Panel = new Color(0.063f, 0.110f, 0.188f, 0.90f);

        readonly PrologueDiorama map;
        readonly VisualElement root, image, column, columnPlate, title, mist;
        readonly Label titleName, titleSites;
        readonly List<Region> regions = new List<Region>();
        readonly Sprite[] marchFrames, wagonFrames, dustFrames;
        readonly VisualElement wagon, dust;
        readonly TianxiaArrow arrow;
        readonly Vector2[] route;   // map_base pixels
        readonly Texture2D regionLayer;
        readonly Region destination;

        public string From { get; }
        public string To { get; }

        TianxiaFlight(PrologueDiorama map, VisualElement parent, string from, string to, string yearKey)
        {
            this.map = map; From = from; To = to;
            root = new VisualElement { pickingMode = PickingMode.Ignore };
            Fill(root);
            parent.Add(root);
            image = new VisualElement { pickingMode = PickingMode.Ignore };
            Fill(image);
            image.style.backgroundImage = Background.FromRenderTexture((RenderTexture)map.Image);
            root.Add(image);

            Vector2 a = PlaceOf(from), b = PlaceOf(to);
            LoadRegions();
            var open = OpenRegions(Year(yearKey), out var danger);
            Region here = null;
            foreach (var r in regions)
            {
                r.State = danger.Contains(r.Id) ? State.Danger : open.Contains(r.Id) ? State.Open : State.Locked;
                if (here == null && Inside(r.Polygon, a)) here = r;
                if (destination == null && Inside(r.Polygon, b)) destination = r;
            }
            // A place just outside every outline (都昌 on the coast) belongs to the region whose name is nearest.
            here ??= Nearest(a);
            destination ??= Nearest(b);
            if (here != null) here.State = State.Current;
            if (destination != null) destination.State = State.Destination;
            route = RouteBetween(here?.Id, destination?.Id, a, b);

            regionLayer = DrawRegions(regions);
            map.Overlay("map_julu", regionLayer, 1f);   // a free overlay slot of the prologue's diorama
            var path = new List<Vector3>();
            foreach (var px in route) path.Add(World(px));
            arrow = new TianxiaArrow(map, path, 0.85f);

            foreach (var r in regions) { r.Plate = RegionPlate(r); root.Add(r.Plate); }

            // Liu Bei's column (Codex art/tianxia-liu-march; the imperial column of the prologue until it is in), the
            // baggage wagon behind it and the dust they raise.
            dust = Sprite(root, 120, 90);
            wagon = Sprite(root, 180, 126);
            column = Sprite(root, 240, 160);
            marchFrames = PrologueMapFx.Slice(Resources.Load<Texture2D>("Prologue/liu_army_march"), 8)
                ?? PrologueMapFx.Slice(Resources.Load<Texture2D>("Prologue/han_army_march"), 8);
            wagonFrames = PrologueMapFx.Slice(Resources.Load<Texture2D>("Prologue/liu_wagon_march"), 8);
            dustFrames = PrologueMapFx.Slice(Resources.Load<Texture2D>("Prologue/march_dust"), 6)
                ?? PrologueMapFx.Slice(Resources.Load<Texture2D>("Prologue/han_army_dust"), 6);
            columnPlate = Plate("刘备军", 22, Cyan);
            columnPlate.style.marginLeft = -48; columnPlate.style.marginTop = -200;
            root.Add(columnPlate);

            title = new VisualElement { pickingMode = PickingMode.Ignore };
            title.style.position = Position.Absolute;
            title.style.left = 0; title.style.right = 0; title.style.top = Length.Percent(36);
            title.style.alignItems = Align.Center;
            titleName = new Label(destination != null ? Spaced(destination.Name) : "") { pickingMode = PickingMode.Ignore };
            titleName.style.fontSize = 76; titleName.style.color = Ivory;
            titleName.style.unityTextOutlineColor = new Color(0.04f, 0.07f, 0.12f, 0.8f); titleName.style.unityTextOutlineWidth = 0.18f;
            titleSites = new Label(StoryText.Line("node." + to)) { pickingMode = PickingMode.Ignore };
            titleSites.style.fontSize = 28; titleSites.style.color = Ivory;
            titleSites.style.unityTextOutlineColor = new Color(0.04f, 0.07f, 0.12f, 0.8f); titleSites.style.unityTextOutlineWidth = 0.2f;
            title.Add(titleName); title.Add(titleSites);
            root.Add(title);

            mist = new VisualElement { pickingMode = PickingMode.Ignore };
            Fill(mist);
            mist.style.backgroundColor = new Color(0.95f, 0.95f, 0.93f);
            root.Add(mist);
            Update(0f);
        }

        // Null where the diorama has not been built (tests, a build without it): the flat chapter map stays.
        public static TianxiaFlight TryCreate(VisualElement parent, string from, string to, string yearKey)
        {
            if (!Places.ContainsKey(from ?? "") || !Places.ContainsKey(to ?? "")) return null;
            var map = PrologueDiorama.TryCreate();
            return map == null ? null : new TianxiaFlight(map, parent, from, to, yearKey);
        }

        public void Update(float t)
        {
            float time = Time.unscaledTime;
            // Camera.
            Vector3 a = World(route[0]), b = World(route[route.Length - 1]);
            float span = Mathf.Max(8f, Vector3.Distance(a, b));
            Vector3 mid = (a + b) * 0.5f;
            var overview = new PrologueDiorama.Pose(mid + new Vector3(0f, span * 1.1f + 9f, -span * 0.7f - 6f), mid, 42f);
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.18f) / 0.57f));
            float len = arrow.Length;
            var (p, _) = arrow.At(k * len);
            // Heading from a stretch of road around the column, so the camera turns smoothly at the bends.
            Vector3 d = arrow.At(Mathf.Min(len, (k + 0.1f) * len)).at - arrow.At(Mathf.Max(0f, (k - 0.1f) * len)).at;
            d.y = 0f;
            d = d.sqrMagnitude < 1e-4f ? Vector3.forward : d.normalized;
            // High and steep behind the column: the peaks of the diorama are tall next to the plains' roads.
            var follow = new PrologueDiorama.Pose(p - d * 3.2f + Vector3.up * 7.2f, p + d * 1.6f, 44f);
            var arrive = new PrologueDiorama.Pose(b + new Vector3(0f, 7.5f, -4.8f), b, 44f);
            follow = Clear(follow, 2.8f, true);
            arrive = Clear(arrive, 3.2f, false);
            PrologueDiorama.Pose pose;
            if (t < 0.24f) pose = PrologueDiorama.Pose.Lerp(overview, follow, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.14f) / 0.10f)));
            else if (t < 0.75f) pose = follow;
            else pose = PrologueDiorama.Pose.Lerp(follow, arrive, Mathf.SmoothStep(0f, 1f, (t - 0.75f) / 0.17f));
            map.View(pose.Position, pose.LookAt, pose.Fov);

            // Region names: all of them on the overview, then only the two ends.
            foreach (var r in regions)
            {
                float alpha = t < 0.15f ? 1f : Mathf.Clamp01(1f - (t - 0.15f) / 0.05f);
                if (r.State == State.Current || r.State == State.Destination) alpha = Mathf.Max(alpha, t < 0.75f ? 0.9f : Mathf.Clamp01(1f - (t - 0.75f) / 0.05f));
                Follow(r.Plate, r.Label, alpha * (r.State == State.Locked ? 0.7f : 1f));
            }

            // The arrow draws the whole road on the overview, then flows forward under the march.
            arrow.Draw(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.03f) / 0.13f)), time, Mathf.Clamp01((0.97f - t) / 0.06f));

            // Liu Bei's column at the head, the wagon some way behind, dust at their heels.
            float colA = Mathf.Clamp01((t - 0.06f) / 0.06f) * Mathf.Clamp01((0.95f - t) / 0.05f);
            bool moving = t > 0.18f && k < 0.999f;
            int frame = moving ? (int)(time * 8f) : 0;
            float gap = Mathf.Min(0.9f, len * 0.08f);
            var (wagonAt, wagonDir) = arrow.At(Mathf.Max(0f, k * len - gap));
            var (dustAt, _) = arrow.At(Mathf.Max(0f, k * len - gap * 0.45f));
            March(column, marchFrames, p, d, colA, frame);
            March(wagon, wagonFrames, wagonAt, wagonDir, colA * (wagonFrames != null ? 1f : 0f), frame + 3);
            March(dust, dustFrames, dustAt, d, colA * (moving ? 0.85f : 0f), (int)(time * 10f));
            FollowWorld(columnPlate, p, colA);

            // The destination's name, then the mist.
            title.style.opacity = Mathf.Clamp01((t - 0.74f) / 0.08f) * Mathf.Clamp01((0.97f - t) / 0.05f);
            mist.style.opacity = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.88f) / 0.1f)) + Mathf.Clamp01(1f - t / 0.04f);
        }

        static VisualElement Sprite(VisualElement parent, float w, float h)
        {
            var v = new VisualElement { pickingMode = PickingMode.Ignore };
            v.style.position = Position.Absolute;
            v.style.width = w; v.style.height = h; v.style.marginLeft = -w / 2f; v.style.marginTop = -h;
            v.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(100));
            parent.Add(v);
            return v;
        }

        // A marching sprite standing on the road at a diorama point, facing the way the road goes on screen.
        void March(VisualElement v, Sprite[] frames, Vector3 at, Vector3 heading, float alpha, int frame)
        {
            FollowWorld(v, at, frames != null ? alpha : 0f);
            if (frames == null) return;
            v.style.backgroundImage = new StyleBackground(frames[frame % frames.Length]);
            var ahead = map.Project(MapOf(at + heading * 0.6f), at.y); var here = map.Project(MapOf(at), at.y);
            if (ahead.HasValue && here.HasValue) v.style.scale = new Scale(new Vector3(ahead.Value.x < here.Value.x ? -1f : 1f, 1f, 1f));
        }

        void FollowWorld(VisualElement v, Vector3 at, float alpha)
        {
            var px = MapOf(at);
            Follow(v, new Vector2(px.x * 1920f, px.y * 1080f), alpha, at.y);
        }

        static Vector2 MapOf(Vector3 world) => new Vector2(world.x / PrologueDiorama.Width, 1f - world.z / PrologueDiorama.Depth);

        // Lifts a pose so the camera, and its view toward the look point, stay above the peaks.
        PrologueDiorama.Pose Clear(PrologueDiorama.Pose pose, float margin, bool smooth)
        {
            float lift = 0f;
            for (int i = 0; i <= 4; i++)
            {
                var q = Vector3.Lerp(pose.Position, pose.LookAt, i * 0.18f);
                lift = Mathf.Max(lift, PrologueDiorama.GroundAt(q.x, q.z) + margin - q.y);
            }
            // Smoothed on the way down, so passing a summit eases the camera instead of dropping it.
            if (smooth) lift = smoothLift = lift >= smoothLift ? lift : Mathf.Lerp(smoothLift, lift, Time.unscaledDeltaTime * 1.5f);
            if (lift <= 0f) return pose;
            return new PrologueDiorama.Pose(pose.Position + Vector3.up * lift, pose.LookAt + Vector3.up * lift * 0.5f, pose.Fov);
        }

        float smoothLift;

        public void Dispose()
        {
            root.RemoveFromHierarchy();
            map.Dispose();
            UnityEngine.Object.Destroy(regionLayer);
            arrow.Dispose();
        }

        // ---------- data ----------

        static Vector2 PlaceOf(string id) => Places.TryGetValue(id ?? "", out var p) ? p : new Vector2(960, 300);
        static Vector2 Norm(Vector2 px) => new Vector2(px.x / 1920f, px.y / 1080f);
        static Vector3 World(Vector2 px) => PrologueDiorama.ToWorld(Norm(px), 0.6f);

        static int Year(string key)
        {
            var m = Regex.Match(key ?? "", @"\d{3}");
            return m.Success ? int.Parse(m.Value) : 184;
        }

        // The regions open by a year after the chapter's chronicle (Chapter1Chronicle; 广宗 alone counts for R3).
        static HashSet<string> OpenRegions(int year, out HashSet<string> danger)
        {
            var open = new HashSet<string>(); danger = new HashSet<string>();
            foreach (var n in Chapter1Chronicle.Nodes)
            {
                if (n.Year > year) break;
                foreach (var id in n.Opens) open.Add(id == Chapter1Chronicle.R3Guangzong ? Chapter1Chronicle.R3 : id);
                foreach (var id in n.Closes) if (id != Chapter1Chronicle.R3Guangzong) open.Remove(id);
                foreach (var id in n.Danger) danger.Add(id);
            }
            return open;
        }

        void LoadRegions()
        {
            var file = Resources.Load<TextAsset>("Prologue/tianxia_regions");
            if (!file) return;
            string text = file.text;
            int list = text.IndexOf("\"regions\"", StringComparison.Ordinal);
            if (list < 0) return;
            foreach (Match m in Regex.Matches(text.Substring(list), "\"id\"\\s*:\\s*\"(R[0-9a-z]+)\"\\s*,\\s*\"name\"\\s*:\\s*\"([^\"]+)\"\\s*,\\s*\"polygon\"\\s*:\\s*(\\[\\s*\\[[^\"]*?\\]\\s*\\])\\s*,\\s*\"label\"\\s*:\\s*\\[\\s*([0-9.]+)\\s*,\\s*([0-9.]+)"))
            {
                var pts = new List<Vector2>();
                foreach (Match p in Regex.Matches(m.Groups[3].Value, @"\[\s*([0-9.]+)\s*,\s*([0-9.]+)\s*\]"))
                    pts.Add(new Vector2(float.Parse(p.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), float.Parse(p.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)));
                if (pts.Count < 3) continue;
                regions.Add(new Region
                {
                    Id = m.Groups[1].Value, Name = m.Groups[2].Value, Polygon = pts.ToArray(),
                    Label = new Vector2(float.Parse(m.Groups[4].Value, System.Globalization.CultureInfo.InvariantCulture), float.Parse(m.Groups[5].Value, System.Globalization.CultureInfo.InvariantCulture)),
                });
            }
        }

        Region Nearest(Vector2 p)
        {
            Region best = null; float bestD = 90f;   // map pixels; farther places (宛城) stay outside the chapter's regions
            foreach (var r in regions)
            {
                float d = Inside(r.Polygon, p) ? 0f : EdgeDistance(r.Polygon, p);
                if (d < bestD) { bestD = d; best = r; }
            }
            return best;
        }

        // The road between two regions from tianxia_regions.json (with the two places at its ends), else a straight
        // line with a slight bow.
        Vector2[] RouteBetween(string ra, string rb, Vector2 a, Vector2 b)
        {
            var file = Resources.Load<TextAsset>("Prologue/tianxia_regions");
            if (file != null && ra != null && rb != null && ra != rb)
            {
                foreach (Match m in Regex.Matches(file.text, "\"from\"\\s*:\\s*\"(R[0-9a-z]+)\"\\s*,\\s*\"to\"\\s*:\\s*\"(R[0-9a-z]+)\"(?:(?!\"from\")[\\s\\S])*?\"points\"\\s*:\\s*(\\[\\s*\\[[^\"]*?\\]\\s*\\])"))
                {
                    bool forward = m.Groups[1].Value == ra && m.Groups[2].Value == rb, back = m.Groups[1].Value == rb && m.Groups[2].Value == ra;
                    if (!forward && !back) continue;
                    var pts = new List<Vector2>();
                    foreach (Match p in Regex.Matches(m.Groups[3].Value, @"\[\s*([0-9.]+)\s*,\s*([0-9.]+)\s*\]"))
                        pts.Add(new Vector2(float.Parse(p.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), float.Parse(p.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)));
                    if (back) pts.Reverse();
                    // The road runs hub to hub; the march runs place to place.
                    if (pts.Count >= 2) { pts[0] = a; pts[pts.Count - 1] = b; return pts.ToArray(); }
                }
            }
            var n = new Vector2(-(b - a).y, (b - a).x).normalized * Vector2.Distance(a, b) * 0.08f;
            var line = new Vector2[9];
            for (int i = 0; i < line.Length; i++)
            {
                float u = i / (line.Length - 1f);
                line[i] = Vector2.Lerp(a, b, u) + n * Mathf.Sin(u * Mathf.PI);
            }
            return line;
        }

        static (Vector2 at, Vector2 dir) Along(Vector2[] path, float k)
        {
            float total = 0f;
            for (int i = 1; i < path.Length; i++) total += Vector2.Distance(path[i - 1], path[i]);
            float want = k * total, run = 0f;
            for (int i = 1; i < path.Length; i++)
            {
                float len = Vector2.Distance(path[i - 1], path[i]);
                if (run + len >= want || i == path.Length - 1)
                    return (Vector2.Lerp(path[i - 1], path[i], len > 0f ? Mathf.Clamp01((want - run) / len) : 1f), path[i] - path[i - 1]);
                run += len;
            }
            return (path[path.Length - 1], Vector2.right);
        }

        static bool Inside(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }

        static float EdgeDistance(Vector2[] poly, Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 e = poly[i] - poly[j];
                float u = Mathf.Clamp01(Vector2.Dot(p - poly[j], e) / Mathf.Max(e.sqrMagnitude, 1e-4f));
                best = Mathf.Min(best, Vector2.Distance(p, poly[j] + e * u));
            }
            return best;
        }

        // ---------- the layers on the ground (map_base space, the overlay's UV) ----------

        static Texture2D DrawRegions(List<Region> regions)
        {
            const int W = 1920, H = 1080;
            var px = new Color32[W * H];
            foreach (var r in regions)
            {
                Color edge, fill; float edgeW;
                switch (r.State)
                {
                    case State.Current: case State.Destination: edge = Cyan; fill = new Color(Cyan.r, Cyan.g, Cyan.b, 0.06f); edgeW = 1.6f; break;
                    case State.Danger: edge = Red; fill = new Color(Red.r, Red.g, Red.b, 0.10f); edgeW = 1.3f; break;
                    case State.Open: edge = Gold; fill = new Color(Gold.r, Gold.g, Gold.b, 0.03f); edgeW = 1.1f; break;
                    default: edge = new Color(0.72f, 0.78f, 0.83f); fill = new Color(0.80f, 0.85f, 0.89f, 0.18f); edgeW = 0.9f; break;
                }
                float minX = float.MaxValue, minY = float.MaxValue, maxX = 0, maxY = 0;
                foreach (var p in r.Polygon) { minX = Mathf.Min(minX, p.x); minY = Mathf.Min(minY, p.y); maxX = Mathf.Max(maxX, p.x); maxY = Mathf.Max(maxY, p.y); }
                for (int y = Mathf.Max(0, (int)minY - 3); y <= Mathf.Min(H - 1, (int)maxY + 3); y++)
                    for (int x = Mathf.Max(0, (int)minX - 3); x <= Mathf.Min(W - 1, (int)maxX + 3); x++)
                    {
                        var p = new Vector2(x + 0.5f, y + 0.5f);
                        bool inside = Inside(r.Polygon, p);
                        float dist = EdgeDistance(r.Polygon, p);
                        Color c = new Color(0, 0, 0, 0);
                        if (inside)
                        {
                            c = fill;
                            // The locked lands lie under thicker mist toward their middle; the dangerous ones are hatched.
                            if (r.State == State.Locked) c.a *= Mathf.Clamp01(0.45f + dist / 14f);
                            if (r.State == State.Danger && ((x + y) % 7) < 2 && dist < 6f) c = new Color(Red.r, Red.g, Red.b, 0.75f);
                            // A soft glow just inside the line.
                            c.a = Mathf.Max(c.a, (r.State == State.Locked ? 0.0f : 0.28f) * Mathf.Clamp01(1f - dist / 3f));
                        }
                        float line = Mathf.Clamp01(edgeW + 0.5f - dist);
                        if (line > 0f) c = Color.Lerp(c, new Color(edge.r, edge.g, edge.b, 0.95f), line);
                        if (c.a <= 0.003f) continue;
                        // Regions share borders: keep the stronger mark.
                        int i = (H - 1 - y) * W + x;
                        if (px[i].a < (byte)(c.a * 255f)) px[i] = c;
                    }
            }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "tianxia regions" };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        // ---------- screen pieces ----------

        void Follow(VisualElement v, Vector2 px, float alpha, float height = 0.9f)
        {
            var at = map.Project(Norm(px), height);
            v.style.display = at.HasValue && alpha > 0.01f ? DisplayStyle.Flex : DisplayStyle.None;
            if (!at.HasValue) return;
            v.style.left = Length.Percent(at.Value.x * 100f); v.style.top = Length.Percent(at.Value.y * 100f);
            v.style.opacity = alpha;
        }

        static VisualElement RegionPlate(Region r)
        {
            bool locked = r.State == State.Locked;
            var edge = r.State == State.Current || r.State == State.Destination ? Cyan : r.State == State.Danger ? Red : locked ? Mist : Gold;
            var v = Plate(r.Name, 24, edge);
            if (locked)
            {
                var note = new Label("未开放") { pickingMode = PickingMode.Ignore };
                note.style.fontSize = 15; note.style.color = Mist;
                v.Add(note);
            }
            if (r.State == State.Current)
            {
                var note = new Label("所在") { pickingMode = PickingMode.Ignore };
                note.style.fontSize = 15; note.style.color = Cyan;
                v.Add(note);
            }
            v.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
            return v;
        }

        static VisualElement Plate(string text, int size, Color edge)
        {
            var v = new VisualElement { pickingMode = PickingMode.Ignore };
            v.style.position = Position.Absolute;
            v.style.backgroundColor = Panel;
            v.style.borderLeftColor = v.style.borderRightColor = v.style.borderTopColor = v.style.borderBottomColor = edge;
            v.style.borderLeftWidth = v.style.borderRightWidth = v.style.borderTopWidth = v.style.borderBottomWidth = 1f;
            v.style.paddingLeft = v.style.paddingRight = 12; v.style.paddingTop = 3; v.style.paddingBottom = 4;
            v.style.alignItems = Align.Center;
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.style.fontSize = size; l.style.color = Ivory;
            v.Add(l);
            return v;
        }

        static string Spaced(string s) => string.Join(" ", s.ToCharArray());

        static void Fill(VisualElement v)
        {
            v.style.position = Position.Absolute;
            v.style.left = v.style.top = v.style.right = v.style.bottom = 0;
        }
    }
}
