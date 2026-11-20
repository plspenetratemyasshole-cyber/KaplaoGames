using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace KaplaoGames.Client
{
    public static class KaplaoUiPointer
    {
        static readonly List<RaycastResult> RaycastResults = new();

        public static bool IsOverUi()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                return false;

            if (eventSystem.currentSelectedGameObject != null &&
                HasInteractiveUiAncestor(eventSystem.currentSelectedGameObject))
                return true;

            Mouse mouse = Mouse.current;
            if (mouse == null)
                return false;

            return IsInteractiveUiAtScreen(mouse.position.ReadValue());
        }

        /// <summary>
        /// Broader than <see cref="IsInteractiveUiAtScreen"/> — also color picker drag surfaces
        /// so Create-mode camera pan does not steal SV/hue drags.
        /// </summary>
        public static bool IsBlockingCameraPanAtScreen(Vector2 screenPosition)
        {
            if (IsInteractiveUiAtScreen(screenPosition))
                return true;

            return IsNonInteractivePointerUiAtScreen(screenPosition);
        }

        /// <summary>
        /// Blocks camera zoom when the wheel should scroll UI (chat, banned list, etc.).
        /// </summary>
        public static bool IsBlockingCameraZoomAtScreen(Vector2 screenPosition)
        {
            if (IsInteractiveUiAtScreen(screenPosition))
                return true;

            if (IsOverScrollRectAtScreen(screenPosition))
                return true;

            return IsNonInteractivePointerUiAtScreen(screenPosition);
        }

        public static bool IsBlockingCameraZoom()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
                return IsOverUi();

            return IsBlockingCameraZoomAtScreen(mouse.position.ReadValue());
        }

        /// <summary>
        /// True only over buttons/toggles/inputs/scrollbars — not decorative HUD labels or panels.
        /// Create-mode world clicks must work while selection toolbars are visible.
        /// </summary>
        public static bool IsInteractiveUiAtScreen(Vector2 screenPosition)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                return false;

            var pointerData = new PointerEventData(eventSystem)
            {
                position = screenPosition
            };

            RaycastResults.Clear();
            eventSystem.RaycastAll(pointerData, RaycastResults);

            for (int i = 0; i < RaycastResults.Count; i++)
            {
                GameObject hit = RaycastResults[i].gameObject;
                if (hit == null)
                    continue;

                if (HasInteractiveUiAncestor(hit))
                    return true;
            }

            return false;
        }

        static bool IsNonInteractivePointerUiAtScreen(Vector2 screenPosition)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                return false;

            var pointerData = new PointerEventData(eventSystem)
            {
                position = screenPosition
            };

            RaycastResults.Clear();
            eventSystem.RaycastAll(pointerData, RaycastResults);

            for (int i = 0; i < RaycastResults.Count; i++)
            {
                GameObject hit = RaycastResults[i].gameObject;
                if (hit == null)
                    continue;

                if (IsColorPickerUi(hit))
                    return true;
            }

            return false;
        }

        static bool IsOverScrollRectAtScreen(Vector2 screenPosition)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                return false;

            var pointerData = new PointerEventData(eventSystem)
            {
                position = screenPosition
            };

            RaycastResults.Clear();
            eventSystem.RaycastAll(pointerData, RaycastResults);

            for (int i = 0; i < RaycastResults.Count; i++)
            {
                GameObject hit = RaycastResults[i].gameObject;
                if (hit == null)
                    continue;

                if (hit.GetComponentInParent<ScrollRect>() != null)
                    return true;
            }

            return false;
        }

        static bool IsColorPickerUi(GameObject hit)
        {
            Transform t = hit.transform;
            while (t != null)
            {
                if (t.GetComponent<KaplaoTileColorUi>() != null)
                    return true;

                if (t.name == "TileColorPanel" && t.GetComponentInParent<Canvas>() != null)
                    return true;

                t = t.parent;
            }

            return false;
        }

        static bool HasInteractiveUiAncestor(GameObject obj)
        {
            if (obj == null)
                return false;

            Selectable selectable = obj.GetComponentInParent<Selectable>();
            if (selectable != null && selectable.interactable)
                return true;

            TMP_InputField inputField = obj.GetComponentInParent<TMP_InputField>();
            return inputField != null && inputField.interactable;
        }
    }
}
