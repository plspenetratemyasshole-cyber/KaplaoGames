using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KaplaoGames.UI
{
    /// <summary>
    /// LeanTween hover / press scale & border glow for UI buttons. Add to any Button GameObject.
    /// All tweens call .setIgnoreTimeScale(true).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/UI/Button Lean Scale")]
    public class UiButtonLeanScale : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler
    {
        [SerializeField] RectTransform target;
        [SerializeField] Graphic borderGraphic;
        [SerializeField] float hoverScale = 1.05f;
        [SerializeField] float pressScale = 0.94f;
        [SerializeField] float tweenDuration = 0.12f;
        [SerializeField] LeanTweenType ease = LeanTweenType.easeOutQuad;
        [SerializeField] LeanTweenType releaseEase = LeanTweenType.easeOutBack;

        Selectable selectable;
        Vector3 baseScale = Vector3.one;
        Color normalBorderColor;
        bool hasBorder;
        bool pointerInside;
        bool pressed;
        int scaleTweenId = -1;
        int borderTweenId = -1;

        void Awake()
        {
            if (target == null)
                target = transform as RectTransform;

            if (target != null)
                baseScale = target.localScale;

            selectable = GetComponent<Selectable>();

            if (borderGraphic == null)
            {
                Transform b = transform.Find("UI_Border");
                if (b != null) borderGraphic = b.GetComponent<Graphic>();
            }

            if (borderGraphic != null)
            {
                normalBorderColor = borderGraphic.color;
                hasBorder = true;
            }
        }

        void OnEnable()
        {
            if (target != null)
                target.localScale = baseScale;

            if (hasBorder && borderGraphic != null)
                borderGraphic.color = normalBorderColor;

            pointerInside = false;
            pressed = false;
        }

        void OnDisable()
        {
            CancelScaleTween();
            CancelBorderTween();
            if (target != null)
                target.localScale = baseScale;
            if (hasBorder && borderGraphic != null)
                borderGraphic.color = normalBorderColor;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (selectable != null && !selectable.interactable)
                return;

            pointerInside = true;
            if (!pressed)
            {
                TweenToScale(hoverScale, ease);
                TweenBorder(1.3f);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
            if (!pressed)
            {
                TweenToScale(1f, ease);
                TweenBorder(1f);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (selectable != null && !selectable.interactable)
                return;

            pressed = true;
            TweenToScale(pressScale, ease);
            TweenBorder(1.6f);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressed = false;
            if (selectable != null && !selectable.interactable)
            {
                TweenToScale(1f, ease);
                TweenBorder(1f);
                return;
            }

            TweenToScale(pointerInside ? hoverScale : 1f, releaseEase);
            TweenBorder(pointerInside ? 1.3f : 1f);
        }

        void TweenToScale(float multiplier, LeanTweenType easeType)
        {
            if (target == null)
                return;

            CancelScaleTween();
            LTDescr tween = LeanTween.scale(target, baseScale * multiplier, tweenDuration)
                .setIgnoreTimeScale(true)
                .setEase(easeType);
            scaleTweenId = tween != null ? tween.uniqueId : -1;
        }

        void TweenBorder(float brightnessMultiplier)
        {
            if (!hasBorder || borderGraphic == null)
                return;

            CancelBorderTween();
            Color targetCol = new Color(
                Mathf.Clamp01(normalBorderColor.r * brightnessMultiplier),
                Mathf.Clamp01(normalBorderColor.g * brightnessMultiplier),
                Mathf.Clamp01(normalBorderColor.b * brightnessMultiplier),
                Mathf.Clamp01(normalBorderColor.a * (brightnessMultiplier > 1f ? 1.2f : 1f)));

            LTDescr tween = LeanTween.value(borderGraphic.gameObject, borderGraphic.color, targetCol, tweenDuration)
                .setIgnoreTimeScale(true)
                .setEase(LeanTweenType.easeOutQuad)
                .setOnUpdate((Color c) =>
                {
                    if (borderGraphic != null)
                        borderGraphic.color = c;
                });
            borderTweenId = tween != null ? tween.uniqueId : -1;
        }

        void CancelScaleTween()
        {
            if (scaleTweenId < 0)
                return;

            LeanTween.cancel(scaleTweenId);
            scaleTweenId = -1;
        }

        void CancelBorderTween()
        {
            if (borderTweenId < 0)
                return;

            LeanTween.cancel(borderTweenId);
            borderTweenId = -1;
        }
    }
}
