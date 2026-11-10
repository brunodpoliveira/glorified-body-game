using UnityEngine;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// A prop that mk_orzabal moves, whose place the save keeps (DEMO_PLAN §10.5, the planters: "the planters stay where she
    /// left them"). The room's save holds its path and its pose. A prop that broke is not in the save, and a load removes it.
    /// </summary>
    public sealed class MovableProp : MonoBehaviour
    {
        /// <summary>Puts it at the saved pose, at rest.</summary>
        public void Place(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            if (TryGetComponent(out Rigidbody body))
            {
                body.position = position;
                body.rotation = rotation;
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }
        }
    }
}
