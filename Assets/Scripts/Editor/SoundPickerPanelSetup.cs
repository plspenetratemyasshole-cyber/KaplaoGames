#if UNITY_EDITOR
using KaplaoGames.Client;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.Editor
{
    public static class SoundPickerPanelSetup
    {
        const string PanelName = "SoundPickerPanel";

        [MenuItem("Kaplao/Setup Sound Picker UI")]
        public static void SetupFromMenu()
        {
            if (!TryGetCanvasAndRoot(out Canvas canvas, out Transform createModeRoot))
                return;

            KaplaoSoundPickerUi soundUi = EnsureSoundUi(canvas);
            if (TryBindExistingPanel(soundUi, createModeRoot))
            {
                Debug.Log("[Kaplao] Bound existing SoundPickerPanel (layout preserved).");
                MarkSceneDirty();
                return;
            }

            BuildPanel(soundUi, createModeRoot);
            Debug.Log("[Kaplao] Sound picker panel created under CreateModeUI/SoundPickerPanel.");
            MarkSceneDirty();
        }

        [MenuItem("Kaplao/Bind Existing Sound Picker UI")]
        public static void BindExistingFromMenu()
        {
            if (!TryGetCanvasAndRoot(out Canvas canvas, out Transform createModeRoot))
                return;

            KaplaoSoundPickerUi soundUi = canvas.GetComponent<KaplaoSoundPickerUi>();
            if (soundUi == null)
            {
                Debug.LogWarning("[Kaplao] KaplaoSoundPickerUi not found on Canvas.");
                return;
            }

            if (!TryBindExistingPanel(soundUi, createModeRoot))
                Debug.LogWarning("[Kaplao] SoundPickerPanel not found. Use Kaplao/Setup Sound Picker UI.");
            else
                MarkSceneDirty();
        }

        public static bool TryBindExistingPanel(KaplaoSoundPickerUi soundUi, Transform parent)
        {
            Transform panel = parent.Find(PanelName);
            if (panel == null)
                return false;

            RectTransform listContent = panel.Find("ScrollRoot/Viewport/Content") as RectTransform;
            GridLayoutGroup listGrid = EnsureGridLayout(listContent);

            var references = new KaplaoSoundPickerUi.PanelReferences
            {
                PanelRoot = panel.GetComponent<RectTransform>(),
                PathLabel = panel.Find("Header/PathLabel")?.GetComponent<TextMeshProUGUI>(),
                SelectedPathLabel = panel.Find("Footer/SelectedPathLabel")?.GetComponent<TextMeshProUGUI>(),
                ListContent = listContent,
                ListGrid = listGrid,
                BackButton = panel.Find("Header/BackButton")?.GetComponent<Button>(),
                CloseButton = panel.Find("Footer/CloseButton")?.GetComponent<Button>(),
                CopyNameButton = panel.Find("Footer/CopyNameButton")?.GetComponent<Button>(),
                StatusLabel = panel.Find("Footer/StatusLabel")?.GetComponent<TextMeshProUGUI>()
            };

            soundUi.AssignPanelReferences(references);
            SerializedObject serializedUi = new SerializedObject(soundUi);
            serializedUi.FindProperty("kaplaoOnlyRoot").objectReferenceValue = parent.gameObject;
            serializedUi.FindProperty("panelRoot").objectReferenceValue = references.PanelRoot;
            serializedUi.FindProperty("pathLabel").objectReferenceValue = references.PathLabel;
            serializedUi.FindProperty("selectedPathLabel").objectReferenceValue = references.SelectedPathLabel;
            serializedUi.FindProperty("listContent").objectReferenceValue = references.ListContent;
            serializedUi.FindProperty("listGrid").objectReferenceValue = references.ListGrid;
            serializedUi.FindProperty("backButton").objectReferenceValue = references.BackButton;
            serializedUi.FindProperty("closeButton").objectReferenceValue = references.CloseButton;
            serializedUi.FindProperty("copyNameButton").objectReferenceValue = references.CopyNameButton;
            serializedUi.FindProperty("statusLabel").objectReferenceValue = references.StatusLabel;
            AssignExplorerSprites(serializedUi);
            serializedUi.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(soundUi);
            return true;
        }

        static void BuildPanel(KaplaoSoundPickerUi soundUi, Transform parent)
        {
            GameObject panelObject = new GameObject(PanelName, typeof(RectTransform));
            panelObject.transform.SetParent(parent, false);
            RectTransform panelRoot = panelObject.GetComponent<RectTransform>();
            panelRoot.anchorMin = new Vector2(0.5f, 0.5f);
            panelRoot.anchorMax = new Vector2(0.5f, 0.5f);
            panelRoot.pivot = new Vector2(0.5f, 0.5f);
            panelRoot.anchoredPosition = Vector2.zero;
            panelRoot.sizeDelta = new Vector2(640f, 520f);

            Image panelBackground = panelObject.AddComponent<Image>();
            panelBackground.color = new Color(0.08f, 0.09f, 0.12f, 0.98f);

            VerticalLayoutGroup rootLayout = panelObject.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(14, 14, 14, 14);
            rootLayout.spacing = 10f;
            rootLayout.childAlignment = TextAnchor.UpperCenter;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = false;
            rootLayout.childForceExpandWidth = true;

            TextMeshProUGUI pathLabel = CreateHeader(panelObject.transform, out Button backButton);
            RectTransform listContent = CreateScrollArea(panelObject.transform, out GridLayoutGroup listGrid);
            CreateFooter(panelObject.transform, out TextMeshProUGUI selectedPathLabel, out Button copyNameButton, out TextMeshProUGUI statusLabel, out Button closeButton);

            var references = new KaplaoSoundPickerUi.PanelReferences
            {
                PanelRoot = panelRoot,
                PathLabel = pathLabel,
                SelectedPathLabel = selectedPathLabel,
                ListContent = listContent,
                ListGrid = listGrid,
                BackButton = backButton,
                CloseButton = closeButton,
                CopyNameButton = copyNameButton,
                StatusLabel = statusLabel
            };

            soundUi.AssignPanelReferences(references);
            SerializedObject serializedUi = new SerializedObject(soundUi);
            serializedUi.FindProperty("kaplaoOnlyRoot").objectReferenceValue = parent.gameObject;
            serializedUi.FindProperty("panelRoot").objectReferenceValue = panelRoot;
            serializedUi.FindProperty("pathLabel").objectReferenceValue = pathLabel;
            serializedUi.FindProperty("selectedPathLabel").objectReferenceValue = selectedPathLabel;
            serializedUi.FindProperty("listContent").objectReferenceValue = listContent;
            serializedUi.FindProperty("listGrid").objectReferenceValue = listGrid;
            serializedUi.FindProperty("backButton").objectReferenceValue = backButton;
            serializedUi.FindProperty("closeButton").objectReferenceValue = closeButton;
            serializedUi.FindProperty("copyNameButton").objectReferenceValue = copyNameButton;
            serializedUi.FindProperty("statusLabel").objectReferenceValue = statusLabel;
            AssignExplorerSprites(serializedUi);
            serializedUi.ApplyModifiedPropertiesWithoutUndo();
            panelObject.SetActive(false);
            EditorUtility.SetDirty(soundUi);
            EditorUtility.SetDirty(panelObject);
        }

        static TextMeshProUGUI CreateHeader(Transform parent, out Button backButton)
        {
            GameObject header = new GameObject("Header", typeof(RectTransform));
            header.transform.SetParent(parent, false);
            LayoutElement headerLayout = header.AddComponent<LayoutElement>();
            headerLayout.preferredHeight = 34f;

            HorizontalLayoutGroup layout = header.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;

            backButton = CreateSmallButton(header.transform, "BackButton", "Back", 72f);
            GameObject pathObject = new GameObject("PathLabel", typeof(RectTransform));
            pathObject.transform.SetParent(header.transform, false);
            LayoutElement pathLayout = pathObject.AddComponent<LayoutElement>();
            pathLayout.flexibleWidth = 1f;
            TextMeshProUGUI pathLabel = pathObject.AddComponent<TextMeshProUGUI>();
            pathLabel.text = "View Sounds";
            pathLabel.fontSize = 16f;
            pathLabel.color = Color.white;
            return pathLabel;
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

        static GridLayoutGroup EnsureGridLayout(RectTransform listContent)
        {
            if (listContent == null)
                return null;

            GridLayoutGroup grid = listContent.GetComponent<GridLayoutGroup>();
            if (grid != null)
                return grid;

            VerticalLayoutGroup vertical = listContent.GetComponent<VerticalLayoutGroup>();
            if (vertical != null)
                Object.DestroyImmediate(vertical);

            grid = listContent.gameObject.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(4, 4, 4, 4);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            grid.cellSize = new Vector2(108f, 118f);
            grid.spacing = new Vector2(8f, 8f);

            if (listContent.GetComponent<ContentSizeFitter>() == null)
            {
                ContentSizeFitter fitter = listContent.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            return grid;
        }

        static void AssignExplorerSprites(SerializedObject serializedUi)
        {
            Sprite folder = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Explorer/folder.png");
            Sprite sound = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Explorer/ogg.png");
            serializedUi.FindProperty("folderIconSprite").objectReferenceValue = folder;
            serializedUi.FindProperty("soundIconSprite").objectReferenceValue = sound;
        }

        static void CreateFooter(Transform parent, out TextMeshProUGUI selectedPathLabel, out Button copyNameButton, out TextMeshProUGUI statusLabel, out Button closeButton)
        {
            GameObject footer = new GameObject("Footer", typeof(RectTransform));
            footer.transform.SetParent(parent, false);
            LayoutElement footerLayout = footer.AddComponent<LayoutElement>();
            footerLayout.preferredHeight = 72f;

            VerticalLayoutGroup layout = footer.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;

            GameObject selectedObject = new GameObject("SelectedPathLabel", typeof(RectTransform));
            selectedObject.transform.SetParent(footer.transform, false);
            selectedPathLabel = selectedObject.AddComponent<TextMeshProUGUI>();
            selectedPathLabel.text = "(none)";
            selectedPathLabel.fontSize = 13f;
            selectedPathLabel.color = new Color(1f, 1f, 1f, 0.85f);

            GameObject row = new GameObject("ButtonsRow", typeof(RectTransform));
            row.transform.SetParent(footer.transform, false);
            HorizontalLayoutGroup rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 8f;
            rowLayout.childAlignment = TextAnchor.MiddleCenter;

            copyNameButton = CreateSmallButton(row.transform, "CopyNameButton", "Copy name", 120f);
            closeButton = CreateSmallButton(row.transform, "CloseButton", "Close", 96f);
            statusLabel = CreateFooterStatus(footer.transform);
        }

        static TextMeshProUGUI CreateFooterStatus(Transform parent)
        {
            GameObject statusObject = new GameObject("StatusLabel", typeof(RectTransform));
            statusObject.transform.SetParent(parent, false);
            TextMeshProUGUI statusLabel = statusObject.AddComponent<TextMeshProUGUI>();
            statusLabel.text = "Put sounds in Assets/StreamingAssets/MinecraftSounds/";
            statusLabel.fontSize = 12f;
            statusLabel.color = new Color(1f, 1f, 1f, 0.65f);
            return statusLabel;
        }

        static Button CreateSmallButton(Transform parent, string name, string labelText, float width)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform));
            buttonObject.transform.SetParent(parent, false);
            LayoutElement buttonLayout = buttonObject.AddComponent<LayoutElement>();
            buttonLayout.preferredWidth = width;
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
                Debug.LogWarning("[Kaplao] CreateModeUI not found. Run Kaplao/Setup Create Mode UI first.");
                return false;
            }

            return true;
        }

        static KaplaoSoundPickerUi EnsureSoundUi(Canvas canvas)
        {
            KaplaoSoundPickerUi soundUi = canvas.GetComponent<KaplaoSoundPickerUi>();
            if (soundUi == null)
                soundUi = canvas.gameObject.AddComponent<KaplaoSoundPickerUi>();
            return soundUi;
        }

        static void MarkSceneDirty()
        {
            if (EditorSceneManager.GetActiveScene().isLoaded)
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
    }
}
#endif
