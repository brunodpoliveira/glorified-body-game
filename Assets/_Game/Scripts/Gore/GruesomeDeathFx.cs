using UnityEngine;
using GlorifiedBody.Combat;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Gore.ThatIndividual;

namespace GlorifiedBody.Gore
{
    /// <summary>
    /// The gruesome kill (design §11.4: the others see a colleague die "in a gruesome manner"). On a fatal hit of a
    /// gruesome kind (the short's shock, the telekinesis: a slam, a throw, a returned round; an explosion) the head
    /// bursts: the head bone shrinks to nothing (the neck is the stump), the vendor skull and meat parts fly from
    /// it, and a heavy spray leaves the neck. Full gore only. The vendor's own slicer needs a ragdoll and cut
    /// models, so this is the cheap path (the gore agent's report, 2026-10-24). PROPOSED.
    /// </summary>
    public sealed class GruesomeDeathFx : MonoBehaviour
    {
        private const float HeadScale = 0.01f;
        private const float PartsLifetime = 5f;

        public static int HeadsBurst { get; private set; }

        private void OnEnable() => HitEventBus.OnHit += OnHit;
        private void OnDisable() => HitEventBus.OnHit -= OnHit;

        public static bool BurstsTheHead(DamageType type)
        {
            switch (type)
            {
                case DamageType.ShortShock:
                case DamageType.PsychicTelekinesis:
                case DamageType.Explosion:
                case DamageType.Grenade:
                case DamageType.EnemyRocket:
                    return true;
                default:
                    return false;
            }
        }

        private void OnHit(HitInfo hit)
        {
            if (hit == null || !hit.WasFatal || hit.Victim == null || !BurstsTheHead(hit.DamageType)) return;
            if (hit.Victim.CompareTag("Player")) return;
            if (GoreManager.Instance == null || GoreManager.Instance.CurrentTier != GoreTier.Full) return;
            Animator animator = hit.Victim.GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) return;
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head == null || hit.Victim.GetComponent<HeadStump>() != null) return;

            Vector3 at = head.position;
            // The animator writes the bone scale back every frame: the stump holds it after the animation.
            HeadStump stump = hit.Victim.GetComponent<HeadStump>() ?? hit.Victim.AddComponent<HeadStump>();
            stump.Hold(head, HeadScale);
            HeadsBurst++;
            ThatIndividualGoreProfile profile = ThatIndividualGoreDriver.Instance != null ? ThatIndividualGoreDriver.Instance.Profile : null;
            if (profile != null)
            {
                Spawn(profile.fatalHeavyExtras, at, Quaternion.identity);
                Spawn(profile.fatalBursts, at, Quaternion.LookRotation(Vector3.up));
                Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck);
                Spawn(profile.heavySprays, neck != null ? neck.position : at, Quaternion.LookRotation(Vector3.up));
            }
            RunTelemetry.Event("gore_head_burst", $"{hit.Victim.name}:{hit.DamageType}");
        }

        private static void Spawn(GameObject[] set, Vector3 at, Quaternion rotation)
        {
            if (set == null || set.Length == 0) return;
            GameObject prefab = set[Random.Range(0, set.Length)];
            if (prefab == null) return;
            GameObject fx = Instantiate(prefab, at, rotation);
            foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>()) ps.Play(true);
            Destroy(fx, PartsLifetime);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => HeadsBurst = 0;
    }

    /// <summary>Keeps a burst head at nothing after the animator's pass, every frame.</summary>
    public sealed class HeadStump : MonoBehaviour
    {
        private Transform head;
        private float scale;

        public void Hold(Transform bone, float value)
        {
            head = bone;
            scale = value;
            if (head != null) head.localScale = Vector3.one * scale;
        }

        private void LateUpdate()
        {
            if (head != null) head.localScale = Vector3.one * scale;
        }
    }
}
