using System.Collections;
using UnityEngine;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Player;
using GlorifiedBody.Saving;
using GlorifiedBody.Story;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// A name card on the conference table (cutscene 07b, beat 22b, 2026-11-05). The Interact key on a card reads its name,
    /// one line. The card at the head of the table plays the flashback once, the first time: the Interact key on it, no
    /// Crown prompt, no Scan key (the writer's rule). Its flag goes up at that first press, so the second press shows the
    /// card and nothing else. After the flashback the SHORT bar rises a little (PROPOSED: a tenth of the bar): a memory
    /// costs her, the same way a hit does.
    /// </summary>
    public sealed class NameCard : Interactable
    {
        public const string FaceDown = "The card lies face down.";

        [Tooltip("The name on the card. Empty: it lies face down.")]
        public string cardName = "";
        [Tooltip("The head card: the Interact key plays it once.")]
        public CutsceneAsset flashback;
        [Tooltip("Raised at the first play (the second press shows the card only; cutscene 09 hears the flag).")]
        public string flag = "";
        [Min(0f)] public float shortGain = 10f;

        public static int Flashbacks { get; private set; }

        public override string Hint => "read the card";
        public override InteractKind KindFor(Interactor who) => PlaysNow ? InteractKind.Act : InteractKind.Message;
        public override string TextFor(Interactor who) => string.IsNullOrEmpty(cardName) ? FaceDown : cardName;

        public bool PlaysNow => flashback != null && !string.IsNullOrEmpty(flag) && !StoryFlags.Has(flag);

        public override string Confirm(Interactor who)
        {
            if (!PlaysNow) return "card:" + cardName;
            StoryFlags.Raise(flag);
            Flashbacks++;
            RunTelemetry.Event("name_card", cardName);
            if (CutscenePlayer.Play(flashback)) StartCoroutine(AfterIt(who));
            return "card:flashback";
        }

        private IEnumerator AfterIt(Interactor who)
        {
            yield return null;
            while (CutscenePlayer.IsPlaying) yield return null;
            PsychicStats stats = who != null ? who.GetComponent<PsychicStats>() : null;
            if (stats != null && shortGain > 0f) stats.AddShort(shortGain);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => Flashbacks = 0;
    }
}
