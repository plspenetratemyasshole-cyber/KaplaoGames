using UnityEngine;
using UnityEngine.EventSystems;

namespace KaplaoGames.UI
{
    /// <summary>
    /// LeanTween hover / press scale for UI buttons. Add to any Button GameObject.
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
        [SerializeField] float hoverScale = 1.08f;
        [SerializeField] float pressScale = 0.94f;
        [SerializeField] float tweenDuration = 0.12f;
        [SerializeField] LeanTweenType ease = LeanTweenType.easeOutQuad;

        Vector3 baseScale = Vector3.one;
        bool pointerInside;
        bool pressed;
        int scaleTweenId = -1;

        void Awake()
        {
            if (target == null)
                target = transform as RectTransform;

            if (target != null)
                baseScale = target.localScale;
        }

        void OnEnable()
        {
            if (target != null)
                target.localScale = baseScale;

            pointerInside = false;
            pressed = false;
        }

        void OnDisable()
        {
            if (target != null)
            {
                CancelScaleTween();
                target.localScale = baseScale;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerInside = true;
            if (!pressed)
                TweenToScale(hoverScale);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
            if (!pressed)
                TweenToScale(1f);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pressed = true;
            TweenToScale(pressScale);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressed = false;
            TweenToScale(pointerInside ? hoverScale : 1f);
        }

        void TweenToScale(float multiplier)
        {
            if (target == null)
                return;

            CancelScaleTween();
            LTDescr tween = LeanTween.scale(target, baseScale * multiplier, tweenDuration).setEase(ease);
            scaleTweenId = tween != null ? tween.uniqueId : -1;
        }

        void CancelScaleTween()
        {
            if (scaleTweenId < 0)
                return;

            LeanTween.cancel(scaleTweenId);
            scaleTweenId = -1;
        }
    }
}
