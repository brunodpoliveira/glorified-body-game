using UnityEngine;

namespace GlorifiedBody.Rendering
{
    /// <summary>
    /// Small lights that blink in two groups (the desktop tree of G10, cutscene 07c: "a string of small lights that blinks").
    /// The even lights show, then the odd ones. It runs on game time, so the pause stops it.
    /// </summary>
    public sealed class Blink : MonoBehaviour
    {
        public Renderer[] lights = new Renderer[0];
        [Min(0.05f)] public float seconds = 0.6f;

        public int Phase { get; private set; } = -1;

        private void Update()
        {
            int phase = Mathf.FloorToInt(Time.time / seconds) % 2;
            if (phase == Phase) return;
            Phase = phase;
            for (int i = 0; i < lights.Length; i++)
                if (lights[i] != null) lights[i].enabled = i % 2 == phase;
        }
    }
}
