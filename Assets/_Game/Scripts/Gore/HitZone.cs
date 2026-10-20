using UnityEngine;

namespace GlorifiedBody.Gore
{
    public sealed class HitZone : MonoBehaviour
    {
        [SerializeField] private string zoneId;
        [SerializeField] private Transform ownerRoot;
        [SerializeField] private Transform socket;

        public string ZoneId => string.IsNullOrWhiteSpace(zoneId) ? name : zoneId;
        public Transform OwnerRoot => ownerRoot != null ? ownerRoot : transform.root;
        public Transform Socket => socket != null ? socket : transform;
    }
}
