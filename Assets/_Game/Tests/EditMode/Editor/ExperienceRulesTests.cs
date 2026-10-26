using NUnit.Framework;
using GlorifiedBody.Enemy;
using GlorifiedBody.Player;

namespace GlorifiedBody.Tests.EditMode
{
    /// <summary>The exp numbers (design §11.2, PROPOSED): civilians pay more than soldiers, the steps grow, the cap holds.</summary>
    public class ExperienceRulesTests
    {
        [Test]
        public void Civilians_PayMoreThanSoldiersAndGuards()
        {
            int soldier = ExperienceRules.ValueOf(EnemyMorale.Rank.Soldier);
            Assert.Greater(ExperienceRules.ValueOf(EnemyMorale.Rank.UnarmedCivilian), soldier, "the unarmed civilian (Bruno: civvies give more)");
            Assert.Greater(ExperienceRules.ValueOf(EnemyMorale.Rank.ArmedCivilian), soldier, "the armed civilian");
            Assert.Greater(soldier, ExperienceRules.ValueOf(EnemyMorale.Rank.Guard));
        }

        [Test]
        public void TheThresholds_Grow_AndTheLevelFollowsThem()
        {
            Assert.AreEqual(0, ExperienceRules.Threshold(1));
            Assert.AreEqual(100, ExperienceRules.Threshold(2));
            Assert.AreEqual(250, ExperienceRules.Threshold(3));
            Assert.AreEqual(450, ExperienceRules.Threshold(4));
            Assert.AreEqual(1, ExperienceRules.LevelFor(0));
            Assert.AreEqual(1, ExperienceRules.LevelFor(99));
            Assert.AreEqual(2, ExperienceRules.LevelFor(100));
            Assert.AreEqual(3, ExperienceRules.LevelFor(260));
            Assert.AreEqual(ExperienceRules.MaxLevel, ExperienceRules.LevelFor(1000000), "the cap");
        }

        [Test]
        public void ALevel_AddsDamageAndHp()
        {
            Assert.AreEqual(1f, ExperienceRules.DamageScale(1), 1e-5f);
            Assert.AreEqual(1.2f, ExperienceRules.DamageScale(3), 1e-5f);
            Assert.AreEqual(0f, ExperienceRules.BonusHp(1), 1e-5f);
            Assert.AreEqual(20f, ExperienceRules.BonusHp(3), 1e-5f);
        }
    }
}
