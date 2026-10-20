using GlorifiedBody.Combat;
using UnityEngine;

namespace GlorifiedBody.Gore
{
    public sealed class DismembermentZone : MonoBehaviour
    {
        [SerializeField] private string zoneId = "RightForeArm";
        [SerializeField] private HitZone hitZone;
        [SerializeField] private Transform limbSocket;
        [SerializeField] private GameObject sourceVisual;
        [SerializeField] private Transform sourceBone;
        [SerializeField] private bool hideSourceVisual;
        [SerializeField] private bool scaleSourceBone;
        [SerializeField] private Vector3 sourceBoneScaleAfterDismemberment = Vector3.one;
        [SerializeField] private GameObject detachedLimbPrefab;
        [SerializeField] private GameObject woundMarkerPrefab;
        [SerializeField, Min(0f)] private float cleanupLifetime = 8f;
        [SerializeField] private DismembermentRule rule;

        private bool dismembered;
        private Vector3 originalSourceBoneScale;

        public string ZoneId => hitZone != null ? hitZone.ZoneId : string.IsNullOrWhiteSpace(zoneId) ? name : zoneId;
        public Transform LimbSocket => limbSocket != null ? limbSocket : hitZone != null ? hitZone.Socket : transform;
        public GameObject DetachedLimbPrefab => detachedLimbPrefab;
        public GameObject WoundMarkerPrefab => woundMarkerPrefab;
        public float CleanupLifetime => cleanupLifetime;
        public DismembermentRule Rule => rule;
        public bool IsDismembered => dismembered;

        private void Awake()
        {
            if (hitZone == null)
            {
                hitZone = GetComponent<HitZone>();
            }

            if (sourceBone != null)
            {
                originalSourceBoneScale = sourceBone.localScale;
            }
        }

        public bool Matches(HitInfo hit)
        {
            if (hit == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(hit.HitZone) && string.Equals(hit.HitZone, ZoneId, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            Transform hitTransform = hit.HitTransform != null ? hit.HitTransform : hit.HitCollider != null ? hit.HitCollider.transform : null;
            return hitTransform != null && (hitTransform == transform || hitTransform.IsChildOf(transform) || transform.IsChildOf(hitTransform));
        }

        public void MarkDismembered()
        {
            if (dismembered)
            {
                return;
            }

            dismembered = true;

            if (hideSourceVisual && sourceVisual != null)
            {
                sourceVisual.SetActive(false);
            }

            if (scaleSourceBone && sourceBone != null)
            {
                sourceBone.localScale = sourceBoneScaleAfterDismemberment;
            }
        }

        public void ResetZone()
        {
            dismembered = false;

            if (hideSourceVisual && sourceVisual != null)
            {
                sourceVisual.SetActive(true);
            }

            if (scaleSourceBone && sourceBone != null)
            {
                sourceBone.localScale = originalSourceBoneScale == Vector3.zero ? Vector3.one : originalSourceBoneScale;
            }
        }
    }
}
