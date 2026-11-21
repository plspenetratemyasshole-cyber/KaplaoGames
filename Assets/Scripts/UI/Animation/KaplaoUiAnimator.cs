using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.UI.Animation
{
    public enum PanelAnimStyle
    {
        PopScale,
        SlideFromBottom,
        SlideFromTop,
        SlideFromLeft,
        SlideFromRight,
        Fade,
        FadeOnly = Fade
    }

    /// <summary>
    /// Central UI LeanTween animation engine.
    /// All tweens strictly use .setIgnoreTimeScale(true) and cancel previous tweens safely.
    /// </summary>
    public static class KaplaoUiAnimator
    {
        static readonly Dictionary<int, Vector3> RestPositions = new();
        static readonly Dictionary<int, int> ActiveHideTweenIds = new();

        public static void ShowPanel(GameObject panel, PanelAnimStyle style = PanelAnimStyle.PopScale, float duration = 0.24f)
        {
            if (panel == null)
                return;

            int id = panel.GetInstanceID();
            if (ActiveHideTweenIds.TryGetValue(id, out int hideTweenId))
            {
                if (hideTweenId >= 0)
                    LeanTween.cancel(hideTweenId);
                ActiveHideTweenIds.Remove(id);
            }

            LeanTween.cancel(panel);

            RectTransform rect = panel.GetComponent<RectTransform>();
            CanvasGroup group = panel.GetComponent<CanvasGroup>();
            if (group == null)
                group = panel.AddComponent<CanvasGroup>();

            panel.SetActive(true);

            if (rect != null && !RestPositions.ContainsKey(id))
                RestPositions[id] = rect.anchoredPosition3D;

            Vector3 restPos = RestPositions.TryGetValue(id, out Vector3 savedPos)
                ? savedPos
                : (rect != null ? rect.anchoredPosition3D : Vector3.zero);

            group.alpha = 0f;

            switch (style)
            {
                case PanelAnimStyle.PopScale:
                    if (rect != null)
                    {
                        rect.localScale = Vector3.one * 0.92f;
                        LeanTween.scale(rect, Vector3.one, duration)
                            .setIgnoreTimeScale(true)
                            .setEase(LeanTweenType.easeOutBack);
                    }
                    LeanTween.alphaCanvas(group, 1f, duration * 0.8f)
                        .setIgnoreTimeScale(true)
                        .setEase(LeanTweenType.easeOutQuad);
                    break;

                case PanelAnimStyle.SlideFromRight:
                    if (rect != null)
                    {
                        rect.localScale = Vector3.one;
                        rect.anchoredPosition3D = restPos + new Vector3(40f, 0f, 0f);
                        LeanTween.move(rect, restPos, duration)
                            .setIgnoreTimeScale(true)
                            .setEase(LeanTweenType.easeOutCubic);
                    }
                    LeanTween.alphaCanvas(group, 1f, duration * 0.8f)
                        .setIgnoreTimeScale(true)
                        .setEase(LeanTweenType.easeOutQuad);
                    break;

                case PanelAnimStyle.SlideFromLeft:
                    if (rect != null)
                    {
                        rect.localScale = Vector3.one;
                        rect.anchoredPosition3D = restPos + new Vector3(-40f, 0f, 0f);
                        LeanTween.move(rect, restPos, duration)
                            .setIgnoreTimeScale(true)
                            .setEase(LeanTweenType.easeOutCubic);
                    }
                    LeanTween.alphaCanvas(group, 1f, duration * 0.8f)
                        .setIgnoreTimeScale(true)
                        .setEase(LeanTweenType.easeOutQuad);
                    break;

                case PanelAnimStyle.SlideFromBottom:
                    if (rect != null)
                    {
                        rect.localScale = Vector3.one;
                        rect.anchoredPosition3D = restPos + new Vector3(0f, -28f, 0f);
                        LeanTween.move(rect, restPos, duration)
                            .setIgnoreTimeScale(true)
                            .setEase(LeanTweenType.easeOutCubic);
                    }
                    LeanTween.alphaCanvas(group, 1f, duration * 0.8f)
                        .setIgnoreTimeScale(true)
                        .setEase(LeanTweenType.easeOutQuad);
                    break;

                case PanelAnimStyle.SlideFromTop:
                    if (rect != null)
                    {
                        rect.localScale = Vector3.one;
                        rect.anchoredPosition3D = restPos + new Vector3(0f, 28f, 0f);
                        LeanTween.move(rect, restPos, duration)
                            .setIgnoreTimeScale(true)
                            .setEase(LeanTweenType.easeOutCubic);
                    }
                    LeanTween.alphaCanvas(group, 1f, duration * 0.8f)
                        .setIgnoreTimeScale(true)
                        .setEase(LeanTweenType.easeOutQuad);
                    break;

                case PanelAnimStyle.Fade:
                default:
                    if (rect != null)
                        rect.localScale = Vector3.one;
                    LeanTween.alphaCanvas(group, 1f, duration)
                        .setIgnoreTimeScale(true)
                        .setEase(LeanTweenType.easeOutQuad);
                    break;
            }
        }

        public static void HidePanel(GameObject panel, PanelAnimStyle style = PanelAnimStyle.PopScale, float duration = 0.16f, Action onComplete = null)
        {
            if (panel == null)
            {
                onComplete?.Invoke();
                return;
            }

            if (!panel.activeSelf)
            {
                onComplete?.Invoke();
                return;
            }

            int id = panel.GetInstanceID();
            LeanTween.cancel(panel);

            RectTransform rect = panel.GetComponent<RectTransform>();
            CanvasGroup group = panel.GetComponent<CanvasGroup>();
            if (group == null)
                group = panel.AddComponent<CanvasGroup>();

            if (rect != null && !RestPositions.ContainsKey(id))
                RestPositions[id] = rect.anchoredPosition3D;

            Vector3 restPos = RestPositions.TryGetValue(id, out Vector3 savedPos)
                ? savedPos
                : (rect != null ? rect.anchoredPosition3D : Vector3.zero);

            if (rect != null && style == PanelAnimStyle.PopScale)
            {
                LeanTween.scale(rect, Vector3.one * 0.95f, duration)
                    .setIgnoreTimeScale(true)
                    .setEase(LeanTweenType.easeInQuad);
            }

            LTDescr alphaTween = LeanTween.alphaCanvas(group, 0f, duration)
                .setIgnoreTimeScale(true)
                .setEase(LeanTweenType.easeInQuad)
                .setOnComplete(() =>
                {
                    ActiveHideTweenIds.Remove(id);
                    panel.SetActive(false);
                    group.alpha = 1f;
                    if (rect != null)
                    {
                        rect.localScale = Vector3.one;
                        rect.anchoredPosition3D = restPos;
                    }
                    onComplete?.Invoke();
                });

            if (alphaTween != null)
                ActiveHideTweenIds[id] = alphaTween.uniqueId;
        }

        public static void AnimateListStagger(Transform container, float stagger = 0.03f, float duration = 0.22f)
        {
            if (container == null)
                return;

            int animatedIndex = 0;
            for (int i = 0; i < container.childCount; i++)
            {
                Transform child = container.GetChild(i);
                if (child == null || !child.gameObject.activeSelf)
                    continue;

                string name = child.gameObject.name;
                if (name.IndexOf("Template", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                CanvasGroup group = child.GetComponent<CanvasGroup>();
                if (group == null)
                    group = child.gameObject.AddComponent<CanvasGroup>();

                group.alpha = 0f;
                child.localScale = Vector3.one * 0.94f;

                float delay = Mathf.Min(animatedIndex, 12) * stagger;
                animatedIndex++;

                LeanTween.cancel(child.gameObject);

                LeanTween.scale(child.gameObject, Vector3.one, duration)
                    .setDelay(delay)
                    .setIgnoreTimeScale(true)
                    .setEase(LeanTweenType.easeOutCubic);

                LeanTween.alphaCanvas(group, 1f, duration * 0.85f)
                    .setDelay(delay)
                    .setIgnoreTimeScale(true)
                    .setEase(LeanTweenType.easeOutQuad);
            }
        }

        public static void Pulse(RectTransform target, float scale = 1.08f, float duration = 0.2f)
        {
            if (target == null)
                return;

            LeanTween.cancel(target.gameObject);
            Vector3 baseScale = target.localScale;
            LeanTween.scale(target, baseScale * scale, duration * 0.4f)
                .setIgnoreTimeScale(true)
                .setEase(LeanTweenType.easeOutQuad)
                .setOnComplete(() =>
                {
                    if (target != null)
                    {
                        LeanTween.scale(target, baseScale, duration * 0.6f)
                            .setIgnoreTimeScale(true)
                            .setEase(LeanTweenType.easeOutBack);
                    }
                });
        }

        public static void ShakeError(GameObject target)
        {
            if (target != null && target.TryGetComponent(out RectTransform rt))
                ShakeError(rt);
        }

        public static void PopScale(GameObject target, float duration = 0.2f)
        {
            if (target == null) return;
            LeanTween.cancel(target);
            target.transform.localScale = Vector3.one * 0.85f;
            LeanTween.scale(target, Vector3.one, duration)
                .setIgnoreTimeScale(true)
                .setEase(LeanTweenType.easeOutBack);
        }

        public static void ShakeError(RectTransform target)
        {
            if (target == null)
                return;

            LeanTween.cancel(target.gameObject);
            Vector3 restPos = target.anchoredPosition3D;
            const float shakeAmount = 8f;
            const float duration = 0.05f;

            LeanTween.move(target, restPos + new Vector3(-shakeAmount, 0f, 0f), duration)
                .setIgnoreTimeScale(true)
                .setEase(LeanTweenType.easeOutQuad)
                .setOnComplete(() =>
                {
                    if (target == null) return;
                    LeanTween.move(target, restPos + new Vector3(shakeAmount, 0f, 0f), duration)
                        .setIgnoreTimeScale(true)
                        .setOnComplete(() =>
                        {
                            if (target == null) return;
                            LeanTween.move(target, restPos + new Vector3(-shakeAmount * 0.5f, 0f, 0f), duration)
                                .setIgnoreTimeScale(true)
                                .setOnComplete(() =>
                                {
                                    if (target == null) return;
                                    LeanTween.move(target, restPos, duration)
                                        .setIgnoreTimeScale(true);
                                });
                        });
                });
        }
    }
}
