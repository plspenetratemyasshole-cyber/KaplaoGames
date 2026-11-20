using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;

namespace KaplaoGames.UI.Editor
{
    /// <summary>
    /// Unity Editor menu item to create Developer Chat UI
    /// </summary>
    public static class DeveloperChatSetup
    {
        [MenuItem("Kaplao/Setup/Create Developer Chat UI")]
        public static void CreateDeveloperChatUI()
        {
            // Check if already exists
            DeveloperChatManager existing = Object.FindObjectOfType<DeveloperChatManager>();
            if (existing != null)
            {
                EditorUtility.DisplayDialog(
                    "Developer Chat Already Exists",
                    "Developer Chat UI already exists in the scene. Please delete it first if you want to recreate it.",
                    "OK"
                );
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            // Create root Canvas
            GameObject canvasObj = new GameObject("DeveloperChatCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000; // Very high priority to render above pause menu and all other UI
            
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            
            canvasObj.AddComponent<GraphicRaycaster>();

            // Create chat root (holds everything)
            GameObject chatRoot = new GameObject("ChatRoot");
            chatRoot.transform.SetParent(canvasObj.transform, false);
            RectTransform chatRootRect = chatRoot.AddComponent<RectTransform>();
            chatRootRect.anchorMin = Vector2.zero;
            chatRootRect.anchorMax = Vector2.one;
            chatRootRect.sizeDelta = Vector2.zero;

            // Create semi-transparent background panel
            GameObject backgroundPanel = new GameObject("BackgroundPanel");
            backgroundPanel.transform.SetParent(chatRoot.transform, false);
            RectTransform bgRect = backgroundPanel.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            
            Image bgImage = backgroundPanel.AddComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.5f); // Semi-transparent black
            
            Button bgButton = backgroundPanel.AddComponent<Button>();
            bgButton.transition = Selectable.Transition.None;

            // Create main chat panel (centered, ~50% screen width)
            GameObject chatPanel = new GameObject("ChatPanel");
            chatPanel.transform.SetParent(chatRoot.transform, false);
            RectTransform panelRect = chatPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(800, 600);
            panelRect.anchoredPosition = Vector2.zero;
            
            Image panelImage = chatPanel.AddComponent<Image>();
            panelImage.color = new Color(0.2f, 0.2f, 0.2f, 0.95f); // Dark gray, slightly transparent

            // Create header with title and close button
            GameObject header = new GameObject("Header");
            header.transform.SetParent(chatPanel.transform, false);
            RectTransform headerRect = header.AddComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.sizeDelta = new Vector2(0, 50);
            headerRect.anchoredPosition = new Vector2(0, -25);
            
            Image headerImage = header.AddComponent<Image>();
            headerImage.color = new Color(0.15f, 0.15f, 0.15f); // Darker gray for header

            // Title text
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(header.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.sizeDelta = new Vector2(-100, 0);
            titleRect.anchoredPosition = new Vector2(0, 0);
            
            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "Contact Developer";
            titleText.fontSize = 24;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Color.white; // White text on dark header

            // Close button
            GameObject closeButton = new GameObject("CloseButton");
            closeButton.transform.SetParent(header.transform, false);
            RectTransform closeRect = closeButton.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1, 0.5f);
            closeRect.anchorMax = new Vector2(1, 0.5f);
            closeRect.sizeDelta = new Vector2(40, 40);
            closeRect.anchoredPosition = new Vector2(-30, 0);
            
            Image closeImage = closeButton.AddComponent<Image>();
            closeImage.color = new Color(0.5f, 0.5f, 0.5f); // Gray close button
            
            Button closeBtn = closeButton.AddComponent<Button>();
            
            GameObject closeText = new GameObject("Text");
            closeText.transform.SetParent(closeButton.transform, false);
            RectTransform closeTextRect = closeText.AddComponent<RectTransform>();
            closeTextRect.anchorMin = Vector2.zero;
            closeTextRect.anchorMax = Vector2.one;
            closeTextRect.sizeDelta = Vector2.zero;
            
            TextMeshProUGUI closeTMP = closeText.AddComponent<TextMeshProUGUI>();
            closeTMP.text = "X";
            closeTMP.fontSize = 20;
            closeTMP.alignment = TextAlignmentOptions.Center;
            closeTMP.color = Color.white;

            // Create scroll view for messages
            GameObject scrollView = new GameObject("MessageScrollView");
            scrollView.transform.SetParent(chatPanel.transform, false);
            RectTransform scrollRect = scrollView.AddComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0, 0);
            scrollRect.anchorMax = new Vector2(1, 1);
            scrollRect.offsetMin = new Vector2(10, 70); // Leave space for input
            scrollRect.offsetMax = new Vector2(-10, -60); // Leave space for header
            
            Image scrollImage = scrollView.AddComponent<Image>();
            scrollImage.color = new Color(0.25f, 0.25f, 0.25f); // Dark gray background for messages
            
            ScrollRect scrollComponent = scrollView.AddComponent<ScrollRect>();
            scrollComponent.horizontal = false;
            scrollComponent.vertical = true;
            scrollComponent.movementType = ScrollRect.MovementType.Clamped;

            // Create viewport
            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollView.transform, false);
            RectTransform viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;
            
            viewport.AddComponent<RectMask2D>(); // RectMask2D is more efficient and doesn't require Image

            // Create content container
            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 0);
            
            VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.childControlHeight = false;
            contentLayout.childControlWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childForceExpandWidth = true;
            contentLayout.spacing = 10;
            contentLayout.padding = new RectOffset(10, 10, 10, 10);
            
            ContentSizeFitter contentFitter = content.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollComponent.content = contentRect;
            scrollComponent.viewport = viewportRect;

            // Create message template (will be instantiated for each message)
            GameObject messageTemplate = new GameObject("MessageTemplate");
            messageTemplate.transform.SetParent(content.transform, false);
            RectTransform msgRect = messageTemplate.AddComponent<RectTransform>();
            msgRect.sizeDelta = new Vector2(0, 80);
            
            Image msgImage = messageTemplate.AddComponent<Image>();
            msgImage.color = new Color(0.3f, 0.3f, 0.3f); // Gray message bubbles
            
            LayoutElement msgLayout = messageTemplate.AddComponent<LayoutElement>();
            msgLayout.minHeight = 80;
            msgLayout.preferredHeight = -1;
            msgLayout.flexibleHeight = 1;
            msgLayout.ignoreLayout = true; // Hide from layout but keep active for serialization
            
            // Make template invisible (but active for serialization)
            CanvasGroup msgCanvasGroup = messageTemplate.AddComponent<CanvasGroup>();
            msgCanvasGroup.alpha = 0f;
            msgCanvasGroup.interactable = false;
            msgCanvasGroup.blocksRaycasts = false;

            GameObject msgText = new GameObject("Text");
            msgText.transform.SetParent(messageTemplate.transform, false);
            RectTransform msgTextRect = msgText.AddComponent<RectTransform>();
            msgTextRect.anchorMin = Vector2.zero;
            msgTextRect.anchorMax = Vector2.one;
            msgTextRect.offsetMin = new Vector2(10, 10);
            msgTextRect.offsetMax = new Vector2(-10, -10);
            
            TextMeshProUGUI msgTMP = msgText.AddComponent<TextMeshProUGUI>();
            msgTMP.fontSize = 16;
            msgTMP.color = Color.white; // White text on gray background
            msgTMP.alignment = TextAlignmentOptions.TopLeft;
            msgTMP.enableWordWrapping = true;
            msgTMP.text = "Message text here";

            // Don't call SetActive(false) - Unity doesn't serialize references to inactive objects!
            // Instead we use ignoreLayout=true above to hide it from the layout system

            // Create input field
            GameObject inputField = new GameObject("InputField");
            inputField.transform.SetParent(chatPanel.transform, false);
            RectTransform inputRect = inputField.AddComponent<RectTransform>();
            inputRect.anchorMin = new Vector2(0, 0);
            inputRect.anchorMax = new Vector2(1, 0);
            inputRect.sizeDelta = new Vector2(-120, 50);
            inputRect.anchoredPosition = new Vector2(0, 35);
            
            Image inputImage = inputField.AddComponent<Image>();
            inputImage.color = new Color(0.3f, 0.3f, 0.3f); // Dark gray input
            
            TMP_InputField inputComponent = inputField.AddComponent<TMP_InputField>();
            inputComponent.lineType = TMP_InputField.LineType.MultiLineNewline;
            inputComponent.characterLimit = 5000;

            GameObject textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(inputField.transform, false);
            RectTransform textAreaRect = textArea.GetComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(12, 6);
            textAreaRect.offsetMax = new Vector2(-12, -6);
            inputComponent.textViewport = textAreaRect;

            GameObject inputText = new GameObject("Text");
            inputText.transform.SetParent(textArea.transform, false);
            RectTransform inputTextRect = inputText.AddComponent<RectTransform>();
            inputTextRect.anchorMin = Vector2.zero;
            inputTextRect.anchorMax = Vector2.one;
            inputTextRect.offsetMin = Vector2.zero;
            inputTextRect.offsetMax = Vector2.zero;
            
            TextMeshProUGUI inputTMP = inputText.AddComponent<TextMeshProUGUI>();
            inputTMP.fontSize = 16;
            inputTMP.color = Color.white; // White text
            inputTMP.alignment = TextAlignmentOptions.TopLeft;
            inputTMP.textWrappingMode = TextWrappingModes.Normal;
            
            inputComponent.textComponent = inputTMP;

            GameObject placeholder = new GameObject("Placeholder");
            placeholder.transform.SetParent(textArea.transform, false);
            RectTransform placeholderRect = placeholder.AddComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = Vector2.zero;
            placeholderRect.offsetMax = Vector2.zero;
            
            TextMeshProUGUI placeholderTMP = placeholder.AddComponent<TextMeshProUGUI>();
            placeholderTMP.text = "Type your message... (Shift+Enter for new line)";
            placeholderTMP.fontSize = 16;
            placeholderTMP.color = new Color(0.6f, 0.6f, 0.6f); // Gray placeholder
            placeholderTMP.alignment = TextAlignmentOptions.TopLeft;
            
            inputComponent.placeholder = placeholderTMP;

            // Create send button
            GameObject sendButton = new GameObject("SendButton");
            sendButton.transform.SetParent(chatPanel.transform, false);
            RectTransform sendRect = sendButton.AddComponent<RectTransform>();
            sendRect.anchorMin = new Vector2(1, 0);
            sendRect.anchorMax = new Vector2(1, 0);
            sendRect.sizeDelta = new Vector2(100, 50);
            sendRect.anchoredPosition = new Vector2(-60, 35);
            
            Image sendImage = sendButton.AddComponent<Image>();
            sendImage.color = new Color(0.4f, 0.4f, 0.4f); // Gray send button
            
            Button sendBtn = sendButton.AddComponent<Button>();

            GameObject sendText = new GameObject("Text");
            sendText.transform.SetParent(sendButton.transform, false);
            RectTransform sendTextRect = sendText.AddComponent<RectTransform>();
            sendTextRect.anchorMin = Vector2.zero;
            sendTextRect.anchorMax = Vector2.one;
            sendTextRect.sizeDelta = Vector2.zero;
            
            TextMeshProUGUI sendTMP = sendText.AddComponent<TextMeshProUGUI>();
            sendTMP.text = "Send";
            sendTMP.fontSize = 18;
            sendTMP.alignment = TextAlignmentOptions.Center;
            sendTMP.color = Color.white;

            // Add DeveloperChatManager component
            DeveloperChatManager manager = canvasObj.AddComponent<DeveloperChatManager>();
            
            // Wire up references using SerializedObject (proper way for Editor scripts)
            SerializedObject serializedManager = new SerializedObject(manager);
            
            serializedManager.FindProperty("chatRoot").objectReferenceValue = chatRoot;
            serializedManager.FindProperty("chatPanel").objectReferenceValue = chatPanel;
            serializedManager.FindProperty("backgroundPanel").objectReferenceValue = backgroundPanel;
            serializedManager.FindProperty("closeButton").objectReferenceValue = closeBtn;
            serializedManager.FindProperty("sendButton").objectReferenceValue = sendBtn;
            serializedManager.FindProperty("messageInput").objectReferenceValue = inputComponent;
            serializedManager.FindProperty("messagesContainer").objectReferenceValue = content.transform;
            serializedManager.FindProperty("messageTemplatePrefab").objectReferenceValue = messageTemplate;
            serializedManager.FindProperty("scrollRect").objectReferenceValue = scrollComponent;
            
            serializedManager.ApplyModifiedProperties();

            // Mark as dirty and select
            EditorUtility.SetDirty(canvasObj);
            Selection.activeGameObject = canvasObj;

            // Hide by default
            chatRoot.SetActive(false);

            EditorUtility.DisplayDialog(
                "Developer Chat UI Created",
                "Developer Chat UI has been created successfully!\n\n" +
                "IMPORTANT: In the inspector, assign the 'Open Button Pause' and 'Open Button Runner' fields to your pause menu buttons.\n\n" +
                "The chat will only work when the player is logged in.",
                "OK"
            );

            Debug.Log("[DeveloperChatSetup] Created Developer Chat UI. Please assign open buttons in inspector.");
        }
    }
}
#endif
