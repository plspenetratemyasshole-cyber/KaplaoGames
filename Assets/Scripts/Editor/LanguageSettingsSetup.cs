#if UNITY_EDITOR
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
    /// Adds language dropdown to SettingsPanel without repositioning other controls.
    /// </summary>
    public static class LanguageSettingsSetup
    {
        const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        const string PanelName = "SettingsPanel";

        [MenuItem("Kaplao/Setup Language Settings")]
        public static void SetupLanguageSettings()
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[Kaplao] Canvas not found in MainMenu.");
                return;
            }

            Transform panel = FindSettingsPanel(canvas.transform);
            if (panel == null)
            {
                Debug.LogError("[Kaplao] SettingsPanel not found. Run Kaplao/Setup Settings Panel first.");
                return;
            }

            EnsureLabel(panel, "LanguageLabel", "Language", new Vector2(-160f, -65f), 22f);
            TMP_Dropdown dropdown = EditorUiDropdownUtility.EnsureDropdown(
                panel, "LanguageDropdown", new Vector2(40f, -65f), new Vector2(220f, 36f),
                new[] { "English", "Russian" });

            MainMenuUI menuUi = canvas.GetComponent<MainMenuUI>() ??
                                Object.FindFirstObjectByType<MainMenuUI>();
            if (menuUi != null)
            {
                SerializedObject serialized = new SerializedObject(menuUi);
                serialized.FindProperty("settingsLanguageDropdown").objectReferenceValue = dropdown;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(menuUi);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Kaplao] Language settings setup complete.");
        }

        static Transform FindSettingsPanel(Transform canvas)
        {
            Transform mainPanel = canvas.Find("MainPanel");
            if (mainPanel != null && mainPanel.parent != null)
            {
                Transform panel = mainPanel.parent.Find(PanelName);
                if (panel != null) return panel;
            }

            for (int i = 0; i < canvas.childCount; i++)
            {
                Transform child = canvas.GetChild(i);
                Transform panel = child.Find(PanelName);
                if (panel != null) return panel;
            }

            return canvas.Find(PanelName);
        }

        static void EnsureLabel(Transform parent, string name, string text, Vector2 pos, float fontSize)
        {
            Transform existing = parent.Find(name);
            GameObject labelObject = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
            if (existing == null)
                labelObject.transform.SetParent(parent, false);

            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(280f, 40f);

            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>() ??
                                    labelObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
        }
    }
}
#endif
