using UnityEngine;

namespace GlorifiedBody.Saving
{
    /// <summary>
    /// Where she comes into a room through an exit of another room (RoomExit names it). Its id is the object's name.
    /// She stands on it, facing its forward. A room builder puts one on the inner side of each door.
    /// </summary>
    public sealed class RoomEntry : MonoBehaviour, ISpot
    {
        public string SpotId => name;
        public Pose SpawnPose => new Pose(transform.position, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
        public void MarkReached() { }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.1f, 0.3f);
            Gizmos.DrawRay(transform.position + Vector3.up * 0.1f, transform.forward);
        }
    }
}
