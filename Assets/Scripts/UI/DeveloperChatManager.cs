using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using KaplaoGames.UI.Animation;

namespace KaplaoGames.UI
{
    /// <summary>
    /// Developer Chat UI - allows players to contact developers
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/UI/Developer Chat Manager")]
    public class DeveloperChatManager : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] GameObject chatRoot;
        [SerializeField] GameObject chatPanel;
        [SerializeField] GameObject backgroundPanel;
        [SerializeField] Button closeButton;
        [SerializeField] Button sendButton;
        [SerializeField] TMP_InputField messageInput;
        [SerializeField] Transform messagesContainer;
        [SerializeField] GameObject messageTemplatePrefab;
        [SerializeField] ScrollRect scrollRect;

        [Header("Open Buttons (optional)")]
        [Tooltip("Button to open chat from PauseMenuRoot (main pause menu)")]
        [SerializeField] Button openButtonPause;
        [Tooltip("Button to open chat from RunnerPauseMenuRoot (runner pause menu)")]
        [SerializeField] Button openButtonRunner;

        [Header("Settings")]
        [SerializeField] string apiBaseUrl = "https://kaplao.ru";
        [SerializeField] float pollInterval = 10f; // Poll every 10 seconds
        [SerializeField] int maxMessageLength = 5000;

        static DeveloperChatManager activeInstance;
        public static bool IsAnyChatOpen => activeInstance != null && activeInstance.isOpen;

        bool isOpen;
        bool isPolling;
        float nextPollTime;
        List<DevMessage> messages = new List<DevMessage>();
        Coroutine sendCoroutine;
        string lastRenderedFingerprint;

        [Serializable]
        class DevMessage
        {
            public string id;
            public string senderEmail;
            public string senderNickname;
            public string message;
            public bool isFromAdmin;
            public string adminEmail;
            public string adminNickname;
            public string createdAt;
            public bool readByAdmin;
            public bool readByPlayer;
        }

        [Serializable]
        class DevMessagesResponse
        {
            public bool ok;
            public DevMessage[] messages;
            public string error;
        }

        [Serializable]
        class SendMessageRequest
        {
            public string message;
        }

        [Serializable]
        class SendMessageResponse
        {
            public bool ok;
            public DevMessage message;
            public string error;
        }

        void Awake()
        {
            activeInstance = this;

            if (chatRoot != null)
                chatRoot.SetActive(false);

            if (closeButton != null)
                closeButton.onClick.AddListener(Close);

            if (sendButton != null)
                sendButton.onClick.AddListener(SendMessage);

            if (backgroundPanel != null)
            {
                Button bgButton = backgroundPanel.GetComponent<Button>();
                if (bgButton == null)
                    bgButton = backgroundPanel.AddComponent<Button>();
                bgButton.onClick.AddListener(Close);
            }

            if (messageInput != null)
            {
                messageInput.characterLimit = maxMessageLength;
                // Don't use onSubmit - it interferes with multiline
                // Instead check for Enter key in SendMessage
            }

            if (openButtonPause != null)
                openButtonPause.onClick.AddListener(Open);

            if (openButtonRunner != null)
                openButtonRunner.onClick.AddListener(Open);
        }

        void OnDestroy()
        {
            if (activeInstance == this)
                activeInstance = null;

            if (closeButton != null)
                closeButton.onClick.RemoveListener(Close);

            if (sendButton != null)
                sendButton.onClick.RemoveListener(SendMessage);

            if (openButtonPause != null)
                openButtonPause.onClick.RemoveListener(Open);

            if (openButtonRunner != null)
                openButtonRunner.onClick.RemoveListener(Open);
        }

        void Update()
        {
            if (!isOpen)
                return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }

            if (!isPolling)
                return;

            // Poll for new messages
            if (Time.time >= nextPollTime)
            {
                nextPollTime = Time.time + pollInterval;
                StartCoroutine(PollMessagesCoroutine());
            }

            // Handle Enter key for sending messages
            if (messageInput != null && messageInput.isFocused)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    // Only send if not holding Shift (Shift+Enter = new line)
                    if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
                    {
                        SendMessage();
                    }
                }
            }
        }

        public void Open()
        {
            if (!Shared.PlayerSession.IsLoggedIn)
            {
                Debug.LogWarning("[DeveloperChat] Player must be logged in to contact developer.");
                KaplaoUiToast.ShowWarning("Sign in to your account in Settings to contact the developer.");
                return;
            }

            isOpen = true;
            isPolling = true;
            nextPollTime = Time.time; // Poll immediately on open

            if (chatRoot != null)
                chatRoot.SetActive(true);

            if (backgroundPanel != null)
                backgroundPanel.SetActive(true);

            StartCoroutine(LoadMessagesCoroutine());
        }

        public void Close()
        {
            isOpen = false;
            isPolling = false;

            if (chatRoot != null)
                chatRoot.SetActive(false);

            if (backgroundPanel != null)
                backgroundPanel.SetActive(false);
        }

        void SendMessage()
        {
            if (!Shared.PlayerSession.IsLoggedIn)
            {
                Debug.LogWarning("[DeveloperChat] Must be logged in to send messages.");
                return;
            }

            if (messageInput == null || string.IsNullOrWhiteSpace(messageInput.text))
                return;

            string messageText = messageInput.text.Trim();
            if (messageText.Length > maxMessageLength)
            {
                Debug.LogWarning($"[DeveloperChat] Message too long ({messageText.Length}/{maxMessageLength})");
                return;
            }

            if (sendCoroutine != null)
                StopCoroutine(sendCoroutine);

            sendCoroutine = StartCoroutine(SendMessageCoroutine(messageText));
        }

        IEnumerator SendMessageCoroutine(string messageText)
        {
            if (sendButton != null)
                sendButton.interactable = false;

            string url = $"{apiBaseUrl}/api/dev-chat/send";
            SendMessageRequest request = new SendMessageRequest { message = messageText };
            string json = JsonUtility.ToJson(request);

            using (UnityWebRequest www = UnityWebRequest.PostWwwForm(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.SetRequestHeader("Authorization", $"Bearer {Shared.PlayerSession.AuthToken}");

                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    SendMessageResponse response = JsonUtility.FromJson<SendMessageResponse>(www.downloadHandler.text);
                    if (response != null && response.ok)
                    {
                        Debug.Log("[DeveloperChat] Message sent successfully.");
                        messageInput.text = string.Empty;
                        messageInput.ActivateInputField();
                        
                        // Reload messages to show the new one
                        yield return LoadMessagesCoroutine();
                    }
                    else
                    {
                        string error = response?.error ?? "Unknown error";
                        Debug.LogError($"[DeveloperChat] Failed to send message: {error}");
                    }
                }
                else
                {
                    Debug.LogError($"[DeveloperChat] Network error: {www.error}");
                }
            }

            if (sendButton != null)
                sendButton.interactable = true;

            sendCoroutine = null;
        }

        IEnumerator LoadMessagesCoroutine()
        {
            if (!Shared.PlayerSession.IsLoggedIn)
                yield break;

            string url = $"{apiBaseUrl}/api/dev-chat/messages";

            using (UnityWebRequest www = UnityWebRequest.Get(url))
            {
                www.SetRequestHeader("Authorization", $"Bearer {Shared.PlayerSession.AuthToken}");
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    DevMessagesResponse response = JsonUtility.FromJson<DevMessagesResponse>(www.downloadHandler.text);
                    if (response != null && response.ok && response.messages != null)
                    {
                        bool hasUnreadAdminMessages = false;
                        foreach (var msg in response.messages)
                        {
                            if (msg.isFromAdmin && !msg.readByPlayer)
                            {
                                hasUnreadAdminMessages = true;
                                break;
                            }
                        }

                        messages.Clear();
                        messages.AddRange(response.messages);
                        RenderMessages();

                        // Mark admin messages as read by player
                        if (hasUnreadAdminMessages)
                        {
                            StartCoroutine(MarkMessagesReadCoroutine());
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[DeveloperChat] Failed to load messages: {response?.error}");
                    }
                }
                else
                {
                    Debug.LogError($"[DeveloperChat] Network error: {www.error}");
                }
            }
        }

        IEnumerator MarkMessagesReadCoroutine()
        {
            string url = $"{apiBaseUrl}/api/dev-chat/mark-read";
            string json = "{\"readByAdmin\":false}";

            using (UnityWebRequest www = UnityWebRequest.Put(url, json))
            {
                www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.SetRequestHeader("Authorization", $"Bearer {Shared.PlayerSession.AuthToken}");
                www.method = "PUT";

                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[DeveloperChat] Failed to mark messages as read: {www.error}");
                }
            }
        }

        IEnumerator PollMessagesCoroutine()
        {
            yield return LoadMessagesCoroutine();
        }

        void RenderMessages()
        {
            if (messagesContainer == null)
            {
                Debug.LogError("[DeveloperChat] messagesContainer is null! Assign it in Inspector.");
                return;
            }

            if (messageTemplatePrefab == null)
            {
                Debug.LogError("[DeveloperChat] messageTemplatePrefab is null! Assign it in Inspector.");
                return;
            }

            string fingerprint = messages.Count + (messages.Count > 0 ? (messages[messages.Count - 1].id + messages[messages.Count - 1].message) : "");
            if (fingerprint == lastRenderedFingerprint && messagesContainer.childCount > 1)
                return;
            lastRenderedFingerprint = fingerprint;

            Debug.Log($"[DeveloperChat] RenderMessages called with {messages.Count} messages");

            // Clear existing messages (but don't destroy the template!)
            foreach (Transform child in messagesContainer)
            {
                // Skip the template itself
                if (child.gameObject == messageTemplatePrefab)
                    continue;
                    
                Destroy(child.gameObject);
            }

            // Render each message
            foreach (DevMessage msg in messages)
            {
                GameObject msgObj = Instantiate(messageTemplatePrefab, messagesContainer);
                msgObj.SetActive(true);

                // Remove CanvasGroup from copy (template has alpha=0 for invisibility)
                CanvasGroup canvasGroup = msgObj.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    Destroy(canvasGroup);
                }

                // Enable layout for the copy (template has ignoreLayout=true)
                LayoutElement layout = msgObj.GetComponent<LayoutElement>();
                if (layout != null)
                {
                    layout.ignoreLayout = false;
                }

                Debug.Log($"[DeveloperChat] Created message object: {msgObj.name}");

                // Find text component
                TMP_Text textComponent = msgObj.GetComponentInChildren<TMP_Text>();
                if (textComponent != null)
                {
                    if (msg.isFromAdmin)
                    {
                        string adminName = !string.IsNullOrEmpty(msg.adminNickname) ? msg.adminNickname : "Developer";
                        textComponent.text = $"<color=#4A90E2><b>{adminName} (Dev):</b></color>\n{msg.message}";
                    }
                    else
                    {
                        // Show player's own message
                        string playerName = !string.IsNullOrEmpty(msg.senderNickname) ? msg.senderNickname : "You";
                        textComponent.text = $"<color=#666><b>{playerName}:</b></color>\n{msg.message}";
                    }
                    Debug.Log($"[DeveloperChat] Set text: {textComponent.text}");
                }
                else
                {
                    Debug.LogError($"[DeveloperChat] No TMP_Text component found in message object!");
                }
            }

            // Scroll to bottom
            if (scrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }
    }
}
