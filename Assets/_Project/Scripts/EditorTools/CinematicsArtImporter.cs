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
            };
            return view;
        }
    }
}
