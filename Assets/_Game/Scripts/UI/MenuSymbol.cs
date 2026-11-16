using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GlorifiedBody.UI
{
    /// <summary>
    /// One symbol of the main menu row (T46, round 173, after Ace Combat 3 Electrosphere). The selected symbol is orange
    /// and larger, with its label and an electric blue rule under it; the others are silver and show no label.
    /// The label Text stays active (alpha 0 when hidden), so the tests find the button by its label.
    /// The pointer selects the symbol it is over, so the mouse and the keys show the same state.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class MenuSymbol : MonoBehaviour, IPointerEnterHandler
    {
        public static readonly Color Orange = new Color32(255, 138, 30, 255);
        public static readonly Color Green = new Color32(57, 226, 122, 255);
        public static readonly Color Blue = new Color32(47, 168, 255, 255);
        public static readonly Color Silver = new Color32(184, 190, 198, 255);
        public static readonly Color OffWhite = new Color32(242, 239, 230, 255);

        public RawImage Icon;
        public Text Label;
        public Image Rule;
        public float SelectedSize = 128f;
        public float IdleSize = 104f;
        public float IdleAlpha = 0.67f;

        public bool IsSelected => EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;

        void OnEnable() => Apply(IsSelected);

        void Update() => Apply(IsSelected);

        private void Apply(bool selected)
        {
            bool usable = GetComponent<Button>().interactable;
            if (Icon != null)
            {
                Color c = selected ? Orange : Silver;
                c.a = selected ? 1f : (usable ? IdleAlpha : 0.25f);
                Icon.color = c;
                float s = selected ? SelectedSize : IdleSize;
                Icon.rectTransform.sizeDelta = new Vector2(s, s);
            }
            if (Label != null)
            {
                Color c = OffWhite;
                c.a = selected ? 1f : 0f;
                Label.color = c;
            }
            if (Rule != null) Rule.enabled = selected;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (EventSystem.current != null && GetComponent<Button>().interactable)
                EventSystem.current.SetSelectedGameObject(gameObject);
        }
    }
}
