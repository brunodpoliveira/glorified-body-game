using UnityEngine;
using GlorifiedBody.Combat;
using GlorifiedBody.Enemy;

namespace GlorifiedBody.Player
{
    /// <summary>
    /// The experience levels (design §11.2, Bruno, 2026-10-22): kills give exp, and "civvies may give more exp than
    /// soldiers, to give incentive for player to kill everyone (then it becomes moral choice: kill everyone and become
    /// stronger, or be merciful and lose out on exp and obligatory fights become tougher)". The firemen of §11.3 are
    /// unarmed civilians: the farm pays the civilian rate.
    /// Only her kills count: her powers, the short, a body or a prop she throws, a fire she lit, a grenade or a rocket
    /// she sends back. A level adds power damage and maximum HP. All numbers are PROPOSED.
    /// </summary>
    public static class ExperienceRules
    {
        public const int MaxLevel = 10;
        /// <summary>The power damage a level adds (her blast, fire and lift; the short's 12 per shock stays, Bruno).</summary>
        public const float DamagePerLevel = 0.10f;
        /// <summary>The maximum HP a level adds, over the outfit's.</summary>
        public const float HpPerLevel = 10f;

        /// <summary>The exp of a kill by rank. The civilians pay the most (Bruno).</summary>
        public static int ValueOf(EnemyMorale.Rank rank)
        {
            switch (rank)
            {
                case EnemyMorale.Rank.UnarmedCivilian: return 50;
                case EnemyMorale.Rank.ArmedCivilian: return 40;
                case EnemyMorale.Rank.Guard: return 15;
                case EnemyMorale.Rank.Heavy: return 60;
                default: return 20;
            }
        }

        /// <summary>The exp of a dead figure: by its rank, a soldier's without one (the training dummies), none for a prop or her.</summary>
        public static int ValueOf(GameObject victim)
        {
            if (victim == null || victim.CompareTag("Player")) return 0;
            LockOnTargetable target = victim.GetComponentInParent<LockOnTargetable>();
            if (target != null && target.IsProp) return 0;
            EnemyMorale morale = victim.GetComponentInParent<EnemyMorale>();
            return ValueOf(morale != null ? morale.RankOf : EnemyMorale.Rank.Soldier);
        }

        /// <summary>The total exp to reach a level: 100 for level 2, then each step 50 more than the one before.</summary>
        public static int Threshold(int level)
        {
            level = Mathf.Clamp(level, 1, MaxLevel);
            int total = 0;
            for (int l = 1; l < level; l++) total += 100 + 50 * (l - 1);
            return total;
        }

        public static int LevelFor(int exp)
        {
            int level = 1;
            while (level < MaxLevel && exp >= Threshold(level + 1)) level++;
            return level;
        }

        public static float DamageScale(int level) => 1f + DamagePerLevel * (Mathf.Clamp(level, 1, MaxLevel) - 1);

        public static float BonusHp(int level) => HpPerLevel * (Mathf.Clamp(level, 1, MaxLevel) - 1);
    }
}
