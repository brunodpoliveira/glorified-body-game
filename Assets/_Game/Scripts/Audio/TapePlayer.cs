using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Interaction;
using GlorifiedBody.Subtitles;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Audio
{
    /// <summary>
    /// The audio logs (Bruno, 2026-10-31: "it can be collected and listened to while walking around; it automatically
    /// pauses when combat begins; music goes down to almost nothing"; "yes once the player un-pauses it"; replay from the
    /// inventory "yes"). A taken audio log plays at once on the Voice channel, in her ear (2D), and the world goes on.
    /// Its lines show as subtitles at their times. While it plays the music drops to MusicLevel (MusicDirector). When the
    /// music goes to Combat the tape pauses, and it stays paused: she plays it again from the inventory (Interact on
    /// it plays or pauses it; at its end it plays from the start). The pause, the inventory and the map stop it with
    /// the world sounds. It lives on the sound system's object, so it plays on through a door. A small label at the
    /// top right shows the tape and its time. Placeholder look.
    /// </summary>
    public sealed class TapePlayer : MonoBehaviour
    {
        public const float MusicLevel = 0.08f; // "music goes down to almost nothing", PROPOSED
        public const string SubtitleOwner = "tape";
        public const string PausedByCombat = "combat";
        public const string PausedByPlayer = "player";
        public const string Ended = "end";

        public static TapePlayer Instance { get; private set; }
        public static event Action<ItemDefinition> OnStarted;

        private AudioSource source;
        private ItemDefinition tape;
        private bool playing;
        private float clock; // the tape's time: game time, so it stops with the world (the sound follows it)
        private float resumeAt;
        private int shownLine = -1;
        private Canvas canvas;
        private Text label;

        public ItemDefinition Tape => tape;
        public bool IsPlaying => tape != null && playing;
        /// <summary>Why the tape stopped: PausedByCombat, PausedByPlayer, Ended, or empty while it plays or with no tape.</summary>
        public string StoppedBy { get; private set; } = "";
        public int CombatPauses { get; private set; }
        public float Length => tape != null && tape.audioClip != null ? tape.audioClip.length : LinesLength;
        /// <summary>The time in the tape, in seconds.</summary>
        public float Position => tape == null ? 0f : playing ? clock : resumeAt;
        public string LabelShown => label != null && label.gameObject.activeSelf ? label.text : "";
        public Canvas Canvas => canvas;

        private float LinesLength
        {
            get
            {
                if (tape == null || tape.tapeLines.Count == 0) return 0f;
                TapeLine last = tape.tapeLines[tape.tapeLines.Count - 1];
                return last.at + SubtitleService.ReadSeconds(last.text);
            }
        }

        private void Awake()
        {
            Instance = this;
            source = gameObject.AddComponent<AudioSource>();
            GameAudio.Bind(source, SoundChannel.Voice, 1f, spatial: false);
            source.loop = false;
            BuildLabel();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            shownLine = -1; // a new scene clears the subtitles: the line shows again
            if (scene.name == MenuScene) Stop(); // a new game, or a load from the menu
        }

        private const string MenuScene = "Main_Menu";

        /// <summary>Plays the tape: a new tape from its start, a paused one from where it stopped, an ended one again.</summary>
        public void Play(ItemDefinition item)
        {
            if (item == null || item.kind != ItemKind.AudioLog) return;
            if (item != tape)
            {
                tape = item;
                resumeAt = 0f;
                source.clip = item.audioClip;
            }
            if (StoppedBy == Ended) resumeAt = 0f;
            playing = true;
            StoppedBy = "";
            shownLine = -1;
            clock = resumeAt;
            if (source.clip != null)
            {
                source.time = Mathf.Clamp(resumeAt, 0f, source.clip.length - 0.01f);
                source.Play();
            }
            UpdateLabel();
            RunTelemetry.Event("tape_play", $"{item.itemId}@{resumeAt:0.0}");
            OnStarted?.Invoke(item);
        }

        /// <summary>The inventory's Interact: plays the tape, or pauses it while it plays.</summary>
        public bool Toggle(ItemDefinition item)
        {
            if (IsPlaying && item == tape)
            {
                Pause(PausedByPlayer);
                return false;
            }
            Play(item);
            return IsPlaying;
        }

        public void Pause(string why)
        {
            if (!IsPlaying) return;
            resumeAt = Position;
            playing = false;
            StoppedBy = why;
            source.Pause();
            SubtitleService.Hide(SubtitleOwner);
            UpdateLabel();
            RunTelemetry.Event("tape_pause", $"{tape.itemId}@{resumeAt:0.0}:{why}");
        }

        /// <summary>No tape any more (a new game).</summary>
        public void Stop()
        {
            if (tape == null) return;
            source.Stop();
            source.clip = null;
            SubtitleService.Hide(SubtitleOwner);
            tape = null;
            playing = false;
            StoppedBy = "";
            resumeAt = 0f;
        }

        /// <summary>Tests: moves the playing tape to the time.</summary>
        public void Seek(float seconds)
        {
            if (tape == null) return;
            seconds = Mathf.Clamp(seconds, 0f, Mathf.Max(0f, Length - 0.01f));
            if (!playing) { resumeAt = seconds; return; }
            clock = seconds;
            if (source.clip != null) source.time = seconds;
            shownLine = -1;
        }

        // The clock on the label, after this frame's time. A play, a pause and the end change the label at once.
        private void LateUpdate() => UpdateLabel();

        private void Update()
        {
            if (!IsPlaying) return;
            GameManager gm = GameManager.Instance;
            if (gm != null && (gm.IsGameOver || gm.IsInMenu))
            {
                Pause(PausedByPlayer);
                return;
            }
            // A fight pauses it, and it stays paused: she plays it again (Bruno: "once the player un-pauses it").
            if (MusicDirector.Instance != null && MusicDirector.Instance.State == MusicState.Combat)
            {
                CombatPauses++;
                Pause(PausedByCombat);
                return;
            }
            clock += Time.deltaTime;
            if (clock >= Length)
            {
                playing = false;
                StoppedBy = Ended;
                resumeAt = 0f;
                source.Stop();
                SubtitleService.Hide(SubtitleOwner);
                UpdateLabel();
                RunTelemetry.Event("tape_end", tape.itemId);
                return;
            }
            ShowLine();
        }

        /// <summary>The line of this time as a subtitle, once per line.</summary>
        private void ShowLine()
        {
            float now = Position;
            int index = -1;
            for (int i = 0; i < tape.tapeLines.Count; i++)
                if (tape.tapeLines[i].at <= now) index = i;
            bool onScreen = false;
            foreach (SubtitleService.Line line in SubtitleService.Lines)
                if (line.Owner == SubtitleOwner) onScreen = true;
            if (index == shownLine && (onScreen || index < 0)) return;
            shownLine = index;
            if (index < 0) { SubtitleService.Hide(SubtitleOwner); return; }
            TapeLine l = tape.tapeLines[index];
            SubtitleService.Show(SubtitleOwner, string.IsNullOrEmpty(l.speaker) ? "TAPE" : l.speaker, l.text);
        }

        private void UpdateLabel()
        {
            GameManager gm = GameManager.Instance;
            bool show = tape != null && StoppedBy != Ended && gm != null && !gm.IsInMenu && !gm.IsGameOver;
            if (label.gameObject.activeSelf != show) label.gameObject.SetActive(show);
            if (!show) return;
            string time = $"{Clock(Position)} / {Clock(Length)}";
            label.text = IsPlaying ? $"TAPE  {tape.displayName}  {time}"
                : StoppedBy == PausedByCombat ? $"TAPE PAUSED: a fight  {time}\nPlay it again from the inventory"
                : $"TAPE PAUSED  {time}";
        }

        private static string Clock(float seconds) => $"{(int)(seconds / 60f)}:{(int)(seconds % 60f):00}";

        private void BuildLabel()
        {
            var obj = new GameObject("TapeUI", typeof(RectTransform));
            obj.transform.SetParent(transform, false);
            canvas = obj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 70;
            var scaler = obj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            var textObj = new GameObject("Label", typeof(RectTransform));
            textObj.transform.SetParent(obj.transform, false);
            RectTransform rect = textObj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-40f, -40f);
            rect.sizeDelta = new Vector2(760f, 80f);
            label = textObj.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 24;
            label.fontStyle = FontStyle.Bold;
            label.color = new Color(0.96f, 0.95f, 0.88f);
            label.alignment = TextAnchor.UpperRight;
            textObj.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            textObj.SetActive(false);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => OnStarted = null;
    }
}
