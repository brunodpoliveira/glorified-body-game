using UnityEngine;
using GlorifiedBody.Audio;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Saving;

namespace GlorifiedBody.Story
{
    /// <summary>
    /// The art puzzle of the foyer (rounds 147 and 148, DEMO_PLAN §10.5b item 2): four copies of veiled statues on wheeled
    /// plinths, and four brass bases I to IV around Modesty. She moves the copies with mk_orzabal (the carry of the defend key,
    /// as the planters). Her art log on each copy gives its year. When each copy rests on its base, the oldest first (Puritas,
    /// Tuccia, Modesty, the Veiled Christ), the flag goes up and the lift comes down. A copy she still carries does not count:
    /// it must stand on the floor. The flag is a story flag, so the saves keep the solved puzzle, and MovableProp keeps where
    /// each copy stands.
    /// </summary>
    public sealed class PlinthPuzzle : MonoBehaviour
    {
        public string flag = "";
        [Tooltip("The bases, I to IV.")]
        public Transform[] bases = new Transform[0];
        [Tooltip("The copies, in the right order: the copy of base I first.")]
        public Transform[] copies = new Transform[0];
        [Tooltip("How far the centre of a copy may stand from the centre of its base, in metres (flat).")]
        [Min(0.1f)] public float radius = 0.55f;
        [Tooltip("A copy that rests stands this high over its base (its centre).")]
        public float restHeight = 0.9f;

        public bool Solved => !string.IsNullOrEmpty(flag) && StoryFlags.Has(flag);
        /// <summary>The bases with their right copy on them now (the tests).</summary>
        public int Placed { get; private set; }

        private float nextCheck;

        /// <summary>The copy of base i rests on it.</summary>
        public bool OnBase(int i)
        {
            if (i < 0 || i >= bases.Length || i >= copies.Length || bases[i] == null || copies[i] == null) return false;
            Vector3 c = copies[i].position, b = bases[i].position;
            float flat = Vector2.Distance(new Vector2(c.x, c.z), new Vector2(b.x, b.z));
            return flat <= radius && Mathf.Abs(c.y - (b.y + restHeight)) <= 0.3f;
        }

        private void Update()
        {
            if (Solved || bases.Length == 0 || Time.time < nextCheck) return;
            nextCheck = Time.time + 0.25f;
            int placed = 0;
            for (int i = 0; i < bases.Length; i++)
                if (OnBase(i)) placed++;
            Placed = placed;
            if (placed < bases.Length) return;
            StoryFlags.Raise(flag);
            Sfx.Play(Sfx.PaChime, transform.position + Vector3.up * 2f, 0.9f);
            RunTelemetry.Event("plinths_solved", flag);
        }
    }
}
