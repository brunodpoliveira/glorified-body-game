using System.Collections.Generic;
using UnityEngine;

namespace GlorifiedBody.Rendering
{
    /// <summary>Marks a light for the light in the air (switch 5), with a scatter scale.</summary>
    [RequireComponent(typeof(Light))]
    public sealed class VolumetricLight : MonoBehaviour
    {
        public static readonly List<VolumetricLight> All = new List<VolumetricLight>();
        public static readonly Dictionary<Light, float> Strengths = new Dictionary<Light, float>();

        [Min(0f)] public float strength = 1f;
        private Light owned;

        private void OnEnable()
        {
            owned = GetComponent<Light>();
            All.Add(this);
            Strengths[owned] = strength;
        }

        private void OnValidate()
        {
            if (owned != null && Strengths.ContainsKey(owned)) Strengths[owned] = strength;
        }

        private void OnDisable()
        {
            All.Remove(this);
            if (owned != null) Strengths.Remove(owned);
        }
    }
}
