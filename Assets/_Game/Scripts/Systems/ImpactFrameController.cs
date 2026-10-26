using UnityEngine;
using System;
using System.Collections;
using UnityEngine.UI;

namespace GlorifiedBody.Systems
{
    public sealed class ImpactFrameController : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float defaultFreezeDuration = 0.18f;
        [SerializeField, Min(0f)] private float defaultRecoveryDuration = 0.55f;
        [Header("Visual")]
        [SerializeField] private bool showOverlayFlash = true;
        [SerializeField, Min(0f)] private float overlayHoldDuration = 0.08f;
        [SerializeField, Min(0f)] private float overlayFadeDuration = 0.16f;
        [SerializeField] private Color overlayColor = new Color(1f, 1f, 1f, 0.8f);

        public static ImpactFrameController Instance { get; private set; }
        public static event Action<float, float> OnImpactFrameRequested;

        private CanvasGroup overlayGroup;
        private Image overlayImage;
        private Coroutine overlayRoutine;
        private Coroutine freezeRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            BuildOverlay();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void RequestImpactFrame()
        {
            RequestImpactFrame(defaultFreezeDuration, defaultRecoveryDuration);
        }

        public void RequestImpactFrame(float freezeDuration, float recoveryDuration)
        {
            OnImpactFrameRequested?.Invoke(freezeDuration, recoveryDuration);
            PlayOverlayFlash();
            Debug.Log($"[ImpactFrame] freeze={freezeDuration:0.00} recovery={recoveryDuration:0.00}.", this);
            if (freezeRoutine != null)
            {
                StopCoroutine(freezeRoutine);
            }

            freezeRoutine = StartCoroutine(FreezeRoutine(freezeDuration, recoveryDuration));
        }

        /// <summary>
        /// Hit-stop. Time.timeScale drops to 0 for freezeDuration (unscaled time), then returns to 1 over
        /// recoveryDuration. A pause or the inventory holds the routine: its clock waits and it leaves timeScale to
        /// them (found 2026-10-26: the pause stored the frozen 0, the routine quit, and the resume put back 0 for
        /// good). A game over, a load or a menu ends it, and they keep their own timeScale.
        /// </summary>
        private IEnumerator FreezeRoutine(float freezeDuration, float recoveryDuration)
        {
            Time.timeScale = 0f;
            float held = 0f;
            while (held < freezeDuration)
            {
                if (IsEnded())
                {
                    freezeRoutine = null;
                    yield break;
                }

                if (IsPlaying()) held += Time.unscaledDeltaTime;
                yield return null;
            }

            float elapsed = 0f;
            float recovery = Mathf.Max(0.01f, recoveryDuration);
            while (elapsed < recovery)
            {
                if (IsEnded())
                {
                    freezeRoutine = null;
                    yield break;
                }

                if (IsPlaying())
                {
                    elapsed += Time.unscaledDeltaTime;
                    Time.timeScale = Mathf.Lerp(0f, 1f, Mathf.Clamp01(elapsed / recovery));
                }
                yield return null;
            }

            if (IsPlaying())
            {
                Time.timeScale = 1f;
            }

            freezeRoutine = null;
        }

        /// <summary>The pause, the inventory, a cutscene and a log or a scan stop the game for a moment: the freeze waits for them.</summary>
        private static bool IsHeld()
        {
            GameManager gm = GameManager.Instance;
            return gm != null && (gm.CurrentState == GameManager.GameState.Paused || gm.CurrentState == GameManager.GameState.Inventory
                                  || gm.CurrentState == GameManager.GameState.Cutscene || gm.CurrentState == GameManager.GameState.Reading);
        }

        private static bool IsEnded() => !IsPlaying() && !IsHeld();

        private static bool IsPlaying()
        {
            return GameManager.Instance == null || GameManager.Instance.IsPlaying;
        }

        private void PlayOverlayFlash()
        {
            if (!showOverlayFlash)
            {
                return;
            }

            BuildOverlay();
            if (overlayRoutine != null)
            {
                StopCoroutine(overlayRoutine);
            }

            overlayRoutine = StartCoroutine(OverlayFlashRoutine());
        }

        private IEnumerator OverlayFlashRoutine()
        {
            overlayImage.color = overlayColor;
            overlayGroup.alpha = 1f;

            float holdUntil = Time.unscaledTime + overlayHoldDuration;
            while (Time.unscaledTime < holdUntil)
            {
                yield return null;
            }

            float elapsed = 0f;
            while (elapsed < overlayFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                overlayGroup.alpha = Mathf.Lerp(1f, 0f, Mathf.Clamp01(elapsed / Mathf.Max(0.01f, overlayFadeDuration)));
                yield return null;
            }

            overlayGroup.alpha = 0f;
            overlayRoutine = null;
        }

        private void BuildOverlay()
        {
            if (overlayGroup != null)
            {
                return;
            }

            var canvasObj = new GameObject("ImpactFrameCanvas");
            canvasObj.transform.SetParent(transform, false);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 109;

            overlayGroup = canvasObj.AddComponent<CanvasGroup>();
            overlayGroup.alpha = 0f;
            overlayGroup.blocksRaycasts = false;
            overlayGroup.interactable = false;

            var flashObj = new GameObject("Flash", typeof(RectTransform));
            flashObj.transform.SetParent(canvasObj.transform, false);
            var rect = flashObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            overlayImage = flashObj.AddComponent<Image>();
            overlayImage.raycastTarget = false;
            overlayImage.color = overlayColor;
        }
    }
}
