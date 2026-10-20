using UnityEngine;
using GlorifiedBody.UI;

namespace GlorifiedBody.Systems
{
    /// <summary>
    /// Attach to a GameObject in the gameplay scene (e.g., [GameplaySystems]).
    /// Signals GameManager that gameplay is ready, and bootstraps manager singletons
    /// if the scene is entered directly in the editor without the main menu.
    /// </summary>
    public class GameplayInitializer : MonoBehaviour
    {
        [Header("Manager Prefab (optional)")]
        [Tooltip("If GameManager doesn't exist (e.g. playing from editor), this prefab will be instantiated.")]
        [SerializeField] private GameObject managersPrefab;

        private void Awake()
        {
            EnsureManagersExist();
        }

        private void Start()
        {
            SetupMenus();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameplaySceneReady();
            }
        }

        private void EnsureManagersExist()
        {
            if (GameManager.Instance != null || FindFirstObjectByType<GameManager>() != null)
            {
                return;
            }

            if (managersPrefab != null)
            {
                Instantiate(managersPrefab);
            }
            else
            {
                var managersObj = new GameObject("[GameManagers]");
                managersObj.AddComponent<GameManager>();
                managersObj.AddComponent<InputManager>();
                managersObj.AddComponent<DifficultyManager>();
                managersObj.AddComponent<SettingsManager>();
            }
        }

        private void SetupMenus()
        {
            // The scene builder (MenuLoopSetup) already hosts these menus on [RuntimeUI]. Never duplicate them.
            GameObject uiHost = GameObject.Find("[RuntimeUI]");
            if (uiHost == null)
            {
                uiHost = new GameObject("[RuntimeUI]");
            }

            EnsureMenu<PauseMenuUI>(uiHost);
            EnsureMenu<DeathMenuUI>(uiHost);
            EnsureMenu<LevelCompleteUI>(uiHost);
            EnsureMenu<DifficultyMenuUI>(uiHost);
            EnsureMenu<OptionsMenuUI>(uiHost);
            EnsureMenu<CreditsUI>(uiHost);
        }

        private static void EnsureMenu<T>(GameObject host) where T : Component
        {
            if (FindFirstObjectByType<T>() == null)
            {
                host.AddComponent<T>();
            }
        }
    }
}
