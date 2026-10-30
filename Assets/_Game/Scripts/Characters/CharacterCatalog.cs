using System;
using System.Collections.Generic;
using UnityEngine;

namespace GlorifiedBody.Characters
{
    public enum CharacterRole { Player, Enemy, Civilian }

    /// <summary>One demo class: the prefab (the model, the material, the animator), its height and its role.</summary>
    [Serializable]
    public sealed class CharacterEntry
    {
        public string id = "";
        public string displayName = "";
        public GameObject prefab;
        [Min(0.5f)] public float height = 1.7f;
        public string outfitId = "";
        public CharacterRole role = CharacterRole.Civilian;
    }

    /// <summary>
    /// The nine demo classes of T19 (`~/MEGA/glorified-body-renders/T19_characters/models/`), as the game sees them.
    /// Built by CreateCharacterAssets, loaded from Resources. Heights are Bruno's (2026-10-21).
    /// </summary>
    public sealed class CharacterCatalog : ScriptableObject
    {
        public const string ResourcePath = "Characters/catalog";

        public List<CharacterEntry> entries = new List<CharacterEntry>();

        public CharacterEntry Find(string id)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].id == id) return entries[i];
            }
            return null;
        }

        public static CharacterCatalog Load() => Resources.Load<CharacterCatalog>(ResourcePath);
    }

    /// <summary>
    /// Body points shared by the systems that aim at a character. Every character stands on its root (the feet
    /// at y 0), so a world point on the body is the root plus a height. PROPOSED numbers for a 1.7 to 1.8 m body.
    /// </summary>
    public static class Body
    {
        /// <summary>The chest: where the shooters aim, where the catch radius sits, where the HUD ring is.</summary>
        public const float ChestHeight = 1.2f;
        /// <summary>Where a shooter's bullet starts.</summary>
        public const float MuzzleHeight = 1.3f;
        /// <summary>3D Studio delivers every character 1.0 m tall. The prefab scale is height / this.</summary>
        public const float ModelSourceHeight = 1.0f;
    }
}
