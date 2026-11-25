#if UNITY_EDITOR
using UnityEditor;

namespace PocketPutt.EditorTools
{
    /// <summary>Import settings for the prerendered hub art (Assets/_PocketPutt/Resources/Hub): big, unmipped, high-quality compressed, never readable.</summary>
    public sealed class HubTextureImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/_PocketPutt/Resources/Hub/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            // the cup maquettes are cut-outs with a real alpha channel; the big map pictures are opaque
            bool home = assetPath.StartsWith("Assets/_PocketPutt/Resources/Hub/Home/");   // the Home hero is a cut-out too
            bool h6 = assetPath.StartsWith("Assets/_PocketPutt/Resources/Hub/H6/");           // H6 ambient floor patches: small feathered cut-outs
            bool cutout = home || h6 || assetPath.StartsWith("Assets/_PocketPutt/Resources/Hub/Dest/");
            ti.alphaSource = cutout ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            ti.alphaIsTransparency = cutout;
            ti.mipmapEnabled = false;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.maxTextureSize = h6 ? 512 : home ? 2048 : cutout ? 1024 : 4096;
            ti.isReadable = false;
            ti.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            ti.filterMode = UnityEngine.FilterMode.Bilinear;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.streamingMipmaps = false;
        }
    }
}
#endif
