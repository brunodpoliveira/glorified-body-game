using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Audio
{
    /// <summary>
    /// The sound system's one object for the whole game ("[GameAudio]", made at the start, kept through every load):
    /// the music director, the listener on Lucy, the pause of the world sounds, the volume sliders, and steps on every
    /// figure of a new room. It needs no scene change: a room built before 2026-10-26 gets it too.
    /// </summary>
    public sealed class AudioHost : MonoBehaviour
    {
        public static AudioHost Instance { get; private set; }

        public MusicDirector Music { get; private set; }
        public ListenerRig Ears { get; private set; }
        public UiSounds Ui { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (Instance != null) return;
            var obj = new GameObject("[GameAudio]");
            DontDestroyOnLoad(obj);
            obj.AddComponent<AudioHost>();
        }

        private void Awake()
        {
            Instance = this;
            Music = gameObject.AddComponent<MusicDirector>();
            var ears = new GameObject("Ears");
            ears.transform.SetParent(transform, false);
            Ears = ears.AddComponent<ListenerRig>();
            Ui = gameObject.AddComponent<UiSounds>();
            SceneManager.sceneLoaded += OnSceneLoaded;
            GameManager.OnStateChanged += OnStateChanged;
            HitEventBus.OnHit += CombatSounds.OnHit;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            GameManager.OnStateChanged -= OnStateChanged;
            HitEventBus.OnHit -= CombatSounds.OnHit;
            if (SettingsManager.Instance != null) SettingsManager.Instance.OnAudioChanged -= GameAudio.Refresh;
            if (Instance == this) Instance = null;
        }

        private bool settingsBound;

        private void Update()
        {
            // The settings object comes with the first scene: bind the sliders once it is there.
            if (!settingsBound && SettingsManager.Instance != null)
            {
                SettingsManager.Instance.OnAudioChanged += GameAudio.Refresh;
                settingsBound = true;
                GameAudio.Refresh();
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            AudioListener.pause = false;
            Ears.Rebind();
            Music.EnterRoom(scene);
            foreach (Health health in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (health.GetComponent<Footsteps>() != null || health.GetComponentInChildren<Animator>() == null) continue;
                bool lucy = health.CompareTag("Player");
                if (lucy && health.GetComponent<PowerSounds>() == null) health.gameObject.AddComponent<PowerSounds>();
                if (!lucy && !health.CompareTag("Enemy")) continue;
                if (!lucy && health.GetComponent<LockOnTargetable>() is LockOnTargetable t && t.IsProp) continue;
                health.gameObject.AddComponent<Footsteps>().Key = lucy ? Sfx.StepLucy : Sfx.StepBoots;
            }
        }

        /// <summary>The pause, the inventory and the death screen stop the world sounds. Music and UI go on.</summary>
        private static void OnStateChanged(GameManager.GameState state)
        {
            // A cutscene stops the world's sounds too; its own lines and music play through (CutscenePlayer).
            AudioListener.pause = state == GameManager.GameState.Paused || state == GameManager.GameState.Inventory
                                  || state == GameManager.GameState.GameOver || state == GameManager.GameState.Cutscene
                                  || state == GameManager.GameState.Reading || state == GameManager.GameState.Map;
            if (state == GameManager.GameState.GameOver) Sfx.Ui(Sfx.DeathSting, 0.9f);
        }
    }
}
