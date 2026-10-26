using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;

namespace GlorifiedBody.DebugTools
{
    /// <summary>
    /// Manual checks of the bars (Bruno, 2026-10-20): F5 fills the SHORT bar, so the next attack or defend press
    /// starts a short. F6 starts the short at once. F7 refills HP and nalcon and gives two drugs of each type.
    /// F8 removes 25 nalcon and F9 adds 25 (the low-nalcon warning by hand, Bruno: the dummies die too soon).
    /// F11 loads the look test rooms of step 4, one after the other (the Gallery, then the corridor with the
    /// recovery room and the therapy gym, then the Gallery again). F3 cycles the dressings there.
    /// F-keys are reserved and never rebound. Gone when the game ships. The tests call the public methods.
    /// </summary>
    public sealed class DebugCheatKeys : MonoBehaviour
    {
        public const float NalconStep = 25f;
        public static readonly string[] LookScenes = { "LookTest", "LookTest2", "LookTestExterior" };

        [SerializeField] private KeyCode fillShortKey = KeyCode.F5;
        [SerializeField] private KeyCode startShortKey = KeyCode.F6;
        [SerializeField] private KeyCode refillKey = KeyCode.F7;
        [SerializeField] private KeyCode removeNalconKey = KeyCode.F8;
        [SerializeField] private KeyCode addNalconKey = KeyCode.F9;
        [SerializeField] private KeyCode careTestKey = KeyCode.F12; // the soldiers' care counts her as unseen (toggle)
        [SerializeField] private KeyCode lookTestKey = KeyCode.F11;

        private void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm != null && !gm.IsPlaying) return;
            if (Input.GetKeyDown(fillShortKey)) FillShort();
            if (Input.GetKeyDown(startShortKey)) StartShort();
            if (Input.GetKeyDown(refillKey)) Refill();
            if (Input.GetKeyDown(removeNalconKey)) RemoveNalcon();
            if (Input.GetKeyDown(addNalconKey)) AddNalcon();
            if (Input.GetKeyDown(careTestKey))
            {
                GlorifiedBody.Enemy.CorpseCare.DebugHidden = !GlorifiedBody.Enemy.CorpseCare.DebugHidden;
                Debug.Log($"[DebugCheatKeys] F12: the care counts her as {(GlorifiedBody.Enemy.CorpseCare.DebugHidden ? "unseen" : "seen")}.");
            }
            if (Input.GetKeyDown(lookTestKey) && GameManager.Instance != null) GameManager.Instance.LoadLevel(NextLookScene(SceneManager.GetActiveScene().name));
        }

        /// <summary>The look room after the given scene: the first one from any other room, the next one from a look room.</summary>
        public static string NextLookScene(string currentScene)
        {
            int i = System.Array.IndexOf(LookScenes, currentScene);
            return i < 0 ? LookScenes[0] : LookScenes[(i + 1) % LookScenes.Length];
        }

        /// <summary>F5: the SHORT bar goes to full. The next attack or defend press starts a short.</summary>
        public bool FillShort()
        {
            PsychicStats stats = FindStats(out _);
            if (stats == null) return false;
            stats.DebugSet(stats.Nalcon, PsychicStats.MaxShort);
            Debug.Log("[DebugCheatKeys] SHORT bar filled. The next attack or defend press starts a short.");
            return true;
        }

        /// <summary>F6: the short starts now.</summary>
        public bool StartShort()
        {
            PsychicStats stats = FindStats(out GameObject player);
            ShortController shortController = player != null ? player.GetComponent<ShortController>() : null;
            if (stats == null || shortController == null || shortController.IsActive) return false;
            stats.DebugSet(stats.Nalcon, PsychicStats.MaxShort);
            shortController.Begin();
            Debug.Log("[DebugCheatKeys] Short started.");
            return true;
        }

        /// <summary>F7: HP and nalcon back to full, two drugs of each type.</summary>
        public bool Refill()
        {
            PsychicStats stats = FindStats(out GameObject player);
            if (stats == null) return false;
            stats.RefillAll();
            DrugInventory drugs = player.GetComponent<DrugInventory>();
            if (drugs != null)
            {
                drugs.DebugSet(DrugType.Nalcon, 2);
                drugs.DebugSet(DrugType.Red, 2);
                drugs.DebugSet(DrugType.DFelon, 2);
            }
            Debug.Log("[DebugCheatKeys] HP and MK refilled, two drugs of each.");
            return true;
        }

        /// <summary>F8: 25 nalcon less (never under 0).</summary>
        public bool RemoveNalcon() => ShiftNalcon(-NalconStep);

        /// <summary>F9: 25 nalcon more (never over the maximum).</summary>
        public bool AddNalcon() => ShiftNalcon(NalconStep);

        private static bool ShiftNalcon(float delta)
        {
            PsychicStats stats = FindStats(out _);
            if (stats == null) return false;
            stats.DebugSet(stats.Nalcon + delta, stats.Short);
            Debug.Log($"[DebugCheatKeys] MK {(delta < 0f ? "-" : "+")}{Mathf.Abs(delta):0}: now {stats.Nalcon:0}.");
            return true;
        }

        private static PsychicStats FindStats(out GameObject player)
        {
            player = GameObject.FindWithTag("Player");
            return player != null ? player.GetComponent<PsychicStats>() : null;
        }
    }
}
