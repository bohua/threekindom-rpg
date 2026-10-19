using System.IO;
using UnityEditor;
using UnityEngine;

namespace ThreeKingdoms.EditorTools
{
    // Shared warm "late-afternoon" grading LUT for town and battle volumes (ADR-0005 §5, §8).
    // Layout required by URP ColorLookup: 1024×32 strip (32 slices of 32×32, blue = slice, red = x, green = y),
    // linear (non-sRGB) import, no mips, uncompressed, clamped. The grade is applied after ACES, in sRGB-encoded
    // space, so the curve below works on display-referred values.
    public static class HD2DLutBuilder
    {
        public const string LutPath = "Assets/_Project/Settings/HD2D_WarmLUT.png";
        public const int Size = 32;

        public static Texture2D Build()
        {
            var tex = new Texture2D(Size * Size, Size, TextureFormat.RGBA32, false, true);
            var pixels = new Color32[Size * Size * Size];
            for (int b = 0; b < Size; b++)
            for (int g = 0; g < Size; g++)
            for (int r = 0; r < Size; r++)
            {
                var c = Grade(new Vector3(r, g, b) / (Size - 1));
                pixels[g * Size * Size + b * Size + r] = new Color32(To8(c.x), To8(c.y), To8(c.z), 255);
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(LutPath));
            File.WriteAllBytes(LutPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(LutPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(LutPath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(LutPath);
        }

        static byte To8(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);

        // Golden-hour grade: warm highlights, slightly warm lifted shadows, gentle S-curve, +12% saturation.
        public static Vector3 Grade(Vector3 c)
        {
            float luma = Vector3.Dot(c, new Vector3(0.2126f, 0.7152f, 0.0722f));
            c = Vector3.LerpUnclamped(new Vector3(luma, luma, luma), c, 1.12f);                         // saturation
            c = new Vector3(S(c.x), S(c.y), S(c.z));                                                    // contrast
            float hi = Mathf.SmoothStep(0.35f, 1f, luma), lo = 1f - Mathf.SmoothStep(0f, 0.35f, luma);
            c += hi * new Vector3(0.035f, 0.012f, -0.045f);                                             // warm highlights
            c += lo * new Vector3(0.025f, 0.012f, 0.0f);                                                // warm, lifted shadows
            return new Vector3(Mathf.Clamp01(c.x), Mathf.Clamp01(c.y), Mathf.Clamp01(c.z));
        }

        static float S(float x) => Mathf.Lerp(x, x * x * (3f - 2f * x), 0.25f);
    }
}
