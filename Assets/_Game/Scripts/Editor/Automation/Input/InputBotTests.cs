#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using GlorifiedBody.Automation;
using GlorifiedBody.Systems;
using GameAction = GlorifiedBody.Systems.InputManager.GameAction;

namespace GlorifiedBody.Editor.Automation
{
    /// <summary>
    /// EditMode tests for the input seam (GameInput + VirtualPad) and the input bot's steering math.
    /// Run: tools/unity.sh test editmode InputBotTests
    /// </summary>
    public class InputBotTests
    {
        private VirtualPad pad;

        [SetUp]
        public void SetUp()
        {
            pad = new VirtualPad();
            GameInput.ClearBotSource(GameInput.BotSource);
        }

        [TearDown]
        public void TearDown()
        {
            GameInput.ClearBotSource(GameInput.BotSource);
        }

        // ── Seam ──────────────────────────────────────────────────────

        [Test]
        public void NoBot_ReadsHardware()
        {
            Assert.IsFalse(GameInput.HasBot);
            pad.Tap(GameAction.Charge);
            pad.Move = new Vector2(1f, 1f);
            pad.Commit(); // a pad that is not registered must not leak into the seam

            Assert.AreEqual(Input.GetKeyDown(KeyCode.Mouse0), GameInput.GetActionDown(GameAction.Charge, KeyCode.Mouse0));
            Assert.AreEqual(Input.GetKey(KeyCode.Mouse0), GameInput.GetAction(GameAction.Charge, KeyCode.Mouse0));
            Assert.AreEqual(Input.GetKeyUp(KeyCode.Mouse0), GameInput.GetActionUp(GameAction.Charge, KeyCode.Mouse0));
            Assert.AreEqual(Input.GetKey(KeyCode.W), GameInput.GetKey(KeyCode.W));
            Assert.AreEqual(Input.GetAxisRaw(GameInput.AxisMoveX), GameInput.GetAxisRaw(GameInput.AxisMoveX));
            Assert.AreEqual(Input.GetAxis(GameInput.AxisLookX), GameInput.GetAxis(GameInput.AxisLookX));
        }

        [Test]
        public void Bot_OverridesActionsAndAxes()
        {
            GameInput.SetBotSource(pad);
            pad.Tap(GameAction.Charge);
            pad.Move = new Vector2(0.5f, -1f);
            pad.Look = new Vector2(2f, -3f);
            pad.Commit();

            Assert.IsTrue(GameInput.HasBot);
            Assert.IsTrue(GameInput.GetActionDown(GameAction.Charge, KeyCode.Mouse0));
            Assert.IsTrue(GameInput.GetAction(GameAction.Charge, KeyCode.Mouse0));
            Assert.IsFalse(GameInput.GetActionDown(GameAction.Dodge, KeyCode.LeftShift));
            Assert.AreEqual(0.5f, GameInput.GetAxisRaw(GameInput.AxisMoveX), 1e-5f);
            Assert.AreEqual(-1f, GameInput.GetAxisRaw(GameInput.AxisMoveY), 1e-5f);
            Assert.AreEqual(2f, GameInput.GetAxis(GameInput.AxisLookX), 1e-5f);
            Assert.AreEqual(-3f, GameInput.GetAxis(GameInput.AxisLookY), 1e-5f);
            Assert.AreEqual(0f, GameInput.GetAxis("Unknown Axis"), 1e-5f);
        }

        [Test]
        public void Bot_RawKeysReadAsNotPressed()
        {
            GameInput.SetBotSource(pad);
            Assert.IsFalse(GameInput.GetKey(KeyCode.W));
            Assert.IsFalse(GameInput.GetKeyDown(KeyCode.F3));
        }

        [Test]
        public void Tap_IsDownForOneFrameThenUp()
        {
            GameInput.SetBotSource(pad);
            pad.Tap(GameAction.SwitchTarget);
            pad.Commit();
            Assert.IsTrue(GameInput.GetActionDown(GameAction.SwitchTarget, KeyCode.Tab));
            Assert.IsFalse(GameInput.GetActionUp(GameAction.SwitchTarget, KeyCode.Tab));

            pad.Commit();
            Assert.IsFalse(GameInput.GetActionDown(GameAction.SwitchTarget, KeyCode.Tab));
            Assert.IsFalse(GameInput.GetAction(GameAction.SwitchTarget, KeyCode.Tab));
            Assert.IsTrue(GameInput.GetActionUp(GameAction.SwitchTarget, KeyCode.Tab));

            pad.Commit();
            Assert.IsFalse(GameInput.GetActionUp(GameAction.SwitchTarget, KeyCode.Tab));
        }

        [Test]
        public void Held_StaysHeldUntilReleased()
        {
            pad.SetHeld(GameAction.PowerTelekinesis, true);
            pad.Commit();
            Assert.IsTrue(pad.GetActionDown(GameAction.PowerTelekinesis));
            pad.Commit();
            Assert.IsFalse(pad.GetActionDown(GameAction.PowerTelekinesis));
            Assert.IsTrue(pad.GetAction(GameAction.PowerTelekinesis));
            pad.SetHeld(GameAction.PowerTelekinesis, false);
            pad.Commit();
            Assert.IsTrue(pad.GetActionUp(GameAction.PowerTelekinesis));
            Assert.IsFalse(pad.GetAction(GameAction.PowerTelekinesis));
        }

        [Test]
        public void ClearBotSource_OnlyRemovesTheSameSource()
        {
            var other = new VirtualPad();
            GameInput.SetBotSource(pad);
            GameInput.ClearBotSource(other);
            Assert.AreSame(pad, GameInput.BotSource);
            GameInput.ClearBotSource(pad);
            Assert.IsFalse(GameInput.HasBot);
        }

        [Test]
        public void Bot_ActionsBlockedWhilePausedOrGameOver()
        {
            // Same rule as InputManager: no actions while paused or game over. Axes still pass.
            GameManager previous = GameManager.Instance;
            var go = new GameObject("GameManager (test)");
            GameManager gm = go.AddComponent<GameManager>(); // Awake does not run in EditMode
            PropertyInfo instance = typeof(GameManager).GetProperty(nameof(GameManager.Instance), BindingFlags.Public | BindingFlags.Static);
            FieldInfo state = typeof(GameManager).GetField("currentState", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(instance);
            Assert.IsNotNull(state, "GameManager.currentState field not found");
            try
            {
                instance.SetValue(null, gm);
                GameInput.SetBotSource(pad);
                pad.SetHeld(GameAction.Charge, true);
                pad.Move = new Vector2(0f, 1f);
                pad.Commit();

                state.SetValue(gm, GameManager.GameState.Playing);
                Assert.IsTrue(GameInput.GetActionDown(GameAction.Charge, KeyCode.Mouse0));
                Assert.IsTrue(GameInput.GetAction(GameAction.Charge, KeyCode.Mouse0));

                state.SetValue(gm, GameManager.GameState.Paused);
                Assert.IsFalse(GameInput.GetActionDown(GameAction.Charge, KeyCode.Mouse0), "paused");
                Assert.IsFalse(GameInput.GetAction(GameAction.Charge, KeyCode.Mouse0), "paused");

                state.SetValue(gm, GameManager.GameState.GameOver);
                Assert.IsFalse(GameInput.GetAction(GameAction.Charge, KeyCode.Mouse0), "game over");
                Assert.AreEqual(1f, GameInput.GetAxisRaw(GameInput.AxisMoveY), 1e-5f, "axes are not blocked");
            }
            finally
            {
                instance.SetValue(null, previous);
                Object.DestroyImmediate(go);
            }
        }

        // ── Steering math ─────────────────────────────────────────────

        [Test]
        public void MoveAxes_AreCameraRelative()
        {
            AssertAxes(new Vector2(0f, 1f), BotSteering.MoveAxesFor(Vector3.forward, Vector3.forward));
            AssertAxes(new Vector2(1f, 0f), BotSteering.MoveAxesFor(Vector3.right, Vector3.forward));
            AssertAxes(new Vector2(0f, 1f), BotSteering.MoveAxesFor(Vector3.right, Vector3.right));
            AssertAxes(new Vector2(-1f, 0f), BotSteering.MoveAxesFor(Vector3.right, Vector3.back));
            AssertAxes(new Vector2(0f, -1f), BotSteering.MoveAxesFor(Vector3.back, new Vector3(0f, -0.5f, 1f)));
            AssertAxes(new Vector2(0f, 0.5f), BotSteering.MoveAxesFor(Vector3.forward * 0.5f, Vector3.forward));
            AssertAxes(Vector2.zero, BotSteering.MoveAxesFor(Vector3.up, Vector3.forward));
        }

        [Test]
        public void MoveAxes_RoundTripThroughCameraBasis()
        {
            // The locomotion maps axes back with forward * y + right * x. That must give the input direction.
            Vector3 camForward = Quaternion.Euler(0f, 37f, 0f) * Vector3.forward;
            Vector3 camRight = Quaternion.Euler(0f, 37f, 0f) * Vector3.right;
            Vector3 wanted = new Vector3(-0.6f, 0f, 0.8f);
            Vector2 axes = BotSteering.MoveAxesFor(wanted, camForward);
            Vector3 result = (camForward * axes.y + camRight * axes.x).normalized;
            Assert.Less(Vector3.Angle(wanted, result), 0.1f);
        }

        [Test]
        public void YawTo_UsesUnityYaw()
        {
            Assert.AreEqual(0f, BotSteering.YawTo(Vector3.zero, Vector3.forward), 1e-3f);
            Assert.AreEqual(90f, BotSteering.YawTo(Vector3.zero, Vector3.right), 1e-3f);
            Assert.AreEqual(-90f, BotSteering.YawTo(Vector3.zero, Vector3.left), 1e-3f);
        }

        [Test]
        public void LookDelta_TurnsTowardTargetAndCaps()
        {
            float d = BotSteering.LookDelta(350f, 10f, 0.1f, 5f, 360f, 1f);
            Assert.Greater(d, 0f, "short way round is +20 degrees");
            Assert.AreEqual(10f, d, 1e-3f); // 20 * clamp01(5 * 0.1)

            float capped = BotSteering.LookDelta(0f, 170f, 0.1f, 10f, 100f, 1f);
            Assert.AreEqual(10f, capped, 1e-3f); // max 100 deg/s * 0.1 s

            float scaled = BotSteering.LookDelta(0f, -20f, 0.1f, 5f, 360f, 2f);
            Assert.AreEqual(-5f, scaled, 1e-3f); // divided by sensitivity
        }

        [Test]
        public void AdvanceWaypoint_SkipsReachedCornersButNotTheLast()
        {
            var corners = new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 5f), new Vector3(5f, 0f, 5f) };
            Assert.AreEqual(1, BotSteering.AdvanceWaypoint(corners, 3, 1, new Vector3(0f, 0f, 1f), 0.8f));
            Assert.AreEqual(2, BotSteering.AdvanceWaypoint(corners, 3, 1, new Vector3(0f, 3f, 4.6f), 0.8f));
            Assert.AreEqual(2, BotSteering.AdvanceWaypoint(corners, 3, 2, new Vector3(5f, 0f, 5f), 0.8f));
            Assert.AreEqual(0, BotSteering.AdvanceWaypoint(corners, 0, 3, Vector3.zero, 0.8f));
        }

        [Test]
        public void StuckDetector_FlagsNoProgress()
        {
            var detector = new BotSteering.StuckDetector(1f, 0.5f);
            Assert.IsFalse(detector.Sample(Vector3.zero, 0f));
            Assert.IsFalse(detector.Sample(new Vector3(0.1f, 0f, 0f), 0.5f));
            Assert.IsTrue(detector.Sample(new Vector3(0.2f, 0f, 0f), 1.1f));
            Assert.IsFalse(detector.Sample(new Vector3(3f, 0f, 0f), 2.2f));
        }

        [Test]
        public void TimeDilationStep_SlowsForCrowdAndRecovers()
        {
            Assert.AreEqual(1, BotSteering.TimeDilationStep(3, 0, 1f, 3, 3));
            Assert.AreEqual(0, BotSteering.TimeDilationStep(3, 0, 0.2f, 3, 3), "low resource: do not start");
            Assert.AreEqual(0, BotSteering.TimeDilationStep(3, 3, 1f, 3, 3), "already at max level");
            Assert.AreEqual(-1, BotSteering.TimeDilationStep(1, 2, 1f, 3, 3), "crowd gone");
            Assert.AreEqual(-1, BotSteering.TimeDilationStep(4, 2, 0.1f, 3, 3), "resource low");
            Assert.AreEqual(0, BotSteering.TimeDilationStep(2, 0, 1f, 3, 3));
        }

        [Test]
        public void TimeDilationStep_GoesDeepForIncomingProjectile()
        {
            Assert.AreEqual(1, BotSteering.TimeDilationStep(0, 0, 1f, 3, 3, true), "no crowd, but a projectile comes");
            Assert.AreEqual(1, BotSteering.TimeDilationStep(1, 2, 0.5f, 3, 3, true), "keep going down, do not speed up");
            Assert.AreEqual(0, BotSteering.TimeDilationStep(0, 3, 1f, 3, 3, true), "hold the deepest slow");
            Assert.AreEqual(-1, BotSteering.TimeDilationStep(0, 3, 0.1f, 3, 3, true), "resource low: give up");
            Assert.AreEqual(-1, BotSteering.TimeDilationStep(0, 3, 1f, 3, 3, false), "threat gone");
        }

        [Test]
        public void ClosestApproach_PredictsPassBeforeItHappens()
        {
            // 10 m away, 30 m/s, passes 1 m to the side: closest after 1/3 s.
            float t = BotSteering.ClosestApproach(new Vector3(1f, 0f, 10f), new Vector3(0f, 0f, -30f), 1f, out float d);
            Assert.AreEqual(1f / 3f, t, 1e-4f);
            Assert.AreEqual(1f, d, 1e-4f);

            // Flying away: closest is now.
            t = BotSteering.ClosestApproach(new Vector3(0f, 0f, 5f), new Vector3(0f, 0f, 10f), 1f, out d);
            Assert.AreEqual(0f, t, 1e-4f);
            Assert.AreEqual(5f, d, 1e-4f);

            // Pass is after the horizon: distance at the horizon.
            t = BotSteering.ClosestApproach(new Vector3(0f, 0f, 20f), new Vector3(0f, 0f, -10f), 1f, out d);
            Assert.AreEqual(1f, t, 1e-4f);
            Assert.AreEqual(10f, d, 1e-4f);

            // Not moving.
            BotSteering.ClosestApproach(new Vector3(3f, 4f, 0f), Vector3.zero, 1f, out d);
            Assert.AreEqual(5f, d, 1e-4f);
        }

        private static void AssertAxes(Vector2 expected, Vector2 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-4f, "x");
            Assert.AreEqual(expected.y, actual.y, 1e-4f, "y");
        }
    }
}
#endif
