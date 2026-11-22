using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using KaplaoGames.UI.Theme;

namespace KaplaoGames.UI.Animation
{
    /// <summary>
    /// Tactile Click-Ripple effect for modern buttons.
    /// Spawns an expanding soft circular wave at the click position that fades out smoothly over 0.32s.
    /// Does not add extra Mask components to keep UI_Border and UI_Shadow intact.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/UI/Button Ripple")]
    public class KaplaoUiButtonRipple : MonoBehaviour, IPointerDownHandler
    {
        [SerializeField] float duration = 0.22f;
        [SerializeField] float startAlpha = 0.09f;
        [SerializeField] Color rippleColor = new Color(1f, 1f, 1f, 1f);

        RectTransform rectTransform;
        Selectable selectable;

        void Awake()
        {
            rectTransform = transform as RectTransform;
            selectable = GetComponent<Selectable>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (selectable != null && !selectable.interactable)
                return;

            if (rectTransform == null)
                rectTransform = transform as RectTransform;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint
            );

            SpawnRipple(localPoint);
        }

        void SpawnRipple(Vector2 localPos)
        {
            GameObject rippleObj = new GameObject("UI_Ripple", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(LayoutElement));
            rippleObj.transform.SetParent(transform, false);

            LayoutElement le = rippleObj.GetComponent<LayoutElement>();
            if (le != null)
                le.ignoreLayout = true;

            // Keep ripple under borders and labels
            Transform border = transform.Find("UI_Border");
            if (border != null)
                rippleObj.transform.SetSiblingIndex(border.GetSiblingIndex());
            else
                rippleObj.transform.SetAsLastSibling();

            RectTransform rt = rippleObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = localPos;

            float initialSize = 14f;
            rt.sizeDelta = new Vector2(initialSize, initialSize);

            Image img = rippleObj.GetComponent<Image>();
            img.sprite = KaplaoUiSprites.Circle();
            img.type = Image.Type.Simple;
            img.color = new Color(rippleColor.r, rippleColor.g, rippleColor.b, startAlpha);
            img.raycastTarget = false;

            CanvasGroup cg = rippleObj.GetComponent<CanvasGroup>();
            cg.alpha = 1f;
            cg.blocksRaycasts = false;
            cg.interactable = false;

            float w = rectTransform.rect.width > 0f ? rectTransform.rect.width : 200f;
            float h = rectTransform.rect.height > 0f ? rectTransform.rect.height : 50f;
            float targetSize = Mathf.Max(w, h) * 1.15f;

            LeanTween.value(rippleObj, initialSize, targetSize, duration)
                .setIgnoreTimeScale(true)
                .setEase(LeanTweenType.easeOutQuad)
                .setOnUpdate((float s) =>
                {
                    if (rt != null)
                        rt.sizeDelta = new Vector2(s, s);
                });

            LeanTween.value(rippleObj, 1f, 0f, duration)
                .setIgnoreTimeScale(true)
                .setEase(LeanTweenType.easeOutQuad)
                .setOnUpdate((float a) =>
                {
                    if (cg != null)
                        cg.alpha = a;
                })
                .setOnComplete(() =>
                {
                    if (rippleObj != null)
                        Destroy(rippleObj);
                });
        }
    }
}
