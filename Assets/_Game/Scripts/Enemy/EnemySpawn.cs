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

        [Tooltip("The prefab name in Resources/Enemies: soldier_pistol, soldier_rifle, soldier_gas, guard_pistol, guard_shield, power_armour, labworker, nurse, labworker_pistol, labworker_knife, patient.")]
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
            if (drop != null) figure.AddComponent<GlorifiedBody.Interaction.ItemCarrier>().Bind(drop);
            if (!string.IsNullOrEmpty(line) && figure.TryGetComponent(out PatientBrain patient)) patient.Line = line;
            if (!string.IsNullOrEmpty(fate)) figure.AddComponent<GlorifiedBody.Story.FateFlag>().fate = fate;
            if (scanImage != null || scanClip != null) AddScan(figure);
            Spawned++;
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
