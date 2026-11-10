using System.Collections;
using UnityEngine;
using GlorifiedBody.Audio;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Rooms;
using GlorifiedBody.Saving;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// The halon room G17 (DEMO_PLAN §10.4, CANON round 39: the fire, the gas, the pass-out; the rest PROPOSED). A strapped
    /// wall of archive boxes hides the reserve cage. mk_erebus at a full circle lights it:
    ///   - LOCKOUT turned first (the cabinet in the room beside): the boxes burn, no horn, the cage shows.
    ///   - Else the horn sounds and the door seals. 30 s, then the halon. ABORT on the panel inside stops it: the door
    ///     opens and the boxes burn. With no ABORT the halon comes: the fire goes out, and she passes out in it (game over,
    ///     killer "halon", an exception to rule 2 by Bruno). The bottles are empty after it: a later fire burns with no horn.
    /// A part circle only flashes on the boxes (the rule of mk_erebus). The boxes are heavy: no lift.
    /// </summary>
    public sealed class HalonSystem : MonoBehaviour
    {
        public enum State { Idle, Horn, Burning, Discharged, Burned }

        public string roomId = "G17";
        [Tooltip("The strapped wall of boxes: a heavy lock target (no Health).")]
        public LockOnTargetable wall;
        [Tooltip("What the fire takes away: the boxes and their straps.")]
        public GameObject boxes;
        public GameObject flames;
        public GameObject ash;
        [Tooltip("The cage's contents: they show when the boxes are gone.")]
        public GameObject cage;
        public FireDoor door;
        public UnityEngine.UI.Text panel;
        public string lockoutFlag = "halon_lockout";
        public string burnedFlag = "demo_g17_burned";
        public const string KillerId = "halon";

        public const float DischargeSeconds = 30f; // CANON (round 39): "30 s later the halon comes"
        public const float BurnSeconds = 8f;       // PROPOSED
        public const float PassOutSeconds = 3f;    // PROPOSED: "You will not know you are going under" (L17)
        public const float ClearSeconds = 6f;      // after a discharge that did not take her (a test room's god mode)

        public State Current { get; private set; } = State.Idle;
        public bool Spent { get; private set; }
        public float SecondsLeft => Current == State.Horn ? Mathf.Max(0f, dischargeAt - Time.time) : 0f;
        public bool HornOn => horn != null && horn.isPlaying;

        private float dischargeAt;
        private AudioSource horn;
        private Coroutine run;

        private void Awake()
        {
            horn = gameObject.AddComponent<AudioSource>();
            GameAudio.Bind(horn, SoundChannel.Sfx, 0.5f, spatial: true);
            horn.clip = UpsBank.Alarm;
            horn.loop = true;
            horn.playOnAwake = false;
        }

        private void OnEnable() => HitEventBus.OnHit += OnHit;
        private void OnDisable() => HitEventBus.OnHit -= OnHit;

        private void Start()
        {
            if (flames != null) flames.SetActive(false);
            if (ash != null) ash.SetActive(false);
            if (cage != null) cage.SetActive(false);
            if (StoryFlags.Has(burnedFlag)) Finish(quiet: true);
            Show();
        }

        private void OnHit(HitInfo hit)
        {
            if (hit == null || wall == null || hit.Victim != wall.gameObject) return;
            if (hit.DamageType != DamageType.PsychicFire || hit.Charge < 0.999f) return;
            Ignite();
        }

        /// <summary>A full circle of mk_erebus on the boxes.</summary>
        public void Ignite()
        {
            if (Current != State.Idle && Current != State.Discharged) return;
            if (flames != null) flames.SetActive(true);
            Sfx.Play(Sfx.Fire, wall != null ? wall.transform.position : transform.position, 0.9f);
            bool locked = StoryFlags.Has(lockoutFlag) || Spent;
            RunTelemetry.Event("halon_ignite", locked ? "no horn" : "horn");
            if (locked)
            {
                Current = State.Burning;
                run = StartCoroutine(BurnDown());
            }
            else
            {
                Current = State.Horn;
                dischargeAt = Time.time + DischargeSeconds;
                horn.Play();
                if (door != null && door.Current == FireDoor.State.Open && !door.Jam("halon") && isActiveAndEnabled) StartCoroutine(SealWhenClear());
                run = StartCoroutine(Countdown());
            }
            Show();
        }

        /// <summary>ABORT on the panel: the horn stops, the door opens, the boxes burn.</summary>
        public bool Abort()
        {
            if (Current != State.Horn) return false;
            if (run != null) StopCoroutine(run);
            horn.Stop();
            if (door != null) door.Open("halon_abort");
            Current = State.Burning;
            RunTelemetry.Event("halon_abort", $"{SecondsLeft:0.0}s left");
            run = StartCoroutine(BurnDown());
            Show();
            return true;
        }

        private IEnumerator SealWhenClear()
        {
            while (Current == State.Horn && door != null && door.Current == FireDoor.State.Open && !door.Jam("halon")) yield return new WaitForSeconds(0.25f);
        }

        private IEnumerator Countdown()
        {
            while (Time.time < dischargeAt)
            {
                Show();
                yield return new WaitForSeconds(0.2f);
            }
            Discharge();
        }

        private void Discharge()
        {
            horn.Stop();
            Spent = true;
            Current = State.Discharged;
            if (flames != null) flames.SetActive(false); // the fire is out
            Sfx.Play(Sfx.GasHiss, transform.position, 1f);
            RunTelemetry.Event("halon_discharge", roomId);
            Show();
            run = StartCoroutine(AfterDischarge());
        }

        private IEnumerator AfterDischarge()
        {
            yield return new WaitForSeconds(PassOutSeconds);
            GameObject her = GameObject.FindWithTag("Player");
            Health health = her != null ? her.GetComponentInChildren<Health>() : null;
            if (RoomTracker.CurrentId == roomId && health != null && !health.IsDead && !health.IsInvulnerable)
            {
                GlorifiedBody.UI.DeathMenuUI.LastKillerId = KillerId;
                RunTelemetry.Event("halon_pass_out", roomId);
                health.Kill();
                yield break;
            }
            yield return new WaitForSeconds(ClearSeconds - PassOutSeconds);
            if (door != null) door.Open("halon_clear");
            Show();
        }

        private IEnumerator BurnDown()
        {
            yield return new WaitForSeconds(BurnSeconds);
            Finish(quiet: false);
        }

        private void Finish(bool quiet)
        {
            Current = State.Burned;
            if (boxes != null) boxes.SetActive(false);
            if (flames != null) flames.SetActive(false);
            if (ash != null) ash.SetActive(true);
            if (cage != null) cage.SetActive(true);
            if (wall != null) wall.SetSelectable(false);
            if (door != null && door.Current == FireDoor.State.Jammed) door.Open("halon_done", instant: quiet);
            StoryFlags.Raise(burnedFlag);
            if (!quiet) RunTelemetry.Event("halon_burned", roomId);
            Show();
        }

        private void Show()
        {
            if (panel == null) return;
            panel.text = Current switch
            {
                State.Horn => $"DISCHARGE IN {Mathf.CeilToInt(SecondsLeft)}",
                State.Discharged => "DISCHARGED",
                _ => Spent ? "EMPTY" : StoryFlags.Has(lockoutFlag) ? "LOCKED OUT" : "ARMED",
            };
        }
    }
}
