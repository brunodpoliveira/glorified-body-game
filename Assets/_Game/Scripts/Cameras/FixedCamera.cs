using UnityEngine;

namespace GlorifiedBody.Cameras
{
    /// <summary>
    /// One fixed camera of a room: a pose marker (this transform) and a field of view. The director copies the
    /// pose onto Camera.main when a CameraZone that points here becomes active. A camera may follow the player
    /// with its yaw inside a small cone, as some pre-rendered games pan their plates.
    /// </summary>
    public sealed class FixedCamera : MonoBehaviour
    {
        public string cameraName = "camera";
        [Min(1f)] public float fieldOfView = 50f;
        [Tooltip("Turn toward the player, but only up to followMaxYaw degrees from the base rotation.")]
        public bool followPlayerYaw;
        [Min(0f)] public float followMaxYaw = 20f;
        [Min(0f)] public float followSpeed = 60f;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
            Gizmos.DrawRay(transform.position, transform.forward * 2f);
        }
    }
}
