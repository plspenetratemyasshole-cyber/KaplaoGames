using KaplaoGames.Client;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KaplaoGames.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/UI/Mobile Hold Button")]
    public class MobileHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public enum ActionType
        {
            Shift,
            Ctrl,
            RunnerLeft,
            RunnerRight,
            Tool1,
            Tool2,
            Tool3,
            Tool4,
            Tool5,
            Tool6,
            Tool7,
            Tool8
        }

        [SerializeField] ActionType actionType;
        [SerializeField] Color toggledColor = new Color(0.25f, 0.65f, 0.35f, 0.95f);

        Image targetImage;
        Color normalColor;
        bool hasNormalColor;

        void Awake()
        {
            targetImage = GetComponent<Image>();
            if (targetImage != null)
            {
                normalColor = targetImage.color;
                hasNormalColor = true;
            }
        }

        void OnEnable()
        {
            RefreshToggleVisual();
        }

        void OnDisable()
        {
            // Keep Shift/Ctrl state globally; only reset hold actions for this button.
            if (actionType == ActionType.RunnerLeft)
                MobileInputState.RunnerLeftHeld = false;
            else if (actionType == ActionType.RunnerRight)
                MobileInputState.RunnerRightHeld = false;
        }

        void LateUpdate()
        {
            if (actionType == ActionType.Shift || actionType == ActionType.Ctrl)
                RefreshToggleVisual();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            MobileInputState.RegisterUiPointer(eventData.pointerId);

            switch (actionType)
            {
                case ActionType.Shift:
                    MobileInputState.ShiftHeld = !MobileInputState.ShiftHeld;
                    RefreshToggleVisual();
                    break;
                case ActionType.Ctrl:
                    MobileInputState.CtrlHeld = !MobileInputState.CtrlHeld;
                    RefreshToggleVisual();
                    break;
                case ActionType.RunnerLeft:
                    MobileInputState.RunnerLeftHeld = true;
                    break;
                case ActionType.RunnerRight:
                    MobileInputState.RunnerRightHeld = true;
                    break;
                case ActionType.Tool1:
                case ActionType.Tool2:
                case ActionType.Tool3:
                case ActionType.Tool4:
                case ActionType.Tool5:
                case ActionType.Tool6:
                case ActionType.Tool7:
                case ActionType.Tool8:
                {
                    int slot = (int)actionType - (int)ActionType.Tool1 + 1;
                    MobileInputState.SelectToolSlot(slot);
                    KaplaoCursorClient.EnsureClientReadyForCommands();
                    KaplaoCursorClient client = KaplaoLocalBindingUtility.ResolveLocalCursorClient();
                    if (client != null)
                        client.MobileSelectToolSlot(slot);
                    break;
                }
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            MobileInputState.UnregisterUiPointer(eventData.pointerId);

            switch (actionType)
            {
                case ActionType.Shift:
                case ActionType.Ctrl:
                    // Toggle mode: do not clear on release.
                    break;
                case ActionType.RunnerLeft:
                    MobileInputState.RunnerLeftHeld = false;
                    break;
                case ActionType.RunnerRight:
                    MobileInputState.RunnerRightHeld = false;
                    break;
            }
        }

        void RefreshToggleVisual()
        {
            if (!hasNormalColor || targetImage == null)
                return;

            bool on = actionType == ActionType.Shift
                ? MobileInputState.ShiftHeld
                : actionType == ActionType.Ctrl && MobileInputState.CtrlHeld;

            if (actionType != ActionType.Shift && actionType != ActionType.Ctrl)
                return;

            targetImage.color = on ? toggledColor : normalColor;
        }
    }
}
