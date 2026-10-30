using UnityEngine;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Saving;

namespace GlorifiedBody.Story
{
    /// <summary>
    /// The game remembers what she did to a person (the design sweep's rank 10, 2026-10-30): three story flags for the
    /// person's fate key ("marta"). "&lt;fate&gt;_killed" when the person dies, "&lt;fate&gt;_spared" when she leaves the room
    /// while the person lives, "&lt;fate&gt;_scanned" when she scans the person (the Scannable raises it). The saves keep
    /// the flags, and anything later reads them (a FlagGate, a ScriptedSequence). EnemySpawn adds this when its
    /// marker names a fate.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class FateFlag : MonoBehaviour
    {
        public string fate = "";

        private Health health;

        public string KilledFlag => fate + "_killed";
        public string SparedFlag => fate + "_spared";
        public string ScannedFlag => fate + "_scanned";

        private void Awake() => health = GetComponent<Health>();

        private void OnEnable()
        {
            health.OnDeath ??= new UnityEngine.Events.UnityEvent();
            health.OnDeath.AddListener(OnDeath);
            RoomExit.OnLeaving += OnLeaving;
        }

        private void OnDisable()
        {
            health.OnDeath?.RemoveListener(OnDeath);
            RoomExit.OnLeaving -= OnLeaving;
        }

        private void OnDeath()
        {
            if (string.IsNullOrEmpty(fate)) return;
            StoryFlags.Drop(SparedFlag);
            if (StoryFlags.Has(KilledFlag)) return;
            StoryFlags.Raise(KilledFlag);
            RunTelemetry.Event("fate", KilledFlag);
        }

        private void OnLeaving()
        {
            if (string.IsNullOrEmpty(fate) || health.IsDead || StoryFlags.Has(SparedFlag)) return;
            StoryFlags.Raise(SparedFlag);
            RunTelemetry.Event("fate", SparedFlag);
        }
    }
}
