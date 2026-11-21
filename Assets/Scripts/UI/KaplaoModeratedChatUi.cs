using System.Collections;
using System.Collections.Generic;
using KaplaoGames.Client;
using KaplaoGames.Network;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using KaplaoGames.UI.Theme;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace KaplaoGames.UI
{
    /// <summary>
    /// Chat + moderation request UI. Hierarchy created by Kaplao/Setup Moderation Chat UI.
    /// </summary>
    [DisallowMultipleComponent]
    public class KaplaoModeratedChatUi : MonoBehaviour
    {
        [SerializeField] GameObject chatRoot;
        [SerializeField] Transform chatContent;
        [SerializeField] ScrollRect chatScrollRect;
        [SerializeField] TMP_InputField inputField;
        [SerializeField] Button sendButton;
        [SerializeField] TextMeshProUGUI statusText;
        [SerializeField] GameObject moderatorPanel;
        [SerializeField] TextMeshProUGUI moderatorRequestText;
        [SerializeField] Button acceptButton;
        [SerializeField] Button rejectButton;
        [SerializeField] Button kickButton;
        [SerializeField] Button banButton;

        [Header("Overhead Message")]
        [SerializeField] TMP_FontAsset overheadFont;
        [SerializeField] Vector3 overheadLocalOffset = new Vector3(0f, 1.55f, 0f);
        [SerializeField] Vector2 overheadCanvasSize = new Vector2(28f, 1.2f);
        [SerializeField] float overheadWorldScale = 0.01f;
        [SerializeField] int overheadSortingOrder = 200;
        [SerializeField] float overheadFontSize = 54f;
        [SerializeField] float overheadFontSizeMin = 24f;
        [SerializeField] float overheadFontSizeMax = 72f;
        [SerializeField] bool overheadAutoSize = true;
        [SerializeField] bool overheadWordWrap = false;
        [SerializeField] Color overheadColor = Color.white;
        [SerializeField] float overheadMessageHoldSeconds = 5f;
        [SerializeField] float overheadMessageFadeSeconds = 0.5f;
        [Tooltip("How much existing overhead messages move up when a new one appears on the same player.")]
        [SerializeField] Vector3 overheadStackPushOffset = new Vector3(0f, 0.45f, 0f);

        public static bool IsChatInputFocused { get; private set; }

        ModerationUiHooksClient hooks;
        ulong currentModeratorRequestId;
        string currentModeratorSenderName;
        bool submitRequested;
        bool chatWasVisible;
        float chatJustAppearedUntil; // Block focus hotkeys for this duration after chat becomes visible
        readonly List<GameObject> chatLineObjects = new List<GameObject>();
        readonly Queue<(string sender, string message, ulong requestId)> moderatorRequestQueue =
            new Queue<(string, string, ulong)>();
        bool moderatorDecisionInFlight;

        struct OverheadMessage
        {
            public Transform owner;
            public Transform target;
            public TextMeshProUGUI text;
            public float fadeStartTime;
            public float expireTime;
        }

        readonly List<OverheadMessage> overheadMessages = new List<OverheadMessage>();

        Coroutine statusFadeRoutine;

        void Awake()
        {
            if (chatRoot != null)
            {
                Image chatBg = chatRoot.GetComponent<Image>();
                if (chatBg != null)
                {
                    chatBg.sprite = KaplaoUiSprites.RoundedRect(12);
                    chatBg.type = Image.Type.Sliced;
                    chatBg.color = new Color(0.06f, 0.09f, 0.15f, 0.72f);
                }
            }

            if (chatScrollRect != null)
            {
                Mask scrollMask = chatScrollRect.GetComponent<Mask>();
                if (scrollMask != null)
                    Destroy(scrollMask);

                Transform vp = chatScrollRect.viewport != null ? chatScrollRect.viewport.transform : chatScrollRect.transform.Find("Viewport");
                if (vp != null)
                {
                    Mask vpMask = vp.GetComponent<Mask>();
                    if (vpMask != null)
                        Destroy(vpMask);

                    if (vp.GetComponent<RectMask2D>() == null)
                        vp.gameObject.AddComponent<RectMask2D>();
                }
            }
        }

        void OnEnable()
        {
            TryBindHooks();
            if (inputField != null)
                inputField.onEndEdit.AddListener(OnInputEndEdit);
            if (sendButton != null)
                sendButton.onClick.AddListener(TrySendCurrentMessage);

            if (acceptButton != null) acceptButton.onClick.AddListener(() => TryDecide(LevelSessionServer.ModerationDecision.Accept));
            if (rejectButton != null) rejectButton.onClick.AddListener(() => TryDecide(LevelSessionServer.ModerationDecision.Reject));
            if (kickButton != null) kickButton.onClick.AddListener(() => TryDecide(LevelSessionServer.ModerationDecision.Kick));
            if (banButton != null) banButton.onClick.AddListener(() => TryDecide(LevelSessionServer.ModerationDecision.Ban));

            KaplaoLocalGameModePredictor.Changed += OnKaplaoGameModeChanged;
            TrySubscribeKaplaoCursor();
        }

        void OnDisable()
        {
            IsChatInputFocused = false;
            UnbindHooks();
            KaplaoLocalGameModePredictor.Changed -= OnKaplaoGameModeChanged;
            UnsubscribeKaplaoCursor();

            if (inputField != null)
                inputField.onEndEdit.RemoveListener(OnInputEndEdit);
            if (sendButton != null)
                sendButton.onClick.RemoveListener(TrySendCurrentMessage);

            if (acceptButton != null) acceptButton.onClick.RemoveAllListeners();
            if (rejectButton != null) rejectButton.onClick.RemoveAllListeners();
            if (kickButton != null) kickButton.onClick.RemoveAllListeners();
            if (banButton != null) banButton.onClick.RemoveAllListeners();
        }

        KaplaoCursorServer subscribedCursor;

        void TrySubscribeKaplaoCursor()
        {
            if (subscribedCursor != null || NetworkClient.localPlayer == null)
                return;

            if (NetworkClient.localPlayer.TryGetComponent(out KaplaoCursorServer cursor))
            {
                subscribedCursor = cursor;
                subscribedCursor.HostGameModeChanged += OnKaplaoGameModeChanged;
            }
        }

        void UnsubscribeKaplaoCursor()
        {
            if (subscribedCursor == null)
                return;

            subscribedCursor.HostGameModeChanged -= OnKaplaoGameModeChanged;
            subscribedCursor = null;
        }

        void OnKaplaoGameModeChanged(HostGameMode mode)
        {
            if (mode == HostGameMode.Usual)
                ClearInputFocus();
        }

        void TryBindHooks()
        {
            if (hooks != null)
                return;

            hooks = LevelSessionServer.ModerationUiHooks;
            if (hooks == null)
                hooks = UnityEngine.Object.FindFirstObjectByType<ModerationUiHooksClient>();

            if (hooks == null)
                return;

            hooks.SenderModerationPending += OnSenderModerationPending;
            hooks.SenderModerationResult += OnSenderModerationResult;
            hooks.ModeratorModerationRequest += OnModeratorModerationRequest;
            hooks.ChatMessageReceived += OnChatMessageReceived;
        }

        void UnbindHooks()
        {
            if (hooks == null)
                return;

            hooks.SenderModerationPending -= OnSenderModerationPending;
            hooks.SenderModerationResult -= OnSenderModerationResult;
            hooks.ModeratorModerationRequest -= OnModeratorModerationRequest;
            hooks.ChatMessageReceived -= OnChatMessageReceived;
            hooks = null;
        }

        void Update()
        {
            if (hooks == null)
                TryBindHooks();

            TrySubscribeKaplaoCursor();

            IsChatInputFocused = inputField != null && inputField.isFocused && chatRoot != null && chatRoot.activeInHierarchy;

            TryFocusChatFromHotkeys();

            // Capture Enter while still focused (TMP often unfocuses before other listeners).
            if (IsChatInputFocused)
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null &&
                    (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
                {
                    submitRequested = true;
                }
            }

            if (submitRequested)
            {
                submitRequested = false;
                TrySendCurrentMessage();
            }

            UpdateChatVisibility();
            UpdateOverheadMessages();
        }

        void TryFocusChatFromHotkeys()
        {
            if (inputField == null || chatRoot == null || !chatRoot.activeInHierarchy)
                return;

            if (inputField.isFocused)
                return;

            // Chat just appeared (Create -> Usual): block focus hotkeys for a short duration to avoid stealing focus from gameplay.
            if (Time.time < chatJustAppearedUntil)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (!keyboard.slashKey.wasPressedThisFrame && !keyboard.tKey.wasPressedThisFrame)
                return;

            IsChatInputFocused = true;
            StartCoroutine(FocusChatInputNextFrame());
        }

        System.Collections.IEnumerator FocusChatInputNextFrame()
        {
            // Wait one frame so T / slash are not typed into the field.
            yield return null;
            if (inputField == null || chatRoot == null || !chatRoot.activeInHierarchy)
                yield break;

            inputField.text = string.Empty;
            inputField.ActivateInputField();
            inputField.Select();
            IsChatInputFocused = true;
        }

        void OnInputEndEdit(string _)
        {
            // Enter also ends edit; if Update already queued submit, this is a no-op next frame.
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame ||
                 keyboard.enterKey.isPressed || keyboard.numpadEnterKey.isPressed))
            {
                submitRequested = true;
            }
        }

        void UpdateChatVisibility()
        {
            if (chatRoot == null || NetworkClient.localPlayer == null)
                return;

            bool visible = true;

            // Hide chat while any pause menu is open (banned list / player list sit on the sides).
            if (PauseCursorState.IsPauseOpen)
                visible = false;
            else if (NetworkClient.localPlayer.TryGetComponent(out NetworkPlayer np) &&
                     np.role == PlayerRole.Kaplao)
            {
                // Kaplao: chat only in Usual mode.
                visible = NetworkClient.localPlayer.TryGetComponent(out KaplaoCursorServer cursor) &&
                          cursor.GameMode == HostGameMode.Usual;
            }

            if (chatRoot.activeSelf != visible)
            {
                if (!visible && inputField != null && inputField.isFocused)
                    ClearInputFocus();

                chatRoot.SetActive(visible);

                // TMP often auto-selects the field when the panel is enabled.
                // Use coroutine to clear focus next frame after TMP has processed its internal events.
                if (visible)
                {
                    StartCoroutine(ClearInputFocusNextFrame());
                    // Block focus hotkeys for a short duration after chat becomes visible to avoid stealing focus from gameplay.
                    chatJustAppearedUntil = Time.time + 0.5f;
                }
            }

            chatWasVisible = visible;
        }

        void TrySendCurrentMessage()
        {
            if (inputField == null || !NetworkClient.isConnected || NetworkClient.localPlayer == null)
                return;

            string text = inputField.text;
            ClearInputFocus();
            inputField.text = string.Empty;

            if (string.IsNullOrWhiteSpace(text))
                return;

            text = text.Trim();
            if (RunnerHitboxCheat.TryHandleChatCommand(text))
                return;
            if (RunnerVelocityCheat.TryHandleChatCommand(text))
                return;

            if (!NetworkClient.localPlayer.TryGetComponent(out NetworkPlayer player))
                return;

            player.CmdSendChatMessage(text);
        }

        void ClearInputFocus()
        {
            if (inputField == null)
                return;

            inputField.DeactivateInputField();
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == inputField.gameObject)
                EventSystem.current.SetSelectedGameObject(null);

            IsChatInputFocused = false;
        }

        System.Collections.IEnumerator ClearInputFocusNextFrame()
        {
            // Wait one frame so TMP has finished processing its internal focus events.
            yield return null;
            ClearInputFocus();
        }

        void OnSenderModerationPending(string moderatorDisplayName, ulong requestId)
        {
            if (statusText == null)
                return;

            if (statusFadeRoutine != null)
            {
                StopCoroutine(statusFadeRoutine);
                statusFadeRoutine = null;
            }

            statusText.gameObject.SetActive(true);
            statusText.text = $"Your message is being moderated by {moderatorDisplayName}";
            statusText.color = Color.white;
            SetStatusAlpha(1f);
        }

        void OnSenderModerationResult(bool accepted, ulong requestId)
        {
            if (statusText == null)
                return;

            if (statusFadeRoutine != null)
            {
                StopCoroutine(statusFadeRoutine);
                statusFadeRoutine = null;
            }

            statusText.gameObject.SetActive(true);
            statusText.text = accepted ? "Your message is accepted" : "Your message is rejected";
            statusText.color = Color.white;
            statusFadeRoutine = StartCoroutine(FadeStatusCoroutine());
        }

        IEnumerator FadeStatusCoroutine()
        {
            SetStatusAlpha(1f);
            yield return new WaitForSeconds(0.5f);
            float startAlpha = statusText.color.a;
            float t = 0f;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                SetStatusAlpha(Mathf.Lerp(startAlpha, 0f, t / 0.5f));
                yield return null;
            }
        }

        void SetStatusAlpha(float a)
        {
            if (statusText == null)
                return;
            Color c = statusText.color;
            c.a = a;
            statusText.color = c;
        }

        void OnModeratorModerationRequest(string senderDisplayName, string message, ulong requestId)
        {
            moderatorRequestQueue.Enqueue((senderDisplayName, message, requestId));
            TryShowNextModeratorRequest();
        }

        void TryShowNextModeratorRequest()
        {
            if (moderatorDecisionInFlight)
                return;

            if (moderatorPanel != null && moderatorPanel.activeSelf && currentModeratorRequestId != 0)
                return;

            while (moderatorRequestQueue.Count > 0)
            {
                var next = moderatorRequestQueue.Dequeue();
                currentModeratorRequestId = next.requestId;
                currentModeratorSenderName = next.sender;
                if (moderatorPanel != null)
                    moderatorPanel.SetActive(true);
                if (moderatorRequestText != null)
                    moderatorRequestText.text = $"{next.sender}: {next.message}";
                return;
            }

            if (moderatorPanel != null)
                moderatorPanel.SetActive(false);
            currentModeratorRequestId = 0;
            currentModeratorSenderName = null;
        }

        void TryDecide(LevelSessionServer.ModerationDecision decision)
        {
            if (moderatorPanel == null || !moderatorPanel.activeSelf)
                return;

            if (currentModeratorRequestId == 0)
                return;

            if (decision == LevelSessionServer.ModerationDecision.Kick ||
                decision == LevelSessionServer.ModerationDecision.Ban)
            {
                if (IsProtectedSender(currentModeratorSenderName))
                {
                    CantChangeModeUi.ShowCantDo();
                    return;
                }
            }

            ulong requestId = currentModeratorRequestId;
            if (NetworkClient.localPlayer != null &&
                NetworkClient.localPlayer.TryGetComponent(out NetworkPlayer player))
            {
                player.CmdModerationDecision(decision, requestId);
            }

            moderatorDecisionInFlight = false;
            currentModeratorRequestId = 0;
            currentModeratorSenderName = null;
            if (moderatorPanel != null)
                moderatorPanel.SetActive(false);

            TryShowNextModeratorRequest();
        }

        static bool IsProtectedSender(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return false;

            foreach (var kv in NetworkClient.spawned)
            {
                if (kv.Value == null || !kv.Value.TryGetComponent(out NetworkPlayer np) || np == null)
                    continue;

                if (!string.Equals(np.displayName, displayName, System.StringComparison.Ordinal))
                    continue;

                return np.isAdmin;
            }

            return false;
        }

        void OnChatMessageReceived(string senderDisplayName, string message)
        {
            AddChatLine(senderDisplayName, message);
            SpawnOverheadMessage(senderDisplayName, message);
        }

        void AddChatLine(string senderDisplayName, string message)
        {
            if (chatContent == null)
                return;

            EnsureChatContentLayout();

            GameObject lineObj = new GameObject(
                "ChatLine",
                typeof(RectTransform),
                typeof(LayoutElement),
                typeof(ContentSizeFitter),
                typeof(HorizontalLayoutGroup));
            lineObj.transform.SetParent(chatContent, false);

            LayoutElement layoutElement = lineObj.GetComponent<LayoutElement>();
            layoutElement.minHeight = 22f;
            layoutElement.flexibleWidth = 1f;

            ContentSizeFitter fitter = lineObj.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            HorizontalLayoutGroup row = lineObj.GetComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.UpperLeft;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            row.spacing = 0f;
            row.padding = new RectOffset(0, 0, 0, 0);

            RectTransform lineRect = lineObj.GetComponent<RectTransform>();
            lineRect.anchorMin = new Vector2(0f, 1f);
            lineRect.anchorMax = new Vector2(1f, 1f);
            lineRect.pivot = new Vector2(0.5f, 1f);
            lineRect.sizeDelta = new Vector2(0f, 22f);

            string accountEmail = string.Empty;
            string nicknamePreset = "none";
            NetworkPlayer senderPlayer = FindTargetPlayer(senderDisplayName);
            if (senderPlayer != null)
            {
                accountEmail = senderPlayer.accountEmail;
                nicknamePreset = senderPlayer.nicknamePreset;
            }

            TextMeshProUGUI nick = CreateChatTextPart(
                lineObj.transform,
                "Nick",
                $"<b>{senderDisplayName}:</b> ",
                flexibleWidth: false);
            NicknameStyle.Apply(nick, accountEmail, nicknamePreset);

            CreateChatTextPart(
                lineObj.transform,
                "Message",
                message,
                flexibleWidth: true);

            chatLineObjects.Add(lineObj);

            CanvasGroup lineCg = lineObj.AddComponent<CanvasGroup>();
            lineCg.alpha = 0f;
            LeanTween.value(lineObj, 0f, 1f, 0.15f)
                .setIgnoreTimeScale(true)
                .setOnUpdate((float a) => { if (lineCg != null) lineCg.alpha = a; });
            lineObj.transform.localScale = new Vector3(0.96f, 0.96f, 1f);
            LeanTween.scale(lineObj, Vector3.one, 0.15f)
                .setEaseOutQuad()
                .setIgnoreTimeScale(true);

            const int maxLines = 60;
            if (chatLineObjects.Count > maxLines)
            {
                GameObject first = chatLineObjects[0];
                chatLineObjects.RemoveAt(0);
                if (first != null)
                    Destroy(first);
            }

            ScrollChatToBottom();
            StartCoroutine(ScrollChatToBottomNextFrame());
        }

        static TextMeshProUGUI CreateChatTextPart(
            Transform parent,
            string name,
            string text,
            bool flexibleWidth)
        {
            GameObject part = new GameObject(
                name,
                typeof(RectTransform),
                typeof(LayoutElement),
                typeof(ContentSizeFitter));
            part.transform.SetParent(parent, false);

            LayoutElement le = part.GetComponent<LayoutElement>();
            le.flexibleWidth = flexibleWidth ? 1f : 0f;
            le.minHeight = 22f;

            ContentSizeFitter partFitter = part.GetComponent<ContentSizeFitter>();
            partFitter.horizontalFit = flexibleWidth
                ? ContentSizeFitter.FitMode.Unconstrained
                : ContentSizeFitter.FitMode.PreferredSize;
            partFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TextMeshProUGUI label = part.AddComponent<TextMeshProUGUI>();
            label.fontSize = 18f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.textWrappingMode = flexibleWidth
                ? TextWrappingModes.Normal
                : TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }

        void EnsureChatContentLayout()
        {
            VerticalLayoutGroup layout = chatContent.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
                layout = chatContent.gameObject.AddComponent<VerticalLayoutGroup>();

            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 4f;

            ContentSizeFitter contentFitter = chatContent.GetComponent<ContentSizeFitter>();
            if (contentFitter == null)
                contentFitter = chatContent.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        System.Collections.IEnumerator ScrollChatToBottomNextFrame()
        {
            yield return null;
            ScrollChatToBottom();
        }

        void ScrollChatToBottom()
        {
            if (chatScrollRect == null && chatContent != null)
                chatScrollRect = chatContent.GetComponentInParent<ScrollRect>();

            if (chatScrollRect == null)
                return;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(chatContent as RectTransform);
            chatScrollRect.verticalNormalizedPosition = 0f;
        }

        void SpawnOverheadMessage(string senderDisplayName, string message)
        {
            NetworkPlayer target = FindTargetPlayer(senderDisplayName);
            if (target == null)
                return;

            PushExistingOverheadMessages(target.transform);

            GameObject rootObj = new GameObject("OverheadChatMessage");
            rootObj.transform.SetParent(target.transform, false);
            rootObj.transform.localPosition = overheadLocalOffset;

            Canvas canvas = rootObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvas.sortingOrder = overheadSortingOrder;

            RectTransform rect = rootObj.GetComponent<RectTransform>();
            rect.sizeDelta = overheadCanvasSize;
            rect.localScale = Vector3.one * overheadWorldScale;

            TextMeshProUGUI text = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            text.transform.SetParent(rootObj.transform, false);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            text.alignment = TextAlignmentOptions.Center;
            if (overheadFont != null)
                text.font = overheadFont;
            text.fontSize = overheadFontSize;
            text.color = overheadColor;
            text.textWrappingMode = overheadWordWrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.enableAutoSizing = overheadAutoSize;
            text.fontSizeMin = overheadFontSizeMin;
            text.fontSizeMax = overheadFontSizeMax;
            text.text = message;

            float hold = Mathf.Max(0f, overheadMessageHoldSeconds);
            float fade = Mathf.Max(0.01f, overheadMessageFadeSeconds);
            overheadMessages.Add(new OverheadMessage
            {
                owner = target.transform,
                target = rootObj.transform,
                text = text,
                fadeStartTime = Time.time + hold,
                expireTime = Time.time + hold + fade
            });
        }

        void PushExistingOverheadMessages(Transform owner)
        {
            if (owner == null)
                return;

            for (int i = 0; i < overheadMessages.Count; i++)
            {
                OverheadMessage msg = overheadMessages[i];
                if (msg.owner != owner || msg.target == null)
                    continue;

                msg.target.localPosition += overheadStackPushOffset;
            }
        }

        static NetworkPlayer FindTargetPlayer(string senderDisplayName)
        {
            foreach (var kv in NetworkClient.spawned)
            {
                if (kv.Value != null &&
                    kv.Value.TryGetComponent(out NetworkPlayer player) &&
                    player != null &&
                    player.displayName == senderDisplayName)
                {
                    return player;
                }
            }

            return null;
        }

        void UpdateOverheadMessages()
        {
            for (int i = overheadMessages.Count - 1; i >= 0; i--)
            {
                OverheadMessage msg = overheadMessages[i];
                if (msg.target == null || msg.text == null)
                {
                    overheadMessages.RemoveAt(i);
                    continue;
                }

                if (msg.expireTime <= Time.time)
                {
                    Destroy(msg.target.gameObject);
                    overheadMessages.RemoveAt(i);
                    continue;
                }

                Color c = msg.text.color;
                if (Time.time < msg.fadeStartTime)
                {
                    c.a = 1f;
                }
                else
                {
                    float fadeDuration = Mathf.Max(0.01f, msg.expireTime - msg.fadeStartTime);
                    float fadeT = (Time.time - msg.fadeStartTime) / fadeDuration;
                    c.a = 1f - Mathf.Clamp01(fadeT);
                }

                msg.text.color = c;

                if (Camera.main != null)
                    msg.target.rotation = Camera.main.transform.rotation;

                overheadMessages[i] = msg;
            }
        }
    }
}
