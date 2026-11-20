using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KaplaoGames.UI.Animation
{
    /// <summary>
    /// Smooth border glow effect for input fields on hover and focus.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/UI/Input Focus Effect")]
    public class KaplaoUiInputFocusEffect : MonoBehaviour,
        ISelectHandler,
        IDeselectHandler,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        [SerializeField] Graphic borderGraphic;
        [SerializeField] Color normalColor = new Color(0x26 / 255f, 0x35 / 255f, 0x52 / 255f, 0.95f);
        [SerializeField] Color hoverColor = new Color(0x47 / 255f, 0x55 / 255f, 0x69 / 255f, 0.95f);
        [SerializeField] Color focusColor = new Color(0x38 / 255f, 0xBD / 255f, 0xF8 / 255f, 0.98f);
        [SerializeField] float duration = 0.14f;

        bool isFocused;
        bool isHovered;

        public void SetBorderGraphic(Graphic graphic)
        {
            borderGraphic = graphic;
        }

        void Awake()
        {
            if (borderGraphic == null)
            {
                Transform b = transform.Find("UI_Border");
                if (b != null) borderGraphic = b.GetComponent<Graphic>();
            }
        }

        void OnEnable()
        {
            isFocused = false;
            isHovered = false;
            if (borderGraphic != null)
                borderGraphic.color = normalColor;
        }

        void OnDisable()
        {
            if (borderGraphic != null)
            {
                LeanTween.cancel(borderGraphic.gameObject);
                borderGraphic.color = normalColor;
            }
        }

        public void OnSelect(BaseEventData eventData)
        {
            isFocused = true;
            ApplyTargetColor(focusColor);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            isFocused = false;
            ApplyTargetColor(isHovered ? hoverColor : normalColor);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            if (!isFocused)
                ApplyTargetColor(hoverColor);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            if (!isFocused)
                ApplyTargetColor(normalColor);
        }

        void ApplyTargetColor(Color target)
        {
            if (borderGraphic == null)
                return;

            if (gameObject.activeInHierarchy && Application.isPlaying)
            {
                LeanTween.cancel(borderGraphic.gameObject);
                Color start = borderGraphic.color;
                LeanTween.value(borderGraphic.gameObject, start, target, duration)
                    .setIgnoreTimeScale(true)
                    .setEase(LeanTweenType.easeOutQuad)
                    .setOnUpdate((Color c) =>
                    {
                        if (borderGraphic != null)
                            borderGraphic.color = c;
                    });
            }
            else
            {
                borderGraphic.color = target;
            }
        }
    }
}
