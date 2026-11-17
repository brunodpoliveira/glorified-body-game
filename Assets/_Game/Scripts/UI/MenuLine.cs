using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GlorifiedBody.UI
{
    /// <summary>
    /// A menu choice drawn as a plain line of words, with no box (T46, 2026-11-17: the death screen keeps its Arkham
    /// style, Bruno: "keep the arkham style, it fits better ... change it to the correct font"). The selected line is
    /// orange with a thin electric-blue rule under it, as the labels of the main menu. The others are silver.
    /// The pointer over a line selects it.
    /// </summary>
    public sealed class MenuLine : MonoBehaviour, IPointerEnterHandler
    {
        public Text Label;
        public Image Rule;

        public bool Selected => EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;

        private void Update()
        {
            bool on = Selected;
            if (Label != null) Label.color = on ? MenuStyle.Orange : MenuStyle.Silver;
            if (Rule != null) Rule.enabled = on;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
        }

        /// <summary>Turns a button of a menu panel into a line: no box, the words in Roboto Condensed, a rule under them.</summary>
        public static MenuLine Make(Button button, int fontSize = 44)
        {
            if (button.targetGraphic != null) button.targetGraphic.color = Color.clear; // it still takes the clicks
            button.transition = Selectable.Transition.None;
            Text text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = text.text.ToUpperInvariant();
                text.font = MenuStyle.Head;
                text.fontSize = fontSize;
                text.fontStyle = FontStyle.Normal;
                text.alignment = TextAnchor.MiddleLeft;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                Outline outline = text.GetComponent<Outline>();
                if (outline == null) outline = text.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            }
            var ruleObj = new GameObject("Rule", typeof(RectTransform));
            ruleObj.transform.SetParent(button.transform, false);
            var rect = (RectTransform)ruleObj.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(0f, 2f);
            rect.sizeDelta = new Vector2(140f, 3f);
            Image rule = ruleObj.AddComponent<Image>();
            rule.color = MenuStyle.Blue;
            rule.raycastTarget = false;
            MenuLine line = button.gameObject.AddComponent<MenuLine>();
            line.Label = text;
            line.Rule = rule;
            line.Update();
            return line;
        }
    }
}
