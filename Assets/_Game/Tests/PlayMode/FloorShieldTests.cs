#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Automation;
using GlorifiedBody.Characters;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;
using GlorifiedBody.UI;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The floor shield (2026-10-31; design §2.3: "It rips chunks out of the floor as a shield"; Bruno, 2026-10-20:
    /// "RMB hold = the floor shield, the defensive full charge"), in the training room. Batch:
    /// tools/unity.sh test playmode FloorShieldTests.
    /// </summary>
    public class FloorShieldTests
    {
        [DefaultExecutionOrder(-1000)]
        private sealed class PadDriver : MonoBehaviour
        {
            public VirtualPad pad;
            private void Update() => pad?.Commit();
        }

        private VirtualPad pad;
        private PadDriver driver;
        private GameObject player;
        private TankController tank;
        private LockOnController lockOn;
        private Health health;
        private PsychicStats stats;
        private PsychicPowers powers;
        private LockOnTargetable dummyA;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            GameManager.Instance.LoadGameplay();
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "TrainingRoom" && GameManager.Instance.IsPlaying, 10f);
            yield return null;
            foreach (DummyShooter shooter in UnityEngine.Object.FindObjectsByType<DummyShooter>(FindObjectsSortMode.None)) shooter.enabled = false;
            player = GameObject.FindGameObjectWithTag("Player");
            tank = player.GetComponent<TankController>();
            lockOn = player.GetComponent<LockOnController>();
            health = player.GetComponentInChildren<Health>();
            stats = player.GetComponent<PsychicStats>();
            powers = player.GetComponent<PsychicPowers>();
            dummyA = GameObject.Find("Dummy_A").GetComponent<LockOnTargetable>();
            pad = new VirtualPad();
            driver = new GameObject("[PadDriver]").AddComponent<PadDriver>();
            driver.pad = pad;
            GameInput.SetBotSource(pad);
            tank.Teleport(Vector3.zero, 0f);
            yield return null;
            lockOn.SetTarget(dummyA);
            stats.DebugSet(100f, 0f);
            stats.RegenPaused = true;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            pad.ReleaseAll();
            GameInput.ClearBotSource(pad);
            if (driver != null) UnityEngine.Object.Destroy(driver.gameObject);
            if (FloorShield.Current != null) UnityEngine.Object.Destroy(FloorShield.Current.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HoldingDefend_WithOrzabal_TearsASlabOutOfTheFloor_InFrontOfHer()
        {
            powers.Select(PowerKind.Telekinesis);
            pad.Tap(InputManager.GameAction.Defend);
            yield return new WaitForSeconds(0.5f);
            Assert.IsNull(FloorShield.Current, "a tap is only the catch");
            Assert.AreEqual(100f, stats.Nalcon, 0.01f, "and costs nothing with no bullet near");

            pad.SetHeld(InputManager.GameAction.Defend, true);
            pad.SetHeld(InputManager.GameAction.Forward, true);
            yield return new WaitForSeconds(PsychicPowers.ShieldHoldDelay + 0.1f); // a tap's time: she still walks
            Vector3 from = player.transform.position;
            yield return new WaitForSeconds(0.6f);
            Assert.Greater(powers.ShownCharge, 0.1f, "the circle fills");
            Assert.Less(Vector3.Distance(from, player.transform.position), 0.2f, "she stands still while it fills");
            yield return WaitUntil(() => FloorShield.Current != null, 3f);
            pad.SetHeld(InputManager.GameAction.Forward, false);
            pad.SetHeld(InputManager.GameAction.Defend, false);

            FloorShield shield = FloorShield.Current;
            Assert.IsNotNull(shield, "a full circle tears out a slab");
            Assert.AreEqual("shield", powers.LastResult);
            Assert.AreEqual(100f - powers.Selected.nalconCost, stats.Nalcon, 0.5f, "the cost of a full circle");
            Assert.IsNotNull(shield.Scar, "a dark scar where it tore out");
            yield return new WaitForSeconds(0.6f);
            Vector3 toTarget = Vector3.ProjectOnPlane(dummyA.transform.position - player.transform.position, Vector3.up).normalized;
            Vector3 toShield = Vector3.ProjectOnPlane(shield.transform.position - player.transform.position, Vector3.up);
            Assert.AreEqual(FloorShield.Ahead, toShield.magnitude, 0.15f, "it hangs in front of her");
            Assert.Less(Vector3.Angle(toTarget, toShield), 15f, "between her and her target");
            Assert.AreEqual(FloorShield.HangHeight, shield.transform.position.y, 0.1f);
            Assert.Less(Vector3.Angle(shield.transform.up, Vector3.up), 5f, "it stands upright");

            // It turns with her.
            tank.Teleport(player.transform.position, player.transform.eulerAngles.y + 90f);
            yield return new WaitForSeconds(0.2f);
            Vector3 after = Vector3.ProjectOnPlane(shield.transform.position - player.transform.position, Vector3.up);
            Assert.Less(Vector3.Angle(player.transform.forward, after), 15f, "it swings around with her");
            Assert.Less(Vector3.Angle(player.transform.forward, shield.transform.forward), 15f, "and faces the way she faces");

            // One at a time.
            pad.SetHeld(InputManager.GameAction.Defend, true);
            yield return new WaitForSeconds(2.5f);
            pad.SetHeld(InputManager.GameAction.Defend, false);
            Assert.AreSame(shield, FloorShield.Current);
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<FloorShield>(FindObjectsSortMode.None).Length, "one shield");
        }

        [UnityTest]
        public IEnumerator Bullets_StopOnTheSlab_AndWearItDown_UntilItBreaks()
        {
            powers.Select(PowerKind.Telekinesis);
            FaceDummy();
            yield return null;
            Assert.IsTrue(powers.TryRaiseShield());
            FloorShield shield = FloorShield.Current;
            yield return new WaitForSeconds(0.6f);
            float hp = health.CurrentHealth;
            Vector3 her = player.transform.position + Vector3.up * Body.ChestHeight;
            Vector3 muzzle = dummyA.transform.position + Vector3.up * Body.MuzzleHeight;
            const float damage = 15f;
            int shots = 0;
            while (FloorShield.Current != null && shots < 10)
            {
                EnemyProjectile.Launch(muzzle, (her - muzzle).normalized, 30f, damage, dummyA.gameObject, "test_pistol");
                shots++;
                yield return new WaitForSeconds(0.4f);
            }
            Assert.AreEqual(hp, health.CurrentHealth, 0.01f, "no bullet reached her");
            Assert.AreEqual(Mathf.CeilToInt(FloorShield.MaxHp / damage), shots, "it held until its HP ran out");
            Assert.IsNull(FloorShield.Current, "then it shattered");
            Assert.IsTrue(shield == null);
            Assert.IsNull(powers.HeldLift, "nothing hangs any more");

            // The next one rises again.
            stats.DebugSet(100f, 0f);
            Assert.IsTrue(powers.TryRaiseShield(), "a new shield after the old one broke");
        }

        [UnityTest]
        public IEnumerator TheAttackKey_ThrowsTheSlab_AtTheTarget_AndItBreaks()
        {
            powers.Select(PowerKind.Telekinesis);
            Assert.IsTrue(powers.TryRaiseShield());
            yield return new WaitForSeconds(0.6f);
            Health target = dummyA.GetComponent<Health>();
            float hp0 = target.CurrentHealth;
            FaceDummy();
            yield return null;
            pad.Tap(InputManager.GameAction.Charge);
            yield return WaitUntil(() => FloorShield.Current == null, 3f);
            Assert.IsNull(FloorShield.Current, "a thrown slab breaks");
            StringAssert.StartsWith("launch_prop:", powers.LastResult, "the attack key throws it as a prop");
            yield return WaitUntil(() => target.CurrentHealth < hp0, 2f);
            Assert.Less(target.CurrentHealth, hp0, "it hurts the one it hits");
        }

        [UnityTest]
        public IEnumerator TheShieldCircle_ThenAnAttack_LeavesHerFeetFree()
        {
            // The seam sweep (2026-10-31): the shield's lock stayed on when the attack key cut its circle.
            powers.Select(PowerKind.Telekinesis);
            pad.SetHeld(InputManager.GameAction.Defend, true);
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(tank.MovementLocked, "the shield's circle holds her feet");
            pad.SetHeld(InputManager.GameAction.Charge, true);
            yield return new WaitForSeconds(0.2f);
            pad.SetHeld(InputManager.GameAction.Charge, false);
            pad.SetHeld(InputManager.GameAction.Defend, false);
            yield return new WaitForSeconds(0.3f);
            Assert.IsFalse(tank.MovementLocked, "her feet are free after both keys are up");
        }

        [UnityTest]
        public IEnumerator AFastBullet_StopsOnTheSlab_Too()
        {
            // The seam sweep (2026-10-31): a bullet checked for her body before the walls, so a long step jumped the slab.
            powers.Select(PowerKind.Telekinesis);
            FaceDummy();
            yield return null;
            Assert.IsTrue(powers.TryRaiseShield());
            yield return new WaitForSeconds(0.6f);
            float hp = health.CurrentHealth;
            Vector3 her = player.transform.position + Vector3.up * Body.ChestHeight;
            Vector3 muzzle = dummyA.transform.position + Vector3.up * Body.MuzzleHeight;
            EnemyProjectile.Launch(muzzle, (her - muzzle).normalized, 150f, 15f, dummyA.gameObject, "test_fast");
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(hp, health.CurrentHealth, 0.01f, "the fast bullet did not reach her");
            Assert.AreEqual(1, FloorShield.Current.Absorbed, "it stopped on the slab");
        }

        [UnityTest]
        public IEnumerator HoldingDefend_WithAnotherPower_DoesNothing()
        {
            powers.Select(PowerKind.Blast);
            pad.SetHeld(InputManager.GameAction.Defend, true);
            yield return new WaitForSeconds(2.6f);
            pad.SetHeld(InputManager.GameAction.Defend, false);
            Assert.IsNull(FloorShield.Current, "only mk_orzabal tears the floor");
            Assert.AreEqual(0f, powers.ShieldCharge);
            Assert.AreEqual(100f, stats.Nalcon, 0.01f);
        }

        private void FaceDummy()
        {
            Vector3 to = Vector3.ProjectOnPlane(dummyA.transform.position - player.transform.position, Vector3.up);
            tank.Teleport(player.transform.position, Quaternion.LookRotation(to).eulerAngles.y);
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
