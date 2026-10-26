#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Automation;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;
using GlorifiedBody.Gore;
using GlorifiedBody.Player;
using GlorifiedBody.Saving;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// Bruno (2026-10-25): after a load the dead "stay on the floor; destruction and gore should be saved as well".
    /// In the AI TEST room: four kinds of death (a burst head on the death clip, a cut arm with its piece on the
    /// floor, a ragdoll knocked down then killed, the heavy with no ragdoll), the pools under them and a floor
    /// stain. A save, a load: each body lies where it lay, in its pose, with its stumps; the piece, the pools and the
    /// stain are back; no one sees the deaths again; her kills are the saved ones. The Gore option Off puts back the
    /// bodies only. Batch: tools/unity.sh test playmode SaveGoreTests (AiTest.unity is local: AiTestSetup.BuildAiTest).
    /// </summary>
    public class SaveGoreTests
    {
        private int savedLevel;

        private sealed class Snapshot
        {
            public Vector3 root, hips;
            public bool headGone, armGone;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SaveSystem.Folder = Path.Combine(Application.temporaryCachePath, "gb_save_gore_tests");
            if (Directory.Exists(SaveSystem.Folder)) Directory.Delete(SaveSystem.Folder, true);
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            savedLevel = SettingsManager.Instance.GoreLevel;
            SettingsManager.Instance.GoreLevel = 2;
            GameManager.Instance.LoadLevel("AiTest");
            yield return Room();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (SettingsManager.Instance != null) SettingsManager.Instance.GoreLevel = savedLevel;
            GruesomeDeathFx.ForcedLimb = HumanBodyBones.LastBone;
            SaveSystem.Folder = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheDead_LieWhereTheyLay_WithTheirGore_AfterALoad()
        {
            yield return KillFourWays();
            Dictionary<string, Snapshot> before = Snap();
            int pools = BodyBloodPool.Count, stains = FloorStains.Count, limbs = SeveredLimb.All.Count;
            Vector3 piece = SeveredLimb.All[0].transform.position;
            Quaternion pieceTurn = SeveredLimb.All[0].BoneTurn;
            Transform shield = Figure("Guard_B").GetComponent<EnemyShield>().Model;
            Vector3 shieldAt = shield.position;
            int kills = Stats().Kills;
            Debug.Log($"[SaveGoreTests] before the save: {pools} pools, {stains} stains, {limbs} limbs, {kills} kills, " + Describe(before));
            Assert.GreaterOrEqual(pools, 3, "the bodies bled");
            Assert.AreEqual(1, limbs, "one cut arm on the floor");
            Shots("before_save");

            yield return SaveAndLoad();
            Shots("after_load");

            Dictionary<string, Snapshot> after = Snap();
            Debug.Log($"[SaveGoreTests] after the load: {BodyBloodPool.Count} pools, {FloorStains.Count} stains, {SeveredLimb.All.Count} limbs, {Stats().Kills} kills, " + Describe(after));
            foreach (var pair in before)
            {
                Snapshot b = pair.Value, a = after[pair.Key];
                Assert.Less(Vector3.Distance(b.root, a.root), 0.05f, $"{pair.Key}: the root");
                Assert.Less(Vector3.Distance(b.hips, a.hips), 0.05f, $"{pair.Key}: the hips");
                Assert.AreEqual(b.headGone, a.headGone, $"{pair.Key}: the head");
                Assert.AreEqual(b.armGone, a.armGone, $"{pair.Key}: the arm");
            }
            Assert.IsTrue(after["Soldier_A"].headGone, "the burst head is still a stump");
            Assert.IsTrue(after["Gasser_D"].armGone, "the cut arm is still a stump");
            Assert.AreEqual(EnemyRagdoll.State.Corpse, Figure("Guard_B").GetComponent<EnemyRagdoll>().Current, "the ragdoll lies frozen");
            Assert.AreEqual(pools, BodyBloodPool.Count, "the pools");
            Assert.AreEqual(stains, FloorStains.Count, "the floor stains");
            Assert.AreEqual(limbs, SeveredLimb.All.Count, "the piece");
            Assert.Less(Vector3.Distance(piece, SeveredLimb.All[0].transform.position), 0.02f, "the piece lies where it lay");
            Assert.Less(Quaternion.Angle(pieceTurn, SeveredLimb.All[0].BoneTurn), 1f, "and turned as it lay (not standing up)");
            shield = Figure("Guard_B").GetComponent<EnemyShield>().Model;
            Assert.Less(Vector3.Distance(shieldAt, shield.position), 0.05f, "the dropped shield lies where it lay");
            Assert.IsFalse(UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)
                .Any(ps => ps.isEmitting && (ps.transform.root == Figure("Gasser_D").transform || ps.GetComponentInParent<SeveredLimb>() != null)), "the old gore does not gush again");
            Assert.AreEqual(kills, Stats().Kills, "her kills are the saved ones (each quiet death counts one, the load overwrites)");
            Assert.IsTrue(UnityEngine.Object.FindObjectsByType<EnemyMorale>(FindObjectsSortMode.None).All(m => m.Witnessed == 0), "no one saw the deaths again");

            // Nothing moves them later: no death clip over the pose, no pool grows, no one tends them.
            yield return new WaitForSeconds(2.5f);
            Dictionary<string, Snapshot> later = Snap();
            foreach (var pair in after)
                Assert.Less(Vector3.Distance(pair.Value.hips, later[pair.Key].hips), 0.05f, $"{pair.Key}: still");
            Assert.AreEqual(pools, BodyBloodPool.Count, "no new pool");
            Assert.Less(Vector3.Distance(shieldAt, shield.position), 0.05f, "the shield still lies there");
            Assert.IsTrue(UnityEngine.Object.FindObjectsByType<CorpseCare>(FindObjectsSortMode.None).All(c => !c.Active), "no care");
        }

        [UnityTest]
        public IEnumerator GoreOff_PutsBackTheBodies_ButNoBlood()
        {
            yield return KillFourWays();
            Dictionary<string, Snapshot> before = Snap();
            SettingsManager.Instance.GoreLevel = 0;
            yield return SaveAndLoad();
            Dictionary<string, Snapshot> after = Snap();
            Debug.Log($"[SaveGoreTests] gore off: {BodyBloodPool.Count} pools, {FloorStains.Count} stains, {SeveredLimb.All.Count} limbs, " + Describe(after));
            foreach (var pair in before)
                Assert.Less(Vector3.Distance(pair.Value.hips, after[pair.Key].hips), 0.05f, $"{pair.Key}: the body lies there");
            Assert.AreEqual(0, BodyBloodPool.Count, "no pool");
            Assert.AreEqual(0, FloorStains.Count, "no stain");
            Assert.AreEqual(0, SeveredLimb.All.Count, "no piece");
            Assert.IsFalse(after["Soldier_A"].headGone, "no stump");
        }

        // ── Helpers ───────────────────────────────────────────────────

        private static readonly string[] Names = { "Soldier_A", "Gasser_D", "Guard_B", "Armour_C" };

        /// <summary>The short bursts a head, a grenade cuts an arm, a knocked-down ragdoll dies, the heavy dies plainly.</summary>
        private static IEnumerator KillFourWays()
        {
            Kill(Figure("Soldier_A"), DamageType.ShortShock);
            GruesomeDeathFx.ForcedLimb = HumanBodyBones.LeftLowerArm;
            Kill(Figure("Gasser_D"), DamageType.Grenade);
            EnemyRagdoll ragdoll = Figure("Guard_B").GetComponent<EnemyRagdoll>();
            Assert.IsTrue(ragdoll.KnockDown(3f, Figure("Guard_B").transform.forward * -2f), "the guard goes down");
            yield return new WaitForSeconds(0.3f);
            Kill(Figure("Guard_B"), DamageType.PsychicBlast);
            Kill(Figure("Armour_C"), DamageType.PsychicBlast);
            // The death clips end, the ragdoll freezes, the piece settles, the pools grow (5 s after the still hips).
            yield return WaitUntil(() => ragdoll.Current == EnemyRagdoll.State.Corpse && SeveredLimb.All.All(l => l.Settled) && BodyBloodPool.Count >= 3, 15f);
            yield return new WaitForSeconds(6f);
            Material pool = BodyBloodPool.Pools[0].GetComponent<Renderer>().sharedMaterial;
            FloorStains.Lay(pool, new Vector3(0.5f, 0f, 0.5f), Vector3.up, 0.6f);
        }

        /// <summary>Shots for Bruno: the room from above, and the burst head and the cut arm up close.</summary>
        private static void Shots(string when)
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "glorified-body-renders", "game", "saves", "2026-10-25_gore");
            var obj = new GameObject("TestShotCamera");
            Camera cam = obj.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            void Shot(string name, Vector3 from, Vector3 at)
            {
                obj.transform.position = from;
                obj.transform.LookAt(at);
                ShotRenderer.Render(cam, Path.Combine(folder, $"{when}_{name}.png"));
            }
            Shot("room", new Vector3(-1f, 9f, -4f), new Vector3(-1f, 0f, 4f));
            foreach (string name in new[] { "Soldier_A", "Gasser_D", "Guard_B" })
            {
                Vector3 hips = Figure(name).GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Hips).position;
                Shot(name, hips + new Vector3(1.6f, 1.8f, -1.6f), hips);
            }
            UnityEngine.Object.Destroy(obj);
        }

        private static IEnumerator SaveAndLoad()
        {
            Assert.IsTrue(SaveSystem.SaveManual(1, ""), "saved");
            int applied = SaveSystem.Applies;
            Assert.IsTrue(SaveSystem.Load(1), "the load starts");
            yield return WaitUntil(() => SaveSystem.Applies > applied, 15f);
            Assert.Greater(SaveSystem.Applies, applied, "the load applied");
            yield return null;
            yield return null; // the stumps hold after the next LateUpdate
        }

        private static Dictionary<string, Snapshot> Snap()
        {
            var map = new Dictionary<string, Snapshot>();
            foreach (string name in Names)
            {
                GameObject figure = Figure(name);
                Assert.IsNotNull(figure, name + " is in the room");
                Assert.IsTrue(figure.activeInHierarchy, name + " lies there");
                Assert.IsTrue(figure.GetComponent<Health>().IsDead, name + " is dead");
                Animator animator = figure.GetComponentInChildren<Animator>();
                map[name] = new Snapshot
                {
                    root = figure.transform.position,
                    hips = animator.GetBoneTransform(HumanBodyBones.Hips).position,
                    headGone = Gone(animator, HumanBodyBones.Head),
                    armGone = Gone(animator, HumanBodyBones.LeftLowerArm),
                };
            }
            return map;
        }

        private static bool Gone(Animator animator, HumanBodyBones bone)
        {
            Transform t = animator.GetBoneTransform(bone);
            return t != null && t.localScale.x < 0.05f;
        }

        private static string Describe(Dictionary<string, Snapshot> map) =>
            string.Join("; ", map.Select(p => $"{p.Key} hips {p.Value.hips} head {(p.Value.headGone ? "gone" : "on")} arm {(p.Value.armGone ? "gone" : "on")}"));

        private static GameObject Figure(string name) => ScenePaths.Find(SceneManager.GetActiveScene(), name) ?? GameObject.Find(name);

        private static PsychicStats Stats() => GameObject.FindGameObjectWithTag("Player").GetComponentInChildren<PsychicStats>();

        private static void Kill(GameObject figure, DamageType type)
        {
            Health h = figure.GetComponent<Health>();
            float amount = h.MaxHealth * 10f;
            h.TakeDamage(amount);
            HitEventBus.Raise(new HitInfo
            {
                Victim = h.gameObject, HitTransform = figure.transform, HitPoint = figure.transform.position + Vector3.up * 1.2f,
                HitNormal = -figure.transform.forward, DamageAmount = amount, DamageType = type, Charge = 0f, WasFatal = true,
            });
        }

        private static IEnumerator Room()
        {
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "AiTest" && GameManager.Instance.IsPlaying, 15f);
            yield return null;
            GameObject.FindGameObjectWithTag("Player").GetComponentInChildren<Health>().SetInvulnerable(true);
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
