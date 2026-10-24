#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Characters;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.Destruction;
using GlorifiedBody.Enemy;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The breakable cover (Bruno, 2026-10-24): all of it is collateral damage; a chest-high barricade stops her
    /// powers until it breaks and the soldiers take cover behind it; enemy bullets wear it down; a blast breaks a
    /// table beside its target and the items fly; an explosion and a thrown body break what they reach; the
    /// short's surge pops the lamps (Realtime, with the fake bounce); a broken room stays broken when she leaves and
    /// comes back, and a restart puts it back. In the cover test room.
    /// Batch: tools/unity.sh test playmode BreakableTests.
    /// </summary>
    public class BreakableTests
    {
        private const string Scene = "CoverTest";
        private GameObject lucy;
        private PsychicPowers powers;
        private PsychicStats stats;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            yield return Load(Scene);
        }

        private IEnumerator Load(string scene)
        {
            GameManager.Instance.LoadLevel(scene);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == scene && GameManager.Instance.IsPlaying, 15f);
            yield return null;
            lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            powers = lucy.GetComponent<PsychicPowers>();
            stats = lucy.GetComponent<PsychicStats>();
            if (stats != null)
            {
                stats.RegenPaused = true;
                stats.DebugSet(45f, 0f); // no MK boost: the base numbers
            }
        }

        private static Breakable Piece(string id) => Breakable.All.FirstOrDefault(b => b.Id == id);
        private static SoldierBrain Soldier(string name) => UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).First(s => s.name == name);

        /// <summary>Puts a soldier on a floor point and holds him there (no walk, no fire) for the test.</summary>
        private static void Pin(SoldierBrain s, Vector3 floor)
        {
            NavMeshAgent agent = s.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled) agent.Warp(floor);
            else s.transform.position = floor;
            s.Hold(999f);
        }

        [UnityTest]
        public IEnumerator Barricade_StopsHerBlast_UntilItBreaks_ThenTheBlastReachesHim()
        {
            SoldierBrain rifleman = Soldier("Rifleman_A");
            Breakable barricade = Piece("Barricade_2"); // Barricade_3 stands on her line to the first one
            Assert.IsNotNull(barricade);
            Pin(rifleman, barricade.transform.position + Vector3.forward * 0.9f);
            yield return new WaitForSeconds(0.3f);
            LockOnTargetable target = rifleman.GetComponent<LockOnTargetable>();
            Vector3 eye = lucy.transform.position + Vector3.up * Body.ChestHeight;
            Assert.AreEqual(barricade, Breakables.CoverOnLine(eye, target.GetLockPoint(), out _), "the barricade stands on her line");
            Health hp = rifleman.GetComponent<Health>();
            float before = hp.CurrentHealth;
            powers.Select(PowerKind.Blast);
            int blasts = 0;
            while (!barricade.IsBroken && blasts < 8)
            {
                powers.FireAt(powers.Selected, 1f, target, 1f);
                blasts++;
                Assert.AreEqual(before, hp.CurrentHealth, $"blast {blasts}: the cover took it, not him");
                yield return null;
            }
            Debug.Log($"[BreakableTests] the barricade broke after {blasts} full blasts (no MK boost)");
            Assert.IsTrue(barricade.IsBroken, "the barricade breaks");
            Assert.AreEqual("cover", powers.LastResult);
            Assert.IsFalse(barricade.GetComponent<NavMeshObstacle>().enabled, "the broken barricade no longer cuts the NavMesh");
            powers.FireAt(powers.Selected, 1f, target, 1f);
            yield return null;
            Assert.Less(hp.CurrentHealth, before, "with the cover gone, the blast reaches him");
            yield return WaitUntil(() => barricade.Pieces.All(p => p.Settled), 8f);
            Assert.IsTrue(barricade.Pieces.All(p => p.Settled), "the pieces settle and stay");
            Assert.IsTrue(barricade.Pieces.All(p => p.gameObject.activeInHierarchy), "no piece vanishes");
        }

        [UnityTest]
        public IEnumerator FullBlast_BesideATable_BreaksIt_AndTheItemsFly()
        {
            SoldierBrain soldier = Soldier("Soldier_B");
            Breakable table = Piece("Table_2");
            Pin(soldier, table.transform.position + new Vector3(0f, 0f, 1f));
            yield return new WaitForSeconds(0.3f);
            LockOnTargetable target = soldier.GetComponent<LockOnTargetable>();
            Assert.IsNull(Breakables.CoverOnLine(lucy.transform.position + Vector3.up * Body.ChestHeight, target.GetLockPoint(), out _), "no cover on the line");
            Greeble monitor = table.Resting.First(g => g.Id.EndsWith("Monitor"));
            Vector3 monitorAt = monitor.transform.position;
            stats.DebugSet(100f, 0f); // the MK boost: double damage
            powers.Select(PowerKind.Blast);
            powers.FireAt(powers.Selected, 1f, target);
            yield return null;
            Assert.IsTrue(table.IsBroken, $"the splash broke the table beside him (health {table.Health:0})");
            yield return new WaitForSeconds(0.4f);
            Assert.Greater(Vector3.Distance(monitor.transform.position, monitorAt), 0.2f, "the monitor flew");
            yield return WaitUntil(() => table.Pieces.All(p => p.Settled) && table.Resting.All(g => !g.Moving), 8f);
            Assert.IsTrue(table.Resting.All(g => !g.Moving), "the items settle");
            Assert.Less(monitor.transform.position.y, 0.6f, "the monitor lies on the floor");
        }

        [UnityTest]
        public IEnumerator EnemyBullets_WearDownTheBarricade()
        {
            Breakable barricade = Piece("Barricade_3");
            float health = barricade.Health;
            Vector3 centre = barricade.Bounds.center;
            Vector3 from = centre + barricade.transform.forward * 4f;
            for (int i = 0; i < 3; i++)
            {
                EnemyProjectile.Launch(from, centre - from, 40f, 15f, null, "test");
                yield return new WaitForSeconds(0.2f);
            }
            Debug.Log($"[BreakableTests] three bullets: {health:0} -> {barricade.Health:0}");
            Assert.AreEqual(health - 45f, barricade.Health, 0.5f, "three bullets of 15");
        }

        [UnityTest]
        public IEnumerator Grenade_BreaksTheTable_ThePlaster_AndTheLampAbove()
        {
            Breakable table = Piece("Table_1");
            Vector3 floor = table.transform.position + Vector3.right * 0.8f;
            EnemyGrenade.Launch(floor + Vector3.up * 2f, floor, 0.3f, 0.2f, 30f, 3f, null, "test");
            yield return new WaitForSeconds(1.2f);
            Assert.IsTrue(table.IsBroken, $"the explosion broke the table (health {table.Health:0})");
            Assert.Greater(Greeble.TotalDisturbed, 0, "the items flew");
        }

        [UnityTest]
        public IEnumerator ThrownBody_BreaksThroughTheBarricade()
        {
            SoldierBrain thrown = Soldier("Soldier_B");
            SoldierBrain behind = Soldier("Rifleman_A");
            Breakable barricade = Piece("Barricade_2");
            Pin(behind, barricade.transform.position + Vector3.forward * 1f);
            Pin(thrown, barricade.transform.position + new Vector3(1.2f, 0f, -2.2f));
            yield return new WaitForSeconds(0.3f);
            powers.Select(PowerKind.Telekinesis);
            powers.FireAt(powers.Selected, 1f, thrown.GetComponent<LockOnTargetable>(), 1f);
            yield return new WaitForSeconds(1.2f);
            TelekinesisLift lift = thrown.GetComponent<TelekinesisLift>();
            Assert.IsNotNull(lift, "the lift holds him");
            lift.Launch(behind.transform.position, behind.GetComponent<Health>());
            yield return new WaitForSeconds(1.5f);
            Debug.Log($"[BreakableTests] the thrown body: barricade {barricade.Health:0}, broken={barricade.IsBroken}");
            Assert.IsTrue(barricade.IsBroken, "the thrown body broke the barricade");
        }

        [UnityTest]
        public IEnumerator TheShort_PopsTheLampsNearHer_TheirLightAndBounceGoOut()
        {
            BreakableLight[] lamps = Breakable.All.OfType<BreakableLight>().ToArray();
            Assert.AreEqual(6, lamps.Length, "six lamps");
            foreach (BreakableLight l in lamps)
            {
                Assert.IsTrue(l.Lamp.enabled && l.Bounce.enabled, $"{l.Id} is on");
                Assert.IsFalse(l.Lamp.bakingOutput.isBaked, $"{l.Id}: a live light, out of the bake");
                Assert.AreEqual(LightShadows.None, l.Bounce.shadows, "the bounce casts no shadow");
            }
            Vector3 head = lucy.transform.position + Vector3.up * 1.5f;
            int near = lamps.Count(l => Vector3.Distance(l.Bounds.ClosestPoint(head), head) <= 5f);
            Assert.Greater(near, 0, "a lamp near her");
            lucy.GetComponent<ShortController>().Begin();
            yield return new WaitForSeconds(0.6f);
            int dark = lamps.Count(l => l.IsDark);
            Debug.Log($"[BreakableTests] the surge: {dark} of {lamps.Length} lamps dark ({near} near her)");
            Assert.AreEqual(near, dark, "the lamps near her are out, the others on");
            foreach (BreakableLight l in lamps.Where(l => l.IsDark)) Assert.IsFalse(l.Lamp.enabled || l.Bounce.enabled, $"{l.Id}: the light and its bounce are out");
            lucy.GetComponent<ShortController>().End(true);
        }

        [UnityTest]
        public IEnumerator Plaster_ComesDown_AndLeavesTheBareWall()
        {
            Breakable plaster = Piece("Plaster_E1");
            Vector3 centre = plaster.Bounds.center;
            Vector3 from = centre + plaster.transform.forward * 3f;
            int shots = 0;
            while (!plaster.IsBroken && shots < 4)
            {
                EnemyProjectile.Launch(from, centre - from, 40f, 15f, null, "test");
                shots++;
                yield return new WaitForSeconds(0.2f);
            }
            Assert.IsTrue(plaster.IsBroken, "the plaster came down");
            Assert.IsTrue(plaster.BrokenRoot.activeInHierarchy, "the bare wall shows");
        }

        [UnityTest]
        public IEnumerator BrokenRoom_StaysBroken_WhenSheComesBack_ARestartPutsItBack()
        {
            Breakable table = Piece("Table_1");
            table.Break(table.Bounds.center + Vector3.up, Vector3.down);
            yield return WaitUntil(() => table.Pieces.All(p => p.Settled), 8f);
            Pose first = new Pose(table.Pieces[0].transform.position, table.Pieces[0].transform.rotation);
            yield return Load("TrainingRoom");
            yield return Load(Scene);
            table = Piece("Table_1");
            Debug.Log($"[BreakableTests] restored piece 0 at {table.Pieces[0].transform.position}, it fell at {first.position}");
            Assert.IsTrue(table.IsBroken, "she came back: the table is still broken");
            Assert.Less(Vector3.Distance(table.Pieces[0].transform.position, first.position), 0.01f, "its pieces lie where they fell");
            Assert.IsFalse(table.IntactRoot.activeSelf, "no intact table");

            Breakable other = Piece("Table_3");
            other.Break(other.Bounds.center + Vector3.up, Vector3.down);
            yield return Load(Scene); // a restart of the room
            Assert.IsFalse(Piece("Table_3").IsBroken, "a restart puts back what broke in this visit");
            Assert.IsTrue(Piece("Table_1").IsBroken, "what broke in an earlier visit stays broken");
        }

        [UnityTest]
        public IEnumerator Soldiers_TakeCover_BehindTheBarricades()
        {
            yield return WaitUntil(() => UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).Count(s => s.Cover != null) >= 1, 12f);
            SoldierBrain[] covered = UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).Where(s => s.Cover != null).ToArray();
            Assert.GreaterOrEqual(covered.Length, 1, "a soldier picks a barricade");
            SoldierBrain s0 = covered[0];
            yield return WaitUntil(() => Vector3.Distance(Flat(s0.transform.position), Flat(s0.Spot)) < 1f, 12f);
            Debug.Log($"[BreakableTests] {s0.name} spot {s0.Spot}, at {s0.transform.position}, phase {s0.Current}");
            Vector3 eye = lucy.transform.position + Vector3.up * Body.ChestHeight;
            Debug.Log($"[BreakableTests] {s0.name} behind {s0.Cover.Id} at {s0.transform.position}");
            Vector3 behind = Flat(s0.transform.position - s0.Cover.Bounds.center);
            Vector3 fromHer = Flat(s0.Cover.Bounds.center - lucy.transform.position);
            Assert.Less(behind.magnitude, 2f, "he stands at his barricade");
            Assert.Greater(Vector3.Dot(behind, fromHer), 0f, "on its far side from her");
            Assert.IsNotNull(Breakables.CoverOnLine(eye, s0.GetComponent<LockOnTargetable>().GetLockPoint(), out _), "cover stands between them");
            Assert.AreEqual(covered.Length, covered.Select(s => s.Cover).Distinct().Count(), "one soldier for each barricade");
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
