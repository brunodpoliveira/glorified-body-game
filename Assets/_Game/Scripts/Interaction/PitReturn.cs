using UnityEngine;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Player;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// A gap in a floor (Bruno, 2026-11-06: "a gap that needs to be filled in by a box"): a step into it does not kill her and does
    /// not trap her. She climbs back out on the side she came from. A box in the pit lies level with the floor, and she walks over it.
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

        private TankController tank;

        public int Returns { get; private set; }

        private void Update()
        {
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
    }
}
