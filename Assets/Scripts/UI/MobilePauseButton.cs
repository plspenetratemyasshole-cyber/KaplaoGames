using KaplaoGames.Network;
using KaplaoGames.Shared;
using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KaplaoGames.UI
{
    /// <summary>
    /// Mobile "Pause" button.
    /// Calls the active pause menu (Kaplao or Runner) depending on local role.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/UI/Mobile Pause Button")]
    public class MobilePauseButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        [SerializeField] bool findUiIfMissing = true;
        [SerializeField] PauseMenuUI pauseMenuUi;
        [SerializeField] RunnerPauseMenuUI runnerPauseMenuUi;

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
            if (findUiIfMissing)
            {
                if (pauseMenuUi == null)
                    pauseMenuUi = FindFirstObjectByType<PauseMenuUI>();

                if (runnerPauseMenuUi == null)
                    runnerPauseMenuUi = FindFirstObjectByType<RunnerPauseMenuUI>();
            }

            PlayerRole? role = LocalPlayerRole.GetOrNull();
            bool isHost = NetworkServer.active && NetworkClient.isConnected;
            bool isProtected = NetworkPlayer.IsLocalAdmin();

            // Host / protected account always opens host pause menu, even as Runner.
            if (isHost || isProtected || role != PlayerRole.Runner)
            {
                pauseMenuUi?.TogglePauseFromMobile();
                return;
            }

            runnerPauseMenuUi?.TogglePauseFromMobile();
        }

        static new T FindFirstObjectByType<T>() where T : class
        {
            var result = UnityEngine.Object.FindFirstObjectByType(typeof(T)) as T;
            return result;
        }
    }
}
