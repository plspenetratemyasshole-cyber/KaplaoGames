#if UNITY_EDITOR
using KaplaoGames.Client;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KaplaoGames.Editor
{
    /// <summary>
    /// Builds the full Code mode IDE UI (file tree, tabs, editor, context menu).
    /// Assign File Icon / Folder Icon on KaplaoCodeModeUi in the Inspector after running.
    /// </summary>
    public static class CodeModeIdeSetup
    {
        const string GameScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Kaplao/Setup Code Mode IDE")]
        public static void SetupCodeModeIde()
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[Kaplao] Canvas not found.");
                return;
            }

            KaplaoCodeModeUi codeUi = canvas.GetComponent<KaplaoCodeModeUi>();
            if (codeUi == null)
                codeUi = canvas.gameObject.AddComponent<KaplaoCodeModeUi>();

            Transform existing = canvas.transform.Find("CodeModeOverlay");
            GameObject overlay = existing != null
                ? existing.gameObject
                : new GameObject("CodeModeOverlay", typeof(RectTransform), typeof(Image));

            if (existing == null)
                overlay.transform.SetParent(canvas.transform, false);

            Transform createModeRoot = canvas.transform.Find("CreateModeUI");
            int createModeIndex = createModeRoot != null ? createModeRoot.GetSiblingIndex() : 0;
            overlay.transform.SetSiblingIndex(createModeIndex);

            RectTransform overlayRect = overlay.GetComponent<RectTransform>();
            StretchFull(overlayRect);

            Image background = overlay.GetComponent<Image>() ?? overlay.AddComponent<Image>();
            background.color = new Color(0.04f, 0.05f, 0.08f, 0.97f);
            background.raycastTarget = true;

            // --- File tree ---
            GameObject treePanel = FindOrCreateChild(overlay.transform, "FileTreePanel");
            Image treeBg = treePanel.GetComponent<Image>() ?? treePanel.AddComponent<Image>();
            treeBg.color = new Color(0.07f, 0.08f, 0.1f, 1f);
            treeBg.raycastTarget = true;

            GameObject treeScrollObject = FindOrCreateChild(treePanel.transform, "FileTreeScroll");
            RectTransform treeScrollRectTransform = treeScrollObject.GetComponent<RectTransform>();
            StretchFull(treeScrollRectTransform);
            treeScrollRectTransform.offsetMin = new Vector2(0f, 0f);
            treeScrollRectTransform.offsetMax = new Vector2(0f, -28f);

            ScrollRect treeScroll = treeScrollObject.GetComponent<ScrollRect>() ?? treeScrollObject.AddComponent<ScrollRect>();
            treeScroll.horizontal = false;
            treeScroll.vertical = true;
            treeScroll.movementType = ScrollRect.MovementType.Clamped;

            Image treeScrollImage = treeScrollObject.GetComponent<Image>() ?? treeScrollObject.AddComponent<Image>();
            treeScrollImage.color = new Color(0f, 0f, 0f, 0.01f);
            treeScrollImage.raycastTarget = true;

            GameObject viewport = FindOrCreateChild(treeScrollObject.transform, "Viewport");
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            StretchFull(viewportRect);
            Mask mask = viewport.GetComponent<Mask>() ?? viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            Image viewportImage = viewport.GetComponent<Image>() ?? viewport.AddComponent<Image>();
            viewportImage.color = Color.white;

            GameObject content = FindOrCreateChild(viewport.transform, "Content");
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 100f);

            treeScroll.viewport = viewportRect;
            treeScroll.content = contentRect;

            KaplaoCodeFileTreePanel fileTree =
                treePanel.GetComponent<KaplaoCodeFileTreePanel>() ??
                treePanel.AddComponent<KaplaoCodeFileTreePanel>();

            SerializedObject treeSo = new SerializedObject(fileTree);
            treeSo.FindProperty("contentRoot").objectReferenceValue = contentRect;
            treeSo.ApplyModifiedPropertiesWithoutUndo();

            FileTreeBackgroundContext bgContext =
                treeScrollObject.GetComponent<FileTreeBackgroundContext>() ??
                treeScrollObject.AddComponent<FileTreeBackgroundContext>();
            bgContext.Panel = fileTree;

            TextMeshProUGUI treeTitle = FindOrCreateLabel(treePanel.transform, "FileTreeTitle", "SCRIPTS",
                new Vector2(8f, -6f), new Vector2(240f, 22f), 13f);
            RectTransform titleRect = treeTitle.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.anchoredPosition = new Vector2(8f, -4f);
            titleRect.sizeDelta = new Vector2(-16f, 22f);
            treeTitle.color = new Color(0.65f, 0.7f, 0.78f, 1f);

            // --- Tab bar ---
            GameObject tabBarObject = FindOrCreateChild(overlay.transform, "TabBar");
            Image tabBarBg = tabBarObject.GetComponent<Image>() ?? tabBarObject.AddComponent<Image>();
            tabBarBg.color = new Color(0.08f, 0.09f, 0.11f, 1f);

            GameObject tabContent = FindOrCreateChild(tabBarObject.transform, "TabContent");
            RectTransform tabContentRect = tabContent.GetComponent<RectTransform>();
            StretchFull(tabContentRect);
            tabContentRect.pivot = new Vector2(0f, 1f);
            tabContentRect.offsetMin = new Vector2(4f, 2f);
            tabContentRect.offsetMax = new Vector2(-4f, -2f);

            HorizontalLayoutGroup tabLayout = tabContent.GetComponent<HorizontalLayoutGroup>();
            if (tabLayout == null)
                tabLayout = tabContent.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 4f;
            tabLayout.childAlignment = TextAnchor.UpperLeft;
            tabLayout.childControlWidth = false;
            tabLayout.childControlHeight = false;
            tabLayout.childForceExpandWidth = false;
            tabLayout.childForceExpandHeight = false;

            KaplaoCodeTabBar tabBar =
                tabBarObject.GetComponent<KaplaoCodeTabBar>() ??
                tabBarObject.AddComponent<KaplaoCodeTabBar>();

            SerializedObject tabSo = new SerializedObject(tabBar);
            tabSo.FindProperty("contentRoot").objectReferenceValue = tabContentRect;
            tabSo.ApplyModifiedPropertiesWithoutUndo();

            // --- Editor ---
            TMP_InputField codeInput = FindOrCreateMultilineInputField(overlay.transform, "CodeInputField");
            if (codeInput.GetComponent<LevelScriptCodeEditorEnhancer>() == null)
                codeInput.gameObject.AddComponent<LevelScriptCodeEditorEnhancer>();

            Button runButton = FindOrCreateButton(overlay.transform, "RunScriptButton", "Run",
                new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(120f, 40f));
            RectTransform runRect = runButton.GetComponent<RectTransform>();
            runRect.pivot = new Vector2(1f, 0f);

            Button documentationButton = FindOrCreateButton(overlay.transform, "DocumentationButton", "Documentation",
                new Vector2(0f, 0f), new Vector2(284f, 24f), new Vector2(160f, 40f));
            RectTransform docRect = documentationButton.GetComponent<RectTransform>();
            docRect.pivot = new Vector2(0f, 0f);

            TextMeshProUGUI statusLabel = FindOrCreateLabel(overlay.transform, "CodeStatusLabel",
                "Write script and press Run.", new Vector2(0f, 24f), new Vector2(700f, 32f), 18f);

            // --- Context menu ---
            GameObject contextRoot = FindOrCreateChild(overlay.transform, "CodeContextMenu");
            Image contextBg = contextRoot.GetComponent<Image>() ?? contextRoot.AddComponent<Image>();
            contextBg.color = new Color(0.12f, 0.13f, 0.16f, 0.98f);
            RectTransform contextRect = contextRoot.GetComponent<RectTransform>();
            contextRect.sizeDelta = new Vector2(180f, 120f);
            GameObject contextButtons = FindOrCreateChild(contextRoot.transform, "Buttons");
            StretchFull(contextButtons.GetComponent<RectTransform>());

            KaplaoCodeContextMenuUi contextMenu =
                overlay.GetComponent<KaplaoCodeContextMenuUi>() ??
                overlay.AddComponent<KaplaoCodeContextMenuUi>();

            SerializedObject ctxSo = new SerializedObject(contextMenu);
            ctxSo.FindProperty("root").objectReferenceValue = contextRect;
            ctxSo.FindProperty("buttonContainer").objectReferenceValue = contextButtons.GetComponent<RectTransform>();
            ctxSo.ApplyModifiedPropertiesWithoutUndo();
            contextRoot.SetActive(false);

            // --- Name prompt ---
            GameObject promptRoot = FindOrCreateChild(overlay.transform, "NamePrompt");
            Image promptBg = promptRoot.GetComponent<Image>() ?? promptRoot.AddComponent<Image>();
            promptBg.color = new Color(0.1f, 0.11f, 0.14f, 0.98f);
            RectTransform promptRect = promptRoot.GetComponent<RectTransform>();
            promptRect.anchorMin = new Vector2(0.5f, 0.5f);
            promptRect.anchorMax = new Vector2(0.5f, 0.5f);
            promptRect.pivot = new Vector2(0.5f, 0.5f);
            promptRect.sizeDelta = new Vector2(320f, 120f);

            TextMeshProUGUI promptTitle = FindOrCreateLabel(promptRoot.transform, "Title", "New File",
                new Vector2(0f, 36f), new Vector2(280f, 24f), 18f);
            promptTitle.alignment = TextAlignmentOptions.Center;

            TMP_InputField promptInput = FindOrCreateSingleLineInput(promptRoot.transform, "NameInput");
            RectTransform promptInputRect = promptInput.GetComponent<RectTransform>();
            promptInputRect.anchorMin = new Vector2(0.5f, 0.5f);
            promptInputRect.anchorMax = new Vector2(0.5f, 0.5f);
            promptInputRect.pivot = new Vector2(0.5f, 0.5f);
            promptInputRect.anchoredPosition = new Vector2(0f, 4f);
            promptInputRect.sizeDelta = new Vector2(280f, 32f);

            FindOrCreateButton(promptRoot.transform, "OkButton", "OK",
                new Vector2(0.5f, 0.5f), new Vector2(-60f, -36f), new Vector2(90f, 30f));
            FindOrCreateButton(promptRoot.transform, "CancelButton", "Cancel",
                new Vector2(0.5f, 0.5f), new Vector2(60f, -36f), new Vector2(90f, 30f));

            promptRoot.SetActive(false);

            // --- Delete confirm ---
            GameObject deleteRoot = FindOrCreateChild(overlay.transform, "DeleteConfirm");
            Image deleteBg = deleteRoot.GetComponent<Image>() ?? deleteRoot.AddComponent<Image>();
            deleteBg.color = new Color(0.1f, 0.11f, 0.14f, 0.98f);
            RectTransform deleteRect = deleteRoot.GetComponent<RectTransform>();
            deleteRect.anchorMin = new Vector2(0.5f, 0.5f);
            deleteRect.anchorMax = new Vector2(0.5f, 0.5f);
            deleteRect.pivot = new Vector2(0.5f, 0.5f);
            deleteRect.sizeDelta = new Vector2(360f, 140f);

            TextMeshProUGUI deleteMessage = FindOrCreateLabel(deleteRoot.transform, "Message", "Are you sure?",
                new Vector2(0f, 28f), new Vector2(320f, 56f), 18f);
            deleteMessage.alignment = TextAlignmentOptions.Center;
            deleteMessage.textWrappingMode = TextWrappingModes.Normal;

            FindOrCreateButton(deleteRoot.transform, "OkButton", "Delete",
                new Vector2(0.5f, 0.5f), new Vector2(-70f, -42f), new Vector2(110f, 32f));
            FindOrCreateButton(deleteRoot.transform, "CancelButton", "Cancel",
                new Vector2(0.5f, 0.5f), new Vector2(70f, -42f), new Vector2(110f, 32f));
            deleteRoot.SetActive(false);

            KaplaoCodeModeUi.ApplyIdeLayout(overlay, codeInput, runButton, statusLabel, documentationButton);
            overlay.SetActive(false);

            SerializedObject so = new SerializedObject(codeUi);
            so.FindProperty("overlayRoot").objectReferenceValue = overlay;
            so.FindProperty("codeInputField").objectReferenceValue = codeInput;
            so.FindProperty("runButton").objectReferenceValue = runButton;
            so.FindProperty("documentationButton").objectReferenceValue = documentationButton;
            so.FindProperty("statusLabel").objectReferenceValue = statusLabel;
            so.FindProperty("fileTreePanel").objectReferenceValue = fileTree;
            so.FindProperty("tabBar").objectReferenceValue = tabBar;
            so.FindProperty("contextMenu").objectReferenceValue = contextMenu;
            so.FindProperty("namePromptRoot").objectReferenceValue = promptRoot;
            so.FindProperty("namePromptInput").objectReferenceValue = promptInput;
            so.FindProperty("namePromptTitle").objectReferenceValue = promptTitle;
            so.FindProperty("deleteConfirmRoot").objectReferenceValue = deleteRoot;
            so.FindProperty("deleteConfirmMessage").objectReferenceValue = deleteMessage;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(codeUi);
            EditorUtility.SetDirty(overlay);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[Kaplao] Code Mode IDE setup complete.\n" +
                      "Assign sprites on Canvas → KaplaoCodeModeUi:\n" +
                      "  • File Icon\n" +
                      "  • Folder Icon\n" +
                      "  • Folder Toggle Icon (expand/collapse arrow; rotates when open)\n" +
                      "Then enter Code mode in Play.");
        }

        static GameObject FindOrCreateChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return existing.gameObject;

            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child;
        }

        static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static TextMeshProUGUI FindOrCreateLabel(Transform parent, string name, string text, Vector2 position, Vector2 size, float fontSize)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                TextMeshProUGUI label = existing.GetComponent<TextMeshProUGUI>();
                if (label != null)
                {
                    label.text = text;
                    return label;
                }
            }

            GameObject labelObject = new GameObject(name, typeof(RectTransform));
            labelObject.transform.SetParent(parent, false);
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            TextMeshProUGUI tmp = labelObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.raycastTarget = false;
            return tmp;
        }

        static Button FindOrCreateButton(Transform parent, string name, string label, Vector2 anchor, Vector2 position, Vector2 size)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return existing.GetComponent<Button>();

            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            GameObject textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(buttonObject.transform, false);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 18f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            StretchFull(text.rectTransform);
            return button;
        }

        static TMP_InputField FindOrCreateMultilineInputField(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return existing.GetComponent<TMP_InputField>();

            GameObject inputObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            inputObject.transform.SetParent(parent, false);

            Image image = inputObject.GetComponent<Image>();
            image.color = new Color(0.12f, 0.13f, 0.16f, 1f);

            GameObject textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(inputObject.transform, false);
            RectTransform textAreaRect = textArea.GetComponent<RectTransform>();
            StretchFull(textAreaRect);
            textAreaRect.offsetMin = new Vector2(12f, 12f);
            textAreaRect.offsetMax = new Vector2(-12f, -12f);

            GameObject textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(textArea.transform, false);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = 20f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            StretchFull(text.rectTransform);

            TMP_InputField inputField = inputObject.AddComponent<TMP_InputField>();
            inputField.textViewport = textAreaRect;
            inputField.textComponent = text;
            inputField.lineType = TMP_InputField.LineType.MultiLineNewline;
            inputField.targetGraphic = image;
            return inputField;
        }

        static TMP_InputField FindOrCreateSingleLineInput(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return existing.GetComponent<TMP_InputField>();

            GameObject inputObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            inputObject.transform.SetParent(parent, false);
            Image image = inputObject.GetComponent<Image>();
            image.color = new Color(0.16f, 0.17f, 0.2f, 1f);

            GameObject textArea = new GameObject("Text Area", typeof(RectTransform));
            textArea.transform.SetParent(inputObject.transform, false);
            RectTransform textAreaRect = textArea.GetComponent<RectTransform>();
            StretchFull(textAreaRect);
            textAreaRect.offsetMin = new Vector2(8f, 4f);
            textAreaRect.offsetMax = new Vector2(-8f, -4f);

            GameObject textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(textArea.transform, false);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = 16f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            StretchFull(text.rectTransform);

            TMP_InputField inputField = inputObject.AddComponent<TMP_InputField>();
            inputField.textViewport = textAreaRect;
            inputField.textComponent = text;
            inputField.lineType = TMP_InputField.LineType.SingleLine;
            inputField.targetGraphic = image;
            return inputField;
        }
    }
}
#endif
