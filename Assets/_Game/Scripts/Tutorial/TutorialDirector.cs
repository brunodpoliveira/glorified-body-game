using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Destruction;
using GlorifiedBody.Enemy;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;
using GlorifiedBody.UI;

namespace GlorifiedBody.Tutorial
{
    /// <summary>
    /// The tutorial mode (design §4.3, round 9: interactive, a separate mode, part of the story; Bruno,
    /// 2026-10-22: "tutorial mode, since it'll allow us to add the other features and test them"). In the
    /// therapy gym the recorded presenter of the investors' demonstration (CONCEPTS 6.4) asks the subject for
    /// each faculty in turn, on the padded dummies: the walk, the run, the dodge, the target switch, mk_warspite
    /// (the blast), mk_erebus (the fire), mk_orzabal (the telekinesis) with the throw and the slam, a crate to lift
    /// and throw, the bullet catch, a capsule, a log to take and read, the scan of the door (a picture) and of the
    /// monitors (a clip), the key and the door, the inventory, the overflow (the short), a barricade to break.
    /// Each step ends when the
    /// player did the thing. The later features (the enemies) add their steps here.
    /// The lines are placeholders until the writer gives the words. F2 skips a step (debug). A safe room: she takes
    /// no damage and her MK stays full (Bruno, 2026-10-24).
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
        [SerializeField] private Vector3 keyPoint = new Vector3(-2.9f, 0f, 15.4f);
        [SerializeField] private string keyItemId = "test_key";
        [SerializeField] private Vector3 doorPoint = new Vector3(-1.6f, 0f, 10.9f);
        [SerializeField] private string doorScanId = "gym_door";
        [SerializeField] private string monitorsScanId = "gym_monitors";
        [SerializeField, Min(0.3f)] private float reachRadius = 1.3f;
        [SerializeField] private string shooterTargetName = "Target_Gun";
        [SerializeField] private KeyCode skipKey = KeyCode.F2;
        [SerializeField] private string coverName = "Barricade_Gym";
        [SerializeField] private string[] dummyNames = { "Target_1", "Target_2", "Target_3", "Target_Gun" };

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
        public Vector3 KeyPoint => keyPoint;
        public string KeyItemId => keyItemId;
        public Vector3 DoorPoint => doorPoint;
        public LockedDoor Door { get; private set; }
        public string DoorScanId => doorScanId;
        public string MonitorsScanId => monitorsScanId;
        public float ReachRadius => reachRadius;
        public DummyShooter Shooter { get; private set; }
        /// <summary>The cover step's barricade (off until its step).</summary>
        public Breakable Cover { get; private set; }

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
        private bool slammed, propThrown, grenadeLeft, shieldFlanked;
        private EnemyShield shield;
        public EnemyShield Shield => shield;
        private bool ranInStep, dodgedSide, launched, caught, drugUsed, shortBegan, shortEnded, logRead, doorScanned, monitorsScanned, inventoryOpened, inventoryClosed;
        private ItemInventory items;
        private readonly List<(LockOnTargetable target, Vector3 position, Quaternion rotation)> dummies = new List<(LockOnTargetable, Vector3, Quaternion)>();
        // The weapon steps show only their own dummy (Bruno, 2026-10-24: "once the grenade steps start we should have
        // only the one dummy, not all four of them").
        private static readonly Dictionary<string, string> OnlyDummy = new Dictionary<string, string>
        {
            { "grenade", "Target_Gun" }, { "shield", "Target_2" }, { "cover", "Target_3" },
        };
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
            items = player.GetComponent<ItemInventory>();
            // The tutorial is a safe room (Bruno, 2026-10-24: no damage, and the MK never runs out).
            player.GetComponentInChildren<Health>()?.SetInvulnerable(true);
            Door = FindFirstObjectByType<LockedDoor>();
            GameObject shooterObject = GameObject.Find(shooterTargetName);
            Shooter = shooterObject != null ? shooterObject.GetComponent<DummyShooter>() : null;
            shooterTarget = shooterObject != null ? shooterObject.GetComponent<LockOnTargetable>() : null;
            // The gun waits for its step, out of the lock too: the faculty steps must not lift or burn it (the
            // first bot run threw it east of the bars, and its bullets hit the rails).
            SetGunActive(false);
            shield = FindFirstObjectByType<EnemyShield>(FindObjectsInactive.Include);
            if (shield != null) shield.enabled = false;
            foreach (string dummyName in dummyNames)
            {
                GameObject d = GameObject.Find(dummyName);
                LockOnTargetable t = d != null ? d.GetComponent<LockOnTargetable>() : null;
                if (t != null) dummies.Add((t, t.transform.position, t.transform.rotation));
            }
            Cover = FindObjectsByType<Breakable>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(b => b.name == coverName);
            if (Cover != null && !Cover.IsBroken) Cover.gameObject.SetActive(false);
            EnemyGrenade.OnBurst += OnGrenadeBurst;

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
            InventoryUI.OnOpened += OnInventoryOpened;
            InventoryUI.OnClosed += OnInventoryClosed;

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
            InventoryUI.OnOpened -= OnInventoryOpened;
            InventoryUI.OnClosed -= OnInventoryClosed;
            EnemyGrenade.OnBurst -= OnGrenadeBurst;
        }

        /// <summary>A frag grenade of the step burst with her outside its ring.</summary>
        private void OnGrenadeBurst(EnemyGrenade grenade, Vector3 centre)
        {
            if (grenade == null || grenade.IsGas || grenade.Returned || lucy == null) return;
            Vector3 her = lucy.position;
            her.y = centre.y;
            if (Vector3.Distance(her, centre) > grenade.Radius) grenadeLeft = true;
        }

        private void SetGunWeapon(DummyShooter.Weapon weapon)
        {
            if (Shooter != null) Shooter.WeaponKind = weapon;
        }

        private void SetShieldActive(bool active)
        {
            if (shield != null) shield.enabled = active;
        }

        private void OnInventoryOpened() => inventoryOpened = true;

        private void OnInventoryClosed()
        {
            if (inventoryOpened) inventoryClosed = true;
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
                "{PowerBlast} selects mk_warspite, the blast. Hold {Charge}, release when the circle is full.",
                () => firedKind == PowerKind.Blast && firedCharge >= 0.99f));
            steps.Add(new TutorialStep("fire",
                "Faculty two. Only a full circle burns.",
                "{PowerFire} selects mk_erebus, the fire. Hold {Charge} to the full circle, then release.",
                () => firedKind == PowerKind.Fire && firedCharge >= 0.999f));
            steps.Add(new TutorialStep("lift_throw",
                "Faculty three. Lift one, turn, and throw it into another.",
                "{PowerTelekinesis} selects mk_orzabal, the telekinesis. Hold {Charge}, release: it rises. Turn with {TurnLeft} or {TurnRight}, press {Charge} to throw.",
                () => launched));
            steps.Add(new TutorialStep("slam",
                "Lift one again. Keep it up, fill the circle on it: it slams.",
                "With mk_orzabal: hold {Charge} and release to lift. While it hangs, hold {Charge} to a full circle and release: a slam. A short press throws instead.",
                () => slammed));
            steps.Add(new TutorialStep("prop",
                "Faculty three moves objects too. Lift the crate and throw it into a dummy.",
                "With mk_orzabal selected, {SwitchTarget} also picks the crate. Hold {Charge}, release: it rises. Turn, press {Charge}: it flies.",
                () => propThrown));
            steps.Add(new TutorialStep("catch",
                "The dummy by the door has a gun. When the circle closes, catch the bullet and send it back.",
                "With mk_orzabal selected, press {Defend} as the bullet reaches you.",
                () => caught,
                onEnter: () => SetGunActive(true),
                onExit: () => SetGunActive(false)));
            // The attacks (Bruno, 2026-10-24): the icon names the attack; a red ring is a grenade.
            steps.Add(new TutorialStep("grenade",
                "The dummy by the door has a new toy. When the ring is under you, do not stay.",
                "The icon by the circle names the attack. A red ring on the floor: a grenade lands there. Walk, run or {Dodge} out of it before it bursts.",
                () => grenadeLeft,
                onEnter: () => { SetGunWeapon(DummyShooter.Weapon.GrenadeLauncher); SetGunActive(true); },
                onExit: () => { SetGunActive(false); SetGunWeapon(DummyShooter.Weapon.Pistol); }));
            steps.Add(new TutorialStep("shield",
                "That one holds a shield. It stops a faculty from the front. Go around it.",
                "Walk to its side or its back. Then mk_warspite: {PowerBlast}, hold {Charge} to the full circle, release.",
                () => shieldFlanked,
                onEnter: () => SetShieldActive(true),
                onExit: () => SetShieldActive(false)));
            // Breakable cover (Bruno, 2026-10-24: "cover only enemies, on purpose"; all of it collateral damage).
            steps.Add(new TutorialStep("cover",
                "A barricade stands in front of the last dummy. A faculty stops on it. Break it.",
                "Lock the dummy behind it ({SwitchTarget}). mk_warspite, {PowerBlast}: the barricade takes the blast until it breaks. A thrown body breaks it too.",
                () => Cover == null || Cover.IsBroken,
                onEnter: () => { if (Cover != null) Cover.gameObject.SetActive(true); }));
            steps.Add(new TutorialStep("capsule",
                "A capsule lies by the bars. Take it. Use it.",
                "Face the capsule. {Interact} asks, {Interact} again takes it. {UseDrug} opens your drugs: {Interact} asks, {Interact} again uses it, the bars refill.",
                () => drugUsed));
            steps.Add(new TutorialStep("log",
                "A record was left by the wall. Take it. Read it.",
                "Face the record. {Interact} asks to take it, {Interact} again takes it, {Back} leaves it. {Interact} closes the page.",
                () => logRead));
            steps.Add(new TutorialStep("scan_image",
                "Look at the door without your hands. Tell us what opens it.",
                "Face the door. {Scan} scans it: a picture. {Scan} again closes it.",
                () => doorScanned));
            steps.Add(new TutorialStep("key",
                "The scan showed a card. It lies by the wall. Take it.",
                "Face the card. {Interact} asks, {Interact} again takes it. A key takes no slot.",
                () => items != null && items.Has(keyItemId)));
            steps.Add(new TutorialStep("door",
                "Now open the door.",
                "Face the door. {Interact} asks to use the key, {Interact} again opens it.",
                () => Door != null && Door.IsOpen));
            steps.Add(new TutorialStep("inventory",
                "Show us what you carry.",
                "{Inventory} opens the inventory: the room stops. {TurnLeft} {TurnRight} select, {Interact} reads or uses. {Inventory} closes it.",
                () => inventoryClosed));
            steps.Add(new TutorialStep("scan_video",
                "Now the monitors. They remember more.",
                "Face the monitors. {Scan} scans them: a short clip. It closes at its end, or with {Scan}.",
                () => monitorsScanned));
            steps.Add(new TutorialStep("short",
                "The overflow. Your bar is full: the next faculty shorts. A capsule ends it.",
                "{Charge} starts the short. {UseDrug} opens your drugs: {Interact} twice ends it.",
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
            if (stats != null && stats.Nalcon < PsychicStats.MaxNalcon) stats.DebugSet(PsychicStats.MaxNalcon, stats.Short);
            if (Current.Id == "short") RearmShort();
            if (Current.IsDone != null && Current.IsDone()) Advance();
        }

        /// <summary>
        /// The short step keeps what it needs (Bruno, 2026-10-24: a drug before the short soft-locked it: the drug
        /// empties the bar and was the only one). Before the short: a full bar. Until the short ends: a drug.
        /// </summary>
        private void RearmShort()
        {
            if (stats != null && !shortBegan && !stats.IsShortFull && (shortController == null || !shortController.IsActive))
                stats.DebugSet(stats.Nalcon, PsychicStats.MaxShort);
            if (drugs != null && !shortEnded && drugs.Count(DrugType.Nalcon) == 0) drugs.Add(DrugType.Nalcon, 1);
        }

        /// <summary>Every dummy back at its place, lift dropped; only one shown in a weapon step.</summary>
        private void ResetDummies(string only)
        {
            foreach ((LockOnTargetable target, Vector3 position, Quaternion rotation) in dummies)
            {
                if (target == null) continue;
                TelekinesisLift lift = target.GetComponent<TelekinesisLift>();
                if (lift != null) DestroyImmediate(lift);
                target.transform.SetPositionAndRotation(position, rotation);
                target.gameObject.SetActive(only == null || target.name == only);
            }
            Physics.SyncTransforms();
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
            slammed = propThrown = false;
            ranInStep = dodgedSide = launched = caught = drugUsed = shortBegan = shortEnded = logRead = doorScanned = monitorsScanned = inventoryOpened = inventoryClosed = false;
            targetChanges = 0;
            firedKind = null;
            firedCharge = 0f;
            ResetDummies(OnlyDummy.TryGetValue(Current.Id, out string only) ? only : null);
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
            // FireAt raises this only when the shield did not stop the power: a hit from the side or the back.
            if (shield != null && shield.enabled && target != null && target.gameObject == shield.gameObject && power.kind == PowerKind.Blast) shieldFlanked = true;
        }

        private void OnPowersChanged()
        {
            if (powers == null) return;
            string result = powers.LastResult ?? "";
            if (result.StartsWith("launch")) launched = true;
            if (result == "slam") slammed = true;
            if (result.StartsWith("launch_prop")) propThrown = true;
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
