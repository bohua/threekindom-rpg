using System.IO;
using ThreeKingdoms.UI.Story;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThreeKingdoms.EditorTools
{
    // Art and film for the story cinematics (user 2026-10-24, design/gdd/story-cinematics.md): the ink-scroll paintings,
    // seal, roller and ink drop go to Art/Cinematics; the oath altar and petal sprites join the town textures; the
    // opening film is copied under StreamingAssets (git-ignored, 63 MB). Each file takes the first source that exists:
    // the Codex implementation batch (art/story-cinematics), else the approved concept delivery. A missing source
    // keeps what the project already has; with nothing at all the view draws plain paper and a plain seal.
    public static class CinematicsArtImporter
    {
        const string Root = @"D:\BuildArtifacts\Codex\hd2d-jrpg";
        public const string Dir = "Assets/_Project/Art/Cinematics";
        public const string VideoAsset = "Assets/StreamingAssets/Video/opening.mp4";
        const string VideoSource = @"opening-assembled-20261021-v1\THREE_KINGDOMS_FANTASY_OPENING_720P.mp4";

        static readonly (string Name, string[] Sources)[] Scroll =
        {
            ("scroll_taoyuan", new[] { @"art\story-cinematics\painting_taoyuan.png", @"art\story-cinematics-concept\_sources\03_ink_raw.png" }),
            ("scroll_huangjin", new[] { @"art\story-cinematics\painting_huangjin.png" }),
            // 0.2 (Codex art/v02-ink-scrolls-b1; the approved thumbnails stand in until the full paintings land).
            ("scroll_zhuanzhan", new[] { @"art\v02-ink-scrolls-b1\painting_zhuanzhan.png", @"art\v02-ink-scrolls-concept\scroll2_huangjin_zhuanzhan_thumb.png" }),
            ("scroll_anxi", new[] { @"art\v02-ink-scrolls-b1\painting_anxi.png", @"art\v02-ink-scrolls-concept\scroll3_anxi_guayin_thumb.png" }),
            // 0.3 (Codex art/v03-ink-scrolls; the approved thumbnails stand in until the full paintings land).
            ("scroll_wenjiu", new[] { @"art\v03-ink-scrolls\painting_wenjiu.png", @"art\v03-ink-scrolls-concept\scroll_wenjiu_huaxiong_thumb.png" }),
            ("scroll_sanying", new[] { @"art\v03-ink-scrolls\painting_sanying.png", @"art\v03-ink-scrolls-concept\scroll_sanying_lubu_thumb.png" }),
            ("scroll_fen_luoyang", new[] { @"art\v03-ink-scrolls\painting_fen_luoyang.png", @"art\v03-ink-scrolls-concept\scroll_fen_luoyang_thumb.png" }),
            ("scroll_panhe", new[] { @"art\v04-ink-scrolls\painting_panhe.png", @"art\v04-ink-scrolls-concept\scroll_panhe_thumb.png" }),
            ("scroll_jieqiao", new[] { @"art\v04-ink-scrolls\painting_jieqiao.png", @"art\v04-ink-scrolls-concept\scroll_jieqiao_thumb.png" }),
            // 0.5 (Codex art/v05-ink-scrolls)
            ("scroll_beihai", new[] { @"art\v05-ink-scrolls\painting_beihai.png", @"art\v04-ink-scrolls\painting_jieqiao.png" }),
            ("scroll_sanrang", new[] { @"art\v05-ink-scrolls\painting_sanrang.png", @"art\v04-ink-scrolls\painting_panhe.png" }),
            ("seal_done", new[] { @"art\story-cinematics\seal_done.png", @"art\story-cinematics-concept\_checks\seal_clean_alpha.png" }),
            ("scroll_roller", new[] { @"art\story-cinematics\scroll_roller.png" }),
            ("ink_drop", new[] { @"art\story-cinematics\ink_drop.png" }),
        };
        static readonly string[] TownSprites = { "oath_altar", "oath_petal" };

        [MenuItem("JRPG/Import story cinematics art and opening film")]
        public static void Import()
        {
            Directory.CreateDirectory(Dir);
            foreach (var (name, sources) in Scroll)
            {
                string from = First(sources);
                if (from == null) continue;
                string to = $"{Dir}/{name}.png";
                File.Copy(from, to, true);
                AssetDatabase.ImportAsset(to, ImportAssetOptions.ForceUpdate);
                var importer = (TextureImporter)AssetImporter.GetAtPath(to);
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 2048;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }
            // Pixel sprites for the grove: the town builder configures everything in its folder.
            foreach (var name in TownSprites)
            {
                string from = First(new[] { $@"art\story-cinematics\{name}.png" });
                if (from != null) File.Copy(from, $"{ZhuojunTownBuilder.TownDir}/{name}.png", true);
            }
            string film = Path.Combine(Root, VideoSource);
            if (File.Exists(film))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(VideoAsset));
                if (!File.Exists(VideoAsset) || new FileInfo(VideoAsset).Length != new FileInfo(film).Length) File.Copy(film, VideoAsset, true);
            }
            AssetDatabase.Refresh();
        }

        // The prologue 天下大势 (design/gdd/prologue.md): Codex art/prologue-art into Resources/Prologue, where the
        // player reads it by name. Scrolls are 3840 wide; the map layers keep their alpha. Missing files are skipped.
        public const string PrologueDir = "Assets/_Project/Resources/Prologue";
        static readonly string[] PrologueSources = { @"art\prologue-art", @"art\prologue-map-fx", @"art\prologue-three-armies", @"art\tianxia-regions-ui" };
        // Layers the map animation reads pixel by pixel (PrologueMapFx).
        static readonly string[] ReadableLayers = { "map_states.png", "cinnabar_circle.png", "march_arrows.png" };

        [MenuItem("JRPG/Import prologue art")]
        public static void ImportPrologue()
        {
            Directory.CreateDirectory(PrologueDir);
            foreach (var source in PrologueSources)
            {
                string from = Path.Combine(Root, source);
                if (!Directory.Exists(from)) { Debug.LogWarning("No prologue art at " + from); continue; }
                foreach (var file in Directory.GetFiles(from))
                {
                    string name = Path.GetFileName(file);
                    bool picture = name.EndsWith(".png") && name != "preview.png" && !name.StartsWith("card_") && !name.StartsWith("TIANXIA_");
                    if (!picture && (!name.EndsWith(".json") || name == "manifest.json")) continue;
                    string to = $"{PrologueDir}/{name}";
                    // Codex writes centre lines as [[x,y],…], which JsonUtility cannot read; store {"x":..,"y":..} points.
                    if (name.EndsWith(".json") && File.ReadAllText(file).Contains("\"centerline\"")) File.WriteAllText(to, CentreLines(File.ReadAllText(file)));
                    else File.Copy(file, to, true);
                    AssetDatabase.ImportAsset(to, ImportAssetOptions.ForceUpdate);
                    if (!picture) continue;
                    var importer = (TextureImporter)AssetImporter.GetAtPath(to);
                    importer.textureType = TextureImporterType.Default;
                    importer.mipmapEnabled = false;
                    importer.alphaIsTransparency = true;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.maxTextureSize = 4096;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.isReadable = System.Array.IndexOf(ReadableLayers, name) >= 0;
                    importer.SaveAndReimport();
                }
            }
            AssetDatabase.Refresh();
        }

        // Every "centerline" array in the file, as {"width","height","arrows":[{"points":[{"x","y"}…]}…]} (PrologueMapFx).
        static string CentreLines(string json)
        {
            var paths = new System.Collections.Generic.List<string>();
            for (int at = json.IndexOf("\"centerline\""); at >= 0; at = json.IndexOf("\"centerline\"", at + 1))
            {
                int open = json.IndexOf('[', at), depth = 0, end = open;
                for (; end < json.Length; end++)
                {
                    if (json[end] == '[') depth++;
                    else if (json[end] == ']' && --depth == 0) break;
                }
                var nums = System.Text.RegularExpressions.Regex.Matches(json.Substring(open, end - open), @"-?\d+(\.\d+)?");
                var pts = new System.Collections.Generic.List<string>();
                for (int i = 0; i + 1 < nums.Count; i += 2) pts.Add($"{{\"x\":{nums[i].Value},\"y\":{nums[i + 1].Value}}}");
                paths.Add("{\"points\":[" + string.Join(",", pts) + "]}");
            }
            return "{\"width\":1920,\"height\":1080,\"arrows\":[" + string.Join(",", paths) + "]}";
        }

        public static void ImportPrologueFromCommandLine()
        {
            ImportPrologue();
            EditorApplication.Exit(0);
        }

        static string First(string[] sources)
        {
            foreach (var s in sources)
            {
                string p = Path.Combine(Root, s);
                if (File.Exists(p)) return p;
            }
            return null;
        }

        static Texture2D Load(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{Dir}/{name}.png");

        // The full-screen cinematics layer above every other panel of the scene.
        public static StoryCinematicsView AddView(PanelSettings panel)
        {
            var go = new GameObject("Story Cinematics");
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            doc.sortingOrder = 100;
            var view = go.AddComponent<StoryCinematicsView>();
            view.Seal = Load("seal_done");
            view.Roller = Load("scroll_roller");
            view.InkDrop = Load("ink_drop");
            view.Paintings = new[]
            {
                new StoryCinematicsView.Painting { Id = "taoyuan", Texture = Load("scroll_taoyuan") },
                new StoryCinematicsView.Painting { Id = "huangjin", Texture = Load("scroll_huangjin") },
                new StoryCinematicsView.Painting { Id = "zhuanzhan", Texture = Load("scroll_zhuanzhan") },
                new StoryCinematicsView.Painting { Id = "anxi", Texture = Load("scroll_anxi") },
                new StoryCinematicsView.Painting { Id = "wenjiu", Texture = Load("scroll_wenjiu") },
                new StoryCinematicsView.Painting { Id = "sanying", Texture = Load("scroll_sanying") },
                new StoryCinematicsView.Painting { Id = "fen_luoyang", Texture = Load("scroll_fen_luoyang") },
                new StoryCinematicsView.Painting { Id = "panhe", Texture = Load("scroll_panhe") },
                new StoryCinematicsView.Painting { Id = "jieqiao", Texture = Load("scroll_jieqiao") },
                new StoryCinematicsView.Painting { Id = "beihai", Texture = Load("scroll_beihai") },
                new StoryCinematicsView.Painting { Id = "sanrang", Texture = Load("scroll_sanrang") },
            };
            return view;
        }
    }
}
