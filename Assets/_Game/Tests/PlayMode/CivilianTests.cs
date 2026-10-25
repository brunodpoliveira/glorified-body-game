#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The unarmed civilians in the civilian test room (Bruno, 2026-10-25; design §3.4 "unarmed civilians cower,
    /// run and beg", §11.5 "an unarmed civvie will always run/cower"): they see her and run, then cower; they never
    /// attack; their scream calls the guard; she walks up and they beg, she walks away and they cower; with no way
    /// out past her they beg at once; the auto-lock never picks one, the switch does; a colleague's death sends
    /// them running. Lucy takes no damage. The room is local only: build it with
    /// CivilianTestSetup.BuildCivilianTest. Batch: tools/unity.sh test playmode CivilianTests.
    /// </summary>
    public class CivilianTests
    {
        private const string SceneName = "CivilianTest";
        private GameObject lucy;
        private CivilianBrain[] civilians;
        private SoldierBrain guard;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            GameManager.Instance.LoadLevel(SceneName);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == SceneName && GameManager.Instance.IsPlaying, 10f);
            Assert.AreEqual(SceneName, SceneManager.GetActiveScene().name, "the civilian room (build it with CivilianTestSetup)");
            lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            civilians = UnityEngine.Object.FindObjectsByType<CivilianBrain>(FindObjectsSortMode.None).OrderBy(c => c.name).ToArray();
            guard = UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).FirstOrDefault();
        }

        /// <summary>
        /// The camera zone around her must not block a sight line: queries hit triggers in this project, and the
        /// zone on the Default layer blinded every enemy while she stood in it (found 2026-10-25).
        /// </summary>
        [UnityTest]
        public IEnumerator TheCameraZone_DoesNotBlockTheirSight()
        {
            yield return null;
            Vector3 her = lucy.transform.position + Vector3.up * 0.9f;
            Assert.IsTrue(UnityEngine.Object.FindObjectsByType<GlorifiedBody.Cameras.CameraZone>(FindObjectsSortMode.None).Any(z => z.Contains(her)), "she starts inside a camera zone");
            CivilianBrain lab = Find("Lab_B");
            Vector3 eye = lab.transform.position + Vector3.up * 1.5f;
            bool blocked = Physics.Linecast(eye, her, out RaycastHit hit, LayerMask.GetMask("Default"));
            Assert.IsFalse(blocked && hit.collider.GetComponent<GlorifiedBody.Cameras.CameraZone>() != null, "the zone is not in the sight line");
        }

        [UnityTest]
        public IEnumerator TheRoom_HasThreeCivilians_WithNoGun_AndOneGuard()
        {
            yield return null;
            Assert.AreEqual(3, civilians.Length, "two lab workers and a nurse");
            Assert.IsNotNull(guard, "the guard");
            foreach (CivilianBrain c in civilians)
            {
                Assert.IsNull(c.GetComponent<DummyShooter>(), c.name + " has no gun");
                Assert.IsTrue(c.GetComponent<LockOnTargetable>().IsCivilian, c.name + " is a civilian target");
                Assert.AreEqual(0, c.GetComponent<EnemyMorale>().FearLimit, c.name + " runs at the first fright");
            }
        }

        [UnityTest]
        public IEnumerator Sight_TheyRunFromHer_ThenCower()
        {
            CivilianBrain lab = Find("Lab_A");
            yield return WaitUntil(() => lab.Alerted, 8f);
            Assert.IsTrue(lab.Alerted, "Lab_A sees her");
            float startGap = Gap(lab);
            yield return WaitUntil(() => lab.Current == CivilianBrain.Phase.Cower, 12f);
            Debug.Log($"[CivilianTests] Lab_A: {lab.AlertReason}, gap {startGap:0.0} -> {Gap(lab):0.0} m, phase {lab.Current}");
            Assert.AreEqual(CivilianBrain.Phase.Cower, lab.Current, "it reached its spot and cowers");
            Assert.Greater(Gap(lab), startGap + 2f, "it ran away from her");
        }

        [UnityTest]
        public IEnumerator TheScream_CallsTheGuard_WhoCannotSeeHer()
        {
            yield return WaitUntil(() => guard.Alerted, 10f);
            Debug.Log($"[CivilianTests] guard: {guard.AlertReason}");
            Assert.IsTrue(guard.Alerted, "the guard knows");
            Assert.AreEqual("heard scream", guard.AlertReason, "the civilians' scream called him (he faces the wall)");
        }

        [UnityTest]
        public IEnumerator SheComesClose_TheyBeg_SheLeaves_TheyCower()
        {
            CivilianBrain lab = Find("Lab_A");
            yield return WaitUntil(() => lab.Current == CivilianBrain.Phase.Cower, 16f);
            Assert.AreEqual(CivilianBrain.Phase.Cower, lab.Current, "it cowers first");
            int begs = lab.Begs;
            int barks = Bark.Count;
            Teleport(lab.transform.position + Away(lab) * 1.6f);
            yield return WaitUntil(() => lab.Current == CivilianBrain.Phase.Beg, 2f);
            Assert.AreEqual(CivilianBrain.Phase.Beg, lab.Current, "she is near: it begs");
            Assert.Greater(lab.Begs, begs);
            Assert.Greater(Bark.Count, barks, "a begging line");
            Teleport(lab.transform.position + Away(lab) * 8f);
            yield return WaitUntil(() => lab.Current == CivilianBrain.Phase.Cower, 2f);
            Assert.AreEqual(CivilianBrain.Phase.Cower, lab.Current, "she left: it cowers again");
        }

        [UnityTest]
        public IEnumerator NoWayOutPastHer_TheNurseBegsAtOnce()
        {
            CivilianBrain nurse = Find("Nurse_C");
            Teleport(new Vector3(5.4f, 0f, 2.1f)); // the alcove's mouth (CivilianTestSetup.AlcoveMouth)
            yield return null;
            Vector3 start = nurse.transform.position;
            nurse.Alert("test", callOthers: false);
            yield return new WaitForSeconds(1.5f);
            Debug.Log($"[CivilianTests] nurse: phase {nurse.Current}, begs {nurse.Begs}, moved {Flat(start, nurse.transform.position):0.00} m");
            Assert.AreEqual(CivilianBrain.Phase.Beg, nurse.Current, "every way out passes her: it kneels and begs");
            Assert.Less(Flat(start, nurse.transform.position), 0.6f, "it did not run past her");
        }

        [UnityTest]
        public IEnumerator TheAutoLock_NeverPicksACivilian_TheSwitchDoes()
        {
            guard.gameObject.SetActive(false);
            LockOnController lockOn = lucy.GetComponent<LockOnController>();
            lockOn.SetTarget(null);
            Teleport(Find("Lab_B").transform.position + Vector3.back * 3f);
            for (int i = 0; i < 10; i++) yield return null;
            Assert.IsTrue(lockOn.CurrentTarget == null || !lockOn.CurrentTarget.IsCivilian, "the auto-lock skips civilians");
            Assert.IsTrue(lockOn.SwitchTarget(), "the switch finds a target");
            Assert.IsTrue(lockOn.CurrentTarget.IsCivilian, "the switch reaches a civilian: a kill is her choice");
            yield return null;
            Assert.IsTrue(lockOn.CurrentTarget != null && lockOn.CurrentTarget.IsCivilian, "the lock stays on the civilian");
        }

        [UnityTest]
        public IEnumerator AColleagueDies_TheWitnessRuns()
        {
            CivilianBrain witness = Find("Lab_B");
            CivilianBrain victim = Find("Lab_A");
            Hide();
            yield return new WaitForSeconds(0.3f);
            // The death as the game reports it, then the witness's reaction through EnemyMorale.
            Health h = victim.GetComponent<Health>();
            h.TakeDamage(h.MaxHealth * 10f);
            HitEventBus.Raise(new HitInfo { Victim = h.gameObject, HitTransform = victim.transform, HitPoint = victim.transform.position + Vector3.up, DamageAmount = h.MaxHealth * 10f, DamageType = DamageType.PsychicBlast, WasFatal = true });
            yield return WaitUntil(() => witness.Retreating, 2f);
            Debug.Log($"[CivilianTests] witness: {witness.AlertReason}, phase {witness.Current}, seen {witness.GetComponent<EnemyMorale>().Witnessed}");
            Assert.IsTrue(witness.Retreating, "the witness runs");
            Assert.GreaterOrEqual(witness.GetComponent<EnemyMorale>().Witnessed, 1, "it saw the death");
        }

        // ── Helpers ───────────────────────────────────────────────────

        private CivilianBrain Find(string name)
        {
            CivilianBrain c = civilians.FirstOrDefault(x => x.name == name);
            Assert.IsNotNull(c, name);
            return c;
        }

        private float Gap(CivilianBrain c) => Flat(c.transform.position, lucy.transform.position);

        /// <summary>From the civilian toward the middle of the room, flat: a clear side to stand on.</summary>
        private static Vector3 Away(CivilianBrain c)
        {
            Vector3 d = -c.transform.position;
            d.y = 0f;
            return d.sqrMagnitude > 0.01f ? d.normalized : Vector3.back;
        }

        private void Teleport(Vector3 at)
        {
            lucy.GetComponent<TankController>().Teleport(at, 0f);
        }

        private void Hide()
        {
            CharacterController cc = lucy.GetComponent<CharacterController>();
            cc.enabled = false;
            lucy.transform.position = new Vector3(0f, -30f, -5f);
            Physics.SyncTransforms();
            lucy.GetComponent<TankController>().enabled = false;
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
