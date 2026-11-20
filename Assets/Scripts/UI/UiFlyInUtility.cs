using System.Collections.Generic;
using UnityEngine;

namespace KaplaoGames.UI
{
    public static class UiFlyInUtility
    {
        static readonly Dictionary<int, Vector3> RestPositions = new();
        static readonly Dictionary<int, int> MoveTweenIds = new();
        static readonly Dictionary<int, int> AlphaTweenIds = new();

        public static void Play(
            RectTransform rect,
            float flyOffsetY,
            float duration,
            float delay,
            LeanTweenType ease)
        {
            if (rect == null)
                return;

            CancelMoveTween(rect);

            float offset = flyOffsetY > 0f
                ? flyOffsetY
                : ResolveAutoFlyOffset(rect);

            int id = rect.GetInstanceID();
            Vector3 targetPosition;
            if (RestPositions.TryGetValue(id, out Vector3 saved))
            {
                targetPosition = saved;
            }
            else
            {
                targetPosition = rect.anchoredPosition3D;
                RestPositions[id] = targetPosition;
            }

            rect.anchoredPosition3D = targetPosition + new Vector3(0f, -offset, 0f);

            CanvasGroup group = rect.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 0f;
                LTDescr alphaTween = LeanTween.alphaCanvas(group, 1f, duration * 0.7f)
                    .setIgnoreTimeScale(true)
                    .setDelay(delay)
                    .setEase(LeanTweenType.easeOutQuad);
                AlphaTweenIds[id] = alphaTween != null ? alphaTween.uniqueId : -1;
            }

            LTDescr tween = LeanTween.move(rect, targetPosition, duration)
                .setIgnoreTimeScale(true)
                .setEase(ease)
                .setDelay(delay);
            MoveTweenIds[id] = tween != null ? tween.uniqueId : -1;
        }

        public static void SnapToRest(RectTransform rect)
        {
            if (rect == null)
                return;

            CancelMoveTween(rect);

            int id = rect.GetInstanceID();
            if (RestPositions.TryGetValue(id, out Vector3 restPosition))
                rect.anchoredPosition3D = restPosition;

            CanvasGroup group = rect.GetComponent<CanvasGroup>();
            if (group != null)
                group.alpha = 1f;
        }

        public static void SyncRestFromCurrentPosition(RectTransform rect)
        {
            if (rect == null)
                return;

            CancelMoveTween(rect);
            RestPositions[rect.GetInstanceID()] = rect.anchoredPosition3D;
        }

        public static bool IsTracked(RectTransform rect)
        {
            return rect != null && RestPositions.ContainsKey(rect.GetInstanceID());
        }

        public static void ClearRestPosition(RectTransform rect)
        {
            if (rect != null)
            {
                int id = rect.GetInstanceID();
                RestPositions.Remove(id);
                MoveTweenIds.Remove(id);
                AlphaTweenIds.Remove(id);
            }
        }

        static void CancelMoveTween(RectTransform rect)
        {
            int id = rect.GetInstanceID();
            if (MoveTweenIds.TryGetValue(id, out int tweenId))
            {
                if (tweenId >= 0)
                    LeanTween.cancel(tweenId);
                MoveTweenIds.Remove(id);
            }

            if (AlphaTweenIds.TryGetValue(id, out int alphaId))
            {
                if (alphaId >= 0)
                    LeanTween.cancel(alphaId);
                AlphaTweenIds.Remove(id);
            }
        }

        static float ResolveAutoFlyOffset(RectTransform rect)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.transform is RectTransform canvasRect)
                return canvasRect.rect.height + 200f;

            return 900f;
        }
    }
}
