using UnityEngine;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// A place where a room puts an enemy or a civilian (2026-10-26, the room kit). The scene holds only this marker;
    /// the figure is the prefab Resources/Enemies/&lt;kind&gt;, built by InvectorEnemyFactory.BuildEnemyPrefabs from the
    /// vendor packages and git-ignored. So a room with enemies can live in the public repo. At the room's load the
    /// marker becomes the figure: same name, same parent, same place, so the saves find it by the same path. A clone
    /// with no prefabs logs one line and the room has no enemies.
    /// </summary>
    public sealed class EnemySpawn : MonoBehaviour
    {
        public const string Folder = "Enemies/";

        [Tooltip("The prefab name in Resources/Enemies: soldier_pistol, soldier_rifle, soldier_gas, soldier_shield, guard_pistol, guard_shield, power_armour, labworker, nurse, labworker_pistol, labworker_knife, patient.")]
        public string kind = "soldier_pistol";
        [Tooltip("A civilian's pose variant (CivilianBrain.SetVariant), or -1 for the prefab's own.")]
        public int variant = -1;
        [Tooltip("An item of the room the figure carries: hidden until it dies, then on the floor beside the body (ItemCarrier).")]
        public GlorifiedBody.Interaction.ItemPickup drop;
        [Tooltip("A figure that stays put (PatientBrain): its line when she comes near. Empty: the brain's own.")]
        public string line = "";
        [Tooltip("The scan of the person (a picture or a clip), read at the chest. Empty: no scan.")]
        public Texture2D scanImage;
        public UnityEngine.Video.VideoClip scanClip;
        public string scanCaption = "";
        [Tooltip("The person's fate key (\"marta\"): the flags <fate>_killed, <fate>_spared, <fate>_scanned (FateFlag).")]
        public string fate = "";
        [Tooltip("A hider (CivilianBrain): it waits at its place and begs when she finds it. Its whisper and its found line.")]
        public bool hides;
        public string hideLine = "";
        public string foundLine = "";
        [Tooltip("A hider is found this near (metres). 0: never (the nurse who prays on).")]
        public float foundAt = 2f;
        [Tooltip("It waits, unaware, until this story flag goes up, or the script or a hit wakes it (a figure at its work before a talk).")]
        public string heldUntilFlag = "";

        [Tooltip("A test room with its AI off: it stands still whatever she does to it, until this story flag goes up or mk_edgar takes it.")]
        public string passiveUntilFlag = "";
        [Tooltip("A post: he fights inside his room and does not leave it until this flag goes up (the Dining Hall team: the duty key). Empty: none.")]
        public string leashUntilFlag = "";

        [Tooltip("mk_edgar takes it at full HP (a puzzle figure; Bruno, 2026-11-06: \"in puzzles (so it controls enemies at full health)\").")]
        public bool dominableUnhurt;
        [Tooltip("A boss: mk_edgar never takes it, and the shocks of the short pass it.")]
        public bool boss;
        [Header("The Hold (round 118)")]
        [Tooltip("A civilian's first run goes here (H2: the door to H3), then it cowers. Unused when escapeLines is empty and escapeTo is null.")]
        public Transform escapeTo;
        public string[] escapeLines = new string[0];
        [Tooltip("Raised when it cowers at the end of its first run.")]
        public string cowerFlag = "";
        [Tooltip("With a drop: it gives the item only after this flag (the scan C11). Before it, the deny line.")]
        public string giveAfterFlag = "";
        public string denyLine = "";
        public string giveLine = "";
        [Tooltip("No drop: asked for an item, it says this line (AskTalk). Empty: no talk.")]
        public string askLine = "";
        public string askItemId = "";
        public string askItemName = "";

        public static int Spawned { get; private set; }
        public static int Missing { get; private set; }

        private void Awake() => Spawn();

        /// <summary>Puts the figure in place of the marker. Null when the prefab is missing.</summary>
        public GameObject Spawn()
        {
            GameObject prefab = Resources.Load<GameObject>(Folder + kind);
            string figureName = name;
            if (prefab == null)
            {
                Missing++;
                Debug.LogError($"[EnemySpawn] no prefab Resources/{Folder}{kind} for {figureName}: build them with InvectorEnemyFactory.BuildEnemyPrefabs (the Invector packages are needed).");
                RunTelemetry.Event("spawn_missing", $"{figureName}:{kind}");
                return null;
            }
            name = figureName + " (spawn)";
            gameObject.SetActive(false);
            GameObject figure = Instantiate(prefab, transform.position, transform.rotation, transform.parent);
            figure.name = figureName;
            figure.transform.SetSiblingIndex(transform.GetSiblingIndex());
            if (variant >= 0 && figure.TryGetComponent(out CivilianBrain civilian)) civilian.SetVariant(variant);
            BarkVoice.Of(figure); // its personality for the night, from the room it spawns in (rounds 133 and 134)
            if (drop != null) figure.AddComponent<GlorifiedBody.Interaction.ItemCarrier>().Bind(drop);
            // Bruno, 2026-11-02: she can also ask an unarmed civilian for the item.
            if (drop != null && figure.GetComponent<CivilianBrain>() != null)
            {
                var talk = figure.AddComponent<GlorifiedBody.Interaction.CarrierTalk>();
                talk.giveAfterFlag = giveAfterFlag;
                talk.denyLine = denyLine;
                talk.giveLine = giveLine;
            }
            if (drop == null && !string.IsNullOrEmpty(askLine))
            {
                var ask = figure.AddComponent<GlorifiedBody.Interaction.AskTalk>();
                ask.line = askLine;
                ask.itemId = askItemId;
                ask.itemName = askItemName;
            }
            if (escapeTo != null && figure.TryGetComponent(out CivilianBrain runner)) runner.SetEscape(escapeTo.position, escapeLines, cowerFlag);
            if (!string.IsNullOrEmpty(line) && figure.TryGetComponent(out PatientBrain patient)) patient.Line = line;
            if (hides && figure.TryGetComponent(out CivilianBrain hider)) hider.SetHider(hideLine, foundLine, foundAt);
            if (!string.IsNullOrEmpty(heldUntilFlag) && figure.TryGetComponent(out SoldierBrain held)) held.ScriptHoldUntil(heldUntilFlag);
            if (!string.IsNullOrEmpty(leashUntilFlag) && figure.TryGetComponent(out SoldierBrain post))
                post.LeashUntil(GetComponentInParent<GlorifiedBody.Rooms.Room>(), leashUntilFlag);
            if (!string.IsNullOrEmpty(passiveUntilFlag))
            {
                if (figure.TryGetComponent(out SoldierBrain passive)) passive.PassiveUntil(passiveUntilFlag);
                if (figure.TryGetComponent(out CivilianBrain still)) still.PassiveUntil(passiveUntilFlag);
            }
            if (!string.IsNullOrEmpty(fate)) figure.AddComponent<GlorifiedBody.Story.FateFlag>().fate = fate;
            if (scanImage != null || scanClip != null) AddScan(figure);
            if (dominableUnhurt) figure.AddComponent<DominableUnhurt>();
            if (boss && figure.TryGetComponent(out GlorifiedBody.Combat.LockOnTargetable lockable)) lockable.SetShockImmune(true);
            Spawned++;
            GlorifiedBody.Audio.AudioHost.Equip(figure.GetComponent<GlorifiedBody.Core.Health>()); // a figure shown by a script later has steps too
            Destroy(gameObject);
            return figure;
        }

        /// <summary>The scan of a person: at the chest bone, so it follows the body (rank 10, 2026-10-30).</summary>
        private void AddScan(GameObject figure)
        {
            var scan = figure.AddComponent<GlorifiedBody.Interaction.Scannable>();
            scan.scanId = string.IsNullOrEmpty(fate) ? figure.name : fate;
            scan.image = scanImage;
            scan.clip = scanClip;
            scan.caption = scanCaption;
            if (!string.IsNullOrEmpty(fate)) scan.raiseFlag = fate + "_scanned";
            Animator animator = figure.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman) scan.focus = animator.GetBoneTransform(HumanBodyBones.Chest) ?? animator.GetBoneTransform(HumanBodyBones.Spine);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.9f, new Vector3(0.6f, 1.8f, 0.6f));
            Gizmos.DrawRay(transform.position + Vector3.up * 1.5f, transform.forward);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            Spawned = 0;
            Missing = 0;
        }
    }
}
