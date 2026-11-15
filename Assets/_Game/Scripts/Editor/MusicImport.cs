using UnityEditor;
using UnityEngine;

namespace GlorifiedBody.Editor
{
    /// <summary>
    /// The import settings of the music (2026-11-14, the T43 kit; the T44 kit since 2026-11-15): every clip under Resources/Audio/Music/. The source
    /// stays a WAV (no MP3: an MP3 has padding at its start, a loop gets a gap). In the game: Vorbis in memory at high
    /// quality, loaded with the asset (a scheduled section must not wait for a load), the sample rate kept (48 kHz: one
    /// bar at 83 BPM is 138,795.18 samples), stereo. The files come from tools/music_kit_import.sh.
    /// </summary>
    public sealed class MusicImport : AssetPostprocessor
    {
        public const string MusicFolder = "Assets/_Game/Resources/Audio/Music/";
        public const float Quality = 0.8f;

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(MusicFolder) || !assetPath.EndsWith(".wav")) return;
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = false;
            importer.loadInBackground = false;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = Quality;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
        }
    }
}
