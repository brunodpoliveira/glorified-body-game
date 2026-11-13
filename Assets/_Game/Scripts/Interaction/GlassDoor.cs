using UnityEngine;
using GlorifiedBody.Audio;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Saving;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// The locked glass door of the booth in H4 (round 118, CANON: "either set her on fire to activate the fire alarm and
    /// open the door, or use warspite to break it down"; DEMO_PLAN §10.6: the first blast cracks it, the second breaks
    /// it). A lock target that does not lift (heavy, not a prop, as the box wall of G17). Its electric lock releases on
    /// the fire alarm (openFlag, §3.5 item 3): the leaf slides into the wall. The flags keep both outcomes in the saves.
    /// The Interact key on the shut door shows its sign.
    /// </summary>
    public sealed class GlassDoor : Interactable
    {
        public const int BlastsToBreak = 2;

        public Transform leaf;
        public Vector3 openOffset = new Vector3(-1.2f, 0f, 0f);
        public string openFlag = "hold_h4_alarm";
        public string brokenFlag = "hold_h4_glass_broken";
        public GameObject crack;
        public GameObject shards;
        public string lockedText = "LOCKED FROM INSIDE. IN A FIRE ALARM THIS DOOR RELEASES.";

        public int Blasts { get; private set; }
        public bool IsOpen { get; private set; }
        public bool IsBroken { get; private set; }

        private Vector3 shut;

        public override bool Available => base.Available && !IsOpen && !IsBroken;
        public override string Hint => "try the door";
        public override InteractKind KindFor(Interactor who) => InteractKind.Message;
        public override string TextFor(Interactor who) => lockedText;
        public override string Confirm(Interactor who) => "locked:" + name;

        private void Awake()
        {
            if (leaf != null) shut = leaf.position;
            if (crack != null) crack.SetActive(false);
            if (shards != null) shards.SetActive(false);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            HitEventBus.OnHit += OnHit;
            StoryFlags.OnChanged += OnFlag;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            HitEventBus.OnHit -= OnHit;
            StoryFlags.OnChanged -= OnFlag;
        }

        private void Start()
        {
            if (StoryFlags.Has(brokenFlag)) Shatter(quiet: true);
            else if (StoryFlags.Has(openFlag)) Open(quiet: true);
        }

        private void OnFlag(string flag)
        {
            if (!IsOpen && !IsBroken && StoryFlags.Has(openFlag)) Open(quiet: false);
        }

        private void OnHit(HitInfo hit)
        {
            if (hit == null || IsOpen || IsBroken || hit.Victim == null) return;
            if (hit.Victim != gameObject && (leaf == null || hit.Victim != leaf.gameObject)) return;
            if (hit.DamageType != DamageType.PsychicBlast) return;
            Blasts++;
            RunTelemetry.Event("glass_blast", $"{name}:{Blasts}");
            if (Blasts >= BlastsToBreak) Shatter(quiet: false);
            else
            {
                if (crack != null) crack.SetActive(true);
                Sfx.Play(Sfx.DoorLocked, transform.position + Vector3.up * 1.2f, 0.9f); // the crack (placeholder)
            }
        }

        /// <summary>The fire alarm releases the lock: the leaf slides into the wall.</summary>
        public void Open(bool quiet)
        {
            if (IsOpen || IsBroken) return;
            IsOpen = true;
            if (leaf != null) leaf.position = shut + openOffset;
            Clear();
            if (!quiet) Sfx.Play(Sfx.DoorOpen, transform.position + Vector3.up * 1.2f, 0.9f);
            RunTelemetry.Event("glass_open", name);
        }

        /// <summary>The second blast: the glass falls out of the frame.</summary>
        public void Shatter(bool quiet)
        {
            if (IsBroken) return;
            IsBroken = true;
            if (leaf != null) leaf.gameObject.SetActive(false);
            if (crack != null) crack.SetActive(false);
            if (shards != null) shards.SetActive(true);
            Clear();
            StoryFlags.Raise(brokenFlag);
            if (!quiet) Sfx.Play(Sfx.DoorClose, transform.position + Vector3.up * 1.2f, 1f); // the glass (placeholder)
            RunTelemetry.Event("glass_broken", name);
        }

        /// <summary>No collider, no carve, no lock target: the way is open.</summary>
        private void Clear()
        {
            foreach (Collider c in GetComponents<Collider>()) c.enabled = false;
            if (leaf != null) foreach (Collider c in leaf.GetComponentsInChildren<Collider>()) c.enabled = false;
            UnityEngine.AI.NavMeshObstacle carve = GetComponent<UnityEngine.AI.NavMeshObstacle>();
            if (carve != null) carve.enabled = false;
            LockOnTargetable target = GetComponent<LockOnTargetable>();
            if (target != null) target.SetSelectable(false);
        }
    }
}
