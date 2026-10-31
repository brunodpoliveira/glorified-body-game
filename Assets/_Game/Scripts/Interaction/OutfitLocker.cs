using UnityEngine;
using GlorifiedBody.Player;
using GlorifiedBody.UI;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// A locker with an outfit in it (2026-10-31; the demo plan: W6, "the lockers: the guard uniform"). Interact asks
    /// "Put on the guard uniform?"; a yes changes her outfit behind a short black (OutfitChange). In the uniform the
    /// same locker asks "Take off the uniform?", and the uniform goes back in. The uniform on its hanger shows while she
    /// does not wear it. Placeholder look.
    /// </summary>
    public sealed class OutfitLocker : Interactable
    {
        public const float FadeSeconds = 0.25f;

        [Tooltip("The outfit id in the locker (Resources/Outfits/outfit_<id>).")]
        public string outfitId = OutfitChange.GuardId;
        public string thing = "the guard uniform";
        [Tooltip("The uniform on its hanger: shown while she does not wear it.")]
        public GameObject shown;

        public int Changes { get; private set; }

        private PsychicStats stats;

        private bool Wearing(Interactor who)
        {
            PsychicStats s = who != null ? who.GetComponent<PsychicStats>() : Stats();
            return s != null && s.Outfit != null && s.Outfit.outfitId == outfitId;
        }

        private PsychicStats Stats()
        {
            if (stats == null)
            {
                GameObject lucy = GameObject.FindGameObjectWithTag("Player");
                stats = lucy != null ? lucy.GetComponent<PsychicStats>() : null;
            }
            return stats;
        }

        private void Update()
        {
            if (shown != null)
            {
                bool show = !Wearing(null);
                if (shown.activeSelf != show) shown.SetActive(show);
            }
        }

        public override string Hint => Wearing(null) ? "take off the uniform" : "put on " + thing;
        public override InteractKind KindFor(Interactor who) => InteractKind.Ask;
        public override string TextFor(Interactor who) => Wearing(who)
            ? "Take off the uniform and leave it here?"
            : $"Put on {thing}?";

        public override string Confirm(Interactor who)
        {
            if (who == null) return "none";
            GameObject lucy = who.gameObject;
            OutfitProfile to = OutfitChange.Load(Wearing(who) ? OutfitChange.NudeId : outfitId);
            if (to == null) return "no_outfit:" + outfitId;
            Changes++;
            // A short black over the change. The change itself is at once, so the tests and the saves see it now.
            var fell = OutfitChange.Change(lucy, to);
            ScreenFade.Out(FadeSeconds, () => ScreenFade.In(FadeSeconds));
            return $"outfit:{to.outfitId}:fell={(fell != null ? fell.Count : 0)}";
        }
    }
}
