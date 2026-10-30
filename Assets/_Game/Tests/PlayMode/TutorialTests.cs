#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using GlorifiedBody.Automation;
using GlorifiedBody.Cameras;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.Interaction;
using GlorifiedBody.Enemy;
using GlorifiedBody.Rendering;
using GlorifiedBody.Systems;
using GlorifiedBody.Tutorial;
using GlorifiedBody.UI;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The tutorial mode (Bruno, 2026-10-22): the gym loads with the director, the prompt, the targets and the
    /// baked light; the main menu's TUTORIAL button opens it; the prompt shows the bound keys; a player who
    /// does each thing completes every step in order and the level completes; every floor point is in the
    /// frame of its camera. Batch: tools/unity.sh test playmode.
    /// </summary>
    public class TutorialTests
    {
        private const string MainMenu = "Main_Menu";
        private static readonly string[] ExpectedSteps = { "walk", "run", "dodge", "switch", "blast", "fire", "lift_throw", "slam", "prop", "catch", "grenade", "shield", "cover", "capsule", "log", "scan_image", "key", "door", "inventory", "scan_video", "short", "complete" };

        private TutorialDirector director;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(MainMenu);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == MainMenu && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            SettingsManager.Instance.LookQuality = LookQuality.Authentic;
            GameManager.Instance.LoadLevel(TutorialDirector.SceneName);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == TutorialDirector.SceneName && GameManager.Instance.IsPlaying, 10f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return new WaitForSeconds(0.3f);
            director = TutorialDirector.Instance;
            Assert.IsNotNull(director, "the TutorialDirector in the gym");
        }

        /// <summary>Bruno (2026-10-24): the tutorial is a safe room: no damage, and the MK never runs out.</summary>
        [UnityTest]
        public IEnumerator TheGym_NoDamage_AndTheMkStaysFull()
        {
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            var health = lucy.GetComponentInChildren<GlorifiedBody.Core.Health>();
            var stats = lucy.GetComponent<GlorifiedBody.Player.PsychicStats>();
            float hp = health.CurrentHealth;
            health.TakeDamage(30f);
            stats.DebugSet(10f, stats.Short);
            yield return null;
            yield return null;
            Assert.AreEqual(hp, health.CurrentHealth, 0.01f, "no damage in the tutorial");
            Assert.AreEqual(GlorifiedBody.Player.PsychicStats.MaxNalcon, stats.Nalcon, 0.01f, "the MK is full again");
        }

        /// <summary>
        /// Bruno's report (2026-10-24): in the short step, a drug used before the short soft-locks the step (the drug
        /// empties the SHORT bar and was the only one). The step must still end.
        /// </summary>
        [UnityTest]
        public IEnumerator ShortStep_ADrugFirst_DoesNotSoftLockTheStep()
        {
            while (director.Current != null && director.Current.Id != "short") director.Advance();
            Assert.AreEqual("short", director.Current.Id);
            yield return null;
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            var powers = lucy.GetComponent<GlorifiedBody.Player.PsychicPowers>();
            var stats = lucy.GetComponent<GlorifiedBody.Player.PsychicStats>();
            var drugs = lucy.GetComponent<GlorifiedBody.Player.DrugInventory>();
            Assert.IsTrue(stats.IsShortFull, "the step fills the bar");
            // The SHORT capsule (Bruno, 2026-10-30: each drug does one thing) lowers the bar before the short.
            Assert.IsTrue(powers.UseDrug(GlorifiedBody.Player.DrugType.Red), "the SHORT capsule before the short");
            yield return new WaitForSeconds(0.5f);
            Debug.Log($"[TutorialTests] after the drug: short {stats.Short:0}, SHORT capsules {drugs.Count(GlorifiedBody.Player.DrugType.Red)}");
            Assert.IsTrue(stats.IsShortFull, "the bar is full again: the short can start");
            Assert.Greater(drugs.Count(GlorifiedBody.Player.DrugType.Red), 0, "a SHORT capsule to end it");
        }

        /// <summary>
        /// Bruno's report (2026-10-24): he took the log in the capsule step (the two stand 1.1 m apart), and the log
        /// step could not end: its pickup was gone. A log read in the inventory ends the step.
        /// </summary>
        [UnityTest]
        public IEnumerator LogStep_TheLogTakenEarly_ReadInTheInventory_EndsTheStep()
        {
            while (director.Current != null && director.Current.Id != "capsule") director.Advance();
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            Interactor interactor = lucy.GetComponent<Interactor>();
            ItemPickup log = UnityEngine.Object.FindObjectsByType<ItemPickup>(FindObjectsSortMode.None).FirstOrDefault(p => p.item != null && p.item.itemId == director.LogItemId);
            Assert.IsNotNull(log, "the log pickup");
            StringAssert.StartsWith("taken:", log.Confirm(interactor), "she takes the log in the capsule step");
            yield return null;
            director.Advance();
            Assert.AreEqual("log", director.Current.Id);
            yield return null;
            InventoryUI ui = UnityEngine.Object.FindFirstObjectByType<InventoryUI>();
            ui.Open();
            yield return null;
            int index = ui.Entries.ToList().FindIndex(e => e.Item != null && e.Item.itemId == director.LogItemId);
            Assert.GreaterOrEqual(index, 0, "the log is in the inventory");
            ui.SelectIndex(index);
            StringAssert.StartsWith("read:", ui.Act(), "she reads it in the inventory");
            ui.Close();
            yield return new WaitForSeconds(0.3f);
            Assert.AreNotEqual("log", director.Current.Id, "the log step ended");
        }

        /// <summary>
        /// Bruno's question (2026-10-25): must she lock the gun dummy to catch its bullet? The lock stays on another
        /// dummy, and the catch press (the same call as the defend key) must still end the step.
        /// </summary>
        [UnityTest]
        public IEnumerator CatchStep_TheLockOnAnotherDummy_TheCatchStillWorks()
        {
            while (director.Current != null && director.Current.Id != "catch") director.Advance();
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            var powers = lucy.GetComponent<GlorifiedBody.Player.PsychicPowers>();
            var lockOn = lucy.GetComponent<GlorifiedBody.Player.LockOnController>();
            powers.Select(GlorifiedBody.Player.PowerKind.Telekinesis);
            LockOnTargetable other = GameObject.Find("Target_1").GetComponent<LockOnTargetable>();
            Vector3 chest = lucy.transform.position + Vector3.up * GlorifiedBody.Characters.Body.ChestHeight;
            EnemyProjectile near = null;
            float t0 = Time.time;
            while (near == null && Time.time - t0 < 15f)
            {
                lockOn.SetTarget(other);
                chest = lucy.transform.position + Vector3.up * GlorifiedBody.Characters.Body.ChestHeight;
                near = UnityEngine.Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None).FirstOrDefault(b => b.Catchable && Vector3.Distance(b.transform.position, chest) < 2f);
                yield return null;
            }
            Assert.IsNotNull(near, "a bullet came within 2 m of her");
            Assert.AreSame(other, lockOn.CurrentTarget, "the lock is on Target_1, not on the gun");
            Assert.IsTrue(powers.TryCatchBullet(), "the catch works with the lock on another dummy");
            yield return null;
            Assert.AreNotEqual("catch", director.Current.Id, "the catch step ended");
        }

        /// <summary>The capsule used before its step: the step gives a drug, so it can still end.</summary>
        [UnityTest]
        public IEnumerator CapsuleStep_TheCapsuleUsedEarly_DoesNotSoftLockTheStep()
        {
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            var drugs = lucy.GetComponent<GlorifiedBody.Player.DrugInventory>();
            var powers = lucy.GetComponent<GlorifiedBody.Player.PsychicPowers>();
            foreach (ItemPickup capsule in UnityEngine.Object.FindObjectsByType<ItemPickup>(FindObjectsSortMode.None).Where(p => p.item != null && p.item.kind == ItemKind.Drug).ToArray())
                capsule.Confirm(lucy.GetComponent<Interactor>());
            // Every drug gone before the step (used or refused: a drug with nothing to do stays, so the test empties them).
            foreach (GlorifiedBody.Player.DrugType type in (GlorifiedBody.Player.DrugType[])Enum.GetValues(typeof(GlorifiedBody.Player.DrugType)))
                drugs.DebugSet(type, 0);
            while (director.Current != null && director.Current.Id != "capsule") director.Advance();
            yield return new WaitForSeconds(0.3f);
            int held = ((GlorifiedBody.Player.DrugType[])Enum.GetValues(typeof(GlorifiedBody.Player.DrugType))).Sum(t => drugs.Count(t));
            Assert.Greater(held, 0, "the step gives a drug to use");
        }

        [UnityTest]
        public IEnumerator TheGym_Loads_WithTheDirectorThePromptTheTargetsAndTheBakedLight()
        {
            yield return null;
            CollectionAssert.AreEqual(ExpectedSteps, director.Steps.Select(s => s.Id).ToArray(), "the steps of the demonstration, in order");
            Assert.AreEqual(0, director.StepIndex, "the first step is on");
            Assert.AreEqual("walk", director.Current.Id);

            TutorialPromptUI prompt = UnityEngine.Object.FindFirstObjectByType<TutorialPromptUI>();
            Assert.IsNotNull(prompt, "the prompt on the runtime UI");
            Assert.AreEqual(director.Current.Line, prompt.LineShown, "the presenter's line");
            Assert.IsFalse(prompt.HintShown.Contains("{"), "every token became a key name: " + prompt.HintShown);
            Assert.IsTrue(prompt.HintShown.Contains(InputManager.GetKeyDisplayName(InputManager.Instance.GetKey(InputManager.GameAction.Forward))), "the walk key in the hint: " + prompt.HintShown);

            foreach (string name in new[] { "Target_1", "Target_2", "Target_3", "Target_Gun" })
            {
                GameObject target = GameObject.Find(name);
                Assert.IsNotNull(target, name);
                Assert.IsNotNull(target.GetComponent<Health>(), name + " carries Health");
                Assert.IsNotNull(target.GetComponent<LockOnTargetable>(), name + " is a lock target");
                Assert.AreEqual(LayerMask.NameToLayer("Enemy"), target.layer, name + " on the Enemy layer");
            }
            DummyShooter shooter = GameObject.Find("Target_Gun").GetComponent<DummyShooter>();
            Assert.IsNotNull(shooter, "the gun on Target_Gun");
            Assert.IsFalse(shooter.enabled, "the gun waits for its step");
            Assert.IsFalse(GameObject.Find("Target_Gun").GetComponent<LockOnTargetable>().Selectable, "the gun dummy is out of the lock until its step");
            Assert.IsNotNull(GameObject.Find("Capsule"), "the capsule by the bars");
            GameObject crate = GameObject.Find("Prop_Crate");
            Assert.IsNotNull(crate, "the crate for the prop step");
            Assert.IsTrue(crate.GetComponent<LockOnTargetable>() != null && crate.GetComponent<LockOnTargetable>().IsProp, "the crate is a prop target");
            Assert.AreEqual(4, UnityEngine.Object.FindObjectsByType<FixedCamera>(FindObjectsSortMode.None).Length, "three cameras in the gym, one in the closet");
            Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<LockedDoor>(), "the locked door");
            Assert.IsNotNull(TutorialDrive.FindPickup("test_key"), "the test key");
            Assert.Greater(LightmapSettings.lightmaps.Length, 0, "the baked bounce (TutorialSetup.BakeTutorial)");
            Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<LookQualityApplier>(), "the LOOK option applies here");
            Assert.IsNull(LookDirector.Instance, "no look director: a game room");
        }

        [UnityTest]
        public IEnumerator TheMainMenu_HasTheTutorialButton_ThatOpensTheGym()
        {
            GameManager.Instance.LoadMainMenu();
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == MainMenu && GameManager.Instance.IsInMenu, 10f);
            yield return new WaitForSecondsRealtime(0.3f);
            Button tutorial = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .FirstOrDefault(b => b.GetComponentInChildren<Text>() != null && b.GetComponentInChildren<Text>().text == "TUTORIAL");
            Assert.IsNotNull(tutorial, "a TUTORIAL button in the main menu");
            tutorial.onClick.Invoke();
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == TutorialDirector.SceneName && GameManager.Instance.IsPlaying, 10f);
            Assert.AreEqual(TutorialDirector.SceneName, SceneManager.GetActiveScene().name, "the button opens the gym");
        }

        [UnityTest]
        public IEnumerator APlayer_WhoDoesEachThing_CompletesEveryStepInOrder()
        {
            var pad = new VirtualPad();
            GameInput.SetBotSource(pad);
            var driver = new GameObject("[PadDriver]").AddComponent<PadDriver>();
            driver.Pad = pad;
            try
            {
                TutorialDrive.Context context = TutorialDrive.Context.From(pad, director);
                for (int i = 0; i < ExpectedSteps.Length; i++)
                {
                    Assert.AreEqual(i, director.StepIndex, $"step {i + 1} ({ExpectedSteps[i]}) is on");
                    string id = director.Current.Id;
                    // Real time for every wait: a time scale of zero must fail the test, never hang the run.
                    float stepStart = Time.realtimeSinceStartup;
                    int attempts = 0;
                    Debug.Log($"[TutorialTests] {id} starts: timeScale={Time.timeScale} playing={GameManager.Instance.IsPlaying}");
                    while (director.StepIndex == i && !director.IsComplete && Time.realtimeSinceStartup - stepStart < TutorialDrive.StepTimeout)
                    {
                        attempts++;
                        yield return TutorialDrive.Drive(context, id);
                        float waitUntil = Time.realtimeSinceStartup + 1.5f;
                        while (director.StepIndex == i && !director.IsComplete && Time.realtimeSinceStartup < waitUntil) yield return null;
                    }
                    Assert.IsTrue(director.StepIndex != i || director.IsComplete, $"{id}: done by doing it ({attempts} attempts, {Time.realtimeSinceStartup - stepStart:0.0} s, timeScale {Time.timeScale}, playing {GameManager.Instance.IsPlaying})");
                    Debug.Log($"[TutorialTests] {id} done: {attempts} attempts, {Time.realtimeSinceStartup - stepStart:0.0} s");
                }
                Assert.IsTrue(director.IsComplete, "the demonstration is complete");
                yield return WaitUntil(() => GameManager.Instance.IsLevelComplete, 5f);
                Assert.IsTrue(GameManager.Instance.IsLevelComplete, "the level completes");
            }
            finally
            {
                GameInput.ClearBotSource(pad);
                UnityEngine.Object.Destroy(driver.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator EveryFloorPoint_IsInTheFrameOfItsCamera()
        {
            yield return null;
            CameraCoverage.Report report = CameraCoverage.Check(FixedCameraDirector.Instance, CameraCoverage.GameRoomNear, targetsOcclude: true);
            Debug.Log("[TutorialTests] " + report);
            Assert.Greater(report.Total, 50, "floor points");
            Assert.LessOrEqual(report.Uncovered.Count, report.Total * 0.03f, report.ToString());
        }

        /// <summary>Commits the pad once a frame, as the bots do.</summary>
        [DefaultExecutionOrder(-1000)]
        private sealed class PadDriver : MonoBehaviour
        {
            public VirtualPad Pad;
            private void Update() => Pad?.Commit();
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
