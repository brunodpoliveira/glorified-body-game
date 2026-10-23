using UnityEngine;

namespace GlorifiedBody.Combat
{
    /// <summary>A piece of a broken prop: it flies, falls, and vanishes after a second. No collider, no physics.</summary>
    public sealed class PropPiece : MonoBehaviour
    {
        public Vector3 Velocity;
        private float age;

        private void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            Velocity += Physics.gravity * dt;
            transform.position += Velocity * dt;
            if (transform.position.y < 0.03f)
            {
                transform.position = new Vector3(transform.position.x, 0.03f, transform.position.z);
                Velocity = Vector3.zero;
            }
            if (age > 1.2f) Destroy(gameObject);
        }
    }
}
