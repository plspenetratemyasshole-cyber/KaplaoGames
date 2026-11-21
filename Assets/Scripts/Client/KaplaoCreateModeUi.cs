using System.Collections;
using System.Collections.Generic;
using KaplaoGames.LevelScript;
using KaplaoGames.Network;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using KaplaoGames.UI;
using KaplaoGames.UI.Animation;
using KaplaoGames.UI.Theme;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Create mode HUD: mode switcher, selection toolbar, set-group panel, and tile settings panel.
    /// </summary>
    [DisallowMultipleComponent]
    public class KaplaoCreateModeUi : MonoBehaviour
    {
        [Header("Mode buttons")]
        [SerializeField] Button usualModeButton;
        [SerializeField] Button createModeButton;
        [SerializeField] Button codeModeButton;

        [Header("Selection toolbar")]
        [SerializeField] GameObject selectionToolbarRoot;
        [SerializeField] Button setGroupButton;
        [SerializeField] Button setTextureButton;
        [SerializeField] Button viewSoundsButton;
        [SerializeField] Button editParticlesButton;
        [SerializeField] Button editKeyframeButton;
        [SerializeField] Button editExecutorButton;
        [SerializeField] Button settingsButton;
        [SerializeField] Button linkButton;
        [SerializeField] Button unlinkButton;
        [SerializeField] Button deselectButton;
        [SerializeField] Button deleteSelectionButton;

        [Header("Set group panel")]
        [SerializeField] GameObject setGroupPanelRoot;
        [SerializeField] TMP_InputField groupInputField;
        [SerializeField] Button nextFreeGroupButton;
        [SerializeField] Button prevGroupButton;
        [SerializeField] Button nextGroupButton;
        [FormerlySerializedAs("setGroupConfirmButton")]
        [SerializeField] Button addGroupButton;
        [FormerlySerializedAs("setGroupCancelButton")]
        [SerializeField] Button setGroupCloseButton;
        [SerializeField] RectTransform groupsGridRoot;

        [Header("Editor layer switcher")]
        [SerializeField] Button editorLayerPrevButton;
        [SerializeField] Button editorLayerNextButton;
        [SerializeField] TextMeshProUGUI editorLayerLabel;

        [Header("Settings panel")]
        [SerializeField] GameObject settingsPanelRoot;
        [SerializeField] TMP_InputField transparencyInputField;
        [SerializeField] Toggle noHitboxToggle;
        [SerializeField] Toggle spawnPointToggle;
        [SerializeField] TextMeshProUGUI createdAtLabel;
        [SerializeField] TMP_InputField posXInputField;
        [SerializeField] TMP_InputField posYInputField;
        [SerializeField] TMP_InputField rotationInputField;
        [SerializeField] TMP_InputField scaleXInputField;
        [SerializeField] TMP_InputField scaleYInputField;
        [SerializeField] TMP_InputField sortingOrderInputField;
        [SerializeField] TMP_InputField editorLayerInputField;
        [SerializeField] Button applyTransformButton;
        [SerializeField] Button settingsCloseButton;

        [Header("Create settings panel")]
        [SerializeField] Button createSettingsButton;
        [SerializeField] GameObject createSettingsPanelRoot;
        [SerializeField] TMP_InputField rotationSnapInputField;
        [SerializeField] TMP_InputField positionSnapInputField;
        [SerializeField] TMP_InputField scaleSnapInputField;
        [SerializeField] Toggle showGridToggle;
        [SerializeField] Toggle showHitboxesToggle;
        [SerializeField] Toggle showPlayersToggle;
        [SerializeField] Toggle hideCursorToggle;
        [SerializeField] Toggle hitboxTrailToggle;
        [SerializeField] TMP_InputField minZoomInputField;
        [SerializeField] TMP_InputField maxZoomInputField;
        [SerializeField] Button createSettingsSaveButton;
        [SerializeField] Button createSettingsCloseButton;

        [Header("HUD")]
        [SerializeField] TextMeshProUGUI cursorCoordinatesLabel;

        [Header("Visibility")]
        [SerializeField] GameObject kaplaoOnlyRoot;

        KaplaoCursorClient cursorClient;
        KaplaoCursorServer cursorServer;
        KaplaoTileTextureUi tileTextureUi;
        KaplaoSoundPickerUi soundPickerUi;
        NetworkPlayer localPlayer;
        bool suppressFieldCallbacks;
        bool dirtyTransformPosX;
        bool dirtyTransformPosY;
        bool dirtyTransformRotation;
        bool dirtyTransformScaleX;
        bool dirtyTransformScaleY;
        bool dirtyTransformSortingOrder;
        readonly List<int> selectedGroupsBuffer = new();
        readonly HashSet<int> groupsOnAllTilesBuffer = new();
        readonly List<Button> groupChipButtons = new();
        float nextGroupGridRefreshTime;

        void Awake()
        {
            if (kaplaoOnlyRoot == null)
                kaplaoOnlyRoot = gameObject;

            ResolveSetGroupPanelRefs();

            if (sortingOrderInputField == null && settingsPanelRoot != null)
            {
                Transform existing = settingsPanelRoot.transform.Find("SortingOrderInputField");
                if (existing != null)
                    sortingOrderInputField = existing.GetComponent<TMP_InputField>();
            }

            if (editorLayerInputField == null && settingsPanelRoot != null)
            {
                Transform existing = settingsPanelRoot.transform.Find("EditorLayerInputField");
                if (existing != null)
                    editorLayerInputField = existing.GetComponent<TMP_InputField>();
            }

            if (deselectButton == null && selectionToolbarRoot != null)
            {
                Transform existing = selectionToolbarRoot.transform.Find("DeselectButton");
                if (existing != null)
                    deselectButton = existing.GetComponent<Button>();
            }

            if (createSettingsPanelRoot != null)
            {
                if (showGridToggle == null)
                {
                    Transform t = createSettingsPanelRoot.transform.Find("ShowGridToggle");
                    if (t != null) showGridToggle = t.GetComponent<Toggle>();
                }
                if (showHitboxesToggle == null)
                {
                    Transform t = createSettingsPanelRoot.transform.Find("ShowHitboxesToggle");
                    if (t != null) showHitboxesToggle = t.GetComponent<Toggle>();
                }
                if (showPlayersToggle == null)
                {
                    Transform t = createSettingsPanelRoot.transform.Find("ShowPlayersToggle");
                    if (t != null) showPlayersToggle = t.GetComponent<Toggle>();
                }
                if (hideCursorToggle == null)
                {
                    Transform t = createSettingsPanelRoot.transform.Find("HideCursorToggle");
                    if (t != null) hideCursorToggle = t.GetComponent<Toggle>();
                }
                if (hitboxTrailToggle == null)
                {
                    Transform t = createSettingsPanelRoot.transform.Find("HitboxTrailToggle");
                    if (t != null)
                        hitboxTrailToggle = t.GetComponent<Toggle>();
                    else
                        hitboxTrailToggle = EnsureHitboxTrailToggle();
                }
                if (minZoomInputField == null)
                {
                    Transform t = createSettingsPanelRoot.transform.Find("MinZoomInputField");
                    if (t != null) minZoomInputField = t.GetComponent<TMP_InputField>();
                }
                if (maxZoomInputField == null)
                {
                    Transform t = createSettingsPanelRoot.transform.Find("MaxZoomInputField");
                    if (t != null) maxZoomInputField = t.GetComponent<TMP_InputField>();
                }
            }

            if (GetComponent<CreateModeSelectionHud>() == null &&
                FindFirstObjectByType<CreateModeSelectionHud>() == null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas != null)
                    canvas.gameObject.AddComponent<CreateModeSelectionHud>();
            }

            EnsureDecimalNormalizers();
            EnsureDismissOverlay();

            if (cursorCoordinatesLabel != null)
            {
                Image pillBg = cursorCoordinatesLabel.GetComponent<Image>();
                if (pillBg == null)
                {
                    pillBg = cursorCoordinatesLabel.gameObject.AddComponent<Image>();
                    pillBg.sprite = KaplaoUiSprites.Pill();
                    pillBg.type = Image.Type.Sliced;
                    pillBg.color = new Color(0.06f, 0.09f, 0.16f, 0.65f);
                    pillBg.raycastTarget = false;
                }
            }
        }

        void OnEnable()
        {
            WireButtons();
            HideSetGroupPanel();
            HideSettingsPanel();
            HideCreateSettingsPanel();
            SetSelectionToolbarVisible(false);
            StartCoroutine(WatchLocalKaplao());
        }

        void OnDisable()
        {
            UnwireButtons();
            UnsubscribeCursor();
        }

        void WireButtons()
        {
            if (usualModeButton != null) usualModeButton.onClick.AddListener(() => RequestMode(HostGameMode.Usual));
            if (createModeButton != null) createModeButton.onClick.AddListener(() => RequestMode(HostGameMode.Create));
            if (codeModeButton != null)
                codeModeButton.onClick.AddListener(() => RequestMode(HostGameMode.Code));

            if (setGroupButton != null) setGroupButton.onClick.AddListener(ToggleSetGroupPanel);
            if (setTextureButton != null) setTextureButton.onClick.AddListener(ToggleTexturePanel);
            if (viewSoundsButton != null) viewSoundsButton.onClick.AddListener(ToggleSoundPanel);
            if (editParticlesButton != null) editParticlesButton.onClick.AddListener(OpenParticleEditor);
            if (editKeyframeButton != null) editKeyframeButton.onClick.AddListener(OpenKeyframeEditor);
            if (editExecutorButton != null) editExecutorButton.onClick.AddListener(OpenExecutorEditor);
            if (settingsButton != null) settingsButton.onClick.AddListener(ToggleSettingsPanel);
            if (linkButton != null) linkButton.onClick.AddListener(LinkSelection);
            if (unlinkButton != null) unlinkButton.onClick.AddListener(UnlinkSelection);
            if (editorLayerPrevButton != null) editorLayerPrevButton.onClick.AddListener(() => StepEditorLayer(-1));
            if (editorLayerNextButton != null) editorLayerNextButton.onClick.AddListener(() => StepEditorLayer(1));
            if (deselectButton != null) deselectButton.onClick.AddListener(DeselectSelection);
            if (createSettingsButton != null) createSettingsButton.onClick.AddListener(ToggleCreateSettingsPanel);
            if (deleteSelectionButton != null) deleteSelectionButton.onClick.AddListener(DeleteSelection);
            if (nextFreeGroupButton != null) nextFreeGroupButton.onClick.AddListener(OnNextFreeGroupClicked);
            if (prevGroupButton != null) prevGroupButton.onClick.AddListener(() => StepGroupInput(-1));
            if (nextGroupButton != null) nextGroupButton.onClick.AddListener(() => StepGroupInput(1));
            if (addGroupButton != null) addGroupButton.onClick.AddListener(AddGroupFromInput);
            if (setGroupCloseButton != null) setGroupCloseButton.onClick.AddListener(HideSetGroupPanel);
            if (settingsCloseButton != null) settingsCloseButton.onClick.AddListener(HideSettingsPanel);
            if (createSettingsSaveButton != null) createSettingsSaveButton.onClick.AddListener(SaveCreateSettings);
            if (createSettingsCloseButton != null) createSettingsCloseButton.onClick.AddListener(HideCreateSettingsPanel);
            if (applyTransformButton != null) applyTransformButton.gameObject.SetActive(false);

            if (transparencyInputField != null) transparencyInputField.onEndEdit.AddListener(OnTransparencyInputCommitted);
            if (editorLayerInputField != null) editorLayerInputField.onEndEdit.AddListener(OnEditorLayerInputCommitted);
            if (posXInputField != null) posXInputField.onEndEdit.AddListener(OnPosXCommitted);
            if (posYInputField != null) posYInputField.onEndEdit.AddListener(OnPosYCommitted);
            if (rotationInputField != null) rotationInputField.onEndEdit.AddListener(OnRotationCommitted);
            if (scaleXInputField != null) scaleXInputField.onEndEdit.AddListener(OnScaleXCommitted);
            if (scaleYInputField != null) scaleYInputField.onEndEdit.AddListener(OnScaleYCommitted);
            if (sortingOrderInputField != null) sortingOrderInputField.onEndEdit.AddListener(OnSortingOrderCommitted);
            if (noHitboxToggle != null) noHitboxToggle.onValueChanged.AddListener(OnNoHitboxToggleChanged);
            if (spawnPointToggle != null) spawnPointToggle.onValueChanged.AddListener(OnSpawnPointToggleChanged);
        }

        void UnwireButtons()
        {
            if (usualModeButton != null) usualModeButton.onClick.RemoveAllListeners();
            if (createModeButton != null) createModeButton.onClick.RemoveAllListeners();
            if (codeModeButton != null) codeModeButton.onClick.RemoveAllListeners();
            if (setGroupButton != null) setGroupButton.onClick.RemoveAllListeners();
            if (setTextureButton != null) setTextureButton.onClick.RemoveAllListeners();
            if (viewSoundsButton != null) viewSoundsButton.onClick.RemoveAllListeners();
            if (editParticlesButton != null) editParticlesButton.onClick.RemoveAllListeners();
            if (editKeyframeButton != null) editKeyframeButton.onClick.RemoveAllListeners();
            if (editExecutorButton != null) editExecutorButton.onClick.RemoveAllListeners();
            if (settingsButton != null) settingsButton.onClick.RemoveAllListeners();
            if (linkButton != null) linkButton.onClick.RemoveAllListeners();
            if (unlinkButton != null) unlinkButton.onClick.RemoveAllListeners();
            if (editorLayerPrevButton != null) editorLayerPrevButton.onClick.RemoveAllListeners();
            if (editorLayerNextButton != null) editorLayerNextButton.onClick.RemoveAllListeners();
            if (deselectButton != null) deselectButton.onClick.RemoveAllListeners();
            if (deleteSelectionButton != null) deleteSelectionButton.onClick.RemoveAllListeners();
            if (nextFreeGroupButton != null) nextFreeGroupButton.onClick.RemoveAllListeners();
            if (prevGroupButton != null) prevGroupButton.onClick.RemoveAllListeners();
            if (nextGroupButton != null) nextGroupButton.onClick.RemoveAllListeners();
            if (addGroupButton != null) addGroupButton.onClick.RemoveAllListeners();
            if (setGroupCloseButton != null) setGroupCloseButton.onClick.RemoveAllListeners();
            if (settingsCloseButton != null) settingsCloseButton.onClick.RemoveAllListeners();
            if (createSettingsButton != null) createSettingsButton.onClick.RemoveAllListeners();
            if (createSettingsSaveButton != null) createSettingsSaveButton.onClick.RemoveAllListeners();
            if (createSettingsCloseButton != null) createSettingsCloseButton.onClick.RemoveAllListeners();
            if (applyTransformButton != null) applyTransformButton.onClick.RemoveAllListeners();

            if (transparencyInputField != null) transparencyInputField.onEndEdit.RemoveAllListeners();
            if (editorLayerInputField != null) editorLayerInputField.onEndEdit.RemoveAllListeners();
            if (posXInputField != null) posXInputField.onEndEdit.RemoveAllListeners();
            if (posYInputField != null) posYInputField.onEndEdit.RemoveAllListeners();
            if (rotationInputField != null) rotationInputField.onEndEdit.RemoveAllListeners();
            if (scaleXInputField != null) scaleXInputField.onEndEdit.RemoveAllListeners();
            if (scaleYInputField != null) scaleYInputField.onEndEdit.RemoveAllListeners();
            if (sortingOrderInputField != null) sortingOrderInputField.onEndEdit.RemoveAllListeners();
            if (noHitboxToggle != null) noHitboxToggle.onValueChanged.RemoveAllListeners();
            if (spawnPointToggle != null) spawnPointToggle.onValueChanged.RemoveAllListeners();
        }

        IEnumerator WatchLocalKaplao()
        {
            while (enabled)
            {
                NetworkIdentity localIdentity = NetworkClient.localPlayer;
                if (localIdentity == null)
                {
                    ClearCursorBinding();
                    yield return null;
                    continue;
                }

                if (!localIdentity.TryGetComponent(out NetworkPlayer networkPlayer))
                {
                    ClearCursorBinding();
                    yield return null;
                    continue;
                }

                if (localPlayer != networkPlayer)
                {
                    if (localPlayer != null)
                        localPlayer.RoleChanged -= OnLocalRoleChanged;

                    localPlayer = networkPlayer;
                    localPlayer.RoleChanged += OnLocalRoleChanged;
                }

                if (networkPlayer.role != PlayerRole.Kaplao)
                {
                    if (cursorClient != null || cursorServer != null)
                        ClearCursorBindingKeepPlayerSubscription();
                    yield return null;
                    continue;
                }

                KaplaoCursorClient nextClient = localIdentity.GetComponent<KaplaoCursorClient>();
                KaplaoCursorServer nextServer = localIdentity.GetComponent<KaplaoCursorServer>();
                if (nextClient == null || nextServer == null)
                {
                    yield return null;
                    continue;
                }

                if (cursorClient != nextClient || cursorServer != nextServer)
                {
                    ClearCursorBindingKeepPlayerSubscription();
                    cursorClient = nextClient;
                    cursorServer = nextServer;
                    tileTextureUi = FindFirstObjectByType<KaplaoTileTextureUi>();
                    soundPickerUi = FindFirstObjectByType<KaplaoSoundPickerUi>();
                    SubscribeCursor();
                    HostGameMode mode = KaplaoLocalGameModePredictor.GetEffective(cursorServer);
                    RefreshModeButtons(mode);
                    RefreshSelectionToolbar();
                    RefreshEditorLayerLabel();
                    SetEditorLayerControlsEnabled(mode == HostGameMode.Create);
                }

                yield return null;
            }
        }

        void OnLocalRoleChanged(PlayerRole role)
        {
            // WatchLocalKaplao rebinds every frame; this only updates UI immediately.
            if (role != PlayerRole.Kaplao)
            {
                ClearCursorBindingKeepPlayerSubscription();
                SetSelectionToolbarVisible(false);
            }
        }

        void SubscribeCursor()
        {
            if (cursorServer == null)
                return;

            CreateModeSnapSettings.LoadLocal();
            CreateModeEditorSettings.LoadLocal();
            CreateModeEditorSettings.NotifyChanged();
            cursorClient?.PushLocalSnapSettingsToServer();
            cursorClient?.PushLocalHideCursorToServer();

            cursorServer.SelectionChanged += RefreshSelectionToolbar;
            cursorServer.HostGameModeChanged += OnHostGameModeChanged;
            cursorServer.ActiveEditorLayerChanged += RefreshEditorLayerLabel;
            KaplaoLocalGameModePredictor.Changed += OnPredictedGameModeChanged;
        }

        void ClearCursorBindingKeepPlayerSubscription()
        {
            if (cursorServer != null)
            {
                cursorServer.SelectionChanged -= RefreshSelectionToolbar;
                cursorServer.HostGameModeChanged -= OnHostGameModeChanged;
                cursorServer.ActiveEditorLayerChanged -= RefreshEditorLayerLabel;
            }

            KaplaoLocalGameModePredictor.Changed -= OnPredictedGameModeChanged;

            cursorServer = null;
            cursorClient = null;
        }

        void ClearCursorBinding()
        {
            ClearCursorBindingKeepPlayerSubscription();
            if (localPlayer != null)
            {
                localPlayer.RoleChanged -= OnLocalRoleChanged;
                localPlayer = null;
            }
        }

        void UnsubscribeCursor()
        {
            ClearCursorBinding();
        }

        KaplaoCursorClient ActiveCursorClient()
        {
            return KaplaoLocalBindingUtility.ResolveLocalCursorClient() ?? cursorClient;
        }

        void OnHostGameModeChanged(HostGameMode mode)
        {
            HostGameMode effective = KaplaoLocalGameModePredictor.GetEffective(cursorServer);
            RefreshModeButtons(effective);
            RefreshSelectionToolbar();
            SetEditorLayerControlsEnabled(effective == HostGameMode.Create);
            if (effective != HostGameMode.Create)
                HideSelectionPanels();
        }

        void OnPredictedGameModeChanged(HostGameMode mode)
        {
            RefreshModeButtons(mode);
            RefreshSelectionToolbar();
            SetEditorLayerControlsEnabled(mode == HostGameMode.Create);
            if (mode != HostGameMode.Create)
                HideSelectionPanels();
        }

        void RefreshEditorLayerLabel()
        {
            if (editorLayerLabel == null || cursorServer == null)
                return;

            string newText = cursorServer.GetActiveEditorLayerLabel();
            if (editorLayerLabel.text != newText)
            {
                editorLayerLabel.text = newText;
                KaplaoValuePunch punch = editorLayerLabel.GetComponent<KaplaoValuePunch>() ??
                                         editorLayerLabel.gameObject.AddComponent<KaplaoValuePunch>();
                punch.Punch();
            }
        }

        void SetEditorLayerControlsEnabled(bool enabled)
        {
            if (editorLayerPrevButton != null)
            {
                editorLayerPrevButton.interactable = enabled;
                editorLayerPrevButton.gameObject.SetActive(enabled);
            }

            if (editorLayerNextButton != null)
            {
                editorLayerNextButton.interactable = enabled;
                editorLayerNextButton.gameObject.SetActive(enabled);
            }

            if (editorLayerLabel != null)
                editorLayerLabel.gameObject.SetActive(enabled);
        }

        void StepEditorLayer(int delta)
        {
            cursorClient?.CmdStepEditorLayer(delta);
            RefreshEditorLayerLabel();
        }

        void Update()
        {
            UpdateDismissOverlay();

            if (IsSetGroupPanelVisible() && Time.unscaledTime >= nextGroupGridRefreshTime)
            {
                nextGroupGridRefreshTime = Time.unscaledTime + 0.2f;
                RefreshGroupChips();
            }

            if (cursorCoordinatesLabel == null || cursorServer == null)
                return;

            KaplaoCursorClient client = ActiveCursorClient();
            if (client == null)
                return;

            if (KaplaoExecutorEditorUi.BlocksCreateHud)
            {
                cursorCoordinatesLabel.gameObject.SetActive(false);
                return;
            }

            cursorCoordinatesLabel.gameObject.SetActive(true);
            Vector2 p = client.LocalWorldPosition;
            cursorCoordinatesLabel.text = $"X: {p.x:F2}  Y: {p.y:F2}";
        }

        void RequestMode(HostGameMode mode)
        {
            KaplaoCursorClient.EnsureClientReadyForCommands();
            KaplaoCursorClient client = ActiveCursorClient();
            if (client == null)
                return;

            client.RequestSetHostGameMode(mode);
            RefreshModeButtons(mode);
            SetEditorLayerControlsEnabled(mode == HostGameMode.Create);
            RefreshSelectionToolbar();
        }

        void RefreshModeButtons(HostGameMode mode)
        {
            KaplaoToggleButtonStyle.Apply(usualModeButton, mode == HostGameMode.Usual);
            KaplaoToggleButtonStyle.Apply(createModeButton, mode == HostGameMode.Create);
            KaplaoToggleButtonStyle.Apply(codeModeButton, mode == HostGameMode.Code);
            RefreshToolbarToggleColors();
        }

        void RefreshToolbarToggleColors()
        {
            bool textureOpen = tileTextureUi != null && tileTextureUi.IsOpen;
            bool soundOpen = soundPickerUi != null && soundPickerUi.IsOpen;

            KaplaoToggleButtonStyle.Apply(setGroupButton, IsSetGroupPanelVisible());
            KaplaoToggleButtonStyle.Apply(setTextureButton, textureOpen);
            KaplaoToggleButtonStyle.Apply(viewSoundsButton, soundOpen);
            KaplaoToggleButtonStyle.Apply(settingsButton, IsSettingsPanelVisible());
            KaplaoToggleButtonStyle.Apply(editParticlesButton, false);
            KaplaoToggleButtonStyle.Apply(editKeyframeButton, false);
            KaplaoToggleButtonStyle.Apply(editExecutorButton, false);
            KaplaoToggleButtonStyle.Apply(linkButton, false);
            KaplaoToggleButtonStyle.Apply(unlinkButton, false);
            KaplaoToggleButtonStyle.Apply(deselectButton, false);
            KaplaoToggleButtonStyle.Apply(deleteSelectionButton, false);
            KaplaoToggleButtonStyle.Apply(createSettingsButton, IsCreateSettingsPanelVisible());
        }

        void RefreshSelectionToolbar()
        {
            if (KaplaoExecutorEditorUi.BlocksCreateHud)
            {
                SetSelectionToolbarVisible(false);
                return;
            }

            bool show = cursorServer != null &&
                        KaplaoLocalGameModePredictor.IsCreate(cursorServer) &&
                        cursorServer.SelectedCount > 0;

            SetSelectionToolbarVisible(show);

            if (!show)
            {
                HideSelectionPanels();
                return;
            }

            if (editParticlesButton != null)
                editParticlesButton.gameObject.SetActive(HasAnySelectedParticleEmitters());

            if (editKeyframeButton != null)
                editKeyframeButton.gameObject.SetActive(HasAnySelectedKeyframes());

            if (editExecutorButton != null)
                editExecutorButton.gameObject.SetActive(HasAnySelectedExecutors());

            if (IsSetGroupPanelVisible())
                RefreshGroupChips();

            if (IsSettingsPanelVisible())
                RefreshSettingsToggles();

            RefreshToolbarToggleColors();
        }

        bool HasAnySelectedParticleEmitters()
        {
            if (cursorServer == null || cursorServer.SelectedTiles == null)
                return false;

            for (int i = 0; i < cursorServer.SelectedTiles.Count; i++)
            {
                if (!LevelScriptObjectUtility.TryResolveTile(cursorServer.SelectedTiles[i], out LevelTileServer tile) || tile == null)
                    continue;

                if (tile.TileType != TileType.Particle)
                    continue;

                if (!tile.TryGetComponent(out ParticleEmitterTile _))
                    continue;

                return true;
            }

            return false;
        }

        bool HasAnySelectedKeyframes()
        {
            if (cursorServer == null || cursorServer.SelectedTiles == null)
                return false;

            for (int i = 0; i < cursorServer.SelectedTiles.Count; i++)
            {
                if (!LevelScriptObjectUtility.TryResolveTile(cursorServer.SelectedTiles[i], out LevelTileServer tile) || tile == null)
                    continue;

                if (tile.TileType == TileType.Keyframe)
                    return true;
            }

            return false;
        }

        bool HasAnySelectedExecutors()
        {
            if (cursorServer == null || cursorServer.SelectedTiles == null)
                return false;

            for (int i = 0; i < cursorServer.SelectedTiles.Count; i++)
            {
                if (!LevelScriptObjectUtility.TryResolveTile(cursorServer.SelectedTiles[i], out LevelTileServer tile) || tile == null)
                    continue;

                if (tile.TileType == TileType.Executor)
                    return true;
            }

            return false;
        }

        void OpenExecutorEditor()
        {
            Debug.Log("[Kaplao] OpenExecutorEditor clicked");
            KaplaoExecutorEditorUi editor = FindFirstObjectByType<KaplaoExecutorEditorUi>(FindObjectsInactive.Include);
            if (editor != null)
            {
                Debug.Log($"[Kaplao] Executor editor component found. IsOpen={editor.IsOpen}");
                if (editor.IsOpen) { editor.Hide(); return; }
                editor.ShowForSelection();
                return;
            }

            // Minimal fallback: just toggle the panel GameObject.
            // This makes the button work even if the UI component is missing/miswired.
            GameObject panelGo = null;
            try
            {
                // Include inactive objects so we can still find the panel if it's currently hidden.
                var allTransforms = UnityEngine.Object.FindObjectsOfType<Transform>(true);
                for (int i = 0; i < allTransforms.Length; i++)
                {
                    Transform t = allTransforms[i];
                    if (t != null && t.name == "ExecutorEditorPanel")
                    {
                        panelGo = t.gameObject;
                        break;
                    }
                }
            }
            catch
            {
                // Ignore and fall back to Find.
            }

            if (panelGo == null)
                panelGo = GameObject.Find("ExecutorEditorPanel");

            if (panelGo != null)
            {
                bool next = !panelGo.activeSelf;
                Debug.Log($"[Kaplao] ExecutorEditorPanel found. activeSelf={panelGo.activeSelf} -> {next}");
                panelGo.SetActive(next);
                return;
            }

            Debug.LogWarning("[Kaplao] ExecutorEditorPanel not found (neither component nor GameObject).");
        }

        void OpenKeyframeEditor()
        {
            KaplaoKeyframeEditorUi editor = FindFirstObjectByType<KaplaoKeyframeEditorUi>(FindObjectsInactive.Include);
            if (editor == null)
            {
                Debug.LogWarning("[Kaplao] KaplaoKeyframeEditorUi not found. Run Kaplao/Setup Keyframes UI.");
                return;
            }

            if (editor.IsOpen) { editor.Hide(); return; }
            editor.ShowForSelection();
        }

        void OpenParticleEditor()
        {
            KaplaoCursorClient.EnsureClientReadyForCommands();
            KaplaoParticleEditorUi editor = FindFirstObjectByType<KaplaoParticleEditorUi>(FindObjectsInactive.Include);
            if (editor == null)
            {
                Debug.LogWarning("[Kaplao] KaplaoParticleEditorUi not found on Canvas. Run Kaplao/Setup Particle Editor UI.");
                return;
            }

            if (editor.IsOpen)
            {
                editor.Hide();
                return;
            }

            editor.ShowForSelection();
        }

        void SetSelectionToolbarVisible(bool visible)
        {
            if (selectionToolbarRoot == null) return;
            if (visible)
            {
                if (!selectionToolbarRoot.activeSelf)
                    KaplaoUiAnimator.ShowPanel(selectionToolbarRoot, PanelAnimStyle.SlideFromBottom, 0.18f);
            }
            else
            {
                if (selectionToolbarRoot.activeSelf)
                    KaplaoUiAnimator.HidePanel(selectionToolbarRoot, PanelAnimStyle.Fade, 0.12f);
            }
        }

        void ToggleSetGroupPanel()
        {
            if (IsSetGroupPanelVisible())
            {
                HideSetGroupPanel();
                return;
            }

            OpenSetGroupPanel();
        }

        bool IsSetGroupPanelVisible()
        {
            return setGroupPanelRoot != null && setGroupPanelRoot.activeSelf;
        }

        void OpenSetGroupPanel()
        {
            HideSettingsPanel();
            ResolveSetGroupPanelRefs();

            if (cursorServer == null)
                return;

            if (setGroupPanelRoot != null && !setGroupPanelRoot.activeSelf)
                KaplaoUiAnimator.ShowPanel(setGroupPanelRoot, PanelAnimStyle.PopScale, 0.2f);

            if (groupInputField != null && string.IsNullOrWhiteSpace(groupInputField.text))
                groupInputField.text = "1";

            RefreshGroupChips();
            RefreshToolbarToggleColors();
        }

        void HideSetGroupPanel()
        {
            if (setGroupPanelRoot != null && setGroupPanelRoot.activeSelf)
                KaplaoUiAnimator.HidePanel(setGroupPanelRoot, PanelAnimStyle.Fade, 0.14f);
            RefreshToolbarToggleColors();
        }

        void ResolveSetGroupPanelRefs()
        {
            if (setGroupPanelRoot == null)
                return;

            Transform root = setGroupPanelRoot.transform;
            if (groupInputField == null)
            {
                Transform t = root.Find("GroupInputField");
                if (t != null) groupInputField = t.GetComponent<TMP_InputField>();
            }

            if (nextFreeGroupButton == null)
            {
                Transform t = root.Find("NextFreeGroupButton");
                if (t != null) nextFreeGroupButton = t.GetComponent<Button>();
            }

            if (prevGroupButton == null)
            {
                Transform t = root.Find("PrevGroupButton");
                if (t != null) prevGroupButton = t.GetComponent<Button>();
            }

            if (nextGroupButton == null)
            {
                Transform t = root.Find("NextGroupButton");
                if (t != null) nextGroupButton = t.GetComponent<Button>();
            }

            if (addGroupButton == null)
            {
                Transform t = root.Find("AddGroupButton");
                if (t == null) t = root.Find("SetGroupConfirmButton");
                if (t != null) addGroupButton = t.GetComponent<Button>();
            }

            if (setGroupCloseButton == null)
            {
                Transform t = root.Find("SetGroupCloseButton");
                if (t == null) t = root.Find("SetGroupCancelButton");
                if (t != null) setGroupCloseButton = t.GetComponent<Button>();
            }

            if (groupsGridRoot == null)
            {
                Transform t = root.Find("GroupsGrid");
                if (t != null) groupsGridRoot = t as RectTransform;
            }
        }

        void OnNextFreeGroupClicked()
        {
            if (cursorServer == null || groupInputField == null)
                return;

            groupInputField.text = cursorServer.FindNextFreeGroupId().ToString();
        }

        void StepGroupInput(int delta)
        {
            if (groupInputField == null)
                return;

            int value = 1;
            if (!string.IsNullOrWhiteSpace(groupInputField.text))
                int.TryParse(groupInputField.text.Trim(), out value);

            value = Mathf.Max(0, value + delta);
            groupInputField.text = value.ToString();
        }

        void AddGroupFromInput()
        {
            KaplaoCursorClient client = ActiveCursorClient();
            if (client == null || groupInputField == null)
                return;

            if (!TileGroups.TryParseNumber(groupInputField.text, out int group))
                return;

            KaplaoCursorClient.EnsureClientReadyForCommands();
            client.RequestAddGroupToSelectedTiles(group);
            nextGroupGridRefreshTime = 0f;
            RefreshGroupChips();
        }

        void RemoveGroupFromSelection(int group)
        {
            KaplaoCursorClient client = ActiveCursorClient();
            if (client == null)
                return;

            KaplaoCursorClient.EnsureClientReadyForCommands();
            client.RequestRemoveGroupFromSelectedTiles(group);
            nextGroupGridRefreshTime = 0f;
            RefreshGroupChips();
        }

        void RefreshGroupChips()
        {
            if (groupsGridRoot == null || cursorServer == null)
                return;

            cursorServer.CollectSelectedGroupStates(selectedGroupsBuffer, groupsOnAllTilesBuffer);

            while (groupChipButtons.Count < selectedGroupsBuffer.Count)
                groupChipButtons.Add(CreateGroupChipButton());

            for (int i = 0; i < groupChipButtons.Count; i++)
            {
                Button button = groupChipButtons[i];
                if (button == null)
                    continue;

                bool active = i < selectedGroupsBuffer.Count;
                button.gameObject.SetActive(active);
                if (!active)
                    continue;

                int group = selectedGroupsBuffer[i];
                bool onAll = groupsOnAllTilesBuffer.Contains(group);
                TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                    label.text = group.ToString();

                Image image = button.targetGraphic as Image;
                if (image != null)
                {
                    image.color = onAll
                        ? new Color(0.22f, 0.55f, 0.9f, 0.95f)
                        : new Color(0.1f, 0.22f, 0.38f, 0.95f);
                }

                button.onClick.RemoveAllListeners();
                int captured = group;
                button.onClick.AddListener(() => RemoveGroupFromSelection(captured));
            }
        }

        Button CreateGroupChipButton()
        {
            GameObject buttonObject = new GameObject("GroupChip", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(groupsGridRoot, false);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(56f, 36f);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.22f, 0.55f, 0.9f, 0.95f);

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            GameObject labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(buttonObject.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 18f;
            label.color = Color.white;
            label.raycastTarget = false;

            return button;
        }

        void ConfirmSetGroup()
        {
            AddGroupFromInput();
        }

        void ToggleTexturePanel()
        {
            if (tileTextureUi == null)
                tileTextureUi = FindFirstObjectByType<KaplaoTileTextureUi>();

            if (tileTextureUi != null && tileTextureUi.IsOpen)
            {
                HideTexturePanel();
                return;
            }

            OpenTexturePanel();
        }

        void OpenTexturePanel()
        {
            HideSetGroupPanel();
            HideSettingsPanel();
            HideCreateSettingsPanel();

            if (tileTextureUi == null)
                tileTextureUi = FindFirstObjectByType<KaplaoTileTextureUi>();

            if (tileTextureUi == null)
            {
                Debug.LogWarning("[Kaplao] Texture picker UI not found. Run Kaplao/Setup Texture Picker UI.");
                return;
            }

            tileTextureUi.SetKaplaoOnlyRoot(kaplaoOnlyRoot);
            tileTextureUi.ShowPanel();
            RefreshToolbarToggleColors();
        }

        void HideTexturePanel()
        {
            if (tileTextureUi == null)
                tileTextureUi = FindFirstObjectByType<KaplaoTileTextureUi>();

            tileTextureUi?.HidePanel();
            RefreshToolbarToggleColors();
        }

        void HideSoundPanel()
        {
            if (soundPickerUi == null)
                soundPickerUi = FindFirstObjectByType<KaplaoSoundPickerUi>();

            soundPickerUi?.HidePanel();
            RefreshToolbarToggleColors();
        }

        void ToggleSoundPanel()
        {
            if (soundPickerUi == null)
                soundPickerUi = FindFirstObjectByType<KaplaoSoundPickerUi>();

            if (soundPickerUi != null && soundPickerUi.IsOpen)
            {
                HideSoundPanel();
                return;
            }

            OpenSoundPanel();
        }

        void OpenSoundPanel()
        {
            HideSetGroupPanel();
            HideSettingsPanel();
            HideCreateSettingsPanel();
            HideTexturePanel();

            if (soundPickerUi == null)
                soundPickerUi = FindFirstObjectByType<KaplaoSoundPickerUi>();

            if (soundPickerUi == null)
            {
                Debug.LogWarning("[Kaplao] Sound picker UI not found. Run Kaplao/Setup Sound Picker UI.");
                return;
            }

            soundPickerUi.SetKaplaoOnlyRoot(kaplaoOnlyRoot);
            soundPickerUi.ShowPanel();
            RefreshToolbarToggleColors();
        }

        void LinkSelection()
        {
            cursorClient?.CmdLinkSelectedTiles();
        }

        void UnlinkSelection()
        {
            cursorClient?.CmdUnlinkSelectedTiles();
        }

        void ToggleSettingsPanel()
        {
            if (IsSettingsPanelVisible())
            {
                HideSettingsPanel();
                return;
            }

            OpenSettingsPanel();
        }

        void OpenSettingsPanel()
        {
            HideSetGroupPanel();
            HideCreateSettingsPanel();

            if (cursorServer == null)
                return;

            if (settingsPanelRoot != null && !settingsPanelRoot.activeSelf)
                KaplaoUiAnimator.ShowPanel(settingsPanelRoot, PanelAnimStyle.PopScale, 0.2f);

            RefreshSettingsToggles();
            RefreshToolbarToggleColors();
        }

        void HideSettingsPanel()
        {
            if (IsSettingsPanelVisible())
            {
                CommitTransformInputFields();
                if (!suppressFieldCallbacks)
                    ApplyTransformPatchFromDirtyFields();
            }

            if (settingsPanelRoot != null && settingsPanelRoot.activeSelf)
                KaplaoUiAnimator.HidePanel(settingsPanelRoot, PanelAnimStyle.Fade, 0.14f);
            RefreshToolbarToggleColors();
        }

        void ToggleCreateSettingsPanel()
        {
            if (IsCreateSettingsPanelVisible())
            {
                HideCreateSettingsPanel();
                return;
            }

            OpenCreateSettingsPanel();
        }

        bool IsCreateSettingsPanelVisible()
        {
            return createSettingsPanelRoot != null && createSettingsPanelRoot.activeSelf;
        }

        void OpenCreateSettingsPanel()
        {
            HideSetGroupPanel();
            HideSettingsPanel();

            if (createSettingsPanelRoot != null && !createSettingsPanelRoot.activeSelf)
                KaplaoUiAnimator.ShowPanel(createSettingsPanelRoot, PanelAnimStyle.PopScale, 0.2f);

            CreateModeSnapSettings.LoadLocal();
            CreateModeEditorSettings.LoadLocal();

            if (rotationSnapInputField != null)
                rotationSnapInputField.text = CreateModeSnapSettings.RotationSnapDegrees.ToString("G");
            if (positionSnapInputField != null)
                positionSnapInputField.text = CreateModeSnapSettings.PositionSnapUnits.ToString("G");
            if (scaleSnapInputField != null)
                scaleSnapInputField.text = CreateModeSnapSettings.ScaleSnapUnits.ToString("G");
            if (showGridToggle != null)
                showGridToggle.isOn = CreateModeEditorSettings.ShowGrid;
            if (showHitboxesToggle != null)
                showHitboxesToggle.isOn = CreateModeEditorSettings.ShowHitboxes;
            if (showPlayersToggle != null)
                showPlayersToggle.isOn = CreateModeEditorSettings.ShowPlayers;
            if (hideCursorToggle != null)
                hideCursorToggle.isOn = cursorServer != null ? cursorServer.HideCursor : CreateModeEditorSettings.HideCursor;
            if (hitboxTrailToggle != null)
                hitboxTrailToggle.isOn = CreateModeEditorSettings.HitboxTrail;

            ResolveKaplaoZoomDefaults(out float minOrtho, out float maxOrtho);
            if (minZoomInputField != null)
                minZoomInputField.text = minOrtho.ToString("G");
            if (maxZoomInputField != null)
                maxZoomInputField.text = maxOrtho.ToString("G");

            RefreshToolbarToggleColors();
        }

        void HideCreateSettingsPanel()
        {
            if (createSettingsPanelRoot != null && createSettingsPanelRoot.activeSelf)
                KaplaoUiAnimator.HidePanel(createSettingsPanelRoot, PanelAnimStyle.Fade, 0.14f);
            RefreshToolbarToggleColors();
        }

        void SaveCreateSettings()
        {
            float rotation = ParseInput(rotationSnapInputField, CreateModeSnapSettings.DefaultRotationSnapDegrees);
            float position = ParseInput(positionSnapInputField, CreateModeSnapSettings.DefaultPositionSnapUnits);
            float scale = ParseInput(scaleSnapInputField, CreateModeSnapSettings.DefaultScaleSnapUnits);
            CreateModeSnapSettings.SaveLocal(rotation, position, scale);
            cursorClient?.CmdSetSnapSettings(rotation, position, scale);

            bool showGrid = showGridToggle == null || showGridToggle.isOn;
            bool showHitboxes = showHitboxesToggle == null || showHitboxesToggle.isOn;
            bool showPlayers = showPlayersToggle == null || showPlayersToggle.isOn;
            bool hideCursor = hideCursorToggle != null && hideCursorToggle.isOn;
            bool hitboxTrail = hitboxTrailToggle != null && hitboxTrailToggle.isOn;

            ResolveKaplaoZoomDefaults(out float fallbackMin, out float fallbackMax);
            float minOrtho = ParseInput(minZoomInputField, fallbackMin);
            float maxOrtho = ParseInput(maxZoomInputField, fallbackMax);
            if (maxOrtho < minOrtho)
                (minOrtho, maxOrtho) = (maxOrtho, minOrtho);

            CreateModeEditorSettings.SaveLocal(showGrid, showHitboxes, showPlayers, hideCursor, hitboxTrail, minOrtho, maxOrtho);
            if (cursorClient != null)
            {
                KaplaoCursorClient.EnsureClientReadyForCommands();
                cursorClient.CmdSetHideCursor(hideCursor);
            }

            GameCameraClient cameraClient = cursorClient != null
                ? cursorClient.GetComponent<GameCameraClient>()
                : FindFirstObjectByType<GameCameraClient>();
            cameraClient?.ClampToCurrentZoomLimits();

            HideCreateSettingsPanel();
        }

        Toggle EnsureHitboxTrailToggle()
        {
            if (createSettingsPanelRoot == null)
                return null;

            if (hideCursorToggle == null)
            {
                Transform hide = createSettingsPanelRoot.transform.Find("HideCursorToggle");
                if (hide != null)
                    hideCursorToggle = hide.GetComponent<Toggle>();
            }

            if (hideCursorToggle == null)
                return null;

            GameObject clone = Instantiate(hideCursorToggle.gameObject, createSettingsPanelRoot.transform);
            clone.name = "HitboxTrailToggle";
            RectTransform rect = clone.GetComponent<RectTransform>();
            RectTransform hideRect = hideCursorToggle.GetComponent<RectTransform>();
            if (rect != null && hideRect != null)
                rect.anchoredPosition = hideRect.anchoredPosition + new Vector2(0f, -40f);

            TextMeshProUGUI label = clone.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
                label.text = "Hitbox Trail";

            Toggle toggle = clone.GetComponent<Toggle>();
            if (toggle != null)
                toggle.isOn = false;
            return toggle;
        }

        void ResolveKaplaoZoomDefaults(out float minOrtho, out float maxOrtho)
        {
            GameCameraClient cameraClient = cursorClient != null
                ? cursorClient.GetComponent<GameCameraClient>()
                : null;
            if (cameraClient == null && NetworkClient.localPlayer != null)
                NetworkClient.localPlayer.TryGetComponent(out cameraClient);

            float baseMin = cameraClient != null ? cameraClient.BaseKaplaoMinOrthographicSize : 3f;
            float baseMax = cameraClient != null ? cameraClient.BaseKaplaoMaxOrthographicSize : 20f;
            minOrtho = CreateModeEditorSettings.GetKaplaoMinOrtho(baseMin);
            maxOrtho = CreateModeEditorSettings.GetKaplaoMaxOrtho(baseMax);
        }

        bool IsSettingsPanelVisible()
        {
            return settingsPanelRoot != null && settingsPanelRoot.activeSelf;
        }

        void RefreshSettingsToggles()
        {
            if (cursorServer == null)
                return;

            suppressFieldCallbacks = true;
            float transparency = cursorServer.GetSelectedTilesTransparency();
            if (transparencyInputField != null)
                transparencyInputField.text = float.IsNaN(transparency) ? string.Empty : transparency.ToString("G");
            if (noHitboxToggle != null)
                noHitboxToggle.isOn = cursorServer.AreAllSelectedTilesNoHitbox();
            bool singleTrigger = cursorServer.TryGetSingleSelectedTileObject(out LevelTileServer singleTile) &&
                                 singleTile.IsTrigger;
            if (spawnPointToggle != null)
            {
                spawnPointToggle.gameObject.SetActive(singleTrigger);
                if (singleTrigger)
                    spawnPointToggle.isOn = KaplaoLocalEditPredictor.GetEffectiveIsSpawnPoint(singleTile);
            }

            if (createdAtLabel != null)
            {
                bool showDate = cursorServer.TryGetSingleSelectedTileObject(out LevelTileServer datedTile) &&
                                datedTile.CreatedAtUnixMs > 0;
                createdAtLabel.gameObject.SetActive(showDate);
                if (showDate)
                {
                    var dt = System.DateTimeOffset.FromUnixTimeMilliseconds(datedTile.CreatedAtUnixMs).LocalDateTime;
                    createdAtLabel.text = $"Created: {dt:yyyy-MM-dd HH:mm}";
                }
            }

            bool hasSingle = cursorServer.TryGetSingleSelectedTile(out _, out Vector2 position, out float rotation, out Vector2 scale, out int sortingOrder);
            if (posXInputField != null) posXInputField.text = hasSingle ? KaplaoNumericInputUtility.FormatFloatField(position.x) : string.Empty;
            if (posYInputField != null) posYInputField.text = hasSingle ? KaplaoNumericInputUtility.FormatFloatField(position.y) : string.Empty;
            if (rotationInputField != null) rotationInputField.text = hasSingle ? KaplaoNumericInputUtility.FormatFloatField(rotation) : string.Empty;
            if (scaleXInputField != null) scaleXInputField.text = hasSingle ? KaplaoNumericInputUtility.FormatFloatField(scale.x) : string.Empty;
            if (scaleYInputField != null) scaleYInputField.text = hasSingle ? KaplaoNumericInputUtility.FormatFloatField(scale.y) : string.Empty;
            if (sortingOrderInputField != null) sortingOrderInputField.text = hasSingle ? sortingOrder.ToString() : string.Empty;
            if (editorLayerInputField != null)
            {
                editorLayerInputField.text = cursorServer.TryGetSelectedTilesEditorLayer(out int editorLayer)
                    ? editorLayer.ToString()
                    : string.Empty;
            }
            if (applyTransformButton != null) applyTransformButton.interactable = cursorServer.SelectedCount > 0;
            ResetTransformDirtyFlags();
            suppressFieldCallbacks = false;
        }

        void ResetTransformDirtyFlags()
        {
            dirtyTransformPosX = false;
            dirtyTransformPosY = false;
            dirtyTransformRotation = false;
            dirtyTransformScaleX = false;
            dirtyTransformScaleY = false;
            dirtyTransformSortingOrder = false;
        }

        void OnPosXCommitted(string _)
        {
            if (!suppressFieldCallbacks) { dirtyTransformPosX = true; ApplyTransformPatchFromDirtyFields(); }
        }

        void OnPosYCommitted(string _)
        {
            if (!suppressFieldCallbacks) { dirtyTransformPosY = true; ApplyTransformPatchFromDirtyFields(); }
        }

        void OnRotationCommitted(string _)
        {
            if (!suppressFieldCallbacks) { dirtyTransformRotation = true; ApplyTransformPatchFromDirtyFields(); }
        }

        void OnScaleXCommitted(string _)
        {
            if (!suppressFieldCallbacks) { dirtyTransformScaleX = true; ApplyTransformPatchFromDirtyFields(); }
        }

        void OnScaleYCommitted(string _)
        {
            if (!suppressFieldCallbacks) { dirtyTransformScaleY = true; ApplyTransformPatchFromDirtyFields(); }
        }

        void OnSortingOrderCommitted(string _)
        {
            if (!suppressFieldCallbacks) { dirtyTransformSortingOrder = true; ApplyTransformPatchFromDirtyFields(); }
        }

        void CommitTransformInputFields()
        {
            KaplaoNumericInputUtility.NormalizeFieldText(posXInputField);
            KaplaoNumericInputUtility.NormalizeFieldText(posYInputField);
            KaplaoNumericInputUtility.NormalizeFieldText(rotationInputField);
            KaplaoNumericInputUtility.NormalizeFieldText(scaleXInputField);
            KaplaoNumericInputUtility.NormalizeFieldText(scaleYInputField);
            KaplaoNumericInputUtility.NormalizeFieldText(sortingOrderInputField);

            posXInputField?.DeactivateInputField(true);
            posYInputField?.DeactivateInputField(true);
            rotationInputField?.DeactivateInputField(true);
            scaleXInputField?.DeactivateInputField(true);
            scaleYInputField?.DeactivateInputField(true);
            sortingOrderInputField?.DeactivateInputField(true);
        }

        void OnTransparencyInputCommitted(string value)
        {
            KaplaoCursorClient client = ActiveCursorClient();
            if (suppressFieldCallbacks || client == null)
                return;

            if (!float.TryParse(KaplaoNumericInputUtility.NormalizeDecimalSeparator(value).Replace(',', '.'),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float transparency))
                return;

            KaplaoCursorClient.EnsureClientReadyForCommands();
            client.RequestSetSelectedTilesTransparency(Mathf.Clamp01(transparency));
            RefreshSettingsToggles();
        }

        void OnEditorLayerInputCommitted(string value)
        {
            KaplaoCursorClient client = ActiveCursorClient();
            if (suppressFieldCallbacks || client == null)
                return;

            if (!int.TryParse(value, out int editorLayer))
                return;

            KaplaoCursorClient.EnsureClientReadyForCommands();
            client.RequestSetSelectedTilesEditorLayer(Mathf.Clamp(editorLayer, 0, 100));
            RefreshSettingsToggles();
        }

        void OnNoHitboxToggleChanged(bool enabled)
        {
            KaplaoCursorClient client = ActiveCursorClient();
            if (suppressFieldCallbacks || client == null)
                return;

            KaplaoCursorClient.EnsureClientReadyForCommands();
            client.RequestSetSelectedTilesNoHitbox(enabled);
            RefreshSettingsToggles();
        }

        void OnSpawnPointToggleChanged(bool enabled)
        {
            KaplaoCursorClient client = ActiveCursorClient();
            if (suppressFieldCallbacks || client == null)
                return;

            KaplaoCursorClient.EnsureClientReadyForCommands();
            client.RequestApplySpawnPointToSelection(enabled);
        }

        void ApplyTransformPatchFromDirtyFields()
        {
            KaplaoCursorClient client = ActiveCursorClient();
            if (client == null || cursorServer == null || cursorServer.SelectedCount == 0)
                return;

            if (!dirtyTransformPosX && !dirtyTransformPosY && !dirtyTransformRotation &&
                !dirtyTransformScaleX && !dirtyTransformScaleY && !dirtyTransformSortingOrder)
                return;

            var patch = new TileTransformPatchRecord();

            if (dirtyTransformPosX && posXInputField != null &&
                KaplaoNumericInputUtility.TryParseFloat(posXInputField.text, out float x))
            {
                patch.hasPositionX = true;
                patch.positionX = x;
            }

            if (dirtyTransformPosY && posYInputField != null &&
                KaplaoNumericInputUtility.TryParseFloat(posYInputField.text, out float y))
            {
                patch.hasPositionY = true;
                patch.positionY = y;
            }

            if (dirtyTransformRotation && rotationInputField != null &&
                KaplaoNumericInputUtility.TryParseFloat(rotationInputField.text, out float rotation))
            {
                patch.hasRotation = true;
                patch.rotationDegrees = rotation;
            }

            if (dirtyTransformScaleX && scaleXInputField != null &&
                KaplaoNumericInputUtility.TryParseFloat(scaleXInputField.text, out float scaleX))
            {
                patch.hasScaleX = true;
                patch.scaleX = scaleX;
            }

            if (dirtyTransformScaleY && scaleYInputField != null &&
                KaplaoNumericInputUtility.TryParseFloat(scaleYInputField.text, out float scaleY))
            {
                patch.hasScaleY = true;
                patch.scaleY = scaleY;
            }

            if (dirtyTransformSortingOrder && sortingOrderInputField != null &&
                KaplaoNumericInputUtility.TryParseFloat(sortingOrderInputField.text, out float sorting))
            {
                patch.hasSortingOrder = true;
                patch.sortingOrder = Mathf.Clamp(Mathf.RoundToInt(sorting), -10, 10);
            }

            if (!patch.HasAnyField)
                return;

            KaplaoCursorClient.EnsureClientReadyForCommands();
            client.RequestApplyTransformPatchToSelection(patch);
            ResetTransformDirtyFlags();
        }

        void ApplySingleTileTransform()
        {
            ApplyTransformPatchFromDirtyFields();
        }

        static float ParseInput(TMP_InputField field, float fallback)
        {
            if (field == null || !KaplaoNumericInputUtility.TryParseFloat(field.text, out float value))
                return fallback;

            return value;
        }

        CreateModePanelDismissOverlay dismissOverlay;

        void EnsureDismissOverlay()
        {
            if (dismissOverlay != null || kaplaoOnlyRoot == null)
                return;

            dismissOverlay = kaplaoOnlyRoot.GetComponentInChildren<CreateModePanelDismissOverlay>(true);
            if (dismissOverlay != null)
                return;

            var overlayObject = new GameObject("PanelDismissOverlay", typeof(RectTransform));
            overlayObject.transform.SetParent(kaplaoOnlyRoot.transform, false);
            RectTransform rect = overlayObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            dismissOverlay = overlayObject.AddComponent<CreateModePanelDismissOverlay>();
        }

        void EnsureDecimalNormalizers()
        {
            if (kaplaoOnlyRoot == null)
                return;

            TMP_InputField[] fields = kaplaoOnlyRoot.GetComponentsInChildren<TMP_InputField>(true);
            for (int i = 0; i < fields.Length; i++)
                KaplaoDecimalInputNormalizer.EnsureOn(fields[i]);
        }

        void UpdateDismissOverlay()
        {
            EnsureDismissOverlay();
            if (dismissOverlay == null)
                return;

            bool anyOpen = IsSetGroupPanelVisible() ||
                           IsSettingsPanelVisible() ||
                           IsCreateSettingsPanelVisible() ||
                           IsTexturePanelVisible() ||
                           IsSoundPanelVisible() ||
                           IsExecutorPanelVisible() ||
                           IsKeyframePanelVisible() ||
                           IsParticlePanelVisible() ||
                           IsAnyImportPanelVisible();

            dismissOverlay.SetVisible(anyOpen);
        }

        bool IsTexturePanelVisible()
        {
            return tileTextureUi != null && tileTextureUi.IsOpen;
        }

        bool IsSoundPanelVisible()
        {
            return soundPickerUi != null && soundPickerUi.IsOpen;
        }

        bool IsExecutorPanelVisible()
        {
            KaplaoExecutorEditorUi editor = KaplaoExecutorEditorUi.Active ??
                FindFirstObjectByType<KaplaoExecutorEditorUi>(FindObjectsInactive.Include);
            return editor != null && editor.IsOpen;
        }

        bool IsKeyframePanelVisible()
        {
            KaplaoKeyframeEditorUi editor = FindFirstObjectByType<KaplaoKeyframeEditorUi>(FindObjectsInactive.Include);
            return editor != null && editor.IsOpen;
        }

        bool IsParticlePanelVisible()
        {
            KaplaoParticleEditorUi editor = FindFirstObjectByType<KaplaoParticleEditorUi>(FindObjectsInactive.Include);
            return editor != null && editor.IsOpen;
        }

        static bool IsAnyImportPanelVisible()
        {
            KaplaoPngImportUi png = FindFirstObjectByType<KaplaoPngImportUi>(FindObjectsInactive.Include);
            if (png != null && png.IsPanelVisible)
                return true;

            KaplaoTextureImportUi texture = FindFirstObjectByType<KaplaoTextureImportUi>(FindObjectsInactive.Include);
            if (texture != null && texture.IsPanelVisible)
                return true;

            KaplaoSoundImportUi sound = FindFirstObjectByType<KaplaoSoundImportUi>(FindObjectsInactive.Include);
            return sound != null && sound.IsPanelVisible;
        }

        public void DismissOpenPanelsFromOverlay()
        {
            HideSelectionPanels();
            KaplaoPngImportUi png = FindFirstObjectByType<KaplaoPngImportUi>(FindObjectsInactive.Include);
            png?.DismissPanelIfOpen();
            KaplaoTextureImportUi texture = FindFirstObjectByType<KaplaoTextureImportUi>(FindObjectsInactive.Include);
            texture?.DismissPanelIfOpen();
            KaplaoSoundImportUi sound = FindFirstObjectByType<KaplaoSoundImportUi>(FindObjectsInactive.Include);
            sound?.DismissPanelIfOpen();
            UpdateDismissOverlay();
        }

        void DeselectSelection()
        {
            KaplaoCursorClient.EnsureClientReadyForCommands();
            HideSelectionPanels();
            KaplaoCursorClient client = ActiveCursorClient();
            if (client != null && client.TryGetComponent(out TileSelectionVisuals visuals))
                visuals.ForceClearSelectionOutlines();
            cursorClient?.CmdClearSelection();
        }

        void HideSelectionPanels()
        {
            HideSetGroupPanel();
            HideSettingsPanel();
            HideCreateSettingsPanel();
            HideTexturePanel();
            HideSoundPanel();
            HideParticleEditorPanel();
            HideExecutorEditorPanel();
            HideKeyframeEditorPanel();
        }

        void HideExecutorEditorPanel()
        {
            KaplaoExecutorEditorUi editor = KaplaoExecutorEditorUi.Active ??
                FindFirstObjectByType<KaplaoExecutorEditorUi>(FindObjectsInactive.Include);
            editor?.Hide();
        }

        void HideKeyframeEditorPanel()
        {
            KaplaoKeyframeEditorUi editor = FindFirstObjectByType<KaplaoKeyframeEditorUi>(FindObjectsInactive.Include);
            editor?.Hide();
        }

        void HideParticleEditorPanel()
        {
            KaplaoParticleEditorUi editor = FindFirstObjectByType<KaplaoParticleEditorUi>(FindObjectsInactive.Include);
            editor?.Hide();
        }

        void DeleteSelection()
        {
            KaplaoCursorClient.EnsureClientReadyForCommands();
            cursorClient?.RequestDeleteSelectedTiles();
        }

        public GameObject KaplaoOnlyRoot => kaplaoOnlyRoot;
    }
}
