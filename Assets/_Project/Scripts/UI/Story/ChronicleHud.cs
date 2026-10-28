using System.Collections.Generic;
using ThreeKingdoms.Gameplay.Story;
using UnityEngine;

namespace ThreeKingdoms.UI.Story
{
    // The 0.3 realm-map chronicle (SYSTEMS_V03 §2; design image art/v03-ui-concept ui_realm_map_chronicle): over the
    // story stage's realm map, the year banner, each power's banner at its city, an event window, and Liu Bei's piece.
    // Static node version: it only shows what the script says (StagePlayer.ChronicleNow) and never writes the story state.
    public sealed class ChronicleHud : MonoBehaviour
    {
        static readonly Color Ink = new Color(0.063f, 0.11f, 0.19f), Silver = new Color(0.55f, 0.65f, 0.73f),
            Ivory = new Color(0.96f, 0.95f, 0.91f), Teal = new Color(0.38f, 0.84f, 0.91f);

        // Cities on the chapter map (normalised from the top left), beside the march nodes of StageHud.
        public static readonly Dictionary<string, Vector2> Cities = new Dictionary<string, Vector2>
        {
            ["ji"] = new Vector2(0.392f, 0.270f), ["gaotang"] = new Vector2(0.384f, 0.520f), ["pingyuan"] = new Vector2(0.411f, 0.424f),
            ["suanzao"] = new Vector2(0.271f, 0.667f), ["hulao"] = new Vector2(0.204f, 0.690f), ["luoyang"] = new Vector2(0.172f, 0.694f),
            ["ye"] = new Vector2(0.294f, 0.562f), ["xuzhou"] = new Vector2(0.483f, 0.727f),
            ["jieqiao"] = new Vector2(0.334f, 0.463f), ["zhending"] = new Vector2(0.290f, 0.460f),
            ["duchang"] = new Vector2(0.5625f, 0.511f), ["puyang"] = new Vector2(0.317f, 0.613f), ["xu"] = new Vector2(0.240f, 0.760f),
        };

        // Powers: name and banner colour.
        static readonly Dictionary<string, (string name, Color color)> Lords = new Dictionary<string, (string, Color)>
        {
            ["dong"] = ("董卓", new Color(0.56f, 0.16f, 0.16f)), ["liuyu"] = ("刘虞", new Color(0.36f, 0.52f, 0.62f)),
            ["gongsun"] = ("公孙瓒", new Color(0.88f, 0.88f, 0.86f)), ["yuan_shao"] = ("袁绍", new Color(0.62f, 0.12f, 0.20f)),
            ["coalition"] = ("关东诸侯", new Color(0.62f, 0.12f, 0.20f)), ["sun"] = ("孙坚", new Color(0.86f, 0.30f, 0.22f)),
            ["han"] = ("韩馥", new Color(0.36f, 0.56f, 0.45f)), ["tao"] = ("陶谦", new Color(0.52f, 0.70f, 0.70f)),
            ["none"] = ("焦土", new Color(0.35f, 0.35f, 0.35f)),
            ["cao"] = ("曹操", new Color(0.20f, 0.20f, 0.24f)), ["lubu"] = ("吕布", new Color(0.78f, 0.30f, 0.20f)),
            ["kong"] = ("孔融", new Color(0.36f, 0.56f, 0.62f)), ["liu"] = ("刘备", new Color(0.93f, 0.52f, 0.18f)),
        };

        // Layouts: city → power, and where Liu Bei's piece stands.
        static readonly Dictionary<string, (Dictionary<string, string> map, string liu)> Layouts = new Dictionary<string, (Dictionary<string, string>, string)>
        {
            ["y186"] = (new Dictionary<string, string> { ["ji"] = "liuyu", ["luoyang"] = "han_court" }, "gaotang"),
            ["y189"] = (new Dictionary<string, string> { ["ji"] = "liuyu", ["ye"] = "han", ["xuzhou"] = "tao" }, "gaotang"),
            ["y190"] = (new Dictionary<string, string> { ["ji"] = "gongsun", ["luoyang"] = "dong", ["hulao"] = "dong", ["suanzao"] = "coalition", ["ye"] = "han", ["xuzhou"] = "tao" }, "suanzao"),
            ["y191b"] = (new Dictionary<string, string> { ["ji"] = "gongsun", ["ye"] = "han", ["xuzhou"] = "tao" }, "ji"),
            ["y192"] = (new Dictionary<string, string> { ["ji"] = "gongsun", ["ye"] = "yuan_shao", ["xuzhou"] = "tao", ["pingyuan"] = "gongsun" }, "pingyuan"),
            ["y193"] = (new Dictionary<string, string> { ["ji"] = "gongsun", ["ye"] = "yuan_shao", ["puyang"] = "cao", ["xuzhou"] = "tao", ["duchang"] = "kong" }, "pingyuan"),
            ["y194"] = (new Dictionary<string, string> { ["ji"] = "gongsun", ["ye"] = "yuan_shao", ["puyang"] = "cao", ["xuzhou"] = "tao" }, "xuzhou"),
            ["y196"] = (new Dictionary<string, string> { ["ye"] = "yuan_shao", ["xu"] = "cao", ["xuzhou"] = "liu", ["puyang"] = "lubu" }, "xuzhou"),
            ["y191"] = (new Dictionary<string, string> { ["ji"] = "gongsun", ["luoyang"] = "none", ["suanzao"] = "none", ["ye"] = "han", ["xuzhou"] = "tao" }, "ji"),
        };

        GUIStyle label, banner, small;

        void OnGUI()
        {
            var c = StagePlayer.ChronicleNow;
            if (c == null) return;
            label ??= new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, normal = { textColor = Ivory } };
            banner ??= new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Ivory } };
            small ??= new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter, wordWrap = true, normal = { textColor = Ivory } };
            float sw = Screen.width, sh = Screen.height, k = sh / 1080f;
            var m = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(k, k, 1f));
            float w = sw / k, h = 1080f;
            if (c.Layout == "settlement") { SettlementPage(w, h); GUI.matrix = m; return; }

            // Year banner.
            var yr = new Rect(w / 2 - 260, 24, 520, 64);
            Box(yr, 0.92f);
            GUI.Label(yr, StoryText.Line(c.Year), banner);

            // The chapter's trail (the settlement): an orange line through every city Liu Bei passed.
            if (c.Layout == "trail")
            {
                Vector2? last = null;
                foreach (var n in ThreeKingdoms.Gameplay.Story.Settlement.TrailNodes())
                {
                    if (!Cities.TryGetValue(n, out var p) && !StageHud.Nodes.TryGetValue(n, out p)) continue;
                    var at = new Vector2(p.x * w, p.y * h);
                    if (last is Vector2 a) DrawLine(a, at, 4f, new Color(0.93f, 0.52f, 0.18f, 0.9f));
                    Fill(new Rect(at.x - 6, at.y - 6, 12, 12), new Color(0.93f, 0.52f, 0.18f));
                    last = at;
                }
            }

            // Powers.
            if (Layouts.TryGetValue(c.Layout ?? "", out var layout))
            {
                var shown = new Dictionary<string, string>(layout.map);
                foreach (var kv in c.Changes) shown[kv.Key] = kv.Value;
                foreach (var kv in shown)
                {
                    if (!Cities.TryGetValue(kv.Key, out var p) || !Lords.TryGetValue(kv.Value, out var lord)) continue;
                    var at = new Vector2(p.x * w, p.y * h);
                    bool flash = kv.Key == c.EventNode && Time.time - c.EventAt < 1.6f && Mathf.Repeat(Time.time * 4f, 1f) < 0.5f;
                    var r = new Rect(at.x - 70, at.y - 64, 140, 34);
                    Fill(new Rect(r.x, r.y, 6, r.height + 18), lord.color);
                    Fill(r, new Color(Ink.r, Ink.g, Ink.b, 0.86f));
                    Fill(new Rect(r.x, r.y, r.width, 3), flash ? Teal : lord.color);
                    GUI.Label(r, lord.name, label);
                }
                if (Cities.TryGetValue(layout.liu, out var lp))
                {
                    var at = new Vector2(lp.x * w, lp.y * h);
                    var pr = new Rect(at.x - 34, at.y + 14, 68, 30);
                    Fill(pr, new Color(0.93f, 0.52f, 0.18f, 0.95f));
                    GUI.Label(pr, "刘备", label);
                }
            }

            // Event window.
            if (!string.IsNullOrEmpty(c.EventText) && Time.time - c.EventAt < 4f)
            {
                var er = new Rect(w / 2 - 330, 110, 660, 70);
                Box(er, 0.95f);
                if (Lords.TryGetValue(c.EventLord ?? "", out var lord)) Fill(new Rect(er.x, er.y, 8, er.height), lord.color);
                GUI.Label(er, StoryText.Line(c.EventText), small);
            }
            GUI.matrix = m;
        }

        // 篇章结算 (SYSTEMS_V05 §5; design image art/v05-ui-concept/ui_chapter_settlement): the trail on the realm map,
        // the ladder of names, and three panels (battles, people, reputation and choices). Read from the play-through only.
        GUIStyle title, head, big, body, tag, pin;
        static readonly Color Gold = new Color(0.84f, 0.73f, 0.47f), Orange = new Color(0.93f, 0.52f, 0.18f);

        void SettlementPage(float w, float h)
        {
            title ??= new GUIStyle(GUI.skin.label) { fontSize = 48, fontStyle = FontStyle.Bold, normal = { textColor = Ivory } };
            head ??= new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, normal = { textColor = Ivory } };
            big ??= new GUIStyle(GUI.skin.label) { fontSize = 64, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Teal } };
            body ??= new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, wordWrap = true, normal = { textColor = new Color(0.71f, 0.78f, 0.84f) } };
            tag ??= new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter, normal = { textColor = Gold } };
            pin ??= new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleLeft, normal = { textColor = Ivory } };
            Fill(new Rect(0, 0, w, 866), Ink);   // the dialogue box below stays visible (IMGUI draws over it)
            GUI.Label(new Rect(40, 14, 460, 70), "第一篇章　崛起", title);
            GUI.Label(new Rect(420, 24, 300, 50), "/　篇章结算", head);

            // The realm map with the trail.
            var mapBox = new Rect(30, 96, w - 500, 580);
            Box(mapBox, 1f);
            var hud = GetComponent<StageHud>();
            var map = hud ? hud.Map : null;
            var mr = mapBox;
            if (map)
            {
                float s = Mathf.Min((mapBox.width - 8) / map.width, (mapBox.height - 8) / map.height);
                mr = new Rect(mapBox.center.x - map.width * s / 2, mapBox.center.y - map.height * s / 2, map.width * s, map.height * s);
                GUI.DrawTexture(mr, map, ScaleMode.StretchToFill);
            }
            Vector2 On(Vector2 p) => new Vector2(mr.x + p.x * mr.width, mr.y + p.y * mr.height);
            bool Where(string n, out Vector2 p) => StageHud.Nodes.TryGetValue(n, out p) || Cities.TryGetValue(n, out p);
            Vector2? last = null;
            foreach (var n in Settlement.TrailNodes())
            {
                if (!Where(n, out var p)) continue;
                var at = On(p);
                if (last is Vector2 a && a != at) DrawLine(a, at, 3f, new Color(Orange.r, Orange.g, Orange.b, 0.85f));
                last = at;
            }
            var stations = Settlement.Stations();
            for (int i = 0; i < stations.Count; i++)
            {
                if (!Where(stations[i].node, out var p)) continue;
                var at = On(p);
                Fill(new Rect(at.x - 6, at.y - 6, 12, 12), i == stations.Count - 1 ? Teal : Orange);
                // Small tags, alternately right and left of the dot, so neighbouring cities do not cover each other.
                string text = $"{i + 1:00} {stations[i].name}";
                float tw = 22 + 15 * stations[i].name.Length;
                var lr = i % 2 == 0 ? new Rect(at.x + 8, at.y - 10, tw, 20) : new Rect(at.x - 8 - tw, at.y - 10, tw, 20);
                Fill(lr, new Color(Ink.r, Ink.g, Ink.b, 0.85f));
                GUI.Label(new Rect(lr.x + 3, lr.y, lr.width, lr.height), text, pin);
            }
            var legend = new Rect(mapBox.x + 16, mapBox.y + 14, 250, 34);
            Fill(legend, new Color(Ink.r, Ink.g, Ink.b, 0.9f));
            Fill(new Rect(legend.x + 12, legend.center.y - 2, 34, 4), Orange);
            GUI.Label(new Rect(legend.x + 50, legend.y, 200, 34), $"刘备足迹 · {stations.Count} 站", label);

            // The ladder of names (the top one reached).
            var lad = new Rect(w - 450, 96, 420, 764);
            Box(lad, 1f);
            GUI.Label(new Rect(lad.x, lad.y + 10, lad.width, 50), "名分阶梯", banner);
            Fill(new Rect(lad.x + 37, lad.y + 120, 2, 560), Silver);
            for (int i = 0; i < Settlement.LadderSteps.Length; i++)
            {
                var r = new Rect(lad.x + 60, lad.y + 80 + i * 108, lad.width - 90, 86);
                bool top = i == 0;
                Fill(r, top ? new Color(0.086f, 0.29f, 0.37f) : new Color(0.125f, 0.21f, 0.29f));
                if (top) { Fill(new Rect(r.x, r.y, r.width, 2), Teal); Fill(new Rect(r.x, r.yMax - 2, r.width, 2), Teal); }
                Fill(new Rect(lad.x + 30, r.center.y - 8, 16, 16), top ? Teal : Silver);
                GUI.Label(new Rect(r.x + 24, r.y, 220, r.height), Settlement.LadderSteps[i], head);
                if (top) GUI.Label(new Rect(r.xMax - 100, r.y + 26, 84, 34), "已达成", tag);
            }

            // Three panels under the map (above the dialogue box).
            float py = 692, ph = 168, pw = (w - 500 - 40) / 3f;
            var p1 = new Rect(30, py, pw, ph);
            var p2 = new Rect(p1.xMax + 20, py, pw, ph);
            var p3 = new Rect(p2.xMax + 20, py, pw, ph);
            foreach (var r in new[] { p1, p2, p3 }) Box(r, 1f);
            GUI.Label(new Rect(p1.x + 20, p1.y + 8, 200, 40), "战斗", head);
            GUI.Label(new Rect(p1.x, p1.y + 44, p1.width, 76), "13 / 13", big);
            GUI.Label(new Rect(p1.x, p1.y + 118, p1.width, 40), $"主线全部告捷 · 选做 {Settlement.OptionalDone}/2", body);
            GUI.Label(new Rect(p2.x + 20, p2.y + 8, 200, 40), "人物", head);
            GUI.Label(new Rect(p2.x, p2.y + 44, p2.width, 76), Settlement.Remembering.ToString(), big);
            GUI.Label(new Rect(p2.x, p2.y + 118, p2.width, 40), $"记住刘备的人 · 下邳回声 {Settlement.EchoesHeard}/{Settlement.EchoCount}", body);
            GUI.Label(new Rect(p3.x + 20, p3.y + 8, p3.width - 40, 40), $"声望 {Settlement.Reputation} · 关键抉择", head);
            var choices = Settlement.ChoiceList();
            float cw = (p3.width - 20) / Mathf.Max(1, choices.Count);
            for (int i = 0; i < choices.Count; i++)
            {
                var cr = new Rect(p3.x + 10 + i * cw, p3.y + 56, cw - 6, 100);
                Fill(new Rect(cr.x + 4, cr.y, cr.width - 8, 2), Gold);
                GUI.Label(new Rect(cr.x, cr.y + 8, cr.width, 36), choices[i].topic, tag);
                GUI.Label(new Rect(cr.x, cr.y + 46, cr.width, 50), choices[i].choice, body);
            }
        }

        void Box(Rect r, float alpha)
        {
            Fill(r, new Color(Ink.r, Ink.g, Ink.b, alpha));
            Fill(new Rect(r.x, r.y, r.width, 1.5f), Silver); Fill(new Rect(r.x, r.yMax - 1.5f, r.width, 1.5f), Silver);
            Fill(new Rect(r.x, r.y, 1.5f, r.height), Silver); Fill(new Rect(r.xMax - 1.5f, r.y, 1.5f, r.height), Silver);
        }

        static void DrawLine(Vector2 a, Vector2 b, float width, Color c)
        {
            var m = GUI.matrix;
            float angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(angle, a);
            Fill(new Rect(a.x, a.y - width / 2f, (b - a).magnitude, width), c);
            GUI.matrix = m;
        }

        static void Fill(Rect r, Color c)
        {
            var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = o;
        }
    }
}
