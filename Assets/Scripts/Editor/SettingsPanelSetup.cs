#if UNITY_EDITOR
using System.Collections.Generic;
using KaplaoGames.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KaplaoGames.Editor
{
    public static class SettingsPanelSetup
    {
        const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        const string PanelName = "SettingsPanel";

        [MenuItem("Kaplao/Setup Settings Panel")]
        public static void SetupSettingsPanel()
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[Kaplao] Canvas not found in MainMenu.");
                return;
            }

            Transform root = ResolveMenuRoot(canvas.transform);
            if (root == null)
            {
                Debug.LogError("[Kaplao] Menu root not found (expected MainPanel parent).");
                return;
            }

            GameObject panel = EnsureSettingsPanel(root);
            BindToMainMenuUi(canvas, panel);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Kaplao] SettingsPanel created/updated (incl. skin toggles). Save done.");
        }

        static Transform ResolveMenuRoot(Transform canvas)
        {
            Transform mainPanel = canvas.Find("MainPanel");
            if (mainPanel != null && mainPanel.parent != null)
                return mainPanel.parent;

            for (int i = 0; i < canvas.childCount; i++)
            {
                Transform child = canvas.GetChild(i);
                if (child.Find("MainPanel") != null)
                    return child;
            }

            return canvas;
        }

        static GameObject EnsureSettingsPanel(Transform parent)
        {
            Transform existing = parent.Find(PanelName);
            GameObject panelObject;
            if (existing != null)
            {
                panelObject = existing.gameObject;
            }
            else
            {
                panelObject = new GameObject(PanelName, typeof(RectTransform));
                panelObject.transform.SetParent(parent, false);
                StretchFull(panelObject.GetComponent<RectTransform>());
            }

            panelObject.SetActive(false);

            EnsureLabel(panelObject.transform, "Title", "Settings", new Vector2(0f, 210f), 36f);
            EnsureLabel(panelObject.transform, "PortLabel", "Port", new Vector2(-160f, 150f), 22f);
            EnsureLabel(panelObject.transform, "NetworkLabel", "Network", new Vector2(-160f, 95f), 22f);
            EnsureLabel(panelObject.transform, "TransportLabel", "Transport", new Vector2(-160f, 40f), 22f);

            EnsurePortInput(panelObject.transform, "PortInput", new Vector2(40f, 150f));
            TransportModeUi.EnsureDropdown(
                panelObject.transform,
                new Vector2(40f, 95f),
                new Vector2(320f, 40f),
                "NetworkDropdown");
            TransportModeUi.EnsureDropdown(
                panelObject.transform,
                new Vector2(40f, 40f),
                new Vector2(320f, 40f),
                TransportModeUi.DropdownObjectName);

            EnsureSkinSection(panelObject.transform);

            // Account controls (created by Setup Account Auth UI) — shift down for skin row.
            SetAnchoredIfExists(panelObject.transform, "NicknameLabel", new Vector2(-160f, -95f));
            SetAnchoredIfExists(panelObject.transform, "NicknameInput", new Vector2(40f, -95f));
            SetAnchoredIfExists(panelObject.transform, "AccountEmailLabel", new Vector2(0f, -140f));
            SetAnchoredIfExists(panelObject.transform, "SignInButton", new Vector2(-120f, -190f));
            SetAnchoredIfExists(panelObject.transform, "SignOutButton", new Vector2(120f, -190f));

            EnsureButton(panelObject.transform, "BackButton", "Back", new Vector2(0f, -250f));

            return panelObject;
        }

        static void EnsureSkinSection(Transform panel)
        {
            EnsureLabel(panel, "SkinLabel", "Skin", new Vector2(-160f, -20f), 22f);

            Transform rowTransform = panel.Find("SkinRow");
            GameObject row;
            if (rowTransform != null)
            {
                row = rowTransform.gameObject;
            }
            else
            {
                row = new GameObject("SkinRow", typeof(RectTransform), typeof(ToggleGroup), typeof(HorizontalLayoutGroup));
                row.transform.SetParent(panel, false);
            }

            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.5f, 0.5f);
            rowRect.anchorMax = new Vector2(0.5f, 0.5f);
            rowRect.pivot = new Vector2(0.5f, 0.5f);
            rowRect.anchoredPosition = new Vector2(40f, -20f);

            ToggleGroup group = row.GetComponent<ToggleGroup>();
            if (group == null)
                group = row.AddComponent<ToggleGroup>();
            group.allowSwitchOff = false;

            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
                layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            IReadOnlyList<string> publicFolders = RunnerSpriteEditorUtil.GetPublicCharacterFolders();
            IReadOnlyList<string> adminFolders = RunnerSpriteEditorUtil.GetAdminCharacterFolders();
            var folders = new List<string>();
            folders.AddRange(publicFolders);
            folders.AddRange(adminFolders);
            Sprite[] previews = LoadIdlePreviewSprites(folders);
            int skinCharacterCount = folders.Count;
            int publicCharacterCount = publicFolders.Count;
            int toggleCount = skinCharacterCount + 1; // + Random
            rowRect.sizeDelta = new Vector2(toggleCount * 72f + Mathf.Max(0, toggleCount - 1) * layout.spacing, 72f);

            EnsureSkinToggle(row.transform, group, "SkinRandomToggle", null, "Rnd", isRandom: true);
            for (int i = 0; i < skinCharacterCount; i++)
            {
                Sprite preview = i < previews.Length ? previews[i] : null;
                string folderName = System.IO.Path.GetFileName(folders[i]);
                EnsureSkinToggle(row.transform, group, $"SkinToggle{i}", preview, folderName, isRandom: false);
            }

            HideUnusedSkinToggles(row.transform, skinCharacterCount);
            HideAdminSkinTogglesByDefault(row.transform, publicCharacterCount, skinCharacterCount);
        }

        static void HideAdminSkinTogglesByDefault(Transform row, int publicCharacterCount, int totalCharacterCount)
        {
            for (int i = publicCharacterCount; i < totalCharacterCount; i++)
            {
                Transform toggle = row.Find($"SkinToggle{i}");
                if (toggle != null)
                    toggle.gameObject.SetActive(false);
            }
        }

        static Sprite[] LoadIdlePreviewSprites(IReadOnlyList<string> folders)
        {
            var result = new Sprite[folders.Count];
            for (int i = 0; i < folders.Count; i++)
            {
                string texturePath = RunnerSpriteEditorUtil.ResolveTexturePath(
                    new[] { "runner_idle", "Idle", "idle" },
                    folders[i]);
                result[i] = LoadFirstIdleSprite(texturePath);
            }

            return result;
        }

        static void HideUnusedSkinToggles(Transform row, int usedCharacterCount)
        {
            for (int i = usedCharacterCount; ; i++)
            {
                Transform toggle = row.Find($"SkinToggle{i}");
                if (toggle == null)
                    break;

                toggle.gameObject.SetActive(false);
            }
        }

        static Sprite LoadFirstIdleSprite(string texturePath)
        {
            if (string.IsNullOrEmpty(texturePath))
                return null;

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(texturePath);
            Sprite best = null;
            string bestName = null;
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is not Sprite sprite)
                    continue;

                string name = sprite.name;
                if (best == null ||
                    string.CompareOrdinal(name, bestName) < 0)
                {
                    best = sprite;
                    bestName = name;
                }
            }

            return best;
        }

        static void EnsureSkinToggle(
            Transform parent,
            ToggleGroup group,
            string name,
            Sprite preview,
            string fallbackLabel,
            bool isRandom)
        {
            Transform existing = parent.Find(name);
            GameObject toggleObject;
            if (existing != null)
            {
                toggleObject = existing.gameObject;
            }
            else
            {
                toggleObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Toggle), typeof(LayoutElement));
                toggleObject.transform.SetParent(parent, false);
            }

            RectTransform rect = toggleObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(72f, 72f);

            LayoutElement layoutElement = toggleObject.GetComponent<LayoutElement>();
            if (layoutElement == null)
                layoutElement = toggleObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 72f;
            layoutElement.preferredHeight = 72f;
            layoutElement.minWidth = 72f;
            layoutElement.minHeight = 72f;

            Image background = toggleObject.GetComponent<Image>();
            background.color = new Color(0.18f, 0.18f, 0.18f, 0.95f);

            Toggle toggle = toggleObject.GetComponent<Toggle>();
            toggle.group = group;
            toggle.targetGraphic = background;

            // Selection tint behind the preview so it never covers the skin art.
            Transform checkTransform = toggleObject.transform.Find("Checkmark");
            Image checkImage;
            if (checkTransform == null)
            {
                GameObject checkObject = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
                checkObject.transform.SetParent(toggleObject.transform, false);
                StretchFull(checkObject.GetComponent<RectTransform>());
                checkTransform = checkObject.transform;
            }

            checkTransform.SetSiblingIndex(0);
            checkImage = checkTransform.GetComponent<Image>();
            checkImage.sprite = null;
            checkImage.color = new Color(0.25f, 0.7f, 1f, 0.55f);
            checkImage.raycastTarget = false;
            toggle.graphic = checkImage;

            // Always-visible preview / label drawn above Checkmark.
            Transform previewTransform = toggleObject.transform.Find("Preview");
            Image previewImage;
            if (previewTransform == null)
            {
                GameObject previewObject = new GameObject("Preview", typeof(RectTransform), typeof(Image));
                previewObject.transform.SetParent(toggleObject.transform, false);
                StretchFull(previewObject.GetComponent<RectTransform>());
                previewObject.GetComponent<RectTransform>().offsetMin = new Vector2(8f, 8f);
                previewObject.GetComponent<RectTransform>().offsetMax = new Vector2(-8f, -8f);
                previewTransform = previewObject.transform;
            }

            previewTransform.SetAsLastSibling();
            previewImage = previewTransform.GetComponent<Image>();
            previewImage.raycastTarget = false;
            if (isRandom || preview == null)
            {
                previewImage.sprite = null;
                previewImage.color = new Color(0.28f, 0.28f, 0.32f, 1f);
            }
            else
            {
                previewImage.sprite = preview;
                previewImage.preserveAspect = true;
                previewImage.color = Color.white;
            }

            Transform labelTransform = toggleObject.transform.Find("Label");
            TextMeshProUGUI label;
            if (labelTransform != null)
            {
                label = labelTransform.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                GameObject labelObject = new GameObject("Label", typeof(RectTransform));
                labelObject.transform.SetParent(toggleObject.transform, false);
                StretchFull(labelObject.GetComponent<RectTransform>());
                label = labelObject.AddComponent<TextMeshProUGUI>();
            }

            labelTransform = label.transform;
            labelTransform.SetAsLastSibling();

            if (isRandom || preview == null)
            {
                label.text = fallbackLabel;
                label.fontSize = 18f;
                label.alignment = TextAlignmentOptions.Center;
                label.color = Color.white;
                label.raycastTarget = false;
                label.gameObject.SetActive(true);
            }
            else
            {
                label.text = string.Empty;
                label.gameObject.SetActive(false);
            }

            ColorBlock colors = toggle.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.95f, 0.95f, 1f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.9f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            toggle.colors = colors;
        }

        static void BindToMainMenuUi(Canvas canvas, GameObject panel)
        {
            MainMenuUI menuUi = canvas.GetComponent<MainMenuUI>();
            if (menuUi == null)
                menuUi = Object.FindFirstObjectByType<MainMenuUI>();

            if (menuUi == null)
            {
                Debug.LogWarning("[Kaplao] MainMenuUI not found — SettingsPanel created but not bound.");
                return;
            }

            Transform skinRow = panel.transform.Find("SkinRow");
            SerializedObject serialized = new SerializedObject(menuUi);
            serialized.FindProperty("settingsPanel").objectReferenceValue = panel;
            serialized.FindProperty("settingsPortInput").objectReferenceValue =
                panel.transform.Find("PortInput")?.GetComponent<TMP_InputField>();
            serialized.FindProperty("settingsNetworkDropdown").objectReferenceValue =
                panel.transform.Find("NetworkDropdown")?.GetComponent<TMP_Dropdown>();
            serialized.FindProperty("settingsTransportDropdown").objectReferenceValue =
                panel.transform.Find(TransportModeUi.DropdownObjectName)?.GetComponent<TMP_Dropdown>();
            serialized.FindProperty("settingsBackButton").objectReferenceValue =
                panel.transform.Find("BackButton")?.GetComponent<Button>();
            serialized.FindProperty("settingsSkinRandomToggle").objectReferenceValue =
                skinRow?.Find("SkinRandomToggle")?.GetComponent<Toggle>();
            serialized.FindProperty("settingsSkinToggle0").objectReferenceValue =
                skinRow?.Find("SkinToggle0")?.GetComponent<Toggle>();
            serialized.FindProperty("settingsSkinToggle1").objectReferenceValue =
                skinRow?.Find("SkinToggle1")?.GetComponent<Toggle>();
            serialized.FindProperty("settingsSkinToggle2").objectReferenceValue =
                skinRow?.Find("SkinToggle2")?.GetComponent<Toggle>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(menuUi);
        }

        static void SetAnchoredIfExists(Transform parent, string name, Vector2 anchoredPosition)
        {
            Transform existing = parent.Find(name);
            if (existing == null)
                return;

            RectTransform rect = existing.GetComponent<RectTransform>();
            if (rect != null)
                rect.anchoredPosition = anchoredPosition;
        }

        static void EnsureLabel(Transform parent, string name, string text, Vector2 anchoredPosition, float fontSize)
        {
            Transform existing = parent.Find(name);
            GameObject labelObject;
            if (existing != null)
            {
                labelObject = existing.gameObject;
            }
            else
            {
                labelObject = new GameObject(name, typeof(RectTransform));
                labelObject.transform.SetParent(parent, false);
            }

            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(280f, 40f);

            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            if (label == null)
                label = labelObject.AddComponent<TextMeshProUGUI>();

            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
        }

        static TMP_InputField EnsurePortInput(Transform parent, string name, Vector2 anchoredPosition)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                TMP_InputField existingInput = existing.GetComponent<TMP_InputField>();
                if (existingInput != null)
                {
                    RectTransform existingRect = existing.GetComponent<RectTransform>();
                    if (existingRect != null)
                        existingRect.anchoredPosition = anchoredPosition;
                    return existingInput;
                }
            }

            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            root.transform.SetParent(parent, false);

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(220f, 36f);

            Image image = root.GetComponent<Image>();
            image.color = new Color(0.18f, 0.18f, 0.18f, 0.95f);

            GameObject textArea = new GameObject("Text Area", typeof(RectTransform));
            textArea.transform.SetParent(root.transform, false);
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
            placeholder.raycastTarget = false;
            StretchFull(placeholder.rectTransform);

            GameObject textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(textArea.transform, false);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = 18f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            StretchFull(text.rectTransform);

            TMP_InputField input = root.GetComponent<TMP_InputField>();
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            input.textViewport = textAreaRect;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.targetGraphic = image;
            return input;
        }

        static Button EnsureButton(Transform parent, string name, string text, Vector2 anchoredPosition)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                Button existingButton = existing.GetComponent<Button>();
                if (existingButton != null)
                {
                    RectTransform existingRect = existing.GetComponent<RectTransform>();
                    if (existingRect != null)
                        existingRect.anchoredPosition = anchoredPosition;
                    return existingButton;
                }
            }

            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(220f, 44f);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 0.95f);

            GameObject labelObject = new GameObject("Text", typeof(RectTransform));
            labelObject.transform.SetParent(buttonObject.transform, false);
            StretchFull(labelObject.GetComponent<RectTransform>());
            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 24f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;

            return buttonObject.GetComponent<Button>();
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
