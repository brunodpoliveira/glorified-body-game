using UnityEngine;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Saving
{
    /// <summary>
    /// A way out of the room (a door, stairs, a corridor's end): she walks into its trigger and the next room loads,
    /// with her at the RoomEntry of the given id (WorldState.Travel). A locked exit refuses until a key item or a
    /// story flag opens it (later: the sealed gate of code silver). The trigger sits past the doorway, so the entry
    /// of the room on the other side, inside its own doorway, never stands in an exit.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class RoomExit : MonoBehaviour
    {
        public string targetScene = "";
        public string targetEntry = "";
        [Tooltip("A story flag that must be set for the exit to work (empty: always open).")]
        public string requiredFlag = "";

        public static int Used { get; private set; }
        public bool IsOpen => string.IsNullOrEmpty(requiredFlag) || StoryFlags.Has(requiredFlag);

        private void Reset() => GetComponent<BoxCollider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player") && !other.transform.root.CompareTag("Player")) return;
            Use();
        }

        /// <summary>Goes through (the trigger, and tests). False when it is shut or a crossing is on its way.</summary>
        public bool Use()
        {
            if (!IsOpen || WorldState.Travelling) return false;
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return false;
            Used++;
            return WorldState.Travel(targetScene, targetEntry);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => Used = 0;
    }
}
