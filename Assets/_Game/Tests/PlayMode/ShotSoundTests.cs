#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Combat;
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

        /// <summary>
        /// Bruno (2026-10-25): "what is this ricochet sound from the pistol? it sounds like a hammer hitting a chisel".
        /// Every bullet hit played Invector's Knife Hit Wall clips, a steel blade on a wall, on every surface.
        /// </summary>
        [UnityTest]
        public IEnumerator ABulletOnConcrete_PlaysAConcreteHit_NotTheKnife()
        {
            yield return BulletOn("GB_ConcreteWall", SurfaceKind.Concrete, "concrete_hit");
        }

        [UnityTest]
        public IEnumerator ABulletOnSteel_PlaysAMetalHit()
        {
            yield return BulletOn("GB_Steel", SurfaceKind.Metal, "impactMetal");
        }

        [UnityTest]
        public IEnumerator ABulletOnPlywood_PlaysAWoodHit()
        {
            yield return BulletOn("GB_Break_Plywood", SurfaceKind.Wood, "impactWood");
        }

        [Test]
        public void TheMaterialName_PicksTheSurface()
        {
            var cases = new Dictionary<string, SurfaceKind>
            {
                { "GB_ConcreteWall", SurfaceKind.Concrete }, { "GB_ConcretePlanter", SurfaceKind.Concrete },
                { "GB_TileWall", SurfaceKind.Concrete }, { "GB_Greybox", SurfaceKind.Concrete }, { "GB_ResinFloor", SurfaceKind.Concrete },
                { "GB_Steel", SurfaceKind.Metal }, { "GB_DarkSteel", SurfaceKind.Metal }, { "GB_VentGrille", SurfaceKind.Metal },
                { "GB_GearEmblem", SurfaceKind.Metal }, { "GB_Break_Plywood", SurfaceKind.Wood }, { "GB_Prop_Crate", SurfaceKind.Wood },
                { "GB_Break_Laminate", SurfaceKind.Wood }, { "GB_Glass", SurfaceKind.Glass }, { "GB_Mirror", SurfaceKind.Glass },
                { "GB_Item_Monitor", SurfaceKind.Glass }, { "GB_Mattress", SurfaceKind.Soft }, { "GB_Leather", SurfaceKind.Soft },
                { "GB_Soil", SurfaceKind.Soft }, { "GB_Plant", SurfaceKind.Soft },
            };
            foreach (var c in cases) Assert.AreEqual(c.Value, Surfaces.OfName(c.Key), c.Key);
        }

        [Test]
        public void EverySurface_HasParticlesAndSounds_AndNoKnife()
        {
            ShotEffects fx = ShotEffects.Load();
            foreach (SurfaceKind kind in Enum.GetValues(typeof(SurfaceKind)))
            {
                Assert.IsTrue(fx.TryGetSurface(kind, out ShotEffects.SurfaceImpact set), $"a set for {kind}");
                Assert.IsNotNull(set.prefab, $"{kind}: the particles");
                Assert.Greater(set.clips.Length, 0, $"{kind}: the sounds");
                Assert.IsFalse(set.clips.Any(c => c == null || c.name.Contains("Knife")), $"{kind}: " + string.Join(", ", set.clips.Select(c => c != null ? c.name : "null")));
            }
            Assert.IsFalse(fx.impactClips.Any(c => c == null || c.name.Contains("Knife")), "the fallback set");
        }

        /// <summary>A wall with the named material 3 m away, an enemy bullet at it: the surface, the clip, one sound.</summary>
        private static IEnumerator BulletOn(string material, SurfaceKind expected, string clipPrefix)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "TestWall";
            wall.transform.position = Point + Vector3.forward * 3f;
            wall.transform.localScale = new Vector3(3f, 3f, 0.3f);
            var renderer = wall.GetComponent<Renderer>();
            renderer.sharedMaterial = new Material(renderer.sharedMaterial) { name = material };
            yield return null;
            int before = ShotEffects.Impacts;
            EnemyProjectile.Launch(Point, Vector3.forward, 40f, 5f, null, "test");
            float t0 = Time.realtimeSinceStartup;
            while (ShotEffects.Impacts == before && Time.realtimeSinceStartup - t0 < 3f) yield return null;
            Assert.Greater(ShotEffects.Impacts, before, "the bullet hit the wall");
            Vector3 hit = Point + Vector3.forward * 2.85f;
            List<string> clips = SoundsAt(hit);
            Debug.Log($"[ShotSoundTests] {material}: surface {ShotEffects.LastSurface}, clip {(ShotEffects.LastClip != null ? ShotEffects.LastClip.name : "none")}, sounds {string.Join(", ", clips)}");
            Assert.AreEqual(expected, ShotEffects.LastSurface, material);
            Assert.IsNotNull(ShotEffects.LastClip, "a hit sound");
            StringAssert.StartsWith(clipPrefix, ShotEffects.LastClip.name);
            Assert.AreEqual(1, clips.Count, "one hit sound: " + string.Join(", ", clips));
            UnityEngine.Object.Destroy(wall);
        }

        /// <summary>The clips of the live sources within 3 m of the point: enabled, with a clip, playing or set to play.</summary>
        private static List<string> SoundsAt(Vector3 point)
        {
            return UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None)
                .Where(s => s.enabled && s.gameObject.activeInHierarchy && s.clip != null && (s.isPlaying || s.playOnAwake)
                    && s.spatialBlend > 0.5f && Vector3.Distance(s.transform.position, point) <= 3f) // world sounds: not the music or the room tone
                .Select(s => s.clip.name).ToList();
        }
    }
}
#endif
