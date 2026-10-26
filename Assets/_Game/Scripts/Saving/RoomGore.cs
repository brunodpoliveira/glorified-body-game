using System;
using System.Collections.Generic;
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;
using GlorifiedBody.Gore;
using GlorifiedBody.Gore.ThatIndividual;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace GlorifiedBody.Saving
{
    /// <summary>
    /// The bodies and the blood of a room in a save (Bruno, 2026-10-25: the dead "stay on the floor; destruction and
    /// gore should be saved as well"). A save keeps each dead body's pose (the root and the humanoid bones), its
    /// stumps (a burst head, a cut arm), the cut arms on the floor, the floor stains and the pools under the bodies.
    /// A load puts them back with no second death: no one sees it, fears it or tends it, no pool grows, no spray
    /// flies. The Gore option of the moment applies: Off puts back no blood and no cuts, Low no cuts. The blood on
    /// the walls is the vendor's decals, which fade: it is not saved.
    /// </summary>
    public static class RoomGore
    {
        // ── Save ──────────────────────────────────────────────────────

        public static void Capture(Scene scene, RoomState room, IEnumerable<Health> dead)
        {
            foreach (Health h in dead)
            {
                if (h == null || !h.gameObject.activeInHierarchy) continue;
                room.bodies.Add(CaptureBody(h));
            }
            foreach (GameObject stain in FloorStains.All) AddStain(room, stain, scene, pool: false);
            foreach (GameObject pool in BodyBloodPool.Pools) AddStain(room, pool, scene, pool: true);
            foreach (SeveredLimb limb in SeveredLimb.All)
            {
                if (limb == null || limb.Victim == null || limb.Victim.scene != scene) continue;
                Animator animator = limb.Victim.GetComponentInChildren<Animator>();
                if (!TryBoneOf(animator, limb.Bone, out HumanBodyBones bone)) continue;
                room.limbs.Add(new LimbState
                {
                    body = ScenePaths.Of(limb.Victim.transform),
                    bone = bone.ToString(),
                    position = limb.transform.position,
                    boneTurn = limb.BoneTurn,
                });
            }
        }

        private static BodyState CaptureBody(Health h)
        {
            var state = new BodyState
            {
                path = ScenePaths.Of(h.transform),
                position = h.transform.position,
                rotation = h.transform.rotation,
            };
            if (h.TryGetComponent(out EnemyShield shield) && shield.Dropped && shield.Model != null)
            {
                state.shield = true;
                state.shieldPosition = shield.Model.position;
                state.shieldRotation = shield.Model.rotation;
            }
            EnemyRagdoll ragdoll = h.GetComponent<EnemyRagdoll>();
            // A ragdoll that still falls or hangs from a carer's hands falls again from the pose after a load.
            state.settled = ragdoll == null || !ragdoll.IsRagdolled || ragdoll.Current == EnemyRagdoll.State.Corpse;
            Animator animator = h.GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) return state;
            for (HumanBodyBones b = 0; b < HumanBodyBones.LastBone; b++)
            {
                Transform t = animator.GetBoneTransform(b);
                if (t == null) continue;
                state.bones.Add(new BonePose { bone = b.ToString(), local = t.localRotation });
                if (b == HumanBodyBones.Hips) state.hipsLocal = t.localPosition;
            }
            foreach (BoneStump stump in h.GetComponents<BoneStump>())
                if (TryBoneOf(animator, stump.Bone, out HumanBodyBones bone)) state.stumps.Add(bone.ToString());
            return state;
        }

        private static void AddStain(RoomState room, GameObject stain, Scene scene, bool pool)
        {
            if (stain == null || stain.scene != scene) return;
            Renderer renderer = stain.GetComponent<Renderer>();
            if (renderer == null || renderer.sharedMaterial == null) return;
            room.stains.Add(new StainState
            {
                pool = pool,
                material = renderer.sharedMaterial.name,
                position = stain.transform.position,
                rotation = stain.transform.rotation,
                scale = stain.transform.localScale,
            });
        }

        private static bool TryBoneOf(Animator animator, Transform t, out HumanBodyBones bone)
        {
            bone = HumanBodyBones.LastBone;
            if (animator == null || !animator.isHuman || t == null) return false;
            for (HumanBodyBones b = 0; b < HumanBodyBones.LastBone; b++)
            {
                if (animator.GetBoneTransform(b) != t) continue;
                bone = b;
                return true;
            }
            return false;
        }

        // ── Load ──────────────────────────────────────────────────────

        /// <summary>Puts the saved bodies back. Returns the paths it restored (the other dead stay hidden).</summary>
        public static HashSet<string> Restore(Scene scene, RoomState room)
        {
            var restored = new HashSet<string>();
            GoreTier tier = GoreManager.Instance != null ? GoreManager.Instance.CurrentTier : GoreTier.Full;
            var bodies = new List<(GameObject figure, BodyState state)>();
            foreach (BodyState state in room.bodies)
            {
                GameObject figure = ScenePaths.Find(scene, state.path);
                if (figure == null || !RestoreBody(figure, state)) continue;
                restored.Add(state.path);
                bodies.Add((figure, state));
            }

            ThatIndividualGoreProfile profile = ThatIndividualGoreDriver.Instance != null ? ThatIndividualGoreDriver.Instance.Profile : null;
            if (tier == GoreTier.Full)
            {
                // The pieces first: each is cut again from its body before the bone becomes a stump.
                foreach (LimbState limb in room.limbs)
                {
                    GameObject figure = ScenePaths.Find(scene, limb.body);
                    Animator animator = figure != null ? figure.GetComponentInChildren<Animator>() : null;
                    if (animator == null || !animator.isHuman || !Enum.TryParse(limb.bone, out HumanBodyBones bone)) continue;
                    SeveredLimb.Restore(figure, animator.GetBoneTransform(bone), limb.position, limb.boneTurn, profile);
                }
                foreach ((GameObject figure, BodyState state) in bodies)
                {
                    Animator animator = figure.GetComponentInChildren<Animator>();
                    foreach (string name in state.stumps)
                        if (Enum.TryParse(name, out HumanBodyBones bone)) GruesomeDeathFx.RestoreStump(figure, animator, bone);
                }
            }
            if (tier != GoreTier.Off && ThatIndividualGoreDriver.Instance != null)
            {
                foreach (StainState stain in room.stains)
                {
                    Material material = ThatIndividualGoreDriver.Instance.FindStainMaterial(stain.material);
                    if (material == null)
                    {
                        Debug.LogWarning($"[RoomGore] no stain material '{stain.material}'");
                        continue;
                    }
                    if (stain.pool) BodyBloodPool.Restore(material, stain.position, stain.rotation, stain.scale);
                    else FloorStains.Restore(material, stain.position, stain.rotation, stain.scale);
                }
            }
            return restored;
        }

        /// <summary>
        /// The figure dies again, quietly, and takes its saved pose. The root moves first (the shield and the gun
        /// fall there), then the death, then the bones over the death clip.
        /// </summary>
        private static bool RestoreBody(GameObject figure, BodyState state)
        {
            Health health = figure.GetComponent<Health>();
            if (health == null) return false;
            if (figure.TryGetComponent(out EnemyMorale morale)) morale.MarkOldDeath();
            CorpseCare.MarkTended(figure.transform);
            BodyBloodPool.MarkPooled(figure);

            if (figure.TryGetComponent(out NavMeshAgent agent)) agent.enabled = false;
            if (figure.TryGetComponent(out Rigidbody rootBody))
            {
                if (!rootBody.isKinematic) rootBody.linearVelocity = Vector3.zero;
                rootBody.isKinematic = true;
            }
            figure.transform.SetPositionAndRotation(state.position, state.rotation);
            if (!health.IsDead) health.SetHealth(0f);
            if (figure.TryGetComponent(out CapsuleCollider capsule)) capsule.enabled = false;

            Animator animator = figure.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman && state.bones.Count > 0)
            {
                animator.enabled = false;
                foreach (BonePose pose in state.bones)
                {
                    if (!Enum.TryParse(pose.bone, out HumanBodyBones b)) continue;
                    Transform t = animator.GetBoneTransform(b);
                    if (t == null) continue;
                    t.localRotation = pose.local;
                    if (b == HumanBodyBones.Hips) t.localPosition = state.hipsLocal;
                }
            }
            if (figure.TryGetComponent(out EnemyRagdoll ragdoll)) ragdoll.RestoreCorpse(state.settled);
            // The death dropped the shield again from the body, with a push that acts at the next physics step: it
            // goes back where it lay and stays still there (kinematic).
            if (state.shield && figure.TryGetComponent(out EnemyShield shield) && shield.Model != null)
            {
                if (shield.Model.TryGetComponent(out Rigidbody body))
                {
                    if (!body.isKinematic) body.linearVelocity = Vector3.zero;
                    body.isKinematic = true;
                }
                shield.Model.SetPositionAndRotation(state.shieldPosition, state.shieldRotation);
            }
            return true;
        }
    }
}
