using System;
using System.Collections;
using System.Collections.Generic;
using KaplaoGames.Client;
using KaplaoGames.Network;
using KaplaoGames.Shared;
using KaplaoGames.UI.Animation;
using KaplaoGames.UI.Theme;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.UI
{
    /// <summary>
    /// Main menu controller. Wire your own Canvas UI in the Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/UI/Main Menu UI")]
    public class MainMenuUI : MonoBehaviour
    {
        enum MenuFlow
        {
            None,
            SinglePlayer,
            NetworkGame
        }

        enum NicknameContext
        {
            None,
            NetworkGame,
            PlayWorld,
            CreateWorld
        }

        readonly Dictionary<long, KaplaoServerResponse> discoveredServers = new Dictionary<long, KaplaoServerResponse>();
        readonly List<PublicServerEntry> publicServers = new List<PublicServerEntry>();
        readonly HashSet<string> publicServerKeys = new HashSet<string>();
        readonly List<GameObject> worldEntryObjects = new List<GameObject>();
        Coroutine refreshServersRoutine;

        [Header("Panels")]
        [SerializeField] GameObject mainPanel;
        [SerializeField] GameObject nicknamePanel;
        [SerializeField] GameObject singlePlayerPanel;
        [SerializeField] GameObject networkPanel;
        [SerializeField] GameObject createWorldNamePanel;
        [SerializeField] GameObject deleteWorldConfirmPanel;
        [SerializeField] GameObject settingsPanel;

        [Header("Main Panel")]
        [SerializeField] Button singlePlayerButton;
        [SerializeField] Button networkGameButton;
        [SerializeField] Button settingsButton;
        [SerializeField] Button exitButton;

        [Header("Settings Panel")]
        [SerializeField] TMP_InputField settingsPortInput;
        [SerializeField] TMP_Dropdown settingsNetworkDropdown;
        [SerializeField] TMP_Dropdown settingsTransportDropdown;
        [SerializeField] Button settingsBackButton;
        [SerializeField] TMP_InputField settingsNicknameInput;
        [SerializeField] TextMeshProUGUI settingsAccountEmailLabel;
        [SerializeField] Button settingsSignInButton;
        [SerializeField] Button settingsSignOutButton;
        [SerializeField] Toggle settingsSkinRandomToggle;
        [SerializeField] Toggle settingsSkinToggle0;
        [SerializeField] Toggle settingsSkinToggle1;
        [SerializeField] Toggle settingsSkinToggle2;
        [SerializeField] TMP_Dropdown settingsMusicPackDropdown;
        [SerializeField] Slider settingsMusicVolumeSlider;
        [SerializeField] Slider settingsSfxVolumeSlider;
        [SerializeField] TMP_Dropdown settingsLanguageDropdown;
        [SerializeField] KaplaoAccountConfig accountConfig;
        [SerializeField] KaplaoAccountClient accountClient;

        [Header("Nickname Panel")]
        [SerializeField] TMP_InputField nicknameInput;
        [SerializeField] Button nicknameContinueButton;
        [SerializeField] Button nicknameBackButton;

        [Header("Single Player Panel")]
        [SerializeField] Button singlePlayerBackButton;
        [SerializeField] Transform worldListContent;
        [SerializeField] GameObject worldEntryTemplate;
        [SerializeField] GameObject worldActionsBar;
        [SerializeField] Button worldPlayButton;
        [SerializeField] Button worldDeleteButton;
        [SerializeField] Button openSavesFolderButton;
        [SerializeField] Button createNewWorldButton;
        [SerializeField] TextMeshProUGUI emptyWorldsLabel;

        [Header("Create World Panel")]
        [SerializeField] TMP_InputField worldNameInput;
        [SerializeField] Button createWorldContinueButton;
        [SerializeField] Button createWorldBackButton;

        [Header("Delete World Confirm")]
        [SerializeField] Button confirmDeleteWorldButton;
        [SerializeField] Button cancelDeleteWorldButton;

        [Header("Network Panel")]
        [SerializeField] TextMeshProUGUI networkStatusText;
        [SerializeField] Transform serverListContent;
        [SerializeField] GameObject serverEntryTemplate;
        [SerializeField] Button refreshServersButton;
        [SerializeField] TMP_InputField ipInput;
        [SerializeField] Button connectDirectlyButton;
        [SerializeField] Button networkBackButton;
        [Tooltip("Raw JSON URL for official servers. Use a Gist raw URL WITHOUT a commit hash so edits apply.")]
        [SerializeField] string publicServersUrl = PublicServerList.DefaultUrl;

        [Header("Networking")]
        [SerializeField] KaplaoNetworkManager networkManager;
        [SerializeField] GameObject runnerPrefab;
        [SerializeField] GameObject kaplaoPrefab;

        KaplaoNetworkDiscovery networkDiscovery;
        MenuFlow pendingFlow;
        NicknameContext nicknameContext;
        string selectedWorldName;
        string pendingCreateWorldName;
        bool pendingBrowsePlaySession;
        List<NetworkAdapterChoice> settingsNetworkChoices;
        readonly List<Toggle> settingsSkinToggles = new List<Toggle>();
        Coroutine ownedSkinsRefreshRoutine;

        void Awake()
        {
            PlayerSession.Load();
            WorldSession.Clear();
            ResolveNetworkComponents();

            // Main menu should always show the OS cursor.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Start()
        {
            KaplaoAudioPreference.Load();
            KaplaoAudioManager.Ensure();
            EnsureAccountComponents();
            EnsureSettingsUi();
            HideLegacyInlineNetworkControls();
            LanNetworkUi.EnsureSelectedInterfaceStillExists();
            WireButtons();
            InitializeFields();
            SetWorldActionsVisible(false);
            ShowMainPanel();
        }

        void OnEnable()
        {
            if (networkDiscovery != null)
                networkDiscovery.OnServerFound.AddListener(OnServerFound);

            WireAccountEvents();
            RefreshAccountSettingsUi();
        }

        void OnDisable()
        {
            if (networkDiscovery != null)
                networkDiscovery.OnServerFound.RemoveListener(OnServerFound);

            UnwireAccountEvents();
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
                RefreshAccountSettingsUi();
        }

        void WireButtons()
        {
            if (singlePlayerButton != null)
                singlePlayerButton.onClick.AddListener(OnSinglePlayerClicked);
            if (networkGameButton != null)
                networkGameButton.onClick.AddListener(OnNetworkGameClicked);
            if (settingsButton != null)
                settingsButton.onClick.AddListener(ShowSettingsPanel);
            if (exitButton != null)
                exitButton.onClick.AddListener(OnExitClicked);

            if (settingsBackButton != null)
                settingsBackButton.onClick.AddListener(OnSettingsBackClicked);
            if (settingsSignInButton != null)
                settingsSignInButton.onClick.AddListener(OnSettingsSignInClicked);
            if (settingsSignOutButton != null)
                settingsSignOutButton.onClick.AddListener(OnSettingsSignOutClicked);
            if (settingsNicknameInput != null)
                settingsNicknameInput.onEndEdit.AddListener(OnSettingsNicknameEndEdit);

            WireSkinToggles();
            WireAudioSettings();
            WireLanguageSettings();
            WireAccountEvents();

            if (nicknameContinueButton != null)
                nicknameContinueButton.onClick.AddListener(OnNicknameContinueClicked);
            if (nicknameBackButton != null)
                nicknameBackButton.onClick.AddListener(OnNicknameBackClicked);

            if (singlePlayerBackButton != null)
                singlePlayerBackButton.onClick.AddListener(ShowMainPanel);

            if (worldPlayButton != null)
                worldPlayButton.onClick.AddListener(OnWorldPlayClicked);

            if (worldDeleteButton != null)
                worldDeleteButton.onClick.AddListener(OnWorldDeleteClicked);

            if (openSavesFolderButton != null)
                openSavesFolderButton.onClick.AddListener(WorldSaveService.OpenSavesFolder);

            if (createNewWorldButton != null)
                createNewWorldButton.onClick.AddListener(OnCreateNewWorldClicked);

            if (createWorldContinueButton != null)
                createWorldContinueButton.onClick.AddListener(OnCreateWorldContinueClicked);

            if (createWorldBackButton != null)
                createWorldBackButton.onClick.AddListener(ShowSinglePlayerPanel);

            if (confirmDeleteWorldButton != null)
                confirmDeleteWorldButton.onClick.AddListener(OnConfirmDeleteWorldClicked);

            if (cancelDeleteWorldButton != null)
                cancelDeleteWorldButton.onClick.AddListener(HideDeleteWorldConfirm);

            if (refreshServersButton != null)
                refreshServersButton.onClick.AddListener(RefreshServerList);
            if (connectDirectlyButton != null)
                connectDirectlyButton.onClick.AddListener(OnConnectDirectlyClicked);
            if (networkBackButton != null)
                networkBackButton.onClick.AddListener(OnNetworkBackClicked);
        }

        void WireAccountEvents()
        {
            if (accountClient == null)
                return;

            accountClient.SignedIn -= OnAccountSignedIn;
            accountClient.SignedOut -= OnAccountSignedOut;
            accountClient.SignInFailed -= OnAccountSignInFailed;
            accountClient.StatusChanged -= OnAccountStatusChanged;

            accountClient.SignedIn += OnAccountSignedIn;
            accountClient.SignedOut += OnAccountSignedOut;
            accountClient.SignInFailed += OnAccountSignInFailed;
            accountClient.StatusChanged += OnAccountStatusChanged;
        }

        void UnwireAccountEvents()
        {
            if (accountClient == null)
                return;

            accountClient.SignedIn -= OnAccountSignedIn;
            accountClient.SignedOut -= OnAccountSignedOut;
            accountClient.SignInFailed -= OnAccountSignInFailed;
            accountClient.StatusChanged -= OnAccountStatusChanged;
        }

        void InitializeFields()
        {
            nicknameInput.text = PlayerSession.Nickname;

            if (worldNameInput != null)
                worldNameInput.text = string.Empty;

            if (serverEntryTemplate != null)
                serverEntryTemplate.SetActive(false);

            if (worldEntryTemplate != null)
                worldEntryTemplate.SetActive(false);

            if (deleteWorldConfirmPanel != null)
                deleteWorldConfirmPanel.SetActive(false);

            if (ipInput != null)
                ipInput.text = string.Empty;

            SyncSettingsControls();
            NetworkTransportConfig.Instance?.ApplyFromSession();
        }

        void SyncSettingsControls()
        {
            if (settingsPortInput != null)
                settingsPortInput.text = PlayerSession.Port.ToString();

            settingsNetworkChoices = LanNetworkUi.RefreshDropdown(settingsNetworkDropdown);
            TransportModeUi.ConfigureDropdown(settingsTransportDropdown);
            TransportModeUi.WireDropdown(settingsTransportDropdown, OnSettingsTransportChanged);
            TransportModeUi.SyncDropdown(settingsTransportDropdown);
            RunnerSkinPreference.Load();
            ApplySkinToggleVisibility();
            SyncSkinTogglesFromPreference();
            SyncAudioSettingsFromPreference();
            SyncLanguageFromPreference();
            RefreshAccountSettingsUi();
        }

        void WireAudioSettings()
        {
            TryAutoBindAudioSettings();

            if (settingsMusicPackDropdown != null)
            {
                settingsMusicPackDropdown.onValueChanged.RemoveListener(OnMusicPackChanged);
                settingsMusicPackDropdown.onValueChanged.AddListener(OnMusicPackChanged);
            }

            if (settingsMusicVolumeSlider != null)
            {
                settingsMusicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
                settingsMusicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            }

            if (settingsSfxVolumeSlider != null)
            {
                settingsSfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
                settingsSfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
            }

            SyncAudioSettingsFromPreference();
        }

        void TryAutoBindAudioSettings()
        {
            if (settingsPanel == null)
                return;

            if (settingsMusicPackDropdown == null)
                settingsMusicPackDropdown =
                    settingsPanel.transform.Find("MusicPackDropdown")?.GetComponent<TMP_Dropdown>();
            if (settingsMusicVolumeSlider == null)
                settingsMusicVolumeSlider =
                    settingsPanel.transform.Find("MusicVolumeSlider")?.GetComponent<Slider>();
            if (settingsSfxVolumeSlider == null)
                settingsSfxVolumeSlider =
                    settingsPanel.transform.Find("SfxVolumeSlider")?.GetComponent<Slider>();
            if (settingsLanguageDropdown == null)
                settingsLanguageDropdown =
                    settingsPanel.transform.Find("LanguageDropdown")?.GetComponent<TMP_Dropdown>();
        }

        void WireLanguageSettings()
        {
            if (settingsLanguageDropdown != null)
            {
                settingsLanguageDropdown.onValueChanged.RemoveListener(OnLanguageChanged);
                settingsLanguageDropdown.onValueChanged.AddListener(OnLanguageChanged);
                if (settingsLanguageDropdown.options.Count < 2)
                {
                    settingsLanguageDropdown.ClearOptions();
                    settingsLanguageDropdown.AddOptions(new List<string> { "English", "Russian" });
                }
            }

            SyncLanguageFromPreference();
        }

        void SyncLanguageFromPreference()
        {
            LanguagePreference.Load();
            if (settingsLanguageDropdown != null)
            {
                settingsLanguageDropdown.SetValueWithoutNotify((int)LanguagePreference.SelectedLanguage);
                settingsLanguageDropdown.RefreshShownValue();
            }
        }

        void OnLanguageChanged(int value)
        {
            if (value < 0 || value > (int)GameLanguage.Russian)
                return;

            LanguagePreference.SetLanguage((GameLanguage)value);
            LocalizationTable.ClearCache();
        }

        void SyncAudioSettingsFromPreference()
        {
            KaplaoAudioPreference.Load();
            KaplaoAudioManager.Ensure();

            if (settingsMusicPackDropdown != null)
            {
                if (settingsMusicPackDropdown.options.Count < 2)
                {
                    settingsMusicPackDropdown.ClearOptions();
                    settingsMusicPackDropdown.AddOptions(new List<string> { "Trick or Treat", "Maf" });
                }

                settingsMusicPackDropdown.SetValueWithoutNotify((int)KaplaoAudioPreference.MusicPack);
                settingsMusicPackDropdown.RefreshShownValue();
            }

            if (settingsMusicVolumeSlider != null)
                settingsMusicVolumeSlider.SetValueWithoutNotify(KaplaoAudioPreference.MusicVolume);

            if (settingsSfxVolumeSlider != null)
                settingsSfxVolumeSlider.SetValueWithoutNotify(KaplaoAudioPreference.SfxVolume);
        }

        void OnMusicPackChanged(int value)
        {
            KaplaoMusicPack pack = value == 1 ? KaplaoMusicPack.Maf : KaplaoMusicPack.TrickOrTreat;
            KaplaoAudioManager.Ensure()?.SetMusicPack(pack);
        }

        void OnMusicVolumeChanged(float value)
        {
            KaplaoAudioManager.Ensure()?.SetMusicVolume(value);
        }

        void OnSfxVolumeChanged(float value)
        {
            KaplaoAudioManager.Ensure()?.SetSfxVolume(value);
        }

        void WireSkinToggles()
        {
            TryAutoBindSkinToggles();

            if (settingsSkinRandomToggle != null)
            {
                settingsSkinRandomToggle.onValueChanged.RemoveListener(OnSkinRandomToggled);
                settingsSkinRandomToggle.onValueChanged.AddListener(OnSkinRandomToggled);
            }

            if (settingsSkinToggle0 != null)
            {
                settingsSkinToggle0.onValueChanged.RemoveListener(OnSkinToggle0);
                settingsSkinToggle0.onValueChanged.AddListener(OnSkinToggle0);
            }

            if (settingsSkinToggle1 != null)
            {
                settingsSkinToggle1.onValueChanged.RemoveListener(OnSkinToggle1);
                settingsSkinToggle1.onValueChanged.AddListener(OnSkinToggle1);
            }

            if (settingsSkinToggle2 != null)
            {
                settingsSkinToggle2.onValueChanged.RemoveListener(OnSkinToggle2);
                settingsSkinToggle2.onValueChanged.AddListener(OnSkinToggle2);
            }

            for (int i = 0; i < settingsSkinToggles.Count; i++)
            {
                Toggle toggle = settingsSkinToggles[i];
                if (toggle == null)
                    continue;

                toggle.onValueChanged.RemoveAllListeners();
                int capturedIndex = i;
                toggle.onValueChanged.AddListener(isOn => OnSkinToggle(capturedIndex, isOn));
            }

            SyncSkinTogglesFromPreference();
            ApplySkinToggleVisibility();
        }

        void TryAutoBindSkinToggles()
        {
            if (settingsPanel == null)
                return;

            Transform row = settingsPanel.transform.Find("SkinRow");
            if (row == null)
                return;

            if (settingsSkinRandomToggle == null)
                settingsSkinRandomToggle = row.Find("SkinRandomToggle")?.GetComponent<Toggle>();
            if (settingsSkinToggle0 == null)
                settingsSkinToggle0 = row.Find("SkinToggle0")?.GetComponent<Toggle>();
            if (settingsSkinToggle1 == null)
                settingsSkinToggle1 = row.Find("SkinToggle1")?.GetComponent<Toggle>();
            if (settingsSkinToggle2 == null)
                settingsSkinToggle2 = row.Find("SkinToggle2")?.GetComponent<Toggle>();

            settingsSkinToggles.Clear();
            for (int i = 0; ; i++)
            {
                Toggle toggle = row.Find($"SkinToggle{i}")?.GetComponent<Toggle>();
                if (toggle == null)
                    break;

                settingsSkinToggles.Add(toggle);
            }
        }

        void SyncSkinTogglesFromPreference()
        {
            RunnerSkinPreference.Load();
            int selected = RunnerSkinPreference.SelectedIndex;

            SetToggleWithoutNotify(settingsSkinRandomToggle, selected < 0);
            for (int i = 0; i < settingsSkinToggles.Count; i++)
                SetToggleWithoutNotify(settingsSkinToggles[i], selected == i);
        }

        static void SetToggleWithoutNotify(Toggle toggle, bool isOn)
        {
            if (toggle == null)
                return;

            toggle.SetIsOnWithoutNotify(isOn);
        }

        void OnSkinRandomToggled(bool isOn)
        {
            if (!isOn)
                return;

            RunnerSkinPreference.SetSelectedIndex(RunnerSkinPreference.RandomIndex);
        }

        void OnSkinToggle0(bool isOn)
        {
            if (isOn)
                RunnerSkinPreference.SetSelectedIndex(0);
        }

        void OnSkinToggle1(bool isOn)
        {
            if (isOn)
                RunnerSkinPreference.SetSelectedIndex(1);
        }

        void OnSkinToggle2(bool isOn)
        {
            if (isOn)
                RunnerSkinPreference.SetSelectedIndex(2);
        }

        void OnSkinToggle(int index, bool isOn)
        {
            if (isOn)
                RunnerSkinPreference.SetSelectedIndex(index);
        }

        static readonly Color SettingsNicknameBg = new Color(0x2E / 255f, 0x2E / 255f, 0x2E / 255f, 1f);

        void RefreshAccountSettingsUi()
        {
            bool loggedIn = PlayerSession.IsLoggedIn;

            if (settingsNicknameInput != null)
            {
                settingsNicknameInput.text = PlayerSession.Nickname;
                settingsNicknameInput.readOnly = loggedIn;
                settingsNicknameInput.interactable = !loggedIn;

                // ColorBlock multiplies over targetGraphic — keep it white so bg stays #2E2E2E.
                ColorBlock colors = settingsNicknameInput.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = Color.white;
                colors.pressedColor = Color.white;
                colors.selectedColor = Color.white;
                colors.disabledColor = Color.white;
                settingsNicknameInput.colors = colors;

                Image target = settingsNicknameInput.targetGraphic as Image;
                if (target != null)
                    target.color = SettingsNicknameBg;

                if (settingsNicknameInput.textComponent != null)
                {
                    settingsNicknameInput.textComponent.color = loggedIn
                        ? new Color(0.72f, 0.72f, 0.74f, 1f)
                        : Color.white;
                }
            }

            if (settingsAccountEmailLabel != null)
            {
                bool busy = accountClient != null && accountClient.IsBusy;
                string status = accountClient != null ? accountClient.StatusMessage : string.Empty;

                if (loggedIn)
                {
                    settingsAccountEmailLabel.gameObject.SetActive(true);
                    settingsAccountEmailLabel.text = string.IsNullOrEmpty(PlayerSession.AccountEmail)
                        ? $"Signed in as {PlayerSession.Nickname}"
                        : $"Signed in as {PlayerSession.MaskedAccountEmail}";
                }
                else if (busy || !string.IsNullOrEmpty(status))
                {
                    settingsAccountEmailLabel.gameObject.SetActive(true);
                    settingsAccountEmailLabel.text = string.IsNullOrEmpty(status)
                        ? "Waiting for browser login…"
                        : status;
                }
                else
                {
                    settingsAccountEmailLabel.gameObject.SetActive(false);
                    settingsAccountEmailLabel.text = string.Empty;
                }
            }

            if (settingsSignInButton != null)
            {
                settingsSignInButton.gameObject.SetActive(!loggedIn);
                var label = settingsSignInButton.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    bool busy = accountClient != null && accountClient.IsBusy;
                    label.text = busy ? "I've signed in" : "Sign in";
                }
            }
            if (settingsSignOutButton != null)
                settingsSignOutButton.gameObject.SetActive(loggedIn);
        }

        void EnsureAccountComponents()
        {
            if (accountConfig == null)
                accountConfig = GetComponent<KaplaoAccountConfig>() ?? gameObject.AddComponent<KaplaoAccountConfig>();
            if (accountClient == null)
                accountClient = GetComponent<KaplaoAccountClient>() ?? gameObject.AddComponent<KaplaoAccountClient>();
        }

        void OnSettingsNicknameEndEdit(string value)
        {
            if (PlayerSession.IsLoggedIn)
            {
                RefreshAccountSettingsUi();
                return;
            }

            PlayerSession.SetNickname(value);
            if (settingsNicknameInput != null)
                settingsNicknameInput.text = PlayerSession.Nickname;
        }

        void OnSettingsSignInClicked()
        {
            EnsureAccountComponents();
            WireAccountEvents();
            string url = accountConfig != null ? accountConfig.WebsiteUrl : null;
            accountClient?.BeginSignIn(url);
            RefreshAccountSettingsUi();
        }

        void OnSettingsSignOutClicked()
        {
            accountClient?.SignOut();
            RefreshAccountSettingsUi();
        }

        void OnAccountSignedIn()
        {
            RefreshAccountSettingsUi();
            if (nicknameInput != null)
                nicknameInput.text = PlayerSession.Nickname;

            if (settingsPanel != null && settingsPanel.activeSelf)
                RefreshOwnedSkinsFromServer();
        }

        void OnAccountSignedOut()
        {
            RunnerSkinPreference.ClearOwnedAdminSkinIds();
            ApplySkinToggleVisibility();
            SyncSkinTogglesFromPreference();
            RefreshAccountSettingsUi();
        }

        void OnAccountStatusChanged() => RefreshAccountSettingsUi();

        void OnAccountSignInFailed()
        {
            Debug.LogWarning("[MainMenuUI] Account sign-in failed or timed out.");
            RefreshAccountSettingsUi();
        }

        void ContinueAfterNicknameResolved()
        {
            if (nicknameContext == NicknameContext.NetworkGame)
            {
                NetworkTransportConfig.Instance?.ApplyFromSession();
                ShowOnlyPanels(networkPanel);
                RefreshServerList();
                return;
            }

            if (nicknameContext == NicknameContext.PlayWorld)
            {
                if (string.IsNullOrWhiteSpace(selectedWorldName))
                    return;

                if (pendingBrowsePlaySession)
                    WorldSession.SelectBrowsePlayWorld(selectedWorldName);
                else
                    WorldSession.SelectExistingWorld(selectedWorldName);

                pendingBrowsePlaySession = false;
                StartSinglePlayerGame();
                return;
            }

            if (nicknameContext == NicknameContext.CreateWorld)
            {
                if (string.IsNullOrWhiteSpace(pendingCreateWorldName))
                    return;

                WorldSession.BeginCreateWorld(pendingCreateWorldName);
                StartSinglePlayerGame();
            }
        }

        /// <summary>
        /// Nickname panel only for guests still using the default "Player" name.
        /// Logged-in accounts and custom local nicknames skip it.
        /// </summary>
        void BeginFlowWithNicknameGate(NicknameContext context)
        {
            nicknameContext = context;
            if (!PlayerSession.IsLoggedIn && PlayerSession.IsDefaultNickname)
            {
                ShowNicknamePanel(context);
                return;
            }

            ContinueAfterNicknameResolved();
        }

        void OnSettingsTransportChanged(int value)
        {
            ApplySettingsFromUi();
        }

        void ApplySettingsFromUi()
        {
            if (settingsPortInput != null)
            {
                if (!PlayerSession.TrySetPort(settingsPortInput.text, out ushort port))
                {
                    port = PlayerSession.DefaultPort;
                    PlayerSession.SetPort(port);
                }

                settingsPortInput.text = port.ToString();
            }

            if (!PlayerSession.IsLoggedIn && settingsNicknameInput != null)
                PlayerSession.SetNickname(settingsNicknameInput.text);

            LanNetworkUi.ApplyDropdown(settingsNetworkDropdown, settingsNetworkChoices);
            if (settingsTransportDropdown != null)
                NetworkTransportConfig.SetUseTcp(settingsTransportDropdown.value == 1);
            else
                NetworkTransportConfig.Instance?.ApplyFromSession();

            KaplaoNetworkManager.Instance?.ApplySessionPort();
            RefreshAccountSettingsUi();
        }

        void EnsureSettingsUi()
        {
            Transform menuRoot = mainPanel != null ? mainPanel.transform.parent : transform;

            if (settingsButton == null && mainPanel != null)
            {
                Transform existingButton = mainPanel.transform.Find("SettingsButton");
                if (existingButton != null)
                    settingsButton = existingButton.GetComponent<Button>();
                else
                    settingsButton = CreateMenuButton(mainPanel.transform, "SettingsButton", "Settings", new Vector2(0f, -383f));
            }

            if (settingsPanel == null && menuRoot != null)
            {
                Transform existingPanel = menuRoot.Find("SettingsPanel");
                if (existingPanel != null)
                    settingsPanel = existingPanel.gameObject;
                else
                    settingsPanel = CreateSettingsPanel(menuRoot);
            }

            if (settingsPanel == null)
                return;

            if (settingsPortInput == null)
            {
                Transform port = settingsPanel.transform.Find("PortInput");
                if (port != null)
                    settingsPortInput = port.GetComponent<TMP_InputField>();
            }

            if (settingsNetworkDropdown == null)
            {
                Transform network = settingsPanel.transform.Find("NetworkDropdown");
                if (network != null)
                    settingsNetworkDropdown = network.GetComponent<TMP_Dropdown>();
            }

            if (settingsTransportDropdown == null)
            {
                Transform transport = settingsPanel.transform.Find(TransportModeUi.DropdownObjectName);
                if (transport != null)
                    settingsTransportDropdown = transport.GetComponent<TMP_Dropdown>();
            }

            if (settingsBackButton == null)
            {
                Transform back = settingsPanel.transform.Find("BackButton");
                if (back != null)
                    settingsBackButton = back.GetComponent<Button>();
            }

            if (settingsPortInput == null)
                settingsPortInput = CreatePortInput(settingsPanel.transform, "PortInput", new Vector2(0f, 70f));

            if (settingsPortInput != null)
            {
                settingsPortInput.onEndEdit.RemoveAllListeners();
                settingsPortInput.onEndEdit.AddListener(_ => ApplySettingsFromUi());
            }

            if (settingsNetworkDropdown == null)
                settingsNetworkDropdown = TransportModeUi.EnsureDropdown(
                    settingsPanel.transform,
                    new Vector2(0f, 10f),
                    new Vector2(420f, 40f),
                    "NetworkDropdown");

            if (settingsNetworkDropdown != null)
            {
                settingsNetworkDropdown.onValueChanged.RemoveAllListeners();
                settingsNetworkDropdown.onValueChanged.AddListener(_ => ApplySettingsFromUi());
            }

            if (settingsTransportDropdown == null)
                settingsTransportDropdown = TransportModeUi.EnsureDropdown(
                    settingsPanel.transform,
                    new Vector2(0f, -50f),
                    new Vector2(420f, 40f));

            if (settingsBackButton == null)
                settingsBackButton = CreateMenuButton(settingsPanel.transform, "BackButton", "Back", new Vector2(0f, -130f));
        }

        void HideLegacyInlineNetworkControls()
        {
            HideChild(mainPanel, "PortInput");
            HideChild(mainPanel, TransportModeUi.DropdownObjectName);
            HideChild(networkPanel, TransportModeUi.DropdownObjectName);
        }

        static void HideChild(GameObject parent, string childName)
        {
            if (parent == null)
                return;

            Transform child = parent.transform.Find(childName);
            if (child != null)
                child.gameObject.SetActive(false);
        }

        GameObject CreateSettingsPanel(Transform parent)
        {
            GameObject panel = new GameObject("SettingsPanel", typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            StretchFull(panel.GetComponent<RectTransform>());
            panel.SetActive(false);

            CreateLabel(panel.transform, "Title", "Settings", new Vector2(0f, 140f), 36f);
            CreateLabel(panel.transform, "PortLabel", "Port", new Vector2(-160f, 70f), 22f);
            CreateLabel(panel.transform, "NetworkLabel", "Network", new Vector2(-160f, 10f), 22f);
            CreateLabel(panel.transform, "TransportLabel", "Transport", new Vector2(-160f, -50f), 22f);
            return panel;
        }

        static Button CreateMenuButton(Transform parent, string name, string text, Vector2 anchoredPosition)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(220f, 44f);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 0.95f);

            GameObject labelObject = new GameObject("Text", typeof(RectTransform));
            labelObject.transform.SetParent(buttonObject.transform, false);
            StretchFull(labelObject.GetComponent<RectTransform>());
            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 24f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;

            return buttonObject.GetComponent<Button>();
        }

        static TMP_InputField CreatePortInput(Transform parent, string name, Vector2 anchoredPosition)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            root.transform.SetParent(parent, false);

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(220f, 36f);

            Image image = root.GetComponent<Image>();
            image.color = new Color(0.18f, 0.18f, 0.18f, 0.95f);

            GameObject textArea = new GameObject("Text Area", typeof(RectTransform));
            textArea.transform.SetParent(root.transform, false);
            RectTransform textAreaRect = textArea.GetComponent<RectTransform>();
            StretchFull(textAreaRect);
            textAreaRect.offsetMin = new Vector2(8f, 4f);
            textAreaRect.offsetMax = new Vector2(-8f, -4f);

            GameObject placeholderObject = new GameObject("Placeholder", typeof(RectTransform));
            placeholderObject.transform.SetParent(textArea.transform, false);
            TextMeshProUGUI placeholder = placeholderObject.AddComponent<TextMeshProUGUI>();
            placeholder.text = "25565";
            placeholder.fontSize = 18f;
            placeholder.color = new Color(1f, 1f, 1f, 0.35f);
            placeholder.raycastTarget = false;
            StretchFull(placeholder.rectTransform);

            GameObject textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(textArea.transform, false);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = 18f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            StretchFull(text.rectTransform);

            TMP_InputField input = root.GetComponent<TMP_InputField>();
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            input.textViewport = textAreaRect;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.targetGraphic = image;
            return input;
        }

        static void CreateLabel(Transform parent, string name, string text, Vector2 anchoredPosition, float fontSize)
        {
            GameObject labelObject = new GameObject(name, typeof(RectTransform));
            labelObject.transform.SetParent(parent, false);
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(280f, 40f);

            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
        }

        static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        void ResolveNetworkComponents()
        {
            if (networkManager == null)
                networkManager = KaplaoNetworkManager.Instance;

            if (networkManager == null)
                networkManager = FindFirstObjectByType<KaplaoNetworkManager>();

            if (networkManager == null)
            {
                networkManager = NetworkInfrastructureBootstrap.EnsureExists(runnerPrefab, kaplaoPrefab);
                if (networkManager == null)
                {
                    Debug.LogError("[MainMenuUI] KaplaoNetworkManager not found and bootstrap failed.");
                    return;
                }
            }

            networkDiscovery = networkManager.networkDiscovery != null
                ? networkManager.networkDiscovery
                : networkManager.GetComponent<KaplaoNetworkDiscovery>();
        }

        void ShowOnlyPanels(params GameObject[] visiblePanels)
        {
            UiFlyInChildren flyIn = FindFirstObjectByType<UiFlyInChildren>();
            flyIn?.SnapAllToRest();

            SetPanelActive(mainPanel, false);
            SetPanelActive(nicknamePanel, false);
            SetPanelActive(singlePlayerPanel, false);
            SetPanelActive(networkPanel, false);
            SetPanelActive(createWorldNamePanel, false);
            SetPanelActive(settingsPanel, false);
            HideDeleteWorldConfirm();

            if (visiblePanels == null)
                return;

            for (int i = 0; i < visiblePanels.Length; i++)
                SetPanelActive(visiblePanels[i], true);
        }

        static void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null)
                panel.SetActive(active);
        }

        void ShowMainPanel()
        {
            pendingFlow = MenuFlow.None;
            nicknameContext = NicknameContext.None;
            selectedWorldName = null;
            pendingCreateWorldName = null;
            networkDiscovery?.StopDiscovery();
            LanNetworkUi.EnsureSelectedInterfaceStillExists();
            ShowOnlyPanels(mainPanel);
        }

        void ShowSettingsPanel()
        {
            // Cancel any active sign-in flow when reopening settings
            // (e.g., user closed the browser window and wants to try again)
            if (accountClient != null && accountClient.IsBusy)
            {
                accountClient.CancelSignIn();
            }

            SyncSettingsControls();
            ShowOnlyPanels(settingsPanel);
            RefreshOwnedSkinsFromServer();
        }

        void RefreshOwnedSkinsFromServer()
        {
            if (ownedSkinsRefreshRoutine != null)
                StopCoroutine(ownedSkinsRefreshRoutine);

            ownedSkinsRefreshRoutine = StartCoroutine(RefreshOwnedSkinsRoutine());
        }

        IEnumerator RefreshOwnedSkinsRoutine()
        {
            if (!PlayerSession.IsLoggedIn)
            {
                RunnerSkinPreference.ClearOwnedAdminSkinIds();
                ApplySkinToggleVisibility();
                SyncSkinTogglesFromPreference();
                ownedSkinsRefreshRoutine = null;
                yield break;
            }

            PlayerSkinsFetchResult fetchResult = default;
            yield return KaplaoPlayerSkinsService.FetchOwnedSkinsCoroutine(result => fetchResult = result);

            if (fetchResult.Success)
                RunnerSkinPreference.SetOwnedAdminSkinIds(fetchResult.SkinIds);

            ApplySkinToggleVisibility();
            SyncSkinTogglesFromPreference();
            ownedSkinsRefreshRoutine = null;
        }

        void ApplySkinToggleVisibility()
        {
            for (int i = 0; i < settingsSkinToggles.Count; i++)
            {
                Toggle toggle = settingsSkinToggles[i];
                if (toggle == null)
                    continue;

                toggle.gameObject.SetActive(RunnerSkinPreference.IsSkinVisibleInSettings(i));
            }
        }

        void OnSettingsBackClicked()
        {
            ApplySettingsFromUi();
            ShowMainPanel();
        }

        void ShowSinglePlayerPanel()
        {
            pendingFlow = MenuFlow.SinglePlayer;
            nicknameContext = NicknameContext.None;
            pendingCreateWorldName = null;
            pendingBrowsePlaySession = false;
            ShowOnlyPanels(singlePlayerPanel);
            RefreshWorldList();
        }

        void ShowNicknamePanel(NicknameContext context)
        {
            nicknameContext = context;
            nicknameInput.text = PlayerSession.Nickname;
            ShowOnlyPanels(nicknamePanel);
        }

        void ShowCreateWorldNamePanel()
        {
            pendingFlow = MenuFlow.SinglePlayer;
            if (worldNameInput != null)
                worldNameInput.text = string.Empty;

            ShowOnlyPanels(createWorldNamePanel);
        }

        void OnSinglePlayerClicked()
        {
            ShowSinglePlayerPanel();
        }

        void OnNetworkGameClicked()
        {
            pendingFlow = MenuFlow.NetworkGame;
            BeginFlowWithNicknameGate(NicknameContext.NetworkGame);
        }

        void OnCreateNewWorldClicked()
        {
            ShowCreateWorldNamePanel();
        }

        void OnCreateWorldContinueClicked()
        {
            string rawName = worldNameInput != null ? worldNameInput.text : string.Empty;
            if (!WorldSaveService.TryValidateWorldName(rawName, out string sanitized, out string error))
            {
                Debug.LogWarning($"[MainMenuUI] {error}");
                KaplaoUiToast.ShowError(error);
                if (worldNameInput != null)
                    KaplaoUiAnimator.ShakeError(worldNameInput.GetComponent<RectTransform>());
                return;
            }

            if (WorldSaveService.WorldExists(sanitized))
            {
                const string existsErr = "A world with this name already exists.";
                Debug.LogWarning($"[MainMenuUI] {existsErr}");
                KaplaoUiToast.ShowError(existsErr);
                if (worldNameInput != null)
                    KaplaoUiAnimator.ShakeError(worldNameInput.GetComponent<RectTransform>());
                return;
            }

            pendingCreateWorldName = sanitized;
            BeginFlowWithNicknameGate(NicknameContext.CreateWorld);
        }

        void OnWorldPlayClicked()
        {
            if (string.IsNullOrWhiteSpace(selectedWorldName))
                return;

            pendingBrowsePlaySession = false;
            BeginFlowWithNicknameGate(NicknameContext.PlayWorld);
        }

        public void PlayWorldByName(string worldName)
        {
            if (string.IsNullOrWhiteSpace(worldName))
                return;

            selectedWorldName = worldName;
            WorldSession.SelectExistingWorld(worldName);
            pendingBrowsePlaySession = false;
            BeginFlowWithNicknameGate(NicknameContext.PlayWorld);
        }

        public void PlayBrowseLevelByName(string worldName)
        {
            if (string.IsNullOrWhiteSpace(worldName))
                return;

            selectedWorldName = worldName;
            pendingBrowsePlaySession = true;
            BeginFlowWithNicknameGate(NicknameContext.PlayWorld);
        }

        public void OnBrowseLevelCopied(string worldName)
        {
            if (string.IsNullOrWhiteSpace(worldName))
            {
                ShowSinglePlayerPanel();
                return;
            }

            selectedWorldName = worldName;
            WorldSession.SelectExistingWorld(worldName);
            ShowSinglePlayerPanel();
            SelectWorldInList(worldName);
        }

        void SelectWorldInList(string worldName)
        {
            for (int i = 0; i < worldEntryObjects.Count; i++)
            {
                GameObject entry = worldEntryObjects[i];
                if (entry == null)
                    continue;

                WorldEntryView view = entry.GetComponent<WorldEntryView>();
                if (view != null && !string.IsNullOrEmpty(view.WorldName))
                {
                    if (string.Equals(view.WorldName, worldName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        SelectWorld(worldName, entry);
                        return;
                    }
                    continue;
                }

                Transform titleTrans = entry.transform.Find("Title");
                TextMeshProUGUI label = titleTrans != null
                    ? titleTrans.GetComponent<TextMeshProUGUI>()
                    : entry.GetComponentInChildren<TextMeshProUGUI>();

                if (label == null || !string.Equals(label.text, worldName, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                SelectWorld(worldName, entry);
                return;
            }
        }

        void OnWorldDeleteClicked()
        {
            if (string.IsNullOrWhiteSpace(selectedWorldName) || deleteWorldConfirmPanel == null)
                return;

            deleteWorldConfirmPanel.SetActive(true);
        }

        void HideDeleteWorldConfirm()
        {
            if (deleteWorldConfirmPanel != null)
                deleteWorldConfirmPanel.SetActive(false);
        }

        void OnConfirmDeleteWorldClicked()
        {
            if (string.IsNullOrWhiteSpace(selectedWorldName))
            {
                HideDeleteWorldConfirm();
                return;
            }

            if (!WorldSaveService.TryDeleteWorld(selectedWorldName, out string error))
                Debug.LogWarning($"[MainMenuUI] Delete failed: {error}");

            selectedWorldName = null;
            HideDeleteWorldConfirm();
            SetWorldActionsVisible(false);
            RefreshWorldList();
        }

        void OnNicknameBackClicked()
        {
            switch (nicknameContext)
            {
                case NicknameContext.PlayWorld:
                case NicknameContext.CreateWorld:
                    ShowSinglePlayerPanel();
                    break;

                default:
                    ShowMainPanel();
                    break;
            }
        }

        void OnNicknameContinueClicked()
        {
            if (!PlayerSession.IsLoggedIn)
                PlayerSession.SetNickname(nicknameInput != null ? nicknameInput.text : PlayerSession.Nickname);

            ContinueAfterNicknameResolved();
        }

        void StartSinglePlayerGame()
        {
            if (networkManager == null)
                ResolveNetworkComponents();

            if (networkManager == null)
            {
                Debug.LogError("[MainMenuUI] Cannot start world: KaplaoNetworkManager is missing.");
                return;
            }

            LanNetworkUi.EnsureSelectedInterfaceStillExists();
            NetworkTransportConfig.Instance?.ApplyFromSession();
            if (WorldSession.HasActiveWorld && !WorldSession.IsBrowsePlaySession)
                WorldSaveService.RecordWorldVisited(WorldSession.ActiveWorldName);
            Debug.Log(
                $"[MainMenuUI] Starting single player host for world '{WorldSession.ActiveWorldName}' port={PlayerSession.Port} transport={(PlayerSession.UseTcpTransport ? "TCP" : "KCP")}.");
            networkManager.StartSinglePlayerHost();
        }

        void RefreshWorldList()
        {
            ClearWorldListUi();
            IReadOnlyList<WorldListItem> worlds = WorldSaveService.ListWorldsWithMetadata();

            if (emptyWorldsLabel != null)
                emptyWorldsLabel.gameObject.SetActive(worlds.Count == 0);

            for (int i = 0; i < worlds.Count; i++)
                CreateWorldEntry(worlds[i]);
        }

        void ClearWorldListUi()
        {
            worldEntryObjects.Clear();

            if (worldListContent == null || worldEntryTemplate == null)
                return;

            for (int i = worldListContent.childCount - 1; i >= 0; i--)
            {
                Transform child = worldListContent.GetChild(i);
                if (child.gameObject == worldEntryTemplate)
                    continue;

                Destroy(child.gameObject);
            }
        }

        void CreateWorldEntry(string worldName)
        {
            WorldMetadata meta = WorldSaveService.ReadWorldMetadata(worldName);
            CreateWorldEntry(new WorldListItem
            {
                Name = worldName,
                LastPlayedUtcTicks = meta.lastPlayedUtcTicks,
                CreatedUtcTicks = meta.createdUtcTicks
            });
        }

        void CreateWorldEntry(WorldListItem item)
        {
            if (worldListContent == null || worldEntryTemplate == null)
                return;

            GameObject entry = Instantiate(worldEntryTemplate, worldListContent);
            entry.SetActive(true);
            worldEntryObjects.Add(entry);

            WorldEntryView view = entry.GetComponent<WorldEntryView>();
            if (view == null)
                view = entry.AddComponent<WorldEntryView>();

            view.WorldName = item.Name;

            Transform titleTrans = entry.transform.Find("Title");
            TextMeshProUGUI title = titleTrans != null
                ? titleTrans.GetComponent<TextMeshProUGUI>()
                : null;

            if (title == null)
            {
                title = entry.GetComponentInChildren<TextMeshProUGUI>();
                if (title != null)
                    title.gameObject.name = "Title";
            }

            if (title != null)
            {
                title.text = item.Name;
                view.Title = title;
            }

            Transform subtitleTrans = entry.transform.Find("Subtitle");
            TextMeshProUGUI subtitle = subtitleTrans != null
                ? subtitleTrans.GetComponent<TextMeshProUGUI>()
                : null;

            if (subtitle == null && title != null)
            {
                GameObject subObj = new GameObject("Subtitle", typeof(RectTransform));
                subObj.transform.SetParent(title.transform.parent, false);
                subtitle = subObj.AddComponent<TextMeshProUGUI>();
                subtitle.fontSize = 13f;
                subtitle.color = new Color(0x94 / 255f, 0xA3 / 255f, 0xB8 / 255f, 1f); // #94A3B8
                subtitle.raycastTarget = false;

                // Adjust positions inside entry card
                RectTransform subRect = subtitle.rectTransform;
                subRect.anchorMin = new Vector2(0f, 0f);
                subRect.anchorMax = new Vector2(1f, 0.42f);
                subRect.offsetMin = new Vector2(18f, 6f);
                subRect.offsetMax = new Vector2(-18f, 0f);

                RectTransform titleRect = title.rectTransform;
                titleRect.anchorMin = new Vector2(0f, 0.42f);
                titleRect.anchorMax = new Vector2(1f, 1f);
                titleRect.offsetMin = new Vector2(18f, 0f);
                titleRect.offsetMax = new Vector2(-18f, -6f);
            }

            if (subtitle != null)
            {
                subtitle.text = WorldEntryView.FormatLastPlayed(item.LastPlayedUtcTicks);
                view.Subtitle = subtitle;
            }

            view.Background = entry.GetComponent<Image>() ?? entry.GetComponentInChildren<Image>();
            Transform borderTrans = entry.transform.Find("UI_Border");
            if (borderTrans != null)
                view.Border = borderTrans.GetComponent<Image>();

            view.SetSelected(string.Equals(selectedWorldName, item.Name, StringComparison.OrdinalIgnoreCase));

            Button button = entry.GetComponent<Button>();
            if (button == null)
                button = entry.GetComponentInChildren<Button>();

            if (button != null)
            {
                string capturedName = item.Name;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectWorld(capturedName, entry));
            }
        }

        void SelectWorld(string worldName, GameObject entryObject)
        {
            selectedWorldName = worldName;
            SetWorldActionsVisible(true);

            for (int i = 0; i < worldEntryObjects.Count; i++)
            {
                GameObject entry = worldEntryObjects[i];
                if (entry == null)
                    continue;

                WorldEntryView view = entry.GetComponent<WorldEntryView>();
                if (view != null)
                {
                    view.SetSelected(entry == entryObject);
                }
                else
                {
                    Image image = entry.GetComponent<Image>() ?? entry.GetComponentInChildren<Image>();
                    if (image != null)
                    {
                        image.color = entry == entryObject
                            ? new Color(0x1D / 255f, 0x3A / 255f, 0x5F / 255f, 0.98f)
                            : new Color(0x17 / 255f, 0x20 / 255f, 0x36 / 255f, 0.95f);
                    }
                }
            }
        }

        void SetWorldActionsVisible(bool visible)
        {
            if (worldActionsBar != null)
                worldActionsBar.SetActive(visible);

            if (worldPlayButton != null)
                worldPlayButton.interactable = visible;

            if (worldDeleteButton != null)
                worldDeleteButton.interactable = visible;
        }

        void RefreshServerList()
        {
            if (refreshServersRoutine != null)
                StopCoroutine(refreshServersRoutine);

            refreshServersRoutine = StartCoroutine(RefreshServerListRoutine());
        }

        IEnumerator RefreshServerListRoutine()
        {
            discoveredServers.Clear();
            publicServers.Clear();
            publicServerKeys.Clear();
            ClearServerListUi();
            SetNetworkStatus("Updating server list...");

            string fetchError = null;
            yield return PublicServerList.Fetch(
                publicServersUrl,
                list =>
                {
                    publicServers.Clear();
                    publicServerKeys.Clear();
                    if (list == null)
                        return;

                    for (int i = 0; i < list.Count; i++)
                    {
                        PublicServerEntry entry = list[i];
                        publicServers.Add(entry);
                        if (PublicServerList.TryParseAddress(entry.address, out string host, out ushort port))
                            publicServerKeys.Add(MakeServerKey(host, port));
                    }
                },
                error => fetchError = error);

            RebuildServerListUi();

            if (!string.IsNullOrEmpty(fetchError))
                SetNetworkStatus($"Public list unavailable ({fetchError}). Searching LAN...");
            else if (publicServers.Count == 0)
                SetNetworkStatus("No public servers. Searching LAN...");
            else
                SetNetworkStatus($"Loaded {publicServers.Count} public server(s). Searching LAN...");

            if (networkDiscovery == null)
            {
                if (string.IsNullOrEmpty(fetchError))
                    SetNetworkStatus(BuildServerCountStatus());
                else
                    SetNetworkStatus($"Public list unavailable ({fetchError}). LAN discovery is not configured.");
                refreshServersRoutine = null;
                yield break;
            }

            networkDiscovery.StopDiscovery();
            networkDiscovery.StartDiscovery();
            refreshServersRoutine = null;
        }

        void SetNetworkStatus(string message)
        {
            if (networkStatusText != null)
                networkStatusText.text = message;
        }

        void OnNetworkBackClicked()
        {
            if (refreshServersRoutine != null)
            {
                StopCoroutine(refreshServersRoutine);
                refreshServersRoutine = null;
            }

            networkDiscovery?.StopDiscovery();
            ShowMainPanel();
        }

        void OnExitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnServerFound(KaplaoServerResponse response)
        {
            if (response.uri != null &&
                publicServerKeys.Contains(MakeServerKey(response.uri.Host, (ushort)response.uri.Port)))
            {
                // Already shown as a public entry — keep public above, skip LAN duplicate.
                return;
            }

            discoveredServers[response.serverId] = response;
            RebuildServerListUi();
        }

        void ClearServerListUi()
        {
            if (serverListContent == null)
                return;

            for (int i = serverListContent.childCount - 1; i >= 0; i--)
            {
                Transform child = serverListContent.GetChild(i);
                if (serverEntryTemplate != null && child.gameObject == serverEntryTemplate)
                    continue;

                Destroy(child.gameObject);
            }
        }

        void RebuildServerListUi()
        {
            ClearServerListUi();

            for (int i = 0; i < publicServers.Count; i++)
                CreatePublicServerEntry(publicServers[i]);

            foreach (KaplaoServerResponse response in discoveredServers.Values)
                CreateLanServerEntry(response);

            SetNetworkStatus(BuildServerCountStatus());
        }

        string BuildServerCountStatus()
        {
            int total = publicServers.Count + discoveredServers.Count;
            if (total == 0)
                return "No servers found yet...";

            if (publicServers.Count > 0 && discoveredServers.Count > 0)
                return $"Found {total} server(s) ({publicServers.Count} public, {discoveredServers.Count} LAN)";

            if (publicServers.Count > 0)
                return $"Found {publicServers.Count} public server(s)";

            return $"Found {discoveredServers.Count} server(s)";
        }

        void CreatePublicServerEntry(PublicServerEntry entry)
        {
            if (serverEntryTemplate == null || serverListContent == null)
                return;

            GameObject uiEntry = Instantiate(serverEntryTemplate, serverListContent);
            uiEntry.SetActive(true);

            TextMeshProUGUI text = uiEntry.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
                text.text = $"{entry.name}\n{entry.address}";

            Button button = uiEntry.GetComponent<Button>();
            if (button == null)
                return;

            string capturedName = entry.name;
            string capturedAddress = entry.address;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => ConnectToPublicServer(capturedName, capturedAddress));
        }

        void CreateLanServerEntry(KaplaoServerResponse response)
        {
            if (serverEntryTemplate == null || serverListContent == null)
                return;

            GameObject entry = Instantiate(serverEntryTemplate, serverListContent);
            entry.SetActive(true);

            TextMeshProUGUI text = entry.GetComponentInChildren<TextMeshProUGUI>();
            string serverName = string.IsNullOrWhiteSpace(response.serverName)
                ? "Unknown Server"
                : response.serverName;
            string address = response.uri != null
                ? $"{response.uri.Host}:{response.uri.Port}"
                : string.Empty;
            if (text != null)
                text.text = $"{serverName}\n{address}";

            Button button = entry.GetComponent<Button>();
            if (button == null)
                return;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => ConnectToLanServer(response));
        }

        void ConnectToPublicServer(string serverName, string address)
        {
            if (!PublicServerList.TryParseAddress(address, out string host, out ushort port))
            {
                SetNetworkStatus($"Invalid server address: {address}");
                return;
            }

            networkDiscovery?.StopDiscovery();
            NetworkTransportConfig.Instance?.ApplyFromSession();
            SetNetworkStatus($"Connecting to {serverName} ({host}:{port})...");
            networkManager.StartDirectClient(host, port);
        }

        void ConnectToLanServer(KaplaoServerResponse response)
        {
            networkDiscovery?.StopDiscovery();
            NetworkTransportConfig.Instance?.ApplyFromSession();
            SetNetworkStatus($"Connecting to {response.serverName}...");
            networkManager.StartLanClient(response.uri);
        }

        static string MakeServerKey(string host, ushort port)
        {
            return $"{host.Trim().ToLowerInvariant()}:{port}";
        }

        void OnConnectDirectlyClicked()
        {
            string raw = ipInput != null ? ipInput.text : string.Empty;
            if (string.IsNullOrWhiteSpace(raw))
            {
                SetNetworkStatus("Enter IP (or IP:Port).");
                return;
            }

            raw = raw.Trim();
            string host = raw;
            ushort port = PlayerSession.Port;

            int colon = raw.LastIndexOf(':');
            if (colon > 0 && colon < raw.Length - 1)
            {
                host = raw.Substring(0, colon).Trim();
                string portText = raw.Substring(colon + 1).Trim();
                if (ushort.TryParse(portText, out ushort parsedPort))
                    port = parsedPort;
            }

            networkDiscovery?.StopDiscovery();
            NetworkTransportConfig.Instance?.ApplyFromSession();
            SetNetworkStatus($"Connecting to {host}:{port} ({(PlayerSession.UseTcpTransport ? "TCP" : "KCP")})...");
            networkManager.StartDirectClient(host, port);
        }
    }
}
