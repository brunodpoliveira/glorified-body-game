using UnityEngine;
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// The second way to a carried item (Bruno, 2026-11-02: "the second way to grab cards from civvies is to simply
    /// interact/talk with them"): the Interact key on a living civilian who carries an item (ItemCarrier) asks "Ask for
    /// the stair card?"; a yes puts the item in her inventory, and the civilian gives it with a line. The kill stays
    /// the first way (the item drops beside the body). The pickup counts as taken, so the saves keep it.
    /// EnemySpawn adds this to an unarmed civilian (CivilianBrain) with a drop. The armed ones fight: no talk (PROPOSED).
    /// </summary>
    [RequireComponent(typeof(ItemCarrier))]
    public sealed class CarrierTalk : Interactable
    {
        public const float Reach = 1.8f;
        public const string LineGive = "Here. Take it. Now go, and you go round the chairs."; // the writer's line (barks.csv, "W11, gives the card when asked", PROPOSED)

        [Tooltip("He gives the item only when this flag is up (the scan C11 of the Hold, round 118). Before it he denies. Empty: he always gives.")]
        public string giveAfterFlag = "";
        public string denyLine = "";
        public string giveLine = "";

        private ItemCarrier carrier;
        private Health health;

        private bool Denies => !string.IsNullOrEmpty(giveAfterFlag) && !GlorifiedBody.Saving.StoryFlags.Has(giveAfterFlag);
        public int Denials { get; private set; }

        private void Awake()
        {
            carrier = GetComponent<ItemCarrier>();
            health = GetComponent<Health>();
            range = Reach;
            Animator animator = GetComponentInChildren<Animator>();
            if (point == null && animator != null && animator.isHuman) point = animator.GetBoneTransform(HumanBodyBones.Hips);
        }

        private ItemPickup Item => carrier != null ? carrier.Drop : null;

        public override bool Available => base.Available && health != null && !health.IsDead
            && Item != null && Item.item != null && !Item.Taken && !carrier.Dropped;

        public override string Hint => "ask for " + Item.Name;

        public override InteractKind KindFor(Interactor who) => InteractKind.Ask;

        public override string TextFor(Interactor who) => $"Ask for {Item.Name}?";

        public override string Confirm(Interactor who)
        {
            ItemPickup pickup = Item;
            if (Denies)
            {
                Denials++;
                Bark.Say(gameObject, string.IsNullOrEmpty(denyLine) ? "I don't have anything." : denyLine, 2.4f);
                return "denied:" + pickup.item.itemId;
            }
            ItemInventory inventory = who != null ? who.Inventory : null;
            if (inventory == null || !inventory.TryAdd(pickup.item)) return "full:" + pickup.item.itemId;
            string id = pickup.item.itemId;
            pickup.MarkTaken();
            Destroy(pickup.gameObject);
            carrier.HideCarried();
            Bark.Say(gameObject, string.IsNullOrEmpty(giveLine) ? LineGive : giveLine, 2.4f);
            return "given:" + id;
        }
    }
}
