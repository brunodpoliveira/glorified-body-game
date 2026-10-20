#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Automation;
using GlorifiedBody.Cameras;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// Build order step 2 in the training room: tank controls, the dodge, the fixed cameras, the lock-on.
    /// The tests drive Lucy through the virtual pad (the input bot seam). Batch: tools/unity.sh test playmode.
    /// </summary>
    public class TankControlTests
    {
        private const string MainMenu = "Main_Menu";
        private const string Room = "TrainingRoom";

        /// <summary>Commits the pad before every player script reads it.</summary>
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

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(MainMenu);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == MainMenu && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            GameManager.Instance.LoadGameplay();
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == Room && GameManager.Instance.IsPlaying, 10f);
            yield return null;

            player = GameObject.FindGameObjectWithTag("Player");
            Assert.IsNotNull(player, "player");
            tank = player.GetComponent<TankController>();
            lockOn = player.GetComponent<LockOnController>();
            health = player.GetComponentInChildren<Health>();
            Assert.IsNotNull(tank, "TankController");
            Assert.IsNotNull(lockOn, "LockOnController");
            Assert.IsNotNull(health, "Health");

            pad = new VirtualPad();
            driver = new GameObject("[PadDriver]").AddComponent<PadDriver>();
            driver.pad = pad;
            GameInput.SetBotSource(pad);
            tank.Teleport(new Vector3(0f, 1f, 0f), 0f);
            // The first frames after a scene load hitch (shader compiles, imports). Let the game clock settle,
            // then measure in scaled time: the controller integrates Time.deltaTime, not the wall clock.
            yield return Settle();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (pad != null)
            {
                pad.ReleaseAll();
                GameInput.ClearBotSource(pad);
            }
            if (driver != null) UnityEngine.Object.Destroy(driver.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Forward_MovesAlongTheFacing_NotTheCamera()
        {
            Vector3 start = player.transform.position;
            Vector3 forward = player.transform.forward;
            pad.SetHeld(InputManager.GameAction.Forward, true);
            yield return WaitReal(1f);
            pad.ReleaseAll();
            yield return null;
            Vector3 delta = player.transform.position - start;
            float along = Vector3.Dot(delta, forward);
            float across = Vector3.ProjectOnPlane(delta, forward).magnitude;
            Assert.GreaterOrEqual(along, 0.8f, "walks at least 0.8 m along the facing in 1 s");
            Assert.LessOrEqual(along, 2.2f, "walks no faster than the walk speed");
            Assert.Less(across, 0.15f, "no drift across the facing");
        }

        [UnityTest]
        public IEnumerator Back_MovesBackward_Slower()
        {
            Vector3 start = player.transform.position;
            Vector3 forward = player.transform.forward;
            pad.SetHeld(InputManager.GameAction.Back, true);
            yield return WaitReal(1f);
            pad.ReleaseAll();
            yield return null;
            float along = Vector3.Dot(player.transform.position - start, forward);
            Assert.Less(along, -0.5f, "moves backward");
            Assert.Greater(along, -1.5f, "slower than the forward walk");
        }

        [UnityTest]
        public IEnumerator TurnRight_ChangesYaw_AtTheTurnRate()
        {
            float yaw0 = tank.Yaw;
            pad.SetHeld(InputManager.GameAction.TurnRight, true);
            yield return WaitReal(0.5f);
            pad.ReleaseAll();
            yield return null;
            float turned = Mathf.DeltaAngle(yaw0, tank.Yaw);
            Assert.Greater(turned, 45f, "turns right (positive yaw)");
            Assert.Less(turned, 110f, "not faster than the turn rate");
        }

        [UnityTest]
        public IEnumerator Run_IsFasterThanWalk()
        {
            Vector3 start = player.transform.position;
            Vector3 forward = player.transform.forward;
            pad.SetHeld(InputManager.GameAction.Forward, true);
            pad.SetHeld(InputManager.GameAction.Run, true);
            yield return WaitReal(0.6f);
            pad.ReleaseAll();
            yield return null;
            float ran = Vector3.Dot(player.transform.position - start, forward);
            Assert.Greater(ran, 0.6f * tank.WalkSpeed * 1.3f, $"run covers more than 1.3x the walk in 0.6 s ({ran:0.00} m)");
        }

        [UnityTest]
        public IEnumerator Wall_StopsTheWalk()
        {
            tank.Teleport(new Vector3(0f, 1f, 3.5f), 0f);
            yield return Settle();
            pad.SetHeld(InputManager.GameAction.Forward, true);
            pad.SetHeld(InputManager.GameAction.Run, true);
            yield return WaitReal(1.5f);
            pad.ReleaseAll();
            yield return null;
            Assert.Less(player.transform.position.z, 4.75f, "the north wall holds her inside the room");
            Assert.Greater(player.transform.position.z, 4.0f, "she walked up to the wall");
        }

        [UnityTest]
        public IEnumerator Dodge_DashesBack_GrantsInvulnerability_ThenCoolsDown()
        {
            Vector3 start = player.transform.position;
            Vector3 forward = player.transform.forward;
            pad.Tap(InputManager.GameAction.Dodge);
            yield return null;
            yield return null;
            Assert.IsTrue(tank.IsDodging, "dodging after the tap");
            Assert.IsTrue(health.IsInvulnerable, "invulnerable during the dodge");

            yield return WaitReal(0.6f);
            float along = Vector3.Dot(player.transform.position - start, forward);
            Assert.Less(along, -1f, "the default dodge is a step back");
            Assert.IsFalse(tank.IsDodging, "the dodge ended");
            Assert.IsFalse(health.IsInvulnerable, "the invulnerability ended");

            Vector3 again = player.transform.position;
            pad.Tap(InputManager.GameAction.Dodge);
            yield return null;
            yield return null;
            Assert.IsFalse(tank.IsDodging, "a second dodge inside the cooldown is refused");
            Assert.Less(Vector3.Distance(again, player.transform.position), 0.05f);
        }

        [UnityTest]
        public IEnumerator CameraZone_CutsToTheZoneCamera_AndBack()
        {
            FixedCameraDirector director = FixedCameraDirector.Instance;
            Assert.IsNotNull(director, "FixedCameraDirector");
            Assert.AreEqual("Cam_Wide", director.CurrentName, "the wide camera at the start");

            tank.Teleport(new Vector3(3f, 1f, 3f), 180f);
            yield return WaitUntil(() => director.CurrentName == "Cam_NorthEast", 1.5f);
            Assert.AreEqual("Cam_NorthEast", director.CurrentName, "the north-east zone cuts to its camera");
            Assert.Less(Vector3.Distance(Camera.main.transform.position, director.Current.transform.position), 0.01f, "Camera.main took the pose");

            tank.Teleport(new Vector3(0f, 1f, 0f), 0f);
            yield return WaitUntil(() => director.CurrentName == "Cam_Wide", 1.5f);
            Assert.AreEqual("Cam_Wide", director.CurrentName, "leaving the zone returns the default camera");
        }

        [UnityTest]
        public IEnumerator LockOn_TakesTheNearest_SwitchesOnTab_DropsOnDeath()
        {
            yield return WaitUntil(() => lockOn.HasTarget, 1.5f);
            Assert.IsTrue(lockOn.HasTarget, "auto-lock found a target");
            Assert.AreEqual("Dummy_A", lockOn.TargetName, "the nearest dummy first");

            pad.Tap(InputManager.GameAction.SwitchTarget);
            yield return null;
            yield return null;
            Assert.AreEqual("Dummy_B", lockOn.TargetName, "Tab switches to the other dummy");

            Health dummyHealth = lockOn.CurrentTarget.GetComponent<Health>();
            dummyHealth.TakeDamage(9999f);
            yield return WaitUntil(() => lockOn.TargetName == "Dummy_A", 1.5f);
            Assert.AreEqual("Dummy_A", lockOn.TargetName, "a dead target drops and the next nearest locks");
        }

        [UnityTest]
        public IEnumerator Charge_StopsTheWalk_AndFacesTheTarget()
        {
            yield return WaitUntil(() => lockOn.HasTarget, 1.5f);
            Vector3 start = player.transform.position;
            pad.SetHeld(InputManager.GameAction.Charge, true);
            pad.SetHeld(InputManager.GameAction.Forward, true);
            yield return WaitReal(0.6f);
            Assert.IsTrue(lockOn.IsCharging, "charging while the button is held");
            Assert.Less(Vector3.Distance(start, player.transform.position), 0.05f, "she stands still while she charges");
            Vector3 to = lockOn.CurrentTarget.GetLockPoint() - player.transform.position;
            to.y = 0f;
            Assert.Less(Vector3.Angle(player.transform.forward, to), 8f, "she faces the target");

            pad.ReleaseAll();
            yield return null;
            yield return null;
            Assert.IsFalse(lockOn.IsCharging, "the charge ends on release");
            Assert.IsFalse(tank.MovementLocked, "movement is free again");
        }

        // ── Helpers ───────────────────────────────────────────────────

        /// <summary>Game-clock wait: the distance a controller covers follows this clock, not the wall clock.</summary>
        private static IEnumerator WaitReal(float seconds)
        {
            yield return new WaitForSeconds(seconds);
        }

        private static IEnumerator Settle()
        {
            yield return new WaitForSecondsRealtime(0.5f);
            yield return new WaitForSeconds(0.2f);
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds)
            {
                yield return null;
            }
        }
    }
}
#endif
