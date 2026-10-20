using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using GlorifiedBody.Core;

namespace GlorifiedBody.Tests.EditMode
{
    public class HealthTests
    {
        private GameObject go;
        private Health health;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("Health");
            health = go.AddComponent<Health>();
            DisableDestroyOnDeath(health);
            // Awake already ran on AddComponent; ensure events are non-null for listeners.
            health.OnDeath ??= new UnityEvent();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Health_TakeDamage_KillsWhenHealthZero()
        {
            Assert.IsFalse(health.IsDead);
            health.TakeDamage(health.MaxHealth);
            Assert.IsTrue(health.IsDead);
        }

        [Test]
        public void Health_SecondWind_SurvivesOnce()
        {
            health.SetSecondWindEnabled(true);
            health.SetSecondWindInvulnerabilityDuration(0f); // no post-hit i-frames, so the 2nd hit lands

            // First lethal hit: Second Wind saves the player at 1 HP.
            health.TakeDamage(health.MaxHealth * 2f);
            Assert.IsFalse(health.IsDead, "Second Wind should prevent the first lethal hit from killing.");
            Assert.IsTrue(health.IsSecondWindActive);

            // Second lethal hit: no charge left, so it kills.
            health.TakeDamage(health.MaxHealth * 2f);
            Assert.IsTrue(health.IsDead, "Second Wind should only save once.");
        }

        [Test]
        public void Health_TakeDamage_InvulnerableIgnoresDamage()
        {
            health.SetInvulnerable(true);
            float before = health.CurrentHealth;
            health.TakeDamage(before * 0.5f);
            Assert.AreEqual(before, health.CurrentHealth);
            Assert.IsFalse(health.IsDead);
        }

        private static void DisableDestroyOnDeath(Health h)
        {
            FieldInfo field = typeof(Health).GetField("destroyOnDeath", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field.SetValue(h, false);
        }
    }
}
