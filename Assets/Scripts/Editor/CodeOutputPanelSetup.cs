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
    /// Standalone Setup tool exclusively for the LevelScript Output Panel (Console).
    /// Creates or updates CodeOutputPanel inside CodeModeOverlay and connects it to KaplaoCodeModeUi.
    /// </summary>
    public static class CodeOutputPanelSetup
    {
        const string GameScenePath = "Assets/Scenes/SampleScene.unity";
        const float FileTreeWidth = 260f;
        const float BottomOffset = 52f;
        const float DefaultHeight = 150f;

        [MenuItem("Kaplao/Setup Code Output Panel")]
        public static void SetupCodeOutputPanel()
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[Kaplao] Canvas not found in scene.");
                return;
            }

            Transform overlayTransform = canvas.transform.Find("CodeModeOverlay");
            if (overlayTransform == null)
            {
                Debug.LogError("[Kaplao] CodeModeOverlay not found on Canvas. Please run 'Kaplao/Setup Code Mode IDE' first.");
                return;
            }

            KaplaoCodeModeUi codeUi = canvas.GetComponent<KaplaoCodeModeUi>();
            if (codeUi == null)
                codeUi = canvas.gameObject.AddComponent<KaplaoCodeModeUi>();

            // --- Find or Create CodeOutputPanel ---
            Transform existing = overlayTransform.Find("CodeOutputPanel");
            GameObject panelObject = existing != null
                ? existing.gameObject
                : new GameObject("CodeOutputPanel", typeof(RectTransform), typeof(Image));

            if (existing == null)
                panelObject.transform.SetParent(overlayTransform, false);

            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(1f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.offsetMin = new Vector2(FileTreeWidth + 8f, BottomOffset);
            panelRect.offsetMax = new Vector2(-8f, BottomOffset + DefaultHeight);

            Image panelBg = panelObject.GetComponent<Image>() ?? panelObject.AddComponent<Image>();
            panelBg.color = new Color(0.08f, 0.09f, 0.12f, 0.98f);
            panelBg.raycastTarget = true;

            // --- Header Bar ---
            GameObject header = FindOrCreateChild(panelObject.transform, "Header");
            Image headerBg = header.GetComponent<Image>() ?? header.AddComponent<Image>();
            headerBg.color = new Color(0.12f, 0.13f, 0.17f, 1f);
            RectTransform headerRect = header.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = Vector2.zero;
            headerRect.sizeDelta = new Vector2(0f, 26f);

            // Title
            TextMeshProUGUI title = FindOrCreateLabel(header.transform, "Title", "OUTPUT",
                new Vector2(10f, 0f), new Vector2(160f, 22f), 13f);
            title.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            title.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            title.rectTransform.pivot = new Vector2(0f, 0.5f);
            title.rectTransform.anchoredPosition = new Vector2(10f, 0f);
            title.fontStyle = FontStyles.Bold;
            title.color = new Color(0.65f, 0.72f, 0.82f, 1f);

            // Toggle Button
            Button toggleButton = FindOrCreateButton(header.transform, "ToggleButton", "▼",
                new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(24f, 20f));
            toggleButton.GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f);
            TextMeshProUGUI toggleLabel = toggleButton.GetComponentInChildren<TextMeshProUGUI>();
            if (toggleLabel != null) toggleLabel.fontSize = 11f;

            // Copy Button
            Button copyButton = FindOrCreateButton(header.transform, "CopyButton", "Copy",
                new Vector2(1f, 0.5f), new Vector2(-46f, 0f), new Vector2(56f, 20f));
            copyButton.GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f);
            TextMeshProUGUI copyLabel = copyButton.GetComponentInChildren<TextMeshProUGUI>();
            if (copyLabel != null) copyLabel.fontSize = 12f;

            // Clear Button
            Button clearButton = FindOrCreateButton(header.transform, "ClearButton", "Clear",
                new Vector2(1f, 0.5f), new Vector2(-108f, 0f), new Vector2(56f, 20f));
            clearButton.GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f);
            TextMeshProUGUI clearLabel = clearButton.GetComponentInChildren<TextMeshProUGUI>();
            if (clearLabel != null) clearLabel.fontSize = 12f;

            // --- Content Area ---
            GameObject contentArea = FindOrCreateChild(panelObject.transform, "ContentArea");
            RectTransform contentRect = contentArea.GetComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = new Vector2(4f, 4f);
            contentRect.offsetMax = new Vector2(-4f, -28f);

            TMP_InputField outputField = FindOrCreateMultilineInputField(contentArea.transform, "OutputInputField");
            RectTransform outputFieldRect = outputField.GetComponent<RectTransform>();
            StretchFull(outputFieldRect);
            outputField.readOnly = true;
            outputField.richText = true;
            outputField.customCaretColor = true;
            outputField.caretColor = Color.clear;
            outputField.selectionColor = new Color(0.25f, 0.5f, 0.95f, 0.4f);
            if (outputField.textComponent != null)
            {
                outputField.textComponent.fontSize = 13.5f;
                outputField.textComponent.color = Color.white;
                outputField.textComponent.textWrappingMode = TextWrappingModes.Normal;
                outputField.textComponent.alignment = TextAlignmentOptions.TopLeft;
            }
            Image outputBg = outputField.GetComponent<Image>();
            if (outputBg != null)
                outputBg.color = new Color(0.06f, 0.07f, 0.09f, 0.95f);

            // --- Component Configuration ---
            KaplaoCodeOutputPanel panel =
                panelObject.GetComponent<KaplaoCodeOutputPanel>() ??
                panelObject.AddComponent<KaplaoCodeOutputPanel>();

            SerializedObject panelSo = new SerializedObject(panel);
            panelSo.FindProperty("panelRoot").objectReferenceValue = panelRect;
            panelSo.FindProperty("outputField").objectReferenceValue = outputField;
            panelSo.FindProperty("titleLabel").objectReferenceValue = title;
            panelSo.FindProperty("copyButton").objectReferenceValue = copyButton;
            panelSo.FindProperty("clearButton").objectReferenceValue = clearButton;
            panelSo.FindProperty("toggleButton").objectReferenceValue = toggleButton;
            panelSo.FindProperty("copyButtonLabel").objectReferenceValue = copyLabel;
            panelSo.ApplyModifiedPropertiesWithoutUndo();

            // --- Connect to KaplaoCodeModeUi ---
            SerializedObject codeUiSo = new SerializedObject(codeUi);
            codeUiSo.FindProperty("outputPanel").objectReferenceValue = panel;
            codeUiSo.ApplyModifiedPropertiesWithoutUndo();

            // --- Adjust Code Editor Bottom Offset so it does not overlap ---
            Transform codeInputTransform = overlayTransform.Find("CodeInputField");
            if (codeInputTransform != null)
            {
                RectTransform codeRect = codeInputTransform.GetComponent<RectTransform>();
                float bottomOffset = BottomOffset + DefaultHeight + 6f;
                codeRect.offsetMin = new Vector2(codeRect.offsetMin.x, bottomOffset);
            }

            EditorUtility.SetDirty(panelObject);
            EditorUtility.SetDirty(codeUi);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[Kaplao] Code Output Panel setup complete successfully.\n" +
                      "Output panel is ready to display errors in Red, warnings in Yellow, and print() in White with timestamps.");
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
            image.color = new Color(0.15f, 0.16f, 0.2f, 0.95f);

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            GameObject textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(buttonObject.transform, false);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 12f;
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
            image.color = new Color(0.1f, 0.11f, 0.14f, 1f);

            GameObject textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(inputObject.transform, false);
            RectTransform textAreaRect = textArea.GetComponent<RectTransform>();
            StretchFull(textAreaRect);
            textAreaRect.offsetMin = new Vector2(8f, 6f);
            textAreaRect.offsetMax = new Vector2(-8f, -6f);

            GameObject textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(textArea.transform, false);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = 13.5f;
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
    }
}
#endif
