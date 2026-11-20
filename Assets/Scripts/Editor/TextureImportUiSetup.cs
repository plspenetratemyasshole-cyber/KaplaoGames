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
    public static class TextureImportUiSetup
    {
        const string GameScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Kaplao/Setup Texture Importer")]
        public static void SetupTextureImportUi()
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

            KaplaoTextureImportUi textureImportUi = canvas.GetComponent<KaplaoTextureImportUi>();
            if (textureImportUi == null)
                textureImportUi = canvas.gameObject.AddComponent<KaplaoTextureImportUi>();

            Button importTextureButton = FindOrCreateImportButton(createModeRoot, "ImportTextureButton", "Import Texture");
            GameObject panel = FindOrCreatePanel(createModeRoot, "TextureImportPanel");
            FindOrCreateLabel(panel.transform, "TextureNameLabel", "Name", new Vector2(-124f, 0f));
            TMP_InputField textureNameInput = FindOrCreateInputField(panel.transform, "TextureNameInput", "custom");
            Button selectFileButton = FindOrCreatePanelButton(panel.transform, "SelectTextureFileButton", "Select File", new Vector2(0f, -44f));
            Button addButton = FindOrCreatePanelButton(panel.transform, "TextureImportAddButton", "Add", new Vector2(-50f, -116f));
            Button closeButton = FindOrCreatePanelButton(panel.transform, "TextureImportCloseButton", "Close", new Vector2(50f, -116f));
            TextMeshProUGUI fileNameLabel = FindOrCreateLabel(panel.transform, "TextureFileNameLabel", string.Empty, new Vector2(0f, -80f));
            TextMeshProUGUI statusLabel = FindOrCreateLabel(panel.transform, "TextureImportStatusLabel", string.Empty, new Vector2(0f, -144f));

            KaplaoTextureImportUi.ApplyTextureImportPanelLayout(
                importTextureButton.transform,
                panel.transform,
                textureNameInput,
                selectFileButton,
                addButton,
                closeButton,
                fileNameLabel,
                statusLabel);

            SerializedObject serializedUi = new SerializedObject(textureImportUi);
            serializedUi.FindProperty("importTextureButton").objectReferenceValue = importTextureButton;
            serializedUi.FindProperty("panelRoot").objectReferenceValue = panel;
            serializedUi.FindProperty("textureNameInput").objectReferenceValue = textureNameInput;
            serializedUi.FindProperty("selectFileButton").objectReferenceValue = selectFileButton;
            serializedUi.FindProperty("addButton").objectReferenceValue = addButton;
            serializedUi.FindProperty("closeButton").objectReferenceValue = closeButton;
            serializedUi.FindProperty("fileNameLabel").objectReferenceValue = fileNameLabel;
            serializedUi.FindProperty("statusLabel").objectReferenceValue = statusLabel;
            serializedUi.FindProperty("kaplaoOnlyRoot").objectReferenceValue = createModeRoot.gameObject;
            serializedUi.ApplyModifiedPropertiesWithoutUndo();

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
            Debug.Log("[Kaplao] Texture Importer UI setup complete in SampleScene.");
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
            rect.sizeDelta = new Vector2(200f, 32f);

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
            placeholder.text = "texture name";
            placeholder.fontSize = 18f;
            placeholder.color = new Color(1f, 1f, 1f, 0.35f);
            StretchFull(placeholder.rectTransform);

            GameObject textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(textArea.transform, false);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = 18f;
            text.color = Color.white;
            StretchFull(text.rectTransform);

            TMP_InputField inputField = inputObject.AddComponent<TMP_InputField>();
            inputField.textViewport = textAreaRect;
            inputField.textComponent = text;
            inputField.placeholder = placeholder;
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
