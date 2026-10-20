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
            GameOver,
            LevelComplete
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
        }

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
        /// Restart at the latest checkpoint. Build-order step 1 has no checkpoints, so this returns false
        /// and the caller falls back to RestartLevel(). The checkpoint path returns with the rooms (step 2).
        /// </summary>
        public bool RestartFromLatestCheckpoint()
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = storedFixedDeltaTime;
            return false;
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

                case GameState.GameOver:
                    SetCursor(true);
                    break;

                case GameState.Loading:
                    SetCursor(false);
                    break;

                case GameState.LevelComplete:
                    SetCursor(true);
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
