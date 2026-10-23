using System.Collections.Generic;
using UnityEngine;

namespace GlorifiedBody.Combat
{
    /// <summary>
    /// Lightweight static registry of <see cref="LockOnTargetable"/> instances.
    ///
    /// Replaces a per-frame OverlapSphere scan
    /// with O(N) traversal of an in-memory list. Targets self-register in
    /// <c>OnEnable</c> and deregister in <c>OnDisable</c>/<c>OnDestroy</c>, so
    /// no manager scene object is required.
    ///
    /// Adopted from MIS' <c>mvTargetManager</c> (camera-yaw sort + signed angle
    /// for cycle), but with no Invector/MIS dependency.
    /// </summary>
    public static class TargetRegistry
    {
        private static readonly List<LockOnTargetable> targets = new List<LockOnTargetable>();
        private static readonly List<ScoredTarget> scratch = new List<ScoredTarget>();

        private struct ScoredTarget
        {
            public LockOnTargetable target;
            public float angle;
            public float signedAngle;
            public float distance;
        }

        public static IReadOnlyList<LockOnTargetable> All => targets;

        public static void Register(LockOnTargetable t)
        {
            if (t == null || targets.Contains(t))
            {
                return;
            }
            targets.Add(t);
        }

        public static void Deregister(LockOnTargetable t)
        {
            if (t == null)
            {
                return;
            }
            targets.Remove(t);
        }

        /// <summary>
        /// Cleared automatically when the play session ends; safety hook for editor reloads.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            targets.Clear();
            scratch.Clear();
        }

        /// <summary>
        /// Returns the best target for an initial lock-on. Score = angle * 1.5 + dist * 0.35
        /// (matches the legacy bespoke scoring so feel is preserved).
        /// </summary>
        public static LockOnTargetable FindBest(
            Camera camera,
            Vector3 originWorld,
            Transform owner,
            float maxDistance,
            float maxAngle,
            LayerMask obstructionMask,
            bool requireLineOfSight,
            bool includeProps = false)
        {
            Vector3 viewForward = camera != null ? camera.transform.forward : (owner != null ? owner.forward : Vector3.forward);

            float bestScore = float.MaxValue;
            LockOnTargetable best = null;

            for (int i = 0; i < targets.Count; i++)
            {
                LockOnTargetable t = targets[i];
                if (!IsCandidate(t, owner, includeProps))
                {
                    continue;
                }

                Vector3 lockPos = t.GetLockPoint();
                Vector3 toTarget = lockPos - originWorld;
                float distance = toTarget.magnitude;
                if (distance > maxDistance || distance < 0.0001f)
                {
                    continue;
                }

                float angle = Vector3.Angle(viewForward, toTarget);
                if (angle > maxAngle)
                {
                    continue;
                }

                if (requireLineOfSight && !HasLineOfSight(originWorld, lockPos, t, obstructionMask))
                {
                    continue;
                }

                float score = (angle * 1.5f) + (distance * 0.35f);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = t;
                }
            }

            return best;
        }

        /// <summary>
        /// Returns the next valid target on the chosen visual side (-1 = left, +1 = right).
        /// Sorting follows MIS' approach: signed yaw angle relative to camera-forward, with
        /// the *current* target excluded. Falls back to wrap-around so cycling never stalls.
        /// Props join the cycle only with includeProps (telekinesis selected).
        /// </summary>
        public static LockOnTargetable Cycle(
            LockOnTargetable current,
            int direction,
            Camera camera,
            Vector3 originWorld,
            Transform owner,
            float maxDistance,
            float maxAngle,
            LayerMask obstructionMask,
            bool requireLineOfSight,
            bool includeProps = false)
        {
            if (direction == 0)
            {
                return current;
            }

            Vector3 viewForward = camera != null ? camera.transform.forward : (owner != null ? owner.forward : Vector3.forward);
            Vector2 forwardFlat = new Vector2(viewForward.x, viewForward.z);
            if (forwardFlat.sqrMagnitude < 0.0001f)
            {
                forwardFlat = new Vector2(0f, 1f);
            }

            scratch.Clear();
            for (int i = 0; i < targets.Count; i++)
            {
                LockOnTargetable t = targets[i];
                if (!IsCandidate(t, owner, includeProps) || t == current)
                {
                    continue;
                }

                Vector3 lockPos = t.GetLockPoint();
                Vector3 toTarget = lockPos - originWorld;
                float distance = toTarget.magnitude;
                if (distance > maxDistance || distance < 0.0001f)
                {
                    continue;
                }

                Vector2 toTargetFlat = new Vector2(toTarget.x, toTarget.z);
                float angle = Vector2.Angle(forwardFlat, toTargetFlat);
                if (angle > maxAngle)
                {
                    continue;
                }

                if (requireLineOfSight && !HasLineOfSight(originWorld, lockPos, t, obstructionMask))
                {
                    continue;
                }

                float signedAngle = Vector2.SignedAngle(forwardFlat, toTargetFlat);
                scratch.Add(new ScoredTarget
                {
                    target = t,
                    angle = angle,
                    signedAngle = signedAngle,
                    distance = distance
                });
            }

            if (scratch.Count == 0)
            {
                return current;
            }

            // Reference signed-angle of the current target (if any).
            float currentSignedAngle = 0f;
            bool hasCurrent = false;
            if (current != null)
            {
                Vector3 currentLock = current.GetLockPoint();
                Vector3 toCurrent = currentLock - originWorld;
                Vector2 toCurrentFlat = new Vector2(toCurrent.x, toCurrent.z);
                if (toCurrentFlat.sqrMagnitude > 0.0001f)
                {
                    currentSignedAngle = Vector2.SignedAngle(forwardFlat, toCurrentFlat);
                    hasCurrent = true;
                }
            }

            // Vector2.SignedAngle: positive when target is on the LEFT of forward (CCW
            // looking down +Y in our XZ plane), negative on the RIGHT.
            //   direction = -1 (cycle left)  → look for signedAngle > current
            //   direction = +1 (cycle right) → look for signedAngle < current
            LockOnTargetable bestOnSide = null;
            float bestSideDelta = float.MaxValue;
            LockOnTargetable bestWrap = null;
            float bestWrapAngle = direction < 0 ? float.MaxValue : float.MinValue;

            for (int i = 0; i < scratch.Count; i++)
            {
                ScoredTarget s = scratch[i];
                float delta = s.signedAngle - currentSignedAngle;

                bool onChosenSide = direction < 0 ? delta > 0.5f : delta < -0.5f;
                if (hasCurrent && onChosenSide)
                {
                    float absDelta = Mathf.Abs(delta);
                    if (absDelta < bestSideDelta)
                    {
                        bestSideDelta = absDelta;
                        bestOnSide = s.target;
                    }
                }

                // Wrap-around fallback: when no target on the chosen side, pick the
                // furthest target on the *opposite* side so a repeated press cycles
                // round the world consistently.
                if (direction < 0)
                {
                    if (s.signedAngle < bestWrapAngle)
                    {
                        bestWrapAngle = s.signedAngle;
                        bestWrap = s.target;
                    }
                }
                else
                {
                    if (s.signedAngle > bestWrapAngle)
                    {
                        bestWrapAngle = s.signedAngle;
                        bestWrap = s.target;
                    }
                }
            }

            if (bestOnSide != null)
            {
                return bestOnSide;
            }

            return bestWrap != null ? bestWrap : current;
        }

        private static bool IsCandidate(LockOnTargetable t, Transform owner, bool includeProps)
        {
            if (t == null || !t.Selectable || (t.IsProp && !includeProps))
            {
                return false;
            }

            Transform tr = t.transform;
            if (owner != null && (tr == owner || tr.IsChildOf(owner)))
            {
                return false;
            }

            return t.IsAlive;
        }

        private static bool HasLineOfSight(Vector3 origin, Vector3 lockPos, LockOnTargetable target, LayerMask obstructionMask)
        {
            if (!Physics.Linecast(origin, lockPos, out RaycastHit hit, obstructionMask, QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            // Hit was on (or under) the target itself → still considered visible.
            return hit.transform == target.transform || hit.transform.IsChildOf(target.transform);
        }
    }
}
