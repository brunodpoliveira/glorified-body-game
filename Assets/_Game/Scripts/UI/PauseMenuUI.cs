using UnityEngine;
using GlorifiedBody.Systems;

namespace GlorifiedBody.UI
{
    public sealed class PauseMenuUI : RuntimeOverlayMenu
    {
        protected override string MenuTitle => "PAUSED";
        protected override string MenuSubtitle => "Resume, or retry the room.";
        protected override Vector2 PanelSize => new Vector2(500f, 620f);
        // The AC3 frame of the menu screens (T46, 2026-11-17).
        protected override bool UseFrame => true;
        protected override string FrameSide => "PAUSE";
        protected override string FrameIcon => "continue";
        protected override string FrameStatus => "ESC  RESUME";

        protected override bool ShouldShow =>
            GameManager.Instance != null
            && GameManager.Instance.IsPaused
            && !IsSubMenuVisible();

        private bool IsSubMenuVisible()
        {
            var difficulty = GetComponent<DifficultyMenuUI>();
            if (difficulty != null && difficulty.IsVisible) return true;
            var options = GetComponent<OptionsMenuUI>();
            if (options != null && options.IsVisible) return true;
            var credits = GetComponent<CreditsUI>();
            if (credits != null && credits.IsVisible) return true;
            return false;
        }

        protected override void BuildButtons(Transform parent)
        {
            AddButton(parent, "Resume", () => GameManager.Instance?.Resume(), true);
            AddButton(parent, "Difficulty", () => GetComponent<DifficultyMenuUI>()?.Show());
            AddButton(parent, "Options", () => GetComponent<OptionsMenuUI>()?.Show());
            // Bruno, 2026-11-16: "restart needs to be clearer: restart from latest checkpoint". With no checkpoint the demo starts over.
            AddButton(parent, "Restart from latest checkpoint", () =>
            {
                if (GameManager.Instance != null && !GameManager.Instance.RestartFromLatestCheckpoint())
                {
                    GameManager.Instance.RestartLevel();
                }
            });
            AddButton(parent, "Main Menu", () => GameManager.Instance?.LoadMainMenu());
            AddButton(parent, "Quit", () => GameManager.Instance?.QuitGame());
        }
    }
}
