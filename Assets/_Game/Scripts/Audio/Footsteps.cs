using UnityEngine;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;

namespace GlorifiedBody.Audio
{
    /// <summary>
    /// Steps by distance: a step each stride of ground the figure covers, a longer stride when it runs. Her bare feet
    /// are soft (step_lucy), the others wear boots (step_boots). No step when it is dead, limp or lifted, and none for a
    /// jump of more than a metre in a frame (a teleport, a load). AudioHost puts one on every figure of a room.
    /// </summary>
    public sealed class Footsteps : MonoBehaviour
    {
        public const float WalkStride = 0.75f;
        public const float RunStride = 1.25f;
        public const float RunSpeed = 2.6f;

        public string Key = Sfx.StepBoots;
        public float Volume = 0.55f;

        private Health health;
        private Vector3 last;
        private float travelled;

        public int Steps { get; private set; }

        private void Start()
        {
            health = GetComponent<Health>();
            last = transform.position;
        }

        private void Update()
        {
            Vector3 now = transform.position;
            Vector3 d = now - last;
            float dy = d.y;
            d.y = 0f;
            last = now;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            float distance = d.magnitude;
            if (distance > 1f || Mathf.Abs(dy) > 0.5f || (health != null && health.IsDead) || RagdollBodies.IsDown(this))
            {
                travelled = 0f;
                return;
            }
            travelled += distance;
            float stride = distance / dt >= RunSpeed ? RunStride : WalkStride;
            if (travelled < stride) return;
            travelled -= stride;
            Steps++;
            Sfx.Play(Key, now + Vector3.up * 0.05f, Volume, 0.08f);
        }
    }
}
