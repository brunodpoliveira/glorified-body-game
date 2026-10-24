#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;
using GlorifiedBody.Gore;
using GlorifiedBody.Integration.Invector;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The soldiers' ragdoll (Bruno, 2026-10-24): mk_orzabal holds the floater limp and every ending drops it limp,
    /// a thrown body knocks down the one it hits, a full mk_warspite knocks him down for 2.5 s, a full mk_warspite
    /// kill cuts an arm and the piece flies off, and blood pools under the body. They stand up with the vendor's stand-up clips. In the AI
    /// test room. Batch: tools/unity.sh test playmode RagdollTests.
    /// </summary>
    public class RagdollTests
    {
        private GameObject lucy;
        private PsychicPowers powers;
        private SoldierBrain[] soldiers;
        private int savedGore;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            savedGore = SettingsManager.Instance.GoreLevel;
            SettingsManager.Instance.GoreLevel = 2;
            GameManager.Instance.LoadLevel("AiTest");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "AiTest" && GameManager.Instance.IsPlaying, 10f);
            yield return null;
            lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            powers = lucy.GetComponent<PsychicPowers>();
            PsychicStats stats = lucy.GetComponent<PsychicStats>();
            stats.RegenPaused = true;
            stats.DebugSet(45f, 0f);
            soldiers = UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).OrderBy(s => s.name).ToArray();
            DebugTools.RunTelemetry.OnEvent -= LogEvent;
            DebugTools.RunTelemetry.OnEvent += LogEvent;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DebugTools.RunTelemetry.OnEvent -= LogEvent;
            GruesomeDeathFx.ForcedLimb = HumanBodyBones.LastBone;
            if (SettingsManager.Instance != null) SettingsManager.Instance.GoreLevel = savedGore;
            yield return null;
        }

        private static void LogEvent(string name, string detail)
        {
            if (name.StartsWith("ragdoll") || name.StartsWith("tk_") || name.StartsWith("gore_") || name == "soldier_down")
                Debug.Log($"[RagdollTests] {Time.time:0.00} event {name} {detail}");
        }

        [UnityTest]
        public IEnumerator Lift_HangsLimpFromTheHips_TheReleaseDropsIt_AndHeStandsUp()
        {
            SoldierBrain soldier = Find("Soldier_A");
            EnemyRagdoll ragdoll = soldier.GetComponent<EnemyRagdoll>();
            var body = soldier.GetComponent<InvectorEnemyBody>();
            Assert.IsNotNull(ragdoll, "the soldier has a ragdoll");
            Assert.IsTrue(ragdoll.Available, "its bones are bound");
            yield return WaitUntil(() => soldier.Engaged, 6f);
            powers.Select(PowerKind.Telekinesis);
            powers.FireAt(powers.Selected, 1f, soldier.GetComponent<LockOnTargetable>(), 1f);
            yield return new WaitForSeconds(1.0f);
            Animator animator = soldier.GetComponent<Animator>();
            Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Debug.Log($"[RagdollTests] held: state={ragdoll.Current} root y={soldier.transform.position.y:0.00} hips y={ragdoll.Hips.position.y:0.00} foot y={foot.position.y:0.00}");
            Assert.AreEqual(EnemyRagdoll.State.Held, ragdoll.Current, "the floater is a held ragdoll");
            Assert.IsFalse(animator.enabled, "no animation while it hangs");
            Assert.IsTrue(ragdoll.Parts[0].isKinematic, "the hips follow the lift");
            Assert.IsTrue(ragdoll.Parts.Skip(1).All(p => !p.isKinematic), "the limbs hang loose");
            Assert.Greater(soldier.transform.position.y, 1f, "it hangs in the air");
            Assert.Less(foot.position.y, ragdoll.Hips.position.y - 0.4f, "the legs hang under the hips");

            Assert.IsTrue(soldier.GetComponent<TelekinesisLift>().Release(null), "dropped");
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(EnemyRagdoll.State.Down, ragdoll.Current, "it falls limp");
            yield return WaitUntil(() => ragdoll.Current == EnemyRagdoll.State.Animated, 8f);
            Debug.Log($"[RagdollTests] up: clip={ragdoll.StandUpClip} stands={ragdoll.StandUps} root={soldier.transform.position}");
            Assert.AreEqual(EnemyRagdoll.State.Animated, ragdoll.Current, "he stood up");
            Assert.AreEqual(1, ragdoll.StandUps);
            Assert.AreNotEqual("", ragdoll.StandUpClip, "with a vendor stand-up clip");
            yield return WaitUntil(() => !body.Suspended, 1f);
            Assert.IsFalse(body.Suspended || body.FsmStopped, "the body runs again");
            Assert.Less(soldier.transform.position.y, 0.2f, "on the floor");
            Assert.IsTrue(soldier.GetComponent<CapsuleCollider>().enabled, "his capsule is back");
            int phases = soldier.Moves + soldier.ShootPhases;
            yield return new WaitForSeconds(6f);
            Assert.Greater(soldier.Moves + soldier.ShootPhases, phases, "he fights again");
        }

        [UnityTest]
        public IEnumerator Throw_IntoAnother_BothFallLimp_ThenBothStandUp()
        {
            SoldierBrain thrown = Find("Soldier_A");
            SoldierBrain struck = soldiers.Where(s => s != thrown && s.GetComponent<EnemyRagdoll>() != null)
                .OrderBy(s => Vector3.Distance(s.transform.position, thrown.transform.position)).First();
            EnemyRagdoll a = thrown.GetComponent<EnemyRagdoll>(), b = struck.GetComponent<EnemyRagdoll>();
            yield return WaitUntil(() => thrown.Engaged, 6f);
            powers.Select(PowerKind.Telekinesis);
            powers.FireAt(powers.Selected, 1f, thrown.GetComponent<LockOnTargetable>(), 1f);
            yield return new WaitForSeconds(0.8f);
            TelekinesisLift lift = thrown.GetComponent<TelekinesisLift>();
            Health struckHealth = struck.GetComponent<Health>();
            Debug.Log($"[RagdollTests] throw {thrown.name} at {thrown.transform.position} into {struck.name} at {struck.transform.position}");
            Assert.IsTrue(lift.Launch(struck.GetComponent<LockOnTargetable>().GetLockPoint(), struckHealth), "thrown");
            yield return WaitUntil(() => lift == null || lift.LaunchVictim != null, 3f);
            Assert.IsTrue(b.Downs > 0, $"{struck.name} falls limp (state {b.Current})");
            Assert.IsTrue(a.Downs > 0, $"{thrown.name} falls limp (state {a.Current})");
            Assert.IsTrue(RagdollBodies.IsDown(struck), "the one it hit is down");
            yield return WaitUntil(() => a.Current == EnemyRagdoll.State.Animated && b.Current == EnemyRagdoll.State.Animated, 10f);
            Debug.Log($"[RagdollTests] after the throw: {thrown.name} {a.Current} hp={thrown.GetComponent<Health>().CurrentHealth:0}, {struck.name} {b.Current} hp={struckHealth.CurrentHealth:0}");
            Assert.AreEqual(EnemyRagdoll.State.Animated, a.Current, "the thrown one stands up");
            Assert.AreEqual(EnemyRagdoll.State.Animated, b.Current, "the struck one stands up");
        }

        [UnityTest]
        public IEnumerator FullBlast_KnocksHimDownFor2_5Seconds_APartCharge_DoesNot()
        {
            SoldierBrain soldier = Find("Soldier_A");
            EnemyRagdoll ragdoll = soldier.GetComponent<EnemyRagdoll>();
            yield return WaitUntil(() => soldier.Engaged, 6f);
            powers.Select(PowerKind.Blast);
            LockOnTargetable target = soldier.GetComponent<LockOnTargetable>();
            powers.FireAt(powers.Selected, 0.5f, target, 0.01f);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(EnemyRagdoll.State.Animated, ragdoll.Current, "a part charge only staggers");

            Vector3 start = soldier.transform.position;
            Vector3 away = Vector3.ProjectOnPlane(start - lucy.transform.position, Vector3.up).normalized;
            float t0 = Time.time;
            powers.FireAt(powers.Selected, 1f, target, 0.01f);
            yield return null;
            Assert.AreEqual(EnemyRagdoll.State.Down, ragdoll.Current, "a full circle knocks him down");
            yield return new WaitForSeconds(2.3f);
            Assert.IsTrue(ragdoll.Current == EnemyRagdoll.State.Down, $"still down at 2.3 s (state {ragdoll.Current})");
            Vector3 pushed = Vector3.ProjectOnPlane(soldier.transform.position - start, Vector3.up);
            Debug.Log($"[RagdollTests] pushed {pushed.magnitude:0.00} m, along her line {Vector3.Dot(pushed, away):0.00} m");
            Assert.Greater(Vector3.Dot(pushed, away), 0.3f, "pushed away from her");
            yield return WaitUntil(() => ragdoll.Current == EnemyRagdoll.State.StandingUp, 5f);
            float down = Time.time - t0;
            Debug.Log($"[RagdollTests] down {down:0.00} s, clip {ragdoll.StandUpClip}");
            Assert.GreaterOrEqual(down, 2.45f, "down for 2.5 s");
            yield return WaitUntil(() => ragdoll.Current == EnemyRagdoll.State.Animated, 5f);
            Assert.AreEqual(EnemyRagdoll.State.Animated, ragdoll.Current, "he stood up");
        }

        [UnityTest]
        public IEnumerator FullBlastKill_CutsALimb_TheBodyFallsLimp_AndAPoolGrows()
        {
            SoldierBrain soldier = Find("Soldier_A");
            EnemyRagdoll ragdoll = soldier.GetComponent<EnemyRagdoll>();
            Animator animator = soldier.GetComponent<Animator>();
            Transform forearm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            Transform hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            float size = forearm.TransformVector(Vector3.up).magnitude;
            int cuts = GruesomeDeathFx.LimbsCut, heads = GruesomeDeathFx.HeadsBurst, pools = BodyBloodPool.Count, pieces = SeveredLimb.Spawned;
            GruesomeDeathFx.ForcedLimb = HumanBodyBones.LeftLowerArm;
            yield return WaitUntil(() => soldier.Engaged, 6f);
            powers.Select(PowerKind.Blast);
            Vector3 middle = (forearm.position + hand.position) * 0.5f;
            Vector3 away = Vector3.ProjectOnPlane(soldier.transform.position - lucy.transform.position, Vector3.up).normalized;
            powers.FireAt(powers.Selected, 1f, soldier.GetComponent<LockOnTargetable>(), 100f);
            SeveredLimb piece = SeveredLimb.Last;
            Assert.AreEqual(pieces + 1, SeveredLimb.Spawned, "the forearm flies off");
            Assert.IsNotNull(piece);
            Debug.Log($"[RagdollTests] piece {piece.Triangles} tris, centre {piece.CutCentre} vs forearm middle {middle} ({Vector3.Distance(piece.CutCentre, middle):0.00} m)");
            Assert.Greater(piece.Triangles, 20, "the piece has the forearm's triangles");
            Assert.Less(Vector3.Distance(piece.CutCentre, middle), 0.25f, "the piece is where the forearm was");
            Vector3 cutAt = piece.transform.position;
            yield return null;
            yield return null;
            Assert.IsTrue(soldier.GetComponent<Health>().IsDead, "the blast killed him");
            Assert.AreEqual(cuts + 1, GruesomeDeathFx.LimbsCut, "a limb is cut");
            Assert.AreEqual(heads, GruesomeDeathFx.HeadsBurst, "the head burst is for the short");
            Assert.AreEqual("LeftLowerArm", GruesomeDeathFx.LastLimb);
            Assert.Less(forearm.TransformVector(Vector3.up).magnitude, size * 0.05f, "the forearm is gone");
            Assert.IsFalse(forearm.GetComponent<Collider>().enabled, "the cut limb has no collider");
            Assert.IsNotNull(forearm.parent.Find("LimbStump"), "the bone ends sit on the cut");
            Assert.IsTrue(soldier.GetComponent<EnemyMorale>().DiedGruesome, "a full mk_warspite kill is gruesome");
            Assert.IsTrue(ragdoll.IsRagdolled, "the corpse flies limp");
            yield return new WaitForSeconds(0.3f);
            Vector3 flew = Vector3.ProjectOnPlane(piece.transform.position - cutAt, Vector3.up);
            Debug.Log($"[RagdollTests] piece flew {flew.magnitude:0.00} m, along her line {Vector3.Dot(flew, away):0.00} m");
            Assert.Greater(Vector3.Dot(flew, away), 0.4f, "the piece flies away from her");
            yield return WaitUntil(() => piece.Settled, 8f);
            Assert.IsTrue(piece.Settled, "the piece lies still and stays");
            Assert.Less(piece.transform.position.y, 0.5f, "on the floor");
            yield return WaitUntil(() => ragdoll.Current == EnemyRagdoll.State.Corpse, 8f);
            Assert.AreEqual(EnemyRagdoll.State.Corpse, ragdoll.Current, "the body settles and stays");
            yield return WaitUntil(() => BodyBloodPool.Count > pools, 6f);
            Assert.Greater(BodyBloodPool.Count, pools, "blood pools under him");
            GameObject pool = BodyBloodPool.Pools[BodyBloodPool.Pools.Count - 1];
            Vector3 flat = Vector3.ProjectOnPlane(pool.transform.position - ragdoll.Hips.position, Vector3.up);
            Assert.Less(flat.magnitude, 0.6f, "the pool is under the hips");
            float early = pool.transform.localScale.x;
            yield return new WaitForSeconds(4f);
            Debug.Log($"[RagdollTests] pool {early:0.00} -> {pool.transform.localScale.x:0.00} m at {pool.transform.position}");
            Assert.Greater(pool.transform.localScale.x, early + 0.4f, "the pool grows");
        }

        [UnityTest]
        public IEnumerator PowerArmour_FullBlast_DoesNotKnockItDown()
        {
            SoldierBrain armour = Find("Armour_C");
            Assert.IsNull(armour.GetComponent<EnemyRagdoll>(), "a heavy has no ragdoll");
            powers.Select(PowerKind.Blast);
            powers.FireAt(powers.Selected, 1f, armour.GetComponent<LockOnTargetable>(), 0.01f);
            yield return new WaitForSeconds(0.3f);
            Assert.IsFalse(RagdollBodies.IsDown(armour), "it stays on its feet");
            Assert.IsFalse(armour.GetComponent<InvectorEnemyBody>().Suspended, "it is not suspended");
        }

        private SoldierBrain Find(string name)
        {
            SoldierBrain soldier = soldiers.FirstOrDefault(s => s.name == name);
            Assert.IsNotNull(soldier, name);
            return soldier;
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
