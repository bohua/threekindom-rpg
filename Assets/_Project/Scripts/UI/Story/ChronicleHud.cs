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
