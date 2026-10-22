using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.Combat;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Enemy;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tutorial
{
    /// <summary>
    /// The tutorial mode (design §4.3, round 9: interactive, a separate mode, part of the story; Bruno,
    /// 2026-10-22: "tutorial mode, since it'll allow us to add the other features and test them"). In the
    /// therapy gym the recorded presenter of the investors' demonstration (CONCEPTS 6.4) asks the subject for
    /// each faculty in turn, on the padded dummies: the walk, the run, the dodge, the target switch, BLAST,
    /// FIRE, TELEKINESIS with the throw, the bullet catch, a capsule, a log to take and read, the scan of the door
    /// (a picture) and of the monitors (a clip), the overflow (the short). Each step ends
    /// when the player did the thing. The later features (the props, the enemies) add their steps here.
    /// The lines are placeholders until the writer gives the words. F2 skips a step (debug).
    /// </summary>
    public sealed class TutorialDirector : MonoBehaviour
    {
        public const string SceneName = "Tutorial";
        public const float CompleteDelay = 2.5f;

        [SerializeField] private Vector3 barsPoint = new Vector3(-1.8f, 0f, 11.2f);
        [SerializeField] private Vector3 monitorsPoint = new Vector3(-2.4f, 0f, 16f);
        [SerializeField] private Vector3 capsulePoint = new Vector3(-2.6f, 0f, 13.5f);
        [SerializeField] private Vector3 logPoint = new Vector3(-2.9f, 0f, 14.6f);
        [SerializeField] private string logItemId = "test_log";
        [SerializeField] private string doorScanId = "gym_door";
        [SerializeField] private string monitorsScanId = "gym_monitors";
        [SerializeField, Min(0.3f)] private float reachRadius = 1.3f;
        [SerializeField] private string shooterTargetName = "Target_Gun";
        [SerializeField] private KeyCode skipKey = KeyCode.F2;

        public static TutorialDirector Instance { get; private set; }

        public IReadOnlyList<TutorialStep> Steps => steps;
        public int StepIndex { get; private set; } = -1;
        public TutorialStep Current => StepIndex >= 0 && StepIndex < steps.Count ? steps[StepIndex] : null;
        public bool IsComplete { get; private set; }
        public float StepStartTime { get; private set; }
        public Vector3 BarsPoint => barsPoint;
        public Vector3 MonitorsPoint => monitorsPoint;
        public Vector3 CapsulePoint => capsulePoint;
        public Vector3 LogPoint => logPoint;
        public string LogItemId => logItemId;
        public string DoorScanId => doorScanId;
        public string MonitorsScanId => monitorsScanId;
        public float ReachRadius => reachRadius;
        public DummyShooter Shooter { get; private set; }

        public event Action<TutorialStep> OnStepChanged;
        public event Action OnCompleted;

        private readonly List<TutorialStep> steps = new List<TutorialStep>();
        private Transform lucy;
        private TankController tank;
        private LockOnController lockOn;
        private PsychicPowers powers;
        private PsychicStats stats;
        private ShortController shortController;
        private DrugInventory drugs;
        private LockOnTargetable shooterTarget;
        private Interactor interactor;
        private PsychicScanner scanner;

        // The flags of the current step, reset on enter.
        private bool ranInStep, dodgedSide, launched, caught, drugUsed, shortBegan, shortEnded, logRead, doorScanned, monitorsScanned;
        private int targetChanges;
        private PowerKind? firedKind;
        private float firedCharge;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Debug.LogError("[TutorialDirector] no player in the room.");
                enabled = false;
                return;
            }
            lucy = player.transform;
            tank = player.GetComponent<TankController>();
            lockOn = player.GetComponent<LockOnController>();
            powers = player.GetComponent<PsychicPowers>();
            stats = player.GetComponent<PsychicStats>();
            shortController = player.GetComponent<ShortController>();
            drugs = player.GetComponent<DrugInventory>();
            interactor = player.GetComponent<Interactor>();
            scanner = player.GetComponent<PsychicScanner>();
            GameObject shooterObject = GameObject.Find(shooterTargetName);
            Shooter = shooterObject != null ? shooterObject.GetComponent<DummyShooter>() : null;
            shooterTarget = shooterObject != null ? shooterObject.GetComponent<LockOnTargetable>() : null;
            // The gun waits for its step, out of the lock too: the faculty steps must not lift or burn it (the
            // first bot run threw it east of the bars, and its bullets hit the rails).
            SetGunActive(false);

            if (tank != null) tank.OnDodge += OnDodge;
            if (lockOn != null) lockOn.OnTargetChanged += OnTargetChanged;
            if (powers != null)
            {
                powers.OnPowerFired += OnPowerFired;
                powers.OnChanged += OnPowersChanged;
            }
            if (shortController != null)
            {
                shortController.OnShortBegan += OnShortBegan;
                shortController.OnShortEnded += OnShortEnded;
            }
            if (interactor != null) interactor.OnChanged += OnInteractorChanged;
            if (scanner != null) scanner.OnScanEnded += OnScanEnded;

            BuildSteps();
            Enter(0);
        }

        private void Unsubscribe()
        {
            if (tank != null) tank.OnDodge -= OnDodge;
            if (lockOn != null) lockOn.OnTargetChanged -= OnTargetChanged;
            if (powers != null)
            {
                powers.OnPowerFired -= OnPowerFired;
                powers.OnChanged -= OnPowersChanged;
            }
            if (shortController != null)
            {
                shortController.OnShortBegan -= OnShortBegan;
                shortController.OnShortEnded -= OnShortEnded;
            }
            if (interactor != null) interactor.OnChanged -= OnInteractorChanged;
            if (scanner != null) scanner.OnScanEnded -= OnScanEnded;
        }

        // ── The steps ─────────────────────────────────────────────────

        private void BuildSteps()
        {
            steps.Clear();
            steps.Add(new TutorialStep("walk",
                "Ladies and gentlemen, the subject. Subject, to the bars, please.",
                "{Forward} walks. {TurnLeft} and {TurnRight} turn. {Back} steps back.",
                () => Near(barsPoint)));
            steps.Add(new TutorialStep("run",
                "Now to the monitors. At a run.",
                "Hold {Run} with {Forward} to run.",
                () => ranInStep && Near(monitorsPoint)));
            steps.Add(new TutorialStep("dodge",
                "Something comes at you. Step aside.",
                "{Dodge} with {TurnLeft} or {TurnRight}: a dodge to that side. Or tap the turn key twice.",
                () => dodgedSide));
            steps.Add(new TutorialStep("switch",
                "The lock finds the nearest dummy by itself. Choose another one.",
                "{SwitchTarget} picks the next target.",
                () => targetChanges >= 1 && lockOn != null && lockOn.CurrentTarget != null));
            steps.Add(new TutorialStep("blast",
                "Faculty one. Fill the circle on a dummy and release.",
                "{PowerBlast} selects BLAST. Hold {Charge}, release when the circle is full.",
                () => firedKind == PowerKind.Blast && firedCharge >= 0.99f));
            steps.Add(new TutorialStep("fire",
                "Faculty two. Only a full circle burns.",
                "{PowerFire} selects FIRE. Hold {Charge} to the full circle, then release.",
                () => firedKind == PowerKind.Fire && firedCharge >= 0.999f));
            steps.Add(new TutorialStep("lift_throw",
                "Faculty three. Lift one, turn, and throw it into another.",
                "{PowerTelekinesis} selects TELEKINESIS. Hold {Charge}, release: it rises. Turn with {TurnLeft} or {TurnRight}, press {Charge} to throw.",
                () => launched));
            steps.Add(new TutorialStep("catch",
                "The dummy by the door has a gun. When the circle closes, catch the bullet and send it back.",
                "With TELEKINESIS selected, press {Defend} as the bullet reaches you.",
                () => caught,
                onEnter: () => SetGunActive(true),
                onExit: () => SetGunActive(false)));
            steps.Add(new TutorialStep("capsule",
                "A capsule lies by the bars. Take it. Use it.",
                "Walk over the capsule. {UseDrug} uses it: the bars refill.",
                () => drugUsed));
            steps.Add(new TutorialStep("log",
                "A record was left by the wall. Take it. Read it.",
                "Face the record. {Interact} asks to take it, {Interact} again takes it, {Back} leaves it. {Interact} closes the page.",
                () => logRead));
            steps.Add(new TutorialStep("scan_image",
                "Look at the door without your hands. Tell us what opens it.",
                "Face the door. {Scan} scans it: a picture. {Scan} again closes it.",
                () => doorScanned));
            steps.Add(new TutorialStep("scan_video",
                "Now the monitors. They remember more.",
                "Face the monitors. {Scan} scans them: a short clip. It closes at its end, or with {Scan}.",
                () => monitorsScanned));
            steps.Add(new TutorialStep("short",
                "The overflow. Your bar is full: the next faculty shorts. A capsule ends it.",
                "{Charge} starts the short. {UseDrug} ends it.",
                () => shortBegan && shortEnded,
                onEnter: () =>
                {
                    if (stats != null) stats.DebugSet(stats.Nalcon, PsychicStats.MaxShort);
                    if (drugs != null) drugs.Add(DrugType.Nalcon, 1);
                }));
            steps.Add(new TutorialStep("complete",
                "Demonstration complete. Thank you, ladies and gentlemen.",
                "",
                () => Time.time - StepStartTime >= CompleteDelay));
        }

        private void SetGunActive(bool active)
        {
            if (Shooter != null) Shooter.enabled = active;
            if (shooterTarget != null) shooterTarget.SetSelectable(active);
        }

        private bool Near(Vector3 point)
        {
            if (lucy == null) return false;
            Vector3 a = lucy.position, b = point;
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b) <= reachRadius;
        }

        // ── The run ───────────────────────────────────────────────────

        private void Update()
        {
            if (IsComplete || Current == null) return;
            if (tank != null && tank.IsRunning) ranInStep = true;
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;
            if (Input.GetKeyDown(skipKey))
            {
                RunTelemetry.Event("tutorial_skip", Current.Id);
                Advance();
                return;
            }
            if (Current.IsDone != null && Current.IsDone()) Advance();
        }

        /// <summary>The current step ends now (done, or skipped by the debug key). Tests and the bot do not call it: they do the thing.</summary>
        public void Advance()
        {
            if (IsComplete || Current == null) return;
            Current.OnExit?.Invoke();
            RunTelemetry.Event("tutorial_done", $"{Current.Id} {Time.time - StepStartTime:0.0}s");
            if (StepIndex + 1 >= steps.Count)
            {
                Complete();
                return;
            }
            Enter(StepIndex + 1);
        }

        private void Enter(int index)
        {
            StepIndex = index;
            StepStartTime = Time.time;
            ranInStep = dodgedSide = launched = caught = drugUsed = shortBegan = shortEnded = logRead = doorScanned = monitorsScanned = false;
            targetChanges = 0;
            firedKind = null;
            firedCharge = 0f;
            Current.OnEnter?.Invoke();
            RunTelemetry.Event("tutorial_step", Current.Id);
            OnStepChanged?.Invoke(Current);
        }

        private void Complete()
        {
            IsComplete = true;
            StepIndex = steps.Count;
            RunTelemetry.Event("tutorial_complete");
            OnCompleted?.Invoke();
            GameManager.Instance?.CompleteLevel();
        }

        // ── The hooks ─────────────────────────────────────────────────

        private void OnDodge()
        {
            if (tank != null && (tank.LastDodge == TankController.DodgeDirection.Left || tank.LastDodge == TankController.DodgeDirection.Right)) dodgedSide = true;
        }

        private void OnTargetChanged(LockOnTargetable target)
        {
            if (target != null) targetChanges++;
        }

        private void OnPowerFired(PowerDefinition power, float charge, LockOnTargetable target)
        {
            firedKind = power.kind;
            firedCharge = charge;
        }

        private void OnPowersChanged()
        {
            if (powers == null) return;
            string result = powers.LastResult ?? "";
            if (result.StartsWith("launch")) launched = true;
            if (result == "catch") caught = true;
            if (result.StartsWith("drug:")) drugUsed = true;
        }

        private void OnShortBegan() => shortBegan = true;

        private void OnInteractorChanged()
        {
            if (interactor != null && interactor.LastResult == "closed:" + logItemId) logRead = true;
        }

        private void OnScanEnded(Scannable target)
        {
            if (target == null) return;
            if (target.scanId == doorScanId) doorScanned = true;
            if (target.scanId == monitorsScanId) monitorsScanned = true;
        }

        private void OnShortEnded()
        {
            if (shortBegan) shortEnded = true;
        }
    }
}
