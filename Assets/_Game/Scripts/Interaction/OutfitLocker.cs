using UnityEngine;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;
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
        public override InteractKind KindFor(Interactor who) => Over(who) > 0 ? InteractKind.Message : InteractKind.Ask;
        public override string TextFor(Interactor who)
        {
            int over = Over(who);
            if (over > 0)
            {
                // Bruno, 2026-10-31: "give the player a warning and a chance to drop items".
                ItemInventory items = who.Inventory;
                string key = InputManager.Instance != null ? InputManager.GetKeyDisplayName(InputManager.Instance.GetKey(InputManager.GameAction.Drop)) : "X";
                return $"Without the uniform she carries {BareSlots} things. She has {items.UsedSlots}.\nDrop {over} first: inventory, {key} drop.";
            }
            return Wearing(who) ? "Take off the uniform and leave it here?" : $"Put on {thing}?";
        }

        private static int BareSlots
        {
            get
            {
                OutfitProfile bare = OutfitChange.Load(OutfitChange.NudeId);
                return bare != null ? bare.itemSlots : 4;
            }
        }

        /// <summary>The grabbables over the bare limit when she takes the uniform off here (0: they fit, or she puts it on).</summary>
        private int Over(Interactor who)
        {
            if (who == null || !Wearing(who) || who.Inventory == null) return 0;
            return Mathf.Max(0, who.Inventory.UsedSlots - BareSlots);
        }

        public override string Confirm(Interactor who)
        {
            if (who == null) return "none";
            if (Over(who) > 0) return "too_many:" + Over(who);
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
