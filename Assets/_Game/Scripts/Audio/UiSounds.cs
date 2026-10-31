using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Audio
{
    /// <summary>
    /// The menus make sound (the audio sweep: all silent). A move of the keyboard selection: ui_move. A button press or
    /// a toggle: ui_confirm. The pause and the inventory open and close with ui_open and ui_close. The menus are built
    /// in code at run time, so the buttons are found and hooked once a second. Placeholders (Kenney CC0).
    /// </summary>
    public sealed class UiSounds : MonoBehaviour
    {
        private const float ScanSeconds = 1f;

        private readonly HashSet<int> hooked = new HashSet<int>();
        private GameObject lastSelected;
        private float nextScan;
        private GameManager.GameState lastState = GameManager.GameState.MainMenu;

        public int Hooked => hooked.Count;

        private void OnEnable() => GameManager.OnStateChanged += OnStateChanged;
        private void OnDisable() => GameManager.OnStateChanged -= OnStateChanged;

        private void OnStateChanged(GameManager.GameState state)
        {
            bool stopped = state == GameManager.GameState.Paused || state == GameManager.GameState.Inventory || state == GameManager.GameState.Map;
            bool wasStopped = lastState == GameManager.GameState.Paused || lastState == GameManager.GameState.Inventory || lastState == GameManager.GameState.Map;
            if (stopped && !wasStopped) Sfx.Ui(Sfx.UiOpen, 0.6f);
            else if (!stopped && wasStopped && state == GameManager.GameState.Playing) Sfx.Ui(Sfx.UiClose, 0.6f);
            lastState = state;
        }

        private void Update()
        {
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + ScanSeconds;
                Hook();
            }
            EventSystem events = EventSystem.current;
            GameObject selected = events != null ? events.currentSelectedGameObject : null;
            if (selected != lastSelected)
            {
                if (selected != null && lastSelected != null) Sfx.Ui(Sfx.UiMove, 0.35f);
                lastSelected = selected;
            }
        }

        /// <summary>Hooks the buttons and toggles of the scene that have no sound yet (tests call it at once).</summary>
        public void Hook()
        {
            foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (hooked.Add(button.GetInstanceID())) button.onClick.AddListener(() => Sfx.Ui(Sfx.UiConfirm, 0.55f));
            foreach (Toggle toggle in FindObjectsByType<Toggle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (hooked.Add(toggle.GetInstanceID())) toggle.onValueChanged.AddListener(_ => Sfx.Ui(Sfx.UiConfirm, 0.45f));
        }
    }
}
