using UnityEngine;
using GlorifiedBody.Systems;

namespace GlorifiedBody.UI
{
    /// <summary>
    /// Plain level complete overlay. Shown when GameManager enters LevelComplete.
    /// </summary>
    public sealed class LevelCompleteUI : RuntimeOverlayMenu
    {
        protected override string MenuTitle => "LEVEL COMPLETE";
        protected override string MenuSubtitle => "Arena clear. Restart or return to menu.";
        protected override UnityEngine.Color AccentColor => new UnityEngine.Color(0.4f, 1f, 0.55f);
        protected override bool ShouldShow =>
            GameManager.Instance != null && GameManager.Instance.IsLevelComplete;

        protected override void Update()
        {
            base.Update();
            if (!ShouldShow)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.R))
            {
                GameManager.Instance?.RestartLevel();
            }
            else if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.M))
            {
                GameManager.Instance?.LoadMainMenu();
            }
        }

        protected override void BuildButtons(UnityEngine.Transform parent)
        {
            AddButton(parent, "Restart", () => GameManager.Instance?.RestartLevel(), true);
            AddButton(parent, "Main Menu", () => GameManager.Instance?.LoadMainMenu());
        }
    }
}
