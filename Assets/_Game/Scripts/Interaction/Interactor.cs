using System;
using UnityEngine;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// The Interact key (Bruno, 2026-10-22: "it'll pick up objects after confirmation"; interact and scan are
    /// separate keys). The focus is the nearest Interactable in its range inside the cone of her facing.
    /// Interact on it asks the question ("Take the test log?", "Use the test key?"): Interact again says yes,
    /// Back says no. A door without its key only shows "It is locked.". A taken log opens for reading;
    /// Interact or Back closes it. She stands still while she answers, reads or sees a message. The page stops the
    /// world (GameState.Reading, Bruno, 2026-10-26).
    /// </summary>
    public sealed class Interactor : MonoBehaviour
    {
        public const float FacingCone = 60f; // degrees each side of her facing, PROPOSED
        public const float MessageSeconds = 2f;

        public enum State { Free, Confirming, Reading, Message }

        public State Current { get; private set; } = State.Free;
        public Interactable Focus { get; private set; }
        public Interactable Pending { get; private set; }
        public ItemDefinition Reading { get; private set; }
        /// <summary>The words on screen: the question or the message.</summary>
        public string Text { get; private set; } = "";
        public string LastResult { get; private set; } = "";
        public ItemInventory Inventory => inventory;

        public event Action OnChanged;

        private TankController tank;
        private ItemInventory inventory;
        private PsychicScanner scanner;
        private bool locked;
        private int stateFrame;
        private float messageUntil;

        private void Awake()
        {
            tank = GetComponent<TankController>();
            inventory = GetComponent<ItemInventory>();
            scanner = GetComponent<PsychicScanner>();
            // The feed (2026-10-30) came after the rooms were built: her Lucy gets it here when the room lacks it.
            if (GetComponent<GlorifiedBody.Player.Feeding>() == null) gameObject.AddComponent<GlorifiedBody.Player.Feeding>();
        }

        private void OnDisable()
        {
            SetLock(false);
        }

        private void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm != null && !gm.IsPlaying && !(gm.IsReading && Current == State.Reading)) return;
            bool interact = GameInput.GetActionDown(InputManager.GameAction.Interact, KeyCode.E);
            bool back = GameInput.GetActionDown(InputManager.GameAction.Back, KeyCode.S);
            bool sameFrame = Time.frameCount == stateFrame;

            switch (Current)
            {
                case State.Free:
                    Focus = FindFocus();
                    bool scanBusy = scanner != null && (scanner.IsViewing || scanner.EndFrame == Time.frameCount);
                    if (interact && Focus != null && !scanBusy) Approach(Focus);
                    break;
                case State.Confirming:
                    if (sameFrame) break;
                    if (Pending == null || !Pending.Available) { Answer(false); break; }
                    if (interact) Answer(true);
                    else if (back) Answer(false);
                    break;
                case State.Reading:
                    if (sameFrame) break;
                    if (interact || back) CloseReader();
                    break;
                case State.Message:
                    if (sameFrame) break;
                    if (interact || back || Time.time >= messageUntil) EndMessage();
                    break;
            }
        }

        /// <summary>The nearest available Interactable in its range and inside the cone of her facing. Null when none.</summary>
        public Interactable FindFocus()
        {
            Interactable best = null;
            float bestDistance = float.MaxValue;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            foreach (Interactable target in Interactable.All)
            {
                if (target == null || !target.Available) continue;
                Vector3 to = Vector3.ProjectOnPlane(target.Point - transform.position, Vector3.up);
                float distance = to.magnitude;
                if (distance > target.range || distance >= bestDistance) continue;
                if (distance > 0.3f && Vector3.Angle(forward, to) > FacingCone) continue;
                best = target;
                bestDistance = distance;
            }
            return best;
        }

        private void Approach(Interactable target)
        {
            Text = target.TextFor(this);
            if (target.KindFor(this) == InteractKind.Message)
            {
                messageUntil = Time.time + MessageSeconds;
                SetState(State.Message);
                Result((target is LockedDoor ? "locked:" : "message:") + target.name);
                return;
            }
            Pending = target;
            SetState(State.Confirming);
            Result("ask:" + target.name);
        }

        private void Answer(bool yes)
        {
            Interactable target = Pending;
            Pending = null;
            if (!yes || target == null)
            {
                SetState(State.Free);
                Result("left:" + (target != null ? target.name : "none"));
                return;
            }
            ItemDefinition item = target is ItemPickup pickup ? pickup.item : null;
            string result = target.Confirm(this);
            Result(result);
            if (item != null && item.kind == ItemKind.Log && result.StartsWith("taken:")) OpenReader(item);
            else if (result.StartsWith("full:"))
            {
                Text = "No room. The inventory is full.";
                messageUntil = Time.time + MessageSeconds;
                SetState(State.Message);
            }
            else SetState(State.Free);
        }

        /// <summary>Opens a log for reading (after the pickup, and from the inventory screen).</summary>
        public void OpenReader(ItemDefinition item)
        {
            if (item == null || item.kind != ItemKind.Log) return;
            Reading = item;
            SetState(State.Reading);
            GameManager.Instance?.BeginReading();
            Result("read:" + item.itemId);
        }

        private void CloseReader()
        {
            string id = Reading != null ? Reading.itemId : "none";
            Reading = null;
            SetState(State.Free);
            GameManager.Instance?.EndReading();
            Result("closed:" + id);
        }

        private void EndMessage()
        {
            Text = "";
            SetState(State.Free);
        }

        private void SetState(State state)
        {
            Current = state;
            stateFrame = Time.frameCount;
            SetLock(state != State.Free);
            OnChanged?.Invoke();
        }

        private void SetLock(bool value)
        {
            if (locked == value || tank == null) return;
            locked = value;
            tank.LockMovement(value);
        }

        private void Result(string what)
        {
            LastResult = what;
            RunTelemetry.Event("interact", what);
            GlorifiedBody.Audio.InteractionSounds.OnResult(what, transform.position);
            OnChanged?.Invoke();
        }
    }
}
