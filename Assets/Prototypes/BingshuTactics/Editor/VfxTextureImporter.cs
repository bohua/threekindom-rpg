// 战棋特效素材的导入设置（BATTLE_VFX_DESIGN 第 5 节）：Resources/BingshuTactics/Vfx 下
// fx_* 是战场像素序列帧（点采样、无多级纹理、不压缩）；cutin_*、name_*、banner_* 是屏幕层高清插画（双线性、不压缩）。
using UnityEditor;
using UnityEngine;

namespace Prototype.BingshuTactics.EditorTools
{
    public sealed class VfxTextureImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.Contains("/Resources/BingshuTactics/Vfx/")) return;
            var imp = (TextureImporter)assetImporter;
            bool pixel = System.IO.Path.GetFileName(path).StartsWith("fx_");
            imp.textureType = TextureImporterType.Default;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.filterMode = pixel ? FilterMode.Point : FilterMode.Bilinear;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.maxTextureSize = 2048;
        }
    }
}
