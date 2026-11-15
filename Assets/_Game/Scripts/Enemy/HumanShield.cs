using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.Player;
using UnityEngine;

namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// The line of fire must be clear of people (the human shield, round 162, CANON, and Bruno's picks of 2026-11-15). A guard
    /// or a soldier does not aim, and does not fire, when the body of a live person covers his line to her: seen from above,
    /// the person stands between him and her within Zone of the line, and the line passes between the person's feet and
    /// head. A person she holds with mk_orzabal counts the same as one who stands ("any live person"; "soldier always fire
    /// through guard, regardless of being lifted up or not"): a person held high hangs over the line and does not cover it,
    /// so "keep it low" (the hint of round 163) still holds. A body that lies on the floor does not cover it, and neither
    /// does a person within NearGun of the gun (a teammate at his side). The power armour and the armed lab workers fire
    /// anyway, and a lobbed grenade goes over. Numbers PROPOSED.
    /// </summary>
    public static class HumanShield
    {
        public const float LowHeight = 0.6f; // m over its floor: a held person this low is "at or near ground level"
        public const float Zone = 1.0f;      // m from the line of fire, seen from above (Bruno, 2026-11-15)
        public const float NearGun = 1.5f;   // m from the gun along the line: a teammate at his side does not stop him
        private const float SpanMargin = 0.1f;

        private static PsychicPowers powers;

        /// <summary>The live person she holds low now (its root object), or null. For the tests and the barks.</summary>
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
            GameObject person = PersonOf(lift);
            return person != null && Alive(person) ? person : null;
        }

        /// <summary>The first live person whose body covers the line from the gun to the target, or null.</summary>
        public static GameObject Covering(Vector3 from, Vector3 to, GameObject shooter)
        {
            foreach (EnemyMorale m in EnemyMorale.All)
            {
                if (m == null || m.gameObject == shooter || !m.isActiveAndEnabled) continue;
                GameObject person = m.gameObject;
                if (!Alive(person)) continue;
                TelekinesisLift lift = person.GetComponent<TelekinesisLift>();
                bool lifted = lift != null && lift.IsLifted;
                if (!lifted && RagdollBodies.IsDown(m)) continue; // on the floor: the shot goes over
                if (Covers(from, to, person)) return person;
            }
            return null;
        }

        /// <summary>This person's body covers the line: between the gun and the target within Zone (from above), past NearGun, between its feet and head.</summary>
        public static bool Covers(Vector3 from, Vector3 to, GameObject person)
        {
            if (person == null) return false;
            Vector3 middle = Middle(person);
            Vector2 a = new Vector2(from.x, from.z), b = new Vector2(to.x, to.z), h = new Vector2(middle.x, middle.z);
            Vector2 line = b - a;
            float length = line.magnitude;
            if (length < 0.01f) return false;
            float along = Vector2.Dot(h - a, line) / (length * length); // 0 at the gun, 1 at the target
            if (along <= 0f || along > 1.05f || along * length < NearGun) return false;
            if (Vector2.Distance(h, a + Mathf.Clamp01(along) * line) > Zone) return false;
            (float low, float high) = Span(person);
            float lineY = Mathf.Lerp(from.y, to.y, Mathf.Clamp01(along));
            return lineY >= low - SpanMargin && lineY <= high + SpanMargin;
        }

        /// <summary>The middle of the body: its chest bone. Not the renderers: a held guard's dropped shield and gun lie on the floor under him.</summary>
        public static Vector3 Middle(GameObject person)
        {
            Animator animator = person.GetComponent<Animator>();
            if (animator != null && animator.isHuman)
            {
                Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest) ?? animator.GetBoneTransform(HumanBodyBones.Spine);
                if (chest != null) return chest.position;
            }
            return person.transform.position + Vector3.up * 1.2f;
        }

        /// <summary>The height of the body from its lowest to its highest bone (feet, hips, head): standing, crouched or hanging.</summary>
        public static (float low, float high) Span(GameObject person)
        {
            Animator animator = person.GetComponent<Animator>();
            if (animator == null || !animator.isHuman) return (person.transform.position.y, person.transform.position.y + 1.8f);
            float low = float.MaxValue, high = float.MinValue;
            foreach (HumanBodyBones bone in new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.Hips, HumanBodyBones.Head })
            {
                Transform t = animator.GetBoneTransform(bone);
                if (t == null) continue;
                low = Mathf.Min(low, t.position.y);
                high = Mathf.Max(high, t.position.y);
            }
            if (low > high) return (person.transform.position.y, person.transform.position.y + 1.8f);
            return (low - 0.1f, high + 0.15f); // the soles under the foot bones, the crown over the head bone
        }

        /// <summary>The ranks that hold their fire: the guards and the soldiers.</summary>
        public static bool HoldsFireFor(GameObject shooter)
        {
            EnemyMorale morale = shooter != null ? shooter.GetComponent<EnemyMorale>() : null;
            return morale != null && (morale.RankOf == EnemyMorale.Rank.Guard || morale.RankOf == EnemyMorale.Rank.Soldier);
        }

        private static GameObject PersonOf(Component c)
        {
            EnemyMorale m = c.GetComponentInParent<EnemyMorale>();
            return m != null ? m.gameObject : null;
        }

        private static bool Alive(GameObject person)
        {
            Health health = person.GetComponent<Health>();
            return health != null && !health.IsDead;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => powers = null;
    }
}
