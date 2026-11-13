using UnityEngine;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Enemy;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// A figure who does not have the item she looks for, and says so (round 118, CANON: "no scientist will cooperate").
    /// The guards of H3 and the nurse of H4, asked for the airlock card, deny it. The Interact key on the living figure,
    /// while it does not fight her (a civilian, or a guard who cowers), and while she does not carry the item yet.
    /// EnemySpawn adds it (askLine). The words are the writer's (barks.csv, h3_deny_card, h4_ask_card).
    /// </summary>
    public sealed class AskTalk : Interactable
    {
        public string line = "";
        public string itemId = "";
        public string itemName = "the card";

        private Health health;
        private SoldierBrain soldier;

        public int Asked { get; private set; }

        private void Awake()
        {
            health = GetComponent<Health>();
            soldier = GetComponent<SoldierBrain>();
            range = CarrierTalk.Reach;
            Animator animator = GetComponentInChildren<Animator>();
            if (point == null && animator != null && animator.isHuman) point = animator.GetBoneTransform(HumanBodyBones.Hips);
        }

        private bool SheHasIt
        {
            get
            {
                if (string.IsNullOrEmpty(itemId)) return false;
                GameObject her = GameObject.FindWithTag("Player");
                ItemInventory items = her != null ? her.GetComponent<ItemInventory>() : null;
                return items != null && items.Has(itemId);
            }
        }

        public override bool Available => base.Available && health != null && !health.IsDead && !string.IsNullOrEmpty(line)
            && (soldier == null || soldier.Retreating) && !SheHasIt;

        public override string Hint => "ask for " + itemName;

        public override InteractKind KindFor(Interactor who) => InteractKind.Ask;

        public override string TextFor(Interactor who) => $"Ask for {itemName}?";

        public override string Confirm(Interactor who)
        {
            Asked++;
            Bark.Say(gameObject, line, 2.4f);
            RunTelemetry.Event("ask_denied", name);
            return "denied:" + itemId;
        }
    }
}
