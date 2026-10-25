using System.Collections;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// The drug key opens the drug choice (Bruno, 2026-10-24), so a bot takes a drug in three presses: the drug
    /// key, then the interact key twice (it asks, then it takes). Frame waits: the overlay pauses the game.
    /// </summary>
    public static class DrugPress
    {
        public static IEnumerator TakeFirst(VirtualPad pad)
        {
            pad.Tap(InputManager.GameAction.UseDrug);
            yield return null;
            yield return null;
            pad.Tap(InputManager.GameAction.Interact);
            yield return null;
            yield return null;
            pad.Tap(InputManager.GameAction.Interact);
            yield return null;
            yield return null;
        }
    }
}
