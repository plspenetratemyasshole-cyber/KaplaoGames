using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Floating context menu near the cursor for the Code IDE file tree.
    /// Outside-click uses each button's hitbox (not the panel rect).
    /// </summary>
    [DisallowMultipleComponent]
    public class KaplaoCodeContextMenuUi : MonoBehaviour
    {
        [SerializeField] RectTransform root;
        [SerializeField] RectTransform buttonContainer;
        [SerializeField] float buttonWidth = 180f;
        [SerializeField] float buttonHeight = 28f;

        readonly List<RectTransform> buttonRects = new List<RectTransform>();
        readonly List<GameObject> spawnedButtons = new List<GameObject>();
        Canvas rootCanvas;
        bool suppressOutsideClose;
        Coroutine pendingHideRoutine;

        public bool IsOpen => root != null && root.gameObject.activeSelf;

        public void Bind(RectTransform menuRoot, RectTransform container)
        {
            root = menuRoot;
            buttonContainer = container;
            if (root != null)
                root.gameObject.SetActive(false);
        }

        public void Hide()
        {
            suppressOutsideClose = false;
            if (pendingHideRoutine != null)
            {
                StopCoroutine(pendingHideRoutine);
                pendingHideRoutine = null;
            }

            if (root != null)
                root.gameObject.SetActive(false);
            ClearButtons();
        }

        public void Show(Vector2 screenPosition, IReadOnlyList<(string label, Action action)> items)
        {
            if (root == null || buttonContainer == null || items == null || items.Count == 0)
                return;

            ClearButtons();
            rootCanvas = root.GetComponentInParent<Canvas>();
            suppressOutsideClose = true;

            for (int i = 0; i < items.Count; i++)
            {
                (string label, Action action) item = items[i];
                CreateButton(item.label, item.action);
            }

            float height = items.Count * buttonHeight;
            root.sizeDelta = new Vector2(buttonWidth, height);
            buttonContainer.sizeDelta = root.sizeDelta;

            PositionNearScreenPoint(screenPosition);
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();

            // Ignore the same RMB/LMB that opened the menu.
            if (pendingHideRoutine != null)
                StopCoroutine(pendingHideRoutine);
            pendingHideRoutine = StartCoroutine(EnableOutsideCloseNextFrame());
        }

        IEnumerator EnableOutsideCloseNextFrame()
        {
            yield return null;
            suppressOutsideClose = false;
            pendingHideRoutine = null;
        }

        void LateUpdate()
        {
            if (!IsOpen)
                return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Hide();
                return;
            }

            if (suppressOutsideClose)
                return;

            if (!Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1) && !Input.GetMouseButtonDown(2))
                return;

            // Close only when the click misses every button hitbox.
            if (!IsPointerOverAnyButton(Input.mousePosition))
                Hide();
        }

        bool IsPointerOverAnyButton(Vector2 screenPosition)
        {
            Camera eventCamera = GetEventCamera();
            for (int i = 0; i < buttonRects.Count; i++)
            {
                RectTransform buttonRect = buttonRects[i];
                if (buttonRect == null)
                    continue;

                if (RectTransformUtility.RectangleContainsScreenPoint(buttonRect, screenPosition, eventCamera))
                    return true;
            }

            return false;
        }

        void PositionNearScreenPoint(Vector2 screenPosition)
        {
            Camera eventCamera = GetEventCamera();
            RectTransform parent = root.parent as RectTransform;
            if (parent == null)
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent, screenPosition, eventCamera, out Vector2 localPoint))
                return;

            Vector2 size = root.sizeDelta;
            Vector2 pivotOffset = new Vector2(8f, -8f);
            Vector2 anchored = localPoint + pivotOffset;

            Vector2 parentSize = parent.rect.size;
            Vector2 parentPivot = parent.pivot;
            float minX = -parentSize.x * parentPivot.x;
            float maxX = parentSize.x * (1f - parentPivot.x) - size.x;
            float minY = -parentSize.y * parentPivot.y + size.y;
            float maxY = parentSize.y * (1f - parentPivot.y);

            anchored.x = Mathf.Clamp(anchored.x, minX + 4f, maxX - 4f);
            anchored.y = Mathf.Clamp(anchored.y, minY + 4f, maxY - 4f);

            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = anchored;
        }

        Camera GetEventCamera()
        {
            if (rootCanvas == null)
                rootCanvas = root != null ? root.GetComponentInParent<Canvas>() : null;
            if (rootCanvas == null)
                return null;
            return rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        }

        void CreateButton(string label, Action action)
        {
            GameObject buttonObject = new GameObject("Ctx_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(buttonContainer, false);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, buttonHeight);
            rect.anchoredPosition = new Vector2(0f, -spawnedButtons.Count * buttonHeight);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.14f, 0.15f, 0.18f, 0.98f);
            image.raycastTarget = true;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;

            // Capture action; run it first, then close so BeginNamePrompt etc. always fire.
            Action captured = action;
            button.onClick.AddListener(() =>
            {
                suppressOutsideClose = true;
                captured?.Invoke();
                Hide();
            });

            // Also handle pointer down so a LateUpdate outside-close in the same frame cannot win.
            var guard = buttonObject.AddComponent<ContextMenuButtonGuard>();
            guard.Menu = this;

            GameObject textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(buttonObject.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 0f);
            textRect.offsetMax = new Vector2(-8f, 0f);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 15f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;

            spawnedButtons.Add(buttonObject);
            buttonRects.Add(rect);
        }

        public void NotifyButtonPointerDown()
        {
            suppressOutsideClose = true;
        }

        void ClearButtons()
        {
            for (int i = 0; i < spawnedButtons.Count; i++)
            {
                if (spawnedButtons[i] != null)
                    Destroy(spawnedButtons[i]);
            }

            spawnedButtons.Clear();
            buttonRects.Clear();
        }
    }

    /// <summary>
    /// Marks a context-menu button press so LateUpdate does not treat it as an outside click.
    /// </summary>
    public sealed class ContextMenuButtonGuard : MonoBehaviour, IPointerDownHandler
    {
        public KaplaoCodeContextMenuUi Menu;

        public void OnPointerDown(PointerEventData eventData)
        {
            Menu?.NotifyButtonPointerDown();
        }
    }
}
