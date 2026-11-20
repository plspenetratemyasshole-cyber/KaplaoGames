using KaplaoGames.Client;
using KaplaoGames.Network;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/UI/Mobile Gameplay UI")]
    public class MobileGameplayUi : MonoBehaviour
    {
        [SerializeField] GameObject root;
        [SerializeField] GameObject kaplaoRoot;
        [SerializeField] GameObject runnerRoot;
        [SerializeField] GameObject pauseButton;
        [SerializeField] MobileJoystick kaplaoMoveJoystick;
        [SerializeField] MobileJoystick runnerMoveJoystick;
        [SerializeField] GameObject kaplaoSelectionMovePad;
        [SerializeField] GameObject createToolSlots;
        [SerializeField] GameObject usualToolSlots;
        [SerializeField] GameObject kaplaoRightButtons;

        const float ToolNameLabelFadeSeconds = 0.5f;

        bool lastMobileEnabled;
        PlayerRole? lastRole;
        bool lastSelectionPadVisible;
        HostGameMode? lastGameMode;
        bool legacyCombinedToolSlots;
        KaplaoToolMode? lastToolMode;
        TextMeshProUGUI toolNameLabel;
        CanvasGroup toolNameLabelCanvasGroup;
        RectTransform toolNameLabelRect;
        RectTransform toolNameLabelCanvasRect;
        float toolNameLabelFadeRemaining;

        void Awake()
        {
            ResolveToolSlotReferences();
            ResolveRightButtonsReference();
        }

        void ResolveRightButtonsReference()
        {
            if (kaplaoRightButtons != null || kaplaoRoot == null)
                return;

            Transform right = kaplaoRoot.transform.Find("RightButtons");
            if (right != null)
                kaplaoRightButtons = right.gameObject;
        }

        void ResolveToolSlotReferences()
        {
            if (kaplaoRoot == null)
                return;

            Transform kaplao = kaplaoRoot.transform;
            if (createToolSlots == null)
            {
                Transform create = kaplao.Find("CreateToolSlots");
                if (create != null)
                    createToolSlots = create.gameObject;
            }

            if (usualToolSlots == null)
            {
                Transform usual = kaplao.Find("UsualToolSlots");
                if (usual != null)
                    usualToolSlots = usual.gameObject;
            }

            if (createToolSlots == null && usualToolSlots == null)
            {
                Transform legacy = kaplao.Find("ToolSlots");
                if (legacy != null)
                {
                    createToolSlots = legacy.gameObject;
                    usualToolSlots = legacy.gameObject;
                    legacyCombinedToolSlots = true;
                }
            }
        }

        void OnEnable()
        {
            lastMobileEnabled = !MobileControlsConfig.IsMobileControlsEnabled;
            lastRole = null;
            lastSelectionPadVisible = false;
            lastGameMode = null;
            ApplyUiState(force: true);
        }

        void OnDisable()
        {
            ResetState();
        }

        void Update()
        {
            ApplyUiState(force: false);

            PlayerRole? role = ResolveLocalRole();
            bool mobile = MobileControlsConfig.IsMobileControlsEnabled;

            // Tool slots follow Kaplao mode even without Build for Phone.
            ApplyToolSlotsVisibility(force: false);
            UpdateToolNameLabel();

            if (!mobile)
            {
                ResetMobileInputOnly();
                return;
            }

            if (role == PlayerRole.Runner)
            {
                MobileInputState.RunnerMove = runnerMoveJoystick != null
                    ? runnerMoveJoystick.Value
                    : Vector2.zero;
                MobileInputState.RunnerMoveJoystickHeld = runnerMoveJoystick != null && runnerMoveJoystick.IsHeld;
                MobileInputState.KaplaoMove = Vector2.zero;
                MobileInputState.KaplaoMoveJoystickHeld = false;
                return;
            }

            HostGameMode mode = ResolveKaplaoGameMode();
            bool selectionPadVisible = ShouldShowSelectionMovePad();
            if (selectionPadVisible != lastSelectionPadVisible)
                ApplySelectionPadVisibility();

            // Create/Code: no move joystick — camera is finger-panned.
            if (mode == HostGameMode.Create || mode == HostGameMode.Code)
            {
                MobileInputState.KaplaoMove = Vector2.zero;
                MobileInputState.KaplaoMoveJoystickHeld = false;
                MobileInputState.RunnerMove = Vector2.zero;
                MobileInputState.RunnerMoveJoystickHeld = false;
                return;
            }

            if (lastSelectionPadVisible)
            {
                MobileInputState.KaplaoMove = Vector2.zero;
                MobileInputState.KaplaoMoveJoystickHeld = false;
                MobileInputState.RunnerMove = Vector2.zero;
                MobileInputState.RunnerMoveJoystickHeld = false;
                return;
            }

            MobileInputState.KaplaoMove = kaplaoMoveJoystick != null
                ? kaplaoMoveJoystick.Value
                : Vector2.zero;
            MobileInputState.KaplaoMoveJoystickHeld = kaplaoMoveJoystick != null && kaplaoMoveJoystick.IsHeld;
            MobileInputState.RunnerMove = Vector2.zero;
            MobileInputState.RunnerMoveJoystickHeld = false;
        }

        void ApplyUiState(bool force)
        {
            bool mobile = MobileControlsConfig.IsMobileControlsEnabled;
            PlayerRole? role = ResolveLocalRole();
            bool needKaplaoToolSlots = role == PlayerRole.Kaplao;
            // Keep root alive for tool slots on PC; mobile chrome is toggled separately.
            bool needRoot = needKaplaoToolSlots || (mobile && role != null);

            if (!force && mobile == lastMobileEnabled && role == lastRole)
            {
                ApplyMobileChromeVisibility(mobile, role);
                return;
            }

            lastMobileEnabled = mobile;
            lastRole = role;

            if (root != null)
                root.SetActive(needRoot);

            if (!needRoot)
            {
                ResetState();
                lastGameMode = null;
                return;
            }

            if (kaplaoRoot != null)
                kaplaoRoot.SetActive(role == PlayerRole.Kaplao);
            if (runnerRoot != null)
                runnerRoot.SetActive(mobile && role == PlayerRole.Runner);

            ApplyMobileChromeVisibility(mobile, role);

            if (force)
            {
                ApplySelectionPadVisibility();
                ApplyToolSlotsVisibility(force: true);
            }
        }

        void ApplyMobileChromeVisibility(bool mobile, PlayerRole? role)
        {
            bool showKaplaoMobile = mobile && role == PlayerRole.Kaplao;
            bool showRunnerMobile = mobile && role == PlayerRole.Runner;
            HostGameMode mode = ResolveKaplaoGameMode();
            bool createOrCode = mode == HostGameMode.Create || mode == HostGameMode.Code;
            bool selectionPad = showKaplaoMobile && ShouldShowSelectionMovePad();

            if (pauseButton != null)
                pauseButton.SetActive(mobile && role != null);

            // Joystick only in Usual Kaplao mode (Create/Code use finger pan).
            if (kaplaoMoveJoystick != null)
                kaplaoMoveJoystick.gameObject.SetActive(showKaplaoMobile && !createOrCode && !selectionPad);

            if (kaplaoSelectionMovePad != null)
                kaplaoSelectionMovePad.SetActive(selectionPad);

            lastSelectionPadVisible = selectionPad;

            if (runnerMoveJoystick != null)
                runnerMoveJoystick.gameObject.SetActive(showRunnerMobile);

            // Shift/Ctrl are phone-only; tool slots stay available on PC.
            if (kaplaoRightButtons != null)
                kaplaoRightButtons.SetActive(showKaplaoMobile);

            if (!mobile)
            {
                if (kaplaoMoveJoystick != null)
                    kaplaoMoveJoystick.gameObject.SetActive(false);
                if (runnerMoveJoystick != null)
                    runnerMoveJoystick.gameObject.SetActive(false);
                if (kaplaoSelectionMovePad != null)
                    kaplaoSelectionMovePad.SetActive(false);
                if (kaplaoRightButtons != null)
                    kaplaoRightButtons.SetActive(false);
            }
        }

        void ApplyToolSlotsVisibility(bool force)
        {
            PlayerRole? role = ResolveLocalRole();
            if (role != PlayerRole.Kaplao || KaplaoExecutorEditorUi.BlocksCreateHud)
            {
                if (createToolSlots != null)
                    createToolSlots.SetActive(false);
                if (usualToolSlots != null)
                    usualToolSlots.SetActive(false);
                lastGameMode = null;
                return;
            }

            // Ensure parents are on so slots can show without Build for Phone.
            if (root != null && !root.activeSelf)
                root.SetActive(true);
            if (kaplaoRoot != null && !kaplaoRoot.activeSelf)
                kaplaoRoot.SetActive(true);

            HostGameMode mode = ResolveKaplaoGameMode();
            if (!force && mode == lastGameMode)
                return;

            lastGameMode = mode;

            bool showCreate = mode == HostGameMode.Create;
            bool showUsual = mode == HostGameMode.Usual;

            if (legacyCombinedToolSlots && createToolSlots != null)
            {
                createToolSlots.SetActive(showCreate || showUsual);
                SetLegacySlotButtonsVisible(showCreate);
                return;
            }

            if (createToolSlots != null)
                createToolSlots.SetActive(showCreate);
            if (usualToolSlots != null)
                usualToolSlots.SetActive(showUsual);
        }

        void UpdateToolNameLabel()
        {
            if (createToolSlots == null || !createToolSlots.activeInHierarchy)
            {
                lastToolMode = null;
                HideToolNameLabelImmediate();
                return;
            }

            KaplaoCursorClient cursor = KaplaoLocalBindingUtility.ResolveLocalCursorClient();
            if (cursor == null)
            {
                lastToolMode = null;
                HideToolNameLabelImmediate();
                return;
            }

            KaplaoToolMode tool = cursor.ToolMode;
            if (lastToolMode == null)
            {
                lastToolMode = tool;
            }
            else if (lastToolMode != tool)
            {
                lastToolMode = tool;
                ShowToolNameLabel(FormatToolName(tool));
            }

            if (toolNameLabel == null || toolNameLabelCanvasGroup == null || toolNameLabelFadeRemaining <= 0f)
                return;

            PositionToolNameLabel(cursor);
            toolNameLabelFadeRemaining -= Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(toolNameLabelFadeRemaining / ToolNameLabelFadeSeconds);
            toolNameLabelCanvasGroup.alpha = t;
            if (toolNameLabelFadeRemaining <= 0f)
                HideToolNameLabelImmediate();
        }

        void ShowToolNameLabel(string text)
        {
            EnsureToolNameLabel();
            if (toolNameLabel == null)
                return;

            toolNameLabel.text = text;
            toolNameLabelFadeRemaining = ToolNameLabelFadeSeconds;
            if (toolNameLabelCanvasGroup != null)
                toolNameLabelCanvasGroup.alpha = 1f;
            toolNameLabel.gameObject.SetActive(true);

            KaplaoCursorClient cursor = KaplaoLocalBindingUtility.ResolveLocalCursorClient();
            if (cursor != null)
                PositionToolNameLabel(cursor);
        }

        void PositionToolNameLabel(KaplaoCursorClient cursor)
        {
            if (toolNameLabelRect == null || toolNameLabelCanvasRect == null || cursor == null)
                return;

            Camera cam = Camera.main;
            if (cam == null)
                return;

            Vector3 world = cursor.LocalWorldPosition;
            float offset = 0.7f;
            if (cursor.TryGetComponent(out KaplaoCursorServer cursorServer))
            {
                world = cursorServer.GetToolPreviewWorldPosition();
                offset = cursorServer.GetToolPreviewLabelWorldOffset();
            }

            Vector3 screen = cam.WorldToScreenPoint(world + Vector3.up * offset);
            if (screen.z < 0f)
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    toolNameLabelCanvasRect, screen, null, out Vector2 local))
                return;

            toolNameLabelRect.anchoredPosition = local;
        }

        void HideToolNameLabelImmediate()
        {
            toolNameLabelFadeRemaining = 0f;
            if (toolNameLabelCanvasGroup != null)
                toolNameLabelCanvasGroup.alpha = 0f;
            if (toolNameLabel != null)
                toolNameLabel.gameObject.SetActive(false);
        }

        void EnsureToolNameLabel()
        {
            DestroyLayoutBreakingToolNameLabel();
            if (toolNameLabel != null)
                return;

            GameObject canvasObject = GameObject.Find("KaplaoZoomHud");
            Canvas canvas;
            if (canvasObject == null)
            {
                canvasObject = new GameObject("KaplaoZoomHud", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 5000;
                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
            }
            else
            {
                canvas = canvasObject.GetComponent<Canvas>();
            }

            toolNameLabelCanvasRect = canvas.transform as RectTransform;

            Transform existing = canvas.transform.Find("ToolNameLabel");
            GameObject labelRoot;
            if (existing != null)
            {
                labelRoot = existing.gameObject;
            }
            else
            {
                labelRoot = new GameObject("ToolNameLabel", typeof(RectTransform), typeof(CanvasGroup));
                labelRoot.transform.SetParent(canvas.transform, false);
            }

            toolNameLabelRect = labelRoot.GetComponent<RectTransform>();
            toolNameLabelRect.anchorMin = new Vector2(0.5f, 0.5f);
            toolNameLabelRect.anchorMax = new Vector2(0.5f, 0.5f);
            toolNameLabelRect.pivot = new Vector2(0.5f, 0f);
            toolNameLabelRect.sizeDelta = new Vector2(420f, 64f);

            toolNameLabelCanvasGroup = labelRoot.GetComponent<CanvasGroup>();
            if (toolNameLabelCanvasGroup == null)
                toolNameLabelCanvasGroup = labelRoot.AddComponent<CanvasGroup>();
            toolNameLabelCanvasGroup.blocksRaycasts = false;
            toolNameLabelCanvasGroup.interactable = false;
            toolNameLabelCanvasGroup.alpha = 0f;

            toolNameLabel = labelRoot.GetComponent<TextMeshProUGUI>();
            if (toolNameLabel == null)
                toolNameLabel = labelRoot.AddComponent<TextMeshProUGUI>();

            toolNameLabel.alignment = TextAlignmentOptions.Center;
            toolNameLabel.fontSize = 36f;
            toolNameLabel.color = Color.white;
            toolNameLabel.raycastTarget = false;
            toolNameLabel.textWrappingMode = TextWrappingModes.NoWrap;
            toolNameLabel.fontStyle = FontStyles.Bold;
            if (TMP_Settings.defaultFontAsset != null)
                toolNameLabel.font = TMP_Settings.defaultFontAsset;
            toolNameLabel.outlineWidth = 0.25f;
            toolNameLabel.outlineColor = Color.black;
            toolNameLabel.gameObject.SetActive(false);
        }

        void DestroyLayoutBreakingToolNameLabel()
        {
            if (createToolSlots == null)
                return;

            Transform leftover = createToolSlots.transform.Find("ToolNameLabel");
            if (leftover != null)
                Destroy(leftover.gameObject);
        }

        static string FormatToolName(KaplaoToolMode tool)
        {
            return tool switch
            {
                KaplaoToolMode.Select => "Select",
                KaplaoToolMode.Block => "Block",
                KaplaoToolMode.Spike => "Spike",
                KaplaoToolMode.Trigger => "Trigger",
                KaplaoToolMode.Delete => "Delete",
                KaplaoToolMode.Particle => "Particle",
                KaplaoToolMode.Keyframe => "Keyframe",
                KaplaoToolMode.Executor => "Executor",
                KaplaoToolMode.Grab => "Grab",
                KaplaoToolMode.Palm => "Palm",
                KaplaoToolMode.Gun => "Gun",
                _ => tool.ToString()
            };
        }

        void SetLegacySlotButtonsVisible(bool showCreateSlots)
        {
            if (createToolSlots == null)
                return;

            for (int i = 1; i <= MobileInputState.MaxToolSlot; i++)
            {
                Transform slot = createToolSlots.transform.Find($"Slot{i}Button");
                if (slot == null)
                    continue;

                bool visible = showCreateSlots || i <= 3;
                slot.gameObject.SetActive(visible);
            }
        }

        void ApplySelectionPadVisibility()
        {
            bool mobile = MobileControlsConfig.IsMobileControlsEnabled;
            HostGameMode mode = ResolveKaplaoGameMode();
            bool createOrCode = mode == HostGameMode.Create || mode == HostGameMode.Code;
            bool selectionPadVisible = mobile && ShouldShowSelectionMovePad();
            lastSelectionPadVisible = selectionPadVisible;

            if (kaplaoMoveJoystick != null)
            {
                kaplaoMoveJoystick.gameObject.SetActive(
                    mobile &&
                    lastRole == PlayerRole.Kaplao &&
                    !createOrCode &&
                    !selectionPadVisible);
            }

            if (kaplaoSelectionMovePad != null)
                kaplaoSelectionMovePad.SetActive(selectionPadVisible);
        }

        bool ShouldShowSelectionMovePad()
        {
            if (kaplaoSelectionMovePad == null)
                return false;

            if (NetworkClient.localPlayer == null)
                return false;

            if (LocalPlayerRole.GetOrNull() != PlayerRole.Kaplao)
                return false;

            if (!NetworkClient.localPlayer.TryGetComponent(out KaplaoCursorServer cursorServer))
                return false;

            return KaplaoLocalGameModePredictor.IsCreate(cursorServer) &&
                   cursorServer.SelectedCount > 0;
        }

        static HostGameMode ResolveKaplaoGameMode()
        {
            if (NetworkClient.localPlayer == null)
                return HostGameMode.Usual;

            if (!NetworkClient.localPlayer.TryGetComponent(out KaplaoCursorServer cursorServer))
                return HostGameMode.Usual;

            return KaplaoLocalGameModePredictor.GetEffective(cursorServer);
        }

        static PlayerRole? ResolveLocalRole()
        {
            return LocalPlayerRole.GetOrNull();
        }

        static void ResetMobileInputOnly()
        {
            MobileInputState.KaplaoMove = Vector2.zero;
            MobileInputState.KaplaoMoveJoystickHeld = false;
            MobileInputState.RunnerMove = Vector2.zero;
            MobileInputState.RunnerMoveJoystickHeld = false;
        }

        static void ResetState()
        {
            ResetMobileInputOnly();
            MobileInputState.ShiftHeld = false;
            MobileInputState.CtrlHeld = false;
            MobileInputState.RunnerLeftHeld = false;
            MobileInputState.RunnerRightHeld = false;
        }
    }
}
