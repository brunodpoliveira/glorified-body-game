using UnityEngine;

namespace GlorifiedBody.Story
{
    /// <summary>A box she walks into: it starts its sequence (once: the sequence's done flag stops a second run).</summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ScriptTrigger : MonoBehaviour
    {
        public ScriptedSequence sequence;

        private void Reset() => GetComponent<BoxCollider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            if (sequence == null) return;
            if (!other.CompareTag("Player") && !other.transform.root.CompareTag("Player")) return;
            sequence.Run();
        }
    }
}
