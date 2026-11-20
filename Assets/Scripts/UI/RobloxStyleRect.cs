using UnityEngine;

namespace KaplaoGames.UI
{
    /// <summary>
    /// Pins a UI element to the top-left with either fixed (offset) or normalized (scale) size/position.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class RobloxStyleRect : MonoBehaviour
    {
        public enum LayoutMode
        {
            OffsetPixels = 0,
            Normalized = 1
        }

        [SerializeField] LayoutMode layoutMode = LayoutMode.OffsetPixels;

        [Header("Anchor point (pivot)")]
        [Tooltip("0..1 pivot inside the rect. Roblox AnchorPoint equivalent. (0,1)=top-left, (0.5,0.5)=center.")]
        [SerializeField] Vector2 anchorPoint = new Vector2(0f, 1f);

        [Header("Offset (pixels in canvas units)")]
        [SerializeField] Vector2 offsetFromTopLeft = new Vector2(16f, -16f);
        [SerializeField] Vector2 size = new Vector2(120f, 40f);

        [Header("Normalized (0..1 of parent rect)")]
        [Tooltip("0..1 from top-left. (0,0)=top-left, (1,1)=bottom-right.")]
        [SerializeField] Vector2 normalizedPosition = new Vector2(0.01f, 0.01f);

        [Tooltip("0..1 size relative to parent rect.")]
        [SerializeField] Vector2 normalizedSize = new Vector2(0.08f, 0.05f);

        Vector2 lastParentSize;

        void OnValidate()
        {
            Apply();
        }

        void Awake()
        {
            Apply();
        }

        void Update()
        {
            if (transform is not RectTransform rect)
                return;

            RectTransform parent = rect.parent as RectTransform;
            if (parent == null)
                return;

            Vector2 parentSize = parent.rect.size;
            if (parentSize != lastParentSize)
            {
                lastParentSize = parentSize;
                Apply();
            }
        }

        public void Apply()
        {
            if (transform is not RectTransform rect)
                return;

            RectTransform parent = rect.parent as RectTransform;
            Vector2 parentSize = parent != null ? parent.rect.size : new Vector2(Screen.width, Screen.height);
            lastParentSize = parentSize;

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(Mathf.Clamp01(anchorPoint.x), Mathf.Clamp01(anchorPoint.y));

            if (layoutMode == LayoutMode.Normalized)
            {
                Vector2 np = new Vector2(Mathf.Clamp01(normalizedPosition.x), Mathf.Clamp01(normalizedPosition.y));
                Vector2 ns = new Vector2(Mathf.Clamp01(normalizedSize.x), Mathf.Clamp01(normalizedSize.y));

                rect.anchoredPosition = new Vector2(parentSize.x * np.x, -parentSize.y * np.y);
                rect.sizeDelta = new Vector2(parentSize.x * ns.x, parentSize.y * ns.y);
            }
            else
            {
                Vector2 ns = new Vector2(Mathf.Clamp01(normalizedSize.x), Mathf.Clamp01(normalizedSize.y));
                Vector2 resolvedSize = size;

                if (Mathf.Approximately(resolvedSize.x, 0f))
                    resolvedSize.x = parentSize.x * ns.x;

                if (Mathf.Approximately(resolvedSize.y, 0f))
                    resolvedSize.y = parentSize.y * ns.y;

                rect.anchoredPosition = offsetFromTopLeft;
                rect.sizeDelta = resolvedSize;
            }

            if (Application.isPlaying && UiFlyInUtility.IsTracked(rect))
                UiFlyInUtility.SyncRestFromCurrentPosition(rect);
        }

        public static void ApplyAllUnder(Transform root)
        {
            if (root == null)
                return;

            RobloxStyleRect[] rects = root.GetComponentsInChildren<RobloxStyleRect>(true);
            for (int i = 0; i < rects.Length; i++)
            {
                if (rects[i] != null)
                    rects[i].Apply();
            }
        }
    }
}
