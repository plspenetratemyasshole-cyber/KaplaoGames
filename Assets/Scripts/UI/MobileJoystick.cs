using UnityEngine;
using UnityEngine.EventSystems;

namespace KaplaoGames.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/UI/Mobile Joystick")]
    public class MobileJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] RectTransform area;
        [SerializeField] RectTransform handle;
        [SerializeField] float radius = 80f;

        Vector2 value;
        int activePointerId = -999;

        public Vector2 Value => value;
        public bool IsHeld => activePointerId != -999;

        public void OnPointerDown(PointerEventData eventData)
        {
            activePointerId = eventData.pointerId;
            MobileInputState.RegisterUiPointer(activePointerId);
            UpdateValue(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            UpdateValue(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            MobileInputState.UnregisterUiPointer(eventData.pointerId);
            if (eventData.pointerId == activePointerId)
                activePointerId = -999;

            value = Vector2.zero;
            UpdateHandle();
        }

        void UpdateValue(PointerEventData eventData)
        {
            if (area == null)
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    area,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 local))
            {
                return;
            }

            value = Vector2.ClampMagnitude(local / Mathf.Max(1f, radius), 1f);
            UpdateHandle();
        }

        void UpdateHandle()
        {
            if (handle == null)
                return;

            handle.anchoredPosition = value * radius;
        }
    }
}
