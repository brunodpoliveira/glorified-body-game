using GlorifiedBody.Systems;
using UnityEngine;

namespace GlorifiedBody.Saving
{
    /// <summary>
    /// A checkpoint (Bruno, 2026-10-25: checkpoints "only at specific save points", "one [slot] for the auto
    /// checkpoints"). When she walks into its box the game saves to the checkpoint slot, once a visit, and the screen
    /// says CHECKPOINT. A death restarts here. A load starts her at its middle, facing its forward.
    /// </summary>
    public sealed class CheckpointPoint : MonoBehaviour, ISpot
    {
        public Vector3 size = new Vector3(2.5f, 2.5f, 2.5f);

        private Transform lucy;

        public bool Reached { get; private set; }
        public string SpotId => name;
        public Pose SpawnPose => new Pose(transform.position, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));

        public void MarkReached() => Reached = true;

        public bool Contains(Vector3 point)
        {
            Vector3 local = transform.InverseTransformPoint(point);
            return Mathf.Abs(local.x) <= size.x * 0.5f && local.y >= -0.5f && local.y <= size.y && Mathf.Abs(local.z) <= size.z * 0.5f;
        }

        private void Update()
        {
            if (Reached) return;
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;
            if (lucy == null)
            {
                GameObject found = GameObject.FindWithTag("Player");
                if (found == null) return;
                lucy = found.transform;
            }
            if (!Contains(lucy.position)) return;
            Reached = true;
            SaveSystem.SaveCheckpoint(SpotId);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(new Vector3(0f, size.y * 0.5f - 0.25f, 0f), new Vector3(size.x, size.y + 0.5f, size.z));
        }
    }
}
