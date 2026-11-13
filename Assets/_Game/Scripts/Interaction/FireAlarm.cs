using UnityEngine;
using GlorifiedBody.Audio;
using GlorifiedBody.Combat;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Saving;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// The fire alarm of a room (round 118, CANON: fire on the nurse starts the alarm and opens her door; DEMO_PLAN §3.5
    /// item 3, PROPOSED: the electric locks of the room release on a fire alarm, for life safety; §10.6: fire on the
    /// drapes of the table works too). A full circle of mk_erebus on anything inside the box raises the flag: the alarm
    /// lamps flash, the bell rings, and the doors that listen to the flag open (GlassDoor.openFlag). The flag is in the
    /// saves.
    /// </summary>
    public sealed class FireAlarm : MonoBehaviour
    {
        public string flag = "hold_h4_alarm";
        [Tooltip("The room's box, around this object.")]
        public Vector3 size = new Vector3(6f, 4f, 5f);
        public GameObject lamps;
        public GameObject flames;

        public bool Ringing => StoryFlags.Has(flag);
        public string LastCause { get; private set; } = "";

        private void OnEnable()
        {
            HitEventBus.OnHit += OnHit;
            StoryFlags.OnChanged += OnFlag;
        }

        private void OnDisable()
        {
            HitEventBus.OnHit -= OnHit;
            StoryFlags.OnChanged -= OnFlag;
        }

        private void Start() => Show();

        private void OnFlag(string _) => Show();

        private void Show()
        {
            bool on = StoryFlags.Has(flag);
            if (lamps != null) lamps.SetActive(on);
            if (flames != null && !on) flames.SetActive(false);
        }

        private void OnHit(HitInfo hit)
        {
            if (hit == null || hit.Victim == null || Ringing) return;
            if (hit.DamageType != DamageType.PsychicFire || hit.Charge < 0.999f) return;
            Vector3 local = transform.InverseTransformPoint(hit.Victim.transform.position);
            if (Mathf.Abs(local.x) > size.x * 0.5f || Mathf.Abs(local.z) > size.z * 0.5f || local.y < -1f || local.y > size.y) return;
            Raise(hit.Victim.name);
        }

        /// <summary>The alarm (a test may call it): the flag, the lamps, the bell.</summary>
        public void Raise(string cause)
        {
            if (Ringing) return;
            LastCause = cause;
            if (flames != null && cause.Contains("Drape")) flames.SetActive(true);
            StoryFlags.Raise(flag);
            Sfx.Play(Sfx.Fire, transform.position + Vector3.up * 1.5f, 0.9f);
            RunTelemetry.Event("fire_alarm", $"{name}:{cause}");
        }
    }
}
