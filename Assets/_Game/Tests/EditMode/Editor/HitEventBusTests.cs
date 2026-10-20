using NUnit.Framework;
using UnityEngine;
using GlorifiedBody.Combat;

namespace GlorifiedBody.Tests.EditMode
{
    public class HitEventBusTests
    {
        private GameObject attacker;
        private GameObject victim;

        [SetUp]
        public void SetUp()
        {
            HitEventBus.ResetForTesting();
            attacker = new GameObject("Attacker");
            victim = new GameObject("Victim");
        }

        [TearDown]
        public void TearDown()
        {
            HitEventBus.ResetForTesting();
            Object.DestroyImmediate(attacker);
            Object.DestroyImmediate(victim);
        }

        [Test]
        public void HitEventBus_Raise_FiresOnHit()
        {
            int fired = 0;
            void Handler(HitInfo _) => fired++;
            HitEventBus.OnHit += Handler;

            HitEventBus.Raise(MakeHit());

            HitEventBus.OnHit -= Handler;
            Assert.AreEqual(1, fired);
        }

        [Test]
        public void HitEventBus_Raise_SuppressDuplicateInSameFrame()
        {
            int fired = 0;
            void Handler(HitInfo _) => fired++;
            HitEventBus.OnHit += Handler;

            // Two identical hits in the same frame: the second is deduplicated.
            HitEventBus.Raise(MakeHit());
            HitEventBus.Raise(MakeHit());

            HitEventBus.OnHit -= Handler;
            Assert.AreEqual(1, fired);
        }

        private HitInfo MakeHit()
        {
            return new HitInfo
            {
                Attacker = attacker,
                Victim = victim,
                HitPoint = Vector3.zero,
                DamageAmount = 10f,
                DamageType = DamageType.TimeDilationMelee,
                WasFatal = false,
            };
        }
    }
}
