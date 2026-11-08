using System.Collections;
using UnityEngine;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Saving;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// A door that needs a key (Bruno, 2026-10-22: "a test key to open a locked door"). Without the key the
    /// Interact key shows "It is locked."; with it the game asks "Use the test key?", and a yes slides the leaf
    /// aside and clears the way. The key stays in the inventory (a key item takes no slot).
    /// The site code (DEMO_PLAN §10.2, the code rotation): once rotatedFlag is up, the card is refused ("CODE ROTATED.
    /// SEE SECURITY.", two bleats) until a terminal (CodeTerminal) raises enrolledFlag. The reader beside the door is red
    /// until then, and green after.
    /// </summary>
    public sealed class LockedDoor : Interactable
    {
        public string keyItemId = "test_key";
        public string keyName = "the test key";
        [Tooltip("The leaf that slides: its collider goes when the door opens.")]
        public Transform leaf;
        public Vector3 openOffset = new Vector3(-1.25f, 0f, 0f);
        [Min(0.05f)] public float openSeconds = 0.8f;

        [Header("The site code")]
        [Tooltip("When this flag is up, the card works only after a terminal enrols it. Empty: no code.")]
        public string rotatedFlag = "";
        [Tooltip("The flag the terminal raises when the card is enrolled.")]
        public string enrolledFlag = "";
        [Tooltip("The reader beside the door: red until the card works here, green after.")]
        public Renderer reader;

        public const string RotatedText = "CODE ROTATED. SEE SECURITY.";
        public static readonly Color ReaderRed = new Color(0.9f, 0.1f, 0.1f), ReaderGreen = new Color(0.15f, 0.85f, 0.25f);

        public bool IsOpen { get; private set; }

        /// <summary>The codes rotated and the card is not enrolled yet: the reader refuses it.</summary>
        public bool CodeBlocked => !string.IsNullOrEmpty(rotatedFlag) && StoryFlags.Has(rotatedFlag)
                                   && (string.IsNullOrEmpty(enrolledFlag) || !StoryFlags.Has(enrolledFlag));

        /// <summary>The reader shows green: the card works here now.</summary>
        public bool ReaderGreenNow => IsOpen || (!string.IsNullOrEmpty(enrolledFlag) && StoryFlags.Has(enrolledFlag));

        private bool? shownGreen;
        private MaterialPropertyBlock block;

        public override bool Available => base.Available && !IsOpen;

        public override string Hint => "open the door";

        public override InteractKind KindFor(Interactor who) => HasKey(who) && !CodeBlocked ? InteractKind.Ask : InteractKind.Message;

        public override string TextFor(Interactor who) => !HasKey(who) ? $"It is locked. It needs {keyName}." : CodeBlocked ? RotatedText : $"Use {keyName}?";

        public override string Shown(Interactor who)
        {
            if (!HasKey(who) || !CodeBlocked) return null;
            StartCoroutine(Bleats());
            RunTelemetry.Event("door_code_rotated", name);
            return "rotated:" + name;
        }

        public override string Confirm(Interactor who)
        {
            if (!HasKey(who)) return "locked:" + name;
            if (CodeBlocked) return "rotated:" + name;
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
            if (!instant) GlorifiedBody.Audio.Sfx.Play(GlorifiedBody.Audio.Sfx.DoorOpen, transform.position + Vector3.up * 1.2f, 0.9f);
        }

        private void Update()
        {
            // The reader of a coded door: red until the card works here, then green.
            if (reader == null || string.IsNullOrEmpty(enrolledFlag)) return;
            bool green = ReaderGreenNow;
            if (shownGreen == green) return;
            shownGreen = green;
            block ??= new MaterialPropertyBlock();
            reader.GetPropertyBlock(block);
            block.SetColor("_BaseColor", green ? ReaderGreen : ReaderRed);
            reader.SetPropertyBlock(block);
        }

        /// <summary>The two bleats of a refused card.</summary>
        private IEnumerator Bleats()
        {
            Vector3 at = reader != null ? reader.transform.position : transform.position + Vector3.up * 1.2f;
            GlorifiedBody.Audio.Sfx.Play(GlorifiedBody.Audio.Sfx.Refuse, at, 0.8f);
            yield return new WaitForSecondsRealtime(0.22f);
            GlorifiedBody.Audio.Sfx.Play(GlorifiedBody.Audio.Sfx.Refuse, at, 0.8f);
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
