using UnityEngine;

namespace GlorifiedBody.Player
{
    public enum PowerKind { Blast, Fire, Telekinesis }

    /// <summary>The drugs, one per power (design §2.2). The names are Galerians' (PROPOSED).</summary>
    public enum DrugType { Nalcon, Red, DFelon }

    /// <summary>
    /// One psychic power. Every attack is a circle charge: the longer the charge, the stronger the attack,
    /// and the cost scales with it. Numbers are PROPOSED.
    /// </summary>
    [CreateAssetMenu(fileName = "power", menuName = "GlorifiedBody/Power Definition")]
    public sealed class PowerDefinition : ScriptableObject
    {
        public PowerKind kind = PowerKind.Blast;
        public string displayName = "BLAST";
        public DrugType drug = DrugType.Nalcon;

        [Header("Charge")]
        [Min(0.1f)] public float fullChargeTime = 1.5f;
        [Range(0.05f, 1f)] public float minCharge = 0.2f;
        [Min(1f)] public float range = 12f;

        [Header("Cost and damage at full charge")]
        [Min(0f)] public float nalconCost = 20f;
        [Min(0f)] public float baseDamage = 35f;

        [Header("Blast")]
        [Min(0f)] public float staggerSeconds = 0.8f;

        [Header("Fire")]
        [Min(0f)] public float burnSeconds = 4f;
        [Min(0f)] public float burnPerSecond = 6f;

        [Header("Telekinesis")]
        [Min(0f)] public float liftHeight = 1.5f;
        [Min(0f)] public float liftSeconds = 0.4f;
        [Min(0f)] public float holdSeconds = 0.8f;
    }
}
