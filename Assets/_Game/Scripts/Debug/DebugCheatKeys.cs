using UnityEngine;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;

namespace GlorifiedBody.DebugTools
{
    /// <summary>
    /// Manual checks of the bars (Bruno, 2026-10-20): F5 fills the SHORT bar, so the next release starts a short.
    /// F6 starts the short at once. F7 refills HP and nalcon and gives two drugs of each type.
    /// F-keys are reserved and never rebound. Gone when the game ships. The tests call the public methods.
    /// </summary>
    public sealed class DebugCheatKeys : MonoBehaviour
    {
        [SerializeField] private KeyCode fillShortKey = KeyCode.F5;
        [SerializeField] private KeyCode startShortKey = KeyCode.F6;
        [SerializeField] private KeyCode refillKey = KeyCode.F7;

        private void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm != null && !gm.IsPlaying) return;
            if (Input.GetKeyDown(fillShortKey)) FillShort();
            if (Input.GetKeyDown(startShortKey)) StartShort();
            if (Input.GetKeyDown(refillKey)) Refill();
        }

        /// <summary>F5: the SHORT bar goes to full. The next release starts a short.</summary>
        public bool FillShort()
        {
            PsychicStats stats = FindStats(out _);
            if (stats == null) return false;
            stats.DebugSet(stats.Nalcon, PsychicStats.MaxShort);
            Debug.Log("[DebugCheatKeys] SHORT bar filled. The next release starts a short.");
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

        private static PsychicStats FindStats(out GameObject player)
        {
            player = GameObject.FindWithTag("Player");
            return player != null ? player.GetComponent<PsychicStats>() : null;
        }
    }
}
