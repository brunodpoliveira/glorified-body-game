using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// The common interaction (Bruno, 2026-11-08: "lucy can interact with objects normally (pressing e) and she'll comment
    /// about it. there should be placeholder lines in cases of cupboards already cleaned up of items"). The Interact key on
    /// a thing of the room shows her comment in the message box, with no question and no voice (P7: she speaks once in act
    /// 1; the writer decides what the words are). A container holds hidden pickups (contents): while one is left, the key
    /// asks to take it; when they are all gone, it shows emptyComment. Anything else the key can reach in range wins the
    /// focus over a comment (Priority): a log on a desk, a door, a switch. The demo gives every prop one
    /// (DemoRoom.Prop, the lines by type: DemoRoom.ExamineLine).
    /// </summary>
    public sealed class Examinable : Interactable
    {
        public const string DefaultComment = "Nothing of note here.";
        public const string DefaultEmpty = "Nothing else of note here.";

        [Tooltip("What she says about it. PLACEHOLDER until the writer's line.")]
        [TextArea] public string comment = DefaultComment;
        [Tooltip("A container after its contents are gone.")]
        public string emptyComment = DefaultEmpty;
        [Tooltip("The words of the hint: \"look at the desk\".")]
        public string thing = "it";
        [Tooltip("The hidden pickups inside it (inactive children). A taken one goes, or stays marked taken after a load.")]
        public List<ItemPickup> contents = new List<ItemPickup>();

        /// <summary>The times the key showed a comment here (the tests).</summary>
        public int Shows { get; private set; }

        public override int Priority => 0;

        // A broken prop (cover, a desk) has nothing left to look at. A broken container keeps its items: they are not lost.
        public override bool Available => base.Available && (breakable == null || !breakable.IsBroken || Next != null);

        private GlorifiedBody.Destruction.Breakable breakable;

        private void Awake() => breakable = GetComponent<GlorifiedBody.Destruction.Breakable>();

        public bool IsContainer => contents.Count > 0;

        /// <summary>The next item left inside, or null.</summary>
        public ItemPickup Next
        {
            get
            {
                foreach (ItemPickup p in contents)
                    if (p != null && !p.Taken && p.item != null) return p;
                return null;
            }
        }

        public override ItemDefinition Gives => Next != null ? Next.item : null;

        public override string Hint => (Next != null ? "search " : "look at ") + thing;

        public override InteractKind KindFor(Interactor who) => Next != null ? InteractKind.Ask : InteractKind.Message;

        public override string TextFor(Interactor who)
        {
            ItemPickup next = Next;
            if (next != null) return $"There is {next.Name} inside. Take it?";
            return IsContainer ? emptyComment : comment;
        }

        public override string Shown(Interactor who)
        {
            Shows++;
            RunTelemetry.Event("examine", name);
            return (IsContainer ? "empty:" : "examined:") + name;
        }

        public override string Confirm(Interactor who)
        {
            ItemPickup next = Next;
            return next != null ? next.Confirm(who) : "examined:" + name;
        }
    }
}
