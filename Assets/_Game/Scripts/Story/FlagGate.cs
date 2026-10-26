using UnityEngine;
using GlorifiedBody.Saving;

namespace GlorifiedBody.Story
{
    /// <summary>
    /// Objects that a story flag shows or hides: a gate sealed until code silver ends, a QRF squad that comes after the
    /// alarm, a body that is there after a beat. Its children are active when the flag is up (showWhenRaised), or
    /// when it is down. It follows every change, and a load (StoryFlags.Set).
    /// </summary>
    public sealed class FlagGate : MonoBehaviour
    {
        public string flag = "";
        public bool showWhenRaised = true;

        public bool Showing { get; private set; }

        private void Awake() => Apply();
        private void OnEnable() => StoryFlags.OnChanged += OnFlag;
        private void OnDisable() => StoryFlags.OnChanged -= OnFlag;

        private void OnFlag(string changed)
        {
            if (changed == null || changed == flag) Apply();
        }

        public void Apply()
        {
            Showing = StoryFlags.Has(flag) == showWhenRaised;
            for (int i = 0; i < transform.childCount; i++) transform.GetChild(i).gameObject.SetActive(Showing);
        }
    }
}
