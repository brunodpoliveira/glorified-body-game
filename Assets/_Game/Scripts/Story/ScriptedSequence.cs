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
        Screen,      // the screen under target plays (InWorldScreen); id "wait": until it ends
        Outfit,      // her outfit (id "nude", "guard"); the grabbables over the new limit fall at her feet
        Bulletin,    // the police bulletin card by her body count (T11); waits until she closes it
        EndDemo,     // the demo ends: back to the main menu (the title)
        Approach,    // every soldier under target talks first: no attack on sight, it walks to the point and turns to her (Alert ends it)
        Give,        // an item (id) into her inventory (a key after a cutscene); no slot free: it lies at her feet
        DoorOpen,    // the fire door at target opens
        DoorJam,     // the fire door at target shuts and jams, when the doorway is free (3 s at most)
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
        public Vector3 point;

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
        /// <summary>Alert, but the figures call nobody (the three behind the shut door of W1 stay unaware).</summary>
        public static ScriptStep AlertAlone(GameObject group) => new ScriptStep { kind = StepKind.Alert, target = group, id = "alone" };
        public static ScriptStep DoorOpen(GameObject door) => new ScriptStep { kind = StepKind.DoorOpen, target = door };
        public static ScriptStep DoorJam(GameObject door) => new ScriptStep { kind = StepKind.DoorJam, target = door };
        public static ScriptStep WaitDead(GameObject group) => new ScriptStep { kind = StepKind.WaitDead, target = group };
        public static ScriptStep SetShort(float value) => new ScriptStep { kind = StepKind.SetShort, seconds = value };
        public static ScriptStep HoldMusic(string state) => new ScriptStep { kind = StepKind.HoldMusic, id = state };
        public static ScriptStep FreeMusic() => new ScriptStep { kind = StepKind.FreeMusic };
        public static ScriptStep Play(CutsceneAsset asset) => new ScriptStep { kind = StepKind.Cutscene, cutscene = asset };
        public static ScriptStep LockHer() => new ScriptStep { kind = StepKind.LockHer };
        public static ScriptStep FreeHer() => new ScriptStep { kind = StepKind.FreeHer };
        public static ScriptStep EndDemo() => new ScriptStep { kind = StepKind.EndDemo };
        public static ScriptStep ShowBulletin() => new ScriptStep { kind = StepKind.Bulletin };
        public static ScriptStep Outfit(string outfitId) => new ScriptStep { kind = StepKind.Outfit, id = outfitId };
        public static ScriptStep Screen(GameObject screen, bool wait = false) => new ScriptStep { kind = StepKind.Screen, target = screen, id = wait ? "wait" : "" };
        public static ScriptStep Approach(GameObject group, Vector3 point) => new ScriptStep { kind = StepKind.Approach, target = group, point = point };
        public static ScriptStep Give(string itemId) => new ScriptStep { kind = StepKind.Give, id = itemId };
    }

    /// <summary>
    /// A scripted beat of the story (the design sweep's rank 2: "the scientist's ultimatum, code silver, the QRF
    /// wave, the sealed gate, Marta's choice"). A list of steps run in order: lines, the PA, story flags, objects
    /// shown or hidden, a group alerted, a wait for a group's death, her SHORT bar, the music, a cutscene, a screen, her outfit. It runs
    /// once: its done flag goes up at the end and the save keeps it, so a load or a return does not run it again.
    /// A ScriptTrigger starts it when she walks in, or another step or system calls Run.
    /// </summary>
    public sealed class ScriptedSequence : MonoBehaviour
    {
        public const float LineGap = 0.25f;

        public List<ScriptStep> steps = new List<ScriptStep>();
        [Tooltip("The flag raised at the end (empty: \"seq_\" + the object's name). A raised flag means it ran.")]
        public string doneFlag = "";

        [Tooltip("It runs by itself when the room starts (the first beat of a room). Its done flag still stops a second run.")]
        public bool runOnStart;
        [Tooltip("With runOnStart: the screen is black from the first frame, because the beat opens on a cutscene (Bruno, 2026-11-04: \"it needs to start on the curscene right away\").")]
        public bool startsBlack;

        /// <summary>
        /// A beat that runs "when the room starts": in a room of the world scene (2026-11-02) that is when she first
        /// enters its room, not when the world loads.
        /// </summary>
        private IEnumerator Start()
        {
            if (!runOnStart) yield break;
            GlorifiedBody.Rooms.Room room = GetComponentInParent<GlorifiedBody.Rooms.Room>();
            // Start, not Awake: the flags of a load stand now. A load past this beat, or a start in another room, stays clear.
            GameObject her = Lucy();
            if (startsBlack && !Done && (room == null || (her != null && room.Contains(her.transform.position + Vector3.up * 0.1f))))
            {
                covered = true;
                GlorifiedBody.UI.ScreenFade.Cover();
            }
            if (room != null && GlorifiedBody.Rooms.RoomTracker.Instance != null)
                while (GlorifiedBody.Rooms.RoomTracker.Current != room) yield return null;
            if (!Run()) ClearCover();
        }

        private bool covered;

        /// <summary>A black start with no cutscene to take it over clears here (the cutscene clears it itself).</summary>
        private void ClearCover()
        {
            if (!covered) return;
            covered = false;
            if (!CutscenePlayer.IsPlaying && GlorifiedBody.UI.ScreenFade.Alpha > 0.5f) GlorifiedBody.UI.ScreenFade.In(0.5f);
        }

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
            ClearCover();
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
                        foreach (SoldierBrain brain in step.target.GetComponentsInChildren<SoldierBrain>()) brain.Alert("script", callOthers: step.id != "alone");
                    break;
                case StepKind.Approach:
                    if (step.target != null)
                        foreach (SoldierBrain brain in step.target.GetComponentsInChildren<SoldierBrain>()) brain.ScriptHold(step.point);
                    break;
                case StepKind.DoorOpen:
                    if (step.target != null && step.target.TryGetComponent(out GlorifiedBody.Interaction.FireDoor opens)) opens.Open("script");
                    break;
                case StepKind.DoorJam:
                    if (step.target != null && step.target.TryGetComponent(out GlorifiedBody.Interaction.FireDoor shuts))
                    {
                        float waited = 0f;
                        while (shuts.Blocked() && waited < 3f)
                        {
                            waited += Time.deltaTime;
                            yield return null;
                        }
                        shuts.Jam("script");
                    }
                    break;
                case StepKind.Give:
                {
                    // Bruno, 2026-11-02: a cutscene "will likely change status of the story (i.e spawn enemies, give the
                    // player a key item etc)".
                    GlorifiedBody.Interaction.ItemDefinition item = GlorifiedBody.Interaction.ItemDefinition.Find(step.id);
                    GameObject her = Lucy();
                    if (item == null || her == null) { Debug.LogWarning($"[ScriptedSequence] {name}: no item '{step.id}' or no Lucy"); break; }
                    var items = her.GetComponent<GlorifiedBody.Interaction.ItemInventory>();
                    if (items != null && items.TryAdd(item)) Sfx.Ui(Sfx.Pickup, 0.8f);
                    else GlorifiedBody.Interaction.FloorItem.Place(item, her.transform.position + her.transform.forward * 0.6f, her.transform.eulerAngles.y);
                    RunTelemetry.Event("script_give", step.id);
                    break;
                }
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
                case StepKind.EndDemo:
                    RunTelemetry.Event("demo_end", name);
                    GlorifiedBody.Systems.GameManager.Instance?.LoadMainMenu();
                    break;
                case StepKind.Bulletin:
                {
                    PsychicStats her = Lucy() != null ? Lucy().GetComponent<PsychicStats>() : null;
                    bool closed = false;
                    if (Bulletin.Show(her != null ? her.People : 0, () => closed = true))
                        while (!closed) yield return null;
                    break;
                }
                case StepKind.Outfit:
                {
                    // The Crown grab of U1 tears the uniform off (Bruno, round 20): every player enters the brain fight bare.
                    OutfitProfile to = Interaction.OutfitChange.Load(step.id);
                    if (Lucy() != null && to != null) Interaction.OutfitChange.Change(Lucy(), to);
                    break;
                }
                case StepKind.Screen:
                {
                    var screen = step.target != null ? step.target.GetComponentInChildren<Interaction.InWorldScreen>() : null;
                    if (screen != null && screen.Play() && step.id == "wait")
                        while (screen != null && screen.IsPlaying) yield return null;
                    break;
                }
            }
        }

        /// <summary>The time of a line: its clip, else the time to read it.</summary>
        public static float LineSeconds(string id, string text)
        {
            AudioClip clip = Voice.Clip(id);
            float read = Mathf.Max(1.6f, Subtitles.SubtitleService.ReadSeconds(text ?? ""));
            return clip != null ? GameAudio.Length(clip, read) + 0.25f : read;
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
