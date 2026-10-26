using System.Collections;
using UnityEngine;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// A door that needs a key (Bruno, 2026-10-22: "a test key to open a locked door"). Without the key the
    /// Interact key shows "It is locked."; with it the game asks "Use the test key?", and a yes slides the leaf
    /// aside and clears the way. The key stays in the inventory (a key item takes no slot).
    /// </summary>
    public sealed class LockedDoor : Interactable
    {
        public string keyItemId = "test_key";
        public string keyName = "the test key";
        [Tooltip("The leaf that slides: its collider goes when the door opens.")]
        public Transform leaf;
        public Vector3 openOffset = new Vector3(-1.25f, 0f, 0f);
        [Min(0.05f)] public float openSeconds = 0.8f;

        public bool IsOpen { get; private set; }

        public override bool Available => base.Available && !IsOpen;

        public override string Hint => "open the door";

        public override InteractKind KindFor(Interactor who) => HasKey(who) ? InteractKind.Ask : InteractKind.Message;

        public override string TextFor(Interactor who) => HasKey(who) ? $"Use {keyName}?" : $"It is locked. It needs {keyName}.";

        public override string Confirm(Interactor who)
        {
            if (!HasKey(who)) return "locked:" + name;
            Open();
            return "opened:" + name;
        }

        private bool HasKey(Interactor who) => who != null && who.Inventory != null && who.Inventory.Has(keyItemId);

        /// <summary>Opens the door now (the key path, and tests). Instant: no slide (a load of a save).</summary>
        public void Open(bool instant = false)
        {
            if (IsOpen) return;
            IsOpen = true;
            if (leaf != null)
            {
                foreach (Collider c in leaf.GetComponentsInChildren<Collider>()) c.enabled = false;
                if (instant) leaf.position += openOffset;
                else StartCoroutine(Slide());
            }
            RunTelemetry.Event("door_opened", name);
        }

        private IEnumerator Slide()
        {
            Vector3 from = leaf.position, to = leaf.position + openOffset;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / openSeconds;
                leaf.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
        }
    }
}
