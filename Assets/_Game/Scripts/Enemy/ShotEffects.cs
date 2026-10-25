using UnityEngine;

namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// The sounds and the impact of an enemy shot (Bruno, 2026-10-23: "add the guns, bullets, effects (muzzle,
    /// smoke, impact), sounds of it"). An asset in Resources/Effects, made by the editor from the vendor's shooter
    /// sounds and impact prefabs. The muzzle flash and the smoke are the gun's own particles (EnemyGun).
    /// </summary>
    [CreateAssetMenu(menuName = "GlorifiedBody/Shot Effects")]
    public sealed class ShotEffects : ScriptableObject
    {
        public const string ResourcePath = "Effects/shot_effects";

        public AudioClip[] fireClips;
        public AudioClip aimClip;
        [Tooltip("Spawned where a bullet hits the room: the particles. Its own sources are removed (a gunshot).")]
        public GameObject impactPrefab;
        [Tooltip("The hit on the wall, one per impact.")]
        public AudioClip[] impactClips;
        [Min(0f)] public float impactScale = 1f;
        [Min(0.1f)] public float impactLifetime = 3f;
        public Color tracerColour = new Color(1f, 0.85f, 0.45f);

        [Header("The other weapons (Bruno, 2026-10-24)")]
        public AudioClip[] rifleFireClips;
        public AudioClip rifleAimClip;
        public AudioClip[] launcherFireClips;
        public AudioClip launcherAimClip;
        public AudioClip[] meleeSwingClips;
        public AudioClip[] meleeHitClips;
        [Tooltip("A power or a returned bullet on the front of a shield.")]
        public AudioClip[] blockClips;
        [Tooltip("The grenade burst: particles and its own sound.")]
        public GameObject explosionPrefab;
        public AudioClip explosionClip;
        [Min(0.1f)] public float explosionLifetime = 4f;

        [Header("Gas and rockets (Bruno, 2026-10-24)")]
        [Tooltip("The poison cloud: the vendor smoke particles, scripts removed. GasCloud tints it green.")]
        public GameObject gasPrefab;
        public AudioClip gasClip;
        public AudioClip[] rocketFireClips;
        [Tooltip("The vendor RPG's missile mesh, pointing along +z after rocketMeshRotation.")]
        public Mesh rocketMesh;
        public Material rocketMaterial;
        public Quaternion rocketMeshRotation = Quaternion.identity;
        [Min(0.01f)] public float rocketMeshScale = 1f;

        private static ShotEffects cached;
        private static bool loaded;

        public static ShotEffects Load()
        {
            if (!loaded)
            {
                cached = Resources.Load<ShotEffects>(ResourcePath);
                loaded = true;
            }
            return cached;
        }

        /// <summary>The impact on a wall or the floor, facing out of the surface. A shield block plays its own sound.</summary>
        public static void Impact(Vector3 point, Vector3 normal, bool withSound = true)
        {
            ShotEffects fx = Load();
            if (fx == null || fx.impactPrefab == null) return;
            Quaternion facing = Quaternion.LookRotation(normal.sqrMagnitude > 0.001f ? normal : Vector3.up);
            GameObject impact = SpawnSilent(fx.impactPrefab, point + normal * 0.02f, facing);
            impact.transform.localScale *= fx.impactScale;
            Destroy(impact, fx.impactLifetime);
            if (withSound) PlayAt(fx.impactClips, point, 0.6f);
        }

        /// <summary>The grenade burst: the vendor explosion and a sound at the point.</summary>
        public static void Explosion(Vector3 point)
        {
            ShotEffects fx = Load();
            if (fx == null) return;
            if (fx.explosionPrefab != null)
            {
                GameObject boom = SpawnSilent(fx.explosionPrefab, point + Vector3.up * 0.1f, Quaternion.identity);
                Destroy(boom, fx.explosionLifetime);
            }
            if (fx.explosionClip != null) PlayAt(fx.explosionClip, point, 1f);
        }

        /// <summary>
        /// A vendor effect without its own sound: one sound for one event (Bruno, 2026-10-25). The copy is made
        /// under an inactive holder, so no source plays on awake before it is off. The sources are turned off, not
        /// removed: a vendor audio filter needs its source.
        /// </summary>
        public static GameObject SpawnSilent(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (holder == null)
            {
                holder = new GameObject("ShotEffects_Holder");
                holder.SetActive(false);
                holder.hideFlags = HideFlags.HideAndDontSave;
            }
            GameObject copy = Instantiate(prefab, position, rotation, holder.transform);
            foreach (AudioSource source in copy.GetComponentsInChildren<AudioSource>(true))
            {
                source.playOnAwake = false;
                source.enabled = false;
            }
            copy.transform.SetParent(parent, true);
            return copy;
        }
        private static GameObject holder;

        /// <summary>A random clip of the set at the point (a 3D one-shot).</summary>
        public static void PlayAt(AudioClip[] clips, Vector3 point, float volume = 1f)
        {
            if (clips == null || clips.Length == 0) return;
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip != null) PlayAt(clip, point, volume);
        }

        public static void PlayAt(AudioClip clip, Vector3 point, float volume)
        {
            var obj = new GameObject("OneShot_" + clip.name);
            obj.transform.position = point;
            var source = obj.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.minDistance = 1.5f;
            source.maxDistance = 40f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.Play();
            Destroy(obj, clip.length + 0.1f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            cached = null;
            loaded = false;
            holder = null;
        }
    }
}
