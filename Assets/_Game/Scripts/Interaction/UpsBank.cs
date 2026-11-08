using System.Collections;
using UnityEngine;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Rooms;
using GlorifiedBody.Saving;
using GlorifiedBody.Subtitles;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// The dark room puzzle (DEMO_PLAN §10.1, Bruno's puzzle; CANON round 52: "the details stand as written"). When the room
    /// goes dark (DarkRoom) the monitors go black and the three units beep. The order is NET, REC, ACCESS. A unit out of
    /// order trips the alarm: the three PCs drop, a continuous tone, and the beeps start again after resetSeconds (CANON
    /// round 40: no enemy comes; the alarm and the 20 s are PROPOSED). When REC comes up the recorder plays; on the last
    /// unit the monitors come back one by one and their lights are the only light (DarkLayer). The room lights never come
    /// back. doneFlag opens the release (FlagSwitch.requiredFlag). The lines are placeholders for the writer's words.
    /// </summary>
    public sealed class UpsBank : MonoBehaviour
    {
        public enum State { Lit, Beeping, Alarm, Done }

        public UpsUnit[] units = new UpsUnit[0];
        public string[] order = { "NET", "REC", "ACCESS" };
        public DarkRoom darkRoom;
        public string doneFlag = "";
        [Min(0f)] public float resetSeconds = 20f;
        public Renderer[] monitors = new Renderer[0];
        public Light[] monitorLights = new Light[0];
        [Min(0f)] public float monitorGap = 0.5f;
        public Color screenColour = new Color(0.35f, 0.75f, 0.55f);
        public string recLine = "PLACEHOLDER: the recorder plays the intake tape of cutscene 08.";
        public string accessLine = "POWER LOST. BREAKER G9. OPENED BY HAND.";

        public State Current { get; private set; } = State.Lit;
        public int Next { get; private set; }
        public int Alarms { get; private set; }
        public int MonitorsOn { get; private set; }
        public bool Beeping => Current == State.Beeping;
        public bool Active => Current == State.Beeping || Current == State.Alarm;
        public bool Done => Current == State.Done;

        private float alarmUntil;
        private AudioSource alarm;
        private MaterialPropertyBlock block;

        private void Awake()
        {
            alarm = gameObject.AddComponent<AudioSource>();
            GlorifiedBody.Audio.GameAudio.Bind(alarm, GlorifiedBody.Audio.SoundChannel.Sfx, 0.45f, spatial: true);
            alarm.clip = AlarmClip();
            alarm.loop = true;
            alarm.playOnAwake = false;
            foreach (UpsUnit u in units) if (u != null) u.bank = this;
        }

        private void OnEnable()
        {
            if (darkRoom != null) darkRoom.OnDark += OnDark;
        }

        private void OnDisable()
        {
            if (darkRoom != null) darkRoom.OnDark -= OnDark;
        }

        private void Start()
        {
            if (!string.IsNullOrEmpty(doneFlag) && StoryFlags.Has(doneFlag))
            {
                // A load after the puzzle: the units run, the monitors are on, the room stays dark.
                Current = State.Done;
                foreach (UpsUnit u in units) u.SetUp(true);
                for (int i = 0; i < monitors.Length; i++) SetMonitor(i, true);
                return;
            }
            for (int i = 0; i < monitors.Length; i++) SetMonitor(i, true); // before the dark: the monitor wall runs
            if (darkRoom != null && darkRoom.IsDark) OnDark();
            else RefreshUnits();
        }

        private void OnDark()
        {
            if (Current != State.Lit) return;
            for (int i = 0; i < monitors.Length; i++) SetMonitor(i, false);
            Current = State.Beeping;
            RunTelemetry.Event("ups_beeping", name);
            RefreshUnits();
        }

        /// <summary>The Interact key on a unit. The result word for the telemetry and the tests.</summary>
        public string Press(UpsUnit unit)
        {
            if (Current != State.Beeping) return "busy:" + unit.label;
            if (Next < order.Length && unit.label == order[Next])
            {
                unit.SetUp(true);
                Next++;
                RunTelemetry.Event("ups_up", unit.label);
                if (unit.label == "REC" && !string.IsNullOrEmpty(recLine)) SubtitleService.Say("ups", "RECORDER", recLine, 5f);
                if (Next >= order.Length) Finish();
                RefreshUnits();
                return "up:" + unit.label;
            }
            // Out of order: the alarm trips, the three PCs drop.
            Current = State.Alarm;
            Alarms++;
            Next = 0;
            alarmUntil = Time.time + resetSeconds;
            foreach (UpsUnit u in units) u.SetUp(false);
            alarm.Play();
            RunTelemetry.Event("ups_alarm", unit.label);
            RefreshUnits();
            return "alarm:" + unit.label;
        }

        private void Finish()
        {
            Current = State.Done;
            if (!string.IsNullOrEmpty(doneFlag)) StoryFlags.Raise(doneFlag);
            if (!string.IsNullOrEmpty(accessLine)) SubtitleService.Say("ups", "ACCESS", accessLine, 5f);
            RunTelemetry.Event("ups_done", name);
            StartCoroutine(MonitorsBack());
        }

        private IEnumerator MonitorsBack()
        {
            for (int i = 0; i < monitors.Length; i++)
            {
                yield return new WaitForSeconds(monitorGap);
                SetMonitor(i, true);
            }
        }

        private void Update()
        {
            if (Current != State.Alarm || Time.time < alarmUntil) return;
            alarm.Stop();
            Current = State.Beeping;
            RunTelemetry.Event("ups_beeping", "after the alarm");
            RefreshUnits();
        }

        private void RefreshUnits()
        {
            foreach (UpsUnit u in units) if (u != null) u.Refresh();
        }

        public bool AlarmOn => alarm != null && alarm.isPlaying;

        private void SetMonitor(int i, bool on)
        {
            Renderer screen = monitors[i];
            if (screen != null)
            {
                block ??= new MaterialPropertyBlock();
                screen.GetPropertyBlock(block);
                block.SetColor("_BaseColor", on ? screenColour : Color.black);
                screen.SetPropertyBlock(block);
            }
            if (i < monitorLights.Length && monitorLights[i] != null) monitorLights[i].enabled = on;
            GlorifiedBody.Rendering.Darkness.RefreshGlow(); // the greybox is unlit: the monitors' glow is a tint
            int count = 0;
            for (int k = 0; k < monitorLights.Length; k++) if (monitorLights[k] != null && monitorLights[k].enabled) count++;
            MonitorsOn = count;
        }

        // A continuous two-tone alarm: a placeholder sound (no file).
        private static AudioClip alarmClip;
        private static AudioClip AlarmClip()
        {
            if (alarmClip != null) return alarmClip;
            const int rate = 22050;
            var data = new float[rate];
            for (int i = 0; i < data.Length; i++)
            {
                float f = i < rate / 2 ? 950f : 760f;
                data[i] = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * i / rate)) * 0.25f;
            }
            alarmClip = AudioClip.Create("ups_alarm", data.Length, 1, rate, false);
            alarmClip.SetData(data, 0);
            return alarmClip;
        }
    }
}
