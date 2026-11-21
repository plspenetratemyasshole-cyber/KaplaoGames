using KaplaoGames.Client;
using KaplaoGames.Network;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using KaplaoGames.UI.Animation;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.UI
{
    /// <summary>
    /// Kaplao pause menu opened with Escape during gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/UI/Pause Menu UI")]
    public class PauseMenuUI : MonoBehaviour
    {
        [SerializeField] GameObject pauseMenuRoot;
        [SerializeField] GameObject pauseMenuPanel;
        [SerializeField] GameObject saveSuccessPanel;
        [SerializeField] GameObject exitWithoutSavePanel;

        [SerializeField] Button resumeButton;
        [SerializeField] Button saveButton;
        [SerializeField] Button saveAndExitButton;
        [SerializeField] Button exitButton;
        [SerializeField] Button openToLanButton;
        [SerializeField] Button saveSuccessCloseButton;
        [SerializeField] Button confirmExitButton;
        [SerializeField] Button cancelExitButton;

        [Header("Header")]
        [SerializeField] TextMeshProUGUI pauseWorldNameLabel;
        [SerializeField] TextMeshProUGUI pauseStatusPillLabel;

        [SerializeField] bool onlyForKaplao = true;

        public GameObject PauseMenuRoot => pauseMenuRoot;

        NetworkPlayer subscribedPlayer;
        bool isOpen;
        bool lanAdvertised;
        GameObject externalOverlay;

        void Awake()
        {
            HideAllOverlays();
            SetPauseOpen(false);

            // Legacy panel — settings moved to Main Menu.
            Transform openToLan = transform.Find("PauseMenuRoot/OpenToLanPanel");
            if (openToLan == null && pauseMenuRoot != null)
                openToLan = pauseMenuRoot.transform.Find("OpenToLanPanel");
            if (openToLan != null)
                openToLan.gameObject.SetActive(false);
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
            if (DeveloperChatManager.IsAnyChatOpen)
                return;

            if (!CanUsePauseMenu())
                return;

            if (IsPauseMenuBlockingCursor())
                ApplyCursorVisibility();

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (IsAnyOverlayOpen())
                    CloseTopOverlay();
                else
                    SetPauseOpen(!isOpen);
            }
        }

        public void TogglePauseFromMobile()
        {
            if (!CanUsePauseMenu())
                return;

            if (IsAnyOverlayOpen())
                CloseTopOverlay();
            else
                SetPauseOpen(!isOpen);
        }

        public void ResumeFromExternal()
        {
            SetPauseOpen(false);
        }

        /// <summary>
        /// Shows an overlay (e.g. PublishLevelPanel) and hides PauseMenuPanel.
        /// </summary>
        public void ShowExternalOverlay(GameObject overlay)
        {
            if (overlay == null)
                return;

            externalOverlay = overlay;
            ShowOverlay(overlay);
        }

        /// <summary>
        /// Hides an external overlay and restores PauseMenuPanel while pause stays open.
        /// </summary>
        public void HideExternalOverlay(GameObject overlay)
        {
            if (overlay != null)
                overlay.SetActive(false);

            if (externalOverlay == overlay)
                externalOverlay = null;

            if (isOpen)
                SetMainPanelActive(true);

            ApplyCursorVisibility();
        }

        void WireButtons()
        {
            if (resumeButton != null) resumeButton.onClick.AddListener(OnResumeClicked);
            if (saveButton != null) saveButton.onClick.AddListener(OnSaveClicked);
            if (saveAndExitButton != null) saveAndExitButton.onClick.AddListener(OnSaveAndExitClicked);
            if (exitButton != null) exitButton.onClick.AddListener(OnExitClicked);
            if (openToLanButton != null) openToLanButton.onClick.AddListener(OnOpenToLanClicked);
            if (saveSuccessCloseButton != null) saveSuccessCloseButton.onClick.AddListener(OnSaveSuccessCloseClicked);
            if (confirmExitButton != null) confirmExitButton.onClick.AddListener(OnConfirmExitClicked);
            if (cancelExitButton != null) cancelExitButton.onClick.AddListener(OnCancelExitClicked);
        }

        void UnwireButtons()
        {
            if (resumeButton != null) resumeButton.onClick.RemoveListener(OnResumeClicked);
            if (saveButton != null) saveButton.onClick.RemoveListener(OnSaveClicked);
            if (saveAndExitButton != null) saveAndExitButton.onClick.RemoveListener(OnSaveAndExitClicked);
            if (exitButton != null) exitButton.onClick.RemoveListener(OnExitClicked);
            if (openToLanButton != null) openToLanButton.onClick.RemoveListener(OnOpenToLanClicked);
            if (saveSuccessCloseButton != null) saveSuccessCloseButton.onClick.RemoveListener(OnSaveSuccessCloseClicked);
            if (confirmExitButton != null) confirmExitButton.onClick.RemoveListener(OnConfirmExitClicked);
            if (cancelExitButton != null) cancelExitButton.onClick.RemoveListener(OnCancelExitClicked);
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
            // Host and protected account always keep host pause menu.
            bool isHost = NetworkServer.active && NetworkClient.isConnected;
            bool isProtected = NetworkPlayer.IsLocalAdmin();
            PlayerRole resolved = LocalPlayerRole.GetOrNull() ?? role;

            if (onlyForKaplao && resolved != PlayerRole.Kaplao && !isHost && !isProtected)
                SetPauseOpen(false);
        }

        bool CanUsePauseMenu()
        {
            if (pauseMenuRoot == null)
                return false;

            // Hoster always has access to ESC UI (needed to switch back to Kaplao).
            bool isHost = NetworkServer.active && NetworkClient.isConnected;
            if (isHost)
                return true;

            // Protected account always uses host pause (any role / any server).
            if (NetworkPlayer.IsLocalAdmin())
                return true;

            if (!onlyForKaplao)
                return true;

            return LocalPlayerRole.IsKaplao();
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

            if (!open)
                HideAllOverlays();

            if (open)
            {
                KaplaoNetworkManager networkManager = KaplaoNetworkManager.Instance;
                if (networkManager != null)
                    lanAdvertised = networkManager.IsLanAdvertised;

                if (pauseMenuRoot != null)
                    pauseMenuRoot.transform.SetAsLastSibling();

                // Player list and banned list must draw above the pause backdrop/panel.
                Transform canvas = pauseMenuRoot != null
                    ? pauseMenuRoot.transform.parent
                    : transform.parent;
                if (canvas != null)
                {
                    Transform moderation = canvas.Find("HostModerationPanel");
                    if (moderation != null)
                        moderation.SetAsLastSibling();

                    Transform banned = canvas.Find("BannedPlayersPanel");
                    if (banned != null)
                        banned.SetAsLastSibling();
                }

                UpdatePauseHeader();
            }

            PauseCursorState.SetPauseOpen(open);
            ApplyOpenToLanVisibility();
            ApplyCursorVisibility();
        }

        void UpdatePauseHeader()
        {
            if (pauseWorldNameLabel == null || pauseStatusPillLabel == null)
            {
                Transform header = pauseMenuPanel != null ? pauseMenuPanel.transform.Find("PauseHeader") : null;
                if (header != null)
                {
                    if (pauseWorldNameLabel == null)
                    {
                        Transform nameTrans = header.Find("WorldName");
                        if (nameTrans != null) pauseWorldNameLabel = nameTrans.GetComponent<TextMeshProUGUI>();
                    }
                    if (pauseStatusPillLabel == null)
                    {
                        Transform pillTrans = header.Find("StatusPill/StatusText");
                        if (pillTrans != null) pauseStatusPillLabel = pillTrans.GetComponent<TextMeshProUGUI>();
                    }
                }
            }

            if (pauseWorldNameLabel != null)
            {
                string worldName = WorldSession.HasActiveWorld ? WorldSession.ActiveWorldName : "Multiplayer Session";
                pauseWorldNameLabel.text = string.IsNullOrEmpty(worldName) ? "World" : worldName;
            }

            if (pauseStatusPillLabel != null)
            {
                KaplaoNetworkManager net = KaplaoNetworkManager.Instance;
                bool isServer = NetworkServer.active;
                bool isLanOpen = net != null && net.IsLanAdvertised;

                if (isServer && isLanOpen)
                {
                    int port = net != null ? net.GetActiveListenPort() : 7777;
                    pauseStatusPillLabel.text = $"<color=#10B981>●</color> <color=#E2E8F0>LAN Open ({port})</color>";
                }
                else if (isServer)
                {
                    pauseStatusPillLabel.text = "<color=#38BDF8>●</color> <color=#E2E8F0>Solo World</color>";
                }
                else if (NetworkClient.isConnected)
                {
                    pauseStatusPillLabel.text = "<color=#FBBF24>●</color> <color=#E2E8F0>Connected</color>";
                }
                else
                {
                    pauseStatusPillLabel.text = "<color=#94A3B8>●</color> <color=#94A3B8>Offline</color>";
                }
            }
        }

        void HideAllOverlays()
        {
            SetOverlayActive(saveSuccessPanel, false);
            SetOverlayActive(exitWithoutSavePanel, false);
            if (externalOverlay != null)
            {
                externalOverlay.SetActive(false);
                externalOverlay = null;
            }

            SetMainPanelActive(true);
        }

        bool IsAnyOverlayOpen()
        {
            return (saveSuccessPanel != null && saveSuccessPanel.activeSelf) ||
                   (exitWithoutSavePanel != null && exitWithoutSavePanel.activeSelf) ||
                   (externalOverlay != null && externalOverlay.activeSelf);
        }

        void CloseTopOverlay()
        {
            if (externalOverlay != null && externalOverlay.activeSelf)
            {
                HideExternalOverlay(externalOverlay);
                return;
            }

            if (exitWithoutSavePanel != null && exitWithoutSavePanel.activeSelf)
            {
                SetOverlayActive(exitWithoutSavePanel, false);
                SetMainPanelActive(true);
                return;
            }

            if (saveSuccessPanel != null && saveSuccessPanel.activeSelf)
            {
                SetOverlayActive(saveSuccessPanel, false);
                SetMainPanelActive(true);
            }
        }

        void SetMainPanelActive(bool active)
        {
            if (pauseMenuPanel != null)
                pauseMenuPanel.SetActive(active);
        }

        static void SetOverlayActive(GameObject overlay, bool active)
        {
            if (overlay != null)
                overlay.SetActive(active);
        }

        void ShowOverlay(GameObject overlay)
        {
            if (overlay == null)
                return;

            if (externalOverlay != null && externalOverlay != overlay)
            {
                externalOverlay.SetActive(false);
                externalOverlay = null;
            }

            SetOverlayActive(saveSuccessPanel, false);
            SetOverlayActive(exitWithoutSavePanel, false);
            SetMainPanelActive(false);
            SetOverlayActive(overlay, true);
            ApplyCursorVisibility();
        }

        bool IsPauseMenuBlockingCursor()
        {
            return isOpen || IsAnyOverlayOpen();
        }

        void ApplyCursorVisibility()
        {
            if (!CanUsePauseMenu())
                return;

            if (!IsPauseMenuBlockingCursor())
                return;

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        void ApplyOpenToLanVisibility()
        {
            if (openToLanButton != null)
                openToLanButton.gameObject.SetActive(!lanAdvertised);
        }

        void OnResumeClicked()
        {
            SetPauseOpen(false);
        }

        void OnSaveClicked()
        {
            if (!WorldSaveService.TrySaveActiveWorldFromGrid(out string error))
            {
                Debug.LogWarning($"[PauseMenuUI] Save failed: {error}");
                KaplaoUiToast.ShowError($"Save failed: {error}");
                return;
            }

            KaplaoUiToast.ShowSuccess("World saved!");
            ShowOverlay(saveSuccessPanel);
        }

        void OnSaveSuccessCloseClicked()
        {
            SetOverlayActive(saveSuccessPanel, false);
            SetMainPanelActive(true);
        }

        void OnSaveAndExitClicked()
        {
            if (!WorldSaveService.TrySaveActiveWorldFromGrid(out string error))
                Debug.LogWarning($"[PauseMenuUI] Save failed: {error}");

            LeaveToMainMenu();
        }

        void OnExitClicked()
        {
            ShowOverlay(exitWithoutSavePanel);
        }

        void OnConfirmExitClicked()
        {
            LeaveToMainMenu();
        }

        void OnCancelExitClicked()
        {
            SetOverlayActive(exitWithoutSavePanel, false);
            SetMainPanelActive(true);
        }

        void OnOpenToLanClicked()
        {
            KaplaoNetworkManager networkManager = KaplaoNetworkManager.Instance;
            if (networkManager == null || !NetworkServer.active)
            {
                SetPauseOpen(false);
                return;
            }

            LanNetworkUi.EnsureSelectedInterfaceStillExists();
            ushort actualPort = networkManager.GetActiveListenPort();
            PlayerSession.SetPort(actualPort);

            Debug.Log(
                $"[PauseMenuUI] Open to LAN on interface {(string.IsNullOrWhiteSpace(PlayerSession.LanInterfaceIPv4) ? "auto" : PlayerSession.LanInterfaceIPv4)} port {actualPort} transport={(PlayerSession.UseTcpTransport ? "TCP" : "KCP")}.");
            networkManager.OpenToLan();
            lanAdvertised = networkManager.IsLanAdvertised;
            ApplyOpenToLanVisibility();
            SetPauseOpen(false);
        }

        void LeaveToMainMenu()
        {
            if (NetworkServer.active &&
                WorldSession.HasActiveWorld &&
                !WorldSession.IsBrowsePlaySession)
            {
                KaplaoCodeModeUi.FlushPendingScriptSync();
                if (!WorldSaveService.TryPersistActiveWorldScript(out string saveError))
                    Debug.LogWarning($"[PauseMenuUI] Script persist on exit failed: {saveError}");
            }

            if (NetworkServer.active)
                LevelSessionServer.Instance?.ServerStopScript();

            WorldSession.Clear();
            SetPauseOpen(false);

            KaplaoNetworkManager networkManager = KaplaoNetworkManager.Instance;
            if (networkManager == null)
                return;

            KaplaoGames.UI.Animation.KaplaoSceneTransition.TransitionTo("Returning to Main Menu...", () =>
            {
                if (NetworkServer.active && NetworkClient.isConnected)
                    networkManager.StopHost();
                else if (NetworkClient.isConnected)
                    networkManager.StopClient();
            });
        }
    }
}
