#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Automation;
using GlorifiedBody.Characters;
using GlorifiedBody.Enemy;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The T19 cast in the engine (2026-10-21): the catalog, the Humanoid avatars, the heights, the forward axis,
    /// the clips, Lucy's animator on the tank controls, the dummies' animator on the shooter, the line-up.
    /// </summary>
    public class CharacterTests
    {
        private const string MainMenu = "Main_Menu";
        private const string Room = "TrainingRoom";

        private static readonly string[] Classes = { "lucy_bare", "lucy_guard", "patient", "nurse", "labworker", "guard", "soldier", "fireman", "power_armour" };

        [DefaultExecutionOrder(-1000)]
        private sealed class PadDriver : MonoBehaviour
        {
            public VirtualPad pad;
            private void Update() => pad?.Commit();
        }

        [UnityTest]
        public IEnumerator Catalog_HasTheNineClasses_AsHumanoids_AtTheirHeights_FacingForward()
        {
            CharacterCatalog catalog = CharacterCatalog.Load();
            Assert.IsNotNull(catalog, "Resources/Characters/catalog (CreateCharacterAssets.BuildAll)");
            foreach (string id in Classes)
            {
                CharacterEntry entry = catalog.Find(id);
                Assert.IsNotNull(entry, id + " is in the catalog");
                Assert.IsNotNull(entry.prefab, id + " has a prefab");
                Assert.Greater(entry.height, 1f, id + " has a height");

                GameObject figure = UnityEngine.Object.Instantiate(entry.prefab, new Vector3(0f, 0f, -20f), Quaternion.identity);
                Animator animator = figure.GetComponentInChildren<Animator>();
                Assert.IsNotNull(animator, id + ": an Animator on the model");
                Assert.IsNotNull(animator.avatar, id + ": an avatar");
                Assert.IsTrue(animator.avatar.isValid && animator.avatar.isHuman, id + ": a valid Humanoid avatar (the mixamo bones mapped)");
                Assert.IsNotNull(animator.runtimeAnimatorController, id + ": a controller");
                yield return null;
                yield return null;

                SkinnedMeshRenderer skin = figure.GetComponentInChildren<SkinnedMeshRenderer>();
                Assert.IsNotNull(skin, id + ": a skinned mesh");
                Assert.IsNotNull(skin.sharedMaterial, id + ": a material");
                Assert.IsTrue(skin.sharedMaterial.shader.name.Contains("Universal Render Pipeline"), id + ": a URP material (" + skin.sharedMaterial.shader.name + ")");
                Assert.IsNotNull(skin.sharedMaterial.GetTexture("_BaseMap"), id + ": the base colour");
                Assert.AreEqual(0f, skin.sharedMaterial.GetFloat("_Smoothness"), 0.01f, id + ": matte");
                float height = skin.bounds.size.y;
                Assert.AreEqual(entry.height, height, entry.height * 0.1f, $"{id}: the model stands {entry.height} m tall (measured {height:0.00})");
                Assert.Less(Mathf.Abs(skin.bounds.min.y - figure.transform.position.y), entry.height * 0.06f, $"{id}: the feet on the root (off by {skin.bounds.min.y - figure.transform.position.y:0.000} m in its own idle)");

                // The forward axis through the Humanoid bones: the right shoulder at +x, the toes in front of the ankles (+z).
                Transform left = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                Transform right = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                Assert.IsNotNull(left, id + ": the left upper arm bone");
                Assert.IsNotNull(right, id + ": the right upper arm bone");
                Vector3 across = right.position - left.position;
                Assert.Greater(across.x, 0.15f, $"{id}: the right arm is at her right (+x), across={across}");
                Assert.Less(Mathf.Abs(across.z), 0.12f, $"{id}: the shoulders span x, not z, across={across}");
                Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform toes = animator.GetBoneTransform(HumanBodyBones.LeftToes);
                if (foot != null && toes != null)
                {
                    Assert.Greater(toes.position.z - foot.position.z, 0.03f, $"{id}: the toes point forward (+z), toes-ankle z={toes.position.z - foot.position.z:0.000}");
                }

                var clipNames = new List<string>();
                foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips) clipNames.Add(clip.name.ToLowerInvariant());
                foreach (string wanted in new[] { "idle", "walk", "run" })
                {
                    Assert.IsTrue(clipNames.Contains(wanted), $"{id}: the controller has the clip '{wanted}' ({string.Join(", ", clipNames)})");
                }
                UnityEngine.Object.Destroy(figure);
            }
        }

        [UnityTest]
        public IEnumerator Lucy_WearsTheModel_AndTheAnimatorFollowsTheTankControls()
        {
            yield return LoadRoom();
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            Assert.AreEqual("Lucy", player.name, "the capsule placeholder is gone");
            Assert.IsNull(player.GetComponent<MeshRenderer>(), "no primitive mesh on the root");
            Assert.IsTrue(player.transform.Find("Nose") == null, "no nose marker");
            TankController tank = player.GetComponent<TankController>();
            LucyAnimator driver = player.GetComponent<LucyAnimator>();
            Assert.IsNotNull(driver, "LucyAnimator on Lucy");
            Animator animator = player.GetComponentInChildren<Animator>();
            Assert.IsNotNull(animator, "the model's Animator under Lucy");
            Assert.IsTrue(animator.avatar != null && animator.avatar.isHuman, "Humanoid");
            CharacterController controller = player.GetComponent<CharacterController>();
            Assert.AreEqual(1.7f, controller.height, 0.01f, "the controller is her height");
            Assert.AreEqual(0.85f, controller.center.y, 0.01f, "centred on her body, the root at her feet");
            Assert.Less(Mathf.Abs(player.transform.position.y), 0.15f, "she stands on the floor");

            var pad = new VirtualPad();
            PadDriver padDriver = new GameObject("[PadDriver]").AddComponent<PadDriver>();
            padDriver.pad = pad;
            GameInput.SetBotSource(pad);
            try
            {
                yield return null;
                yield return null;
                Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"), "idle in the locomotion blend");
                Assert.Less(animator.GetFloat(LucyAnimator.SpeedHash), 0.05f, "Speed 0 when she stands");

                pad.SetHeld(InputManager.GameAction.Forward, true);
                yield return new WaitForSeconds(0.5f);
                Assert.AreEqual(0.5f, driver.Speed, 0.01f, "the walk asks for Speed 0.5");
                Assert.Greater(animator.GetFloat(LucyAnimator.SpeedHash), 0.3f, "the blend moved toward the walk");
                pad.SetHeld(InputManager.GameAction.Run, true);
                yield return new WaitForSeconds(0.5f);
                Assert.AreEqual(1f, driver.Speed, 0.01f, "the run asks for Speed 1");
                Assert.Greater(animator.GetFloat(LucyAnimator.SpeedHash), 0.8f, "the blend moved toward the run");
                pad.ReleaseAll();
                yield return null;

                pad.SetHeld(InputManager.GameAction.Back, true);
                yield return new WaitForSeconds(0.4f);
                Assert.IsTrue(animator.GetBool(LucyAnimator.BackHash), "Back while she steps back");
                Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("WalkBack"), "the walk plays backwards");
                pad.ReleaseAll();
                yield return null;
                yield return null;

                pad.Tap(InputManager.GameAction.Dodge);
                yield return null;
                yield return null;
                yield return new WaitForSeconds(0.15f);
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
                Assert.IsTrue(state.IsName("Dodge") || next.IsName("Dodge"), "the dodge plays its state");
                yield return new WaitForSeconds(tank.DodgeCooldown);
                yield return WaitUntil(() => animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion") && !animator.IsInTransition(0), 3f);
                Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"), "back in the locomotion blend after the dodge");

                pad.SetHeld(InputManager.GameAction.Charge, true);
                yield return new WaitForSeconds(0.3f);
                Assert.IsTrue(animator.GetBool(LucyAnimator.ChargingHash), "Charging while she holds the charge");
                state = animator.GetCurrentAnimatorStateInfo(0);
                next = animator.GetNextAnimatorStateInfo(0);
                Assert.IsTrue(state.IsName("Charge") || next.IsName("Charge"), "the charge stance");
                pad.ReleaseAll();
            }
            finally
            {
                pad.ReleaseAll();
                GameInput.ClearBotSource(pad);
                UnityEngine.Object.Destroy(padDriver.gameObject);
            }
        }

        /// <summary>
        /// The walk and the run of the source travel across the floor (1.5 m and 3 m per cycle at 1 m scale, the
        /// hips move). The import takes the travel out as root motion that the Animator does not apply, so the
        /// model stays over her root: no run ahead, no snap back at the loop, no slide back when she stops
        /// (Bruno, 2026-10-21: "her model slides back"). Baked into the pose, the hips reached 2.6 m from the root.
        /// </summary>
        [UnityTest]
        public IEnumerator Lucy_WalksAndRunsInPlace_TheModelStaysOverHerRoot()
        {
            yield return LoadRoom();
            foreach (DummyShooter shooter in UnityEngine.Object.FindObjectsByType<DummyShooter>(FindObjectsSortMode.None)) shooter.enabled = false;
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            TankController tank = player.GetComponent<TankController>();
            Animator animator = player.GetComponentInChildren<Animator>();
            Transform model = animator.transform;
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Assert.IsNotNull(hips, "the hips bone of the Humanoid");
            // Facing north from the south half: about 9 m of floor ahead, no collider on her line.
            tank.Teleport(new Vector3(0f, 1f, -4f), 0f);

            var pad = new VirtualPad();
            PadDriver padDriver = new GameObject("[PadDriver]").AddComponent<PadDriver>();
            padDriver.pad = pad;
            GameInput.SetBotSource(pad);
            var worst = new float[1];
            try
            {
                yield return null;
                yield return null;
                float idle = HipsOffset(hips, player.transform);
                Assert.Less(idle, HipsLimit, $"idle: the hips over the root ({idle:0.00} m)");

                pad.SetHeld(InputManager.GameAction.Forward, true);
                yield return SampleHips(hips, player.transform, model, 2f, worst);
                Assert.Less(worst[0], HipsLimit, $"walk: the hips stay over the root (worst {worst[0]:0.00} m)");

                pad.SetHeld(InputManager.GameAction.Run, true);
                yield return SampleHips(hips, player.transform, model, 1.2f, worst);
                Assert.Less(worst[0], HipsLimit, $"run: the hips stay over the root (worst {worst[0]:0.00} m)");

                pad.ReleaseAll();
                Vector3 stop = player.transform.position;
                yield return SampleHips(hips, player.transform, model, 0.6f, worst);
                Assert.Less(worst[0], HipsLimit, $"stop: no slide back ({worst[0]:0.00} m)");
                Assert.Less(Vector3.Distance(stop, player.transform.position), 0.05f, "the root stays where she stopped");
                Assert.Greater(stop.z, -1f, $"she crossed the floor (z {stop.z:0.0})");
            }
            finally
            {
                pad.ReleaseAll();
                GameInput.ClearBotSource(pad);
                UnityEngine.Object.Destroy(padDriver.gameObject);
            }
        }

        /// <summary>
        /// Every class plays its locomotion in place: the idle, the walk and the run sampled across one cycle on
        /// the prefab (Animator.Play at a normalized time, then Update(0)). The hips never leave the root and the
        /// cycle has no net travel. The import takes the travel out of a clip that moves in the source
        /// (CharacterImportSettings with tools/characters_clips.py), so a future clip set cannot slide.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryClass_PlaysItsLocomotionInPlace()
        {
            CharacterCatalog catalog = CharacterCatalog.Load();
            Assert.IsNotNull(catalog, "Resources/Characters/catalog (CreateCharacterAssets.BuildAll)");
            foreach (string id in Classes)
            {
                CharacterEntry entry = catalog.Find(id);
                Assert.IsNotNull(entry, id + " is in the catalog");
                GameObject figure = UnityEngine.Object.Instantiate(entry.prefab, new Vector3(0f, 0f, -20f), Quaternion.identity);
                Animator animator = figure.GetComponentInChildren<Animator>();
                Assert.IsNotNull(animator, id + ": an Animator");
                yield return null;
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                Assert.IsNotNull(hips, id + ": the hips bone");
                foreach (float speed in new[] { 0f, 0.5f, 1f })
                {
                    string clip = speed == 0f ? "idle" : speed == 0.5f ? "walk" : "run";
                    animator.SetFloat("Speed", speed);
                    Vector3 first = Vector3.zero;
                    Vector3 last = Vector3.zero;
                    float worst = 0f;
                    const int steps = 20;
                    for (int i = 0; i <= steps; i++)
                    {
                        animator.Play("Locomotion", 0, i / (float)steps);
                        animator.Update(0f);
                        Vector3 d = hips.position - figure.transform.position;
                        d.y = 0f;
                        if (i == 0) first = d;
                        last = d;
                        worst = Mathf.Max(worst, d.magnitude);
                    }
                    float travel = (last - first).magnitude;
                    Assert.Less(travel, 0.1f * entry.height, $"{id} {clip}: no travel over a cycle ({travel:0.00} m, the source walk baked would give {1.5f * entry.height:0.0} m)");
                    Assert.Less(worst, 0.3f * entry.height, $"{id} {clip}: the hips stay over the root (worst {worst:0.00} m)");
                    Assert.Less(animator.transform.localPosition.magnitude, 0.01f, $"{id} {clip}: the Animator does not move the model root");
                }
                UnityEngine.Object.Destroy(figure);
                yield return null;
            }
        }

        /// <summary>The hips sit near the centre of mass, which the import keeps over the root. 0.35 m leaves room for the sway.</summary>
        private const float HipsLimit = 0.35f;

        private static float HipsOffset(Transform hips, Transform root)
        {
            Vector3 d = hips.position - root.position;
            d.y = 0f;
            return d.magnitude;
        }

        /// <summary>Samples the hips offset every frame for a while. worst[0] = the largest offset. The model root must not move.</summary>
        private static IEnumerator SampleHips(Transform hips, Transform root, Transform model, float seconds, float[] worst)
        {
            worst[0] = 0f;
            float t0 = Time.time;
            while (Time.time - t0 < seconds)
            {
                yield return null;
                worst[0] = Mathf.Max(worst[0], HipsOffset(hips, root));
                if (model.localPosition.magnitude > 0.01f) worst[0] = Mathf.Max(worst[0], 99f);
            }
        }

        [UnityTest]
        public IEnumerator Dummy_WearsTheGuard_AndAimsWithTheAimPose()
        {
            yield return LoadRoom();
            GameObject dummy = GameObject.Find("Dummy_A");
            Assert.IsNull(dummy.GetComponent<MeshRenderer>(), "no primitive mesh on the dummy root");
            SkinnedMeshRenderer skin = dummy.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.IsNotNull(skin, "the guard model under Dummy_A");
            Assert.IsNotNull(dummy.GetComponent<EnemyAnimator>(), "EnemyAnimator on the dummy");
            Animator animator = dummy.GetComponentInChildren<Animator>();
            Assert.IsTrue(animator != null && animator.avatar.isHuman, "Humanoid");
            CapsuleCollider capsule = dummy.GetComponent<CapsuleCollider>();
            Assert.AreEqual(1.8f, capsule.height, 0.01f, "the collider is the guard's height");
            Assert.AreEqual(0.9f, capsule.center.y, 0.01f, "on its feet");
            Assert.AreEqual(Body.ChestHeight, dummy.GetComponent<Combat.LockOnTargetable>().GetLockPoint().y - dummy.transform.position.y, 0.01f, "the lock point at the chest");

            DummyShooter shooter = dummy.GetComponent<DummyShooter>();
            Assert.IsTrue(shooter.enabled, "the shooter runs in the room");
            yield return WaitUntil(() => shooter.HoldsSlot, 6f);
            Assert.IsTrue(shooter.HoldsSlot, "it aims at her");
            yield return null;
            yield return null;
            Assert.IsTrue(animator.GetBool(EnemyAnimator.AimingHash), "Aiming while it holds the slot");
            yield return new WaitForSeconds(0.25f);
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
            Assert.IsTrue(state.IsName("Aim") || next.IsName("Aim"), "the aim pose plays");
            Assert.AreEqual(Body.MuzzleHeight, shooter.Muzzle.y - dummy.transform.position.y, 0.01f, "the muzzle at chest height");
        }

        [UnityTest]
        public IEnumerator TheCast_StandsAlongTheNorthWall_InIdle()
        {
            yield return LoadRoom();
            string[] cast = { "power_armour", "lucy_guard", "patient", "nurse", "labworker", "fireman" };
            foreach (string id in cast)
            {
                GameObject figure = GameObject.Find("Cast_" + id);
                Assert.IsNotNull(figure, "Cast_" + id + " stands in the room");
                Assert.Greater(figure.transform.position.z, 4f, id + " along the north wall");
                Assert.IsNotNull(figure.GetComponentInChildren<SkinnedMeshRenderer>(), id + " has its model");
                Animator animator = figure.GetComponentInChildren<Animator>();
                Assert.IsNotNull(animator, id + " has an Animator");
                Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"), id + " plays its idle");
                Assert.IsNull(figure.GetComponent<Core.Health>(), id + " has no Health (visual only)");
            }
            GameObject powerArmour = GameObject.Find("Cast_power_armour");
            float tall = powerArmour.GetComponentInChildren<SkinnedMeshRenderer>().bounds.size.y;
            Assert.Greater(tall, 2.3f, $"the power armour is larger than a person ({tall:0.00} m)");
        }

        private static IEnumerator LoadRoom()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(MainMenu);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == MainMenu && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            GameManager.Instance.LoadGameplay();
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == Room && GameManager.Instance.IsPlaying, 10f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return new WaitForSeconds(0.2f);
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
