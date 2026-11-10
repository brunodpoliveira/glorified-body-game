using System;
using System.Collections;
using UnityEngine;
using GlorifiedBody.Audio;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Player;
using GlorifiedBody.Rooms;
using GlorifiedBody.Saving;
using GlorifiedBody.Story;
using GlorifiedBody.Subtitles;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// A radio or a TV that plays a show with no input from the player (the writer's N1 and N2, CANON round 108: "a radio
    /// or a TV always starts mid-show"). It starts the first time she enters its room, and the show goes on if she leaves.
    /// The lines show as subtitles while she is in the room (no voices yet: round 40). A report can end on a line by the
    /// body count of the bulletin (N1b: low, middle, high), then a line that is cut off. At the end it raises its flag and
    /// plays its loop low (the music of the station). Examine: the Interact key on the set shows the picture of the line
    /// that plays (N1b, first pass: the picture as text).
    /// </summary>
    public sealed class Broadcast : MonoBehaviour
    {
        [Serializable]
        public sealed class Line
        {
            public string speaker = "";
            public string id = "";
            [TextArea] public string text = "";
            [Tooltip("What the picture shows (a TV): the Interact key shows it.")]
            public string picture = "";
        }

        public string roomId = "";
        public Line[] lines = new Line[0];
        [Tooltip("Three lines, low, middle and high (Bulletin.WordingFor): one plays after the lines. [N] is the count.")]
        public Line[] byBand = new Line[0];
        [Tooltip("After the band's line (N1b: the line that is cut off).")]
        public Line[] closing = new Line[0];
        public string doneFlag = "";
        public AudioClip loop;
        [Range(0f, 1f)] public float loopVolume = 0.3f;
        [Tooltip("The Interact key on the set: the picture of the line that plays. Null: no pictures.")]
        public Examinable pictures;
        [TextArea] public string afterComment = "";

        public const float Gap = 0.35f; // s between two lines

        public bool Started { get; private set; }
        public bool Done { get; private set; }
        public int Spoken { get; private set; }
        public string LastText { get; private set; } = "";
        public string Band { get; private set; } = "";
        public AudioSource LoopSource { get; private set; }

        private void OnEnable() => RoomTracker.OnChanged += OnRoom;
        private void OnDisable() => RoomTracker.OnChanged -= OnRoom;

        private void Start()
        {
            if (!string.IsNullOrEmpty(doneFlag) && StoryFlags.Has(doneFlag))
            {
                // A load after the show: only the station's music.
                Started = Done = true;
                PlayLoop();
                return;
            }
            if (RoomTracker.CurrentId == roomId) Begin();
        }

        private void OnRoom(Room from, Room to)
        {
            if (to != null && to.id == roomId) Begin();
        }

        /// <summary>Starts the show (her first step into the room).</summary>
        public void Begin()
        {
            if (Started) return;
            Started = true;
            RunTelemetry.Event("broadcast_start", name);
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            foreach (Line line in lines) yield return Speak(line, line.text);
            if (byBand.Length > 0)
            {
                PsychicStats her = GameObject.FindWithTag("Player")?.GetComponentInChildren<PsychicStats>();
                int people = her != null ? her.People : 0;
                Bulletin.WordingFor(people, out string band);
                Band = band;
                int index = band == "low" ? 0 : band == "middle" ? 1 : 2;
                Line chosen = byBand[Mathf.Min(index, byBand.Length - 1)];
                yield return Speak(chosen, chosen.text.Replace("[N]", people.ToString()));
            }
            foreach (Line line in closing) yield return Speak(line, line.text);
            Done = true;
            if (pictures != null && !string.IsNullOrEmpty(afterComment)) pictures.comment = afterComment;
            if (!string.IsNullOrEmpty(doneFlag)) StoryFlags.Raise(doneFlag);
            RunTelemetry.Event("broadcast_done", $"{name}:{Band}");
            PlayLoop();
        }

        private IEnumerator Speak(Line line, string text)
        {
            Spoken++;
            LastText = text;
            if (pictures != null && !string.IsNullOrEmpty(line.picture)) pictures.comment = "[" + line.picture + "]";
            float seconds = SubtitleService.ReadSeconds(text) + 0.6f;
            if (RoomTracker.CurrentId == roomId)
            {
                SubtitleService.Say("broadcast:" + name, line.speaker, text, seconds);
                if (!string.IsNullOrEmpty(line.id)) Voice.PlayOnly(line.id, VoiceMode.World, gameObject);
            }
            yield return new WaitForSeconds(seconds + Gap);
        }

        private void PlayLoop()
        {
            if (loop == null || LoopSource != null) return;
            LoopSource = gameObject.AddComponent<AudioSource>();
            LoopSource.clip = loop;
            LoopSource.loop = true;
            GameAudio.Bind(LoopSource, SoundChannel.Sfx, loopVolume, spatial: true);
            LoopSource.Play();
        }
    }
}
