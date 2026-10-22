using System;
using UnityEngine;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// The Interact key (Bruno, 2026-10-22: "it'll pick up objects after confirmation"; interact and scan are
    /// separate keys). The focus is the nearest pickup in range inside the cone of her facing. Interact on it
    /// opens the question "Take it?": Interact again takes it, the Back key leaves it. A log opens for reading
    /// after it is taken; Interact or Back closes it. She stands still while she answers or reads.
    /// </summary>
    public sealed class Interactor : MonoBehaviour
    {
        public const float FacingCone = 60f; // degrees each side of her facing, PROPOSED

        public enum State { Free, Confirming, Reading }

        public State Current { get; private set; } = State.Free;
        public ItemPickup Focus { get; private set; }
        public ItemPickup Pending { get; private set; }
        public ItemDefinition Reading { get; private set; }
        public string LastResult { get; private set; } = "";

        public event Action OnChanged;

        private TankController tank;
        private ItemInventory inventory;
        private PsychicScanner scanner;
        private bool locked;
        private int stateFrame;

        private void Awake()
        {
            tank = GetComponent<TankController>();
            inventory = GetComponent<ItemInventory>();
            scanner = GetComponent<PsychicScanner>();
        }

        private void OnDisable()
        {
            SetLock(false);
        }

        private void Update()
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;
            bool interact = GameInput.GetActionDown(InputManager.GameAction.Interact, KeyCode.E);
            bool back = GameInput.GetActionDown(InputManager.GameAction.Back, KeyCode.S);
            bool sameFrame = Time.frameCount == stateFrame;

            switch (Current)
            {
                case State.Free:
                    Focus = FindFocus();
                    bool scanBusy = scanner != null && (scanner.IsViewing || scanner.EndFrame == Time.frameCount);
                    if (interact && Focus != null && !scanBusy) Ask(Focus);
                    break;
                case State.Confirming:
                    if (sameFrame) break;
                    if (Pending == null) { Leave(); break; }
                    if (interact) Take();
                    else if (back) Leave();
                    break;
                case State.Reading:
                    if (sameFrame) break;
                    if (interact || back) CloseReader();
                    break;
            }
        }

        /// <summary>The nearest pickup in its range and inside the cone of her facing. Null when none.</summary>
        public ItemPickup FindFocus()
        {
            ItemPickup best = null;
            float bestDistance = float.MaxValue;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            foreach (ItemPickup pickup in ItemPickup.All)
            {
                if (pickup == null || pickup.item == null) continue;
                Vector3 to = Vector3.ProjectOnPlane(pickup.transform.position - transform.position, Vector3.up);
                float distance = to.magnitude;
                if (distance > pickup.range || distance >= bestDistance) continue;
                if (distance > 0.3f && Vector3.Angle(forward, to) > FacingCone) continue;
                best = pickup;
                bestDistance = distance;
            }
            return best;
        }

        private void Ask(ItemPickup pickup)
        {
            Pending = pickup;
            SetState(State.Confirming);
            Result("ask:" + pickup.item.itemId);
        }

        private void Take()
        {
            ItemPickup pickup = Pending;
            Pending = null;
            if (inventory == null || !inventory.TryAdd(pickup.item))
            {
                SetState(State.Free);
                Result("full:" + pickup.item.itemId);
                return;
            }
            ItemDefinition item = pickup.item;
            pickup.gameObject.SetActive(false);
            Destroy(pickup.gameObject);
            Result("taken:" + item.itemId);
            if (item.kind == ItemKind.Log) OpenReader(item);
            else SetState(State.Free);
        }

        private void Leave()
        {
            string id = Pending != null && Pending.item != null ? Pending.item.itemId : "none";
            Pending = null;
            SetState(State.Free);
            Result("left:" + id);
        }

        /// <summary>Opens a log for reading (after the pickup, and later from the inventory screen).</summary>
        public void OpenReader(ItemDefinition item)
        {
            if (item == null || item.kind != ItemKind.Log) return;
            Reading = item;
            SetState(State.Reading);
            Result("read:" + item.itemId);
        }

        private void CloseReader()
        {
            string id = Reading != null ? Reading.itemId : "none";
            Reading = null;
            SetState(State.Free);
            Result("closed:" + id);
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
            OnChanged?.Invoke();
        }
    }
}
