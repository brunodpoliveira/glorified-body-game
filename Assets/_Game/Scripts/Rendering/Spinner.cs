using UnityEngine;

namespace GlorifiedBody.Rendering
{
    /// <summary>T26 switch 7, a live layer: turns its object about a local axis (the ceiling fan; its shadow sweeps the floor).</summary>
    public sealed class Spinner : MonoBehaviour
    {
        public Vector3 axis = Vector3.up;
        public float degreesPerSecond = 220f;

        private void Update()
        {
            transform.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.Self);
        }
    }
}
