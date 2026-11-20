using System.Collections.Generic;
using UnityEngine;

namespace KaplaoGames.UI
{
    public static class UiFlyInUtility
    {
        static readonly Dictionary<int, Vector3> RestPositions = new();
        static readonly Dictionary<int, int> MoveTweenIds = new();

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

            Vector3 targetPosition = rect.anchoredPosition3D;
            RestPositions[rect.GetInstanceID()] = targetPosition;
            rect.anchoredPosition3D = targetPosition + new Vector3(0f, -offset, 0f);
            LTDescr tween = LeanTween.move(rect, targetPosition, duration)
                .setEase(ease)
                .setDelay(delay);
            MoveTweenIds[rect.GetInstanceID()] = tween != null ? tween.uniqueId : -1;
        }

        public static void SnapToRest(RectTransform rect)
        {
            if (rect == null)
                return;

            CancelMoveTween(rect);

            if (RestPositions.TryGetValue(rect.GetInstanceID(), out Vector3 restPosition))
                rect.anchoredPosition3D = restPosition;
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
                RestPositions.Remove(rect.GetInstanceID());
                MoveTweenIds.Remove(rect.GetInstanceID());
            }
        }

        static void CancelMoveTween(RectTransform rect)
        {
            int id = rect.GetInstanceID();
            if (!MoveTweenIds.TryGetValue(id, out int tweenId))
                return;

            if (tweenId >= 0)
                LeanTween.cancel(tweenId);

            MoveTweenIds.Remove(id);
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
