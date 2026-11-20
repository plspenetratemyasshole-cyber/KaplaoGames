#if UNITY_EDITOR
using KaplaoGames.Client;
using KaplaoGames.Shared;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.Editor
{
    public static class TexturePickerPanelSetup
    {
        const string PanelName = "TexturePickerPanel";

        [MenuItem("Kaplao/Setup Texture Picker UI")]
        public static void SetupFromMenu()
        {
            if (!TryGetCanvasAndRoot(out Canvas canvas, out Transform createModeRoot))
                return;

            KaplaoTileTextureUi textureUi = EnsureTextureUi(canvas);
            if (TryBindExistingPanel(textureUi, createModeRoot))
            {
                Debug.Log("[Kaplao] Bound existing TexturePickerPanel (layout preserved).");
                MarkSceneDirty();
                return;
            }

            BuildPanel(textureUi, createModeRoot);
            Debug.Log("[Kaplao] Texture picker panel created under CreateModeUI/TexturePickerPanel.");
            MarkSceneDirty();
        }

        [MenuItem("Kaplao/Bind Existing Texture Picker UI")]
        public static void BindExistingFromMenu()
        {
            if (!TryGetCanvasAndRoot(out Canvas canvas, out Transform createModeRoot))
                return;

            KaplaoTileTextureUi textureUi = canvas.GetComponent<KaplaoTileTextureUi>();
            if (textureUi == null)
            {
                Debug.LogWarning("[Kaplao] KaplaoTileTextureUi not found on Canvas.");
                return;
            }

            if (!TryBindExistingPanel(textureUi, createModeRoot))
                Debug.LogWarning("[Kaplao] TexturePickerPanel not found. Use Kaplao/Setup Texture Picker UI.");
            else
                MarkSceneDirty();
        }

        public static bool TryBindExistingPanel(KaplaoTileTextureUi textureUi, Transform parent)
        {
            Transform panel = parent.Find(PanelName);
            if (panel == null)
                return false;

            RectTransform panelRoot = panel.GetComponent<RectTransform>();
            TMP_InputField searchField = panel.Find("SearchRow/SearchField")?.GetComponent<TMP_InputField>();
            RectTransform listContent = panel.Find("ScrollRoot/Viewport/Content") as RectTransform;
            GridLayoutGroup listGrid = listContent != null ? listContent.GetComponent<GridLayoutGroup>() : null;
            TextMeshProUGUI statusLabel = panel.Find("Footer/StatusLabel")?.GetComponent<TextMeshProUGUI>();
            Button closeButton = panel.Find("Footer/CloseButton")?.GetComponent<Button>();
            TMP_InputField renameInputField = panel.Find("Footer/RenameInputField")?.GetComponent<TMP_InputField>();
            Button changeNameButton = panel.Find("Footer/ChangeNameButton")?.GetComponent<Button>();

            var references = new KaplaoTileTextureUi.PanelReferences
            {
                PanelRoot = panelRoot,
                SearchField = searchField,
                ListContent = listContent,
                ListGrid = listGrid,
                StatusLabel = statusLabel,
                CloseButton = closeButton,
                RenameInputField = renameInputField,
                ChangeNameButton = changeNameButton
            };

            textureUi.AssignPanelReferences(references);

            SerializedObject serializedUi = new SerializedObject(textureUi);
            serializedUi.FindProperty("kaplaoOnlyRoot").objectReferenceValue = parent.gameObject;
            serializedUi.FindProperty("panelRoot").objectReferenceValue = panelRoot;
            serializedUi.FindProperty("searchField").objectReferenceValue = searchField;
            serializedUi.FindProperty("listContent").objectReferenceValue = listContent;
            serializedUi.FindProperty("listGrid").objectReferenceValue = listGrid;
            serializedUi.FindProperty("statusLabel").objectReferenceValue = statusLabel;
            serializedUi.FindProperty("closeButton").objectReferenceValue = closeButton;
            serializedUi.FindProperty("renameInputField").objectReferenceValue = renameInputField;
            serializedUi.FindProperty("changeNameButton").objectReferenceValue = changeNameButton;
            serializedUi.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(textureUi);
            EditorUtility.SetDirty(panel.gameObject);
            return true;
        }

        public static void BuildPanel(KaplaoTileTextureUi textureUi, Transform parent)
        {
            SerializedObject serializedUi = new SerializedObject(textureUi);
            serializedUi.FindProperty("kaplaoOnlyRoot").objectReferenceValue = parent.gameObject;

            Vector2 panelSize = serializedUi.FindProperty("panelSize").vector2Value;
            if (panelSize == Vector2.zero)
                panelSize = new Vector2(640f, 520f);

            GameObject panelObject = new GameObject(PanelName, typeof(RectTransform));
            panelObject.transform.SetParent(parent, false);
            RectTransform panelRoot = panelObject.GetComponent<RectTransform>();
            panelRoot.anchorMin = new Vector2(0.5f, 0.5f);
            panelRoot.anchorMax = new Vector2(0.5f, 0.5f);
            panelRoot.pivot = new Vector2(0.5f, 0.5f);
            panelRoot.anchoredPosition = Vector2.zero;
            panelRoot.sizeDelta = panelSize;

            Image panelBackground = panelObject.AddComponent<Image>();
            panelBackground.color = new Color(0.08f, 0.09f, 0.12f, 0.98f);

            VerticalLayoutGroup rootLayout = panelObject.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(14, 14, 14, 14);
            rootLayout.spacing = 10f;
            rootLayout.childAlignment = TextAnchor.UpperCenter;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = false;
            rootLayout.childForceExpandWidth = true;

            CreateHeader(panelObject.transform);
            TMP_InputField searchField = CreateSearchRow(panelObject.transform);
            RectTransform listContent = CreateScrollArea(panelObject.transform, out GridLayoutGroup listGrid);
            TextMeshProUGUI statusLabel = CreateFooter(
                panelObject.transform,
                out TMP_InputField renameInputField,
                out Button changeNameButton,
                out Button closeButton);

            CreateEditorSampleEntries(listContent, serializedUi);

            var references = new KaplaoTileTextureUi.PanelReferences
            {
                PanelRoot = panelRoot,
                SearchField = searchField,
                ListContent = listContent,
                ListGrid = listGrid,
                StatusLabel = statusLabel,
                CloseButton = closeButton,
                RenameInputField = renameInputField,
                ChangeNameButton = changeNameButton
            };

            textureUi.AssignPanelReferences(references);

            serializedUi.FindProperty("panelRoot").objectReferenceValue = panelRoot;
            serializedUi.FindProperty("searchField").objectReferenceValue = searchField;
            serializedUi.FindProperty("listContent").objectReferenceValue = listContent;
            serializedUi.FindProperty("listGrid").objectReferenceValue = listGrid;
            serializedUi.FindProperty("statusLabel").objectReferenceValue = statusLabel;
            serializedUi.FindProperty("closeButton").objectReferenceValue = closeButton;
            serializedUi.FindProperty("renameInputField").objectReferenceValue = renameInputField;
            serializedUi.FindProperty("changeNameButton").objectReferenceValue = changeNameButton;
            serializedUi.ApplyModifiedPropertiesWithoutUndo();

            panelObject.SetActive(true);
            EditorUtility.SetDirty(textureUi);
            EditorUtility.SetDirty(panelObject);
        }

        static bool TryGetCanvasAndRoot(out Canvas canvas, out Transform createModeRoot)
        {
            canvas = Object.FindFirstObjectByType<Canvas>();
            createModeRoot = null;

            if (canvas == null)
            {
                Debug.LogWarning("[Kaplao] Canvas not found in open scene.");
                return false;
            }

            createModeRoot = canvas.transform.Find("CreateModeUI");
            if (createModeRoot == null)
            {
                Debug.LogWarning("[Kaplao] CreateModeUI not found. Create it first or run Kaplao/Setup Create Mode UI.");
                return false;
            }

            return true;
        }

        static KaplaoTileTextureUi EnsureTextureUi(Canvas canvas)
        {
            KaplaoTileTextureUi textureUi = canvas.GetComponent<KaplaoTileTextureUi>();
            if (textureUi == null)
                textureUi = canvas.gameObject.AddComponent<KaplaoTileTextureUi>();
            return textureUi;
        }

        static void MarkSceneDirty()
        {
            if (EditorSceneManager.GetActiveScene().isLoaded)
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        static void CreateHeader(Transform parent)
        {
            GameObject header = new GameObject("Header", typeof(RectTransform));
            header.transform.SetParent(parent, false);
            LayoutElement headerLayout = header.AddComponent<LayoutElement>();
            headerLayout.preferredHeight = 28f;

            TextMeshProUGUI title = header.AddComponent<TextMeshProUGUI>();
            title.text = "Set Texture";
            title.fontSize = 22f;
            title.alignment = TextAlignmentOptions.Center;
            title.color = Color.white;
        }

        static TMP_InputField CreateSearchRow(Transform parent)
        {
            GameObject row = new GameObject("SearchRow", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            LayoutElement rowLayout = row.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = 34f;

            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = true;

            GameObject labelObject = new GameObject("SearchLabel", typeof(RectTransform));
            labelObject.transform.SetParent(row.transform, false);
            LayoutElement labelLayout = labelObject.AddComponent<LayoutElement>();
            labelLayout.preferredWidth = 56f;
            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = "Search";
            label.fontSize = 15f;
            label.color = new Color(1f, 1f, 1f, 0.85f);

            return CreateInputField(row.transform, "SearchField", 520f);
        }

        static RectTransform CreateScrollArea(Transform parent, out GridLayoutGroup listGrid)
        {
            GameObject scrollRoot = new GameObject("ScrollRoot", typeof(RectTransform));
            scrollRoot.transform.SetParent(parent, false);
            LayoutElement scrollLayout = scrollRoot.AddComponent<LayoutElement>();
            scrollLayout.preferredHeight = 390f;
            scrollLayout.flexibleHeight = 1f;

            Image scrollBackground = scrollRoot.AddComponent<Image>();
            scrollBackground.color = new Color(0.05f, 0.06f, 0.08f, 1f);

            ScrollRect scrollRect = scrollRoot.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 24f;

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(scrollRoot.transform, false);
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(6f, 6f);
            viewportRect.offsetMax = new Vector2(-6f, -6f);
            viewport.AddComponent<RectMask2D>();
            scrollRect.viewport = viewportRect;

            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            RectTransform listContent = content.GetComponent<RectTransform>();
            listContent.anchorMin = new Vector2(0f, 1f);
            listContent.anchorMax = new Vector2(1f, 1f);
            listContent.pivot = new Vector2(0.5f, 1f);
            listContent.anchoredPosition = Vector2.zero;

            listGrid = content.AddComponent<GridLayoutGroup>();
            listGrid.padding = new RectOffset(4, 4, 4, 4);
            listGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            listGrid.constraintCount = 5;
            listGrid.cellSize = new Vector2(108f, 118f);
            listGrid.spacing = new Vector2(8f, 8f);

            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = listContent;
            return listContent;
        }

        static TextMeshProUGUI CreateFooter(
            Transform parent,
            out TMP_InputField renameInputField,
            out Button changeNameButton,
            out Button closeButton)
        {
            GameObject footer = new GameObject("Footer", typeof(RectTransform));
            footer.transform.SetParent(parent, false);
            LayoutElement footerLayout = footer.AddComponent<LayoutElement>();
            footerLayout.preferredHeight = 68f;

            HorizontalLayoutGroup layout = footer.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 12f;
            layout.childControlWidth = false;
            layout.childControlHeight = true;

            renameInputField = CreateInputField(footer.transform, "RenameInputField", 180f);
            if (renameInputField != null)
            {
                renameInputField.text = string.Empty;
                if (renameInputField.placeholder == null)
                {
                    GameObject placeholderObject = new GameObject("Placeholder", typeof(RectTransform));
                    placeholderObject.transform.SetParent(renameInputField.textViewport, false);
                    RectTransform placeholderRect = placeholderObject.GetComponent<RectTransform>();
                    placeholderRect.anchorMin = Vector2.zero;
                    placeholderRect.anchorMax = Vector2.one;
                    placeholderRect.offsetMin = Vector2.zero;
                    placeholderRect.offsetMax = Vector2.zero;
                    TextMeshProUGUI placeholder = placeholderObject.AddComponent<TextMeshProUGUI>();
                    placeholder.text = "new custom name";
                    placeholder.fontSize = 14f;
                    placeholder.color = new Color(1f, 1f, 1f, 0.35f);
                    renameInputField.placeholder = placeholder;
                }
            }
            changeNameButton = CreateFooterButton(footer.transform, "ChangeNameButton", "Change Name");
            TextMeshProUGUI statusLabel = CreateFooterLabel(footer.transform, "StatusLabel", "Editor preview", 180f);
            closeButton = CreateFooterButton(footer.transform, "CloseButton", "Close");
            return statusLabel;
        }

        static void CreateEditorSampleEntries(Transform listContent, SerializedObject serializedUi)
        {
            float previewSize = serializedUi.FindProperty("previewSize").floatValue;
            if (previewSize <= 0f)
                previewSize = 88f;
            float nameRowHeight = serializedUi.FindProperty("nameRowHeight").floatValue;
            if (nameRowHeight <= 0f)
                nameRowHeight = 34f;

            BlockTextureCatalog catalog = AssetDatabase.LoadAssetAtPath<BlockTextureCatalog>(
                "Assets/Resources/BlockTextureCatalog.asset");

            if (catalog != null && catalog.Entries.Count > 0)
            {
                int count = Mathf.Min(12, catalog.Entries.Count);
                for (int i = 0; i < count; i++)
                {
                    BlockTextureCatalog.Entry entry = catalog.Entries[i];
                    if (entry.sprite == null)
                        continue;
                    CreateSampleEntry(listContent, entry.displayName, entry.sprite, previewSize, nameRowHeight);
                }
                return;
            }

            string[] names = { "stone", "dirt", "grass block", "oak log", "sand", "gravel" };
            Color[] colors =
            {
                new Color(0.55f, 0.55f, 0.55f),
                new Color(0.45f, 0.32f, 0.18f),
                new Color(0.35f, 0.65f, 0.25f),
                new Color(0.50f, 0.38f, 0.22f),
                new Color(0.85f, 0.78f, 0.45f),
                new Color(0.62f, 0.62f, 0.62f)
            };

            for (int i = 0; i < names.Length; i++)
                CreateSampleEntry(listContent, names[i], null, previewSize, nameRowHeight, colors[i]);
        }

        static void CreateSampleEntry(Transform parent, string displayName, Sprite sprite, float previewSize, float nameRowHeight, Color? tint = null)
        {
            GameObject item = new GameObject($"Sample_{displayName}", typeof(RectTransform));
            item.transform.SetParent(parent, false);

            Image background = item.AddComponent<Image>();
            background.color = new Color(0.12f, 0.13f, 0.16f, 1f);

            VerticalLayoutGroup layout = item.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;

            GameObject previewObject = new GameObject("Preview", typeof(RectTransform));
            previewObject.transform.SetParent(item.transform, false);
            LayoutElement previewLayout = previewObject.AddComponent<LayoutElement>();
            previewLayout.preferredWidth = previewSize;
            previewLayout.preferredHeight = previewSize;

            Image preview = previewObject.AddComponent<Image>();
            preview.sprite = sprite;
            preview.preserveAspect = true;
            preview.color = sprite != null ? Color.white : tint ?? Color.gray;

            GameObject nameObject = new GameObject("Name", typeof(RectTransform));
            nameObject.transform.SetParent(item.transform, false);
            LayoutElement nameLayout = nameObject.AddComponent<LayoutElement>();
            nameLayout.preferredHeight = nameRowHeight;

            TextMeshProUGUI nameLabel = nameObject.AddComponent<TextMeshProUGUI>();
            nameLabel.text = displayName;
            nameLabel.fontSize = 11f;
            nameLabel.alignment = TextAlignmentOptions.Top;
            nameLabel.textWrappingMode = TextWrappingModes.Normal;
            nameLabel.overflowMode = TextOverflowModes.Ellipsis;
            nameLabel.color = new Color(1f, 1f, 1f, 0.9f);
        }

        static TMP_InputField CreateInputField(Transform parent, string name, float width)
        {
            GameObject fieldObject = new GameObject(name, typeof(RectTransform));
            fieldObject.transform.SetParent(parent, false);
            LayoutElement fieldLayout = fieldObject.AddComponent<LayoutElement>();
            fieldLayout.preferredWidth = width;
            fieldLayout.preferredHeight = 30f;

            Image background = fieldObject.AddComponent<Image>();
            background.color = new Color(0.12f, 0.13f, 0.16f, 1f);

            GameObject textArea = new GameObject("Text Area", typeof(RectTransform));
            textArea.transform.SetParent(fieldObject.transform, false);
            RectTransform textAreaRect = textArea.GetComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(8f, 4f);
            textAreaRect.offsetMax = new Vector2(-8f, -4f);

            GameObject textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(textArea.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = 14f;
            text.color = Color.white;

            TMP_InputField inputField = fieldObject.AddComponent<TMP_InputField>();
            inputField.textViewport = textAreaRect;
            inputField.textComponent = text;
            return inputField;
        }

        static TextMeshProUGUI CreateFooterLabel(Transform parent, string name, string textValue, float width)
        {
            GameObject labelObject = new GameObject(name, typeof(RectTransform));
            labelObject.transform.SetParent(parent, false);
            LayoutElement labelLayout = labelObject.AddComponent<LayoutElement>();
            labelLayout.preferredWidth = width;

            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = textValue;
            label.fontSize = 13f;
            label.color = new Color(1f, 1f, 1f, 0.75f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            return label;
        }

        static Button CreateFooterButton(Transform parent, string name, string labelText)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform));
            buttonObject.transform.SetParent(parent, false);
            LayoutElement buttonLayout = buttonObject.AddComponent<LayoutElement>();
            buttonLayout.preferredWidth = 96f;
            buttonLayout.preferredHeight = 30f;

            Image background = buttonObject.AddComponent<Image>();
            background.color = new Color(0.2f, 0.22f, 0.28f, 1f);

            GameObject labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(buttonObject.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = labelText;
            label.fontSize = 14f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = background;
            return button;
        }
    }
}
#endif
