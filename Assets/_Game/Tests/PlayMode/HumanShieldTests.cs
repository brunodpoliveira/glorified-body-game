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
            Assert.AreSame(lab.GetComponent<CivilianBrain>(), HumanShield.Held(), "held low: a human shield");

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
            Debug.Log($"[HumanShieldTests] the face plant: the feet rose {ragdoll.FacePlantFlip:0.00} m over the head");
            Assert.Greater(ragdoll.FacePlantFlip, 0.3f, "head first: the feet go up over the head");
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
