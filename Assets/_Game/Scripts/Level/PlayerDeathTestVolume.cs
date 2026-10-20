using UnityEngine;
using GlorifiedBody.Core;

namespace GlorifiedBody.Level
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class PlayerDeathTestVolume : MonoBehaviour
    {
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool bypassSecondWind;

        private void Reset()
        {
            SetTrigger();
        }

        private void OnValidate()
        {
            SetTrigger();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null)
            {
                return;
            }

            Transform root = other.transform.root;
            bool isPlayer = other.CompareTag(playerTag) || (root != null && root.CompareTag(playerTag));
            if (!isPlayer)
            {
                return;
            }

            Health health = other.GetComponentInParent<Health>();
            if (health != null && !health.IsDead)
            {
                if (bypassSecondWind)
                {
                    health.SetSecondWindEnabled(false);
                    health.SetRuntimeInvulnerable(false);
                }

                health.TakeDamage(health.CurrentHealth + health.MaxHealth + 999f);
            }
        }

        private void SetTrigger()
        {
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }
    }
}
