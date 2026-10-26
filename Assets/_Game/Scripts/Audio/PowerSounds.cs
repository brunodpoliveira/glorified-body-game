using UnityEngine;
using GlorifiedBody.Combat;
using GlorifiedBody.Player;

namespace GlorifiedBody.Audio
{
    /// <summary>
    /// Her powers make sound (the audio sweep, 2026-10-26: all three were silent). A hum while the circle charges, its
    /// pitch rising with the charge. At the release: mk_warspite's blast, mk_erebus's flame (quiet for a part charge,
    /// which only stuns), mk_orzabal's lift. The throw, the slam, the catch, the drug, and a refusal ("TOO HEAVY",
    /// "NOT ENOUGH MK"). AudioHost puts one on Lucy in every room. All the clips are placeholders (Sfx).
    /// </summary>
    public sealed class PowerSounds : MonoBehaviour
    {
        public const float HumLevel = 0.3f;

        private PsychicPowers powers;
        private AudioSource hum;

        public bool Humming => hum != null && hum.isPlaying;
        public float HumPitch => hum != null ? hum.pitch : 0f;

        private void Start()
        {
            powers = GetComponent<PsychicPowers>();
            if (powers == null) { enabled = false; return; }
            powers.OnResult += OnResult;
            powers.OnPowerFired += OnFired;
        }

        private void OnDestroy()
        {
            if (powers == null) return;
            powers.OnResult -= OnResult;
            powers.OnPowerFired -= OnFired;
        }

        private void Update()
        {
            float charge = powers.Charge;
            if (charge > 0f)
            {
                if (hum == null) hum = Sfx.Loop(Sfx.ChargeHum, gameObject, HumLevel);
                if (hum != null)
                {
                    if (!hum.isPlaying) hum.Play();
                    hum.pitch = 0.8f + 0.7f * charge;
                }
            }
            else if (hum != null && hum.isPlaying)
            {
                hum.Stop();
            }
        }

        private Vector3 Chest => transform.position + Vector3.up * 1.2f;

        private void OnFired(PowerDefinition power, float charge, LockOnTargetable target)
        {
            if (power == null) return;
            Vector3 at = target != null ? target.GetLockPoint() : Chest + transform.forward * 2f;
            switch (power.kind)
            {
                case PowerKind.Blast: Sfx.Play(Sfx.Blast, at, 0.5f + 0.5f * charge); break;
                case PowerKind.Fire: Sfx.Play(Sfx.Fire, at, charge >= 0.999f ? 1f : 0.4f); break;
                case PowerKind.Telekinesis: Sfx.Play(Sfx.Lift, at, 0.8f); break;
            }
        }

        private void OnResult(string result)
        {
            if (string.IsNullOrEmpty(result)) return;
            TelekinesisLift lift = powers.HeldLift;
            Vector3 there = lift != null ? lift.transform.position : Chest + transform.forward * 2f;
            if (result == "slam") Sfx.Play(Sfx.Slam, there, 1f);
            else if (result.StartsWith("launch")) Sfx.Play(Sfx.Throw, there, 0.9f);
            else if (result.StartsWith("catch")) Sfx.Play(Sfx.Catch, Chest, 0.9f);
            else if (result.StartsWith("drug:")) Sfx.Play(Sfx.Drug, Chest, 0.8f);
            else if (result == "too_heavy" || result == "no_nalcon" || result == "drug_none") Sfx.Ui(Sfx.Refuse, 0.6f);
        }
    }
}
