using KaplaoGames.Client;
using KaplaoGames.Server;
using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KaplaoGames.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/UI/Mobile Tap Button")]
    public class MobileTapButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public enum ActionType
        {
            SelectMoveUp,
            SelectMoveDown,
            SelectMoveLeft,
            SelectMoveRight,
            SelectDuplicate,
            RunnerSuicide
        }

        [SerializeField] ActionType actionType;

        public void OnPointerDown(PointerEventData eventData)
        {
            MobileInputState.RegisterUiPointer(eventData.pointerId);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            MobileInputState.UnregisterUiPointer(eventData.pointerId);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (actionType == ActionType.RunnerSuicide)
            {
                RequestRunnerSuicide();
                return;
            }

            KaplaoCursorClient.EnsureClientReadyForCommands();
            KaplaoCursorClient client = KaplaoLocalBindingUtility.ResolveLocalCursorClient();
            if (client == null)
                return;

            switch (actionType)
            {
                case ActionType.SelectMoveUp:
                    client.MobileMoveSelectedTiles(0, 1);
                    break;
                case ActionType.SelectMoveDown:
                    client.MobileMoveSelectedTiles(0, -1);
                    break;
                case ActionType.SelectMoveLeft:
                    client.MobileMoveSelectedTiles(-1, 0);
                    break;
                case ActionType.SelectMoveRight:
                    client.MobileMoveSelectedTiles(1, 0);
                    break;
                case ActionType.SelectDuplicate:
                    client.MobileDuplicateSelectedTiles();
                    break;
            }
        }

        static void RequestRunnerSuicide()
        {
            if (NetworkClient.localPlayer == null)
                return;

            if (!NetworkClient.localPlayer.TryGetComponent(out RunnerMovementServer runner))
                return;

            runner.RequestSuicide();
        }
    }
}
