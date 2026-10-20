using UnityEngine;
using GlorifiedBody.Core;

namespace GlorifiedBody.Combat
{
    /// <summary>
    /// Marks an enemy (or any hostile actor) as a valid lock-on target and exposes
    /// a deterministic aim point for the player's lock-on system.
    ///
    /// Authoring guidance:
    ///   - Place on the same GameObject as the enemy's <see cref="Health"/> root.
    ///   - Drag a chest/torso bone (e.g. Mixamo Spine1/Spine2) into <c>lockPoint</c>.
    ///     If left null, a deterministic chest-height fallback is used.
    /// </summary>
    [DisallowMultipleComponent]
    public class LockOnTargetable : MonoBehaviour
    {
        [Header("Aim Point")]
        [Tooltip("Optional explicit aim transform (chest/torso bone preferred). If null, a fallback height is used.")]
        [SerializeField] private Transform lockPoint;
        [Tooltip("Optional UI anchor for the reticle. Defaults to lockPoint when null.")]
        [SerializeField] private Transform displayPoint;
        [Tooltip("Fallback aim height (world-up offset from this transform) when lockPoint is null.")]
        [SerializeField] private float fallbackHeight = 1.2f;

        [Header("Behaviour")]
        [Tooltip("If false, the registry will skip this target during scans.")]
        [SerializeField] private bool selectable = true;

        private Health cachedHealth;
        private bool registered;

        public Transform LockPoint => lockPoint != null ? lockPoint : transform;
        public Transform DisplayPoint => displayPoint != null ? displayPoint : LockPoint;
        public bool Selectable => selectable && isActiveAndEnabled;

        private void Awake()
        {
            cachedHealth = GetComponent<Health>();
            if (cachedHealth == null)
            {
                cachedHealth = GetComponentInParent<Health>();
            }
            if (cachedHealth == null)
            {
                cachedHealth = GetComponentInChildren<Health>();
            }
        }

        private void OnValidate()
        {
            fallbackHeight = Mathf.Max(0f, fallbackHeight);
        }

        private void OnEnable()
        {
            if (!registered)
            {
                TargetRegistry.Register(this);
                registered = true;
            }
        }

        private void OnDisable()
        {
            if (registered)
            {
                TargetRegistry.Deregister(this);
                registered = false;
            }
        }

        private void OnDestroy()
        {
            if (registered)
            {
                TargetRegistry.Deregister(this);
                registered = false;
            }
        }

        /// <summary>
        /// World-space aim point. Prefers the bound transform; otherwise falls back to a
        /// deterministic chest-height offset above this transform.
        /// </summary>
        public Vector3 GetLockPoint()
        {
            if (lockPoint != null)
            {
                return lockPoint.position;
            }

            return transform.position + Vector3.up * fallbackHeight;
        }

        public Vector3 GetDisplayPoint()
        {
            if (displayPoint != null)
            {
                return displayPoint.position;
            }

            return GetLockPoint();
        }

        public bool IsAlive
        {
            get
            {
                if (cachedHealth != null && cachedHealth.IsDead) return false;
                return true; // Step 3 adds the enemy lifecycle checks
            }
        }

        public void SetSelectable(bool value)
        {
            selectable = value;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.85f);
            Vector3 p = lockPoint != null ? lockPoint.position : transform.position + Vector3.up * fallbackHeight;
            Gizmos.DrawWireSphere(p, 0.18f);
        }
#endif
    }
}
