using UnityEngine;

namespace GlorifiedBody.Level.Build
{
    /// <summary>Camera marker for automated screenshots (gb_shots). Looks along +Z of this transform.</summary>
    public class ShotPoint : MonoBehaviour
    {
        public string shotName = "shot";
        public float fieldOfView = 60f;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.4f);
            Gizmos.DrawRay(transform.position, transform.forward * 3f);
        }
    }
}
