using UnityEngine;
using GlorifiedBody.Combat;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Player;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// A gap in a floor (Bruno, 2026-11-06: "a gap that needs to be filled in by a box"): a step into it does not kill her and does
    /// not trap her. She climbs back out on the side she came from. A box in the pit lies level with the floor, and she walks over it.
    /// The gap leaves a box 5 cm on each side, so the pit guides a box that comes to rest on its rim or near it: it slides over the
    /// gap and falls in (2026-11-08: the drop landed on the rim when the frame time changed). A held or flying box is left alone.
    /// </summary>
    public sealed class PitReturn : MonoBehaviour
    {
        [Tooltip("The pit's size, flat, around this object: x across, z along.")]
        public Vector2 size = new Vector2(1.2f, 6f);
        [Tooltip("Where she stands again, on the side she came from.")]
        public Transform back;
        [Tooltip("She counts as fallen this far under the room's floor.")]
        [Min(0.05f)] public float fallen = 0.35f;
        [Tooltip("The room's floor, in the height of this object.")]
        public float floorAbove = 1.1f;

        [Tooltip("A box at rest this far past the edge of the gap slides into it, metres.")]
        [Min(0f)] public float guide = 0.45f;

        private TankController tank;
        private readonly Collider[] near = new Collider[16];
        private float nextGuide;

        public int Returns { get; private set; }
        public int Guided { get; private set; }

        private void Update()
        {
            if (Time.time >= nextGuide)
            {
                nextGuide = Time.time + 0.2f;
                GuideBoxes();
            }
            if (tank == null)
            {
                GameObject her = GameObject.FindWithTag("Player");
                tank = her != null ? her.GetComponent<TankController>() : null;
                if (tank == null) return;
            }
            Vector3 at = tank.transform.position - transform.position;
            if (Mathf.Abs(at.x) > size.x * 0.5f || Mathf.Abs(at.z) > size.y * 0.5f || at.y > floorAbove - fallen) return;
            Returns++;
            tank.Teleport(back.position, tank.transform.eulerAngles.y);
            RunTelemetry.Event("pit_return", name);
        }

        private void GuideBoxes()
        {
            Vector3 c = transform.position;
            float floorY = c.y + floorAbove;
            int n = Physics.OverlapBoxNonAlloc(new Vector3(c.x, floorY + 0.8f, c.z), new Vector3(size.x * 0.5f + guide, 0.9f, size.y * 0.5f),
                near, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Rigidbody box = near[i].attachedRigidbody;
                if (box == null || box.isKinematic || box.GetComponent<TelekinesisLift>() != null) continue;
                LockOnTargetable prop = box.GetComponent<LockOnTargetable>();
                if (prop == null || !prop.IsProp) continue;
                if (box.linearVelocity.magnitude > 0.5f) continue; // still in the air or sliding
                Vector3 local = Quaternion.Inverse(transform.rotation) * (box.position - c);
                if (local.y < floorAbove - 0.3f || Mathf.Abs(local.x) < 0.02f) continue; // already in, or already centred
                if (Mathf.Abs(local.x) > size.x * 0.5f + guide) continue;
                float half = near[i].bounds.extents.z;
                local.x = 0f;
                local.z = Mathf.Clamp(local.z, -size.y * 0.5f + half, size.y * 0.5f - half);
                box.position = c + transform.rotation * local;
                box.rotation = transform.rotation; // square to the gap
                box.linearVelocity = Vector3.zero;
                box.angularVelocity = Vector3.zero;
                Guided++;
                RunTelemetry.Event("pit_guides", box.name);
            }
        }
    }
}
