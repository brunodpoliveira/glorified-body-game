#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GlorifiedBody.Editor
{
    /// <summary>
    /// The web build's texture budget (2026-10-31, itch.io: 500 MB for all files, 200 MB for one). The look textures and
    /// a few others import uncompressed for clarity (2K RGB: 16 MB each, 91 % of the Linux build is textures). For a web
    /// import only, this compresses them (DXT, at most 2048). The desktop builds keep them as they are. Like the gore
    /// postprocessor, it changes the import result only and puts the importer back, so no .meta file changes.
    /// </summary>
    public sealed class WebTexturePostprocessor : AssetPostprocessor
    {
        public const uint Version = 1;
        public const int MaxSize = 2048;
        private const string WebPlatform = "WebGL";

        private static readonly Dictionary<string, TextureImporterPlatformSettings> Originals = new();

        public override uint GetVersion() => Version;

        private void OnPreprocessTexture()
        {
            if (context.selectedBuildTarget != BuildTarget.WebGL) return;
            var importer = (TextureImporter)assetImporter;
            TextureImporterPlatformSettings web = importer.GetPlatformTextureSettings(WebPlatform);
            if (web.overridden || importer.textureCompression != TextureImporterCompression.Uncompressed) return;
            Originals[assetPath] = web;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = WebPlatform,
                overridden = true,
                maxTextureSize = Mathf.Min(importer.maxTextureSize, MaxSize),
                format = TextureImporterFormat.Automatic,
                textureCompression = TextureImporterCompression.Compressed,
            });
        }

        private void OnPostprocessTexture(Texture2D texture)
        {
            if (!Originals.TryGetValue(assetPath, out TextureImporterPlatformSettings original)) return;
            Originals.Remove(assetPath);
            var importer = (TextureImporter)assetImporter;
            importer.SetPlatformTextureSettings(original);
            EditorUtility.ClearDirty(importer);
        }
    }
}
#endif
