using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Gore.ThatIndividual;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Gore
{
    /// <summary>
    /// Starts the gore in every gameplay room (Bruno, 2026-10-24: "gore urp implementation"). The gore port of step 1
    /// never ran: no scene held the GoreManager. On each scene load with gameplay systems (a GameplayInitializer),
    /// this adds PF_GoreManager from Resources, removes its debug keys (F7, F8, F9, F11, F12 clash with ours), starts
    /// the That Individual driver (the vendor blood sprays, bursts, stains) and the gruesome-kill effects
    /// (GruesomeDeathFx), and applies the player's Gore option (SettingsManager.GoreLevel: Off, Low, Full).
    /// </summary>
    public static class GoreBootstrap
    {
        public const string ResourcePath = "Gore/PF_GoreManager";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Ensure();

        /// <summary>The gore systems in this room, once. Nothing in a menu scene.</summary>
        public static void Ensure()
        {
            if (Object.FindFirstObjectByType<GameplayInitializer>() == null) return;
            if (GoreManager.Instance == null)
            {
                GameObject prefab = Resources.Load<GameObject>(ResourcePath);
                if (prefab == null)
                {
                    Debug.LogWarning($"[Gore] Resources/{ResourcePath} is missing: no gore in this room.");
                    return;
                }
                GameObject manager = Object.Instantiate(prefab);
                manager.name = prefab.name;
                foreach (GoreDebugControls keys in manager.GetComponentsInChildren<GoreDebugControls>(true)) Object.Destroy(keys);
                foreach (DismembermentDebugControls keys in manager.GetComponentsInChildren<DismembermentDebugControls>(true)) Object.Destroy(keys);
                if (manager.GetComponent<GruesomeDeathFx>() == null) manager.AddComponent<GruesomeDeathFx>();
            }
            ThatIndividualGoreDriver.EnsureDriver();
            ApplySetting();
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.OnGoreLevelChanged -= OnGoreLevelChanged;
                SettingsManager.Instance.OnGoreLevelChanged += OnGoreLevelChanged;
            }
        }

        private static void OnGoreLevelChanged(int level) => ApplySetting();

        private static void ApplySetting()
        {
            if (GoreManager.Instance == null || SettingsManager.Instance == null) return;
            GoreManager.Instance.SetTier((GoreTier)Mathf.Clamp(SettingsManager.Instance.GoreLevel, 0, 2));
        }
    }
}
