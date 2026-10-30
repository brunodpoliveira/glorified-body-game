#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Saving;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The design sweep's rank 6 (2026-10-27, Bruno: "let's start with 6"), in the LAB TEST room (in git:
    /// LabTestSetup.BuildLabTest; the enemy prefabs are local: InvectorEnemyFactory.BuildEnemyPrefabs): the knife lab
    /// worker runs at her and only strikes, the lab worker with a pistol holds it in both hands, shakes and can miss,
    /// both retreat at a fear of 3, and a figure that carries a key card drops it beside its body, through saves too.
    /// Batch: tools/unity.sh test playmode ArmedCivilianTests.
    /// </summary>
    public class ArmedCivilianTests
    {
        private const string Room = "LabTest";
        private const string Knife = "Lab_Knife", Carrier = "Lab_Gun1", Gunman = "Lab_Gun2", Drop = "Drop_LabCard", Door = "Door_Store";
        private GameObject lucy;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SaveSystem.Folder = Path.Combine(Application.temporaryCachePath, "gb_lab_tests");
            if (Directory.Exists(SaveSystem.Folder)) Directory.Delete(SaveSystem.Folder, true);
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            GameManager.Instance.LoadLevel(Room);
            yield return InRoom();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SaveSystem.Folder = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheMarkers_BecomeArmedLabWorkers()
        {
            yield return null;
            Assert.AreEqual(0, EnemySpawn.Missing, "every prefab is there (InvectorEnemyFactory.BuildEnemyPrefabs)");
            foreach (string name in new[] { Knife, Carrier, Gunman })
            {
                GameObject figure = GameObject.Find(name);
                Assert.IsNotNull(figure, name);
                Assert.IsNotNull(figure.GetComponent<SoldierBrain>(), name + " fights");
                EnemyMorale morale = figure.GetComponent<EnemyMorale>();
                Assert.AreEqual(EnemyMorale.Rank.ArmedCivilian, morale.RankOf, name);
                Assert.AreEqual(3, morale.FearLimit, name + " retreats at a fear of 3 (design §11.5)");
                Assert.IsFalse(figure.GetComponent<LockOnTargetable>().IsCivilian, name + " is a threat: the auto-lock takes it");
                Assert.IsNotNull(figure.GetComponent<Tremble>(), name + " has nerves");
            }
            DummyShooter knife = GameObject.Find(Knife).GetComponent<DummyShooter>();
            Assert.AreEqual(DummyShooter.Weapon.Knife, knife.WeaponKind);
            Assert.IsTrue(knife.CanMelee);
            Assert.AreEqual("labworker_knife", knife.EnemyId);
            Assert.IsTrue(GameObject.Find(Knife).GetComponent<SoldierBrain>().Rusher, "a knife runs at her");
            Assert.IsNotNull(GameObject.Find(Knife).GetComponent<EnemyGun>().Model, "the knife model in the hand");

            DummyShooter pistol = GameObject.Find(Gunman).GetComponent<DummyShooter>();
            Assert.AreEqual(DummyShooter.Weapon.Pistol, pistol.WeaponKind);
            Assert.IsFalse(pistol.CanMelee, "a lab worker with a gun does not fight hand to hand");
            Assert.AreEqual(0.35f, pistol.AimSpread, 0.001f);
            Assert.AreEqual("labworker_pistol", pistol.EnemyId);
            EnemyGun hold = GameObject.Find(Gunman).GetComponent<EnemyGun>();
            Assert.IsTrue(hold.TwoHanded, "both hands on the pistol");
            Assert.Greater(hold.ShakeDegrees, 0f, "the gun shakes");

            ItemCarrier carrier = GameObject.Find(Carrier).GetComponent<ItemCarrier>();
            Assert.IsNotNull(carrier, "the carrier carries");
            Assert.IsNotNull(carrier.Drop, "its item");
            Assert.IsFalse(carrier.Drop.gameObject.activeSelf, "the card is hidden while he lives");
            Assert.IsNull(GameObject.Find(Knife).GetComponent<ItemCarrier>(), "only the carrier carries");
        }

        [UnityTest]
        public IEnumerator TheKnife_RunsAtHer_AndOnlyStrikes()
        {
            Remove(Carrier);
            Remove(Gunman);
            Health her = lucy.GetComponentInChildren<Health>();
            her.SetInvulnerable(false);
            GlorifiedBody.UI.DeathMenuUI.LastKillerId = "";
            GameObject figure = GameObject.Find(Knife);
            DummyShooter knife = figure.GetComponent<DummyShooter>();
            float start = Flat(figure.transform.position, lucy.transform.position);
            figure.GetComponent<SoldierBrain>().Alert("test", callOthers: false);
            yield return WaitUntil(() => knife.MeleeStrikes >= 1, 20f);
            Debug.Log($"[ArmedCivilianTests] the knife: start {start:0.0} m, now {Flat(figure.transform.position, lucy.transform.position):0.0} m, strikes {knife.MeleeStrikes}, hits {knife.MeleeHits}, rounds {knife.RoundsFired}");
            Assert.Greater(start, 8f, "it starts across the room");
            Assert.GreaterOrEqual(knife.MeleeStrikes, 1, "it came to her and struck");
            Assert.AreEqual(0, knife.RoundsFired, "a knife never shoots");
            Assert.GreaterOrEqual(knife.MeleeHits, 1, "she stood still: the strike lands");
            Assert.AreEqual("labworker_knife", GlorifiedBody.UI.DeathMenuUI.LastKillerId, "the death screen knows the knife");
            Assert.AreEqual(0, figure.GetComponent<SoldierBrain>().MovedWhileShootingFrames, "no step during the strike's telegraph");
        }

        [UnityTest]
        public IEnumerator TheKnife_FindsHerAgain_WhenSheMoves()
        {
            Remove(Carrier);
            Remove(Gunman);
            GameObject figure = GameObject.Find(Knife);
            figure.GetComponent<SoldierBrain>().Alert("test", callOthers: false);
            yield return WaitUntil(() => Flat(figure.transform.position, lucy.transform.position) < 4f, 15f);
            Assert.Less(Flat(figure.transform.position, lucy.transform.position), 4f, "it closes in");
            // She goes to the far corner of the hall.
            lucy.GetComponent<TankController>().Teleport(new Vector3(7f, 0f, -5.5f), 0f);
            float far = Flat(figure.transform.position, lucy.transform.position);
            yield return WaitUntil(() => Flat(figure.transform.position, lucy.transform.position) <= 2.2f, 10f);
            Debug.Log($"[ArmedCivilianTests] she moved {far:0.0} m away; the knife is {Flat(figure.transform.position, lucy.transform.position):0.0} m from her");
            Assert.Greater(far, 6f, "she moved far");
            Assert.LessOrEqual(Flat(figure.transform.position, lucy.transform.position), 2.2f, "it found her again");
        }

        [UnityTest]
        public IEnumerator TheGunman_ShakesInBothHands_AndShootsWide()
        {
            Remove(Knife);
            Remove(Carrier);
            GameObject figure = GameObject.Find(Gunman);
            DummyShooter pistol = figure.GetComponent<DummyShooter>();
            EnemyGun hold = figure.GetComponent<EnemyGun>();
            Tremble tremble = figure.GetComponent<Tremble>();
            Animator animator = figure.GetComponent<Animator>();
            Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            figure.GetComponent<SoldierBrain>().Alert("test", callOthers: false);
            float maxShake = 0f, maxTremble = 0f, maxHands = 0f, maxMiss = 0f;
            int shots = 0;
            float t0 = Time.time;
            while (pistol.RoundsFired < 2 && Time.time - t0 < 25f)
            {
                yield return null;
                maxShake = Mathf.Max(maxShake, hold.LastShake);
                maxTremble = Mathf.Max(maxTremble, tremble.LastDegrees);
                if (pistol.HoldsSlot) maxHands = Mathf.Max(maxHands, Vector3.Distance(leftHand.position, rightHand.position));
                if (pistol.RoundsFired > shots)
                {
                    shots = pistol.RoundsFired;
                    maxMiss = Mathf.Max(maxMiss, pistol.LastMissDistance);
                }
            }
            Debug.Log($"[ArmedCivilianTests] the gunman: rounds {pistol.RoundsFired}, gun shake {maxShake:0.0} deg, tremble {maxTremble:0.0} deg, hands apart {maxHands:0.00} m, worst miss {maxMiss:0.00} m");
            Assert.GreaterOrEqual(pistol.RoundsFired, 2, "it shoots");
            Assert.Greater(maxShake, 0.5f, "the gun shakes");
            Assert.Greater(maxTremble, 0.2f, "the body trembles while it hunts her");
            Assert.Greater(maxHands, 0f, "the aim was measured");
            Assert.Less(maxHands, 0.25f, "both hands on the pistol");
            Assert.Greater(maxMiss, 0f, "the lock lands off her chest");
            Assert.LessOrEqual(maxMiss, 0.351f, "but no more than the spread");
        }

        [UnityTest]
        public IEnumerator AFearOfThree_SendsThemAway_Trembling()
        {
            yield return null;
            foreach (string name in new[] { Knife, Gunman })
            {
                GameObject figure = GameObject.Find(name);
                SoldierBrain brain = figure.GetComponent<SoldierBrain>();
                EnemyMorale morale = figure.GetComponent<EnemyMorale>();
                morale.See(null, gruesome: false);
                morale.See(null, gruesome: false);
                Assert.IsFalse(brain.Retreating, name + " holds at a fear of 2");
                morale.See(null, gruesome: false);
                Assert.IsTrue(brain.Retreating, name + " runs at a fear of 3");
            }
            SoldierBrain gunman = GameObject.Find(Gunman).GetComponent<SoldierBrain>();
            yield return WaitUntil(() => gunman.Current == SoldierBrain.Phase.Cower, 10f);
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(SoldierBrain.Phase.Cower, gunman.Current, "it cowers");
            Assert.Greater(GameObject.Find(Gunman).GetComponent<Tremble>().LastDegrees, 1f, "it shakes hard when it cowers");
        }

        [UnityTest]
        public IEnumerator TheCarrier_DropsTheCard_BesideHisBody_AndTheCardOpensTheDoor()
        {
            Interactor hands = lucy.GetComponent<Interactor>();
            LockedDoor door = GameObject.Find(Door).GetComponent<LockedDoor>();
            Assert.AreEqual(InteractKind.Message, door.KindFor(hands), "locked, and she has no card");
            GameObject carrier = GameObject.Find(Carrier);
            Transform hips = carrier.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Hips);
            Kill(Carrier);
            yield return new WaitForSeconds(ItemCarrier.FallSeconds + 0.5f);
            ItemPickup card = carrier.GetComponent<ItemCarrier>().Drop;
            Assert.IsTrue(card.gameObject.activeInHierarchy, "the card shows after his fall");
            float apart = Flat(card.transform.position, hips.position);
            Debug.Log($"[ArmedCivilianTests] the card lies {apart:0.00} m from his hips, {card.transform.position.y:0.00} m up");
            Assert.LessOrEqual(apart, 1.0f, "beside the body");
            Assert.GreaterOrEqual(LimbClearance(carrier, card.transform.position), 0.19f, "no limb lies over the card (the first record hid it under his legs)");
            Assert.Less(card.transform.position.y, 0.06f, "on the floor (the card is 2 cm thick, built 3 cm up)");

            lucy.GetComponent<TankController>().Teleport(card.transform.position - Vector3.forward * 0.6f, 0f);
            Assert.AreEqual("taken:lab_card", card.Confirm(hands));
            Assert.IsTrue(hands.Inventory.Has("lab_card"), "the card is hers");
            Assert.AreEqual(InteractKind.Ask, door.KindFor(hands), "the card fits");
            door.Confirm(hands);
            Assert.IsTrue(door.IsOpen, "the store room opens");
        }

        [UnityTest]
        public IEnumerator ASaveBeforeHisDeath_KeepsTheHiddenCard()
        {
            yield return SaveAndLoad(1);
            Assert.IsNotNull(GameObject.Find(Carrier).GetComponent<ItemCarrier>().Drop, "a save while he lives does not count the hidden card as taken");
            Kill(Carrier);
            yield return new WaitForSeconds(ItemCarrier.FallSeconds + 0.5f);
            Assert.IsTrue(GameObject.Find(Carrier).GetComponent<ItemCarrier>().Drop.gameObject.activeInHierarchy, "the card drops after the load");
        }

        [UnityTest]
        public IEnumerator TheCard_LiesBesideHim_AfterALoad_UntilSheTakesIt()
        {
            Kill(Carrier);
            yield return new WaitForSeconds(ItemCarrier.FallSeconds + 0.5f);
            yield return SaveAndLoad(1);
            yield return new WaitForSeconds(ItemCarrier.FallSeconds + 0.5f);
            Assert.IsTrue(GameObject.Find(Carrier).GetComponent<Health>().IsDead, "he stays dead");
            ItemPickup card = GameObject.Find(Carrier).GetComponent<ItemCarrier>().Drop;
            Assert.IsNotNull(card, "the card is in the room");
            Assert.IsTrue(card.gameObject.activeInHierarchy, "beside his body again");
            Assert.GreaterOrEqual(LimbClearance(GameObject.Find(Carrier), card.transform.position), 0.19f, "no limb over the card after the load (the corpse's colliders are off)");

            Assert.AreEqual("taken:lab_card", card.Confirm(lucy.GetComponent<Interactor>()));
            yield return null;
            yield return SaveAndLoad(2);
            yield return new WaitForSeconds(ItemCarrier.FallSeconds + 0.5f);
            ItemPickup gone = GameObject.Find(Carrier).GetComponent<ItemCarrier>().Drop;
            Assert.IsTrue(gone == null || !gone.gameObject.activeInHierarchy, "a taken card stays taken");
            Assert.IsTrue(lucy.GetComponent<Interactor>().Inventory.Has("lab_card"), "and it is hers after the load");
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

        /// <summary>The flat distance from the point to the nearest limb of the body (bone to bone), in metres.</summary>
        private static float LimbClearance(GameObject body, Vector3 point)
        {
            Animator animator = body.GetComponent<Animator>();
            HumanBodyBones[,] segments =
            {
                { HumanBodyBones.Hips, HumanBodyBones.Chest }, { HumanBodyBones.Chest, HumanBodyBones.Head },
                { HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg }, { HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot },
                { HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg }, { HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot },
                { HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm }, { HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand },
                { HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm }, { HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand },
            };
            Vector2 p = new Vector2(point.x, point.z);
            float best = float.MaxValue;
            for (int i = 0; i < segments.GetLength(0); i++)
            {
                Vector3 a3 = animator.GetBoneTransform(segments[i, 0]).position, b3 = animator.GetBoneTransform(segments[i, 1]).position;
                Vector2 a = new Vector2(a3.x, a3.z), ab = new Vector2(b3.x, b3.z) - a;
                float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            Debug.Log($"[ArmedCivilianTests] the card is {best:0.00} m from the nearest limb");
            return best;
        }

        /// <summary>Out of the test: the figure goes away (no shot, no alert, no witness).</summary>
        private static void Remove(string name) => GameObject.Find(name).SetActive(false);

        private static float Flat(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private IEnumerator InRoom()
        {
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == Room && GameManager.Instance.IsPlaying, 10f);
            yield return null;
            Assert.AreEqual(Room, SceneManager.GetActiveScene().name, "the lab test room (LabTestSetup.BuildLabTest)");
            lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
