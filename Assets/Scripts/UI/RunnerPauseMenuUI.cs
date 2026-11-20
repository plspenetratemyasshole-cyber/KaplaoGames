using KaplaoGames.Client;
using KaplaoGames.Network;
using KaplaoGames.Shared;
using KaplaoGames.UI;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.UI
{
    /// <summary>
    /// Runner pause menu opened with Escape during gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/UI/Runner Pause Menu UI")]
    public class RunnerPauseMenuUI : MonoBehaviour
    {
        [SerializeField] GameObject pauseMenuRoot;
        [SerializeField] Button resumeButton;
        [SerializeField] Button exitButton;

        public GameObject PauseMenuRoot => pauseMenuRoot;

        NetworkPlayer subscribedPlayer;
        bool isOpen;

        void Awake()
        {
            SetPauseOpen(false);
        }

        void OnEnable()
        {
            WireButtons();
            StartCoroutine(WatchLocalPlayerRole());
        }

        void OnDisable()
        {
            if (isOpen)
                PauseCursorState.SetPauseOpen(false);

            UnsubscribePlayer();
            UnwireButtons();
        }

        void Update()
        {
            if (!CanUsePauseMenu())
                return;

            if (isOpen)
                ApplyCursorVisibility();

            if (Input.GetKeyDown(KeyCode.Escape))
                SetPauseOpen(!isOpen);
        }

        public void TogglePauseFromMobile()
        {
            if (!CanUsePauseMenu())
                return;

            SetPauseOpen(!isOpen);
        }

        void WireButtons()
        {
            if (resumeButton != null)
                resumeButton.onClick.AddListener(OnResumeClicked);

            if (exitButton != null)
                exitButton.onClick.AddListener(OnExitClicked);
        }

        void UnwireButtons()
        {
            if (resumeButton != null)
                resumeButton.onClick.RemoveListener(OnResumeClicked);

            if (exitButton != null)
                exitButton.onClick.RemoveListener(OnExitClicked);
        }

        System.Collections.IEnumerator WatchLocalPlayerRole()
        {
            uint lastLocalNetId = 0;
            while (true)
            {
                if (NetworkClient.localPlayer == null)
                {
                    if (subscribedPlayer != null)
                        UnsubscribePlayer();

                    SetPauseOpen(false);
                    lastLocalNetId = 0;
                    yield return null;
                    continue;
                }

                if (!NetworkClient.localPlayer.TryGetComponent(out NetworkPlayer networkPlayer))
                {
                    SetPauseOpen(false);
                    yield return null;
                    continue;
                }

                if (subscribedPlayer != networkPlayer || networkPlayer.netId != lastLocalNetId)
                {
                    lastLocalNetId = networkPlayer.netId;
                    SubscribePlayer(networkPlayer);
                }

                yield return null;
            }
        }

        void SubscribePlayer(NetworkPlayer networkPlayer)
        {
            UnsubscribePlayer();
            subscribedPlayer = networkPlayer;
            subscribedPlayer.RoleChanged += OnRoleChanged;
            OnRoleChanged(subscribedPlayer.role);
        }

        void UnsubscribePlayer()
        {
            if (subscribedPlayer == null)
                return;

            subscribedPlayer.RoleChanged -= OnRoleChanged;
            subscribedPlayer = null;
        }

        void OnRoleChanged(PlayerRole role)
        {
            // Host / protected account never uses runner pause.
            if (NetworkServer.active && NetworkClient.isConnected)
            {
                SetPauseOpen(false);
                return;
            }

            if (NetworkPlayer.IsLocalAdmin())
            {
                SetPauseOpen(false);
                return;
            }

            PlayerRole resolved = LocalPlayerRole.GetOrNull() ?? role;
            if (resolved != PlayerRole.Runner)
                SetPauseOpen(false);
        }

        bool CanUsePauseMenu()
        {
            if (pauseMenuRoot == null)
                return false;

            // Host always uses Kaplao/host pause menu only.
            if (NetworkServer.active && NetworkClient.isConnected)
                return false;

            // Protected account always uses host pause menu.
            if (NetworkPlayer.IsLocalAdmin())
                return false;

            return LocalPlayerRole.IsRunner();
        }

        void SetPauseOpen(bool open)
        {
            if (isOpen == open)
            {
                if (open)
                    ApplyCursorVisibility();
                return;
            }

            isOpen = open;
            if (pauseMenuRoot != null)
            {
                pauseMenuRoot.SetActive(open);
                if (open)
                    pauseMenuRoot.transform.SetAsLastSibling();
            }

            if (open)
            {
                transform.SetAsLastSibling();
                Transform canvas = pauseMenuRoot != null
                    ? pauseMenuRoot.transform.parent
                    : transform.parent;
                if (canvas != null)
                {
                    Transform moderation = canvas.Find("HostModerationPanel");
                    if (moderation != null)
                        moderation.SetAsLastSibling();
                }
            }

            PauseCursorState.SetPauseOpen(open);
            ApplyCursorVisibility();
        }

        void ApplyCursorVisibility()
        {
            if (!CanUsePauseMenu() || !isOpen)
                return;

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        void OnResumeClicked()
        {
            SetPauseOpen(false);
        }

        void OnExitClicked()
        {
            SetPauseOpen(false);

            KaplaoNetworkManager networkManager = KaplaoNetworkManager.Instance;
            if (networkManager == null)
                return;

            if (NetworkClient.isConnected)
                networkManager.StopClient();
        }
    }
}
