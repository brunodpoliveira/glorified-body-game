#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Enemy;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// One sound for one event (Bruno, 2026-10-25: "the pistol has two sound effects? check if other attacks also
    /// have this bug"). The vendor impact prefab played its own clip on awake, pistol-shot-ricochet.wav, and that
    /// clip opens with a full gunshot: every bullet that hit a wall sounded like a second shot. The vendor explosion
    /// prefab played its own Explosion_A.wav over ours. The sources are counted, not heard: audio can be off in
    /// batch. Batch: tools/unity.sh test playmode ShotSoundTests.
    /// </summary>
    public class ShotSoundTests
    {
        private static readonly Vector3 Point = new Vector3(0f, 50f, 0f);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Main_Menu");
            float t0 = Time.realtimeSinceStartup;
            while (!(SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null) && Time.realtimeSinceStartup - t0 < 10f) yield return null;
        }

        [UnityTest]
        public IEnumerator AWallImpact_PlaysNoGunshot()
        {
            Assert.IsNotNull(ShotEffects.Load(), "the shot effects asset");
            ShotEffects.Impact(Point, Vector3.up);
            yield return null;
            List<string> clips = SoundsAt(Point);
            Debug.Log("[ShotSoundTests] impact: " + string.Join(", ", clips));
            Assert.IsFalse(clips.Any(c => c.IndexOf("shot", StringComparison.OrdinalIgnoreCase) >= 0), "no gunshot at the wall: " + string.Join(", ", clips));
            Assert.LessOrEqual(clips.Count, 1, "at most one hit sound: " + string.Join(", ", clips));
        }

        [UnityTest]
        public IEnumerator AnExplosion_PlaysOneSound()
        {
            ShotEffects.Explosion(Point);
            yield return null;
            List<string> clips = SoundsAt(Point);
            Debug.Log("[ShotSoundTests] explosion: " + string.Join(", ", clips));
            Assert.AreEqual(1, clips.Count, "one explosion sound: " + string.Join(", ", clips));
        }

        [UnityTest]
        public IEnumerator AShieldBlock_PlaysTheBlockOnly()
        {
            // The calls of EnemyShield.Blocked: the impact particles, then the block sound.
            ShotEffects fx = ShotEffects.Load();
            ShotEffects.Impact(Point, Vector3.forward, withSound: false);
            ShotEffects.PlayAt(fx.blockClips, Point);
            yield return null;
            List<string> clips = SoundsAt(Point);
            Debug.Log("[ShotSoundTests] block: " + string.Join(", ", clips));
            Assert.IsFalse(clips.Any(c => c.IndexOf("shot", StringComparison.OrdinalIgnoreCase) >= 0), "a power on a shield is no gunshot: " + string.Join(", ", clips));
            Assert.AreEqual(1, clips.Count, "the block sound only: " + string.Join(", ", clips));
        }

        [UnityTest]
        public IEnumerator AGasCloud_PlaysOneSound()
        {
            GasCloud.Spawn(Point, 2f, 1f, 0f, "test");
            yield return null;
            List<string> clips = SoundsAt(Point);
            Debug.Log("[ShotSoundTests] gas: " + string.Join(", ", clips));
            Assert.AreEqual(1, clips.Count, "one gas sound: " + string.Join(", ", clips));
        }

        [Test]
        public void EachGun_FiresOneFamilyOfSounds()
        {
            // The pistol set mixed the old Handgun@Fire with the new LightWeaponShot clips, the rifle set the old
            // Rifle@Fire with the MediumWeaponShot clips: two different guns in one hand.
            ShotEffects fx = ShotEffects.Load();
            Assert.IsTrue(fx.fireClips.All(c => c != null && c.name.StartsWith("LightWeaponShot")), "pistol: " + string.Join(", ", fx.fireClips.Select(c => c != null ? c.name : "null")));
            Assert.IsTrue(fx.rifleFireClips.All(c => c != null && c.name.StartsWith("MediumWeaponShot")), "rifle: " + string.Join(", ", fx.rifleFireClips.Select(c => c != null ? c.name : "null")));
        }

        /// <summary>The clips of the live sources within 3 m of the point: enabled, with a clip, playing or set to play.</summary>
        private static List<string> SoundsAt(Vector3 point)
        {
            return UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None)
                .Where(s => s.enabled && s.gameObject.activeInHierarchy && s.clip != null && (s.isPlaying || s.playOnAwake)
                    && Vector3.Distance(s.transform.position, point) <= 3f)
                .Select(s => s.clip.name).ToList();
        }
    }
}
#endif
