using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// A thing the psychic scan reads (design §2.9, round 9: "sometimes it's an image (scan a door and you'll
    /// see the image of the key needed to open it), or a short movie"). It carries a picture or a clip, and a
    /// caption. The pictures and clips are placeholders until the cutscene stills are made.
    /// </summary>
    public sealed class Scannable : MonoBehaviour
    {
        public string scanId = "scan";
        public string caption = "";
        public Texture2D image;
        public VideoClip clip;
        [Min(0.5f)] public float range = 3f;
        [Tooltip("The point the scan aims at (the centre of the door, the middle of the screen). Empty = this transform.")]
        public Transform focus;

        private static readonly List<Scannable> all = new List<Scannable>();
        public static IReadOnlyList<Scannable> All => all;

        public bool IsVideo => clip != null;
        public Vector3 FocusPoint => focus != null ? focus.position : transform.position;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => all.Clear();

        private void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
        }

        private void OnDisable()
        {
            all.Remove(this);
        }
    }
}
