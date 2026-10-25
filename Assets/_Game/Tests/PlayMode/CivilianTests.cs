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
        public IEnumerator Sight_TheyBackAwaySlowly_FacingHer_AndTremble()
        {
            // Bruno, 2026-10-25: "initially, the civvies walk back slowly"; "they'll only run and cower if the player
            // kills one"; "a tremble to their animation".
            CivilianBrain lab = Find("Lab_A");
            Teleport(lab.transform.position + Away(lab) * 4f);
            yield return WaitUntil(() => lab.Alerted, 8f);
            Assert.IsTrue(lab.Alerted, "Lab_A sees her");
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(CivilianBrain.Phase.Wary, lab.Current, "it backs away, it does not run");
            float startGap = Gap(lab);
            Vector3 start = lab.transform.position;
            float maxSpeed = 0f;
            Vector3 last = start;
            float dot = 1f;
            float trembled = 0f;
            for (float t = 0f; t < 2.5f; t += Time.deltaTime)
            {
                yield return null;
                if (Time.deltaTime > 0f) maxSpeed = Mathf.Max(maxSpeed, Flat(last, lab.transform.position) / Mathf.Max(Time.deltaTime, 0.02f));
                last = lab.transform.position;
                Vector3 toHer = lucy.transform.position - lab.transform.position;
                toHer.y = 0f;
                dot = Mathf.Min(dot, Vector3.Dot(lab.transform.forward, toHer.normalized));
                trembled = Mathf.Max(trembled, lab.Tremble.LastDegrees);
            }
            Debug.Log($"[CivilianTests] Lab_A wary: gap {startGap:0.0} -> {Gap(lab):0.0} m, max speed {maxSpeed:0.00} m/s, min facing dot {dot:0.00}, tremble {trembled:0.0} deg");
            Assert.AreEqual(CivilianBrain.Phase.Wary, lab.Current, "still wary: no kill, no run");
            Assert.IsFalse(lab.Panicked, "no panic without a kill");
            Assert.Greater(Gap(lab), startGap + 0.8f, "it walked back from her");
            Assert.Less(Flat(start, lab.transform.position) / 2.5f, 2.2f, "slowly: a walk, not a run");
            Assert.Greater(dot, 0.3f, "it faces her while it backs away");
            Assert.Greater(trembled, 0.5f, "it trembles");
        }

        [UnityTest]
        public IEnumerator AKillInTheirSight_TheyRun_ThenCower()
        {
            // Bruno, 2026-10-25: "they'll only run and cower if the player kills one (guard, civvie doesn't matter)".
            CivilianBrain witness = Find("Lab_A");
            CivilianBrain victim = Find("Lab_B");
            Teleport(new Vector3(-2.5f, 0f, 0.5f));
            yield return WaitUntil(() => witness.Current == CivilianBrain.Phase.Wary, 8f);
            Assert.AreEqual(CivilianBrain.Phase.Wary, witness.Current, "wary first");
            float startGap = Gap(witness);
            Kill(victim);
            yield return WaitUntil(() => witness.Current == CivilianBrain.Phase.Flee, 1.5f);
            Assert.IsTrue(witness.Panicked, "the kill panics it");
            Assert.AreEqual(CivilianBrain.Phase.Flee, witness.Current, "it runs now");
            yield return WaitUntil(() => witness.Current == CivilianBrain.Phase.Cower, 12f);
            Debug.Log($"[CivilianTests] witness: {witness.PanicReason}, gap {startGap:0.0} -> {Gap(witness):0.0} m, tremble {witness.Tremble.LastDegrees:0.0}");
            Assert.AreEqual(CivilianBrain.Phase.Cower, witness.Current, "it reached its spot and cowers");
            Assert.Greater(Gap(witness), startGap + 2f, "it ran away from her");
            yield return new WaitForSeconds(0.8f); // the tremble eases in over half a second
            Assert.Greater(witness.Tremble.LastDegrees, 0.5f, "it trembles in its corner");
        }

        [UnityTest]
        public IEnumerator TheNurse_TendsABody_InHerSight()
        {
            // Bruno, 2026-10-25: "a nurse'll drag and cpr even if player on sight".
            guard.gameObject.SetActive(false);
            Find("Lab_B").gameObject.SetActive(false);
            CivilianBrain nurse = Find("Nurse_C");
            CivilianBrain victim = Find("Lab_A");
            Teleport(new Vector3(-2.5f, 0f, 0.5f));
            yield return new WaitForSeconds(0.5f);
            Kill(victim);
            CorpseCare care = nurse.GetComponent<CorpseCare>();
            yield return WaitUntil(() => care.Active, 6f);
            Assert.IsTrue(care.Active, "the nurse goes to the body");
            Assert.AreEqual(victim.transform, care.Patient, "the body of Lab_A");
            yield return WaitUntil(() => care.Current == CorpseCare.Step.Cpr || !care.Active, 25f);
            Vector3 eye = nurse.transform.position + Vector3.up * 1.6f;
            Vector3 her = lucy.transform.position + Vector3.up * GlorifiedBody.Characters.Body.ChestHeight;
            bool inSight = !Physics.Linecast(eye, her, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore);
            Debug.Log($"[CivilianTests] nurse care: {care.Current}, dragged {care.Dragged:0.0} m, in sight {inSight}, gap {Gap(nurse):0.0} m");
            Assert.AreEqual(CorpseCare.Step.Cpr, care.Current, "it dragged the body and does CPR");
            Assert.Greater(care.Dragged, 1f, "it dragged the body");
            Assert.IsTrue(inSight, "with her in plain sight");
        }

        [UnityTest]
        public IEnumerator TheLabWorker_TendsABody_OnlyWhenSheIsGone()
        {
            // Bruno, 2026-10-25: "a scientist will do this if player away same as the others".
            guard.gameObject.SetActive(false);
            Find("Nurse_C").gameObject.SetActive(false);
            CivilianBrain lab = Find("Lab_B");
            CivilianBrain victim = Find("Lab_A");
            Teleport(new Vector3(-2.5f, 0f, 0.5f));
            yield return new WaitForSeconds(0.5f);
            Kill(victim);
            CorpseCare care = lab.GetComponent<CorpseCare>();
            yield return new WaitForSeconds(5f);
            Debug.Log($"[CivilianTests] lab worker in her sight: phase {lab.Current}, care {care.Current}");
            Assert.IsFalse(care.Active, "no care while she is there");
            Hide();
            yield return WaitUntil(() => care.Active, 6f);
            Assert.IsTrue(care.Active, "she is gone: it goes to the body");
            Assert.AreEqual(victim.transform, care.Patient);
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
        public IEnumerator SheComesClose_TheyBeg_SheLeaves_TheyBackAway_OrCower()
        {
            CivilianBrain lab = Find("Lab_A");
            yield return WaitUntil(() => lab.Current == CivilianBrain.Phase.Wary, 8f);
            Assert.AreEqual(CivilianBrain.Phase.Wary, lab.Current, "wary first");
            int begs = lab.Begs;
            int barks = Bark.Count;
            Teleport(lab.transform.position + Away(lab) * 1.6f);
            yield return WaitUntil(() => lab.Current == CivilianBrain.Phase.Beg, 2f);
            Assert.AreEqual(CivilianBrain.Phase.Beg, lab.Current, "she is near: it begs");
            Assert.Greater(lab.Begs, begs);
            Assert.Greater(Bark.Count, barks, "a begging line");
            Teleport(lab.transform.position + Away(lab) * 8f);
            yield return WaitUntil(() => lab.Current != CivilianBrain.Phase.Beg, 2f);
            Assert.AreEqual(CivilianBrain.Phase.Wary, lab.Current, "she left, and nobody died: it backs away again");

            // After a panic the same walk ends in its corner.
            lab.Panic("test");
            yield return WaitUntil(() => lab.Current == CivilianBrain.Phase.Cower, 12f);
            Assert.AreEqual(CivilianBrain.Phase.Cower, lab.Current, "panicked: it runs and cowers");
            Teleport(lab.transform.position + Away(lab) * 1.6f);
            yield return WaitUntil(() => lab.Current == CivilianBrain.Phase.Beg, 2f);
            Assert.AreEqual(CivilianBrain.Phase.Beg, lab.Current, "she is near: it begs");
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
            Kill(victim);
            yield return WaitUntil(() => witness.Retreating, 2f);
            Debug.Log($"[CivilianTests] witness: {witness.AlertReason}, phase {witness.Current}, seen {witness.GetComponent<EnemyMorale>().Witnessed}");
            Assert.IsTrue(witness.Retreating, "the witness runs");
            Assert.GreaterOrEqual(witness.GetComponent<EnemyMorale>().Witnessed, 1, "it saw the death");
        }

        [UnityTest]
        public IEnumerator TheShort_HurtsACivilian()
        {
            // Bruno, 2026-10-25: "it should also dmg civilians". The shocks pick the nearest live target in 6 m.
            guard.gameObject.SetActive(false);
            CivilianBrain lab = Find("Lab_B");
            Teleport(lab.transform.position + Away(lab) * 2.5f);
            yield return null;
            ShortController shortController = lucy.GetComponent<ShortController>();
            shortController.Begin();
            yield return new WaitForSecondsRealtime(0.3f); // the first shock lands before the start's freeze
            Health victim = shortController.Victim;
            Debug.Log($"[CivilianTests] short victim: {(victim != null ? victim.name : "none")}");
            Assert.IsNotNull(victim, "the short found a victim");
            CivilianBrain civilian = victim.GetComponent<CivilianBrain>();
            Assert.IsNotNull(civilian, "the victim is a civilian");
            Assert.Less(victim.CurrentHealth, victim.MaxHealth, "the shocks hurt it");
            // Bruno, 2026-10-25: "the shock should also stun the victim".
            Assert.IsTrue(civilian.Stunned, "the shock stuns it");
            Assert.IsTrue(civilian.Tremble.Jolting, "its body jolts");
            shortController.End(byDrug: false);
        }

        [UnityTest]
        public IEnumerator AHurt_AlertsThem_OnlyAKillMakesThemRun()
        {
            // Bruno, 2026-10-25: "after a kill; the civvies will have low enough health that a direct hit will kill a
            // civvie anyway".
            guard.gameObject.SetActive(false);
            CivilianBrain lab = Find("Lab_A");
            Hide();
            yield return new WaitForSeconds(0.3f);
            lab.GetComponent<Health>().TakeDamage(5f);
            yield return new WaitForSeconds(0.5f);
            Debug.Log($"[CivilianTests] hurt: {lab.AlertReason}, phase {lab.Current}, panicked {lab.Panicked}");
            Assert.IsTrue(lab.Alerted, "the hurt alerts it");
            Assert.IsFalse(lab.Panicked, "a hurt is no kill: no panic");
            Assert.AreEqual(CivilianBrain.Phase.Wary, lab.Current, "it stays wary");
        }

        [UnityTest]
        public IEnumerator Wary_Standing_Startles_ThenTakesAFearStance()
        {
            // The RFab presets on the lab worker (2026-10-25): afraid once, then scared_01 or scared_02 in a loop.
            CivilianBrain lab = Find("Lab_A");
            Hide();
            yield return null;
            lab.Alert("test", callOthers: false);
            yield return WaitUntil(() => lab.CurrentPose == CivilianBrain.Pose.Startle, 2f);
            var clips = GlorifiedBody.Characters.PoseClips.Load();
            Assert.AreEqual(CivilianBrain.Pose.Startle, lab.CurrentPose, "a startle first");
            Assert.AreEqual(clips.afraid, lab.PoseOverride.Current, "the afraid clip");
            yield return new WaitForSeconds(clips.afraid.length + 0.5f);
            Debug.Log($"[CivilianTests] wary stance: {lab.CurrentPose}, clip {(lab.PoseOverride.Current != null ? lab.PoseOverride.Current.name : "none")}");
            Assert.That(lab.CurrentPose, Is.EqualTo(CivilianBrain.Pose.Shake).Or.EqualTo(CivilianBrain.Pose.Hug), "then a fear stance");
            Assert.IsTrue(lab.PoseOverride.Current == clips.scared01 || lab.PoseOverride.Current == clips.scared02, "a scared clip in a loop");
        }

        [UnityTest]
        public IEnumerator Panicked_TheyTakeDifferentPoses_OneLeansOnTheWall()
        {
            // Bruno, 2026-10-25: "the lean should be added; the multiple poses is to add more dynamicism to the
            // scene, to avoid multiple npcs of being in exact same pose".
            guard.gameObject.SetActive(false);
            CivilianBrain a = Find("Lab_A");
            CivilianBrain b = Find("Lab_B");
            a.SetVariant(1);
            b.SetVariant(2);
            Teleport(new Vector3(-2.5f, 0f, 0.5f));
            yield return new WaitForSeconds(0.5f);
            a.Panic("test");
            b.Panic("test");
            yield return WaitUntil(() => a.Current == CivilianBrain.Phase.Cower && b.Current == CivilianBrain.Phase.Cower, 15f);
            yield return new WaitForSeconds(1f);
            Debug.Log($"[CivilianTests] poses: {a.name} {a.Current}/{a.CurrentPose} lean {a.Lean.Weight:0.00} hips gap {a.Lean.LastHipsGap:0.00} m at {a.transform.position.x:0.0},{a.transform.position.z:0.0}; {b.name} {b.Current}/{b.CurrentPose}");
            Assert.AreEqual(CivilianBrain.Phase.Cower, a.Current);
            Assert.AreEqual(CivilianBrain.Pose.Lean, a.CurrentPose, "the lean variant: its back on the wall");
            Assert.AreEqual(1f, a.Lean.Weight, 0.01f, "fully leaning");
            Assert.Less(a.Lean.LastHipsGap, 0.3f, "the hips at the wall");
            Assert.AreEqual(CivilianBrain.Pose.Hug, b.CurrentPose, "the self-hug variant");
            Assert.AreEqual(GlorifiedBody.Characters.PoseClips.Load().scared02, b.PoseOverride.Current, "the scared_02 clip");
        }

        // ── Helpers ───────────────────────────────────────────────────

        private CivilianBrain Find(string name)
        {
            CivilianBrain c = civilians.FirstOrDefault(x => x.name == name);
            Assert.IsNotNull(c, name);
            return c;
        }

        private float Gap(CivilianBrain c) => Flat(c.transform.position, lucy.transform.position);

        /// <summary>The death as the game reports it (a blast from her); the room reacts through EnemyMorale.</summary>
        private void Kill(CivilianBrain victim)
        {
            Health h = victim.GetComponent<Health>();
            h.TakeDamage(h.MaxHealth * 10f);
            HitEventBus.Raise(new HitInfo { Attacker = lucy, Victim = h.gameObject, HitTransform = victim.transform, HitPoint = victim.transform.position + Vector3.up, DamageAmount = h.MaxHealth * 10f, DamageType = DamageType.PsychicBlast, WasFatal = true });
        }

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
