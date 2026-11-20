using KaplaoGames.LevelScript;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using Mirror;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.Client
{
    [DisallowMultipleComponent]
    public class KaplaoExecutorEditorUi : MonoBehaviour
    {
        [SerializeField] GameObject kaplaoOnlyRoot;
        [SerializeField] RectTransform panelRoot;
        [SerializeField] TMP_InputField targetGroupInput;
        [SerializeField] TMP_InputField durationInput;
        [SerializeField] TMP_Dropdown animDropdown;
        [SerializeField] TMP_InputField x360Input;
        [SerializeField] Toggle spawnedExecutorToggle;
        [SerializeField] Toggle spawnAtEndToggle;
        [SerializeField] TMP_InputField spawnAtEndGroupInput;
        [SerializeField] Toggle loopToggle;
        [SerializeField] TMP_InputField loopEveryInput;
        [SerializeField] Toggle useKeyframesToggle;
        [SerializeField] TMP_InputField keyframesGroupInput;
        [SerializeField] Toggle returnToggle;
        [SerializeField] TMP_InputField returnDelayInput;
        [SerializeField] Button editStatesButton;
        [SerializeField] Button closeButton;

        [Header("Edit States mode")]
        [SerializeField] Button leaveEditStatesButton;
        [SerializeField] Button editStartStateButton;
        [SerializeField] Button editEndStateButton;
        [SerializeField] Button resetStartStateButton;
        [SerializeField] Button resetEndStateButton;
        [SerializeField] TextMeshProUGUI grtHintLabel;

        static readonly string[] EditStatesControlNames =
        {
            "LeaveEditStatesButton",
            "EditStartStateButton",
            "EditEndStateButton",
            "ResetStartStateButton",
            "ResetEndStateButton",
            "ExecutorEditStatesHintLabel"
        };

        static readonly string[] ExtraHudNamesToHide =
        {
            "SelectionToolbar",
            "ImportPngButton",
            "ImportTextureButton",
            "CreateToolSlots",
            "UsualToolSlots",
            "CreateSettingsButton"
        };

        KaplaoCursorClient cursorClient;
        readonly KaplaoExecutorEditStatesSession session = new KaplaoExecutorEditStatesSession();
        readonly List<GameObject> hiddenUiObjects = new List<GameObject>();
        readonly List<bool> hiddenUiObjectsWereActive = new List<bool>();
        readonly List<LevelTileServer> targetTiles = new List<LevelTileServer>();
        uint[] selectionBeforeEditStates = System.Array.Empty<uint>();
        uint executorNetIdBeforeEditStates;
        bool inEditStatesMode;
        bool buttonsWired;
        uint[] applyTargetNetIds = System.Array.Empty<uint>();
        bool suppressApply;

        public static KaplaoExecutorEditorUi Active { get; private set; }
        public bool IsOpen => panelRoot != null && panelRoot.gameObject.activeSelf;
        public bool IsInEditStatesMode => inEditStatesMode;
        public static bool BlocksCreateHud => Active != null && Active.inEditStatesMode;

        void Awake()
        {
            Active = this;
            if (kaplaoOnlyRoot == null)
                kaplaoOnlyRoot = gameObject;
            if (panelRoot != null) panelRoot.gameObject.SetActive(false);
            HideApplyButton();
        }

        void OnDestroy()
        {
            if (Active == this)
                Active = null;
            session.End();
        }

        void OnEnable()
        {
            EnsureButtonsWired();
            WireLiveApply(true);
            if (spawnAtEndToggle != null) spawnAtEndToggle.onValueChanged.AddListener(OnSpawnAtEndChanged);
            if (loopToggle != null) loopToggle.onValueChanged.AddListener(OnLoopChanged);
            if (useKeyframesToggle != null) useKeyframesToggle.onValueChanged.AddListener(OnUseKeyframesChanged);
            if (returnToggle != null) returnToggle.onValueChanged.AddListener(OnReturnChanged);
        }

        void OnDisable()
        {
            if (IsOpen || inEditStatesMode)
                FlushPendingChanges();

            UnwireButtons();
            WireLiveApply(false);
            if (spawnAtEndToggle != null) spawnAtEndToggle.onValueChanged.RemoveListener(OnSpawnAtEndChanged);
            if (loopToggle != null) loopToggle.onValueChanged.RemoveListener(OnLoopChanged);
            if (useKeyframesToggle != null) useKeyframesToggle.onValueChanged.RemoveListener(OnUseKeyframesChanged);
            if (returnToggle != null) returnToggle.onValueChanged.RemoveListener(OnReturnChanged);
        }

        void Update()
        {
            if (inEditStatesMode)
                session.Tick();
        }

        void EnsureButtonsWired()
        {
            TryResolveBindingsFromScene();
            HideApplyButton();
            WireButton(closeButton, Hide);
            WireButton(editStatesButton, EnterEditStatesMode);
            WireButton(leaveEditStatesButton, LeaveEditStatesMode);
            WireButton(editStartStateButton, OnEditStartStateClicked);
            WireButton(editEndStateButton, OnEditEndStateClicked);
            WireButton(resetStartStateButton, OnResetStartStateClicked);
            WireButton(resetEndStateButton, OnResetEndStateClicked);
            buttonsWired = editStatesButton != null;
        }

        void UnwireButtons()
        {
            if (!buttonsWired)
                return;

            if (closeButton != null) closeButton.onClick.RemoveListener(Hide);
            if (editStatesButton != null) editStatesButton.onClick.RemoveListener(EnterEditStatesMode);
            if (leaveEditStatesButton != null) leaveEditStatesButton.onClick.RemoveListener(LeaveEditStatesMode);
            if (editStartStateButton != null) editStartStateButton.onClick.RemoveListener(OnEditStartStateClicked);
            if (editEndStateButton != null) editEndStateButton.onClick.RemoveListener(OnEditEndStateClicked);
            if (resetStartStateButton != null) resetStartStateButton.onClick.RemoveListener(OnResetStartStateClicked);
            if (resetEndStateButton != null) resetEndStateButton.onClick.RemoveListener(OnResetEndStateClicked);
            buttonsWired = false;
        }

        static void WireButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null || action == null)
                return;

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        void OnEditStartStateClicked() => SetActiveState(true);
        void OnEditEndStateClicked() => SetActiveState(false);
        void OnResetStartStateClicked() => session.ResetStartState();
        void OnResetEndStateClicked() => session.ResetEndState();

        void HideApplyButton()
        {
            Transform apply = panelRoot != null ? panelRoot.Find("ExApplyButton") : null;
            if (apply == null && panelRoot != null)
            {
                Button leftover = FindChildComponent<Button>(panelRoot, "ExApplyButton");
                if (leftover != null)
                    leftover.gameObject.SetActive(false);
            }
            else if (apply != null)
                apply.gameObject.SetActive(false);
        }

        public void ShowForSelection()
        {
            EnsureButtonsWired();
            if (panelRoot == null)
            {
                Debug.LogWarning("[Kaplao] Executor editor panel is missing. Run Kaplao/Setup Executor UI.");
                return;
            }

            panelRoot.gameObject.SetActive(true);
            SetEditStatesControlsVisible(false);
            CaptureApplyTargetsFromSelection();
            RefreshFromSelection();
        }

        public void Hide()
        {
            FlushPendingChanges();
            if (panelRoot != null) panelRoot.gameObject.SetActive(false);
        }

        /// <summary>
        /// Commits focused inputs and pushes settings/states to the server while still in Create mode.
        /// </summary>
        public void FlushPendingChanges()
        {
            CommitFocusedInputFields();
            if (inEditStatesMode)
                LeaveEditStatesMode();
            else
                Apply();
        }

        public static void FlushAllOpenEditorsBeforeModeChange()
        {
            KaplaoExecutorEditorUi executor = Active ??
                FindFirstObjectByType<KaplaoExecutorEditorUi>(FindObjectsInactive.Include);
            if (executor != null && (executor.IsOpen || executor.IsInEditStatesMode))
                executor.FlushPendingChanges();

            KaplaoKeyframeEditorUi keyframe = FindFirstObjectByType<KaplaoKeyframeEditorUi>(FindObjectsInactive.Include);
            if (keyframe != null && keyframe.IsOpen)
                keyframe.Hide();

            KaplaoParticleEditorUi particle = FindFirstObjectByType<KaplaoParticleEditorUi>(FindObjectsInactive.Include);
            if (particle != null && particle.IsOpen)
                particle.Hide();
        }

        void CommitFocusedInputFields()
        {
            if (targetGroupInput != null && targetGroupInput.isFocused)
                targetGroupInput.DeactivateInputField(true);
            if (durationInput != null && durationInput.isFocused)
                durationInput.DeactivateInputField(true);
            if (x360Input != null && x360Input.isFocused)
                x360Input.DeactivateInputField(true);
            if (spawnAtEndGroupInput != null && spawnAtEndGroupInput.isFocused)
                spawnAtEndGroupInput.DeactivateInputField(true);
            if (loopEveryInput != null && loopEveryInput.isFocused)
                loopEveryInput.DeactivateInputField(true);
            if (keyframesGroupInput != null && keyframesGroupInput.isFocused)
                keyframesGroupInput.DeactivateInputField(true);
            if (returnDelayInput != null && returnDelayInput.isFocused)
                returnDelayInput.DeactivateInputField(true);
        }

        void RefreshFromSelection()
        {
            KaplaoCursorServer server = NetworkClient.localPlayer != null
                ? NetworkClient.localPlayer.GetComponent<KaplaoCursorServer>()
                : null;
            if (server == null || server.SelectedCount <= 0) return;

            suppressApply = true;
            if (server.SelectedCount == 1)
            {
                if (!LevelScriptObjectUtility.TryResolveTile(server.SelectedTiles[0], out LevelTileServer tile) ||
                    tile == null || !tile.TryGetComponent(out ExecutorTile executor))
                    return;

                if (targetGroupInput != null) targetGroupInput.text = executor.TargetGroup.ToString();
                if (durationInput != null) durationInput.text = executor.Duration.ToString("G");
                if (x360Input != null) x360Input.text = executor.X360.ToString();
                if (spawnedExecutorToggle != null) spawnedExecutorToggle.isOn = executor.SpawnedExecutor;
                if (spawnAtEndToggle != null) spawnAtEndToggle.isOn = executor.SpawnExecutorAtEnd;
                if (spawnAtEndGroupInput != null) spawnAtEndGroupInput.text = executor.SpawnAtEndGroup.ToString();
                if (loopToggle != null) loopToggle.isOn = executor.Loop;
                if (loopEveryInput != null) loopEveryInput.text = executor.LoopEverySeconds.ToString("G");
                if (useKeyframesToggle != null) useKeyframesToggle.isOn = executor.UseKeyframes;
                if (keyframesGroupInput != null) keyframesGroupInput.text = executor.KeyframesGroup.ToString();
                if (returnToggle != null) returnToggle.isOn = executor.ReturnEnabled;
                if (returnDelayInput != null) returnDelayInput.text = executor.ReturnDelaySeconds.ToString("G");

                if (animDropdown != null)
                {
                    int index = executor.Anim switch
                    {
                        "easein" => 1,
                        "easeout" => 2,
                        "easeinout" => 3,
                        _ => 0
                    };
                    animDropdown.value = index;
                }
            }
            else
            {
                if (targetGroupInput != null) targetGroupInput.text = string.Empty;
                if (durationInput != null) durationInput.text = string.Empty;
                if (x360Input != null) x360Input.text = string.Empty;
                if (spawnedExecutorToggle != null) spawnedExecutorToggle.isOn = false;
                if (spawnAtEndToggle != null) spawnAtEndToggle.isOn = false;
                if (spawnAtEndGroupInput != null) spawnAtEndGroupInput.text = string.Empty;
                if (loopToggle != null) loopToggle.isOn = false;
                if (loopEveryInput != null) loopEveryInput.text = string.Empty;
                if (useKeyframesToggle != null) useKeyframesToggle.isOn = false;
                if (keyframesGroupInput != null) keyframesGroupInput.text = string.Empty;
                if (returnToggle != null) returnToggle.isOn = false;
                if (returnDelayInput != null) returnDelayInput.text = string.Empty;
                if (animDropdown != null) animDropdown.value = 0;
            }

            RefreshConditionalVisibility();
            suppressApply = false;
        }

        void RefreshConditionalVisibility()
        {
            bool useKf = useKeyframesToggle != null && useKeyframesToggle.isOn;
            bool showReturn = !useKf && returnToggle != null && returnToggle.isOn;
            SetFieldVisible(editStatesButton, !useKf);
            SetFieldVisible(durationInput, !useKf, "ExDurationLabel");
            SetFieldVisible(animDropdown, !useKf, "ExAnimLabel");
            SetFieldVisible(x360Input, !useKf, "X360Label");
            SetFieldVisible(returnToggle, !useKf);
            SetFieldVisible(keyframesGroupInput, useKf, "KeyframesGroupLabel");
            SetFieldVisible(spawnAtEndGroupInput, spawnAtEndToggle != null && spawnAtEndToggle.isOn, "SpawnAtEndGroupLabel");
            SetFieldVisible(loopEveryInput, loopToggle != null && loopToggle.isOn, "LoopEveryLabel");
            SetFieldVisible(returnDelayInput, showReturn, "ReturnDelayLabel");
        }

        void SetFieldVisible(Component field, bool visible, string labelName = null)
        {
            if (field != null)
                field.gameObject.SetActive(visible);

            if (string.IsNullOrEmpty(labelName) || panelRoot == null)
                return;

            Transform label = panelRoot.Find(labelName);
            if (label != null)
                label.gameObject.SetActive(visible);
        }

        void OnSpawnAtEndChanged(bool _)
        {
            RefreshConditionalVisibility();
            if (!suppressApply) Apply();
        }

        void OnLoopChanged(bool _)
        {
            RefreshConditionalVisibility();
            if (!suppressApply) Apply();
        }

        void OnUseKeyframesChanged(bool _)
        {
            RefreshConditionalVisibility();
            if (!suppressApply) Apply();
        }

        void OnReturnChanged(bool _)
        {
            RefreshConditionalVisibility();
            if (!suppressApply) Apply();
        }

        void WireLiveApply(bool bind)
        {
            WireInput(targetGroupInput, bind);
            WireInput(durationInput, bind);
            WireInput(x360Input, bind);
            WireInput(spawnAtEndGroupInput, bind);
            WireInput(loopEveryInput, bind);
            WireInput(keyframesGroupInput, bind);
            WireInput(returnDelayInput, bind);

            if (animDropdown != null)
            {
                animDropdown.onValueChanged.RemoveListener(OnLiveApplyDropdown);
                if (bind) animDropdown.onValueChanged.AddListener(OnLiveApplyDropdown);
            }

            WireToggle(spawnedExecutorToggle, bind);
        }

        void WireInput(TMP_InputField field, bool bind)
        {
            if (field == null)
                return;
            field.onEndEdit.RemoveListener(OnLiveApplyText);
            if (bind)
                field.onEndEdit.AddListener(OnLiveApplyText);
        }

        void WireToggle(Toggle toggle, bool bind)
        {
            if (toggle == null)
                return;
            toggle.onValueChanged.RemoveListener(OnLiveApplyToggle);
            if (bind)
                toggle.onValueChanged.AddListener(OnLiveApplyToggle);
        }

        void OnLiveApplyText(string _)
        {
            if (!suppressApply)
                Apply();
        }

        void OnLiveApplyToggle(bool _)
        {
            if (!suppressApply)
                Apply();
        }

        void OnLiveApplyDropdown(int _)
        {
            if (!suppressApply)
                Apply();
        }

        void CaptureApplyTargetsFromSelection()
        {
            KaplaoCursorServer server = NetworkClient.localPlayer != null
                ? NetworkClient.localPlayer.GetComponent<KaplaoCursorServer>()
                : null;
            if (server == null || server.SelectedCount <= 0)
            {
                applyTargetNetIds = System.Array.Empty<uint>();
                return;
            }

            var ids = new List<uint>(server.SelectedCount);
            for (int i = 0; i < server.SelectedCount; i++)
            {
                uint netId = server.SelectedTiles[i];
                if (!LevelScriptObjectUtility.TryResolveTile(netId, out LevelTileServer tile) || tile == null)
                    continue;
                if (tile.TileType != TileType.Executor)
                    continue;
                ids.Add(netId);
            }

            applyTargetNetIds = ids.ToArray();
        }

        void Apply()
        {
            if (suppressApply)
                return;

            cursorClient = KaplaoLocalBindingUtility.ResolveLocalCursorClient();
            if (cursorClient == null)
                return;

            if (applyTargetNetIds == null || applyTargetNetIds.Length == 0)
                CaptureApplyTargetsFromSelection();

            if (applyTargetNetIds == null || applyTargetNetIds.Length == 0)
                return;

            bool single = applyTargetNetIds.Length == 1;
            var settings = new ExecutorTileSettings();

            if (single &&
                LevelScriptObjectUtility.TryResolveTile(applyTargetNetIds[0], out LevelTileServer tile) &&
                tile != null &&
                tile.TryGetComponent(out ExecutorTile executor))
            {
                settings.hasTargetGroup = true;
                settings.targetGroup = executor.TargetGroup;
                settings.hasDuration = true;
                settings.duration = executor.Duration;
                settings.hasAnim = true;
                settings.anim = executor.Anim;
                settings.hasX360 = true;
                settings.x360 = executor.X360;
                settings.hasSpawnedExecutor = true;
                settings.spawnedExecutor = executor.SpawnedExecutor;
                settings.hasSpawnExecutorAtEnd = true;
                settings.spawnExecutorAtEnd = executor.SpawnExecutorAtEnd;
                settings.hasSpawnAtEndGroup = true;
                settings.spawnAtEndGroup = executor.SpawnAtEndGroup;
                settings.hasLoop = true;
                settings.loop = executor.Loop;
                settings.hasLoopEverySeconds = true;
                settings.loopEverySeconds = executor.LoopEverySeconds;
                settings.hasUseKeyframes = true;
                settings.useKeyframes = executor.UseKeyframes;
                settings.hasKeyframesGroup = true;
                settings.keyframesGroup = executor.KeyframesGroup;
                settings.hasCloseLoop = true;
                settings.closeLoop = executor.CloseLoop;
                settings.hasReturnEnabled = true;
                settings.returnEnabled = executor.ReturnEnabled;
                settings.hasReturnDelaySeconds = true;
                settings.returnDelaySeconds = executor.ReturnDelaySeconds;
            }

            if (targetGroupInput != null &&
                !string.IsNullOrWhiteSpace(targetGroupInput.text) &&
                int.TryParse(targetGroupInput.text, out int tg))
            {
                settings.hasTargetGroup = true;
                settings.targetGroup = tg;
            }

            if (durationInput != null &&
                !string.IsNullOrWhiteSpace(durationInput.text) &&
                KaplaoNumericInputUtility.TryParseFloat(durationInput.text, out float dur))
            {
                settings.hasDuration = true;
                settings.duration = dur;
            }

            if (x360Input != null &&
                !string.IsNullOrWhiteSpace(x360Input.text) &&
                int.TryParse(x360Input.text, out int x))
            {
                settings.hasX360 = true;
                settings.x360 = x;
            }

            if (single)
            {
                if (animDropdown != null)
                {
                    settings.hasAnim = true;
                    settings.anim = animDropdown.options[animDropdown.value].text;
                }

                if (spawnedExecutorToggle != null)
                {
                    settings.hasSpawnedExecutor = true;
                    settings.spawnedExecutor = spawnedExecutorToggle.isOn;
                }

                if (spawnAtEndToggle != null)
                {
                    settings.hasSpawnExecutorAtEnd = true;
                    settings.spawnExecutorAtEnd = spawnAtEndToggle.isOn;
                }

                if (loopToggle != null)
                {
                    settings.hasLoop = true;
                    settings.loop = loopToggle.isOn;
                }

                if (useKeyframesToggle != null)
                {
                    settings.hasUseKeyframes = true;
                    settings.useKeyframes = useKeyframesToggle.isOn;
                }

                if (returnToggle != null)
                {
                    settings.hasReturnEnabled = true;
                    settings.returnEnabled = returnToggle.isOn;
                }
            }

            if (spawnAtEndGroupInput != null &&
                !string.IsNullOrWhiteSpace(spawnAtEndGroupInput.text) &&
                int.TryParse(spawnAtEndGroupInput.text, out int sag))
            {
                settings.hasSpawnAtEndGroup = true;
                settings.spawnAtEndGroup = sag;
            }

            if (returnDelayInput != null &&
                !string.IsNullOrWhiteSpace(returnDelayInput.text) &&
                KaplaoNumericInputUtility.TryParseFloat(returnDelayInput.text, out float delay))
            {
                settings.hasReturnDelaySeconds = true;
                settings.returnDelaySeconds = Mathf.Max(0f, delay);
            }

            if (loopEveryInput != null &&
                !string.IsNullOrWhiteSpace(loopEveryInput.text) &&
                KaplaoNumericInputUtility.TryParseFloat(loopEveryInput.text, out float loopSec))
            {
                settings.hasLoopEverySeconds = true;
                settings.loopEverySeconds = loopSec;
            }

            if (keyframesGroupInput != null &&
                !string.IsNullOrWhiteSpace(keyframesGroupInput.text) &&
                int.TryParse(keyframesGroupInput.text, out int kfg))
            {
                settings.hasKeyframesGroup = true;
                settings.keyframesGroup = kfg;
            }

            if (settings.hasLoop && settings.loop)
            {
                float duration = settings.hasDuration ? settings.duration : 0f;
                float returnDelay = settings.hasReturnDelaySeconds ? settings.returnDelaySeconds : 0f;
                bool returnEnabled = settings.hasReturnEnabled && settings.returnEnabled &&
                                     !(settings.hasUseKeyframes && settings.useKeyframes);
                float minLoop = ExecutorTile.MinLoopEverySeconds(duration, returnEnabled, returnDelay);
                if (!settings.hasLoopEverySeconds || settings.loopEverySeconds < minLoop)
                {
                    settings.hasLoopEverySeconds = true;
                    settings.loopEverySeconds = minLoop;
                    if (loopEveryInput != null)
                    {
                        suppressApply = true;
                        loopEveryInput.text = minLoop.ToString("G");
                        suppressApply = false;
                    }
                }
            }

            if (!settings.HasAnyField)
                return;

            KaplaoCursorClient.EnsureClientReadyForCommands();
            cursorClient.CmdApplyExecutorSettingsToTiles(applyTargetNetIds, settings);
        }

        void EnterEditStatesMode()
        {
            EnsureButtonsWired();
            if (!TryResolveSelectedExecutor(out ExecutorTile executor))
            {
                Debug.LogWarning("[Kaplao] Edit States: no Executor in current selection.");
                return;
            }

            Apply();
            CaptureApplyTargetsFromSelection();
            if (!TryResolveSelectedExecutor(out executor))
            {
                Debug.LogWarning("[Kaplao] Edit States: no Executor in current selection.");
                return;
            }

            int targetGroup = ResolveTargetGroup(executor);
            CollectTargetTiles(targetGroup, targetTiles, executor.netId);
            if (targetTiles.Count == 0)
            {
                Debug.LogWarning($"[Kaplao] Edit States: target group {targetGroup} has no tiles.");
                return;
            }

            selectionBeforeEditStates = SnapshotCurrentSelection();
            executorNetIdBeforeEditStates = executor.netId;
            cursorClient = KaplaoLocalBindingUtility.ResolveLocalCursorClient();
            cursorClient?.CmdClearSelection();

            float duration = 1f;
            if (durationInput != null && KaplaoNumericInputUtility.TryParseFloat(durationInput.text, out float parsedDuration))
                duration = parsedDuration;
            string anim = animDropdown != null ? animDropdown.options[animDropdown.value].text : executor.Anim;
            int x360 = 0;
            if (x360Input != null && int.TryParse(x360Input.text, out int parsedX360))
                x360 = parsedX360;

            if (!session.Begin(targetTiles, executor.StartState, executor.EndState, duration, anim, x360, targetGroup, executor.ReturnEnabled, executor.ReturnDelaySeconds))
                return;

            inEditStatesMode = true;
            if (panelRoot != null)
                panelRoot.gameObject.SetActive(false);

            HideKaplaoUiExceptEditStatesControls();
            SetEditStatesControlsVisible(true);
            SetActiveState(false);
        }

        int ResolveTargetGroup(ExecutorTile executor)
        {
            if (targetGroupInput != null && int.TryParse(targetGroupInput.text, out int parsed))
                return parsed;
            return executor.TargetGroup;
        }

        void LeaveEditStatesMode()
        {
            if (!inEditStatesMode)
            {
                RestoreKaplaoUiAfterEditStatesMode();
                SetEditStatesControlsVisible(false);
                return;
            }

            session.Tick();
            session.CaptureBothStates();
            ExecutorGroupState start = session.StartState;
            ExecutorGroupState end = session.EndState;
            session.End();

            uint[] executorIds = selectionBeforeEditStates != null && selectionBeforeEditStates.Length > 0
                ? selectionBeforeEditStates
                : new[] { executorNetIdBeforeEditStates };

            cursorClient = KaplaoLocalBindingUtility.ResolveLocalCursorClient();
            if (cursorClient != null)
            {
                KaplaoCursorClient.EnsureClientReadyForCommands();
                cursorClient.CmdApplyExecutorStatesToTiles(executorIds, start, end);
                cursorClient.RequestSetSelection(executorIds);
            }

            inEditStatesMode = false;
            RestoreKaplaoUiAfterEditStatesMode();
            SetEditStatesControlsVisible(false);
            if (panelRoot != null)
                panelRoot.gameObject.SetActive(true);
            targetTiles.Clear();
        }

        void SetActiveState(bool editStart)
        {
            session.SetActiveState(editStart);
            if (editStartStateButton != null)
                editStartStateButton.interactable = !editStart;
            if (editEndStateButton != null)
                editEndStateButton.interactable = editStart;
        }

        void TryResolveBindingsFromScene()
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
                canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null)
                return;

            Transform createMode = canvas.transform.Find("CreateModeUI");
            if (kaplaoOnlyRoot == null)
            {
                KaplaoCreateModeUi createModeUi = canvas.GetComponent<KaplaoCreateModeUi>();
                if (createModeUi != null && createModeUi.KaplaoOnlyRoot != null)
                    kaplaoOnlyRoot = createModeUi.KaplaoOnlyRoot;
                else if (createMode != null)
                    kaplaoOnlyRoot = createMode.gameObject;
            }

            if (panelRoot == null)
                panelRoot = FindChildComponent<RectTransform>(canvas.transform, "ExecutorEditorPanel");
            targetGroupInput ??= FindChildComponent<TMP_InputField>(canvas.transform, "TargetGroupInput");
            durationInput ??= FindChildComponent<TMP_InputField>(canvas.transform, "ExDurationInput");
            animDropdown ??= FindChildComponent<TMP_Dropdown>(canvas.transform, "ExAnimDropdown");
            x360Input ??= FindChildComponent<TMP_InputField>(canvas.transform, "X360Input");
            spawnedExecutorToggle ??= FindChildComponent<Toggle>(canvas.transform, "SpawnedExecutorToggle");
            spawnAtEndToggle ??= FindChildComponent<Toggle>(canvas.transform, "SpawnAtEndToggle");
            spawnAtEndGroupInput ??= FindChildComponent<TMP_InputField>(canvas.transform, "SpawnAtEndGroupInput");
            loopToggle ??= FindChildComponent<Toggle>(canvas.transform, "LoopToggle");
            loopEveryInput ??= FindChildComponent<TMP_InputField>(canvas.transform, "LoopEveryInput");
            useKeyframesToggle ??= FindChildComponent<Toggle>(canvas.transform, "UseKeyframesToggle");
            keyframesGroupInput ??= FindChildComponent<TMP_InputField>(canvas.transform, "KeyframesGroupInput");
            returnToggle ??= FindChildComponent<Toggle>(canvas.transform, "ReturnToggle");
            returnDelayInput ??= FindChildComponent<TMP_InputField>(canvas.transform, "ReturnDelayInput");
            editStatesButton ??= FindChildComponent<Button>(canvas.transform, "EditStatesButton");
            closeButton ??= FindChildComponent<Button>(canvas.transform, "ExCloseButton");
            leaveEditStatesButton ??= FindChildComponent<Button>(canvas.transform, "LeaveEditStatesButton");
            editStartStateButton ??= FindChildComponent<Button>(canvas.transform, "EditStartStateButton");
            editEndStateButton ??= FindChildComponent<Button>(canvas.transform, "EditEndStateButton");
            resetStartStateButton ??= FindChildComponent<Button>(canvas.transform, "ResetStartStateButton");
            resetEndStateButton ??= FindChildComponent<Button>(canvas.transform, "ResetEndStateButton");
            grtHintLabel ??= FindChildComponent<TextMeshProUGUI>(canvas.transform, "ExecutorEditStatesHintLabel");
            HideApplyButton();
        }

        void SetEditStatesControlsVisible(bool visible)
        {
            EnsureEditStatesControlRefs();
            if (leaveEditStatesButton != null) leaveEditStatesButton.gameObject.SetActive(visible);
            if (editStartStateButton != null) editStartStateButton.gameObject.SetActive(visible);
            if (editEndStateButton != null) editEndStateButton.gameObject.SetActive(visible);
            if (resetStartStateButton != null) resetStartStateButton.gameObject.SetActive(visible);
            if (resetEndStateButton != null) resetEndStateButton.gameObject.SetActive(visible);
            if (grtHintLabel != null) grtHintLabel.gameObject.SetActive(visible);
        }

        void EnsureEditStatesControlRefs()
        {
            Transform root = kaplaoOnlyRoot != null ? kaplaoOnlyRoot.transform : transform;
            leaveEditStatesButton ??= FindChildComponent<Button>(root, "LeaveEditStatesButton");
            editStartStateButton ??= FindChildComponent<Button>(root, "EditStartStateButton");
            editEndStateButton ??= FindChildComponent<Button>(root, "EditEndStateButton");
            resetStartStateButton ??= FindChildComponent<Button>(root, "ResetStartStateButton");
            resetEndStateButton ??= FindChildComponent<Button>(root, "ResetEndStateButton");
            grtHintLabel ??= FindChildComponent<TextMeshProUGUI>(root, "ExecutorEditStatesHintLabel");
        }

        void HideKaplaoUiExceptEditStatesControls()
        {
            hiddenUiObjects.Clear();
            hiddenUiObjectsWereActive.Clear();
            EnsureEditStatesControlRefs();

            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
                canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);

            HideChildren(kaplaoOnlyRoot != null ? kaplaoOnlyRoot.transform : null);
            if (canvas != null)
                HideNamed(canvas.transform, ExtraHudNamesToHide);
        }

        void HideChildren(Transform hideRoot)
        {
            if (hideRoot == null)
                return;

            for (int i = 0; i < hideRoot.childCount; i++)
            {
                GameObject child = hideRoot.GetChild(i).gameObject;
                if (child == null || IsEditStatesControlObject(child))
                    continue;

                HideObject(child);
            }
        }

        void HideNamed(Transform root, string[] names)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                GameObject go = all[i].gameObject;
                if (go == null || IsEditStatesControlObject(go))
                    continue;

                for (int n = 0; n < names.Length; n++)
                {
                    if (go.name == names[n])
                    {
                        HideObject(go);
                        break;
                    }
                }
            }
        }

        void HideObject(GameObject child)
        {
            if (hiddenUiObjects.Contains(child))
                return;

            hiddenUiObjects.Add(child);
            hiddenUiObjectsWereActive.Add(child.activeSelf);
            child.SetActive(false);
        }

        bool IsEditStatesControlObject(GameObject child)
        {
            if (child == null)
                return false;

            for (int i = 0; i < EditStatesControlNames.Length; i++)
            {
                if (child.name == EditStatesControlNames[i])
                    return true;
            }

            return false;
        }

        void RestoreKaplaoUiAfterEditStatesMode()
        {
            for (int i = 0; i < hiddenUiObjects.Count; i++)
            {
                if (hiddenUiObjects[i] != null)
                    hiddenUiObjects[i].SetActive(hiddenUiObjectsWereActive[i]);
            }

            hiddenUiObjects.Clear();
            hiddenUiObjectsWereActive.Clear();
        }

        bool TryResolveSelectedExecutor(out ExecutorTile executor)
        {
            executor = null;
            KaplaoCursorServer server = NetworkClient.localPlayer != null
                ? NetworkClient.localPlayer.GetComponent<KaplaoCursorServer>()
                : null;
            if (server == null || server.SelectedCount <= 0)
                return false;

            for (int i = 0; i < server.SelectedTiles.Count; i++)
            {
                if (!LevelScriptObjectUtility.TryResolveTile(server.SelectedTiles[i], out LevelTileServer tile) || tile == null)
                    continue;

                if (tile.TryGetComponent(out ExecutorTile found))
                {
                    executor = found;
                    return true;
                }
            }

            return false;
        }

        uint[] SnapshotCurrentSelection()
        {
            KaplaoCursorServer server = NetworkClient.localPlayer != null
                ? NetworkClient.localPlayer.GetComponent<KaplaoCursorServer>()
                : null;
            if (server == null || server.SelectedCount <= 0)
                return System.Array.Empty<uint>();

            uint[] result = new uint[server.SelectedCount];
            for (int i = 0; i < server.SelectedCount; i++)
                result[i] = server.SelectedTiles[i];
            return result;
        }

        static void CollectTargetTiles(int group, List<LevelTileServer> output, uint executorNetId)
        {
            output.Clear();
            LevelTileServer[] allTiles = FindObjectsByType<LevelTileServer>(FindObjectsSortMode.None);
            for (int i = 0; i < allTiles.Length; i++)
            {
                LevelTileServer tile = allTiles[i];
                if (tile == null || tile.netId == executorNetId || tile.IsExecutor || tile.IsKeyframe)
                    continue;

                if (tile.HasGroup(group))
                    output.Add(tile);
            }
        }

        static T FindChildComponent<T>(Transform root, string name) where T : Component
        {
            if (root == null)
                return null;

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name != name)
                    continue;

                return all[i].GetComponent<T>();
            }

            return null;
        }
    }
}
