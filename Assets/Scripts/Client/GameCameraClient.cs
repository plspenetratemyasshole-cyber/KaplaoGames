using KaplaoGames.Network;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using KaplaoGames.UI;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Orthographic 2D camera follow/zoom for the LOCAL player.
    /// Put this on Kaplao and Runner player prefabs — NOT on the Main Camera.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    [AddComponentMenu("Kaplao/Client/Game Camera Client (on Kaplao/Runner prefab)")]
    public class GameCameraClient : NetworkBehaviour
    {
        [Header("Follow")]
        [SerializeField] Vector2 kaplaoOffset = Vector2.zero;
        [SerializeField] Vector2 runnerOffset = new Vector2(0f, 1f);
        [SerializeField] float followSmooth = 8f;
        [SerializeField] float cameraZ = -10f;

        [Header("Zoom")]
        [SerializeField] float defaultOrthographicSize = 8f;
        [Tooltip("Orthographic size that counts as Zoom 1x. Smaller size = higher Zoom (e.g. size 4 => 2x).")]
        [SerializeField] float zoomOneXOrthographicSize = 8f;
        [FormerlySerializedAs("minOrthographicSize")]
        [SerializeField] float kaplaoMinOrthographicSize = 3f;
        [FormerlySerializedAs("maxOrthographicSize")]
        [SerializeField] float kaplaoMaxOrthographicSize = 20f;
        [SerializeField] float runnerMinOrthographicSize = 3f;
        [SerializeField] float runnerMaxOrthographicSize = 20f;
        [Tooltip("Per mouse-wheel notch: orthographic size is multiplied/divided by this factor (e.g. 1.1 ≈ ±10% zoom).")]
        [SerializeField] float zoomScrollFactor = 1.1f;
        [SerializeField] float zoomLabelFadeSeconds = 0.5f;

        [Header("Pixel Art")]
        [SerializeField] bool snapCameraToPixels = true;
        [SerializeField] float assetsPixelsPerUnit = 100f;

        [Header("Create Pan")]
        [SerializeField] float panDragThresholdPixels = 4f;

        Camera followCamera;
        float lastPinchDistance = -1f;
        bool freeCameraActive;
        Vector3 freeCameraPosition;
        bool panPointerHeld;
        bool panStartedOnUi;
        bool panDragging;
        Vector2 panPressScreenPosition;
        Vector2 lastPanScreenPosition;
        int panTouchId = -1;
        KaplaoCursorServer cursorServer;
        float lastAppliedOrthoSize = -1f;
        TextMeshProUGUI zoomLabel;
        CanvasGroup zoomLabelCanvasGroup;
        float zoomLabelFadeRemaining;
        Vector3 flyFrom;
        Vector3 flyTo;
        float flyDuration;
        float flyElapsed = -1f;

        /// <summary>True while LMB camera pan is active (blocks create cursor clicks).</summary>
        public static bool IsBlockingCursor { get; private set; }

        /// <summary>True on the release frame after a pan gesture — click must not place/select.</summary>
        public static bool CancelClickOnRelease { get; private set; }

        public static bool ConsumeCancelClickOnRelease()
        {
            bool value = CancelClickOnRelease;
            CancelClickOnRelease = false;
            return value;
        }

        public Vector2 CameraWorldCenter
        {
            get
            {
                if (followCamera == null)
                    return transform.position;
                Vector3 p = followCamera.transform.position;
                return new Vector2(p.x, p.y);
            }
        }

        public void FlyToWorld(Vector2 worldPosition, float duration = 0.45f)
        {
            if (followCamera == null)
                followCamera = Camera.main;
            if (followCamera == null)
                return;

            freeCameraActive = true;
            flyFrom = followCamera.transform.position;
            flyTo = new Vector3(worldPosition.x, worldPosition.y, cameraZ);
            flyDuration = Mathf.Max(0.01f, duration);
            flyElapsed = 0f;
        }

        public override void OnStartLocalPlayer()
        {
            if (GetComponent<Camera>() != null)
            {
                Debug.LogError(
                    "[GameCameraClient] This component is on a Camera. Move it to the Kaplao/Runner player prefab " +
                    "(same object as NetworkPlayer). Remove NetworkIdentity from the Main Camera.");
            }

            followCamera = Camera.main;
            if (followCamera == null)
                followCamera = FindFirstObjectByType<Camera>();

            cursorServer = GetComponent<KaplaoCursorServer>();
            if (followCamera == null)
            {
                Debug.LogWarning("[GameCameraClient] Main Camera not found.");
                return;
            }

            followCamera.orthographic = true;
            followCamera.orthographicSize = defaultOrthographicSize;
            lastAppliedOrthoSize = followCamera.orthographicSize;
            followCamera.transform.position = new Vector3(transform.position.x, transform.position.y, cameraZ);
            freeCameraPosition = followCamera.transform.position;

            if (cursorServer != null)
            {
                cursorServer.HostGameModeChanged += OnHostGameModeChanged;
                KaplaoLocalGameModePredictor.Changed += OnPredictedGameModeChanged;
            }

            EnsureZoomLabel();
        }

        public override void OnStopLocalPlayer()
        {
            if (cursorServer != null)
                cursorServer.HostGameModeChanged -= OnHostGameModeChanged;

            KaplaoLocalGameModePredictor.Changed -= OnPredictedGameModeChanged;

            HideZoomLabelImmediate();
        }

        void OnHostGameModeChanged(HostGameMode mode)
        {
            ApplyFreeCameraForMode(KaplaoLocalGameModePredictor.GetEffective(cursorServer));
        }

        void OnPredictedGameModeChanged(HostGameMode mode)
        {
            ApplyFreeCameraForMode(mode);
        }

        void ApplyFreeCameraForMode(HostGameMode mode)
        {
            if (followCamera == null)
                return;

            bool wantFree = mode == HostGameMode.Create || mode == HostGameMode.Code;
            if (wantFree && !freeCameraActive)
            {
                freeCameraPosition = followCamera.transform.position;
                freeCameraActive = true;
            }
            else if (!wantFree && freeCameraActive)
            {
                ExitFreeCameraToKaplao();
            }

            if (mode != HostGameMode.Create)
                HideZoomLabelImmediate();
        }

        void ExitFreeCameraToKaplao()
        {
            if (!freeCameraActive || followCamera == null)
                return;

            Vector3 center = freeCameraPosition;
            transform.position = new Vector3(center.x, center.y, transform.position.z);
            freeCameraActive = false;
            ResetPanState();
        }

        void Update()
        {
            if (!isLocalPlayer || followCamera == null)
                return;

            HostGameMode mode = KaplaoLocalGameModePredictor.GetEffective(cursorServer);
            bool wantFree = mode == HostGameMode.Create || mode == HostGameMode.Code;
            if (!wantFree)
            {
                // Predicted Usual can arrive before SyncVar — still snap Kaplao to camera center.
                if (freeCameraActive)
                    ExitFreeCameraToKaplao();
                return;
            }

            if (!freeCameraActive)
            {
                freeCameraPosition = followCamera.transform.position;
                freeCameraActive = true;
            }

            ApplyCreateModePan();
        }

        void LateUpdate()
        {
            if (!isLocalPlayer || followCamera == null)
                return;

            ApplyScrollZoom();
            ApplyPinchZoom();
            UpdateZoomLabelFade();

            HostGameMode mode = KaplaoLocalGameModePredictor.GetEffective(cursorServer);
            bool wantFree = mode == HostGameMode.Create || mode == HostGameMode.Code;
            if (wantFree)
            {
                if (!freeCameraActive)
                {
                    freeCameraPosition = followCamera.transform.position;
                    freeCameraActive = true;
                }

                if (flyElapsed >= 0f)
                {
                    flyElapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(flyElapsed / flyDuration);
                    t = 1f - (1f - t) * (1f - t);
                    Vector3 pos = Vector3.Lerp(flyFrom, flyTo, t);
                    freeCameraPosition = pos;
                    if (t >= 1f)
                        flyElapsed = -1f;
                }

                followCamera.transform.position = SnapCameraPosition(freeCameraPosition);
                return;
            }

            if (freeCameraActive)
                ExitFreeCameraToKaplao();

            Vector2 offset = TryGetComponent(out NetworkPlayer networkPlayer) &&
                             networkPlayer.role == PlayerRole.Kaplao
                ? kaplaoOffset
                : runnerOffset;

            Vector3 target = new Vector3(
                transform.position.x + offset.x,
                transform.position.y + offset.y,
                cameraZ);

            followCamera.transform.position = Vector3.Lerp(
                followCamera.transform.position,
                target,
                followSmooth * Time.deltaTime);

            followCamera.transform.position = SnapCameraPosition(followCamera.transform.position);
        }

        void ApplyCreateModePan()
        {
            bool shiftHeld = (Keyboard.current != null &&
                              (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed)) ||
                             MobileInputState.ShiftHeld;
            bool inTransform = KaplaoLocalTransformSession.IsActive;
            if (shiftHeld || inTransform)
            {
                ResetPanState();
                return;
            }

            if (!TryReadPanPointer(out Vector2 screen, out bool pressed, out bool held, out bool released, out bool pressOverUi))
            {
                if (panPointerHeld && !held)
                {
                    if (!panStartedOnUi && panDragging)
                        CancelClickOnRelease = true;
                    ResetPanState();
                }

                return;
            }

            if (pressed)
            {
                panPointerHeld = true;
                panStartedOnUi = pressOverUi;
                panDragging = false;
                IsBlockingCursor = false;
                panPressScreenPosition = screen;
                lastPanScreenPosition = screen;
            }

            if (!held || released || !panPointerHeld)
            {
                if (released || !held)
                {
                    if (panPointerHeld && !panStartedOnUi && panDragging)
                        CancelClickOnRelease = true;
                    ResetPanState();
                }
                return;
            }

            if (panStartedOnUi)
                return;

            if (!panDragging)
            {
                if ((screen - panPressScreenPosition).sqrMagnitude <
                    panDragThresholdPixels * panDragThresholdPixels)
                {
                    return;
                }

                panDragging = true;
                IsBlockingCursor = true;
                lastPanScreenPosition = screen;
                return;
            }

            Vector3 prevWorld = followCamera.ScreenToWorldPoint(new Vector3(lastPanScreenPosition.x, lastPanScreenPosition.y, 0f));
            Vector3 currWorld = followCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));
            Vector3 delta = prevWorld - currWorld;
            freeCameraPosition += new Vector3(delta.x, delta.y, 0f);
            lastPanScreenPosition = screen;
        }

        bool TryReadPanPointer(
            out Vector2 screen,
            out bool pressed,
            out bool held,
            out bool released,
            out bool pressOverUi)
        {
            screen = default;
            pressed = false;
            held = false;
            released = false;
            pressOverUi = false;

            Mouse mouse = Mouse.current;
            if (mouse != null &&
                (mouse.leftButton.isPressed ||
                 mouse.leftButton.wasPressedThisFrame ||
                 mouse.leftButton.wasReleasedThisFrame))
            {
                screen = mouse.position.ReadValue();
                pressed = mouse.leftButton.wasPressedThisFrame;
                held = mouse.leftButton.isPressed;
                released = mouse.leftButton.wasReleasedThisFrame;
                pressOverUi = KaplaoUiPointer.IsBlockingCameraPanAtScreen(screen);
                panTouchId = -1;
                return true;
            }

            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null)
                return false;

            var touches = touchscreen.touches;
            UnityEngine.InputSystem.Controls.TouchControl candidate = null;

            if (panTouchId >= 0)
            {
                for (int i = 0; i < touches.Count; i++)
                {
                    var touch = touches[i];
                    if (touch.touchId.ReadValue() == panTouchId)
                    {
                        candidate = touch;
                        break;
                    }
                }
            }

            if (candidate == null)
            {
                for (int i = 0; i < touches.Count; i++)
                {
                    var touch = touches[i];
                    int touchId = touch.touchId.ReadValue();
                    if (touchId <= 0)
                        continue;

                    if (!touch.press.isPressed &&
                        !touch.press.wasPressedThisFrame &&
                        !touch.press.wasReleasedThisFrame)
                    {
                        continue;
                    }

                    candidate = touch;
                    break;
                }
            }

            if (candidate == null)
            {
                panTouchId = -1;
                return false;
            }

            int id = candidate.touchId.ReadValue();
            if (id <= 0)
            {
                panTouchId = -1;
                return false;
            }

            screen = candidate.position.ReadValue();
            pressed = candidate.press.wasPressedThisFrame;
            held = candidate.press.isPressed;
            released = candidate.press.wasReleasedThisFrame;
            pressOverUi = MobileInputState.IsUiPointer(id) ||
                          (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(id));

            panTouchId = held || pressed ? id : -1;
            if (released)
                panTouchId = -1;

            return true;
        }

        void ResetPanState()
        {
            panPointerHeld = false;
            panStartedOnUi = false;
            panDragging = false;
            IsBlockingCursor = false;
            panTouchId = -1;
        }

        Vector3 SnapCameraPosition(Vector3 position)
        {
            position.z = cameraZ;
            if (!snapCameraToPixels || assetsPixelsPerUnit <= 0f)
                return position;

            position.x = Mathf.Round(position.x * assetsPixelsPerUnit) / assetsPixelsPerUnit;
            position.y = Mathf.Round(position.y * assetsPixelsPerUnit) / assetsPixelsPerUnit;
            return position;
        }

        void GetZoomLimits(out float minSize, out float maxSize)
        {
            bool isKaplao = TryGetComponent(out NetworkPlayer player) && player.role == PlayerRole.Kaplao;
            if (isKaplao || cursorServer != null)
            {
                minSize = CreateModeEditorSettings.GetKaplaoMinOrtho(kaplaoMinOrthographicSize);
                maxSize = CreateModeEditorSettings.GetKaplaoMaxOrtho(kaplaoMaxOrthographicSize);
                return;
            }

            minSize = Mathf.Max(0.01f, runnerMinOrthographicSize);
            maxSize = Mathf.Max(minSize, runnerMaxOrthographicSize);
        }

        public float BaseKaplaoMinOrthographicSize => Mathf.Max(0.01f, kaplaoMinOrthographicSize);
        public float BaseKaplaoMaxOrthographicSize => Mathf.Max(BaseKaplaoMinOrthographicSize, kaplaoMaxOrthographicSize);

        public void ClampToCurrentZoomLimits()
        {
            if (followCamera == null)
                return;

            GetZoomLimits(out float minSize, out float maxSize);
            followCamera.orthographicSize = Mathf.Clamp(followCamera.orthographicSize, minSize, maxSize);
            lastAppliedOrthoSize = followCamera.orthographicSize;
        }

        void SetOrthographicSize(float size, Vector2? zoomScreenPoint = null)
        {
            GetZoomLimits(out float minSize, out float maxSize);
            float clamped = Mathf.Clamp(size, minSize, maxSize);
            float oldSize = followCamera.orthographicSize;

            Vector3 worldBefore = Vector3.zero;
            bool zoomToCursor = zoomScreenPoint.HasValue && !Mathf.Approximately(oldSize, clamped);
            if (zoomToCursor)
            {
                worldBefore = ScreenToCameraWorld(zoomScreenPoint.Value);
            }

            followCamera.orthographicSize = clamped;
            lastAppliedOrthoSize = clamped;

            if (zoomToCursor)
            {
                Vector3 worldAfter = ScreenToCameraWorld(zoomScreenPoint.Value);
                Vector3 delta = worldBefore - worldAfter;
                Vector3 camPos = followCamera.transform.position + new Vector3(delta.x, delta.y, 0f);
                camPos.z = cameraZ;

                // Keep world point under cursor fixed while zooming (Create free-cam).
                HostGameMode mode = KaplaoLocalGameModePredictor.GetEffective(cursorServer);
                if (freeCameraActive || mode == HostGameMode.Create || mode == HostGameMode.Code)
                {
                    freeCameraPosition = camPos;
                    followCamera.transform.position = SnapCameraPosition(freeCameraPosition);
                }
            }

            ShowZoomLabel();
        }

        Vector3 ScreenToCameraWorld(Vector2 screen)
        {
            // Orthographic: keep the same depth as the camera's view plane.
            float depth = Mathf.Abs(followCamera.transform.position.z);
            return followCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
        }

        void ApplyScrollZoom()
        {
            if (KaplaoUiPointer.IsBlockingCameraZoom())
                return;

            Mouse mouse = Mouse.current;
            if (mouse == null)
                return;

            float scroll = mouse.scroll.y.ReadValue();
            if (Mathf.Approximately(scroll, 0f))
                return;

            float direction = Mathf.Sign(scroll);
            float factor = Mathf.Max(1.01f, zoomScrollFactor);
            // Scroll up (direction > 0) zooms in → smaller orthographic size.
            float nextSize = followCamera.orthographicSize * Mathf.Pow(factor, -direction);
            Vector2 screen = mouse.position.ReadValue();
            SetOrthographicSize(nextSize, screen);
        }

        void ApplyPinchZoom()
        {
            if (!MobileControlsConfig.IsMobileControlsEnabled)
                return;

            if (KaplaoUiPointer.IsOverUi() || MobileInputState.HasAnyUiPointer())
            {
                lastPinchDistance = -1f;
                return;
            }

            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null)
            {
                lastPinchDistance = -1f;
                return;
            }

            Vector2? firstTouch = null;
            Vector2? secondTouch = null;

            for (int i = 0; i < touchscreen.touches.Count; i++)
            {
                var touch = touchscreen.touches[i];
                if (!touch.press.isPressed)
                    continue;

                int touchId = touch.touchId.ReadValue();
                if (touchId <= 0)
                    continue;

                if (MobileInputState.IsUiPointer(touchId))
                    continue;

                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touchId))
                    continue;

                Vector2 position = touch.position.ReadValue();
                if (firstTouch == null)
                    firstTouch = position;
                else
                {
                    secondTouch = position;
                    break;
                }
            }

            if (firstTouch == null || secondTouch == null)
            {
                lastPinchDistance = -1f;
                return;
            }

            float distance = Vector2.Distance(firstTouch.Value, secondTouch.Value);
            if (lastPinchDistance > 0f && distance > 0.01f)
            {
                // Relative pinch: fingers apart → zoom in (smaller ortho).
                float nextSize = followCamera.orthographicSize * (lastPinchDistance / distance);
                Vector2 pivot = (firstTouch.Value + secondTouch.Value) * 0.5f;
                SetOrthographicSize(nextSize, pivot);
            }

            lastPinchDistance = distance;
        }

        void ShowZoomLabel()
        {
            if (cursorServer == null || !KaplaoLocalGameModePredictor.IsCreate(cursorServer))
                return;

            EnsureZoomLabel();
            if (zoomLabel == null)
                return;

            float oneX = Mathf.Max(0.01f, zoomOneXOrthographicSize);
            float zoom = oneX / Mathf.Max(0.01f, followCamera.orthographicSize);
            zoomLabel.text = FormatZoomLabel(zoom);
            zoomLabelFadeRemaining = Mathf.Max(0.01f, zoomLabelFadeSeconds);
            if (zoomLabelCanvasGroup != null)
                zoomLabelCanvasGroup.alpha = 1f;
            zoomLabel.gameObject.SetActive(true);
        }

        void UpdateZoomLabelFade()
        {
            if (zoomLabel == null || zoomLabelCanvasGroup == null || zoomLabelFadeRemaining <= 0f)
                return;

            zoomLabelFadeRemaining -= Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(zoomLabelFadeRemaining / Mathf.Max(0.01f, zoomLabelFadeSeconds));
            zoomLabelCanvasGroup.alpha = t;
            if (zoomLabelFadeRemaining <= 0f)
                HideZoomLabelImmediate();
        }

        void HideZoomLabelImmediate()
        {
            zoomLabelFadeRemaining = 0f;
            if (zoomLabelCanvasGroup != null)
                zoomLabelCanvasGroup.alpha = 0f;
            if (zoomLabel != null)
                zoomLabel.gameObject.SetActive(false);
        }

        static string FormatZoomLabel(float zoom)
        {
            float rounded = Mathf.Round(zoom * 100f) / 100f;
            if (Mathf.Approximately(rounded, Mathf.Round(rounded)))
                return $"Zoom {rounded:0}x";
            return $"Zoom {rounded:0.##}x";
        }

        void EnsureZoomLabel()
        {
            if (zoomLabel != null)
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

            Transform existing = canvas.transform.Find("ZoomLabel");
            GameObject root;
            if (existing != null)
            {
                root = existing.gameObject;
            }
            else
            {
                root = new GameObject("ZoomLabel", typeof(RectTransform), typeof(CanvasGroup));
                root.transform.SetParent(canvas.transform, false);
            }

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.75f);
            rect.anchorMax = new Vector2(0.5f, 0.75f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(420f, 64f);

            zoomLabelCanvasGroup = root.GetComponent<CanvasGroup>();
            if (zoomLabelCanvasGroup == null)
                zoomLabelCanvasGroup = root.AddComponent<CanvasGroup>();
            zoomLabelCanvasGroup.blocksRaycasts = false;
            zoomLabelCanvasGroup.interactable = false;
            zoomLabelCanvasGroup.alpha = 0f;

            zoomLabel = root.GetComponent<TextMeshProUGUI>();
            if (zoomLabel == null)
                zoomLabel = root.AddComponent<TextMeshProUGUI>();

            zoomLabel.alignment = TextAlignmentOptions.Center;
            zoomLabel.fontSize = 36f;
            zoomLabel.color = Color.white;
            zoomLabel.raycastTarget = false;
            zoomLabel.textWrappingMode = TextWrappingModes.NoWrap;
            zoomLabel.fontStyle = FontStyles.Bold;
            if (TMP_Settings.defaultFontAsset != null)
                zoomLabel.font = TMP_Settings.defaultFontAsset;
            zoomLabel.outlineWidth = 0.25f;
            zoomLabel.outlineColor = Color.black;
            zoomLabel.gameObject.SetActive(false);
        }
    }
}
