using UnityEngine;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// One UPS unit of the dark room (DEMO_PLAN §10.1): a PC on its battery, a label (NET, REC, ACCESS), an amber LED and a
    /// beep at its own pitch. The Interact key starts its PC; the bank (UpsBank) decides if that was the right one.
    /// </summary>
    public sealed class UpsUnit : Interactable
    {
        public UpsBank bank;
        public string label = "NET";
        public Renderer led;
        [Tooltip("The pitch of its beep: each unit has its own.")]
        public float pitch = 1f;

        public bool Up { get; private set; }

        public static readonly Color Amber = new Color(1f, 0.6f, 0.1f), Green = new Color(0.2f, 1f, 0.3f), Off = new Color(0.05f, 0.05f, 0.05f);

        private AudioSource beep;
        private MaterialPropertyBlock block;

        public override bool Available => base.Available && bank != null && bank.Beeping && !Up;
        public override string Hint => "start the " + label + " unit";
        public override InteractKind KindFor(Interactor who) => InteractKind.Act;
        public override string TextFor(Interactor who) => label;
        public override string Confirm(Interactor who) => bank != null ? bank.Press(this) : "no_bank";

        private void Awake()
        {
            beep = gameObject.AddComponent<AudioSource>();
            GlorifiedBody.Audio.GameAudio.Bind(beep, GlorifiedBody.Audio.SoundChannel.Sfx, 0.5f, spatial: true);
            beep.clip = BeepClip();
            beep.loop = true;
            beep.pitch = pitch;
            beep.playOnAwake = false;
        }

        /// <summary>Its PC runs: the LED turns green and the beep stops.</summary>
        public void SetUp(bool up)
        {
            Up = up;
            Refresh();
        }

        /// <summary>The bank's state changed: the beep and the LED follow.</summary>
        public void Refresh()
        {
            bool beeping = bank != null && bank.Beeping && !Up;
            if (beep != null)
            {
                beep.pitch = pitch;
                if (beeping && !beep.isPlaying) beep.Play();
                else if (!beeping && beep.isPlaying) beep.Stop();
            }
            if (led == null) return;
            block ??= new MaterialPropertyBlock();
            led.GetPropertyBlock(block);
            block.SetColor("_BaseColor", Up ? Green : bank != null && bank.Active ? Amber : Off);
            led.SetPropertyBlock(block);
        }

        public bool IsBeeping => beep != null && beep.isPlaying;

        // One beep of 0.12 s, then silence to 1.4 s: a placeholder sound (no file).
        private static AudioClip beepClip;
        private static AudioClip BeepClip()
        {
            if (beepClip != null) return beepClip;
            const int rate = 22050;
            var data = new float[(int)(rate * 1.4f)];
            for (int i = 0; i < rate * 0.12f; i++) data[i] = Mathf.Sin(2f * Mathf.PI * 1200f * i / rate) * 0.5f;
            beepClip = AudioClip.Create("ups_beep", data.Length, 1, rate, false);
            beepClip.SetData(data, 0);
            return beepClip;
        }
    }
}
