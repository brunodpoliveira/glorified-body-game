using UnityEngine;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;

namespace GlorifiedBody.DebugTools
{
    /// <summary>
    /// Manual checks of the bars (Bruno, 2026-10-20): F5 fills the SHORT bar, so the next attack or defend press
    /// starts a short. F6 starts the short at once. F7 refills HP and nalcon and gives two drugs of each type.
    /// F8 removes 25 nalcon and F9 adds 25 (the low-nalcon warning by hand, Bruno: the dummies die too soon).
    /// F-keys are reserved and never rebound. Gone when the game ships. The tests call the public methods.
    /// </summary>
    public sealed class DebugCheatKeys : MonoBehaviour
    {
        public const float NalconStep = 25f;

        [SerializeField] private KeyCode fillShortKey = KeyCode.F5;
        [SerializeField] private KeyCode startShortKey = KeyCode.F6;
        [SerializeField] private KeyCode refillKey = KeyCode.F7;
        [SerializeField] private KeyCode removeNalconKey = KeyCode.F8;
        [SerializeField] private KeyCode addNalconKey = KeyCode.F9;
        [SerializeField] private KeyCode lookTestKey = KeyCode.F11; // the look test room of step 4 (F10 cycles its dressings)
        [SerializeField] private string lookTestScene = "LookTest";

        private void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm != null && !gm.IsPlaying) return;
            if (Input.GetKeyDown(fillShortKey)) FillShort();
            if (Input.GetKeyDown(startShortKey)) StartShort();
            if (Input.GetKeyDown(refillKey)) Refill();
            if (Input.GetKeyDown(removeNalconKey)) RemoveNalcon();
            if (Input.GetKeyDown(addNalconKey)) AddNalcon();
            if (Input.GetKeyDown(lookTestKey) && GameManager.Instance != null) GameManager.Instance.LoadLevel(lookTestScene);
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
            Debug.Log("[DebugCheatKeys] HP and nalcon refilled, two drugs of each.");
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
            Debug.Log($"[DebugCheatKeys] Nalcon {(delta < 0f ? "-" : "+")}{Mathf.Abs(delta):0}: now {stats.Nalcon:0}.");
            return true;
        }

        private static PsychicStats FindStats(out GameObject player)
        {
            player = GameObject.FindWithTag("Player");
            return player != null ? player.GetComponent<PsychicStats>() : null;
        }
    }
}
