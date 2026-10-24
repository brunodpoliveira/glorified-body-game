using UnityEngine;

namespace GlorifiedBody.Combat
{
    /// <summary>
    /// A body that can go limp: the lift holds it by the hips with the limbs loose, a throw or a full mk_warspite
    /// knocks it down for some seconds, then it stands up again (Bruno, 2026-10-24: "orzabal should ragdoll the
    /// floater, and cause the people hit by it to ragdoll as well"; "warspite at full charge should knock down the
    /// enemy for a while (2.5 seconds)"). A dead body stays down. EnemyRagdoll is the one implementation.
    /// </summary>
    public interface IRagdollBody
    {
        /// <summary>True while it is limp, held, down or standing up: it does nothing else.</summary>
        bool IsDown { get; }
        /// <summary>Limp now: the bones follow the physics.</summary>
        bool IsRagdolled { get; }
        /// <summary>The bones go limp with this velocity. It stands up after the seconds (never when dead).</summary>
        bool KnockDown(float seconds, Vector3 velocity);
        /// <summary>The lift takes it: the hips follow the root, the rest hangs.</summary>
        bool BeginHold();
        /// <summary>The lift ends (a throw, a slam, a release): all of it falls with this velocity.</summary>
        void EndHold(Vector3 velocity, float seconds);
        /// <summary>A cut limb (the gore): the bones from this one down lose their colliders and hang on, unseen.</summary>
        void Sever(Transform bone);
    }

    public static class RagdollBodies
    {
        /// <summary>True when the object has a ragdoll body that is down (limp, held or standing up).</summary>
        public static bool IsDown(Component c)
        {
            if (c == null) return false;
            IRagdollBody body = c.GetComponentInParent<IRagdollBody>();
            return body != null && body.IsDown;
        }
    }
}
