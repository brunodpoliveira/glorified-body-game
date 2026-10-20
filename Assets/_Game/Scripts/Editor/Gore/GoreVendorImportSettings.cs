#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace GlorifiedBody.Editor.Gore
{
    /// <summary>
    /// GTX 1050 budget for the ThatIndividual gore vendor folder: every texture imports at max size 2048 with
    /// normal-quality compression (some source PNGs are 45-111 MB).
    ///
    /// <see cref="GoreVendorTexturePostprocessor"/> applies these settings at import time, in every checkout
    /// and after every vendor re-import, with no manual step. It changes the import result only: it clears the
    /// importer's dirty flag after import, so a later AssetDatabase.SaveAssets does not write the vendor .meta.
    /// Apply() only forces a re-import of the vendor textures, for a Library imported before the postprocessor.
    ///
    /// Batch: tools/unity.sh exec GlorifiedBody.Editor.Gore.GoreVendorImportSettings.Apply
    /// Writes gore-import.json to the run dir when -gbRunDir is given.
    /// </summary>
    public static class GoreVendorImportSettings
    {
        public const string VendorRoot = "Assets/ThatIndividual STORE ASSETS/GoreSystemsByThatIndividual";
        public const int MaxSize = 2048;
        private static readonly string[] Platforms = { "Standalone" };

        [MenuItem("GlorifiedBody/Gore/Re-import ThatIndividual Textures")]
        public static void Apply()
        {
            var paths = new List<string>();
            if (!AssetDatabase.IsValidFolder(VendorRoot))
            {
                Debug.LogWarning($"[GoreImport] {VendorRoot} not found. Nothing to do.");
                WriteReport(paths, false);
                return;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { VendorRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is TextureImporter)
                {
                    paths.Add(path);
                }
            }

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string path in paths)
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            Debug.Log($"[GoreImport] Re-imported {paths.Count} vendor textures through GoreVendorTexturePostprocessor (max {MaxSize}, compressed).");
            WriteReport(paths, true);
        }

        /// <summary>True for asset paths inside the vendor gore folder.</summary>
        public static bool IsVendorPath(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath) &&
                   assetPath.Replace('\\', '/').StartsWith(VendorRoot + "/", System.StringComparison.Ordinal);
        }

        /// <summary>Sets the budget on an importer in memory. Returns true when a setting changed. Does not save.</summary>
        public static bool Configure(TextureImporter importer)
        {
            bool dirty = false;
            if (importer.maxTextureSize > MaxSize)
            {
                importer.maxTextureSize = MaxSize;
                dirty = true;
            }

            if (importer.textureCompression == TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Compressed;
                dirty = true;
            }

            // BC/DXT needs sides that are a multiple of 4. The vendor flipbooks (9600x5760 and similar) are
            // not, and import uncompressed (RGBA32) unless they scale to a power of two. UVs are 0-1, so the
            // flipbook grid still lines up.
            if (importer.npotScale == TextureImporterNPOTScale.None)
            {
                importer.npotScale = TextureImporterNPOTScale.ToNearest;
                dirty = true;
            }

            if (importer.crunchedCompression)
            {
                // Crunch costs import time for no VRAM gain over plain DXT/BC.
                importer.crunchedCompression = false;
                dirty = true;
            }

            foreach (string platform in Platforms)
            {
                TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
                if (settings.overridden && settings.maxTextureSize > MaxSize)
                {
                    settings.maxTextureSize = MaxSize;
                    importer.SetPlatformTextureSettings(settings);
                    dirty = true;
                }
            }

            return dirty;
        }

        /// <summary>
        /// Imported vendor textures above the budget: larger than <see cref="MaxSize"/> or not compressed.
        /// Checks the imported texture, which is what uses VRAM, not the importer settings.
        /// Returns -1 when the vendor folder is missing.
        /// </summary>
        public static int CountOverBudget(List<string> offenders = null)
        {
            if (!AssetDatabase.IsValidFolder(VendorRoot))
            {
                return -1;
            }

            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { VendorRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter))
                {
                    continue;
                }

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture != null && IsOverBudget(texture))
                {
                    count++;
                    offenders?.Add($"{path} ({texture.width}x{texture.height} {texture.graphicsFormat})");
                }
            }

            return count;
        }

        public static bool IsOverBudget(Texture2D texture)
        {
            return texture.width > MaxSize || texture.height > MaxSize ||
                   !GraphicsFormatUtility.IsCompressedFormat(texture.graphicsFormat);
        }

        private static void WriteReport(List<string> paths, bool vendorPresent)
        {
            string runDir = GetArg("-gbRunDir");
            if (string.IsNullOrEmpty(runDir))
            {
                return;
            }

            var offenders = new List<string>();
            int over = vendorPresent ? CountOverBudget(offenders) : -1;
            var sb = new StringBuilder();
            sb.Append("{\n  \"vendor_present\": ").Append(vendorPresent ? "true" : "false")
              .Append(",\n  \"vendor_textures\": ").Append(paths.Count)
              .Append(",\n  \"over_budget_after\": ").Append(over)
              .Append(",\n  \"max_size\": ").Append(MaxSize)
              .Append(",\n  \"offenders\": [");
            for (int i = 0; i < offenders.Count; i++)
            {
                sb.Append(i == 0 ? "\n    \"" : ",\n    \"").Append(offenders[i].Replace("\"", "'")).Append('"');
            }
            sb.Append("\n  ]\n}\n");
            Directory.CreateDirectory(runDir);
            File.WriteAllText(Path.Combine(runDir, "gore-import.json"), sb.ToString());
        }

        internal static string GetArg(string name)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Caps the ThatIndividual vendor textures at import time (see <see cref="GoreVendorImportSettings"/>).
    /// Whatever the vendor .meta asks for, the import is at most 2048 and compressed, and the .meta stays as
    /// it is. Unity keeps the importer object after import, and AssetDatabase.SaveAssets writes changed
    /// importers to their .meta. So OnPostprocessTexture puts the original settings back once the texture is
    /// built. Raise <see cref="Version"/> after a rule change: Unity then re-imports the textures.
    /// </summary>
    public sealed class GoreVendorTexturePostprocessor : AssetPostprocessor
    {
        public const uint Version = 2;

        /// <summary>Extra folder that gets the same rule. Only tests set it, for a temp folder outside the vendor folder.</summary>
        internal static string TestRoot;

        private struct Snapshot
        {
            public int MaxSize;
            public TextureImporterCompression Compression;
            public bool Crunched;
            public TextureImporterNPOTScale Npot;
            public TextureImporterPlatformSettings Standalone;
        }

        private static readonly Dictionary<string, Snapshot> Originals = new();

        public override uint GetVersion() => Version;

        private void OnPreprocessTexture()
        {
            if (!Applies(assetPath))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            Originals[assetPath] = new Snapshot
            {
                MaxSize = importer.maxTextureSize,
                Compression = importer.textureCompression,
                Crunched = importer.crunchedCompression,
                Npot = importer.npotScale,
                Standalone = importer.GetPlatformTextureSettings("Standalone")
            };
            GoreVendorImportSettings.Configure(importer);
        }

        private void OnPostprocessTexture(Texture2D texture)
        {
            if (!Originals.TryGetValue(assetPath, out Snapshot original))
            {
                return;
            }

            Originals.Remove(assetPath);
            var importer = (TextureImporter)assetImporter;
            importer.maxTextureSize = original.MaxSize;
            importer.textureCompression = original.Compression;
            importer.crunchedCompression = original.Crunched;
            importer.npotScale = original.Npot;
            importer.SetPlatformTextureSettings(original.Standalone);
            EditorUtility.ClearDirty(importer);
        }

        internal static bool Applies(string path)
        {
            if (GoreVendorImportSettings.IsVendorPath(path))
            {
                return true;
            }

            return !string.IsNullOrEmpty(TestRoot) && path != null &&
                   path.StartsWith(TestRoot + "/", System.StringComparison.Ordinal);
        }
    }
}
#endif
