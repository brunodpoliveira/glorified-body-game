#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using GlorifiedBody.Player;

namespace GlorifiedBody.Editor
{
    /// <summary>
    /// Creates the power and outfit assets under Assets/_Game/Resources (PsychicStats and PsychicPowers load them).
    /// Idempotent: it upserts every asset. Numbers are PROPOSED (2026-10-20).
    /// Menu: GlorifiedBody > Create Power Assets. Batch: tools/unity.sh exec GlorifiedBody.Editor.CreatePowerAssets.CreateAll
    /// </summary>
    public static class CreatePowerAssets
    {
        private const string Resources = "Assets/_Game/Resources";

        [MenuItem("GlorifiedBody/Create Power Assets")]
        public static void CreateAll()
        {
            EnsureFolder("Assets", "_Game");
            EnsureFolder("Assets/_Game", "Resources");
            EnsureFolder(Resources, "Powers");
            EnsureFolder(Resources, "Outfits");

            Power("power_blast", p =>
            {
                p.kind = PowerKind.Blast; p.displayName = "mk_warspite"; p.drug = DrugType.Nalcon;
                p.fullChargeTime = 1.5f; p.minCharge = 0.2f; p.range = 12f;
                p.nalconCost = 20f; p.baseDamage = 35f; p.staggerSeconds = 0.8f;
            });
            Power("power_fire", p =>
            {
                p.kind = PowerKind.Fire; p.displayName = "mk_erebus"; p.drug = DrugType.Red;
                p.fullChargeTime = 1.5f; p.minCharge = 0.2f; p.range = 12f;
                // staggerSeconds: the flash of a part charge, times the charge (Bruno, 2026-10-20, option c).
                p.nalconCost = 25f; p.baseDamage = 10f; p.burnSeconds = 4f; p.burnPerSecond = 6f; p.staggerSeconds = 1f;
            });
            Power("power_telekinesis", p =>
            {
                p.kind = PowerKind.Telekinesis; p.displayName = "mk_orzabal"; p.drug = DrugType.DFelon;
                p.fullChargeTime = 1.8f; p.minCharge = 0.2f; p.range = 12f;
                // The hold is the launch window (Bruno, 2026-10-20): 1.5 s to press again and throw it.
                p.nalconCost = 30f; p.baseDamage = 40f; p.liftHeight = 1.5f; p.liftSeconds = 0.4f; p.holdSeconds = 1.5f;
            });

            Outfit("outfit_nude", o =>
            {
                o.outfitId = "nude"; o.displayName = "NUDE"; o.maxHp = 100f;
                // nalconRegenPerSecond: 6 was too quick (Bruno, 2026-10-20). 2 = a full bar in 50 s, PROPOSED.
                o.hpRegenPerSecond = 1f; o.nalconRegenPerSecond = 2f; o.shortDecayPerSecond = 1f; o.drugSlotsPerType = 2; o.itemSlots = 4;
            });
            Outfit("outfit_guard", o =>
            {
                o.outfitId = "guard"; o.displayName = "SECURITY GUARD"; o.maxHp = 150f;
                o.hpRegenPerSecond = 0f; o.nalconRegenPerSecond = 0f; o.shortDecayPerSecond = 0f; o.drugSlotsPerType = 4; o.itemSlots = 8;
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CreatePowerAssets] 3 powers and 2 outfits created or updated.");
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }

        private static void Power(string id, System.Action<PowerDefinition> fill)
        {
            string path = $"{Resources}/Powers/{id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<PowerDefinition>(path);
            bool isNew = asset == null;
            if (isNew) asset = ScriptableObject.CreateInstance<PowerDefinition>();
            fill(asset);
            if (isNew) AssetDatabase.CreateAsset(asset, path); else EditorUtility.SetDirty(asset);
        }

        private static void Outfit(string id, System.Action<OutfitProfile> fill)
        {
            string path = $"{Resources}/Outfits/{id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<OutfitProfile>(path);
            bool isNew = asset == null;
            if (isNew) asset = ScriptableObject.CreateInstance<OutfitProfile>();
            fill(asset);
            if (isNew) AssetDatabase.CreateAsset(asset, path); else EditorUtility.SetDirty(asset);
        }
    }
}
#endif
