using UnityEngine;
using UnityEngine.InputSystem;

namespace GlorifiedBody.Gore
{
    public sealed class GoreDebugControls : MonoBehaviour
    {
        [SerializeField] private GoreManager goreManager;

        private void Awake()
        {
            if (goreManager == null)
            {
                goreManager = GetComponent<GoreManager>();
            }

            Debug.Log($"[GoreDebug] Active on {name}. Keys: F8 Off, F9 Low, F11 Full, F7 Clear, F12 Verbose.");
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || goreManager == null)
            {
                return;
            }

            if (keyboard.f8Key.wasPressedThisFrame)
            {
                goreManager.SetTier(GoreTier.Off);
                Debug.Log($"[GoreDebug] F8 pressed. Gore OFF.");
            }

            if (keyboard.f9Key.wasPressedThisFrame)
            {
                goreManager.SetTier(GoreTier.Low);
                Debug.Log($"[GoreDebug] F9 pressed. Gore LOW.");
            }

            if (keyboard.f11Key.wasPressedThisFrame)
            {
                goreManager.SetTier(GoreTier.Full);
                Debug.Log($"[GoreDebug] F11 pressed. Gore FULL.");
            }

            if (keyboard.f7Key.wasPressedThisFrame)
            {
                goreManager.ClearEffects();
                Debug.Log("[GoreDebug] F7 pressed. Gore effects cleared.");
            }

            if (keyboard.f12Key.wasPressedThisFrame)
            {
                goreManager.ToggleDebugLogging();
            }
        }
    }
}
