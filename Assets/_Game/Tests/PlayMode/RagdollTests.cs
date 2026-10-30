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
            if (name.StartsWith("ragdoll") || name.StartsWith("tk_") || name.StartsWith("gore_") || name == "soldier_down" || name == "death_fall")
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
            SoldierBrain struck = soldiers.Where(s => s != thrown && s.GetComponent<EnemyRagdoll>() != null && !s.GetComponent<EnemyRagdoll>().DeathOnly)
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
            // The part charge's flinch (HitReactions, the vendor's big hit) ends first: the knockdown starts from a
            // standing body.
            yield return new WaitForSeconds(1.2f);

            Vector3 start = soldier.transform.position;
            Vector3 away = Vector3.ProjectOnPlane(start - lucy.transform.position, Vector3.up).normalized;
            float t0 = Time.time;
            powers.FireAt(powers.Selected, 1f, target, 0.01f);
            yield return null;
            Assert.AreEqual(EnemyRagdoll.State.Down, ragdoll.Current, "a full circle knocks him down");
            // The push itself (the stagger step of the part charge, 2026-10-27, can put a cover block behind him, so
            // the distance alone depends on the room).
            float pushSpeed = Vector3.Dot(ragdoll.Parts[0].linearVelocity, away);
            Debug.Log($"[RagdollTests] knockdown speed along her line {pushSpeed:0.00} m/s");
            Assert.Greater(pushSpeed, 2f, "the blast throws him away from her");
            yield return new WaitForSeconds(2.3f);
            Assert.IsTrue(ragdoll.Current == EnemyRagdoll.State.Down, $"still down at 2.3 s (state {ragdoll.Current})");
            Vector3 pushed = Vector3.ProjectOnPlane(soldier.transform.position - start, Vector3.up);
            Debug.Log($"[RagdollTests] pushed {pushed.magnitude:0.00} m, along her line {Vector3.Dot(pushed, away):0.00} m");
            Assert.Greater(Vector3.Dot(pushed, away), 0.15f, "pushed away from her");
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
            EnemyRagdoll armourRagdoll = armour.GetComponent<EnemyRagdoll>();
            Assert.IsNotNull(armourRagdoll, "a heavy has a ragdoll for its death (Bruno, 2026-10-27)");
            Assert.IsTrue(armourRagdoll.DeathOnly, "for its death only");
            Assert.IsFalse(armourRagdoll.KnockDown(2.5f, Vector3.up), "no knockdown of a heavy");
            powers.Select(PowerKind.Blast);
            powers.FireAt(powers.Selected, 1f, armour.GetComponent<LockOnTargetable>(), 0.01f);
            yield return new WaitForSeconds(0.3f);
            Assert.IsFalse(RagdollBodies.IsDown(armour), "it stays on its feet");
            Assert.IsFalse(armour.GetComponent<InvectorEnemyBody>().Suspended, "it is not suspended");
        }

        // ── Every death plays out physically (Bruno, 2026-10-27) ──────

        [UnityTest]
        public IEnumerator APartBlastKill_FallsLimp_TheWayTheHitPushed_NoDeathClip()
        {
            SoldierBrain soldier = Find("Soldier_A");
            EnemyRagdoll ragdoll = soldier.GetComponent<EnemyRagdoll>();
            Animator animator = soldier.GetComponent<Animator>();
            yield return null;
            Vector3 away = Flat(soldier.transform.position - lucy.transform.position).normalized;
            Vector3 hips0 = ragdoll.Hips.position;
            soldier.GetComponent<Health>().SetHealth(1f);
            powers.Select(PowerKind.Blast);
            powers.FireAt(powers.Selected, 0.5f, soldier.GetComponent<LockOnTargetable>());
            yield return WaitUntil(() => soldier.GetComponent<Health>().IsDead, 1f);
            yield return null;
            Debug.Log($"[RagdollTests] part blast kill: result {powers.LastResult}, hp {soldier.GetComponent<Health>().CurrentHealth}");
            Assert.IsTrue(soldier.GetComponent<Health>().IsDead, "the blast killed him");
            Assert.IsTrue(ragdoll.DiedFalling, "the death goes limp from the living pose");
            Assert.AreEqual(EnemyRagdoll.DeathKind.Plain, ragdoll.LastDeath);
            Assert.IsTrue(ragdoll.IsRagdolled, "a ragdoll, not a death clip");
            Assert.IsFalse(animator.enabled, "the animator is off");
            Assert.IsTrue(ragdoll.InThroes, "the muscles hold him for a moment");
            yield return new WaitForSeconds(0.6f);
            Vector3 moved = Flat(ragdoll.Hips.position - hips0);
            Debug.Log($"[RagdollTests] part blast death: hips moved {moved.magnitude:0.00} m, along the push {Vector3.Dot(moved, away):0.00} m, height {ragdoll.Hips.position.y - hips0.y:0.00} m, bone {ragdoll.LastDeathBone}");
            Assert.Greater(Vector3.Dot(moved, away), 0.05f, "he falls away from her");
            Assert.Less(ragdoll.Hips.position.y, hips0.y - 0.15f, "the knees buckle: the hips drop");
            yield return WaitUntil(() => ragdoll.Current == EnemyRagdoll.State.Corpse, 9f);
            Assert.AreEqual(EnemyRagdoll.State.Corpse, ragdoll.Current, "the body settles and freezes");
            Assert.Less(ragdoll.Hips.position.y, hips0.y - 0.5f, "he lies on the floor");
        }

        [UnityTest]
        public IEnumerator AFullBlastKill_ThrowsTheBodyFar()
        {
            SoldierBrain soldier = Find("Soldier_A");
            EnemyRagdoll ragdoll = soldier.GetComponent<EnemyRagdoll>();
            yield return null;
            Vector3 away = Flat(soldier.transform.position - lucy.transform.position).normalized;
            Vector3 hips0 = ragdoll.Hips.position;
            soldier.GetComponent<Health>().SetHealth(1f);
            powers.Select(PowerKind.Blast);
            powers.FireAt(powers.Selected, 1f, soldier.GetComponent<LockOnTargetable>());
            yield return null;
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(ragdoll.DiedFalling);
            Assert.AreEqual(EnemyRagdoll.DeathKind.Blast, ragdoll.LastDeath, "a full mk_warspite kill is a blast");
            // The speed right after the death: a cover block behind him can stop the body (the flight was 0.9 to 1.6 m
            // over five runs in the same room), the push itself does not depend on the room.
            float speed = Vector3.Dot(ragdoll.Parts[0].linearVelocity, away);
            yield return new WaitForSeconds(1.2f);
            float along = Vector3.Dot(Flat(ragdoll.Hips.position - hips0), away);
            Debug.Log($"[RagdollTests] full blast death: {speed:0.0} m/s at the start, {along:0.00} m along the push");
            Assert.Greater(speed, 3f, "the blast throws the body away from her");
            Assert.Greater(along, 0.5f, "the body flies away from her");
        }

        [UnityTest]
        public IEnumerator AShockKill_Convulses_AFireKill_Writhes_ThenTheyLieStill()
        {
            SoldierBrain shocked = Find("Soldier_A");
            SoldierBrain burnt = soldiers.First(s => s != shocked && !s.GetComponent<EnemyRagdoll>().DeathOnly && s.GetComponent<EnemyShield>() == null);
            EnemyRagdoll a = shocked.GetComponent<EnemyRagdoll>(), b = burnt.GetComponent<EnemyRagdoll>();
            yield return null;
            KillWith(shocked, DamageType.ShortShock);
            KillWith(burnt, DamageType.PsychicFire);
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(EnemyRagdoll.DeathKind.Shock, a.LastDeath);
            Assert.AreEqual(EnemyRagdoll.DeathKind.Burn, b.LastDeath);
            Assert.IsTrue(a.InThroes && b.InThroes, "both in their throes");
            float spin = a.Parts.Where(p => p != null).Max(p => p.angularVelocity.magnitude);
            Debug.Log($"[RagdollTests] shock: the fastest part turns at {spin:0.0} rad/s");
            Assert.Greater(spin, 3f, "the shock makes the limbs jerk");
            yield return new WaitForSeconds(2f);
            Assert.IsFalse(a.InThroes, "the convulsion ends after 1.6 s");
            Assert.IsTrue(b.InThroes, "the writhe goes on (3.2 s)");
            yield return WaitUntil(() => a.Current == EnemyRagdoll.State.Corpse && b.Current == EnemyRagdoll.State.Corpse, 10f);
            Assert.AreEqual(EnemyRagdoll.State.Corpse, a.Current, "the shocked body lies still");
            Assert.AreEqual(EnemyRagdoll.State.Corpse, b.Current, "the burnt body lies still");
        }

        [UnityTest]
        public IEnumerator ThePowerArmour_DiesAsARagdoll()
        {
            SoldierBrain armour = Find("Armour_C");
            EnemyRagdoll ragdoll = armour.GetComponent<EnemyRagdoll>();
            yield return null;
            float hips0 = ragdoll.Hips.position.y;
            KillWith(armour, DamageType.PsychicBlast);
            yield return null;
            yield return null;
            Assert.IsTrue(ragdoll.DiedFalling, "its death plays out physically");
            yield return WaitUntil(() => ragdoll.Current == EnemyRagdoll.State.Corpse, 10f);
            Assert.AreEqual(EnemyRagdoll.State.Corpse, ragdoll.Current);
            Assert.Less(ragdoll.Hips.position.y, hips0 - 0.6f, "the 2.6 m armour lies on the floor");
        }

        [UnityTest]
        // FOR LATER (Bruno, 2026-10-30: "later, annotate it"): the 220 kg power armour sometimes bends a knee 25 to 58
        // degrees backward in its fire death, so this test fails in about half the runs (devlog-notes/2026-10-30_drugs_feeding_ward.md).
        public IEnumerator TheKnees_BendOnlyLikeKnees_InEveryDeath()
        {
            // Bruno, 2026-10-27: "the power armor and the shock death has this uncanny knee/leg rotation; the lower leg
            // bends inwards in a very unnatural way". A knee is a hinge: the shin may turn about the knee's axis, and
            // only a few degrees about the other two.
            SoldierBrain shocked = Find("Soldier_A");
            SoldierBrain burnt = soldiers.First(s => s != shocked && !s.GetComponent<EnemyRagdoll>().DeathOnly && s.GetComponent<EnemyShield>() == null);
            SoldierBrain armour = Find("Armour_C");
            SoldierBrain plain = Find("Guard_B");
            var figures = new[] { shocked, burnt, armour, plain };
            yield return null;
            KillWith(shocked, DamageType.ShortShock);
            KillWith(burnt, DamageType.PsychicFire);
            KillWith(armour, DamageType.PsychicFire);
            KillWith(plain, DamageType.EnemyProjectile);
            yield return null;
            yield return null;
            var knees = figures.SelectMany(f => new[] { Knee.Of(f, true), Knee.Of(f, false) }).ToArray();
            float t0 = Time.time;
            while (Time.time - t0 < 4f)
            {
                foreach (Knee k in knees) k.Sample();
                yield return null;
            }
            foreach (Knee k in knees)
                Debug.Log($"[RagdollTests] knee {k.Name}: off the hinge {k.MaxOff:0.0} deg, out of the plane {k.MaxPlane:0.0} deg, bent back {k.MaxBack:0.0} deg, bent {k.MaxTwist:0.0} deg; off/plane/bend each 0.25 s:{k.Trace}");
            foreach (Knee k in knees)
            {
                // Before the fix (the joint's twist sign): knees 66 to 137 degrees bent the wrong way, 16 to 135 off.
                Assert.Less(k.MaxBack, 10f, $"{k.Name}: the knee bends the wrong way");
                Assert.Less(k.MaxPlane, 40f, $"{k.Name}: the shin leaves the knee's plane");
                Assert.Less(k.Plane, 25f, $"{k.Name}: the body lies with the shin out of the knee's plane");
            }
        }

        /// <summary>One knee: the shin's turn against the thigh since the death, split into the hinge turn and the rest.</summary>
        private sealed class Knee
        {
            public string Name;
            public float MaxOff, MaxBack, MaxPlane, MaxTwist, Plane;
            public string Trace = "";
            private Transform foot;
            private float nextTrace;
            private Transform thigh, shin;
            private Quaternion rest;
            private Vector3 hinge, bend;

            public static Knee Of(SoldierBrain f, bool left)
            {
                Animator a = f.GetComponent<Animator>();
                Transform thigh = a.GetBoneTransform(left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
                Transform shin = a.GetBoneTransform(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
                Transform foot = a.GetBoneTransform(left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
                CharacterJoint joint = shin.GetComponent<CharacterJoint>();
                Assert.IsNotNull(joint, $"{f.name}: a knee joint");
                // The hinge in the thigh's frame, its sign so that a positive turn moves the foot back (a knee bend).
                Vector3 axis = shin.TransformDirection(joint.axis).normalized;
                Vector3 along = (foot.position - shin.position).normalized;
                Vector3 back = -f.transform.forward;
                if (Vector3.Dot(Quaternion.AngleAxis(10f, axis) * along - along, back) < 0f) axis = -axis;
                return new Knee
                {
                    Name = $"{f.name}.{(left ? "L" : "R")}",
                    thigh = thigh, shin = shin, foot = foot,
                    rest = Quaternion.Inverse(thigh.rotation) * shin.rotation,
                    hinge = thigh.InverseTransformDirection(axis),
                };
            }

            public void Sample()
            {
                Quaternion now = Quaternion.Inverse(thigh.rotation) * shin.rotation;
                Quaternion delta = now * Quaternion.Inverse(rest);
                // Swing-twist split about the hinge.
                Vector3 r = new Vector3(delta.x, delta.y, delta.z);
                Vector3 p = Vector3.Project(r, hinge);
                Quaternion twist = new Quaternion(p.x, p.y, p.z, delta.w);
                float n = Mathf.Sqrt(twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w);
                twist = n < 1e-6f ? Quaternion.identity : new Quaternion(twist.x / n, twist.y / n, twist.z / n, twist.w / n);
                Quaternion swing = delta * Quaternion.Inverse(twist);
                float off = Quaternion.Angle(Quaternion.identity, swing);
                twist.ToAngleAxis(out float angle, out Vector3 ax);
                if (angle > 180f) angle -= 360f;
                float signed = Vector3.Dot(ax, hinge) >= 0f ? angle : -angle;
                MaxOff = Mathf.Max(MaxOff, off);
                MaxBack = Mathf.Max(MaxBack, -signed);
                MaxTwist = Mathf.Max(MaxTwist, signed);
                Vector3 v = (foot.position - shin.position).normalized;
                float plane = Mathf.Asin(Mathf.Clamp(Mathf.Abs(Vector3.Dot(v, thigh.TransformDirection(hinge))), 0f, 1f)) * Mathf.Rad2Deg;
                MaxPlane = Mathf.Max(MaxPlane, plane);
                Plane = plane;
                if (Time.time >= nextTrace) { nextTrace = Time.time + 0.25f; Trace += $" {off:0}/{plane:0}/{signed:0}"; }
            }
        }

        private void KillWith(SoldierBrain figure, DamageType type)
        {
            Health h = figure.GetComponent<Health>();
            h.TakeDamage(h.MaxHealth * 10f);
            HitEventBus.Raise(new HitInfo { Attacker = lucy, Victim = figure.gameObject, HitTransform = figure.transform, HitPoint = figure.transform.position + Vector3.up * 1.2f, HitNormal = -figure.transform.forward, DamageAmount = 999f, DamageType = type, WasFatal = true });
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

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
