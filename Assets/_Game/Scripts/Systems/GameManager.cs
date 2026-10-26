using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.UI;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Systems
{
    /// <summary>
    /// Central game state manager. Handles scene loading, pause, cursor state.
    /// GameManager owns Time.timeScale in this project (pause, game over, load).
    /// Persists across scenes via DontDestroyOnLoad.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public enum GameState
        {
            MainMenu,
            Loading,
            Playing,
            Paused,
            Inventory, // the inventory overlay: time stops, only its keys pass (Bruno, 2026-10-22)
            GameOver,
            LevelComplete,
            Cutscene // a cutscene plays: time stops, no game key passes (2026-10-26)
        }

        public static GameManager Instance { get; private set; }

        [Header("Scene Names")]
        [SerializeField] private string mainMenuSceneName = "Main_Menu";
        [SerializeField] private string gameplaySceneName = "TrainingRoom";

        private GameState currentState = GameState.MainMenu;
        private float storedTimeScale = 1f;
        private float storedFixedDeltaTime;

        public GameState CurrentState => currentState;
        public bool IsPaused => currentState == GameState.Paused;
        public bool IsInventory => currentState == GameState.Inventory;

        /// <summary>The keys the inventory overlay uses; every other game action is blocked while it is open.</summary>
        public static bool IsInventoryAction(InputManager.GameAction action) =>
            action == InputManager.GameAction.Inventory || action == InputManager.GameAction.Interact
            || action == InputManager.GameAction.Back || action == InputManager.GameAction.Forward
            || action == InputManager.GameAction.TurnLeft || action == InputManager.GameAction.TurnRight;

        /// <summary>False while a menu or the game over owns the keys; inside the inventory only its keys pass.</summary>
        public bool AllowsAction(InputManager.GameAction action)
        {
            if (currentState == GameState.Paused || currentState == GameState.GameOver) return false;
            if (currentState == GameState.Cutscene) return action == InputManager.GameAction.Interact; // the skip key
            if (currentState == GameState.Inventory) return IsInventoryAction(action);
            return true;
        }

        /// <summary>Opens the inventory overlay: the game stops as in the pause, the overlay owns the keys.</summary>
        public void OpenInventory()
        {
            if (currentState != GameState.Playing) return;
            storedTimeScale = Time.timeScale;
            storedFixedDeltaTime = Time.fixedDeltaTime;
            Time.timeScale = 0f;
            Time.fixedDeltaTime = 0f;
            SetState(GameState.Inventory);
        }

        public void CloseInventory()
        {
            if (currentState != GameState.Inventory) return;
            Time.timeScale = storedTimeScale;
            Time.fixedDeltaTime = storedFixedDeltaTime;
            SetState(GameState.Playing);
        }

        public bool IsCutscene => currentState == GameState.Cutscene;

        /// <summary>A cutscene starts: the world stops as in the pause, and no game key passes. False when the game is not playing.</summary>
        public bool BeginCutscene()
        {
            if (currentState != GameState.Playing) return false;
            storedTimeScale = Time.timeScale;
            storedFixedDeltaTime = Time.fixedDeltaTime;
            Time.timeScale = 0f;
            Time.fixedDeltaTime = 0f;
            SetState(GameState.Cutscene);
            return true;
        }

        public void EndCutscene()
        {
            if (currentState != GameState.Cutscene) return;
            Time.timeScale = storedTimeScale;
            Time.fixedDeltaTime = storedFixedDeltaTime;
            SetState(GameState.Playing);
        }
        public bool IsGameOver => currentState == GameState.GameOver;
        public bool IsPlaying => currentState == GameState.Playing;
        public bool IsInMenu => currentState == GameState.MainMenu;
        public bool IsLevelComplete => currentState == GameState.LevelComplete;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            storedFixedDeltaTime = Time.fixedDeltaTime;
        }

        void Update()
        {
            if (currentState == GameState.Inventory)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) CloseInventory();
                return;
            }
            if (currentState == GameState.Playing || currentState == GameState.Paused)
            {
                if (InputManager.Instance != null && InputManager.Instance.IsRebinding) return;

                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    var options = FindFirstObjectByType<OptionsMenuUI>();
                    if (options != null && options.IsVisible) return;
                    var credits = FindFirstObjectByType<CreditsUI>();
                    if (credits != null && credits.IsVisible) return;
                    var difficulty = FindFirstObjectByType<DifficultyMenuUI>();
                    if (difficulty != null && difficulty.IsVisible) return;

                    if (currentState == GameState.Playing)
                        Pause();
                    else if (currentState == GameState.Paused)
                        Resume();
                }
            }
        }

        // ── State Transitions ──────────────────────────────────────────

        public void SetState(GameState newState)
        {
            if (newState != currentState) RunTelemetry.Event("game_state", newState.ToString());
            if (currentState == newState) return;

            currentState = newState;

            ApplyStateSettings(newState);
            OnStateChanged?.Invoke(newState);
        }

        /// <summary>Each change of the game state (the sound pauses with the game, 2026-10-26).</summary>
        public static event Action<GameState> OnStateChanged;

        public void Pause()
        {
            if (currentState != GameState.Playing) return;

            storedTimeScale = Time.timeScale;
            storedFixedDeltaTime = Time.fixedDeltaTime;

            Time.timeScale = 0f;
            Time.fixedDeltaTime = 0f;

            SetState(GameState.Paused);
        }

        public void Resume()
        {
            if (currentState != GameState.Paused) return;

            Time.timeScale = storedTimeScale;
            Time.fixedDeltaTime = storedFixedDeltaTime;

            SetState(GameState.Playing);
        }

        public void TriggerGameOver()
        {
            if (currentState == GameState.GameOver || currentState == GameState.Loading)
                return;

            if (currentState == GameState.MainMenu && SceneManager.GetActiveScene().name == mainMenuSceneName)
                return;

            storedTimeScale = Time.timeScale;
            storedFixedDeltaTime = Time.fixedDeltaTime;

            Time.timeScale = 0f;
            Time.fixedDeltaTime = 0f;

            SetState(GameState.GameOver);
        }

        public void CompleteLevel()
        {
            if (currentState == GameState.LevelComplete || currentState == GameState.GameOver
                || currentState == GameState.Loading || currentState == GameState.MainMenu)
                return;

            storedTimeScale = Time.timeScale;
            storedFixedDeltaTime = Time.fixedDeltaTime;

            Time.timeScale = 0f;
            Time.fixedDeltaTime = 0f;

            SetState(GameState.LevelComplete);
        }

        // ── Scene Loading ──────────────────────────────────────────────

        public void LoadMainMenu()
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = storedFixedDeltaTime;

            SetState(GameState.Loading);
            SceneManager.LoadScene(mainMenuSceneName);
        }

        public void LoadGameplay()
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = storedFixedDeltaTime;

            SetState(GameState.Loading);
            SceneManager.LoadScene(gameplaySceneName);
        }

        /// <summary>Load any level scene by name. The scene must be in Build Settings.</summary>
        public void LoadLevel(string sceneName)
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = storedFixedDeltaTime;

            SetState(GameState.Loading);
            RunTelemetry.Event("level_load", sceneName);
            SceneManager.LoadScene(sceneName);
        }

        public void RestartLevel()
        {
            Resume();
            Time.timeScale = 1f;
            Time.fixedDeltaTime = storedFixedDeltaTime;

            SetState(GameState.Loading);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        /// <summary>
        /// Restart at the checkpoint of this game (SaveSystem: a checkpoint point, a manual save or a load). False
        /// when this game has none: the caller falls back to RestartLevel().
        /// </summary>
        public bool RestartFromLatestCheckpoint()
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = storedFixedDeltaTime;
            return GlorifiedBody.Saving.SaveSystem.RestartFromCheckpoint();
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ── Gameplay Entry ─────────────────────────────────────────────

        public void OnGameplaySceneReady()
        {
            SetState(GameState.Playing);
        }

        public void OnMainMenuReady()
        {
            SetState(GameState.MainMenu);
        }

        // ── Internal ──────────────────────────────────────────────────

        private void ApplyStateSettings(GameState state)
        {
            switch (state)
            {
                case GameState.MainMenu:
                    SetCursor(true);
                    break;

                case GameState.Playing:
                    SetCursor(false);
                    break;

                case GameState.Paused:
                    SetCursor(true);
                    break;

                case GameState.Inventory:
                    SetCursor(false);
                    break;

                case GameState.GameOver:
                    SetCursor(true);
                    break;

                case GameState.Loading:
                    SetCursor(false);
                    break;

                case GameState.LevelComplete:
                    SetCursor(true);
                    break;

                case GameState.Cutscene:
                    SetCursor(false);
                    break;
            }
        }

        private void SetCursor(bool visible)
        {
            Cursor.visible = visible;
            Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
        }
    }
}
