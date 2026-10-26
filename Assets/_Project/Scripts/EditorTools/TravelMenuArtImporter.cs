using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ThreeKingdoms.EditorTools
{
    // The travel menu's art (user 2026-10-24, GAME_MENU_UI_REDESIGN.md): the Codex slices from art/travel-menu-ui-slices
    // (frames, icons, item and skill pictures, save thumbnails) copied to Art/UI/TravelMenu, with their nine-slice
    // borders read from the delivery manifest; and the military-book screen's slices (Prototypes/BingshuTactics/Art/
    // Destiny) shared for panel frames, gems, scrolls and roster busts. Painted UI art: bilinear, mipmaps, no compression.
    // A missing delivery leaves the menu on its drawn fallbacks.
    public static class TravelMenuArtImporter
    {
        const string Source = @"D:\BuildArtifacts\Codex\hd2d-jrpg\art\travel-menu-ui-slices";
        const string SectorSource = @"D:\BuildArtifacts\Codex\hd2d-jrpg\art\bingshu-sector-highlights";
        const string VacantSource = @"D:\BuildArtifacts\Codex\hd2d-jrpg\art\bingshu-vacant-dim";
        public const string Dir = "Assets/_Project/Art/UI/TravelMenu";
        public const string BingshuDir = "Assets/Prototypes/BingshuTactics/Art/Destiny";

        [System.Serializable] sealed class Border { public int left, right, top, bottom; }
        [System.Serializable] sealed class Asset { public string file; public Border nine_slice; }
        [System.Serializable] sealed class Manifest { public Asset[] assets; }

        [MenuItem("JRPG/Import travel menu art")]
        public static void ImportMenuItem() => Import(out _, out _);

        public static void Import(out Texture2D[] art, out string[] borders)
        {
            Directory.CreateDirectory(Dir);
            // The 命盘's lit bagua trapezoids (user 2026-10-24), each on the plate's own 720×720 canvas.
            if (Directory.Exists(SectorSource))
                foreach (var src in Directory.GetFiles(SectorSource, "sector_lit_*.png"))
                    File.Copy(src, $"{Dir}/{Path.GetFileName(src)}", true);
            // Dark inactive directions and the hollow empty socket (user 2026-10-26).
            if (Directory.Exists(VacantSource))
                foreach (var src in Directory.GetFiles(VacantSource, "*.png").Where(f => Path.GetFileName(f).StartsWith("sector_dim_") || Path.GetFileName(f).StartsWith("socket_vacant")))
                    File.Copy(src, $"{Dir}/{Path.GetFileName(src)}", true);
            if (Directory.Exists(Source))
            {
                foreach (var src in Directory.GetFiles(Source, "*.png"))
                {
                    var file = Path.GetFileName(src);
                    if (file.StartsWith("contact") || file.StartsWith("preview") || file.StartsWith("mockup")) continue;
                    File.Copy(src, $"{Dir}/{file}", true);
                }
                var manifestSrc = Path.Combine(Source, "manifest.json");
                if (File.Exists(manifestSrc)) File.Copy(manifestSrc, $"{Dir}/manifest.json", true);
            }
            var list = new List<Texture2D>();
            foreach (var path in Directory.GetFiles(Dir, "*.png").Select(p => p.Replace('\\', '/')))
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                imp.textureType = TextureImporterType.Default; imp.mipmapEnabled = true; imp.alphaIsTransparency = true;
                imp.npotScale = TextureImporterNPOTScale.None; imp.filterMode = FilterMode.Trilinear;
                imp.textureCompression = TextureImporterCompression.Uncompressed; imp.wrapMode = TextureWrapMode.Clamp;
                imp.SaveAndReimport();
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (t) list.Add(t);
            }
            art = list.ToArray();

            var b = new List<string>();
            var manifestPath = $"{Dir}/manifest.json";
            if (File.Exists(manifestPath))
            {
                var m = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
                foreach (var a in m?.assets ?? new Asset[0])
                    if (a?.file != null && a.nine_slice != null && (a.nine_slice.left + a.nine_slice.right + a.nine_slice.top + a.nine_slice.bottom) > 0)
                        b.Add($"{Path.GetFileNameWithoutExtension(a.file)},{a.nine_slice.left},{a.nine_slice.right},{a.nine_slice.top},{a.nine_slice.bottom}");
            }
            borders = b.ToArray();
            Debug.Log($"TRAVEL_MENU_ART {art.Length} textures, {borders.Length} nine-slices");
        }

        public static Texture2D[] BingshuSlices() =>
            Directory.Exists(BingshuDir)
                ? Directory.GetFiles(BingshuDir, "*.png").Select(p => AssetDatabase.LoadAssetAtPath<Texture2D>(p.Replace('\\', '/'))).Where(t => t).ToArray()
                : new Texture2D[0];
    }
}
