#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Automation;
using GlorifiedBody.Cameras;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Rendering;
using GlorifiedBody.Rooms;
using GlorifiedBody.Saving;
using GlorifiedBody.Story;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The dark room (DEMO_PLAN §10.1, Bruno's puzzle, CANON round 52) in the DARK TEST room: the office and the closet go
    /// dark when she enters, and the dark shows on screen. The units start in the order NET, REC, ACCESS; one out of order
    /// trips the alarm, the PCs drop, and the beeps come back after the reset. The monitors come back one by one, and the
    /// release works only then.
    /// Batch: tools/unity.sh test playmode "DarkRoomTests"
    /// </summary>
    public class DarkRoomTests
    {
        private const string Scene = "DarkTest";
        private const string DarkFlag = "dark_test_dark", DoneFlag = "dark_test_units_up", Released = "dark_test_released";

        private VirtualPad pad;
        private PadDriver driver;
        private TankController tank;
        private Interactor interactor;
        private DarkRoom dark;
        private UpsBank bank;
        private Room hall, office, closet;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            foreach (string f in new[] { DarkFlag, DoneFlag, Released }) StoryFlags.Drop(f);
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            GameManager.Instance.LoadLevel(Scene);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == Scene && GameManager.Instance.IsPlaying, 10f);
            Assert.AreEqual(Scene, SceneManager.GetActiveScene().name, "the room is in the build (DarkTestSetup.BuildDarkTest)");
            yield return null;
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            tank = lucy.GetComponent<TankController>();
            interactor = lucy.GetComponent<Interactor>();
            dark = UnityEngine.Object.FindFirstObjectByType<DarkRoom>();
            bank = UnityEngine.Object.FindFirstObjectByType<UpsBank>();
            hall = Room.Of("Q1");
            office = Room.Of("Q2");
            closet = Room.Of("Q3");
            pad = new VirtualPad();
            GameInput.SetBotSource(pad);
            driver = new GameObject("[PadDriver]").AddComponent<PadDriver>();
            driver.Pad = pad;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameInput.ClearBotSource(pad);
            if (driver != null) UnityEngine.Object.Destroy(driver.gameObject);
            foreach (string f in new[] { DarkFlag, DoneFlag, Released }) StoryFlags.Drop(f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheOffice_GoesDark_WhenSheEnters_AndSheIsDarkOnlyInside()
        {
            Renderer floor = office.transform.Find("[Room]/Floor").GetComponent<Renderer>();
            Renderer closetFloor = closet.transform.Find("[Room]/Floor").GetComponent<Renderer>();
            Renderer lucyBody = tank.GetComponentsInChildren<SkinnedMeshRenderer>().First();
            Assert.IsFalse(dark.IsDark, "lit until she comes in");
            Assert.AreEqual(Darkness.LitLayer, floor.renderingLayerMask);
            Assert.AreEqual(UpsBank.State.Lit, bank.Current);
            Assert.AreEqual(6, bank.MonitorsOn, "the monitor wall runs");

            tank.Teleport(office.Centre, 0f);
            yield return WaitUntil(() => dark.IsDark && dark.HasInside(tank.gameObject), 2f);
            Assert.IsTrue(dark.IsDark, "the lights go out");
            Assert.IsTrue(StoryFlags.Has(DarkFlag), "its flag for the saves");
            Assert.IsTrue(Darkness.IsDark(floor) && Darkness.IsDark(closetFloor), "the office and the closet");
            Assert.AreEqual(Darkness.DarkLayer, floor.renderingLayerMask, "off the sun's layer");
            Assert.IsTrue(Darkness.IsDark(lucyBody), "she is dark inside");
            Assert.AreEqual(UpsBank.State.Beeping, bank.Current, "the units beep");
            Assert.AreEqual(0, bank.MonitorsOn, "the monitors go black");

            tank.Teleport(hall.Centre, 0f);
            yield return WaitUntil(() => !dark.HasInside(tank.gameObject), 2f);
            Assert.IsFalse(Darkness.IsDark(lucyBody), "lit again in the hall");
            Assert.IsTrue(dark.IsDark, "the room lights never come back");
        }

        [UnityTest]
        public IEnumerator TheDark_ShowsOnScreen_AndTheMonitorsLightTheRoom()
        {
            FixedCamera pose = office.GetComponentsInChildren<FixedCamera>().First();
            float lit = Brightness(pose);
            dark.SetDark();
            yield return null;
            float black = Brightness(pose);
            Debug.Log($"[DarkRoom] brightness lit {lit:0.000}, dark {black:0.000}");
            Assert.Less(black, lit * 0.35f, "the office is dark on screen");

            yield return StartUnits("NET", "REC", "ACCESS");
            yield return WaitUntil(() => bank.MonitorsOn == 6, 6f);
            float monitors = Brightness(pose);
            Debug.Log($"[DarkRoom] brightness with the monitors {monitors:0.000}");
            Assert.Greater(monitors, black * 1.5f, "the monitors light the dark");
            Assert.Less(monitors, lit, "and the room lights stay out");
        }

        [UnityTest]
        public IEnumerator TheUnitsInOrder_BringTheMonitorsBack_OneByOne_AndOpenTheRelease()
        {
            FlagSwitch release = GameObject.Find("Release_Button").GetComponent<FlagSwitch>();
            dark.SetDark();
            yield return Face(release.Point + Vector3.left * 1.0f, release.Point);
            yield return Press(InputManager.GameAction.Interact);
            Assert.AreEqual("WAITING FOR NET.", interactor.Text, "the release is dead");
            yield return new WaitForSeconds(Interactor.MessageSeconds + 0.2f);

            yield return StartUnits("NET", "REC");
            Assert.AreEqual(2, bank.Next);
            Assert.IsFalse(StoryFlags.Has(DoneFlag));
            yield return StartUnits("ACCESS");
            Assert.IsTrue(bank.Done && StoryFlags.Has(DoneFlag), "the third unit: done");
            yield return new WaitForSeconds(bank.monitorGap * 1.5f);
            Assert.Greater(bank.MonitorsOn, 0, "the monitors come back");
            Assert.Less(bank.MonitorsOn, 6, "one by one");
            yield return WaitUntil(() => bank.MonitorsOn == 6, 5f);
            Assert.AreEqual(6, bank.MonitorsOn);

            yield return Face(release.Point + Vector3.left * 1.0f, release.Point);
            yield return Press(InputManager.GameAction.Interact);
            StringAssert.StartsWith("ask:", interactor.LastResult, "the release asks now");
            yield return Press(InputManager.GameAction.Interact);
            Assert.IsTrue(StoryFlags.Has(Released));
        }

        [UnityTest]
        public IEnumerator AUnitOutOfOrder_TripsTheAlarm_ThePCsDrop_AndTheBeepsComeBack()
        {
            bank.resetSeconds = 1.5f; // 5 s in the game
            dark.SetDark();
            yield return StartUnits("NET");
            UpsUnit net = bank.units.First(u => u.label == "NET");
            Assert.IsTrue(net.Up);
            yield return StartUnits("ACCESS");
            Assert.AreEqual(UpsBank.State.Alarm, bank.Current, "out of order: the alarm");
            Assert.AreEqual(1, bank.Alarms);
            Assert.IsFalse(net.Up, "the PCs drop");
            Assert.IsFalse(bank.units.Any(u => u.Available), "no unit starts during the alarm");
            yield return new WaitForSeconds(bank.resetSeconds + 0.3f);
            Assert.AreEqual(UpsBank.State.Beeping, bank.Current, "the beeps come back");
            Assert.AreEqual(0, bank.Next, "from the start");
            yield return StartUnits("NET", "REC", "ACCESS");
            Assert.IsTrue(bank.Done, "the right order works after a mistake");
        }

        [UnityTest]
        public IEnumerator TheAlarm_WithTheRoomsOwnTime_Ends_AndTheUnitsTakeTheKeyAgain()
        {
            // Bruno, 2026-11-08: "when i press the wrong one the thing keeps on alarm and never lets me try it again". The
            // test above shortens the reset: this one keeps the room's own time.
            float reset = bank.resetSeconds;
            Debug.Log($"[DarkRoomTests] the room's reset: {reset:0.0} s");
            Assert.LessOrEqual(reset, 5f, "Bruno, 2026-11-08: \"20 seconds is too long\"");
            dark.SetDark();
            yield return StartUnits("REC");
            Assert.AreEqual(UpsBank.State.Alarm, bank.Current, "out of order: the alarm");
            float t0 = Time.realtimeSinceStartup;
            while (bank.Current == UpsBank.State.Alarm && Time.realtimeSinceStartup - t0 < reset + 10f) yield return null;
            Debug.Log($"[DarkRoomTests] the alarm ended after {Time.realtimeSinceStartup - t0:0.0} s (state {bank.Current}, sound {bank.AlarmOn})");
            Assert.AreEqual(UpsBank.State.Beeping, bank.Current, "the alarm ends");
            Assert.IsFalse(bank.AlarmOn, "its sound stops");
            yield return StartUnits("NET", "REC", "ACCESS");
            Assert.IsTrue(bank.Done, "she can try again");
        }

        // ── Helpers ───────────────────────────────────────────────────

        /// <summary>Each unit by the Interact key, as she does it: in front of it, facing it.</summary>
        private IEnumerator StartUnits(params string[] labels)
        {
            foreach (string label in labels)
            {
                UpsUnit unit = bank.units.First(u => u.label == label);
                yield return Face(unit.Point, unit.transform.position);
                Assert.AreSame(unit, interactor.Focus, $"the {label} unit in front of her");
                yield return Press(InputManager.GameAction.Interact);
            }
        }

        private static float Brightness(FixedCamera pose)
        {
            var obj = new GameObject("[BrightnessCamera]");
            Camera cam = obj.AddComponent<Camera>();
            if (Camera.main != null) cam.CopyFrom(Camera.main);
            cam.enabled = false;
            cam.fieldOfView = pose.fieldOfView;
            obj.transform.SetPositionAndRotation(pose.transform.position, pose.transform.rotation);
            var rt = new RenderTexture(320, 180, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(320, 180, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 320, 180), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            float sum = tex.GetPixels().Sum(c => c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f) / (320 * 180);
            cam.targetTexture = null;
            UnityEngine.Object.Destroy(tex);
            rt.Release();
            UnityEngine.Object.Destroy(obj);
            return sum;
        }

        private IEnumerator Face(Vector3 point, Vector3 target)
        {
            point.y = 0f;
            Vector3 to = target - point;
            tank.Teleport(point, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
            yield return null;
            yield return null;
        }

        private IEnumerator Press(InputManager.GameAction action)
        {
            pad.Tap(action);
            yield return null;
            yield return null;
            yield return null;
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }

        [DefaultExecutionOrder(-1000)]
        private sealed class PadDriver : MonoBehaviour
        {
            public VirtualPad Pad;
            private void Update() => Pad?.Commit();
        }
    }
}
#endif
