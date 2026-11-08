#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Automation;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Saving;
using GlorifiedBody.Story;
using GlorifiedBody.Systems;
using GlorifiedBody.UI;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The code rotation (DEMO_PLAN §10.2, Bruno's puzzle, CANON round 52; 2026-11-08: a backtracker, the code 8675309) in
    /// the CODE TEST room: the card opens two doors, then the PA rotates the codes, and the third door refuses the card
    /// until the terminal enrols it with the code from the wall. A wrong code costs nothing. The keypad stops the world as
    /// a log does, and the power keys do nothing in it.
    /// Batch: tools/unity.sh test playmode "CodeRotationTests"
    /// </summary>
    public class CodeRotationTests
    {
        private const string Scene = "CodeTest";
        private const string Rotated = "code_test_rotated", Enrolled = "code_test_enrolled";
        private const string Code = "8675309";

        private VirtualPad pad;
        private PadDriver driver;
        private TankController tank;
        private Interactor interactor;
        private LockedDoor door, storeA, storeB;
        private CodeRotation rotation;
        private CodeTerminal terminal;
        private ItemPickup card;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            GameManager.Instance.LoadLevel(Scene);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == Scene && GameManager.Instance.IsPlaying, 10f);
            Assert.AreEqual(Scene, SceneManager.GetActiveScene().name, "the room is in the build (CodeTestSetup.BuildCodeTest)");
            yield return null;
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            tank = lucy.GetComponent<TankController>();
            interactor = lucy.GetComponent<Interactor>();
            door = Door("K6");
            storeA = Door("K2");
            storeB = Door("K5");
            rotation = UnityEngine.Object.FindFirstObjectByType<CodeRotation>();
            terminal = UnityEngine.Object.FindFirstObjectByType<CodeTerminal>();
            card = UnityEngine.Object.FindObjectsByType<ItemPickup>(FindObjectsSortMode.None).First(p => p.item != null && p.item.itemId == "code_test_card");
            Assert.IsNotNull(door, "the card door of the way on");
            Assert.IsNotNull(storeA, "the card door of store A");
            Assert.IsNotNull(storeB, "the card door of store B");
            Assert.IsNotNull(rotation, "the code rotation");
            Assert.IsNotNull(terminal, "the terminal");
            pad = new VirtualPad();
            GameInput.SetBotSource(pad);
            driver = new GameObject("[PadDriver]").AddComponent<PadDriver>();
            driver.Pad = pad;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (KeypadUI.IsOpen) KeypadUI.Instance.Close();
            GameInput.ClearBotSource(pad);
            if (driver != null) UnityEngine.Object.Destroy(driver.gameObject);
            StoryFlags.Drop(Rotated);
            StoryFlags.Drop(Enrolled);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BeforeTheRotation_TheCardOpensTheDoor()
        {
            interactor.Inventory.TryAdd(card.item);
            yield return StandFacing(storeA.Point + Vector3.back * 1.1f, storeA.Point);
            Assert.AreSame(storeA, interactor.Focus);
            yield return Press(InputManager.GameAction.Interact);
            StringAssert.StartsWith("ask:", interactor.LastResult, "it asks to use the card");
            yield return Press(InputManager.GameAction.Interact);
            Assert.IsTrue(storeA.IsOpen, "the card opens it");
        }

        [UnityTest]
        public IEnumerator TheWayOn_TriedFirst_RotatesTheCodesAtOnce_AndRefusesTheCard()
        {
            interactor.Inventory.TryAdd(card.item);
            Assert.IsTrue(door.rotatesOnTry, "the way on (CodeTestSetup)");
            int said = Subtitles.SubtitleService.Said;
            yield return FaceDoor();
            yield return Press(InputManager.GameAction.Interact);
            Assert.IsTrue(StoryFlags.Has(Rotated), "the first try rotates the codes: no way past the puzzle");
            StringAssert.StartsWith("rotated:", interactor.LastResult, "the reader refuses the card");
            Assert.AreEqual(LockedDoor.RotatedText, interactor.Text);
            Assert.IsFalse(door.IsOpen);
            yield return WaitUntil(() => Subtitles.SubtitleService.Said > said, 3f);
            Assert.IsTrue(Subtitles.SubtitleService.Lines.Any(l => l.Text.Contains("Re-enrol your card")), "the PA says it");
        }

        [UnityTest]
        public IEnumerator TheCard_OpensTwoDoors_ThenThePaRotatesTheCodes_AndTheThirdRefusesIt()
        {
            interactor.Inventory.TryAdd(card.item);
            yield return Open(storeA);
            Assert.IsTrue(storeA.IsOpen, "the card opens the first door");
            yield return new WaitForSeconds(rotation.delaySeconds + 0.5f);
            Assert.IsFalse(StoryFlags.Has(Rotated), "one door: the codes do not rotate yet");

            int said = Subtitles.SubtitleService.Said;
            yield return Open(storeB);
            Assert.IsTrue(storeB.IsOpen, "the card opens the second door");
            Assert.IsFalse(StoryFlags.Has(Rotated), "not at once: she goes through the door first");
            yield return WaitUntil(() => StoryFlags.Has(Rotated), rotation.delaySeconds + 2f);
            Assert.IsTrue(StoryFlags.Has(Rotated), "two doors: the codes rotate");
            yield return WaitUntil(() => Subtitles.SubtitleService.Said > said, 3f);
            Assert.Greater(Subtitles.SubtitleService.Said, said, "the PA says it");
            Assert.IsTrue(Subtitles.SubtitleService.Lines.Any(l => l.Text.Contains("Re-enrol your card")), "the words of call 1B on screen");

            yield return FaceDoor();
            yield return Press(InputManager.GameAction.Interact);
            StringAssert.StartsWith("rotated:", interactor.LastResult, "the third door refuses the card");
            Assert.IsFalse(door.IsOpen);
            Assert.IsTrue(storeA.IsOpen && storeB.IsOpen, "the doors she opened stay open");
        }

        [UnityTest]
        public IEnumerator AfterTheRotation_TheReaderRefusesTheCard_AndStaysRed()
        {
            interactor.Inventory.TryAdd(card.item);
            StoryFlags.Raise(Rotated);
            yield return FaceDoor();
            yield return Press(InputManager.GameAction.Interact);
            StringAssert.StartsWith("rotated:", interactor.LastResult, "the reader refuses the card");
            Assert.AreEqual(LockedDoor.RotatedText, interactor.Text);
            Assert.AreEqual(LockedDoor.RotatedText, UnityEngine.Object.FindFirstObjectByType<InteractionUI>().ConfirmTextShown, "the words on screen");
            Assert.IsFalse(door.IsOpen);
            AssertColour(LockedDoor.ReaderRed, ReaderColour(), "the reader is red");
            yield return new WaitForSeconds(Interactor.MessageSeconds + 0.2f);
            Assert.AreEqual(Interactor.State.Free, interactor.Current, "the message goes, and she can move");
        }

        [UnityTest]
        public IEnumerator TheTerminal_WithoutTheCard_AsksForIt()
        {
            StoryFlags.Raise(Rotated);
            yield return FaceTerminal();
            Assert.AreSame(terminal, interactor.Focus);
            yield return Press(InputManager.GameAction.Interact);
            Assert.AreEqual(CodeTerminal.PresentText, interactor.Text);
            Assert.IsFalse(KeypadUI.IsOpen, "no keypad without the card");
        }

        [UnityTest]
        public IEnumerator TheKeypad_StopsTheWorld_AWrongCodeCostsNothing_TheRightCodeEnrolsTheCard()
        {
            interactor.Inventory.TryAdd(card.item);
            StoryFlags.Raise(Rotated);
            yield return FaceTerminal();
            yield return Press(InputManager.GameAction.Interact);
            Assert.IsTrue(KeypadUI.IsOpen, "the card and the rotated code: the keypad opens");
            Assert.IsTrue(GameManager.Instance.IsReading, "it stops the world as a log does");
            Assert.AreEqual(0f, Time.timeScale);

            PsychicPowers powers = tank.GetComponent<PsychicPowers>();
            var selected = powers.Selected.kind;
            yield return Press(InputManager.GameAction.PowerFire);
            Assert.AreEqual(selected, powers.Selected.kind, "a power key does nothing in the keypad");

            Assert.AreEqual(Code.Length, KeypadUI.Instance.Length, "one digit for each digit of the code");
            yield return Dial("1234567");
            Assert.AreEqual("1234567", KeypadUI.Instance.Digits);
            yield return Press(InputManager.GameAction.Interact);
            Assert.AreEqual(CodeTerminal.WrongText, KeypadUI.Instance.Message);
            Assert.IsTrue(KeypadUI.IsOpen, "a wrong code: it stays open, try again at once");
            Assert.IsFalse(StoryFlags.Has(Enrolled));

            yield return Dial(Code);
            yield return Press(InputManager.GameAction.Interact);
            Assert.AreEqual(CodeTerminal.EnrolledText, KeypadUI.Instance.Message);
            Assert.IsTrue(StoryFlags.Has(Enrolled), "the right code enrols the card");
            yield return WaitUntil(() => !KeypadUI.IsOpen, 3f);
            Assert.IsFalse(KeypadUI.IsOpen, "it closes by itself");
            Assert.IsTrue(GameManager.Instance.IsPlaying);
            Assert.AreEqual(1f, Time.timeScale, "the world runs again");
            Assert.AreEqual(2, terminal.Tries);
        }

        [UnityTest]
        public IEnumerator TheInventoryKey_ClosesTheKeypad_WithNoCode()
        {
            interactor.Inventory.TryAdd(card.item);
            StoryFlags.Raise(Rotated);
            yield return FaceTerminal();
            yield return Press(InputManager.GameAction.Interact);
            Assert.IsTrue(KeypadUI.IsOpen);
            yield return Press(InputManager.GameAction.Inventory);
            Assert.IsFalse(KeypadUI.IsOpen);
            Assert.IsTrue(GameManager.Instance.IsPlaying);
            Assert.IsFalse(GameManager.Instance.IsInventory, "the same key does not open the inventory");
            Assert.AreEqual(0, terminal.Tries);
        }

        [UnityTest]
        public IEnumerator AfterTheEnrolment_TheReaderIsGreen_AndTheCardOpensTheDoor()
        {
            interactor.Inventory.TryAdd(card.item);
            StoryFlags.Raise(Rotated);
            StoryFlags.Raise(Enrolled);
            yield return FaceTerminal();
            yield return Press(InputManager.GameAction.Interact);
            Assert.AreEqual(CodeTerminal.EnrolledText, interactor.Text, "the terminal says it is done");
            Assert.IsFalse(KeypadUI.IsOpen);
            yield return new WaitForSeconds(Interactor.MessageSeconds + 0.2f);
            yield return FaceDoor();
            AssertColour(LockedDoor.ReaderGreen, ReaderColour(), "the reader is green");
            yield return Press(InputManager.GameAction.Interact);
            yield return Press(InputManager.GameAction.Interact);
            Assert.IsTrue(door.IsOpen, "the enrolled card opens it");
        }

        // ── Helpers ───────────────────────────────────────────────────

        private static LockedDoor Door(string to) =>
            UnityEngine.Object.FindObjectsByType<LockedDoor>(FindObjectsSortMode.None).FirstOrDefault(d => d.name == "Door_" + to);

        private IEnumerator Open(LockedDoor which)
        {
            yield return StandFacing(which.Point + (which.reader.transform.position.z > which.Point.z ? Vector3.forward : Vector3.back) * 1.1f, which.Point);
            Assert.AreSame(which, interactor.Focus, which.name);
            yield return Press(InputManager.GameAction.Interact);
            yield return Press(InputManager.GameAction.Interact);
            yield return new WaitForSeconds(0.2f);
        }

        private IEnumerator Dial(string code)
        {
            // From the first digit: each digit by the forward key from its present value, then the turn key to the next.
            int back = KeypadUI.Instance.Cursor;
            for (int i = 0; i < back; i++) yield return Press(InputManager.GameAction.TurnLeft);
            Assert.AreEqual(0, KeypadUI.Instance.Cursor, "the cursor at the first digit");
            for (int i = 0; i < code.Length; i++)
            {
                int now = KeypadUI.Instance.Digits[i] - '0', want = code[i] - '0';
                for (int k = 0; k < (want - now + 10) % 10; k++) yield return Press(InputManager.GameAction.Forward);
                if (i < code.Length - 1) yield return Press(InputManager.GameAction.TurnRight);
            }
        }

        private static void AssertColour(Color want, Color was, string what)
        {
            Assert.AreEqual(want.r, was.r, 0.01f, what);
            Assert.AreEqual(want.g, was.g, 0.01f, what);
            Assert.AreEqual(want.b, was.b, 0.01f, what);
        }

        private Color ReaderColour()
        {
            var block = new MaterialPropertyBlock();
            door.reader.GetPropertyBlock(block);
            return block.GetColor("_BaseColor");
        }

        private IEnumerator FaceDoor() => StandFacing(door.Point + Vector3.forward * 1.1f, door.Point);

        private IEnumerator FaceTerminal() => StandFacing(terminal.Point, terminal.transform.position);

        private IEnumerator StandFacing(Vector3 point, Vector3 target)
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
