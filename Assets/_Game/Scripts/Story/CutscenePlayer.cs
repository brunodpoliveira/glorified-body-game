using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using GlorifiedBody.Audio;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Saving;
using GlorifiedBody.Subtitles;
using GlorifiedBody.Systems;
using GlorifiedBody.UI;

namespace GlorifiedBody.Story
{
    /// <summary>
    /// Plays a CutsceneAsset over the room (the design sweep's rank 4). The game goes to its Cutscene state: the world
    /// stops, no game key passes, the world sounds pause, the music holds the cutscene's track or the silence. Each shot
    /// fills the screen (a still with its pan and zoom, or a video), its line plays on the Voice channel, and its
    /// subtitle shows "SPEAKER: words". The skip key (Interact, Escape or Enter) ends it at once. At the end the screen
    /// fades through black back to the room, the done flag goes up, and the game plays again.
    /// </summary>
    public sealed class CutscenePlayer : MonoBehaviour
    {
        public const string SubtitleKey = "cutscene";
        public const float FadeSeconds = 0.35f;
        public const float SkipGuardSeconds = 0.3f;

        private static CutscenePlayer instance;
        private Canvas canvas;
        private RawImage picture;
        private VideoPlayer video;
        private AudioSource videoSound;
        private RenderTexture videoTexture;
        private bool skipped;

        public static bool IsPlaying { get; private set; }
        public static string Current { get; private set; } = "";
        public static int ShotIndex { get; private set; } = -1;
        public static int Played { get; private set; }
        public static int Skipped { get; private set; }
        public static Texture Showing => instance != null && instance.picture != null && instance.canvas.enabled ? instance.picture.texture : null;
        public static Rect ShowingRect => instance != null && instance.picture != null ? instance.picture.uvRect : default;

        /// <summary>Plays the cutscene. False when the game cannot stop for it (not playing) or one plays.</summary>
        public static bool Play(CutsceneAsset asset)
        {
            if (asset == null || IsPlaying) return false;
            if (GameManager.Instance != null && !GameManager.Instance.BeginCutscene()) return false;
            Ensure().StartCoroutine(instance.Run(asset));
            return true;
        }

        /// <summary>Ends the playing cutscene now (the skip key, and tests).</summary>
        public static void Skip()
        {
            if (instance != null && IsPlaying) instance.skipped = true;
        }

        private static CutscenePlayer Ensure()
        {
            if (instance != null) return instance;
            var obj = new GameObject("[Cutscene]");
            DontDestroyOnLoad(obj);
            instance = obj.AddComponent<CutscenePlayer>();
            instance.Build();
            return instance;
        }

        private void Build()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // over the HUD (60), under the subtitles (SubtitleUI, 112) and the fade (1000)
            canvas.enabled = false;
            var back = new GameObject("Black", typeof(RectTransform));
            back.transform.SetParent(transform, false);
            Stretch(back.GetComponent<RectTransform>());
            back.AddComponent<Image>().color = Color.black;
            var pic = new GameObject("Picture", typeof(RectTransform));
            pic.transform.SetParent(transform, false);
            Stretch(pic.GetComponent<RectTransform>());
            picture = pic.AddComponent<RawImage>();
            picture.raycastTarget = false;
            video = gameObject.AddComponent<VideoPlayer>();
            video.playOnAwake = false;
            video.isLooping = false;
            video.renderMode = VideoRenderMode.RenderTexture;
            video.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            videoSound = gameObject.AddComponent<AudioSource>();
            GameAudio.Bind(videoSound, SoundChannel.Voice, 1f, spatial: false);
            videoSound.ignoreListenerPause = true;
            video.audioOutputMode = VideoAudioOutputMode.AudioSource;
            video.SetTargetAudioSource(0, videoSound);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private IEnumerator Run(CutsceneAsset asset)
        {
            IsPlaying = true;
            skipped = false;
            Current = asset.cutsceneId;
            Played++;
            RunTelemetry.Event("cutscene_start", asset.cutsceneId);
            MusicDirector.Score(asset.music);
            bool fadeDone = false;
            ScreenFade.Out(FadeSeconds, () => fadeDone = true);
            while (!fadeDone) yield return null;
            canvas.enabled = true;
            ScreenFade.In(FadeSeconds);
            float startedAt = Time.unscaledTime;

            for (ShotIndex = 0; ShotIndex < asset.shots.Length && !skipped; ShotIndex++)
            {
                CutsceneAsset.Shot shot = asset.shots[ShotIndex];
                if (shot == null) continue;
                AudioSource line = null;
                if (!string.IsNullOrEmpty(shot.text))
                {
                    SubtitleService.Show(SubtitleKey, string.IsNullOrEmpty(shot.speaker) ? SubtitleService.UnknownSpeaker : shot.speaker, shot.text);
                    line = Voice.PlayOnly(shot.lineId, VoiceMode.Radio);
                    if (line != null) line.ignoreListenerPause = true;
                }
                float length = Mathf.Max(shot.seconds, line != null && line.clip != null ? line.clip.length + 0.3f : 0f);
                if (shot.video != null)
                {
                    if (videoTexture == null) videoTexture = new RenderTexture(1280, 720, 0) { name = "cutscene_video" };
                    // The video's picture at once (black until the first frame), not the last still.
                    picture.texture = videoTexture;
                    picture.uvRect = new Rect(0f, 0f, 1f, 1f);
                    video.targetTexture = videoTexture;
                    video.clip = shot.video;
                    video.Prepare();
                    while (!video.isPrepared && !skipped) yield return null;
                    video.Play();
                    length = Mathf.Max(length, (float)shot.video.length);
                }
                else
                {
                    picture.texture = shot.still;
                }
                float t = 0f;
                while (t < length && !skipped)
                {
                    t += Time.unscaledDeltaTime;
                    if (shot.video == null) picture.uvRect = Pan(shot, Mathf.Clamp01(t / length));
                    if (Time.unscaledTime - startedAt > SkipGuardSeconds && SkipPressed()) skipped = true;
                    yield return null;
                }
                if (video.isPlaying) video.Stop();
                if (line != null && skipped) line.Stop();
                SubtitleService.Hide(SubtitleKey);
            }

            ShotIndex = -1; // the shots are over (the loop left it past the last one)
            if (skipped) Skipped++;
            RunTelemetry.Event(skipped ? "cutscene_skipped" : "cutscene_done", $"{asset.cutsceneId} at shot {ShotIndex}");
            fadeDone = false;
            ScreenFade.Out(FadeSeconds, () => fadeDone = true);
            while (!fadeDone) yield return null;
            canvas.enabled = false;
            picture.texture = null;
            SubtitleService.Hide(SubtitleKey);
            StoryFlags.Raise(asset.DoneFlag);
            MusicDirector.Hold(null);
            if (GameManager.Instance != null) GameManager.Instance.EndCutscene();
            ShotIndex = -1;
            IsPlaying = false;
            ScreenFade.In(FadeSeconds);
        }

        /// <summary>The still's view at a point of the shot: the corner moves and the view narrows (zoom).</summary>
        public static Rect Pan(CutsceneAsset.Shot shot, float u)
        {
            Vector3 v = Vector3.Lerp(shot.panFrom, shot.panTo, Mathf.SmoothStep(0f, 1f, u));
            float size = 1f / Mathf.Max(1f, v.z);
            float x = Mathf.Clamp(v.x, 0f, 1f - size);
            float y = Mathf.Clamp(v.y, 0f, 1f - size);
            return new Rect(x, y, size, size);
        }

        private static bool SkipPressed()
        {
            return GameInput.GetActionDown(InputManager.GameAction.Interact, KeyCode.E)
                   || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            instance = null;
            IsPlaying = false;
            Current = "";
            ShotIndex = -1;
            Played = 0;
            Skipped = 0;
        }
    }
}
