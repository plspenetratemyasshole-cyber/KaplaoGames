#if UNITY_EDITOR
using KaplaoGames.Client;
using KaplaoGames.Server;
using KaplaoGames.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KaplaoGames.Editor
{
    public static class PauseMenuSceneSetup
    {
        const string GameScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Kaplao/Setup Pause Menu UI")]
        public static void SetupPauseMenuUi()
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[Kaplao] Canvas not found in SampleScene.");
                return;
            }

            Transform root = EnsurePauseMenuRoot(canvas.transform);
            EnsurePauseMenuHierarchy(root);
            EnsureRunnerPauseMenu(canvas.transform);
            EnsurePauseMenuComponent(canvas.gameObject);
            EnsureWorldLoadBootstrap();
            DisableLegacyUi(canvas.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Kaplao] Pause + Runner pause menu setup complete in SampleScene.");
        }

        [MenuItem("Kaplao/Setup Moderation Chat UI")]
        public static void SetupModerationChatUi()
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[Kaplao] Canvas not found in SampleScene.");
                return;
            }

            Transform pauseRoot = EnsurePauseMenuRoot(canvas.transform);
            EnsurePauseMenuHierarchy(pauseRoot);
            EnsurePauseMenuComponent(canvas.gameObject);

            // Keep on Canvas (not PauseMenuRoot) so runners also see the list in their pause menu.
            Transform hostPanel = PausePlayerListSetup.EnsurePlayerListPanel(canvas.transform);
            Transform chatRoot = EnsureChatRoot(canvas.transform);
            Transform moderatorPanel = EnsureModeratorPanel(canvas.transform);

            if (canvas.GetComponent<ModerationUiHooksClient>() == null)
                canvas.gameObject.AddComponent<ModerationUiHooksClient>();

            KaplaoModerationEscUi escUi = canvas.GetComponent<KaplaoModerationEscUi>();
            if (escUi == null)
                escUi = canvas.gameObject.AddComponent<KaplaoModerationEscUi>();

            SerializedObject escSo = new SerializedObject(escUi);
            escSo.FindProperty("panelRoot").objectReferenceValue = hostPanel.gameObject;
            escSo.FindProperty("listContent").objectReferenceValue = hostPanel.Find("PlayersScroll/Viewport/Content");
            escSo.FindProperty("playerRowTemplate").objectReferenceValue =
                hostPanel.Find("PlayersScroll/Viewport/Content/PlayerRowTemplate")?.gameObject;
            escSo.FindProperty("moderateChatButton").objectReferenceValue =
                hostPanel.Find("ModerateChatButton")?.GetComponent<Button>();
            escSo.FindProperty("moderateChatButtonText").objectReferenceValue =
                hostPanel.Find("ModerateChatButton/Text")?.GetComponent<TextMeshProUGUI>();
            escSo.ApplyModifiedPropertiesWithoutUndo();

            KaplaoModeratedChatUi chatUi = canvas.GetComponent<KaplaoModeratedChatUi>();
            if (chatUi == null)
                chatUi = canvas.gameObject.AddComponent<KaplaoModeratedChatUi>();

            SerializedObject chatSo = new SerializedObject(chatUi);
            chatSo.FindProperty("chatRoot").objectReferenceValue = chatRoot.gameObject;
            chatSo.FindProperty("chatContent").objectReferenceValue =
                chatRoot.Find("ChatPanel/ChatScroll/Viewport/Content");
            chatSo.FindProperty("chatScrollRect").objectReferenceValue =
                chatRoot.Find("ChatPanel/ChatScroll")?.GetComponent<ScrollRect>();
            chatSo.FindProperty("inputField").objectReferenceValue =
                chatRoot.Find("ChatPanel/InputContainer/ChatInput")?.GetComponent<TMP_InputField>();
            chatSo.FindProperty("statusText").objectReferenceValue =
                chatRoot.Find("ChatPanel/InputContainer/StatusText")?.GetComponent<TextMeshProUGUI>();
            chatSo.FindProperty("moderatorPanel").objectReferenceValue = moderatorPanel.gameObject;
            chatSo.FindProperty("moderatorRequestText").objectReferenceValue =
                moderatorPanel.Find("ModeratorRequestText")?.GetComponent<TextMeshProUGUI>();
            chatSo.FindProperty("acceptButton").objectReferenceValue =
                moderatorPanel.Find("AcceptButton")?.GetComponent<Button>();
            chatSo.FindProperty("rejectButton").objectReferenceValue =
                moderatorPanel.Find("RejectButton")?.GetComponent<Button>();
            chatSo.FindProperty("kickButton").objectReferenceValue =
                moderatorPanel.Find("KickButton")?.GetComponent<Button>();
            chatSo.FindProperty("banButton").objectReferenceValue =
                moderatorPanel.Find("BanButton")?.GetComponent<Button>();
            chatSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Kaplao] Moderation chat UI hierarchy created and bound in SampleScene. Save the scene.");
        }

        [MenuItem("Kaplao/Setup Banned Players UI")]
        public static void SetupBannedPlayersUi()
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[Kaplao] Canvas not found in SampleScene.");
                return;
            }

            EnsurePauseMenuRoot(canvas.transform);
            Transform bannedPanel = EnsureBannedPlayersPanel(canvas.transform);

            KaplaoBannedPlayersUi bannedUi = canvas.GetComponent<KaplaoBannedPlayersUi>();
            if (bannedUi == null)
                bannedUi = canvas.gameObject.AddComponent<KaplaoBannedPlayersUi>();

            SerializedObject so = new SerializedObject(bannedUi);
            so.FindProperty("panelRoot").objectReferenceValue = bannedPanel.gameObject;
            so.FindProperty("listContent").objectReferenceValue =
                bannedPanel.Find("BannedScroll/Viewport/Content");
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Kaplao] Banned players UI ready (left side on Canvas, host/protected pause). Save the scene.");
        }

        static Transform EnsureBannedPlayersPanel(Transform canvas)
        {
            Transform existing = canvas.Find("BannedPlayersPanel");
            if (existing == null)
            {
                Transform underPause = canvas.Find("PauseMenuRoot/BannedPlayersPanel");
                if (underPause != null)
                {
                    underPause.SetParent(canvas, true);
                    existing = underPause;
                }
            }

            if (existing != null)
            {
                existing.gameObject.SetActive(false);
                return existing;
            }

            GameObject panel = CreateImage("BannedPlayersPanel", canvas, new Color(0f, 0f, 0f, 0.55f));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0.5f);
            panelRect.anchorMax = new Vector2(0f, 0.5f);
            panelRect.pivot = new Vector2(0f, 0.5f);
            panelRect.anchoredPosition = new Vector2(12f, 0f);
            panelRect.sizeDelta = new Vector2(280f, 420f);

            GameObject title = new GameObject("Title", typeof(RectTransform));
            title.transform.SetParent(panel.transform, false);
            RectTransform titleRect = title.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.05f, 0.88f);
            titleRect.anchorMax = new Vector2(0.95f, 0.98f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            TextMeshProUGUI titleText = title.AddComponent<TextMeshProUGUI>();
            titleText.text = "Banned";
            titleText.fontSize = 22f;
            titleText.color = Color.white;
            titleText.alignment = TextAlignmentOptions.Center;

            GameObject scrollGo = new GameObject("BannedScroll", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
            scrollGo.transform.SetParent(panel.transform, false);
            RectTransform scrollRect = scrollGo.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0.04f, 0.04f);
            scrollRect.anchorMax = new Vector2(0.96f, 0.86f);
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = Vector2.zero;
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scrollGo.transform, false);
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            StretchFull(viewportRect);
            viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;

            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            layout.spacing = 6f;

            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.viewport = viewportRect;
            scroll.content = contentRect;

            panel.SetActive(false);
            return panel.transform;
        }

        static Transform EnsureHostModerationPanel(Transform canvas) =>
            PausePlayerListSetup.EnsurePlayerListPanel(canvas);

        static Transform EnsureChatRoot(Transform canvas)
        {
            Transform existing = canvas.Find("ModeratedChatUiRoot");
            if (existing != null)
                return existing;

            GameObject root = new GameObject("ModeratedChatUiRoot", typeof(RectTransform));
            root.transform.SetParent(canvas, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 0f);
            rootRect.anchorMax = new Vector2(0f, 0f);
            rootRect.pivot = new Vector2(0f, 0f);
            rootRect.anchoredPosition = new Vector2(18f, 18f);
            rootRect.sizeDelta = new Vector2(420f, 320f);

            GameObject chatPanel = new GameObject("ChatPanel", typeof(RectTransform));
            chatPanel.transform.SetParent(root.transform, false);
            StretchFull(chatPanel.GetComponent<RectTransform>());

            GameObject scrollGo = new GameObject("ChatScroll", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
            scrollGo.transform.SetParent(chatPanel.transform, false);
            RectTransform scrollRect = scrollGo.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0f, 0.22f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = Vector2.zero;
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scrollGo.transform, false);
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            StretchFull(viewportRect);
            viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;

            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            layout.spacing = 4f;
            layout.padding = new RectOffset(8, 8, 8, 8);

            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.viewport = viewportRect;
            scroll.content = contentRect;

            GameObject inputContainer = new GameObject("InputContainer", typeof(RectTransform));
            inputContainer.transform.SetParent(chatPanel.transform, false);
            RectTransform inputContainerRect = inputContainer.GetComponent<RectTransform>();
            inputContainerRect.anchorMin = new Vector2(0f, 0f);
            inputContainerRect.anchorMax = new Vector2(1f, 0.22f);
            inputContainerRect.offsetMin = Vector2.zero;
            inputContainerRect.offsetMax = Vector2.zero;

            TMP_InputField chatInput = CreateInputField(inputContainer.transform, "ChatInput", new Vector2(0f, 12f),
                new Vector2(400f, 36f));
            chatInput.contentType = TMP_InputField.ContentType.Standard;
            chatInput.characterLimit = 240;
            SetInputPlaceholder(chatInput, "Write a message...");
            RectTransform chatInputRect = chatInput.GetComponent<RectTransform>();
            chatInputRect.anchorMin = new Vector2(0.5f, 0.55f);
            chatInputRect.anchorMax = new Vector2(0.5f, 0.55f);

            GameObject status = new GameObject("StatusText", typeof(RectTransform));
            status.transform.SetParent(inputContainer.transform, false);
            RectTransform statusRect = status.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.02f, 0f);
            statusRect.anchorMax = new Vector2(0.98f, 0.4f);
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = Vector2.zero;
            TextMeshProUGUI statusText = status.AddComponent<TextMeshProUGUI>();
            statusText.fontSize = 16f;
            statusText.color = new Color(1f, 1f, 1f, 0f);
            statusText.alignment = TextAlignmentOptions.Center;

            return root.transform;
        }

        static Transform EnsureModeratorPanel(Transform canvas)
        {
            Transform existing = canvas.Find("ModeratorModerationPanel");
            if (existing != null)
                return existing;

            GameObject panel = CreateImage("ModeratorModerationPanel", canvas, new Color(0f, 0f, 0f, 0.65f));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -18f);
            panelRect.sizeDelta = new Vector2(560f, 140f);
            panel.SetActive(false);

            GameObject requestTextGo = new GameObject("ModeratorRequestText", typeof(RectTransform));
            requestTextGo.transform.SetParent(panel.transform, false);
            RectTransform requestRect = requestTextGo.GetComponent<RectTransform>();
            requestRect.anchorMin = new Vector2(0.04f, 0.55f);
            requestRect.anchorMax = new Vector2(0.96f, 0.95f);
            requestRect.offsetMin = Vector2.zero;
            requestRect.offsetMax = Vector2.zero;
            TextMeshProUGUI requestText = requestTextGo.AddComponent<TextMeshProUGUI>();
            requestText.fontSize = 20f;
            requestText.color = Color.white;
            requestText.alignment = TextAlignmentOptions.Center;

            CreateMenuButton(panel.transform, "AcceptButton", "Accept", new Vector2(0.14f, 0.22f), new Vector2(120f, 40f));
            CreateMenuButton(panel.transform, "RejectButton", "Reject", new Vector2(0.38f, 0.22f), new Vector2(120f, 40f));
            CreateMenuButton(panel.transform, "KickButton", "Kick", new Vector2(0.62f, 0.22f), new Vector2(120f, 40f));
            CreateMenuButton(panel.transform, "BanButton", "Ban", new Vector2(0.86f, 0.22f), new Vector2(120f, 40f));

            return panel.transform;
        }

        static Transform EnsurePauseMenuRoot(Transform canvas)
        {
            Transform existing = canvas.Find("PauseMenuRoot");
            if (existing != null)
                return existing;

            var rootObject = new GameObject("PauseMenuRoot", typeof(RectTransform));
            rootObject.transform.SetParent(canvas, false);
            StretchFull(rootObject.GetComponent<RectTransform>());
            rootObject.SetActive(false);
            return rootObject.transform;
        }

        static void EnsurePauseMenuHierarchy(Transform root)
        {
            CreateDimBackground(root);
            CreatePauseMenuPanel(root);
            CreateSaveSuccessPanel(root);
            CreateExitWithoutSavePanel(root);
            CreateOpenToLanPanel(root);
        }

        static void CreateDimBackground(Transform root)
        {
            if (root.Find("DimBackground") != null)
                return;

            GameObject dim = CreateImage("DimBackground", root, new Color(0f, 0f, 0f, 0.55f));
            StretchFull(dim.GetComponent<RectTransform>());
        }

        static void CreatePauseMenuPanel(Transform root)
        {
            Transform panel = root.Find("PauseMenuPanel");
            if (panel == null)
            {
                GameObject panelObject = CreateImage("PauseMenuPanel", root, new Color(0.12f, 0.12f, 0.12f, 0.95f));
                RectTransform rect = panelObject.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(360f, 320f);
                panel = panelObject.transform;
            }

            CreateMenuButton(panel, "ResumeButton", "Resume", new Vector2(0.5f, 0.78f));
            CreateMenuButton(panel, "SaveButton", "Save", new Vector2(0.5f, 0.58f));
            CreateMenuButton(panel, "SaveAndExitButton", "Save and Exit", new Vector2(0.5f, 0.38f));
            CreateMenuButton(panel, "ExitButton", "Exit", new Vector2(0.5f, 0.18f));
            CreateMenuButton(panel, "OpenToLanButton", "Open to LAN", new Vector2(0.5f, -0.02f));
        }

        static void CreateSaveSuccessPanel(Transform root)
        {
            Transform panel = EnsureModalPanel(root, "SaveSuccessPanel", "Level is Saved!");
            CreateMenuButton(panel, "CloseButton", "X", new Vector2(0.5f, 0.15f), new Vector2(48f, 48f));
        }

        static void CreateExitWithoutSavePanel(Transform root)
        {
            Transform panel = EnsureModalPanel(root, "ExitWithoutSavePanel", "Are you sure you want to exit without saving?");
            CreateMenuButton(panel, "ConfirmExitButton", "Exit", new Vector2(0.35f, 0.15f));
            CreateMenuButton(panel, "CancelButton", "Cancel", new Vector2(0.65f, 0.15f));
        }

        static void CreateOpenToLanPanel(Transform root)
        {
            Transform panel = EnsureModalPanel(root, "OpenToLanPanel",
                "Open this world to LAN?\nOther players will see it in Network Game.");
            EnsureModalPanelSize(panel, new Vector2(520f, 320f));
            EnsurePortInput(panel);
            EnsureTransportLabel(panel);
            CreateMenuButton(panel, "ContinueButton", "Continue", new Vector2(0.35f, 0.15f));
            CreateMenuButton(panel, "CancelButton", "Cancel", new Vector2(0.65f, 0.15f));
        }

        static void EnsureTransportLabel(Transform panel)
        {
            if (panel == null || panel.Find("TransportLabel") != null)
                return;

            GameObject labelObject = new GameObject("TransportLabel", typeof(RectTransform));
            labelObject.transform.SetParent(panel, false);
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -70f);
            rect.sizeDelta = new Vector2(460f, 48f);

            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = "Transport: KCP (UDP)\n(change in Main Menu before hosting)";
            label.fontSize = 16f;
            label.color = new Color(1f, 1f, 1f, 0.85f);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
        }

        static void EnsureModalPanelSize(Transform panel, Vector2 sizeDelta)
        {
            if (panel == null)
                return;

            if (panel.TryGetComponent(out RectTransform rect))
                rect.sizeDelta = sizeDelta;
        }

        static void EnsurePortInput(Transform panel)
        {
            if (panel == null)
                return;

            if (panel.Find("PortLabel") == null)
            {
                GameObject labelObject = new GameObject("PortLabel", typeof(RectTransform));
                labelObject.transform.SetParent(panel, false);
                RectTransform rect = labelObject.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(-90f, -24f);
                rect.sizeDelta = new Vector2(120f, 32f);

                TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
                label.text = "Port:";
                label.alignment = TextAlignmentOptions.Right;
                label.fontSize = 22f;
                label.color = Color.white;
            }

            if (panel.Find("PortInput") == null)
            {
                TMP_InputField input = CreateInputField(panel, "PortInput", new Vector2(40f, -24f), new Vector2(160f, 36f));
                SetInputPlaceholder(input, "25565");
            }
        }

        static TMP_InputField CreateInputField(Transform parent, string name, Vector2 position, Vector2 size)
        {
            GameObject inputObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            inputObject.transform.SetParent(parent, false);

            RectTransform rect = inputObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = inputObject.GetComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            GameObject textArea = new GameObject("Text Area", typeof(RectTransform));
            textArea.transform.SetParent(inputObject.transform, false);
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
            StretchFull(placeholder.rectTransform);

            GameObject textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(textArea.transform, false);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = 18f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            StretchFull(text.rectTransform);

            TMP_InputField inputField = inputObject.AddComponent<TMP_InputField>();
            inputField.contentType = TMP_InputField.ContentType.IntegerNumber;
            inputField.textViewport = textAreaRect;
            inputField.textComponent = text;
            inputField.placeholder = placeholder;
            inputField.targetGraphic = image;
            return inputField;
        }

        static void SetInputPlaceholder(TMP_InputField inputField, string text)
        {
            if (inputField?.placeholder is TextMeshProUGUI placeholder)
                placeholder.text = text;
        }

        static Transform EnsureModalPanel(Transform root, string name, string message)
        {
            Transform panel = root.Find(name);
            if (panel != null)
                return panel;

            GameObject panelObject = CreateImage(name, root, new Color(0.12f, 0.12f, 0.12f, 0.98f));
            RectTransform rect = panelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(520f, 220f);
            panelObject.SetActive(false);

            GameObject labelObject = new GameObject("MessageText", typeof(RectTransform));
            labelObject.transform.SetParent(panelObject.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.1f, 0.35f);
            labelRect.anchorMax = new Vector2(0.9f, 0.9f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = message;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 24f;
            label.color = Color.white;

            return panelObject.transform;
        }

        static void CreateMenuButton(Transform parent, string name, string text, Vector2 anchorY, Vector2? size = null)
        {
            if (parent.Find(name) != null)
                return;

            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(anchorY.x, anchorY.y);
            rect.anchorMax = new Vector2(anchorY.x, anchorY.y);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size ?? new Vector2(280f, 44f);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            GameObject labelObject = new GameObject("Text", typeof(RectTransform));
            labelObject.transform.SetParent(buttonObject.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            StretchFull(labelRect);

            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 20f;
            label.color = Color.white;
        }

        static GameObject CreateImage(string name, Transform parent, Color color)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            return imageObject;
        }

        static void EnsurePauseMenuComponent(GameObject canvasObject)
        {
            PauseMenuUI pauseMenuUi = canvasObject.GetComponent<PauseMenuUI>();
            if (pauseMenuUi == null)
                pauseMenuUi = canvasObject.AddComponent<PauseMenuUI>();

            RunnerPauseMenuUI runnerPauseMenuUi = canvasObject.GetComponent<RunnerPauseMenuUI>();
            if (runnerPauseMenuUi == null)
                runnerPauseMenuUi = canvasObject.AddComponent<RunnerPauseMenuUI>();

            EditorUtility.SetDirty(pauseMenuUi);
            EditorUtility.SetDirty(runnerPauseMenuUi);
        }

        static void EnsureRunnerPauseMenu(Transform canvas)
        {
            Transform root = canvas.Find("RunnerPauseMenuRoot");
            if (root == null)
            {
                var rootObject = new GameObject("RunnerPauseMenuRoot", typeof(RectTransform));
                rootObject.transform.SetParent(canvas, false);
                StretchFull(rootObject.GetComponent<RectTransform>());
                rootObject.SetActive(false);
                root = rootObject.transform;
            }

            if (root.Find("DimBackground") == null)
            {
                GameObject dim = CreateImage("DimBackground", root, new Color(0f, 0f, 0f, 0.55f));
                StretchFull(dim.GetComponent<RectTransform>());
            }

            Transform panel = root.Find("RunnerPauseMenuPanel");
            if (panel == null)
            {
                GameObject panelObject = CreateImage("RunnerPauseMenuPanel", root, new Color(0.12f, 0.12f, 0.12f, 0.95f));
                RectTransform rect = panelObject.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(320f, 180f);
                panel = panelObject.transform;
            }

            CreateMenuButton(panel, "ResumeButton", "Resume", new Vector2(0.5f, 0.62f));
            CreateMenuButton(panel, "ExitButton", "Exit", new Vector2(0.5f, 0.32f));
        }

        static void EnsureWorldLoadBootstrap()
        {
            LevelGridServer grid = Object.FindFirstObjectByType<LevelGridServer>();
            if (grid == null)
            {
                Debug.LogWarning("[Kaplao] LevelGridServer not found. Add WorldLoadBootstrap manually.");
                return;
            }

            if (grid.GetComponent<WorldLoadBootstrap>() == null)
                grid.gameObject.AddComponent<WorldLoadBootstrap>();
        }

        static void DisableLegacyUi(Transform canvas)
        {
            Transform leaveButton = canvas.Find("LeaveButton");
            if (leaveButton != null)
                leaveButton.gameObject.SetActive(false);

            Transform levelIoPanel = canvas.Find("CreateModeUI/LevelIoPanel");
            if (levelIoPanel == null)
            {
                foreach (Transform child in canvas)
                {
                    Transform found = child.Find("LevelIoPanel");
                    if (found != null)
                    {
                        levelIoPanel = found;
                        break;
                    }
                }
            }

            if (levelIoPanel != null)
                levelIoPanel.gameObject.SetActive(false);
        }

        static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
#endif
