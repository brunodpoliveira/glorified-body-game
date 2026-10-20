using UnityEngine;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// The input bot's controller. The bot writes the next frame (Move, Look, held and tapped
    /// actions), then calls Commit() once per frame before the player scripts update. Every reader
    /// in that frame then sees the same button state, so Down/Up edges last exactly one frame.
    /// Pure C# (no scene objects), so EditMode tests can drive it.
    /// </summary>
    public sealed class VirtualPad : IGameInputSource
    {
        /// <summary>Move stick, -1..1 on each axis (x = right, y = forward, camera relative).</summary>
        public Vector2 Move;
        /// <summary>Look delta in mouse-axis units for this frame (camera multiplies by sensitivity).</summary>
        public Vector2 Look;
        public float Scroll;

        private int pendingHeld;
        private int pendingTaps;
        private int current;
        private int previous;

        private static int Bit(InputManager.GameAction action) => 1 << (int)action;

        /// <summary>Hold or release an action from the next Commit on.</summary>
        public void SetHeld(InputManager.GameAction action, bool held)
        {
            if (held) pendingHeld |= Bit(action);
            else pendingHeld &= ~Bit(action);
        }

        public bool IsHeldPending(InputManager.GameAction action) => (pendingHeld & Bit(action)) != 0;

        /// <summary>Press an action for one committed frame, then release it.</summary>
        public void Tap(InputManager.GameAction action) => pendingTaps |= Bit(action);

        /// <summary>Publishes the pending state as this frame's state.</summary>
        public void Commit()
        {
            previous = current;
            current = pendingHeld | pendingTaps;
            pendingTaps = 0;
        }

        /// <summary>Releases everything and zeroes the sticks. Takes effect at the next Commit.</summary>
        public void ReleaseAll()
        {
            pendingHeld = 0;
            pendingTaps = 0;
            Move = Vector2.zero;
            Look = Vector2.zero;
            Scroll = 0f;
        }

        public bool GetAction(InputManager.GameAction action) => (current & Bit(action)) != 0;
        public bool GetActionDown(InputManager.GameAction action) => (current & Bit(action)) != 0 && (previous & Bit(action)) == 0;
        public bool GetActionUp(InputManager.GameAction action) => (current & Bit(action)) == 0 && (previous & Bit(action)) != 0;

        public float GetAxis(string axisName)
        {
            switch (axisName)
            {
                case GameInput.AxisMoveX: return Mathf.Clamp(Move.x, -1f, 1f);
                case GameInput.AxisMoveY: return Mathf.Clamp(Move.y, -1f, 1f);
                case GameInput.AxisLookX: return Look.x;
                case GameInput.AxisLookY: return Look.y;
                case GameInput.AxisScroll: return Scroll;
                default: return 0f;
            }
        }
    }
}
