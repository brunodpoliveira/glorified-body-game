#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Automation;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Saving;
using GlorifiedBody.Story;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The design sweep's rank 10 and the feed (Bruno, 2026-10-30: "the only form to regain hp is by feeding on fallen
    /// enemies; civilians give more hp back as usual"; "2 to be built and tested as well"), in the WARD TEST room (in
    /// git: WardTestSetup.BuildWardTest; the enemy prefabs are local: InvectorEnemyFactory.BuildEnemyPrefabs): the
    /// patient stays in her bed and says her line once; the scan reads Marta; her fate raises a flag that the room
    /// reads; the feed gives a body's HP once, stops on a hit, and the saves keep it.
    /// Batch: tools/unity.sh test playmode WardTests.
    /// </summary>
    public class WardTests
    {
        private const string Room = "WardTest";
        private const string Patient = "Patient_W1", Marta = "Marta", Lab = "Lab_W3", Guard = "Guard_W4";
        private GameObject lucy;
        private Health hers;
        private Interactor hands;
        private Feeding feeding;
        private VirtualPad pad;
        private PadDriver driver;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SaveSystem.Folder = Path.Combine(Application.temporaryCachePath, "gb_ward_tests");
            if (Directory.Exists(SaveSystem.Folder)) Directory.Delete(SaveSystem.Folder, true);
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            GameManager.Instance.LoadLevel(Room);
            yield return InRoom();
            pad = new VirtualPad();
            GameInput.SetBotSource(pad);
            driver = new GameObject("[PadDriver]").AddComponent<PadDriver>();
            UnityEngine.Object.DontDestroyOnLoad(driver.gameObject);
            driver.Pad = pad;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameInput.ClearBotSource(pad);
            if (driver != null) UnityEngine.Object.Destroy(driver.gameObject);
            SaveSystem.Folder = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheMarkers_BecomeThePatient_Marta_AndBodiesToFeedOn()
        {
            yield return null;
            Assert.AreEqual(0, EnemySpawn.Missing, "every prefab is there (InvectorEnemyFactory.BuildEnemyPrefabs)");
            Assert.IsNotNull(GameObject.Find(Patient).GetComponent<PatientBrain>(), "the patient stays put");
            Assert.IsNull(GameObject.Find(Patient).GetComponent<CivilianBrain>(), "she never runs");
            FateFlag fate = GameObject.Find(Marta).GetComponent<FateFlag>();
            Assert.IsNotNull(fate, "Marta's fate");
            Assert.AreEqual("marta", fate.fate);
            Scannable scan = GameObject.Find(Marta).GetComponent<Scannable>();
            Assert.IsNotNull(scan, "a scan on Marta");
            Assert.IsNotNull(scan.image, "her picture");
            Assert.AreEqual("marta_scanned", scan.raiseFlag);
            Transform chest = GameObject.Find(Marta).GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Chest);
            Assert.AreSame(chest, scan.focus, "the scan reads her chest, so it follows her");
            foreach (string name in new[] { Patient, Marta, Lab, Guard })
                Assert.IsNotNull(GameObject.Find(name).GetComponent<FeedableBody>(), name + " feeds her once dead");
            Assert.IsFalse(GameObject.Find("Gate_MartaKilled").GetComponent<FlagGate>().Showing, "the mark hides while Marta lives");
            Assert.IsNotNull(lucy.GetComponent<Feeding>(), "Lucy can feed");
        }

        [UnityTest]
        public IEnumerator ThePatient_StaysInHerBed_SaysHerLineOnce_AndShakesAtADeath()
        {
            Remove(Guard);
            GameObject patient = GameObject.Find(Patient);
            PatientBrain brain = patient.GetComponent<PatientBrain>();
            yield return new WaitForSeconds(0.5f);
            Vector3 bed = patient.transform.position;
            Assert.Greater(bed.y, 0.4f, "on the bed, not on the floor");
            Assert.IsFalse(brain.Spoke, "no line while she is far");

            int barks = Bark.Count;
            Teleport(new Vector3(bed.x, 0f, bed.z - 1.6f), 0f); // south of the bed head, outside the bed
            yield return WaitUntil(() => brain.Spoke, 2f);
            Assert.IsTrue(brain.Spoke, "she speaks when Lucy comes near");
            Assert.AreEqual(barks + 1, Bark.Count, "one line");
            Teleport(new Vector3(0f, 0f, -3f), 0f);
            yield return new WaitForSeconds(0.3f);
            Teleport(new Vector3(bed.x, 0f, bed.z - 1.6f), 0f); // south of the bed head, outside the bed
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(barks + 1, Bark.Count, "the line comes once");

            Kill(Marta);
            yield return new WaitForSeconds(2.5f);
            Assert.IsTrue(brain.Frightened, "she saw the death");
            Assert.IsFalse(brain.Retreating, "she cannot run");
            float shake = 0f;
            for (int i = 0; i < 20; i++) { yield return null; shake = Mathf.Max(shake, patient.GetComponent<Tremble>().LastDegrees); }
            Assert.Greater(shake, 1f, "she shakes in her bed");
            Assert.Less(Vector3.Distance(bed, patient.transform.position), 0.1f, "she never left the bed");
        }

        [UnityTest]
        public IEnumerator TheScan_ReadsMarta_AndRaisesTheFlag()
        {
            Remove(Guard);
            GameObject marta = GameObject.Find(Marta);
            marta.GetComponent<CivilianBrain>().enabled = false; // she stands for the scan
            Vector3 front = marta.transform.position + marta.transform.forward * 1.1f;
            Teleport(new Vector3(front.x, 0f, front.z), Quaternion.LookRotation(marta.transform.position - front).eulerAngles.y);
            yield return null;
            PsychicScanner scanner = lucy.GetComponent<PsychicScanner>();
            Assert.AreSame(marta.GetComponent<Scannable>(), scanner.FindFocus(), "Marta is in reach and in front of her");
            yield return Press(InputManager.GameAction.Scan);
            Assert.IsTrue(scanner.IsViewing, "the scan reads Marta");
            Assert.AreEqual("marta", scanner.Viewing.scanId);
            Assert.IsTrue(StoryFlags.Has("marta_scanned"), "the game remembers the scan");
            yield return new WaitForSecondsRealtime(PsychicScanner.MinViewSeconds + 0.1f);
            yield return Press(InputManager.GameAction.Scan);
            Assert.IsFalse(scanner.IsViewing);
        }

        [UnityTest]
        public IEnumerator KillingMarta_RaisesKilled_TheMarkShows_AndASaveKeepsIt()
        {
            Kill(Marta);
            yield return null;
            Assert.IsTrue(StoryFlags.Has("marta_killed"));
            Assert.IsFalse(StoryFlags.Has("marta_spared"));
            Assert.IsTrue(GameObject.Find("Gate_MartaKilled").GetComponent<FlagGate>().Showing, "the room shows what she did");
            yield return new WaitForSeconds(1.5f);
            yield return SaveAndLoad(1);
            Assert.IsTrue(StoryFlags.Has("marta_killed"), "the save keeps her fate");
            Assert.IsTrue(GameObject.Find("Gate_MartaKilled").GetComponent<FlagGate>().Showing, "and the mark");
        }

        [UnityTest]
        public IEnumerator LeavingWithMartaAlive_RaisesSpared()
        {
            Remove(Guard);
            int arrivals = SaveSystem.Arrivals;
            Assert.IsTrue(GameObject.Find("Exit_WardWest").GetComponent<RoomExit>().Use());
            yield return WaitUntil(() => SaveSystem.Arrivals > arrivals, 10f);
            Assert.AreEqual("TravelTest_B", SceneManager.GetActiveScene().name);
            Assert.IsTrue(StoryFlags.Has("marta_spared"), "she left Marta alive");
            Assert.IsFalse(StoryFlags.Has("marta_killed"));
        }

        [UnityTest]
        public IEnumerator TheFeed_GivesTheBodysHp_Once_CiviliansMore()
        {
            Remove(Marta); // the nurse would tend the bodies
            Kill(Guard);
            Kill(Lab);
            yield return new WaitForSeconds(1.5f);
            hers.SetHealth(30f);
            FeedableBody lab = GameObject.Find(Lab).GetComponent<FeedableBody>();
            FeedableBody guard = GameObject.Find(Guard).GetComponent<FeedableBody>();
            Assert.AreEqual(Feeding.ValueOf(EnemyMorale.Rank.UnarmedCivilian), lab.Total, 0.01f);
            Assert.AreEqual(Feeding.ValueOf(EnemyMorale.Rank.Guard), guard.Total, 0.01f);
            Assert.Greater(lab.Total, guard.Total, "a civilian gives more (Bruno)");

            Teleport(Beside(lab), 0f);
            Assert.AreEqual(InteractKind.Ask, lab.KindFor(hands), "it asks first");
            Assert.AreEqual("Feed on the body?", lab.TextFor(hands));
            Assert.AreEqual("feed:" + Lab, lab.Confirm(hands));
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(feeding.IsFeeding, "she feeds");
            Assert.IsTrue(lucy.GetComponent<TankController>().MovementLocked, "she stands still");
            Animator animator = lucy.GetComponentInChildren<Animator>();
            Assert.IsTrue(animator.GetBool("Feeding"), "she crouches at the body (the animator's Feeding)");
            yield return WaitUntil(() => !feeding.IsFeeding, Feeding.FeedSeconds + 2f);
            Debug.Log($"[WardTests] the lab worker gave {feeding.LastGain:0.0} HP ({feeding.LastEnd}); HP {hers.CurrentHealth:0.0}");
            Assert.AreEqual(30f + lab.Total, hers.CurrentHealth, 0.5f, "the lab worker's HP");
            Assert.AreEqual("done", feeding.LastEnd);
            Assert.IsFalse(lucy.GetComponent<TankController>().MovementLocked, "her legs are free again");
            Assert.IsFalse(lab.Available, "a body feeds her once");

            Teleport(Beside(guard), 0f);
            Assert.AreEqual("feed:" + Guard, guard.Confirm(hands));
            yield return WaitUntil(() => !feeding.IsFeeding, Feeding.FeedSeconds + 2f);
            Assert.AreEqual(30f + lab.Total + guard.Total, hers.CurrentHealth, 0.5f, "the guard's HP");
        }

        [UnityTest]
        public IEnumerator AHit_StopsTheFeed_SheKeepsWhatSheTook_AndTheBodyTheRest()
        {
            Remove(Marta);
            Remove(Guard);
            Kill(Lab);
            yield return new WaitForSeconds(1.5f);
            hers.SetHealth(20f);
            FeedableBody lab = GameObject.Find(Lab).GetComponent<FeedableBody>();
            Teleport(Beside(lab), 0f);
            Assert.AreEqual("feed:" + Lab, lab.Confirm(hands));
            yield return new WaitForSeconds(Feeding.FeedSeconds * 0.4f);
            HitEventBus.Raise(new HitInfo { Attacker = GameObject.Find(Guard), Victim = lucy, HitTransform = lucy.transform, HitPoint = lucy.transform.position + Vector3.up, DamageAmount = 5f, DamageType = DamageType.EnemyProjectile });
            yield return null;
            Assert.IsFalse(feeding.IsFeeding, "a hit stops the feed");
            Assert.AreEqual("hit", feeding.LastEnd);
            float got = feeding.LastGain;
            Debug.Log($"[WardTests] the hit stopped the feed after {got:0.0} HP; the body keeps {lab.Left:0.0}");
            Assert.That(got, Is.InRange(lab.Total * 0.25f, lab.Total * 0.6f), "she keeps what she took");
            Assert.AreEqual(lab.Total - got, lab.Left, 0.5f, "the body keeps the rest");
            Assert.IsTrue(lab.Available, "she can feed on it again");
            Assert.AreEqual("feed:" + Lab, lab.Confirm(hands));
            yield return WaitUntil(() => !feeding.IsFeeding, Feeding.FeedSeconds + 2f);
            Assert.AreEqual(20f + lab.Total, hers.CurrentHealth, 0.5f, "the whole body, in two feeds");
        }

        [UnityTest]
        public IEnumerator AFeed_FrightensTheOnesWhoSeeIt_AndTheBodyBleeds()
        {
            // Bruno, 2026-10-30: "a crouch + sound + blood effects for now"; witnesses "should react to it".
            Kill(Lab);
            yield return new WaitForSeconds(1.5f);
            EnemyMorale marta = GameObject.Find(Marta).GetComponent<EnemyMorale>();
            EnemyMorale guard = GameObject.Find(Guard).GetComponent<EnemyMorale>();
            int martaSaw = marta.FeedsSeen, guardSaw = guard.FeedsSeen, guardFear = guard.Fear;
            hers.SetHealth(30f);
            FeedableBody lab = GameObject.Find(Lab).GetComponent<FeedableBody>();
            Teleport(Beside(lab), 0f);
            int barks = Bark.Count;
            Assert.AreEqual("feed:" + Lab, lab.Confirm(hands));
            yield return null;
            Debug.Log($"[WardTests] the feed seen: Marta {marta.FeedsSeen - martaSaw}, the guard {guard.FeedsSeen - guardSaw} (fear {guardFear} -> {guard.Fear}), lines {Bark.Count - barks}");
            Assert.AreEqual(martaSaw + 1, marta.FeedsSeen, "Marta sees the feed");
            Assert.IsTrue(GameObject.Find(Marta).GetComponent<CivilianBrain>().Panicked, "a civilian runs from it");
            Assert.AreEqual(guardSaw + 1, guard.FeedsSeen, "the guard sees it");
            Assert.AreEqual(guardFear + 2, guard.Fear, "a feed is a gruesome sight: +2 fear");
            Assert.GreaterOrEqual(Bark.Count - barks, 2, "each says a line");
            yield return new WaitForSeconds(Feeding.BloodSeconds * 2f + 0.1f);
            if (GlorifiedBody.Gore.ThatIndividual.ThatIndividualGoreDriver.Instance != null)
                Assert.GreaterOrEqual(feeding.BloodSprays, 2, "the body bleeds while she feeds");
        }

        [UnityTest]
        public IEnumerator AtFullHp_NoFeed_OnlyAMessage()
        {
            Remove(Marta);
            Remove(Guard);
            Kill(Lab);
            yield return new WaitForSeconds(1.5f);
            FeedableBody lab = GameObject.Find(Lab).GetComponent<FeedableBody>();
            Teleport(Beside(lab), 0f);
            Assert.AreEqual(hers.MaxHealth, hers.CurrentHealth, 0.01f, "full HP");
            Assert.AreEqual(InteractKind.Message, lab.KindFor(hands));
            Assert.AreEqual("She is not hurt.", lab.TextFor(hands));
            Assert.IsFalse(feeding.Begin(lab), "no feed");
            Assert.AreEqual(0f, lab.Taken, 0.001f, "the body keeps all");
        }

        [UnityTest]
        public IEnumerator TheFeed_SurvivesASaveAndALoad()
        {
            Remove(Marta);
            Kill(Guard);
            Kill(Lab);
            yield return new WaitForSeconds(1.5f);
            hers.SetHealth(30f);
            FeedableBody lab = GameObject.Find(Lab).GetComponent<FeedableBody>();
            Teleport(Beside(lab), 0f);
            lab.Confirm(hands);
            yield return WaitUntil(() => !feeding.IsFeeding, Feeding.FeedSeconds + 2f);
            FeedableBody guard = GameObject.Find(Guard).GetComponent<FeedableBody>();
            Teleport(Beside(guard), 0f);
            guard.Confirm(hands);
            yield return new WaitForSeconds(Feeding.FeedSeconds * 0.5f);
            HitEventBus.Raise(new HitInfo { Victim = lucy, HitTransform = lucy.transform, HitPoint = lucy.transform.position, DamageAmount = 1f, DamageType = DamageType.EnemyProjectile });
            yield return null;
            float guardTaken = guard.Taken;
            float hp = hers.CurrentHealth;
            Assert.Greater(guardTaken, 1f, "a part of the guard");

            yield return SaveAndLoad(1);
            FeedableBody lab2 = GameObject.Find(Lab).GetComponent<FeedableBody>();
            FeedableBody guard2 = GameObject.Find(Guard).GetComponent<FeedableBody>();
            Assert.IsTrue(lab2.IsDead && guard2.IsDead, "the bodies are back");
            Assert.AreEqual(lab2.Total, lab2.Taken, 0.5f, "the lab worker stays fed on");
            Assert.IsFalse(lab2.Available, "no second feed after a load");
            Assert.AreEqual(guardTaken, guard2.Taken, 0.5f, "the guard keeps the rest");
            Assert.AreEqual(hp, hers.CurrentHealth, 0.5f, "her HP as saved");
        }

        // ── Helpers ───────────────────────────────────────────────────

        private Vector3 Beside(FeedableBody body)
        {
            Vector3 p = body.Point;
            return new Vector3(p.x, 0f, p.z - 0.9f);
        }

        private void Teleport(Vector3 at, float yaw)
        {
            TankController tank = lucy.GetComponent<TankController>();
            if (tank != null) tank.Teleport(at, yaw);
            else lucy.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
        }

        private IEnumerator SaveAndLoad(int slot)
        {
            SavePoint point = UnityEngine.Object.FindFirstObjectByType<SavePoint>();
            Assert.IsTrue(SaveSystem.SaveManual(slot, point.SpotId));
            int applied = SaveSystem.Applies;
            Assert.IsTrue(SaveSystem.Load(slot));
            yield return WaitUntil(() => SaveSystem.Applies > applied, 10f);
            yield return InRoom();
        }

        private void Kill(string name)
        {
            Health h = GameObject.Find(name).GetComponent<Health>();
            h.TakeDamage(h.MaxHealth * 10f);
            HitEventBus.Raise(new HitInfo { Attacker = lucy, Victim = h.gameObject, HitTransform = h.transform, HitPoint = h.transform.position + Vector3.up, DamageAmount = 999f, DamageType = DamageType.PsychicBlast, WasFatal = true });
        }

        /// <summary>Out of the test: the figure goes away (no shot, no care, no witness).</summary>
        private static void Remove(string name) => GameObject.Find(name).SetActive(false);

        private IEnumerator Press(InputManager.GameAction action)
        {
            pad.Tap(action);
            yield return null;
            yield return null;
            yield return null;
        }

        private IEnumerator InRoom()
        {
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == Room && GameManager.Instance.IsPlaying, 10f);
            yield return null;
            Assert.AreEqual(Room, SceneManager.GetActiveScene().name, "the ward test room (WardTestSetup.BuildWardTest)");
            lucy = GameObject.FindGameObjectWithTag("Player");
            hers = lucy.GetComponentInChildren<Health>();
            hers.SetInvulnerable(true);
            hands = lucy.GetComponent<Interactor>();
            feeding = lucy.GetComponent<Feeding>();
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
