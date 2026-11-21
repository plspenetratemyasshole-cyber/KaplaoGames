using System;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.UI.Animation
{
    /// <summary>
    /// Automatic panel animator (inspired by RTSokak's UIPanelAnimator).
    /// Plays smooth unscaled entry animation on OnEnable, supports stagger for child items,
    /// and safely respects LayoutGroups and RobloxStyleRect.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/UI/Panel Animator")]
    public class KaplaoPanelAnimator : MonoBehaviour
    {
        [Header("Animation")]
        [SerializeField] PanelAnimStyle style = PanelAnimStyle.PopScale;
        [SerializeField] float duration = 0.24f;
        [SerializeField] bool animateChildrenStagger;
        [SerializeField] float staggerDelay = 0.03f;

        RectTransform rectTransform;
        CanvasGroup canvasGroup;
        Vector2 baseAnchoredPosition;
        Vector3 baseScale = Vector3.one;
        bool hasBasePosition;
        int activeTweenId = -1;
        int alphaTweenId = -1;
        internal bool suppressNextOnEnable;

        public PanelAnimStyle Style
        {
            get => style;
            set => style = value;
        }

        public float Duration
        {
            get => duration;
            set => duration = value;
        }

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                baseAnchoredPosition = rectTransform.anchoredPosition;
                baseScale = rectTransform.localScale;
                hasBasePosition = true;
            }

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        void OnEnable()
        {
            if (suppressNextOnEnable)
            {
                suppressNextOnEnable = false;
                return;
            }

            if (rectTransform == null)
                rectTransform = GetComponent<RectTransform>();

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

            if (!hasBasePosition && rectTransform != null)
            {
                baseAnchoredPosition = rectTransform.anchoredPosition;
                baseScale = rectTransform.localScale;
                hasBasePosition = true;
            }

            PlayEntryAnimation();
        }

        void OnDisable()
        {
            CancelTweens();

            if (rectTransform != null && hasBasePosition)
            {
                if (style != PanelAnimStyle.PopScale && style != PanelAnimStyle.FadeOnly)
                    rectTransform.anchoredPosition = baseAnchoredPosition;
                rectTransform.localScale = baseScale;
            }

            if (canvasGroup != null)
                canvasGroup.alpha = 1f;
        }

        public void PlayEntryAnimation()
        {
            CancelTweens();

            if (rectTransform == null)
                return;

            // Check if position shifting is allowed (not driven by a layout group or RobloxStyleRect)
            bool allowPositionShift = true;
            if (transform.parent != null && transform.parent.GetComponent<LayoutGroup>() != null)
                allowPositionShift = false;
            if (GetComponent("RobloxStyleRect") != null)
                allowPositionShift = false;

            canvasGroup.alpha = 0f;
            alphaTweenId = LeanTween.alphaCanvas(canvasGroup, 1f, duration)
                .setIgnoreTimeScale(true)
                .setEaseOutQuad()
                .id;

            PanelAnimStyle effectiveStyle = allowPositionShift ? style : (style == PanelAnimStyle.FadeOnly ? PanelAnimStyle.FadeOnly : PanelAnimStyle.PopScale);

            switch (effectiveStyle)
            {
                case PanelAnimStyle.PopScale:
                    rectTransform.localScale = baseScale * 0.88f;
                    activeTweenId = LeanTween.scale(rectTransform, baseScale, duration)
                        .setIgnoreTimeScale(true)
                        .setEaseOutBack()
                        .id;
                    break;

                case PanelAnimStyle.SlideFromBottom:
                    rectTransform.localScale = baseScale;
                    rectTransform.anchoredPosition = baseAnchoredPosition + new Vector2(0f, -80f);
                    activeTweenId = LeanTween.value(gameObject, rectTransform.anchoredPosition, baseAnchoredPosition, duration)
                        .setIgnoreTimeScale(true)
                        .setEaseOutCubic()
                        .setOnUpdate((Vector2 pos) => rectTransform.anchoredPosition = pos)
                        .id;
                    break;

                case PanelAnimStyle.SlideFromTop:
                    rectTransform.localScale = baseScale;
                    rectTransform.anchoredPosition = baseAnchoredPosition + new Vector2(0f, 80f);
                    activeTweenId = LeanTween.value(gameObject, rectTransform.anchoredPosition, baseAnchoredPosition, duration)
                        .setIgnoreTimeScale(true)
                        .setEaseOutCubic()
                        .setOnUpdate((Vector2 pos) => rectTransform.anchoredPosition = pos)
                        .id;
                    break;

                case PanelAnimStyle.SlideFromLeft:
                    rectTransform.localScale = baseScale;
                    rectTransform.anchoredPosition = baseAnchoredPosition + new Vector2(-120f, 0f);
                    activeTweenId = LeanTween.value(gameObject, rectTransform.anchoredPosition, baseAnchoredPosition, duration)
                        .setIgnoreTimeScale(true)
                        .setEaseOutCubic()
                        .setOnUpdate((Vector2 pos) => rectTransform.anchoredPosition = pos)
                        .id;
                    break;

                case PanelAnimStyle.SlideFromRight:
                    rectTransform.localScale = baseScale;
                    rectTransform.anchoredPosition = baseAnchoredPosition + new Vector2(120f, 0f);
                    activeTweenId = LeanTween.value(gameObject, rectTransform.anchoredPosition, baseAnchoredPosition, duration)
                        .setIgnoreTimeScale(true)
                        .setEaseOutCubic()
                        .setOnUpdate((Vector2 pos) => rectTransform.anchoredPosition = pos)
                        .id;
                    break;

                case PanelAnimStyle.FadeOnly:
                default:
                    rectTransform.anchoredPosition = baseAnchoredPosition;
                    rectTransform.localScale = baseScale;
                    break;
            }

            if (animateChildrenStagger)
            {
                KaplaoUiAnimator.AnimateListStagger(transform, staggerDelay, duration);
            }
        }

        public void Hide(Action onComplete = null)
        {
            KaplaoUiAnimator.HidePanel(gameObject, style, Mathf.Min(0.18f, duration), onComplete);
        }

        void CancelTweens()
        {
            if (activeTweenId != -1)
            {
                LeanTween.cancel(activeTweenId);
                activeTweenId = -1;
            }

            if (alphaTweenId != -1)
            {
                LeanTween.cancel(alphaTweenId);
                alphaTweenId = -1;
            }
        }
    }
}
