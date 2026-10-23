using System;
using System.Collections.Generic;
using UnityEngine;

namespace GlorifiedBody.Systems
{
    /// <summary>
    /// Manages remappable key bindings. All gameplay scripts should query this
    /// instead of hardcoding KeyCode values.
    /// Persists bindings via PlayerPrefs.
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        /// <summary>
        /// The tank-control action list (PROPOSED 2026-10-20, build order step 2 may rename members).
        /// Pause stays on Escape and is not remappable: Escape also cancels a rebind.
        /// </summary>
        public enum GameAction
        {
            Forward,
            Back,
            TurnLeft,
            TurnRight,
            Run,
            Dodge,
            Charge,
            SwitchTarget,
            PowerBlast,
            PowerFire,
            PowerTelekinesis,
            UseDrug,
            Interact,
            Defend, // RMB: the defensive use of the selected power (Bruno, 2026-10-20)
            Scan,     // the psychic scan, separate from Interact (Bruno, 2026-10-22)
            Inventory // the inventory overlay, pauses the game (Bruno, 2026-10-22)
        }

        [Serializable]
        public struct KeyBinding
        {
            public GameAction action;
            public KeyCode defaultKey;
            [HideInInspector] public KeyCode currentKey;

            public string DisplayName => action switch
            {
                GameAction.Forward => "Walk Forward",
                GameAction.Back => "Walk Back",
                GameAction.TurnLeft => "Turn Left",
                GameAction.TurnRight => "Turn Right",
                GameAction.Run => "Run (hold)",
                GameAction.Dodge => "Dodge",
                GameAction.Charge => "Charge Power (hold)",
                GameAction.SwitchTarget => "Switch Target",
                GameAction.PowerBlast => "Power: mk_warspite (blast)",
                GameAction.PowerFire => "Power: mk_erebus (fire)",
                GameAction.PowerTelekinesis => "Power: mk_orzabal (telekinesis)",
                GameAction.UseDrug => "Use Drug",
                GameAction.Interact => "Interact (take, read)",
                GameAction.Scan => "Psychic Scan",
                GameAction.Inventory => "Inventory",
                GameAction.Defend => "Defend (hold: shield, later)",
                _ => action.ToString()
            };
        }

        /// <summary>
        /// The code defaults. The scene keeps its own serialized copy of the list, so a new action added here
        /// never reached a built scene (Defend was missing from the remap list, 2026-10-20). Awake now appends
        /// every default the serialized list lacks (EnsureAllActions).
        /// </summary>
        private static readonly KeyBinding[] Defaults =
        {
            new KeyBinding { action = GameAction.Forward, defaultKey = KeyCode.W },
            new KeyBinding { action = GameAction.Back, defaultKey = KeyCode.S },
            new KeyBinding { action = GameAction.TurnLeft, defaultKey = KeyCode.A },
            new KeyBinding { action = GameAction.TurnRight, defaultKey = KeyCode.D },
            new KeyBinding { action = GameAction.Run, defaultKey = KeyCode.LeftShift },
            new KeyBinding { action = GameAction.Dodge, defaultKey = KeyCode.Space },
            new KeyBinding { action = GameAction.Charge, defaultKey = KeyCode.Mouse0 },
            new KeyBinding { action = GameAction.SwitchTarget, defaultKey = KeyCode.Tab },
            new KeyBinding { action = GameAction.PowerBlast, defaultKey = KeyCode.Alpha1 },
            new KeyBinding { action = GameAction.PowerFire, defaultKey = KeyCode.Alpha2 },
            new KeyBinding { action = GameAction.PowerTelekinesis, defaultKey = KeyCode.Alpha3 },
            new KeyBinding { action = GameAction.UseDrug, defaultKey = KeyCode.Q },
            new KeyBinding { action = GameAction.Interact, defaultKey = KeyCode.E },
            new KeyBinding { action = GameAction.Defend, defaultKey = KeyCode.Mouse1 },
            new KeyBinding { action = GameAction.Scan, defaultKey = KeyCode.F },
            new KeyBinding { action = GameAction.Inventory, defaultKey = KeyCode.I },
        };

        [Header("Default Bindings")]
        [SerializeField] private List<KeyBinding> bindings = new List<KeyBinding>(Defaults);

        private Dictionary<GameAction, KeyCode> keyMap = new Dictionary<GameAction, KeyCode>();

        public event Action OnBindingsChanged;

        private GameAction? actionBeingRebound = null;
        public bool IsRebinding => actionBeingRebound.HasValue;
        public GameAction? ActionBeingRebound => actionBeingRebound;

        private Action<bool> rebindCallback;

        private const string PREFS_PREFIX = "keybind_";

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureAllActions();
            LoadBindings();
        }

        /// <summary>Appends the code default of every action the serialized list lacks, in enum order.</summary>
        private void EnsureAllActions()
        {
            if (bindings == null) bindings = new List<KeyBinding>();
            foreach (KeyBinding fallback in Defaults)
            {
                bool present = false;
                for (int i = 0; i < bindings.Count; i++)
                {
                    if (bindings[i].action == fallback.action) { present = true; break; }
                }
                if (!present) bindings.Add(new KeyBinding { action = fallback.action, defaultKey = fallback.defaultKey });
            }
        }

        void Update()
        {
            if (actionBeingRebound.HasValue)
            {
                ListenForRebind();
            }
        }

        // ── Query Methods ─────────────────────────────────────────────

        public bool GetActionDown(GameAction action)
        {
            if (GameManager.Instance != null && !GameManager.Instance.AllowsAction(action)) return false;
            if (IsRebinding) return false;
            return Input.GetKeyDown(GetKey(action));
        }

        public bool GetAction(GameAction action)
        {
            if (GameManager.Instance != null && !GameManager.Instance.AllowsAction(action)) return false;
            if (IsRebinding) return false;
            return Input.GetKey(GetKey(action));
        }

        public bool GetActionUp(GameAction action)
        {
            if (GameManager.Instance != null && !GameManager.Instance.AllowsAction(action)) return false;
            if (IsRebinding) return false;
            return Input.GetKeyUp(GetKey(action));
        }

        public KeyCode GetKey(GameAction action)
        {
            if (keyMap.TryGetValue(action, out KeyCode key))
                return key;

            for (int i = 0; i < bindings.Count; i++)
            {
                if (bindings[i].action == action)
                    return bindings[i].currentKey;
            }

            return KeyCode.None;
        }

        public static string GetKeyDisplayName(KeyCode key)
        {
            return key switch
            {
                KeyCode.Mouse0 => "LMB",
                KeyCode.Mouse1 => "RMB",
                KeyCode.Mouse2 => "MMB",
                KeyCode.Mouse3 => "Mouse 4",
                KeyCode.Mouse4 => "Mouse 5",
                KeyCode.LeftControl => "L-Ctrl",
                KeyCode.RightControl => "R-Ctrl",
                KeyCode.LeftShift => "L-Shift",
                KeyCode.RightShift => "R-Shift",
                KeyCode.LeftAlt => "L-Alt",
                KeyCode.RightAlt => "R-Alt",
                KeyCode.Alpha0 => "0",
                KeyCode.Alpha1 => "1",
                KeyCode.Alpha2 => "2",
                KeyCode.Alpha3 => "3",
                KeyCode.Alpha4 => "4",
                KeyCode.Alpha5 => "5",
                KeyCode.Alpha6 => "6",
                KeyCode.Alpha7 => "7",
                KeyCode.Alpha8 => "8",
                KeyCode.Alpha9 => "9",
                KeyCode.Return => "Enter",
                KeyCode.Escape => "Esc",
                KeyCode.BackQuote => "`",
                KeyCode.Tab => "Tab",
                KeyCode.Space => "Space",
                _ => key.ToString()
            };
        }

        // ── Rebinding ──────────────────────────────────────────────────

        public void StartRebind(GameAction action, Action<bool> callback = null)
        {
            actionBeingRebound = action;
            rebindCallback = callback;
        }

        public void CancelRebind()
        {
            actionBeingRebound = null;
            rebindCallback?.Invoke(false);
            rebindCallback = null;
        }

        private void ListenForRebind()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelRebind();
                return;
            }

            foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
            {
                if (key == KeyCode.Escape || key == KeyCode.None) continue;
                if (key >= KeyCode.F1 && key <= KeyCode.F15) continue;

                if (Input.GetKeyDown(key))
                {
                    SetBinding(actionBeingRebound.Value, key);
                    actionBeingRebound = null;
                    rebindCallback?.Invoke(true);
                    rebindCallback = null;
                    return;
                }
            }
        }

        public void SetBinding(GameAction action, KeyCode key)
        {
            keyMap[action] = key;

            for (int i = 0; i < bindings.Count; i++)
            {
                if (bindings[i].action == action)
                {
                    var b = bindings[i];
                    b.currentKey = key;
                    bindings[i] = b;
                    break;
                }
            }

            SaveBindings();
            OnBindingsChanged?.Invoke();
        }

        // ── Persistence ────────────────────────────────────────────────

        private void LoadBindings()
        {
            keyMap.Clear();

            for (int i = 0; i < bindings.Count; i++)
            {
                var b = bindings[i];
                string prefsKey = PREFS_PREFIX + b.action.ToString();

                if (PlayerPrefs.HasKey(prefsKey))
                {
                    b.currentKey = (KeyCode)PlayerPrefs.GetInt(prefsKey);
                }
                else
                {
                    b.currentKey = b.defaultKey;
                }

                bindings[i] = b;
                keyMap[b.action] = b.currentKey;
            }
        }

        private void SaveBindings()
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                string prefsKey = PREFS_PREFIX + bindings[i].action.ToString();
                PlayerPrefs.SetInt(prefsKey, (int)bindings[i].currentKey);
            }
            PlayerPrefs.Save();
        }

        public void ResetToDefaults()
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                var b = bindings[i];
                b.currentKey = b.defaultKey;
                bindings[i] = b;
                keyMap[b.action] = b.currentKey;

                string prefsKey = PREFS_PREFIX + b.action.ToString();
                PlayerPrefs.DeleteKey(prefsKey);
            }

            PlayerPrefs.Save();
            OnBindingsChanged?.Invoke();
        }

        public IReadOnlyList<KeyBinding> GetAllBindings()
        {
            return bindings.AsReadOnly();
        }
    }
}
