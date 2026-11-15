using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.Player;
using UnityEngine;

namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// The human shield (round 162, CANON): with mk_orzabal active, when she holds a live civilian at or near ground level,
    /// the guards and the soldiers do not fire through the civilian. The game does not teach it; one of the soldier's
    /// death screen hints does (round 163). A held civilian is "low" when its root hangs LowHeight or less over the floor
    /// it lay on (the lift starts at 1.5 m: she lowers it with the wheel). The power armour and the armed lab workers fire
    /// anyway, and a lobbed grenade goes over. Numbers PROPOSED.
    /// </summary>
    public static class HumanShield
    {
        public const float LowHeight = 0.6f; // m over its floor: "at or near ground level"
        private const float Margin = 0.1f;   // m around the body's bounds

        private static PsychicPowers powers;

        /// <summary>The live civilian she holds low now, or null.</summary>
        public static CivilianBrain Held()
        {
            if (powers == null)
            {
                GameObject her = GameObject.FindWithTag("Player");
                powers = her != null ? her.GetComponent<PsychicPowers>() : null;
                if (powers == null) return null;
            }
            TelekinesisLift lift = powers.HeldLift;
            if (lift == null || lift.IsBox || lift.HeightOverFloor > LowHeight) return null;
            CivilianBrain civilian = lift.GetComponentInParent<CivilianBrain>();
            if (civilian == null) return null;
            Health health = civilian.GetComponent<Health>();
            return health != null && !health.IsDead ? civilian : null;
        }

        /// <summary>The held civilian stands on the line from the gun to the target.</summary>
        public static bool Blocks(Vector3 from, Vector3 to) => Blocks(from, to, Held());

        public static bool Blocks(Vector3 from, Vector3 to, CivilianBrain held)
        {
            if (held == null) return false;
            Renderer[] renderers = held.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return false;
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
            b.Expand(Margin * 2f);
            Vector3 line = to - from;
            float length = line.magnitude;
            if (length < 0.01f) return false;
            return b.IntersectRay(new Ray(from, line / length), out float at) && at <= length;
        }

        /// <summary>The ranks that hold their fire: the guards and the soldiers.</summary>
        public static bool HoldsFireFor(GameObject shooter)
        {
            EnemyMorale morale = shooter != null ? shooter.GetComponent<EnemyMorale>() : null;
            return morale != null && (morale.RankOf == EnemyMorale.Rank.Guard || morale.RankOf == EnemyMorale.Rank.Soldier);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => powers = null;
    }
}
