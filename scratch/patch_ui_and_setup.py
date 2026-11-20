# 1. KaplaoCodeModeUi.cs
with open('Assets/Scripts/Client/KaplaoCodeModeUi.cs', 'r', encoding='utf-8') as f:
    text = f.read()

target_ui_field = '[SerializeField] TextMeshProUGUI deleteConfirmMessage;'
replacement_ui_field = '[SerializeField] TextMeshProUGUI deleteConfirmMessage;\n        [SerializeField] KaplaoCodeOutputPanel outputPanel;'

if target_ui_field in text and 'KaplaoCodeOutputPanel outputPanel;' not in text:
    text = text.replace(target_ui_field, replacement_ui_field, 1)

target_awake = 'ResolveStatusLabel();'
replacement_awake = '''ResolveStatusLabel();
            if (outputPanel == null && overlayRoot != null)
                outputPanel = overlayRoot.GetComponentInChildren<KaplaoCodeOutputPanel>(true);'''

if target_awake in text and 'GetComponentInChildren<KaplaoCodeOutputPanel>' not in text:
    text = text.replace(target_awake, replacement_awake, 1)

target_layout = '''            if (codeInputField != null)
            {
                RectTransform codeRect = codeInputField.GetComponent<RectTransform>();
                codeRect.anchorMin = Vector2.zero;
                codeRect.anchorMax = Vector2.one;
                codeRect.offsetMin = new Vector2(FileTreeWidth + 8f, 72f);
                codeRect.offsetMax = new Vector2(-8f, -(TabBarHeight + 16f));
            }

            if (runButton != null)
            {
                RectTransform runRect = runButton.GetComponent<RectTransform>();
                runRect.anchorMin = new Vector2(1f, 0f);
                runRect.anchorMax = new Vector2(1f, 0f);
                runRect.pivot = new Vector2(1f, 0f);
                runRect.anchoredPosition = new Vector2(-24f, 24f);
                runRect.sizeDelta = new Vector2(120f, 40f);
            }

            if (documentationButton != null)
            {
                RectTransform docRect = documentationButton.GetComponent<RectTransform>();
                docRect.anchorMin = new Vector2(0f, 0f);
                docRect.anchorMax = new Vector2(0f, 0f);
                docRect.pivot = new Vector2(0f, 0f);
                docRect.anchoredPosition = new Vector2(FileTreeWidth + 24f, 24f);
                docRect.sizeDelta = new Vector2(160f, 40f);
            }

            if (statusLabel != null)
            {
                RectTransform statusRect = statusLabel.rectTransform;
                statusRect.anchorMin = new Vector2(0.5f, 0f);
                statusRect.anchorMax = new Vector2(0.5f, 0f);
                statusRect.pivot = new Vector2(0.5f, 0f);
                statusRect.anchoredPosition = new Vector2(FileTreeWidth * 0.5f, 24f);
                statusRect.sizeDelta = new Vector2(700f, 32f);
            }'''

replacement_layout = '''            Transform outputPanelTransform = overlayRoot.transform.Find("CodeOutputPanel");
            float outputHeight = 150f;
            if (outputPanelTransform != null)
            {
                RectTransform outRect = outputPanelTransform.GetComponent<RectTransform>();
                outRect.anchorMin = new Vector2(0f, 0f);
                outRect.anchorMax = new Vector2(1f, 0f);
                outRect.pivot = new Vector2(0.5f, 0f);
                outRect.anchoredPosition = Vector2.zero;
                outRect.offsetMin = new Vector2(FileTreeWidth + 8f, 52f);
                outRect.offsetMax = new Vector2(-8f, 52f + outputHeight);
            }

            if (codeInputField != null)
            {
                RectTransform codeRect = codeInputField.GetComponent<RectTransform>();
                codeRect.anchorMin = Vector2.zero;
                codeRect.anchorMax = Vector2.one;
                float bottomOffset = outputPanelTransform != null ? (52f + outputHeight + 6f) : 72f;
                codeRect.offsetMin = new Vector2(FileTreeWidth + 8f, bottomOffset);
                codeRect.offsetMax = new Vector2(-8f, -(TabBarHeight + 16f));
            }

            if (runButton != null)
            {
                RectTransform runRect = runButton.GetComponent<RectTransform>();
                runRect.anchorMin = new Vector2(1f, 0f);
                runRect.anchorMax = new Vector2(1f, 0f);
                runRect.pivot = new Vector2(1f, 0f);
                runRect.anchoredPosition = new Vector2(-24f, 12f);
                runRect.sizeDelta = new Vector2(120f, 32f);
            }

            if (documentationButton != null)
            {
                RectTransform docRect = documentationButton.GetComponent<RectTransform>();
                docRect.anchorMin = new Vector2(0f, 0f);
                docRect.anchorMax = new Vector2(0f, 0f);
                docRect.pivot = new Vector2(0f, 0f);
                docRect.anchoredPosition = new Vector2(FileTreeWidth + 16f, 12f);
                docRect.sizeDelta = new Vector2(140f, 32f);
            }

            if (statusLabel != null)
            {
                RectTransform statusRect = statusLabel.rectTransform;
                statusRect.anchorMin = new Vector2(0.5f, 0f);
                statusRect.anchorMax = new Vector2(0.5f, 0f);
                statusRect.pivot = new Vector2(0.5f, 0f);
                statusRect.anchoredPosition = new Vector2(FileTreeWidth * 0.5f, 14f);
                statusRect.sizeDelta = new Vector2(500f, 28f);
            }'''

assert target_layout in text, "target_layout not found in KaplaoCodeModeUi.cs"
text = text.replace(target_layout, replacement_layout, 1)

target_methods = '''        public void NotifyScriptRuntimeError(string message)
        {
            SetRunningState(false);
            if (string.IsNullOrEmpty(message))
                return;

            SetStatus(message);'''

replacement_methods = '''        public void AppendOutput(string message, OutputLogType type)
        {
            if (outputPanel == null && overlayRoot != null)
                outputPanel = overlayRoot.GetComponentInChildren<KaplaoCodeOutputPanel>(true);

            if (outputPanel != null)
                outputPanel.AppendEntry(message, type);
            else
                LevelScriptOutputService.Log(message, type);
        }

        public void NotifyScriptWarning(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            AppendOutput(message, OutputLogType.Warning);
        }

        public void OnOutputPanelToggled(bool collapsed, float height)
        {
            if (codeInputField == null) return;
            RectTransform codeRect = codeInputField.GetComponent<RectTransform>();
            float bottomOffset = 52f + height + 6f;
            codeRect.offsetMin = new Vector2(FileTreeWidth + 8f, bottomOffset);
        }

        public void NotifyScriptRuntimeError(string message)
        {
            SetRunningState(false);
            if (string.IsNullOrEmpty(message))
                return;

            SetStatus(message);
            AppendOutput(message, OutputLogType.Error);'''

assert target_methods in text, "target_methods not found in KaplaoCodeModeUi.cs"
text = text.replace(target_methods, replacement_methods, 1)

# In ToggleRunStop, add AppendOutput on validation error
target_validate_err = '''            LevelScriptIdeService.ActiveFilePath = WorldScriptProjectService.EntryFileName;
            if (!LevelScriptIdeService.TryValidate(script ?? string.Empty, out LevelScriptDiagnostic diagnostic))
            {
                SetStatus(diagnostic.Message);'''

replacement_validate_err = '''            LevelScriptIdeService.ActiveFilePath = WorldScriptProjectService.EntryFileName;
            if (!LevelScriptIdeService.TryValidate(script ?? string.Empty, out LevelScriptDiagnostic diagnostic))
            {
                SetStatus(diagnostic.Message);
                AppendOutput(diagnostic.Message, OutputLogType.Error);'''

assert target_validate_err in text, "target_validate_err not found in KaplaoCodeModeUi.cs"
text = text.replace(target_validate_err, replacement_validate_err, 1)

with open('Assets/Scripts/Client/KaplaoCodeModeUi.cs', 'w', encoding='utf-8') as f:
    f.write(text)
print("Updated KaplaoCodeModeUi.cs")

# 2. CodeModeIdeSetup.cs
with open('Assets/Scripts/Editor/CodeModeIdeSetup.cs', 'r', encoding='utf-8') as f:
    text = f.read()

target_editor_setup = '''            // --- Editor ---
            TMP_InputField codeInput = FindOrCreateMultilineInputField(overlay.transform, "CodeInputField");
            if (codeInput.GetComponent<LevelScriptCodeEditorEnhancer>() == null)
                codeInput.gameObject.AddComponent<LevelScriptCodeEditorEnhancer>();

            Button runButton = FindOrCreateButton(overlay.transform, "RunScriptButton", "Run",'''

replacement_editor_setup = '''            // --- Editor ---
            TMP_InputField codeInput = FindOrCreateMultilineInputField(overlay.transform, "CodeInputField");
            if (codeInput.GetComponent<LevelScriptCodeEditorEnhancer>() == null)
                codeInput.gameObject.AddComponent<LevelScriptCodeEditorEnhancer>();

            // --- Output Panel ---
            KaplaoCodeOutputPanel outputPanel = FindOrCreateOutputPanel(overlay.transform);

            Button runButton = FindOrCreateButton(overlay.transform, "RunScriptButton", "Run",'''

assert target_editor_setup in text, "target_editor_setup not found in CodeModeIdeSetup.cs"
text = text.replace(target_editor_setup, replacement_editor_setup, 1)

target_so_apply = '''            so.FindProperty("deleteConfirmMessage").objectReferenceValue = deleteMessage;
            so.ApplyModifiedPropertiesWithoutUndo();'''

replacement_so_apply = '''            so.FindProperty("deleteConfirmMessage").objectReferenceValue = deleteMessage;
            so.FindProperty("outputPanel").objectReferenceValue = outputPanel;
            so.ApplyModifiedPropertiesWithoutUndo();'''

assert target_so_apply in text, "target_so_apply not found in CodeModeIdeSetup.cs"
text = text.replace(target_so_apply, replacement_so_apply, 1)

target_eof = '''        static TMP_InputField FindOrCreateSingleLineInput(Transform parent, string name)
        {'''

replacement_eof = '''        static KaplaoCodeOutputPanel FindOrCreateOutputPanel(Transform parent)
        {
            Transform existing = parent.Find("CodeOutputPanel");
            GameObject panelObject = existing != null
                ? existing.gameObject
                : new GameObject("CodeOutputPanel", typeof(RectTransform), typeof(Image));

            if (existing == null)
                panelObject.transform.SetParent(parent, false);

            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(1f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 52f);
            panelRect.sizeDelta = new Vector2(0f, 150f);

            Image panelBg = panelObject.GetComponent<Image>() ?? panelObject.AddComponent<Image>();
            panelBg.color = new Color(0.08f, 0.09f, 0.12f, 0.98f);
            panelBg.raycastTarget = true;

            // Header bar
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

            // Toggle button
            Button toggleButton = FindOrCreateButton(header.transform, "ToggleButton", "▼",
                new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(24f, 20f));
            toggleButton.GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f);
            TextMeshProUGUI toggleLabel = toggleButton.GetComponentInChildren<TextMeshProUGUI>();
            if (toggleLabel != null) toggleLabel.fontSize = 11f;

            // Copy button
            Button copyButton = FindOrCreateButton(header.transform, "CopyButton", "Copy",
                new Vector2(1f, 0.5f), new Vector2(-46f, 0f), new Vector2(56f, 20f));
            copyButton.GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f);
            TextMeshProUGUI copyLabel = copyButton.GetComponentInChildren<TextMeshProUGUI>();
            if (copyLabel != null) copyLabel.fontSize = 12f;

            // Clear button
            Button clearButton = FindOrCreateButton(header.transform, "ClearButton", "Clear",
                new Vector2(1f, 0.5f), new Vector2(-108f, 0f), new Vector2(56f, 20f));
            clearButton.GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f);
            TextMeshProUGUI clearLabel = clearButton.GetComponentInChildren<TextMeshProUGUI>();
            if (clearLabel != null) clearLabel.fontSize = 12f;

            // Content Area
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

            return panel;
        }

        static TMP_InputField FindOrCreateSingleLineInput(Transform parent, string name)
        {'''

assert target_eof in text, "target_eof not found in CodeModeIdeSetup.cs"
text = text.replace(target_eof, replacement_eof, 1)

with open('Assets/Scripts/Editor/CodeModeIdeSetup.cs', 'w', encoding='utf-8') as f:
    f.write(text)
print("Updated CodeModeIdeSetup.cs")
