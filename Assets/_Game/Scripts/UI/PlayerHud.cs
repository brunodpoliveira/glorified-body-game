using UnityEngine;
using UnityEngine.UI;
using GlorifiedBody.Core;
using GlorifiedBody.Player;

namespace GlorifiedBody.UI
{
    /// <summary>
    /// The greybox HUD: the three bars (HP, NALCON, SHORT), the selected power and the drug counts, the
    /// charge circle over the target, and the SHORT warning. Built in code like the menus. The final art
    /// comes with the look test.
    /// </summary>
    public sealed class PlayerHud : MonoBehaviour
    {
        private Canvas canvas;
        private RectTransform canvasRect;
        private Image hpFill;
        private Image nalconFill;
        private Image shortFill;
        private Text powerText;
        private Text drugText;
        private Text shortText;
        private Image chargeRing;
        private RectTransform ringRect;
        private Sprite white;
        private Sprite ringSprite;

        private Transform player;
        private Health health;
        private PsychicStats stats;
        private PsychicPowers powers;
        private DrugInventory drugs;
        private ShortController shortController;
        private LockOnController lockOn;

        public Canvas Canvas => canvas;

        private void Start()
        {
            white = MakeWhiteSprite();
            ringSprite = MakeRingSprite();
            BuildUi();
        }

        private void BuildUi()
        {
            var canvasObj = new GameObject("PlayerHudCanvas");
            canvasObj.transform.SetParent(transform, false);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasRect = canvasObj.GetComponent<RectTransform>();

            // Bars, bottom-left.
            var bars = Element("Bars", canvasObj.transform);
            var barsRect = bars.GetComponent<RectTransform>();
            barsRect.anchorMin = new Vector2(0f, 0f);
            barsRect.anchorMax = new Vector2(0f, 0f);
            barsRect.pivot = new Vector2(0f, 0f);
            barsRect.anchoredPosition = new Vector2(32f, 32f);
            barsRect.sizeDelta = new Vector2(420f, 110f);
            hpFill = Bar(bars.transform, "HP", 0, new Color(0.85f, 0.15f, 0.15f));
            nalconFill = Bar(bars.transform, "NALCON", 1, new Color(0.25f, 0.5f, 1f));
            shortFill = Bar(bars.transform, "SHORT", 2, new Color(1f, 0.85f, 0.2f));

            // Power and drugs, bottom-right.
            powerText = Label(canvasObj.transform, "Power", 24, TextAnchor.LowerRight, new Vector2(1f, 0f), new Vector2(-32f, 64f), new Vector2(700f, 36f));
            drugText = Label(canvasObj.transform, "Drugs", 20, TextAnchor.LowerRight, new Vector2(1f, 0f), new Vector2(-32f, 32f), new Vector2(700f, 30f));

            // The SHORT warning, top-centre.
            shortText = Label(canvasObj.transform, "Short", 48, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(800f, 60f));
            shortText.color = new Color(1f, 0.3f, 0.2f);
            shortText.fontStyle = FontStyle.Bold;
            shortText.gameObject.SetActive(false);

            // The charge circle, over the target.
            var ringObj = Element("ChargeRing", canvasObj.transform);
            ringRect = ringObj.GetComponent<RectTransform>();
            ringRect.anchorMin = new Vector2(0.5f, 0.5f);
            ringRect.anchorMax = new Vector2(0.5f, 0.5f);
            ringRect.sizeDelta = new Vector2(180f, 180f);
            chargeRing = ringObj.AddComponent<Image>();
            chargeRing.sprite = ringSprite;
            chargeRing.type = Image.Type.Filled;
            chargeRing.fillMethod = Image.FillMethod.Radial360;
            chargeRing.fillOrigin = (int)Image.Origin360.Top;
            chargeRing.fillClockwise = true;
            chargeRing.raycastTarget = false;
            ringObj.SetActive(false);
        }

        private Image Bar(Transform parent, string label, int row, Color color)
        {
            float y = -row * 36f;
            Text text = Label(parent, label, 18, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(90f, 24f));
            text.fontStyle = FontStyle.Bold;

            var bg = Element(label + "Bg", parent);
            var bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 1f);
            bgRect.anchorMax = new Vector2(0f, 1f);
            bgRect.pivot = new Vector2(0f, 1f);
            bgRect.anchoredPosition = new Vector2(96f, y);
            bgRect.sizeDelta = new Vector2(320f, 22f);
            var bgImage = bg.AddComponent<Image>();
            bgImage.sprite = white;
            bgImage.color = new Color(0.08f, 0.08f, 0.1f, 0.85f);
            bgImage.raycastTarget = false;

            var fill = Element(label + "Fill", bg.transform);
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);
            var fillImage = fill.AddComponent<Image>();
            fillImage.sprite = white;
            fillImage.color = color;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.raycastTarget = false;
            return fillImage;
        }

        private Text Label(Transform parent, string name, int size, TextAnchor anchor, Vector2 anchorPoint, Vector2 position, Vector2 sizeDelta)
        {
            var obj = Element(name, parent);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchorPoint;
            rect.anchorMax = anchorPoint;
            rect.pivot = anchorPoint;
            rect.anchoredPosition = position;
            rect.sizeDelta = sizeDelta;
            var text = obj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = anchor;
            text.color = new Color(0.92f, 0.92f, 0.88f);
            text.supportRichText = true;
            text.raycastTarget = false;
            text.text = name;
            return text;
        }

        private static GameObject Element(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private static Sprite MakeWhiteSprite()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color32[16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        }

        private static Sprite MakeRingSprite()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    bool ring = r >= 46f && r <= 60f;
                    px[y * size + x] = ring ? new Color32(255, 255, 255, 230) : new Color32(0, 0, 0, 0);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
        }

        private bool Bind()
        {
            if (player != null) return true;
            GameObject playerObject = GameObject.FindWithTag("Player");
            if (playerObject == null) return false;
            player = playerObject.transform;
            health = playerObject.GetComponentInChildren<Health>();
            stats = playerObject.GetComponent<PsychicStats>();
            powers = playerObject.GetComponent<PsychicPowers>();
            drugs = playerObject.GetComponent<DrugInventory>();
            shortController = playerObject.GetComponent<ShortController>();
            lockOn = playerObject.GetComponent<LockOnController>();
            return true;
        }

        private void Update()
        {
            if (canvas == null || !Bind()) return;

            if (health != null) hpFill.fillAmount = health.MaxHealth > 0f ? health.CurrentHealth / health.MaxHealth : 0f;
            if (stats != null)
            {
                nalconFill.fillAmount = stats.Nalcon / PsychicStats.MaxNalcon;
                shortFill.fillAmount = stats.Short / PsychicStats.MaxShort;
                shortFill.color = stats.IsShortFull ? new Color(1f, 0.3f, 0.2f) : new Color(1f, 0.85f, 0.2f);
            }

            if (powers != null && powers.Powers != null)
            {
                string line = "";
                for (int i = 0; i < powers.Powers.Count; i++)
                {
                    PowerDefinition p = powers.Powers[i];
                    bool selected = i == powers.SelectedIndex;
                    line += (selected ? "<color=#ffd24d>" : "<color=#9a9a94>") + (i + 1) + " " + p.displayName + "</color>   ";
                }
                powerText.text = line.TrimEnd();
            }

            if (drugs != null)
            {
                drugText.text = $"NALCON {drugs.Count(DrugType.Nalcon)}   RED {drugs.Count(DrugType.Red)}   D-FELON {drugs.Count(DrugType.DFelon)}";
            }

            bool shortActive = shortController != null && shortController.IsActive;
            if (shortText.gameObject.activeSelf != shortActive) shortText.gameObject.SetActive(shortActive);
            if (shortActive)
            {
                bool blink = Mathf.FloorToInt(Time.unscaledTime * 6f) % 2 == 0;
                shortText.text = blink ? $"SHORT   {shortController.Remaining:0.0}" : "";
            }

            bool charging = powers != null && powers.Charge > 0f;
            if (ringRect.gameObject.activeSelf != charging) ringRect.gameObject.SetActive(charging);
            if (charging)
            {
                chargeRing.fillAmount = powers.Charge;
                chargeRing.color = Color.Lerp(Color.white, new Color(1f, 0.25f, 0.2f), powers.Charge);
                Vector3 world = lockOn != null && lockOn.CurrentTarget != null ? lockOn.CurrentTarget.GetDisplayPoint() : player.position + Vector3.up * 1.2f;
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Vector3 screen = cam.WorldToScreenPoint(world);
                    if (screen.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
                    {
                        ringRect.anchoredPosition = local;
                    }
                }
            }
        }
    }
}
