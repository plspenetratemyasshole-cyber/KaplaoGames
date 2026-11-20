using UnityEngine;
using UnityEngine.EventSystems;

public class PanelClickOutside : MonoBehaviour, IPointerExitHandler
{
    private DocumentationPanel documentationPanel;
    private RectTransform panelRect;

    private void Start()
    {
        documentationPanel = GetComponentInParent<DocumentationPanel>();
        panelRect = GetComponent<RectTransform>();
    }

    private void Update()
    {
        if (!gameObject.activeSelf) return;

        if (Input.GetMouseButtonDown(0))
        {
            // If documentation panel told us to ignore the next outside click (button race), skip once.
            if (documentationPanel != null && documentationPanel.IgnoreNextOutsideClick)
            {
                documentationPanel.ClearIgnoreNextOutsideClick();
                return;
            }

            if (!RectTransformUtility.RectangleContainsScreenPoint(panelRect, Input.mousePosition))
            {
                documentationPanel?.ClosePanel();
            }
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
    }
}
