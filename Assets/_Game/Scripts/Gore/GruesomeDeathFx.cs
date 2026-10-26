using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.Combat;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Gore.ThatIndividual;

namespace GlorifiedBody.Gore
{
    /// <summary>
    /// The gruesome kill (design §11.4: the others see a colleague die "in a gruesome manner"). Full gore only
    /// (Bruno, 2026-10-24: "gore low: no cut").
    /// - A kill by the short's shock or by mk_orzabal bursts the head (Bruno: "the head burst is for shock enemy
    ///   kills", "tk bursts head"): the head bone shrinks to nothing (the neck is the stump), the vendor skull and
    ///   meat parts fly from it, and a heavy spray leaves the neck.
    /// - A full-charge mk_warspite kill and an explosion kill (a grenade, a rocket) cut an arm (Bruno: "explosions
    ///   dismember (same as gruesome warspite kill)", "forearm or whole arm, randomized", "blast kill at full
    ///   charge to cut"). The piece flies off (SeveredLimb, "flying piece too"), the bone shrinks to nothing, the
    ///   vendor's bone ends and meat (goreLimb) sit on the cut, with a gush. The ragdoll loses that limb's
    ///   colliders.
    /// The vendor slicer needs cut models for each class (the gore study, 2026-10-24): this is the cheap path.
    /// </summary>
    public sealed class GruesomeDeathFx : MonoBehaviour
    {
        private const float StumpScale = 0.01f;
        private const float PartsLifetime = 5f;

        private static readonly HumanBodyBones[] Limbs =
        {
            HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm,
        };

        public static int HeadsBurst { get; private set; }
        public static int LimbsCut { get; private set; }
        public static string LastLimb { get; private set; } = "";
        /// <summary>For the tests: the limb the next cut takes (None = at random).</summary>
        public static HumanBodyBones ForcedLimb = HumanBodyBones.LastBone;

        private void OnEnable() => HitEventBus.OnHit += OnHit;
        private void OnDisable() => HitEventBus.OnHit -= OnHit;

        public static bool BurstsTheHead(DamageType type) => type == DamageType.ShortShock || type == DamageType.PsychicTelekinesis;

        public static bool CutsALimb(DamageType type, float charge)
        {
            switch (type)
            {
                case DamageType.PsychicBlast:
                    return charge >= 0.999f;
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
            if (hit == null || !hit.WasFatal || hit.Victim == null || hit.Victim.CompareTag("Player")) return;
            bool head = BurstsTheHead(hit.DamageType);
            if (!head && !CutsALimb(hit.DamageType, hit.Charge)) return;
            if (GoreManager.Instance == null || GoreManager.Instance.CurrentTier != GoreTier.Full) return;
            Animator animator = hit.Victim.GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman || hit.Victim.GetComponent<BoneStump>() != null) return;
            ThatIndividualGoreProfile profile = ThatIndividualGoreDriver.Instance != null ? ThatIndividualGoreDriver.Instance.Profile : null;
            if (head) BurstHead(hit, animator, profile);
            else CutLimb(hit, animator, profile);
        }

        private static void BurstHead(HitInfo hit, Animator animator, ThatIndividualGoreProfile profile)
        {
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head == null) return;
            Vector3 at = head.position;
            // The animator writes the bone scale back every frame: the stump holds it after the animation.
            hit.Victim.AddComponent<BoneStump>().Hold(head, StumpScale);
            HeadsBurst++;
            if (profile != null)
            {
                Spawn(profile.fatalHeavyExtras, at, Quaternion.identity);
                Spawn(profile.fatalBursts, at, Quaternion.LookRotation(Vector3.up));
                Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck);
                Spawn(profile.heavySprays, neck != null ? neck.position : at, Quaternion.LookRotation(Vector3.up));
            }
            RunTelemetry.Event("gore_head_burst", $"{hit.Victim.name}:{hit.DamageType}");
        }

        private static void CutLimb(HitInfo hit, Animator animator, ThatIndividualGoreProfile profile)
        {
            var choices = new List<HumanBodyBones>();
            foreach (HumanBodyBones limb in Limbs)
                if (animator.GetBoneTransform(limb) != null) choices.Add(limb);
            if (choices.Count == 0) return;
            HumanBodyBones pick = choices.Contains(ForcedLimb) ? ForcedLimb : choices[Random.Range(0, choices.Count)];
            Transform bone = animator.GetBoneTransform(pick);
            Transform joint = bone.parent;
            Vector3 at = bone.position;
            Vector3 along = joint != null ? bone.position - joint.position : Vector3.up;
            Quaternion outward = Quaternion.LookRotation(along.sqrMagnitude > 0.0001f ? along.normalized : Vector3.up);

            // The piece first: it is baked from the pose before the bone shrinks.
            // A blast's normal faces her, an explosion's faces out from its centre: the piece flies away from both.
            bool burst = hit.DamageType != DamageType.PsychicBlast;
            SeveredLimb.Cut(hit.Victim, bone, burst ? hit.HitNormal : -hit.HitNormal, profile);
            hit.Victim.AddComponent<BoneStump>().Hold(bone, StumpScale);
            hit.Victim.GetComponentInParent<IRagdollBody>()?.Sever(bone);
            LimbsCut++;
            LastLimb = pick.ToString();
            if (profile != null)
            {
                // The bone ends and meat stay on the body (they ride the parent bone), the sprays fly and go.
                GameObject stumpPrefab = Pick(profile.limbStumps);
                if (stumpPrefab != null)
                {
                    GameObject stump = Instantiate(stumpPrefab, at, outward);
                    stump.name = "LimbStump";
                    if (joint != null) stump.transform.SetParent(joint, true);
                }
                // One gush on the body: the piece carries its own, and the fatal hit already plays the driver's spray.
                Spawn(profile.limbSprays, at, outward);
            }
            RunTelemetry.Event("gore_limb_cut", $"{hit.Victim.name}:{pick}:{hit.DamageType}");
        }

        /// <summary>
        /// The gore on a body that a load puts back: the stump holds the bone at nothing, a cut arm gets its bone
        /// ends and meat and loses its ragdoll colliders. No sprays, no flying parts: the kill is old.
        /// </summary>
        public static void RestoreStump(GameObject victim, Animator animator, HumanBodyBones which)
        {
            if (victim == null || animator == null || !animator.isHuman) return;
            Transform bone = animator.GetBoneTransform(which);
            if (bone == null) return;
            if (which != HumanBodyBones.Head)
            {
                victim.GetComponentInParent<IRagdollBody>()?.Sever(bone);
                ThatIndividualGoreProfile profile = ThatIndividualGoreDriver.Instance != null ? ThatIndividualGoreDriver.Instance.Profile : null;
                GameObject stumpPrefab = profile != null ? Pick(profile.limbStumps) : null;
                Transform joint = bone.parent;
                if (stumpPrefab != null)
                {
                    Vector3 along = joint != null ? bone.position - joint.position : Vector3.up;
                    GameObject stump = Instantiate(stumpPrefab, bone.position, Quaternion.LookRotation(along.sqrMagnitude > 0.0001f ? along.normalized : Vector3.up));
                    stump.name = "LimbStump";
                    Still(stump);
                    if (joint != null) stump.transform.SetParent(joint, true);
                }
            }
            victim.AddComponent<BoneStump>().Hold(bone, StumpScale);
        }

        /// <summary>A gore copy that a load puts back: its particles and sounds ended long ago (the gush, the mist).</summary>
        public static void Still(GameObject copy)
        {
            foreach (ParticleSystem ps in copy.GetComponentsInChildren<ParticleSystem>(true))
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (AudioSource source in copy.GetComponentsInChildren<AudioSource>(true)) source.enabled = false;
        }

        private static GameObject Pick(GameObject[] set) => set == null || set.Length == 0 ? null : set[Random.Range(0, set.Length)];

        private static void Spawn(GameObject[] set, Vector3 at, Quaternion rotation)
        {
            GameObject prefab = Pick(set);
            if (prefab == null) return;
            GameObject fx = Instantiate(prefab, at, rotation);
            foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>()) ps.Play(true);
            Destroy(fx, PartsLifetime);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            HeadsBurst = 0;
            LimbsCut = 0;
            LastLimb = "";
            ForcedLimb = HumanBodyBones.LastBone;
        }
    }

    /// <summary>Keeps a burst head or a cut limb at nothing after the animator's pass, every frame.</summary>
    public sealed class BoneStump : MonoBehaviour
    {
        private Transform bone;
        private float scale;

        public Transform Bone => bone;

        public void Hold(Transform target, float value)
        {
            bone = target;
            scale = value;
            if (bone != null) bone.localScale = Vector3.one * scale;
        }

        private void LateUpdate()
        {
            if (bone != null) bone.localScale = Vector3.one * scale;
        }
    }
}
