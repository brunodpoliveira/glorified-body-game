using System.Collections.Generic;
using UnityEngine;

namespace GlorifiedBody.Interaction
{
    /// <summary>The answer of an Interactable to the Interact key.</summary>
    public enum InteractKind
    {
        /// <summary>Ask first ("Take the test log?", "Use the test key?"), act on a yes.</summary>
        Ask,
        /// <summary>Only a message ("It is locked."), no question.</summary>
        Message
    }

    /// <summary>
    /// Anything the Interact key reaches: an item to take (ItemPickup), a door (LockedDoor). The interactor
    /// picks the nearest one in its range inside the cone of her facing, asks the question, and calls Confirm on
    /// a yes. All of them register here, so no physics query is needed.
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        [Min(0.3f)] public float range = 1.6f;
        [Tooltip("The point the reach and the facing are measured to. Empty = this transform.")]
        public Transform point;

        private static readonly List<Interactable> all = new List<Interactable>();
        public static IReadOnlyList<Interactable> All => all;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => all.Clear();

        protected virtual void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
        }

        protected virtual void OnDisable()
        {
            all.Remove(this);
        }

        public Vector3 Point => point != null ? point.position : transform.position;

        /// <summary>False hides it from the interactor (an open door, a taken item).</summary>
        public virtual bool Available => isActiveAndEnabled;

        /// <summary>The words of the hint after the key name: "take the test log", "open the door".</summary>
        public abstract string Hint { get; }

        /// <summary>What the key does now for this player: ask a question, or only show a message.</summary>
        public abstract InteractKind KindFor(Interactor who);

        /// <summary>The question ("Take the test log?") or the message ("It is locked.").</summary>
        public abstract string TextFor(Interactor who);

        /// <summary>The yes. Returns the result word for the telemetry and the tests ("taken:test_log", "full:test_log", "opened:door").</summary>
        public abstract string Confirm(Interactor who);
    }
}
