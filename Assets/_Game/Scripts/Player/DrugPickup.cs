using UnityEngine;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Player
{
    /// <summary>A drug on the floor. Lucy walks into it and takes it, up to the outfit's capacity.</summary>
    [RequireComponent(typeof(Collider))]
    public sealed class DrugPickup : MonoBehaviour
    {
        public DrugType drugType = DrugType.Nalcon;
        [Min(1)] public int amount = 1;
        [SerializeField] private string playerTag = "Player";

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            Transform root = other.transform.root;
            bool isPlayer = other.CompareTag(playerTag) || (root != null && root.CompareTag(playerTag));
            if (!isPlayer) return;
            DrugInventory inventory = other.GetComponentInParent<DrugInventory>() ?? root.GetComponentInChildren<DrugInventory>();
            if (inventory == null) return;
            int taken = inventory.Add(drugType, amount);
            if (taken <= 0)
            {
                RunTelemetry.Event("drug_full", drugType.ToString());
                return;
            }

            Destroy(gameObject);
        }
    }
}
