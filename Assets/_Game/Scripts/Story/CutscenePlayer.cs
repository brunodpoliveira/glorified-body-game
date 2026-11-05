using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using GlorifiedBody.Audio;
using GlorifiedBody.Core;
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
    /// subtitle shows "SPEAKER: words". The skip key (Interact, Escape or Enter) opens a menu with the movie paused
    /// (Bruno, 2026-11-02: "it opens a menu where the player keeps the movie paused; they can resume or skip"): RESUME
    /// plays on, SKIP ends it. At the end the screen fades through black back to the room, the done flag goes up, and the
    /// game plays again; a sequence goes on with its next steps (a skip too: the story still changes).
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
        private GameObject menu;
        private Text resumeLabel, skipLabel;
        private int choice; // 0 resume, 1 skip

        public static bool IsPlaying { get; private set; }
        public static string Current { get; private set; } = "";
        public static int ShotIndex { get; private set; } = -1;
        public static int Played { get; private set; }
        public static int Skipped { get; private set; }
        /// <summary>The skip menu is open: the movie holds.</summary>
        public static bool MenuOpen { get; private set; }
        public static Texture Showing => instance != null && instance.picture != null && instance.canvas.enabled ? instance.picture.texture : null;
        public static Rect ShowingRect => instance != null && instance.picture != null ? instance.picture.uvRect : default;

        /// <summary>Plays the cutscene. False when the game cannot stop for it (not playing) or one plays.</summary>
        public static bool Play(CutsceneAsset asset)
        {
            if (asset == null || IsPlaying) return false;
            if (Saving.WorldState.Travelling) return false; // she is in a doorway: the trip ends first (seam sweep, 2026-10-31)
            if (GameManager.Instance != null && !GameManager.Instance.BeginCutscene()) return false;
            Ensure().StartCoroutine(instance.Run(asset));
            return true;
        }

        /// <summary>The cutscene's picture is on the screen (the fade may still clear over it).</summary>
        public static bool ShowsPicture => instance != null && instance.canvas != null && instance.canvas.enabled;

        /// <summary>Ends the playing cutscene now (SKIP in the menu, and tests).</summary>
        public static void Skip()
        {
            if (instance != null && IsPlaying) instance.skipped = true;
            MenuOpen = false;
        }

        /// <summary>Opens the skip menu: the movie, the line and the pan hold (the skip key, and tests).</summary>
        public static void OpenMenu()
        {
            if (instance == null || !IsPlaying || MenuOpen) return;
            MenuOpen = true;
            instance.choice = 0;
            instance.ShowMenu();
            RunTelemetry.Event("cutscene_menu", Current);
        }

        /// <summary>RESUME: the movie plays on from where it held.</summary>
        public static void Resume()
        {
            if (!MenuOpen) return;
            MenuOpen = false;
            if (instance != null) instance.ShowMenu();
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
            BuildMenu();
        }

        private void BuildMenu()
        {
            // The held frame dims behind a solid panel: the words of a still do not show through the menu.
            menu = new GameObject("SkipMenu", typeof(RectTransform));
            menu.transform.SetParent(transform, false);
            Stretch(menu.GetComponent<RectTransform>());
            menu.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(menu.transform, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(420f, 230f);
            panel.AddComponent<Image>().color = new Color(0.03f, 0.03f, 0.04f, 1f);
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Label(panel.transform, font, "PAUSED", 30, new Vector2(0f, 70f), new Color(0.7f, 0.7f, 0.75f));
            resumeLabel = Label(panel.transform, font, "RESUME", 38, new Vector2(0f, 5f), Color.white);
            skipLabel = Label(panel.transform, font, "SKIP", 38, new Vector2(0f, -55f), Color.white);
            foreach ((Text label, int index) in new[] { (resumeLabel, 0), (skipLabel, 1) })
            {
                Button button = label.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                int which = index;
                button.onClick.AddListener(() => { choice = which; Confirm(); });
            }
            menu.SetActive(false);
        }

        private static Text Label(Transform parent, Font font, string words, int size, Vector2 at, Color colour)
        {
            var obj = new GameObject(words, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchoredPosition = at;
            rect.sizeDelta = new Vector2(380f, 50f);
            Text text = obj.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = colour;
            text.text = words;
            return text;
        }

        private void ShowMenu()
        {
            menu.SetActive(MenuOpen);
            resumeLabel.text = choice == 0 ? "> RESUME <" : "RESUME";
            skipLabel.text = choice == 1 ? "> SKIP <" : "SKIP";
            resumeLabel.color = choice == 0 ? new Color(1f, 0.85f, 0.3f) : Color.white;
            skipLabel.color = choice == 1 ? new Color(1f, 0.85f, 0.3f) : Color.white;
        }

        private void Confirm()
        {
            if (!MenuOpen) return;
            if (choice == 1) Skip();
            else Resume();
            ShowMenu();
        }

        /// <summary>The menu's keys: up and down (or forward and back) choose, Interact or Enter confirms, Escape resumes.</summary>
        private void MenuKeys()
        {
            if (GameInput.GetActionDown(InputManager.GameAction.Forward, KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) { choice = 0; ShowMenu(); }
            else if (GameInput.GetActionDown(InputManager.GameAction.Back, KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) { choice = 1; ShowMenu(); }
            else if (Input.GetKeyDown(KeyCode.Escape)) Resume();
            else if (GameInput.GetActionDown(InputManager.GameAction.Interact, KeyCode.E) || Input.GetKeyDown(KeyCode.Return)) Confirm();
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
            // Already black (a world that opens on this cutscene, ScriptedSequence.startsBlack): no fade first.
            if (ScreenFade.Alpha < 0.99f)
            {
                ScreenFade.Out(FadeSeconds, () => fadeDone = true);
                while (!fadeDone) yield return null;
            }
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
                float length = Mathf.Max(shot.seconds, line != null && line.clip != null ? GameAudio.Length(line.clip, 0f) + 0.3f : 0f);
                if (shot.video != null)
                {
                    if (videoTexture == null) videoTexture = new RenderTexture(1280, 720, 0) { name = "cutscene_video" };
                    // The video's picture at once (black until the first frame), not the last still.
                    picture.texture = videoTexture;
                    picture.uvRect = new Rect(0f, 0f, 1f, 1f);
                    video.targetTexture = videoTexture;
                    VideoClips.Assign(video, shot.video);
                    video.Prepare();
                    while (!video.isPrepared && !skipped) yield return null;
                    video.Play();
                    length = Mathf.Max(length, (float)video.length); // the player's length: on the web the clip plays by URL
                }
                else
                {
                    picture.texture = shot.still;
                }
                float t = 0f;
                while (t < length && !skipped)
                {
                    if (MenuOpen)
                    {
                        // The movie holds: the clip, the line, the pan. The menu decides.
                        if (video.isPlaying) video.Pause();
                        if (line != null && line.isPlaying) line.Pause();
                        int openedAt = Time.frameCount;
                        while (MenuOpen && !skipped)
                        {
                            if (Time.frameCount > openedAt) MenuKeys();
                            yield return null;
                        }
                        if (skipped) break;
                        if (shot.video != null) video.Play();
                        if (line != null) line.UnPause();
                        yield return null; // the confirm key does not open the menu again
                        continue;
                    }
                    t += Time.unscaledDeltaTime;
                    if (shot.video == null) picture.uvRect = Pan(shot, Mathf.Clamp01(t / length));
                    if (Time.unscaledTime - startedAt > SkipGuardSeconds && SkipPressed()) OpenMenu();
                    yield return null;
                }
                if (video.isPlaying) video.Stop();
                if (line != null && skipped) line.Stop();
                SubtitleService.Hide(SubtitleKey);
            }

            ShotIndex = -1; // the shots are over (the loop left it past the last one)
            MenuOpen = false;
            ShowMenu();
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
            MenuOpen = false;
        }
    }
}
