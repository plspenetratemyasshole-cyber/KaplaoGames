using UnityEngine;
using UnityEngine.EventSystems;

public class PanelClickOutside : MonoBehaviour, IPointerExitHandler
{
    private DocumentationPanel documentationPanel;
    private RectTransform panelRect;
    private Canvas parentCanvas;

    private void Start()
    {
        documentationPanel = GetComponentInParent<DocumentationPanel>();
        panelRect = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();
    }

    private void Update()
    {
        if (!gameObject.activeSelf || panelRect == null) return;

        if (Input.GetMouseButtonDown(0))
        {
            // If documentation panel told us to ignore the next outside click (button race), skip once.
            if (documentationPanel != null && documentationPanel.IgnoreNextOutsideClick)
            {
                documentationPanel.ClearIgnoreNextOutsideClick();
                return;
            }

            if (parentCanvas == null)
                parentCanvas = GetComponentInParent<Canvas>();

            Camera eventCamera = (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? parentCanvas.worldCamera
                : null;

            if (!RectTransformUtility.RectangleContainsScreenPoint(panelRect, Input.mousePosition, eventCamera))
            {
                documentationPanel?.ClosePanel();
            }
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
    }
}
