#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Characters;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The answer to a hit that does not kill (Bruno, 2026-10-27: option D, then "we should use invector clips for
    /// everyone"): the vendor's small and big hit states from the side of the attacker, a stagger step, a limp, a
    /// spasm for a shock, a panic for a fire, a heavy that only flinches small, and a fear pose that holds. In the AI
    /// test room. Batch: tools/unity.sh test playmode HitReactionTests.
    /// </summary>
    public class HitReactionTests
    {
        private static readonly string[] Sides = { "Front", "Right", "Left", "Back" };
        private GameObject lucy;
        private SoldierBrain[] soldiers;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            GameManager.Instance.LoadLevel("AiTest");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "AiTest" && GameManager.Instance.IsPlaying, 10f);
            yield return null;
            lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            soldiers = UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).OrderBy(s => s.name).ToArray();
            // No aim in these tests: nobody shoots her.
            foreach (DummyShooter gun in UnityEngine.Object.FindObjectsByType<DummyShooter>(FindObjectsSortMode.None)) gun.enabled = false;
            yield return new WaitForSeconds(0.3f);
        }

        [UnityTest]
        public IEnumerator ASmallHit_PlaysTheVendorSmallHit_FromHerSide_AndSteps()
        {
            SoldierBrain soldier = Find("Soldier_A");
            HitReactions reactions = soldier.GetComponent<HitReactions>();
            Assert.IsNotNull(reactions, "the factory adds HitReactions (BuildEnemyPrefabs)");
            Animator animator = soldier.GetComponent<Animator>();
            Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            int expected = reactions.DirectionOf(lucy.transform.position);
            Hit(soldier, chest.position + soldier.transform.forward * 0.12f, 12f, DamageType.EnemyProjectile, 0f);
            Assert.AreEqual(HitReactions.Zone.Chest, reactions.LastZone);
            Assert.IsFalse(reactions.LastBig, "12 damage: a small hit");
            Assert.AreEqual(expected, reactions.LastDirection);
            Assert.AreEqual(0, animator.GetInteger("ReactionID"));
            Assert.AreEqual(expected, animator.GetInteger("HitDirection"), "the vendor's HitDirection");
            string state = null;
            yield return WaitFor(() => (state = StateOf(animator, "UpperBody", "Small_From_")) != null, 0.6f);
            Debug.Log($"[HitReactionTests] small hit: direction {expected}, the vendor state {state}");
            Assert.AreEqual("Small_From_" + Side(expected), state, "the vendor's small hit from her side, on the upper body");
            yield return new WaitForSeconds(0.4f);
            Debug.Log($"[HitReactionTests] small hit: stepped {reactions.StepTravelled:0.00} m");
            Assert.Greater(reactions.StepTravelled, 0.08f, "a step the way the hit pushed (the AI's own walk not counted)");
        }

        [UnityTest]
        public IEnumerator AStrongHit_PlaysTheVendorBigHit_OnTheWholeBody()
        {
            SoldierBrain soldier = Find("Soldier_A");
            HitReactions reactions = soldier.GetComponent<HitReactions>();
            Animator animator = soldier.GetComponent<Animator>();
            Vector3 start = soldier.transform.position;
            Hit(soldier, soldier.transform.position + Vector3.up * 1.2f, 20f, DamageType.PsychicBlast, 0.5f);
            Assert.IsTrue(reactions.LastBig, "a blast of half a charge: a big hit");
            Assert.AreEqual(1, animator.GetInteger("ReactionID"));
            string state = null;
            yield return WaitFor(() => (state = StateOf(animator, "FullBody", "Big_From_")) != null, 0.6f);
            Assert.AreEqual("Big_From_" + Side(reactions.LastDirection), state, "the vendor's big hit on the whole body");
            yield return new WaitForSeconds(1f);
            Debug.Log($"[HitReactionTests] big hit: {state}, the clip moved the body {Flat(soldier.transform.position - start).magnitude:0.00} m");
            Assert.AreEqual(0f, reactions.StepTravelled, 0.001f, "no extra step: the big clip moves the body");
        }

        [UnityTest]
        public IEnumerator ALegHit_Limps()
        {
            SoldierBrain b = soldiers.First(s => s.name != "Soldier_A" && !s.GetComponent<EnemyRagdoll>().DeathOnly);
            HitReactions rb = b.GetComponent<HitReactions>();
            Hit(b, b.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.LeftLowerLeg).position, 8f, DamageType.EnemyProjectile, 0f);
            yield return null;
            Assert.AreEqual(HitReactions.Zone.Leg, rb.LastZone);
            Assert.IsTrue(rb.Limping, "a leg hit makes a limp");
            Assert.AreEqual(HitReactions.LimpAnimatorSpeed, b.GetComponent<Animator>().speed, 0.01f, "a slower walk");
            yield return new WaitForSeconds(HitReactions.LimpSeconds + 0.3f);
            Assert.IsFalse(rb.Limping);
            Assert.AreEqual(1f, b.GetComponent<Animator>().speed, 0.01f, "the walk is normal again");
        }

        [UnityTest]
        public IEnumerator AShock_LocksTheBody_AndShakesIt()
        {
            SoldierBrain a = Find("Soldier_A");
            HitReactions r = a.GetComponent<HitReactions>();
            Animator animator = a.GetComponent<Animator>();
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            Hit(a, a.transform.position + Vector3.up * 1.2f, 5f, DamageType.ShortShock, 0f);
            yield return null;
            Assert.AreEqual("spasm", r.LastClip);
            Assert.IsTrue(r.Spasming);
            Assert.AreEqual(0f, animator.speed, 0.001f, "the body locks: the animator stops");
            // The largest turn of the head from the locked pose over 0.3 s (a frame's change depends on the frame rate).
            float shake = 0f;
            Quaternion locked = head.localRotation;
            float t0 = Time.time;
            while (Time.time - t0 < 0.3f)
            {
                yield return null;
                shake = Mathf.Max(shake, Quaternion.Angle(locked, head.localRotation));
            }
            Debug.Log($"[HitReactionTests] spasm: the head turns up to {shake:0.0} degrees from the locked pose");
            Assert.Greater(shake, 3f, "the locked body shakes");
            Assert.Less(shake, 40f, "a shake, not a drift (the animator writes the locked pose each frame)");
            yield return new WaitForSeconds(HitReactions.SpasmSeconds);
            Assert.IsFalse(r.Spasming);
            Assert.AreEqual(1f, animator.speed, 0.001f, "the animator runs again");
        }

        [UnityTest]
        public IEnumerator AFire_Panics_ForThreeSeconds_WithBigHits_AndNoAim()
        {
            SoldierBrain b = soldiers.First(s => s.name != "Soldier_A" && !s.GetComponent<EnemyRagdoll>().DeathOnly);
            HitReactions rb = b.GetComponent<HitReactions>();
            BurningStatus.Ignite(b.gameObject, lucy, 1f, 4f);
            b.GetComponent<DummyShooter>().enabled = true;
            yield return null;
            yield return null;
            Assert.IsTrue(rb.Writhing, "the fire: a panic");
            float t0 = Time.time;
            int flinches0 = rb.Flinches;
            bool aimed = false, allBig = true;
            while (Time.time - t0 < HitReactions.WritheSeconds - 0.2f)
            {
                aimed |= b.GetComponent<DummyShooter>().HoldsSlot;
                if (rb.LastClip == "fire") allBig &= rb.LastBig;
                yield return null;
            }
            int big = rb.Flinches - flinches0;
            Debug.Log($"[HitReactionTests] fire panic: {big} big hits in {HitReactions.WritheSeconds - 0.2f:0.0} s");
            Assert.GreaterOrEqual(big, 3, "a big hit from a new side about every 0.8 s");
            Assert.IsTrue(allBig);
            Assert.IsFalse(aimed, "no aim while he panics");
        }

        [UnityTest]
        public IEnumerator TheHeavy_OnlyFlinchesSmall()
        {
            SoldierBrain armour = Find("Armour_C");
            HitReactions r = armour.GetComponent<HitReactions>();
            Hit(armour, armour.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.LeftLowerLeg).position, 40f, DamageType.PsychicBlast, 0.5f);
            yield return null;
            Assert.AreEqual(1, r.Flinches, "a flinch");
            Assert.IsFalse(r.LastBig, "only small");
            Assert.IsFalse(r.Limping, "no limp");
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(0f, r.StepTravelled, 0.001f, "no step");
        }

        [UnityTest]
        public IEnumerator AFearPose_Holds_AndTheBodyJolts()
        {
            SoldierBrain soldier = Find("Soldier_A");
            PoseOverride pose = soldier.GetComponent<PoseOverride>() ?? soldier.gameObject.AddComponent<PoseOverride>();
            PoseClips clips = PoseClips.Load();
            pose.Play(clips.scared01, loop: true);
            yield return null;
            Hit(soldier, soldier.transform.position + Vector3.up * 1.3f, 8f, DamageType.EnemyProjectile, 0f);
            Assert.AreEqual("jolt", soldier.GetComponent<HitReactions>().LastClip, "a jolt, no vendor flinch under the pose");
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(clips.scared01, pose.Current, "the fear pose holds");
            pose.Stop();
        }

        [UnityTest]
        public IEnumerator Lucy_FlinchesOnHerUpperBody_FromTheSideOfTheShot()
        {
            // Bruno, 2026-10-27: "plus hit on lucy". The vendor's small hit on a layer over her upper body.
            LucyAnimator lucyAnimator = lucy.GetComponent<LucyAnimator>();
            Assert.IsNotNull(lucyAnimator);
            yield return null;
            Animator animator = lucyAnimator.Animator;
            int layer = animator.GetLayerIndex("Hit");
            Assert.GreaterOrEqual(layer, 0, "her controller has the Hit layer (CreateCharacterAssets.BuildAll)");
            SoldierBrain shooter = Find("Soldier_A");
            int expected = lucyAnimator.DirectionOf(shooter.transform.position);
            Health her = lucy.GetComponentInChildren<Health>();
            HitEventBus.Raise(new HitInfo { Attacker = shooter.gameObject, Victim = her.gameObject, HitTransform = lucy.transform, HitPoint = lucy.transform.position + Vector3.up * 1.2f, HitNormal = Vector3.up, DamageAmount = 10f, DamageType = DamageType.EnemyProjectile });
            Assert.AreEqual(1, lucyAnimator.Hits, "a flinch");
            Assert.AreEqual(expected, lucyAnimator.LastHitDirection);
            HitEventBus.Raise(new HitInfo { Attacker = shooter.gameObject, Victim = her.gameObject, HitTransform = lucy.transform, HitPoint = lucy.transform.position, HitNormal = Vector3.up, DamageAmount = 10f, DamageType = DamageType.EnemyProjectile });
            Assert.AreEqual(1, lucyAnimator.Hits, "a second bullet of the same burst: no second flinch");
            string state = null;
            yield return WaitFor(() => (state = StateOf(animator, "Hit", "Hit_")) != null, 0.5f);
            Debug.Log($"[HitReactionTests] Lucy: direction {expected}, the state {state}");
            Assert.AreEqual("Hit_" + Side(expected), state, "the vendor's small hit from the side of the shot");
            yield return WaitFor(() => StateOf(animator, "Hit", "Hit_") == null, 2f);
            Assert.IsNull(StateOf(animator, "Hit", "Hit_"), "the layer goes back to None");
        }

        /// <summary>The name of the state that plays (or comes next) on the layer, among the vendor's hit states.</summary>
        private static string StateOf(Animator animator, string layer, string prefix)
        {
            int index = animator.GetLayerIndex(layer);
            if (index < 0) return null;
            foreach (string side in Sides)
            {
                int hash = Animator.StringToHash(prefix + side);
                if (animator.GetCurrentAnimatorStateInfo(index).shortNameHash == hash) return prefix + side;
                if (animator.IsInTransition(index) && animator.GetNextAnimatorStateInfo(index).shortNameHash == hash) return prefix + side;
            }
            return null;
        }

        private static string Side(int direction) => direction == 0 ? "Front" : direction == 90 ? "Right" : direction == -90 ? "Left" : "Back";

        private void Hit(SoldierBrain figure, Vector3 point, float damage, DamageType type, float charge)
        {
            Health h = figure.GetComponent<Health>();
            h.TakeDamage(damage);
            Vector3 normal = (lucy.transform.position - point).normalized;
            HitEventBus.Raise(new HitInfo { Attacker = lucy, Victim = figure.gameObject, HitTransform = figure.transform, HitPoint = point, HitNormal = normal, DamageAmount = damage, DamageType = type, Charge = charge, WasFatal = h.IsDead });
        }

        private SoldierBrain Find(string name)
        {
            SoldierBrain s = soldiers.FirstOrDefault(x => x.name == name);
            Assert.IsNotNull(s, name);
            return s;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.time;
            while (!condition() && Time.time - t0 < timeoutSeconds) yield return null;
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
