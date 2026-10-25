using System;
using UnityEngine;
using GlorifiedBody.Combat;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Player
{
    /// <summary>
    /// Auto-lock on the nearest enemy, with a button to switch targets (design §5.3). The lock needs no
    /// input: the nearest live LockOnTargetable inside lockRange is the target, and SwitchTarget cycles to the
    /// next one by yaw. The lock drops when the target dies or leaves dropRange. While Charge is held, Lucy
    /// stands still (the circle charge of step 3) and turns to face the target. A small marker floats over
    /// the target so the lock reads in the greybox. Props (crates): the auto-lock never picks one; the switch
    /// cycles through them too while IncludeProps is on (telekinesis selected, set by PsychicPowers), and a prop
    /// lock drops when it goes off.
    /// </summary>
    [RequireComponent(typeof(TankController))]
    public sealed class LockOnController : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float lockRange = 14f;
        [SerializeField, Min(1f)] private float dropRange = 18f;
        [SerializeField, Min(0f)] private float faceTurnSpeed = 360f;
        [SerializeField] private LayerMask obstructionMask = 0;
        [SerializeField] private bool requireLineOfSight = false;

        private TankController tank;
        private Transform marker;
        [SerializeField] private bool showMarker = true;

        public LockOnTargetable CurrentTarget { get; private set; }
        public bool HasTarget => CurrentTarget != null;
        public bool IsCharging { get; private set; }
        public string TargetName => CurrentTarget != null ? CurrentTarget.name : "none";
        /// <summary>The switch includes props (telekinesis selected). PsychicPowers sets it.</summary>
        public bool IncludeProps { get; set; }

        public event Action<LockOnTargetable> OnTargetChanged;

        private void Awake()
        {
            tank = GetComponent<TankController>();
            BuildMarker();
        }

        private void OnDisable()
        {
            if (IsCharging)
            {
                IsCharging = false;
                tank.LockMovement(false);
            }
        }

        /// <summary>Forces a target (tests). Null clears the lock.</summary>
        public void SetTarget(LockOnTargetable target)
        {
            if (target == CurrentTarget) return;
            CurrentTarget = target;
            RunTelemetry.Event(target != null ? "lock_on" : "lock_off", target != null ? target.name : "");
            OnTargetChanged?.Invoke(target);
        }

        /// <summary>Switches to the next target to the right (yaw order). Returns false when no other target exists.</summary>
        public bool SwitchTarget()
        {
            LockOnTargetable next = TargetRegistry.Cycle(CurrentTarget, +1, null, transform.position, transform,
                lockRange, 360f, obstructionMask, requireLineOfSight, IncludeProps);
            if (next == null || next == CurrentTarget) return false;
            SetTarget(next);
            return true;
        }

        private void Update()
        {
            if (Time.deltaTime <= 0f) return;

            // Drop a dead, gone, or far target.
            if (CurrentTarget != null)
            {
                bool gone = !CurrentTarget.Selectable || !CurrentTarget.IsAlive || (CurrentTarget.IsProp && !IncludeProps);
                bool far = (CurrentTarget.GetLockPoint() - transform.position).sqrMagnitude > dropRange * dropRange;
                if (gone || far) SetTarget(null);
            }

            // Auto-lock: the nearest live target in range, never a civilian. Distance only, no angle (she turns to it).
            if (CurrentTarget == null)
            {
                LockOnTargetable nearest = Nearest();
                if (nearest != null) SetTarget(nearest);
            }

            if (GameInput.GetActionDown(InputManager.GameAction.SwitchTarget, KeyCode.Tab))
            {
                SwitchTarget();
            }

            bool charge = GameInput.GetAction(InputManager.GameAction.Charge, KeyCode.Mouse0) && !tank.IsDodging;
            if (charge != IsCharging)
            {
                IsCharging = charge;
                tank.LockMovement(charge);
                RunTelemetry.Event(charge ? "charge_start" : "charge_end", TargetName);
            }

            if (IsCharging && CurrentTarget != null)
            {
                tank.FaceTowards(CurrentTarget.GetLockPoint(), faceTurnSpeed);
            }

            UpdateMarker();
        }

        private LockOnTargetable Nearest()
        {
            LockOnTargetable best = null;
            float bestDistance = lockRange * lockRange;
            var all = TargetRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                LockOnTargetable t = all[i];
                if (t == null || !t.Selectable || t.IsProp || t.IsCivilian || !t.IsAlive || t.transform == transform || t.transform.IsChildOf(transform)) continue;
                float d = (t.GetLockPoint() - transform.position).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = t;
                }
            }
            return best;
        }

        private void BuildMarker()
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            obj.name = "LockMarker";
            var collider = obj.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            obj.transform.localScale = Vector3.one * 0.25f;
            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                if (shader != null)
                {
                    renderer.material = new Material(shader) { color = new Color(1f, 0.15f, 0.15f) };
                }
            }
            marker = obj.transform;
            marker.gameObject.SetActive(false);
        }

        /// <summary>The red sphere over the target. Off in the look test, where the picture is the point.</summary>
        public bool ShowMarker
        {
            get => showMarker;
            set => showMarker = value;
        }

        /// <summary>The marker floats this far over the top of the target's meshes (PROPOSED).</summary>
        public const float MarkerClearance = 0.3f;
        public Vector3 MarkerPosition => marker != null ? marker.position : Vector3.zero;
        public bool MarkerVisible => marker != null && marker.gameObject.activeSelf;

        private LockOnTargetable markerOwner;
        private Renderer[] markerRenderers = Array.Empty<Renderer>();

        private void UpdateMarker()
        {
            if (marker == null) return;
            bool show = showMarker && CurrentTarget != null;
            if (marker.gameObject.activeSelf != show) marker.gameObject.SetActive(show);
            if (show) marker.position = MarkerPoint(CurrentTarget);
        }

        /// <summary>
        /// Over the head of the target, from its own meshes: a fixed height hid the marker inside the 2.6 m power
        /// armour (Bruno, 2026-10-24). Without a mesh, 0.9 m over the display point.
        /// </summary>
        private Vector3 MarkerPoint(LockOnTargetable target)
        {
            if (markerOwner != target)
            {
                markerOwner = target;
                var found = new System.Collections.Generic.List<Renderer>();
                foreach (Renderer r in target.GetComponentsInChildren<Renderer>())
                {
                    if (r is SkinnedMeshRenderer || r is MeshRenderer) found.Add(r);
                }
                markerRenderers = found.ToArray();
            }
            float top = float.MinValue;
            foreach (Renderer r in markerRenderers)
            {
                if (r != null && r.enabled && r.gameObject.activeInHierarchy) top = Mathf.Max(top, r.bounds.max.y);
            }
            Vector3 display = target.GetDisplayPoint();
            if (top == float.MinValue) return display + Vector3.up * 0.9f;
            return new Vector3(target.transform.position.x, top + MarkerClearance, target.transform.position.z);
        }

        private void OnDestroy()
        {
            if (marker != null) Destroy(marker.gameObject);
        }
    }
}
