using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.Player;
using UnityEngine;

namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// The human shield (round 162, CANON): with mk_orzabal active, when she holds a live person at or near ground level,
    /// the guards and the soldiers do not fire past it. Round 162 said "a live civilian"; Bruno, 2026-11-15, after he held
    /// a guard: any live person (a civilian, a guard, a soldier), and the zone is 1 m around the line of fire (in the
    /// earlier rule only a line through the body stopped a shot, and from a fixed camera a shot 0.7 m beside the hostage
    /// looked like a shot through it). The game does not teach it; one of the soldier's death screen hints does (round 163).
    /// A held person is "low" when its root hangs LowHeight or less over the floor it lay on (the lift starts at 1.5 m: she
    /// lowers it with the wheel). The power armour and the armed lab workers fire anyway, and a lobbed grenade goes over.
    /// Numbers PROPOSED.
    /// </summary>
    public static class HumanShield
    {
        public const float LowHeight = 0.6f; // m over its floor: "at or near ground level"
        public const float Zone = 1.0f;      // m from the line of fire, seen from above (Bruno, 2026-11-15)

        private static PsychicPowers powers;

        /// <summary>The live person she holds low now (its root object), or null.</summary>
        public static GameObject Held()
        {
            if (powers == null)
            {
                GameObject her = GameObject.FindWithTag("Player");
                powers = her != null ? her.GetComponent<PsychicPowers>() : null;
                if (powers == null) return null;
            }
            TelekinesisLift lift = powers.HeldLift;
            if (lift == null || lift.IsBox || lift.HeightOverFloor > LowHeight) return null;
            Component person = lift.GetComponentInParent<CivilianBrain>();
            if (person == null) person = lift.GetComponentInParent<SoldierBrain>();
            if (person == null) return null;
            Health health = person.GetComponent<Health>();
            return health != null && !health.IsDead ? person.gameObject : null;
        }

        /// <summary>The civilian she holds low now, or null (the CIV tag of the "hold fire" line).</summary>
        public static CivilianBrain HeldCivilian()
        {
            GameObject held = Held();
            return held != null ? held.GetComponent<CivilianBrain>() : null;
        }

        /// <summary>The held person stands between the gun and the target, within Zone of the line, seen from above.</summary>
        public static bool Blocks(Vector3 from, Vector3 to) => Blocks(from, to, Held());

        public static bool Blocks(Vector3 from, Vector3 to, GameObject held)
        {
            if (held == null) return false;
            Vector3 middle = Middle(held);
            Vector2 a = new Vector2(from.x, from.z), b = new Vector2(to.x, to.z), h = new Vector2(middle.x, middle.z);
            Vector2 line = b - a;
            if (line.sqrMagnitude < 0.0001f) return false;
            float along = Vector2.Dot(h - a, line) / line.sqrMagnitude; // 0 at the gun, 1 at the target
            if (along <= 0f || along > 1.05f) return false;              // behind the gun, or behind her
            return Vector2.Distance(h, a + Mathf.Clamp01(along) * line) <= Zone;
        }

        /// <summary>
        /// The middle of the body: its chest bone (the root of a held body hangs away from it). Not the renderers: a held guard's
        /// dropped shield and gun are renderers under him, on the floor (2026-11-15: the middle left the line, he took 4 shots).
        /// </summary>
        public static Vector3 Middle(GameObject held)
        {
            Animator animator = held.GetComponent<Animator>();
            if (animator != null && animator.isHuman)
            {
                Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest) ?? animator.GetBoneTransform(HumanBodyBones.Spine);
                if (chest != null) return chest.position;
            }
            Renderer[] renderers = held.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return held.transform.position;
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
            return b.center;
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
