using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using GlorifiedBody.Audio;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Subtitles;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// A screen in the room that plays a clip with its sound (2026-10-31, Bruno: "in-world screens (a CRT clip with
    /// sound)"; the demo plan: the rehearsal tape on the CRT wall of W7, the orientation program in G7, the monitors of
    /// G9). One video player draws into one texture, and every face of the set shows it (a wall of CRTs is one screen
    /// with many faces). The sound comes from the screen, in 3D, on the Voice channel, so a pause stops it with the world
    /// sounds. The clip stops with the world too (the pause, the inventory, the map, a log, a cutscene). Its lines show
    /// as subtitles while she is near. While it plays the music drops as for a tape, and a playing tape pauses (she plays
    /// it again with the tape key). With no clip it runs its lines alone on its own clock (the greybox card).
    /// Interact turns it on and off when it is interactive; a ScriptedSequence step (StepKind.Screen) starts it when
    /// she walks in.
    /// </summary>
    public sealed class InWorldScreen : Interactable
    {
        public const string PausedTape = "screen";

        public VideoClip clip;
        [Tooltip("The faces that show the clip (a wall of CRTs: one face a monitor). Empty: the renderers of the children named \"...Face...\".")]
        public Renderer[] faces = new Renderer[0];
        [Tooltip("The lines of the clip, as subtitles at their times.")]
        public List<TapeLine> lines = new List<TapeLine>();
        [Tooltip("Interact turns it on and off. Off: only a script starts it.")]
        public bool interactive = true;
        public bool loop;
        [Tooltip("The subtitles show while she is this near (metres).")]
        public float hearing = 12f;
        [Range(0f, 1f)] public float volume = 1f;
        public bool ducksMusic = true;
        public bool pausesTape = true;
        [Tooltip("The words of the hint: \"turn on the screen\".")]
        public string thing = "the screen";

        private static readonly List<InWorldScreen> playing = new List<InWorldScreen>();
        /// <summary>A screen that lowers the music plays now (MusicDirector).</summary>
        public static bool AnyDucking
        {
            get
            {
                foreach (InWorldScreen s in playing) if (s != null && s.ducksMusic && s.Heard) return true;
                return false;
            }
        }

        public static event Action<InWorldScreen> OnStarted;
        public event Action OnEnded;

        private VideoPlayer video;
        private AudioSource sound;
        private RenderTexture texture;
        private Material[] idle;
        private Material shown;
        private bool on;
        private bool held; // the world stopped while it played
        private float clock; // the time of a clip with no video, or the video's last time
        private int shownLine = -1;
        private Transform lucy;

        public bool IsPlaying => on;
        public int Plays { get; private set; }
        public int Ends { get; private set; }
        /// <summary>The time in the clip, in seconds.</summary>
        public float Time => on && video != null && clip != null ? (float)video.time : clock;
        public float Length => clip != null ? (float)clip.length : LinesLength;
        /// <summary>The frame the video shows (tests: it moves while it plays, it holds while the world stops).</summary>
        public long Frame => video != null ? video.frame : -1;
        public bool ShowsClip => faces.Length > 0 && faces[0] != null && faces[0].sharedMaterial == shown && shown != null;
        public AudioSource Sound => sound;
        public string SubtitleOwner => "screen:" + name;
        /// <summary>She is near enough to hear its words.</summary>
        public bool Heard => on && lucy != null && Vector3.Distance(lucy.position, SoundPoint) <= hearing && GlorifiedBody.Rooms.Room.Same(lucy.position, SoundPoint); // not through a wall (2026-11-02)

        private Vector3 SoundPoint
        {
            get
            {
                if (faces.Length == 0 || faces[0] == null) return transform.position;
                Bounds b = faces[0].bounds;
                foreach (Renderer r in faces) if (r != null) b.Encapsulate(r.bounds);
                return b.center;
            }
        }

        private float LinesLength
        {
            get
            {
                if (lines.Count == 0) return 0f;
                TapeLine last = lines[lines.Count - 1];
                return last.at + SubtitleService.ReadSeconds(last.text);
            }
        }

        private void Awake()
        {
            if (faces == null || faces.Length == 0)
            {
                var found = new List<Renderer>();
                foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                    if (r.name.Contains("Face")) found.Add(r);
                faces = found.ToArray();
            }
            idle = new Material[faces.Length];
            for (int i = 0; i < faces.Length; i++) if (faces[i] != null) idle[i] = faces[i].sharedMaterial;

            var soundObj = new GameObject("ScreenSound");
            soundObj.transform.SetParent(transform, false);
            soundObj.transform.position = SoundPoint;
            sound = soundObj.AddComponent<AudioSource>();
            GameAudio.Bind(sound, SoundChannel.Voice, volume, spatial: true);

            if (clip != null)
            {
                video = gameObject.AddComponent<VideoPlayer>();
                video.playOnAwake = false;
                GlorifiedBody.Core.VideoClips.Assign(video, clip);
                video.isLooping = loop;
                video.renderMode = VideoRenderMode.RenderTexture;
                video.timeUpdateMode = VideoTimeUpdateMode.GameTime;
                video.audioOutputMode = VideoAudioOutputMode.AudioSource;
                video.SetTargetAudioSource(0, sound);
                video.waitForFirstFrame = true;
                video.skipOnDrop = true;
                video.loopPointReached += _ => { if (!loop) Finish(); };
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (on) Stop();
        }

        private void OnDestroy()
        {
            playing.Remove(this);
            if (texture != null) { texture.Release(); Destroy(texture); }
            if (shown != null) Destroy(shown);
        }

        /// <summary>Starts the clip from its start. False when it plays already.</summary>
        public bool Play()
        {
            if (on || !isActiveAndEnabled) return false;
            on = true;
            held = false;
            clock = 0f;
            shownLine = -1;
            Plays++;
            if (!playing.Contains(this)) playing.Add(this);
            FindLucy();
            ShowClip(true);
            if (video != null)
            {
                video.time = 0;
                video.Play();
            }
            if (pausesTape && TapePlayer.Instance != null && TapePlayer.Instance.IsPlaying) TapePlayer.Instance.Pause(PausedTape);
            RunTelemetry.Event("screen_play", name);
            OnStarted?.Invoke(this);
            return true;
        }

        /// <summary>Turns it off: the faces go back to their idle look.</summary>
        public void Stop()
        {
            if (!on) return;
            clock = Time;
            on = false;
            held = false;
            playing.Remove(this);
            if (video != null) video.Stop();
            SubtitleService.Hide(SubtitleOwner);
            ShowClip(false);
            RunTelemetry.Event("screen_stop", name);
        }

        /// <summary>Tests: moves the playing clip to the time.</summary>
        public void Seek(float seconds)
        {
            if (!on) return;
            if (video != null) video.time = seconds;
            else clock = seconds;
            shownLine = -1;
        }

        private void Finish()
        {
            if (!on) return;
            Ends++;
            Stop();
            RunTelemetry.Event("screen_end", name);
            OnEnded?.Invoke();
        }

        private void Update()
        {
            if (!on) return;
            GameManager gm = GameManager.Instance;
            bool stopped = gm != null && !gm.IsPlaying;
            // The clip stops with the world (a pause, the inventory, the map, a log, a cutscene) and goes on after it.
            if (stopped != held)
            {
                held = stopped;
                if (video != null) { if (held) video.Pause(); else video.Play(); }
            }
            if (held) return;
            if (video == null)
            {
                clock += UnityEngine.Time.deltaTime;
                if (clock >= Length)
                {
                    if (loop) { clock = 0f; shownLine = -1; }
                    else { Finish(); return; }
                }
            }
            if (lucy == null) FindLucy();
            ShowLine();
        }

        private void ShowLine()
        {
            if (!Heard)
            {
                if (shownLine >= 0) SubtitleService.Hide(SubtitleOwner);
                shownLine = -1;
                return;
            }
            float now = Time;
            int index = -1;
            for (int i = 0; i < lines.Count; i++) if (lines[i].at <= now) index = i;
            if (index == shownLine) return;
            shownLine = index;
            if (index < 0) { SubtitleService.Hide(SubtitleOwner); return; }
            TapeLine l = lines[index];
            SubtitleService.Show(SubtitleOwner, string.IsNullOrEmpty(l.speaker) ? "SCREEN" : l.speaker, l.text);
        }

        private void ShowClip(bool show)
        {
            if (show && shown == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                shown = new Material(shader) { name = name + "_Clip" };
                if (video != null)
                {
                    texture = new RenderTexture(640, 480, 0, RenderTextureFormat.ARGB32) { name = name + "_Video" };
                    texture.Create();
                    video.targetTexture = texture;
                    shown.SetTexture("_BaseMap", texture);
                    shown.SetColor("_BaseColor", new Color(1.3f, 1.3f, 1.3f)); // a screen glows over the room's light
                }
                else shown.SetColor("_BaseColor", new Color(0.32f, 0.62f, 0.4f)); // the greybox card: a lit green face
            }
            for (int i = 0; i < faces.Length; i++)
                if (faces[i] != null) faces[i].sharedMaterial = show ? shown : idle[i];
        }

        private void FindLucy()
        {
            GameObject obj = GameObject.FindGameObjectWithTag("Player");
            lucy = obj != null ? obj.transform : null;
        }

        // ── Interact ──────────────────────────────────────────────────

        public override bool Available => interactive && base.Available;
        public override string Hint => (on ? "turn off " : "turn on ") + thing;
        public override InteractKind KindFor(Interactor who) => InteractKind.Act;
        public override string TextFor(Interactor who) => "";

        public override string Confirm(Interactor who)
        {
            if (on) { Stop(); return "screen_off:" + name; }
            Play();
            return "screen_on:" + name;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            playing.Clear();
            OnStarted = null;
        }
    }
}
