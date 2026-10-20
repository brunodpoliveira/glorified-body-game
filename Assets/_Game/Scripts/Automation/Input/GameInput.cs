using UnityEngine;

namespace GlorifiedBody.Systems
{
    /// <summary>
    /// A source of player input other than the hardware (the input bot's virtual pad).
    /// Axis names are the legacy Input Manager names that the player stack reads (see GameInput).
    /// </summary>
    public interface IGameInputSource
    {
        float GetAxis(string axisName);
        bool GetAction(InputManager.GameAction action);
        bool GetActionDown(InputManager.GameAction action);
        bool GetActionUp(InputManager.GameAction action);
    }

    /// <summary>
    /// The one input seam of the player stack. With no bot source registered, every call reads the
    /// hardware exactly as before (InputManager bindings, else the legacy KeyCode fallback).
    /// With a bot source registered, the bot supplies the actions and axes, and raw hardware keys
    /// read as not pressed. The bot only gives input. MomentumLocomotion still owns all movement.
    /// </summary>
    public static class GameInput
    {
        public const string AxisMoveX = "LeftAnalogHorizontal";
        public const string AxisMoveY = "LeftAnalogVertical";
        public const string AxisLookX = "Mouse X";
        public const string AxisLookY = "Mouse Y";
        public const string AxisScroll = "Mouse ScrollWheel";

        public static IGameInputSource BotSource { get; private set; }
        public static bool HasBot => BotSource != null;

        public static void SetBotSource(IGameInputSource source) => BotSource = source;

        /// <summary>Removes the bot source, but only if it is still the one registered.</summary>
        public static void ClearBotSource(IGameInputSource source)
        {
            if (BotSource == source) BotSource = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => BotSource = null;

        // ── Actions ───────────────────────────────────────────────────

        public static bool GetActionDown(InputManager.GameAction action, KeyCode fallback)
        {
            if (BotSource != null) return !IsBlocked() && BotSource.GetActionDown(action);
            return InputManager.Instance != null ? InputManager.Instance.GetActionDown(action) : Input.GetKeyDown(fallback);
        }

        public static bool GetAction(InputManager.GameAction action, KeyCode fallback)
        {
            if (BotSource != null) return !IsBlocked() && BotSource.GetAction(action);
            return InputManager.Instance != null ? InputManager.Instance.GetAction(action) : Input.GetKey(fallback);
        }

        public static bool GetActionUp(InputManager.GameAction action, KeyCode fallback)
        {
            if (BotSource != null) return !IsBlocked() && BotSource.GetActionUp(action);
            return InputManager.Instance != null ? InputManager.Instance.GetActionUp(action) : Input.GetKeyUp(fallback);
        }

        // ── Raw keys and axes ─────────────────────────────────────────

        /// <summary>Raw key (movement keys, debug toggles). A bot never presses raw keys.</summary>
        public static bool GetKey(KeyCode key) => BotSource == null && Input.GetKey(key);

        public static bool GetKeyDown(KeyCode key) => BotSource == null && Input.GetKeyDown(key);

        public static float GetAxis(string axisName) => BotSource != null ? BotSource.GetAxis(axisName) : Input.GetAxis(axisName);

        public static float GetAxisRaw(string axisName) => BotSource != null ? BotSource.GetAxis(axisName) : Input.GetAxisRaw(axisName);

        // Same block rule as InputManager: no actions while paused or game over.
        private static bool IsBlocked()
        {
            GameManager gm = GameManager.Instance;
            return gm != null && (gm.IsPaused || gm.IsGameOver);
        }
    }
}
