using System.IO;
using UnityEditor;
using UnityEngine;

namespace ThreeKingdoms.EditorTools
{
    // 换色 (the NPC bible §8, producer §4.2): one-scene historical guests and original side-quest people reuse an existing
    // Q sheet with its clothes turned to another hue. Skin, hair and outlines stay (hues 10–50° and greys are left alone),
    // so a face never turns green. The recoloured atlas is its own character key next to the others.
    internal static class NpcRecolor
    {
        // key: the new character key; from: the existing key; hue: degrees to turn; sat, val: multipliers.
        internal static string Make(string key, string from, float hue, float sat = 1f, float val = 1f)
        {
            string src = HD2DSampleBuilder.AtlasPath(from), dst = HD2DSampleBuilder.AtlasPath(key);
            if (!File.Exists(src)) return from;
            var srcTex = new Texture2D(2, 2);
            srcTex.LoadImage(File.ReadAllBytes(src));
            var px = srcTex.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                var p = px[i];
                if (p.a == 0) continue;
                Color.RGBToHSV(p, out float h, out float s, out float v);
                float deg = h * 360f;
                if (s < 0.12f || (deg >= 10f && deg <= 50f && v > 0.35f)) continue;   // greys, outlines, skin
                var c = Color.HSVToRGB(Mathf.Repeat(h + hue / 360f, 1f), Mathf.Clamp01(s * sat), Mathf.Clamp01(v * val));
                px[i] = new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), p.a);
            }
            var outTex = new Texture2D(srcTex.width, srcTex.height, TextureFormat.RGBA32, false);
            outTex.SetPixels32(px);
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            File.WriteAllBytes(dst, outTex.EncodeToPNG());
            AssetDatabase.ImportAsset(dst);
            if (AssetImporter.GetAtPath(src) is TextureImporter si && AssetImporter.GetAtPath(dst) is TextureImporter di)
            {
                var ts = new TextureImporterSettings();
                si.ReadTextureSettings(ts);
                di.SetTextureSettings(ts);
                di.textureCompression = si.textureCompression;
                di.maxTextureSize = si.maxTextureSize;
                di.SaveAndReimport();
            }
            HD2DSampleBuilder.PixelMaterial(key, AssetDatabase.LoadAssetAtPath<Texture2D>(dst));
            return key;
        }
    }
}
