using UnityEngine;

namespace GlorifiedBody.Cameras
{
    /// <summary>
    /// A box volume of a room. While the player stands inside, the director shows this zone's FixedCamera.
    /// The director tests containment every frame (local-space box test), so a teleport, a respawn or a missed
    /// trigger event never leaves a stale camera. Overlapping zones: the higher priority wins, then the last entered.
    /// The BoxCollider only gives the box its shape and is a trigger, so it never blocks anyone.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class CameraZone : MonoBehaviour
    {
        public FixedCamera fixedCamera;
        public int priority;

        private BoxCollider box;

        private void Reset()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void Awake()
        {
            box = GetComponent<BoxCollider>();
            // Off the Default layer: queries hit triggers in this project, and Invector's sight line (a Linecast on
            // Default) hit the zone around her, so no enemy saw her while she stood in a zone (2026-10-25). The
            // director tests containment itself, so the zone needs no trigger events.
            int ignore = LayerMask.NameToLayer("Ignore Raycast");
            if (ignore >= 0) gameObject.layer = ignore;
        }

        private void OnEnable()
        {
            FixedCameraDirector.RegisterZone(this);
        }

        private void OnDisable()
        {
            FixedCameraDirector.UnregisterZone(this);
        }

        /// <summary>True when the world point lies inside the box (rotation and scale included).</summary>
        public bool Contains(Vector3 worldPoint)
        {
            if (box == null) box = GetComponent<BoxCollider>();
            if (box == null) return false;
            Vector3 local = transform.InverseTransformPoint(worldPoint) - box.center;
            Vector3 half = box.size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }
    }
}
