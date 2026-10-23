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
        [Tooltip("Spawned where a bullet hits the room: particles and its own sound.")]
        public GameObject impactPrefab;
        [Min(0f)] public float impactScale = 1f;
        [Min(0.1f)] public float impactLifetime = 3f;
        public Color tracerColour = new Color(1f, 0.85f, 0.45f);

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

        /// <summary>The impact on a wall or the floor, facing out of the surface.</summary>
        public static void Impact(Vector3 point, Vector3 normal)
        {
            ShotEffects fx = Load();
            if (fx == null || fx.impactPrefab == null) return;
            Quaternion facing = Quaternion.LookRotation(normal.sqrMagnitude > 0.001f ? normal : Vector3.up);
            GameObject impact = Instantiate(fx.impactPrefab, point + normal * 0.02f, facing);
            impact.transform.localScale *= fx.impactScale;
            Destroy(impact, fx.impactLifetime);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            cached = null;
            loaded = false;
        }
    }
}
