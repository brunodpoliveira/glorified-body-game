using GlorifiedBody.Destruction;
using UnityEngine;

namespace GlorifiedBody.Combat
{
    /// <summary>What a hit strikes, for its sound and its particles.</summary>
    public enum SurfaceKind { Concrete, Metal, Wood, Glass, Soft }

    /// <summary>
    /// The surface under a hit (Bruno, 2026-10-25: a bullet on a concrete wall sounded like metal on metal). An
    /// intact breakable gives its own kind. Else the material name decides (the room materials are GB_ConcreteWall,
    /// GB_Steel, GB_Break_Plywood and so on). Anything not named is concrete: the rooms are concrete.
    /// </summary>
    public static class Surfaces
    {
        private static readonly string[] Concrete = { "concrete", "plaster", "tile", "stone", "brick", "cement" };
        private static readonly string[] Metal = { "steel", "metal", "grille", "gear", "conduit", "pipe", "door", "lightpanel", "iron" };
        private static readonly string[] Wood = { "wood", "plywood", "laminate", "crate", "plank", "desk" };
        private static readonly string[] Glass = { "glass", "mirror", "monitor", "crt", "skylight", "lamp", "screen" };
        private static readonly string[] Soft = { "mattress", "leather", "canvas", "soil", "plant", "fern", "vest", "cardboard", "binder", "fabric", "cushion", "pad" };

        public static SurfaceKind Of(Collider collider)
        {
            if (collider == null) return SurfaceKind.Concrete;
            Breakable piece = collider.GetComponentInParent<Breakable>();
            if (piece != null && !piece.IsBroken) return Of(piece.Material);
            Renderer renderer = collider.GetComponent<Renderer>();
            if (renderer == null) renderer = collider.GetComponentInParent<Renderer>();
            if (renderer == null) renderer = collider.GetComponentInChildren<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null) return OfName(renderer.sharedMaterial.name);
            return OfName(collider.name);
        }

        public static SurfaceKind Of(Breakable.Kind kind)
        {
            switch (kind)
            {
                case Breakable.Kind.Metal: return SurfaceKind.Metal;
                case Breakable.Kind.Glass: return SurfaceKind.Glass;
                case Breakable.Kind.Plaster: return SurfaceKind.Concrete;
                default: return SurfaceKind.Wood; // wood and plastic: a hollow knock
            }
        }

        /// <summary>The kind for a material or an object name. Concrete words first: "GB_ConcretePlanter" is not a plant.</summary>
        public static SurfaceKind OfName(string name)
        {
            if (string.IsNullOrEmpty(name)) return SurfaceKind.Concrete;
            string lower = name.ToLowerInvariant();
            if (Has(lower, Concrete)) return SurfaceKind.Concrete;
            if (Has(lower, Metal)) return SurfaceKind.Metal;
            if (Has(lower, Glass)) return SurfaceKind.Glass;
            if (Has(lower, Wood)) return SurfaceKind.Wood;
            if (Has(lower, Soft)) return SurfaceKind.Soft;
            return SurfaceKind.Concrete;
        }

        private static bool Has(string name, string[] words)
        {
            foreach (string word in words)
            {
                if (name.Contains(word)) return true;
            }
            return false;
        }
    }
}
