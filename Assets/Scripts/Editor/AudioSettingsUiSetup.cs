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
    /// <summary>
    /// Creates KaplaoAudioManager + Settings audio controls (pack dropdown, music/SFX volumes).
    /// Menu: Kaplao / Setup Audio System
    /// Does not rewrite Setup Settings Panel.
    /// </summary>
    public static class AudioSettingsUiSetup
    {
        const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        const string PanelName = "SettingsPanel";
        const string AudioManagerName = "KaplaoAudioManager";

        [MenuItem("Kaplao/Setup Audio System")]
        public static void SetupAudioSystem()
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[Kaplao] Canvas not found in MainMenu.");
                return;
            }

            KaplaoAudioManager audioManager = EnsureAudioManager();
            Transform root = ResolveMenuRoot(canvas.transform);
            if (root == null)
            {
                Debug.LogError("[Kaplao] Menu root not found.");
                return;
            }

            Transform settings = root.Find(PanelName);
            if (settings == null)
            {
                Debug.LogError("[Kaplao] SettingsPanel not found. Run Kaplao/Setup Settings Panel first.");
                return;
            }

            EnsureLabel(settings, "MusicPackLabel", "Music", new Vector2(-160f, -285f), 20f);
            TMP_Dropdown packDropdown = EnsureDropdown(
                settings,
                "MusicPackDropdown",
                new Vector2(40f, -285f),
                new Vector2(280f, 36f),
                new[] { "Trick or Treat", "Maf" });

            EnsureLabel(settings, "MusicVolumeLabel", "Music Vol", new Vector2(-160f, -330f), 18f);
            Slider musicSlider = EnsureSlider(settings, "MusicVolumeSlider", new Vector2(40f, -330f), new Vector2(280f, 24f));

            EnsureLabel(settings, "SfxVolumeLabel", "SFX Vol", new Vector2(-160f, -370f), 18f);
            Slider sfxSlider = EnsureSlider(settings, "SfxVolumeSlider", new Vector2(40f, -370f), new Vector2(280f, 24f));

            Transform back = settings.Find("BackButton");
            if (back != null)
            {
                RectTransform backRect = back.GetComponent<RectTransform>();
                if (backRect != null)
                    backRect.anchoredPosition = new Vector2(0f, -420f);
            }

            MainMenuUI menuUi = canvas.GetComponent<MainMenuUI>();
            if (menuUi == null)
                menuUi = Object.FindFirstObjectByType<MainMenuUI>();

            if (menuUi != null)
            {
                SerializedObject serialized = new SerializedObject(menuUi);
                serialized.FindProperty("settingsMusicPackDropdown").objectReferenceValue = packDropdown;
                serialized.FindProperty("settingsMusicVolumeSlider").objectReferenceValue = musicSlider;
                serialized.FindProperty("settingsSfxVolumeSlider").objectReferenceValue = sfxSlider;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(menuUi);
            }
            else
            {
                Debug.LogWarning("[Kaplao] MainMenuUI not found — audio UI created but not fully bound.");
            }

            EditorUtility.SetDirty(audioManager);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(
                "[Kaplao] Audio system ready. Assign 4 AudioClips on KaplaoAudioManager in the Hierarchy, then save.");
        }

        static KaplaoAudioManager EnsureAudioManager()
        {
            KaplaoAudioManager existing = Object.FindFirstObjectByType<KaplaoAudioManager>();
            if (existing != null)
                return existing;

            GameObject go = new GameObject(AudioManagerName);
            KaplaoAudioManager manager = go.AddComponent<KaplaoAudioManager>();
            Undo.RegisterCreatedObjectUndo(go, "Create KaplaoAudioManager");
            return manager;
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

        static TextMeshProUGUI EnsureLabel(
            Transform parent,
            string name,
            string text,
            Vector2 anchoredPosition,
            float fontSize)
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
            rect.sizeDelta = new Vector2(200f, 36f);

            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            if (label == null)
                label = labelObject.AddComponent<TextMeshProUGUI>();

            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        static TMP_Dropdown EnsureDropdown(
            Transform parent,
            string name,
            Vector2 anchoredPosition,
            Vector2 size,
            string[] options)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                TMP_Dropdown existingDropdown = existing.GetComponent<TMP_Dropdown>();
                if (existingDropdown != null)
                {
                    RectTransform existingRect = existing.GetComponent<RectTransform>();
                    if (existingRect != null)
                        existingRect.anchoredPosition = anchoredPosition;
                    ConfigureDropdownOptions(existingDropdown, options);
                    return existingDropdown;
                }
            }

            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_Dropdown));
            root.transform.SetParent(parent, false);

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = root.GetComponent<Image>();
            image.color = new Color(0.18f, 0.18f, 0.18f, 0.95f);

            GameObject labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(root.transform, false);
            RectTransform labelRect = labelGo.GetComponent<RectTransform>();
            Stretch(labelRect, 10f, 10f, 4f, 4f);
            TextMeshProUGUI label = labelGo.AddComponent<TextMeshProUGUI>();
            label.fontSize = 18f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;

            GameObject template = CreateDropdownTemplate(root.transform);

            TMP_Dropdown dropdown = root.GetComponent<TMP_Dropdown>();
            dropdown.targetGraphic = image;
            dropdown.captionText = label;
            dropdown.template = template.GetComponent<RectTransform>();
            dropdown.itemText = template.transform.Find("Viewport/Content/Item/Item Label")
                ?.GetComponent<TextMeshProUGUI>();
            ConfigureDropdownOptions(dropdown, options);
            template.SetActive(false);
            return dropdown;
        }

        static GameObject CreateDropdownTemplate(Transform parent)
        {
            GameObject template = new GameObject(
                "Template",
                typeof(RectTransform),
                typeof(Image),
                typeof(ScrollRect));
            template.transform.SetParent(parent, false);
            RectTransform templateRect = template.GetComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = new Vector2(0f, 2f);
            templateRect.sizeDelta = new Vector2(0f, 150f);
            template.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.14f, 0.98f);

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(template.transform, false);
            Stretch(viewport.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            viewport.GetComponent<Image>().color = Color.white;
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 36f);

            GameObject item = new GameObject(
                "Item",
                typeof(RectTransform),
                typeof(Image),
                typeof(Toggle));
            item.transform.SetParent(content.transform, false);
            RectTransform itemRect = item.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0f, 0.5f);
            itemRect.anchorMax = new Vector2(1f, 0.5f);
            itemRect.sizeDelta = new Vector2(0f, 32f);
            item.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.24f, 1f);

            GameObject itemLabel = new GameObject("Item Label", typeof(RectTransform));
            itemLabel.transform.SetParent(item.transform, false);
            Stretch(itemLabel.GetComponent<RectTransform>(), 8f, 8f, 2f, 2f);
            TextMeshProUGUI itemText = itemLabel.AddComponent<TextMeshProUGUI>();
            itemText.fontSize = 16f;
            itemText.color = Color.white;
            itemText.alignment = TextAlignmentOptions.Center;

            Toggle toggle = item.GetComponent<Toggle>();
            toggle.targetGraphic = item.GetComponent<Image>();
            toggle.isOn = true;

            ScrollRect scroll = template.GetComponent<ScrollRect>();
            scroll.content = contentRect;
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.horizontal = false;
            scroll.vertical = true;
            return template;
        }

        static void ConfigureDropdownOptions(TMP_Dropdown dropdown, string[] options)
        {
            dropdown.ClearOptions();
            dropdown.AddOptions(new System.Collections.Generic.List<string>(options));
        }

        static Slider EnsureSlider(Transform parent, string name, Vector2 anchoredPosition, Vector2 size)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                Slider existingSlider = existing.GetComponent<Slider>();
                if (existingSlider != null)
                {
                    RectTransform existingRect = existing.GetComponent<RectTransform>();
                    if (existingRect != null)
                    {
                        existingRect.anchoredPosition = anchoredPosition;
                        existingRect.sizeDelta = size;
                    }

                    return existingSlider;
                }
            }

            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Slider));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(root.transform, false);
            Stretch(background.GetComponent<RectTransform>(), 0f, 0f, 8f, 8f);
            background.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.18f, 1f);

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            Stretch(fillArea.GetComponent<RectTransform>(), 5f, 5f, 10f, 10f);

            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            Stretch(fill.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            fill.GetComponent<Image>().color = new Color(0.35f, 0.65f, 0.95f, 1f);

            GameObject handleSlideArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleSlideArea.transform.SetParent(root.transform, false);
            Stretch(handleSlideArea.GetComponent<RectTransform>(), 10f, 10f, 0f, 0f);

            GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleSlideArea.transform, false);
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(18f, 18f);
            handle.GetComponent<Image>().color = Color.white;

            Slider slider = root.GetComponent<Slider>();
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handleRect;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.value = 0.6f;
            return slider;
        }

        static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
#endif
