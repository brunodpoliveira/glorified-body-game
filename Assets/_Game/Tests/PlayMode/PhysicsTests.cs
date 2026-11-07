#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Automation;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Saving;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The carry of mk_orzabal (Bruno, 2026-11-06: hold the defend key to select and lift, the mouse moves it, release to drop) and the
    /// physics puzzle, in the PHYSICS TEST room (PhysicsTestSetup.BuildPhysicsTest). Batch: tools/unity.sh test playmode PhysicsTests.
    /// </summary>
    public class PhysicsTests
    {
        private const string Room = "PhysicsTest";

        private sealed class PadDriver : MonoBehaviour
        {
            public VirtualPad pad;
            private void Update() => pad?.Commit();
        }

        private VirtualPad pad;
        private PadDriver driver;
        private GameObject lucy;
        private PsychicStats stats;
        private PsychicPowers powers;
        private TankController tank;
        private GameObject boxA, boxB;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            StoryFlags.Set(new string[0]);
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            GameManager.Instance.LoadLevel(Room);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == Room && GameManager.Instance.IsPlaying, 15f);
            yield return null;
            Assert.AreEqual(Room, SceneManager.GetActiveScene().name, "PhysicsTestSetup.BuildPhysicsTest");
            lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            stats = lucy.GetComponent<PsychicStats>();
            powers = lucy.GetComponent<PsychicPowers>();
            tank = lucy.GetComponent<TankController>();
            boxA = GameObject.Find("Box_A");
            boxB = GameObject.Find("Box_B");
            pad = new VirtualPad();
            driver = new GameObject("[PadDriver]").AddComponent<PadDriver>();
            driver.pad = pad;
            GameInput.SetBotSource(pad);
            powers.Select(PowerKind.Telekinesis);
            yield return new WaitForSeconds(0.5f); // the boxes settle
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            pad.ReleaseAll();
            pad.Look = Vector2.zero;
            pad.Scroll = 0f;
            GameInput.ClearBotSource(pad);
            if (driver != null) UnityEngine.Object.Destroy(driver.gameObject);
            yield return null;
        }

        private void Face(GameObject target)
        {
            Vector3 to = target.transform.position - lucy.transform.position;
            to.y = 0f;
            tank.Teleport(lucy.transform.position, Quaternion.LookRotation(to).eulerAngles.y);
        }

        private IEnumerator Lift(GameObject box)
        {
            Face(box);
            yield return null;
            pad.SetHeld(InputManager.GameAction.Defend, true);
            yield return WaitUntil(() => powers.Carrying, 2f);
            Assert.IsTrue(powers.Carrying, "the held key lifts " + box.name);
            Assert.AreEqual(box, powers.HeldLift.gameObject, "the one in front of her");
            yield return new WaitForSeconds(PsychicPowers.CarryRise + 0.1f);
        }

        /// <summary>Holds the mouse until the box is near the point, seen from above.</summary>
        private IEnumerator Steer(GameObject box, Vector3 point, float seconds)
        {
            Camera cam = Camera.main;
            float t0 = Time.time;
            while (Time.time - t0 < seconds)
            {
                Vector3 to = point - box.transform.position;
                to.y = 0f;
                if (to.magnitude < 0.08f) break;
                Vector3 right = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized, away = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
                pad.Look = new Vector2(Mathf.Clamp(Vector3.Dot(to, right) * 3f, -1f, 1f), Mathf.Clamp(Vector3.Dot(to, away) * 3f, -1f, 1f));
                yield return null;
            }
            pad.Look = Vector2.zero;
        }

        private IEnumerator Raise(float seconds)
        {
            pad.Scroll = 0.05f;
            yield return new WaitForSeconds(seconds);
            pad.Scroll = 0f;
        }

        [UnityTest]
        public IEnumerator TheHeldKey_LiftsTheBox_TheMouseMovesIt_TheReleaseDropsIt_ForTheCostOfALift()
        {
            stats.DebugSet(100f, 0f);
            float floorY = boxA.transform.position.y;
            yield return Lift(boxA);
            Assert.Greater(boxA.transform.position.y, floorY + 0.4f, "it rises");
            Assert.IsTrue(tank.MovementLocked, "she stands while she holds it");
            float heading = lucy.transform.eulerAngles.y;

            Vector3 at = boxA.transform.position;
            Camera cam = Camera.main;
            Vector3 right = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;
            pad.Look = new Vector2(1f, 0f);
            yield return new WaitForSeconds(0.4f);
            pad.Look = Vector2.zero;
            Assert.Greater(Vector3.Dot(boxA.transform.position - at, right), 0.3f, "mouse right: it goes right on the screen");

            at = boxA.transform.position;
            Vector3 away = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            pad.Look = new Vector2(0f, -1f);
            yield return new WaitForSeconds(0.3f);
            pad.Look = Vector2.zero;
            Assert.Less(Vector3.Dot(boxA.transform.position - at, away), -0.2f, "mouse down: it comes toward the camera");

            float height = boxA.transform.position.y;
            yield return Raise(0.3f);
            yield return new WaitForSeconds(0.3f);
            Assert.Greater(boxA.transform.position.y, height + 0.3f, "the wheel raises it");

            pad.SetHeld(InputManager.GameAction.Defend, false);
            yield return new WaitForSeconds(1.5f);
            Assert.IsFalse(powers.Carrying);
            Assert.AreEqual(floorY, boxA.transform.position.y, 0.05f, "it falls to the floor");
            Assert.IsNotNull(boxA, "a drop does not break it");
            Assert.IsFalse(tank.MovementLocked, "her feet are free");
            Assert.AreEqual(100f - PsychicPowers.LiftCost, stats.Mk(PowerKind.Telekinesis), 0.01f, "a lift costs the same on anything");
        }

        [UnityTest]
        public IEnumerator AWall_StopsTheBox_TheBoxStaysInTheRoom()
        {
            yield return Lift(boxA);
            pad.Look = new Vector2(-1f, 0f);
            yield return new WaitForSeconds(3f);
            pad.Look = Vector2.zero;
            Assert.Greater(boxA.transform.position.x, -PhysicsWest, "the west wall stops it");
            pad.SetHeld(InputManager.GameAction.Defend, false);
            yield return null;
        }

        private const float PhysicsWest = 8f; // the half width of the room

        [UnityTest]
        public IEnumerator TheBox_OverTheRail_OntoThePlate_OpensTheGate_AndOffItShutsIt()
        {
            const string flag = "physics_test_plate";
            PressurePlate plate = UnityEngine.Object.FindAnyObjectByType<PressurePlate>();
            GameObject gate = GameObject.Find("Gate_Leaf");
            Assert.IsNotNull(gate);
            Assert.IsTrue(gate.activeInHierarchy, "the gate is shut");
            yield return Lift(boxA);
            Vector3 over = plate.transform.position;

            // Without the height the rail stops it.
            yield return Steer(boxA, over, 2.5f);
            Assert.Less(boxA.transform.position.z, 1.5f, "the rail stops a low box");

            yield return Raise(0.5f);
            yield return new WaitForSeconds(0.2f);
            yield return Steer(boxA, over, 4f);
            Assert.Less(Vector3.ProjectOnPlane(boxA.transform.position - over, Vector3.up).magnitude, 0.3f, "over the plate");
            yield return new WaitForFixedUpdate();
            Assert.IsFalse(plate.Pressed, "in the air it presses nothing");
            pad.SetHeld(InputManager.GameAction.Defend, false);
            yield return WaitUntil(() => plate.Pressed, 3f);
            Assert.IsTrue(plate.Pressed, $"the box lies on the plate: it lies at {boxA.transform.position}, the plate at {over}");
            Assert.IsTrue(StoryFlags.Has(flag));
            yield return null;
            Assert.IsFalse(gate.activeInHierarchy, "the gate opens");
            Debug.Log($"[PhysicsTests] the box lies at {boxA.transform.position}; the plate at {over}");

            // She lifts it again: the plate rises and the gate shuts.
            pad.ReleaseAll();
            yield return null;
            yield return Lift(boxA);
            yield return WaitUntil(() => !plate.Pressed, 2f);
            Assert.IsFalse(plate.Pressed, "she lifted it");
            yield return null;
            Assert.IsTrue(gate.activeInHierarchy, "the gate shuts");
            pad.SetHeld(InputManager.GameAction.Defend, false);
        }

        [UnityTest]
        public IEnumerator ABox_DroppedOnABox_StaysOnIt()
        {
            yield return Lift(boxA);
            Vector3 onto = boxB.transform.position;
            yield return Raise(0.5f);
            yield return new WaitForSeconds(0.3f);
            yield return Steer(boxA, onto, 3f);
            pad.SetHeld(InputManager.GameAction.Defend, false);
            yield return new WaitForSeconds(1.5f);
            float edge = boxB.GetComponent<Collider>().bounds.size.y;
            Debug.Log($"[PhysicsTests] stack: A at {boxA.transform.position}, B at {boxB.transform.position}");
            Assert.AreEqual(boxB.transform.position.y + edge, boxA.transform.position.y, 0.1f, "it rests on the other box");
        }

        [UnityTest]
        public IEnumerator AThrow_FromTheCarry_CostsByItsCharge_AndWithNoMkTheBoxStaysHeld()
        {
            stats.DebugSet(100f, 0f);
            yield return Lift(boxA);
            stats.DebugSetMk(PowerKind.Telekinesis, 5f);
            Assert.IsFalse(powers.TryLaunch(0.5f), "no MK");
            Assert.AreEqual("no_nalcon", powers.LastResult);
            Assert.IsTrue(powers.Carrying, "she still holds it");
            stats.DebugSet(100f, 0f);
            Assert.IsTrue(powers.TryLaunch(0.5f));
            Assert.AreEqual(100f - powers.Selected.nalconCost * 0.5f, stats.Nalcon, 0.01f, "half a charge");
            yield return new WaitForSeconds(2f);
            Assert.IsTrue(boxA == null, "a thrown prop breaks");
            Assert.IsFalse(tank.MovementLocked, "her feet are free");
            pad.SetHeld(InputManager.GameAction.Defend, false);
        }

        [UnityTest]
        public IEnumerator SwitchingThePower_DropsTheBox()
        {
            yield return Lift(boxA);
            powers.Select(PowerKind.Blast);
            yield return new WaitForSeconds(1.5f);
            Assert.IsFalse(powers.Carrying);
            Assert.IsNotNull(boxA);
            Assert.IsFalse(tank.MovementLocked);
            pad.SetHeld(InputManager.GameAction.Defend, false);
        }

        /// <summary>The lift by a press: she locks the box and it rises at once. Her feet stay free, and she holds it with no key down.</summary>
        private IEnumerator PressLift(GameObject box)
        {
            Face(box);
            yield return null;
            lucy.GetComponent<LockOnController>().SetTarget(box.GetComponent<LockOnTargetable>());
            pad.Tap(InputManager.GameAction.Charge);
            yield return new WaitForSeconds(powers.Selected.liftSeconds + 0.1f);
            Assert.AreEqual(box, powers.HeldLift != null ? powers.HeldLift.gameObject : null, "she holds " + box.name);
        }

        [UnityTest]
        public IEnumerator TheDefendPress_DropsALiftedBox_Whole_AndItStaysOnAnotherBox()
        {
            // Bruno, 2026-11-07: "right clicking should cancel the lift and drop the object gently on the ground".
            stats.DebugSet(100f, 0f);
            yield return PressLift(boxA);
            Vector3 onto = boxB.transform.position;
            yield return Steer(boxA, onto, 3f);
            float mk = stats.Mk(PowerKind.Telekinesis);

            pad.Tap(InputManager.GameAction.Defend);
            yield return null;
            yield return null;
            Assert.IsNull(powers.HeldLift, "the press lets it go");
            float fastest = 0f;
            Rigidbody body = boxA.GetComponent<Rigidbody>();
            float t0 = Time.time;
            while (Time.time - t0 < 1.5f)
            {
                fastest = Mathf.Max(fastest, body.linearVelocity.magnitude);
                yield return null;
            }
            float edge = boxB.GetComponent<Collider>().bounds.size.y;
            Debug.Log($"[PhysicsTests] the drop: fastest {fastest:0.00} m/s; A at {boxA.transform.position}, B at {boxB.transform.position}");
            Assert.IsTrue(boxA != null, "a drop does not break it");
            Assert.AreEqual(boxB.transform.position.y + edge, boxA.transform.position.y, 0.1f, "it rests on the other box");
            Assert.Less(Vector3.Distance(onto, boxB.transform.position), 0.1f, "the other box stays where it was");
            Assert.AreEqual(mk, stats.Mk(PowerKind.Telekinesis), 0.01f, "a drop costs no MK");
        }

        [UnityTest]
        public IEnumerator TheDefendPress_FromTheTopHeight_KeepsTheStack()
        {
            stats.DebugSet(100f, 0f);
            yield return PressLift(boxA);
            Vector3 onto = boxB.transform.position;
            yield return Steer(boxA, onto, 3f);
            yield return Raise(1.5f);
            yield return new WaitForSeconds(0.5f);
            yield return Steer(boxA, onto, 1f);
            float height = boxA.transform.position.y;
            Assert.Greater(height, TelekinesisLift.MaxHeight - 0.2f, "the wheel took it to the top");

            pad.Tap(InputManager.GameAction.Defend);
            yield return new WaitForSeconds(2f);
            float edge = boxB.GetComponent<Collider>().bounds.size.y;
            Debug.Log($"[PhysicsTests] from {height:0.00} m: A at {boxA.transform.position}, B at {boxB.transform.position}");
            Assert.IsTrue(boxA != null, "a drop does not break it");
            Assert.AreEqual(boxB.transform.position.y + edge, boxA.transform.position.y, 0.1f, "it rests on the other box");
            Assert.Less(Vector3.Distance(onto, boxB.transform.position), 0.1f, "the other box stays where it was");
        }

        [UnityTest]
        public IEnumerator TheDefendPress_DuringACharge_OnlyCancels_AndTheKeyHeldLiftsNothingNew()
        {
            stats.DebugSet(100f, 0f);
            yield return PressLift(boxA);
            pad.SetHeld(InputManager.GameAction.Charge, true);
            yield return WaitUntil(() => powers.Charge > 0.1f, 2f);
            pad.Tap(InputManager.GameAction.Defend);
            yield return null;
            yield return null;
            pad.SetHeld(InputManager.GameAction.Charge, false);
            yield return null;
            Assert.AreEqual("cancel", powers.LastResult, "the press ends the charge");
            Assert.IsNotNull(powers.HeldLift, "and she still holds it");

            // The next press drops it. The key stays down: it starts no carry and no shield until it rises.
            pad.SetHeld(InputManager.GameAction.Defend, true);
            yield return new WaitForSeconds(PsychicPowers.ShieldHoldDelay + 0.3f);
            Assert.IsNull(powers.HeldLift, "the press lets it go");
            Assert.IsFalse(powers.Carrying, "the same key lifts nothing new");
            Assert.AreEqual(0f, powers.ShieldCharge, 0.001f, "and fills no circle");
            pad.SetHeld(InputManager.GameAction.Defend, false);
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
