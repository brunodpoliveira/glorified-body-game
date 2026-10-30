using UnityEngine;
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;
using GlorifiedBody.Player;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// A dead enemy or civilian she can feed on (Bruno, 2026-10-30: "the only form to regain hp is by feeding on fallen
    /// enemies; civilians give more hp back as usual"). The Interact key on the body asks "Feed on the body?"; a yes
    /// starts the feed (Feeding). Each body gives its rank's HP once (Feeding.ValueOf), in parts if a hit stops the
    /// feed. At full HP there is no question, only "She is not hurt.". The HP taken is saved with the body (RoomGore).
    /// The factory puts this on every figure. It reaches for the hips, where the body lies.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class FeedableBody : Interactable
    {
        private Health health;
        private EnemyMorale morale;
        private float taken;

        /// <summary>The HP this body gives in all (its rank, the difficulty's MENDING).</summary>
        public float Total => Feeding.ValueOf(morale != null ? morale.RankOf : EnemyMorale.Rank.Soldier) * Feeding.DifficultyScale;
        public float Taken => taken;
        public float Left => Mathf.Max(0f, Total - taken);
        public bool IsDead => health != null && health.IsDead;

        private void Awake()
        {
            health = GetComponent<Health>();
            morale = GetComponent<EnemyMorale>();
            Animator animator = GetComponentInChildren<Animator>();
            if (point == null && animator != null && animator.isHuman) point = animator.GetBoneTransform(HumanBodyBones.Hips);
            range = 1.6f;
        }

        public override bool Available => base.Available && IsDead && Left > 0.5f;

        public override string Hint => "feed on the body";

        public override InteractKind KindFor(Interactor who) => Hurt(who) ? InteractKind.Ask : InteractKind.Message;

        public override string TextFor(Interactor who) => Hurt(who) ? "Feed on the body?" : "She is not hurt.";

        public override string Confirm(Interactor who)
        {
            Feeding feeding = who != null ? who.GetComponent<Feeding>() : null;
            return feeding != null && feeding.Begin(this) ? "feed:" + name : "feed_refused:" + name;
        }

        /// <summary>The feed takes HP from the body.</summary>
        public void Take(float amount) => taken = Mathf.Min(Total, taken + Mathf.Max(0f, amount));

        /// <summary>A load: the HP taken before the save.</summary>
        public void Restore(float amountTaken) => taken = Mathf.Max(0f, amountTaken);

        private static bool Hurt(Interactor who)
        {
            Health hers = who != null ? who.GetComponentInChildren<Health>() : null;
            return hers != null && hers.CurrentHealth < hers.MaxHealth - 0.5f;
        }
    }
}
