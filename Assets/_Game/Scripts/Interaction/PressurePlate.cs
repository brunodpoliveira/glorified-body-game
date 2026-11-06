using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.Combat;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Saving;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// A plate on the floor for the physics puzzles (Bruno, 2026-11-06: "this will allow us to do physics puzzles with boxes and
    /// whatnot"). It holds a story flag up while enough mass rests on it, and a box that she still carries does not count: the box
    /// must lie there. A FlagGate on that flag opens the way. Numbers PROPOSED.
    /// </summary>
    public sealed class PressurePlate : MonoBehaviour
    {
        public string flag = "";
        [Min(0f)] public float mass = 20f;
        [Tooltip("The part that sinks when it is pressed.")]
        public Transform top;
        public Vector3 size = new Vector3(1.2f, 0.4f, 1.2f);
        private const float Sink = 0.04f;

        private readonly HashSet<Rigidbody> seen = new HashSet<Rigidbody>();
        private Vector3 topAt;

        public bool Pressed { get; private set; }

        private void Awake()
        {
            if (top != null) topAt = top.localPosition;
        }

        private void FixedUpdate()
        {
            seen.Clear();
            float resting = 0f;
            Vector3 centre = transform.position + Vector3.up * (size.y * 0.5f);
            foreach (Collider other in Physics.OverlapBox(centre, size * 0.5f, transform.rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                Rigidbody body = other.attachedRigidbody;
                if (body == null || body.isKinematic || !seen.Add(body)) continue;
                if (body.GetComponent<TelekinesisLift>() != null) continue; // still in the air, in her hands
                resting += body.mass;
            }
            bool pressed = resting >= mass;
            if (pressed == Pressed) return;
            Pressed = pressed;
            if (top != null) top.localPosition = topAt + (pressed ? Vector3.down * Sink : Vector3.zero);
            RunTelemetry.Event("plate", $"{name}:{(pressed ? "down" : "up")}");
            if (string.IsNullOrEmpty(flag)) return;
            if (pressed) StoryFlags.Raise(flag);
            else StoryFlags.Drop(flag);
        }
    }
}
