using UnityEngine;
using GlorifiedBody.Characters;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// A figure that stays where the room puts it (the design sweep's rank 10, 2026-10-30: the patient in her bed). It
    /// sits (the RFab sit of PoseOverride), never walks, runs, begs or tends a body, and says its line once when she
    /// comes within lineRange (the subtitle names the speaker). It knows fear but cannot run: a death it sees makes it
    /// tremble hard (an unarmed civilian's fear limit is 0). The body is suspended (no NavMesh, kinematic), so a bed
    /// holds it. A kill drops it as a ragdoll like any figure. The line is a PLACEHOLDER for the writer. Numbers
    /// PROPOSED.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class PatientBrain : MonoBehaviour, IFearful, IStaggerable
    {
        public const string DefaultLine = "Is mine done? Are they done with mine yet?";

        [SerializeField] private string line = DefaultLine;
        [SerializeField, Min(0.5f)] private float lineRange = 2.5f;
        [SerializeField, Range(0f, 1f)] private float alertTremble = 0.3f;
        [SerializeField, Range(0f, 1f)] private float fearTremble = 0.9f;

        private IEnemyBody body;
        private Health health;
        private Tremble tremble;
        private PoseOverride pose;
        private Transform player;
        private bool posed;

        public string Line { get => line; set => line = value; }
        public bool Spoke { get; private set; }
        public bool Alerted { get; private set; }
        public bool Frightened { get; private set; }
        /// <summary>It never runs: there is nowhere to go from the bed.</summary>
        public bool Retreating => false;
        /// <summary>Where the room put it.</summary>
        public Vector3 Place { get; private set; }

        private void Awake()
        {
            body = GetComponent<IEnemyBody>();
            health = GetComponent<Health>();
            tremble = GetComponent<Tremble>() ?? gameObject.AddComponent<Tremble>();
            pose = GetComponent<PoseOverride>() ?? gameObject.AddComponent<PoseOverride>();
        }

        private void OnEnable()
        {
            health.OnDeath ??= new UnityEngine.Events.UnityEvent();
            health.OnDeath.AddListener(OnDeath);
        }

        private void OnDisable() => health.OnDeath?.RemoveListener(OnDeath);

        private void Start()
        {
            Place = transform.position;
            body?.Suspend(true);
        }

        private void Update()
        {
            if (health.IsDead || Time.deltaTime <= 0f) return;
            TelekinesisLift lift = GetComponent<TelekinesisLift>();
            bool held = (lift != null && lift.IsLifted) || RagdollBodies.IsDown(this);
            if (held)
            {
                if (posed) { pose.Halt(); posed = false; }
                return;
            }
            if (!posed)
            {
                PoseClips clips = PoseClips.Load();
                if (clips != null && clips.sit != null) pose.Play(clips.sit, loop: true, fade: 0.3f);
                posed = true;
            }
            tremble.Amount = Frightened ? fearTremble : Alerted ? alertTremble : 0f;

            if (player == null)
            {
                GameObject p = GameObject.FindWithTag("Player");
                player = p != null ? p.transform : null;
                if (player == null) return;
            }
            if (!Spoke && !string.IsNullOrEmpty(line) && EnemyNav.Flat(transform.position, player.position) <= lineRange)
            {
                Spoke = true;
                Bark.Say(gameObject, line, 3f);
                RunTelemetry.Event("patient_line", name);
            }
        }

        public void Alert(string reason, bool callOthers)
        {
            if (health.IsDead || Alerted) return;
            Alerted = true;
            RunTelemetry.Event("patient_alerted", $"{name}:{reason}");
        }

        public void Shock(float seconds, bool gruesome) => Retreat(gruesome ? "gruesome death" : "death");

        /// <summary>The fear limit of a civilian is 0: it would run. It cannot, so it shakes in its bed.</summary>
        public void Retreat(string reason)
        {
            if (health.IsDead) return;
            Alerted = true;
            if (!Frightened) RunTelemetry.Event("patient_frightened", $"{name}:{reason}");
            Frightened = true;
        }

        /// <summary>A shock of the short: it jolts where it lies.</summary>
        public void Stagger(float seconds)
        {
            if (!health.IsDead) tremble.Jolt(Mathf.Min(seconds, 0.6f));
        }

        private void OnDeath()
        {
            tremble.Amount = 0f;
            if (posed) pose.Halt();
            posed = false;
        }
    }
}
