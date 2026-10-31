using System.IO;
using UnityEngine;
using UnityEngine.Video;

namespace GlorifiedBody.Core
{
    /// <summary>
    /// The one way to give a VideoPlayer its clip (2026-10-31, the web build). The web player cannot play an imported
    /// VideoClip (Unity manual, "Video playback in Web"): there the player plays the clip's file by URL from
    /// StreamingAssets/Video/, where the web build copies every clip (PlayerBuild). Every other platform plays the clip.
    /// </summary>
    public static class VideoClips
    {
        public const string Folder = "Video";

        public static void Assign(VideoPlayer player, VideoClip clip)
        {
            player.errorReceived -= OnError;
            player.errorReceived += OnError;
#if UNITY_WEBGL && !UNITY_EDITOR
            player.source = VideoSource.Url;
            player.url = clip != null ? Application.streamingAssetsPath + "/" + Folder + "/" + FileName(clip) : string.Empty;
#else
            player.source = VideoSource.VideoClip;
            player.clip = clip;
#endif
        }

        private static void OnError(VideoPlayer player, string message) =>
            Debug.LogWarning($"[Video] {(player.source == VideoSource.Url ? player.url : player.clip != null ? player.clip.name : "no clip")}: {message}");

        /// <summary>The clip's file name in StreamingAssets/Video: its asset name and its source extension (.webm: the Linux editor has no H.264).</summary>
        public static string FileName(VideoClip clip)
        {
            string extension = Path.GetExtension(clip.originalPath);
            return clip.name + (string.IsNullOrEmpty(extension) ? ".webm" : extension);
        }
    }
}
