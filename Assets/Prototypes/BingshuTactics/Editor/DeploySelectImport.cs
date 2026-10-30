using UnityEditor;

namespace Prototype.BingshuTactics.EditorTools
{
    // The deploy-select slices (Codex art/deploy-select-slices) are UI art drawn 1:1-ish: no mipmaps, high-quality
    // compression, clamped edges so nine-slice borders do not bleed.
    sealed class DeploySelectImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Resources/DeploySelect/")) return;
            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Default;
            t.mipmapEnabled = false;
            t.alphaIsTransparency = true;
            t.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            t.npotScale = TextureImporterNPOTScale.None;
            t.textureCompression = TextureImporterCompression.CompressedHQ;
            t.maxTextureSize = 2048;
        }
    }
}
