#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Automation;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;
using GlorifiedBody.Player;
using GlorifiedBody.Saving;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// Round 162 (CANON): the human shield (a guard or a soldier does not fire through a live civilian she holds low) and
    /// the face plant of a full slam (head first, the legs over). In the KIT TEST room. Batch: tools/unity.sh test playmode HumanShieldTests.
    /// </summary>
    public class HumanShieldTests
    {
        private const string Room = "KitTest";
        private GameObject lucy;
        private PsychicPowers powers;
        private PsychicStats stats;
        private LockOnController lockOn;
        private VirtualPad pad;
        private PadDriver driver;

        [DefaultExecutionOrder(-1000)]
        private sealed class PadDriver : MonoBehaviour
        {
            public VirtualPad pad;
            private void Update() => pad?.Commit();
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SaveSystem.Folder = Path.Combine(Application.temporaryCachePath, "gb_shield_tests");
            if (Directory.Exists(SaveSystem.Folder)) Directory.Delete(SaveSystem.Folder, true);
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            GameManager.Instance.LoadLevel(Room);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == Room && GameManager.Instance.IsPlaying, 10f);
            yield return null;
            lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            powers = lucy.GetComponent<PsychicPowers>();
            stats = lucy.GetComponent<PsychicStats>();
            lockOn = lucy.GetComponent<LockOnController>();
            pad = new VirtualPad();
            driver = new GameObject("[Pad]").AddComponent<PadDriver>();
            driver.pad = pad;
            GameInput.SetBotSource(pad);
            Bark.ResetVoices();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            pad.ReleaseAll();
            GameInput.ClearBotSource(pad);
            if (driver != null) UnityEngine.Object.Destroy(driver.gameObject);
            SaveSystem.Folder = null;
            yield return null;
        }

        /// <summary>Lifts the figure with a press of the attack key (mk_orzabal lifts at once).</summary>
        private IEnumerator Lift(GameObject figure)
        {
            stats.FillMk(PowerKind.Telekinesis);
            powers.Select(PowerKind.Telekinesis);
            Vector3 to = figure.transform.position - lucy.transform.position;
            lucy.GetComponent<TankController>().Teleport(lucy.transform.position, Quaternion.LookRotation(new Vector3(to.x, 0f, to.z)).eulerAngles.y);
            lockOn.SetTarget(figure.GetComponentInChildren<LockOnTargetable>());
            yield return null;
            pad.SetHeld(InputManager.GameAction.Charge, true);
            yield return WaitUntil(() => powers.HeldLift != null, 2f);
            pad.SetHeld(InputManager.GameAction.Charge, false);
            Assert.IsNotNull(powers.HeldLift, "she lifts " + figure.name);
            yield return new WaitForSeconds(0.6f);
        }

        [UnityTest]
        public IEnumerator ASoldier_DoesNotFireThroughACivilianSheHoldsLow_ButFiresWhenSheRaisesIt()
        {
            GameObject lab = GameObject.Find("Lab_K4");
            lucy.GetComponent<TankController>().Teleport(lab.transform.position + new Vector3(-2.2f, 0f, 0f), 90f); // west of the civilian: the soldier stands east of it, in the room
            yield return null;
            yield return Lift(lab);
            TelekinesisLift held = powers.HeldLift;
            Assert.Greater(held.HeightOverFloor, HumanShield.LowHeight, "the lift starts high");
            Assert.IsNull(HumanShield.Held(), "held high: no shield");
            for (int i = 0; i < 20 && held.HeightOverFloor > 0.4f; i++) { held.SteerBy(Vector3.zero, -0.5f, 10f); yield return null; }
            yield return new WaitForSeconds(0.6f);
            Debug.Log($"[HumanShieldTests] the civilian hangs {held.HeightOverFloor:0.00} m over the floor");
            Assert.AreSame(lab, HumanShield.Held(), "held low: a human shield");

            // The soldier on the far side of the civilian, his brain off: only his gun runs, on the line.
            GameObject soldier = GameObject.Find("Soldier_K2");
            soldier.GetComponent<SoldierBrain>().enabled = false;
            NavMeshAgent agent = soldier.GetComponent<NavMeshAgent>();
            Vector3 away = Vector3.ProjectOnPlane(lab.transform.position - lucy.transform.position, Vector3.up).normalized;
            Vector3 spot = lab.transform.position + away * 4.5f;
            Assert.IsTrue(NavMesh.SamplePosition(spot, out NavMeshHit onMesh, 1.5f, NavMesh.AllAreas), "a place behind the civilian");
            if (agent != null) { agent.Warp(onMesh.position); agent.isStopped = true; }
            else soldier.transform.position = onMesh.position;
            soldier.transform.rotation = Quaternion.LookRotation(-away);
            soldier.GetComponent<SoldierBrain>().Body.Suspend(true); // the FSM stops too: an alert cannot move him off the line
            DummyShooter gun = soldier.GetComponent<DummyShooter>();
            gun.RequireLineOfSight = false;
            gun.HoldFire = false;
            int shots = gun.ShotsFired;
            yield return WaitUntil(() => gun.HeldForShield >= 2, 12f);
            Debug.Log($"[HumanShieldTests] the soldier: held {gun.HeldForShield}, fired {gun.ShotsFired - shots}, his line '{soldier.GetComponent<Bark>()?.Shown}'");
            Assert.GreaterOrEqual(gun.HeldForShield, 2, "he holds his fire, again and again");
            Assert.AreEqual(shots, gun.ShotsFired, "no shot through the civilian");
            Assert.GreaterOrEqual(Bark.CountOf(soldier), 1, "he says why (civilians present)");
            BarkVoice voice = BarkVoice.Of(soldier);
            string said = soldier.GetComponent<Bark>().Shown;
            if (!string.IsNullOrEmpty(said))
            {
                BarkRow row = BarkBook.RowsFor("soldier", voice.Personality, BarkSituation.CiviliansPresent).FirstOrDefault(r => r.line == said);
                Assert.IsNotNull(row, "a civilians present line of his personality");
                Assert.IsTrue(row.civ == "" || row.civ == "man", "a line for a man or for anyone, never a CIV: woman or group line: " + row.civ);
            }

            // She raises the civilian: he fires.
            for (int i = 0; i < 20 && held.HeightOverFloor < 1.6f; i++) { held.SteerBy(Vector3.zero, 0.5f, 10f); yield return null; }
            yield return new WaitForSeconds(0.4f);
            Assert.IsNull(HumanShield.Held(), "held high: no shield");
            yield return WaitUntil(() => gun.ShotsFired > shots, 12f);
            Assert.Greater(gun.ShotsFired, shots, "he fires when the civilian is no longer low");
        }

        [UnityTest]
        public IEnumerator WithTheirBrainsOn_NoShotGoesThroughTheHostage()
        {
            // Bruno, 2026-11-15: "still firing through the hostage" (KIT TEST, the lab worker held low). The fight as he
            // plays it: every brain on, she holds the lab worker low on the line to the soldiers. A shot whose line passes
            // through the held civilian at the moment it leaves the gun is the fault.
            GameObject lab = GameObject.Find("Lab_K4");
            GameObject k1 = GameObject.Find("Soldier_K1"), k2 = GameObject.Find("Soldier_K2"), k3 = GameObject.Find("Guard_K3");
            Vector3 fighters = (k1.transform.position + k2.transform.position) * 0.5f;
            Vector3 away = Vector3.ProjectOnPlane(lab.transform.position - fighters, Vector3.up).normalized;
            Vector3 stand = lab.transform.position + away * 1.6f;
            Assert.IsTrue(NavMesh.SamplePosition(stand, out NavMeshHit onMesh, 1.5f, NavMesh.AllAreas));
            lucy.GetComponent<TankController>().Teleport(onMesh.position, Quaternion.LookRotation(-away).eulerAngles.y);
            yield return null;
            yield return Lift(lab);
            TelekinesisLift held = powers.HeldLift;
            for (int i = 0; i < 20 && held.HeightOverFloor > 0.4f; i++) { held.SteerBy(Vector3.zero, -0.5f, 10f); yield return null; }
            int through = 0, shots = 0, heldFire = 0, between = 0;
            var lines = new System.Collections.Generic.List<string>();
            foreach (GameObject f in new[] { k1, k2, k3 })
            {
                DummyShooter gun = f.GetComponent<DummyShooter>();
                string who = f.name;
                gun.OnFired += (origin, dir) =>
                {
                    shots++;
                    Vector3 chest = lucy.transform.position + Vector3.up * 1.2f;
                    GameObject hostage = HumanShield.Held();
                    bool blocks = HumanShield.Covers(origin, chest, lab);
                    if (blocks) through++;
                    // Top down: how far the hostage's middle stands from the shot's line, and where along it (0 the gun, 1 her).
                    Vector2 a2 = new Vector2(origin.x, origin.z), b2 = new Vector2(chest.x, chest.z);
                    Bounds body = lab.GetComponentInChildren<Renderer>().bounds;
                    foreach (Renderer r in lab.GetComponentsInChildren<Renderer>()) body.Encapsulate(r.bounds);
                    Vector2 h2 = new Vector2(body.center.x, body.center.z);
                    float along = Vector2.Dot(h2 - a2, b2 - a2) / Mathf.Max(0.0001f, (b2 - a2).sqrMagnitude);
                    float side = Vector2.Distance(h2, a2 + Mathf.Clamp01(along) * (b2 - a2));
                    if (side <= HumanShield.Zone && along > 0f && along <= 1.05f) between++; // the rule itself, measured apart
                    float lineY = Mathf.Lerp(origin.y, chest.y, Mathf.Clamp01(along));
                    lines.Add($"{who} {gun.CurrentAttack} held={(hostage != null)} h={held.HeightOverFloor:0.00} blocks={blocks} side={side:0.00} along={along:0.00} lineY={lineY:0.00} body top={body.max.y:0.00}");
                };
            }
            foreach (SoldierBrain b in UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None)) b.Alert("test", false);
            float t0 = Time.time;
            while (Time.time - t0 < 20f && powers.HeldLift == held)
            {
                if (held.HeightOverFloor > 0.4f) held.SteerBy(Vector3.zero, -0.5f, 10f);
                yield return null;
            }
            foreach (GameObject f in new[] { k1, k2, k3 }) heldFire += f.GetComponent<DummyShooter>().HeldForShield;
            Debug.Log($"[HumanShieldTests] the fight: {shots} shots, {through} past the hostage (the rule), {between} within 1 m of the line top down, {heldFire} held, she held it {Time.time - t0:0.0} s\n" + string.Join("\n", lines));
            Assert.AreEqual(0, through, "no shot past the hostage within 1 m of the line (Bruno, 2026-11-15)");
        }

        [UnityTest]
        public IEnumerator AHeldGuard_IsAShieldToo()
        {
            // Bruno, 2026-11-15, after he held Guard_K3 and a soldier fired past him: "any live person", within 1 m of the line.
            GameObject guard = GameObject.Find("Guard_K3");
            lucy.GetComponent<TankController>().Teleport(guard.transform.position + new Vector3(-2.2f, 0f, 0f), 90f);
            yield return null;
            yield return Lift(guard);
            TelekinesisLift held = powers.HeldLift;
            for (int i = 0; i < 20 && held.HeightOverFloor > 0.4f; i++) { held.SteerBy(Vector3.zero, -0.5f, 10f); yield return null; }
            yield return new WaitForSeconds(0.6f);
            Assert.AreSame(guard, HumanShield.Held(), "a held guard, low: a shield");
            GameObject soldier = GameObject.Find("Soldier_K2");
            soldier.GetComponent<SoldierBrain>().enabled = false;
            Vector3 away = Vector3.ProjectOnPlane(HumanShield.Middle(guard) - lucy.transform.position, Vector3.up).normalized;
            // The soldier 1.6 m off the straight line: the line passes about 0.5 m beside the guard, inside the 1 m zone, outside the body.
            Vector3 side = Vector3.Cross(Vector3.up, away) * 0.8f;
            Assert.IsTrue(NavMesh.SamplePosition(HumanShield.Middle(guard) + away * 4.5f + side * 2f, out NavMeshHit onMesh, 1.5f, NavMesh.AllAreas));
            soldier.GetComponent<NavMeshAgent>().Warp(onMesh.position);
            soldier.transform.rotation = Quaternion.LookRotation(-away);
            soldier.GetComponent<SoldierBrain>().Body.Suspend(true);
            DummyShooter gun = soldier.GetComponent<DummyShooter>();
            gun.RequireLineOfSight = false;
            gun.HoldFire = false;
            int shots = gun.ShotsFired;
            yield return WaitUntil(() => gun.HeldForShield >= 2, 12f);
            Debug.Log($"[HumanShieldTests] the held guard: held {gun.HeldForShield}, fired {gun.ShotsFired - shots}, still held low {HumanShield.Held() == guard} at {held.HeightOverFloor:0.00} m, his middle {HumanShield.Middle(guard)}");
            Assert.GreaterOrEqual(gun.HeldForShield, 2, "he holds his fire for a held colleague");
            Assert.AreEqual(shots, gun.ShotsFired, "no shot past him");
        }

        [UnityTest]
        public IEnumerator AGuardOnHisFeet_InTheLine_StopsTheSoldiersFire()
        {
            // Bruno, 2026-11-15: "soldier always fire through guard, regardless of being lifted up or not". A guard who stands
            // between the soldier and her, not lifted: the soldier holds his fire.
            GameObject guard = GameObject.Find("Guard_K3");
            GameObject soldier = GameObject.Find("Soldier_K2");
            foreach (GameObject f in new[] { guard, soldier }) { f.GetComponent<SoldierBrain>().enabled = false; }
            Vector3 her = lucy.transform.position;
            Vector3 toSoldier = Vector3.ProjectOnPlane(soldier.transform.position - her, Vector3.up).normalized;
            Assert.IsTrue(NavMesh.SamplePosition(her + toSoldier * 1.2f, out NavMeshHit g, 1.0f, NavMesh.AllAreas));
            Assert.IsTrue(NavMesh.SamplePosition(her + toSoldier * 6f, out NavMeshHit s2, 1.5f, NavMesh.AllAreas));
            guard.GetComponent<NavMeshAgent>().Warp(g.position);
            soldier.GetComponent<NavMeshAgent>().Warp(s2.position);
            soldier.transform.rotation = Quaternion.LookRotation(-toSoldier);
            foreach (GameObject f in new[] { guard, soldier }) f.GetComponent<SoldierBrain>().Body.Suspend(true);
            guard.GetComponent<DummyShooter>().HoldFire = true; // the guard does not shoot her in this test
            yield return new WaitForSeconds(0.5f);
            DummyShooter gun = soldier.GetComponent<DummyShooter>();
            gun.RequireLineOfSight = false;
            gun.HoldFire = false;
            int shots = gun.ShotsFired;
            yield return WaitUntil(() => gun.HeldForShield >= 2, 12f);
            (float low, float high) = HumanShield.Span(guard);
            Debug.Log($"[HumanShieldTests] the standing guard: held {gun.HeldForShield}, fired {gun.ShotsFired - shots}, his body {low:0.00} to {high:0.00} m");
            Assert.GreaterOrEqual(gun.HeldForShield, 2, "he holds his fire for a colleague in his line");
            Assert.AreEqual(shots, gun.ShotsFired, "no shot past him");
            // The guard steps out of the line: the soldier fires.
            Vector3 aside = Vector3.Cross(Vector3.up, toSoldier) * 3f;
            Assert.IsTrue(NavMesh.SamplePosition(g.position + aside, out NavMeshHit out1, 1.5f, NavMesh.AllAreas));
            guard.GetComponent<NavMeshAgent>().enabled = true;
            guard.GetComponent<NavMeshAgent>().Warp(out1.position);
            guard.GetComponent<NavMeshAgent>().enabled = false;
            yield return WaitUntil(() => gun.ShotsFired > shots, 12f);
            Assert.Greater(gun.ShotsFired, shots, "a clear line: he fires");
        }

        [UnityTest]
        public IEnumerator AFullSlam_LandsTheTarget_HeadFirst_TheLegsOver()
        {
            GameObject soldier = GameObject.Find("Soldier_K1");
            soldier.GetComponent<Health>().SetInvulnerable(true); // the slam's damage does not kill him: the fall is the test
            lucy.GetComponent<TankController>().Teleport(soldier.transform.position + new Vector3(0f, 0f, -3f), 0f);
            yield return null;
            yield return Lift(soldier);
            EnemyRagdoll ragdoll = soldier.GetComponent<EnemyRagdoll>();
            Assert.IsNotNull(ragdoll, "the soldier has a ragdoll");
            stats.FillMk(PowerKind.Telekinesis);
            Assert.IsTrue(powers.TrySlam(), "the full slam");
            yield return new WaitForSeconds(1.6f);
            Debug.Log($"[HumanShieldTests] the face plant: the feet rose {ragdoll.FacePlantFlip:0.00} m over the head, first on the floor: {ragdoll.FacePlantFirst}");
            Assert.Greater(ragdoll.FacePlantFlip, 1.0f, "upside down: the feet high over the head (Bruno, 2026-11-15: \"not dramatic enough\")");
            Assert.AreEqual("head", ragdoll.FacePlantFirst, "the head hits the floor first");
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
