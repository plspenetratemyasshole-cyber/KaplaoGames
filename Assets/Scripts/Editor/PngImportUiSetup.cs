#if UNITY_EDITOR
using KaplaoGames.Client;
using KaplaoGames.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KaplaoGames.Editor
{
    public static class PngImportUiSetup
    {
        const string GameScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Kaplao/Setup PNG Import UI")]
        public static void SetupPngImportUi()
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[Kaplao] Canvas not found in SampleScene.");
                return;
            }

            Transform createModeRoot = canvas.transform.Find("CreateModeUI");
            if (createModeRoot == null)
            {
                Debug.LogWarning("[Kaplao] CreateModeUI not found. Run Kaplao/Setup Create Mode UI first.");
                return;
            }

            if (canvas.GetComponent<KaplaoGameUiCanvas>() == null)
                canvas.gameObject.AddComponent<KaplaoGameUiCanvas>();

            KaplaoPngImportUi pngImportUi = canvas.GetComponent<KaplaoPngImportUi>();
            if (pngImportUi == null)
                pngImportUi = canvas.gameObject.AddComponent<KaplaoPngImportUi>();

            Button importPngButton = FindOrCreateImportButton(createModeRoot, "ImportPngButton", "Import PNG");
            GameObject panel = FindOrCreatePanel(createModeRoot, "PngImportPanel");
            TMP_InputField tilesXInput = FindOrCreateInputField(panel.transform, "TilesXInput", "10");
            TMP_InputField tilesYInput = FindOrCreateInputField(panel.transform, "TilesYInput", "10");
            if (tilesXInput != null)
                tilesXInput.text = "10";
            if (tilesYInput != null)
                tilesYInput.text = "10";
            FindOrCreateLabel(panel.transform, "TilesXLabel", "Tiles X", new Vector2(-124f, 0f));
            FindOrCreateLabel(panel.transform, "TilesYLabel", "Tiles Y", new Vector2(-124f, -36f));
            Button selectFileButton = FindOrCreatePanelButton(panel.transform, "SelectPngFileButton", "Select File", new Vector2(0f, -76f));
            Button addButton = FindOrCreatePanelButton(panel.transform, "PngImportAddButton", "Add", new Vector2(-50f, -148f));
            Button closeButton = FindOrCreatePanelButton(panel.transform, "PngImportCloseButton", "Close", new Vector2(50f, -148f));
            TextMeshProUGUI fileNameLabel = FindOrCreateLabel(panel.transform, "PngFileNameLabel", string.Empty, new Vector2(0f, -112f));
            TextMeshProUGUI statusLabel = FindOrCreateLabel(panel.transform, "PngImportStatusLabel", string.Empty, new Vector2(0f, -176f));

            KaplaoPngImportUi.ApplyPngImportPanelLayout(
                importPngButton.transform,
                panel.transform,
                tilesXInput,
                tilesYInput,
                selectFileButton,
                addButton,
                closeButton,
                fileNameLabel,
                statusLabel);

            SerializedObject serializedPngUi = new SerializedObject(pngImportUi);
            serializedPngUi.FindProperty("importPngButton").objectReferenceValue = importPngButton;
            serializedPngUi.FindProperty("panelRoot").objectReferenceValue = panel;
            serializedPngUi.FindProperty("tilesXInput").objectReferenceValue = tilesXInput;
            serializedPngUi.FindProperty("tilesYInput").objectReferenceValue = tilesYInput;
            serializedPngUi.FindProperty("selectFileButton").objectReferenceValue = selectFileButton;
            serializedPngUi.FindProperty("addButton").objectReferenceValue = addButton;
            serializedPngUi.FindProperty("closeButton").objectReferenceValue = closeButton;
            serializedPngUi.FindProperty("fileNameLabel").objectReferenceValue = fileNameLabel;
            serializedPngUi.FindProperty("statusLabel").objectReferenceValue = statusLabel;
            serializedPngUi.FindProperty("kaplaoOnlyRoot").objectReferenceValue = createModeRoot.gameObject;
            serializedPngUi.ApplyModifiedPropertiesWithoutUndo();

            GameSessionUi sessionUi = canvas.GetComponent<GameSessionUi>();
            if (sessionUi != null)
            {
                SerializedObject serializedSession = new SerializedObject(sessionUi);
                SerializedProperty roots = serializedSession.FindProperty("kaplaoOnlyRoots");
                AddKaplaoOnlyRoot(roots, createModeRoot.gameObject);
                serializedSession.ApplyModifiedPropertiesWithoutUndo();
            }

            panel.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Kaplao] PNG Import UI setup complete in SampleScene.");
        }

        static Button FindOrCreateImportButton(Transform parent, string name, string label)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return existing.GetComponent<Button>();

            return CreateButton(parent, name, label, new Vector2(220f, 36f));
        }

        static GameObject FindOrCreatePanel(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return existing.gameObject;

            GameObject panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(parent, false);
            Image image = panelObject.GetComponent<Image>();
            image.color = new Color(0.1f, 0.1f, 0.1f, 0.92f);
            return panelObject;
        }

        static Button FindOrCreatePanelButton(Transform parent, string name, string label, Vector2 position)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return existing.GetComponent<Button>();

            Button button = CreateButton(parent, name, label, new Vector2(180f, 36f));
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            return button;
        }

        static Button CreateButton(Transform parent, string name, string label, Vector2 size)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = size;

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            TextMeshProUGUI text = CreateLabel(buttonObject.transform, "Text", label, Vector2.zero, size, 18f);
            text.alignment = TextAlignmentOptions.Center;
            StretchFull(text.rectTransform);
            return button;
        }

        static TMP_InputField FindOrCreateInputField(Transform parent, string name, string defaultText)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                TMP_InputField field = existing.GetComponent<TMP_InputField>();
                if (field != null && string.IsNullOrEmpty(field.text))
                    field.text = defaultText;
                return field;
            }

            GameObject inputObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            inputObject.transform.SetParent(parent, false);

            RectTransform rect = inputObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(96f, 32f);

            Image image = inputObject.GetComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            GameObject textArea = new GameObject("Text Area", typeof(RectTransform));
            textArea.transform.SetParent(inputObject.transform, false);
            RectTransform textAreaRect = textArea.GetComponent<RectTransform>();
            StretchFull(textAreaRect);
            textAreaRect.offsetMin = new Vector2(8f, 4f);
            textAreaRect.offsetMax = new Vector2(-8f, -4f);

            GameObject textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(textArea.transform, false);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = 18f;
            text.color = Color.white;
            StretchFull(text.rectTransform);

            TMP_InputField inputField = inputObject.AddComponent<TMP_InputField>();
            inputField.textViewport = textAreaRect;
            inputField.textComponent = text;
            inputField.contentType = TMP_InputField.ContentType.IntegerNumber;
            inputField.targetGraphic = image;
            inputField.text = defaultText;
            return inputField;
        }

        static TextMeshProUGUI FindOrCreateLabel(Transform parent, string name, string text, Vector2 position)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return existing.GetComponent<TextMeshProUGUI>();

            TextMeshProUGUI label = CreateLabel(parent, name, text, position, new Vector2(72f, 22f), 14f);
            label.alignment = TextAlignmentOptions.MidlineRight;
            return label;
        }

        static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, Vector2 position, Vector2 size, float fontSize)
        {
            GameObject labelObject = new GameObject(name, typeof(RectTransform));
            labelObject.transform.SetParent(parent, false);

            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void AddKaplaoOnlyRoot(SerializedProperty roots, GameObject root)
        {
            if (root == null)
                return;

            for (int i = 0; i < roots.arraySize; i++)
            {
                if (roots.GetArrayElementAtIndex(i).objectReferenceValue == root)
                    return;
            }

            roots.InsertArrayElementAtIndex(roots.arraySize);
            roots.GetArrayElementAtIndex(roots.arraySize - 1).objectReferenceValue = root;
        }
    }
}
#endif
