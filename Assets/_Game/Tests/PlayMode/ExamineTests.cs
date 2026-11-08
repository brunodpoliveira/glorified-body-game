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
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The common interaction (Bruno, 2026-11-08: "lucy can interact with objects normally (pressing e) and she'll comment
    /// about it. there should be placeholder lines in cases of cupboards already cleaned up of items") in the CODE TEST
    /// room: the Interact key on a prop shows her comment, a container asks for each item inside and then says that
    /// nothing else is there, and a thing to take on a prop keeps the key.
    /// Batch: tools/unity.sh test playmode "ExamineTests"
    /// </summary>
    public class ExamineTests
    {
        private const string Scene = "CodeTest";

        private VirtualPad pad;
        private PadDriver driver;
        private TankController tank;
        private Interactor interactor;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            GameManager.Instance.LoadLevel(Scene);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == Scene && GameManager.Instance.IsPlaying, 10f);
            yield return null;
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            tank = lucy.GetComponent<TankController>();
            interactor = lucy.GetComponent<Interactor>();
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
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheCardOnTheTable_KeepsTheKey_ThenTheTableShowsHerComment()
        {
            ItemPickup card = UnityEngine.Object.FindObjectsByType<ItemPickup>(FindObjectsSortMode.None).First(p => p.item != null && p.item.itemId == "code_test_card");
            Examinable table = Look("K1", "Table");
            yield return StandFacing(card.Point + Vector3.back * 0.9f, card.Point);
            Assert.AreSame(card, interactor.Focus, "the card on the table keeps the key");
            yield return Press(InputManager.GameAction.Interact);
            yield return Press(InputManager.GameAction.Interact);
            StringAssert.StartsWith("taken:", interactor.LastResult);

            yield return null;
            Assert.AreSame(table, interactor.Focus, "then the table");
            yield return null;
            Assert.AreEqual("", UnityEngine.Object.FindFirstObjectByType<GlorifiedBody.UI.InteractionUI>().HintShown, "no hint over a prop (Bruno, 2026-11-08)");
            yield return Press(InputManager.GameAction.Interact);
            Assert.AreEqual(Interactor.State.Message, interactor.Current, "a comment, no question");
            Assert.AreEqual("A table. Nothing of note here.", interactor.Text);
            Assert.AreEqual("examined:Table", interactor.LastResult);
            yield return new WaitForSeconds(Interactor.MessageSeconds + 0.2f);
            Assert.AreEqual(Interactor.State.Free, interactor.Current, "the comment goes, and she can move");
        }

        [UnityTest]
        public IEnumerator TheShelfOfStoreA_AsksForTheCapsule_ThenSaysNothingElseIsThere()
        {
            Examinable shelf = Look("K2", "Shelf");
            Assert.IsTrue(shelf.IsContainer, "a container");
            ItemDefinition capsule = shelf.Next.item;
            yield return StandFacing(shelf.Point + Vector3.back * 1.3f, shelf.Point);
            Assert.AreSame(shelf, interactor.Focus);
            yield return null;
            Assert.AreEqual("", UnityEngine.Object.FindFirstObjectByType<GlorifiedBody.UI.InteractionUI>().HintShown, "no hint over a container either");
            int had = interactor.Inventory.DrugsCarried + interactor.Inventory.Carried.Count;
            yield return Press(InputManager.GameAction.Interact);
            Assert.AreEqual(Interactor.State.Confirming, interactor.Current, "it asks");
            StringAssert.Contains("inside. Take it?", interactor.Text);
            yield return Press(InputManager.GameAction.Interact);
            Assert.AreEqual(had + 1, interactor.Inventory.DrugsCarried + interactor.Inventory.Carried.Count, "the capsule is hers");
            Assert.IsNull(shelf.Next, "nothing is left inside");

            yield return Press(InputManager.GameAction.Interact);
            Assert.AreEqual(Interactor.State.Message, interactor.Current);
            Assert.AreEqual(Examinable.DefaultEmpty, interactor.Text, "a cleaned-out container says so");
            Assert.AreEqual("empty:Shelf", interactor.LastResult);
        }

        [UnityTest]
        public IEnumerator TheShelfOfStoreB_HoldsNothing_AndSaysSo()
        {
            Examinable shelf = Look("K5", "Shelf");
            Assert.IsFalse(shelf.IsContainer);
            yield return StandFacing(shelf.Point + Vector3.back * 1.3f, shelf.Point);
            yield return Press(InputManager.GameAction.Interact);
            Assert.AreEqual("A shelf. Nothing of note here.", interactor.Text);
            Assert.AreEqual(1, shelf.Shows);
        }

        // ── Helpers ───────────────────────────────────────────────────

        private static Examinable Look(string room, string name)
        {
            Examinable look = UnityEngine.Object.FindObjectsByType<Examinable>(FindObjectsSortMode.None)
                .FirstOrDefault(e => e.name == name && e.transform.parent != null && e.transform.parent.parent != null && e.transform.parent.parent.name == "[" + room + "]");
            Assert.IsNotNull(look, $"{name} in {room} has a comment (DemoRoom.Prop)");
            return look;
        }

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
