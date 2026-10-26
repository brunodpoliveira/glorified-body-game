using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.Audio;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Enemy;
using GlorifiedBody.Player;
using GlorifiedBody.Saving;

namespace GlorifiedBody.Story
{
    public enum StepKind
    {
        Wait,        // seconds
        Say,         // target speaks (World), or the speaker name over a radio; id, text; waits for the line
        PA,          // the building's speakers: id, text; waits for the chime and the line
        Raise,       // a story flag (id)
        Drop,        // a story flag (id)
        WaitFlag,    // until the flag (id) is up
        Show,        // target active
        Hide,        // target inactive
        Alert,       // every soldier under target hunts her
        WaitDead,    // until every figure under target is dead
        SetShort,    // her SHORT bar to the value (seconds holds it, 0..100)
        HoldMusic,   // the music holds Tension (id "tension"), Combat or the silence (id "silent")
        FreeMusic,   // the room decides the music again
        Cutscene,    // plays the cutscene, waits for its end
        LockHer,     // her feet stop (a talk)
        FreeHer,     // her feet move again
    }

    /// <summary>One step of a sequence: what it does and its data (the room builder fills them in code).</summary>
    [Serializable]
    public sealed class ScriptStep
    {
        public StepKind kind;
        public float seconds;
        public string id = "";
        public string text = "";
        public string speaker = "";
        public GameObject target;
        public CutsceneAsset cutscene;

        public static ScriptStep Wait(float s) => new ScriptStep { kind = StepKind.Wait, seconds = s };
        public static ScriptStep Say(GameObject who, string id, string text) => new ScriptStep { kind = StepKind.Say, target = who, id = id, text = text };
        public static ScriptStep Radio(string speaker, string id, string text) => new ScriptStep { kind = StepKind.Say, speaker = speaker, id = id, text = text };
        public static ScriptStep PA(string id, string text) => new ScriptStep { kind = StepKind.PA, id = id, text = text };
        public static ScriptStep Raise(string flag) => new ScriptStep { kind = StepKind.Raise, id = flag };
        public static ScriptStep Drop(string flag) => new ScriptStep { kind = StepKind.Drop, id = flag };
        public static ScriptStep WaitFlag(string flag) => new ScriptStep { kind = StepKind.WaitFlag, id = flag };
        public static ScriptStep Show(GameObject what) => new ScriptStep { kind = StepKind.Show, target = what };
        public static ScriptStep Hide(GameObject what) => new ScriptStep { kind = StepKind.Hide, target = what };
        public static ScriptStep Alert(GameObject group) => new ScriptStep { kind = StepKind.Alert, target = group };
        public static ScriptStep WaitDead(GameObject group) => new ScriptStep { kind = StepKind.WaitDead, target = group };
        public static ScriptStep SetShort(float value) => new ScriptStep { kind = StepKind.SetShort, seconds = value };
        public static ScriptStep HoldMusic(string state) => new ScriptStep { kind = StepKind.HoldMusic, id = state };
        public static ScriptStep FreeMusic() => new ScriptStep { kind = StepKind.FreeMusic };
        public static ScriptStep Play(CutsceneAsset asset) => new ScriptStep { kind = StepKind.Cutscene, cutscene = asset };
        public static ScriptStep LockHer() => new ScriptStep { kind = StepKind.LockHer };
        public static ScriptStep FreeHer() => new ScriptStep { kind = StepKind.FreeHer };
    }

    /// <summary>
    /// A scripted beat of the story (the design sweep's rank 2: "the scientist's ultimatum, code silver, the QRF
    /// wave, the sealed gate, Marta's choice"). A list of steps run in order: lines, the PA, story flags, objects
    /// shown or hidden, a group alerted, a wait for a group's death, her SHORT bar, the music, a cutscene. It runs
    /// once: its done flag goes up at the end and the save keeps it, so a load or a return does not run it again.
    /// A ScriptTrigger starts it when she walks in, or another step or system calls Run.
    /// </summary>
    public sealed class ScriptedSequence : MonoBehaviour
    {
        public const float LineGap = 0.25f;

        public List<ScriptStep> steps = new List<ScriptStep>();
        [Tooltip("The flag raised at the end (empty: \"seq_\" + the object's name). A raised flag means it ran.")]
        public string doneFlag = "";

        public string DoneFlag => string.IsNullOrEmpty(doneFlag) ? "seq_" + name : doneFlag;
        public bool Running { get; private set; }
        public bool Done => StoryFlags.Has(DoneFlag);
        public int StepIndex { get; private set; } = -1;
        public event Action OnFinished;

        /// <summary>Starts it, unless it runs or ran. True when it starts now.</summary>
        public bool Run()
        {
            if (Running || Done || !isActiveAndEnabled) return false;
            StartCoroutine(Steps());
            return true;
        }

        private IEnumerator Steps()
        {
            Running = true;
            RunTelemetry.Event("sequence_start", name);
            for (StepIndex = 0; StepIndex < steps.Count; StepIndex++)
            {
                ScriptStep step = steps[StepIndex];
                if (step == null) continue;
                yield return Do(step);
            }
            StoryFlags.Raise(DoneFlag);
            Running = false;
            RunTelemetry.Event("sequence_done", name);
            OnFinished?.Invoke();
        }

        private IEnumerator Do(ScriptStep step)
        {
            switch (step.kind)
            {
                case StepKind.Wait:
                    yield return new WaitForSeconds(step.seconds);
                    break;
                case StepKind.Say:
                {
                    float seconds = LineSeconds(step.id, step.text);
                    if (step.target != null) Voice.Say(step.target, step.id, step.text, VoiceMode.World, seconds);
                    else Voice.SayAs(step.speaker, step.id, step.text, VoiceMode.Radio, seconds);
                    yield return new WaitForSeconds(seconds + LineGap);
                    break;
                }
                case StepKind.PA:
                    Voice.PA(step.id, step.text);
                    yield return new WaitForSeconds(Voice.PaDelay + LineSeconds(step.id, step.text) + LineGap);
                    break;
                case StepKind.Raise: StoryFlags.Raise(step.id); break;
                case StepKind.Drop: StoryFlags.Drop(step.id); break;
                case StepKind.WaitFlag:
                    while (!StoryFlags.Has(step.id)) yield return null;
                    break;
                case StepKind.Show: if (step.target != null) step.target.SetActive(true); break;
                case StepKind.Hide: if (step.target != null) step.target.SetActive(false); break;
                case StepKind.Alert:
                    if (step.target != null)
                        foreach (SoldierBrain brain in step.target.GetComponentsInChildren<SoldierBrain>()) brain.Alert("script", callOthers: true);
                    break;
                case StepKind.WaitDead:
                    while (step.target != null && AnyAlive(step.target)) yield return null;
                    break;
                case StepKind.SetShort:
                {
                    PsychicStats stats = Lucy() != null ? Lucy().GetComponent<PsychicStats>() : null;
                    if (stats != null) stats.AddShort(Mathf.Clamp(step.seconds, 0f, PsychicStats.MaxShort) - stats.Short);
                    break;
                }
                case StepKind.HoldMusic:
                    MusicDirector.Hold(step.id == "silent" ? MusicState.Silent : step.id == "combat" ? MusicState.Combat : step.id == "ambient" ? MusicState.Ambient : MusicState.Tension);
                    break;
                case StepKind.FreeMusic: MusicDirector.Hold(null); break;
                case StepKind.Cutscene:
                    if (step.cutscene != null && CutscenePlayer.Play(step.cutscene))
                        while (CutscenePlayer.IsPlaying) yield return null;
                    break;
                case StepKind.LockHer: SetLock(true); break;
                case StepKind.FreeHer: SetLock(false); break;
            }
        }

        /// <summary>The time of a line: its clip, else the time to read it.</summary>
        public static float LineSeconds(string id, string text)
        {
            AudioClip clip = Voice.Clip(id);
            if (clip != null) return clip.length + 0.25f;
            return Mathf.Max(1.6f, Subtitles.SubtitleService.ReadSeconds(text ?? ""));
        }

        private static bool AnyAlive(GameObject group)
        {
            foreach (Health h in group.GetComponentsInChildren<Health>())
                if (h.isActiveAndEnabled && !h.IsDead) return true;
            return false;
        }

        private static GameObject Lucy() => GameObject.FindWithTag("Player");

        private static void SetLock(bool locked)
        {
            GameObject lucy = Lucy();
            TankController tank = lucy != null ? lucy.GetComponent<TankController>() : null;
            if (tank != null) tank.LockMovement(locked);
        }
    }
}
